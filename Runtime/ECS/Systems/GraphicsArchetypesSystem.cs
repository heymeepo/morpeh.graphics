﻿using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class GraphicsArchetypesSystem : ICleanupSystem
    {
        public World World { get; set; }

        private GraphicsArchetypesHandle archetypesHandle;
        private GraphicsArchetypesContext archetypes;
        private BatchRendererGroupContext brg;

        private IntHashMap<ArchetypeProperty> propertiesTypeIdCache;
        private ResizableArray<UnmanagedStash> propertiesStashes;

        private LongHashMap<GraphicsArchetype> graphicsArchetypes;
        private LongHashMap<Filter> graphicsArchetypesFilters;
        private FastList<int> usedGraphicsArchetypesIndices;

        private LongHashMap<GraphicsArchetype> newGraphicsArchetypes;
        private LongHashMap<Filter> newGraphicsArchetypesFilters;
        private int[] newArchetypeIncludeExcludeBuffer;

        private LongHashSet usedEcsArchetypes;
        private Filter allExistingGraphicsEntitiesFilter;

        private Stash<SharedGraphicsArchetypesContext> graphicsArchetypesStash;

        private int objectToWorldIndex;
        private int worldToObjectIndex;

        public void OnAwake()
        {
            allExistingGraphicsEntitiesFilter = World.Filter
                .With<LocalToWorld>()
                .With<MaterialMeshInfo>()
                .Build();
            
            brg = EcsHelpers.GetBatchRendererGroupContext(World);
            InitializeGraphicsArchetypes();
        }

        public void OnUpdate(float deltaTime)
        {
            AddNewGraphicsArchetypes();
            UpdateGraphicsArchetypesFilters();
            GatherNewGraphicsArchetypes();
            UpdateArchetypePropertiesStashes();
        }

        public void Dispose()
        {
            foreach (var idx in graphicsArchetypes)
            {
                ref var graphicsArchetype = ref graphicsArchetypes.GetValueRefByIndex(idx);
                graphicsArchetype.Dispose();
            }

            propertiesStashes.Dispose();
            archetypesHandle.Dispose();
        }

        private void InitializeGraphicsArchetypes()
        {
            propertiesTypeIdCache = new IntHashMap<ArchetypeProperty>();
            graphicsArchetypes = new LongHashMap<GraphicsArchetype>();
            graphicsArchetypesFilters = new LongHashMap<Filter>();
            usedGraphicsArchetypesIndices = new FastList<int>();
            newGraphicsArchetypes = new LongHashMap<GraphicsArchetype>();
            newGraphicsArchetypesFilters = new LongHashMap<Filter>();
            usedEcsArchetypes = new LongHashSet();

            var overrideComponentsTypes = ReflectionHelpers
                .GetAssemblies()
                .GetTypesWithAttribute<BatchMaterialPropertyAttribute>()
                .Where(x => typeof(IComponent).IsAssignableFrom(x) && x.IsValueType);

            foreach (var componentType in overrideComponentsTypes)
            {
                var typeId = MorpehInternalTools.GetTypeId(componentType);
                var typeHash = MorpehInternalTools.GetTypeHash(componentType);
                var attribute = componentType.GetAttribute<BatchMaterialPropertyAttribute>();
                var shaderId = Shader.PropertyToID(attribute.MaterialPropertyId);
                var size = (short)attribute.Format.GetSizeFormat();

                var property = new ArchetypeProperty()
                {
                    componentTypeId = typeId,
                    componentTypeHash = typeHash,
                    size = size
                };

                var propertyOverride = new MaterialPropertyOverride()
                {
                    shaderId = shaderId,
                    size = size
                };

                propertiesTypeIdCache.Add(typeId, property, out int propertyIndex);
                brg.AddPropertyOverride(propertyOverride, propertyIndex);
            }


            var basePropertiesArrayLength = propertiesTypeIdCache.data.Length;
            var totalPropertiesArrayLength = basePropertiesArrayLength + 2;

            //Add two properties, objectToWorld and worldToObject, at the end of the internal propertiesTypeIdCache array under special indices.
            //This won't break hashmap enumerator, and it will give us the ability to access them directly by indices inside the jobs without unnecessary checks.
            Array.Resize(ref propertiesTypeIdCache.data, totalPropertiesArrayLength);

            objectToWorldIndex = totalPropertiesArrayLength - 2;
            worldToObjectIndex = totalPropertiesArrayLength - 1;

            propertiesTypeIdCache.data[objectToWorldIndex] = new ArchetypeProperty()
            {
                componentTypeId = MorpehInternalTools.GetTypeId(typeof(LocalToWorld)),
                componentTypeHash = MorpehInternalTools.GetTypeHash(typeof(LocalToWorld)),
                size = SIZE_OF_MATRIX3X4
            };

            propertiesTypeIdCache.data[worldToObjectIndex] = new ArchetypeProperty()
            {
                size = SIZE_OF_MATRIX3X4
            };

            var objectToWorldOverride = new MaterialPropertyOverride()
            {
                shaderId = OBJECT_TO_WORLD_ID,
                size = SIZE_OF_MATRIX3X4
            };

            var worldToObjectOverride = new MaterialPropertyOverride()
            {
                shaderId = WORLD_TO_OBJECT_ID,
                size = SIZE_OF_MATRIX3X4
            };

            brg.AddPropertyOverride(objectToWorldOverride, objectToWorldIndex);
            brg.AddPropertyOverride(worldToObjectOverride, worldToObjectIndex);

            newArchetypeIncludeExcludeBuffer = new int[basePropertiesArrayLength];
            propertiesStashes = new ResizableArray<UnmanagedStash>(totalPropertiesArrayLength);

            archetypesHandle = new GraphicsArchetypesHandle()
            {
                propertiesTypeIdCache = propertiesTypeIdCache,
                propertiesStashes = propertiesStashes,
                graphicsArchetypes = graphicsArchetypes,
                usedGraphicsArchetypesIndices = usedGraphicsArchetypesIndices,
                propertiesCount = propertiesTypeIdCache.length + 2
            };

            archetypes = new GraphicsArchetypesContext(archetypesHandle);
            graphicsArchetypesStash = World.GetStash<SharedGraphicsArchetypesContext>();
            graphicsArchetypesStash.Set(World.CreateEntity(), new SharedGraphicsArchetypesContext() { graphicsArchetypes = archetypes });
        }

        private void UpdateGraphicsArchetypesFilters()
        {
            usedEcsArchetypes.Clear();
            usedGraphicsArchetypesIndices.Clear();

            foreach (var idx in graphicsArchetypes)
            {
                var filter = graphicsArchetypesFilters.GetValueByIndex(idx);

                if (filter.IsNotEmpty())
                {
                    ref var archetype = ref graphicsArchetypes.GetValueRefByIndex(idx);
                    archetype.entities = filter.AsNative();
                    usedGraphicsArchetypesIndices.Add(idx);

                    var info = FilterWorkaroundExtensions.GetInternalFilterInfo(filter);

                    for (int i = 0; i < info.archetypesLength; i++)
                    {
                        usedEcsArchetypes.Add(info.archetypes[i].GetArchetypeHash()); //TODO: use list of hashmaps instead of copying them
                    }
                }
            }
        }

        private void GatherNewGraphicsArchetypes()
        {
            var filterInfo = FilterWorkaroundExtensions.GetInternalFilterInfo(allExistingGraphicsEntitiesFilter);

            if (filterInfo.archetypesLength == usedEcsArchetypes.length)
            {
                return;
            }

            for (int i = 0; i < filterInfo.archetypesLength; i++)
            {
                var archetype = filterInfo.archetypes[i];
                var archetypeHash = archetype.GetArchetypeHash();

                if (usedEcsArchetypes.Has(archetypeHash) == false)
                {
                    var graphicsArchetypeHash = 0L;
                    var components = ArchetypeWorkaroundExtensions.GetArchetypeComponents(archetype);
                    var newComponentsCounter = 0;

                    foreach (var typeId in components)
                    {
                        if (propertiesTypeIdCache.TryGetValue(typeId, out var property, out var index))
                        {
                            newArchetypeIncludeExcludeBuffer[index] = 1;
                            graphicsArchetypeHash ^= property.componentTypeHash;
                            newComponentsCounter++;
                        }
                    }

                    if (newGraphicsArchetypes.Has(graphicsArchetypeHash) == false)
                    {
                        CreateGraphicsArchetype(graphicsArchetypeHash, newComponentsCounter, newArchetypeIncludeExcludeBuffer);
                    }

                    Array.Clear(newArchetypeIncludeExcludeBuffer, 0, newArchetypeIncludeExcludeBuffer.Length);
                }
            }
        }

        private void CreateGraphicsArchetype(long graphicsArchetypeHash, int propertiesCount, int[] includeBuffer)
        {
            propertiesCount += 2;
            var properties = new NativeArray<int>(propertiesCount, Allocator.Persistent);

            properties[0] = objectToWorldIndex;
            properties[1] = worldToObjectIndex;

            var counter = 2;
            var filterBuilder = World.Filter.With<LocalToWorld>().With<MaterialMeshInfo>();

            foreach (var idx in propertiesTypeIdCache)
            {
                ref var property = ref propertiesTypeIdCache.GetValueRefByIndex(idx);

                if (includeBuffer[idx] > 0)
                {
                    filterBuilder = filterBuilder.With(property.componentTypeId);
                    properties[counter++] = idx;
                }
                else
                {
                    filterBuilder = filterBuilder.Without(property.componentTypeId);
                }
            }

            int bytesPerEntity = 0;

            for (int i = 0; i < properties.Length; i++)
            {
                ref var size = ref propertiesTypeIdCache.GetValueRefByIndex(properties[i]).size;
                bytesPerEntity += size;
            }

            var maxEntitiesPerBatch = BYTES_PER_BATCH_RAW_BUFFER / bytesPerEntity;

            var overrideStream = new NativeArray<int>(propertiesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            overrideStream[0] = 0;

            for (int i = 1; i < propertiesCount; i++)
            {
                int sizeBytesComponent = Align16Bytes(propertiesTypeIdCache.GetValueByIndex(properties[i - 1]).size * maxEntitiesPerBatch);
                overrideStream[i] = overrideStream[i - 1] + sizeBytesComponent;
            }

            var graphicsArchetype = new GraphicsArchetype()
            {
                propertiesIndices = properties,
                sourceMetadataStream = overrideStream,
                batchesIndices = new NativeList<int>(4, Allocator.Persistent),
                maxEntitiesPerBatch = maxEntitiesPerBatch,
                hash = graphicsArchetypeHash,
            };

            newGraphicsArchetypes.Add(graphicsArchetypeHash, graphicsArchetype, out _);
            newGraphicsArchetypesFilters.Add(graphicsArchetypeHash, filterBuilder.Build(), out _);
        }

        private void AddNewGraphicsArchetypes()
        {
            if (newGraphicsArchetypes.length == 0)
            {
                return;
            }

            foreach (var idx in newGraphicsArchetypes)
            {
                var archetype = newGraphicsArchetypes.GetValueByIndex(idx);
                var filter = newGraphicsArchetypesFilters.GetValueByIndex(idx);

                graphicsArchetypes.Add(archetype.hash, archetype, out _);
                graphicsArchetypesFilters.Add(archetype.hash, filter, out _);
            }

            newGraphicsArchetypes.Clear();
            newGraphicsArchetypesFilters.Clear();
        }

        private void UpdateArchetypePropertiesStashes()
        {
            foreach (var idx in propertiesTypeIdCache)
            {
                ref var property = ref propertiesTypeIdCache.GetValueRefByIndex(idx);
                propertiesStashes[idx] = World.CreateUnmanagedStashDangerous(property.componentTypeId);
            }

            var objectToWorldTypeId = propertiesTypeIdCache.data[objectToWorldIndex].componentTypeId;
            propertiesStashes[objectToWorldIndex] = World.CreateUnmanagedStashDangerous(objectToWorldTypeId);
        }
    }
}
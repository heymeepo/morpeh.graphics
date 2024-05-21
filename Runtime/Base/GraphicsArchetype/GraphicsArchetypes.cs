using JetBrains.Annotations;
using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using static Scellecs.Morpeh.Graphics.Utilities.BRGHelpers;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class GraphicsArchetypes : IDisposable
    {
        private World world;

        private IntHashMap<ArchetypeProperty> propertiesTypeIdCache;
        private NativeArray<UnmanagedStash> propertiesStashes;

        private LongHashMap<GraphicsArchetype> graphicsArchetypes;
        private LongHashMap<Filter> graphicsArchetypesFilters;

        private FastList<int> usedGraphicsArchetypesIndices;
        private LongHashSet usedEcsArchetypes;

        private LongHashMap<GraphicsArchetype> newGraphicsArchetypes;
        private LongHashMap<Filter> newGraphicsArchetypesFilters;
        private int[] newArchetypeIncludeMappingBuffer;

        private Filter allExistingGraphicsEntitiesFilter;

        private int objectToWorldIndex;
        private int worldToObjectIndex;

        private int basePropertiesCount;
        private int totalPropertiesCount;

        public GraphicsArchetypes(World world)
        {
            this.world = world;
            propertiesTypeIdCache = new IntHashMap<ArchetypeProperty>(32);
            usedEcsArchetypes = new LongHashSet(128);
            graphicsArchetypes = new LongHashMap<GraphicsArchetype>(32);
            graphicsArchetypesFilters = new LongHashMap<Filter>(32);
            usedGraphicsArchetypesIndices = new FastList<int>(32);
            newGraphicsArchetypes = new LongHashMap<GraphicsArchetype>(16);
            newGraphicsArchetypesFilters = new LongHashMap<Filter>(16);
        }

        public void Initialize()
        {
            allExistingGraphicsEntitiesFilter = world.Filter
                .With<LocalToWorld>()
                .With<MaterialMeshInfo>()
                .Build();

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
                    shaderId = shaderId,
                    componentTypeId = typeId,
                    componentTypeHash = typeHash,
                    size = size
                };

                propertiesTypeIdCache.Add(typeId, property, out _);
            }

            basePropertiesCount = propertiesTypeIdCache.data.Length;
            totalPropertiesCount = basePropertiesCount + 2;

            //Add two properties, objectToWorld and worldToObject, at the end of the internal propertiesTypeIdCache array under special indices.
            //This won't break hashmap enumerator, and it will give us the ability to access them directly by indices inside the jobs without unnecessary checks.
            Array.Resize(ref propertiesTypeIdCache.data, totalPropertiesCount);

            objectToWorldIndex = totalPropertiesCount - 2;
            worldToObjectIndex = totalPropertiesCount - 1;

            propertiesTypeIdCache.data[objectToWorldIndex] = new ArchetypeProperty()
            {
                shaderId = OBJECT_TO_WORLD_ID,
                componentTypeId = MorpehInternalTools.GetTypeId(typeof(LocalToWorld)),
                componentTypeHash = MorpehInternalTools.GetTypeHash(typeof(LocalToWorld)),
                size = SIZE_OF_MATRIX3X4
            };

            propertiesTypeIdCache.data[worldToObjectIndex] = new ArchetypeProperty()
            {
                shaderId = WORLD_TO_OBJECT_ID,
                size = SIZE_OF_MATRIX3X4
            };

            newArchetypeIncludeMappingBuffer = new int[basePropertiesCount];
            propertiesStashes = new NativeArray<UnmanagedStash>(totalPropertiesCount, Allocator.Persistent);
            CreateGraphicsArchetype(0, 0, newArchetypeIncludeMappingBuffer);
        }

        public void Dispose()
        {
            foreach (var idx in graphicsArchetypes)
            {
                ref var graphicsArchetype = ref graphicsArchetypes.GetValueRefByIndex(idx);
                graphicsArchetype.Dispose();
            }

            propertiesStashes.Dispose(default);
        }

        public int GetArchetypePropertiesCount() => totalPropertiesCount;

        public NativeArray<UnmanagedStash> GetArchetypesPropertiesStashes() => propertiesStashes;

        public LongHashMap<GraphicsArchetype> GetGraphicsArchetypesMap() => graphicsArchetypes;

        public IntHashMap<ArchetypeProperty> GetArchetypesPropertiesMap() => propertiesTypeIdCache;

        public FastList<int> GetUsedArchetypesIndices() => usedGraphicsArchetypesIndices;

        public ref GraphicsArchetype GetGraphicsArchetypeByIndex(in int index) => ref graphicsArchetypes.GetValueRefByIndex(index);

        public ref ArchetypeProperty GetArchetypePropertyByIndex(in int index) => ref propertiesTypeIdCache.GetValueRefByIndex(index);

        public void Update()
        {
            AddNewGraphicsArchetypes();
            UpdateGraphicsArchetypesFilters();
            GatherNewGraphicsArchetypes();
            UpdateArchetypePropertiesStashes();
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
                            newArchetypeIncludeMappingBuffer[index] = 1;
                            graphicsArchetypeHash ^= property.componentTypeHash;
                            newComponentsCounter++;
                        }
                    }

                    if (newGraphicsArchetypes.Has(graphicsArchetypeHash) == false)
                    {
                        CreateGraphicsArchetype(graphicsArchetypeHash, newComponentsCounter, newArchetypeIncludeMappingBuffer);
                    }

                    Array.Clear(newArchetypeIncludeMappingBuffer, 0, newArchetypeIncludeMappingBuffer.Length);
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
            var filterBuilder = world.Filter.With<LocalToWorld>().With<MaterialMeshInfo>();

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
                propertiesStashes[idx] = world.CreateUnmanagedStashDangerous(property.componentTypeId);
            }

            var objectToWorldTypeId = propertiesTypeIdCache.data[objectToWorldIndex].componentTypeId;
            propertiesStashes[objectToWorldIndex] = world.CreateUnmanagedStashDangerous(objectToWorldTypeId);
        }
    }
}

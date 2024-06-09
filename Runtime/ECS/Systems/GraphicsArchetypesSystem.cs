using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpersNonBursted;

namespace Scellecs.Morpeh.Graphics
{
    /// <summary>
    /// Updates and prepares the rendering state of the current frame for further processing based on entities archetypes.
    /// Any structural changes, such as component additions/removals and entity creations/destroying, are not allowed after this system has executed.
    /// </summary>
    public sealed class GraphicsArchetypesSystem : ICleanupSystem
    {
        public World World { get; set; }

        private GraphicsArchetypesHandle archetypesHandle;
        private GraphicsArchetypesContext archetypes;

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
            allExistingGraphicsEntitiesFilter = World.Filter.Extend<GraphicsAspect>().Build();
            InitializeGraphicsArchetypes();
        }

        public void OnUpdate(float deltaTime)
        {
            UpdateGraphicsArchetypesFilters();
            GatherNewGraphicsArchetypes();
            AddNewGraphicsArchetypes();
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

            AddArchetypePropertiesReflection();
            AddArchetypeProperty(SPHERICAL_HARMONIC_COEFFICIENTS_ID, SIZE_OF_SHCOEFFICIENTS, typeof(BuiltinMaterialPropertyUnity_SHCoefficients));

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
                shaderId = OBJECT_TO_WORLD_ID,
                size = SIZE_OF_MATRIX3X4
            };

            propertiesTypeIdCache.data[worldToObjectIndex] = new ArchetypeProperty()
            {
                shaderId = WORLD_TO_OBJECT_ID,
                size = SIZE_OF_MATRIX3X4
            };

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

        private void AddArchetypePropertiesReflection()
        {
            var overrideComponentsTypes = ReflectionHelpers
                .GetAssemblies()
                .GetTypesWithAttribute<BatchMaterialPropertyAttribute>()
                .Where(x => typeof(IComponent).IsAssignableFrom(x) && x.IsValueType);

            foreach (var componentType in overrideComponentsTypes)
            {
                var attribute = componentType.GetAttribute<BatchMaterialPropertyAttribute>();
                var size = attribute.Format.GetSizeFormat();
                var shaderId = Shader.PropertyToID(attribute.MaterialPropertyId);

                AddArchetypeProperty(shaderId, size, componentType);
            }
        }

        private void AddArchetypeProperty(int shaderId, int size, Type componentType)
        {
            var typeId = MorpehInternalTools.GetTypeId(componentType);
            var typeHash = MorpehInternalTools.GetTypeHash(componentType);

            var property = new ArchetypeProperty()
            {
                componentTypeId = typeId,
                componentTypeHash = typeHash,
                shaderId = shaderId,
                size = size
            };

            propertiesTypeIdCache.Add(typeId, property, out _);
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
                        usedEcsArchetypes.Add(info.archetypes[i].GetArchetypeHash());
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
                    var propertiesCount = 0;

                    foreach (var typeId in components)
                    {
                        if (propertiesTypeIdCache.TryGetValue(typeId, out var property, out var idx))
                        {
                            newArchetypeIncludeExcludeBuffer[idx] = 1;
                            graphicsArchetypeHash ^= property.componentTypeHash;
                            propertiesCount++;
                        }
                    }

                    if (newGraphicsArchetypes.Has(graphicsArchetypeHash) == false)
                    {
                        CreateGraphicsArchetype(graphicsArchetypeHash, propertiesCount, newArchetypeIncludeExcludeBuffer);
                    }

                    Array.Clear(newArchetypeIncludeExcludeBuffer, 0, newArchetypeIncludeExcludeBuffer.Length);
                }
            }
        }

        private void CreateGraphicsArchetype(long graphicsArchetypeHash, int propertiesCount, int[] includeBuffer)
        {
            propertiesCount += 2;

            var properties = new NativeArray<int>(propertiesCount, Allocator.Persistent)
            {
                [0] = objectToWorldIndex,
                [1] = worldToObjectIndex
            };

            var counter = 2;
            var filterBuilder = World.Filter.Extend<GraphicsAspect>();

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

            var maxEntitiesPerBatch = BYTES_PER_BATCH / bytesPerEntity;

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
                archetype.entities = filter.AsNative();

                graphicsArchetypes.Add(archetype.hash, archetype, out int index);
                graphicsArchetypesFilters.Add(archetype.hash, filter, out _);

                usedGraphicsArchetypesIndices.Add(index);
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

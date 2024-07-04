using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpersNonBursted;

namespace Scellecs.Morpeh.Graphics
{
    /// <summary>
    /// Updates and prepares the rendering state of the current frame for further processing based on entities archetypes.
    /// Any structural changes, such as component additions/removals and entity creations/destroying, are not allowed after this system has executed.
    /// </summary>
    public sealed unsafe class GraphicsArchetypesSystem : ICleanupSystem
    {
        public World World { get; set; }

        private IntHashMap<ArchetypeProperty> propertiesTypeIdCache;

        private NativeArray<ArchetypeProperty> pinnedProperties;
        private NativeArray<UnmanagedStash> propertiesStashes;

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

        private ulong propertiesHandle;
        private ulong archetypesHandle;
        private ulong indicesHandle;

        public void OnAwake()
        {
            allExistingGraphicsEntitiesFilter = World.Filter.Extend<GraphicsAspect>().Build();
            InitializeGraphicsArchetypes();
        }

        public void OnUpdate(float deltaTime)
        {
            ReleaseContextHandles();

            UpdateExistingGraphicsArchetypes();
            GatherNewGraphicsArchetypes();
            AddNewGraphicsArchetypes();
            UpdateArchetypePropertiesStashes();

            UpdateContext();
        }

        public void Dispose()
        {
            foreach (var idx in graphicsArchetypes)
            {
                ref var graphicsArchetype = ref graphicsArchetypes.GetValueRefByIndex(idx);
                graphicsArchetype.Dispose();
            }

            if (propertiesStashes.IsCreated)
            {
                propertiesStashes.Dispose();
            }

            ReleaseContextHandles();

            UnsafeUtility.ReleaseGCObject(propertiesHandle);
            pinnedProperties = default;
        }

        private void ReleaseContextHandles()
        {
            UnsafeUtility.ReleaseGCObject(archetypesHandle);
            UnsafeUtility.ReleaseGCObject(indicesHandle);
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
            AddArchetypeProperty(LIGHTMAP_INDEX_ID, SIZE_OF_FLOAT4, typeof(BuiltinMaterialPropertyUnity_LightmapIndex));
            AddArchetypeProperty(LIGHTMAP_ST_ID, SIZE_OF_FLOAT4, typeof(BuiltinMaterialPropertyUnity_LightmapST));

            var basePropertiesArrayLength = propertiesTypeIdCache.data.Length;
            var totalPropertiesArrayLength = basePropertiesArrayLength + 2;

            //Add two properties, objectToWorld and worldToObject, at the end of the internal propertiesTypeIdCache array under special indices.
            //This won't break hashmap, because it's immutable.
            //It will give us the ability to access them directly by indices inside the jobs without unnecessary checks.

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

            propertiesStashes = new NativeArray<UnmanagedStash>(totalPropertiesArrayLength, Allocator.Persistent);
            pinnedProperties = UnsafeHelpers.PinGCArrayAndConvert<ArchetypeProperty>(propertiesTypeIdCache.data, totalPropertiesArrayLength, out propertiesHandle);

            graphicsArchetypesStash = World.GetStash<SharedGraphicsArchetypesContext>();
            graphicsArchetypesStash.Add(World.CreateEntity());

            UpdateContext();
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

        private void UpdateExistingGraphicsArchetypes()
        {
            usedEcsArchetypes.Clear();
            usedGraphicsArchetypesIndices.Clear();

            foreach (var idx in graphicsArchetypes)
            {
                var filter = graphicsArchetypesFilters.GetValueByIndex(idx);

                if (filter.IsNotEmpty())
                {
                    ref var archetype = ref graphicsArchetypes.GetValueRefByIndex(idx);
                    archetype.entities = filter.AsNative(); //TODO: Use own filter with pinned array of chunks instead NativeFastList
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
            var isLightMapped = false;

            foreach (var idx in propertiesTypeIdCache)
            {
                ref var property = ref propertiesTypeIdCache.GetValueRefByIndex(idx);
                var componentTypeId = property.componentTypeId;

                if (includeBuffer[idx] > 0)
                {
                    filterBuilder = filterBuilder.With(componentTypeId);
                    properties[counter++] = idx;

                    if (property.shaderId == LIGHTMAP_INDEX_ID)
                    {
                        isLightMapped = true;
                    }
                }
                else
                {
                    filterBuilder = filterBuilder.Without(componentTypeId);
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
                int sizeBytesComponent = Align16Bytes(propertiesTypeIdCache.GetValueRefByIndex(properties[i - 1]).size * maxEntitiesPerBatch);
                overrideStream[i] = overrideStream[i - 1] + sizeBytesComponent;
            }

            var graphicsArchetype = new GraphicsArchetype()
            {
                propertiesIndices = properties,
                sourceMetadataStream = overrideStream,
                batchesIndices = new NativeList<int>(4, Allocator.Persistent),
                maxEntitiesPerBatch = maxEntitiesPerBatch,
                isLightMapped = isLightMapped,
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

        private void UpdateContext()
        {
            ref var shared = ref GetSharedContext();
            var indicesArray = UnsafeHelpers.PinGCArrayAndConvert<int>(usedGraphicsArchetypesIndices.data, usedGraphicsArchetypesIndices.length, out indicesHandle);
            var graphicsArchetypesPtr = (GraphicsArchetype*)UnsafeUtility.PinGCArrayAndGetDataAddress(graphicsArchetypes.data, out archetypesHandle);

            shared.graphicsArchetypes = new GraphicsArchetypesContext(pinnedProperties, propertiesStashes, indicesArray, graphicsArchetypesPtr);
        }

        private ref SharedGraphicsArchetypesContext GetSharedContext()
        {
            var enumerator = graphicsArchetypesStash.GetEnumerator();
            var exists = enumerator.MoveNext();

            if (exists)
            {
                return ref enumerator.Current;
            }
            else
            {
                throw new NullReferenceException("SharedGraphicsArchetypesContext was not found during the execution of the GraphicsArchetypesSystem, the graphics state is corrupted.");
            }
        }
    }
}

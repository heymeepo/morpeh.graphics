using Scellecs.Morpeh.Native;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    internal struct GraphicsArchetype : IDisposable
    {
        /// <summary>
        /// Overriden shader properties indices that correspond to this GraphicsArchetype
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> propertiesIndices;

        /// <summary>
        /// List of batches indices that correspond to this GraphicsArchetype, including potentially outdated ones
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeList<int> batchesIndices;

        /// <summary>
        /// The source memory layout per ArchetypeProperty inside the GPU buffer
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> sourceMetadataStream;

        /// <summary>
        /// All entities that correspond to this GraphicsArchetype
        /// </summary>
        [ReadOnly]
        public NativeFilter entities;

        /// <summary>
        /// Are entities of this GraphicsArchetype have baked lightmaps
        /// </summary>
        [ReadOnly]
        public bool isLightMapped;

        /// <summary>
        /// The maximum number of entities per batch. Based on the amount of memory allocated on the GPU for each entity of this archetype.
        /// </summary>
        [ReadOnly]
        public int maxEntitiesPerBatch;

        /// <summary>
        /// The hash of this GraphicsArchetype. Based on the ArchetypeProperties hashes.
        /// </summary>
        [ReadOnly]
        public long hash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ExpectedBatchesCount() => (int)math.ceil((float)entities.length / maxEntitiesPerBatch);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ActualBatchesCount() => math.min(ExpectedBatchesCount(), batchesIndices.Length);

        public void Dispose()
        {
            if (propertiesIndices.IsCreated)
            {
                propertiesIndices.Dispose(default);
            }

            if (batchesIndices.IsCreated)
            {
                batchesIndices.Dispose(default);
            }

            if (sourceMetadataStream.IsCreated)
            {
                sourceMetadataStream.Dispose(default);
            }
        }
    }
}

using Scellecs.Morpeh.Native;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    internal struct GraphicsArchetype : IDisposable
    {
        /// <summary>
        /// List of overriden shader properties indices that correspond to this GraphicsArchetype
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
        /// The source memory layout of ArchetypeProperties inside the GPU buffer
        /// </summary>
        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> sourceMetadataStream;

        /// <summary>
        /// All entities that correspond to this GraphicsArchetype
        /// </summary>
        public NativeFilter entities;

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

        public int ExpectedBatchesCount() => (int)math.ceil((float)entities.length / maxEntitiesPerBatch);

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

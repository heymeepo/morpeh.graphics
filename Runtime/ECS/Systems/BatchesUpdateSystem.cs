using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Collections;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public sealed unsafe class BatchesUpdateSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;
        private GraphicsArchetypesContext graphicsArchetypes;

        private FastList<BatchCreateInfo> batchCreateInfos;
        private BitMap unreferencedBatchesIndices;

        public void OnAwake()
        {
            batchCreateInfos = new FastList<BatchCreateInfo>();
            unreferencedBatchesIndices = new BitMap();
        }

        public void OnUpdate(float deltaTime)
        {
            brg = BrgHelpersNonBursted.GetBatchRendererGroupContext(World);
            graphicsArchetypes = BrgHelpersNonBursted.GetGraphicsArchetypesContext(World);

            UpdateBatches();
        }

        public void Dispose() { }

        private void UpdateBatches()
        {
            unreferencedBatchesIndices.Clear();
            batchCreateInfos.Clear();

            var existingBatchesIndices = brg.ExistingBatchesIndices;
            var archetypesIndices = graphicsArchetypes.usedGraphicsArchetypesIndices;

            foreach (var batchIndex in existingBatchesIndices)
            {
                unreferencedBatchesIndices.Set(batchIndex);
            }

            for (int i = 0; i < archetypesIndices.Length; i++)
            {
                ref var graphicsArchetype = ref graphicsArchetypes.graphicsArchetypes[archetypesIndices[i]];

                var expectedBatchesCount = graphicsArchetype.ExpectedBatchesCount();
                var actualBatchesCount = graphicsArchetype.ActualBatchesCount();
                var diff = expectedBatchesCount - actualBatchesCount;

                if (diff > 0)
                {
                    var batchCreate = new BatchCreateInfo()
                    {
                        archetypeIndex = i,
                        batchesCount = diff
                    };

                    batchCreateInfos.Add(batchCreate);
                }

                if (actualBatchesCount > 0)
                {
                    for (int j = 0; j < actualBatchesCount; j++)
                    {
                        unreferencedBatchesIndices.Unset(graphicsArchetype.batchesIndices[j]);
                    }
                }
            }

            //foreach (var graphicsArchetypeIndex in archetypesIndices)
            //{
            //    ref var graphicsArchetype = ref graphicsArchetypes.graphicsArchetypes[graphicsArchetypeIndex];

            //    var expectedBatchesCount = graphicsArchetype.ExpectedBatchesCount();
            //    var actualBatchesCount = graphicsArchetype.ActualBatchesCount();
            //    var diff = expectedBatchesCount - actualBatchesCount;

            //    if (diff > 0)
            //    {
            //        var batchCreate = new BatchCreateInfo()
            //        {
            //            archetypeIndex = graphicsArchetypeIndex,
            //            batchesCount = diff
            //        };

            //        batchCreateInfos.Add(batchCreate);
            //    }

            //    if (actualBatchesCount > 0)
            //    {
            //        for (int i = 0; i < actualBatchesCount; i++)
            //        {
            //            unreferencedBatchesIndices.Unset(graphicsArchetype.batchesIndices[i]);
            //        }
            //    }
            //}

            RemoveUnreferencedBatches();
            AddBatches();
        }

        private void RemoveUnreferencedBatches()
        {
            foreach (var batchIndex in unreferencedBatchesIndices)
            {
                RemoveBatch(batchIndex);
            }
        }

        private unsafe void RemoveBatch(int batchIndex)
        {
            var brgBuffer = brg.Buffer;
            var batchInfos = brg.BatchesInfosPtr;
            var batchInfo = batchInfos[batchIndex];

            var archetypeIndex = batchInfo.archetypeIndex;
            ref var archetype = ref graphicsArchetypes.graphicsArchetypes[batchInfo.archetypeIndex];

            archetype.batchesIndices.RemoveAt(archetype.batchesIndices.Length - 1);
            brg.RemoveBatch(IntAsBatchID(batchIndex));

            if (batchInfo.batchGpuAllocation.Empty == false)
            {
                brgBuffer.Free(batchInfo.batchGpuAllocation);
            }
        }

        private void AddBatches()
        {
            for (int i = 0; i < batchCreateInfos.length; i++)
            {
                var info = batchCreateInfos.data[i];
                ref var archetype = ref graphicsArchetypes.graphicsArchetypes[info.archetypeIndex];

                for (int j = 0; j < info.batchesCount; j++)
                {
                    AddBatch(ref archetype, info.archetypeIndex);
                }
            }
        }

        private bool AddBatch(ref GraphicsArchetype archetype, int archetypeIndex)
        {
            if (brg.Buffer.Allocate(BYTES_PER_BATCH, BATCH_ALLOCATION_ALIGNMENT, out var batchGpuAllocation) == false)
            {
                return false;
            }

            var overrides = archetype.propertiesIndices;
            var overrideStream = archetype.sourceMetadataStream;
            var metadata = new NativeArray<MetadataValue>(archetype.propertiesIndices.Length, Allocator.Temp);
            var batchBegin = (int)batchGpuAllocation.begin;

            for (int i = 0; i < archetype.propertiesIndices.Length; i++)
            {
                int gpuAddress = batchBegin + overrideStream[i];
                var property = graphicsArchetypes.properties[overrides[i]];

                metadata[i] = new MetadataValue
                {
                    NameID = property.shaderId,
                    Value = (uint)gpuAddress | MSB
                };
            }

            var batchID = brg.AddBatch(metadata);
            var batchIndex = BatchIDAsInt(batchID);
            var batchInternalIndex = archetype.batchesIndices.Length;
            var batchInfo = new BatchInfo()
            {
                batchGpuAllocation = batchGpuAllocation,
                batchAABB = default,
                archetypeIndex = archetypeIndex,
                archetypeInternalIndex = batchInternalIndex
            };

            archetype.batchesIndices.Add(batchIndex);
            brg.AddBatchInfo(batchInfo, batchIndex);

            return true;
        }
    }
}

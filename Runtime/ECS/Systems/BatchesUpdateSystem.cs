using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Collections;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class BatchesUpdateSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;
        private GraphicsArchetypesContext graphicsArchetypes;
        private ResizableArray<int> batchIndexToArchetypeIndex;

        private BitMap unreferencedBatchesIndices;

        public void OnAwake()
        {
            brg = EcsHelpers.GetBatchRendererGroupContext(World);
            graphicsArchetypes = EcsHelpers.GetGraphicsArchetypesContext(World);
            batchIndexToArchetypeIndex = new ResizableArray<int>();
            unreferencedBatchesIndices = new BitMap();
        }

        public void OnUpdate(float deltaTime) => UpdateBatches();

        public void Dispose() => batchIndexToArchetypeIndex.Dispose();

        private void UpdateBatches()
        {
            unreferencedBatchesIndices.Clear();

            NativeList<BatchCreateInfo> batchCreateInfos = new NativeList<BatchCreateInfo>(Allocator.Temp);
            var existingBatchesIndices = brg.GetExistingBatchesIndices();
            var archetypesIndices = graphicsArchetypes.GetUsedGraphicsArchetypesIndices();

            foreach (var batchIndex in existingBatchesIndices)
            {
                unreferencedBatchesIndices.Set(batchIndex);
            }

            foreach (var graphicsArchetypeIndex in archetypesIndices)
            {
                ref var graphicsArchetype = ref graphicsArchetypes.GetGraphicsArchetypeByIndex(graphicsArchetypeIndex);

                var expectedBatchesCount = graphicsArchetype.ExpectedBatchesCount();
                var actualBatchesCount = graphicsArchetype.ActualBatchesCount();
                var diff = expectedBatchesCount - actualBatchesCount;

                if (diff > 0)
                {
                    var batchCreate = new BatchCreateInfo()
                    {
                        archetypeIndex = graphicsArchetypeIndex,
                        batchesCount = diff
                    };

                    batchCreateInfos.Add(batchCreate);
                }

                if (actualBatchesCount > 0)
                {
                    for (int i = 0; i < actualBatchesCount; i++)
                    {
                        unreferencedBatchesIndices.Unset(graphicsArchetype.batchesIndices[i]);
                    }
                }
            }

            ReleaseUnreferencedBatches(unreferencedBatchesIndices);
            AddBatches(batchCreateInfos);
        }

        private void ReleaseUnreferencedBatches(BitMap batchesIndices)
        {
            foreach (var batchIndex in batchesIndices)
            {
                RemoveBatch(batchIndex);
            }
        }

        private void RemoveBatch(int batchIndex)
        {
            var archetypeIndex = batchIndexToArchetypeIndex[batchIndex];
            ref var archetype = ref graphicsArchetypes.GetGraphicsArchetypeByIndex(archetypeIndex);
            archetype.batchesIndices.RemoveAt(archetype.batchesIndices.Length - 1);
            brg.RemoveBatch(IntAsBatchID(batchIndex));
        }

        private void AddBatches(NativeList<BatchCreateInfo> createInfos)
        {
            for (int i = 0; i < createInfos.Length; i++)
            {
                var info = createInfos[i];
                ref var archetype = ref graphicsArchetypes.GetGraphicsArchetypeByIndex(info.archetypeIndex);

                for (int j = 0; j < info.batchesCount; j++)
                {
                    AddBatch(ref archetype, info.archetypeIndex);
                }
            }
        }

        private bool AddBatch(ref GraphicsArchetype archetype, int archetypeIndex)
        {
            if (brg.AddBatch(archetype.propertiesIndices, archetype.sourceMetadataStream, out var batchID))
            {
                var batchIndex = batchID.AsInt();
                archetype.batchesIndices.Add(batchIndex);
                batchIndexToArchetypeIndex.AddAt(batchIndex, archetypeIndex);
                return true;
            }

            return false;
        }
    }
}

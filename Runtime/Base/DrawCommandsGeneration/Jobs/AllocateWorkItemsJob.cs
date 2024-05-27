using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Assertions;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct AllocateWorkItemsJob : IJob
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute()
        {
            int numBins = drawCommandOutput.UnsortedBins.Length;

            drawCommandOutput.BinIndices.Resize(numBins, NativeArrayOptions.UninitializedMemory);

            // Each thread can have one item per bin, but likely not all threads will.
            int workItemsUpperBound = BrgHelpers.MAX_JOB_WORKERS * numBins;
            drawCommandOutput.WorkItems.SetCapacity(workItemsUpperBound);
        }
    }
}

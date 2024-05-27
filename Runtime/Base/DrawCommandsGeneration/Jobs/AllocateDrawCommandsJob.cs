using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct AllocateDrawCommandsJob : IJob
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute()
        {
            int numBins = drawCommandOutput.SortedBins.Length;

            int drawCommandPrefixSum = 0;

            for (int i = 0; i < numBins; ++i)
            {
                var sortedBin = drawCommandOutput.SortedBins.ElementAt(i);
                ref var bin = ref drawCommandOutput.BinIndices.ElementAt(sortedBin);
                bin.DrawCommandOffset = drawCommandPrefixSum;

                // Bins with sorting positions will be expanded to one draw command
                // per instance, whereas other bins will be expanded to contain
                // many instances per command.
                int numDrawCommands = bin.NumDrawCommands;
                drawCommandPrefixSum += numDrawCommands;
            }

            var output = drawCommandOutput.CullingOutputDrawCommands;

            // Draw command count is exact at this point, we can set it up front
            int drawCommandCount = drawCommandPrefixSum;

            output->drawCommandCount = drawCommandCount;
            output->drawCommands = UnsafeHelpers.Malloc<BatchDrawCommand>(drawCommandCount);
            output->drawCommandPickingInstanceIDs = null;

            // Worst case is one range per draw command, so this is an upper bound estimate.
            // The real count could be less.
            output->drawRangeCount = 0;
            output->drawRanges = UnsafeHelpers.Malloc<BatchDrawRange>(drawCommandCount);
        }
    }
}

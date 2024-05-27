using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Jobs;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct AllocateInstancesJob : IJob
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute()
        {
            int numBins = drawCommandOutput.BinIndices.Length;

            int instancePrefixSum = 0;
            int sortingPositionPrefixSum = 0;

            for (int i = 0; i < numBins; ++i)
            {
                ref var bin = ref drawCommandOutput.BinIndices.ElementAt(i);
                bool hasSortingPosition = bin.HasSortingPosition;

                bin.InstanceOffset = instancePrefixSum;

                // Keep kNoSortingPosition in the PositionOffset if no sorting
                // positions, so draw command jobs can reliably check it to
                // to know whether there are positions without needing access to flags
                bin.PositionOffset = hasSortingPosition
                    ? sortingPositionPrefixSum
                    : DrawCommandBin.kNoSortingPosition;

                int numInstances = bin.NumInstances;
                int numPositions = hasSortingPosition ? numInstances : 0;

                instancePrefixSum += numInstances;
                sortingPositionPrefixSum += numPositions;
            }

            var output = drawCommandOutput.CullingOutputDrawCommands;
            output->visibleInstanceCount = instancePrefixSum;
            output->visibleInstances = UnsafeHelpers.Malloc<int>(instancePrefixSum);

            int numSortingPositionFloats = sortingPositionPrefixSum * 3;
            output->instanceSortingPositionFloatCount = numSortingPositionFloats;
            output->instanceSortingPositions = (sortingPositionPrefixSum == 0)
                ? null
                : UnsafeHelpers.Malloc<float>(numSortingPositionFloats);
        }
    }

}

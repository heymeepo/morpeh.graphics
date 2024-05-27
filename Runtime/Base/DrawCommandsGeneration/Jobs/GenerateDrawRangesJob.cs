using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct GenerateDrawRangesJob : IJob
    {
        public DrawCommandOutput drawCommandOutput;

        [NativeDisableUnsafePtrRestriction] 
        public BatchFilterSettings* filterSettings;

        private const int MaxInstances = BrgHelpers.MAX_INSTANCES_PER_DRAW_RANGE;
        private const int MaxCommands = BrgHelpers.MAX_DRAW_COMMANDS_PER_DRAW_RANGE;

        private int m_PrevFilterIndex;
        private int m_CommandsInRange;
        private int m_InstancesInRange;

        public void Execute()
        {
            int numBins = drawCommandOutput.SortedBins.Length;
            var output = drawCommandOutput.CullingOutputDrawCommands;

            ref int rangeCount = ref output->drawRangeCount;
            var ranges = output->drawRanges;

            rangeCount = 0;
            m_PrevFilterIndex = -1;
            m_CommandsInRange = 0;
            m_InstancesInRange = 0;

            for (int i = 0; i < numBins; ++i)
            {
                var sortedBin = drawCommandOutput.SortedBins.ElementAt(i);
                var settings = drawCommandOutput.UnsortedBins.ElementAt(sortedBin);
                var bin = drawCommandOutput.BinIndices.ElementAt(sortedBin);

                int numInstances = bin.NumInstances;
                int drawCommandOffset = bin.DrawCommandOffset;
                int numDrawCommands = bin.NumDrawCommands;
                int filterIndex = settings.FilterIndex;
                bool hasSortingPosition = settings.HasSortingPosition;

                for (int j = 0; j < numDrawCommands; ++j)
                {
                    int instancesInCommand = math.min(numInstances, DrawCommandBin.MaxInstancesPerCommand);

                    AccumulateDrawRange(
                        ref rangeCount,
                        ranges,
                        drawCommandOffset,
                        instancesInCommand,
                        filterIndex,
                        hasSortingPosition);

                    ++drawCommandOffset;
                    numInstances -= instancesInCommand;
                }
            }

            //Assert.IsTrue(rangeCount <= output->drawCommandCount);
        }

        private void AccumulateDrawRange(
            ref int rangeCount,
            BatchDrawRange* ranges,
            int drawCommandOffset,
            int numInstances,
            int filterIndex,
            bool hasSortingPosition)
        {
            bool isFirst = rangeCount == 0;

            bool addNewCommand;

            if (isFirst)
            {
                addNewCommand = true;
            }
            else
            {
                int newInstanceCount = m_InstancesInRange + numInstances;
                int newCommandCount = m_CommandsInRange + 1;

                bool sameFilter = filterIndex == m_PrevFilterIndex;
                bool tooManyInstances = newInstanceCount > MaxInstances;
                bool tooManyCommands = newCommandCount > MaxCommands;

                addNewCommand = !sameFilter || tooManyInstances || tooManyCommands;
            }

            if (addNewCommand)
            {
                ranges[rangeCount] = new BatchDrawRange
                {
                    filterSettings = filterSettings[filterIndex],
                    drawCommandsBegin = (uint)drawCommandOffset,
                    drawCommandsCount = 1,
                };

                ranges[rangeCount].filterSettings.allDepthSorted = hasSortingPosition;

                m_PrevFilterIndex = filterIndex;
                m_CommandsInRange = 1;
                m_InstancesInRange = numInstances;

                ++rangeCount;
            }
            else
            {
                ref var range = ref ranges[rangeCount - 1];

                ++range.drawCommandsCount;
                range.filterSettings.allDepthSorted &= hasSortingPosition;

                ++m_CommandsInRange;
                m_InstancesInRange += numInstances;
            }
        }
    }
}

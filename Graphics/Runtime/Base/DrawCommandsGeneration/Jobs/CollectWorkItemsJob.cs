using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct CollectWorkItemsJob : IJobParallelForDefer
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute(int index)
        {
            var settings = drawCommandOutput.UnsortedBins.ElementAt(index);
            bool hasSortingPosition = settings.HasSortingPosition;

            long* binPresentFilter = drawCommandOutput.BinPresentFilterForSettings(settings);

            int maxWorkItems = 0;
            for (int qwIndex = 0; qwIndex < DrawCommandOutput.NUM_THREADS_BITFIELD_LENGTH; ++qwIndex)
                maxWorkItems += math.countbits(binPresentFilter[qwIndex]);

            // Since we collect at most one item per thread, we will have N = thread count at most
            var workItems = drawCommandOutput.WorkItems.List->AsParallelWriter();
            var collectBuffer = drawCommandOutput.CollectBuffer;
            collectBuffer->EnsureCapacity(workItems, maxWorkItems);

            int numInstancesPrefixSum = 0;

            for (int qwIndex = 0; qwIndex < DrawCommandOutput.NUM_THREADS_BITFIELD_LENGTH; ++qwIndex)
            {
                // Load a filter bitfield which has a 1 bit for every thread index that might contain
                // draws for a given DrawCommandSettings. The filter is exact if there are no hash
                // collisions, but might contain false positives if hash collisions happened.
                ulong qword = (ulong)binPresentFilter[qwIndex];

                while (qword != 0)
                {
                    int bitIndex = math.tzcnt(qword);
                    ulong mask = 1ul << bitIndex;
                    qword ^= mask;

                    int i = (qwIndex << 6) + bitIndex;

                    var threadDraws = drawCommandOutput.ThreadLocalDrawCommands[i];

                    if (!threadDraws.DrawCommandStreamIndices.IsCreated)
                        continue;

                    if (threadDraws.DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
                    {
                        var stream = threadDraws.DrawCommands[streamIndex].Stream;

                        if (hasSortingPosition)
                        {
                            var transformStream = threadDraws.DrawCommands[streamIndex].TransformsStream;
                            collectBuffer->Add(new DrawCommandWorkItem
                            {
                                Arrays = stream.Head,
                                TransformArrays = transformStream.Head,
                                BinIndex = index,
                                PrefixSumNumInstances = numInstancesPrefixSum,
                            });
                        }
                        else
                        {
                            collectBuffer->Add(new DrawCommandWorkItem
                            {
                                Arrays = stream.Head,
                                TransformArrays = null,
                                BinIndex = index,
                                PrefixSumNumInstances = numInstancesPrefixSum,
                            });
                        }

                        numInstancesPrefixSum += stream.TotalInstanceCount;
                    }
                }
            }

            drawCommandOutput.BinIndices.ElementAt(index) = new DrawCommandBin
            {
                NumInstances = numInstancesPrefixSum,
                InstanceOffset = 0,
                PositionOffset = hasSortingPosition ? 0 : DrawCommandBin.kNoSortingPosition,
            };
        }
    }
}

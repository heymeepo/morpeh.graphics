using Scellecs.Morpeh.Transforms;
using System;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct ExpandVisibleInstancesJob : IJobParallelForDefer
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute(int index)
        {
            var workItem = drawCommandOutput.WorkItems.ElementAt(index);
            var header = workItem.Arrays;
            var transformHeader = workItem.TransformArrays;
            int binIndex = workItem.BinIndex;

            var bin = drawCommandOutput.BinIndices.ElementAt(binIndex);
            int binInstanceOffset = bin.InstanceOffset;
            int binPositionOffset = bin.PositionOffset;
            int workItemInstanceOffset = workItem.PrefixSumNumInstances;
            int headerInstanceOffset = 0;

            int* visibleInstances = drawCommandOutput.CullingOutputDrawCommands->visibleInstances;
            float3* sortingPositions = (float3*)drawCommandOutput.CullingOutputDrawCommands->instanceSortingPositions;

            if (transformHeader == null)
            {
                while (header != null)
                {
                    ExpandArray(
                        visibleInstances,
                        header,
                        binInstanceOffset + workItemInstanceOffset + headerInstanceOffset);

                    headerInstanceOffset += header->NumInstances;
                    header = header->Next;
                }
            }
            else
            {
                while (header != null)
                {
                    Assert.IsTrue(transformHeader != null);

                    int instanceOffset = binInstanceOffset + workItemInstanceOffset + headerInstanceOffset;
                    int positionOffset = binPositionOffset + workItemInstanceOffset + headerInstanceOffset;

                    ExpandArrayWithPositions(
                        visibleInstances,
                        sortingPositions,
                        header,
                        transformHeader,
                        instanceOffset,
                        positionOffset);

                    headerInstanceOffset += header->NumInstances;
                    header = header->Next;
                    transformHeader = transformHeader->Next;
                }
            }
        }

        private int ExpandArray(
            int* visibleInstances,
            DrawStream<DrawCommandVisibility>.Header* header,
            int instanceOffset)
        {
            int numStructs = header->NumElements;

            for (int i = 0; i < numStructs; ++i)
            {
                var visibility = *header->Element(i);
                int numInstances = ExpandVisibility(visibleInstances + instanceOffset, visibility);
                Assert.IsTrue(numInstances > 0);
                instanceOffset += numInstances;
            }

            return instanceOffset;
        }

        private int ExpandArrayWithPositions(
            int* visibleInstances,
            float3* sortingPositions,
            DrawStream<DrawCommandVisibility>.Header* header,
            DrawStream<IntPtr>.Header* transformHeader,
            int instanceOffset,
            int positionOffset)
        {
            int numStructs = header->NumElements;

            for (int i = 0; i < numStructs; ++i)
            {
                var visibility = *header->Element(i);
                var transforms = (float4x4*)(*transformHeader->Element(i));
                int numInstances = ExpandVisibilityWithPositions(
                    visibleInstances + instanceOffset,
                    sortingPositions + positionOffset,
                    visibility,
                    transforms);
                Assert.IsTrue(numInstances > 0);
                instanceOffset += numInstances;
                positionOffset += numInstances;
            }

            return instanceOffset;
        }


        private int ExpandVisibility(int* outputInstances, DrawCommandVisibility visibility)
        {
            int numInstances = 0;
            int startIndex = visibility.ChunkStartIndex;

            for (int i = 0; i < 2; ++i)
            {
                ulong qword = visibility.VisibleInstances[i];
                while (qword != 0)
                {
                    int bitIndex = math.tzcnt(qword);
                    ulong mask = 1ul << bitIndex;
                    qword ^= mask;
                    int instanceIndex = (i << 6) + bitIndex;
                    int visibilityIndex = startIndex + instanceIndex;
                    outputInstances[numInstances] = visibilityIndex;
                    ++numInstances;
                }
            }

            return numInstances;
        }

        private int ExpandVisibilityWithPositions(
            int* outputInstances,
            float3* outputSortingPosition,
            DrawCommandVisibility visibility,
            float4x4* transforms)
        {
            int numInstances = 0;
            int startIndex = visibility.ChunkStartIndex;

            for (int i = 0; i < 2; ++i)
            {
                ulong qword = visibility.VisibleInstances[i];
                while (qword != 0)
                {
                    int bitIndex = math.tzcnt(qword);
                    ulong mask = 1ul << bitIndex;
                    qword ^= mask;
                    int instanceIndex = (i << 6) + bitIndex;

                    var instanceTransform = new LocalToWorld
                    {
                        value = transforms[instanceIndex],
                    };

                    int visibilityIndex = startIndex + instanceIndex;
                    outputInstances[numInstances] = visibilityIndex;
                    outputSortingPosition[numInstances] = instanceTransform.Position;

                    ++numInstances;
                }
            }

            return numInstances;
        }
    }

}

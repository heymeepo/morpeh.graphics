using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct GenerateDrawCommandsJob : IJobParallelForDefer
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute(int index)
        {
            var sortedBin = drawCommandOutput.SortedBins.ElementAt(index);
            var settings = drawCommandOutput.UnsortedBins.ElementAt(sortedBin);
            var bin = drawCommandOutput.BinIndices.ElementAt(sortedBin);

            bool hasSortingPosition = settings.HasSortingPosition;
            uint maxPerCommand = hasSortingPosition ? 1u : BrgHelpers.MAX_INSTANCES_PER_DRAW_COMMAND;
            uint numInstances = (uint)bin.NumInstances;
            int numDrawCommands = bin.NumDrawCommands;

            uint drawInstanceOffset = (uint)bin.InstanceOffset;
            uint drawPositionFloatOffset = (uint)bin.PositionOffset * 3; // 3 floats per position

            var cullingOutput = drawCommandOutput.CullingOutputDrawCommands;
            var draws = cullingOutput->drawCommands;

            for (int i = 0; i < numDrawCommands; ++i)
            {
                var draw = new BatchDrawCommand
                {
                    visibleOffset = drawInstanceOffset,
                    visibleCount = math.min(maxPerCommand, numInstances),
                    batchID = settings.BatchID,
                    materialID = settings.MaterialID,
                    meshID = settings.MeshID,
                    submeshIndex = (ushort)settings.SubMeshIndex,
                    splitVisibilityMask = settings.SplitMask,
                    flags = settings.Flags,
                    sortingPosition = hasSortingPosition ? (int)drawPositionFloatOffset : 0,
                };

                int drawCommandIndex = bin.DrawCommandOffset + i;
                draws[drawCommandIndex] = draw;

                drawInstanceOffset += draw.visibleCount;
                drawPositionFloatOffset += draw.visibleCount * 3;
                numInstances -= draw.visibleCount;
            }
        }
    }

}

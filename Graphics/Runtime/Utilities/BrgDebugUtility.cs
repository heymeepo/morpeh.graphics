using System.Text;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    public static unsafe class BrgDebugUtility
    {
        private static void DebugDrawCommands(JobHandle drawCommandsDependency, BatchCullingOutput cullingOutput)
        {
            drawCommandsDependency.Complete();

            var drawCommands = cullingOutput.drawCommands[0];

            Debug.Log($"Draw Command summary: visibleInstanceCount: {drawCommands.visibleInstanceCount} drawCommandCount: {drawCommands.drawCommandCount} drawRangeCount: {drawCommands.drawRangeCount}");

#if DEBUG_LOG_DRAW_COMMANDS_VERBOSE
            bool verbose = true;
#else
            bool verbose = false;
#endif
            if (verbose)
            {
                for (int i = 0; i < drawCommands.drawCommandCount; ++i)
                {
                    var cmd = drawCommands.drawCommands[i];
                    DrawCommandSettings settings = new DrawCommandSettings
                    {
                        BatchID = cmd.batchID,
                        MaterialID = cmd.materialID,
                        MeshID = cmd.meshID,
                        SubMeshIndex = cmd.submeshIndex,
                        Flags = cmd.flags,
                    };
                    Debug.Log($"Draw Command #{i}: {settings} visibleOffset: {cmd.visibleOffset} visibleCount: {cmd.visibleCount}");
                    StringBuilder sb = new StringBuilder((int)cmd.visibleCount * 30);
                    bool hasSortingPosition = settings.HasSortingPosition;
                    for (int j = 0; j < cmd.visibleCount; ++j)
                    {
                        sb.Append(drawCommands.visibleInstances[cmd.visibleOffset + j]);
                        if (hasSortingPosition)
                            sb.AppendFormat(" ({0:F3} {1:F3} {2:F3})",
                                drawCommands.instanceSortingPositions[cmd.sortingPosition + 0],
                                drawCommands.instanceSortingPositions[cmd.sortingPosition + 1],
                                drawCommands.instanceSortingPositions[cmd.sortingPosition + 2]);
                        sb.Append(", ");
                    }
                    Debug.Log($"Draw Command #{i} instances: [{sb}]");
                }
            }
        }
    }
}

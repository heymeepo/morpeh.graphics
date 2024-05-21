using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    internal static class SparseBufferUtility
    {
        public static GraphicsBuffer CreateGraphicsBuffer(ref SparseBufferArgs args, ulong bufferSize)
        {
            return new GraphicsBuffer(args.target, args.flags, (int)bufferSize / args.stride, args.stride);
        }

        [BurstCompile]
        public static SparseBufferUploadRequirements ComputeUploadSizeRequirements(int numGpuUploadOperations, NativeArray<GpuUploadOperation> gpuUploadOperations, NativeArray<ValueBlitDescriptor> valueBlits)
        {
            var numOperations = numGpuUploadOperations + valueBlits.Length;
            var totalUploadBytes = 0;
            var biggestUploadBytes = 0;

            for (int i = 0; i < numGpuUploadOperations; ++i)
            {
                var numBytes = gpuUploadOperations[i].BytesRequiredInUploadBuffer;
                totalUploadBytes += numBytes;
                biggestUploadBytes = math.max(biggestUploadBytes, numBytes);
            }

            for (int i = 0; i < valueBlits.Length; ++i)
            {
                var numBytes = valueBlits[i].BytesRequiredInUploadBuffer;
                totalUploadBytes += numBytes;
                biggestUploadBytes = math.max(biggestUploadBytes, numBytes);
            }

            return new SparseBufferUploadRequirements()
            {
                numOperations = numOperations,
                totalUploadBytes = totalUploadBytes,
                biggestUploadBytes = biggestUploadBytes
            };
        }
    }
}

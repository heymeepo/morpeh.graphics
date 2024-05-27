using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal struct SparseBufferUploadRequirements
    {
        public int numOperations;
        public int totalUploadBytes;
        public int biggestUploadBytes;

        public static SparseBufferUploadRequirements ComputeUploadSizeRequirements(int numGpuUploadOperations, NativeArray<GpuUploadOperation> gpuUploadOperations)
        {
            var totalUploadBytes = 0;
            var biggestUploadBytes = 0;

            for (int i = 0; i < numGpuUploadOperations; ++i)
            {
                var numBytes = gpuUploadOperations[i].BytesRequiredInUploadBuffer;
                totalUploadBytes += numBytes;
                biggestUploadBytes = math.max(biggestUploadBytes, numBytes);
            }

            return new SparseBufferUploadRequirements()
            {
                numOperations = numGpuUploadOperations,
                totalUploadBytes = totalUploadBytes,
                biggestUploadBytes = biggestUploadBytes
            };
        }

        public static SparseBufferUploadRequirements ComputeUploadSizeRequirements(ValueBlitDescriptor blit)
        {
            return new SparseBufferUploadRequirements()
            {
                numOperations = 1,
                totalUploadBytes = blit.BytesRequiredInUploadBuffer,
                biggestUploadBytes = blit.BytesRequiredInUploadBuffer
            };
        }

        public static SparseBufferUploadRequirements operator +(SparseBufferUploadRequirements a, SparseBufferUploadRequirements b)
        {
            return new SparseBufferUploadRequirements
            {
                numOperations = a.numOperations + b.numOperations,
                totalUploadBytes = a.totalUploadBytes + b.totalUploadBytes,
                biggestUploadBytes = math.max(a.biggestUploadBytes, b.biggestUploadBytes)
            };
        }
    }
}

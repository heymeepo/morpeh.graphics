using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Burst.Intrinsics;
using Unity.Burst.CompilerServices;
using Unity.Burst;
using System;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    [BurstCompile]
    internal readonly struct BrgHelpers
    {
        public const int SIZE_OF_UINT = sizeof(uint);
        public const int SIZE_OF_FLOAT = sizeof(float);
        public const int SIZE_OF_FLOAT2 = SIZE_OF_FLOAT * 2;
        public const int SIZE_OF_FLOAT3 = SIZE_OF_FLOAT * 3;
        public const int SIZE_OF_FLOAT4 = SIZE_OF_FLOAT * 4;
        public const int SIZE_OF_MATRIX2X4 = SIZE_OF_FLOAT4 * 2;
        public const int SIZE_OF_MATRIX3X4 = SIZE_OF_FLOAT4 * 3;
        public const int SIZE_OF_MATRIX4X4 = SIZE_OF_FLOAT4 * 4;

        public const long GPU_BUFFER_INITIAL_SIZE = 32 * 1024 * 1024;
        public const long GPU_BUFFER_MAX_SIZE = 1023 * 1024 * 1024;

        public const int GPU_UPLOADER_CHUNK_SIZE = 4 * 1024 * 1024;
        public const int BYTES_PER_BATCH = 256 * 1024;

        public const int MAX_INSTANCES_PER_DRAW_COMMAND = 4096;
        public const int MAX_INSTANCES_PER_DRAW_RANGE = 4096;
        public const int MAX_DRAW_COMMANDS_PER_DRAW_RANGE = 512;

        public const int MAX_INSTANCES_PER_BATCH = BYTES_PER_BATCH / (SIZE_OF_MATRIX3X4 * 2);
        public const int MAX_BATCHES_COUNT = (int)GPU_BUFFER_MAX_SIZE / BYTES_PER_BATCH;

        public const uint BATCH_ALLOCATION_ALIGNMENT = 16;
        public const uint MSB = 0x80000000;

        public static readonly int MAX_JOB_WORKERS = JobsUtility.ThreadIndexCount;

        public static BatchID IntAsBatchID(int id) => new BatchID() { value = (uint)id };

        public static int BatchIDAsInt(BatchID batchID) => (int)batchID.value;

        public static int Align16Bytes(int size) => ((size + 15) >> 4) << 4;

        public static v128 ComputeBitmask(int entityCount) => ShiftRight(new v128(ulong.MaxValue), 128 - entityCount);

        public static v128 ShiftRight(in v128 v, int n)
        {
            if (Hint.Unlikely(n >= 128))
                return default;
            if (Hint.Unlikely(n == 0))
                return v;
            if (n >= 64)
                return new v128(v.ULong1 >> (n - 64), 0);
            // 0 < n < 64
            ulong lowToLow = v.ULong0 >> n;
            ulong highToLow = v.ULong1 << (64 - n);
            return new v128(lowToLow | highToLow, v.ULong1 >> n);
        }
    }

    internal static class BrgHelpersNonBursted
    {
        public static readonly int OBJECT_TO_WORLD_ID = Shader.PropertyToID("unity_ObjectToWorld");
        public static readonly int WORLD_TO_OBJECT_ID = Shader.PropertyToID("unity_WorldToObject");

        public static BatchRendererGroupContext GetBatchRendererGroupContext(World world)
        {
            var brgStash = world.GetStash<SharedBatchRendererGroupContext>();
            var enumerator = brgStash.GetEnumerator();

            if (enumerator.MoveNext())
            {
                return enumerator.Current.brg;
            }
            else
            {
                throw new NotImplementedException("BatchRendererGroupContext not found, most likely you have not added BatchRendererInitializer to the world.");
            }
        }

        public static GraphicsArchetypesContext GetGraphicsArchetypesContext(World world)
        {
            var brgStash = world.GetStash<SharedGraphicsArchetypesContext>();
            var enumerator = brgStash.GetEnumerator();

            if (enumerator.MoveNext())
            {
                return enumerator.Current.graphicsArchetypes;
            }
            else
            {
                throw new NotImplementedException("GraphicsArchetypesContext not found, most likely you have not added GraphicsArchetypesSystem to the world, or you have setup the systems in the wrong order.");
            }
        }
    }
}

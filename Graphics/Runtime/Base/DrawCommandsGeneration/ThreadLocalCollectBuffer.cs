using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Assertions;
using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct ThreadLocalCollectBuffer
    {
        public const Allocator kAllocator = Allocator.TempJob;
        public static readonly int kCollectBufferSize = BrgHelpers.MAX_JOB_WORKERS;

        public UnsafeList<DrawCommandWorkItem> WorkItems;
        private fixed int m_CacheLinePadding[12]; // The padding here assumes some internal sizes

        public void EnsureCapacity(UnsafeList<DrawCommandWorkItem>.ParallelWriter dst, int count)
        {
            Assert.IsTrue(sizeof(ThreadLocalCollectBuffer) >= JobsUtility.CacheLineSize);
            Assert.IsTrue(count <= kCollectBufferSize);

            if (!WorkItems.IsCreated)
                WorkItems = new UnsafeList<DrawCommandWorkItem>(
                    kCollectBufferSize,
                    kAllocator,
                    NativeArrayOptions.UninitializedMemory);

            if (WorkItems.Length + count > WorkItems.Capacity)
                Flush(dst);
        }

        public void Flush(UnsafeList<DrawCommandWorkItem>.ParallelWriter dst)
        {
            dst.AddRangeNoResize(WorkItems.Ptr, WorkItems.Length);
            WorkItems.Clear();
        }

        public void Add(DrawCommandWorkItem workItem) => WorkItems.Add(workItem);

        public void Dispose()
        {
            if (WorkItems.IsCreated)
                WorkItems.Dispose();
        }
    }
}

using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Unity.Mathematics;
using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct ThreadLocalAllocator
    {
        public const int kInitialSize = 1024 * 1024;
        public const Allocator kAllocator = Allocator.Persistent;
        public static readonly int NumThreads = BRGHelpers.MAX_JOB_WORKERS;

        [StructLayout(LayoutKind.Explicit, Size = JobsUtility.CacheLineSize)]
        public unsafe struct PaddedAllocator
        {
            [FieldOffset(0)]
            public AllocatorHelper<RewindableAllocator> Allocator;
            [FieldOffset(16)]
            public bool UsedSinceRewind;

            public void Initialize(int initialSize)
            {
                Allocator = new AllocatorHelper<RewindableAllocator>(AllocatorManager.Persistent);
                Allocator.Allocator.Initialize(initialSize);
            }
        }

        public UnsafeList<PaddedAllocator> Allocators;

        public ThreadLocalAllocator(int expectedUsedCount = -1, int initialSize = kInitialSize)
        {
            // Note, the comparison is <= as on 32-bit builds this size will be smaller, which is fine.
            Assert.IsTrue(sizeof(AllocatorHelper<RewindableAllocator>) <= 16, $"PaddedAllocator's Allocator size has changed. The type layout needs adjusting.");
            Assert.IsTrue(sizeof(PaddedAllocator) >= JobsUtility.CacheLineSize,
                $"Thread local allocators should be on different cache lines. Size: {sizeof(PaddedAllocator)}, Cache Line: {JobsUtility.CacheLineSize}");

            if (expectedUsedCount < 0)
                expectedUsedCount = math.max(0, JobsUtility.JobWorkerCount + 1);

            Allocators = new UnsafeList<PaddedAllocator>(
                NumThreads,
                kAllocator,
                NativeArrayOptions.ClearMemory);
            Allocators.Resize(NumThreads);

            for (int i = 0; i < NumThreads; ++i)
            {
                if (i < expectedUsedCount)
                    Allocators.ElementAt(i).Initialize(initialSize);
                else
                    Allocators.ElementAt(i).Initialize(1);
            }
        }

        public void Rewind()
        {
            Profiler.BeginSample("RewindAllocators");
            for (int i = 0; i < NumThreads; ++i)
            {
                ref var allocator = ref Allocators.ElementAt(i);
                if (allocator.UsedSinceRewind)
                {
                    Profiler.BeginSample("Rewind");
                    Allocators.ElementAt(i).Allocator.Allocator.Rewind();
                    Profiler.EndSample();
                }
                allocator.UsedSinceRewind = false;
            }
            Profiler.EndSample();

        }

        public void Dispose()
        {
            for (int i = 0; i < NumThreads; ++i)
            {
                Allocators.ElementAt(i).Allocator.Allocator.Dispose();
                Allocators.ElementAt(i).Allocator.Dispose();
            }

            Allocators.Dispose();
        }

        public RewindableAllocator* ThreadAllocator(int threadIndex)
        {
            ref var allocator = ref Allocators.ElementAt(threadIndex);
            allocator.UsedSinceRewind = true;
            return (RewindableAllocator*)UnsafeUtility.AddressOf(ref allocator.Allocator.Allocator);
        }

        public RewindableAllocator* GeneralAllocator => ThreadAllocator(Allocators.Length - 1);
    }
}

using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    [NoAlias]
    internal unsafe struct DrawCommandOutput
    {
        public const Allocator kAllocator = Allocator.TempJob;

        public static readonly int NUM_THREADS_BITFIELD_LENGTH = (MAX_JOB_WORKERS + 63) / 64;
        public const int NUM_RELEASE_THREADS = 4;
        public const int BIN_PRESENT_FILTER_SIZE = 1 << 10;

        [NativeSetThreadIndex] 
        public int ThreadIndex;

        public UnsafeList<ThreadLocalDrawCommands> ThreadLocalDrawCommands;
        public UnsafeList<ThreadLocalCollectBuffer> ThreadLocalCollectBuffers;

        public UnsafeList<long> BinPresentFilter;

        public DrawBinCollector BinCollector;
        public IndirectList<DrawCommandSettings> UnsortedBins => BinCollector.Bins;

        [NativeDisableUnsafePtrRestriction]
        public IndirectList<int> SortedBins;

        [NativeDisableUnsafePtrRestriction]
        public IndirectList<DrawCommandBin> BinIndices;

        [NativeDisableUnsafePtrRestriction]
        public IndirectList<DrawCommandWorkItem> WorkItems;

        [NativeDisableParallelForRestriction]
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchCullingOutputDrawCommands> CullingOutput;

        public int BinCapacity;

        public ThreadLocalAllocator ThreadLocalAllocator;

        //public ProfilerMarker ProfilerEmit;

        public DrawCommandOutput(
            int initialBinCapacity,
            ThreadLocalAllocator tlAllocator,
            BatchCullingOutput cullingOutput)
        {
            BinCapacity = initialBinCapacity;
            CullingOutput = cullingOutput.drawCommands;

            ThreadLocalAllocator = tlAllocator;
            var generalAllocator = ThreadLocalAllocator.GeneralAllocator;

            ThreadLocalDrawCommands = new UnsafeList<ThreadLocalDrawCommands>(
                MAX_JOB_WORKERS,
                generalAllocator->Handle,
                NativeArrayOptions.ClearMemory);
            ThreadLocalDrawCommands.Resize(ThreadLocalDrawCommands.Capacity);
            ThreadLocalCollectBuffers = new UnsafeList<ThreadLocalCollectBuffer>(
                MAX_JOB_WORKERS,
                generalAllocator->Handle,
                NativeArrayOptions.ClearMemory);
            ThreadLocalCollectBuffers.Resize(ThreadLocalCollectBuffers.Capacity);
            BinPresentFilter = new UnsafeList<long>(
                BIN_PRESENT_FILTER_SIZE * NUM_THREADS_BITFIELD_LENGTH,
                generalAllocator->Handle,
                NativeArrayOptions.ClearMemory);
            BinPresentFilter.Resize(BinPresentFilter.Capacity);

            BinCollector = new DrawBinCollector(ThreadLocalDrawCommands, generalAllocator);
            SortedBins = new IndirectList<int>(0, generalAllocator);
            BinIndices = new IndirectList<DrawCommandBin>(0, generalAllocator);
            WorkItems = new IndirectList<DrawCommandWorkItem>(0, generalAllocator);


            // Initialized by job system
            ThreadIndex = 0;

            //ProfilerEmit = new ProfilerMarker("Emit");
        }

        public void InitializeForEmitThread()
        {
            // First to use the thread local initializes is, but don't double init
            if (!ThreadLocalDrawCommands[ThreadIndex].IsCreated)
                ThreadLocalDrawCommands[ThreadIndex] = new ThreadLocalDrawCommands(BinCapacity, ThreadLocalAllocator);
        }

        public BatchCullingOutputDrawCommands* CullingOutputDrawCommands =>
            (BatchCullingOutputDrawCommands*)CullingOutput.GetUnsafePtr();

        private ThreadLocalDrawCommands* DrawCommands
        {
            [return: NoAlias]
            get => ThreadLocalDrawCommands.Ptr + ThreadIndex;
        }

        public ThreadLocalCollectBuffer* CollectBuffer
        {
            [return: NoAlias]
            get => ThreadLocalCollectBuffers.Ptr + ThreadIndex;
        }

        public void Emit(DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex)
        {
            // Update the cached hash code here, so all processing after this can just use the cached value
            // without recomputing the hash each time.
            settings.ComputeHashCode();

            bool newBinAdded = DrawCommands->Emit(settings, entityQword, entityBit, chunkStartIndex, ThreadIndex);
            if (newBinAdded)
            {
                BinCollector.Add(settings);
                MarkBinPresentInThread(settings, ThreadIndex);
            }
        }

        public void EmitDepthSorted(
            DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex,
            float4x4* chunkTransforms)
        {
            // Update the cached hash code here, so all processing after this can just use the cached value
            // without recomputing the hash each time.
            settings.ComputeHashCode();

            bool newBinAdded = DrawCommands->EmitDepthSorted(settings, entityQword, entityBit, chunkStartIndex, chunkTransforms, ThreadIndex);
            if (newBinAdded)
            {
                BinCollector.Add(settings);
                MarkBinPresentInThread(settings, ThreadIndex);
            }
        }

        [return: NoAlias]
        public long* BinPresentFilterForSettings(DrawCommandSettings settings)
        {
            uint hash = (uint)settings.GetHashCode();
            uint index = hash % (uint)BIN_PRESENT_FILTER_SIZE;
            return BinPresentFilter.Ptr + index * NUM_THREADS_BITFIELD_LENGTH;
        }

        private void MarkBinPresentInThread(DrawCommandSettings settings, int threadIndex)
        {
            long* settingsFilter = BinPresentFilterForSettings(settings);

            uint threadQword = (uint)threadIndex / 64;
            uint threadBit = (uint)threadIndex % 64;

            AtomicHelpers.AtomicOr(
                settingsFilter,
                (int)threadQword,
                1L << (int)threadBit);
        }

        public static int FastHash<T>(T value) where T : struct
        {
            // TODO: Replace with hardware CRC32?
            return (int)xxHash3.Hash64(UnsafeUtility.AddressOf(ref value), UnsafeUtility.SizeOf<T>()).x;
        }

        public JobHandle Dispose(JobHandle dependencies)
        {
            // First schedule a job to release all the thread local arrays, which requires
            // that the data structures are still in place so we can find them.
            var releaseChunkDrawCommandsDependency = new ReleaseChunkDrawCommandsJob
            {
                DrawCommandOutput = this,
                NumThreads = NUM_RELEASE_THREADS,
            }.ScheduleParallel(NUM_RELEASE_THREADS, 1, dependencies);

            // When those have been released, release the data structures.
            var disposeDone = new JobHandle();
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                ThreadLocalDrawCommands.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                ThreadLocalCollectBuffers.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                BinPresentFilter.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                BinCollector.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                SortedBins.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                BinIndices.Dispose(releaseChunkDrawCommandsDependency));
            disposeDone = JobHandle.CombineDependencies(disposeDone,
                WorkItems.Dispose(releaseChunkDrawCommandsDependency));

            return disposeDone;
        }

        [BurstCompile]
        private struct ReleaseChunkDrawCommandsJob : IJobFor
        {
            public DrawCommandOutput DrawCommandOutput;
            public int NumThreads;

            public void Execute(int index)
            {
                for (int i = index; i < MAX_JOB_WORKERS; i += NumThreads)
                {
                    DrawCommandOutput.ThreadLocalDrawCommands[i].Dispose();
                    DrawCommandOutput.ThreadLocalCollectBuffers[i].Dispose();
                }
            }
        }
    }
}

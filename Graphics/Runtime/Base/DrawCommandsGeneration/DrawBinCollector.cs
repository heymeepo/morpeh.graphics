using Scellecs.Morpeh.Graphics.Collections;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct DrawBinCollector
    {
        public const Allocator kAllocator = Allocator.TempJob;
        public static readonly int NumThreads = BrgHelpers.MAX_JOB_WORKERS;

        public IndirectList<DrawCommandSettings> Bins;
        private UnsafeParallelHashSet<DrawCommandSettings> m_BinSet;
        private UnsafeList<ThreadLocalDrawCommands> m_ThreadLocalDrawCommands;

        public DrawBinCollector(UnsafeList<ThreadLocalDrawCommands> tlDrawCommands, RewindableAllocator* allocator)
        {
            Bins = new IndirectList<DrawCommandSettings>(0, allocator);
            m_BinSet = new UnsafeParallelHashSet<DrawCommandSettings>(0, kAllocator);
            m_ThreadLocalDrawCommands = tlDrawCommands;
        }

        public bool Add(DrawCommandSettings settings)
        {
            return true;
        }

        [BurstCompile]
        internal struct AllocateBinsJob : IJob
        {
            public IndirectList<DrawCommandSettings> Bins;
            public UnsafeParallelHashSet<DrawCommandSettings> BinSet;
            public UnsafeList<ThreadLocalDrawCommands> ThreadLocalDrawCommands;

            public void Execute()
            {
                int numBinsUpperBound = 0;

                for (int i = 0; i < NumThreads; ++i)
                    numBinsUpperBound += ThreadLocalDrawCommands.ElementAt(i).DrawCommands.Length;

                Bins.SetCapacity(numBinsUpperBound);
                BinSet.Capacity = numBinsUpperBound;
            }
        }

        [BurstCompile]
        internal struct CollectBinsJob : IJobParallelFor
        {
            public const int ThreadLocalArraySize = 256;

            public IndirectList<DrawCommandSettings> Bins;
            public UnsafeParallelHashSet<DrawCommandSettings>.ParallelWriter BinSet;
            public UnsafeList<ThreadLocalDrawCommands> ThreadLocalDrawCommands;

            private UnsafeList<DrawCommandSettings>.ParallelWriter m_BinsParallel;

            public void Execute(int index)
            {
                ref var drawCommands = ref ThreadLocalDrawCommands.ElementAt(index);
                if (!drawCommands.IsCreated)
                    return;

                m_BinsParallel = Bins.List->AsParallelWriter();

                var uniqueSettings = new NativeArray<DrawCommandSettings>(
                    ThreadLocalArraySize,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                int numSettings = 0;

                var keys = drawCommands.DrawCommandStreamIndices.GetEnumerator();
                while (keys.MoveNext())
                {
                    var settings = keys.Current.Key;
                    if (BinSet.Add(settings))
                        AddBin(uniqueSettings, ref numSettings, settings);
                }
                keys.Dispose();

                Flush(uniqueSettings, numSettings);
            }

            private void AddBin(
                NativeArray<DrawCommandSettings> uniqueSettings,
                ref int numSettings,
                DrawCommandSettings settings)
            {
                if (numSettings >= ThreadLocalArraySize)
                {
                    Flush(uniqueSettings, numSettings);
                    numSettings = 0;
                }

                uniqueSettings[numSettings] = settings;
                ++numSettings;
            }

            private void Flush(
                NativeArray<DrawCommandSettings> uniqueSettings,
                int numSettings)
            {
                if (numSettings <= 0)
                    return;

                m_BinsParallel.AddRangeNoResize(
                    uniqueSettings.GetUnsafeReadOnlyPtr(),
                    numSettings);
            }
        }

        public JobHandle ScheduleFinalize(JobHandle dependency)
        {
            var allocateDependency = new AllocateBinsJob
            {
                Bins = Bins,
                BinSet = m_BinSet,
                ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
            }.Schedule(dependency);

            return new CollectBinsJob
            {
                Bins = Bins,
                BinSet = m_BinSet.AsParallelWriter(),
                ThreadLocalDrawCommands = m_ThreadLocalDrawCommands,
            }.Schedule(NumThreads, 1, allocateDependency);
        }

        public JobHandle Dispose(JobHandle dependency)
        {
            return JobHandle.CombineDependencies(
                Bins.Dispose(dependency),
                m_BinSet.Dispose(dependency));
        }
    }
}

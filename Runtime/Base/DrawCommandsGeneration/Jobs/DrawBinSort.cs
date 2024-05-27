using Scellecs.Morpeh.Graphics.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct DrawBinSort
    {
        public const int kNumSlices = 4;
        public const Allocator kAllocator = Allocator.TempJob;

        [BurstCompile]
        internal unsafe struct SortArrays
        {
            public IndirectList<int> SortedBins;
            public IndirectList<int> SortTemp;

            public int ValuesPerIndex => (SortedBins.Length + kNumSlices - 1) / kNumSlices;

            [return: NoAlias] public int* ValuesTemp(int i = 0) => SortTemp.List->Ptr + i;
            [return: NoAlias] public int* ValuesDst(int i = 0) => SortedBins.List->Ptr + i;

            public void GetBeginEnd(int index, out int begin, out int end)
            {
                begin = index * ValuesPerIndex;
                end = math.min(begin + ValuesPerIndex, SortedBins.Length);
            }
        }

        internal unsafe struct BinSortComparer : IComparer<int>
        {
            [NoAlias]
            public DrawCommandSettings* Bins;

            public BinSortComparer(IndirectList<DrawCommandSettings> bins)
            {
                Bins = bins.List->Ptr;
            }

            public int Compare(int x, int y) => Key(x).CompareTo(Key(y));

            private DrawCommandSettings Key(int bin) => Bins[bin];
        }

        [BurstCompile]
        internal unsafe struct AllocateForSortJob : IJob
        {
            public IndirectList<DrawCommandSettings> UnsortedBins;
            public SortArrays Arrays;

            public void Execute()
            {
                int numBins = UnsortedBins.Length;
                Arrays.SortedBins.Resize(numBins, NativeArrayOptions.UninitializedMemory);
                Arrays.SortTemp.Resize(numBins, NativeArrayOptions.UninitializedMemory);
            }
        }

        [BurstCompile]
        internal unsafe struct SortSlicesJob : IJobParallelFor
        {
            public SortArrays Arrays;
            public IndirectList<DrawCommandSettings> UnsortedBins;

            public void Execute(int index)
            {
                Arrays.GetBeginEnd(index, out int begin, out int end);

                var valuesFromZero = Arrays.ValuesTemp();
                int N = end - begin;

                for (int i = begin; i < end; ++i)
                    valuesFromZero[i] = i;

                NativeSortExtension.Sort(Arrays.ValuesTemp(begin), N, new BinSortComparer(UnsortedBins));
            }
        }

        [BurstCompile]
        internal unsafe struct MergeSlicesJob : IJob
        {
            public SortArrays Arrays;
            public IndirectList<DrawCommandSettings> UnsortedBins;
            public int NumSlices => kNumSlices;

            public void Execute()
            {
                var sliceRead = stackalloc int[NumSlices];
                var sliceEnd = stackalloc int[NumSlices];

                int sliceMask = 0;

                for (int i = 0; i < NumSlices; ++i)
                {
                    Arrays.GetBeginEnd(i, out sliceRead[i], out sliceEnd[i]);
                    if (sliceRead[i] < sliceEnd[i])
                        sliceMask |= 1 << i;
                }

                int N = Arrays.SortedBins.Length;
                var dst = Arrays.ValuesDst();
                var src = Arrays.ValuesTemp();
                var comparer = new BinSortComparer(UnsortedBins);

                for (int i = 0; i < N; ++i)
                {
                    int iterMask = sliceMask;
                    int firstNonEmptySlice = math.tzcnt(iterMask);

                    int bestSlice = firstNonEmptySlice;
                    int bestValue = src[sliceRead[firstNonEmptySlice]];
                    iterMask ^= 1 << firstNonEmptySlice;

                    while (iterMask != 0)
                    {
                        int slice = math.tzcnt(iterMask);
                        int value = src[sliceRead[slice]];

                        if (comparer.Compare(value, bestValue) < 0)
                        {
                            bestSlice = slice;
                            bestValue = value;
                        }

                        iterMask ^= 1 << slice;
                    }

                    dst[i] = bestValue;

                    int nextValue = sliceRead[bestSlice] + 1;
                    bool sliceExhausted = nextValue >= sliceEnd[bestSlice];
                    sliceRead[bestSlice] = nextValue;

                    int mask = 1 << bestSlice;
                    mask = sliceExhausted ? mask : 0;
                    sliceMask ^= mask;
                }

                Arrays.SortTemp.Dispose(default);
            }
        }

        public static JobHandle ScheduleBinSort(
            RewindableAllocator* allocator,
            IndirectList<int> sortedBins,
            IndirectList<DrawCommandSettings> unsortedBins,
            JobHandle dependency = default)
        {
            var sortArrays = new SortArrays
            {
                SortedBins = sortedBins,
                SortTemp = new IndirectList<int>(0, allocator),
            };

            var alloc = new AllocateForSortJob
            {
                Arrays = sortArrays,
                UnsortedBins = unsortedBins,
            }.Schedule(dependency);

            var sortSlices = new SortSlicesJob
            {
                Arrays = sortArrays,
                UnsortedBins = unsortedBins,
            }.Schedule(kNumSlices, 1, alloc);

            var mergeSlices = new MergeSlicesJob
            {
                Arrays = sortArrays,
                UnsortedBins = unsortedBins,
            }.Schedule(sortSlices);

            return mergeSlices;
        }
    }


}

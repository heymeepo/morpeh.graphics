using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal unsafe struct IndirectList<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        public UnsafeList<T>* List;

        public IndirectList(int capacity, RewindableAllocator* allocator)
        {
            List = AllocIndirectList(capacity, allocator);
        }

        public int Length => List->Length;
        public void Resize(int length, NativeArrayOptions options) => List->Resize(length, options);
        public void SetCapacity(int capacity) => List->SetCapacity(capacity);
        public ref T ElementAt(int i) => ref List->ElementAt(i);
        public void Add(T value) => List->Add(value);

        private static UnsafeList<T>* AllocIndirectList(int capacity, RewindableAllocator* allocator)
        {
            AllocatorManager.AllocatorHandle allocatorHandle = allocator->Handle;
            var indirectList = (UnsafeList<T>*)allocatorHandle.Allocate(UnsafeUtility.SizeOf<UnsafeList<T>>(), UnsafeUtility.AlignOf<UnsafeList<T>>(), 1);
            *indirectList = new UnsafeList<T>(capacity, allocatorHandle);
            return indirectList;
        }

        public JobHandle Dispose(JobHandle dependency) => default;
    }
}

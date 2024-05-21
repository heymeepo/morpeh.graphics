using Unity.Collections;

namespace Scellecs.Morpeh.Graphics.UnityInternal
{
    public static class UnityInternalUnsafeUtility
    {
        public static unsafe void* Allocate(long size, int align, AllocatorManager.AllocatorHandle allocator) => Memory.Unmanaged.Allocate(size, align, allocator);

        public static unsafe void Free<T>(T* pointer, AllocatorManager.AllocatorHandle allocator) where T : unmanaged => Memory.Unmanaged.Free(pointer, allocator);
    }
}

using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    public static unsafe class UnsafeHelpers
    {
        public static unsafe T* Malloc<T>(int count, Allocator allocator = Allocator.TempJob) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<T>() * count, UnsafeUtility.AlignOf<T>(), allocator);
        }

        public static unsafe void Free(IntPtr ptr, Allocator allocator)
        {
            UnsafeUtility.Free((void*)ptr, allocator);
        }

        public static unsafe void Free(void* ptr, Allocator allocator)
        {
            UnsafeUtility.Free(ptr, allocator);
        }
    }
}

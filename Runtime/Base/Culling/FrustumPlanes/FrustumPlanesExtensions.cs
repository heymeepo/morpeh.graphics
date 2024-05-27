using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Scellecs.Morpeh.Graphics.Culling
{
    internal static class FrustumPlanesExtensions
    {
        // We want to use UnsafeList to use RewindableAllocator, but PlanePacket APIs want NativeArrays
        internal static unsafe NativeArray<T> AsNativeArray<T>(this UnsafeList<T> list) where T : unmanaged
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
            return array;
        }

        internal static NativeArray<T> GetSubNativeArray<T>(this UnsafeList<T> list, int start, int length)
            where T : unmanaged =>
            list.AsNativeArray().GetSubArray(start, length);
    }
}

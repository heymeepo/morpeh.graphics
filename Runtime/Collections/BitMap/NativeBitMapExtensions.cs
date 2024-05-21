#if MORPEH_BURST
namespace Scellecs.Morpeh.Graphics.Collections
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public unsafe static class NativeBitMapExtensions
    {
        public static NativeBitMap AsNative(this BitMap bitMap)
        {
            var nativeBitMap = new NativeBitMap();

            fixed (int* countPtr = &bitMap.count)
            fixed (int* lengthPtr = &bitMap.length)
            fixed (int* capacityPtr = &bitMap.capacity)
            fixed (int* capacityMinusOnePtr = &bitMap.capacityMinusOne)
            fixed (int* lastIndexPtr = &bitMap.lastIndex)
            {
                nativeBitMap.countPtr = countPtr;
                nativeBitMap.lengthPtr = lengthPtr;
                nativeBitMap.capacityPtr = capacityPtr;
                nativeBitMap.capacityMinusOnePtr = capacityMinusOnePtr;
                nativeBitMap.lastIndexPtr = lastIndexPtr;
                nativeBitMap.bucketsPtr = bitMap.buckets.ptr;
                nativeBitMap.dataPtr = bitMap.data.ptr;
                nativeBitMap.slotsPtr = bitMap.slots.ptr;
            }

            return nativeBitMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int tzcnt(uint x)
        {
            if (x == 0)
                return 32;

            x &= (uint)-x;
            LongDoubleUnion u;
            u.doubleValue = 0.0;
            u.longValue = 0x4330000000000000L + x;
            u.doubleValue -= 4503599627370496.0;
            return (int)(u.longValue >> 52) - 0x3FF;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct LongDoubleUnion
        {
            [FieldOffset(0)]
            public long longValue;
            [FieldOffset(0)]
            public double doubleValue;
        }
    }
}
#endif
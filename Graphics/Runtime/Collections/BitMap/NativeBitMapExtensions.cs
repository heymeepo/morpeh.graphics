#if MORPEH_BURST
namespace Scellecs.Morpeh.Graphics.Collections
{
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
    }
}
#endif
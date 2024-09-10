using Unity.Collections.LowLevel.Unsafe;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal unsafe struct NativeIntSparseSet
    {
        [NativeDisableUnsafePtrRestriction]
        public int* sparse;

        [NativeDisableUnsafePtrRestriction]
        public int* dense;

        public int count;
    }
}

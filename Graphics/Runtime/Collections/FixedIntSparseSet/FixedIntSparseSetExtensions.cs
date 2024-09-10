namespace Scellecs.Morpeh.Graphics.Collections
{
    internal static unsafe class FixedIntSparseSetExtensions
    {
        public static int* GetUnsafeDataPtr(this FixedIntSparseSet set) => set.dense.ptr;

        public static NativeIntSparseSet AsNative(this FixedIntSparseSet set)
        {
            return new NativeIntSparseSet()
            {
                sparse = set.sparse.ptr,
                dense = set.dense.ptr,
                count = set.count
            };
        }
    }
}

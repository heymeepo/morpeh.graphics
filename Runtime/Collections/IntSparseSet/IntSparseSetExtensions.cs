﻿namespace Scellecs.Morpeh.Graphics.Collections
{
    internal static unsafe class IntSparseSetExtensions
    {
        public static int* GetUnsafeDataPtr(this IntSparseSet set) => set.dense.ptr;

        public static NativeIntSparseSet AsNative(this IntSparseSet set)
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
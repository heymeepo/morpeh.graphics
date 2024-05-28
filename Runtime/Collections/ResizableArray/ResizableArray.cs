using Scellecs.Morpeh.Collections;
using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Collections
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal sealed class ResizableArray<T> : IDisposable where T : unmanaged
    {
        private PinnedArray<T> data;

        public ResizableArray(int capacity = 4) => data = new PinnedArray<T>(capacity);

        public unsafe ref T this[int index] => ref data.ptr[index];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, T value)
        {
            if (index >= data.Length)
            {
                data.Resize(data.Length * 2);
            }

            data[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T* GetUnsafePtr() => data.ptr;

        public void Dispose() => data.Dispose();
    }
}

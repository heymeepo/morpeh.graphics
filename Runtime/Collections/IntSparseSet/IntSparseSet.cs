﻿using Scellecs.Morpeh.Collections;
using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal unsafe sealed class IntSparseSet : IDisposable
    {
        internal IntPinnedArray sparse;
        internal IntPinnedArray dense;

        internal int capacity;
        internal int count;

        public IntSparseSet(int defaultCapacity)
        {
            this.capacity = defaultCapacity;

            this.sparse = new IntPinnedArray(defaultCapacity);
            this.dense = new IntPinnedArray(defaultCapacity);

            this.count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newCapacity)
        {
            this.capacity = newCapacity;

            sparse.Resize(this.capacity);
            dense.Resize(this.capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int value)
        {
            if (this.Contains(value))
            {
                return false;
            }

            if (value >= capacity)
            {
                var logBase2 = Math.Log(value, 2);
                int nextPower = (int)Math.Ceiling(logBase2);
                Resize(1 << nextPower);
            }

            this.dense.ptr[this.count] = value;
            this.sparse.ptr[value] = this.count;

            ++this.count;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(int value)
        {
            if (!this.Contains(value))
            {
                return false;
            }

            var index = this.sparse.ptr[value];

            --this.count;
            this.dense.ptr[index] = this.dense.ptr[this.count];
            this.sparse.ptr[this.dense.ptr[index]] = index;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int value)
        {
            return value < capacity && this.sparse.ptr[value] < this.count && this.dense.ptr[this.sparse.ptr[value]] == value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            for (var i = 0; i < this.count; i++)
            {
                this.sparse.ptr[this.dense.ptr[i]] = 0;
            }

            this.count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            var e = default(Enumerator);
            e.dense = this.dense.ptr;
            e.index = -1;
            e.count = this.count;
            return e;
        }

        public void Dispose()
        {
            dense.Dispose();
            sparse.Dispose();
        }

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct Enumerator
        {
            internal int* dense;
            internal int index;
            internal int count;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++this.index < this.count;
            }

            public int Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.dense[this.index];
            }
        }
    }
}
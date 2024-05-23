using Scellecs.Morpeh.Collections;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal struct ReadOnlyIntHashSet
    {
        public int Count { get; private set; }

        private readonly IntHashSet set;

        public ReadOnlyIntHashSet(IntHashSet set)
        {
            this.set = set;
            Count = set.length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(set, 0, default);

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public unsafe struct Enumerator
        {
            private IntHashSet set;

            private int index;
            private int current;

            public Enumerator(IntHashSet set, int index, int current)
            {
                this.set = set;
                this.index = index;
                this.current = current;
            }

            public bool MoveNext()
            {
                var slotsPtr = this.set.slots.ptr;
                for (var len = this.set.lastIndex; this.index < len; this.index += 2)
                {
                    var v = slotsPtr[this.index] - 1;
                    if (v < 0)
                    {
                        continue;
                    }

                    this.current = v;
                    this.index += 2;
                    return true;
                }

                this.index = this.set.lastIndex + 1;
                this.current = default;
                return false;
            }

            public int Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.current;
            }
        }
    }
}

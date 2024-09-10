using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Collections
{
    internal struct ReadOnlyIntFastList
    {
        public int Count { get; private set; }

        public readonly int this[int index] => data[index];

        private readonly int[] data;

        public ReadOnlyIntFastList(int[] data, int length)
        {
            this.data = data;
            this.Count = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(data, -1, Count);

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.DivideByZeroChecks, false)]
        public struct Enumerator
        {
            private int[] data;
            private int index;
            private int length;

            public Enumerator(int[] data, int index, int length)
            {
                this.data = data;
                this.index = index;
                this.length = length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++this.index < this.length;
            }

            public int Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => this.data[this.index];
            }
        }
    }
}

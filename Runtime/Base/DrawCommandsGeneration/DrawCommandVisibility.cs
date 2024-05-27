using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct DrawCommandVisibility
    {
        public int ChunkStartIndex;
        public fixed ulong VisibleInstances[2];

        public DrawCommandVisibility(int startIndex)
        {
            ChunkStartIndex = startIndex;
            VisibleInstances[0] = 0;
            VisibleInstances[1] = 0;
        }

        public int VisibleInstanceCount => math.countbits(VisibleInstances[0]) + math.countbits(VisibleInstances[1]);

        public override string ToString()
        {
            return $"Visibility({ChunkStartIndex}, {VisibleInstances[1]:x16}, {VisibleInstances[0]:x16})";
        }
    }
}

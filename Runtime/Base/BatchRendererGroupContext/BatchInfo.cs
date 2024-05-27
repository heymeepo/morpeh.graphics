using Scellecs.Morpeh.Graphics.Culling;

namespace Scellecs.Morpeh.Graphics
{
    internal struct BatchInfo
    {
        public HeapBlock batchGpuAllocation;
        public AABB batchAABB;
        public int archetypeIndex;
        public int archetypeInternalIndex;
    }
}

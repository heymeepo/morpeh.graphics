using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    internal struct DepthSortedDrawCommand
    {
        public DrawCommandSettings Settings;
        public int InstanceIndex;
        public float3 SortingWorldPosition;
    }
}

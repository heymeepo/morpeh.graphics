using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics.Culling
{
    internal unsafe struct CullingSplitData
    {
        public float3 CullingSphereCenter;
        public float CullingSphereRadius;
        public float ShadowCascadeBlendCullingFactor;
        public int PlanePacketOffset;
        public int PlanePacketCount;
        public int CombinedPlanePacketOffset;
        public int CombinedPlanePacketCount;
    }
}

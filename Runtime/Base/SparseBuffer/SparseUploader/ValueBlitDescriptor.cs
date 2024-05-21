using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    internal struct ValueBlitDescriptor
    {
        public float4x4 value;
        public uint destinationOffset;
        public uint valueSizeBytes;
        public uint count;

        public int BytesRequiredInUploadBuffer => (int)(valueSizeBytes * count);
    }
}

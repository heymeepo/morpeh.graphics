using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    internal struct SparseBufferArgs
    {
        public GraphicsBuffer.Target target;
        public GraphicsBuffer.UsageFlags flags;
        public ulong initialSize;
        public ulong maxSize;
        public int stride;
        public int uploaderChunkSize;
    }
}

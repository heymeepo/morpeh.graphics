using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    internal static class SparseBufferUtility
    {
        public static GraphicsBuffer CreateGraphicsBuffer(ref SparseBufferArgs args, ulong bufferSize)
        {
            return new GraphicsBuffer(args.target, args.flags, (int)bufferSize / args.stride, args.stride);
        }
    }
}

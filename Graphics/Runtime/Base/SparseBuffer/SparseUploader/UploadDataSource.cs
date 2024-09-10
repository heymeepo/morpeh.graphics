using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Workaround;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct UploadDataSource
    {
        public UnmanagedStash* srcData;
        public NativeFilter filter;
        public int filterOffset;
        public int count;
    }
}

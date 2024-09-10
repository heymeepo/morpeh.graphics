using System;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct DrawCommandWorkItem
    {
        public DrawStream<DrawCommandVisibility>.Header* Arrays;
        public DrawStream<IntPtr>.Header* TransformArrays;
        public int BinIndex;
        public int PrefixSumNumInstances;
    }
}

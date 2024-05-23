namespace Scellecs.Morpeh.Graphics
{
    internal readonly struct BatchInfo
    {
        public readonly HeapBlock batchGpuAllocation;

        public BatchInfo(HeapBlock allocation) => batchGpuAllocation = allocation;
    }
}

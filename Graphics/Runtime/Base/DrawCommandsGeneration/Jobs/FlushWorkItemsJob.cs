using Unity.Burst;
using Unity.Jobs;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct FlushWorkItemsJob : IJobParallelFor
    {
        public DrawCommandOutput drawCommandOutput;

        public void Execute(int index)
        {
            var dst = drawCommandOutput.WorkItems.List->AsParallelWriter();
            drawCommandOutput.ThreadLocalCollectBuffers[index].Flush(dst);
        }
    }
}

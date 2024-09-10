using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct ThreadLocalDrawCommands
    {
        public const Allocator kAllocator = Allocator.TempJob;

        // Store the actual streams in a separate array so we can mutate them in place,
        // the hash map only supports a get/set API.
        public UnsafeParallelHashMap<DrawCommandSettings, int> DrawCommandStreamIndices;
        public UnsafeList<DrawCommandStream> DrawCommands;
        public ThreadLocalAllocator ThreadLocalAllocator;

        private fixed int m_CacheLinePadding[8]; // The padding here assumes some internal sizes

        public ThreadLocalDrawCommands(int capacity, ThreadLocalAllocator tlAllocator)
        {
            // Make sure we don't get false sharing by placing the thread locals on different cache lines.
            Assert.IsTrue(sizeof(ThreadLocalDrawCommands) >= JobsUtility.CacheLineSize);
            DrawCommandStreamIndices = new UnsafeParallelHashMap<DrawCommandSettings, int>(capacity, kAllocator);
            DrawCommands = new UnsafeList<DrawCommandStream>(capacity, kAllocator);
            ThreadLocalAllocator = tlAllocator;
        }

        public bool IsCreated => DrawCommandStreamIndices.IsCreated;

        public void Dispose()
        {
            if (!IsCreated)
                return;

            for (int i = 0; i < DrawCommands.Length; ++i)
                DrawCommands[i].Dispose();

            if (DrawCommandStreamIndices.IsCreated)
                DrawCommandStreamIndices.Dispose();
            if (DrawCommands.IsCreated)
                DrawCommands.Dispose();
        }

        public bool Emit(DrawCommandSettings settings, int qwordIndex, int bitIndex, int chunkStartIndex, int threadIndex)
        {
            var allocator = ThreadLocalAllocator.ThreadAllocator(threadIndex);

            if (DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
            {
                DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                stream->Emit(allocator, qwordIndex, bitIndex, chunkStartIndex);
                return false;
            }
            else
            {

                streamIndex = DrawCommands.Length;
                DrawCommands.Add(new DrawCommandStream(allocator));
                DrawCommandStreamIndices.Add(settings, streamIndex);

                DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                stream->Emit(allocator, qwordIndex, bitIndex, chunkStartIndex);

                return true;
            }
        }

        public bool EmitDepthSorted(
            DrawCommandSettings settings, int qwordIndex, int bitIndex, int chunkStartIndex,
            float4x4* chunkTransforms,
            int threadIndex)
        {
            var allocator = ThreadLocalAllocator.ThreadAllocator(threadIndex);

            if (DrawCommandStreamIndices.TryGetValue(settings, out int streamIndex))
            {
                DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                stream->EmitDepthSorted(allocator, qwordIndex, bitIndex, chunkStartIndex, chunkTransforms);
                return false;
            }
            else
            {

                streamIndex = DrawCommands.Length;
                DrawCommands.Add(new DrawCommandStream(allocator));
                DrawCommandStreamIndices.Add(settings, streamIndex);

                DrawCommandStream* stream = DrawCommands.Ptr + streamIndex;
                stream->EmitDepthSorted(allocator, qwordIndex, bitIndex, chunkStartIndex, chunkTransforms);

                return true;
            }
        }
    }
}

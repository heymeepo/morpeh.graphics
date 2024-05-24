using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class BatchRendererInitializer : IInitializer
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;
        private Stash<SharedBatchRendererGroupContext> sharedContextStash;

        public void OnAwake()
        {
            brg = new BatchRendererGroupContext(new SparseBufferArgs()
            {
                target = GraphicsBuffer.Target.Raw,
                flags = GraphicsBuffer.UsageFlags.None,
                initialSize = GPU_BUFFER_INITIAL_SIZE,
                maxSize = GPU_BUFFER_MAX_SIZE,
                stride = SIZE_OF_UINT,
                uploaderChunkSize = GPU_UPLOADER_CHUNK_SIZE
            });

            brg.SetEnabledViewTypes(new BatchCullingViewType[]
            {
                BatchCullingViewType.Camera,
                BatchCullingViewType.Light
            });

            brg.SetGlobalBounds(new Bounds(float3.zero, new float3(1048576f)));
            brg.GetBuffer().Allocate(SIZE_OF_MATRIX4X4, 16, out _);

            sharedContextStash = World.GetStash<SharedBatchRendererGroupContext>();
            sharedContextStash.Set(World.CreateEntity(), new SharedBatchRendererGroupContext() { brg = brg });
        }

        public void Dispose() => brg?.Dispose();
    }
}

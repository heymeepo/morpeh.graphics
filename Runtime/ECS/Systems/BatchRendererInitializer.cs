#if URP_10_0_0_OR_NEWER
using UnityEngine.Rendering.Universal;
using System.Reflection;
#endif

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
        private Stash<SharedBatchRendererGroupContext> sharedBrgStash;

        public void OnAwake()
        {
            brg = new BatchRendererGroupContext(MAX_BATCHES_COUNT, new SparseBufferArgs()
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
            brg.Buffer.Allocate(SIZE_OF_MATRIX4X4, 16, out _);

            sharedBrgStash = World.GetStash<SharedBatchRendererGroupContext>();
            sharedBrgStash.Set(World.CreateEntity(), new SharedBatchRendererGroupContext() { brg = brg });

            ValidateUsingURPForwardPlus();
        }

        public void Dispose() => brg?.Dispose();

        private void ValidateUsingURPForwardPlus()
        {
#if URP_10_0_0_OR_NEWER
            RenderPipelineAsset pipelineAsset = GraphicsSettings.renderPipelineAsset;
            if (pipelineAsset is UniversalRenderPipelineAsset)
            {
                UniversalRenderPipelineAsset settings = pipelineAsset as UniversalRenderPipelineAsset;
                var rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var defaultRendererIndexField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                if (rendererDataListField != null && defaultRendererIndexField != null)
                {
                    ScriptableRendererData[] rendererDatas = rendererDataListField.GetValue(settings) as ScriptableRendererData[];
                    int defaultRendererDataIndex = (int)defaultRendererIndexField.GetValue(settings);
                    UniversalRendererData universalRendererData = rendererDatas[defaultRendererDataIndex] as UniversalRendererData;
                    var renderingModeField = typeof(UniversalRendererData).GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (renderingModeField != null && universalRendererData != null)
                    {
                        RenderingMode renderingMode = (RenderingMode)renderingModeField.GetValue(universalRendererData);
                        if (renderingMode != RenderingMode.ForwardPlus)
                        {
                            Debug.LogWarning("BatchRendererGroup should be used with URP Forward+. Change Rendering Path on " + universalRendererData.name + " for best compatibility.");
                        }
                    }
                }
            }
#endif
        }
    }
}

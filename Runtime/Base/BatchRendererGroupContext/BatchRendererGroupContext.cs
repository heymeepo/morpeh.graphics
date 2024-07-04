using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Workaround;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe sealed class BatchRendererGroupContext : IDisposable
    {
        public SparseBuffer Buffer { get; private set; }
        public FixedIntSparseSet ExistingBatchesIndices => existingBatchesIndices; //TODO: return as ReadOnly
        public BatchFilterSettings* BatchFilterSettingsPtr => batchFilterSettings.GetUnsafeDataPtr();
        public BatchInfo* BatchesInfosPtr => batchesInfos.GetUnsafePtr();

        private BatchRendererGroup brg;
        private ThreadedBatchContext threadedBatchContext;

        private ResizableArray<BatchInfo> batchesInfos;
        private IntHashMap<BatchFilterSettings> batchFilterSettings;
        private FixedIntSparseSet existingBatchesIndices;

        private BatchRendererGroup.OnPerformCulling cullingCallback;

        public BatchRendererGroupContext(int maxBatchesCount, SparseBufferArgs bufferArgs)
        {
            brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            Buffer = new SparseBuffer(bufferArgs);
            batchesInfos = new ResizableArray<BatchInfo>();
            batchFilterSettings = new IntHashMap<BatchFilterSettings>();
            existingBatchesIndices = new FixedIntSparseSet(maxBatchesCount);
            threadedBatchContext = brg.GetThreadedBatchContext();
        }

        public void SetGlobalBounds(Bounds bounds) => brg.SetGlobalBounds(bounds);

        public void SetEnabledViewTypes(BatchCullingViewType[] viewTypes) => brg.SetEnabledViewTypes(viewTypes);

        public void SetCullingCallback(BatchRendererGroup.OnPerformCulling callback) => cullingCallback = callback;

        public BatchID AddBatch(NativeArray<MetadataValue> metadata)
        {
            var batchID = threadedBatchContext.AddBatch(metadata, Buffer.Handle);
            var batchIndex = BatchIDAsInt(batchID);
            existingBatchesIndices.Add(batchIndex);
            return batchID;
        }

        public void RemoveBatch(BatchID batchID)
        {
            var batchIndex = BatchIDAsInt(batchID);
            existingBatchesIndices.Remove(batchIndex);
            threadedBatchContext.RemoveBatch(batchID);
        }

        public void AddBatchInfo(BatchInfo info, int index) => batchesInfos.Set(index, info);

        public int GetBatchFilterSettingsIndex(ref RenderFilterSettings filterSettings)
        {
            var hash = filterSettings.GetHashCode(); //TODO: precompute hash some way?
            var index = batchFilterSettings.TryGetIndex(hash);

            if (index < 0)
            {
                var settings = new BatchFilterSettings
                {
                    layer = (byte)filterSettings.layer,
                    renderingLayerMask = filterSettings.renderingLayerMask,
                    motionMode = filterSettings.motionMode,
                    shadowCastingMode = filterSettings.shadowCastingMode,
                    receiveShadows = filterSettings.receiveShadows,
                    staticShadowCaster = filterSettings.staticShadowCaster,
                    allDepthSorted = false
                };

                batchFilterSettings.Add(hash, settings, out index);
            }

            return index;
        }

        public BatchMeshID RegisterMesh(Mesh mesh) => brg.RegisterMesh(mesh);

        public BatchMaterialID RegisterMaterial(Material material) => brg.RegisterMaterial(material);

        public void UnregisterMesh(BatchMeshID meshID) => brg.UnregisterMesh(meshID);

        public void UnregisterMaterial(BatchMaterialID materialID) => brg.UnregisterMaterial(materialID);

        public void UpdateBatchBufferHandles()
        {
            foreach (var batchID in existingBatchesIndices)
            {
                brg.SetBatchBuffer(IntAsBatchID(batchID), Buffer.Handle);
            }
        }

        public void Dispose()
        {
            brg.Dispose();
            Buffer.Dispose();
            batchesInfos.Dispose();
            existingBatchesIndices.Dispose();
            threadedBatchContext = default;
            cullingCallback = default;
        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            return cullingCallback != null ? cullingCallback.Invoke(rendererGroup, cullingContext, cullingOutput, userContext) : default;
        }
    }
}

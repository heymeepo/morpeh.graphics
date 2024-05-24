using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class BatchRendererGroupContext : IDisposable
    {
        private BatchRendererGroup brg;
        private SparseBuffer brgBuffer;
        private ThreadedBatchContext threadedBatchContext;

        private ResizableArray<BatchInfo> batchInfos;
        private IntSparseSet existingBatchesIndices;

        private BatchRendererGroup.OnPerformCulling cullingCallback;

        public BatchRendererGroupContext(SparseBufferArgs bufferArgs)
        {
            brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            brgBuffer = new SparseBuffer(bufferArgs);
            batchInfos = new ResizableArray<BatchInfo>();
            threadedBatchContext = brg.GetThreadedBatchContext();
            existingBatchesIndices = new IntSparseSet(64);
        }

        public void SetGlobalBounds(Bounds bounds) => brg.SetGlobalBounds(bounds);

        public void SetEnabledViewTypes(BatchCullingViewType[] viewTypes) => brg.SetEnabledViewTypes(viewTypes);

        public void SetCullingCallback(BatchRendererGroup.OnPerformCulling callback) => cullingCallback = callback;

        public BatchID AddBatch(NativeArray<MetadataValue> metadata)
        {
            var batchID = threadedBatchContext.AddBatch(metadata, brgBuffer.Handle);
            var batchIndex = batchID.AsInt();
            existingBatchesIndices.Add(batchIndex);
            return batchID;
        }

        public void RemoveBatch(BatchID batchID)
        {
            var batchIndex = batchID.AsInt();
            existingBatchesIndices.Remove(batchIndex);
            threadedBatchContext.RemoveBatch(batchID);
        }

        public void AddBatchInfo(BatchInfo info, int index) => batchInfos.AddAt(index, info);

        public BatchMeshID RegisterMesh(Mesh mesh) => brg.RegisterMesh(mesh);

        public BatchMaterialID RegisterMaterial(Material material) => brg.RegisterMaterial(material);

        public void UnregisterMesh(BatchMeshID meshID) => brg.UnregisterMesh(meshID);

        public void UnregisterMaterial(BatchMaterialID materialID) => brg.UnregisterMaterial(materialID);

        public unsafe BatchInfo* GetBatchInfosUnsafePtr() => batchInfos.GetUnsafePtr();

        public IntSparseSet GetExistingBatchesIndices() => existingBatchesIndices;

        public SparseBuffer GetBuffer() => brgBuffer;

        public void UpdateBatchBufferHandles()
        {
            foreach (var batchID in existingBatchesIndices)
            {
                brg.SetBatchBuffer(IntAsBatchID(batchID), brgBuffer.Handle);
            }
        }

        public void Dispose()
        {
            brg.Dispose();
            brgBuffer.Dispose();
            batchInfos.Dispose();
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

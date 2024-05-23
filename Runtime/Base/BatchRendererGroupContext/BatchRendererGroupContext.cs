using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BRGHelpers;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class BatchRendererGroupContext : IDisposable
    {
        private BatchRendererGroup brg;
        private SparseBuffer brgBuffer;
        private ThreadedBatchContext threadedBatchContext;

        private ResizableArray<MaterialPropertyOverride> overrides;
        private ResizableArray<BatchInfo> batchInfos;
        private ResizableArray<BatchAABB> batchAABBs;

        private IntHashSet existingBatchIndices;
        private BatchRendererGroup.OnPerformCulling cullingCallback;

        private uint bytesPerBatch;
        private uint batchAllocationAlignment;

        public BatchRendererGroupContext(uint bytesPerBatch, uint batchAllocationAlignment, SparseBufferArgs bufferArgs)
        {
            brg = new BatchRendererGroup(OnPerformCulling, IntPtr.Zero);
            brgBuffer = new SparseBuffer(bufferArgs);
            overrides = new ResizableArray<MaterialPropertyOverride>();
            batchInfos = new ResizableArray<BatchInfo>();
            batchAABBs = new ResizableArray<BatchAABB>();
            existingBatchIndices = new IntHashSet();
            threadedBatchContext = brg.GetThreadedBatchContext();
            this.bytesPerBatch = bytesPerBatch;
            this.batchAllocationAlignment = batchAllocationAlignment;
        }

        public void SetGlobalBounds(Bounds bounds) => brg.SetGlobalBounds(bounds);

        public void SetEnabledViewTypes(BatchCullingViewType[] viewTypes) => brg.SetEnabledViewTypes(viewTypes);

        public void SetCullingCallback(BatchRendererGroup.OnPerformCulling callback) => cullingCallback = callback;

        public void AddOverridenProperty(MaterialPropertyOverride property, int index) => overrides.AddAt(index, property);

        public bool AddBatch(NativeArray<int> overridesIndices, NativeArray<int> sourceMetadataStream, out BatchID batchID)
        {
            if (brgBuffer.Allocate(bytesPerBatch, batchAllocationAlignment, out var batchGpuAllocation) == false)
            {
                batchID = default;
                return false;
            }

            var metadata = new NativeArray<MetadataValue>(overridesIndices.Length, Allocator.Temp);
            var batchInfo = new BatchInfo(batchGpuAllocation);
            var batchBegin = (int)batchGpuAllocation.begin;

            for (int i = 0; i < overridesIndices.Length; i++)
            {
                int gpuAddress = batchBegin + sourceMetadataStream[i];
                ref var property = ref overrides[overridesIndices[i]];
                metadata[i] = CreateMetadataValue(property.shaderId, gpuAddress);
            }

            batchID = threadedBatchContext.AddBatch(metadata, brgBuffer.Handle);
            var batchIndex = batchID.AsInt();
            existingBatchIndices.Add(batchIndex);
            batchInfos.AddAt(batchIndex, batchInfo);
            batchAABBs.AddAt(batchIndex, default);

            return true;
        }

        public void RemoveBatch(BatchID batchID)
        {
            var batchIndex = batchID.AsInt();
            var batchInfo = batchInfos[batchIndex];
            existingBatchIndices.Remove(batchIndex);
            threadedBatchContext.RemoveBatch(batchID);

            if (batchInfo.batchGpuAllocation.Empty == false)
            {
                brgBuffer.Free(batchInfo.batchGpuAllocation);
            }
        }

        public BatchMeshID RegisterMesh(Mesh mesh) => brg.RegisterMesh(mesh);

        public BatchMaterialID RegisterMaterial(Material material) => brg.RegisterMaterial(material);

        public void UnregisterMesh(BatchMeshID meshID) => brg.UnregisterMesh(meshID);

        public void UnregisterMaterial(BatchMaterialID materialID) => brg.UnregisterMaterial(materialID);

        public void Dispose()
        {
            brg.Dispose();
            brgBuffer.Dispose();
            overrides.Dispose();
            batchInfos.Dispose();
            batchAABBs.Dispose();
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

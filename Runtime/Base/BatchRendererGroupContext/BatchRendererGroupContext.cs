using Scellecs.Morpeh.Collections;
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
        private ThreadedBatchContext threadedBatchContext;

        private SparseBuffer brgBuffer;
        private HeapBlock bufferHeader;
        private ValueBlitDescriptor uploadHeaderBlitDescriptor;

        private ResizableArray<MaterialPropertyOverride> overrides;
        private ResizableArray<BatchInfo> batchInfos;
        private ResizableArray<BatchAABB> batchAABBs;

        private IntHashSet existingBatchesIndices;
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
            threadedBatchContext = brg.GetThreadedBatchContext();
            existingBatchesIndices = new IntHashSet();
            this.bytesPerBatch = bytesPerBatch;
            this.batchAllocationAlignment = batchAllocationAlignment;

            brgBuffer.Allocate(SIZE_OF_MATRIX4X4, 16, out var zeroAllocationHeader);
            bufferHeader = zeroAllocationHeader;
            uploadHeaderBlitDescriptor = default;
        }

        public void SetGlobalBounds(Bounds bounds) => brg.SetGlobalBounds(bounds);

        public void SetEnabledViewTypes(BatchCullingViewType[] viewTypes) => brg.SetEnabledViewTypes(viewTypes);

        public void SetCullingCallback(BatchRendererGroup.OnPerformCulling callback) => cullingCallback = callback;

        public void AddPropertyOverride(MaterialPropertyOverride property, int index) => overrides.AddAt(index, property);

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
            existingBatchesIndices.Add(batchIndex);
            batchInfos.AddAt(batchIndex, batchInfo);
            batchAABBs.AddAt(batchIndex, default);

            return true;
        }

        public void RemoveBatch(BatchID batchID)
        {
            var batchIndex = batchID.AsInt();
            var batchInfo = batchInfos[batchIndex];
            existingBatchesIndices.Remove(batchIndex);
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

        public ReadOnlyIntHashSet GetExistingBatchesIndices() => existingBatchesIndices.AsReadOnly();

        public unsafe BatchInfo* GetBatchInfosUnsafePtr() => batchInfos.GetUnsafePtr();

        public unsafe BatchAABB* GetBatchAABBsUnsafePtr() => batchAABBs.GetUnsafePtr();

        public unsafe ThreadedSparseUploader BeginUpload(SparseBufferUploadRequirements uploadRequirements)
        {
            bool uploadHeader = false;

            if (uploadHeaderBlitDescriptor.BytesRequiredInUploadBuffer == 0)
            {
                uploadHeaderBlitDescriptor = new ValueBlitDescriptor()
                {
                    value = float4x4.zero,
                    destinationOffset = (uint)bufferHeader.begin,
                    valueSizeBytes = SIZE_OF_MATRIX4X4,
                    count = 1
                };

                var numBytes = uploadHeaderBlitDescriptor.BytesRequiredInUploadBuffer;
                uploadRequirements.numOperations++;
                uploadRequirements.totalUploadBytes += numBytes;
                uploadRequirements.biggestUploadBytes = math.max(uploadRequirements.biggestUploadBytes, numBytes);
                uploadHeader = true;
            }

            var threadedSparseUploader = brgBuffer.Begin(uploadRequirements, out bool bufferResized);

            if (uploadHeader)
            {
                new UploadBufferHeaderJob()
                {
                    uploadDescriptor = uploadHeaderBlitDescriptor,
                    threadedSparseUploader = threadedSparseUploader
                }
                .Schedule().Complete();
            }

            if (bufferResized)
            {
                foreach (var batchID in existingBatchesIndices)
                {
                    brg.SetBatchBuffer(IntAsBatchID(batchID), brgBuffer.Handle);
                }
            }

            return threadedSparseUploader;
        }

        public void EndUploadAndCommit() => brgBuffer.EndAndCommit();

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

    [BurstCompile]
    internal unsafe struct UploadBufferHeaderJob : IJob
    {
        [ReadOnly]
        public ValueBlitDescriptor uploadDescriptor;
        public ThreadedSparseUploader threadedSparseUploader;

        public void Execute()
        {
            var blit = uploadDescriptor;
            threadedSparseUploader.AddUpload(&blit.value, (int)blit.valueSizeBytes, (int)blit.destinationOffset, (int)blit.count);
        }
    }
}

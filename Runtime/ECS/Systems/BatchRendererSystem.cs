using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.WorldAllocator;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public sealed unsafe class BatchRendererSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;
        private GraphicsArchetypesContext graphicsArchetypes;

        private ValueBlitDescriptor bufferHeaderBlitDescriptor;
        private ThreadLocalAllocator threadAllocator;

        public void OnAwake()
        {
            brg = EcsHelpers.GetBatchRendererGroupContext(World);
            brg.SetCullingCallback(OnPerformCulling);
            graphicsArchetypes = EcsHelpers.GetGraphicsArchetypesContext(World);
            bufferHeaderBlitDescriptor = default;
        }

        public void OnUpdate(float deltaTime)
        {
            //threadAllocator.Rewind();
            ExecuteGpuUploads();
        }

        public void Dispose()
        {

        }

        private void ExecuteGpuUploads()
        {
            var allocator = World.GetUpdateAllocator();

            var existingBatches = brg.GetExistingBatchesIndices();
            var totalBatchesCount = existingBatches.count;
            var totalOverridesCount = graphicsArchetypes.GetTotalArchetypePropertiesCount();

            int maximumGpuUploads = totalBatchesCount * totalOverridesCount;
            var gpuUploadOperations = allocator.AllocateNativeArray<GpuUploadOperation>(maximumGpuUploads, NativeArrayOptions.UninitializedMemory);
            var numGpuUploads = new NativeReference<int>(Allocator.TempJob);

            var nativeArchetypes = graphicsArchetypes.AsNative();

            new SetupGpuUploadOperationsJob()
            {
                batchesIndices = existingBatches.GetUnsafeDataPtr(),
                archetypes = nativeArchetypes.archetypes,
                properties = nativeArchetypes.properties,
                propertiesStashes = nativeArchetypes.propertiesStashes,
                batchesInfos = brg.GetBatchInfosUnsafePtr(),
                numGpuUploadOperations = numGpuUploads.GetUnsafePtr(),
                gpuUploadOperations = gpuUploadOperations
            }
            .ScheduleParallel(totalBatchesCount, 16, default).Complete();

            var uploadHeader = bufferHeaderBlitDescriptor.BytesRequiredInUploadBuffer == 0;
            var uploadRequirements = SparseBufferUtility.ComputeUploadSizeRequirements(numGpuUploads.Value, gpuUploadOperations);

            if (uploadHeader)
            {
                bufferHeaderBlitDescriptor = new ValueBlitDescriptor()
                {
                    value = float4x4.zero,
                    destinationOffset = 0u,
                    valueSizeBytes = SIZE_OF_MATRIX4X4,
                    count = 1
                };

                var numBytes = bufferHeaderBlitDescriptor.BytesRequiredInUploadBuffer;
                uploadRequirements.numOperations++;
                uploadRequirements.totalUploadBytes += numBytes;
                uploadRequirements.biggestUploadBytes = math.max(uploadRequirements.biggestUploadBytes, numBytes);
            }

            var brgBuffer = brg.GetBuffer();
            var threadedBufferUploader = brgBuffer.Begin(uploadRequirements, out bool bufferResized);

            var uploadGpuOperationsHandle = new ExecuteGpuUploadOperationsJob()
            {
                gpuUploadOperations = gpuUploadOperations,
                threadedSparseUploader = threadedBufferUploader
            }
            .ScheduleParallel(numGpuUploads.Value, 16, default);

            var uploadHeaderHandle = uploadHeader ? new UploadBufferHeaderJob()
            {
                uploadDescriptor = bufferHeaderBlitDescriptor,
                threadedSparseUploader = threadedBufferUploader
            }
            .Schedule() : default;

            if (bufferResized)
            {
                brg.UpdateBatchBufferHandles();
            }

            numGpuUploads.Dispose(default);
            JobHandle.CombineDependencies(uploadHeaderHandle, uploadGpuOperationsHandle).Complete();
            brgBuffer.EndAndCommit();
        }

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            //var visibilityItems = new IndirectList<>

            return default;
        }
    }

    [BurstCompile]
    internal unsafe struct SetupGpuUploadOperationsJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public int* batchesIndices;

        [NativeDisableUnsafePtrRestriction]
        public BatchInfo* batchesInfos;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [NativeDisableUnsafePtrRestriction]
        public ArchetypeProperty* properties;

        [NativeDisableUnsafePtrRestriction]
        public UnmanagedStash* propertiesStashes;

        [NativeDisableUnsafePtrRestriction]
        public int* numGpuUploadOperations;

        [NativeDisableParallelForRestriction]
        public NativeArray<GpuUploadOperation> gpuUploadOperations;

        public void Execute(int index)
        {
            ref var batchInfo = ref batchesInfos[batchesIndices[index]];
            ref var archetype = ref archetypes[batchInfo.archetypeIndex];

            var batchFilterOffset = archetype.maxEntitiesPerBatch * batchInfo.archetypeInternalIndex;
            var batchEntitiesCount = math.min(archetype.entities.length - batchFilterOffset, archetype.maxEntitiesPerBatch);
            var batchBegin = (int)batchInfo.batchGpuAllocation.begin;

            var propertyIndex = archetype.propertiesIndices[0];
            var dstOffset = batchBegin;
            var dstOffsetInverse = batchBegin + archetype.sourceMetadataStream[1];
            var sizeBytes = SIZE_OF_MATRIX3X4 * batchEntitiesCount;

            var uploadSrc = new UploadDataSource()
            {
                srcData = propertiesStashes + propertyIndex,
                filter = archetype.entities,
                filterOffset = batchFilterOffset,
                count = batchEntitiesCount
            };

            AddUpload(ref uploadSrc, sizeBytes, dstOffset, dstOffsetInverse, GpuUploadOperation.UploadOperationKind.SOAMatrixUpload3x4);

            for (int j = 2; j < archetype.propertiesIndices.Length; j++)
            {
                propertyIndex = archetype.propertiesIndices[j];
                ref var property = ref properties[propertyIndex];

                dstOffset = batchBegin + archetype.sourceMetadataStream[j];
                sizeBytes = property.size * batchEntitiesCount;

                uploadSrc = new UploadDataSource()
                {
                    srcData = propertiesStashes + propertyIndex,
                    filter = archetype.entities,
                    filterOffset = batchFilterOffset,
                    count = batchEntitiesCount
                };

                AddUpload(ref uploadSrc, sizeBytes, dstOffset, -1, GpuUploadOperation.UploadOperationKind.Memcpy);
            }
        }

        private unsafe void AddUpload(ref UploadDataSource src, int sizeBytes, int dstOffset, int dstOffsetInverse, GpuUploadOperation.UploadOperationKind kind)
        {
            int index = System.Threading.Interlocked.Add(ref *numGpuUploadOperations, 1) - 1;

            gpuUploadOperations[index] = new GpuUploadOperation
            {
                kind = kind,
                src = src,
                dstOffset = dstOffset,
                dstOffsetInverse = dstOffsetInverse,
                size = sizeBytes
            };
        }
    }

    [BurstCompile]
    internal struct ExecuteGpuUploadOperationsJob : IJobFor
    {
        [ReadOnly]
        public NativeArray<GpuUploadOperation> gpuUploadOperations;
        public ThreadedSparseUploader threadedSparseUploader;

        public void Execute(int index)
        {
            var operation = gpuUploadOperations[index];

            switch (operation.kind)
            {
                case GpuUploadOperation.UploadOperationKind.Memcpy:
                    threadedSparseUploader.AddUpload(ref operation.src, operation.dstOffset);
                    break;

                case GpuUploadOperation.UploadOperationKind.SOAMatrixUpload3x4:
                    threadedSparseUploader.AddMatrixUpload(ref operation.src, operation.dstOffset, operation.dstOffsetInverse);
                    break;

                default:
                    break;
            }
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

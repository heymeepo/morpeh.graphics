using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Workaround;
using Scellecs.Morpeh.Workaround.WorldAllocator;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
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

        private JobHandle cullingJobDependency;
        private JobHandle cullingJobReleaseDependency;

        private Stash<WorldRenderBounds> boundsStash;
        private Stash<MaterialMeshInfo> materialMeshInfosStash;
        private Stash<RenderFilterSettingsIndex> filterSettingsIndicesStash;

        public void OnAwake()
        {
            brg = BrgHelpersNonBursted.GetBatchRendererGroupContext(World);
            graphicsArchetypes = BrgHelpersNonBursted.GetGraphicsArchetypesContext(World);

            threadAllocator = new ThreadLocalAllocator(-1);
            bufferHeaderBlitDescriptor = default;

            brg.SetCullingCallback(OnPerformCulling);

            cullingJobDependency = default;
            cullingJobReleaseDependency = default;

            boundsStash = World.GetStash<WorldRenderBounds>();
            materialMeshInfosStash = World.GetStash<MaterialMeshInfo>();
            filterSettingsIndicesStash = World.GetStash<RenderFilterSettingsIndex>();
        }

        public void OnUpdate(float deltaTime)
        {
            CompleteAndResetDependencies();
            threadAllocator.Rewind();

            brg = BrgHelpersNonBursted.GetBatchRendererGroupContext(World);
            graphicsArchetypes = BrgHelpersNonBursted.GetGraphicsArchetypesContext(World);

            ExecuteGpuUploads();
        }

        public void Dispose()
        {
            CompleteAndResetDependencies();
            threadAllocator.Dispose();
        }

        private void ExecuteGpuUploads()
        {
            var existingBatches = brg.ExistingBatchesIndices;
            var batchesCount = existingBatches.count;

            if (batchesCount == 0)
            {
                return;
            }

            var allocator = World.GetUpdateAllocator();
            var totalOverridesCount = graphicsArchetypes.properties.Length;

            var maximumGpuUploads = batchesCount * totalOverridesCount;
            var gpuUploadOperations = allocator.AllocateNativeArray<GpuUploadOperation>(maximumGpuUploads, NativeArrayOptions.UninitializedMemory);
            var numGpuUploads = new NativeReference<int>(Allocator.TempJob);

            new SetupGpuUploadOperationsJob()
            {
                batchesIndices = existingBatches.GetUnsafeDataPtr(),
                archetypes = graphicsArchetypes.graphicsArchetypes,
                properties = (ArchetypeProperty*)graphicsArchetypes.properties.GetUnsafePtr(),
                propertiesStashes = (UnmanagedStash*)graphicsArchetypes.propertiesStashes.GetUnsafePtr(),
                batchesInfos = brg.BatchesInfosPtr,
                numGpuUploadOperations = numGpuUploads.GetUnsafePtr(),
                gpuUploadOperations = gpuUploadOperations
            }
            .ScheduleParallel(batchesCount, 16, default).Complete();

            var uploadHeader = bufferHeaderBlitDescriptor.BytesRequiredInUploadBuffer == 0;
            var uploadRequirements = SparseBufferUploadRequirements.ComputeUploadSizeRequirements(numGpuUploads.Value, gpuUploadOperations);

            if (uploadHeader)
            {
                bufferHeaderBlitDescriptor = new ValueBlitDescriptor()
                {
                    value = float4x4.zero,
                    destinationOffset = 0u,
                    valueSizeBytes = SIZE_OF_MATRIX4X4,
                    count = 1
                };

                uploadRequirements += SparseBufferUploadRequirements.ComputeUploadSizeRequirements(bufferHeaderBlitDescriptor);
            }

            var threadedBufferUploader = brg.Buffer.Begin(uploadRequirements, out bool bufferResized);
            var uploadGpuOperationsHandle = new ExecuteGpuUploadOperationsJob()
            {
                gpuUploadOperations = gpuUploadOperations,
                threadedSparseUploader = threadedBufferUploader
            }
            .ScheduleParallel(numGpuUploads.Value, 16, default);

            var uploadHeaderHandle = uploadHeader ? new UploadBufferHeaderJob()
            {
                uploadBlitDescriptor = bufferHeaderBlitDescriptor,
                threadedSparseUploader = threadedBufferUploader
            }
            .Schedule() : default;

            if (bufferResized)
            {
                brg.UpdateBatchBufferHandles();
            }

            numGpuUploads.Dispose(default);
            JobHandle.CombineDependencies(uploadHeaderHandle, uploadGpuOperationsHandle).Complete();
            brg.Buffer.EndAndCommit();
        }

        private void CompleteAndResetDependencies()
        {
            cullingJobDependency.Complete();
            cullingJobDependency = default;
            cullingJobReleaseDependency.Complete();
            cullingJobReleaseDependency = default;
        }

        private void DidScheduleCullingJob(JobHandle job) => cullingJobDependency = JobHandle.CombineDependencies(job, cullingJobDependency);

        private JobHandle OnPerformCulling(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            var existingBatches = brg.ExistingBatchesIndices;
            var batchesCount = existingBatches.count;

            if (batchesCount == 0)
            {
                return cullingJobDependency;
            }

            var maxVisibilityItemsCount = (int)math.ceil((float)batchesCount * MAX_INSTANCES_PER_BATCH / 128);
            var visibilityItems = new IndirectList<BatchVisibilityItem>(maxVisibilityItemsCount, threadAllocator.GeneralAllocator);
            var cullLightmapShadowCasters = (cullingContext.cullingFlags & BatchCullingFlags.CullLightmappedShadowCasters) != 0;

            var frustumCullingHandle = new FrustumCullingJob()
            {
                threadIndex = 0,
                batchesIndices = existingBatches.GetUnsafeDataPtr(),
                batchesInfos = brg.BatchesInfosPtr,
                archetypes = graphicsArchetypes.graphicsArchetypes,
                boundsStash = boundsStash.AsNative(),
                visibilityItems = visibilityItems,
                threadLocalAllocator = threadAllocator,
                cullingSplits = CullingSplits.Create(&cullingContext, QualitySettings.shadowProjection, threadAllocator.GeneralAllocator->Handle),
                cullingViewType = cullingContext.viewType,
                cullLightmapShadowCasters = cullLightmapShadowCasters
            }
            .ScheduleParallel(batchesCount, 8, cullingJobDependency);

            DidScheduleCullingJob(frustumCullingHandle);

            var drawCommandOutput = new DrawCommandOutput(1, threadAllocator, cullingOutput);

            var emitDrawCommandsJob = new EmitDrawCommandsJob
            {
                batchFilterSettings = brg.BatchFilterSettingsPtr,
                batchesInfos = brg.BatchesInfosPtr,
                archetypes = graphicsArchetypes.graphicsArchetypes,
                visibilityItems = visibilityItems,
                filterSettingsIndices = filterSettingsIndicesStash.AsNative(),
                materialMeshInfos = materialMeshInfosStash.AsNative(),
                cullingLayerMask = cullingContext.cullingLayerMask,
                drawCommandOutput = drawCommandOutput
            };

            var allocateWorkItemsJob = new AllocateWorkItemsJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var collectWorkItemsJob = new CollectWorkItemsJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var flushWorkItemsJob = new FlushWorkItemsJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var allocateInstancesJob = new AllocateInstancesJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var allocateDrawCommandsJob = new AllocateDrawCommandsJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var expandInstancesJob = new ExpandVisibleInstancesJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var generateDrawCommandsJob = new GenerateDrawCommandsJob
            {
                drawCommandOutput = drawCommandOutput
            };

            var generateDrawRangesJob = new GenerateDrawRangesJob
            {
                drawCommandOutput = drawCommandOutput,
                filterSettings = brg.BatchFilterSettingsPtr,
            };

            var emitDrawCommandsDependency = emitDrawCommandsJob.ScheduleWithIndirectList(visibilityItems, 1, cullingJobDependency);
            var collectGlobalBinsDependency = drawCommandOutput.BinCollector.ScheduleFinalize(emitDrawCommandsDependency);

            var sortBinsDependency = DrawBinSort.ScheduleBinSort(
                threadAllocator.GeneralAllocator,
                drawCommandOutput.SortedBins,
                drawCommandOutput.UnsortedBins,
                collectGlobalBinsDependency);

            var allocateWorkItemsDependency = allocateWorkItemsJob.Schedule(collectGlobalBinsDependency);
            var collectWorkItemsDependency = collectWorkItemsJob.ScheduleWithIndirectList(drawCommandOutput.UnsortedBins, 1, allocateWorkItemsDependency);
            var flushWorkItemsDependency = flushWorkItemsJob.Schedule(MAX_JOB_WORKERS, 1, collectWorkItemsDependency);
            var allocateInstancesDependency = allocateInstancesJob.Schedule(flushWorkItemsDependency);

            var allocateDrawCommandsDependency = allocateDrawCommandsJob.Schedule(
                JobHandle.CombineDependencies(sortBinsDependency, flushWorkItemsDependency));

            var allocationsDependency = JobHandle.CombineDependencies(
                allocateInstancesDependency,
                allocateDrawCommandsDependency);

            var expandInstancesDependency = expandInstancesJob.ScheduleWithIndirectList(
                drawCommandOutput.WorkItems,
                1,
                allocateInstancesDependency);

            var generateDrawCommandsDependency = generateDrawCommandsJob.ScheduleWithIndirectList(
                drawCommandOutput.SortedBins,
                1,
                allocationsDependency);

            var generateDrawRangesDependency = generateDrawRangesJob.Schedule(allocateDrawCommandsDependency);

            var expansionDependency = JobHandle.CombineDependencies(
                expandInstancesDependency,
                generateDrawCommandsDependency,
                generateDrawRangesDependency);

            cullingJobReleaseDependency = JobHandle.CombineDependencies(cullingJobReleaseDependency, drawCommandOutput.Dispose(expansionDependency));

            DidScheduleCullingJob(emitDrawCommandsDependency);
            DidScheduleCullingJob(expansionDependency);

            return cullingJobDependency;
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

            //ObjectToWorld and WorldToObject properties are always at 0 and 1 indices in any archetype

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

            for (int i = 2; i < archetype.propertiesIndices.Length; i++)
            {
                propertyIndex = archetype.propertiesIndices[i];
                ref var property = ref properties[propertyIndex];

                dstOffset = batchBegin + archetype.sourceMetadataStream[i];
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
            }
        }
    }

    [BurstCompile]
    internal unsafe struct UploadBufferHeaderJob : IJob
    {
        [ReadOnly]
        public ValueBlitDescriptor uploadBlitDescriptor;
        public ThreadedSparseUploader threadedSparseUploader;

        public void Execute()
        {
            var blit = uploadBlitDescriptor;
            threadedSparseUploader.AddUpload(&blit.value, (int)blit.valueSizeBytes, (int)blit.destinationOffset, (int)blit.count);
        }
    }
}

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

        private NativeList<ValueBlitDescriptor> valueBlits;

        private IntHashSet existingBatchIndices;
        private BitMap unreferencedBatchesIndices;

        private ThreadLocalAllocator threadAllocator;

        public void OnAwake()
        {
            brg = EcsHelpers.GetBatchRendererGroupContext(World);
            brg.SetCullingCallback(OnPerformCulling);
            graphicsArchetypes = EcsHelpers.GetGraphicsArchetypesContext(World);


            //InitializeSharedBatchRenderer();
        }

        public void OnUpdate(float deltaTime)
        {
            //threadAllocator.Rewind();
            ExecuteGpuUploads();
        }

        public void Dispose()
        {

        }

        //private void InitializeGraphicsBuffer()
        //{
        //    brgBuffer = new SparseBuffer(new SparseBufferArgs()
        //    {
        //        target = GraphicsBuffer.Target.Raw,
        //        flags = GraphicsBuffer.UsageFlags.None,
        //        initialSize = GPU_BUFFER_INITIAL_SIZE,
        //        maxSize = GPU_BUFFER_MAX_SIZE,
        //        stride = SIZE_OF_UINT,
        //        uploaderChunkSize = GPU_UPLOADER_CHUNK_SIZE
        //    });

        //    brgBuffer.Allocate(SIZE_OF_MATRIX4X4, 16, out var zeroAllocationHeader);

        //    valueBlits = new NativeList<ValueBlitDescriptor>(Allocator.Persistent)
        //    {
        //        new ValueBlitDescriptor()
        //        {
        //            value = float4x4.zero,
        //            destinationOffset = (uint)zeroAllocationHeader.begin,
        //            valueSizeBytes = SIZE_OF_MATRIX4X4,
        //            count = 1
        //        }
        //    };
        //}

        private void ScheduleUpdateBatchesRenderBounds()
        {
            //var archetypesIndices = graphicsArchetypes.GetUsedArchetypesIndices();
            //var archetypes = graphicsArchetypes.GetGraphicsArchetypesMap();

            //fixed (int* archetypesIndicesPtr = &archetypesIndices.data[0])
            //fixed (GraphicsArchetype* archetypesPtr = &archetypes.data[0])
            //{ 
                
            //}
        }

        private void ExecuteGpuUploads()
        {
            //var inputDeps = World.JobHandle;
            //var allocator = World.GetUpdateAllocator();

            //int maximumGpuUploads = existingBatchIndices.length * graphicsArchetypes.GetArchetypePropertiesCount();
            //var gpuUploadOperations = allocator.AllocateNativeArray<GpuUploadOperation>(maximumGpuUploads, NativeArrayOptions.UninitializedMemory);
            //var numGpuUploads = new NativeReference<int>(Allocator.TempJob);

            //var archetypesIndices = graphicsArchetypes.GetUsedArchetypesIndices();
            //var archetypes = graphicsArchetypes.GetGraphicsArchetypesMap();
            //var stashes = graphicsArchetypes.GetArchetypesPropertiesStashes();
            //var properties = graphicsArchetypes.GetArchetypesPropertiesMap();

            //fixed (int* archetypesIndicesPtr = &archetypesIndices.data[0])
            //fixed (GraphicsArchetype* archetypesPtr = &archetypes.data[0])
            //fixed (ArchetypeProperty* propertiesPtr = &properties.data[0])
            //{
            //    new SetupGpuUploadOperationsJob()
            //    {
            //        archetypesIndices = archetypesIndicesPtr,
            //        archetypes = archetypesPtr,
            //        properties = propertiesPtr,
            //        propertiesStashes = (UnmanagedStash*)stashes.GetUnsafePtr(),
            //        batchInfos = batchInfos.GetUnsafePtr(),
            //        numGpuUploadOperations = numGpuUploads.GetUnsafePtr(),
            //        gpuUploadOperations = gpuUploadOperations
            //    }
            //    .ScheduleParallel(archetypesIndices.length, 16, inputDeps).Complete();
            //}

            //var beginRequirements = SparseBufferUtility.ComputeUploadSizeRequirements(numGpuUploads.Value, gpuUploadOperations, valueBlits.AsArray());
            //var threadedBufferUploader = brgBuffer.Begin(beginRequirements, out bool bufferResized);

            //var uploadGpuOperationsHandle = new ExecuteGpuUploadOperationsJob()
            //{
            //    gpuUploadOperations = gpuUploadOperations,
            //    threadedSparseUploader = threadedBufferUploader
            //}
            //.ScheduleParallel(numGpuUploads.Value, 16, default);

            //var uploadValueBlitsHandle = new JobHandle();

            //if (valueBlits.Length > 0)
            //{
            //    uploadValueBlitsHandle = new UploadValueBlitsJob()
            //    {
            //        valueBlits = valueBlits,
            //        threadedSparseUploader = threadedBufferUploader
            //    }
            //    .Schedule(valueBlits.Length, default);
            //}

            //if (bufferResized)
            //{
            //    foreach (var batchID in existingBatchIndices)
            //    {
            //        brg.SetBatchBuffer(IntAsBatchID(batchID), brgBuffer.Handle);
            //    }
            //}

            //numGpuUploads.Dispose(default);
            //JobHandle.CombineDependencies(uploadGpuOperationsHandle, uploadValueBlitsHandle).Complete();
            //valueBlits.Clear();
            //brgBuffer.EndAndCommit();
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
    internal unsafe struct UpdateBatchesRenderBoundsJob : IJobFor
    {
        [NativeSetThreadIndex] 
        public int threadIndex;

        [NativeDisableUnsafePtrRestriction]
        public ThreadLocalAABB* localAABBs;

        [NativeDisableUnsafePtrRestriction]
        public int* archetypesIndices;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [NativeDisableUnsafePtrRestriction]
        public BatchAABB* batchAABBs;

        public NativeStash<LocalToWorld> localToWorldStash;

        public NativeStash<RenderBounds> renderBoundsStash;

        public NativeStash<WorldRenderBounds> worldRenderBoundsStash;

        public void Execute(int index)
        {
            var archetypeIndex = archetypesIndices[index];
            ref var archetype = ref archetypes[archetypeIndex];

            var entitiesCount = archetype.entities.length;
            var entitiesPerBatch = archetype.maxEntitiesPerBatch;
            var filter = archetype.entities;

            for (int i = 0; i < archetype.batchesIndices.Length; i++)
            {
                var srcFilterOffset = entitiesPerBatch * i;
                var srcCount = math.min(entitiesCount - srcFilterOffset, entitiesPerBatch);
                var combined = MinMaxAABB.Empty;

                for (int j = 0; j < srcCount; j++)
                {
                    var entityId = filter[srcFilterOffset + j];

                    ref var localBounds = ref renderBoundsStash.Get(entityId);
                    ref var worldBounds = ref worldRenderBoundsStash.Get(entityId);
                    ref var localToWorld = ref localToWorldStash.Get(entityId);

                    var transformed = AABB.Transform(localToWorld.value, localBounds.value);
                    worldBounds.value = transformed;
                    combined.Encapsulate(transformed);
                }

                batchAABBs[archetype.batchesIndices[i]].value = combined;
                var threadLocalAABB = localAABBs + threadIndex;
                ref var aabb = ref threadLocalAABB->AABB;
                aabb.Encapsulate(combined);
            }
        }
    }

    [BurstCompile]
    internal unsafe struct SetupGpuUploadOperationsJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public int* archetypesIndices;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [NativeDisableUnsafePtrRestriction]
        public ArchetypeProperty* properties;

        [NativeDisableUnsafePtrRestriction]
        public UnmanagedStash* propertiesStashes;

        [NativeDisableUnsafePtrRestriction]
        public BatchInfo* batchInfos;

        [NativeDisableUnsafePtrRestriction]
        public int* numGpuUploadOperations;

        [NativeDisableParallelForRestriction]
        public NativeArray<GpuUploadOperation> gpuUploadOperations;

        public void Execute(int index)
        {
            var archetypeIndex = archetypesIndices[index];
            ref var archetype = ref archetypes[archetypeIndex];

            var entitiesCount = archetype.entities.length;
            var entitiesPerBatch = archetype.maxEntitiesPerBatch;

            for (int i = 0; i < archetype.batchesIndices.Length; i++)
            {
                var batchInfo = batchInfos[archetype.batchesIndices[i]];
                var batchBegin = (int)batchInfo.batchGpuAllocation.begin;

                var srcFilterOffset = entitiesPerBatch * i;
                var srcCount = math.min(entitiesCount - srcFilterOffset, entitiesPerBatch);

                {
                    var propertyIndex = archetype.propertiesIndices[0];
                    var dstOffset = batchBegin;
                    var dstOffsetInverse = batchBegin + archetype.sourceMetadataStream[1];
                    var sizeBytes = SIZE_OF_MATRIX3X4 * srcCount;

                    var uploadSrc = new UploadDataSource()
                    {
                        srcData = propertiesStashes + propertyIndex,
                        filter = archetype.entities,
                        filterOffset = srcFilterOffset,
                        count = srcCount
                    };

                    AddUpload(ref uploadSrc, sizeBytes, dstOffset, dstOffsetInverse, GpuUploadOperation.UploadOperationKind.SOAMatrixUpload3x4);
                }

                for (int j = 2; j < archetype.propertiesIndices.Length; j++)
                {
                    var propertyIndex = archetype.propertiesIndices[j];
                    ref var property = ref properties[propertyIndex];

                    var dstOffset = batchBegin + archetype.sourceMetadataStream[j];
                    var sizeBytes = property.size * srcCount;
                    var operationKind = GpuUploadOperation.UploadOperationKind.Memcpy;

                    var uploadSrc = new UploadDataSource()
                    {
                        srcData = propertiesStashes + propertyIndex,
                        filter = archetype.entities,
                        filterOffset = srcFilterOffset,
                        count = srcCount
                    };

                    AddUpload(ref uploadSrc, sizeBytes, dstOffset, -1, operationKind);
                }
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
    internal unsafe struct UploadValueBlitsJob : IJobFor
    {
        [ReadOnly]
        public NativeList<ValueBlitDescriptor> valueBlits;

        public ThreadedSparseUploader threadedSparseUploader;

        public void Execute(int index)
        {
            var blit = valueBlits[index];
            threadedSparseUploader.AddUpload(&blit.value, (int)blit.valueSizeBytes, (int)blit.destinationOffset, (int)blit.count);
        }
    }
}

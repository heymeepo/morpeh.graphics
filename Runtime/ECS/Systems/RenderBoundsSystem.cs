using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround.WorldAllocator;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using static Scellecs.Morpeh.Graphics.Utilities.BrgHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public unsafe sealed class RenderBoundsSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;
        private GraphicsArchetypesContext graphicsArchetypes;

        private Stash<RenderBounds> renderBoundsStash;
        private Stash<WorldRenderBounds> worldRenderBoundsStash;
        private Stash<LocalToWorld> localToWorldStash;

        public void OnAwake()
        {
            brg = EcsHelpers.GetBatchRendererGroupContext(World);
            graphicsArchetypes = EcsHelpers.GetGraphicsArchetypesContext(World);

            renderBoundsStash = World.GetStash<RenderBounds>();
            worldRenderBoundsStash = World.GetStash<WorldRenderBounds>();
            localToWorldStash = World.GetStash<LocalToWorld>();
        }

        public void OnUpdate(float deltaTime)
        {
            var allocator = World.GetUpdateAllocator();
            var threadLocalAABBs = allocator.Allocate<ThreadLocalAABB>(MAX_JOB_WORKERS, NativeArrayOptions.UninitializedMemory);

            var zeroAABBHandle = new ZeroThreadLocalAABBJob()
            {
                threadLocalAABBs = threadLocalAABBs
            }
            .ScheduleParallel(MAX_JOB_WORKERS, 16, default);
            ThreadLocalAABB.AssertCacheLineSize();

            var nativeArchetypes = graphicsArchetypes.AsNative();
            var batchesIndices = brg.GetExistingBatchesIndices();
            var batchInfos = brg.GetBatchInfosUnsafePtr();

            var updateRenderBoundsHandle = new UpdateRenderBoundsJob()
            {
                threadIndex = 0,
                localAABBs = threadLocalAABBs,
                batchesIndices = batchesIndices.GetUnsafeDataPtr(),
                archetypes = nativeArchetypes.archetypes,
                batchesInfos = batchInfos,
                localToWorldStash = localToWorldStash.AsNative(),
                renderBoundsStash = renderBoundsStash.AsNative(),
                worldRenderBoundsStash = worldRenderBoundsStash.AsNative()
            }
            .ScheduleParallel(batchesIndices.count, 16, zeroAABBHandle);
            updateRenderBoundsHandle.Complete();

            MinMaxAABB aabb = MinMaxAABB.Empty;

            for (int i = 0; i < MAX_JOB_WORKERS; ++i)
            {
                aabb.Encapsulate(threadLocalAABBs[i].AABB);
            }

            var centerExtentsAABB = (AABB)aabb;
            brg.SetGlobalBounds(centerExtentsAABB.ToBounds());
        }

        public void Dispose() { }
    }

    [BurstCompile]
    internal unsafe struct ZeroThreadLocalAABBJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public ThreadLocalAABB* threadLocalAABBs;

        public void Execute(int index)
        {
            var threadLocalAABB = threadLocalAABBs + index;
            threadLocalAABB->AABB = MinMaxAABB.Empty;
        }
    }

    [BurstCompile]
    internal unsafe struct UpdateRenderBoundsJob : IJobFor
    {
        [NativeSetThreadIndex]
        public int threadIndex;

        [NativeDisableUnsafePtrRestriction]
        public ThreadLocalAABB* localAABBs;

        [NativeDisableUnsafePtrRestriction]
        public int* batchesIndices;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [NativeDisableUnsafePtrRestriction]
        public BatchInfo* batchesInfos;

        public NativeStash<LocalToWorld> localToWorldStash;

        public NativeStash<RenderBounds> renderBoundsStash;

        public NativeStash<WorldRenderBounds> worldRenderBoundsStash;

        public void Execute(int index)
        {
            ref var batchInfo = ref batchesInfos[batchesIndices[index]];
            ref var archetype = ref archetypes[batchInfo.archetypeIndex];

            var batchFilterOffset = archetype.maxEntitiesPerBatch * batchInfo.archetypeInternalIndex;
            var batchEntitiesCount = math.min(archetype.entities.length - batchFilterOffset, archetype.maxEntitiesPerBatch);
            var filter = archetype.entities;

            var combined = MinMaxAABB.Empty;

            for (int i = 0; i < batchEntitiesCount; i++)
            {
                var entityId = filter[batchFilterOffset + i];

                ref var localBounds = ref renderBoundsStash.Get(entityId);
                ref var worldBounds = ref worldRenderBoundsStash.Get(entityId);
                ref var localToWorld = ref localToWorldStash.Get(entityId);

                var transformed = AABB.Transform(localToWorld.value, localBounds.value);
                worldBounds.value = transformed;
                combined.Encapsulate(transformed);
            }

            batchInfo.batchAABB = combined;
            var threadLocalAABB = localAABBs + threadIndex;
            ref var aabb = ref threadLocalAABB->AABB;
            aabb.Encapsulate(combined);
        }
    }
}

using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Culling
{
    [BurstCompile]
    internal unsafe struct FrustumCullingJob : IJobFor
    {
        [NativeSetThreadIndex]
        public int threadIndex;

        [NativeDisableUnsafePtrRestriction]
        public int* batchesIndices;

        [NativeDisableUnsafePtrRestriction]
        public BatchInfo* batchesInfos;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        public NativeStash<WorldRenderBounds> boundsStash;

        public IndirectList<BatchVisibilityItem> visibilityItems;
        public ThreadLocalAllocator threadLocalAllocator;
        public CullingSplits cullingSplits;
        public BatchCullingViewType cullingViewType;

        //public bool CullLightmapShadowCasters;
        //[ReadOnly] public SharedComponentTypeHandle<LightMaps> LightMaps;

        public void Execute(int index)
        {
            var batchIndex = batchesIndices[index];
            ref var batchInfo = ref batchesInfos[batchIndex];
            ref var archetype = ref archetypes[batchInfo.archetypeIndex];

            var batchBounds = batchInfo.batchAABB;
            var allocator = threadLocalAllocator.ThreadAllocator(threadIndex);

            var batchFilterOffset = archetype.maxEntitiesPerBatch * batchInfo.archetypeInternalIndex;
            var batchEntitiesCount = math.min(archetype.entities.length - batchFilterOffset, archetype.maxEntitiesPerBatch);
            var batchChunks128Count = (int)math.ceil((float)batchEntitiesCount / 128);

            // Filter out entities that affect lightmap if the cull lighmap shadow casters flag is set
            //bool isLightMapped = chunk.GetSharedComponentIndex(LightMaps) >= 0;
            //if (isLightMapped && CullLightmapShadowCasters)
            //    return;

            bool isLightCulling = cullingViewType == BatchCullingViewType.Light;
            var visibilityItemWriter = visibilityItems.List->AsParallelWriter();

            if (isLightCulling)
            {
                var useSphereTest = cullingSplits.SphereTestEnabled;
                ref var receiverPlanes = ref cullingSplits.ReceiverPlanePackets;
                bool haveReceiverPlanes = cullingSplits.ReceiverPlanePackets.Length > 0;

                if (haveReceiverPlanes)
                {
                    if (FrustumPlanes.Intersect2NoPartial(receiverPlanes.AsNativeArray(), batchBounds) == FrustumPlanes.IntersectResult.Out)
                    {
                        return;
                    }
                }

                int visibleSplitMask = useSphereTest ? cullingSplits.ReceiverSphereCuller.Cull(batchBounds) : ~0;
                ref var splits = ref cullingSplits.Splits;
                var batchIntersection = stackalloc FrustumPlanes.IntersectResult[splits.Length];

                for (int splitIndex = 0; splitIndex < splits.Length; splitIndex++)
                {
                    var s = splits[splitIndex];
                    var splitPlanes = cullingSplits.SplitPlanePackets.GetSubNativeArray(s.PlanePacketOffset, s.PlanePacketCount);
                    batchIntersection[splitIndex] = (visibleSplitMask & (1 << splitIndex)) == 0 ? FrustumPlanes.IntersectResult.Out : FrustumPlanes.Intersect2(splitPlanes, batchBounds);
                }

                for (int i = 0; i < batchChunks128Count; i++)
                {
                    BatchVisibility128 batchVisibility;

                    batchVisibility.visibleEntities[0] = 0;
                    batchVisibility.visibleEntities[1] = 0;

                    var batchChunkOffset = i * 128;
                    var entitiesCount = math.min(batchEntitiesCount - batchChunkOffset, 128);
                    var enabledMask = BrgHelpers.ComputeBitmask(entitiesCount);

                    var batchChunkFilterOffset = batchFilterOffset + batchChunkOffset;
                    var filter = archetype.entities;

                    FrustumCullWithReceiverAndSphereCulling(batchIntersection, entitiesCount, batchChunkFilterOffset, filter, enabledMask, &batchVisibility, useSphereTest);

                    if (batchVisibility.AnyVisible)
                    {
                        var visibilityItem = new BatchVisibilityItem
                        {
                            batchIndex = batchIndex,
                            batchChunkBegin = batchChunkOffset,
                            visibility = (BatchVisibility128*)allocator->Allocate(UnsafeUtility.SizeOf<BatchVisibility128>(), UnsafeUtility.AlignOf<BatchVisibility128>(), 1),
                        };

                        UnsafeUtility.MemCpy(visibilityItem.visibility, &batchVisibility, UnsafeUtility.SizeOf<BatchVisibility128>());
                        visibilityItemWriter.AddNoResize(visibilityItem);
                    }
                }
            }
            else
            {
                var batchIn = FrustumPlanes.Intersect2(cullingSplits.SplitPlanePackets.AsNativeArray(), batchBounds);

                for (int i = 0; i < batchChunks128Count; i++)
                {
                    BatchVisibility128 batchVisibility;

                    batchVisibility.visibleEntities[0] = 0;
                    batchVisibility.visibleEntities[1] = 0;

                    var batchChunkOffset = i * 128;
                    var entitiesCount = math.min(batchEntitiesCount - batchChunkOffset, 128);
                    var enabledMask = BrgHelpers.ComputeBitmask(entitiesCount);

                    var batchChunkFilterOffset = batchFilterOffset + batchChunkOffset;
                    var filter = archetype.entities;

                    FrustumCull(batchIn, batchChunkFilterOffset, filter, enabledMask, &batchVisibility);

                    if (batchVisibility.AnyVisible)
                    {
                        var visibilityItem = new BatchVisibilityItem
                        {
                            batchIndex = batchIndex,
                            batchChunkBegin = batchChunkOffset,
                            visibility = (BatchVisibility128*)allocator->Allocate(UnsafeUtility.SizeOf<BatchVisibility128>(), UnsafeUtility.AlignOf<BatchVisibility128>(), 1),
                        };

                        UnsafeUtility.MemCpy(visibilityItem.visibility, &batchVisibility, UnsafeUtility.SizeOf<BatchVisibility128>());
                        visibilityItemWriter.AddNoResize(visibilityItem);
                    }
                }
            }
        }

        private void FrustumCull(
            FrustumPlanes.IntersectResult batchIn,
            int batchChunkFilterOffset,
            NativeFilter filter,
            v128 enabledMask128,
            BatchVisibility128* batchVisibility)
        {
            if (batchIn == FrustumPlanes.IntersectResult.Partial)
            {
                for (int j = 0; j < 2; j++)
                {
                    var enabledMask = j == 0 ? enabledMask128.ULong0 : enabledMask128.ULong1;
                    var lodWord = enabledMask;
                    ulong visibleWord = 0;
                    UnityEngine.Debug.Assert(true);
                    while (lodWord != 0)
                    {
                        var bitIndex = math.tzcnt(lodWord);
                        var entityIndex = (j << 6) + bitIndex;

                        var entityId = filter[batchChunkFilterOffset + entityIndex];
                        var bounds = boundsStash.Get(entityId);
                        var splitPlanePackets = cullingSplits.SplitPlanePackets.AsNativeArray();

                        int visible = FrustumPlanes.Intersect2NoPartial(splitPlanePackets, bounds.value) != FrustumPlanes.IntersectResult.Out ? 1 : 0;

                        lodWord ^= 1ul << bitIndex;
                        visibleWord |= (ulong)visible << bitIndex;
                    }

                    batchVisibility->visibleEntities[j] = visibleWord;
                }
            }
            else if (batchIn == FrustumPlanes.IntersectResult.In)
            {
                for (int j = 0; j < 2; j++)
                {
                    var enabledMask = j == 0 ? enabledMask128.ULong0 : enabledMask128.ULong1;
                    batchVisibility->visibleEntities[j] = enabledMask;
                }
            }
        }

        private void FrustumCullWithReceiverAndSphereCulling(
            FrustumPlanes.IntersectResult* batchIntersection,
            int numEntities,
            int batchChunkFilterOffset,
            NativeFilter filter,
            v128 enabledMask128,
            BatchVisibility128* batchVisibility,
            bool useSphereTest)
        {
            UnsafeUtility.MemSet(batchVisibility->splitMasks, 0, numEntities);
            ref var splits = ref cullingSplits.Splits;

            for (int splitIndex = 0; splitIndex < splits.Length; ++splitIndex)
            {
                var s = splits[splitIndex];
                byte splitMask = (byte)(1 << splitIndex);
                var batchIn = batchIntersection[splitIndex];

                if (batchIn == FrustumPlanes.IntersectResult.Partial)
                {
                    var combinedSplitPlanes = cullingSplits.CombinedSplitAndReceiverPlanePackets.GetSubNativeArray(s.CombinedPlanePacketOffset, s.CombinedPlanePacketCount);

                    for (int j = 0; j < 2; j++)
                    {
                        var enabledMask = j == 0 ? enabledMask128.ULong0 : enabledMask128.ULong1;
                        var lodWord = enabledMask;
                        ulong visibleWord = 0;

                        while (lodWord != 0)
                        {
                            var bitIndex = math.tzcnt(lodWord);
                            var entityIndex = (j << 6) + bitIndex;
                            ulong mask = 1ul << bitIndex;

                            var entityId = filter[batchChunkFilterOffset + entityIndex];
                            var bounds = boundsStash.Get(entityId);
                            int visible = FrustumPlanes.Intersect2NoPartial(combinedSplitPlanes, bounds.value) != FrustumPlanes.IntersectResult.Out ? 1 : 0;

                            lodWord ^= mask;
                            visibleWord |= ((ulong)visible) << bitIndex;

                            if (visible != 0)
                                batchVisibility->splitMasks[entityIndex] |= splitMask;
                        }

                        batchVisibility->visibleEntities[j] |= visibleWord;
                    }
                }
                else if (batchIn == FrustumPlanes.IntersectResult.In)
                {
                    batchVisibility->visibleEntities[0] |= enabledMask128.ULong0;
                    batchVisibility->visibleEntities[1] |= enabledMask128.ULong1;

                    for (int i = 0; i < numEntities; ++i)
                    {
                        batchVisibility->splitMasks[i] |= splitMask;
                    }
                }
            }

            // If anything survived the culling, perform sphere testing for each split
            if (useSphereTest && batchVisibility->AnyVisible)
            {
                for (int j = 0; j < 2; j++)
                {
                    ulong visibleWord = batchVisibility->visibleEntities[j];

                    while (visibleWord != 0)
                    {
                        int bitIndex = math.tzcnt(visibleWord);
                        int entityIndex = (j << 6) + bitIndex;
                        ulong mask = 1ul << bitIndex;

                        var entityId = filter[batchChunkFilterOffset + entityIndex];
                        var bounds = boundsStash.Get(entityId);

                        int planeSplitMask = batchVisibility->splitMasks[entityIndex];
                        int sphereSplitMask = cullingSplits.ReceiverSphereCuller.Cull(bounds.value);

                        byte newSplitMask = (byte)(planeSplitMask & sphereSplitMask);
                        batchVisibility->splitMasks[entityIndex] = newSplitMask;

                        if (newSplitMask == 0)
                        {
                            batchVisibility->visibleEntities[j] ^= mask;
                        }

                        visibleWord ^= mask;
                    }
                }
            }
        }
    }
}

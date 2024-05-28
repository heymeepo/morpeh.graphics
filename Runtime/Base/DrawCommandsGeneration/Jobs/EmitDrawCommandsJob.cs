using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Native;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    internal unsafe struct EmitDrawCommandsJob : IJobParallelForDefer
    {
        [NativeDisableUnsafePtrRestriction]
        public BatchFilterSettings* batchFilterSettings;

        [NativeDisableUnsafePtrRestriction]
        public BatchInfo* batchesInfos;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [ReadOnly] 
        public IndirectList<BatchVisibilityItem> visibilityItems;

        [ReadOnly]
        public NativeStash<RenderFilterSettingsIndex> filterSettingsIndices;

        [ReadOnly]
        public NativeStash<MaterialMeshInfo> materialMeshInfos;

        [ReadOnly]
        public uint cullingLayerMask;

        public DrawCommandOutput drawCommandOutput;

        public void Execute(int index)
        {
            var visibilityItem = visibilityItems.ElementAt(index);
            var batchVisibility128 = visibilityItem.visibility;

            drawCommandOutput.InitializeForEmitThread();

            int batchIndex = visibilityItem.batchIndex;
            int batchChunkBegin = visibilityItem.batchChunkBegin;

            ref var batchInfo = ref batchesInfos[batchIndex];
            ref var archetype = ref archetypes[batchInfo.archetypeIndex];

            var batchChunkFilterOffset = archetype.maxEntitiesPerBatch * batchInfo.archetypeInternalIndex + batchChunkBegin;
            var filter = archetype.entities;

            //bool isDepthSorted = chunk.Has(ref DepthSorted);
            //bool isLightMapped = chunk.GetSharedComponentIndex(LightMaps) >= 0;

            for (int j = 0; j < 2; j++)
            {
                ulong visibleWord = batchVisibility128->visibleEntities[j];

                while (visibleWord != 0)
                {
                    int bitIndex = math.tzcnt(visibleWord);
                    int entityIndex = (j << 6) + bitIndex;
                    ulong entityMask = 1ul << bitIndex;

                    var entityId = filter[batchChunkFilterOffset + entityIndex];

                    visibleWord ^= entityMask;

                    var filterIndex = filterSettingsIndices.Get(entityId).index;
                    ref var filterSettings = ref batchFilterSettings[filterIndex];

                    if (((1 << filterSettings.layer) & cullingLayerMask) == 0)
                    {
                        continue;
                    }

                    var materialMeshInfo = materialMeshInfos.Get(entityId);
                    var batchID = new BatchID { value = (uint)batchIndex };
                    ushort splitMask = batchVisibility128->splitMasks[entityIndex];
                    //bool flipWinding = (chunkCullingData.FlippedWinding[j] & entityMask) != 0;

                    BatchDrawCommandFlags drawCommandFlags = 0;

                    //if (flipWinding)
                    //    drawCommandFlags |= BatchDrawCommandFlags.FlipWinding;

                    //if (isDepthSorted)
                    //    drawCommandFlags |= BatchDrawCommandFlags.HasSortingPosition;

                    BatchMeshID meshID = materialMeshInfo.meshID;
                    BatchMaterialID materialID = materialMeshInfo.materialID;

                    var settings = new DrawCommandSettings
                    {
                        FilterIndex = filterIndex,
                        BatchID = batchID,
                        MaterialID = materialID,
                        MeshID = meshID,
                        SplitMask = splitMask,
                        SubMeshIndex = (ushort)materialMeshInfo.submeshIndex,
                        Flags = drawCommandFlags
                    };

                    EmitDrawCommand(settings, j, bitIndex, batchChunkBegin);
                }
            }
        }

        private void EmitDrawCommand(in DrawCommandSettings settings, int entityQword, int entityBit, int chunkStartIndex/*, NativeArray<LocalToWorld> localToWorlds*/)
        {
            //Expand LocalToWorld hashmap into an array in the RenderBoundsSystem with threaded rewindable allocator for each index in the NativeFilter?
            //Do the same for WorldRenderBonds instead of components usage?
            //Alternatively some rework in the ExpandVisibleInstancesJob

            // Depth sorted draws are emitted with access to entity transforms,
            // so they can also be written out for sorting
            //if (settings.HasSortingPosition)
            //{
            //    DrawCommandOutput.EmitDepthSorted(settings, entityQword, entityBit, chunkStartIndex,
            //        (float4x4*)localToWorlds.GetUnsafeReadOnlyPtr());
            //}
            //else
            //{
                drawCommandOutput.Emit(settings, entityQword, entityBit, chunkStartIndex);
            //}
        }
    }
}

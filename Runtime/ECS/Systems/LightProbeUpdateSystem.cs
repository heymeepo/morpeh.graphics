using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class LightProbeUpdateSystem : ICleanupSystem
    {
        public World World { get; set; }

        private CalculateInterpolatedLightAndOcclusionProbesDelegate CalculateInterpolatedLightAndOcclusionProbes;

        private Vector3[] positions = new Vector3[512];
        private Vector4[] occlusionProbes = new Vector4[512];
        private SphericalHarmonicsL2[] lightProbes = new SphericalHarmonicsL2[512];

        private Filter probeGridFilter;
        private Filter probeGridAnchorFilter;

        private Stash<BuiltinMaterialPropertyUnity_SHCoefficients> SHStash;
        private Stash<WorldRenderBounds> renderBoundsStash;
        private Stash<BlendProbeTag> blendProbesStash;
        private Stash<LocalToWorld> localToWorldStash;
        private Stash<OverrideLightProbeAnchor> overrideLightProbeAnchorStash;

        public void OnAwake()
        {
            CalculateInterpolatedLightAndOcclusionProbes = LightProbeHelpers.BakeCalculateInterpolatedLightAndOcclusionProbesDelegate();

            probeGridFilter = World.Filter
                .With<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .With<WorldRenderBounds>()
                .With<BlendProbeTag>()
                .Without<OverrideLightProbeAnchor>()
                .Build();

            probeGridAnchorFilter = World.Filter
                .With<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .With<WorldRenderBounds>()
                .With<BlendProbeTag>()
                .With<OverrideLightProbeAnchor>()
                .Build();

            SHStash = World.GetStash<BuiltinMaterialPropertyUnity_SHCoefficients>();
            renderBoundsStash = World.GetStash<WorldRenderBounds>();
            blendProbesStash = World.GetStash<BlendProbeTag>();
            localToWorldStash = World.GetStash<LocalToWorld>();
            overrideLightProbeAnchorStash = World.GetStash<OverrideLightProbeAnchor>();
        }

        public void OnUpdate(float deltaTime)
        {
            if (LightProbeHelpers.IsValidLightProbeGrid())
            {
                UpdateGrid();
                UpdateGridAnchor();
            }
        }

        private unsafe void UpdateGrid()
        {
            if (probeGridFilter.IsEmpty())
            {
                return;
            }

            var nativeFilter = probeGridFilter.AsNative();
            var positionsCount = nativeFilter.length;

            EnsureCapacity(positionsCount);

            fixed (Vector3* positionsPtr = &positions[0])
            {
                new FillPositionsJob()
                {
                    positions = positionsPtr,
                    filter = nativeFilter,
                    renderBounds = renderBoundsStash.AsNative()
                }
                .ScheduleParallel(positionsCount, 64, default).Complete();
            }

            UploadSHCoefficients(nativeFilter);
        }

        private unsafe void UpdateGridAnchor()
        {
            if (probeGridAnchorFilter.IsEmpty())
            {
                return;
            }

            var nativeFilter = probeGridAnchorFilter.AsNative();
            var positionsCount = nativeFilter.length;

            EnsureCapacity(positionsCount);

            fixed (Vector3* positionsPtr = &positions[0])
            {
                new FillPositionsOverridedJob()
                {
                    positions = positionsPtr,
                    filter = nativeFilter,
                    localToWorlds = localToWorldStash.AsNative(),
                    overridesStash = overrideLightProbeAnchorStash.AsNative()
                }
                .ScheduleParallel(positionsCount, 64, default).Complete();
            }

            UploadSHCoefficients(nativeFilter);
        }

        private unsafe void UploadSHCoefficients(NativeFilter filter)
        {
            CalculateInterpolatedLightAndOcclusionProbes(positions, filter.length, lightProbes, occlusionProbes);

            fixed (Vector4* occlusionProbesPtr = &occlusionProbes[0])
            fixed (SphericalHarmonicsL2* lightProbesPtr = &lightProbes[0])
            {
                new UploadSHCoefficientsJob()
                {
                    occlusionProbes = occlusionProbesPtr,
                    lightProbes = lightProbesPtr,
                    filter = filter,
                    SHStash = SHStash.AsNative()
                }
                .ScheduleParallel(filter.length, 64, default).Complete();
            }
        }

        private void EnsureCapacity(int count)
        {
            int newSize = positions.Length;

            if (positions.Length < count)
            {
                while (newSize < count)
                {
                    newSize <<= 1;
                }

                Array.Resize(ref positions, newSize);
                Array.Resize(ref lightProbes, newSize);
                Array.Resize(ref occlusionProbes, newSize);
            }
        }

        public void Dispose() { }
    }

    [BurstCompile]
    internal unsafe struct FillPositionsJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public Vector3* positions;

        public NativeFilter filter;
        public NativeStash<WorldRenderBounds> renderBounds;

        public void Execute(int index)
        {
            var entityId = filter[index];
            ref var bounds = ref renderBounds.Get(entityId);
            positions[index] = bounds.value.Center;
        }
    }

    [BurstCompile]
    internal unsafe struct FillPositionsOverridedJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public Vector3* positions;

        public NativeFilter filter;
        public NativeStash<LocalToWorld> localToWorlds;
        public NativeStash<OverrideLightProbeAnchor> overridesStash;

        public void Execute(int index)
        {
            var entityId = filter[index];
            var targetEntitiyId = overridesStash.Get(entityId).entity;
            ref var localToWorld = ref localToWorlds.Get(targetEntitiyId, out bool exists);
            positions[index] = exists ? localToWorld.Position : Vector3.zero;
        }
    }

    [BurstCompile]
    internal unsafe struct UploadSHCoefficientsJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        public Vector4* occlusionProbes;

        [NativeDisableUnsafePtrRestriction]
        public SphericalHarmonicsL2* lightProbes;

        public NativeFilter filter;
        public NativeStash<BuiltinMaterialPropertyUnity_SHCoefficients> SHStash;

        public void Execute(int index)
        {
            var entityId = filter[index];
            var shCoefficients = new SHCoefficients(lightProbes[index], occlusionProbes[index]);
            ref var sh = ref SHStash.Get(entityId);
            sh.value = shCoefficients;
        }
    }
}

using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Culling
{
    internal unsafe struct CullingSplits
    {
        public UnsafeList<Plane> BackfacingReceiverPlanes;
        public UnsafeList<FrustumPlanes.PlanePacket4> SplitPlanePackets;
        public UnsafeList<FrustumPlanes.PlanePacket4> ReceiverPlanePackets;
        public UnsafeList<FrustumPlanes.PlanePacket4> CombinedSplitAndReceiverPlanePackets;
        public UnsafeList<CullingSplitData> Splits;
        public ReceiverSphereCuller ReceiverSphereCuller;
        public bool SphereTestEnabled;

        public static CullingSplits Create(BatchCullingContext* cullingContext, ShadowProjection shadowProjection, AllocatorManager.AllocatorHandle allocator)
        {
            CullingSplits cullingSplits = default;

            var createJob = new CreateJob
            {
                cullingContext = cullingContext,
                shadowProjection = shadowProjection,
                allocator = allocator,
                Splits = &cullingSplits
            };
            createJob.Run();

            return cullingSplits;
        }

        [BurstCompile]
        private struct CreateJob : IJob
        {
            [NativeDisableUnsafePtrRestriction]
            [ReadOnly] public BatchCullingContext* cullingContext;
            [ReadOnly] public ShadowProjection shadowProjection;
            [ReadOnly] public AllocatorManager.AllocatorHandle allocator;

            [NativeDisableUnsafePtrRestriction]
            public CullingSplits* Splits;

            public void Execute()
            {
                *Splits = new CullingSplits(ref *cullingContext, shadowProjection, allocator);
            }
        }

        private CullingSplits(ref BatchCullingContext cullingContext,
            ShadowProjection shadowProjection,
            AllocatorManager.AllocatorHandle allocator)
        {
            BackfacingReceiverPlanes = default;
            SplitPlanePackets = default;
            ReceiverPlanePackets = default;
            CombinedSplitAndReceiverPlanePackets = default;
            Splits = default;
            ReceiverSphereCuller = default;
            SphereTestEnabled = false;

            // Initialize receiver planes first, so they are ready to be combined in
            // InitializeSplits
            InitializeReceiverPlanes(ref cullingContext, allocator);
            InitializeSplits(ref cullingContext, allocator);
            InitializeSphereTest(ref cullingContext, shadowProjection);
        }

        private void InitializeReceiverPlanes(ref BatchCullingContext cullingContext, AllocatorManager.AllocatorHandle allocator)
        {
#if DISABLE_HYBRID_RECEIVER_CULLING
            bool disableReceiverCulling = true;
#else
            bool disableReceiverCulling = false;
#endif
            // Receiver culling is only used for shadow maps
            if ((cullingContext.viewType != BatchCullingViewType.Light) ||
                (cullingContext.receiverPlaneCount == 0) ||
                disableReceiverCulling)
            {
                // Make an empty array so job system doesn't complain.
                ReceiverPlanePackets = new UnsafeList<FrustumPlanes.PlanePacket4>(0, allocator);
                return;
            }

            bool isOrthographic = cullingContext.projectionType == BatchCullingProjectionType.Orthographic;
            int numPlanes = 0;

            var planes = cullingContext.cullingPlanes.GetSubArray(
                cullingContext.receiverPlaneOffset,
                cullingContext.receiverPlaneCount);
            BackfacingReceiverPlanes = new UnsafeList<Plane>(planes.Length, allocator);
            BackfacingReceiverPlanes.Resize(planes.Length);

            float3 lightDir = ((float4)cullingContext.localToWorldMatrix.GetColumn(2)).xyz;
            Vector3 lightPos = cullingContext.localToWorldMatrix.GetPosition();

            for (int i = 0; i < planes.Length; ++i)
            {
                var p = planes[i];
                float3 n = p.normal;

                const float kEpsilon = (float)1e-12;

                // Compare with epsilon so that perpendicular planes are not counted
                // as back facing
                bool isBackfacing = isOrthographic
                    ? math.dot(n, lightDir) < -kEpsilon
                    : p.GetSide(lightPos);

                if (isBackfacing)
                {
                    BackfacingReceiverPlanes[numPlanes] = p;
                    ++numPlanes;
                }
            }

            ReceiverPlanePackets = FrustumPlanes.BuildSOAPlanePackets(
                BackfacingReceiverPlanes.GetSubNativeArray(0, numPlanes),
                allocator);
            BackfacingReceiverPlanes.Resize(numPlanes);
        }

#if DEBUG_VALIDATE_EXTRA_SPLITS
        private static int s_DebugExtraSplitsCounter = 0;
#endif

        private void InitializeSplits(ref BatchCullingContext cullingContext, AllocatorManager.AllocatorHandle allocator)
        {
            var cullingPlanes = cullingContext.cullingPlanes;
            var cullingSplits = cullingContext.cullingSplits;

            int numSplits = cullingSplits.Length;

#if DEBUG_VALIDATE_EXTRA_SPLITS
            // If extra splits validation is enabled, pad the split number so it's between 5 and 8 by copying existing
            // splits, to ensure that the code functions correctly with higher split counts.
            if (numSplits > 1 && numSplits < 5)
            {
                numSplits = 5 + s_DebugExtraSplitsCounter;
                s_DebugExtraSplitsCounter = (s_DebugExtraSplitsCounter + 1) % 4;
            }
#endif

            Assert.IsTrue(numSplits > 0, "No culling splits provided, expected at least 1");
            Assert.IsTrue(numSplits <= 8, "Split count too high, only up to 8 splits supported");

            int planePacketCount = 0;
            int combinedPlanePacketCount = 0;
            for (int i = 0; i < numSplits; ++i)
            {
                int splitIndex = i;
#if DEBUG_VALIDATE_EXTRA_SPLITS
                splitIndex %= cullingSplits.Length;
#endif

                planePacketCount += (cullingSplits[splitIndex].cullingPlaneCount + 3) / 4;
                combinedPlanePacketCount +=
                    ((cullingSplits[splitIndex].cullingPlaneCount + BackfacingReceiverPlanes.Length) + 3) / 4;
            }

            SplitPlanePackets = new UnsafeList<FrustumPlanes.PlanePacket4>(planePacketCount, allocator);
            CombinedSplitAndReceiverPlanePackets = new UnsafeList<FrustumPlanes.PlanePacket4>(combinedPlanePacketCount, allocator);
            Splits = new UnsafeList<CullingSplitData>(numSplits, allocator);

            var combinedPlanes = new UnsafeList<Plane>(combinedPlanePacketCount * 4, allocator);

            int planeIndex = 0;
            int combinedPlaneIndex = 0;

            for (int i = 0; i < numSplits; ++i)
            {
                int splitIndex = i;
#if DEBUG_VALIDATE_EXTRA_SPLITS
                splitIndex %= cullingSplits.Length;
#endif

                var s = cullingSplits[splitIndex];
                float3 p = s.sphereCenter;
                float r = s.sphereRadius;

                if (s.sphereRadius <= 0)
                    r = 0;

                var splitCullingPlanes = cullingPlanes.GetSubArray(s.cullingPlaneOffset, s.cullingPlaneCount);

                var planePackets = FrustumPlanes.BuildSOAPlanePackets(
                    splitCullingPlanes,
                    allocator);

                foreach (var pp in planePackets)
                    SplitPlanePackets.Add(pp);

                combinedPlanes.Resize(splitCullingPlanes.Length + BackfacingReceiverPlanes.Length);

                // Make combined packets that have both the split planes and the receiver planes so
                // they can be tested simultaneously
                UnsafeUtility.MemCpy(
                    combinedPlanes.Ptr,
                    splitCullingPlanes.GetUnsafeReadOnlyPtr(),
                    splitCullingPlanes.Length * UnsafeUtility.SizeOf<Plane>());
                UnsafeUtility.MemCpy(
                    combinedPlanes.Ptr + splitCullingPlanes.Length,
                    BackfacingReceiverPlanes.Ptr,
                    BackfacingReceiverPlanes.Length * UnsafeUtility.SizeOf<Plane>());

                var combined = FrustumPlanes.BuildSOAPlanePackets(
                    combinedPlanes.AsNativeArray(),
                    allocator);

                foreach (var pp in combined)
                    CombinedSplitAndReceiverPlanePackets.Add(pp);

                Splits.Add(new CullingSplitData
                {
                    CullingSphereCenter = p,
                    CullingSphereRadius = r,
                    ShadowCascadeBlendCullingFactor = s.cascadeBlendCullingFactor,
                    PlanePacketOffset = planeIndex,
                    PlanePacketCount = planePackets.Length,
                    CombinedPlanePacketOffset = combinedPlaneIndex,
                    CombinedPlanePacketCount = combined.Length,
                });

                planeIndex += planePackets.Length;
                combinedPlaneIndex += combined.Length;
            }
        }

        private void InitializeSphereTest(ref BatchCullingContext cullingContext, ShadowProjection shadowProjection)
        {
            // Receiver sphere testing is only enabled if the cascade projection is stable
            bool projectionIsStable = shadowProjection == ShadowProjection.StableFit;
            bool allSplitsHaveValidReceiverSpheres = true;
            for (int i = 0; i < Splits.Length; ++i)
            {
                // This should also catch NaNs, which return false
                // for every comparison.
                if (!(Splits[i].CullingSphereRadius > 0))
                {
                    allSplitsHaveValidReceiverSpheres = false;
                    break;
                }
            }

            if (projectionIsStable && allSplitsHaveValidReceiverSpheres)
            {
                ReceiverSphereCuller = new ReceiverSphereCuller(cullingContext, this);
                SphereTestEnabled = true;
            }
        }
    }
}

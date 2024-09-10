using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Culling
{
    internal struct ReceiverSphereCuller
    {
        private float4 ReceiverSphereCenterX4;
        private float4 ReceiverSphereCenterY4;
        private float4 ReceiverSphereCenterZ4;
        private float4 LSReceiverSphereCenterX4;
        private float4 LSReceiverSphereCenterY4;
        private float4 LSReceiverSphereCenterZ4;
        private float4 ReceiverSphereRadius4;
        private float4 CoreSphereRadius4;
        private UnsafeList<Plane> ShadowFrustumPlanes;

        private float3 LightAxisX;
        private float3 LightAxisY;
        private float3 LightAxisZ;
        private int NumSplits;

        public ReceiverSphereCuller(in BatchCullingContext cullingContext, in CullingSplits splits)
        {
            int numSplits = splits.Splits.Length;

            Assert.IsTrue(numSplits <= 4, "More than 4 culling splits is not supported for sphere testing");
            Assert.IsTrue(numSplits > 0, "No valid culling splits for sphere testing");

            if (numSplits > 4)
                numSplits = 4;

            // Initialize with values that will always fail the sphere test
            ReceiverSphereCenterX4 = new float4(float.PositiveInfinity);
            ReceiverSphereCenterY4 = new float4(float.PositiveInfinity);
            ReceiverSphereCenterZ4 = new float4(float.PositiveInfinity);
            LSReceiverSphereCenterX4 = new float4(float.PositiveInfinity);
            LSReceiverSphereCenterY4 = new float4(float.PositiveInfinity);
            LSReceiverSphereCenterZ4 = new float4(float.PositiveInfinity);
            ReceiverSphereRadius4 = float4.zero;
            CoreSphereRadius4 = float4.zero;

            LightAxisX = new float4(cullingContext.localToWorldMatrix.GetColumn(0)).xyz;
            LightAxisY = new float4(cullingContext.localToWorldMatrix.GetColumn(1)).xyz;
            LightAxisZ = new float4(cullingContext.localToWorldMatrix.GetColumn(2)).xyz;
            NumSplits = numSplits;

            ShadowFrustumPlanes = GetUnsafeListView(cullingContext.cullingPlanes,
                cullingContext.receiverPlaneOffset,
                cullingContext.receiverPlaneCount);

            for (int i = 0; i < numSplits; ++i)
            {
                int elementIndex = i & 3;
                ref CullingSplitData split = ref splits.Splits.ElementAt(i);
                float3 lsReceiverSphereCenter = TransformToLightSpace(split.CullingSphereCenter, LightAxisX, LightAxisY, LightAxisZ);

                ReceiverSphereCenterX4[elementIndex] = split.CullingSphereCenter.x;
                ReceiverSphereCenterY4[elementIndex] = split.CullingSphereCenter.y;
                ReceiverSphereCenterZ4[elementIndex] = split.CullingSphereCenter.z;

                LSReceiverSphereCenterX4[elementIndex] = lsReceiverSphereCenter.x;
                LSReceiverSphereCenterY4[elementIndex] = lsReceiverSphereCenter.y;
                LSReceiverSphereCenterZ4[elementIndex] = lsReceiverSphereCenter.z;

                ReceiverSphereRadius4[elementIndex] = split.CullingSphereRadius;
                CoreSphereRadius4[elementIndex] = split.CullingSphereRadius * split.ShadowCascadeBlendCullingFactor;
            }
        }

        public int Cull(AABB aabb)
        {
            int visibleSplitMask = CullSIMD(aabb);

#if DEBUG_VALIDATE_VECTORIZED_CULLING
            int referenceSplitMask = CullNonSIMD(aabb);

            // Use Debug.Log instead of Debug.Assert so that Burst does not remove it
            if (visibleSplitMask != referenceSplitMask)
                Debug.Log($"Vectorized culling test ({visibleSplitMask:x2}) disagrees with reference test ({referenceSplitMask:x2})");
#endif

            return visibleSplitMask;
        }

        int CullSIMD(AABB aabb)
        {
            float4 casterRadius4 = new float4(math.length(aabb.Extents));
            float4 combinedRadius4 = casterRadius4 + ReceiverSphereRadius4;
            float4 combinedRadiusSq4 = combinedRadius4 * combinedRadius4;

            float3 lsCasterCenter = TransformToLightSpace(aabb.Center, LightAxisX, LightAxisY, LightAxisZ);
            float4 lsCasterCenterX4 = lsCasterCenter.xxxx;
            float4 lsCasterCenterY4 = lsCasterCenter.yyyy;
            float4 lsCasterCenterZ4 = lsCasterCenter.zzzz;

            float4 lsCasterToReceiverSphereX4 = lsCasterCenterX4 - LSReceiverSphereCenterX4;
            float4 lsCasterToReceiverSphereY4 = lsCasterCenterY4 - LSReceiverSphereCenterY4;
            float4 lsCasterToReceiverSphereSqX4 = lsCasterToReceiverSphereX4 * lsCasterToReceiverSphereX4;
            float4 lsCasterToReceiverSphereSqY4 = lsCasterToReceiverSphereY4 * lsCasterToReceiverSphereY4;

            float4 lsCasterToReceiverSphereDistanceSq4 = lsCasterToReceiverSphereSqX4 + lsCasterToReceiverSphereSqY4;
            bool4 doCirclesOverlap4 = lsCasterToReceiverSphereDistanceSq4 <= combinedRadiusSq4;

            float4 lsZMaxAccountingForCasterRadius4 = LSReceiverSphereCenterZ4 + math.sqrt(combinedRadiusSq4 - lsCasterToReceiverSphereSqX4 - lsCasterToReceiverSphereSqY4);
            bool4 isBehindCascade4 = lsCasterCenterZ4 <= lsZMaxAccountingForCasterRadius4;

            int isFullyCoveredByCascadeMask = 0b1111;

#if !DISABLE_SHADOW_CULLING_CAPSULE_TEST
            float3 shadowCapsuleBegin;
            float3 shadowCapsuleEnd;
            float shadowCapsuleRadius;
            ComputeShadowCapsule(LightAxisZ, aabb.Center, casterRadius4.x, ShadowFrustumPlanes,
                out shadowCapsuleBegin, out shadowCapsuleEnd, out shadowCapsuleRadius);

            bool4 isFullyCoveredByCascade4 = IsCapsuleInsideSphereSIMD(shadowCapsuleBegin, shadowCapsuleEnd, shadowCapsuleRadius,
                ReceiverSphereCenterX4, ReceiverSphereCenterY4, ReceiverSphereCenterZ4, CoreSphereRadius4);

            if (math.any(isFullyCoveredByCascade4))
            {
                // The goal here is to find the first non-zero bit in the mask, then set all the bits after it to 0 and all the ones before it to 1.

                // So for example 1100 should become 0111. The transformation logic looks like this:
                // Find first non-zero bit with tzcnt and build a mask -> 0100
                // Left shift by one -> 1000
                // Subtract 1 -> 0111

                int boolMask = math.bitmask(isFullyCoveredByCascade4);
                isFullyCoveredByCascadeMask = 1 << math.tzcnt(boolMask);
                isFullyCoveredByCascadeMask = isFullyCoveredByCascadeMask << 1;
                isFullyCoveredByCascadeMask = isFullyCoveredByCascadeMask - 1;
            }
#endif

            return math.bitmask(doCirclesOverlap4 & isBehindCascade4) & isFullyCoveredByCascadeMask;
        }

        // Keep non-SIMD version around for debugging and validation purposes.
        int CullNonSIMD(AABB aabb)
        {
            // This test has been ported from the corresponding test done by Unity's built in shadow culling.

            float casterRadius = math.length(aabb.Extents);

            float3 lsCasterCenter = TransformToLightSpace(aabb.Center, LightAxisX, LightAxisY, LightAxisZ);
            float2 lsCasterCenterXY = new float2(lsCasterCenter.x, lsCasterCenter.y);

#if !DISABLE_SHADOW_CULLING_CAPSULE_TEST
            float3 shadowCapsuleBegin;
            float3 shadowCapsuleEnd;
            float shadowCapsuleRadius;
            ComputeShadowCapsule(LightAxisZ, aabb.Center, casterRadius, ShadowFrustumPlanes,
                out shadowCapsuleBegin, out shadowCapsuleEnd, out shadowCapsuleRadius);
#endif

            int visibleSplitMask = 0;

            for (int i = 0; i < NumSplits; i++)
            {
                float receiverSphereRadius = ReceiverSphereRadius4[i];
                float3 lsReceiverSphereCenter = new float3(LSReceiverSphereCenterX4[i], LSReceiverSphereCenterY4[i], LSReceiverSphereCenterZ4[i]);
                float2 lsReceiverSphereCenterXY = new float2(lsReceiverSphereCenter.x, lsReceiverSphereCenter.y);

                // A spherical caster casts a cylindrical shadow volume. In XY in light space this ends up being a circle/circle intersection test.
                // Thus we first check if the caster bounding circle is at least partially inside the cascade circle.
                float lsCasterToReceiverSphereDistanceSq = math.lengthsq(lsCasterCenterXY - lsReceiverSphereCenterXY);
                float combinedRadius = casterRadius + receiverSphereRadius;
                float combinedRadiusSq = combinedRadius * combinedRadius;

                // If the 2D circles intersect, then the caster is potentially visible in the cascade.
                // If they don't intersect, then there is no way for the caster to cast a shadow that is
                // visible inside the circle.
                // Casters that intersect the circle but are behind the receiver sphere also don't cast shadows.
                // We don't consider that here, since those casters should be culled out by the receiver
                // plane culling.
                if (lsCasterToReceiverSphereDistanceSq <= combinedRadiusSq)
                {
                    float2 lsCasterToReceiverSphereXY = lsCasterCenterXY - lsReceiverSphereCenterXY;
                    float2 lsCasterToReceiverSphereSqXY = lsCasterToReceiverSphereXY * lsCasterToReceiverSphereXY;

                    // If in light space the shadow caster is behind the current cascade sphere then it can't cast a shadow on it and we can skip it.
                    // sphere equation is (x - x0)^2 + (y - y0)^2 + (z - z0)^2 = R^2 and we are looking for the farthest away z position
                    // thus zMaxInLightSpace = z0 + Sqrt(R^2 - (x - x0)^2 - (y - y0)^2 )). R being Cascade + caster radius.
                    float lsZMaxAccountingForCasterRadius = lsReceiverSphereCenter.z + math.sqrt(combinedRadiusSq - lsCasterToReceiverSphereSqXY.x - lsCasterToReceiverSphereSqXY.y);
                    if (lsCasterCenter.z > lsZMaxAccountingForCasterRadius)
                    {
                        // This is equivalent (but cheaper) than : if (!IntersectCapsuleSphere(shadowVolume, cascades[cascadeIndex].outerSphere))
                        // As the shadow volume is defined as a capsule, while shadows receivers are defined by a sphere (the cascade split).
                        // So if they do not intersect there is no need to render that shadow caster for the current cascade.
                        continue;
                    }

                    visibleSplitMask |= 1 << i;

#if !DISABLE_SHADOW_CULLING_CAPSULE_TEST
                    float3 receiverSphereCenter = new float3(ReceiverSphereCenterX4[i], ReceiverSphereCenterY4[i], ReceiverSphereCenterZ4[i]);
                    float coreSphereRadius = CoreSphereRadius4[i];

                    // Next step is to detect if the shadow volume is fully covered by the cascade. If so we can avoid rendering all other cascades
                    // as we know that in the case of cascade overlap, the smallest cascade index will always prevail. This help as cascade overlap is usually huge.
                    if (IsCapsuleInsideSphere(shadowCapsuleBegin, shadowCapsuleEnd, shadowCapsuleRadius, receiverSphereCenter, coreSphereRadius))
                    {
                        // Ideally we should test against the union of all cascades up to this one, however in a lot of cases (cascade configuration + light orientation)
                        // the overlap of current and previous cascades is a super set of the union of these cascades. Thus testing only the previous cascade does
                        // not create too much overestimation and the math is simpler.
                        break;
                    }
#endif
                }
            }

            return visibleSplitMask;
        }

        static void ComputeShadowCapsule(float3 lightDirection, float3 casterPosition, float casterRadius, UnsafeList<Plane> shadowFrustumPlanes,
            out float3 shadowCapsuleBegin, out float3 shadowCapsuleEnd, out float shadowCapsuleRadius)
        {
            float shadowCapsuleLength = GetShadowVolumeLengthFromCasterAndFrustumAndLightDir(lightDirection,
                casterPosition,
                casterRadius,
                shadowFrustumPlanes);

            shadowCapsuleBegin = casterPosition;
            shadowCapsuleEnd = casterPosition + shadowCapsuleLength * lightDirection;
            shadowCapsuleRadius = casterRadius;
        }

        static float GetShadowVolumeLengthFromCasterAndFrustumAndLightDir(float3 lightDir, float3 casterPosition, float casterRadius, UnsafeList<Plane> planes)
        {
            // The idea here is to find the capsule that goes from the caster and cover all possible shadow receiver in the frustum.
            // First we find the distance from the caster center to the frustum
            var casterRay = new Ray(casterPosition, lightDir);
            int planeIndex;
            float distFromCasterToFrustumInLightDirection = RayDistanceToFrustumOriented(casterRay, planes, out planeIndex);
            if (planeIndex == -1)
            {
                // Shadow caster center is outside of frustum and ray do not intersect it.
                // Shadow volume is thus the caster bounding sphere.
                return 0;
            }

            // Then we need to account for the radius of the capsule.
            // The distance returned might actually be too large in the case of a caster outside of the frustum
            // however detecting this would require to run another RayDistanceToFrustum and the case is rare enough
            // so its not a problem (these caster will just be less likely to be culled away).
            Assert.IsTrue(planeIndex >= 0 && planeIndex < planes.Length);

            float distFromCasterToPlane = math.abs(planes[planeIndex].GetDistanceToPoint(casterPosition));
            float sinAlpha = distFromCasterToPlane / (distFromCasterToFrustumInLightDirection + 0.0001f);
            float tanAlpha = sinAlpha / (math.sqrt(1.0f - (sinAlpha * sinAlpha)));
            distFromCasterToFrustumInLightDirection += casterRadius / (tanAlpha + 0.0001f);

            return distFromCasterToFrustumInLightDirection;
        }

        // Returns the shortest distance to the front facing plane from the ray.
        // Return -1 if no plane intersect this ray.
        // planeNumber will contain the index of the plane found or -1.
        static float RayDistanceToFrustumOriented(Ray ray, UnsafeList<Plane> planes, out int planeNumber)
        {
            planeNumber = -1;
            float maxDistance = float.PositiveInfinity;
            for (int i = 0; i < planes.Length; ++i)
            {
                float distance;
                if (IntersectRayPlaneOriented(ray, planes[i], out distance) && distance < maxDistance)
                {
                    maxDistance = distance;
                    planeNumber = i;
                }
            }

            return planeNumber != -1 ? maxDistance : -1.0f;
        }

        static bool IntersectRayPlaneOriented(Ray ray, Plane plane, out float distance)
        {
            distance = 0f;

            float vdot = math.dot(ray.direction, plane.normal);
            float ndot = -math.dot(ray.origin, plane.normal) - plane.distance;

            // No collision if the ray it the plane from behind
            if (vdot > 0)
                return false;

            // is line parallel to the plane? if so, even if the line is
            // at the plane it is not considered as intersection because
            // it would be impossible to determine the point of intersection
            if (Mathf.Approximately(vdot, 0.0F))
                return false;

            // the resulting intersection is behind the origin of the ray
            // if the result is negative ( enter < 0 )
            distance = ndot / vdot;

            return distance > 0.0F;
        }

        static bool IsInsideSphere(BoundingSphere sphere, BoundingSphere containingSphere)
        {
            if (sphere.radius >= containingSphere.radius)
                return false;

            float squaredDistance = math.lengthsq(containingSphere.position - sphere.position);
            float radiusDelta = containingSphere.radius - sphere.radius;
            float squaredRadiusDelta = radiusDelta * radiusDelta;

            return squaredDistance < squaredRadiusDelta;
        }

        static bool4 IsInsideSphereSIMD(float4 sphereCenterX, float4 sphereCenterY, float4 sphereCenterZ, float4 sphereRadius,
            float4 containingSphereCenterX, float4 containingSphereCenterY, float4 containingSphereCenterZ, float4 containingSphereRadius)
        {
            float4 dx = containingSphereCenterX - sphereCenterX;
            float4 dy = containingSphereCenterY - sphereCenterY;
            float4 dz = containingSphereCenterZ - sphereCenterZ;

            float4 squaredDistance = dx * dx + dy * dy + dz * dz;
            float4 radiusDelta = containingSphereRadius - sphereRadius;
            float4 squaredRadiusDelta = radiusDelta * radiusDelta;

            bool4 canSphereFit = sphereRadius < containingSphereRadius;
            bool4 distanceTest = squaredDistance < squaredRadiusDelta;

            return canSphereFit & distanceTest;
        }

        static bool IsCapsuleInsideSphere(float3 capsuleBegin, float3 capsuleEnd, float capsuleRadius, float3 sphereCenter, float sphereRadius)
        {
            var sphere = new BoundingSphere(sphereCenter, sphereRadius);
            var beginPoint = new BoundingSphere(capsuleBegin, capsuleRadius);
            var endPoint = new BoundingSphere(capsuleEnd, capsuleRadius);

            return IsInsideSphere(beginPoint, sphere) && IsInsideSphere(endPoint, sphere);
        }

        static bool4 IsCapsuleInsideSphereSIMD(float3 capsuleBegin, float3 capsuleEnd, float capsuleRadius,
            float4 sphereCenterX, float4 sphereCenterY, float4 sphereCenterZ, float4 sphereRadius)
        {
            float4 beginSphereX = capsuleBegin.xxxx;
            float4 beginSphereY = capsuleBegin.yyyy;
            float4 beginSphereZ = capsuleBegin.zzzz;

            float4 endSphereX = capsuleEnd.xxxx;
            float4 endSphereY = capsuleEnd.yyyy;
            float4 endSphereZ = capsuleEnd.zzzz;

            float4 capsuleRadius4 = new float4(capsuleRadius);

            bool4 isInsideBeginSphere = IsInsideSphereSIMD(beginSphereX, beginSphereY, beginSphereZ, capsuleRadius4,
                sphereCenterX, sphereCenterY, sphereCenterZ, sphereRadius);

            bool4 isInsideEndSphere = IsInsideSphereSIMD(endSphereX, endSphereY, endSphereZ, capsuleRadius4,
                sphereCenterX, sphereCenterY, sphereCenterZ, sphereRadius);

            return isInsideBeginSphere & isInsideEndSphere;
        }

        static float3 TransformToLightSpace(float3 positionWS, float3 lightAxisX, float3 lightAxisY, float3 lightAxisZ) => new float3(
            math.dot(positionWS, lightAxisX),
            math.dot(positionWS, lightAxisY),
            math.dot(positionWS, lightAxisZ));

        static unsafe UnsafeList<Plane> GetUnsafeListView(NativeArray<Plane> array, int start, int length)
        {
            NativeArray<Plane> subArray = array.GetSubArray(start, length);
            return new UnsafeList<Plane>((Plane*)subArray.GetUnsafeReadOnlyPtr(), length);
        }
    }
}

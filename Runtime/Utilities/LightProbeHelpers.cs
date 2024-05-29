using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    internal delegate void CalculateInterpolatedLightAndOcclusionProbesDelegate(Vector3[] positions, int positionsCount, SphericalHarmonicsL2[] lightProbes, Vector4[] occlusionProbes);

    internal static class LightProbeHelpers
    {
        public static bool IsValidLightProbeGrid()
        {
            var probes = LightmapSettings.lightProbes;
            bool validGrid = probes != null && probes.count > 0;
            return validGrid;
        }

        public static CalculateInterpolatedLightAndOcclusionProbesDelegate BakeCalculateInterpolatedLightAndOcclusionProbesDelegate()
        {
            var methodInfo = typeof(LightProbes).GetMethod("CalculateInterpolatedLightAndOcclusionProbes_Internal", BindingFlags.Static | BindingFlags.NonPublic);

            if (methodInfo != null)
            {
                return (CalculateInterpolatedLightAndOcclusionProbesDelegate)Delegate.CreateDelegate(typeof(CalculateInterpolatedLightAndOcclusionProbesDelegate), methodInfo);
            }

            return null;
        }
    }
}

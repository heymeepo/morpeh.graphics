#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    internal static class LightmapBakingUtility
    {
        public static readonly int UNITY_LIGHTMAPS_ID = Shader.PropertyToID("unity_Lightmaps");
        public static readonly int UNITY_LIGHTMAPS_IND_ID = Shader.PropertyToID("unity_LightmapsInd");
        public static readonly int UNITY_SHADOW_MASKS_ID = Shader.PropertyToID("unity_ShadowMasks");

        public static bool IsLightMapped(this Renderer renderer) => renderer.lightmapIndex is < 65534 and >= 0;

        public static Material CreateLightMappedMaterial(Material material, SceneLightmapsSharedDataAsset lightMaps)
        {
            var lightMappedMaterial = new Material(material);
            lightMappedMaterial.name = $"{lightMappedMaterial.name}_Lightmapped_";
            lightMappedMaterial.EnableKeyword("LIGHTMAP_ON");

            lightMappedMaterial.SetTexture(UNITY_LIGHTMAPS_ID, lightMaps.colors);
            lightMappedMaterial.SetTexture(UNITY_LIGHTMAPS_IND_ID, lightMaps.directions);
            lightMappedMaterial.SetTexture(UNITY_SHADOW_MASKS_ID, lightMaps.shadowMasks);

            if (lightMaps.hasDirections)
            {
                lightMappedMaterial.name += "_DIRLIGHTMAP";
                lightMappedMaterial.EnableKeyword("DIRLIGHTMAP_COMBINED");
            }

            if (lightMaps.hasShadowMask)
            {
                lightMappedMaterial.name += "_SHADOW_MASK";
            }

            return lightMappedMaterial;
        }

        /// <summary>
        /// Converts a provided list of Texture2Ds into a Texture2DArray.
        /// </summary>
        /// <param name="source">A list of Texture2Ds.</param>
        /// <returns>Returns a Texture2DArray that contains the list of Texture2Ds.</returns>
        public static Texture2DArray CopyToTextureArray(List<Texture2D> source)
        {
            if (source == null || !source.Any())
                return null;

            var data = source.First();
            if (data == null)
                return null;

            bool isSRGB = GraphicsFormatUtility.IsSRGBFormat(data.graphicsFormat);
            var result = new Texture2DArray(data.width, data.height, source.Count, source[0].format, true, !isSRGB);
            result.filterMode = FilterMode.Trilinear;
            result.wrapMode = TextureWrapMode.Clamp;
            result.anisoLevel = 3;

            for (var sliceIndex = 0; sliceIndex < source.Count; sliceIndex++)
            {
                var lightMap = source[sliceIndex];
                UnityEngine.Graphics.CopyTexture(lightMap, 0, result, sliceIndex);
            }

            return result;
        }

        /// <summary>
        /// Constructs a LightMaps instance from a list of textures for colors, direction lights, and shadow masks.
        /// </summary>
        /// <param name="inColors">The list of Texture2D for colors.</param>
        /// <param name="inDirections">The list of Texture2D for direction lights.</param>
        /// <param name="inShadowMasks">The list of Texture2D for shadow masks.</param>
        public static void ConstructLightMaps(List<Texture2D> inColors, List<Texture2D> inDirections, List<Texture2D> inShadowMasks, SceneLightmapsSharedDataAsset asset)
        {
            asset.colors = CopyToTextureArray(inColors);
            asset.directions = CopyToTextureArray(inDirections);
            asset.shadowMasks = CopyToTextureArray(inShadowMasks);
        }
    }
}
#endif
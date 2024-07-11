#if UNITY_EDITOR
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Scellecs.Morpeh.EntityConverter.Utilities;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class SceneLightmapsAsset : ScriptableObject, ISceneAsset
    {
        [field: SerializeField]
        public string SceneGuid { get; set; }

        [SerializeField]
        internal SceneLightmapsSharedDataAsset sharedData;

        [SerializeField]
        private List<Renderer> renderers;

        internal Material GetMaterialForRenderer(Renderer renderer)
        {
            ValidateRenderers();



            return null;
        }

        private void ValidateRenderers()
        {
            for (int i = renderers.Count; i >= 0; i--)
            {
                if (renderers[i] == null)
                {

                }
            }
        }

        /// <summary>
        /// Converts a provided list of Texture2Ds into a Texture2DArray.
        /// </summary>
        /// <param name="source">A list of Texture2Ds.</param>
        /// <returns>Returns a Texture2DArray that contains the list of Texture2Ds.</returns>
        private static Texture2DArray CopyToTextureArray(List<Texture2D> source)
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
#endif

    [Serializable]
    internal sealed class LightMappedMaterialRef
    {
        public Material material;
        public int refCount;
    }
}

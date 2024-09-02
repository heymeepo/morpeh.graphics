#if UNITY_EDITOR
using UnityEngine;
using Scellecs.Morpeh.EntityConverter.Utilities;
using System.Collections.Generic;
using System;
using Scellecs.Morpeh.EntityConverter.Collections;
using System.Linq;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class SceneLightmapsAsset : ScriptableObject, ISceneAsset
    {
        [field: SerializeField]
        public string SceneGuid { get; set; }

        [SerializeField]
        internal SceneLightmapsSharedDataAsset sharedData;

        [SerializeField]
        private List<Renderer> renderers;

        [SerializeField]
        private SerializableDictionary<LightMapKey, int> lightmapIndicesCache;

        internal LightmapData GetLightmapDataForRenderer(Renderer renderer)
        {
            int lightmapIndex = 0;

            if (renderers.Contains(renderer) == false)
            {
                renderers.Add(renderer);

                if (TryGetCachedLightmapIndexForUnityLightmapIndex(renderer.lightmapIndex, out lightmapIndex) == false)
                {
                    UpdateLightmapSceneData();
                }
            }

            var lightmappedMaterial = GetLightmappedMaterial();

            return new LightmapData()
            {
                lightmapIndex = lightmapIndex,
                lightmappedMaterial = lightmappedMaterial,
                shared = sharedData
            };
        }

        private bool TryGetCachedLightmapIndexForUnityLightmapIndex(int index, out int cachedIndex)
        {
            var lightmaps = LightmapSettings.lightmaps;
            var lightmapData = lightmaps[index];
            var key = new LightMapKey(lightmapData);

            return lightmapIndicesCache.TryGetValue(key, out cachedIndex);
        }

        private Material GetLightmappedMaterial()
        {


            return null;
        }

        private void UpdateLightmapSceneData()
        {
            var lightmaps = LightmapSettings.lightmaps;
            var colors = new List<Texture2D>();
            var directions = new List<Texture2D>();
            var shadowMasks = new List<Texture2D>();
            var lightmapIndices = new List<int>();

            foreach (var renderer in renderers)
            {
                lightmapIndices.Add(renderer.lightmapIndex);
            }

            var uniqueIndices = lightmapIndices
                .Distinct()
                .OrderBy(x => x)
                .Where(x => x >= 0 && x != 65534 && x < lightmaps.Length)
                .ToArray();

            for (var i = 0; i < uniqueIndices.Length; i++)
            {
                var index = uniqueIndices[i];
                var lightmapData = lightmaps[index];
                var key = new LightMapKey(lightmapData);

                colors.Add(lightmapData.lightmapColor);
                directions.Add(lightmapData.lightmapDir);
                shadowMasks.Add(lightmapData.shadowMask);
                lightmapIndicesCache.Add(key, i);
            }
        }

        private void ValidateRenderers()
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                if (renderers[i] == null)
                {

                }
            }
        }
    }
}
#endif

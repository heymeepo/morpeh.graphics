#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Scellecs.Morpeh.EntityConverter;
using Scellecs.Morpeh.EntityConverter.Utilities;
using System.Collections.Generic;

namespace Scellecs.Morpeh.Graphics
{
    using EntityConverter = EntityConverter.EntityConverter;

    internal class LightmapBaking : AssetPostprocessor
    {
        private static Dictionary<string, SceneLightmapsAsset> lightmapsPerSceneCache = new Dictionary<string, SceneLightmapsAsset>();

        public static LightmapData GetLightmapData(Renderer renderer)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var scenePath = scene.path;
            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);

            if (lightmapsPerSceneCache.TryGetValue(sceneGuid, out var lightmapsAsset) == false)
            {
                lightmapsPerSceneCache[sceneGuid] = lightmapsAsset = SceneLightmapsExtensions.CreateSceneLightmapsAssets(scenePath);
            }

            var lightmappedMaterial = lightmapsAsset.GetMaterialForRenderer(renderer);
            var sharedData = lightmapsAsset.sharedData;

            return new LightmapData()
            {
                lightmappedMaterial = lightmappedMaterial,
                shared = sharedData
            };
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                Reset();
            }

            ValidateDeletedScenes();
        }

        private static void Reset()
        {
            lightmapsPerSceneCache.Clear();
            var converter = EntityConverter.Instance;
            if (converter != null)
            {
                var guids = AssetDatabase.FindAssets("t:SceneLightmapsAsset");
                foreach (string guid in guids)
                {
                    var asset = AssetDatabaseUtility.LoadAssetFromGuid<SceneLightmapsAsset>(guid);
                    if (asset != null)
                    {
                        lightmapsPerSceneCache.Add(asset.SceneGuid, asset);
                    }
                }
            }

            Lightmapping.bakeCompleted += OnLightmapsBaked;
        }

        private static void OnLightmapsBaked()
        {
            var converter = EntityConverter.Instance;
            if (converter != null)
            {
                var sceneGuid = SceneUtility.GetActiveSceneGUID();
                if (converter.TryGetSceneBakedDataWithGuid(sceneGuid, out var sceneData))
                {
                    sceneData.BakeScene();
                }
            }
        }

        private static void ValidateDeletedScenes()
        {
            var deleted = new List<string>();

            foreach (var sceneGuid in lightmapsPerSceneCache.Keys)
            {
                if (AssetDatabaseUtility.HasAssetFromGuid<SceneAsset>(sceneGuid) == false)
                {
                    deleted.Add(sceneGuid);
                }
            }

            if (deleted.Count > 0)
            {
                foreach (var sceneGuid in deleted)
                {
                    DeleteLightmapsAssetsForDeletedScene(sceneGuid);
                    lightmapsPerSceneCache.Remove(sceneGuid);
                }
            }
        }

        private static void DeleteLightmapsAssetsForDeletedScene(string sceneGuid)
        {
            var lightmapsAsset = lightmapsPerSceneCache[sceneGuid];
            var lightmapsSharedDataAsset = lightmapsAsset.sharedData;

            var lightmapsAssetPath = AssetDatabase.GetAssetPath(lightmapsAsset);
            var lightmapsSharedDataAssetPath = AssetDatabase.GetAssetPath(lightmapsSharedDataAsset);

            AssetDatabase.DeleteAsset(lightmapsAssetPath);
            AssetDatabase.DeleteAsset(lightmapsSharedDataAssetPath);
        }
    }
}
#endif

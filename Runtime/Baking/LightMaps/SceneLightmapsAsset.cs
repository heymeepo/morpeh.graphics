//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//using UnityEngine;
//using System.Collections.Generic;
//using System;

//namespace Scellecs.Morpeh.Graphics
//{
//    using EntityConverter = EntityConverter.EntityConverter;

//#if UNITY_EDITOR
//    internal class LightmapBaking : AssetPostprocessor
//    {
//        private static EntityConverter converter;
//        //private static Dictionary<string, SceneLightmapsAsset> lightmapsPerSceneCache = new Dictionary<string, SceneLightmapsAsset>();

//        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
//        {
//            if (didDomainReload)
//            {
                 
//            }
//        }

//        //public static bool TryGetLightmapData(Renderer renderer, out LightmapData data)
//        //{
//        //    var scene = EditorSceneManager.GetActiveScene();
//        //    var guid = AssetDatabase.GUIDFromAssetPath(scene.path).ToString();

//        //    if (lightmapsPerSceneCache.TryGetValue(guid, out var lightmapsAsset))
//        //    {

//        //    }

//        //    data = default;
//        //    return true;
//        //}

//        //private static void Reload()
//        //{
//        //    database = EntityConverterDatabase.GetInstance();
//        //    lightmapsPerSceneCache.Clear();

//        //    if (database != null)
//        //    {
//        //        var guids = AssetDatabase.FindAssets("t:SceneLightmapsAsset");

//        //        foreach (string guid in guids)
//        //        {
//        //            var asset = AssetDatabaseUtils.LoadAssetFromGUID<SceneLightmapsAsset>(guid);

//        //            if (asset != null)
//        //            {
//        //                lightmapsPerSceneCache.Add(asset.sceneGuid, asset);
//        //            }
//        //        }
//        //    }
//        //}

//        //private static void CreateSceneLightmapsAsset()
//        //{
//        //    if (TryGetDatabase(out var database))
//        //    {
//        //        var scene = EditorSceneManager.GetActiveScene();

//        //        if (database.TryGetSceneBakedDataWithScenePath(scene.path, out _))
//        //        {

//        //        }
//        //    }
//        //}

//        //private static bool TryGetDatabase(out EntityConverterDatabase database)
//        //{
//        //    database = EntityConverterDatabase.GetInstance();
//        //    return database != null;
//        //}
//    }
//#endif
//    public sealed class SceneLightmapsAsset : ScriptableObject
//    {
//        [SerializeField]
//        internal string sceneGuid;

//        [SerializeField]
//        internal SceneLightmapsSharedDataAsset sharedData;

//        [SerializeField]
//        private List<Renderer> renderers;

//        internal Material GetMaterialForRenderer(Renderer renderer)
//        {
//            return null;
//        }

//        private void ValidateRenderers()
//        {

//        }
//    }

//    public struct LightmapData
//    {
//        public Material lightmappedMaterial;
//        public SceneLightmapsSharedDataAsset shared;
//    }

//    public sealed class SceneLightmapsSharedDataAsset : ScriptableObject
//    {
//        /// <summary>
//        /// An array of color maps.
//        /// </summary>
//        public Texture2DArray colors;

//        /// <summary>
//        /// An array of directional maps.
//        /// </summary>
//        public Texture2DArray directions;

//        /// <summary>
//        /// An array of Shadow masks.
//        /// </summary>
//        public Texture2DArray shadowMasks;

//        /// <summary>
//        /// An array of lightmapped materials.
//        /// </summary>
//        public List<Material> materials;
//    }

//    [Serializable]
//    internal sealed class LightMappedMaterialRef
//    {
//        public Material material;
//        public int refCount;
//    }
//}

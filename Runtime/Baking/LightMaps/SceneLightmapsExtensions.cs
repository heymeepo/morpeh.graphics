#if UNITY_EDITOR
using Scellecs.Morpeh.EntityConverter.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    internal static class SceneLightmapsExtensions
    {
        public static SceneLightmapsAsset CreateSceneLightmapsAssets(string scenePath)
        {
            var sceneLightmaps = AssetDatabaseUtility.CreateAssetForScene<SceneLightmapsAsset>(scenePath, "LightmapsAsset");
            sceneLightmaps.sharedData = AssetDatabaseUtility.CreateAssetForScene<SceneLightmapsSharedDataAsset>(scenePath, "LightmapsSharedDataAsset");
            return sceneLightmaps;
        }
    }
}
#endif

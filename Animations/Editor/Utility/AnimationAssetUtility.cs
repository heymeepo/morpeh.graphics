using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations.Editor
{
    internal static class AnimationAssetUtility
    {
        internal static AnimationDataAsset CreateAsset(string name, string path)
        {
            var asset = ScriptableObject.CreateInstance<AnimationDataAsset>();
            AssignNewID(asset);
            AssetDatabase.CreateAsset(asset, $"{path}/{name}.asset");
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void AssignNewID(AnimationDataAsset asset)
        {
            var existingAssets = AssetDatabase.FindAssets($"t:{typeof(AnimationDataAsset).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationDataAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null);

            int maxId = existingAssets.Any() ? existingAssets.Max(a => a.id) : 0;
            asset.id = maxId + 1;
        }
    }

    internal sealed class AnimationAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (deletedAssets.Length > 0)
            {
                ReassignIDs();
            }
        }

        private static void ReassignIDs()
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(AnimationDataAsset).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<AnimationDataAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .OrderBy(a => a.id)
                .ToList();

            int count = assets.Count;
            for (int i = 0; i < count - 1; i++)
            {
                if (assets[i + 1].id > assets[i].id + 1)
                {
                    int missingId = assets[i].id + 1;
                    var lastAsset = assets.Last();
                    lastAsset.id = missingId;
                    EditorUtility.SetDirty(lastAsset);

                    assets.RemoveAt(assets.Count - 1);
                    count--;
                }
            }

            if (assets.Count > 0 && assets.Last().id != assets.Count)
            {
                assets.Last().id = assets.Count;
                EditorUtility.SetDirty(assets.Last());
            }

            AssetDatabase.SaveAssets();
        }
    }
}

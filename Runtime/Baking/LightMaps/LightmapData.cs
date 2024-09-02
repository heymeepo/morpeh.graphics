#if UNITY_EDITOR
using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    public struct LightmapData
    {
        public int lightmapIndex;
        public Material lightmappedMaterial;
        public SceneLightmapsSharedDataAsset shared;
    }
}
#endif

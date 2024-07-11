using UnityEngine;
using Scellecs.Morpeh.EntityConverter.Utilities;
using System.Collections.Generic;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class SceneLightmapsSharedDataAsset : ScriptableObject, ISceneAsset
    {
        public string SceneGuid { get; set; }

        /// <summary>
        /// An array of color maps.
        /// </summary>
        public Texture2DArray colors;

        /// <summary>
        /// An array of directional maps.
        /// </summary>
        public Texture2DArray directions;

        /// <summary>
        /// An array of Shadow masks.
        /// </summary>
        public Texture2DArray shadowMasks;

        /// <summary>
        /// An array of lightmapped materials.
        /// </summary>
        public List<Material> materials;
    }
}

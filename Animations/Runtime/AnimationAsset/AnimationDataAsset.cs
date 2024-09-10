using Scellecs.Morpeh.Graphics.Animations.TAO.VertexAnimation;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations
{
    public sealed class AnimationDataAsset : ScriptableObject
    {
        [SerializeField]
        internal int id;

        [SerializeField]
        internal AnimationData[] animations; //TODO: Rework to Blobs

        [SerializeField]
        internal Mesh mesh;

        [SerializeField]
        internal Material material;
    }
}

#if MORPEH_ENTITY_CONVERTER && UNITY_EDITOR
using Scellecs.Morpeh.EntityConverter;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations.Authoring
{
    public class AnimationAuthoring : EcsAuthoring
    {
        [SerializeField]
        private AnimationBakerAsset animationBakerAsset;

        public override void OnBeforeBake(UserContext userContext)
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            meshFilter.sharedMesh = GetMesh();
            meshRenderer.sharedMaterial = GetMaterial();
        }

        public override void OnBake(BakingContext bakingContext, UserContext userContext)
        {
            if (animationBakerAsset == null)
            {
                Debug.LogWarning("The AnimationBakerAsset is not assigned!");
                return;
            }

            var dataAsset = animationBakerAsset.animationDataAsset;
            var animationStatesInfo = animationBakerAsset.GetAnimationStatesInfo();

            if (animationStatesInfo == null || animationStatesInfo.Any() == false)
            {
                Debug.LogWarning("...");
                return;
            }

            bakingContext.SetComponent(new AnimationComponent()
            {
                data = dataAsset
            });

            foreach (var componentInfo in animationStatesInfo)
            {
                if (AnimationStatesMap.TryGetAnimationComponentType(componentInfo.stateName, out var componentType))
                {
                    bakingContext.SetComponentReinterpret(componentType, componentInfo.index);
                }
                else
                {
                    //warning
                }
            }
        }

        public virtual Mesh GetMesh()
        {
            if (animationBakerAsset != null && animationBakerAsset.animationDataAsset != null)
            {
                return animationBakerAsset.animationDataAsset.mesh;
            }

            return null;
        }

        public virtual Material GetMaterial()
        {
            if (animationBakerAsset != null && animationBakerAsset.animationDataAsset != null)
            {
                return animationBakerAsset.animationDataAsset.material;
            }

            return null;
        }

        private void OnValidate()
        {
            var meshFilter = GetComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            var mesh = GetMesh();
            var material = GetMaterial();

            if (meshFilter.sharedMesh != mesh || meshRenderer.sharedMaterial != material)
            {
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = material;
                EditorUtility.SetDirty(this);
            }
        }
    }
}
#endif

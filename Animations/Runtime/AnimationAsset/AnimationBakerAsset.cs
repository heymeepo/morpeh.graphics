#if UNITY_EDITOR
using Scellecs.Morpeh.Graphics.Animations.TAO.VertexAnimation;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [CreateAssetMenu(fileName = "AnimationBaker", menuName = "ECS/Animations/AnimationBaker")]
    public sealed class AnimationBakerAsset : ScriptableObject
    {
        [SerializeField] 
        internal GameObject sourcePrefab;

        [SerializeField] 
        internal Shader shader;

        [SerializeField] 
        internal Material presetMaterial;

        [SerializeField]
        internal AnimationDataAsset animationDataAsset;

        [SerializeField]
        internal List<AnimationStateData> animations;

        [SerializeField, Range(1, 60)] 
        internal int fps = 24;

        [SerializeField] 
        internal int textureWidth = 512;

        [SerializeField] 
        internal bool applyRootMotion = false;

        [SerializeField]
        private AnimationStateBakingInfo[] animationStatesInfo;

        internal IEnumerable<AnimationStateBakingInfo> GetAnimationStatesInfo() => animationStatesInfo;

        internal void Bake()
        {
            if (CheckRequired())
            {
                var target = Instantiate(sourcePrefab);
                target.name = sourcePrefab.name;

                MeshCombiner.CombineAndConvertGameObject(target);
                var bakedData = BakeAnimations(target);
                var positionMap = CreatePositionMap(bakedData);

                DestroyImmediate(target);
                GenerateData(bakedData, positionMap);
                SaveAsset(animationDataAsset);
            }
        }

        private AnimationBaker.BakedData BakeAnimations(GameObject target)
        {
            var clips = new AnimationClip[animations.Count];

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = animations[i].clip;
            }

            return AnimationBaker.Bake(target, clips, applyRootMotion, fps, textureWidth);
        }

        private Texture2DArray CreatePositionMap(AnimationBaker.BakedData bakedData)
        {
            return Texture2DArrayUtils.CreateTextureArray(bakedData.positionMaps.ToArray(), false, true, TextureWrapMode.Repeat, FilterMode.Point, 1, string.Format("{0} PositionMap", sourcePrefab.name), true);
        }

        private void GenerateData(AnimationBaker.BakedData bakedData, Texture2DArray positionMap)
        {
            GenerateRenderData(bakedData, positionMap);
            GenerateAnimationData(bakedData);
        }

        private void GenerateRenderData(AnimationBaker.BakedData bakedData, Texture2DArray positionMap)
        {
            NamingConventionUtils.PositionMapInfo info = bakedData.GetPositionMap.name.GetTextureInfo();
            var material = AnimationMaterial.Create(string.Format("{0} Material", sourcePrefab.name), shader, positionMap, info.maxFrames, presetMaterial);

            bakedData.mesh.Optimize();
            bakedData.mesh.UploadMeshData(true);

            AssetDatabaseUtils.RemoveChildAssets(animationDataAsset);
            AssetDatabase.AddObjectToAsset(material, animationDataAsset);
            AssetDatabase.AddObjectToAsset(bakedData.mesh, animationDataAsset);
            AssetDatabase.AddObjectToAsset(positionMap, animationDataAsset);

            animationDataAsset.mesh = bakedData.mesh;
            animationDataAsset.material = material;
        }

        private unsafe void GenerateAnimationData(AnimationBaker.BakedData bakedData)
        {
            animationDataAsset.animations = new AnimationData[bakedData.positionMaps.Count];
            animationStatesInfo = new AnimationStateBakingInfo[bakedData.positionMaps.Count];
            var info = new List<NamingConventionUtils.PositionMapInfo>();

            foreach (var tex in bakedData.positionMaps)
            {
                info.Add(tex.name.GetTextureInfo());
            }

            for (int i = 0; i < info.Count; i++)
            {
                AnimationData newData = new AnimationData(info[i].frames, info[i].maxFrames, info[i].fps, i, animations[i].clip.isLooping, -1);
                animationDataAsset.animations[i] = newData;
                animationStatesInfo[i] = new AnimationStateBakingInfo() { stateName = animations[i].stateName, index = i };
            }
        }

        private void SaveAsset(ScriptableObject asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private bool CheckRequired()
        {
            bool result = true;

            if (sourcePrefab == null)
            {
                Debug.LogError($"Animation baker {name} : prefab is not assigned");
                result = false;
            }
            if (shader == null)
            {
                Debug.LogError($"Animation baker {name} : shader is not assigned");
                result = false;
            }
            if (animationDataAsset == null)
            {
                Debug.LogError($"Animation baker {name} : animationData asset is not assigned");
                result = false;
            }
            if (animations.Count == 0)
            {
                Debug.LogError($"Animation baker {name} : animations are not assigned");
                result = false;
            }

            return result;
        }

    }
}
#endif

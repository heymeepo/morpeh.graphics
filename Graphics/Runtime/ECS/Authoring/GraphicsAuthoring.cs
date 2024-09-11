#if MORPEH_ENTITY_CONVERTER && UNITY_EDITOR
using Scellecs.Morpeh.EntityConverter;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Authoring
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    [Icon("Packages/com.scellecs.morpeh.graphics/Graphics/Editor/DefaultResources/Icons/d_Renderer@64.png")]
    public sealed class GraphicsAuthoring : EcsAuthoring
    {
        [SerializeField]
        internal List<OverrideData> overrides;

        public override void OnBeforeBake(UserContext userContext)
        {
            
        }

        public override void OnBake(BakingContext bakingContext, UserContext userContext)
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            var material = GetMaterial();
            var mesh = GetMesh();

            if (material == null || mesh == null)
            {
                Debug.LogWarning($"You haven't set a mesh or material in {gameObject.name} at {gameObject.scene.name} scene, the graphics data will not be added for this object!");
                return;
            }

            var filterSettings = new RenderFilterSettings()
            {
                layer = gameObject.layer,
                renderingLayerMask = meshRenderer.renderingLayerMask,
                shadowCastingMode = meshRenderer.shadowCastingMode,
                receiveShadows = material.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF") == false,
                motionMode = MotionVectorGenerationMode.Camera,
                staticShadowCaster = meshRenderer.staticShadowCaster
            };

            if (filterSettings != RenderFilterSettings.Default)
            {
                bakingContext.SetComponent(filterSettings);
            }

            foreach (var overrideData in overrides)
            {
                if (MaterialOverriesMap.TryGetOverrideType(overrideData.name, out var componentType) == false)
                {
                    Debug.LogWarning($"There is no material property override component found for {overrideData.name} in {gameObject.name} at {gameObject.scene.name} scene. It should be ignored");
                    continue;
                }

                if (material.HasProperty(overrideData.name) == false)
                {
                    Debug.LogWarning($"The shader is not compatible with the property {overrideData.name} in {gameObject.name} at {gameObject.scene.name} scene. It should be ignored.");
                    continue;
                }

                if (overrideData.type is ShaderPropertyType.Color or ShaderPropertyType.Vector)
                {
                    float4 value = overrideData.value;
                    bakingContext.SetComponentReinterpret(componentType, value);
                }
                else
                {
                    float value = overrideData.value.x;
                    bakingContext.SetComponentReinterpret(componentType, value);
                }
            }

            //    if (meshRenderer.IsLightMapped() && gameObject.isStatic)
            //    {
            //        var lightmapData = LightmapBaking.GetLightmapData(meshRenderer);
            //        material = lightmapData.lightmappedMaterial;
            //        SetComponent(new LightMaps() { lightmaps = lightmapData.shared });
            //        SetComponent(new BuiltinMaterialPropertyUnity_LightmapIndex() { value = lightmapData.lightmapIndex});
            //        SetComponent(new BuiltinMaterialPropertyUnity_LightmapST() { value = meshRenderer.lightmapScaleOffset });
            //    }

            bakingContext.SetComponent(new MaterialMeshManaged()
            {
                material = material,
                mesh = mesh,
                submeshIndex = 0
            });
        }

        internal Mesh GetMesh()
        {
            return GetComponent<MeshFilter>().sharedMesh;
        }

        internal Material GetMaterial() 
        {
            return GetComponent<MeshRenderer>().sharedMaterial;
        }
    }
}
#endif

#if MORPEH_ENTITY_CONVERTER && UNITY_EDITOR
using Scellecs.Morpeh.EntityConverter;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Baking
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    [Icon("Packages/com.scellecs.morpeh.graphics/Editor/DefaultResources/Icons/d_Renderer@64.png")]
    public sealed class GraphicsAuthoring : EcsAuthoring
    {
        [SerializeField] 
        internal List<OverrideData> overrides;

        public override void Bake()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            var meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer.sharedMaterial == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"You have not set a mesh or material in {gameObject.name} at {gameObject.scene.name} scene, the graphics data will not be added for this object!");
                return;
            }

            var material = meshRenderer.sharedMaterial;
            var mesh = meshFilter.sharedMesh;

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
                SetComponent(filterSettings);
            }

            foreach (var overrideData in overrides)
            {
                if (MaterialOverriesMap.TryGetOverrideType(overrideData.name, out var componentType) == false)
                {
                    Debug.LogWarning($"There is no material property override component found for {overrideData.name} in {gameObject.name}. It should be ignored");
                    continue;
                }

                var typeId = GetComponentTypeId(componentType);

                if (overrideData.type is ShaderPropertyType.Color or ShaderPropertyType.Vector)
                {
                    float4 value = overrideData.value;
                    SetComoponentDataUnsafe(value, typeId);
                }
                else
                {
                    float value = overrideData.value.x;
                    SetComoponentDataUnsafe(value, typeId);
                }
            }

            if (meshRenderer.lightmapIndex is < 65534 and >= 0 && gameObject.isStatic)
            {
                //if (LightmapBaking.TryGetLightmapData(meshRenderer, out var data))
                //{
                //    material = data.lightmappedMaterial;
                //    SetComponent(new LightMaps() { lightmaps = data.shared });
                //}
            }

            SetComponent(new MaterialMeshManaged()
            {
                material = material,
                mesh = mesh,
                submeshIndex = 0
            });
        }
    }
}
#endif

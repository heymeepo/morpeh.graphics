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
        public MeshRenderer meshRenderer;
        public MeshFilter meshFilter;
        public List<OverrideData> overrides;

        public override void Bake()
        {
            SetComponent(new MaterialMeshManaged()
            {
                material = meshRenderer.sharedMaterial,
                mesh = meshFilter.sharedMesh,
                submeshIndex = 0
            });

            var filterSettings = new RenderFilterSettings()
            {
                layer = gameObject.layer,
                renderingLayerMask = meshRenderer.renderingLayerMask,
                shadowCastingMode = meshRenderer.shadowCastingMode,
                receiveShadows = meshRenderer.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF") == false,
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

                if (overrideData.type == ShaderPropertyType.Color || overrideData.type == ShaderPropertyType.Vector)
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
        }

        private void Reset()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
        }
    }
}
#endif

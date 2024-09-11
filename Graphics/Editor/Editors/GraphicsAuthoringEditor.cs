using Scellecs.Morpeh.Graphics.Authoring;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Scellecs.Morpeh.Graphics.Editor
{
    [CustomEditor(typeof(GraphicsAuthoring), false)]
    public sealed class GraphicsAuthoringEditor : UnityEditor.Editor
    {
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private GameObject targetGameObject;

        private SerializedObject meshRendererObject;
        private SerializedObject meshFilterObject;

        private PropertyField materialField;
        private PropertyField meshField;

        private Foldout filterSettingsFoldout;
        private MaskField renderingLayerMask;
        private PropertyField castShadowsField;
        private PropertyField staticShadowCasterField;

        private CustomListView<OverrideData> overridesList;

        public override VisualElement CreateInspectorGUI()
        {
            var inspector = new VisualElement();

            CreateMaterialMesh();
            CreateFilterSettings();
            CreateOverridesList();

            inspector.Add(materialField);
            inspector.Add(meshField);

            inspector.Add(filterSettingsFoldout);
            filterSettingsFoldout.Add(renderingLayerMask);
            filterSettingsFoldout.Add(castShadowsField);
            filterSettingsFoldout.Add(staticShadowCasterField);

            inspector.Add(overridesList);

            serializedObject.ApplyModifiedProperties();
            meshRendererObject.ApplyModifiedProperties();
            meshFilterObject.ApplyModifiedProperties();

            inspector.schedule.Execute(_ => //xddddddd
            {
                if (target != null)
                {
                    meshRenderer.hideFlags = HideFlags.HideInInspector;
                    meshFilter.hideFlags = HideFlags.HideInInspector;
                    InternalEditorUtility.SetIsInspectorExpanded(meshFilter, !InternalEditorUtility.GetIsInspectorExpanded(meshFilter));
                    InternalEditorUtility.SetIsInspectorExpanded(meshRenderer, !InternalEditorUtility.GetIsInspectorExpanded(meshRenderer));
                }
            })
            .ForDuration(1);

            return inspector;
        }

        private void CreateMaterialMesh()
        {
            targetGameObject = (target as GraphicsAuthoring).gameObject;

            meshRenderer = targetGameObject.GetComponent<MeshRenderer>();
            meshFilter = targetGameObject.GetComponent<MeshFilter>();

            meshRendererObject = new SerializedObject(meshRenderer);
            meshFilterObject = new SerializedObject(meshFilter);

            var meshRendererProperty = meshRendererObject.FindProperty("m_Materials");
            var meshFilterProperty = meshFilterObject.FindProperty("m_Mesh");

            materialField = new PropertyField(meshRendererProperty.GetArrayElementAtIndex(0), "Material");
            meshField = new PropertyField(meshFilterProperty, "Mesh");

            materialField.BindProperty(meshRendererProperty.GetArrayElementAtIndex(0));
            meshField.BindProperty(meshFilterProperty);
        }

        private void CreateFilterSettings()
        {
            filterSettingsFoldout = new Foldout();
            filterSettingsFoldout.text = "Filter Settings";

            var meshRendererLayerMask = meshRendererObject.FindProperty("m_RenderingLayerMask");
            renderingLayerMask = new MaskField("Rendering Layer Mask");
            renderingLayerMask.choicesMasks = Enumerable.Range(0, 32).Select(i => 1 << i).ToList();
            renderingLayerMask.schedule.Execute(_ =>
            {
                if (target != null)
                {
                    renderingLayerMask.choices = new List<string>(SRPUtility.RenderingLayerMaskNames);
                    renderingLayerMask.value = meshRendererLayerMask.intValue;
                }
            })
            .Every(1000);
            renderingLayerMask.RegisterValueChangedCallback(x =>
            {
                if (target != null)
                {
                    meshRendererLayerMask.uintValue = (uint)x.newValue;
                    meshRendererObject.ApplyModifiedProperties();
                }
            });

            var meshRendererStaticShadowCaster = meshRendererObject.FindProperty("m_StaticShadowCaster");
            staticShadowCasterField = new PropertyField(null, "Static Shadow Caster");
            staticShadowCasterField.BindProperty(meshRendererStaticShadowCaster);

            var meshRendererShadowCastingMode = meshRendererObject.FindProperty("m_CastShadows");
            castShadowsField = new PropertyField(null, "Cast Shadows");
            castShadowsField.BindProperty(meshRendererShadowCastingMode);
            castShadowsField.RegisterValueChangeCallback(x =>
            {
                var value = (ShadowCastingMode)x.changedProperty.intValue;
                staticShadowCasterField.style.visibility = value == ShadowCastingMode.Off ? Visibility.Hidden : Visibility.Visible;
            });
        }

        private void CreateOverridesList()
        {
            var overridesProperty = serializedObject.FindProperty(nameof(GraphicsAuthoring.overrides));
            overridesList = new CustomListView<OverrideData>(overridesProperty, MaterialPropertyOverrideSelector, OverrideDataEqualityComparer.Default, true);
        }

        private IEnumerable<DropdownItem<OverrideData>> MaterialPropertyOverrideSelector()
        {
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                var shader = meshRenderer.sharedMaterial.shader;
                int propertyCount = shader.GetPropertyCount();

                List<DropdownItem<OverrideData>> shaderProperties = new List<DropdownItem<OverrideData>>();

                for (int i = 0; i < propertyCount; i++)
                {
                    string propertyName = shader.GetPropertyName(i);
                    var propertyType = shader.GetPropertyType(i);

                    bool isValidType =
                        propertyType == ShaderPropertyType.Vector ||
                        propertyType == ShaderPropertyType.Color ||
                        propertyType == ShaderPropertyType.Float ||
                        propertyType == ShaderPropertyType.Range;

                    if (isValidType)
                    {
                        shaderProperties.Add(new DropdownItem<OverrideData>
                        {
                            value = new OverrideData
                            {
                                name = propertyName,
                                type = propertyType
                            },
                            name = propertyName
                        });
                    }
                }

                return shaderProperties;
            }

            return Array.Empty<DropdownItem<OverrideData>>();
        }

        private void OnDestroy()
        {
            if (target == null &&
                targetGameObject != null &&
                targetGameObject.GetComponent<GraphicsAuthoring>() == null &&
                Application.isPlaying == false)
            {
                DestroyImmediate(meshRenderer, true);
                DestroyImmediate(meshFilter, true);
            }
        }
    }
}

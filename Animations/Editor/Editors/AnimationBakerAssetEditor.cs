using Scellecs.Morpeh.Graphics.Editor;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scellecs.Morpeh.Graphics.Animations.Editor
{
    [CustomEditor(typeof(AnimationBakerAsset), false)]
    public sealed class AnimationBakerAssetEditor : UnityEditor.Editor
    {
        private VisualElement inspector;
        private Button createAnimationDataAssetButton;
        private AnimationBakerAsset targetAsset;

        public override VisualElement CreateInspectorGUI()
        {
            inspector = new VisualElement();
            targetAsset = target as AnimationBakerAsset;
            CreateGUI();
            return inspector;
        }

        private void CreateGUI()
        {
            inspector.Clear();

            var prefabProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.sourcePrefab));
            var shaderProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.shader));
            var presetMaterialProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.presetMaterial));
            var fpsProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.fps));
            var textureWidthProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.textureWidth));
            var applyRootMotionProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.applyRootMotion));
            var animationDataAssetProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.animationDataAsset));

            var prefabField = new PropertyField(prefabProperty);
            var shaderField = new PropertyField(shaderProperty);
            var presetMaterialField = new PropertyField(presetMaterialProperty);
            var fpsField = new PropertyField(fpsProperty);
            var textureWidthField = new PropertyField(textureWidthProperty);
            var applyRootMotionField = new PropertyField(applyRootMotionProperty);
            var animationsList = CreateOverridesList();
            var animationDataAssetField = new PropertyField(animationDataAssetProperty);
            animationDataAssetField.SetEnabled(false);
            var bakeAnimationsButton = CreateBakeButton();
            CreateAnimationDataAssetCreationButton(animationDataAssetProperty);

            inspector.Add(prefabField);
            inspector.Add(shaderField);
            inspector.Add(presetMaterialField);
            inspector.Add(animationDataAssetField);
            inspector.Add(animationsList);
            inspector.Add(fpsField);
            inspector.Add(textureWidthField);
            inspector.Add(applyRootMotionField);
            inspector.Add(bakeAnimationsButton);

            if (createAnimationDataAssetButton != null)
            {
                inspector.Add(createAnimationDataAssetButton);
            }
        }

        private Button CreateBakeButton()
        {
            var button = new Button(() => targetAsset.Bake());
            button.text = "Bake Animations";
            return button;
        }

        private void CreateAnimationDataAssetCreationButton(SerializedProperty animationDataAssetProperty)
        {
            if (animationDataAssetProperty.objectReferenceValue == null)
            {
                if (targetAsset != null)
                {
                    try
                    {
                        var name = $"{target.name}_SharedData";
                        var path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(target));
                        createAnimationDataAssetButton = new Button(() =>
                        {
                            animationDataAssetProperty.objectReferenceValue = AnimationAssetUtility.CreateAsset(name, path);
                            serializedObject.ApplyModifiedProperties();
                            inspector.Remove(createAnimationDataAssetButton);
                            inspector.MarkDirtyRepaint();
                        });
                        createAnimationDataAssetButton.text = "Create AnimationDataAsset";
                    }
                    catch (System.Exception)
                    {
                        return;
                    }
                }
            }
        }

        private CustomListView<AnimationStateData> CreateOverridesList()
        {
            var animationsProperty = serializedObject.FindProperty(nameof(AnimationBakerAsset.animations));
            return new CustomListView<AnimationStateData>(animationsProperty, AnimationStateSelector, AnimationStateDataEqualityComparer.Default, true);
        }

        private IEnumerable<DropdownItem<AnimationStateData>> AnimationStateSelector()
        {
            var states = new List<DropdownItem<AnimationStateData>>();
            var stateNames = AnimationStatesMap.GetAnimationStateNames();

            foreach (var name in stateNames)
            {
                states.Add(new DropdownItem<AnimationStateData>()
                {
                    name = name,
                    value = new AnimationStateData()
                    {
                        stateName = name,
                        clip = default
                    }
                });
            }

            return states;
        }
    }
}

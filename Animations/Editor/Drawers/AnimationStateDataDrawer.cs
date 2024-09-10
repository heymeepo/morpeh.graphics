using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Scellecs.Morpeh.Graphics.Animations.Editor
{
    [CustomPropertyDrawer(typeof(AnimationStateData), true)]
    public sealed class AnimationStateDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var animationClipProperty = property.FindPropertyRelative(nameof(AnimationStateData.clip));
            var stateNameProperty = property.FindPropertyRelative(nameof(AnimationStateData.stateName));
            var name = string.IsNullOrEmpty(stateNameProperty.stringValue) ? "Null" : stateNameProperty.stringValue;
            var clipField = new PropertyField(animationClipProperty, name);
            container.Add(clipField);

            return container;
        }
    }
}

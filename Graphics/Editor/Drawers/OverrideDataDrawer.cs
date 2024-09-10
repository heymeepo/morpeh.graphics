using Scellecs.Morpeh.Graphics.Authoring;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Editor
{
    [CustomPropertyDrawer(typeof(OverrideData), true)]
    public sealed class OverrideDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var typeProperty = property.FindPropertyRelative(nameof(OverrideData.type));
            var nameProperty = property.FindPropertyRelative(nameof(OverrideData.name));
            var valueProperty = property.FindPropertyRelative(nameof(OverrideData.value));
            var type = (ShaderPropertyType)typeProperty.intValue;
            var name = nameProperty.stringValue;

            if (type == ShaderPropertyType.Vector)
            {
                var vec4 = new Vector4Field(name);
                vec4.BindProperty(valueProperty);
                container.Add(vec4);
            }
            else if (type == ShaderPropertyType.Color)
            {
                var color = new ColorField(name);
                color.value = valueProperty.vector4Value;
                color.RegisterValueChangedCallback(x =>
                {
                    valueProperty.vector4Value = x.newValue;
                    property.serializedObject.ApplyModifiedProperties();
                });
                container.Add(color);
            }
            else
            {
                var vec1 = new FloatField(name);
                vec1.BindProperty(valueProperty.FindPropertyRelative("x"));
                container.Add(vec1);
            }

            return container;
        }
    }
}

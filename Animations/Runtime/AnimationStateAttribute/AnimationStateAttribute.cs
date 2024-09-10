using System;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class AnimationStateAttribute : Attribute
    {
        public string EditorName { get; }

        public AnimationStateAttribute(string editorName)
        {
            EditorName = editorName;
        }
    }
}

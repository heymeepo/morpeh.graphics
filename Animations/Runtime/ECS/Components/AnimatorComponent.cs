using Scellecs.Morpeh.Graphics.Animations.TAO.VertexAnimation;
using System;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public unsafe struct AnimatorComponent : IComponent
    {
        [NonSerialized]
        internal AnimationData* animations;

        public int currentIndex;
        public float speed;
        public float time;
        public bool isDone;

        public int newIndex;
    }
}

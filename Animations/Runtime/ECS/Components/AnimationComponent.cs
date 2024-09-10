using System;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal struct AnimationComponent : IComponent
    {
        public AnimationDataAsset data;
    }
}

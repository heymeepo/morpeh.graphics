using System;
using Unity.IL2CPP.CompilerServices;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    [BatchMaterialProperty("_AnimationData", BatchMaterialPropertyFormat.Float4)]
    public struct AnimationDataProperty : IComponent
    {
        public float4 value;
    }
}

using System;
using Unity.IL2CPP.CompilerServices;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    [BatchMaterialProperty("_BaseColor", BatchMaterialPropertyFormat.Float4)]
    public struct BaseColorMaterialProperty : IComponent
    {
        public float4 value;
    }
}

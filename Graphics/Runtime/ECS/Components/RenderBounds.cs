﻿using Scellecs.Morpeh.Graphics.Culling;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    [System.Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct RenderBounds : IComponent
    {
        public AABB value;
    }
}
﻿using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    [System.Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct LightMaps : IComponent
    {
        public SceneLightmapsSharedDataAsset lightmaps;
    }
}
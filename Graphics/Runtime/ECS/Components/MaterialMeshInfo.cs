﻿using Scellecs.Morpeh.Workaround;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [System.Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal struct MaterialMeshInfo : ICleanupComponent
    {
        public BatchMeshID meshID;
        public BatchMaterialID materialID;
        public int submeshIndex;
    }
}
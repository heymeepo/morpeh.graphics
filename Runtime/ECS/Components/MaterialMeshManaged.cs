using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct MaterialMeshManaged : IComponent
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
    }
}

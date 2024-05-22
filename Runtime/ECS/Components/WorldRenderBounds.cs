using Scellecs.Morpeh.Graphics.Culling;
using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct WorldRenderBounds : IComponent
    {
        public AABB value;
    }
}

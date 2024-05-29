using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    /// <summary>
    /// A tag component that marks an entity as a blend probe.
    /// </summary>
    /// <remarks>
    /// The LightProbeUpdateSystem uses this to manage light probes.
    /// </remarks>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct BlendProbeTag : IComponent { }
}

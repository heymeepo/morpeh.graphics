using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    /// <summary>
    /// A tag component that marks an entity as a custom light probe.
    /// </summary>
    /// <remarks>
    /// The ManageSHPropertiesSystem uses this to manage shadow harmonics.
    /// </remarks>
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct CustomProbeTag : IComponent { }
}

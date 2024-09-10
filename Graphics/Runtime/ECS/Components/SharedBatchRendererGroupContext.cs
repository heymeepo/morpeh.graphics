using Unity.IL2CPP.CompilerServices;

namespace Scellecs.Morpeh.Graphics
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    internal struct SharedBatchRendererGroupContext : IComponent
    {
        public BatchRendererGroupContext brg;
    }
}

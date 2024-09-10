using Scellecs.Morpeh.Workaround;
using Unity.Collections.LowLevel.Unsafe;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct NativeGraphicsArchetypes
    {
        [NativeDisableUnsafePtrRestriction]
        public int* archetypesIndices;

        [NativeDisableUnsafePtrRestriction]
        public GraphicsArchetype* archetypes;

        [NativeDisableUnsafePtrRestriction]
        public ArchetypeProperty* properties;

        [NativeDisableUnsafePtrRestriction]
        public UnmanagedStash* propertiesStashes;

        public int usedArchetypesCount;
    }
}

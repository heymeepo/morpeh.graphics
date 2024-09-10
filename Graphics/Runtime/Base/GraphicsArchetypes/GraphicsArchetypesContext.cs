using Scellecs.Morpeh.Workaround;
using Unity.Collections;

namespace Scellecs.Morpeh.Graphics
{
    internal readonly unsafe struct GraphicsArchetypesContext
    {
        /// <summary>
        /// 
        /// </summary>
        public readonly NativeArray<ArchetypeProperty> properties;

        /// <summary>
        /// 
        /// </summary>
        public readonly NativeArray<UnmanagedStash> propertiesStashes;

        /// <summary>
        /// 
        /// </summary>
        public readonly NativeArray<int> usedGraphicsArchetypesIndices;

        /// <summary>
        /// 
        /// </summary>
        public readonly GraphicsArchetype* graphicsArchetypes;

        public GraphicsArchetypesContext(NativeArray<ArchetypeProperty> properties, NativeArray<UnmanagedStash> propertiesStashes, NativeArray<int> usedGraphicsArchetypesIndices, GraphicsArchetype* graphicsArchetypes)
        {
            this.properties = properties;
            this.propertiesStashes = propertiesStashes;
            this.usedGraphicsArchetypesIndices = usedGraphicsArchetypesIndices;
            this.graphicsArchetypes = graphicsArchetypes;
        }
    }
}

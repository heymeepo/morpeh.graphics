#if UNITY_EDITOR || DEBUG
#define THROW_EXCEPTIONS
#endif

using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Collections;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Workaround;
using System;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class GraphicsArchetypesContext
    {
        private readonly GraphicsArchetypesHandle handle;

        public GraphicsArchetypesContext(GraphicsArchetypesHandle handle) => this.handle = handle;

        public int GetTotalArchetypePropertiesCount()
        {
            ThrowExceptionIfIsNotValid();
            return handle.propertiesCount;
        }

        public ref GraphicsArchetype GetGraphicsArchetypeByIndex(in int index)
        {
            ThrowExceptionIfIsNotValid();
            return ref handle.graphicsArchetypes.GetValueRefByIndex(index);
        }

        public ref ArchetypeProperty GetArchetypePropertyByIndex(in int index)
        {
            ThrowExceptionIfIsNotValid();
            return ref handle.propertiesTypeIdCache.GetValueRefByIndex(index);
        }

        public ReadOnlyIntFastList GetUsedGraphicsArchetypesIndices()
        {
            ThrowExceptionIfIsNotValid();
            return handle.usedGraphicsArchetypesIndices.AsReadOnly();
        }

        public unsafe NativeGraphicsArchetypes AsNative()
        {
            ThrowExceptionIfIsNotValid();
            var nativeArchetypes = new NativeGraphicsArchetypes();

            fixed (int* archetypesIndicesPtr = &handle.usedGraphicsArchetypesIndices.data[0])
            fixed (GraphicsArchetype* archetypesPtr = &handle.graphicsArchetypes.data[0])
            {
                nativeArchetypes.archetypesIndices = archetypesIndicesPtr;
                nativeArchetypes.archetypes = archetypesPtr;
                nativeArchetypes.properties = handle.propertiesTypeIdCache.GetUnsafeDataPtr();
                nativeArchetypes.propertiesStashes = handle.propertiesStashes.GetUnsafePtr();
                nativeArchetypes.usedArchetypesCount = handle.usedGraphicsArchetypesIndices.length;
            }

            return nativeArchetypes;
        }

        [System.Diagnostics.Conditional("THROW_EXCEPTIONS")]
        private void ThrowExceptionIfIsNotValid()
        {
            if (handle.IsValid == false)
            {
                throw new ObjectDisposedException("GraphicsArchetypesContext has already beed disposed.");
            }
        }
    }

    internal sealed class GraphicsArchetypesHandle : IDisposable
    {
        public bool IsValid { get; private set; } = true;

        internal IntHashMap<ArchetypeProperty> propertiesTypeIdCache;
        internal ResizableArray<UnmanagedStash> propertiesStashes;
        internal LongHashMap<GraphicsArchetype> graphicsArchetypes;
        internal FastList<int> usedGraphicsArchetypesIndices;
        internal int propertiesCount;

        public void Dispose() => IsValid = false;
    }
}

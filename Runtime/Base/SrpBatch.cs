using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    internal struct SrpBatch
    {
        public BatchID batchId;
        public DrawKey drawKey;
        public Hash128 renderHash;
        public HeapBlock memoryAllocationGPU;
        public int archetypeIndex;
        public int instancesCount;
        public int capacity;
    }
}

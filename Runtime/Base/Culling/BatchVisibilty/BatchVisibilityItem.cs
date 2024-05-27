namespace Scellecs.Morpeh.Graphics.Culling
{
    internal unsafe struct BatchVisibilityItem
    {
        public int batchIndex;
        public int batchChunkBegin;
        public BatchVisibility128* visibility;
    }
}

namespace Scellecs.Morpeh.Graphics.Culling
{
    internal unsafe struct BatchVisibility128
    {
        public fixed ulong visibleEntities[2];
        public fixed byte splitMasks[128];

        public bool AnyVisible => (visibleEntities[0] | visibleEntities[1]) != 0;
    }
}

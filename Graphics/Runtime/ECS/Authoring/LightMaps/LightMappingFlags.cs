using System;

namespace Scellecs.Morpeh.Graphics
{
    [Flags]
    internal enum LightMappingFlags
    {
        None = 0,
        Lightmapped = 1,
        Directional = 2,
        ShadowMask = 4
    }
}

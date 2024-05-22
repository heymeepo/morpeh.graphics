using System.Runtime.CompilerServices;
using static Scellecs.Morpeh.Graphics.Utilities.BRGHelpers;

namespace Scellecs.Morpeh.Graphics
{
    public enum BatchMaterialPropertyFormat
    {
        Float = 0,
        Float4 = 3,
    }

    public static class BatchMaterialPropertyFormatExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetSizeFormat(this BatchMaterialPropertyFormat format)
        {
            return format == BatchMaterialPropertyFormat.Float ? SIZE_OF_FLOAT : SIZE_OF_FLOAT4;
        }
    }
}

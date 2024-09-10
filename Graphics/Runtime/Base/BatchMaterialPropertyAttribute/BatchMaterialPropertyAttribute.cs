using System;

namespace Scellecs.Morpeh.Graphics
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class BatchMaterialPropertyAttribute : Attribute
    {
        public string MaterialPropertyId { get; } 
        public BatchMaterialPropertyFormat Format { get; }

        public BatchMaterialPropertyAttribute(string materialPropertyId, BatchMaterialPropertyFormat format)
        {
            MaterialPropertyId = materialPropertyId;
            Format = format;
        }
    }
}

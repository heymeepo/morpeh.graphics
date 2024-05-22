namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct GpuUploadOperation
    {
        public enum UploadOperationKind
        {
            Memcpy = 0,
            SOAMatrixUpload3x4 = 255,
        }

        /// <summary>
        /// Which kind of upload operation this is
        /// </summary>
        public UploadOperationKind kind;

        /// <summary>
        /// Source data
        /// </summary>
        public UploadDataSource src;

        /// <summary>
        /// GPU offset to start writing destination data in
        /// </summary>
        public int dstOffset;
        /// <summary>
        /// GPU offset to start writing any inverse matrices in, if applicable
        /// </summary>
        /// 
        public int dstOffsetInverse;

        /// <summary>
        /// Size in bytes for raw operations, size in whole matrices for matrix operations
        /// </summary>
        public int size;

        /// <summary>
        /// Raw uploads require their size in bytes from the upload buffer.
        /// </summary>
        public int BytesRequiredInUploadBuffer => size;
    }
}

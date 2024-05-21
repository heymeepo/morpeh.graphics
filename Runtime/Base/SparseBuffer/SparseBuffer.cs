using System;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    internal sealed class SparseBuffer : IDisposable
    {
        public GraphicsBufferHandle Handle { get; private set; }

        internal GraphicsBuffer buffer;
        internal HeapAllocator allocator;
        internal SparseUploader uploader;
        internal ThreadedSparseUploader threadedUploader;
        internal SparseBufferArgs args;
        internal ulong bufferSize;

        public SparseBuffer(SparseBufferArgs creationArgs)
        {
            args = creationArgs;
            bufferSize = args.initialSize;
            buffer = SparseBufferUtility.CreateGraphicsBuffer(ref args, bufferSize);
            allocator = new HeapAllocator(args.maxSize, 16);
            uploader = new SparseUploader(buffer, args.uploaderChunkSize);
            threadedUploader = default;
            Handle = buffer.bufferHandle;
        }

        /// <summary>
        /// Attempt to allocate a virtual memory block for the GraphicsBuffer
        /// </summary>
        public bool Allocate(ulong size, uint alignment, out HeapBlock allocation)
        {
            allocation = allocator.Allocate(size, alignment);

            if (allocation.Empty)
            {
                Debug.Log($"Out of memory in the instance data buffer. Attempted to allocate {size}, buffer size: {allocator.Size}, free size left: {allocator.FreeSpace}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Releasing the given virtual memory block of the GraphicsBuffer and marks it as free.
        /// </summary>
        public void Free(HeapBlock allocation)
        {
            allocator.Release(allocation);
        }

        public ThreadedSparseUploader Begin(in SparseBufferUploadRequirements args, out bool resized)
        {
            resized = CheckResize();
            threadedUploader = uploader.Begin(args.totalUploadBytes, args.biggestUploadBytes, args.numOperations);
            return threadedUploader;
        }

        public bool EndAndCommit()
        {
            if (threadedUploader.IsValid)
            {
                uploader.EndAndCommit(threadedUploader);
                threadedUploader = default;
                return true;
            }

            threadedUploader = default;
            return false;
        }

        public void Dispose()
        {
            uploader.Dispose();
            allocator.Dispose();
            buffer.Dispose();

            threadedUploader = default;
            uploader = default;
            allocator = default;
            buffer = default;
        }

        private bool CheckResize()
        {
            var persistentBytes = allocator.OnePastHighestUsedAddress;

            if (persistentBytes > bufferSize)
            {
                while (bufferSize < persistentBytes)
                {
                    bufferSize *= 2;
                }

                if (bufferSize > args.maxSize)
                {
                    bufferSize = args.maxSize; // Some backends fails at loading 1024 MiB, but 1023 is fine... This should ideally be a device cap.
                }

                if (persistentBytes > args.maxSize)
                {
                    Debug.LogError($"BatchRendererGroup: Current loaded scenes need more than {args.maxSize} of persistent GPU memory. Try to reduce amount of loaded data.");
                }

                var newBuffer = SparseBufferUtility.CreateGraphicsBuffer(ref args, bufferSize);
                uploader.ReplaceBuffer(newBuffer, true);
                Handle = newBuffer.bufferHandle;
                Debug.Log(true);
                buffer.Dispose();
                buffer = newBuffer;
                return true;
            }

            return false;
        }
    }
}

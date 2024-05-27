using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Scellecs.Morpeh.Graphics
{
    [BurstCompile]
    [NoAlias]
    internal unsafe struct DrawStream<T> where T : unmanaged
    {
        public const int kArraySizeElements = 16;
        public static int ElementsPerHeader => (sizeof(Header) + sizeof(T) - 1) / sizeof(T);
        public const int ElementsPerArray = kArraySizeElements;

        public Header* Head;
        private T* m_Begin;
        private int m_Count;
        private int m_TotalInstances;

        public DrawStream(RewindableAllocator* allocator)
        {
            Head = null;
            m_Begin = null;
            m_Count = 0;
            m_TotalInstances = 0;

            Init(allocator);
        }

        public void Init(RewindableAllocator* allocator)
        {
            AllocateNewBuffer(allocator);
        }

        public bool IsCreated => Head != null;

        // No need to dispose anything with RewindableAllocator
        // public void Dispose()
        // {
        //     Header* h = Head;
        //
        //     while (h != null)
        //     {
        //         Header* next = h->Next;
        //         DisposeArray(h, kAllocator);
        //         h = next;
        //     }
        // }

        private void AllocateNewBuffer(RewindableAllocator* allocator)
        {
            LinkHead(AllocateArray(allocator));
            m_Begin = Head->Element(0);
            m_Count = 0;
            Assert.IsTrue(Head->NumElements == 0);
            Assert.IsTrue(Head->NumInstances == 0);
        }

        public void LinkHead(Header* newHead)
        {
            newHead->Next = Head;
            Head = newHead;
        }

        [BurstCompile]
        [NoAlias]
        internal unsafe struct Header
        {
            // Next array in the chain of arrays
            public Header* Next;
            // Number of structs in this array
            public int NumElements;
            // Number of instances in this array
            public int NumInstances;

            public T* Element(int i)
            {
                fixed (Header* self = &this)
                    return (T*)self + i + ElementsPerHeader;
            }
        }

        public int TotalInstanceCount => m_TotalInstances;

        public static Header* AllocateArray(RewindableAllocator* allocator)
        {
            int alignment = math.max(
                UnsafeUtility.AlignOf<Header>(),
                UnsafeUtility.AlignOf<T>());

            // Make sure we always have space for ElementsPerArray elements,
            // so several streams can be kept in lockstep
            int allocCount = ElementsPerHeader + ElementsPerArray;

            Header* buffer = (Header*)allocator->Allocate(sizeof(T), alignment, allocCount);

            // Zero clear the header area (first struct)
            UnsafeUtility.MemSet(buffer, 0, sizeof(Header));

            // Buffer allocation pointer, to be used for Free()
            return buffer;
        }

        // Assume that the given header is part of an array allocated with AllocateArray,
        // and release the array.
        // public static void DisposeArray(Header* header, Allocator allocator)
        // {
        //     UnsafeUtility.Free(header, allocator);
        // }

        [return: NoAlias]
        public T* AppendElement(RewindableAllocator* allocator)
        {
            if (m_Count >= ElementsPerArray)
                AllocateNewBuffer(allocator);

            T* elem = m_Begin + m_Count;
            ++m_Count;
            Head->NumElements += 1;
            return elem;
        }

        public void AddInstances(int numInstances)
        {
            Head->NumInstances += numInstances;
            m_TotalInstances += numInstances;
        }
    }

    [BurstCompile]
    [NoAlias]
    internal unsafe struct DrawCommandStream
    {
        private DrawStream<DrawCommandVisibility> m_Stream;
        private DrawStream<IntPtr> m_ChunkTransformsStream;
        private int m_PrevChunkStartIndex;
        [NoAlias]
        private DrawCommandVisibility* m_PrevVisibility;

        public DrawCommandStream(RewindableAllocator* allocator)
        {
            m_Stream = new DrawStream<DrawCommandVisibility>(allocator);
            m_ChunkTransformsStream = default; // Don't allocate here, only on demand
            m_PrevChunkStartIndex = -1;
            m_PrevVisibility = null;
        }

        public void Dispose()
        {
            // m_Stream.Dispose();
        }

        public void Emit(RewindableAllocator* allocator, int qwordIndex, int bitIndex, int chunkStartIndex)
        {
            DrawCommandVisibility* visibility;

            if (chunkStartIndex == m_PrevChunkStartIndex)
            {
                visibility = m_PrevVisibility;
            }
            else
            {
                visibility = m_Stream.AppendElement(allocator);
                *visibility = new DrawCommandVisibility(chunkStartIndex);
            }

            visibility->VisibleInstances[qwordIndex] |= 1ul << bitIndex;

            m_PrevChunkStartIndex = chunkStartIndex;
            m_PrevVisibility = visibility;
            m_Stream.AddInstances(1);
        }

        public void EmitDepthSorted(RewindableAllocator* allocator,
            int qwordIndex, int bitIndex, int chunkStartIndex,
            float4x4* chunkTransforms)
        {
            DrawCommandVisibility* visibility;

            if (chunkStartIndex == m_PrevChunkStartIndex)
            {
                visibility = m_PrevVisibility;

                // Transforms have already been written when the element was added
            }
            else
            {
                visibility = m_Stream.AppendElement(allocator);
                *visibility = new DrawCommandVisibility(chunkStartIndex);

                // Store a pointer to the chunk transform array, which
                // instance expansion can use to get the positions.

                if (!m_ChunkTransformsStream.IsCreated)
                    m_ChunkTransformsStream.Init(allocator);

                var transforms = m_ChunkTransformsStream.AppendElement(allocator);
                *transforms = (IntPtr)chunkTransforms;
            }

            visibility->VisibleInstances[qwordIndex] |= 1ul << bitIndex;

            m_PrevChunkStartIndex = chunkStartIndex;
            m_PrevVisibility = visibility;
            m_Stream.AddInstances(1);
        }

        public DrawStream<DrawCommandVisibility> Stream => m_Stream;
        public DrawStream<IntPtr> TransformsStream => m_ChunkTransformsStream;
    }
}

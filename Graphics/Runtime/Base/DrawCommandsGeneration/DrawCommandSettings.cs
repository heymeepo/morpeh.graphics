using System;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    internal unsafe struct DrawCommandSettings : IEquatable<DrawCommandSettings>
    {
        public int FilterIndex;
        public BatchDrawCommandFlags Flags;
        public BatchMaterialID MaterialID;
        public BatchMeshID MeshID;
        public ushort SplitMask;
        public ushort SubMeshIndex;
        public BatchID BatchID;
        private int m_CachedHash;

        public bool Equals(DrawCommandSettings other)
        {
            // Use temp variables so CPU can co-issue all comparisons
            bool eq_batch = BatchID == other.BatchID;
            bool eq_rest = math.all(PackedUint4 == other.PackedUint4);

            return eq_batch && eq_rest;
        }

        private uint4 PackedUint4
        {
            get
            {
                Assert.IsTrue(MeshID.value < (1 << 24));
                Assert.IsTrue(SubMeshIndex < (1 << 8));
                Assert.IsTrue((uint)Flags < (1 << 24));
                Assert.IsTrue(SplitMask < (1 << 8));

                return new uint4(
                    (uint)FilterIndex,
                    (((uint)SplitMask & 0xff) << 24) | ((uint)Flags & 0x00ffffffff),
                    MaterialID.value,
                    ((MeshID.value & 0x00ffffff) << 8) | ((uint)SubMeshIndex & 0xff)
                );
            }
        }

        public int CompareTo(DrawCommandSettings other)
        {
            uint4 a = PackedUint4;
            uint4 b = other.PackedUint4;
            int cmp_batchID = BatchID.CompareTo(other.BatchID);

            int4 lt = math.select(int4.zero, new int4(-1), a < b);
            int4 gt = math.select(int4.zero, new int4(1), a > b);
            int4 neq = lt | gt;

            int* firstNonZero = stackalloc int[4];

            bool4 nz = neq != int4.zero;
            bool anyNz = math.any(nz);
            math.compress(firstNonZero, 0, neq, nz);

            return anyNz ? firstNonZero[0] : cmp_batchID;
        }

        // Used to verify correctness of fast CompareTo
        public int CompareToReference(DrawCommandSettings other)
        {
            int cmpFilterIndex = FilterIndex.CompareTo(other.FilterIndex);
            int cmpFlags = ((int)Flags).CompareTo((int)other.Flags);
            int cmpMaterialID = MaterialID.CompareTo(other.MaterialID);
            int cmpMeshID = MeshID.CompareTo(other.MeshID);
            int cmpSplitMask = SplitMask.CompareTo(other.SubMeshIndex);
            int cmpSubMeshIndex = SubMeshIndex.CompareTo(other.SubMeshIndex);
            int cmpBatchID = BatchID.CompareTo(other.BatchID);

            if (cmpFilterIndex != 0) return cmpFilterIndex;
            if (cmpFlags != 0) return cmpFlags;
            if (cmpMaterialID != 0) return cmpMaterialID;
            if (cmpMeshID != 0) return cmpMeshID;
            if (cmpSubMeshIndex != 0) return cmpSubMeshIndex;
            if (cmpSplitMask != 0) return cmpSplitMask;

            return cmpBatchID;
        }

        public override int GetHashCode() => m_CachedHash;

        public void ComputeHashCode()
        {
            m_CachedHash = DrawCommandOutput.FastHash(this);
        }

        public bool HasSortingPosition => (int)(Flags & BatchDrawCommandFlags.HasSortingPosition) != 0;

        public override string ToString()
        {
            return $"DrawCommandSettings(batchID: {BatchID.value}, materialID: {MaterialID.value}, meshID: {MeshID.value}, submesh: {SubMeshIndex}, filter: {FilterIndex}, flags: {Flags:x}, splitMask: {SplitMask:x})";
        }
    }
}

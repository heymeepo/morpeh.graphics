﻿using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    internal static class BRGHelpers
    {
        public const int SIZE_OF_UINT = sizeof(uint);
        public const int SIZE_OF_FLOAT = sizeof(float);
        public const int SIZE_OF_FLOAT2 = SIZE_OF_FLOAT * 2;
        public const int SIZE_OF_FLOAT3 = SIZE_OF_FLOAT * 3;
        public const int SIZE_OF_FLOAT4 = SIZE_OF_FLOAT * 4;
        public const int SIZE_OF_MATRIX2X4 = SIZE_OF_FLOAT4 * 2;
        public const int SIZE_OF_MATRIX3X4 = SIZE_OF_FLOAT4 * 3;
        public const int SIZE_OF_MATRIX4X4 = SIZE_OF_FLOAT4 * 4;

        public const long GPU_BUFFER_INITIAL_SIZE = 32 * 1024 * 1024;
        public const long GPU_BUFFER_MAX_SIZE = 1023 * 1024 * 1024;

        public const int GPU_UPLOADER_CHUNK_SIZE = 4 * 1024 * 1024;
        public const int BYTES_PER_BATCH_RAW_BUFFER = 256 * 1024;

        public const int MAX_INSTANCES_PER_DRAW_COMMAND = 4096;
        public const int MAX_INSTANCES_PER_DRAW_RANGE = 4096;
        public const int MAX_DRAW_COMMANDS_PER_DRAW_RANGE = 512;

        public const uint BATCH_ALLOCATION_ALIGNMENT = 16;
        public const uint MSB = 0x80000000;

        public static readonly int OBJECT_TO_WORLD_ID = Shader.PropertyToID("unity_ObjectToWorld");
        public static readonly int WORLD_TO_OBJECT_ID = Shader.PropertyToID("unity_WorldToObject");

        public static BatchID IntAsBatchID(int id) => new BatchID() { value = (uint)id };

        public static int AsInt(this BatchID batchId) => (int)batchId.value;

        public static int Align16Bytes(int size) => ((size + 15) >> 4) << 4;

        public static MetadataValue CreateMetadataValue(int nameID, int gpuAddress)
        {
            return new MetadataValue
            {
                NameID = nameID,
                Value = (uint)gpuAddress | MSB
            };
        }
    }
}
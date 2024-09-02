#if UNITY_EDITOR
using UnityEngine;
using System;
using Unity.Collections;

namespace Scellecs.Morpeh.Graphics
{
    [Serializable]
    internal struct LightMapKey : IEquatable<LightMapKey>
    {
        public Hash128 colorHash;
        public Hash128 directionHash;
        public Hash128 shadowMaskHash;

        public LightMapKey(UnityEngine.LightmapData lightmapData)
            : this(lightmapData.lightmapColor,
                lightmapData.lightmapDir,
                lightmapData.shadowMask)
        {
        }

        public LightMapKey(Texture2D color, Texture2D direction, Texture2D shadowMask)
        {
            colorHash = default;
            directionHash = default;
            shadowMaskHash = default;

            if (color != null) colorHash = color.imageContentsHash;
            if (direction != null) directionHash = direction.imageContentsHash;
            if (shadowMask != null) shadowMaskHash = shadowMask.imageContentsHash;
        }

        public bool Equals(LightMapKey other)
        {
            return colorHash.Equals(other.colorHash) && directionHash.Equals(other.directionHash) && shadowMaskHash.Equals(other.shadowMaskHash);
        }

        public override int GetHashCode()
        {
            var hash = new xxHash3.StreamingState(true);
            hash.Update(colorHash);
            hash.Update(directionHash);
            hash.Update(shadowMaskHash);
            return (int)hash.DigestHash64().x;
        }
    }
}
#endif
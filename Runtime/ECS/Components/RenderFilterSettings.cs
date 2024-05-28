using System;
using Unity.Collections;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    [System.Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.DivideByZeroChecks, false)]
    public struct RenderFilterSettings : IComponent, IEquatable<RenderFilterSettings>
    {
        /// For entities that Unity converts from GameObjects, this value is the same as the Layer setting of the source
        /// GameObject.
        /// </summary>
        public int layer;

        /// <summary>
        /// The rendering layer the entity is part of.
        /// </summary>
        /// <remarks>
        /// This value corresponds to <see cref="Renderer.renderingLayerMask"/>.
        /// </remarks>
        public uint renderingLayerMask;

        /// <summary>
        /// Specifies what kinds of motion vectors to generate for the entity, if any.
        /// </summary>
        /// <remarks>
        /// This value corresponds to <see cref="Renderer.motionVectorGenerationMode"/>.
        ///
        /// This value only affects render pipelines that use motion vectors.
        /// </remarks>
        public MotionVectorGenerationMode motionMode;

        /// <summary>
        /// Specifies how the entity should cast shadows.
        /// </summary>
        /// <remarks>
        /// For entities that Unity converts from GameObjects, this value is the same as the Cast Shadows property of the source
        /// Mesh Renderer component.
        /// For more information, refer to [ShadowCastingMode](https://docs.unity3d.com/ScriptReference/Rendering.ShadowCastingMode.html).
        /// </remarks>
        public ShadowCastingMode shadowCastingMode;

        /// <summary>
        /// Indicates whether to cast shadows onto the entity.
        /// </summary>
        /// <remarks>
        /// For entities that Unity converts from GameObjects, this value is the same as the Receive Shadows property of the source
        /// Mesh Renderer component.
        /// This value only affects [Progressive Lightmappers](https://docs.unity3d.com/Manual/ProgressiveLightmapper.html).
        /// </remarks>
        public bool receiveShadows;

        /// <summary>
        /// Indicates whether the entity is a static shadow caster.
        /// </summary>
        /// <remarks>
        /// This value is important to the BatchRenderGroup.
        /// </remarks>
        public bool staticShadowCaster;

        /// <summary>
        /// Returns a new default instance of RenderFilterSettings.
        /// </summary>
        public static RenderFilterSettings Default => new RenderFilterSettings
        {
            layer = 0,
            renderingLayerMask = 0xffffffff,
            motionMode = MotionVectorGenerationMode.Object,
            shadowCastingMode = ShadowCastingMode.On,
            receiveShadows = true,
            staticShadowCaster = false,
        };

        /// <summary>
        /// Indicates whether the motion mode for the current pass is not camera.
        /// </summary>
        public bool IsInMotionPass => motionMode != MotionVectorGenerationMode.Camera;

        /// <summary>
        /// Indicates whether the current instance is equal to the specified object.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>Returns true if the current instance is equal to the specified object. Otherwise, returns false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is RenderFilterSettings)
                return Equals((RenderFilterSettings)obj);

            return false;
        }

        /// <summary>
        /// Indicates whether the current instance is equal to the specified RenderFilterSettings.
        /// </summary>
        /// <param name="other">The RenderFilterSettings to compare with the current instance.</param>
        /// <returns>Returns true if the current instance is equal to the specified RenderFilterSettings. Otherwise, returns false.</returns>
        public bool Equals(RenderFilterSettings other)
        {
            return layer == other.layer && renderingLayerMask == other.renderingLayerMask && motionMode == other.motionMode && shadowCastingMode == other.shadowCastingMode && receiveShadows == other.receiveShadows && staticShadowCaster == other.staticShadowCaster;
        }

        /// <summary>
        /// Calculates the hash code for this object.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            var hash = new xxHash3.StreamingState(true);
            hash.Update(layer);
            hash.Update(renderingLayerMask);
            hash.Update(motionMode);
            hash.Update(shadowCastingMode);
            hash.Update(receiveShadows);
            hash.Update(staticShadowCaster);
            return (int)hash.DigestHash64().x;
        }

        /// <inheritdoc/>
        public static bool operator ==(RenderFilterSettings left, RenderFilterSettings right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(RenderFilterSettings left, RenderFilterSettings right)
        {
            return !left.Equals(right);
        }
    }
}

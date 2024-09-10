using System;

namespace Scellecs.Morpeh.Graphics.Animations.TAO.VertexAnimation
{
    [Serializable]
    internal struct AnimationData
    {
        /// <summary>
        /// The frames in this animation.
        /// </summary>
        public int frames;

        /// <summary>
        /// The maximum of frames the texture holds.
        /// </summary>
        public int maxFrames;

        /// <summary>
        /// The index of the related animation texture.
        /// </summary>
        public int animationMapIndex;

        /// <summary>
        /// The index of the related color textures if/when added.
        /// </summary>
        public int colorMapIndex;

        /// <summary>
        /// Time of a single frame.
        /// </summary>
        public float frameTime;

        /// <summary>
        /// Total time of the animation.
        /// </summary>
        public float duration;

        /// <summary>
        /// Is the animation looping
        /// </summary>
        public bool isLooping;

        public AnimationData(int frames, int maxFrames, int fps, int positionMapIndex, bool isLooping, int colorMapIndex = -1)
        {
            this.frames = frames;
            this.maxFrames = maxFrames;
            animationMapIndex = positionMapIndex;
            this.colorMapIndex = colorMapIndex;
            frameTime = 1.0f / maxFrames * fps;
            duration = 1.0f / maxFrames * (frames - 1);
            this.isLooping = isLooping;
        }
    }
}
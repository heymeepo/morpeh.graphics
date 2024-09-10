#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [Serializable]
    public struct AnimationStateData
    {
        [SerializeField]
        public AnimationClip clip;

        [SerializeField]
        public string stateName;
    }

    public class AnimationStateDataEqualityComparer : IEqualityComparer<AnimationStateData>
    {
        public static AnimationStateDataEqualityComparer Default { get; private set; } = new AnimationStateDataEqualityComparer();

        public bool Equals(AnimationStateData x, AnimationStateData y) => x.stateName.Equals(y.stateName);

        public int GetHashCode(AnimationStateData obj) => obj.stateName.GetHashCode();
    }
}
#endif

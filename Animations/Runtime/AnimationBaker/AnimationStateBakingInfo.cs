#if UNITY_EDITOR
using System;

namespace Scellecs.Morpeh.Graphics.Animations
{
    [Serializable]
    internal struct AnimationStateBakingInfo
    {
        public string stateName;
        public int index;
    }
}
#endif

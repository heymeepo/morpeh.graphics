#if MORPEH_ENTITY_CONVERTER && UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics.Authoring
{
    [Serializable]
    public struct OverrideData
    {
        public string name;
        public ShaderPropertyType type;
        public Vector4 value;
    }


    public class OverrideDataEqualityComparer : IEqualityComparer<OverrideData>
    {
        public static OverrideDataEqualityComparer Default = new OverrideDataEqualityComparer();

        public bool Equals(OverrideData x, OverrideData y) => x.name.Equals(y.name) && x.type.Equals(y.type);

        public int GetHashCode(OverrideData obj) => obj.name.GetHashCode();
    }
}
#endif

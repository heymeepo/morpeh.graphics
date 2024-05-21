using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics
{
    [Serializable]
    public sealed class RenderData
    {
        public Mesh sharedMesh;
        public Material sharedMaterial;
        [SerializeReference] public List<IComponent> materialProperties = new List<IComponent>();
    }
}

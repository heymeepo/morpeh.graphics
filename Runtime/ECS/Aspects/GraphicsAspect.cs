using Scellecs.Morpeh.Transforms;

namespace Scellecs.Morpeh.Graphics
{
    public struct GraphicsAspect : IFilterExtension
    {
        public FilterBuilder Extend(FilterBuilder rootFilter)
        {
            return rootFilter
                .With<LocalToWorld>()
                .With<RenderBounds>()
                .With<WorldRenderBounds>()
                .With<MaterialMeshInfo>()
                .With<RenderFilterSettingsIndex>()
                .Without<DisableRendering>();
        }
    }
}

using System;

namespace Scellecs.Morpeh.Graphics.Utilities
{
    internal static class EcsHelpers
    {
        public static BatchRendererGroupContext GetBatchRendererGroupContext(World world)
        {
            var brgStash = world.GetStash<SharedBatchRendererGroupContext>();
            var enumerator = brgStash.GetEnumerator();

            if (enumerator.MoveNext())
            {
                return enumerator.Current.brg;
            }
            else
            {
                throw new NotImplementedException("BatchRendererGroupContext not found, most likely you have not added BatchRendererInitializer to the world.");
            }
        }

        public static GraphicsArchetypesContext GetGraphicsArchetypesContext(World world)
        {
            var brgStash = world.GetStash<SharedGraphicsArchetypesContext>();
            var enumerator = brgStash.GetEnumerator();

            if (enumerator.MoveNext())
            {
                return enumerator.Current.graphicsArchetypes;
            }
            else
            {
                throw new NotImplementedException("GraphicsArchetypesContext not found, most likely you have not added GraphicsArchetypesSystem to the world, or you have setup the systems in the wrong order.");
            }
        }
    }
}

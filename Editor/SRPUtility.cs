using System;
using System.Linq;
using System.Reflection;

namespace Scellecs.Morpeh.Graphics.Editor
{
    internal static class SRPUtility
    {
        public static string[] RenderingLayerMaskNames
        {
#if URP_10_0_0_OR_NEWER
            get
            {
                if (urpGlobalSettingsInstance == null)
                {
                    LoadRenderingLayerNames();
                }

                return renderingLayerMaskNamesProperty.GetValue(urpGlobalSettingsInstance) as string[];
            }
#else
            get => Array.Empty<string>();
#endif
        }

        private static PropertyInfo renderingLayerMaskNamesProperty;

#if URP_10_0_0_OR_NEWER
        private static object urpGlobalSettingsInstance;
        private static void LoadRenderingLayerNames()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.GlobalAssemblyCache && a.FullName.StartsWith("Unity.RenderPipelines.Universal.Runtime")).FirstOrDefault();
            //TODO:
            //reload instance on each method call
            //use own collection of prefixed based on renderingLayerMaskNames, because prefixedRenderingLayerNames are not clearing after reset
            if (asm != null)
            {
                var urpGlobalSettingsType = asm.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
                var instanceProperty = urpGlobalSettingsType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                urpGlobalSettingsInstance = instanceProperty.GetValue(null);
                renderingLayerMaskNamesProperty = urpGlobalSettingsType.GetProperty("prefixedRenderingLayerNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }
#endif
    }
}

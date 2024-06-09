#if MORPEH_ENTITY_CONVERTER && UNITY_EDITOR
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Scellecs.Morpeh.Graphics.Baking
{
    internal static class MaterialOverriesMap
    {
        private static Dictionary<string, Type> map = new Dictionary<string, Type>();

        [InitializeOnLoadMethod]
        public static void Reload()
        {
            map.Clear();

            var overrideComponentsTypes = ReflectionHelpers
                .GetAssemblies()
                .GetTypesWithAttribute<BatchMaterialPropertyAttribute>()
                .Where(x => typeof(IComponent).IsAssignableFrom(x) && x.IsValueType);

            foreach (var overrideType in overrideComponentsTypes)
            {
                var attribute = overrideType.GetAttribute<BatchMaterialPropertyAttribute>();
                map.Add(attribute.MaterialPropertyId, overrideType);
            }
        }

        public static bool TryGetOverrideType(string propertyName, out Type componentType) => map.TryGetValue(propertyName, out componentType);
    }
}
#endif

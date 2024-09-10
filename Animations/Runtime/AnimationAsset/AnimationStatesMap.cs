#if UNITY_EDITOR
using Scellecs.Morpeh.Workaround.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scellecs.Morpeh.Graphics.Animations
{
    internal static class AnimationStatesMap
    {
        private static Dictionary<string, Type> map = new Dictionary<string, Type>();

        [InitializeOnLoadMethod]
        public static void Reload()
        {
            map.Clear();

            var animationComponentTypes = ReflectionHelpers
                .GetAssemblies()
                .GetTypesWithAttribute<AnimationStateAttribute>()
                .Where(x => typeof(IComponent).IsAssignableFrom(x) && x.IsValueType);

            foreach (var componentType in animationComponentTypes)
            {
                var attribute = componentType.GetAttribute<AnimationStateAttribute>();
                var key = attribute.EditorName;

                if (map.TryGetValue(key, out var type))
                {
                    Debug.LogError($"Duplicate keys found in AnimationState, which is not allowed.\n {key} key already exists for {type}, the component with type {componentType} will be ignored.\n Please set a different keys for types {type} and {componentType}.");
                    continue;
                }

                map.Add(key, componentType);
            }
        }

        public static IEnumerable<string> GetAnimationStateNames() => map.Keys;

        public static bool TryGetAnimationComponentType(string stateName, out Type componentType) => map.TryGetValue(stateName, out componentType);
    }
}
#endif

using Scellecs.Morpeh.Graphics.Animations.TAO.VertexAnimation;
using Scellecs.Morpeh.Graphics.Utilities;
using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

namespace Scellecs.Morpeh.Graphics.Animations
{
#if MORPEH_ELYSIUM
    public sealed class AnimatorInitializeSystem : Elysium.IUpdateSystem //TODO: Rework to Blobs, remove this system
#else
    public sealed class AnimatorInitializeSystem : ISystem
#endif
    {   
        public World World { get; set; } 

        private Dictionary<int, IntPtr> animationDataMap;
        private Filter filter;

        private Stash<AnimationComponent> animationStash;
        private Stash<AnimatorComponent> animatorStash;

        public void OnAwake()
        {
            animationDataMap = new Dictionary<int, IntPtr>();

            filter = World.Filter
                .With<AnimationComponent>()
                .With<AnimationDataProperty>()
                .Without<AnimatorComponent>()
                .Build();

            animationStash = World.GetStash<AnimationComponent>();
            animatorStash = World.GetStash<AnimatorComponent>();
        }

        public unsafe void OnUpdate(float deltaTime)
        {
            foreach (var entity in filter)
            {
                ref var animationComponent = ref animationStash.Get(entity);
                var key = animationComponent.data.id;

                if (animationDataMap.TryGetValue(key, out var animationData) == false)
                {
                    var animationDataPtr = UnsafeHelpers.Malloc<AnimationData>(animationComponent.data.animations.Length, Allocator.Persistent);

                    for (int i = 0; i < animationComponent.data.animations.Length; i++)
                    {
                        animationDataPtr[i] = animationComponent.data.animations[i];
                    }

                    animationDataMap[key] = animationData = (IntPtr)animationDataPtr;
                }

                animatorStash.Set(entity, new AnimatorComponent()
                {
                    currentIndex = 0,
                    speed = 1f,
                    animations = (AnimationData*)animationData,
                    isDone = false,
                    newIndex = 0,
                });
            }
        }

        public void Dispose()
        {
            foreach (var ptr in animationDataMap.Values)
            {
                unsafe
                {
                    UnsafeUtility.Free((void*)ptr, Allocator.Persistent);
                }
            }

            animationDataMap.Clear();
            animationDataMap = null;
        }
    }
}

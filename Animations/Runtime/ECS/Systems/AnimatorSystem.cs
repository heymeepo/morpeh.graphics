using Scellecs.Morpeh.Native;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Scellecs.Morpeh.Graphics.Animations
{
#if MORPEH_ELYSIUM
    public sealed class AnimatorSystem : Elysium.IUpdateSystem
#else
    public sealed class AnimatorSystem : ISystem
#endif
    {
        public World World { get; set; }

        private Filter filter;

        private Stash<AnimatorComponent> animatorStash;
        private Stash<AnimationDataProperty> animationPropertyStash;

        public void OnAwake()
        {
            filter = World.Filter
                .With<AnimatorComponent>()
                .With<AnimationDataProperty>()
                .Build();

            animatorStash = World.GetStash<AnimatorComponent>();
            animationPropertyStash = World.GetStash<AnimationDataProperty>();
        }

        public void OnUpdate(float deltaTime)
        {
            var nativeFilter = filter.AsNative();

            new AnimatorJob()
            {
                filter = nativeFilter,
                animatorStash = animatorStash.AsNative(),
                animationPropertyStash = animationPropertyStash.AsNative(),
                deltaTime = deltaTime
            }
            .ScheduleParallel(nativeFilter.length, 32, default)
            .Complete();
        }

        public void Dispose() { }
    }

    [BurstCompile]
    public unsafe struct AnimatorJob : IJobFor
    {
        public NativeFilter filter;
        public NativeStash<AnimatorComponent> animatorStash;
        public NativeStash<AnimationDataProperty> animationPropertyStash;

        [ReadOnly] public float deltaTime;

        public void Execute(int index)
        {
            var entityId = filter[index];
            ref var animator = ref animatorStash.Get(entityId);

            if (animator.currentIndex != animator.newIndex)
            {
                animator.currentIndex = animator.newIndex;
                animator.time = 0f;
                animator.isDone = false;
            }

            if (animator.isDone)
            {
                return;
            }

            ref var animationProperty = ref animationPropertyStash.Get(entityId);
            ref var animation = ref animator.animations[animator.currentIndex];

            animator.time += deltaTime * animator.speed * animation.frameTime;
            var animationTimeNext = animator.time + (1f / animation.maxFrames);

            if (animationTimeNext > animation.duration)
            {
                if (animation.isLooping)
                {
                    animationTimeNext -= animator.time;
                    animator.time = 0f;
                }
                else
                {
                    animator.time = animation.duration;
                    animationTimeNext = animation.duration;
                    animator.isDone = true;
                }
            }

            animationProperty.value = new float4(animator.time, animation.animationMapIndex, animationTimeNext, animation.animationMapIndex);
        }
    }
}

using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Transforms;
using Unity.Burst;
using Unity.Jobs;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class RenderBoundsSystem : ICleanupSystem
    {
        public World World { get; set; }

        private Filter filter;

        private Stash<RenderBounds> renderBoundsStash;
        private Stash<WorldRenderBounds> worldRenderBoundsStash;
        private Stash<LocalToWorld> localToWorldStash;

        public void OnAwake()
        {
            filter = World.Filter
                .With<RenderBounds>()
                .With<WorldRenderBounds>()
                .With<LocalToWorld>()
                .Build();

            renderBoundsStash = World.GetStash<RenderBounds>();
            worldRenderBoundsStash = World.GetStash<WorldRenderBounds>();
            localToWorldStash = World.GetStash<LocalToWorld>();
        }

        public void OnUpdate(float deltaTime)
        {
            var inputDeps = World.JobHandle;
            var nativeFilter = filter.AsNative();

            var updateWorldBoundsHandle = new UpdateWorldRenderBoundsJob()
            {
                filter = nativeFilter,
                renderBoundsStash = renderBoundsStash.AsNative(),
                worldRenderBoundsStash = worldRenderBoundsStash.AsNative(),
                localToWorldStash = localToWorldStash.AsNative()
            }
            .ScheduleParallel(nativeFilter.length, 64, inputDeps);

            World.JobHandle = updateWorldBoundsHandle;
        }

        public void Dispose() { }
    }

    [BurstCompile]
    internal struct UpdateWorldRenderBoundsJob : IJobFor
    {
        public NativeFilter filter;

        public NativeStash<RenderBounds> renderBoundsStash;
        public NativeStash<WorldRenderBounds> worldRenderBoundsStash;
        public NativeStash<LocalToWorld> localToWorldStash;

        public void Execute(int index)
        {
            var entityId = filter[index];

            ref var localBounds = ref renderBoundsStash.Get(entityId);
            ref var worldBounds = ref worldRenderBoundsStash.Get(entityId);
            ref var localToWorld = ref localToWorldStash.Get(entityId);

            worldBounds.value = AABB.Transform(localToWorld.value, localBounds.value);
        }
    }
}

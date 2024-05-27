using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Native;
using Scellecs.Morpeh.Workaround;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class BatchFilterSettingsSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;

        private Filter changeBatchFilterSettingsIndex;

        private Stash<RenderFilterSettings> filterSettingsStash;
        private Stash<RenderFilterSettingsIndex> filterSettingsIndicesStash;

        public void OnAwake()
        {
            brg = BrgHelpersNonBursted.GetBatchRendererGroupContext(World);

            changeBatchFilterSettingsIndex = World.Filter
                .With<RenderFilterSettings>()
                .Build();

            filterSettingsStash = World.GetStash<RenderFilterSettings>();
            filterSettingsIndicesStash = World.GetStash<RenderFilterSettingsIndex>();
        }

        public unsafe void OnUpdate(float deltaTime)
        {
            if (filterSettingsStash.Length > 0)
            {
                foreach (var entity in changeBatchFilterSettingsIndex)
                {
                    ref var filterSettings = ref filterSettingsStash.Get(entity);
                    var settingsIndex = brg.GetBatchFilterSettingsIndex(ref filterSettings);
                    filterSettingsIndicesStash.Set(entity, new RenderFilterSettingsIndex() { index = settingsIndex });
                }

                filterSettingsStash.RemoveAll();
            }
        }

        public void Dispose() { }
    }
}

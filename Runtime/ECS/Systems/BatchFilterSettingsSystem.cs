using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    /// <summary>
    /// Manages batch filter settings of graphics entities.
    /// Should be called before <see cref="GraphicsArchetypesSystem"/>.
    /// </summary>
    public sealed class BatchFilterSettingsSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;

        private Filter setDefaultFilterSettings;
        private Filter changeBatchFilterSettingsIndex;

        private Stash<RenderFilterSettings> filterSettingsStash;
        private Stash<RenderFilterSettingsIndex> filterSettingsIndicesStash;

        private int defaultBatchFilterSettingsIndex;

        public void OnAwake()
        {
            brg = BrgHelpersNonBursted.GetBatchRendererGroupContext(World);

            setDefaultFilterSettings = World.Filter
                .With<MaterialMeshInfo>()
                .Without<RenderFilterSettings>()
                .Without<RenderFilterSettingsIndex>()
                .Build();

            changeBatchFilterSettingsIndex = World.Filter
                .With<RenderFilterSettings>()
                .Build();

            filterSettingsStash = World.GetStash<RenderFilterSettings>();
            filterSettingsIndicesStash = World.GetStash<RenderFilterSettingsIndex>();

            InitDefaultIndex();
        }

        public unsafe void OnUpdate(float deltaTime)
        {
            SetDefaultFilterIndex();
            ChangeBatchFilterSettingsIndex();
        }

        private void InitDefaultIndex()
        {
            var def = RenderFilterSettings.Default;
            defaultBatchFilterSettingsIndex = brg.GetBatchFilterSettingsIndex(ref def);
        }

        private void SetDefaultFilterIndex()
        {
            foreach (var entity in setDefaultFilterSettings)
            {
                filterSettingsIndicesStash.Set(entity, new RenderFilterSettingsIndex() { index = defaultBatchFilterSettingsIndex });
            }
        }

        private void ChangeBatchFilterSettingsIndex()
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

using Scellecs.Morpeh.Graphics.Utilities;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class ManageSHPropertiesSystem : ICleanupSystem
    {
        public World World { get; set; }

        private Filter missingSHCustomFilter;
        private Filter missingSHBlendFilter;
        private Filter missingProbeTagFilter;
        private Filter removeSHFromBlendProbeTagFilter;

        private Stash<BuiltinMaterialPropertyUnity_SHCoefficients> SHStash;

        public void OnAwake()
        {
            missingSHCustomFilter = World.Filter
                .With<CustomProbeTag>()
                .Without<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .Build();

            missingSHBlendFilter = World.Filter
                .With<BlendProbeTag>()
                .Without<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .Build();

            missingProbeTagFilter = World.Filter
                .With<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .Without<BlendProbeTag>()
                .Without<CustomProbeTag>()
                .Build();

            removeSHFromBlendProbeTagFilter = World.Filter
                .With<BuiltinMaterialPropertyUnity_SHCoefficients>()
                .With<BlendProbeTag>()
                .Build();

            SHStash = World.GetStash<BuiltinMaterialPropertyUnity_SHCoefficients>();
        }

        public void OnUpdate(float deltaTime)
        {
            var validGrid = LightProbeHelpers.IsValidLightProbeGrid();

            foreach (var entity in missingSHCustomFilter)
            {
                SHStash.Add(entity);
            }

            if (validGrid)
            {
                foreach (var entity in missingSHBlendFilter)
                {
                    SHStash.Add(entity);
                }
            }
            else
            {
                foreach (var entity in removeSHFromBlendProbeTagFilter)
                {
                    SHStash.Remove(entity);
                }
            }

            foreach (var entity in missingProbeTagFilter)
            {
                SHStash.Remove(entity);
            }
        }

        public void Dispose() { }
    }
}

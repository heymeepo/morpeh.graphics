using Scellecs.Morpeh.Graphics.Culling;
using Scellecs.Morpeh.Graphics.Utilities;
using Scellecs.Morpeh.Transforms;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class MaterialMeshSystem : ICleanupSystem
    {
        public World World { get; set; }

        private BatchRendererGroupContext brg;

        private Filter registerMaterialMeshFilter;
        private Filter unregisterMaterialMeshFilter;
        private Filter changeMaterialMeshFilter;

        private Stash<MaterialMeshInfo> materialMeshInfoStash;
        private Stash<MaterialMeshManaged> materialMeshManagedStash;

        private Stash<RenderBounds> renderBoundsStash;
        private Stash<WorldRenderBounds> worldRenderBoundsStash;

        public void OnAwake()
        {
            brg = EcsHelpers.GetBatchRendererGroupContext(World);

            registerMaterialMeshFilter = World.Filter
                .With<LocalToWorld>()
                .With<MaterialMeshManaged>()
                .Without<MaterialMeshInfo>()
                .Without<RenderBounds>()
                .Without<WorldRenderBounds>()
                .Build();

            unregisterMaterialMeshFilter = World.Filter
                .With<MaterialMeshInfo>()
                .Without<LocalToWorld>()
                .Build();

            changeMaterialMeshFilter = World.Filter
                .With<LocalToWorld>()
                .With<RenderBounds>()
                .With<MaterialMeshInfo>()
                .With<MaterialMeshManaged>()
                .Build();

            materialMeshInfoStash = World.GetStash<MaterialMeshInfo>();
            materialMeshManagedStash = World.GetStash<MaterialMeshManaged>();
            renderBoundsStash = World.GetStash<RenderBounds>();
            worldRenderBoundsStash = World.GetStash<WorldRenderBounds>();
        }

        public void OnUpdate(float deltaTime)
        {
            RegisterNewEntitiesMaterialMeshInfo();
            UnregisterDeletedEntitiesMaterialMeshInfo();
            UpdateChangedMaterialMeshInfo();
        }

        private void RegisterNewEntitiesMaterialMeshInfo()
        {
            foreach (var entity in registerMaterialMeshFilter)
            {
                ref var managed = ref materialMeshManagedStash.Get(entity);

                materialMeshInfoStash.Set(entity, new MaterialMeshInfo()
                {
                    meshID = brg.RegisterMesh(managed.mesh),
                    materialID = brg.RegisterMaterial(managed.material),
                    submeshIndex = 0
                });

                renderBoundsStash.Set(entity, new RenderBounds()
                {
                    value = managed.mesh.bounds.ToAABB()
                });

                worldRenderBoundsStash.Add(entity);
                materialMeshManagedStash.Remove(entity);
            }
        }

        private void UnregisterDeletedEntitiesMaterialMeshInfo()
        {
            foreach (var entity in unregisterMaterialMeshFilter)
            {
                ref var materialMeshInfo = ref materialMeshInfoStash.Get(entity);

                brg.UnregisterMesh(materialMeshInfo.meshID);
                brg.UnregisterMaterial(materialMeshInfo.materialID);

                materialMeshInfoStash.Remove(entity);
            }
        }

        private void UpdateChangedMaterialMeshInfo()
        {
            foreach (var entity in changeMaterialMeshFilter)
            {
                ref var managed = ref materialMeshManagedStash.Get(entity);
                ref var materialMeshInfo = ref materialMeshInfoStash.Get(entity);
                ref var renderBounds = ref renderBoundsStash.Get(entity);

                brg.UnregisterMesh(materialMeshInfo.meshID);
                brg.UnregisterMaterial(materialMeshInfo.materialID);

                materialMeshInfo.meshID = brg.RegisterMesh(managed.mesh);
                materialMeshInfo.materialID = brg.RegisterMaterial(managed.material);

                renderBounds.value = managed.mesh.bounds.ToAABB();
                materialMeshManagedStash.Remove(entity);
            }
        }

        public void Dispose() { }
    }
}

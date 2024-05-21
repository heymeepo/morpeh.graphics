using Scellecs.Morpeh.Transforms;
using UnityEngine.Rendering;

namespace Scellecs.Morpeh.Graphics
{
    public sealed class MaterialMeshSystem : ICleanupSystem
    {
        public World World { get; set; }

        private Filter brgFilter;
        private Filter registerMaterialMeshFilter;
        private Filter unregisterMaterialMeshFilter;
        private Filter changeMaterialMeshFilter;

        private Stash<SharedBRG> brgStash;
        private Stash<MaterialMeshInfo> materialMeshInfoStash;
        private Stash<MaterialMeshManaged> materialMeshManagedStash;

        public void OnAwake()
        {
            brgFilter = World.Filter
                .With<SharedBRG>()
                .Build();

            registerMaterialMeshFilter = World.Filter
                .With<LocalToWorld>()
                .With<MaterialMeshManaged>()
                .Without<MaterialMeshInfo>()
                .Build();

            unregisterMaterialMeshFilter = World.Filter
                .With<MaterialMeshInfo>()
                .Without<LocalToWorld>()
                .Build();

            changeMaterialMeshFilter = World.Filter
                .With<LocalToWorld>()
                .With<MaterialMeshInfo>()
                .With<MaterialMeshManaged>()
                .Build();

            brgStash = World.GetStash<SharedBRG>();
            materialMeshInfoStash = World.GetStash<MaterialMeshInfo>();
            materialMeshManagedStash = World.GetStash<MaterialMeshManaged>();
        }

        public void OnUpdate(float deltaTime)
        {
            var brg = brgStash.Get(brgFilter.First()).brg;

            RegisterNewEntitiesMaterialMeshInfo(brg);
            UnregisterDeletedEntitiesMaterialMeshInfo(brg);
            UpdateChangedMaterialMeshInfo(brg);
        }

        private void RegisterNewEntitiesMaterialMeshInfo(BatchRendererGroup brg)
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

                materialMeshManagedStash.Remove(entity);
            }
        }

        private void UnregisterDeletedEntitiesMaterialMeshInfo(BatchRendererGroup brg)
        {
            foreach (var entity in unregisterMaterialMeshFilter)
            {
                ref var materialMeshInfo = ref materialMeshInfoStash.Get(entity);

                brg.UnregisterMesh(materialMeshInfo.meshID);
                brg.UnregisterMaterial(materialMeshInfo.materialID);

                materialMeshInfoStash.Remove(entity);
            }
        }

        private void UpdateChangedMaterialMeshInfo(BatchRendererGroup brg)
        {
            foreach (var entity in changeMaterialMeshFilter)
            {
                ref var managed = ref materialMeshManagedStash.Get(entity);
                ref var materialMeshInfo = ref materialMeshInfoStash.Get(entity);

                brg.UnregisterMesh(materialMeshInfo.meshID);
                brg.UnregisterMaterial(materialMeshInfo.materialID);

                materialMeshInfo.meshID = brg.RegisterMesh(managed.mesh);
                materialMeshInfo.materialID = brg.RegisterMaterial(managed.material);

                materialMeshManagedStash.Remove(entity);
            }
        }

        public void Dispose() { }
    }
}

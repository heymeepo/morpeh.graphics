using Scellecs.Morpeh.Collections;
using Scellecs.Morpeh.Transforms;
using Scellecs.Morpeh.Workaround;

namespace Scellecs.Morpeh.Graphics
{
    //internal sealed class UpdateRenderArchetypesSubSystem
    //{
    //    private readonly World world;

    //    private FastList<long> usedGraphicsEntitiesArchetypes;
    //    private FastList<long> componentsBuffer;
    //    private FastList<long> newRenderArchetypeComponentsBuffer;

    //    private Filter allExistingGraphicsEntitiesFilter;
    //    private LongHashMap<ArchetypeId> allExistingGraphicsEntitiesArchetypes;

    //    public UpdateRenderArchetypesSubSystem(World world) => this.world = world;

    //    public void Initialize()
    //    {
    //        usedGraphicsEntitiesArchetypes = new FastList<long>(256);
    //        componentsBuffer = new FastList<long>(64);
    //        newRenderArchetypeComponentsBuffer = new FastList<long>(64);

    //        allExistingGraphicsEntitiesFilter = world.Filter
    //            .With<LocalToWorld>()
    //            .With<MaterialMeshInfo>()
    //            .Build();
    //    }

    //    public void UpdateRenderArchetypesFilters(
    //        FastList<RenderArchetype> renderArchetypes, 
    //        FastList<Filter> renderArchetypesFilters,
    //        FastList<int> usedRenderArchetypesIds)
    //    {
    //        for (int i = 0; i < renderArchetypes.length; i++)
    //        {
    //            var filter = renderArchetypesFilters.data[i];

    //            if (filter.IsNotEmpty())
    //            {
    //                foreach (var archetype in filter.ArchetypeIds())
    //                {
    //                    usedGraphicsEntitiesArchetypes.Add(archetype.ID);
    //                }

    //                renderArchetypes.data[i].entities = filter.AsNativeFilterArray();
    //                usedRenderArchetypesIds.Add(i);
    //            }
    //        }
    //    }

    //    public void GatherAndCreateNewRenderArchetypes(LongHashMap<ArchetypeProperty> propertiesCache)
    //    {
    //        //var archetypes = allExistingGraphicsEntitiesFilter.ReadOnlyArchetypes();

    //        //if (archetypes.Count == usedGraphicsEntitiesArchetypes.length)
    //        //{
    //        //    return;
    //        //}

    //        //foreach (var archetype in archetypes)
    //        //{
    //        //    allExistingGraphicsEntitiesArchetypes.Add(archetype.ID, archetype, out _);
    //        //}

    //        //foreach (var archetypeKey in usedGraphicsEntitiesArchetypes)
    //        //{
    //        //    allExistingGraphicsEntitiesArchetypes.Remove(archetypeKey, out _);
    //        //}

    //        //foreach (var idx in allExistingGraphicsEntitiesArchetypes)
    //        //{
    //        //    var archetype = allExistingGraphicsEntitiesArchetypes.GetValueByIndex(idx);
    //        //    archetype.ExtractComponents(componentsBuffer);

    //        //    foreach (var componentId in componentsBuffer)
    //        //    {
    //        //        if (propertiesCache.Has(componentId))
    //        //        {
    //        //            newRenderArchetypeComponentsBuffer.Add(componentId);
    //        //        }
    //        //    }

    //        //    componentsBuffer.Clear();
    //        //}
    //    }

    //    private void CreateRenderArchetype(FastList<long> includedProperties)
    //    {
    //        var filterBuilder = world.Filter.With<LocalToWorld>().With<MaterialMeshInfo>();

    //        for (int i = 0; i < includedProperties.length; i++)
    //        {
    //            filterBuilder.With(includedProperties.data[i]);
    //        }
    //    }
    //}
}

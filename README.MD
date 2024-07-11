# Morpeh Graphics

Provides a generic solution for high-performance entity rendering for [Morpeh ECS](https://github.com/scellecs/morpeh).

Based on the [BatchRendererGroup](https://docs.unity3d.com/Manual/batch-renderer-group.html) and [Entities Graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/index.html).

> [!WARNING]
> This package is in the early stage of development for the Morpeh-2024, which has not yet been released. There will likely be many significant changes.

## Installation

Requirements:

- Unity 2022.3+
- SRP 10.0+
- Compute shaders support on the target device
- [DOTS Instancing shaders](https://docs.unity3d.com/Manual/dots-instancing-shaders.html)

Dependencies:

- [morpeh](https://github.com/scellecs/morpeh/tree/stage-2024.1) #stage-2024.1
- [morpeh.transforms](https://github.com/heymeepo/morpeh.transforms/tree/stage-2024) #stage-2024
- com.unity.burst: 1.8.12
- com.unity.collections: 2.1.4
- com.unity.mathematics: 1.2.6

Install via git URL

```bash
https://github.com/heymeepo/morpeh.graphics.git
```

## Usage

The following components are used in the package:

- MaterialMeshManaged
- MaterialMeshInfo *(Internal)*
- RenderBounds
- WorldRenderBounds
- RenderFilterSettings
- RenderFilterSettingsIndex *(Internal)*
- DisableRendering

Optional components:

- LightMaps 
- CustomProbeTag 
- BlendProbeTag 
- OverrideLightProbeAnchor

To start, add the systems in the following order:

```
-- Entity Converter package
WarmupComponentsTypesInitializer -Optional

-- Transforms package
ParentSystem
LocalToWorldSystem

-- Graphics package
BatchRendererInitializer
MaterialMeshSystem
BatchFilterSettingsSystem
ManageSHPropertiesSystem -Optional
GraphicsArchetypesSystem
LightProbeUpdateSystem -Optional
BatchesUpdateSystem
RenderBoundsSystem
BatchRendererSystem
```

> [!IMPORTANT]  
> The set of graphics systems should be added as the very last systems in your list. Any structural changes during and after the execution of these systems are strictly prohibited. Given that the ``OnPerformCulling`` callback reads data from the ECS state, and its invocation always occurs only after all ECS systems have been executed. If you have any entity cleanup or removal systems, they must be invoked either before the execution of graphics systems or at the beginning of the next frame. Also, ensure that in your plugin systems there are no structural changes. That's why all graphics systems are ``ICleanupSystem``.

### Simple start

To render your first entity, you just need to add this 3 components to it:

- LocalTransform
- LocalToWorld
- MaterialMeshManaged 

``MaterialMeshSystem`` will register the mesh and material and add the ``MaterialMeshInfo`` component. ``BatchFilterSettingsSystem`` will add standard render filter settings. ``RenderBoundsSystem`` will add the ``RenderBounds`` and ``WorldRenderBounds`` components. But you can use your own filter settings by adding the ``RenderFilterSettings`` component.

> [!IMPORTANT]
> The ``MaterialMeshManaged`` and ``RenderFilterSettings`` components are removed from entities after the execution of ``MaterialMeshSystem`` and ``BatchFilterSettingsSystem``, and are replaced with the internal ``MaterialMeshInfo`` and ``RenderFilterSettingsIndex`` components.
> It's also worth noting that ``MaterialMeshInfo`` is ``ICleanupComponent`` (you can find more details in the documentation for the transforms package). This means that if you delete an entity that was involved in rendering, it will remain alive until MaterialMeshSystem is executed.

You can always change in runtime the the mesh and material, as well as the render filter settings, by adding the ``MaterialMeshManaged`` and ``RenderFilterSettings`` components again with the new data.

If you want to disable rendering for an entity, add the ``DisableRendering`` component.

### Material overrides components

Morpeh Graphics supports per-entity overrides material properties. Let's explain using the example of the BaseColorMaterialProperty component, provided this package by default.

```csharp
[BatchMaterialProperty("_BaseColor", BatchMaterialPropertyFormat.Float4)]
public struct BaseColorMaterialProperty : IComponent
{
    public float4 value;
}
```
Declare a new component for the shader property that you want to override. Add the ``BatchMaterialPropertyAttribute`` to it and specify the exact matching name of the property in the shader. Also, specify the format for this property: ``Float`` for float and boolean values, or ``Float4`` for vectors and colors.

Now, add this component to the entity you want to render. By changing the value in this component, you will affect the shader property value for that entity.

This is similar to how you interact with a ``MaterialPropertyBlock`` using standard instancing, but in this case, the component defines the specific property you want.

Be careful, you are fully responsible for ensuring that your overridden property is compatible with the shader you're using for the specific entity.

Also, you can dynamically add and remove such components, as graphics systems are completely stateless, and this happens seamlessly. Sometimes, this can be useful if you need to change the material on an existing entity and add/remove shader overrides. However, remember that you must ensure compatibility yourself.

### Using Entity Converter

Provided ``GraphicsAuthoring`` MonoBehaviour. Add it to your convertable Prefab or GameObject, and set material property overrides in the list as needed. Authoring validates the presence of override components in the project, if the components for the properties you've set don't exist, they will be ignored. It also validates shader compatibility with the set properties.

### Lightmaps & Lightprobes

Available only if you're using the entity converter and only for scene conversion.
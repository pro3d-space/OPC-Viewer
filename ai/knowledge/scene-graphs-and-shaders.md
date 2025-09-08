# Scene Graphs and Shaders in Aardvark/PRo3D

## Scene Graph Basics

### What is a Scene Graph?
A scene graph is a hierarchical data structure that represents 3D scenes. In Aardvark, scene graphs are built using the `ISg` interface and composed using combinators in the `Sg` module.

### Key Scene Graph Operations
- `Sg.ofList [sg1; sg2; ...]` - Creates a scene graph from a list of child graphs
- `sg1 |> Sg.andAlso sg2` - Combines two scene graphs
- `sg |> Sg.shader { ... }` - Applies shaders to a scene graph
- `sg |> Sg.uniform "name" value` - Adds uniform values for shaders
- `sg |> Sg.trafo matrix` - Applies transformations
- `sg |> Sg.viewTrafo` / `Sg.projTrafo` - Camera transformations
- `sg |> Sg.fillMode mode` - Sets rendering mode (fill, wireframe, etc.)

### Scene Graph Evaluation Order
Operations are applied in pipeline order (top to bottom). The order matters:
```fsharp
sg 
|> Sg.shader { ... }      // Applied first
|> Sg.uniform "foo" bar   // Applied second
|> Sg.viewTrafo view      // Applied third
```

## Shader System

### FShade
PRo3D uses FShade, which allows writing GPU shaders in F#. Shaders are composed using computation expressions:

```fsharp
Sg.shader {
    do! Shader.stableTrafo           // Vertex transformation
    do! DefaultSurfaces.constantColor C4f.White 
    do! DefaultSurfaces.diffuseTexture 
    do! Shader.LoDColor              // Custom shader
}
```

### Shader Uniforms
Uniforms are values passed from CPU to GPU that remain constant during rendering of a batch:
- Set using: `sg |> Sg.uniform "UniformName" value`
- Accessed in shaders via: `uniform?UniformName`
- Must be provided before shader execution or will cause runtime errors

### Common Shader Issues

#### Missing Uniform Error
**Problem**: "Could not find uniform 'X'"
**Cause**: Shader expects uniform that scene doesn't provide
**Solution**: Either provide the uniform or use different shader

#### Shader Composition for Mixed Scene Types
When combining different scene types (e.g., OPC + OBJ):
1. Apply appropriate shaders to each scene type separately
2. Then combine the pre-shaded scenes
3. Apply common transforms to the combined result

**Incorrect approach:**
```fsharp
let scene = 
    opcScene 
    |> Sg.andAlso objScene
    |> Sg.shader { do! ShaderRequiringOpcUniforms }  // Crashes on objScene
```

**Correct approach:**
```fsharp
let opcWithShaders = opcScene |> Sg.shader { do! OpcShaders }
let objWithShaders = objScene |> Sg.shader { do! ObjShaders }
let scene = opcWithShaders |> Sg.andAlso objWithShaders
```

## PRo3D-Specific Patterns

### OPC Scene Requirements
- Requires `LoDColor` uniform for LOD visualization
- Uses `LodVisEnabled` uniform to toggle LOD coloring
- Has patch-based rendering with level-of-detail support

### OBJ Scene Requirements  
- Standard mesh rendering without LOD
- May include texture coordinates and materials
- No special uniforms required

### Combining Different Scene Types
1. **Identify requirements**: What uniforms/shaders each type needs
2. **Apply type-specific shaders**: Before combination
3. **Combine scenes**: Using `Sg.andAlso` or `Sg.ofList`
4. **Apply common operations**: View/projection transforms, fill modes

## Debugging Tips

### Shader Errors
- Check console output for missing uniform names
- Verify all required uniforms are provided before shader application
- Use separate shader pipelines for different scene types

### Scene Graph Visualization
- Use wireframe mode (F key) to debug geometry
- Check bounding boxes to ensure proper scene positioning
- Verify transformation order (model → view → projection)

### Performance Considerations
- Minimize shader switches by grouping similar objects
- Apply expensive operations once at appropriate level
- Use adaptive values (AVal) for dynamic updates

## Real-World Example: OPC + OBJ Rendering

### Problem
When combining OPC and OBJ scenes, applying OPC-specific shaders to both scene types causes crashes:
```
ERROR: [GL] Could not find uniform 'LoDColor'
```

### Solution
Apply appropriate shaders to each scene type before combining:

```fsharp
// OPC scene with LOD support
let opcSceneWithShaders = 
    Sg.ofList hierarchies
    |> Sg.shader {
            do! Shader.stableTrafo
            do! DefaultSurfaces.constantColor C4f.White 
            do! DefaultSurfaces.diffuseTexture 
            do! Shader.LoDColor              // OPC-specific
            do! Shader.encodePickIds
        }
    |> Sg.uniform "LodVisEnabled" lodVisEnabled

// OBJ scene without LOD
let objSceneWithShaders = 
    Sg.ofList objSceneGraphs
    |> Sg.shader {
            do! Shader.stableTrafo
            do! DefaultSurfaces.constantColor C4f.White 
            do! DefaultSurfaces.diffuseTexture 
            do! Shader.encodePickIds
        }

// Combine and apply common transforms
let scene = 
    opcSceneWithShaders
    |> Sg.andAlso objSceneWithShaders
    |> Sg.andAlso cursor
    |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
    |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
    |> Sg.fillMode fillMode
```

### Key Lessons
1. **Separate shader pipelines** for different scene types
2. **Apply shaders before combination**, not after
3. **Common transforms** can be applied to the combined result
4. **Type-specific uniforms** only go with their respective scene types

This pattern ensures each scene type gets appropriate rendering while sharing common camera and display transformations.
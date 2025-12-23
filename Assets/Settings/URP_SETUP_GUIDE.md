# URP Setup Guide for 3D Maze Rendering

This guide documents the Universal Render Pipeline (URP) configuration for the 3D maze rendering system, including PBR materials, post-processing, and shader migration.

## Overview

The project uses Unity's Universal Render Pipeline (URP) to support both 2D and 3D rendering modes:

- **2D Renderer** - Original sprite-based 2D rendering (`Renderer2D.asset`)
- **3D Forward Renderer** - New 3D mesh-based rendering (`ForwardRenderer3D.asset`)

Both renderers are configured in the main URP asset and can be switched based on the scene requirements.

## Project Structure

```
Assets/
├── Settings/
│   ├── UniversalRP.asset              # Main URP asset
│   ├── Renderer2D.asset               # 2D renderer for sprites
│   ├── ForwardRenderer3D.asset        # 3D forward renderer (NEW)
│   └── PostProcessingProfile.asset    # Post-processing effects (NEW)
├── Scripts/
│   └── Systems/
│       ├── PBRMaterialFactory.cs      # PBR material creation (NEW)
│       └── MazeRenderer.cs            # Updated to use PBR materials
```

## URP Asset Configuration

### Main Settings (UniversalRP.asset)

Key settings configured for 3D rendering:

```yaml
Rendering:
  - HDR: Enabled
  - MSAA: 1x (can be increased for better quality)
  - Render Scale: 1.0
  - SRP Batcher: Enabled (performance optimization)

Lighting:
  - Main Light: Per-Pixel
  - Main Light Shadows: Enabled (2048 resolution)
  - Additional Lights: Per-Pixel
  - Additional Lights Per Object: 4
  - Shadow Distance: 50 units

Quality:
  - Color Grading LUT Size: 32
  - Shadow Type: Hard shadows
```

### Forward Renderer 3D (ForwardRenderer3D.asset)

The 3D forward renderer includes:

- **Rendering Mode**: Forward rendering
- **Depth Priming**: Enabled (optimization for complex scenes)
- **Opaque/Transparent Layer Masks**: All layers enabled
- **Native Render Pass**: Disabled (for compatibility)

## Post-Processing Configuration

### Post-Processing Profile (PostProcessingProfile.asset)

Three effects are enabled for enhanced 3D visuals:

#### 1. Bloom
Adds glow to emissive objects (like the Heart of the Maze):

```yaml
Threshold: 0.9      # Only bright objects bloom
Intensity: 0.3      # Subtle bloom effect
Scatter: 0.7        # Bloom spread
```

**Purpose**: Makes the glowing heart and emissive materials more visible and atmospheric.

#### 2. Color Adjustments
Enhances overall image quality:

```yaml
Post Exposure: 0.1  # Slight brightness boost
Contrast: 5         # Adds depth to lighting
Saturation: 10      # Richer colors
```

**Purpose**: Ensures colors are vibrant and lighting has good contrast.

#### 3. Tonemapping
Maps HDR values to display range:

```yaml
Mode: ACES           # Industry-standard tone mapping
```

**Purpose**: Provides cinematic color grading and handles bright lights properly.

### Applying Post-Processing in Scene

To enable post-processing in a scene:

1. Create a GameObject: `PostProcessing Volume`
2. Add Component: `Volume` (from URP)
3. Set Profile: `PostProcessingProfile`
4. Enable `Is Global`: true
5. Set Priority: 0

## PBR Material System

### PBRMaterialFactory.cs

A factory class that creates URP/Lit PBR materials with appropriate properties for different tile types.

#### Material Types

##### Wall Material (Tree Bramble)
```csharp
PBRMaterialFactory.CreateWallMaterial(color)

Properties:
  - Metallic: 0.0     # Non-metallic organic surface
  - Smoothness: 0.2   # Rough bark-like texture
  - Emission: None
```

**Use**: Maze walls, tree brambles, obstacles

##### Undergrowth Material
```csharp
PBRMaterialFactory.CreateUndergrowthMaterial(color)

Properties:
  - Metallic: 0.0     # Non-metallic vegetation
  - Smoothness: 0.3   # Slightly rough plant surface
  - Emission: None
```

**Use**: Vegetation, bushes, ground cover

##### Water Material
```csharp
PBRMaterialFactory.CreateWaterMaterial(color)

Properties:
  - Metallic: 0.1     # Slight reflectivity
  - Smoothness: 0.9   # Very smooth water surface
  - Emission: None
```

**Use**: Water tiles, ponds, streams

##### Path Material
```csharp
PBRMaterialFactory.CreatePathMaterial(color)

Properties:
  - Metallic: 0.0     # Non-metallic ground
  - Smoothness: 0.4   # Medium roughness
  - Emission: None
```

**Use**: Walkable paths, floors

##### Emissive Material (Heart)
```csharp
PBRMaterialFactory.CreateEmissiveMaterial(baseColor, emissionColor, intensity)

Properties:
  - Metallic: 0.0
  - Smoothness: 0.6
  - Emission: Enabled with color and intensity
```

**Use**: Glowing objects (Heart of the Maze, magical effects)

### Color Palette Preservation

The PBR materials maintain the original color palette from the 2D sprite system:

| Tile Type | Original Color (2D) | PBR Material |
|-----------|-------------------|--------------|
| Wall | Dark Gray | PBR Wall (rough, dark) |
| Undergrowth | Medium Gray | PBR Undergrowth (slightly rough) |
| Water | Blue | PBR Water (smooth, reflective) |
| Path | Light Color | PBR Path (medium rough) |
| Heart | Red/Pink | PBR Emissive (glowing) |

The `CreatePBRMaterialForSymbol()` method in `MazeRenderer.cs` automatically selects the appropriate material based on the tile symbol.

## Shader Migration

### From Sprite Shaders to PBR

| Old Shader | New Shader | Migration Path |
|------------|------------|----------------|
| Sprites/Default | URP/Lit | Use PBRMaterialFactory |
| Unlit/Color | URP/Unlit | Use CreateUnlitMaterial() |
| Standard | URP/Lit | Direct replacement |

### Shader Properties Mapping

**Sprite Shader → URP/Lit:**
- `_Color` → `_BaseColor`
- N/A → `_Metallic` (new property)
- N/A → `_Smoothness` (new property)
- `_EmissionColor` → `_EmissionColor` (same)

## Lighting Setup

### Recommended Lighting for 3D Maze

#### Main Directional Light
```yaml
Type: Directional
Intensity: 0.8-1.0
Color: Warm white (255, 245, 230)
Rotation: 50° pitch, 330° yaw
Shadows: Enabled (soft shadows)
```

**Purpose**: Simulates sunlight filtering through trees

#### Point Lights (Heart, Props)
```yaml
Type: Point
Intensity: 1.5-3.0
Range: 5-15 units
Color: Varies by object
Shadows: Optional (expensive)
```

**Purpose**: Local lighting for interactive objects

#### Ambient Light
```yaml
Source: Gradient
Sky Color: Light blue-gray
Equator Color: Medium gray
Ground Color: Dark gray
Intensity: 0.3-0.5
```

**Purpose**: Fill lighting for shadowed areas

## Performance Considerations

### Optimization Settings

1. **SRP Batcher**: Enabled in URP asset
   - Reduces draw call overhead
   - Automatically batches materials with same shader

2. **Dynamic Batching**: Disabled
   - Not needed with SRP Batcher
   - Would conflict with 3D transforms

3. **MSAA**: 1x by default
   - Can be increased to 2x or 4x for better quality
   - Performance impact increases significantly

4. **Shadow Distance**: 50 units
   - Adjust based on camera distance
   - Larger values = more expensive

### Material Instancing

PBRMaterialFactory creates new material instances to avoid sharing materials between tiles. This allows:
- Different colors per tile
- Independent property modification
- No material leaking between objects

However, this increases memory usage. For production, consider:
- Material pooling for common colors
- Shared materials with vertex colors
- Texture atlasing for variations

## Common Issues and Solutions

### Issue: Materials appear black

**Cause**: No lighting in scene
**Solution**: Add a Directional Light with intensity > 0.5

### Issue: Shadows not appearing

**Cause**: Shadows disabled or shadow distance too low
**Solution**:
1. Enable shadows on Main Light
2. Increase Shadow Distance in URP asset
3. Check camera distance to objects

### Issue: Bloom not working

**Cause**: Emissive materials not bright enough
**Solution**: Increase emission intensity or lower bloom threshold

### Issue: Colors look washed out

**Cause**: Post-processing exposure too high
**Solution**: Reduce Post Exposure in Color Adjustments

### Issue: Performance drop in 3D mode

**Cause**: Too many draw calls or shadow casting lights
**Solution**:
1. Enable SRP Batcher
2. Reduce number of lights
3. Lower shadow resolution
4. Disable shadows on additional lights

## Future Enhancements

Potential improvements to the rendering system:

### Advanced Materials
- Normal mapping for texture detail
- Parallax occlusion for depth
- Subsurface scattering for vegetation
- Water flow animations with shader graph

### Advanced Lighting
- Light probes for dynamic objects
- Reflection probes for water reflections
- Volumetric fog for atmosphere
- God rays through trees

### Advanced Post-Processing
- Ambient Occlusion (SSAO)
- Screen Space Reflections (SSR)
- Depth of Field
- Motion Blur
- Vignette and Film Grain

### Performance
- LOD system for distant tiles
- Occlusion culling for maze walls
- GPU instancing for repeated meshes
- Texture atlasing for material batching

## Testing Checklist

When updating URP settings or materials:

- [ ] Test in 2D mode (should still work with Renderer2D)
- [ ] Test in 3D mode (should use ForwardRenderer3D)
- [ ] Verify all tile types render correctly
- [ ] Check Heart emission and glow
- [ ] Verify post-processing effects are visible
- [ ] Test performance (FPS should be stable)
- [ ] Check lighting in different camera angles
- [ ] Verify shadows render correctly
- [ ] Test on different quality settings
- [ ] Verify color palette matches design

## Reference Links

- [URP Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
- [PBR Material Properties](https://docs.unity3d.com/Manual/StandardShaderMaterialParameters.html)
- [Post-Processing in URP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/integration-with-post-processing.html)
- [Lighting in URP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/manual/lighting.html)

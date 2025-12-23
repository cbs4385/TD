# 3D Rendering Optimization Guide

This guide covers all optimization techniques implemented for the 3D maze rendering system, including mesh batching, LOD systems, and performance profiling.

## Overview

The 3D rendering system includes several optimization layers to maintain high performance even with large mazes:

1. **Mesh Batching** - Combines tiles to reduce draw calls
2. **LOD System** - Shows simplified meshes at distance
3. **SRP Batcher** - Unity's built-in batching for URP
4. **Static Batching** - Marks batched meshes as static
5. **Performance Profiling** - Real-time monitoring and analysis

## Mesh Batching System

### What is Mesh Batching?

Mesh batching combines multiple small meshes into larger meshes, reducing the number of draw calls required to render the scene. This dramatically improves performance for scenes with many identical tiles.

**Without Batching:**
- 1000 tiles = 1000 draw calls
- High CPU overhead
- Poor performance

**With Batching:**
- 1000 tiles → 10 batches = 10 draw calls
- Low CPU overhead
- Excellent performance

### MeshBatcher.cs

A utility class for combining meshes:

#### Key Methods

**CombineMeshes()**
```csharp
// Combines multiple GameObjects into a single mesh
GameObject batched = MeshBatcher.CombineMeshes(
    objects: tileList,
    parent: tilesParent,
    batchName: "BatchedWalls",
    destroyOriginals: true  // Removes individual tiles after batching
);
```

**BatchByMaterial()**
```csharp
// Groups objects by material before batching
List<GameObject> batches = MeshBatcher.BatchByMaterial(
    objects: allTiles,
    parent: tilesParent,
    destroyOriginals: true
);
```

**BatchInChunks()**
```csharp
// Batches in chunks to avoid creating meshes that are too large
List<GameObject> batches = MeshBatcher.BatchInChunks(
    objects: tiles,
    parent: tilesParent,
    chunkSize: 100,  // Max 100 tiles per batch
    destroyOriginals: true
);
```

### MazeRenderer Integration

The MazeRenderer automatically batches tiles after creation:

#### Settings (in Inspector)

| Setting | Default | Description |
|---------|---------|-------------|
| Enable Mesh Batching | true | Turn batching on/off |
| Batch Chunk Size | 100 | Max tiles per batch |
| Enable LOD | false | Enable LOD system |

#### How It Works

1. **Creation Phase**: All tiles are created individually
2. **Collection Phase**: Tiles are grouped by type (walls, undergrowth, water, paths)
3. **Batching Phase**: Each group is batched into chunks
4. **Cleanup Phase**: Original tiles are destroyed

**Example Output:**
```
[MazeRenderer] Batched 450 wall tiles into 5 batches
[MazeRenderer] Batched 200 undergrowth tiles into 2 batches
[MazeRenderer] Batched 100 water tiles into 1 batch
[MazeRenderer] Batched 350 path tiles into 4 batches
[MazeRenderer] Total: Batched 1100 tiles into 12 combined meshes
```

### Performance Impact

| Maze Size | Tiles | Without Batching | With Batching | Improvement |
|-----------|-------|------------------|---------------|-------------|
| Small (30x30) | 900 | 900 draw calls | ~9 draw calls | 99% reduction |
| Medium (50x50) | 2500 | 2500 draw calls | ~25 draw calls | 99% reduction |
| Large (100x100) | 10000 | 10000 draw calls | ~100 draw calls | 99% reduction |

### Limitations

1. **Dynamic Objects**: Batching only works for static (non-moving) objects
2. **Different Materials**: Objects with different materials cannot be batched together
3. **Mesh Complexity**: Very large batches can cause issues (hence chunk size limit)
4. **Vertex Limit**: Meshes limited to 65,535 vertices (32-bit indices used if needed)

## LOD System

### What is LOD?

Level of Detail (LOD) shows different mesh complexity based on distance from camera:
- **LOD0**: Full detail (close to camera)
- **LOD1**: Medium detail (middle distance)
- **LOD2**: Low detail (far from camera)

### TileLODManager.cs

Manages LOD groups for maze tiles.

#### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Enable LOD | true | Turn LOD system on/off |
| LOD0 Screen Height | 0.5 | Full detail threshold |
| LOD1 Screen Height | 0.25 | Medium detail threshold |
| LOD2 Screen Height | 0.1 | Low detail threshold |
| LOD Walls | true | Apply LOD to walls |
| LOD Undergrowth | true | Apply LOD to vegetation |
| LOD Water | false | Apply LOD to water (usually not needed) |

#### Usage

**Automatic Application:**
```csharp
// In MazeRenderer
TileLODManager lodManager = GetComponent<TileLODManager>();
lodManager.ApplyLOD(tileObject, TileType.TreeBramble);
```

**Creating LOD Prefabs:**
```csharp
// Create a prefab with LOD levels
GameObject lodPrefab = TileLODManager.CreateLODPrefab(originalPrefab);
```

### Performance Impact

LODs reduce vertex processing and fill rate for distant objects:

| Scenario | Without LOD | With LOD | Improvement |
|----------|-------------|----------|-------------|
| Close view | 100% detail | 100% detail | Same |
| Medium view | 100% detail | 50% detail | 2x faster |
| Far view | 100% detail | 20% detail | 5x faster |

## Performance Profiling

### RenderingProfiler.cs

Real-time performance monitoring overlay.

#### Features

- FPS (Frames Per Second)
- Batch count
- SetPass calls
- Triangle count
- Vertex count
- Memory usage (total and graphics)
- Automatic warnings for performance issues

#### Usage

**Add to Scene:**
1. Create empty GameObject: "Rendering Profiler"
2. Add Component: `RenderingProfiler`
3. Configure in Inspector

**Inspector Settings:**

| Setting | Default | Description |
|---------|---------|-------------|
| Show Overlay | true | Display on-screen stats |
| Update Interval | 0.5s | How often to update stats |
| Font Size | 14 | Overlay text size |
| Draw Call Warning | 100 | Warn if batches exceed this |
| Triangle Warning | 100000 | Warn if triangles exceed this |

**Reading the Overlay:**

```
Rendering Profiler
FPS: 60.0

Batches: 15                    ← Draw calls (should be low)
SetPass Calls: 18              ← Material switches
Triangles: 45,230              ← Geometry complexity
Vertices: 68,450

Memory: 256 MB                 ← Total memory
GFX Memory: 128 MB             ← Graphics memory
```

**Warnings:**
- Red text indicates high batch count (> threshold)
- Yellow text indicates high triangle count (> threshold)

#### API

**Get Report:**
```csharp
RenderingProfiler profiler = GetComponent<RenderingProfiler>();
string report = profiler.GetStatsReport();
Debug.Log(report);
```

**Toggle Overlay:**
```csharp
profiler.ToggleOverlay();  // Show/hide overlay
```

## Runtime Initialization Validation

### RuntimeInitializationValidator.cs

Validates that all systems initialize correctly from Start().

#### What It Checks

1. **GameController**
   - Instance exists
   - Current essence value
   - Event subscribers (OnEssenceChanged)
   - MazeGrid registration
   - Heart registration

2. **MazeGrid**
   - MazeGridBehaviour exists
   - Grid created with correct dimensions
   - Walkable/blocked tile counts

3. **MazeRenderer**
   - Renderer exists
   - Tiles rendered
   - Batching active (if enabled)

4. **UI Controllers**
   - UIController exists
   - PlayerResourcesUIController exists

5. **Heart of the Maze**
   - Heart exists
   - Grid position
   - 3D components (Light, Collider)

#### Usage

**Add to Scene:**
1. Create empty GameObject: "Initialization Validator"
2. Add Component: `RuntimeInitializationValidator`
3. Run scene - validation happens automatically

**Settings:**

| Setting | Default | Description |
|---------|---------|-------------|
| Enable Validation | true | Run validation on start |
| Validation Delay | 1s | Wait before validating |

**Example Output:**

```
=== Runtime Initialization Validation ===
Validation Time: 1.00s after scene start

--- GameController ---
✓ GameController.Instance exists
✓ Current Essence: 50
✓ OnEssenceChanged has subscribers
✓ MazeGrid registered
✓ Heart registered

--- MazeGrid ---
✓ MazeGridBehaviour exists
✓ MazeGrid created (50x50)
  MazeGrid [50x50]: 1543 walkable, 957 blocked

--- MazeRenderer ---
✓ MazeRenderer exists
✓ Tiles rendered: 15 objects
✓ Mesh batching active: 15 batches

--- UI Controllers ---
✓ UIController exists
✓ PlayerResourcesUIController exists

--- Heart of the Maze ---
✓ HeartOfTheMaze exists
  Grid Position: (25, 25)
✓ 3D Point Light: Point, Intensity: 2
✓ 3D Collider: SphereCollider

=== Validation Complete ===
```

## Best Practices

### When to Use Mesh Batching

**✓ Good for:**
- Static maze tiles (walls, floors)
- Large numbers of identical objects
- Procedurally generated meshes
- Objects with same material

**✗ Not suitable for:**
- Moving objects (visitors, props)
- Objects with different materials
- Objects that need individual manipulation
- Very complex meshes (too many vertices)

### When to Use LOD

**✓ Good for:**
- Complex 3D prefabs (trees, rocks)
- Large open mazes (camera can be far away)
- Vegetation with high poly count
- Performance-critical scenarios

**✗ Not needed for:**
- Simple geomet

ry (flat tiles)
- Small mazes (camera always close)
- Already-optimized prefabs
- Top-down views with fixed camera distance

### Optimization Checklist

Before deploying:

- [ ] Enable mesh batching in MazeRenderer
- [ ] Set appropriate batch chunk size (50-200)
- [ ] Enable SRP Batcher in URP settings
- [ ] Mark batched meshes as static
- [ ] Use LOD for complex prefabs
- [ ] Profile with RenderingProfiler
- [ ] Validate initialization with RuntimeInitializationValidator
- [ ] Test on target hardware
- [ ] Check memory usage
- [ ] Verify FPS meets target (e.g., 60 FPS)

### Performance Targets

| Target Platform | Min FPS | Max Draw Calls | Max Triangles |
|----------------|---------|----------------|---------------|
| Desktop | 60 | 200 | 500,000 |
| Mobile (High) | 30 | 100 | 200,000 |
| Mobile (Low) | 30 | 50 | 100,000 |
| WebGL | 30 | 150 | 300,000 |

### Common Issues and Solutions

#### Issue: High Batch Count

**Symptoms:**
- Batches > 100
- Low FPS despite low triangle count

**Solutions:**
1. Enable mesh batching in MazeRenderer
2. Reduce number of different materials
3. Enable SRP Batcher in URP settings
4. Increase batch chunk size

#### Issue: High Triangle Count

**Symptoms:**
- Triangles > 100,000
- Low FPS on lower-end hardware

**Solutions:**
1. Enable LOD system
2. Simplify prefab meshes
3. Reduce maze size
4. Use simpler procedural meshes (cubes instead of complex models)

#### Issue: High SetPass Calls

**Symptoms:**
- SetPass calls significantly higher than batches
- Material switching overhead

**Solutions:**
1. Reduce number of unique materials
2. Use material atlasing
3. Combine materials where possible
4. Use shared materials instead of material instances

#### Issue: Memory Usage Too High

**Symptoms:**
- Memory > 500 MB
- Out of memory errors

**Solutions:**
1. Reduce batch chunk size
2. Use texture compression
3. Reduce texture resolution
4. Clear batching lists after batching
5. Use object pooling for dynamic objects

## Advanced Optimization Techniques

### GPU Instancing

For large numbers of identical objects:

```csharp
// Enable GPU instancing on material
material.enableInstancing = true;
```

**Benefits:**
- Renders identical objects with one draw call
- Better than batching for dynamic objects
- Works with SRP Batcher

**Requirements:**
- Identical mesh and material
- URP shader with instancing support

### Occlusion Culling

For complex mazes where walls block view:

1. Window → Rendering → Occlusion Culling
2. Bake occlusion data
3. Enable in Camera settings

**Benefits:**
- Doesn't render objects behind walls
- Reduces draw calls and vertex processing

### Texture Atlasing

Combine multiple textures into one:

**Benefits:**
- Reduces material count
- Enables batching across different tile types
- Reduces SetPass calls

**Drawbacks:**
- More complex UV mapping
- Larger initial texture size

### Mesh Simplification

For LOD levels:

1. Use external tools (Blender, Simplygon)
2. Create LOD1 at 50% triangles
3. Create LOD2 at 20% triangles

**Benefits:**
- Automatic performance scaling with distance
- Maintains visual quality where it matters

## Monitoring Performance

### Unity Profiler

Access via Window → Analysis → Profiler:

**Key Metrics to Watch:**
- CPU Usage → Rendering
- Memory → Total Allocated
- Rendering → Batches
- Rendering → SetPass Calls
- Rendering → Triangles

### Frame Debugger

Access via Window → Analysis → Frame Debugger:

**Use for:**
- Inspecting individual draw calls
- Identifying batching failures
- Viewing material switches
- Debugging rendering issues

### Stats Overlay

Enable in Game view → Stats button:

**Shows:**
- FPS
- Batches
- SetPass Calls
- Triangles
- Vertices
- Shadow casters

## Testing Scenarios

### Small Maze (30x30)
- Expected: < 20 batches
- Expected: > 60 FPS
- Expected: < 50,000 triangles

### Medium Maze (50x50)
- Expected: < 40 batches
- Expected: > 60 FPS
- Expected: < 150,000 triangles

### Large Maze (100x100)
- Expected: < 120 batches
- Expected: > 30 FPS
- Expected: < 500,000 triangles

## Summary

The optimization system provides:

1. **Mesh Batching**: 99% reduction in draw calls
2. **LOD System**: 2-5x performance improvement at distance
3. **Profiling Tools**: Real-time performance monitoring
4. **Validation**: Ensures systems initialize correctly

These techniques work together to maintain high performance even with large, complex 3D mazes while preserving all gameplay functionality and event systems.

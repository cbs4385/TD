# Claude Code Project Guidelines - FaeMaze

This file contains critical architectural rules that MUST be followed in every session.
These rules exist because violations have caused repeated bugs that took hours to fix.

---

# 🚨🚨🚨 CRITICAL: NO RESOURCES.LOAD - SEARCH FIRST 🚨🚨🚨

## NEVER USE Resources.Load() - ALWAYS SEARCH FOR EXISTING PATTERNS FIRST

**Before writing ANY asset loading code:**
1. **STOP and SEARCH** the codebase for how similar assets are already loaded
2. **COPY the EXACT pattern** from existing working code
3. **DO NOT invent new approaches or use Resources.Load**

### Existing Patterns to Copy:

**EarthenGroundTexture (COPY FROM MazeRenderer.cs line ~222):**
```csharp
Texture2D texture = null;

#if UNITY_EDITOR
texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/EarthenGroundTexture.png");
#endif
```

**Prefabs (COPY FROM HeartPowerManager):**
```csharp
[SerializeField] private GameObject devourPrefab;
public GameObject DevourPrefab => devourPrefab;
```

### VIOLATIONS - NEVER DO THESE:
```csharp
// WRONG - Resources.Load fails, assets not in Resources folder
Resources.Load<GameObject>("Prefabs/Props/devour");
Resources.Load<Texture2D>("EarthenGroundTexture");

// WRONG - Never copy files to Resources folder
// WRONG - Never create new SerializeField when AssetDatabase pattern exists for that asset
```

### Reference Files:
| Need to load... | Look at this file first |
|-----------------|------------------------|
| EarthenGroundTexture | `MazeRenderer.cs` ExtractHeartGroundMaterial() |
| Prefabs | `HeartPowerManager.cs` serialized fields |
| Shaders | `Shader.Find("Custom/ShaderName")` |

**SEARCH THE CODEBASE BEFORE WRITING ASSET LOADING CODE!**

---

# 🚨🚨🚨 CRITICAL: COORDINATE SYSTEM 🚨🚨🚨

## WORLD UP IS **-Z**, NOT Y!

**This is NOT a standard Unity Y-up coordinate system!**

```
     -Z  (WORLD UP - toward camera)
      ↑
      |
      |
      +----→ +X
     /
    /
   +Y
```

### The Rules:
| Axis | Direction |
|------|-----------|
| **XY plane** | The playing surface (horizontal ground) |
| **-Z** | World UP (toward camera) |
| **+Z** | World DOWN (into the ground) |

### Rotation in the XY plane:
- Use `Mathf.Atan2(direction.y, direction.x)` for angles
- Rotate around the **Z axis**: `Quaternion.Euler(0f, 0f, angle)`
- **NEVER** use Y-axis rotation for orienting objects in the play area
- **NEVER** assume standard Unity Y-up conventions

### For hand/visual orientation:
- Palm facing camera = palm facing -Z
- Fingers pointing in XY plane direction = rotate around Z axis only
- `Quaternion.Euler(0f, 0f, angle)` where `angle = Atan2(dir.y, dir.x) * Rad2Deg`

**READ THIS BEFORE WRITING ANY ROTATION CODE!**

---

# 🚨🚨🚨 CRITICAL: NODE CENTER IS UNWALKABLE 🚨🚨🚨

## NODE CENTERS ARE NOT WALKABLE - VISITORS CANNOT PATH THROUGH THEM!

**This is a fundamental architectural constraint that affects ALL pathfinding and navigation code.**

### Node Geometry:
```
         NODE_RADIUS (3.0 units)
         ←─────────────────────→

              Walkable Ring
           ╭───────────────────╮
          ╱                     ╲
         │    ╭─────────────╮    │
         │   ╱  UNWALKABLE   ╲   │
         │  │   (< 1.0 unit   │  │   ← Props/hazards go here
         │   ╲   from center)╱   │
         │    ╰─────────────╯    │
          ╲                     ╱
           ╰───────────────────╯

         Inner radius: 1.0 unit (unwalkable)
         Outer radius: 3.0 units (NODE_RADIUS)
         Walkable area: ring from 1.0 to 3.0 units
```

### The Rules:
| Distance from Node Center | Walkable? | Purpose |
|---------------------------|-----------|---------|
| 0.0 - 1.0 units | **NO** | Reserved for props (Pond, Lantern, FairyRing) |
| 1.0 - 3.0 units | Yes | Visitor walking area |
| > 3.0 units | Edge paths | Corridors between nodes |

**Exception**: The root/heart node (Kind == "root") has walkable center tiles.

### Implementation (WorldSpaceMaze.cs line ~705):
```csharp
// Mark tiles within 1 unit radius of node center as unwalkable (except root/heart)
float distFromCenter = offset.magnitude;
bool isInCentralCircle = distFromCenter < 1.0f;
if (isInCentralCircle && node.Kind != "root")
{
    tile.Walkable = false;
}
```

### VIOLATIONS - DO NOT DO THIS:
```csharp
// WRONG - Pathfinding through node centers
waypoints.Add(nodeCenter); // NO! Node centers are unwalkable!

// WRONG - Assuming visitors can reach node center
Vector3 destination = node.Position; // NO! Use edge of walkable ring!

// WRONG - Graph-based navigation using node positions directly
path = FindPath(startNode.Position, endNode.Position); // NO! Positions are unwalkable!
```

### Correct Patterns for Navigation:

**Finding a walkable position near a node:**
```csharp
// Get position on walkable ring (1.0 to 3.0 units from center)
Vector2 directionFromCenter = (visitorPos - nodeCenter).normalized;
Vector2 walkablePos = nodeCenter + directionFromCenter * 1.5f; // Middle of walkable ring
```

**Pathfinding must use TILES, not node centers:**
- The A* algorithm uses `WorldSpaceTile` objects with `Walkable` property
- Tiles within 1.0 unit of non-root node centers have `Walkable = false`
- Pathfinding automatically avoids unwalkable tiles

**Edge entry/exit points:**
- Edges connect at NODE_RADIUS (3.0 units) from node center
- Visitors enter/exit nodes at the edge of the walkable area
- Movement through nodes follows the walkable ring, not straight lines through center

### Why This Matters for Pathfinding:
The current tile-based A* works correctly because it respects `Walkable` flags. Any "optimization" that tries to use node centers as waypoints will break navigation. Visitors would path into unwalkable areas and get stuck.

**ALWAYS use tile-based pathfinding or ensure any graph-based approach respects the unwalkable center region!**

---

## Path Simplification (CRITICAL for Movement)

**Paths MUST be simplified before use - without this, visitors trigger waypoint events too frequently.**

### The Problem:
Tiles are spaced ~0.5 units apart along edges. Without simplification, a path across one edge might have 10-20+ waypoints. This causes:
- `HandleDetourAtWaypoint()` triggers at every tile boundary
- Excessive waypoint processing overhead
- Jittery direction updates

### Movement System:
**Spline smoothing is DISABLED** (`useSplineSmoothing = false` in VisitorControllerBase).
Visitors now use direct tile-to-tile movement via `UpdateDirectWalking()`.
Spline smoothing was disabled because it caused visitors to drift off walkable tiles and get stuck.

### The Solution:
`SimplifyTilePath()` removes intermediate collinear points, keeping only:
1. Points where direction changes by >20 degrees (sharp turns)
2. Points where cumulative small turns exceed 30 degrees (gradual curves)
3. At least one point every 10 tiles (~5 world units) for waypoint spacing

### Implementation (called in BuildWorldPath):
```csharp
// A* through walkable tiles
var tilePath = FindTilePath(mazeData, startTile, endTile);

// CRITICAL: Simplify path for smooth movement
tilePath = SimplifyTilePath(tilePath);

// Convert to world positions...
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - Using raw tile path without simplification
foreach (var tile in tilePath)  // NO! Simplify first!
{
    result.Add(new Vector3(tile.Position.x, tile.Position.y, start.z));
}
```

### Shared Pathfinding Utility:
All movement pathfinding should use `ForestMaze.MazePathfinding` (in `Assets/Scripts/Maze/MazePathfinding.cs`):

```csharp
// Standard usage - includes path simplification automatically
var path = MazePathfinding.BuildWorldPath(
    mazeData,
    startPosition,
    endPosition,
    heartNodePenalty: 20f,      // Penalty for crossing heart node
    penalizeHeartNode: true     // Set false if destination IS the heart
);
```

### Files that build paths:
| File | Method | Uses Shared Utility? | Notes |
|------|--------|---------------------|-------|
| `VisitorControllerBase.cs` | `BuildWorldPath()` | Yes | Primary visitor pathfinding |
| `RedCapController.cs` | `BuildWorldPath()` | Yes | RedCap enemy pathfinding |
| `HeartPowerEffects.cs` | `GeneratePathToHeart()` | No | Fog coverage (needs dense points) |

**Any new pathfinding code for MOVEMENT must use `MazePathfinding.BuildWorldPath()` which handles simplification automatically!**
(Visual effect paths may need dense points and should not use the shared utility.)

---

## ⛔ LOCKED FILES - DO NOT MODIFY ⛔

The following files have been stabilized after extensive debugging. **DO NOT MODIFY** without explicit user approval:

| File | Purpose |
|------|---------|
| `WallCollisionChecker.cs` | Wall destruction validation - throws on invalid removal |
| `GraphElementWallContainer.cs` | Wall ownership and collision trigger system |
| `TagManager.asset` | Unity tags for MazePath/MazeNode |
| `MazeRenderer.cs` (collider sections) | Path tile 2/3 colliders, node SphereCollider |
| `PowerFog.shader` | Heart Power 1 fog visual effect shader |
| `HeartPowerEffects.cs` (MurmuringPathsEffect) | Power fog coverage and affected area logic |

**Before touching these files, you MUST:**
1. Read this entire CLAUDE.md file
2. Explain to the user WHY you need to modify the file
3. Get explicit approval ("yes, modify it")
4. Make the minimum necessary change

These files took hours of debugging to get right. "Improvements" or "refactoring" will likely break wall placement.

---

## CRITICAL RULE 1: Wall Rotation

**Walls MUST be rotated so their front face (+X axis) is parallel to the tangent of the graph element they are placed from.**

### What this means:
- **Node walls**: Front face tangent to the node's circular edge (perpendicular points toward node center)
- **Edge walls**: Front face parallel to the edge direction
- **End cap walls**: Front face oriented to wrap the frontier terminus

### Correct pattern:
```csharp
// For edge walls - perpendicular is already calculated
float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
if (side < 0) orientationDegrees += 180f;
CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: layer);
```

```csharp
// For node walls - wallAngle is the radial angle from node center
float orientationDegrees = (wallAngle * Mathf.Rad2Deg) + 180f;
CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - This removes rotation and aligns walls to world axis
CreateWorldSpaceTile(worldPos, 0f, '#', mazeOrigin, isWall: true, wallLayer: layer);
```

**Never pass `0f` for orientationDegrees unless the wall genuinely should have no rotation (which is rare).**

---

## CRITICAL RULE 2: Walls May Overlap Other Walls

**Wall tiles are ALLOWED to overlap other wall tiles. This is intentional - it creates a dense forest barrier.**

### The architecture:
1. Place ALL walls unconditionally
2. WallCollisionChecker detects collisions with PATHS and NODES only
3. Walls overlapping paths/nodes are destroyed via Unity physics
4. Walls overlapping OTHER WALLS are LEFT ALONE

### WallCollisionChecker must ONLY check for:
- Tag "MazePath" - path tiles
- Tag "MazeNode" - node cylinders
- Name patterns: NodeColumn, NodeCylinder, PathTile, WorldTile_*

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - checking for wall-to-wall collisions
if (collider.CompareTag("Wall") || collider.name.Contains("Wall"))
{
    Destroy(gameObject); // NO! Walls may overlap walls!
}
```

```csharp
// WRONG - any distance check that removes walls for being near other walls
if (IsPositionTooCloseToOtherWall(wallPos))
{
    continue; // NO! Place the wall anyway!
}
```

---

## Wall Removal Rules

Walls may ONLY be removed by:
1. **Physics collision** with a path or node (via WallCollisionChecker)
2. **Complete maze regeneration** (destroying all tiles)

Walls may NOT be removed by:
- Manual distance checks
- Intersection tests with other walls
- Any proximity calculation

The following methods are deprecated and throw exceptions:
- `CheckWallPathIntersection`
- `IsWallPositionValid`
- `IsPositionTooCloseToEdge`

---

## Key Constants Reference

| Constant | Value | Purpose |
|----------|-------|---------|
| NODE_RADIUS | 3.0 | Node clearing size |
| PATH_WIDTH | 1.25 | Edge corridor width |
| PATH_HALF_WIDTH | 0 | Walls start at path edge, NOT offset |
| WALL_STEP_SIZE | 0.8 | Spacing between walls along edges |
| WALL_SPACING | 0.8 | Depth between wall layers |
| WALL_DEPTH | 3 | Number of wall layers |
| WALL_CHECK_RADIUS | 0.3 | Physics collision detection radius |

---

## CRITICAL RULE 4: Path Tile Collider Size

**Path tile colliders (edges) MUST be reduced to 2/3 (0.667) of visual size.**
**Node colliders stay FULL SIZE.**

### Why this matters:
- Walls are placed tight against path edges (PATH_HALF_WIDTH = 0)
- On curves, the outer edge of the path bends away from wall positions
- Full-size path tile colliders cause false positives: walls that DON'T visually intersect get destroyed
- Reducing path tile colliders to 2/3 size prevents this while still detecting actual intersections
- Node colliders stay full size because node walls radiate outward uniformly (no curve issues)

### Implementation:
```csharp
// For path tiles ONLY (BoxCollider on cubes) - reduce to 2/3
boxCol.size = new Vector3(0.667f, 0.667f, 1f);

// For node cylinders - use SphereCollider at FULL SIZE
// Replace CapsuleCollider (affected by rotation) with SphereCollider
Object.Destroy(capCol);
SphereCollider sphereCol = cylinder.AddComponent<SphereCollider>();
sphereCol.radius = 0.5f;  // Local radius, scaled by transform to match visual
sphereCol.isTrigger = true;
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - increasing PATH_HALF_WIDTH to "fix" wall gaps
private const float PATH_HALF_WIDTH = 0.5f; // NO! This creates ugly gaps between walls and path!

// WRONG - reducing node collider size
sphereCol.radius = 0.333f; // NO! Nodes need full-size colliders!

// WRONG - using CapsuleCollider (rotation affects its behavior)
capCol.isTrigger = true; // NO! CapsuleCollider doesn't work correctly after 90° rotation
```

### The correct approach:
1. Keep PATH_HALF_WIDTH = 0 so walls are tight against the path
2. Reduce path tile colliders to 2/3 size so curve geometry doesn't cause false collisions
3. Use SphereCollider for nodes at full size (radius = 0.5 in local space)

---

## Before Modifying Wall Code

1. Read this entire file
2. Check existing working patterns in MazeRenderer.cs (search for "orientationDegrees")
3. Ensure rotation is calculated, not hardcoded to 0
4. Ensure no wall-to-wall collision removal is added
5. Test visually that walls face the correct direction

---

## CRITICAL RULE 3: No Dead Code

**Never leave dead code behind. Delete it. If in doubt, delete it.**

### Principles:
- Unused methods, variables, and parameters must be deleted, not commented out
- Code that is "no longer needed" should be removed entirely
- Do not keep code "just in case" - git history preserves everything
- Missing elements can always be recovered from version control

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - commented out code
// private void OldMethod() { ... }

// WRONG - unused variable kept "for reference"
// private float oldValue = 1.0f;

// WRONG - dead code path
if (false) { DoSomething(); }
```

### Correct approach:
- Delete the code completely
- If you need it later, recover from git history
- A clean codebase is easier to understand and maintain

---

## CRITICAL RULE 5: Heart Power 1 (MurmuringPaths) Fog Coverage

**The power fog MUST cover exactly the affected graph elements - no more, no less.**

### Affected area definition:
- ALL nodes along the path from activation point to heart
- ALL edges along the path from activation point to heart
- For the **triggering edge**: only tiles CLOSER to heart than the focal point
- Tiles beyond the focal point (away from heart) are NOT affected

### Implementation in PopulateAllAffectedTilePositions:
```csharp
// For tiles on the TRIGGERING edge, only include those closer to heart than the focal point
if (affectedEdgeIndex >= 0 && tile.EdgeIndex == affectedEdgeIndex)
{
    float tileDistFromHeart = Vector2.Distance(heartPos2D, tile.Position);
    if (tileDistFromHeart > targetDistFromHeart + 0.5f) // Small tolerance
    {
        // This tile is beyond the focal point (away from heart), skip it
        continue;
    }
}
```

### Key constants:
| Constant | Value | Purpose |
|----------|-------|---------|
| FOG_Z_POSITION | -0.2 | Z position of fog quad (above path, below UI) |
| FOG_PADDING | 3.0 | Padding around fog bounds |
| MASK_PIXELS_PER_UNIT | 4.0 | Resolution of path mask texture |

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - using sampled positions instead of all tiles
var positions = mazeData.GetHeartPower1Positions(...); // NO! This was for old light system

// WRONG - including entire triggering edge
if (isOnAffectedEdge) { allAffectedTilePositions.Add(...); } // NO! Must filter triggering edge

// WRONG - sparse sampling for fog coverage
int samplesPerNode = Mathf.Min(6, tiles.Count); // NO! Fog needs ALL tiles, not samples
```

### The correct approach:
1. Iterate through ALL tiles in `mazeData.Tiles`
2. Include all tiles on affected nodes (full coverage)
3. Include all tiles on affected edges EXCEPT triggering edge tiles beyond focal point
4. Filter triggering edge by distance from heart vs focal point distance

---

## CRITICAL RULE 6: DevouringMaw (Heart Power 3) Animation

**The devour animation uses frame-based control with a 62-frame animation at 60fps.**

### Animation sequence:
1. **Emerging phase** (1.04s): Model translates z=0 → z=-0.5 while animation plays frames 1→62 (bite cycle)
2. **Paused phase** (1.0s): Model holds at z=-0.5, animation holds at frame 62 (closed mouth)
3. **Sinking phase** (0.5s): Model translates z=-0.5 → z=+1.0, animation holds at frame 62, visitors follow

### Key files and assets:
| Asset | Path | Purpose |
|-------|------|---------|
| Prefab | `Assets/Prefabs/Props/devour.prefab` | Devour visual model |
| GLB model | `Assets/Animations/Devour/devour.glb` | Source model with FaceRig |
| Controller | `Assets/Animations/Devour/devour.controller` | Animator controller with FaceRigAction state |
| Animation clip | `FaceRigAction` (inside devour.glb) | 62 frames at 60fps, 1.042s duration |

### Asset loading pattern:
```csharp
// In HeartPowerManager.Awake() - LoadPrefabsIfNeeded()
#if UNITY_EDITOR
if (devourPrefab == null)
{
    devourPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/devour.prefab");
}
#endif

// In DevouringMawEffect.InstantiateDevourVisual() - load controller if missing
#if UNITY_EDITOR
controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Devour/devour.controller");
if (controller != null)
{
    devourAnimator.runtimeAnimatorController = controller;
}
#endif
```

### Animation constants:
| Constant | Value | Description |
|----------|-------|-------------|
| DEVOUR_ANIMATION_NAME | "FaceRigAction" | Animation state name in controller |
| DEVOUR_ANIMATION_FRAMES | 62 | Total frames (1-62 at 60fps) |
| EMERGE_DURATION | 1.04f | Matches animation length |
| PAUSE_DURATION | 1.0f | Hold time with mouth closed |
| SINK_DURATION | 0.5f | Time to sink into ground |
| TRIGGER_RADIUS | 2.5f | Visitor detection radius |

### Frame-based animation control:
```csharp
private void SetDevourAnimatorFrame(int frame)
{
    if (devourAnimator == null) return;

    // Frame is 1-based (1-62), convert to normalized time (0-1)
    float normalizedTime = Mathf.Min(frame / (float)DEVOUR_ANIMATION_FRAMES, 0.999f);

    devourAnimator.Play(DEVOUR_ANIMATION_NAME, 0, normalizedTime);
    devourAnimator.Update(0f);
}
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - Using old animation name
private const string DEVOUR_ANIMATION_NAME = "Devour_24fps_1_25"; // NO! Use "FaceRigAction"

// WRONG - Using old frame count
private const int DEVOUR_ANIMATION_FRAMES = 25; // NO! Animation is 62 frames at 60fps

// WRONG - Resources.Load for controller
Resources.Load<RuntimeAnimatorController>("devour"); // NO! Use AssetDatabase pattern
```

---

## CRITICAL RULE 8: Physics Object Pooling (Prevents 15+ Second Freezes)

**NEVER create/destroy GameObjects with colliders during gameplay - it causes 15+ second freezes!**

### The Problem:
When GameObjects containing colliders are created, destroyed, or activated/deactivated during gameplay:
- Unity's physics engine rebuilds its broadphase data structure
- With many visitors (each has a Rigidbody), this takes 15-20+ seconds
- The freeze happens in FixedUpdate, blocking the entire game
- Even `Physics.autoSyncTransforms = false` doesn't prevent the freeze

### The Solution:
**Pre-create all physics objects at startup and REUSE them.**

1. **Create pooled colliders/objects in Start()** - before gameplay begins
2. **Hide unused objects far underground** (Z = 1000) instead of destroying them
3. **Reposition to actual locations when needed** instead of creating new ones
4. **Never use SetActive() on objects with colliders** - reposition instead

### Implementation Pattern:
```csharp
private const float COLLIDER_HIDDEN_Z = 1000f;

// In Start() - create pool ONCE
private void Start()
{
    CreateColliderPool();      // Pre-create all colliders at Z=1000
    PreCreateTongueInstance(); // Pre-create tongue model at Z=1000
}

// To "spawn" - reposition from underground to play area
colliderObject.transform.position = targetWorldPosition;

// To "despawn" - reposition back underground (don't destroy!)
colliderObject.transform.position = new Vector3(0, 0, COLLIDER_HIDDEN_Z);

// NEVER do these during gameplay:
Instantiate(prefabWithColliders);     // NO! Causes freeze!
Destroy(objectWithColliders);          // NO! Causes freeze!
colliderObject.SetActive(true/false);  // NO! Causes freeze!
AddComponent<Collider>();              // NO! Causes freeze!
AddComponent<Rigidbody>();             // NO! Causes freeze!
```

### Key Constants (HeartOfTheMaze.cs):
| Constant | Value | Description |
|----------|-------|-------------|
| COLLIDER_HIDDEN_Z | 1000f | Z position to hide pooled objects |
| COLLIDER_POOL_SIZE | 28 | Pre-calculated pool size for bone colliders |
| EXPECTED_BONE_COUNT | 540 | Number of bones in tongue model |
| TONGUE_BONE_BLOCKING_RADIUS | 0.25f | Script-based blocking radius per bone |

### Pooled Objects in HeartOfTheMaze:
| Object | Created In | Purpose |
|--------|-----------|---------|
| `pooledColliderObjects[]` | `CreateBoneColliderPool()` | 28 pooled bone colliders (TRIGGER ONLY, no solid colliders!) |
| `heartTongueInstance` | `PreCreateTongueInstance()` | Single reusable tongue model |

### Script-Based Blocking (No Solid Colliders):
Solid colliders caused 14+ second freezes even with pooling, because Unity's physics engine
does expensive collision calculations when many rigidbodies exist (visitors each have one).

**Solution**: Use ONLY trigger colliders for detection, and script-based distance checks for blocking:
- `HeartOfTheMaze.IsPositionBlockedByTongue(Vector2)` - Checks if position is near any bone
- `HeartOfTheMaze.GetUnblockedPosition(Vector2 current, Vector2 target)` - Binary search for safe position
- `VisitorControllerBase.IsBlockedByTongue()` - Uses the above instead of Physics.SphereCast

### Files affected:
- `HeartOfTheMaze.cs` - Tongue and bone colliders use pooling
- Any future code adding physics objects dynamically must follow this pattern

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - creating colliders during gameplay
private void SpawnTongue()
{
    heartTongueInstance = Instantiate(tonguePrefab);  // NO! Causes freeze!
    for (int i = 0; i < bones.Length; i++)
    {
        GameObject obj = new GameObject();
        obj.AddComponent<SphereCollider>();  // NO! Causes freeze!
        obj.AddComponent<Rigidbody>();       // NO! Causes freeze!
    }
}

// WRONG - destroying colliders during gameplay
private void CleanupTongue()
{
    Destroy(heartTongueInstance);  // NO! Causes freeze!
    foreach (var collider in boneColliders)
        Destroy(collider);         // NO! Causes freeze!
}
```

---

## CRITICAL RULE 9: Collider World Position Detection

**ALWAYS use `col.transform.position` for animated/scaled colliders, NEVER `col.bounds.center`!**

### The Problem:
When detecting collider positions for collision blocking, `Collider.bounds.center` can return incorrect world positions for colliders that are:
1. **Parented to animated bones** (transforms change every frame)
2. **Non-uniformly scaled** (scale affects bounds calculation incorrectly)

### The Solution:
Use `col.transform.position` which gives the actual world-space position of the collider's GameObject.

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - bounds.center is incorrect for animated/scaled colliders
Vector3 colliderCenter = col.bounds.center;
Vector2 collider2D = new Vector2(colliderCenter.x, colliderCenter.y);
float dist2D = Vector2.Distance(visitor2D, collider2D);  // Distance will be WRONG!
```

### Correct Pattern:
```csharp
// CORRECT - transform.position gives actual world position
Vector3 colliderWorldPos = col.transform.position;
Vector2 collider2D = new Vector2(colliderWorldPos.x, colliderWorldPos.y);
float dist2D = Vector2.Distance(visitor2D, collider2D);  // Distance is accurate
```

### When This Matters:
- Tongue bone colliders (parented to animated skeleton)
- Any collider with non-uniform scale
- Colliders on procedurally animated objects
- Colliders on objects with complex parent hierarchies

### Reference:
This bug caused visitors to walk through tongue colliders. The colliders were being found by `Physics.OverlapSphere`, but `bounds.center` reported positions 2+ units away from the actual visual position. Using `transform.position` fixed the issue completely.

---

## CRITICAL RULE 7: Heart of the Maze Model Structure

**The Heart of the Maze is a two-part model: a static base ring and an animated tongue.**

### Two-part model architecture:
1. **heartbase** - Static ring/base, no animations
2. **heart tongue** - Procedurally animated tentacle/tongue with reach and grab colliders

### Tongue model orientation:
**The tongue model has base at origin with bones extending along +X toward the tip.**
- Bones extend in local +X direction in model space
- At spawn, tongue instance is rotated -90° around Y: `Quaternion.Euler(0f, -90f, 0f)`
- This transforms model +X to world -Z, so tongue points up when emerging
- Colliders are positioned relative to bone local +X axis

### Key files and assets:
| Asset | Path | Purpose |
|-------|------|---------|
| Base prefab | `Assets/Prefabs/Tile/heartbase.prefab` | Static ring model |
| Tongue prefab | `Assets/Prefabs/Tile/heart tongue.prefab` | Tongue with colliders (no animation controller) |
| Base GLB | `Assets/Animations/heart/heartbase.glb` | Source model for ring (GUID: aaa0818b631d950429276c037c33ddf4) |
| Tongue FBX | `Assets/Animations/heart/heart tongue.glb` | Source model with armature (GUID: c7f35852d61045f4b82d89d34171c99e) |

### Procedural animation (no animation controller):
The tongue is controlled procedurally via direct bone manipulation in `HeartOfTheMaze.cs`.
Bones are rotated to make the tongue emerge, bend toward visitor, curl around them, and retract.

| Phase | Description |
|-------|-------------|
| Emerging | Tongue translates up from z=3 to lip level, bones at rest pose |
| Reaching | Bones above lip rotate to point toward visitor |
| Touching | Tip bones curl around visitor (180° half-circle) |
| Pulling | Tongue retracts, curl rotations locked |
| Sinking | Tongue sinks below ground, visitor consumed |

### Prefab components:
The `heart tongue.prefab` includes:
- **reach** child object with SphereCollider (radius 0.5, trigger) - reparented to tip bone (Bone_539) at runtime
- **grab** child object with SphereCollider (radius 0.5, trigger) - reparented to grab bone (~25% from tip, Bone_404) at runtime

### Collider reparenting at runtime:
```csharp
// Reach collider -> tip bone (Bone_539), positioned at far end along +X
reachColliderTransform.localPosition = new Vector3(1.0f, 0, 0);

// Grab collider -> GRAB_BONE_OFFSET bones from tip (~25%, Bone_404), positioned at bone origin
grabColliderTransform.localPosition = new Vector3(0, 0, 0);
```

### HeartOfTheMaze State Machine:
The `HeartOfTheMaze.cs` implements a three-state system:

| State | Description |
|-------|-------------|
| Idle | Only heartbase visible, monitoring for visitors in detection radius |
| Reaching | Tongue spawned, procedural bone animation toward visitor |
| Grabbing | Visitor locked to grab collider, tongue retracting |

### State transitions:
1. **Idle → Reaching**: Visitor enters detection radius (default 2.5 units)
2. **Reaching → Grabbing**: Wrap complete (reverseCurlProgress >= 1.0)
3. **Grabbing → Idle**: Tongue fully retracted below ground, visitor consumed

### Tongue Phases (within Reaching state):
The tongue progresses through multiple phases while in the Reaching state:
```
Emerging → Extending → Curving → Wrapping → Pulling → Sinking
                          ↓ (miss)
                      Retracting → Extending (retry)
```

| Phase | Description | Visitor State |
|-------|-------------|---------------|
| Emerging | Tongue translates up from z=9 until tip at lip | Walking (unchanged) |
| Extending | Lip bone bends, horizontal section grows to node edge | Walking (unchanged) |
| Curving | Horizontal portion curves toward visitor's predicted position | Walking (unchanged) |
| Retracting | Miss timeout - straighten curve, retract to lip, then retry | Walking (unchanged) |
| Wrapping | Bone collider contact - wrap around visitor from contact point | Grabbed |
| Pulling | Wrap complete, horizontal retraction | Grabbed |
| Sinking | Below lip - rotates down into heart | Grabbed |

**Key behavioral changes:**
- Visitor keeps moving until bone collider contact triggers Wrapping phase
- Tongue aims ahead at visitor's predicted position (using `GetPredictedPosition()`)
- Per-bone colliders (every 5th bone) detect contact anywhere along the tongue
- Miss/retry: If curving for 3+ seconds without contact, retract and try again

### Key implementation details:
```csharp
// Detection radius for visitor proximity
private float detectionRadius = 2.5f;

// Bone direction calculation (bones extend in local +X)
Vector3 boneLocalDir = Vector3.right;  // local +X
Vector3 boneWorldDir = parentWorldRot * boneRestRotations[i] * boneLocalDir;

// Move visitor with grab collider (all axes for sinking)
Vector3 grabPos = grabColliderTransform.position;
targetVisitor.transform.position = grabPos;
```

### Asset loading pattern:
```csharp
#if UNITY_EDITOR
heartBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heartbase.prefab");
heartTonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heart tongue.prefab");
#endif
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - Using Resources.Load
Resources.Load<GameObject>("heart tongue"); // NO! Use AssetDatabase pattern

// WRONG - Assuming bones extend in +Y (old model orientation)
Vector3 boneLocalY = Vector3.up; // NO! Bones extend in +X

// WRONG - Spawning tongue without detection
// Tongue should ONLY spawn when visitor enters detection radius
```

### Key constants:
| Constant | Value | Purpose |
|----------|-------|---------|
| detectionRadius | 2.5 | Radius to detect visitors and trigger reaching state |
| TONGUE_START_Z | 6.0 | Starting Z position (below ground, increased for longer model) |
| TONGUE_LIP_Z | -0.5 | Z where tip emerges above lip (triggers Reaching phase) |
| GRAB_BONE_OFFSET | 135 | Grab bone is ~25% from tip (bone 404 out of 540) |
| TONGUE_EMERGE_SPEED | 1.5 | Units per second for vertical movement |
| TONGUE_CURL_SPEED | 2.0 | Rate of curl progress (0→1) |

### Bone hierarchy:
The tongue has 540 bones (indices 0-539), named Bone_000 through Bone_539:
- Bone_000: Base of tongue (root)
- Bone_001 through Bone_538: Segments along tongue
- Bone_539: Tip of tongue
- Grab bone: Index 404 (GRAB_BONE_OFFSET=135 from tip, ~25% back from end)
- All bones have localPosition (0,0,0) - bone chain extends in local +X

---

## TODO

### Completed - Heart Tongue Visitor Consumption

**Status**: Fully implemented and working. Tongue emerges, extends, curls around visitor, tightens, hinges 90°, and descends into heart.

**Model details:**
- 540 bones named Bone_000 through Bone_539
- Base at origin, tip extends along +X
- Bones use local +Y as forward direction after rest rotation
- Prefab scale: (1, 0.3, 0.3), no runtime scaling applied
- Tongue length ~8.1 world units
- 27 bone colliders (every 20th bone) for contact detection and physics blocking

**Six-phase tongue sequence:**
1. **Emerging**: Tongue translates up from z=9 until tip reaches lip (z=-0.25)
2. **Extending**: Lip bone bends 90°, horizontal section grows to detection radius
3. **Curling**: 360° closed circle forms around visitor, physics colliders contain visitor
4. **Pulling**: Tongue retracts to tighten curl, physics colliders push visitor inward
5. **Hinging**: Tight curl rotates 90° from horizontal to vertical, visitor rotates with it
6. **Descending**: Hinged curl pulls straight down into heart, visitor manually positioned

**Physics-based containment:**
- During Curling: Solid colliders surround and trap the visitor
- During Pulling: Tongue retracts (tongueZPosition increases), fewer horizontal bones = tighter curl
- During Hinging: Colliders rotate and push visitor, manual rotation delta applied to visitor
- During Descending: Manual positioning (physics won't work underground)

**Key implementation details:**
- File: `HeartOfTheMaze.cs`
- State machine: `HeartState` (Idle, Reaching, Grabbing)
- Tongue phase: `TonguePhase` (Emerging, Extending, Curling, Pulling, Hinging, Descending)
- Bone direction: `Vector3.up` (local +Y points toward next bone)
- Curl is HORIZONTAL (parallel to ground, in XY plane)
- Bone colliders: solid (blocking) on every 20th bone

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| TONGUE_START_Z | 9.0 | Starting Z position (below ground) |
| TONGUE_LIP_Z | -0.25 | Z where tip emerges above lip |
| TONGUE_EMERGE_SPEED | 1.5 | Units per second for vertical movement |
| TONGUE_CURL_SPEED | 2.0 | Rate of curl/hinge progress (0→1) |
| PULLING_SPEED | 0.5 | Rate of curl tightening (0→1 per second) |
| BONE_COLLIDER_SPACING | 20 | Add collider every 20th bone (27 colliders) |
| BONE_COLLIDER_RADIUS | 0.0045 | World radius (localScale=0.03, radius=0.15) |
| GRAB_BONE_OFFSET | 50 | Bones from tip for grab collider |
| CURL_DIAMETER | 0.5 | Target tight curl diameter |
| detectionRadius | 3.0 | Visitor detection radius |

**Phase transitions:**
1. **Emerging → Extending**: Tip reaches TONGUE_LIP_Z
2. **Extending → Curling**: Horizontal length >= detectionRadius
3. **Curling → Pulling**: 360° curl complete AND visitor contact made
4. **Pulling → Hinging**: pullingProgress >= 1.0 (curl fully tight)
5. **Hinging → Descending**: sinkingRotationProgress >= 1.0 (90° rotation complete)
6. **Descending → Idle**: tongueZPosition >= TONGUE_START_Z (visitor consumed)

---

### Completed - HeartwardGrasp (Heart Power 2)

**Status**: Fully implemented with tongue-based grabbing. Uses the same tongue prefab and vertical emergence behavior as HeartOfTheMaze.

**What works:**
- Grabbing HGZ: Idle → Emerging → Extending → Curling → Pulling → Transporting ✓
- Pushing HGZ: Emerging → Uncurling → Withdrawing ✓
- Tongue emerges vertically from ground at wall tile position (like HeartOfTheMaze) ✓
- Lip bone bends 90° to extend horizontally toward visitor ✓
- 360° curl wraps around visitor ✓
- Curl tightens while tongue sinks back into ground ✓
- Reverse curl releases visitor near heart ✓
- Push continues until visitor is on valid walkable area ✓

**Grabbing HGZ State Machine (tongue-based, mirrors HeartOfTheMaze):**
| Phase | Description |
|-------|-------------|
| Idle | Waiting for visitors to enter the zone |
| Emerging | Tongue translates up from z=TONGUE_START_Z until tip at TONGUE_LIP_Z |
| Extending | Lip bone bends 90°, horizontal section grows to GRASP_ZONE_RADIUS |
| Curling | Tongue curls into 360° horizontal circle around visitor |
| Pulling | Curl tightens, tongue sinks back into ground (+Z) |
| Transporting | 1 second, visitor invisible, relocate to pushing zone |

**Pushing HGZ State Machine (tongue-based, reverse of grab):**
| Phase | Description |
|-------|-------------|
| Idle | Waiting for transported visitor |
| Emerging | Tongue emerges from wall with visitor curled inside |
| Uncurling | Tongue uncurls to release visitor (reverse of curl) |
| Withdrawing | Tongue retracts back into wall |

**Key implementation details:**
- File: `HeartPowerEffects.cs` - `HeartwardGraspEffect` class (line ~1946)
- Uses same tongue prefab as HeartOfTheMaze: `Assets/Prefabs/Tile/heart tongue.prefab`
- Tongue spawned as child of grabbingZoneObject at wall tile position
- Vertical Z-axis emergence: `grabbingTongueZPosition` controls root Z position
- Lip bone index calculated dynamically based on `grabbingTongueZPosition`
- Bone rotations controlled via `ApplyGrabbingTongueBoneState()` (mirrors HeartOfTheMaze)
- Contact detection via `CheckTongueBoneContact()` - checks 2D distance from bones to visitor

**Tongue bone structure (same as HeartOfTheMaze):**
- 540 bones named Bone_000 through Bone_539
- Bones use local +Y as forward direction after rest rotation
- Bone colliders created on every 20th bone (27 colliders) for contact detection

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| GRASP_ZONE_RADIUS | 2.5 | Trigger radius for visitor detection |
| TONGUE_START_Z | 9.0 | Starting Z position (below ground) |
| TONGUE_LIP_Z | -0.25 | Z where tip emerges above lip |
| TONGUE_EMERGE_SPEED | 6.0 | Units per second for vertical movement |
| TONGUE_EXTEND_SPEED | 4.0 | Rate of bone rotation for extending |
| TONGUE_CURL_SPEED | 3.0 | Rate of curl for grabbing |
| TONGUE_RETRACT_SPEED | 4.0 | Speed when retracting |
| BEND_BONE_COUNT | 3 | Bones for the 90° bend at lip |
| BONE_COLLIDER_RADIUS | 0.0045 | World radius (localScale=0.03, radius=0.15) |
| MIN_PUSH_DISTANCE | 1.0 | Minimum push before checking for valid area |
| MAX_PUSH_DISTANCE | 10.0 | Safety limit for push distance |
| GRAB_ESSENCE_COST | 25 | Essence deducted from visitor when grabbed |

**NOTE**: Prefab scale is (1, 0.3, 0.3), no runtime scaling applied.

**Vertical emergence behavior (like HeartOfTheMaze):**
The wall tile position acts as the "heart center" for the grabbing tongue:
1. Tongue spawned as child of grabbingZoneObject at wall tile XY position
2. Initial Z = TONGUE_START_Z (9.0, below ground)
3. During Emerging: Z decreases until tip reaches TONGUE_LIP_Z (-0.25)
4. During Extending: Z continues decreasing, lip bone bends 90° toward visitor
5. During Pulling: Z increases (tongue sinks back into ground with visitor)

**FindForestDirection algorithm (HGZ placement):**
The HGZ is placed at a wall tile position, with the direction determined by finding the deepest forest:
1. Collect all walkable tile positions within 5 units of HGZ position
2. Test all 360° directions in 5° increments
3. For each direction, place a candidate point at 3 units
4. Skip if candidate point is walkable (we want forest positions only)
5. Calculate minimum distance from candidate to ANY walkable tile
6. Select direction with greatest minimum distance = deepest into forest

---

### Heart Power Essence Costs

**Player essence costs to activate powers (defined in ScriptableObject assets):**

| Power | Essence Cost | Asset File |
|-------|-------------|------------|
| Power 1 (MurmuringPaths) | 100 | `MurmuringPaths_T1.asset` |
| Power 2 (HeartwardGrasp) | 10 | `HeartwardGrasp_T1.asset` |
| Power 3 (DevouringMaw) | 50 | `DevouringMaw_T1.asset` |
| Power 4 (Sculpting) | 0 | Free to use |

**Visitor essence costs:**

| Effect | Essence Cost | Location |
|--------|-------------|----------|
| Grabbed by HeartwardGrasp | 25 | `HeartPowerEffects.cs` GRAB_ESSENCE_COST |

The visitor essence deduction uses `VisitorControllerBase.DeductEssence(float amount)` which triggers `OnEssenceDepleted()` if essence drops to 0.

---

### Completed - Sculpting (Heart Power 4)

**Status**: Fully implemented and working.

**What it does**: Toggle power that opens a radial menu to change a node's prop type (Pond, Lantern, FairyRing, or Remove).

**Key files:**
- `HeartPowerEffects.cs` - `SculptingEffect` class (line ~3960)
- `DynamicMazeGrowth.cs` - `SetNodeProp()` and `RemovePropFromNode()` methods
- `FaeLantern.cs` - `ReleaseAllFascinatedVisitors()` on disable
- `FairyRing.cs` - `ReleaseAllFascinatedVisitors()` on disable
- `VisitorControllerBase.cs` - `EndLanternFascination()` and `EndRingFascination()` public methods

**Implementation details:**
- Radial menu with 5 circular buttons (center cancel + 4 prop options)
- Menu size is 50% of screen height (uses reference height 1080 for CanvasScaler)
- Prop preview images loaded from `Assets/Textures/PropPreviews/` (pond_preview.png, lantern_preview.png, ring_preview.png)
- Remove option uses EarthenGroundTexture
- Smoke particle effect spawns on prop change (0.5 second duration, expands to NODE_RADIUS)

**Prop effect cleanup on change:**
When a prop is changed/removed:
1. `RemovePropFromNode()` destroys the old prop GameObject
2. Prop's `OnDisable()` calls `ReleaseAllFascinatedVisitors()`
3. All visitors affected by that prop have their fascination ended immediately
4. Visitors resume normal walking behavior
5. New prop (if any) starts fresh with no historical effects

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| MENU_SCREEN_HEIGHT_FRACTION | 0.5 | Menu is 50% of screen height |
| BUTTON_SIZE_FRACTION | 0.30 | Buttons are 30% of menu size |
| CENTER_BUTTON_FRACTION | 0.22 | Center button is 22% of menu size |
| MENU_RADIUS_FRACTION | 0.33 | Button positions at 33% from center |
| NODE_RADIUS | 3.0 | Node size for smoke effect coverage |
| SMOKE_DURATION | 0.5 | Smoke effect duration in seconds |

**Visitor state cleanup:**
- `VisitorControllerBase.CurrentFaeLantern` - public property to check which lantern is fascinating visitor
- `VisitorControllerBase.CurrentFairyRing` - public property to check which ring is fascinating visitor
- `VisitorControllerBase.EndLanternFascination()` - forcibly ends lantern fascination without cooldown
- `VisitorControllerBase.EndRingFascination()` - forcibly ends ring fascination without immunity

---

### Completed - RedCap Essence-Based Behavior

**Status**: Implemented. RedCap now spawns/despawns based on essence thresholds, not time.

**Spawn condition**: RedCap spawns when:
- `enableRedCap` is true (in GameSettings)
- No RedCap currently exists on the graph
- Player essence >= 2× starting essence

**Flee condition**: RedCap flees to exit when:
- Player essence drops below starting essence
- Paths to nearest exit and despawns upon arrival

**Killing behavior**:
- When RedCap catches a visitor, enters Killing state for 1 second
- Visitor is dazed (immobilized) during kill
- After 1 second, visitor is destroyed and essence penalty applied
- RedCap then returns to Hunting (or Fleeing if essence is low)

**Frightening visitors**:
- Visitors within `frightenRadius` (default 5.0 units) become Frightened
- Checked every `frightenCheckInterval` (default 0.25 seconds)
- Frightened visitors flee away from RedCap

**Key constants (RedCapController.cs):**
| Constant | Value | Description |
|----------|-------|-------------|
| killingDuration | 1.0f | Seconds to complete a kill |
| frightenRadius | 5.0f | Radius to frighten visitors |
| frightenCheckInterval | 0.25f | Seconds between frighten checks |

**Files modified:**
- `RedCapController.cs` - New states (Killing, Fleeing), frightening logic
- `WaveSpawner.cs` - Essence-based spawn logic
- `GameSettings.cs` - Removed `RedCapSpawnDelay`
- `OptionsManager.cs` - Removed spawn delay UI

---

### Completed - Game Over Statistics

**Status**: Implemented. Game Over scene displays visitor fates and essence summary.

**Key files:**
- `GameStatsTracker.cs` - Singleton that tracks visitor fates by archetype
- `GameOverManager.cs` - Displays statistics on Game Over scene
- `VisitorControllerBase.cs` - Records fates when visitors exit/die

**VisitorFate enum** (defined in `GameStatsTracker.cs`):
| Fate | Description | Recorded By |
|------|-------------|-------------|
| Consumed | Consumed by Heart tongue | `HeartOfTheMaze.OnVisitorConsumed()` |
| Devoured | Devoured by Maw power | `HeartPowerEffects.DevouringMawEffect.ConsumeVisitor()` |
| FairyRing | Essence depleted at fairy ring | `VisitorControllerBase.OnEssenceDepleted()` |
| Lantern | Essence depleted at lantern | `VisitorControllerBase.OnEssenceDepleted()` |
| Escaped | Escaped through exit portal | `VisitorControllerBase.OnExitedThroughPortal()` |
| RedCapKill | Killed by RedCap | `RedCapController.CompleteKill()` |
| Drowned | Drowned by Puka/Kelpie | `PukaHazard.DrownVisitorCoroutine()` |

**Recording a visitor fate:**
```csharp
GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, VisitorFate.Consumed, essenceValue);
```

**Game Over display shows:**
1. Max wave reached
2. Game length (MM:SS format)
3. Visitor fates with counts and essence per fate
4. Essence summary by source (from GameController.EssenceAuditLog)
5. Net essence change

**Note**: Props placed tracking was removed - players no longer place props manually.

---

### In Progress
- [ ] Ensure other visitor types work as intended with heart powers
- [x] ~~Test tongue collision fix~~ - **FIXED** - visitors now properly blocked by tongue colliders (see Session Notes item 12)

### Heart & Powers
- [x] Fix heart prefab - separated into two parts (heartbase + heart tongue) with state machine
- [x] Sculpting power (Heart Power 4) - radial menu to change node props
- [x] Make icons for heart power buttons - used in sculpt power selection menu
- [x] Finalize heart power essence use costs - see Heart Power Essence Costs section
- [x] Heart tongue visitor consumption cycle - complete with 5-phase sequence
- [ ] Push magic numbers and constants to configurable settings

### UI & Scenes
- [ ] Synchronize, consolidate, and rationalize options scene (ON HOLD - see Options Scene Restructure section below)
- [x] Clean up game over scene - redesigned with visitor fates and essence summary (see Game Over Statistics section)
- [ ] Improve player UI layout
- [x] Replace the focus point indicator - now uses conic section with spiraling energy bolts

### Game State
- [ ] Enable game over state
- [ ] Implement difficulty progression

### Visitors
- [x] Enable all visitor types - removed individual enable/disable toggles from options
- [x] All visitor archetypes now always spawn (LanternDrunk, WaryWayfarer, SleepwalkingDevotee)

---

## Session Notes (January 2026)

### Recent Changes This Session

1. **Sculpting Power Menu Sizing**: Changed from fixed pixel sizes to screen-relative sizing (50% of screen height). Uses reference height 1080 since CanvasScaler is set to ScaleWithScreenSize.

2. **Prop Preview Images**: Switched from runtime rendering to pre-saved PNG textures in `Assets/Textures/PropPreviews/`. User needs to save screenshots of props from editor scene at (0,0,0), (8,0,0), (12,0,0).

3. **Visitor Fascination Cleanup**: When props are destroyed/changed:
   - Added `OnDisable()` to `FaeLantern.cs` and `FairyRing.cs` that calls `ReleaseAllFascinatedVisitors()`
   - Added public properties `CurrentFaeLantern` and `CurrentFairyRing` to `VisitorControllerBase`
   - Added public methods `EndLanternFascination()` and `EndRingFascination()` to `VisitorControllerBase`
   - Existing check in Update loop already handles lantern destroyed mid-fascination

4. **Smoke Effect**: Added `SpawnSmokeEffect()` method in SculptingEffect that creates a particle system:
   - 80 particles burst from center
   - Expands to NODE_RADIUS (3.0) over 0.5 seconds
   - Uses noise for organic swirling motion
   - Pale cream/smoke colors
   - Auto-destroys after completion

5. **LanternGlow Material Leak Fix**: Added early return in edit mode to prevent material instantiation warnings.

### Files Modified This Session
- `HeartPowerEffects.cs` - SculptingEffect class, smoke effect, menu sizing
- `VisitorControllerBase.cs` - CurrentFaeLantern/CurrentFairyRing properties, EndLanternFascination/EndRingFascination methods, null lantern check in Update, GetCurrentPath() method, **IsBlockedByTongue() collision fix (bounds.center → transform.position)**
- `FaeLantern.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `FairyRing.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `LanternGlow.cs` - Edit mode material leak fix
- `HeartOfTheMaze.cs` - CalculatePathInterceptionPoint() rewritten, **IsTongueActiveWithColliders static flag added**

6. **Focal Point Indicator Replacement** (FocalPointGlow.cs):
   - Replaced pink cylinder with a conic section surface following z = -1/(10*r^1.5)
   - Energy bolts spiral along the cone surface with jagged lightning appearance
   - Colors alternate between dark red and purple (previously blue/purple)
   - Dynamic fog occlusion: when over walkable area, extends to ground (z=0); when over fog, stops at fog level (z=-1)
   - Points above fog cutoff collapse to previous valid point (bolt disappears into fog, no pooling)
   - Bolts and branches regenerate jitter every 0.08s for flickering effect

7. **Heart Tongue Debug Visualization Removed** (HeartOfTheMaze.cs):
   - Removed `AddDebugSphere()` method entirely
   - Removed cyan (reach) and magenta (grab) debug sphere meshes
   - Colliders remain functional (SphereCollider triggers still work for detection)

8. **Heart Tongue Aiming Fix** (HeartOfTheMaze.cs):
   - **Problem**: Tongue was aiming at visitor's current/predicted position instead of their EXIT point from the node
   - **Solution**: Rewrote `CalculatePathInterceptionPoint()` to use visitor's actual worldPath
   - Now iterates through visitor's path points to find the first point OUTSIDE the node boundary
   - Uses ray-circle intersection (t2 = -b + sqrtDisc) to find exact exit point on the node edge
   - Tongue extends to where visitor will EXIT, then curves back toward them

   **Implementation:**
   ```csharp
   // Get the visitor's actual path
   List<Vector3> visitorPath = visitor.GetCurrentPath(out currentPathIndex);

   // Find first path point OUTSIDE node boundary
   for (int i = currentPathIndex; i < visitorPath.Count; i++)
   {
       float distFromHeart = Vector2.Distance(pathPoint2D, heartPos);
       if (distFromHeart > nodeRadius)
       {
           // Ray-circle intersection for exact exit point
           float t2 = -b + sqrtDisc;  // Exit point (farther intersection)
           return prevPoint2D + t2 * segmentDir;
       }
   }
   ```

9. **Visitor Path Exposure** (VisitorControllerBase.cs):
   - Added `GetCurrentPath(out int currentIndex)` method to expose visitor's worldPath
   - Required for tongue aiming to use actual path instead of guessing movement direction

10. **Tongue Collision Z-Level Fix** (VisitorControllerBase.cs) - **TESTED AND WORKING**:
    - **Problem**: Visitors were walking through the tongue
    - **Root cause 1**: Tongue colliders are at varying Z levels, visitors at z=0
    - **Root cause 2**: Using `col.bounds.center` returned incorrect positions for scaled colliders on animated bones
    - **Solution**: Use `col.transform.position` for accurate world positions, search with large radius (10 units)

    **Implementation:**
    ```csharp
    // Early exit if no tongue active - avoids expensive Physics.OverlapSphere
    if (!Maze.HeartOfTheMaze.IsTongueActiveWithColliders) return false;

    // Search from visitor position with large radius
    Collider[] overlaps = Physics.OverlapSphere(currentPos, 10f, ~0, QueryTriggerInteraction.Ignore);
    foreach (var col in overlaps)
    {
        if (!col.enabled || col.isTrigger) continue;
        if (!col.gameObject.name.StartsWith("SolidCollider_")) continue;

        // CRITICAL: Use transform.position, NOT bounds.center
        Vector3 colliderWorldPos = col.transform.position;
        Vector2 collider2D = new Vector2(colliderWorldPos.x, colliderWorldPos.y);
        float dist2D = Vector2.Distance(current2D, collider2D);

        if (dist2D < blockRadius) { /* blocked */ }
    }
    ```

    **Key fix**: `col.bounds.center` returns incorrect positions for colliders that are scaled and parented to animated bones. Using `col.transform.position` gives the actual world-space position.

    **Key values:**
    - Search radius: 10.0 (catches colliders at any Z level)
    - Visitor collision radius: 0.2f
    - Average collider radius: 0.2f (ranges from 0.3 at base to 0.1 at tip)
    - Block radius: 0.4f (visitor + collider)

11. **FindForestDirection Rewrite** (HeartPowerEffects.cs) - PARTIALLY TESTED:
    - **Problem**: Grabbing hand was emerging from wrong side of path (opposite side from forest interior)
    - **Root cause**: Previous algorithm measured "forest depth" by probing outward, but both sides of a straight path have similar depth
    - **Solution**: Rewrote to find direction with greatest minimum distance from ALL nearby walkable tiles
    - **Status**: Passed initial tests, may need further validation on complex path geometries

    **Algorithm:**
    1. Collect all walkable tiles within 5 units (2× GRASP_ZONE_RADIUS)
    2. For each direction (360° in 5° steps), place candidate point at 3 units
    3. Skip if candidate is walkable (want forest only)
    4. Calculate minimum distance from candidate to ANY walkable tile
    5. Pick direction with greatest minimum distance = deepest into forest

    **Key change**: Instead of measuring how far you can go before hitting walkable (forest depth), now measures how far candidate point is from ALL walkable tiles (isolation metric).

12. **Tongue Collision Static Flag Optimization** (HeartOfTheMaze.cs) - **IMPLEMENTED**:
    - **Problem**: Expensive `Physics.OverlapSphere` was being called every frame for every visitor, even when no tongue existed
    - **Solution**: Added `public static bool IsTongueActiveWithColliders` property
    - Set to `true` in `EnableBoneColliders()` when tongue colliders are active
    - Set to `false` in `DisableBoneColliders()` when tongue colliders are disabled
    - Visitors check this flag and skip the expensive search when no tongue is active

    **Implementation:**
    ```csharp
    // In HeartOfTheMaze.cs
    public static bool IsTongueActiveWithColliders { get; private set; } = false;

    private void EnableBoneColliders()
    {
        // ... enable colliders ...
        IsTongueActiveWithColliders = true;
    }

    private void DisableBoneColliders()
    {
        // ... disable colliders ...
        IsTongueActiveWithColliders = false;
    }
    ```

### Key Constants (FocalPointGlow.cs)
| Constant | Value | Description |
|----------|-------|-------------|
| GROUND_Z_LEVEL | 0 | Z cutoff when over walkable area |
| FOG_Z_LEVEL | -1 | Z cutoff when over fog |
| BOLT_REGENERATE_INTERVAL | 0.08f | Seconds between jitter regeneration |
| deepPurple | (0.4, 0.1, 0.6) | Purple bolt color |
| darkRed | (0.6, 0.1, 0.15) | Dark red bolt color |

---

### On Hold - Options Scene Restructure

**Status**: On hold. Partially implemented. Key issues identified but not fully resolved.

**Goal**: Reorganize the Options scene into a tabbed interface with three tabs (Gameplay, Video, Audio), each containing collapsible sections with consistent styling.

**Key files:**
- `Assets/Editor/OptionsSceneRestructure.cs` - Editor script to restructure the scene
- `Assets/Scripts/UI/CollapsibleSection.cs` - Component for expandable/collapsible sections
- `Assets/Scripts/UI/OptionsManager.cs` - Manages options panel state and settings
- `Assets/Scenes/Options.unity` - The Options scene file

**Editor script usage:**
Run from Unity menu: `FaeMaze > Restructure Options Scene`

**IMPORTANT**: Before running the script, restore Options.unity from git:
```bash
git checkout HEAD -- Assets/Scenes/Options.unity
```
This ensures AudioSection and CameraSection exist in the scene for content to be moved properly.

**Tab structure:**
| Tab | Content |
|-----|---------|
| Gameplay | VisitorSection, VisitorTypeSection, WaveSection, FlowSection |
| Video | DISPLAYSETTINGSSection (Fullscreen toggle, Resolution dropdown) |
| Audio | MASTER VOLUME (SFX, Music), PROP SOUNDS (Lantern, Fairy Ring, Pond, Sculpt) |

**Style constants (must match existing Gameplay sections):**
| Element | Value |
|---------|-------|
| Header background color | `new Color(0.2f, 0.3f, 0.4f, 1f)` (blue/teal) |
| Header font size | 28px |
| Header height | 50px |
| Content font size | 20px |
| Scroll view bottom padding | 120px (clearance for Apply/Reset/Back buttons) |

**Known issues being addressed:**
1. **AudioSection/CameraSection not found**: Scene needs to be restored from git before running script. Fallback code creates settings from scratch but may not match original styling perfectly.

2. **Style inconsistency across tabs**: Fixed header color from green (0.2, 0.4, 0.3) to blue/teal (0.2, 0.3, 0.4). Fixed header font size from 20px to 28px.

3. **Scroll view overlap with bottom buttons**: Increased `offsetMin` from 60px to 120px for proper clearance.

**Prop sound volume controls:**
Individual volume sliders for each prop type, combined with master SFX volume:
```csharp
// In PropAudioSource.UpdateVolume()
float finalVolume = propVolume * masterSfxVolume * distanceVolume * (isActive ? 1f : 0f);
```

**CollapsibleSection component:**
- Uses `v` and `>` ASCII characters for expand/collapse indicators (not Unicode arrows which may not render)
- Structure: Header (Button + ArrowText + HeaderText) → ContentPanel
- `startExpanded` serialized field controls initial state

**Next steps when resuming:**
1. Restore Options.unity from git
2. Run the restructure script
3. Verify all settings appear in correct sections
4. Verify scroll view no longer overlaps bottom buttons
5. Test font consistency across all tabs
6. Save scene and test in play mode

---

### FIXED - Heart Tongue Grab Behavior (Session Jan 27, 2026)

**Status**: FIXED - Tongue now properly blocks visitors and completes the grab sequence.

**Bug 1: Visitors walking through tongue colliders** - **FIXED**
- **Root cause**: `col.bounds.center` returned incorrect world positions for scaled colliders parented to animated bones
- **Solution**: Changed to use `col.transform.position` which gives actual world-space position
- **File**: `VisitorControllerBase.cs` - `IsBlockedByTongue()` method

**Bug 2: Performance - expensive search every frame** - **FIXED**
- **Root cause**: `Physics.OverlapSphere` called every frame for every visitor even when no tongue existed
- **Solution**: Added `HeartOfTheMaze.IsTongueActiveWithColliders` static flag
- Visitors skip the expensive search when flag is false
- **File**: `HeartOfTheMaze.cs` - `EnableBoneColliders()` and `DisableBoneColliders()`

**Working tongue sequence:**
1. Tongue emerges and extends toward visitor's exit point
2. Tongue curls into 360° spiral around visitor
3. Solid colliders block visitor from walking through tongue
4. When tip touches shaft, visitor is grabbed
5. Pulling phase retracts tongue with visitor
6. Sinking phase pulls visitor into heart for consumption

**Key implementation details:**
- Use `col.transform.position` NOT `col.bounds.center` for collider positions
- Search radius 10.0 (catches colliders at any Z level)
- Block radius 0.4 (visitor radius 0.2 + collider radius 0.2)
- Static flag avoids expensive physics queries when no tongue active

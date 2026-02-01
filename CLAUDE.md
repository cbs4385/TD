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
| EXPECTED_BONE_COUNT | 540 | Number of bones in tongue model |

### Pooled Objects in HeartOfTheMaze:
| Object | Created In | Purpose |
|--------|-----------|---------|
| `heartTongueInstance` | `PreCreateTongueInstance()` | Single reusable tongue model with baked colliders |

### Physics-Based Blocking (Colliders Baked in Prefab):
Colliders are **baked into the tongue prefab** by the editor script `TonguePrefabColliderSetup.cs`.
Since colliders are part of the prefab and never created/destroyed at runtime, they don't cause freezes.

**After running FaeMaze > Setup Tongue Prefab Colliders:**

**The tongue prefab includes BOTH solid and trigger colliders per bone (every 10th bone = 54 pairs):**
- `SolidCollider_N` objects - Solid sphere colliders (not reliably detected by OnCollisionEnter)
- `BoneCollider_N` objects - Trigger sphere colliders for detection events
- `TipTrigger` - Trigger on tip for exit detection

**CRITICAL: Use OnTriggerEnter with BoneCollider_N for detection!**
- `OnCollisionEnter` with `SolidCollider_N` is **unreliable** - collision events often don't fire
- `OnTriggerEnter` with `BoneCollider_N` **works reliably** for detecting tongue contact
- Detection code should check for `BoneCollider_N` or `TipTrigger` names in OnTriggerEnter

**Solid collider setup (in prefab, created by editor script):**
```csharp
// SOLID - for physics blocking
SphereCollider solidSphere = solidObj.AddComponent<SphereCollider>();
solidSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
solidSphere.isTrigger = false;  // SOLID for physics blocking

Rigidbody solidRb = solidObj.AddComponent<Rigidbody>();
solidRb.isKinematic = true;     // Moves with bones, doesn't respond to forces
solidRb.useGravity = false;
```

**Trigger collider setup (same bone, created by editor script):**
```csharp
// TRIGGER - for detection events
SphereCollider triggerSphere = triggerObj.AddComponent<SphereCollider>();
triggerSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
triggerSphere.isTrigger = true;  // TRIGGER for OnTriggerEnter events

Rigidbody triggerRb = triggerObj.AddComponent<Rigidbody>();
triggerRb.isKinematic = true;
triggerRb.useGravity = false;
```

**Collider sizing (accounts for 100× Armature scale):**
- Tapered from base to tip
- World radius at base: ~0.3 units (scale 0.006)
- World radius at tip: ~0.1 units (scale 0.002)

### Files affected:
- `TonguePrefabColliderSetup.cs` - Editor script that bakes colliders into prefab
- `HeartOfTheMaze.cs` - Tongue instance uses pooling (prefab instantiated once at startup)
- Any future code adding physics objects dynamically must follow the pooling pattern

### Physics Layer Configuration:
For tongue colliders to block visitors via Unity physics, the layer collision matrix must allow collisions:

| Object | Layer | Layer Index | Collider Type | Rigidbody |
|--------|-------|-------------|---------------|-----------|
| Visitor "Detect" child | Visitor | 6 | CapsuleCollider (solid) | On root (NOT Detect) |
| Tongue SolidCollider_N | Default | 0 | SphereCollider (solid) | Kinematic |
| Tongue BoneCollider_N | Default | 0 | SphereCollider (trigger) | Kinematic |

**Visitor Collider Architecture (IMPORTANT):**
- Collider is on a child object named "Detect" baked into each visitor model prefab
- NO runtime collider creation - colliders are pre-baked into prefabs
- The "Detect" object is set to layer 6 (Visitor) at runtime by `SetupDetectCollider()`
- **CRITICAL**: The Detect object must NOT have its own Rigidbody - it must be a compound collider using the root's Rigidbody

**Compound Collider Requirement (CRITICAL for Physics):**
Unity physics requires at least one non-kinematic Rigidbody for collision response.
- Tongue bone colliders: kinematic Rigidbody (must be kinematic to move with bones)
- Visitor Detect child: NO Rigidbody (compound collider uses parent's)
- Visitor root: non-kinematic Rigidbody (receives physics responses)

If the Detect child has its own kinematic Rigidbody, kinematic-to-kinematic collision won't work!
`SetupDetectCollider()` destroys any Rigidbody on the Detect child to ensure proper physics.

**Visitor "Detect" CapsuleCollider configuration (baked in prefab):**
- Radius: 0.5
- Height: 1.5
- Direction: Y-axis
- Center: (0, 0, 0)
- NO Rigidbody on Detect object (runtime destroyed if present)

**Key physics requirements for collision to work:**
1. Both colliders must be solid (`isTrigger = false`)
2. At least one must have a non-kinematic Rigidbody (visitor root has this)
3. Detect child must NOT have its own Rigidbody (compound collider uses parent's)
4. Layer collision matrix must allow Default ↔ Visitor collisions (check `DynamicsManager.asset`)
5. Colliders must **actually overlap in 3D space** (check Z-level alignment!)

**Hierarchy:**
```
Visitor (root) - Rigidbody (non-kinematic), layer 6
  └─ ModelInstance (instantiated prefab)
       └─ Armature
       └─ Detect - CapsuleCollider (solid), NO Rigidbody, layer 6
```

**Debug: Check `DynamicsManager.asset` LayerCollisionMatrix:**
The hex string encodes which layers can collide. To verify Visitor (6) and Default (0) can collide:
- Look at the `m_LayerCollisionMatrix` in `ProjectSettings/DynamicsManager.asset`
- Each layer's collision mask is 4 bytes (8 hex chars)
- If the bit for layer 0 is set in layer 6's mask, they can collide

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

## CRITICAL RULE 9: Velocity-Based Movement (NEVER MovePosition!)

**NEVER use `MovePosition()` for visitor movement - it TELEPORTS and bypasses physics collisions!**

### The Problem:
`Rigidbody.MovePosition()` **teleports** the rigidbody to the desired position, completely ignoring solid colliders in the way. This means visitors walk right through tongue `SolidCollider_N` objects even though collision events fire.

### The Solution:
Use **velocity-based movement** in `FixedUpdate()`. Set a velocity toward the destination and let Unity physics handle collision naturally:

```csharp
// CRITICAL: Use VELOCITY-based movement, NOT MovePosition()!
// MovePosition() teleports and bypasses solid colliders.
Vector3 moveDir = desiredPosition - rb3D.position;
float moveDist = moveDir.magnitude;

if (moveDist > 0.001f)
{
    // Calculate velocity to reach desired position in one fixed timestep
    // Physics will naturally stop us if we hit a solid collider
    float speed = moveDist / Time.fixedDeltaTime;
    rb3D.linearVelocity = moveDir.normalized * speed;
}
else
{
    rb3D.linearVelocity = Vector3.zero;
}
```

### How It Works:
1. Calculate direction and distance to desired position
2. Set velocity to reach that position in one physics timestep
3. Unity physics engine integrates velocity and checks for collisions
4. If visitor hits a `SolidCollider_N`, physics naturally stops them
5. `OnCollisionEnter`/`OnCollisionStay` set `isBlockedByTongue = true`
6. When blocked, we skip setting new velocity - physics handles the rest

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - MovePosition TELEPORTS and bypasses solid colliders!
rb3D.MovePosition(desiredPosition);  // NO! Visitor walks through tongue!

// WRONG - Zeroing velocity when blocked prevents natural physics response
if (isBlockedByTongue)
{
    rb3D.linearVelocity = Vector3.zero;  // NO! Let physics push naturally!
}
```

---

## CRITICAL RULE 10: Collider World Position Detection

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

## CRITICAL RULE 11: Rigidbody Position Sync When Releasing Grabbed Objects

**When releasing a visitor that was moved via `transform.position`, ALWAYS sync `rb3D.position` first!**

### The Problem:
When a visitor is grabbed, they are moved via `transform.position` (direct transform manipulation). However, the Rigidbody's internal position tracking may not be updated, especially with interpolation enabled. When the visitor is "released" and physics takes over again, the Rigidbody snaps back to where it thinks the object should be - often the original grab location.

### The Solution:
Before releasing control back to physics, sync the Rigidbody position:

```csharp
public virtual void ClearGrabbedState()
{
    if (state != VisitorState.Grabbed) return;

    // CRITICAL: Sync the Rigidbody position with the transform position.
    // When grabbed, the visitor is moved via transform.position, but the Rigidbody's
    // internal position tracking may not be updated (especially with interpolation).
    if (rb3D != null)
    {
        rb3D.position = transform.position;  // SYNC FIRST!
        rb3D.linearVelocity = Vector3.zero;
        rb3D.angularVelocity = Vector3.zero;
    }

    state = VisitorState.Idle;
}
```

### VIOLATION - DO NOT DO THIS:
```csharp
// WRONG - Only clearing velocity, not syncing position
if (rb3D != null)
{
    rb3D.linearVelocity = Vector3.zero;  // Position still desynced!
    rb3D.angularVelocity = Vector3.zero;
}
// Visitor will teleport back to original grab location!
```

### When This Matters:
- Releasing visitors from HeartwardGrasp tongue
- Releasing visitors from HeartOfTheMaze tongue
- Any system that moves objects via `transform.position` while they have a Rigidbody

---

## CRITICAL RULE 7: Heart of the Maze - Frog Tongue Behavior

**The Heart of the Maze uses a two-part model: static base ring and frog-tongue that grabs visitors.**

### Two-part model architecture:
1. **heartbase** - Static ring/base, no animations
2. **heart tongue** - Procedurally animated tongue with "frog tongue" grab behavior

### Frog Tongue Behavior (simplified approach):
The tongue uses a simple three-phase sequence:
1. **EMERGING**: Tongue rises from underground (Z=28), tip emerges first
2. **EXTENDING**: Tip bends 90° at ground level and extends horizontally toward visitor, tracking their position
3. **RETRACTING**: When visitor touches any bone collider, tongue descends back underground, pulling visitor

### Key files and assets:
| Asset | Path | Purpose |
|-------|------|---------|
| Base prefab | `Assets/Prefabs/Tile/heartbase.prefab` | Static ring model |
| Tongue prefab | `Assets/Prefabs/Tile/heart tongue.prefab` | Tongue with baked SolidCollider_N objects |
| Base GLB | `Assets/Animations/heart/heartbase.glb` | Source model for ring |
| Tongue GLB | `Assets/Animations/heart/heart tongue.glb` | Source model with 540-bone armature |

### HeartOfTheMaze State Machine:

| State | Description |
|-------|-------------|
| Idle | Only heartbase visible, monitoring for visitors in detection radius |
| Reaching | Tongue emerging and extending toward visitor |
| Grabbing | Visitor grabbed, tongue retracting with visitor attached |

### Tongue Phases (within Reaching state):

| Phase | Description |
|-------|-------------|
| Emerging | Tongue rises from Z=28, tip not yet at ground level (Z=0) |
| Extending | Tip above ground, bones bend 90° to point horizontally at visitor |

### State transitions:
1. **Idle → Reaching**: Visitor enters detection radius (default 2.5 units)
2. **Reaching → Grabbing**: Visitor's OnTriggerEnter fires for `BoneCollider_N` and calls `NotifyVisitorTouchedTongue()`
3. **Grabbing → Idle**: Tongue fully retracted to Z=28, visitor consumed

### Collision Detection:
Visitor trigger contact with tongue triggers the grab. The visitor's `OnTriggerEnter` calls `HeartOfTheMaze.NotifyVisitorTouchedTongue()`:
```csharp
// In VisitorControllerBase.OnTriggerEnter
if (other.gameObject.name.StartsWith("BoneCollider_") || other.gameObject.name == "TipTrigger")
{
    var heart = FindFirstObjectByType<HeartOfTheMaze>();
    if (heart != null) heart.NotifyVisitorTouchedTongue(this);
}
```

**NOTE**: OnCollisionEnter with SolidCollider_N is unreliable. Always use OnTriggerEnter with BoneCollider_N for detection.

### Bone rotation logic:
```csharp
// Find which bone is at ground level (Z=0)
// Bones BELOW ground: stay at rest pose (pointing up, -Z direction)
// Bones AT bend zone: interpolate from vertical to horizontal over BEND_BONE_COUNT bones
// Bones ABOVE bend zone: point horizontally toward visitor
```

### Asset loading pattern:
```csharp
#if UNITY_EDITOR
heartBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heartbase.prefab");
heartTonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heart tongue.prefab");
#endif
```

### Key constants (HeartOfTheMaze.cs):
| Constant | Value | Purpose |
|----------|-------|---------|
| detectionRadius | 2.5 | Radius to detect visitors and trigger reaching state |
| TONGUE_HIDDEN_Z | 1000 | Z position when pooled (far underground) |
| TONGUE_START_Z | 28.0 | Z position to start emerging (must be > tongue length ~27) |
| TONGUE_GROUND_Z | 0.0 | Ground level where tip emerges |
| TONGUE_EMERGE_SPEED | 9.0 | Units per second for vertical movement |
| TONGUE_RETRACT_SPEED | 9.0 | Units per second when retracting with visitor |
| BEND_BONE_COUNT | 5 | Number of bones for the 90° bend at ground level |

### Bone hierarchy:
The tongue has 540 bones (indices 0-539), named Bone_000 through Bone_539:
- Bone_000: Base of tongue (root)
- Bone_539: Tip of tongue
- Bones extend in local +Y direction (forward)
- Prefab has baked `SolidCollider_N` objects for physics collision

### Pooling:
Tongue instance is pre-created at startup and repositioned rather than instantiated/destroyed:
- **Idle**: Tongue at Z=1000 (hidden far underground)
- **Active**: Tongue repositioned to Z=28, then rises by decreasing Z

---

## TODO

### Completed - Heart Tongue Visitor Consumption

**Status**: Fully implemented and working with simplified "frog tongue" behavior.

**Model details:**
- 540 bones named Bone_000 through Bone_539
- Base at origin, tip extends along local +Y
- Tongue length ~27 world units
- 54 SolidCollider_N + 54 BoneCollider_N objects baked into prefab

**Three-phase frog tongue sequence:**
1. **Emerging**: Tongue rises from Z=28 (underground), tip emerges at Z=0 (ground level)
2. **Extending**: Bones above ground bend 90° and extend horizontally toward visitor, tracking their position
3. **Retracting**: When visitor touches tongue, tongue descends back to Z=28, visitor follows tip

**Trigger-based grab (OnTriggerEnter with BoneCollider_N):**
- Visitor's `OnTriggerEnter` detects contact with `BoneCollider_N` trigger colliders
- Calls `HeartOfTheMaze.NotifyVisitorTouchedTongue()` to trigger grab
- OnCollisionEnter with SolidCollider_N is unreliable - do NOT use for detection

**Key implementation details:**
- File: `HeartOfTheMaze.cs`
- State machine: `HeartState` (Idle, Reaching, Grabbing)
- Tongue phase: `TonguePhase` (Emerging, Extending, Retracting)
- Bone direction: `Vector3.up` (local +Y points toward next bone)
- Tongue tracks visitor position continuously during Extending phase

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| TONGUE_HIDDEN_Z | 1000 | Z position when pooled |
| TONGUE_START_Z | 28.0 | Starting Z position (underground) |
| TONGUE_GROUND_Z | 0.0 | Ground level (tip emergence point) |
| TONGUE_EMERGE_SPEED | 9.0 | Units per second for vertical movement |
| TONGUE_RETRACT_SPEED | 9.0 | Units per second when retracting |
| BEND_BONE_COUNT | 5 | Bones for the 90° bend at ground level |
| detectionRadius | 2.5 | Visitor detection radius |

**Phase transitions:**
1. **Emerging → Extending**: Tip reaches ground level (Z=0)
2. **Extending → Retracting**: Visitor collision detected via `NotifyVisitorTouchedTongue()`
3. **Retracting → Idle**: Tongue fully retracted (Z >= 28), visitor consumed

---

### Completed - HeartwardGrasp (Heart Power 2)

**Status**: Fully implemented with tongue-based grabbing. Uses the same tongue prefab and frog-tongue behavior as HeartOfTheMaze.

**What works:**
- Grabbing HGZ: Idle → Emerging → Extending → Retracting → Transporting ✓
- Pushing HGZ: Emerging → Extending → Releasing → Retracting ✓
- Tongue emerges vertically from ground at wall tile position (like HeartOfTheMaze) ✓
- Bones above ground bend 90° to extend horizontally toward visitor ✓
- Trigger-based grab: visitor OnTriggerEnter with BoneCollider_N triggers grab ✓
- Tongue retracts with visitor attached ✓
- Visitor released at ground level (Z=-0.01) over walkable area ✓
- Visitor position synced with Rigidbody on release (prevents teleportation) ✓

**Grabbing HGZ State Machine (mirrors HeartOfTheMaze frog-tongue):**
| Phase | Description |
|-------|-------------|
| Idle | Waiting for visitors to enter detection zone |
| Emerging | Tongue rises from Z=TONGUE_START_Z until tip reaches ground (Z=0) |
| Extending | Bones bend 90° at ground level, extend horizontally toward visitor |
| Retracting | Visitor grabbed on collision, tongue descends with visitor attached |

**Pushing HGZ State Machine (reverse of grab):**
| Phase | Description |
|-------|-------------|
| Idle | Waiting for transported visitor |
| Emerging | Tongue rises from underground with visitor at tip (hidden) |
| Extending | Tongue extends horizontally, visitor becomes visible at ground level |
| Releasing | Visitor released over walkable area, tongue pauses briefly |
| Retracting | Tongue descends back underground |

**Key implementation details:**
- File: `HeartPowerEffects.cs` - `HeartwardGraspEffect` class
- Uses same tongue prefab as HeartOfTheMaze: `Assets/Prefabs/Tile/heart tongue.prefab`
- SolidCollider_N objects baked into prefab for collision detection
- Collision-based grab via `NotifyVisitorTouchedGraspTongue()` (like HeartOfTheMaze)
- Visitor released at ground level Z=-0.01 with Rigidbody position synced
- `ClearGrabbedState()` syncs `rb3D.position = transform.position` to prevent teleportation

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| GRASP_ZONE_RADIUS | 2.5 | Trigger radius for visitor detection |
| TONGUE_START_Z | 28.0 | Starting Z position (deep underground) |
| TONGUE_GROUND_Z | 0.0 | Ground level where tip emerges |
| TONGUE_EMERGE_SPEED | 6.0 | Units per second for vertical movement |
| TONGUE_RETRACT_SPEED | 4.0 | Speed when retracting |
| BEND_BONE_COUNT | 5 | Bones for the 90° bend at ground level |
| HGZ_WALL_OFFSET | 2.4 | Offset into forest (3 wall layers deep) |
| GRAB_ESSENCE_COST | 25 | Essence deducted from visitor when grabbed |

**Visitor release process:**
1. Set visitor Z to ground level (Z=-0.01)
2. Make visitor visible
3. Call `ClearGrabbedState()` which syncs Rigidbody position and clears velocity
4. Call `Resume()` and `RecalculatePath()`
5. Apply daze effect via `OnWitnessMazeGrowth()`

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

### Completed - Difficulty Progression System

**Status**: Fully implemented with essence-threshold tiers and asymptotic spawn intervals.

**Core Architecture**:
- `DifficultyManager.cs` - Monitors essence, calculates tier, fires `OnTierChanged` events
- `DifficultyScaling.cs` - Static class providing asymptotic scaling curves

**Game Flow** (IMPORTANT - No Waves):
- Game is ONE CONTINUOUS spawn session until essence depletes
- NO discrete waves with completion events
- Spawn interval grows asymptotically (approaches max of 15s, never exceeds)
- Difficulty tier increases at essence milestones

**Essence-Threshold Tiers** (relative to starting essence, default 100):

| Tier | Threshold | With 100 Start | Notes |
|------|-----------|----------------|-------|
| 1 | Start | 0+ | Baseline |
| 2 | 1.5x | 150 | First milestone |
| 3 | 2.0x | 200 | RedCap spawns |
| 4 | 3.0x | 300 | |
| 5 | 4.0x | 400 | |
| 6 | 6.0x | 600 | |
| 7 | 8.0x | 800 | Maximum |

**Hysteresis**: Tier increases immediately when threshold crossed, but only decreases if essence drops below PREVIOUS tier threshold (prevents oscillation).

**Scaled Parameters Per Tier**:

| Parameter | Tier 1 | Tier 4 | Tier 7 (Max) |
|-----------|--------|--------|--------------|
| Visitor Speed | 1.0x | 1.25x | 1.5x |
| RedCap Speed | 1.0x | 1.3x | 1.6x |
| RedCap Penalty | 1.0x | 1.75x | 2.5x |
| Confusion Chance | 1.0x | 1.3x | 1.75x |
| Essence Rewards | 1.0x | 1.25x | 1.5x |

**Asymptotic Spawn Interval**:
- Base: 5.0s (starting rate)
- Max: 15.0s (never exceeds)
- Formula: `base + (max - base) * (1 - e^(-0.02 * spawnCount))`
- Progression: 5.0s → 6.3s (10 spawns) → 9.8s (50 spawns) → 14.1s (200 spawns)

**Implementation Points**:
- `DifficultyManager.OnEssenceChanged()` - recalculates tier with hysteresis
- `WaveSpawner.SpawnWaveCoroutine()` - uses asymptotic spawn interval
- `WaveSpawner.SpawnVisitor()` - calls `SetDifficultyTier()` on spawned visitors
- `VisitorControllerBase.SetDifficultyTier()` - applies speed scaling
- `VisitorControllerBase.GetConfusionChance()` - applies confusion scaling
- `VisitorControllerBase.GetEssenceReward()` - applies reward scaling
- `RedCapController.SetDifficultyTier()` - applies speed scaling
- `RedCapController.GetScaledEssencePenalty()` - applies penalty scaling

**Key Files**:
| File | Purpose |
|------|---------|
| `DifficultyManager.cs` | Tier management, essence monitoring, hysteresis |
| `DifficultyScaling.cs` | Asymptotic spawn interval, tier-based multipliers |
| `WaveSpawner.cs` | Applies spawn interval, gets tier from manager |
| `VisitorControllerBase.cs` | Visitor speed/confusion/reward scaling |
| `RedCapController.cs` | RedCap speed/penalty scaling |

**Scaling Formula**:
- Growth: `1 + (maxBonus) * (1 - e^(-rate * (tier-1)))`
- Spawn interval: `base + (max - base) * (1 - e^(-rate * spawnCount))`

---

## Building the Game

### Multi-Platform Build System

The project includes a build script for creating Windows and macOS builds.

**Build from Unity Editor menu:**
| Menu Item | Description |
|-----------|-------------|
| `FaeMaze > Build > Windows (64-bit)` | Build Windows executable |
| `FaeMaze > Build > macOS` | Build macOS application bundle |
| `FaeMaze > Build > Both Platforms` | Build both platforms sequentially |
| `FaeMaze > Build > Open Build Folder` | Open the Builds directory in file explorer |
| `FaeMaze > Fix Graphics APIs` | Configure graphics APIs for both platforms |
| `FaeMaze > Show Current Graphics APIs` | Display current graphics API configuration |

**Build output locations:**
| Platform | Output Path |
|----------|-------------|
| Windows | `Builds/Windows/HungryForest.exe` |
| macOS | `Builds/macOS/HungryForest.app` |

**Command-line builds (for CI/CD):**
```bash
# Windows build
Unity -batchmode -projectPath . -executeMethod FaeMaze.Editor.BuildScript.BuildWindowsCommandLine -quit

# macOS build
Unity -batchmode -projectPath . -executeMethod FaeMaze.Editor.BuildScript.BuildMacOSCommandLine -quit

# Both platforms
Unity -batchmode -projectPath . -executeMethod FaeMaze.Editor.BuildScript.BuildAllCommandLine -quit
```

**Build scenes (from EditorBuildSettings):**
1. MainMenu
2. PlanarForestMazeScene (main game)
3. Options
4. GameOver

**Platform requirements:**
| Platform | Minimum OS | Architecture |
|----------|------------|--------------|
| Windows | Windows 10 | 64-bit |
| macOS | macOS 12.0 (Monterey) | Universal (Intel + Apple Silicon) |

**Graphics API Configuration:**

The build system requires specific graphics APIs to be configured for each platform. If builds fail with graphics-related errors, run `FaeMaze > Fix Graphics APIs` to configure them correctly.

| Platform | Graphics APIs | Notes |
|----------|---------------|-------|
| Windows | Direct3D 11, Direct3D 12, Vulkan | D3D11 primary for compatibility |
| macOS | Metal | Required for Apple Silicon support |

**macOS Build Module:**
To build for macOS from Windows, install the Mac Build Support module via Unity Hub:
1. Open Unity Hub → Installs
2. Click gear icon on your Unity version → Add modules
3. Select "Mac Build Support (Mono)"
4. Install and restart Unity

**Key files:**
| File | Purpose |
|------|---------|
| `Assets/Editor/BuildScript.cs` | Build automation script |
| `Assets/Editor/FixGraphicsAPIs.cs` | Graphics API configuration utility |
| `ProjectSettings/ProjectSettings.asset` | Platform settings, bundle identifiers |
| `ProjectSettings/EditorBuildSettings.asset` | Scene list for builds |

**Troubleshooting builds:**
| Error | Solution |
|-------|----------|
| "Build target was unsupported" | Install the platform's build module via Unity Hub |
| "Apple silicon support requires Metal" | Run `FaeMaze > Fix Graphics APIs` |
| "Graphics APIs do not include Direct3D 11 or 12" | Run `FaeMaze > Fix Graphics APIs` |
| Shader compilation crash | Ensure all shader fallbacks use URP-compatible shaders (not legacy) |

**Bundle identifier:** `com.gamestrubios.hungryforest`

---

### In Progress
- [ ] Ensure other visitor types work as intended with heart powers

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
- [x] Enable game over state - fully implemented (see Game Over Statistics section)
- [x] Implement difficulty progression - wave-based scaling system (see Difficulty Progression section)

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
- `VisitorControllerBase.cs` - CurrentFaeLantern/CurrentFairyRing properties, EndLanternFascination/EndRingFascination methods, null lantern check in Update, GetCurrentPath() method
- `FaeLantern.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `FairyRing.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `LanternGlow.cs` - Edit mode material leak fix
- `HeartOfTheMaze.cs` - CalculateVisitorExitPoint() for tongue aiming
- `TonguePrefabColliderSetup.cs` - Bakes solid colliders into prefab for physics-based blocking

6. **Focal Point Indicator Replacement** (FocalPointGlow.cs):
   - Replaced pink cylinder with a conic section surface following z = -1/(10*r^1.5)
   - Energy bolts spiral along the cone surface with jagged lightning appearance
   - Colors alternate between dark red and purple (previously blue/purple)
   - Dynamic fog occlusion: when over walkable area, extends to ground (z=0); when over fog, stops at fog level (z=-1)
   - Points above fog cutoff collapse to previous valid point (bolt disappears into fog, no pooling)
   - Bolts and branches regenerate jitter every 0.08s for flickering effect

7. **Heart Tongue Simplified to Frog Behavior** (HeartOfTheMaze.cs):
   - Removed complex spiral/curl approach that relied on physics blocking
   - Simplified to: emerge → extend toward visitor → grab on contact → retract
   - Tongue tracks visitor position continuously during Extending phase
   - Collision detection triggers grab (no physics blocking needed)

8. **Tongue Collision Detection** (VisitorControllerBase.cs):
   - `OnCollisionEnter` detects `SolidCollider_N` contact
   - Calls `HeartOfTheMaze.NotifyVisitorTouchedTongue(this)` to trigger grab
   - `tongueCollisionCount` tracks active collisions for reference

9. **Tongue Colliders Baked in Prefab** (TonguePrefabColliderSetup.cs):
   - `SolidCollider_N` objects baked into prefab by editor script
   - No runtime collider creation/destruction (prevents physics freezes)

10. **FindForestDirection Rewrite** (HeartPowerEffects.cs):
    - Fixed HGZ placement to emerge from forest side, not path side
    - Algorithm: find direction with greatest minimum distance from ALL nearby walkable tiles

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

### Completed - Heart Tongue Frog Behavior (Session Jan 28, 2026)

**Status**: Implemented and working with simplified "frog tongue" approach.

**Solution: Collision-Based Grab**
- Abandoned complex spiral/curl approach that relied on physics blocking
- New approach: tongue emerges, extends toward visitor, grabs on contact, retracts
- Visitor's `OnCollisionEnter` detects contact with `SolidCollider_N` and notifies HeartOfTheMaze
- Tongue tracks visitor position continuously during Extending phase
- On contact, visitor becomes grabbed and follows tongue tip back underground

**Working tongue sequence:**
1. Visitor enters detection radius → tongue starts emerging from Z=28
2. Tip reaches ground level (Z=0) → transitions to Extending phase
3. Bones above ground bend 90° and track visitor position
4. Visitor collides with tongue → `NotifyVisitorTouchedTongue()` triggers Grabbing state
5. Tongue retracts (Z increases), visitor follows tip
6. Tongue reaches Z=28 → visitor consumed, tongue returns to pool

**Key implementation:**
- `HeartOfTheMaze.NotifyVisitorTouchedTongue(visitor)` - public method called by visitor collision
- `VisitorControllerBase.OnCollisionEnter()` - detects `SolidCollider_N` and notifies heart
- Tongue continuously updates target angle to track visitor during Extending

---

### Completed - Key Binding Capture System Redesign (Session Jan 30, 2026)

**Status**: Fully implemented. Key binding capture now uses Toggle-based activation to avoid left-click conflicts.

**Problem**: The original system used a Button click to activate capture mode. This created an inherent conflict when trying to bind left-click - clicking the button to start capture would also register as the binding input.

**Solution**: Separated "select this binding" from "capture input" by using a Toggle (checkbox):
1. User checks the toggle → enters capture mode ("Press any key...")
2. User presses any input (including left-click) → binding captured
3. Toggle automatically unchecks

**Key files modified:**
| File | Changes |
|------|---------|
| `KeyBindingCapture.cs` | Redesigned to use Toggle instead of Button for capture activation |
| `OptionsManager.cs` | Added `SyncScreenshotBindings()` to keep VIDEO and CONTROLS tab bindings in sync |
| `OptionsBindingToggleSetup.cs` | Editor script to add Toggles to all KeyBindingCapture components |
| `OptionsScreenshotSetup.cs` | Editor script to set up screenshot capture controls |

**KeyBindingCapture component structure:**
```
KeyBindingCapture (GameObject)
├── CaptureToggle (Toggle) - activates capture mode
│   └── Checkmark (Image) - yellow-orange when checked
└── BindingText (TextMeshProUGUI) - displays current binding or "Press any key..."
```

**Editor scripts to run (in order):**
1. `FaeMaze > Add KeyBindingCapture to Screenshot Button` - if screenshot button lacks component
2. `FaeMaze > Setup Binding Capture Toggles` - adds toggles to all KeyBindingCapture components
3. Save the Options scene

**Sync behavior:**
- Screenshot binding appears on both VIDEO tab (in SCREENSHOT section) and CONTROLS tab (in UTILITY section)
- When either is changed, `SyncScreenshotBindings()` updates all screenshot KeyBindingCapture components
- Both tabs always show the same value

---

### Completed - Light Level Setting Application (Session Jan 30, 2026)

**Status**: Fixed. Light Level setting now applies when game scene loads.

**Problem**: The Light Level slider in Options worked (value was saved), but the Directional Light intensity wasn't updated when starting a new game. The setting was only applied in the Options scene preview.

**Solution**: Added `GameSettings.ApplySettings()` call to `GameController.Start()`:
```csharp
private void Start()
{
    ValidateReferences();

    // Apply saved settings (light level, video settings, etc.)
    GameSettings.ApplySettings();

    // Invoke event for initial essence value
    OnEssenceChanged?.Invoke(currentEssence);
}
```

**Call chain:**
1. `GameController.Start()` calls `GameSettings.ApplySettings()`
2. `ApplySettings()` calls `ApplyVideoSettings()`
3. `ApplyVideoSettings()` calls `ApplyLightLevel()`
4. `ApplyLightLevel()` finds the Directional Light and sets `light.intensity = LightLevel`

**Default value**: `GameSettings.LightLevel` defaults to 0.9f (stored in PlayerPrefs)

---

### Completed - Void Fog Z Position Fix (Session Jan 30, 2026)

**Status**: Fixed. Void fog now renders at Z=-1.2 instead of Z=-1.0.

**File**: `VoidFogGenerator.cs`

**Change**:
```csharp
// Before
private float zPosition = -1f;

// After
private float zPosition = -1.2f;
```

**Reason**: The fog needs to render slightly above the wall tops to properly occlude the forest without clipping through path geometry.

---

### Key Binding Default Values

**Current default bindings (from GameSettings.cs):**

| Control | Default | Category |
|---------|---------|----------|
| Heart Power 1 (Murmuring) | 1 | Heart Powers |
| Heart Power 2 (Grasp) | 2 | Heart Powers |
| Heart Power 3 (Devour) | 3 | Heart Powers |
| Heart Power 4 (Sculpt) | 4 | Heart Powers |
| Sculpt Pond | Z | Sculpt Menu |
| Sculpt Lantern | X | Sculpt Menu |
| Sculpt Ring | C | Sculpt Menu |
| Sculpt Remove | V | Sculpt Menu |
| Camera Forward (Mouse) | Mouse0 (Left Click) | Camera Mouse |
| Camera Orbit | Mouse1 (Right Click) | Camera Mouse |
| Camera Pan | Mouse2 (Middle Click) | Camera Mouse |
| Camera Focus Heart | F5 | Camera Focus |
| Camera Focus Entrance | F6 | Camera Focus |
| Camera Focus Visitor | F7 | Camera Focus |
| Screenshot | F12 | Utility |

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
2. **Reaching → Grabbing**: Grab collider touches visitor and curl progress >= 80%
3. **Grabbing → Idle**: Tongue fully retracted below ground, visitor consumed

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

### In Progress - Heart Tongue Visitor Consumption

**Current State**: Full grab/consume sequence is working. Tongue emerges, reaches toward visitor, curls around them, pulls them back horizontally, then rotates the curl section 90° while sinking to consume the visitor.

**Model details:**
- 540 bones named Bone_000 through Bone_539
- Base at origin, tip extends along +X
- Bones use local +X as forward direction (boneLocalDir = Vector3.up after rest rotation)
- All bones have localPosition (0,0,0)
- Tongue length ~8.4 world units (scaled)
- Colliders positioned using bone world positions directly

**What works:**
- Tongue spawns at z=9 (below ground) ✓
- Tongue emerges straight during Emerging phase ✓
- Bones stay at rest pose until Reaching phase begins ✓
- When tip reaches TONGUE_LIP_Z (-0.25), transitions to Reaching phase ✓
- During Reaching phase, bones above lipBoneIndex rotate toward visitor ✓
- Tongue prefab's lights are removed at spawn ✓
- Visitor dazed when tongue emerges ✓
- **Initial curl works** - 180° horizontal curl in XY plane (parallel to ground) ✓
- Curl direction determined by visitor angle (CCW if upper half, CW if lower) ✓
- Collider positions update using bone.position (tracks deformed mesh) ✓
- Grab hold timer delays pulling after grab contact ✓
- **Pulling phase** - Horizontal retraction by reversing reaching motion ✓
- **Sinking phase** - Pivot bone rotates, curl follows via locked local rotations ✓
- **Visitor tracking** - Position at midpoint of grab/reach, rotation via delta ✓

**Five-phase tongue sequence:**
1. **Emerging**: Tongue translates up from z=9 until tip reaches lip (z=-0.25)
2. **Reaching**: Bones above lip rotate toward visitor in XY plane
3. **Touching**: Tip contacts visitor, curl progresses 180° around them
4. **Pulling**: Tongue retracts horizontally (lip bone recalculated dynamically as tongue sinks)
5. **Sinking**: When grab reaches lip, pivot bone rotates 90°, curl follows via locked rotations

**Pivot bone pattern (Sinking phase):**
The curl section maintains its shape during the 90° rotation from horizontal to vertical:
- **Pivot bone** (grabBoneIndex - 1): Rotates from horizontal to vertical via `desiredDir`
- **Curl bones** (grabBoneIndex and beyond): Use locked local rotations, cascading from pivot
- This causes the entire curl section (with visitor) to rotate as a unit

**Visitor position and rotation tracking:**
```csharp
// Position: midpoint between grab and reach colliders
Vector3 midpoint = (grabPos + reachPos) * 0.5f;
targetVisitor.transform.position = midpoint;

// Rotation: delta from grab bone applied incrementally
Quaternion rotationDelta = currentGrabBoneRotation * Quaternion.Inverse(previousGrabBoneRotation);
targetVisitor.transform.rotation = rotationDelta * targetVisitor.transform.rotation;
```

**Key implementation details:**
- File: `HeartOfTheMaze.cs`
- State machine: `HeartState` (Idle, Reaching, Grabbing)
- Tongue phase: `TonguePhase` (Emerging, Reaching, Touching, Pulling, Sinking)
- Bones from SkinnedMeshRenderer: 540 bones
- Bone direction: `Vector3.up` (local +Y points toward next bone after rest rotation)
- Curl is HORIZONTAL (parallel to ground, in XY plane, rotates around Z axis)

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| TONGUE_START_Z | 9.0 | Starting Z position (below ground) |
| TONGUE_LIP_Z | -0.25 | Z where tip emerges (triggers Reaching phase) |
| TONGUE_EMERGE_SPEED | 1.5 | Units per second for vertical movement |
| TONGUE_CURL_SPEED | 2.0 | Rate of curl progress (0→1) |
| GRAB_BONE_OFFSET | 50 | Bones from tip for grab collider (~9% from tip) |
| CURL_DIAMETER | 0.5 | Target diameter of curl around visitor |
| GRAB_HOLD_DURATION | 0.5 | Seconds to hold after grab before pulling |
| detectionRadius | 2.5 | Visitor detection radius |

**Curl state tracking:**
```csharp
private int curlDirection = 1;           // +1 = CCW (left), -1 = CW (right)
private bool grabContactMade = false;     // True when grab collider touches visitor
private float reverseCurlProgress = 0f;   // Progress of reverse curl (0-1)
private float grabCurlProgress = 0f;      // Progress of initial curl (0-1)
private Quaternion previousGrabBoneRotation;  // For rotation delta calculation
private bool hasPreviousGrabBoneRotation = false;
```

**Locked curl rotations:**
```csharp
// LockCurlBoneRotations includes pivot bone (grabBoneIndex - 1) plus all curl bones
int pivotBoneIndex = grabBoneIndex - 1;
lockedCurlRotations = new Quaternion[boneCount - pivotBoneIndex];

// During Sinking, pivot bone rotates via desiredDir, others use locked local rotations
if (tonguePhase == TonguePhase.Sinking && i == pivotBoneIndex)
{
    float t = sinkingRotationProgress;
    desiredDir = Vector3.Slerp(targetDirWorld, downDir, t);
}
else if (curlRotationsLocked && curlIndex >= 0)
{
    tongueBones[i].localRotation = lockedCurlRotations[curlIndex];
    continue;
}
```

**Collider positions:**
- Reach collider: `tongueBones[boneCount - 1].position` (tip bone world pos)
- Grab collider: `tongueBones[boneCount - 1 - GRAB_BONE_OFFSET].position`

**Phase transitions:**
1. **Emerging → Reaching**: Tip reaches TONGUE_LIP_Z
2. **Reaching → Touching**: Reach collider touches visitor (via trigger callback)
3. **Touching → Pulling**: Grab collider touches visitor, grab hold timer completes
4. **Pulling → Sinking**: Grab bone reaches lip level (freeze lip bone index)
5. **Sinking → Idle**: Tongue fully retracted (`tongueZPosition >= TONGUE_START_Z`), visitor consumed

**Remaining polish:**
- [ ] Fine-tune timing and speeds for satisfying feel
- [ ] Add visual/audio feedback for consumption

---

### Completed - HeartwardGrasp (Heart Power 2)

**Status**: Fully implemented and working. Core grab/transport/push sequence complete. Animation plays smoothly through all phases. Push phase dynamically extends until visitor is on valid walkable area.

**What works:**
- Grab sequence: Idle → Reaching → Grabbing → Pulling → Transporting ✓
- Push sequence: Pushing → Releasing → Withdrawing ✓
- Animation plays smoothly (frames 0-24, reverse 24-0) ✓
- Hand stays closed during pull phase (fixed normalizedTime clamping to 0.999) ✓
- Push continues until visitor is on valid walkable area (no walls, on path/node tile) ✓
- Visitor positioned in front of pushing hand along push axis ✓

**Key implementation details:**
- File: `HeartPowerEffects.cs` - `HeartwardGraspEffect` class (line ~1580)
- Two state machines: `GrabPhase` and `PushPhase` enums
- Animation controlled via `SetAnimatorFrame(animator, frameNumber)` using normalized time (clamped to 0.999 max)
- Push phase uses continuous movement at `PUSH_SPEED` until `IsVisitorOnValidWalkableArea()` returns true
- `IsVisitorOnValidWalkableArea()` checks for: on path/node tile AND not touching wall tiles

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| GRASP_ZONE_RADIUS | 2.5 | Trigger radius for visitor detection |
| MIN_PUSH_DISTANCE | 1.0 | Minimum push before checking for valid area |
| MAX_PUSH_DISTANCE | 10.0 | Safety limit for push distance |
| PUSH_SPEED | 2.0 | Units per second during push |
| VISITOR_CHECK_RADIUS | 0.3 | Collision check radius for walkable area |
| GRAB_ESSENCE_COST | 25 | Essence deducted from visitor when grabbed |

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

### Other In Progress
- [ ] Ensure other visitor types work as intended with heart powers

### Heart & Powers
- [x] Fix heart prefab - separated into two parts (heartbase + heart tongue) with state machine
- [x] Sculpting power (Heart Power 4) - radial menu to change node props
- [ ] Make icons for heart power buttons
- [x] Finalize heart power essence use costs - see Heart Power Essence Costs section below
- [ ] Push magic numbers and constants to configurable settings

### UI & Scenes
- [ ] Synchronize, consolidate, and rationalize options scene (IN PROGRESS - see Options Scene Restructure section below)
- [ ] Clean up game over scene
- [ ] Improve player UI layout
- [x] Replace the focus point indicator - now uses conic section with spiraling energy bolts

### Game State
- [ ] Enable game over state
- [ ] Implement difficulty progression

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
- `VisitorControllerBase.cs` - CurrentFaeLantern/CurrentFairyRing properties, EndLanternFascination/EndRingFascination methods, null lantern check in Update
- `FaeLantern.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `FairyRing.cs` - OnDisable with ReleaseAllFascinatedVisitors
- `LanternGlow.cs` - Edit mode material leak fix

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

# Claude Code Project Guidelines - FaeMaze

This file contains critical architectural rules that MUST be followed in every session.
These rules exist because violations have caused repeated bugs that took hours to fix.

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

## TODO

### In Progress - HeartwardGrasp (Heart Power 2)

**Current State**: Core grab/transport/push sequence is implemented and partially tested. Grab sequence (Idle→Reaching→Grabbing→Pulling→Transporting) works. Push sequence starts but needs full testing. Recent fixes address HGZ positioning depth and visitor orientation during teleport.

**What was done this session:**
1. Restructured HeartwardGrasp to use TWO HGZs (GrabbingHGZ and PushingHGZ)
2. Implemented frame-based animation control via Animator normalized time
3. Fixed wall detection using `Physics.RaycastAll` with `QueryTriggerInteraction.Collide`
4. Walls are filtered by name (`WorldTile_#`) since they use trigger colliders on default layer
5. First hit = PushingHGZ (near heart), last hit before focal = GrabbingHGZ (near focal point)
6. Fixed hand rotation: model's X axis is forward, Z points to world -Z. Uses `Quaternion.Euler(0f, -angle, 0f)`
7. Increased WALL_OFFSET from 0.5 to 1.5 for better HGZ placement depth into walls
8. Fixed pushing hand to orient TOWARD heart (was incorrectly pointing away)
9. Added `visitorPushOffset` - transforms grab offset to match pushing hand's different orientation
10. Teleportation now rotates the visitor offset based on angle difference between grabbing/pushing directions

**Recent issues addressed:**
- HGZs were not far enough into walls → increased WALL_OFFSET to 1.5
- Pushing hand was oriented away from heart instead of toward it → fixed direction
- Visitor teleported to wrong position because offset wasn't transformed for pushing hand's rotation → added angle-based offset transformation

**Next steps to complete HeartwardGrasp:**
- [ ] Test that HGZs are now properly positioned 1.5 units into walls
- [ ] Verify pushing hand now points toward heart
- [ ] Test full push sequence: Reaching (reverse 62→46) → Releasing (46→20, daze visitor) → Withdrawing (20→0)
- [ ] Verify visitor position is correct after teleport (using transformed offset)
- [ ] Verify daze effect applied when visitor released
- [ ] Verify power expires after tier-count captures

**Key implementation details:**
- File: `HeartPowerEffects.cs` - `HeartwardGraspEffect` class (line ~1580)
- Two state machines: `GrabPhase` and `PushPhase` enums
- Animation controlled via `SetAnimatorFrame(animator, frameNumber)` using normalized time
- Wall raycast filters by `gameObject.name.StartsWith("WorldTile_#")`
- Hand rotation: `Quaternion.Euler(0f, -angle, 0f)` where angle = `Atan2(dir.y, dir.x) * Rad2Deg`
- Grabbing hand points AWAY from heart (toward focal)
- Pushing hand points TOWARD heart
- Visitor offset transformed during teleport: rotates by angle difference between grabbing/pushing directions

**Key constants:**
| Constant | Value | Description |
|----------|-------|-------------|
| WALL_OFFSET | 1.5 | How far into wall to position HGZ |
| GRASP_ZONE_RADIUS | 1.0 | Trigger radius for visitor detection |
| GRAB_REACH_END_FRAME | 20 | End of reach phase |
| GRAB_GRAB_END_FRAME | 46 | Visitor stops here |
| GRAB_PULL_END_FRAME | 62 | Animation end |
| TRANSPORT_DURATION | 1.0f | Seconds for transport |

---

### Other In Progress
- [ ] Ensure other visitor types work as intended with heart powers

### Heart & Powers
- [ ] Fix heart prefab - separate into two parts:
  - Static ring (base)
  - Tongue with animations: idle, reach, grab, retract
- [ ] Make icons for heart power buttons
- [ ] Finalize heart power essence use costs
- [ ] Push magic numbers and constants to configurable settings

### UI & Scenes
- [ ] Synchronize, consolidate, and rationalize options scene
- [ ] Clean up game over scene
- [ ] Improve player UI layout
- [ ] Replace the focus point indicator

### Game State
- [ ] Enable game over state
- [ ] Implement difficulty progression

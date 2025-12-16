# Visitor Archetype System

## Overview

The Visitor Archetype System allows you to create distinct visitor types with unique behavioral patterns, hazard interactions, and reward values. Three core archetypes are implemented:

1. **LanternDrunk Pilgrim** - Highly susceptible to fascination, easy to distract
2. **Wary Wayfarer** - Cautious and resistant to distraction, but flees when threatened
3. **Sleepwalking Devotee** - Begins mesmerized toward the Heart, valuable target

## Architecture

### Core Components

- **`VisitorArchetype`** (enum) - Defines the three archetype types
- **`IVisitorArchetypeConfig`** (interface) - Contract for archetype configuration
- **`VisitorArchetypeConfig`** (ScriptableObject) - Tunable parameters per archetype
- **`IArchetypedVisitor`** (interface) - Marks visitors as having an archetype
- **`TypedVisitorControllerBase`** - Base class for archetype-aware visitors

### Archetype Controllers

- **`LanternDrunkVisitorController`** - Extends `TypedVisitorControllerBase`
  - High confusion chance at intersections
  - Very susceptible to FaeLantern fascination
  - Longer detour segments when lost

- **`WaryWayfarerVisitorController`** - Extends `TypedVisitorControllerBase`
  - Low confusion/misstep chance
  - Repaths to nearest exit when frightened
  - Resistant to fascination

- **`SleepwalkingDevoteeController`** - Extends `TypedVisitorControllerBase`
  - Starts in Mesmerized state heading to Heart
  - No confusion while mesmerized
  - High essence reward when consumed
  - Reacts strongly to interference (trance can be broken)

## Creating Archetype Configs

### Step 1: Create ScriptableObject Assets

In Unity Editor:
1. Right-click in Project window
2. Select `Create > FaeMaze > Visitor Archetype Config`
3. Name the asset (e.g., "LanternDrunkConfig")
4. Repeat for each archetype

### Step 2: Configure Parameters

#### LanternDrunk Pilgrim (Baseline)

```
Archetype: LanternDrunk
Base Speed: 0.9

Fascination:
- Fascination Chance: 0.7
- Duration Min/Max: 3 / 6 seconds
- Cooldown: 2 seconds
- Wander Detour Min/Max: 6 / 12 tiles

Confusion/Lost:
- Confusion Chance: 0.3
- Lost Detour Min/Max: 6 / 12 tiles

Frightened:
- Duration: 2 seconds
- Speed Multiplier: 1.1x
- Prefers Exit: false

Mesmerized:
- Starts Mesmerized: false
- Initial Duration: 0

Hazards:
- Fairy Ring Slow Mult: 1.2x
- Puka Teleport Chance: 0.3
- Puka Kill Chance: 0.1
- Wisp Capture Priority: 1.0

Reward:
- Essence: 1
```

#### Wary Wayfarer (Semi-Elite)

```
Archetype: WaryWayfarer
Base Speed: 1.1

Fascination:
- Fascination Chance: 0.15
- Duration Min/Max: 2 / 3 seconds
- Cooldown: 6 seconds
- Wander Detour Min/Max: 2 / 4 tiles

Confusion/Lost:
- Confusion Chance: 0.05
- Lost Detour Min/Max: 2 / 5 tiles

Frightened:
- Duration: 4 seconds
- Speed Multiplier: 1.5x
- Prefers Exit: TRUE

Mesmerized:
- Starts Mesmerized: false
- Initial Duration: 0

Hazards:
- Fairy Ring Slow Mult: 1.0x
- Puka Teleport Chance: 0.2
- Puka Kill Chance: 0.2
- Wisp Capture Priority: 0.6

Reward:
- Essence: 3
```

#### Sleepwalking Devotee (VIP)

```
Archetype: SleepwalkingDevotee
Base Speed: 1.0

Fascination:
- Fascination Chance: 0.4 (when trance broken)
- Duration Min/Max: 3 / 5 seconds
- Cooldown: 5 seconds
- Wander Detour Min/Max: 4 / 9 tiles

Confusion/Lost:
- Confusion Chance: 0.0 (while mesmerized)
- Lost Detour Min/Max: 5 / 12 tiles

Frightened:
- Duration: 3 seconds
- Speed Multiplier: 1.3x
- Prefers Exit: false (still drawn to Heart)

Mesmerized:
- Starts Mesmerized: TRUE
- Initial Duration: 10 seconds

Hazards:
- Fairy Ring Slow Mult: 1.1x
- Puka Teleport Chance: 0.5
- Puka Kill Chance: 0.05
- Wisp Capture Priority: 2.0

Reward:
- Essence: 5
```

### Step 3: Create Visitor Prefabs

1. Create empty GameObject
2. Add appropriate controller component:
   - `LanternDrunkVisitorController`
   - `WaryWayfarerVisitorController`
   - `SleepwalkingDevoteeController`
3. Assign the matching config asset to the `Config` field
4. Configure visual settings (sprite, color, size)
5. Save as prefab

## Hazard Integration

Hazards automatically respect archetype configs when available:

### FaeLantern
- Uses `FascinationChance` from visitor config
- Uses `FascinationDurationMin/Max` for pause duration
- Uses `FascinationCooldown` between triggers

### FairyRing
- Multiplies slow effect by `FairyRingSlowMultiplier`
- Devotees may refresh mesmerized or become lost on exit

### PukaHazard
- Uses `PukaTeleportChance` and `PukaKillChance`
- Devotees bias teleport toward Heart (when implemented)

### WillowTheWisp
- Weights capture priority by `WispCapturePriority`
- Devotees strongly preferred (2.0x priority)

## Usage in Wave System

### Spawning Specific Archetypes

The wave system can spawn visitors by prefab. Reference the archetype prefabs in wave configurations:

```csharp
// Example wave setup
public GameObject lanternDrunkPrefab;
public GameObject waryWayfarerPrefab;
public GameObject sleepwalkingDevoteePrefab;
```

## Creating New Archetypes

To add a new visitor archetype:

1. **Add enum value** to `VisitorArchetype`
2. **Create ScriptableObject config** with parameters
3. **Create controller class**:
   ```csharp
   public class MyNewVisitorController : TypedVisitorControllerBase
   {
       // Override methods as needed
   }
   ```
4. **Override behavior methods**:
   - `GetFascinationChance()` - Fascination susceptibility
   - `GetConfusionChance()` - Confusion/misstep rate
   - `GetFrightenedSpeedMultiplier()` - Panic speed
   - `ShouldFrightenedPreferExit()` - Flight behavior
   - `GetEssenceReward()` - Consumption value

5. **Implement unique behaviors**:
   - Override `ShouldAttemptDetour()` for path decisions
   - Override `HandleStateSpecificDetour()` for detour logic
   - Override `GetDestinationForCurrentState()` for routing
   - Override `EnterFaeInfluence()` for custom fascination

6. **Create prefab** with controller and config

## Extension Points

### Custom Fascination Behavior

Override `EnterFaeInfluence()` to customize how archetypes respond to lanterns:

```csharp
protected override void EnterFaeInfluence(FaeLantern lantern, Vector2Int pos)
{
    // Custom logic before base behavior
    base.EnterFaeInfluence(lantern, pos);
    // Custom logic after
}
```

### State-Specific Routing

Override `GetDestinationForCurrentState()` to change routing based on visitor state:

```csharp
protected override Vector2Int GetDestinationForCurrentState(Vector2Int currentPos)
{
    if (state == VisitorState.Frightened && myCustomCondition)
    {
        return FindCustomDestination();
    }
    return base.GetDestinationForCurrentState(currentPos);
}
```

### Archetype Query from Hazards

Hazards can query visitor archetypes using extension methods:

```csharp
var config = visitor.GetArchetypeConfig();
if (config != null)
{
    float chance = config.FascinationChance;
    // Apply archetype-specific behavior
}
```

## State Machine Integration

Archetypes use the existing visitor state machine:

- **Idle** - Not moving
- **Walking** - Normal pathfinding
- **Fascinated** - Drawn to FaeLantern
- **Confused** - Taking wrong turns
- **Frightened** - Fleeing (may prefer exits)
- **Mesmerized** - Tranced toward Heart
- **Lost** - Wandering aimlessly
- **Consumed** - Reached Heart
- **Escaping** - Fleeing to exit

Each archetype perceives and transitions between states differently based on config parameters.

## Debugging

### Gizmos

Enable debug gizmos in controller components:
- Confusion segments (magenta lines)
- Misstep paths (yellow lines)
- Mesmerized state (purple wireframe)

### Logging

Controllers log state transitions:
```
[SleepwalkingDevotee] Initialized in mesmerized state for 10s
[LanternDrunk] Confusion triggered at intersection
[WaryWayfarer] Frightened -> fleeing to exit
```

## Performance Notes

- Archetype configs are ScriptableObjects (shared, not per-instance)
- Confusion/misstep calculations only at waypoints/intersections
- Fascination checks only when in movement states
- State timers updated once per frame in Update()

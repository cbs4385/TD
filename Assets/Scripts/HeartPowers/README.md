# Heart of the Maze - Powers System

## Overview

The Heart Powers system adds player-activated abilities that manipulate visitor behavior, pathfinding, and prop interactions to help lure more visitors to the Heart of the Maze.

## Architecture

### Core Components

- **HeartPowerManager**: Central singleton managing power activation, cooldowns, charges, and essence
- **PathCostModifier**: Overlay system for temporary pathfinding cost modifications
- **HeartPowerDefinition**: ScriptableObject defining power properties and tuning parameters
- **HeartPowerEffects**: Individual power effect implementations
- **HeartPowerUI**: Test UI for activating powers via buttons or keyboard (1-7)
- **HeartPowerDebugVisualizer**: Gizmo-based debug visualization

### Integration Points

- **WaveManager**: Calls `OnWaveStart()`, `OnWaveSuccess()`, and `OnWaveFail()` hooks
- **MazeGrid**: Pathfinding cost modifiers applied via `attraction` field on nodes
- **VisitorControllerBase**: State manipulation via `SetMesmerized()`, `SetFrightened()`, `SetLost()`
- **Props**: FaeLantern, FairyRing, PukaHazard, WillowTheWisp integrations

## Seven Heart Powers

### 1. Heartbeat of Longing
**Synergy**: FaeLantern, Fascinated state

Amplifies FaeLanterns to pull visitors more strongly and biases routes through lantern influence.

**Tiers:**
- **I - Echoing Thrum**: Visitors approaching lantern areas have early fascination chance
- **II - Hungry Glow**: Post-fascination routes bias toward Heart
- **III - Devouring Chorus**: Consumed visitors trigger Heart-ward path bias for others in lantern influence

### 2. Murmuring Paths
**Synergy**: Lost/Confused states, pathfinding

Creates corridors of desire (or sealing) by tilting pathfinding costs on selected tile segments.

**Tiers:**
- **I - Twin Murmurs**: Maintain 2 active segments simultaneously
- **II - Labyrinth's Memory**: Marked visitors prefer Lost state with Heart bias at intersections
- **III - Sealed Ways**: Toggle mode to increase costs instead (soft barriers)

### 3. Dream Snare
**Synergy**: Mesmerized + Lost states

AoE that Mesmerizes visitors, then pushes them into Lost state with Heart-ward bias.

**Tiers:**
- **I - Lingering Thorns**: Edge tiles become thorn-marked, applying Frightened on step
- **II - Shared Nightmare**: Affected visitors prefer common Lost detour path
- **III - Marked for Harvest**: Bonus essence if consumed, increased penalty if escape

### 4. Feastward Panic
**Synergy**: Frightened state, emergency redirection

Global or cone pulse that makes everywhere but the Heart feel deadly.

**Tiers:**
- **I - Selective Terror**: Cone/arc mode instead of global
- **II - Last Refuge**: Visitors about to exit become Fascinated to Heart-closer lanterns
- **III - Hunger Crescendo**: Each consumption extends duration and refunds charge

### 5. Covenant with the Wisps
**Synergy**: WillowTheWisp AI

Wisps temporarily obey you, prioritizing marked victims and Heart-preferred routes.

**Tiers:**
- **I - Twin Flames**: Wisps can lead 2 visitors at once
- **II - Shepherd's Call**: Place Wisp Beacon for patrol targeting
- **III - Burning Tithe**: Increased essence and reduced dread for Wisp-delivered visitors

### 6. Puka's Bargain
**Synergy**: PukaHazard teleportation

Bribes Puka to reduce random drowning and bias teleportation toward Heart-adjacent water.

**Tiers:**
- **I - Chosen Channels**: Mark Pact Pools as preferred teleport targets
- **II - Drowning Debt**: Deaths emit fear AoE with Heart-ward bias
- **III - Undertow**: Once per wave, teleport visitor adjacent to Heart entrance

### 7. Ring of Invitations
**Synergy**: FairyRing entrancement

FairyRings become irresistible invitations redirecting pilgrims toward Heart.

**Tiers:**
- **I - Stacked Circles**: Spawn illusory rings along Heart-ward paths
- **II - Circle Remembered**: Entranced visitors prefer routes through rings
- **III - Closing the Dance**: Visitors inside rings when power ends become Mesmerized

## Usage

### Setup

1. Add `HeartPowerManager` component to scene (typically on Heart GameObject)
2. Assign references:
   - MazeGridBehaviour
   - GameController
3. Create HeartPowerDefinition ScriptableObjects for each power/tier combination:
   - Right-click → Create → FaeMaze → Heart Powers → Power Definition
   - Configure: powerType, tier, costs, cooldown, duration, parameters
4. Assign power definitions array in HeartPowerManager
5. Add `HeartPowerUI` component for testing (optional)
6. Wire HeartPowerManager reference in WaveManager

### Keyboard Shortcuts (when HeartPowerUI is active)

- **1**: Heartbeat of Longing
- **2**: Murmuring Paths (targeted at mouse)
- **3**: Dream Snare (AoE at mouse)
- **4**: Feastward Panic (global or cone toward mouse)
- **5**: Covenant with the Wisps
- **6**: Puka's Bargain (targeted at nearest Puka to mouse)
- **7**: Ring of Invitations

### Programmatic Activation

```csharp
// Global power
HeartPowerManager.Instance.TryActivatePower(HeartPowerType.HeartbeatOfLonging);

// Targeted power
Vector3 targetPos = transform.position;
HeartPowerManager.Instance.TryActivatePower(HeartPowerType.DreamSnare, targetPos);

// Check if can activate
if (HeartPowerManager.Instance.CanActivatePower(powerType, out string reason))
{
    // Activate...
}
```

### Creating Power Definitions

**Example: Dream Snare Tier I**

```
powerType: DreamSnare
tier: 1
chargeCost: 1
cooldown: 15
duration: 8
radius: 3
param1: 4.0 (mesmerize duration)
param2: 2.0 (frightened duration for thorns)
flag1: true (enable Lingering Thorns)
```

### Tuning Parameters

Each power uses HeartPowerDefinition fields differently:

- **duration**: How long the effect lasts
- **radius**: AoE size for targeted powers
- **param1-3**: Power-specific floats (documented in effect class)
- **intParam1-2**: Power-specific ints (e.g., segment length, pool count)
- **flag1-2**: Tier feature toggles

## Pathfinding Integration

Powers modify pathfinding via `PathCostModifier`:

- **Negative costDelta** → cheaper/more desirable (increases attraction)
- **Positive costDelta** → more expensive/less desirable (decreases attraction)
- Applies to `MazeNode.attraction` field
- Final cost = `baseCost - attraction` (clamped to MIN_MOVE_COST)

**Example:**
```csharp
// Make tiles more desirable (Heart-ward bias)
pathModifier.AddModifier(tile, -3.0f, duration: 10f, sourceId: "MyPower");

// Make tiles less desirable (soft barrier)
pathModifier.AddModifier(tile, +5.0f, duration: 10f, sourceId: "SealedWays");
```

## Debug Visualization

- **Gizmos**: Modified tiles shown as colored cubes (green = desirable, red = expensive)
- **HeartPowerDebugVisualizer**: Adds mouse preview and cost labels in Scene view
- **Console Logs**: Enable via HeartPowerManager.debugLog field

## Extension Points

### Adding New Powers

1. Add enum value to `HeartPowerType`
2. Create effect class extending `ActivePowerEffect` in HeartPowerEffects.cs
3. Implement `OnStart()`, `Update()`, `OnEnd()` methods
4. Add case to `HeartPowerManager.ActivatePower()` switch statement
5. Create HeartPowerDefinition ScriptableObjects for each tier
6. Add UI button and keyboard shortcut in HeartPowerUI (optional)

### Hooking into Visitor Events

Some tier upgrades require external event hooks:

```csharp
// In GameController or VisitorControllerBase
private void OnVisitorConsumed(VisitorControllerBase visitor)
{
    // Notify active Heartbeat of Longing effect
    if (HeartPowerManager.Instance != null)
    {
        var effect = HeartPowerManager.Instance.GetActiveEffect<HeartbeatOfLongingEffect>();
        effect?.OnVisitorConsumed(visitor);
    }
}
```

## Performance Notes

- Path cost modifiers use Dictionary lookups (O(1) per tile)
- Cleanup happens once per Update via `CleanupExpired()`
- Visitor scanning in effects uses `FindObjectsByType` (consider caching for large visitor counts)
- Gizmo visualization disabled in builds via `#if UNITY_EDITOR`

## Known Limitations

- Some tier features require additional visitor/prop API hooks (marked as placeholders)
- Murmuring Paths segment generation is simplified (random walk, not optimal)
- FairyRing/WillowTheWisp enhanced control requires prop script modifications
- Visitor state change events not fully implemented (manual polling in some effects)

## Future Improvements

- Event-driven visitor state changes instead of polling
- Pathfinding cache invalidation when modifiers change
- Power combo system for synergy bonuses
- Upgrade persistence and unlock progression
- VFX/SFX integration hooks
- Analytics tracking for power effectiveness

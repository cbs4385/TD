# Willow-the-Wisp Setup Guide

## Overview

The **Willow-the-Wisp** is a mystical prop that wanders the maze and lures visitors to the Heart of the Maze. This guide explains how to integrate it into your game.

## Features

- **Wandering**: Randomly wanders the maze at 2x visitor speed (6 units/sec)
- **Visitor Capture**: When encountering a visitor, captures them to follow
- **Leading**: Leads captured visitors to the HeartOfMaze
- **Speed Matching**: Slows to visitor speed (3 units/sec) when leading
- **Essence Cost**: 50 essence to place

## Files Created

### Scripts
1. **WillowTheWisp.cs** (`Assets/Scripts/Props/WillowTheWisp.cs`)
   - Main wisp behavior script
   - Handles wandering and leading states
   - Detects and captures visitors via trigger collisions

2. **FollowWispBehavior.cs** (`Assets/Scripts/Visitors/FollowWispBehavior.cs`)
   - Attached to visitors when captured by wisp
   - Makes visitors follow the wisp smoothly
   - Auto-removes when wisp reaches destination

### Prefabs
3. **WillowTheWisp.prefab** (`Assets/Prefabs/Props/WillowTheWisp.prefab`)
   - Pre-configured prefab with all required components
   - Components included:
     - Transform
     - SpriteRenderer (yellow-green glow, sorting order 16)
     - Rigidbody2D (Kinematic, no gravity)
     - CircleCollider2D (trigger, radius 0.4)
     - WillowTheWisp script

## Unity Editor Setup

### 1. Add to PropPlacementController

1. In the Unity Editor, find the GameObject with the **PropPlacementController** component
2. In the Inspector, locate the **Placeable Items** section
3. Increase the **Size** by 1 (e.g., if you have 2 items, set it to 3)
4. Configure the new element:

#### Willow-the-Wisp Configuration:
- **Id**: `WillowTheWisp`
- **Display Name**: `Willow-the-Wisp`
- **Prefab**: Drag `WillowTheWisp.prefab` from `Assets/Prefabs/Props/`
- **Essence Cost**: `50`
- **Description**: `A mystical light that wanders the maze and leads visitors to the heart`
- **Preview Color**: RGB(0.9, 1.0, 0.4, 0.5) - Semi-transparent yellow-green

### 2. Test Placement

Once configured, you can place the wisp in two ways:

#### Option A: Direct Placement (for testing)
```csharp
// In a debug script or Update():
if (Keyboard.current.digit3Key.wasPressedThisFrame)
{
    propPlacementController.SelectItemById("WillowTheWisp");
}
```

#### Option B: UI Integration
```csharp
// In a UI button click handler:
void OnWillowWispButtonClicked()
{
    propPlacementController.SelectItemById("WillowTheWisp");
}
```

## Behavior Details

### Wandering State
- Wisp generates random paths through walkable maze tiles
- Moves at **6 units/second** (2x visitor speed)
- Continuously explores the maze
- Uses A* pathfinding for smooth navigation

### Visitor Capture
- Uses **OnTriggerEnter2D** for detection
- Captures walking visitors only (ignores consumed/escaping)
- Adds **FollowWispBehavior** component to captured visitor
- Transitions to Leading state

### Leading State
- Generates path to HeartOfMaze
- Slows to **3 units/second** (matches visitor speed)
- Visitor follows at ~0.3 units behind
- Returns to wandering after visitor is consumed

### Completion
- Visitor reaches HeartOfMaze via normal trigger detection
- HeartOfMaze awards essence as usual
- Wisp returns to wandering state
- Generates new random path

## Component Reference

### WillowTheWisp Component

**Serialized Fields:**
- `wanderSpeed` (float, default: 6) - Speed when wandering
- `leadSpeed` (float, default: 3) - Speed when leading visitor
- `waypointReachedDistance` (float, default: 0.05) - Waypoint threshold
- `wispColor` (Color, default: yellow-green) - Visual color
- `wispSize` (float, default: 0.5) - Sprite size
- `sortingOrder` (int, default: 16) - Render layer
- `enablePulse` (bool, default: true) - Pulsing glow effect
- `pulseSpeed` (float, default: 3) - Pulse frequency
- `pulseMagnitude` (float, default: 0.15) - Pulse intensity

**Public Properties:**
- `WispState State` - Current state (Wandering/Leading)
- `bool IsLeading` - Whether currently leading a visitor

### FollowWispBehavior Component

**Serialized Fields:**
- `followDistance` (float, default: 0.3) - Distance to maintain from wisp
- `followSpeedMultiplier` (float, default: 1.0) - Speed modifier when following

**Public Methods:**
- `void StartFollowing(WillowTheWisp wisp)` - Begin following wisp
- `void StopFollowing()` - Stop following and remove component

## Integration with Existing Systems

### MazeGrid
- Uses `MazeGridBehaviour.WorldToGrid()` for position conversion
- Uses `MazeGridBehaviour.GridToWorld()` for world positioning
- Respects walkable tiles only

### GameController
- Uses `GameController.Instance.TryFindPath()` for pathfinding
- Uses `GameController.Instance.Heart.GridPosition` for destination
- Follows existing pathfinding system

### HeartOfMaze
- No modifications needed
- Visitors led by wisp consume normally
- Essence awarded as usual

### VisitorController
- No modifications to core visitor logic needed
- FollowWispBehavior is dynamically added/removed
- Compatible with all existing visitor states

## Visual Appearance

- **Color**: Yellow-green glow (0.9, 1.0, 0.4)
- **Size**: 0.5 units (smaller than visitors)
- **Effect**: Pulsing glow animation
- **Sorting Order**: 16 (renders above visitors)
- **Sprite**: Soft-edged circle with alpha gradient

## Gizmos (Scene View Only)

When selected in the editor:
- Green path: Current wander path
- Yellow path: Path to heart when leading
- Yellow sphere: Current waypoint target

## Balance Considerations

### Cost vs. Benefit
- **Cost**: 50 essence (highest of all props)
- **Benefit**: Guarantees visitor reaches heart
- **Trade-off**: Expensive but reliable

### Comparison to Other Props
- **FaeLantern** (20 essence): Attracts & fascinates visitors
- **FairyRing** (15 essence): Slows & entrances visitors
- **Willow-the-Wisp** (50 essence): Guarantees delivery to heart

### Strategy
- Use early game: Waste of essence, visitors naturally reach heart
- Use mid game: Good for securing difficult visitors
- Use late game: Essential for confused or distant visitors

## Testing Checklist

### Basic Functionality
- [ ] Wisp appears when placed with PropPlacementController
- [ ] 50 essence is deducted on placement
- [ ] Wisp wanders randomly through maze
- [ ] Wisp moves at 6 units/second when wandering
- [ ] Visual pulsing effect works

### Visitor Capture
- [ ] Wisp detects visitors via trigger collision
- [ ] Visitor begins following when captured
- [ ] FollowWispBehavior component is added
- [ ] Wisp transitions to Leading state
- [ ] Debug logs show capture event

### Leading Behavior
- [ ] Wisp generates path to HeartOfMaze
- [ ] Wisp slows to 3 units/second when leading
- [ ] Visitor follows at ~0.3 units behind
- [ ] Path is visualized in Scene view (yellow)
- [ ] Wisp leads visitor all the way to heart

### Completion
- [ ] Visitor reaches HeartOfMaze
- [ ] Visitor is consumed normally
- [ ] Essence is awarded (10 essence default)
- [ ] Wisp returns to Wandering state
- [ ] Wisp generates new random path
- [ ] FollowWispBehavior is removed from visitor

### Edge Cases
- [ ] Wisp placed on non-walkable tile is rejected
- [ ] Multiple wisps can exist simultaneously
- [ ] Each wisp can only lead one visitor at a time
- [ ] Wisp handles visitor being consumed early
- [ ] Wisp handles Heart being missing gracefully
- [ ] Pathfinding failures don't crash wisp

### Performance
- [ ] Wisp doesn't cause frame drops
- [ ] Multiple wisps (3-5) work smoothly
- [ ] Gizmos don't impact editor performance
- [ ] No memory leaks from component add/remove

## Known Limitations

1. **Single Visitor**: Each wisp can only lead one visitor at a time
2. **No Re-capture**: Once leading, wisp ignores other visitors
3. **Fixed Speed**: Leading speed is fixed at 3 units/sec
4. **No Pathfinding Updates**: Path to heart is calculated once

## Future Enhancements (Optional)

- [ ] **Multi-visitor**: Allow wisp to lead multiple visitors in a train
- [ ] **Adjustable Speed**: Match slowest visitor's speed dynamically
- [ ] **Smart Wandering**: Prefer areas with high visitor traffic
- [ ] **Path Updates**: Recalculate if maze changes or shortcuts appear
- [ ] **Visual Trail**: Leave a glowing trail for visitors to follow
- [ ] **Visitor Preference**: Prioritize entranced or fascinated visitors

## Code Example: Spawning Wisp Programmatically

```csharp
// Get references
PropPlacementController controller = FindObjectOfType<PropPlacementController>();
MazeGridBehaviour mazeGrid = FindObjectOfType<MazeGridBehaviour>();
GameController gameController = GameController.Instance;

// Check essence
if (gameController.TrySpendEssence(50))
{
    // Get a walkable grid position
    Vector2Int gridPos = new Vector2Int(5, 10); // Example position

    // Get the WillowTheWisp item
    var wispItem = controller.GetItemById("WillowTheWisp");

    if (wispItem != null && wispItem.prefab != null)
    {
        // Convert grid to world position
        Vector3 worldPos = mazeGrid.GridToWorld(gridPos.x, gridPos.y);

        // Instantiate
        GameObject wisp = Instantiate(wispItem.prefab, worldPos, Quaternion.identity);
        wisp.name = $"WillowTheWisp_{gridPos.x}_{gridPos.y}";
    }
}
```

## Troubleshooting

### Wisp doesn't appear
- Check PropPlacementController has WillowTheWisp in placeableItems list
- Verify prefab is assigned
- Check essence is >= 50
- Ensure tile is walkable

### Wisp doesn't capture visitors
- Check CircleCollider2D is trigger (isTrigger = true)
- Verify Rigidbody2D is Kinematic
- Check visitor has Rigidbody2D and CircleCollider2D
- Ensure visitor state is Walking

### Wisp doesn't lead to heart
- Check HeartOfTheMaze exists in scene
- Verify GameController.Instance.Heart is not null
- Check pathfinding succeeds (walkable path exists)
- Review console for pathfinding errors

### Visitor doesn't follow
- Check FollowWispBehavior was added
- Verify FollowWispBehavior.IsFollowing returns true
- Check visitor has Rigidbody2D for movement
- Review visitor's current state

### Performance issues
- Limit number of active wisps (3-5 recommended)
- Disable gizmos in builds (they're editor-only)
- Check for excessive pathfinding calls
- Review console for error spam

## Summary

The Willow-the-Wisp is a high-cost, high-reward prop that guarantees visitors reach the HeartOfMaze. It:

1. **Wanders** the maze at 2x speed when alone
2. **Captures** visitors via trigger collision
3. **Leads** them to the heart at visitor speed
4. **Completes** delivery and returns to wandering

Integration is simple: add to PropPlacementController's placeableItems list with 50 essence cost, and it's ready to use!

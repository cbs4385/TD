# FairyRing Setup and Usage Guide

## Overview

The **FairyRing** is a mystical prop that affects visitors passing through it:
- **Slows down** visitors while they are inside the ring
- **Marks visitors as "Entranced"** permanently (design choice)
- **Optional pathfinding attraction** via MazeAttractor component

## Implementation Status

✅ **FairyRing.cs** - Fully implemented with:
- Trigger detection (OnTriggerEnter2D / OnTriggerExit2D)
- Speed modification (slows visitors to 50% speed by default)
- Entrancement state tracking
- Visual pulsing effect
- Debug gizmos (purple circle showing trigger area)

✅ **VisitorController.cs** - Fully implemented with:
- `SetEntranced(bool)` method
- `SpeedMultiplier` property (clamped 0.1x to 2.0x)
- `IsEntranced` property for querying state
- Movement system uses speed multiplier

## Required Components

The FairyRing prefab must have:

1. **FairyRing Script** (Assets/Scripts/Props/FairyRing.cs)
2. **CircleCollider2D** (automatically enforced via [RequireComponent])
   - `isTrigger` = true ✅ Required!
   - `radius` = appropriate size for your ring visual
3. **SpriteRenderer** (optional, for visual representation)
4. **MazeAttractor** (optional, for pathfinding attraction)

## Setup Instructions

### 1. Create FairyRing Prefab (if not exists)

1. In Unity Hierarchy, create a new GameObject: `FairyRing`
2. Add the **FairyRing** component
3. Unity will automatically add a **Collider2D** (due to RequireComponent)
4. Select the GameObject and configure the CircleCollider2D:
   - Check ✅ **Is Trigger**
   - Set **Radius** (e.g., 0.5 to 1.0 depending on desired area)

### 2. Configure FairyRing Settings

In the Inspector, configure:

#### Entrancement Settings
- **Slow Factor**: `0.5` (visitors move at 50% speed inside)
  - Range: 0.1 to 2.0
  - Lower = slower, Higher = faster
  - Default: 0.5 (50% speed)

#### Visual Settings (optional pulsing effect)
- **Enable Pulse**: ✅ Checked
- **Pulse Speed**: `2.0` (how fast the ring pulses)
- **Pulse Magnitude**: `0.1` (10% scale variation)

### 3. Add Visual Sprite (optional)

If you want a visible ring sprite:
1. Add or configure the **SpriteRenderer** component
2. Assign a sprite texture (e.g., a ring or circle sprite)
3. Set appropriate color, size, and sorting order

### 4. Add Pathfinding Attraction (optional)

To make the FairyRing slightly attract visitor paths:

1. Add **MazeAttractor** component to the FairyRing GameObject
2. Configure attraction settings:
   - **Radius**: `2.0` to `3.0` (smaller than FaeLantern)
   - **Attraction Strength**: `0.2` to `0.3` (weaker than FaeLantern)
   - **Visitor Slowing**: ⚠️ **Uncheck** "Enable Visitor Slowing"
     - Important: FairyRing handles slowing directly
     - MazeAttractor slowing would conflict
     - Only use MazeAttractor for pathfinding influence

**Recommended MazeAttractor Settings for FairyRing:**
```
Radius: 2.5
Attraction Strength: 0.25
Show Debug Radius: ✅ (for testing)
Enable Visitor Slowing: ❌ (disabled - FairyRing handles this)
```

### 5. Place in Scene

1. Drag FairyRing prefab into the scene
2. Position it on a **walkable path tile**
3. The ring will automatically detect its grid position
4. Purple gizmo circle shows the trigger area in Scene view

## How It Works

### Visitor Enters Ring
1. **OnTriggerEnter2D** detects VisitorController
2. Calls `visitor.SetEntranced(true)` - marks as entranced permanently
3. Calls `visitor.SpeedMultiplier = slowFactor` - applies slow effect
4. Visitor moves at reduced speed (e.g., 50%)

### Visitor Inside Ring
- Continues moving at reduced speed
- Entranced flag remains true
- Normal pathfinding behavior

### Visitor Exits Ring
1. **OnTriggerExit2D** detects VisitorController leaving
2. Calls `visitor.SpeedMultiplier = 1f` - restores normal speed
3. **Entranced flag stays TRUE** (design choice)
4. Visitor returns to normal movement speed

### Permanent Entrancement
Once a visitor passes through a FairyRing:
- The `IsEntranced` flag remains **true forever**
- Speed returns to normal after exiting
- Could be used for future mechanics (e.g., entranced visitors give bonus essence)

## Testing Checklist

### Basic Functionality
- [ ] Place FairyRing on a walkable path
- [ ] Start game and spawn visitors
- [ ] Verify visitors slow down when entering ring
- [ ] Verify `IsEntranced` becomes true (check in Inspector during play)
- [ ] Verify visitors return to normal speed when exiting
- [ ] Verify no NullReferenceException errors in Console

### Visual Feedback
- [ ] Purple gizmo circle visible in Scene view
- [ ] Pulsing animation works (if enabled)
- [ ] Sprite renders correctly (if using SpriteRenderer)

### Pathfinding (if using MazeAttractor)
- [ ] Orange attraction gizmo visible in Scene view
- [ ] Visitors' paths slightly favor tiles near FairyRing
- [ ] FaeLanterns still have stronger attraction
- [ ] No conflicts with MazeAttractor slowing (should be disabled)

### Edge Cases
- [ ] Multiple visitors can enter/exit simultaneously
- [ ] Works with visitors already entranced by other rings
- [ ] Ring works when placed at maze entrance
- [ ] Ring works when placed near maze heart
- [ ] No physics errors with trigger collisions

## Debug Information

### Gizmos
**Purple Circle** (always visible in Scene view):
- Shows FairyRing trigger area
- Brighter when GameObject is selected
- Radius matches CircleCollider2D radius

**Magenta Point** (when selected):
- Shows exact center of ring

### Console Logs
Currently, FairyRing does not log messages. Check VisitorController.IsEntranced value in Inspector during play.

### Inspector Values (during Play Mode)
Select a Visitor GameObject to see:
- **Is Entranced**: true/false
- **Speed Multiplier**: current value (1.0 = normal, 0.5 = half speed)
- **State**: should be "Walking" when moving through ring

## Common Issues

### "Visitors don't slow down"
- ✅ Check CircleCollider2D has **Is Trigger** enabled
- ✅ Verify FairyRing has **CircleCollider2D** component
- ✅ Check visitor has **CircleCollider2D** and **Rigidbody2D** (auto-created)
- ✅ Ensure FairyRing is on a walkable path tile

### "Speed doesn't return to normal after exiting"
- This is a known limitation: VisitorController uses direct assignment
- If visitor enters multiple speed-modifying triggers, last one wins
- For MVP, this is acceptable behavior

### "No trigger events firing"
- ✅ Both FairyRing and Visitor must have Collider2D components
- ✅ Visitor needs Rigidbody2D (Kinematic) - auto-created by VisitorController
- ✅ At least one must be a trigger (FairyRing is, Visitor should be)
- ✅ Check Physics2D collision matrix (Edit → Project Settings → Physics 2D)

### "NullReferenceException on SetEntranced"
- Should not occur - VisitorController always has SetEntranced method
- If it does, verify you're using the latest VisitorController.cs

## Future Enhancements

Possible improvements for later:

### Multiple Speed Modifiers
Currently, speed modifiers overwrite each other. Could implement:
- Stack-based system (push/pop slow effects)
- List of active modifiers with combined multiplier
- Priority system (highest priority effect wins)

### Enhanced Entrancement Effects
- Change visitor sprite color when entranced
- Add particle effects to entranced visitors
- Different movement patterns for entranced visitors
- Bonus essence when entranced visitors reach heart

### Advanced Pathfinding
- Entranced visitors prefer paths through more FairyRings
- Entranced visitors avoid FaeLanterns
- FairyRing attraction increases over time (becomes more powerful)

### Multiple FairyRing Types
- **Slow Ring**: Current implementation (slows visitors)
- **Boost Ring**: Speeds up visitors temporarily
- **Teleport Ring**: Instantly moves visitor to another ring
- **Confusion Ring**: Reverses visitor direction

## Code Reference

### FairyRing.cs
Location: `Assets/Scripts/Props/FairyRing.cs`

Key Methods:
- `OnVisitorEnter(VisitorController)` - Called when visitor enters
- `OnVisitorExit(VisitorController)` - Called when visitor exits
- `UpdatePulse()` - Visual pulsing effect

Properties:
- `SlowFactor` - Get the configured slow factor

### VisitorController.cs
Location: `Assets/Scripts/Visitors/VisitorController.cs`

Key Methods:
- `SetEntranced(bool)` - Mark visitor as entranced
- Properties:
  - `IsEntranced` - Query if visitor is entranced
  - `SpeedMultiplier` - Get/set speed multiplier (clamped 0.1x to 2.0x)
  - `MoveSpeed` - Base movement speed

### MazeAttractor.cs (optional)
Location: `Assets/Scripts/Maze/MazeAttractor.cs`

Key Configuration:
- `radius` - Attraction radius in grid units
- `attractionStrength` - How strongly it pulls paths
- `enableVisitorSlowing` - ❌ Should be FALSE for FairyRing

## Acceptance Criteria Status

✅ **FairyRing detects visitors via trigger**
- OnTriggerEnter2D and OnTriggerExit2D implemented
- Requires CircleCollider2D with isTrigger = true

✅ **Slows visitors while inside**
- Sets SpeedMultiplier to slowFactor (default 0.5)
- Restores to 1.0 when exiting

✅ **Marks visitors as entranced**
- Calls SetEntranced(true) on enter
- Permanent effect (remains true after exit)

✅ **No NullReference errors**
- All methods safely check for null
- VisitorController always has required methods

✅ **Optional pathfinding attraction** (via MazeAttractor)
- Can add MazeAttractor component
- Configure with smaller radius and strength than FaeLantern
- Disable visitor slowing in MazeAttractor to avoid conflicts

## Summary

The FairyRing system is **fully implemented and ready to use**:
1. Add FairyRing component to a GameObject
2. Ensure CircleCollider2D is set to trigger
3. Configure slow factor and visual settings
4. Optionally add MazeAttractor for pathfinding influence
5. Place on walkable path and test with visitors

The system integrates seamlessly with existing VisitorController and MazeGrid systems.

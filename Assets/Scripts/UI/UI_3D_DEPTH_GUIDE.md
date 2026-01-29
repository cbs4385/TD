# UI Depth Adjustments for 3D Rendering

This guide explains how the UI system has been adapted to work with the 3D maze rendering system while maintaining proper depth perception and camera interaction.

## Overview

The UI system uses screen-space overlays - HUD elements that stay fixed on screen.

## Screen-Space UI (HUD)

### Current Implementation

The main HUD elements use screen-space overlays, which is the correct approach for 3D games:

- **UIController.cs** - Canvas settings configuration
- **EssenceBarController.cs** - Essence bar at top of screen
- **HeartPowerPanelController.cs** - Heart power activation buttons and run timer

All these use `RenderMode.ScreenSpaceOverlay`, which ensures they:
- Always render on top of the 3D scene
- Remain at consistent screen positions
- Don't require camera assignment
- Work regardless of camera orientation

### Essence Event System

The essence update system works identically in both 2D and 3D:

```csharp
// GameController fires events when essence changes
GameController.Instance.OnEssenceChanged?.Invoke(newEssence);

// UI controllers subscribe to these events
GameController.Instance.OnEssenceChanged += UpdateEssence;
```

This event-driven architecture ensures UI updates work seamlessly with the 3D system.

## Camera Focus Shortcuts

The 3D camera system maintains all the focus shortcuts from the 2D system:

### Keyboard Shortcuts

| Key | Action | Behavior |
|-----|--------|----------|
| `1` | Focus on Heart | Smoothly pans to the Heart of the Maze |
| `2` | Focus on Entrance | Smoothly pans to the maze entrance |
| `3` | Focus on Last Visitor | Tracks the most recently spawned visitor |

### Implementation

Focus movements use smooth lerping for camera-friendly transitions:

```csharp
// Instant focus (jumps immediately)
cameraController.FocusOnHeart(instant: true);

// Smooth focus (lerps over time)
cameraController.FocusOnHeart(instant: false);

// Visitor tracking (continuous following)
cameraController.FocusOnVisitor(visitor, instant: false);
```

The focus system:
- Uses `Vector3.MoveTowards` for smooth interpolation
- Respects maze bounds clamping
- Supports continuous tracking for moving targets
- Maintains proper Z-plane positioning (z=0 for focus point)

## Camera Controls

The 3D camera provides enhanced controls while preserving the 2D control scheme:

### Mouse Controls

- **Right Mouse Drag** - Orbit around focus point (pitch & yaw)
- **Middle Mouse Drag** - Pan camera across the maze
- **Mouse Wheel** - Dolly zoom in/out
- **Collision Detection** - Automatically pulls camera forward when obstructed

### Keyboard Controls

- **WASD / Arrow Keys** - Pan camera relative to current orientation
- **1** - Focus on Heart
- **2** - Focus on Entrance
- **3** - Focus on Last Visitor

## Best Practices

### When to Use Screen-Space Overlay

Use for:
- HUD elements (health, resources, scores)
- Menu systems
- Control panels
- Minimap overlays
- Tutorial text

## Technical Details

### Z-Plane Considerations

The maze operates in the XY plane with Z used for depth:
- Maze tiles: Z varies by height
- Camera looks down at angle onto XY plane
- Focus point always has Z=0 (stays on primary plane)

### Camera-World Coordinate Mapping

The 3D camera system converts XY movement to 3D space:
- X movement → World X (left/right)
- Y movement → World Y (up/down in plane, not altitude)
- Z position → Altitude/depth (camera distance from focus)

# Debug Panel Setup Instructions

This guide explains how to set up the in-game debug panel UI in Unity Editor.

## Overview

The debug panel provides:
- Toggle for grid gizmos visualization
- Toggle for attraction heatmap visualization
- Slider to adjust game timescale (0.1x to 2.0x) with live value display
- Button to spawn a test visitor
- F1 key toggle to show/hide the panel

## Quick Setup (Recommended)

The **easiest way** to set up the debug panel is to let it auto-create itself:

### Option 1: Auto-Creation (No Manual UI Setup Required!)

1. In the **Hierarchy** window, locate or create a GameObject to hold the script (e.g., `UIRoot` or `GameRoot`)
2. Select that GameObject
3. In the **Inspector**, click **Add Component**
4. Search for and add `DebugUIController`
5. **That's it!** The script will automatically:
   - Find or create a Canvas
   - Create the complete debug panel UI hierarchy
   - Wire up all controls and references
   - Auto-find `MazeGridBehaviour` and `WaveSpawner` in the scene

### Testing

1. Enter **Play Mode**
2. The debug panel will appear in the top-right corner automatically
3. Press **F1** to toggle visibility
4. All controls should work immediately:
   - Toggle **Grid Gizmos** and **Attraction Heatmap** (view in Scene window)
   - Move the **Timescale** slider to see game speed change (displays current value)
   - Click **Spawn Visitor** to spawn a test visitor

## Manual Setup (Optional)

If you prefer to manually create the UI hierarchy in the editor, you can do so and assign the references to the DebugUIController component. The script will only auto-create UI elements that are not already assigned.

### Manual UI Creation Steps

1. **Create Canvas** (if not already present)
   - Right-click in Hierarchy → **UI** → **Canvas**
   - Set **Render Mode**: Screen Space - Overlay
   - Add **Canvas Scaler** component
     - UI Scale Mode: Scale With Screen Size
     - Reference Resolution: 1920 x 1080

2. **Create Debug Panel**
   - Right-click **Canvas** → **UI** → **Panel**
   - Rename to `DebugPanel`
   - Position in top-right corner:
     - Anchor Presets: Top-Right (Alt+Shift+Click)
     - Pos X: -10, Pos Y: -10
     - Width: 300, Height: 300
     - Pivot: X: 1, Y: 1
   - Set background color (e.g., RGBA: 26, 26, 26, 230)

3. **Add Title**
   - Right-click `DebugPanel` → **UI** → **Text - TextMeshPro**
   - Rename to `Title`
   - Set text: `DEBUG PANEL`
   - Position at top of panel
   - Font size: 20, Bold, Center aligned

4. **Add Grid Toggle**
   - Right-click `DebugPanel` → **UI** → **Toggle**
   - Rename to `GridToggle`
   - Set label text: `Grid Gizmos`
   - Position below title

5. **Add Heatmap Toggle**
   - Right-click `DebugPanel` → **UI** → **Toggle**
   - Rename to `HeatmapToggle`
   - Set label text: `Attraction Heatmap`
   - Position below grid toggle

6. **Add Timescale Label**
   - Right-click `DebugPanel` → **UI** → **Text - TextMeshPro**
   - Set text: `Timescale:`
   - Position below toggles

7. **Add Timescale Slider**
   - Right-click `DebugPanel` → **UI** → **Slider**
   - Rename to `TimescaleSlider`
   - Set Min Value: 0.1, Max Value: 2.0, Value: 1.0
   - Position below timescale label

8. **Add Spawn Button**
   - Right-click `DebugPanel` → **UI** → **Button - TextMeshPro**
   - Rename to `SpawnTestVisitorButton`
   - Set button text: `Spawn Visitor`
   - Position at bottom of panel

9. **Attach and Configure Script**
   - Select the GameObject where you want to attach the controller (e.g., `UIRoot`)
   - Add `DebugUIController` component
   - Drag and drop references:
     - **Debug Panel**: The `DebugPanel` GameObject
     - **Grid Toggle**: The `GridToggle` GameObject
     - **Heatmap Toggle**: The `HeatmapToggle` GameObject
     - **Timescale Slider**: The `TimescaleSlider` GameObject
     - **Spawn Test Visitor Button**: The `SpawnTestVisitorButton` GameObject
     - **Maze Grid Behaviour**: Auto-found (or manually assign)
     - **Wave Spawner**: Auto-found (or manually assign)

## Features

### Grid Gizmos Toggle
- Enables/disables grid visualization in the Scene view
- Shows walkable tiles (green) and blocked tiles (red)
- Highlights entrance (blue) and heart (yellow)

### Attraction Heatmap Toggle
- Overlays attraction values on the grid visualization
- Higher attraction areas shown in cyan
- Only visible when Grid Gizmos is also enabled
- View in Scene window (not Game window)

### Timescale Slider
- Range: 0.1x (slow motion) to 2.0x (fast forward)
- Affects all time-dependent operations:
  - Visitor movement
  - Wave spawning intervals
  - Animations
  - Physics
- Displays current value (e.g., "1.5x")

### Spawn Visitor Button
- Spawns a single test visitor immediately
- Uses WaveSpawner's pathfinding logic
- Visitor follows normal path from entrance to heart
- Useful for testing without waiting for wave timers

### F1 Toggle
- Press F1 at any time to show/hide the debug panel
- Panel starts visible by default
- Convenient for gameplay testing without UI clutter

## Troubleshooting

### Panel doesn't appear
- Check that the DebugUIController component is on an active GameObject
- Verify the GameObject is enabled in the hierarchy
- Look for error messages in the Console window

### F1 doesn't toggle the panel
- Make sure the script is attached to an active GameObject
- Check that the `debugPanel` reference exists (should auto-create)
- Verify no input conflicts with other systems

### Toggles don't affect visualization
- **IMPORTANT**: Gizmos are only visible in the **Scene view**, not the Game view
- Click on the Scene tab in Unity Editor to see the visualization
- Make sure `MazeGridBehaviour` exists in the scene
- Check that the toggles are enabled (checkmarks visible)

### Spawn button doesn't work
- Verify that `WaveSpawner` exists in the scene
- Check that entrance and heart objects are properly configured
- Look for error/warning messages in Console
- Ensure the visitor prefab is assigned to WaveSpawner

### Timescale doesn't work
- Timescale affects physics and time-based operations
- Some custom scripts might use unscaled time (`Time.unscaledDeltaTime`)
- Check that your movement code uses `Time.deltaTime` (scaled)

### Auto-created UI looks wrong
- The auto-creation uses TextMeshPro for text elements
- If TMP isn't imported, fallback to legacy Text components may occur
- For custom styling, use Manual Setup instead

### References not found
- The script auto-finds `MazeGridBehaviour` and `WaveSpawner` using `FindFirstObjectByType`
- If multiple instances exist, the first found will be used
- Manually assign references in Inspector if auto-finding selects wrong instances

## Technical Notes

- The debug panel uses **TextMeshPro** for text rendering (better quality than legacy Text)
- Auto-creation happens in `Start()` if UI references are null
- The script is completely standalone - no dependencies on other UI scripts
- All UI elements are created programmatically if not manually set up
- System references (`MazeGridBehaviour`, `WaveSpawner`) are auto-found if not assigned
- Panel starts visible by default for easier initial testing

## Optional Enhancements

Want to customize the debug panel? Here are some ideas:

- **Add more debug info**: Visitor count, current wave number, essence amount
- **Add keyboard shortcuts**: G for grid, H for heatmap, T for timescale reset
- **Add FPS counter**: Display current framerate
- **Add performance metrics**: Draw calls, memory usage, etc.
- **Style customization**: Change colors, fonts, sizes in the script
- **Resizable panel**: Add drag handles for repositioning
- **Multiple panels**: Create separate panels for different debug categories
- **Save preferences**: Persist toggle states between sessions using PlayerPrefs

## Advanced Usage

### Accessing from Other Scripts

You can control the debug panel from other scripts:

```csharp
// Find the controller
DebugUIController debugUI = FindFirstObjectByType<DebugUIController>();

// Toggle visibility programmatically
if (Input.GetKeyDown(KeyCode.F2))
{
    // Access is through Unity's GameObject.SetActive
    // since ToggleDebugPanel is private
}
```

### Extending Functionality

To add new debug controls:

1. Add serialized field for the new UI element
2. Create the UI element in `CreateDebugPanelUI()`
3. Add initialization in `InitializeControls()`
4. Implement callback method
5. Add cleanup in `OnDestroy()`

Example for adding a reset button:

```csharp
[SerializeField] private Button resetButton;

// In CreateDebugPanelUI():
resetButton = CreateButton(debugPanel.transform, "Reset", yPos);

// In InitializeControls():
resetButton.onClick.AddListener(OnResetClicked);

// New callback:
private void OnResetClicked()
{
    Time.timeScale = 1.0f;
    // Reset other values...
}

// In OnDestroy():
if (resetButton != null)
    resetButton.onClick.RemoveListener(OnResetClicked);
```

## Summary

The debug panel is designed to be **zero-configuration** - just attach the script and it works! Manual setup is available for those who want custom styling or specific positioning, but the auto-creation feature makes it incredibly easy to get started.

Press **F1** in Play Mode to toggle the panel on/off at any time.

# Debug Panel Setup Instructions

This guide explains how to set up the in-game debug panel UI in Unity Editor.

## Overview

The debug panel provides:
- Toggle for grid gizmos visualization
- Toggle for attraction heatmap visualization
- Slider to adjust game timescale (0.1x to 2.0x)
- Button to spawn a test visitor
- F1 key toggle to show/hide the panel

## Step-by-Step Setup

### 1. Create the Debug Panel

1. In the **Hierarchy** window, locate your **Canvas** GameObject
2. Right-click on **Canvas** → **UI** → **Panel**
3. Rename it to `DebugPanel`

### 2. Position the Debug Panel

1. Select `DebugPanel` in the Hierarchy
2. In the **Inspector**, find the **Rect Transform** component
3. Set the following anchors for top-right corner positioning:
   - **Anchor Presets**: Click the square in the top-left, then hold **Alt+Shift** and click the **top-right** preset
   - **Pos X**: -10
   - **Pos Y**: -10
   - **Width**: 300
   - **Height**: 250
4. Set **Pivot**: X: 1, Y: 1 (top-right)
5. In the **Image** component, set **Color** alpha to ~150 for transparency (or RGB: 40, 40, 40, A: 200)

### 3. Add UI Elements to Debug Panel

#### A. Grid Toggle
1. Right-click `DebugPanel` → **UI** → **Toggle**
2. Rename it to `GridToggle`
3. Position:
   - **Pos X**: -150
   - **Pos Y**: -30
   - **Width**: 280
   - **Height**: 30
4. Select the **Label** child object (Text component)
   - Set text to: `Grid Gizmos`
   - Set font size: 16

#### B. Heatmap Toggle
1. Right-click `DebugPanel` → **UI** → **Toggle**
2. Rename it to `HeatmapToggle`
3. Position:
   - **Pos X**: -150
   - **Pos Y**: -70
   - **Width**: 280
   - **Height**: 30
4. Select the **Label** child object
   - Set text to: `Attraction Heatmap`
   - Set font size: 16

#### C. Timescale Slider
1. Right-click `DebugPanel` → **UI** → **Slider**
2. Rename it to `TimescaleSlider`
3. Position:
   - **Pos X**: -150
   - **Pos Y**: -120
   - **Width**: 280
   - **Height**: 30
4. In the **Slider** component:
   - **Min Value**: 0.1
   - **Max Value**: 2.0
   - **Value**: 1.0
5. Add a label above the slider:
   - Right-click `DebugPanel` → **UI** → **Text**
   - Rename to `TimescaleLabel`
   - Position above the slider (Pos Y: -100)
   - Set text to: `Timescale`
   - Set font size: 16

#### D. Spawn Visitor Button
1. Right-click `DebugPanel` → **UI** → **Button**
2. Rename it to `SpawnTestVisitorButton`
3. Position:
   - **Pos X**: -150
   - **Pos Y**: -170
   - **Width**: 280
   - **Height**: 40
4. Select the **Text** child object
   - Set text to: `Spawn Visitor`
   - Set font size: 18

#### E. Panel Title (Optional)
1. Right-click `DebugPanel` → **UI** → **Text**
2. Rename to `TitleText`
3. Position at top of panel:
   - **Pos X**: -150
   - **Pos Y**: -10
   - **Width**: 280
   - **Height**: 30
4. Set text to: `DEBUG PANEL`
5. Set font size: 20
6. Set alignment to center
7. Make it bold if possible

### 4. Add the DebugUIController Script

1. Select `DebugPanel` (or your Canvas/UIRoot GameObject)
2. In the **Inspector**, click **Add Component**
3. Search for and add `DebugUIController`

### 5. Wire Up References

1. With the GameObject containing `DebugUIController` selected:
2. In the **Inspector**, find the **DebugUIController** component
3. Drag and drop the following from the Hierarchy:
   - **Debug Panel**: The `DebugPanel` GameObject itself
   - **Grid Toggle**: The `GridToggle` GameObject
   - **Heatmap Toggle**: The `HeatmapToggle` GameObject
   - **Timescale Slider**: The `TimescaleSlider` GameObject
   - **Spawn Test Visitor Button**: The `SpawnTestVisitorButton` GameObject
   - **Maze Grid Behaviour**: The GameObject with `MazeGridBehaviour` (usually `MazeGrid`)
   - **Wave Spawner**: The GameObject with `WaveSpawner` (usually `GameController` or similar)

### 6. Test the Debug Panel

1. Enter **Play Mode**
2. The debug panel should appear in the top-right corner
3. Press **F1** to toggle visibility
4. Test each control:
   - Toggle **Grid Gizmos** and **Attraction Heatmap** (open Scene view to see gizmos)
   - Move the **Timescale** slider to see game speed change
   - Click **Spawn Visitor** to spawn a test visitor

## Troubleshooting

### Panel doesn't appear
- Check that the Canvas has a **Canvas** component with render mode set to **Screen Space - Overlay** or **Screen Space - Camera**
- Ensure the DebugPanel GameObject is active in the hierarchy
- Check that the panel's Image component has a visible color/alpha

### F1 doesn't toggle the panel
- Make sure the DebugUIController script is attached to an active GameObject
- Check that the `debugPanel` reference is assigned in the Inspector

### Toggles don't affect visualization
- Verify that `MazeGridBehaviour` reference is assigned
- Make sure you're viewing the **Scene view** (not just Game view) to see gizmos
- Check that the toggles are properly wired up in the Inspector

### Spawn button doesn't work
- Verify that `WaveSpawner` reference is assigned
- Check console for any error messages
- Ensure the scene has entrance and heart objects properly configured

### Timescale doesn't work
- Check that physics/animation are using `Time.deltaTime` (scaled time)
- Some systems might use `Time.fixedDeltaTime` which also scales with timeScale

## Notes

- The debug panel starts **visible** by default for easier initial testing
- Press **F1** at any time to toggle visibility
- Grid gizmos and heatmap are only visible in the **Scene view** (not Game view)
- Timescale affects all time-based operations including movement, animations, and physics
- The script will auto-find `MazeGridBehaviour` and `WaveSpawner` if they exist in the scene

## Optional Enhancements

- Add a **Text** element next to the timescale slider showing the current value
- Style the panel with custom colors, borders, or rounded corners
- Add keyboard shortcuts for individual toggles (e.g., G for grid, H for heatmap)
- Add more debug features like visitor count display, FPS counter, etc.
- Create a prefab of the DebugPanel for easy reuse across scenes

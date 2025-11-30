# WaveController Setup Guide

## Overview

The **WaveController** is a self-contained system for spawning visitors at a configurable rate with an integrated UI display showing visitor counts and spawn countdown.

## Features

- **Configurable Spawning**: Set total visitors and spawn interval
- **Integrated UI**: Auto-creates UI display for visitor count and countdown
- **Real-time Countdown**: Shows time until next visitor spawns
- **Auto-cleanup**: Countdown disappears after final visitor is spawned
- **Spawn Marker Support**: Uses random spawn point pairs (requires 2+ spawn markers)

## Quick Start

### 1. Add Component to Scene

1. Create an empty GameObject in your scene (e.g., "WaveController")
2. Add the **WaveController** component to it
3. Configure the required fields:

### 2. Required Configuration

**In the Inspector:**

#### Spawn Configuration
- **Visitor Prefab**: Drag the visitor prefab from `Assets/Prefabs/Visitors/`
  - Example: `Visitor_FestivalTourist.prefab`
- **Total Visitors**: Number of visitors to spawn (default: 10)
- **Spawn Interval**: Seconds between spawns (default: 1.5)

#### UI References (Optional)
- Leave these **empty** for auto-creation
- Or manually assign if you have custom UI elements

### 3. Maze Requirements

**Important**: Your maze must have at least **2 spawn markers** (A, B, C, D) defined in the maze file.

Example maze markers:
```
A.....  (Spawn point A)
......
.....B  (Spawn point B)
```

## Configuration Options

### Spawn Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `visitorPrefab` | VisitorController | None | The visitor prefab to spawn |
| `totalVisitors` | int | 10 | Total number of visitors to spawn |
| `spawnInterval` | float | 1.5 | Time in seconds between spawns |

### UI Settings

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `uiCanvas` | Canvas | Auto | Canvas for UI (auto-creates if null) |
| `visitorsText` | TextMeshProUGUI | Auto | Text showing visitor count |
| `countdownText` | TextMeshProUGUI | Auto | Text showing countdown |
| `visitorsFontSize` | int | 20 | Font size for visitor count |
| `countdownFontSize` | int | 16 | Font size for countdown |
| `uiTextColor` | Color | White | Color for UI text |

## UI Display

### Auto-Created UI

If you leave UI references empty, WaveController auto-creates:

1. **Canvas** (if none exists)
   - Screen Space Overlay
   - 1920x1080 reference resolution

2. **Wave Info Panel** (top-right corner)
   - Semi-transparent dark background
   - 250x80 pixels

3. **Visitors Text** (top of panel)
   - Shows: `Visitors: {spawned}/{total}`
   - Updates in real-time as visitors spawn

4. **Countdown Text** (bottom of panel)
   - Shows: `Next spawn in: 2.5s`
   - Updates every frame while spawning
   - **Disappears** after final visitor is spawned

### UI Appearance Example

```
┌─────────────────────────┐
│   Visitors: 7/10        │
│                         │
│   Next spawn in: 1.2s   │
└─────────────────────────┘
```

After final spawn:
```
┌─────────────────────────┐
│   Visitors: 10/10       │
│                         │
│                         │
└─────────────────────────┘
```

## Usage

### Automatic Start

By default, WaveController starts spawning automatically when the scene starts.

### Manual Control

You can control spawning via code:

```csharp
// Get reference
WaveController waveController = FindObjectOfType<WaveController>();

// Start spawning
waveController.StartSpawning();

// Stop spawning
waveController.StopSpawning();

// Check status
int spawned = waveController.VisitorsSpawned;
int total = waveController.TotalVisitors;
bool active = waveController.IsSpawning;
float countdown = waveController.TimeUntilNextSpawn;
```

## How It Works

### Spawning Flow

1. **Initialization** (Start)
   - Finds MazeGridBehaviour
   - Validates visitor prefab
   - Creates UI if needed
   - Starts spawning coroutine

2. **Spawn Loop** (Coroutine)
   - For each visitor (0 to totalVisitors):
     - Spawn visitor at random spawn point pair
     - Increment visitor count
     - Wait for spawnInterval seconds
     - Update countdown timer
   - After last visitor: clear countdown, stop spawning

3. **UI Updates** (Update)
   - Visitors Text: `Visitors: {spawned}/{total}`
   - Countdown Text: `Next spawn in: {time}s`
   - Hide countdown when `allVisitorsSpawned == true`

### Visitor Spawning Details

Each visitor is spawned with:
- **Random spawn pair**: Picks 2 different spawn markers (A, B, C, or D)
- **A* pathfinding**: Calculates optimal path from start to destination
- **Initialization**: Calls `visitor.Initialize(GameController.Instance)`
- **Path assignment**: Calls `visitor.SetPath(pathNodes)`
- **Naming**: `Visitor_1`, `Visitor_2`, etc.

## Integration with Existing Systems

### GameController
- Uses `GameController.Instance.TryFindPath()` for pathfinding
- Requires GameController to be present in scene

### MazeGridBehaviour
- Uses `MazeGridBehaviour.GetSpawnPointCount()` to verify spawn markers
- Uses `MazeGridBehaviour.TryGetRandomSpawnPair()` for random spawn selection
- Uses `MazeGridBehaviour.GridToWorld()` for position conversion

### VisitorController
- Instantiates the configured visitor prefab
- Initializes with GameController
- Assigns calculated path

## Differences from WaveSpawner

| Feature | WaveController | WaveSpawner |
|---------|---------------|-------------|
| UI Display | ✅ Integrated | ❌ No UI |
| Countdown Timer | ✅ Yes | ❌ No |
| Auto-start | ✅ Yes | Manual only |
| Wave Support | ❌ Single wave | ✅ Multiple waves |
| Legacy Support | ❌ Spawn markers only | ✅ Entrance/Heart fallback |
| Focus | Simple, UI-focused | Complex, wave-based |

**Use WaveController when**: You need simple spawning with UI feedback
**Use WaveSpawner when**: You need multi-wave gameplay

## Troubleshooting

### No visitors spawning

**Check:**
- Visitor prefab is assigned
- Maze has at least 2 spawn markers (A, B, C, D)
- GameController is in the scene
- MazeGridBehaviour is in the scene
- Console for error messages

### UI not appearing

**Check:**
- UI references are left empty (for auto-creation)
- Canvas is being created in scene hierarchy
- Check Canvas sorting order if using custom canvas

### Countdown not updating

**Check:**
- `isSpawning` is true (check in Inspector during play mode)
- `allVisitorsSpawned` is false
- Update() is being called (component is enabled)

### Visitors spawning at same location

**Check:**
- Multiple spawn markers exist in maze
- Spawn markers are on different tiles
- `TryGetRandomSpawnPair()` is returning different positions

## Example Configurations

### Quick Test (Fast spawning)
```
Total Visitors: 5
Spawn Interval: 0.5
```

### Normal Gameplay
```
Total Visitors: 10
Spawn Interval: 1.5
```

### Slow/Strategic
```
Total Visitors: 20
Spawn Interval: 3.0
```

### Stress Test
```
Total Visitors: 50
Spawn Interval: 0.1
```

## Advanced Customization

### Custom UI Layout

If you want custom UI positioning:

1. Create your own Canvas and UI elements
2. Assign them to the WaveController fields:
   - `uiCanvas`: Your canvas
   - `visitorsText`: Your visitor count text
   - `countdownText`: Your countdown text
3. WaveController will use your UI instead of auto-creating

### Dynamic Configuration

Change settings at runtime:

```csharp
WaveController wc = GetComponent<WaveController>();

// Modify via reflection (if fields were public)
// Or create public setter methods in the script

// For now, set in Inspector before play mode
```

### Extending Functionality

To add features, modify `WaveController.cs`:

```csharp
// Add events
public event System.Action<int> OnVisitorSpawned;
public event System.Action OnAllVisitorsSpawned;

// Call in SpawnVisitorsCoroutine():
OnVisitorSpawned?.Invoke(visitorsSpawned);
OnAllVisitorsSpawned?.Invoke();
```

## Best Practices

1. **Start Simple**: Use default values (10 visitors, 1.5s interval)
2. **Test Maze First**: Ensure spawn markers work before tweaking
3. **Monitor Console**: Watch for pathfinding errors
4. **Adjust Interval**: Based on maze complexity (larger maze = longer interval)
5. **UI Feedback**: Keep countdown visible for player awareness

## Known Limitations

1. **Single Wave Only**: Spawns one batch, then stops
2. **Spawn Markers Required**: No fallback to entrance/heart
3. **No Pause/Resume**: Stopping cancels remaining spawns
4. **Fixed UI Position**: Auto-created UI is top-right only

## Future Enhancements (Optional)

- [ ] Multiple wave support
- [ ] Pause/resume functionality
- [ ] Wave progress bar
- [ ] Sound effects on spawn
- [ ] Configurable UI positioning
- [ ] Spawn point visualization
- [ ] Per-visitor spawn callbacks

## Summary

WaveController is a **simple, focused spawning system** with **integrated UI**:

✅ Easy to set up (just assign visitor prefab)
✅ Auto-creates UI for instant feedback
✅ Real-time countdown display
✅ Clean, self-contained code

Perfect for straightforward visitor spawning with visual feedback!

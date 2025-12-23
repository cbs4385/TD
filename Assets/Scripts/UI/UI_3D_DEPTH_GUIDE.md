# UI Depth Adjustments for 3D Rendering

This guide explains how the UI system has been adapted to work with the 3D maze rendering system while maintaining proper depth perception and camera interaction.

## Overview

The UI system now supports both:
1. **Screen-space overlays** - Traditional HUD elements that stay fixed on screen
2. **World-space canvases** - UI elements that exist in 3D world space (e.g., floating text, indicators)

## Screen-Space UI (HUD)

### Current Implementation

The main HUD elements remain as screen-space overlays, which is the correct approach for 3D games:

- **UIController.cs** - Main game UI with essence display and wave controls
- **PlayerResourcesUIController.cs** - Resource panel showing essence count
- **PlacementUIController.cs** - Placement mode UI
- **DebugUIController.cs** - Debug information overlay

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

## World-Space UI (In-World Elements)

### New Components

Two new components have been added to support world-space UI elements in the 3D environment:

#### Billboard.cs

Makes GameObjects always face the camera. Essential for readability of world-space text and icons.

**Usage:**
```csharp
// Add to any GameObject that should face the camera
var billboard = gameObject.AddComponent<Billboard>();

// Optional: Lock Y-axis to prevent tilting
billboard.lockYAxis = true;
```

**Features:**
- Auto-detects Camera.main if no camera specified
- Optional Y-axis locking to prevent vertical tilting
- Runs in LateUpdate to ensure smooth following after camera movement
- Can reverse facing direction if needed

**Common Use Cases:**
- Floating damage numbers
- Name plates above visitors
- Quest markers
- Prop status indicators

#### WorldSpaceCanvasHelper.cs

Automatically configures Canvas components for world-space rendering with proper scaling and billboarding.

**Usage:**
```csharp
// Add to a Canvas GameObject
var canvasHelper = canvas.gameObject.AddComponent<WorldSpaceCanvasHelper>();

// It will automatically:
// - Set RenderMode to WorldSpace
// - Assign the world camera
// - Apply appropriate scaling
// - Add billboarding behavior
```

**Configuration:**
- `canvasScale` - Controls pixel density (default: 0.01)
- `enableBillboarding` - Auto-face camera (default: true)
- `lockYAxis` - Prevent vertical tilting (default: true)
- `sortOrder` - Canvas rendering order

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

### When to Use World-Space Canvas

Use for:
- Floating text above game objects
- Interactive in-world buttons
- Prop status indicators
- Visitor name plates
- Damage numbers

### Billboard Considerations

1. **Always lock Y-axis** for top-down games to prevent vertical tilting
2. **Use in LateUpdate** to avoid jitter (Billboard component does this automatically)
3. **Keep text minimal** - world-space text can clutter the 3D scene
4. **Scale appropriately** - use WorldSpaceCanvasHelper's `canvasScale` parameter

## Migration Notes

### Existing Systems

No changes required for existing UI systems:
- ✅ UIController - Already uses ScreenSpaceOverlay
- ✅ PlayerResourcesUIController - Already uses ScreenSpaceOverlay
- ✅ PlacementUIController - Already uses ScreenSpaceOverlay
- ✅ Essence events - Event-driven, camera-agnostic

### New World-Space UI

When adding new world-space UI:

1. Create a Canvas GameObject in the scene
2. Add `WorldSpaceCanvasHelper` component
3. Configure scale and billboard settings
4. Add UI elements as children (TextMeshPro, Images, etc.)

```csharp
// Example: Floating damage number
GameObject damageCanvas = new GameObject("DamageIndicator");
Canvas canvas = damageCanvas.AddComponent<Canvas>();
WorldSpaceCanvasHelper helper = damageCanvas.AddComponent<WorldSpaceCanvasHelper>();
helper.canvasScale = 0.005f; // Smaller for damage numbers

TextMeshProUGUI text = CreateTextChild(damageCanvas);
text.text = "-10";
text.color = Color.red;
```

## Technical Details

### Z-Plane Considerations

The maze operates in the XY plane with Z used for depth:
- Maze tiles: Z varies by height
- Camera looks down at angle onto XY plane
- Focus point always has Z=0 (stays on primary plane)
- World-space UI should offset slightly toward camera (negative Z)

### Camera-World Coordinate Mapping

The 3D camera system converts XY movement to 3D space:
- X movement → World X (left/right)
- Y movement → World Y (up/down in plane, not altitude)
- Z position → Altitude/depth (camera distance from focus)

### Performance Considerations

Billboard components run `LateUpdate` on every frame. For performance:
- Minimize the number of billboarded objects
- Pool and reuse billboard canvases for frequent elements (damage numbers, particles)
- Disable billboarding when objects are off-screen
- Use LOD techniques for distant world-space UI

## Example: Creating a Visitor Health Bar

```csharp
public class VisitorHealthBarUI : MonoBehaviour
{
    private Canvas canvas;
    private Image healthFillImage;

    void Start()
    {
        // Create world-space canvas
        GameObject canvasObj = new GameObject("HealthBar");
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = new Vector3(0, 1.5f, -0.1f); // Above visitor

        canvas = canvasObj.AddComponent<Canvas>();
        WorldSpaceCanvasHelper helper = canvasObj.AddComponent<WorldSpaceCanvasHelper>();
        helper.canvasScale = 0.01f;

        // Create health bar background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = Color.black;

        // Create health bar fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(bgObj.transform, false);
        healthFillImage = fillObj.AddComponent<Image>();
        healthFillImage.color = Color.green;
    }

    public void UpdateHealth(float healthPercent)
    {
        healthFillImage.fillAmount = healthPercent;
    }
}
```

## Future Enhancements

Potential additions for advanced 3D UI:
- World-space particle effects for visual feedback
- 3D icon billboards for quest markers
- Dynamic LOD system for world-space text
- Occlusion detection to hide UI behind walls
- Smooth fade-in/out based on camera distance

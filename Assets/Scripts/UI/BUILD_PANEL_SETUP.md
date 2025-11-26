# Build Panel UI Setup Guide

## Overview

The **Build Panel** provides UI buttons for selecting which placeable item (FaeLantern or FairyRing) to place with left-click. This guide shows how to set up the UI hierarchy in Unity Editor.

## Prerequisites

✅ **PropPlacementController** configured with PlaceableItems
✅ **Canvas** exists in the scene (UIRoot or similar)
✅ **PlacementUIController.cs** script exists

## Step-by-Step UI Creation

### 1. Create Build Panel Container

1. In **Hierarchy**, locate the **Canvas** GameObject (usually under UIRoot)
2. Right-click **Canvas** → **UI** → **Panel**
3. Rename it to `BuildPanel`

### 2. Configure Build Panel Position

Select `BuildPanel` in Hierarchy, then in Inspector:

**RectTransform Settings:**
- **Anchor Preset**: Click the square in top-left, hold **Alt+Shift**, click **bottom-left** preset
- **Pos X**: `10`
- **Pos Y**: `10`
- **Width**: `200`
- **Height**: `150`
- **Pivot**: X: `0`, Y: `0` (bottom-left)

**Image Component:**
- **Color**: RGBA `(0.2, 0.2, 0.2, 0.8)` (dark semi-transparent)

### 3. Create Lantern Button

1. Right-click `BuildPanel` → **UI** → **Button - TextMeshPro**
2. Rename to `LanternButton`

**RectTransform:**
- **Anchor**: Stretch horizontally, top
  - Anchor Min: `(0, 1)`
  - Anchor Max: `(1, 1)`
- **Pivot**: `(0.5, 1)` (top-center)
- **Pos X**: `0`
- **Pos Y**: `-10`
- **Left/Right**: `10` each (for padding)
- **Height**: `40`

**Button Text (child object):**
- Select the **Text (TMP)** child of LanternButton
- Set **Text**: `Lantern`
- Set **Font Size**: `18`
- Set **Alignment**: Center (both horizontal and vertical)
- Set **Color**: White

### 4. Create Lantern Cost Label

1. Right-click `BuildPanel` → **UI** → **Text - TextMeshPro**
2. Rename to `LanternCostText`

**RectTransform:**
- **Anchor**: Stretch horizontally
  - Anchor Min: `(0, 1)`
  - Anchor Max: `(1, 1)`
- **Pivot**: `(0.5, 1)`
- **Pos X**: `0`
- **Pos Y**: `-55`
- **Left/Right**: `10`
- **Height**: `20`

**TextMeshPro Settings:**
- **Text**: `20 Essence` (placeholder, will update automatically)
- **Font Size**: `14`
- **Alignment**: Center
- **Color**: RGBA `(0.8, 0.8, 0.8, 1)` (light gray)

### 5. Create FairyRing Button

1. Right-click `BuildPanel` → **UI** → **Button - TextMeshPro**
2. Rename to `FairyRingButton`

**RectTransform:**
- **Anchor**: Stretch horizontally
  - Anchor Min: `(0, 1)`
  - Anchor Max: `(1, 1)`
- **Pivot**: `(0.5, 1)`
- **Pos X**: `0`
- **Pos Y**: `-80`
- **Left/Right**: `10`
- **Height**: `40`

**Button Text:**
- Select the **Text (TMP)** child
- Set **Text**: `Fairy Ring`
- Set **Font Size**: `18`
- Set **Alignment**: Center
- Set **Color**: White

### 6. Create FairyRing Cost Label

1. Right-click `BuildPanel` → **UI** → **Text - TextMeshPro**
2. Rename to `FairyRingCostText`

**RectTransform:**
- **Anchor**: Stretch horizontally
  - Anchor Min: `(0, 1)`
  - Anchor Max: `(1, 1)`
- **Pivot**: `(0.5, 1)`
- **Pos X**: `0`
- **Pos Y**: `-125`
- **Left/Right**: `10`
- **Height**: `20`

**TextMeshPro Settings:**
- **Text**: `15 Essence` (placeholder)
- **Font Size**: `14`
- **Alignment**: Center
- **Color**: Light gray `(0.8, 0.8, 0.8, 1)`

### 7. Add PlacementUIController Script

1. Select `BuildPanel` in Hierarchy
2. In Inspector, click **Add Component**
3. Search for `PlacementUIController`
4. Click to add it

### 8. Wire Up References

With `BuildPanel` selected, find the **PlacementUIController** component in Inspector:

**Item Buttons:**
- **Lantern Button**: Drag `LanternButton` from Hierarchy
- **Fairy Ring Button**: Drag `FairyRingButton` from Hierarchy

**Cost Labels:**
- **Lantern Cost Text**: Drag `LanternCostText` from Hierarchy
- **Fairy Ring Cost Text**: Drag `FairyRingCostText` from Hierarchy

**References:**
- **Prop Placement Controller**: Drag the GameObject with PropPlacementController component

**Visual Settings** (optional customization):
- **Selected Color**: White `(1, 1, 1, 1)` (default)
- **Deselected Color**: Faded white `(1, 1, 1, 0.6)` (default)

### 9. Final Hierarchy

Your Hierarchy should look like this:

```
Canvas
└── BuildPanel (PlacementUIController)
    ├── LanternButton (Button)
    │   └── Text (TMP)
    ├── LanternCostText (TextMeshProUGUI)
    ├── FairyRingButton (Button)
    │   └── Text (TMP)
    └── FairyRingCostText (TextMeshProUGUI)
```

## Expected Behavior

### On Start
- Cost labels update automatically from PropPlacementController data
  - `LanternCostText` shows FaeLantern essence cost
  - `FairyRingCostText` shows FairyRing essence cost
- Default selection visual applied (usually FaeLantern)
- Console logs: `"PlacementUIController: Found PropPlacementController"`

### During Play
- **Click "Lantern" button**:
  - PropPlacementController switches to FaeLantern
  - Lantern button brightens (selected color)
  - FairyRing button dims (deselected color)
  - Console: `"PlacementUIController: Selected FaeLantern"`
  - Left-click places FaeLantern

- **Click "Fairy Ring" button**:
  - PropPlacementController switches to FairyRing
  - FairyRing button brightens
  - Lantern button dims
  - Console: `"PlacementUIController: Selected FairyRing"`
  - Left-click places FairyRing

### Visual Feedback
- **Selected button**: Full opacity white
- **Unselected button**: 60% opacity (faded)
- Hovering/clicking uses Unity's default button color states

## Optional Enhancements

### Add Icons to Buttons

1. **Prepare Icon Sprites**:
   - Import lantern and fairy ring icons as sprites
   - Set texture type to **Sprite (2D and UI)**

2. **Add Image Component**:
   - Right-click button → **UI** → **Image**
   - Rename to `Icon`
   - Assign sprite
   - Position to left of text

3. **Adjust Text Layout**:
   - Move text to right side
   - Or use Horizontal Layout Group

### Add Panel Title

1. Right-click `BuildPanel` → **UI** → **Text - TextMeshPro**
2. Rename to `TitleText`
3. Position at top: Pos Y: `-5`, Height: `25`
4. Set text: `"BUILD"`
5. Font size: `20`, Bold, Center aligned

### Add Keyboard Shortcuts

In **PlacementUIController.cs**, add to `Update()`:

```csharp
private void Update()
{
    if (Keyboard.current.digit1Key.wasPressedThisFrame)
    {
        OnLanternButtonClicked();
    }

    if (Keyboard.current.digit2Key.wasPressedThisFrame)
    {
        OnFairyRingButtonClicked();
    }
}
```

### Disable Buttons When Unaffordable

Add to PlacementUIController:

```csharp
private void Update()
{
    RefreshButtonInteractivity();
}

private void RefreshButtonInteractivity()
{
    if (GameController.Instance == null) return;

    int currentEssence = GameController.Instance.CurrentEssence;

    var lanternItem = propPlacementController.GetItemById("FaeLantern");
    if (lanternItem != null)
    {
        lanternButton.interactable = (currentEssence >= lanternItem.essenceCost);
    }

    var ringItem = propPlacementController.GetItemById("FairyRing");
    if (ringItem != null)
    {
        fairyRingButton.interactable = (currentEssence >= ringItem.essenceCost);
    }
}
```

This will gray out buttons when the player can't afford them.

### Add Tooltip Hover

Use Unity's EventTrigger component:
1. Add **Event Trigger** to buttons
2. Add **Pointer Enter** event
3. Show tooltip with item description
4. Add **Pointer Exit** event
5. Hide tooltip

## Troubleshooting

### "PropPlacementController not found!"
- ✅ Ensure PropPlacementController is attached to a GameObject in the scene
- ✅ Or manually drag the GameObject to PlacementUIController's reference field

### Buttons don't highlight selection
- ✅ Check that buttons are assigned in Inspector
- ✅ Verify Selected Color and Deselected Color are different
- ✅ Look in Console for "Selected FaeLantern/FairyRing" messages

### Cost labels show "? Essence"
- ✅ PropPlacementController must have PlaceableItems configured
- ✅ Item IDs must match exactly: "FaeLantern" and "FairyRing"
- ✅ Check Console for any errors

### Clicking buttons doesn't change placement
- ✅ Verify PropPlacementController reference is assigned
- ✅ Check that PlaceableItems list has both items configured
- ✅ Look for Console logs when clicking

### Panel not visible
- ✅ Check BuildPanel GameObject is active (checkbox in Hierarchy)
- ✅ Verify Canvas is rendering (check Canvas component)
- ✅ Ensure panel is within screen bounds

## Testing Checklist

### Setup Verification
- [ ] BuildPanel exists with all child objects
- [ ] PlacementUIController attached to BuildPanel
- [ ] All references assigned in Inspector
- [ ] PropPlacementController has 2+ PlaceableItems configured

### Functional Testing
- [ ] Enter Play Mode without errors
- [ ] Cost labels show correct essence costs
- [ ] Default selection highlighted (usually Lantern)
- [ ] Click "Lantern" button highlights it
- [ ] Click "Fairy Ring" button highlights it
- [ ] Only one button highlighted at a time
- [ ] Left-click places the selected item type
- [ ] Console shows selection messages

### Visual Testing
- [ ] Panel positioned in bottom-left
- [ ] Buttons readable and properly sized
- [ ] Cost labels visible and formatted correctly
- [ ] Selection highlighting is clear
- [ ] No layout overlapping issues

## Integration with Existing Systems

### PropPlacementController
PlacementUIController calls:
- `SelectItemById(string)` - Switch active item
- `GetCurrentSelection()` - Query default selection
- `GetItemById(string)` - Get item data for costs

### GameController (Optional Future)
Can query `CurrentEssence` to:
- Disable unaffordable buttons
- Show visual feedback for insufficient essence
- Update costs in real-time

### UIController
Both can coexist:
- UIController handles essence display
- PlacementUIController handles item selection
- Consider adding essence display to BuildPanel

## Summary

The Build Panel provides a clean UI for selecting placeable items:

1. **Create UI hierarchy** - Panel with buttons and cost labels
2. **Add PlacementUIController** - Handles button clicks
3. **Wire references** - Connect buttons and PropPlacementController
4. **Test in Play Mode** - Verify selection and placement work

The system integrates seamlessly with PropPlacementController's multi-item placement system. Clicking buttons calls `SelectItemById()`, and left-click places the selected item!

## Next Steps

After basic setup:
- [ ] Add item icons for visual clarity
- [ ] Implement keyboard shortcuts (1, 2 keys)
- [ ] Add essence checking to disable unaffordable items
- [ ] Show current essence in build panel
- [ ] Add hover tooltips with item descriptions
- [ ] Create visual cursor preview of selected item

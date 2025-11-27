# Player Resources Panel Setup Guide

This guide will help you set up the ResourcesPanel UI in Unity to display the player's Essence and future resources.

## Overview

The code is already in place:
- ✅ `GameController.cs` - Has `OnEssenceChanged` event and invokes it when essence changes
- ✅ `PlayerResourcesUIController.cs` - Script that listens to events and updates UI
- ❌ **ResourcesPanel UI** - Needs to be created in Unity Editor (this guide)

## Step-by-Step Unity Setup

### 1. Open the FaeMazeScene

1. In Unity, open `Assets/Scenes/FaeMazeScene.unity`
2. In the Hierarchy window, find the **Canvas** (or **UIRoot**)

### 2. Create the ResourcesPanel

1. Right-click on **Canvas** → **UI** → **Panel**
2. Rename it to `ResourcesPanel`
3. In the Inspector, configure the RectTransform:
   - **Anchor Preset**: Top-Left (click the square box, hold Alt+Shift, click top-left)
   - **Pivot**: `(0, 1)` (top-left)
   - **Anchored Position**: `(10, -10)` (10 pixels from top-left)
   - **Size Delta**: `(200, 80)` (or adjust to fit your design)

4. Adjust the Panel's **Image** component (optional styling):
   - **Color**: Semi-transparent (e.g., `#000000` with alpha `128`)
   - **Material**: None

### 3. Create the Essence Row

Inside `ResourcesPanel`:

1. Right-click on `ResourcesPanel` → **Create Empty**
2. Rename it to `EssenceRow`
3. Add a **Horizontal Layout Group** component:
   - Check **Child Force Expand**: Width ✅, Height ✅
   - **Child Alignment**: Middle Left
   - **Spacing**: 10

### 4. Add Essence Label

Inside `EssenceRow`:

1. Right-click on `EssenceRow` → **UI** → **Text - TextMeshPro**
2. Rename it to `EssenceLabel`
3. Configure the TextMeshProUGUI component:
   - **Text**: `Essence:`
   - **Font Size**: 18
   - **Color**: White (or your preference)
   - **Alignment**: Center Left
4. Configure RectTransform:
   - **Width**: 80 (or auto)

### 5. Add Essence Value Text

Inside `EssenceRow`:

1. Right-click on `EssenceRow` → **UI** → **Text - TextMeshPro**
2. Rename it to `EssenceValue`
3. Configure the TextMeshProUGUI component:
   - **Text**: `0` (placeholder)
   - **Font Size**: 18
   - **Font Style**: Bold
   - **Color**: Yellow or Gold (e.g., `#FFD700`)
   - **Alignment**: Center Left
4. Configure RectTransform:
   - **Width**: 60 (or auto)

### 6. Add the PlayerResourcesUIController Component

1. Select `ResourcesPanel` in the Hierarchy
2. In the Inspector, click **Add Component**
3. Search for `PlayerResourcesUIController` and add it
4. Assign the reference:
   - Drag **EssenceValue** (from Hierarchy) into the **Essence Value Text** field

### 7. Optional: Add an Icon

If you want to add an essence icon/image:

1. Inside `EssenceRow`, right-click → **UI** → **Image**
2. Rename it to `EssenceIcon`
3. Drag it above `EssenceLabel` in the Hierarchy (to position it first)
4. Assign your essence icon sprite
5. Configure size (e.g., 24x24 pixels)

### 8. Remove Old Essence Text (if exists)

If there's an old standalone `EssenceText` in your UI:

1. Find it in the Hierarchy (usually under Canvas or UIRoot)
2. **Option A**: Delete it completely
3. **Option B**: Disable it (uncheck the GameObject in Inspector)

**Note**: The old `UpdateEssence()` calls in `GameController` have already been removed from the code.

### 9. Test in Play Mode

1. Press **Play** in Unity
2. Check that the ResourcesPanel shows `Essence: 0` (or your starting value)
3. Start a wave and let visitors reach the Heart
4. Verify that the Essence value **updates automatically**
5. Place a Lantern or Fairy Ring
6. Verify that the Essence **decreases and updates**

## Expected Result

You should see a clean panel in the top-left corner displaying:

```
┌─────────────────┐
│ Essence: 100    │
└─────────────────┘
```

The value should update instantly whenever:
- Visitors reach the Heart (essence increases)
- You place props (essence decreases)

## Future Extensibility

The `PlayerResourcesUIController` is ready for additional resources:

### To add Suspicion:

1. Create a new row `SuspicionRow` under `ResourcesPanel`
2. Add label (`Suspicion:`) and value text
3. Assign the value text to **Suspicion Value Text** in the Inspector
4. When ready, call `playerResourcesUI.SetSuspicion(value)` from your suspicion system

### To add Wave Number:

1. Create a new row `WaveRow` under `ResourcesPanel`
2. Add label (`Wave:`) and value text
3. Assign the value text to **Wave Value Text** in the Inspector
4. When ready, call `playerResourcesUI.SetWave(waveNumber)` from your wave system

## Troubleshooting

### Essence value doesn't update
- Verify `PlayerResourcesUIController` is attached to `ResourcesPanel`
- Check that **Essence Value Text** is assigned in the Inspector
- Make sure `GameController.Instance` exists and is active
- Check the Console for any errors

### Panel not visible
- Check Canvas Render Mode is set to **Screen Space - Overlay** or **Camera**
- Verify the ResourcesPanel is active (checkbox enabled)
- Check the panel's position isn't off-screen
- Ensure Canvas Scaler is properly configured

### Text shows but doesn't format correctly
- Make sure you're using **TextMeshProUGUI** (not legacy Text)
- Check the Layout Group settings on EssenceRow
- Adjust font sizes and widths as needed

## Summary

✅ Event-driven architecture (GameController → PlayerResourcesUIController)
✅ Single source of truth for essence display
✅ Easy to extend with new resources
✅ No polling or Update() loops needed

You're all set! The ResourcesPanel is now ready to use.

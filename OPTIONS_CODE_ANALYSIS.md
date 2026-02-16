# Options System - Code Analysis

## Overview
This document analyzes the options system implementation to identify potential issues.

## Settings Flow

### 1. Saving Settings
**When**: User clicks "Apply" button in Options menu
**File**: `OptionsManager.cs` line 996-1193

```
OnApplyClicked() →
  SaveSettings() →
    Read all UI values →
    Write to GameSettings properties →
    GameSettings.Save() →
      PlayerPrefs.Save()
```

### 2. Loading Settings
**When**: Options menu opens
**File**: `OptionsManager.cs` line 570-690

```
Start() →
  LoadSettings() →
    Read from GameSettings properties →
    Update UI sliders, toggles, dropdowns, etc.
```

### 3. Applying Settings
**When**: Game starts OR user clicks Apply
**File**: `GameController.cs` line 178 AND `OptionsManager.cs` line 999

```
GameController.Start() →
  GameSettings.ApplySettings() →
    ApplyVideoSettings() (fullscreen, resolution, light level) →
    Find and configure SoundManager →
    Find and configure CameraController3D
```

## Verified Correct Implementations

### ✅ Light Level Application
- **Status**: FIXED (Session Jan 30, 2026)
- **Location**: `GameController.cs` line 178
- **Behavior**: Light level is applied when game scene loads via `GameSettings.ApplySettings()`

### ✅ Settings Persistence
- **Storage**: PlayerPrefs (OS-level persistent storage)
- **Save**: Called on Apply button click
- **Load**: Automatic via PlayerPrefs.Get* methods with default fallbacks

### ✅ Difficulty Slider
- **Implementation**: `OptionsManager.cs` lines 515-647
- **Behavior**:
  - Slider shows 3 stops: 0 (EASY), 1 (NORMAL), 2 (HARD)
  - Text shows label instead of number
  - Input field is HIDDEN
  - Maps to spawn interval AND starting essence

### ✅ Starting Essence
- **Implementation**: `OptionsManager.cs` lines 534-540
- **Behavior**:
  - Row is HIDDEN (controlled by difficulty slider)
  - Value automatically syncs with difficulty selection

## Potential Issues

### ⚠️ Issue 1: Resolution Index vs Dimensions
**Severity**: Medium
**Location**: `GameSettings.cs` lines 19-36

The code stores BOTH `ResolutionIndex` and `ResolutionWidth/Height`. Different monitors may have different resolution lists, so the index might not match the intended resolution.

**Mitigation**: The code prefers width/height over index (lines 1034-1046), so this should work correctly.

### ⚠️ Issue 2: Camera Settings Application Timing
**Severity**: Low
**Location**: `GameSettings.cs` lines 1017-1021

Camera settings are applied by finding the CameraController3D and calling `ApplySettingsFromGameSettings()`. If the camera controller hasn't been instantiated yet, settings won't apply.

**Verification Needed**: Test changing camera settings mid-game and verify they apply immediately.

### ⚠️ Issue 3: Audio Settings Application
**Severity**: Low
**Location**: `GameSettings.cs` lines 1009-1014

Audio settings rely on finding the SoundManager in the scene. If it doesn't exist, settings are silently ignored.

**Verification Needed**: Verify SoundManager exists in all game scenes.

### ⚠️ Issue 4: Individual Prop Volume Calculation
**Severity**: Low
**Location**: Prop volumes multiply with master SFX volume

The final volume is calculated as:
```
finalVolume = propVolume * masterSfxVolume * distanceVolume * (isActive ? 1f : 0f)
```

This means setting Lantern Volume to 50% AND SFX Volume to 50% results in 25% final volume (0.5 * 0.5 = 0.25).

**Verification Needed**: Test if this multiplication behavior is intuitive or confusing for players.

## Settings NOT in Options Menu

These settings exist in `GameSettings.cs` but are NOT exposed in the Options UI:

1. **Essence Decay** (lines 198-208)
   - `EssenceDecayEnabled` (default: true)
   - `EssenceDecayRate` (default: 1.0)

2. **Prop Essence Mechanics** (lines 688-710)
   - `RingEssenceDrainPerSecond` (default: 5)
   - `LanternEssenceDrainPerSecond` (default: 5)
   - `LanternEssenceAwardPerSecond` (default: 2)

3. **Heart Powers** (lines 713-757)
   - `HeartwardGraspEssenceCost` (default: 25)
   - `HeartDetectionRadius` (default: 2.5)
   - `HeartwardGraspRadius` (default: 2.5)
   - `DevouringMawRadius` (default: 2.0)
   - `TongueEmergeSpeed` (default: 9)
   - `TongueRetractSpeed` (default: 9)

4. **Visitor Behavior** (lines 759-783)
   - `FrightenedSpeedMultiplier` (default: 1.3)
   - `FrightenedRecoveryChance` (default: 0.25)

5. **Difficulty Scaling** (lines 785-882)
   - `MaxSpawnInterval` (default: 15)
   - `SpawnIntervalGrowthRate` (default: 0.02)
   - `TierMultipliers` (default: "0,1.5,2,3,4,6,8")
   - Various max multipliers and growth rates for speed, penalties, confusion, essence rewards

These are tuning knobs that could be exposed in a "Developer" or "Advanced" options tab if needed.

## UI-Specific Implementation Details

### Slider-Input Field Sync
**Location**: `OptionsManager.cs` lines 1236-1285

Each slider with an input field has bidirectional sync:
1. Slider change → updates input field immediately
2. Input field edit → updates slider on Enter/focus loss
3. Invalid input → reverts to slider value

**Test**: Verify this works for all settings with input fields.

### Key Binding Capture
**Location**: `KeyBindingCapture.cs` + `OptionsManager.cs` lines 1287-1663

Bindings use a Toggle-based system:
1. Click toggle → enters capture mode ("Press any key...")
2. Press any input → binding captured
3. Toggle automatically unchecks

**Test**: Verify key bindings work for all controls, including mouse buttons.

### Screenshot Binding Sync
**Location**: `OptionsManager.cs` lines 1639-1660

Screenshot binding appears in BOTH Video and Controls tabs. When changed, `SyncScreenshotBindings()` updates both instances.

**Test**: Change screenshot key in Video tab → verify Controls tab shows same value, and vice versa.

## Recommendations for Testing

1. **Persistence Test**: For EVERY setting:
   - Change value → Apply → Close game → Relaunch → Verify value persisted

2. **Application Test**: For settings that affect gameplay:
   - Change value → Apply → Return to game → Verify change took effect
   - OR: Change value → Apply → Start new game → Verify change took effect

3. **Edge Case Test**:
   - Set all sliders to minimum values → Verify game is playable
   - Set all sliders to maximum values → Verify game is playable
   - Set conflicting values (e.g., Min Zoom > Max Zoom) → Verify clamping works

4. **Reset Test**:
   - Change multiple settings → Click Reset → Verify all return to defaults
   - Close game → Relaunch → Verify defaults persisted

## Code Quality Assessment

### Strengths
- Centralized settings management via static `GameSettings` class
- PlayerPrefs for persistence (cross-platform compatible)
- Clear separation: UI (OptionsManager) ↔ Data (GameSettings)
- Bidirectional sync for slider/input fields
- Migration system for backwards compatibility

### Weaknesses
- Settings application depends on finding scene objects (SoundManager, CameraController)
  - If objects don't exist, settings fail silently
- No validation messages if settings fail to apply
- Some settings are tuning knobs not exposed in UI (could add Advanced tab)

## Summary

The options system is well-implemented with proper persistence and clear data flow. Main areas to test:

1. **Light Level** - Recently fixed, should apply on game start
2. **Camera Settings** - Verify they apply mid-game when changed
3. **Audio Settings** - Verify SoundManager exists and settings apply
4. **Key Bindings** - Verify toggle-based capture works for all controls
5. **Persistence** - Verify all settings save/load correctly across game restarts

No critical bugs were found in the code review. Testing should focus on runtime behavior and user experience.

# HungryForest - Options Menu Test Plan

This document provides a comprehensive test plan to verify that all options menu selections are accurately reflected in the game.

## Pre-Test Setup

1. **Reset to Defaults**: Before starting tests, go to Options and click "Reset to Defaults" to ensure a clean baseline
2. **Close and Relaunch**: After resetting, close the game and relaunch to verify defaults are applied

## Test Categories

### 1. VIDEO SETTINGS

#### 1.1 Display Settings
- [ ] **Fullscreen Toggle**
  - Set to OFF → Apply → Verify game is in windowed mode
  - Set to ON → Apply → Verify game is in fullscreen
  - Close game → Relaunch → Verify setting persisted

- [ ] **Resolution Dropdown**
  - Select lowest resolution → Apply → Verify window/screen size changes
  - Select highest resolution → Apply → Verify window/screen size changes
  - Close game → Relaunch → Verify setting persisted

#### 1.2 Camera Settings
- [ ] **Field of View (FOV)** (Range: 30-120, Default: 60)
  - Set to 30 → Apply → Start game → Verify camera is very zoomed in
  - Set to 120 → Apply → Start game → Verify camera has wide view
  - Test: Should affect 3D perspective view

- [ ] **Camera Pan Speed** (Range: 1-30, Default: 10)
  - Set to 1 → Apply → In-game: Right-click drag → Verify camera pans slowly
  - Set to 30 → Apply → In-game: Right-click drag → Verify camera pans quickly

- [ ] **Camera Zoom Speed** (Range: 1-20, Default: 5)
  - Set to 1 → Apply → In-game: Scroll wheel → Verify zoom is slow
  - Set to 20 → Apply → In-game: Scroll wheel → Verify zoom is fast

- [ ] **Camera Min Zoom** (Range: 1-10, Default: 3)
  - Set to 10 → Apply → In-game: Scroll in → Verify cannot zoom closer than 10 units

- [ ] **Camera Max Zoom** (Range: 10-50, Default: 20)
  - Set to 10 → Apply → In-game: Scroll out → Verify cannot zoom further than 10 units

- [ ] **Camera Movement Speed** (Range: 0.1-10, Default: 1)
  - Set to 0.1 → Apply → In-game: Press WASD → Verify camera moves slowly
  - Set to 10 → Apply → In-game: Press WASD → Verify camera moves quickly

- [ ] **Light Level** (Range: 0-2, Default: 0.9)
  - Set to 0 → Apply → Start game → Verify scene is very dark
  - Set to 2 → Apply → Start game → Verify scene is very bright
  - **IMPORTANT**: This should apply when game scene loads

#### 1.3 Screenshot Settings
- [ ] **Screenshot Path**
  - Browse button opens folder
  - Custom path can be set
  - Screenshots save to specified location

- [ ] **Screenshot Key** (Default: F12)
  - Change to different key
  - In-game: Press key → Verify screenshot is taken

---

### 2. AUDIO SETTINGS

#### 2.1 Master Volume
- [ ] **SFX Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Verify no sound effects
  - Set to 50% → Apply → In-game: Verify SFX at half volume
  - Set to 100% → Apply → In-game: Verify SFX at full volume

- [ ] **Music Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Verify no music
  - Set to 50% → Apply → In-game: Verify music at half volume
  - Set to 100% → Apply → In-game: Verify music at full volume

#### 2.2 Individual Prop Sounds
**Note**: These volumes multiply with master SFX volume

- [ ] **Lantern Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Near lantern → Verify no lantern sound
  - Set to 50% → Apply → In-game: Near lantern → Verify lantern at half volume

- [ ] **Fairy Ring Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Near fairy ring → Verify no ring sound
  - Set to 50% → Apply → In-game: Near fairy ring → Verify ring at half volume

- [ ] **Pond Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Near pond → Verify no pond sound
  - Set to 50% → Apply → In-game: Near pond → Verify pond at half volume

- [ ] **Sculpt Volume** (Range: 0-100%, Default: 100%)
  - Set to 0% → Apply → In-game: Use sculpt power → Verify no sculpt sound
  - Set to 50% → Apply → In-game: Use sculpt power → Verify sculpt at half volume

---

### 3. GAMEPLAY SETTINGS

#### 3.1 Visitor Settings
- [ ] **Visitor Speed** (Range: 0.5-10, Default: 3)
  - Set to 0.5 → Apply → Start game → Verify visitors move very slowly
  - Set to 10 → Apply → Start game → Verify visitors move quickly

- [ ] **Confusion Enabled** (Default: ON)
  - Set to OFF → Apply → Start game → Verify visitors never get confused
  - Set to ON → Apply → Start game → Verify visitors can get confused

- [ ] **Confusion Chance** (Range: 0-100%, Default: 25%)
  - Requires Confusion Enabled = ON
  - Set to 0% → Apply → Start game → Verify no confusion
  - Set to 100% → Apply → Start game → Verify frequent confusion

- [ ] **Confusion Distance Min** (Range: 1-50, Default: 15)
  - Set to 1 → Apply → Start game → Verify confused visitors pick destinations very close
  - Set to 50 → Apply → Start game → Verify confused visitors pick distant destinations

- [ ] **Confusion Distance Max** (Range: 1-50, Default: 20)
  - Set to 50 → Apply → Start game → Verify confused visitors can pick very distant destinations

#### 3.2 Spawning Settings
- [ ] **Difficulty Slider** (3 stops: EASY, NORMAL, HARD)
  - Set to EASY → Apply → Start game → Verify slow spawn rate, high starting essence (300)
  - Set to NORMAL → Apply → Start game → Verify moderate spawn rate, medium essence (200)
  - Set to HARD → Apply → Start game → Verify fast spawn rate, low essence (100)

- [ ] **Enable Goblin** (Default: ON)
  - Set to OFF → Apply → Start game → Verify RedCap never spawns
  - Set to ON → Apply → Start game → Verify RedCap spawns when essence >= 2x starting

#### 3.3 Game Flow Settings
- [ ] **Auto Start Delay** (Range: 0-10s, Default: 2s)
  - Set to 0 → Apply → Start game → Verify spawning starts immediately
  - Set to 10 → Apply → Start game → Verify spawning starts after 10 seconds

- [ ] **Use Fixed Seed** (Default: OFF)
  - Set to ON + enter seed value (e.g., 12345) → Apply → Start game twice → Verify identical maze layout
  - Set to OFF → Apply → Start game twice → Verify different maze layouts

- [ ] **Show Tutorial** (Default: ON)
  - Set to OFF → Apply → Start game → Verify no tutorial appears
  - Set to ON → Apply → Start game → Verify tutorial appears

#### 3.4 Player Controls
- [ ] **Focus Speed** (Range: 5-15, Default: 10)
  - Set to 5 → Apply → In-game: Press F5/F6/F7 → Verify camera focuses slowly
  - Set to 15 → Apply → In-game: Press F5/F6/F7 → Verify camera focuses quickly

---

### 4. CONTROLS (KEY BINDINGS)

#### 4.1 Heart Power Keys
- [ ] **Heart Power 1** (Default: 1)
  - Change to Q → Apply → In-game: Press Q → Verify power 1 activates
- [ ] **Heart Power 2** (Default: 2)
  - Change to W → Apply → In-game: Press W → Verify power 2 activates
- [ ] **Heart Power 3** (Default: 3)
  - Change to E → Apply → In-game: Press E → Verify power 3 activates
- [ ] **Heart Power 4** (Default: 4)
  - Change to R → Apply → In-game: Press R → Verify sculpt menu opens
- [ ] **Heart Power 5** (Default: 5)
  - Change to T → Apply → In-game: Press T → Verify power 5 activates

#### 4.2 Sculpt Menu Keys (When sculpt radial menu is open)
- [ ] **Sculpt Pond** (Default: Z)
  - Change binding → Apply → In-game: Open sculpt menu → Press new key → Verify pond selected
- [ ] **Sculpt Lantern** (Default: X)
  - Change binding → Apply → In-game: Open sculpt menu → Press new key → Verify lantern selected
- [ ] **Sculpt Ring** (Default: C)
  - Change binding → Apply → In-game: Open sculpt menu → Press new key → Verify ring selected
- [ ] **Sculpt Remove** (Default: V)
  - Change binding → Apply → In-game: Open sculpt menu → Press new key → Verify remove selected

#### 4.3 Camera Movement Keys
- [ ] **Move Forward** (Default: W)
  - Change to Up Arrow → Apply → In-game: Press Up Arrow → Verify camera moves forward
- [ ] **Move Backward** (Default: S)
  - Change to Down Arrow → Apply → In-game: Press Down Arrow → Verify camera moves backward
- [ ] **Turn Left** (Default: Q)
  - Change to different key → Apply → In-game: Press key → Verify camera rotates left
- [ ] **Turn Right** (Default: E)
  - Change to different key → Apply → In-game: Press key → Verify camera rotates right
- [ ] **Strafe Left** (Default: A)
  - Change to different key → Apply → In-game: Press key → Verify camera strafes left
- [ ] **Strafe Right** (Default: D)
  - Change to different key → Apply → In-game: Press key → Verify camera strafes right

#### 4.4 Camera Mouse Controls
- [ ] **Camera Forward** (Default: Mouse0 / Left Click)
  - Change binding → Apply → In-game: Use new binding → Verify focal point placement
- [ ] **Camera Orbit** (Default: Mouse1 / Right Click)
  - Change binding → Apply → In-game: Use new binding → Verify camera orbits
- [ ] **Camera Pan** (Default: Mouse2 / Middle Click)
  - Change binding → Apply → In-game: Use new binding → Verify camera pans

#### 4.5 Camera Focus Shortcuts
- [ ] **Focus Heart** (Default: F5)
  - Change to different key → Apply → In-game: Press key → Verify camera focuses on heart
- [ ] **Focus Entrance** (Default: F6)
  - Change to different key → Apply → In-game: Press key → Verify camera focuses on entrance
- [ ] **Focus Visitor** (Default: F7)
  - Change to different key → Apply → In-game: Press key → Verify camera focuses on next visitor

---

## CRITICAL ISSUES TO CHECK

### Settings Persistence
After changing ANY setting:
1. Apply the setting
2. Close the game completely
3. Relaunch the game
4. Go to Options
5. Verify the setting is still at the value you set

### Settings Application Timing
- [ ] **Light Level applies on game start**: This was recently fixed - verify it works
- [ ] **Camera settings apply mid-game**: Change camera speed in options while game is running → Return to game → Verify change took effect

### Input Field Sync
- [ ] Verify that slider changes update the input field immediately
- [ ] Verify that typing in input field updates the slider when you press Enter
- [ ] Verify that invalid input field values revert to slider value

### Difficulty Interaction
- [ ] Verify difficulty slider controls BOTH spawn interval AND starting essence
- [ ] Verify starting essence row is hidden (difficulty controls it)

---

## KNOWN ISSUES TO VERIFY

Based on code analysis, these are potential issues to check:

1. **Starting Essence**: The Starting Essence slider should be HIDDEN (controlled by difficulty). Verify this row is not visible.

2. **Spawn Interval**: The spawn interval INPUT FIELD should be hidden (difficulty shows label instead). Verify only the label shows ("EASY", "NORMAL", or "HARD").

3. **Light Level Application**: Recently fixed in `GameController.Start()` - verify light level applies when starting a new game.

4. **Key Binding Toggle**: Toggles should be unchecked by default, check to activate capture mode, then auto-uncheck when key is captured.

---

## Test Results Template

```
Date: _______________
Tester: _______________

VIDEO SETTINGS:
- Fullscreen: PASS / FAIL
- Resolution: PASS / FAIL
- FOV: PASS / FAIL
- Camera Speeds: PASS / FAIL
- Light Level: PASS / FAIL

AUDIO SETTINGS:
- Master SFX: PASS / FAIL
- Master Music: PASS / FAIL
- Prop Sounds: PASS / FAIL

GAMEPLAY SETTINGS:
- Visitor Settings: PASS / FAIL
- Difficulty: PASS / FAIL
- Game Flow: PASS / FAIL

CONTROLS:
- Heart Powers: PASS / FAIL
- Sculpt Menu: PASS / FAIL
- Camera Movement: PASS / FAIL
- Camera Mouse: PASS / FAIL

PERSISTENCE:
- Settings save/load: PASS / FAIL

CRITICAL BUGS FOUND:
_________________________________
_________________________________
_________________________________
```

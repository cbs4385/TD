# FaeMaze Roguelike Systems Documentation

This document tracks the implementation of roguelike meta-progression systems for FaeMaze.

**Last Updated**: February 2026
**Status**: Phase 6 Complete - Prop Mutations

---

## Overview

FaeMaze is implementing roguelike meta-progression inspired by games like Hades and The Binding of Isaac. The core loop:

1. **Run**: Play endless survival mode, earn essence, use Heart Powers
2. **Game Over**: Calculate Fae Dust rewards based on performance
3. **Meta-Progression**: Spend Fae Dust to permanently unlock power tiers, blessings, mutations
4. **Next Run**: Start fresh with new unlocks available

### Key Design Philosophy

- **Props are COMPETING RESOURCE FARMERS** - Lanterns, Fairy Rings, and Ponds compete with the player for visitor essence
- **Lantern**: Partial ally (shares essence with player via LanternFascination)
- **Fairy Ring**: Silent thief (drains visitor essence, player gets nothing)
- **Puka/Pond**: Destroyer (drowns visitors, wasting them entirely)
- **RedCap**: Active enemy (kills visitors, costs player essence directly)

---

## Implementation Status

### Phase 1: Core Infrastructure ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| MetaProgressionManager | `MetaProgressionManager.cs` | ✅ | Fae Dust, lifetime stats, PlayerPrefs persistence |
| UnlockManager | `UnlockManager.cs` | ✅ | Unlock definitions, purchase system, prerequisites |
| PowerProgressionManager | `PowerProgressionManager.cs` | ✅ | Run-based tier tracking, upgrade triggers |
| RoguelikeBootstrap | `RoguelikeBootstrap.cs` | ✅ | Auto-instantiation of managers |
| TierUpgradeUI | `TierUpgradeUI.cs` | ✅ | Modal overlay for tier upgrade choices |
| GameController hooks | Modified | ✅ | OnRunStart, essence tracking |
| GameStatsTracker hooks | Modified | ✅ | FinalizeRunStats, meta-progression bridge |
| DifficultyManager hooks | Modified | ✅ | Tier change recording |
| HeartPowerManager hooks | Modified | ✅ | Power activation recording, run tier lookup |

### Phase 2: Unlock Shop UI ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| UnlockShopUI | `UnlockShopUI.cs` | ✅ | Full-screen shop with category tabs |
| MainMenuManager | Modified | ✅ | Shrine button, Fae Dust display |
| GameOverManager | Modified | ✅ | Fae Dust earned display |

**Features:**
- Full-screen "Shrine" shop accessible from main menu
- Category tabs: Powers, Blessings, Forms, Mutations, Challenges
- Scrollable unlock cards with:
  - Name, description, cost
  - Lock/unlock visual states
  - Prerequisite display
  - Purchase button (grayed when locked/unaffordable)
- Fae Dust display in main menu header
- Fae Dust earned shown on game over screen

### Phase 3: Blessings System ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| BlessingDefinition | `BlessingDefinition.cs` | ✅ | ScriptableObject for blessing properties |
| BlessingManager | `BlessingManager.cs` | ✅ | Blessing state, selection, effect queries |
| BlessingSelectionUI | `BlessingSelectionUI.cs` | ✅ | Run-start blessing choice overlay |
| RuntimeSceneSetup | Modified | ✅ | Blessing selection before wave start |
| GameController | Modified | ✅ | Starting essence multiplier, Forest's Favor |
| HeartOfTheMaze | Modified | ✅ | Consumption essence multiplier |
| HeartPowerManager | Modified | ✅ | Power cost modifiers (Desperate Grasp, Vengeful Spirit, Devouring Hunger) |
| VisitorControllerBase | Modified | ✅ | Visitor speed multiplier |
| WaveSpawner | Modified | ✅ | Spawn interval multiplier |
| HeartPowerEffects | Modified | ✅ | Maw speed multiplier |

**Available Blessings:**

| Blessing | Effect | Cost | Prerequisite |
|----------|--------|------|--------------|
| Greedy Heart | +50% essence from consumption, -25% starting essence | 100 | ReachEssence500 |
| Patient Hunter | -15% visitor speed, +20% spawn rate | 100 | Survive10Minutes |
| Desperate Grasp | Yoink! costs 0 when below 25 essence | 75 | None |
| Spreading Corruption | +25% prop effect radius | 100 | None |
| Devouring Hunger | +50% Maw speed, +25% Maw cost | 100 | None |
| Forest's Favor | Start with 1 extra Lantern | 100 | None |
| Vengeful Spirit | -50% power costs when below 50 essence | 125 | None |

**Blessing Selection Flow:**
1. Game scene loads, maze generates
2. BlessingSelectionUI appears (game paused)
3. Player sees 3 random unlocked blessings
4. Player selects one or skips
5. Blessing effects applied, waves begin

### Phase 4: Heart Forms ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| HeartFormDefinition | `HeartFormDefinition.cs` | ✅ | ScriptableObject for form properties |
| HeartFormManager | `HeartFormManager.cs` | ✅ | Form state, selection, effect queries |
| HeartFormSelectionUI | `HeartFormSelectionUI.cs` | ✅ | Run-start form choice overlay |
| RuntimeSceneSetup | Modified | ✅ | Form selection before blessing |
| GameController | Modified | ✅ | Starting essence modifier |
| HeartOfTheMaze | Modified | ✅ | Tongue speed multiplier, essence reward multiplier |
| VisitorControllerBase | Modified | ✅ | Lantern effectiveness multiplier |
| DynamicMazeGrowth | Modified | ✅ | Hazard spawn rate multiplier |
| HeartPowerEffects | Modified | ✅ | Maw essence reward multiplier |

**Available Heart Forms:**

| Form | Bonus | Drawback | Cost |
|------|-------|----------|------|
| Hungry Heart | None (balanced) | None | Free |
| Ravenous Heart | +50% tongue speed | -20 starting threads | 150 Dust |
| Patient Heart | +30% lantern yield | +25% hazard spawn rate | 200 Dust |
| Famished Heart | +50% thread rewards | -2/sec thread decay | 250 Dust |

**Form Selection Flow:**
1. Game scene loads, maze generates
2. HeartFormSelectionUI appears (if multiple forms unlocked)
3. Player selects a form
4. Form effects applied
5. BlessingSelectionUI appears (existing flow)

### Phase 5: Challenge Modifiers ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| ChallengeModifierDefinition | `ChallengeModifierDefinition.cs` | ✅ | ScriptableObject for challenge properties |
| ChallengeModifierManager | `ChallengeModifierManager.cs` | ✅ | Challenge state, multi-selection, effect queries |
| ChallengeSelectionUI | `ChallengeSelectionUI.cs` | ✅ | Run-start challenge selection overlay (multi-select) |
| RuntimeSceneSetup | Modified | ✅ | Challenge selection after form, before mutation |
| WaveSpawner | Modified | ✅ | Spawn interval multiplier (Endless Tide) |
| HeartPowerManager | Modified | ✅ | Power cost multiplier (Frugal Heart) |
| MetaProgressionManager | Modified | ✅ | Fae Dust multiplier from challenges |
| VisitorControllerBase | Modified | ✅ | Elite visitor support (ChampionVisitors) |

**Available Challenges:**

| Challenge | Effect | Dust Multiplier | Cost |
|-----------|--------|-----------------|------|
| Frugal Heart | Powers cost 50% more | 1.25x | 50 |
| Endless Tide | Visitors arrive 2x faster | 1.5x | 50 |
| Blind Faith | Visitor state indicators hidden | 1.25x | 50 |
| Essence Drought | Hazards don't share harvest | 1.5x | 50 |
| Champion Visitors | 10% elites (2x stats, 3x reward) | 1.75x | 75 |

**Challenge Selection Flow:**
1. Game scene loads, maze generates
2. HeartFormSelectionUI appears (if multiple forms unlocked)
3. ChallengeSelectionUI appears (game paused)
4. Player can select MULTIPLE challenges (toggle behavior)
5. Combined Fae Dust multiplier shown
6. Confirm or skip selection
7. PropMutationSelectionUI appears next

### Phase 6: Prop Mutations ✅ COMPLETE

| Component | File | Status | Description |
|-----------|------|--------|-------------|
| PropMutationDefinition | `PropMutationDefinition.cs` | ✅ | ScriptableObject for mutation properties |
| PropMutationManager | `PropMutationManager.cs` | ✅ | Mutation state, selection, effect queries |
| PropMutationSelectionUI | `PropMutationSelectionUI.cs` | ✅ | Run-start mutation selection overlay |
| RuntimeSceneSetup | Modified | ✅ | Mutation selection after challenge, before blessing |
| VisitorControllerBase | Modified | ✅ | Lantern multiplier (Greedy Glow), Ring Tithe mechanic |
| FairyRing | Modified | ✅ | Ring Tithe essence sharing |
| PukaHazard | Modified | ✅ | Puka's Portion essence sharing |
| GameController | Modified | ✅ | RingTithe and PukaGift essence sources |

**Available Mutations:**

| Mutation | Effect | Cost |
|----------|--------|------|
| Greedy Glow | Lanterns yield 50% more essence | 100 |
| Ring Tithe | Player receives 50% of ring-drained essence | 100 |
| Puka's Portion | Player receives 50% of drowned visitor essence | 100 |

**Mutation Selection Flow:**
1. After ChallengeSelectionUI (if any)
2. PropMutationSelectionUI appears (if multiple mutations unlocked)
3. Player selects ONE mutation (single selection)
4. Mutation effects applied
5. BlessingSelectionUI appears next

---

## File Reference

### Core Roguelike Files (`Assets/Scripts/Roguelike/`)

| File | Purpose | Key Classes |
|------|---------|-------------|
| `MetaProgressionManager.cs` | Fae Dust currency, lifetime stats | `MetaProgressionManager`, `LifetimeStats`, `CurrentRunStats` |
| `UnlockManager.cs` | Unlock state management | `UnlockManager`, `UnlockDefinition`, `UnlockCategory` |
| `PowerProgressionManager.cs` | Run-based power tiers | `PowerProgressionManager` |
| `BlessingDefinition.cs` | Blessing properties | `BlessingDefinition`, `BlessingType` |
| `BlessingManager.cs` | Blessing selection and effects | `BlessingManager` |
| `HeartFormDefinition.cs` | Heart Form properties | `HeartFormDefinition`, `HeartFormType` |
| `HeartFormManager.cs` | Heart Form selection and effects | `HeartFormManager` |
| `ChallengeModifierDefinition.cs` | Challenge properties | `ChallengeModifierDefinition`, `ChallengeModifierType` |
| `ChallengeModifierManager.cs` | Challenge selection and effects | `ChallengeModifierManager` |
| `PropMutationDefinition.cs` | Mutation properties | `PropMutationDefinition`, `PropMutationType` |
| `PropMutationManager.cs` | Mutation selection and effects | `PropMutationManager` |
| `RoguelikeBootstrap.cs` | Manager instantiation | `RoguelikeBootstrap` |

### UI Files (`Assets/Scripts/UI/`)

| File | Purpose |
|------|---------|
| `TierUpgradeUI.cs` | Tier upgrade modal overlay |
| `UnlockShopUI.cs` | Unlock shop with category tabs |
| `BlessingSelectionUI.cs` | Run-start blessing selection |
| `HeartFormSelectionUI.cs` | Run-start heart form selection |
| `ChallengeSelectionUI.cs` | Run-start challenge selection (multi-select) |
| `PropMutationSelectionUI.cs` | Run-start mutation selection |
| `MainMenuManager.cs` | Modified - Shrine button, Fae Dust display |
| `GameOverManager.cs` | Modified - Fae Dust earned display |

### Modified System Files

| File | Changes Made |
|------|--------------|
| `GameController.cs` | OnRunStart, BlessingManager hooks, Forest's Favor lanterns, starting essence multiplier, heart form modifier, RingTithe/PukaGift essence sources |
| `GameStatsTracker.cs` | NotifyMetaProgression, FinalizeRunStats |
| `DifficultyManager.cs` | RecordDifficultyTier call |
| `HeartPowerManager.cs` | GetPowerDefinition uses PowerProgressionManager, GetEffectivePowerCost for blessing modifiers, challenge power cost multiplier |
| `HeartOfTheMaze.cs` | Consumption essence multiplier, tongue speed multiplier from form |
| `VisitorControllerBase.cs` | Visitor speed multiplier from blessing, lantern effectiveness from form, elite visitor support, lantern mutation multiplier, ring tithe mechanic |
| `DynamicMazeGrowth.cs` | Hazard spawn rate multiplier from form |
| `HeartPowerEffects.cs` | Maw essence reward multiplier from form |
| `RuntimeSceneSetup.cs` | Run-start selection order: Form → Challenge → Mutation → Blessing → Wave Start |
| `WaveSpawner.cs` | Spawn interval multiplier from blessing and challenges, elite visitor spawning |
| `HeartPowerEffects.cs` | DevouringMaw speed/duration multipliers |
| `DynamicMazeGrowth.cs` | HasPropAtNode, GetPropTypeAtNode helpers |
| `MetaProgressionManager.cs` | Challenge Fae Dust multiplier |
| `PukaHazard.cs` | Puka's Portion mutation essence sharing |

---

## Data Flow

### Run Start
```
RuntimeSceneSetup.StartAfterDelay()
  └─> Wait for maze growth
  └─> ShowHeartFormSelection()
       └─> HeartFormSelectionUI.Show() (if multiple forms unlocked)
       └─> Player selects form
       └─> HeartFormManager.SelectFormForRun()
  └─> ShowChallengeSelection()
       └─> ChallengeSelectionUI.Show() (if challenges unlocked)
       └─> Player selects multiple challenges (toggle)
       └─> ChallengeModifierManager.SetChallengesForRun()
  └─> ShowMutationSelection()
       └─> PropMutationSelectionUI.Show() (if mutations unlocked)
       └─> Player selects one mutation
       └─> PropMutationManager.SelectMutationForRun()
  └─> ShowBlessingSelection()
       └─> BlessingSelectionUI.Show()
       └─> Player selects blessing
       └─> BlessingManager.SelectBlessingForRun()
  └─> WaveSpawner.StartWave()

GameController.Start()
  └─> MetaProgressionManager.OnRunStart()
       └─> Reset run stats
       └─> Check daily bonus
  └─> BlessingManager.OnRunStart()
  └─> HeartFormManager.OnRunStart()
  └─> ChallengeModifierManager.OnRunStart()
  └─> PropMutationManager.OnRunStart()
  └─> PowerProgressionManager.ResetRunTiers()
       └─> All powers → Tier I
  └─> ApplyForestsFavorBlessing()
       └─> Place extra lanterns if blessing active
  └─> Apply form starting essence modifier
```

### During Gameplay
```
Essence Change (GameController.AddEssence)
  └─> MetaProgressionManager.RecordEssence()
  └─> DifficultyManager.OnEssenceChanged()
       └─> MetaProgressionManager.RecordDifficultyTier()
  └─> PowerProgressionManager.OnEssenceChanged()
       └─> Check threshold crossings
       └─> Trigger TierUpgradeUI if upgrade available

Visitor Spawn (WaveSpawner.SpawnVisitor)
  └─> Apply BlessingManager.GetSpawnIntervalMultiplier()
  └─> Apply ChallengeModifierManager.GetSpawnIntervalMultiplier()
  └─> Check ChallengeModifierManager.GetEliteSpawnChance()
       └─> If elite, call visitor.SetElite(stats, reward)
  └─> Visitor.ApplyDifficultyScaling()
       └─> Apply BlessingManager.GetVisitorSpeedMultiplier()

Power Activation (HeartPowerManager.TryActivatePower)
  └─> GetEffectivePowerCost()
       └─> Check Desperate Grasp (HeartwardGrasp free below threshold)
       └─> Check Devouring Hunger (Maw costs more)
       └─> Check Vengeful Spirit (all powers cheaper below threshold)
       └─> Apply ChallengeModifierManager.GetPowerCostMultiplier()
  └─> MetaProgressionManager.RecordPowerActivation()

Visitor Consumption (HeartOfTheMaze.OnVisitorConsumed)
  └─> Apply BlessingManager.GetEssenceFromConsumptionMultiplier()
  └─> GameController.AddEssence()

Lantern Fascination (VisitorControllerBase.OnLanternFascinationComplete)
  └─> Apply PropMutationManager.GetLanternAwardMultiplier()
  └─> GameController.AddEssence(EssenceSource.LanternFascination)

Fairy Ring Drain (FairyRing.DrainEssence)
  └─> Check PropMutationManager.GetRingEssenceTithe()
  └─> If tithe > 0, GameController.AddEssence(EssenceSource.RingTithe)

Puka Drowning (PukaHazard.DrownVisitorCoroutine)
  └─> Check PropMutationManager.GetPukaEssenceShare()
  └─> If share > 0, GameController.AddEssence(EssenceSource.PukaGift)
```

### Game Over
```
GameOverManager.Start()
  └─> GameStatsTracker.FinalizeRunStats()
       └─> MetaProgressionManager.RecordRunDuration()
       └─> MetaProgressionManager.RecordEssence()
       └─> MetaProgressionManager.OnRunEnd()
            └─> Calculate Fae Dust rewards
            └─> Apply ChallengeModifierManager.GetFaeDustMultiplier()
            └─> Update lifetime stats
            └─> Save to PlayerPrefs
  └─> BlessingManager.OnRunEnd()
       └─> Clear active blessing
  └─> ChallengeModifierManager.OnRunEnd()
       └─> Clear active challenges
  └─> PropMutationManager.OnRunEnd()
       └─> Clear active mutation
```

---

## Fae Dust Economy

### Earning Rates (per run)

| Source | Amount | Notes |
|--------|--------|-------|
| Heart Consume | 1 | Per visitor consumed by Heart tongue |
| Maw Devour | 2 | Per visitor devoured by Maw power |
| Prop Drain | 1 | Per visitor drained at props (with mutation) |
| Reach Tier 3 | 10 | One-time per run |
| Reach Tier 5 | 25 | One-time per run |
| Reach Tier 7 | 50 | One-time per run |
| Survive 5 min | 5 | One-time per run |
| Survive 10 min | 15 | One-time per run |
| First run of day | 10 | Daily bonus |

### Spending Costs (unlock shop)

| Category | Item | Cost | Prerequisites |
|----------|------|------|---------------|
| Power Tier | Any T2 | 150 | None |
| Power Tier | Any T3 | 300 | T2 unlocked |
| Blessing | GreedyHeart | 100 | Achievement: ReachEssence500 |
| Blessing | PatientHunter | 100 | Achievement: Survive10Minutes |
| Blessing | DesperateGrasp | 75 | None |
| Blessing | SpreadingCorruption | 100 | None |
| Blessing | DevouringHunger | 100 | None |
| Blessing | ForestsFavor | 100 | None |
| Blessing | VengefulSpirit | 125 | None |
| Heart Form | RavenousHeart | 150 | None |
| Heart Form | PatientHeart | 200 | None |
| Heart Form | StarvingHeart | 250 | None |
| Mutation | GreedyGlow | 100 | None |
| Mutation | RingTithe | 100 | None |
| Mutation | DrowningGift | 100 | None |
| Challenge | FrugalHeart | 50 | None |
| Challenge | EndlessTide | 50 | None |
| Challenge | ChampionVisitors | 75 | None |

---

## Power Tier Upgrade Thresholds

Tier upgrades are offered when essence crosses these thresholds (relative to starting essence):

| Tier | Thresholds | Notes |
|------|------------|-------|
| Tier II | 1.5x, 3.0x | Two chances to get a Tier II upgrade |
| Tier III | 5.0x, 8.0x | Two chances to get a Tier III upgrade |

When a threshold is crossed:
1. TierUpgradeUI appears (game pauses)
2. Player chooses which power to upgrade
3. Only powers with that tier permanently unlocked are shown
4. Player can skip the upgrade

---

## PlayerPrefs Keys

| Key Pattern | Purpose |
|-------------|---------|
| `FaeMaze_FaeDust` | Current Fae Dust amount |
| `FaeMaze_Lifetime_*` | Lifetime statistics |
| `FaeMaze_Unlock_*` | Unlock states |
| `FaeMaze_Achievement_*` | Achievement completion |
| `FaeMaze_Blessing_*` | Blessing unlock states |
| `FaeMaze_HeartForm_*` | Heart Form unlock states |
| `FaeMaze_Challenge_*` | Challenge unlock states |
| `FaeMaze_Mutation_*` | Mutation unlock states |
| `FaeMaze_TotalRuns` | Total run count |
| `FaeMaze_LastDailyRun` | Date of last daily bonus |

---

## Testing

### Debug Commands (Context Menu)

**MetaProgressionManager**:
- `Debug: Add 100 Fae Dust` - Adds test currency
- `Debug: Log Stats` - Prints current stats
- `Reset All Progress` - Clears all data (use with caution)

**UnlockManager**:
- `Debug: Log All Unlocks` - Shows all unlock states
- `Debug: Unlock All` - Unlocks everything
- `Reset All Unlocks` - Clears unlock state

**PowerProgressionManager**:
- `Debug: Log Run Tiers` - Shows current run tier states
- `Debug: Force Tier 2 Upgrade` - Triggers T2 upgrade UI
- `Debug: Force Tier 3 Upgrade` - Triggers T3 upgrade UI

**BlessingManager**:
- `Debug: Unlock All Blessings` - Unlocks all blessing types
- `Debug: Log Blessing State` - Shows current blessing state
- `Reset All Blessing Unlocks` - Clears blessing unlock state

**HeartFormManager**:
- `Debug: Unlock All Forms` - Unlocks all heart forms
- `Debug: Log Form State` - Shows current form state
- `Reset All Form Unlocks` - Clears form unlock state

**ChallengeModifierManager**:
- `Debug: Unlock All Challenges` - Unlocks all challenge types
- `Debug: Log Challenge State` - Shows active challenges and multipliers
- `Reset All Challenge Unlocks` - Clears challenge unlock state

**PropMutationManager**:
- `Debug: Unlock All Mutations` - Unlocks all mutation types
- `Debug: Log Mutation State` - Shows active mutation
- `Reset All Mutation Unlocks` - Clears mutation unlock state

---

## Future Work

### Completed Features
1. ~~**Unlock Shop UI** - Main menu screen to spend Fae Dust~~ ✅
2. ~~**Game Over Fae Dust Display** - Show earned dust on game over screen~~ ✅
3. ~~**Blessing Selection UI** - Pre-run blessing choice~~ ✅
4. ~~**Blessing Effects** - Implement blessing modifiers~~ ✅
5. ~~**Heart Form Selection** - Pre-run form choice~~ ✅
6. ~~**Challenge Modifiers** - Risk/reward system with Fae Dust multipliers~~ ✅
7. ~~**Prop Mutations** - Mutation effects for props~~ ✅

### Medium Priority
8. **Power Tier Visual Feedback** - Show tier level on power UI
9. **Challenge Visual Effects** - Elite visitor visual indicators
10. **Blind Faith Challenge** - Hide visitor state indicators

### Lower Priority
11. **Achievements** - Track milestones
12. **Cosmetic Unlocks** - Visual customization
13. **Additional Challenges** - More challenge variety
14. **Additional Mutations** - More mutation variety

---

## Design Document Reference

The full roguelike design document is at:
`C:\Users\chris\.claude\plans\graceful-whistling-axolotl.md`

This contains:
- Detailed blessing descriptions
- Heart form specifications
- Mutation mechanics
- Achievement definitions
- Synergy system ideas
- Visitor variant concepts

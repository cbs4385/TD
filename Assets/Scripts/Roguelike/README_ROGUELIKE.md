# FaeMaze Roguelike Systems Documentation

This document tracks the implementation of roguelike meta-progression systems for FaeMaze.

**Last Updated**: February 2026
**Status**: Phase 1 Complete - Core Infrastructure

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

### Phase 2: Unlock Shop UI 🔲 NOT STARTED

- Main menu "Shrine" or upgrade shop screen
- Category tabs (Powers, Blessings, Heart Forms, Mutations)
- Fae Dust display and spending
- Lock/unlock visual states

### Phase 3: Blessings System 🔲 NOT STARTED

- Run-start blessing selection UI
- Blessing effects implementation
- Blessing unlock progression

### Phase 4: Heart Forms 🔲 NOT STARTED

- Character/form selection at run start
- Stat modifiers per form
- Visual differences (optional)

### Phase 5: Challenge Modifiers 🔲 NOT STARTED

- Challenge selection UI
- Modifier effects implementation
- Fae Dust multiplier system

### Phase 6: Prop Mutations 🔲 NOT STARTED

- Mutation effects on prop behavior
- Essence sharing/tithe mechanics
- Mutation selection system

---

## File Reference

### Core Roguelike Files (`Assets/Scripts/Roguelike/`)

| File | Purpose | Key Classes |
|------|---------|-------------|
| `MetaProgressionManager.cs` | Fae Dust currency, lifetime stats | `MetaProgressionManager`, `LifetimeStats`, `CurrentRunStats` |
| `UnlockManager.cs` | Unlock state management | `UnlockManager`, `UnlockDefinition`, `UnlockCategory` |
| `PowerProgressionManager.cs` | Run-based power tiers | `PowerProgressionManager` |
| `RoguelikeBootstrap.cs` | Manager instantiation | `RoguelikeBootstrap` |

### UI Files (`Assets/Scripts/UI/`)

| File | Purpose |
|------|---------|
| `TierUpgradeUI.cs` | Tier upgrade modal overlay |

### Modified System Files

| File | Changes Made |
|------|--------------|
| `GameController.cs` | Added `OnRunStart()`, `ResetRunTiers()`, `RecordEssence()` calls |
| `GameStatsTracker.cs` | Added `NotifyMetaProgression()`, `FinalizeRunStats()` |
| `DifficultyManager.cs` | Added `RecordDifficultyTier()` call |
| `HeartPowerManager.cs` | Modified `GetPowerDefinition()` to use PowerProgressionManager |

---

## Data Flow

### Run Start
```
GameController.Start()
  └─> MetaProgressionManager.OnRunStart()
       └─> Reset run stats
       └─> Check daily bonus
  └─> PowerProgressionManager.ResetRunTiers()
       └─> All powers → Tier I
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

Visitor Fate (GameStatsTracker.RecordVisitorFate)
  └─> NotifyMetaProgression()
       └─> MetaProgressionManager.RecordHeartConsume/RecordMawDevour/etc.

Power Activation (HeartPowerManager.TryActivatePower)
  └─> MetaProgressionManager.RecordPowerActivation()
```

### Game Over
```
GameOverManager.Start()
  └─> GameStatsTracker.FinalizeRunStats()
       └─> MetaProgressionManager.RecordRunDuration()
       └─> MetaProgressionManager.RecordEssence()
       └─> MetaProgressionManager.OnRunEnd()
            └─> Calculate Fae Dust rewards
            └─> Update lifetime stats
            └─> Save to PlayerPrefs
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

---

## Future Work

### High Priority
1. **Unlock Shop UI** - Main menu screen to spend Fae Dust
2. **Game Over Fae Dust Display** - Show earned dust on game over screen
3. **Power Tier Visual Feedback** - Show tier level on power UI

### Medium Priority
4. **Blessing Selection UI** - Pre-run blessing choice
5. **Blessing Effects** - Implement blessing modifiers
6. **Heart Form Selection** - Character selection screen

### Lower Priority
7. **Challenge Modifiers** - Risk/reward system
8. **Prop Mutations** - Mutation effects
9. **Achievements** - Track milestones
10. **Cosmetic Unlocks** - Visual customization

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

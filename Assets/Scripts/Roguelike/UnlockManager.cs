using System;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Defines categories of unlockable content.
    /// </summary>
    public enum UnlockCategory
    {
        PowerTier,          // Heart Power tier unlocks (Tier II, Tier III)
        Blessing,           // Run-start blessing unlocks
        HeartForm,          // Different Heart form/character unlocks
        PropMutation,       // Prop farming mutation unlocks
        Cosmetic,           // Visual customization unlocks
        ChallengeModifier   // Challenge modifier unlocks
    }

    /// <summary>
    /// Defines a single unlockable item with its requirements.
    /// </summary>
    [Serializable]
    public class UnlockDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public UnlockCategory Category;
        public int FaeDustCost;
        public string RequiredAchievement;  // Optional - must complete this achievement first
        public string RequiredUnlock;       // Optional - must have this unlock first
        public bool IsDefault;              // True if unlocked from the start
    }

    /// <summary>
    /// Manages all unlockable content in the game.
    /// Tracks unlock state, validates unlock requirements, and handles Fae Dust purchases.
    /// </summary>
    public class UnlockManager : PersistentSingletonMonoBehaviour<UnlockManager>
    {

        #region Events

        /// <summary>Fired when an item is unlocked</summary>
        public event Action<string> OnItemUnlocked;

        /// <summary>Fired when unlock state changes</summary>
        public event Action OnUnlockStateChanged;

        #endregion

        #region Constants

        private const string PREFS_UNLOCK_PREFIX = "FaeMaze_Unlock_";

        #endregion

        #region Private Fields

        private Dictionary<string, UnlockDefinition> _allUnlocks;
        private HashSet<string> _unlockedItems;
        private bool _initialized;

        #endregion

        #region Unity Lifecycle

        protected override void OnAwake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;

            _allUnlocks = new Dictionary<string, UnlockDefinition>();
            _unlockedItems = new HashSet<string>();

            // Register all unlock definitions
            RegisterDefaultUnlocks();

            // Load unlock state from PlayerPrefs
            LoadUnlockState();

            _initialized = true;
        }

        #endregion

        #region Unlock Definitions

        /// <summary>
        /// Registers all unlock definitions for the game.
        /// </summary>
        private void RegisterDefaultUnlocks()
        {
            // Power Tier Unlocks (Tier II and III for each power)
            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_MurmuringPaths_T2",
                DisplayName = "Spooky Fog II",
                Description = "Unlock Tier II - Labyrinth's Memory: Marked visitors prefer Lost state with Heart bias at intersections",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 150,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_MurmuringPaths_T3",
                DisplayName = "Spooky Fog III",
                Description = "Unlock Tier III - Sealed Ways: Toggle mode to increase costs instead (soft barriers)",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 300,
                RequiredUnlock = "PowerTier_MurmuringPaths_T2",
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_HeartwardGrasp_T2",
                DisplayName = "Yoink! II",
                Description = "Unlock Tier II - Extended reach and faster retraction",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 150,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_HeartwardGrasp_T3",
                DisplayName = "Yoink! III",
                Description = "Unlock Tier III - Multiple simultaneous grabs",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 300,
                RequiredUnlock = "PowerTier_HeartwardGrasp_T2",
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_DevouringMaw_T2",
                DisplayName = "Nom Nom II",
                Description = "Unlock Tier II - Larger area of effect",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 150,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_DevouringMaw_T3",
                DisplayName = "Nom Nom III",
                Description = "Unlock Tier III - Chain devouring on consume",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 300,
                RequiredUnlock = "PowerTier_DevouringMaw_T2",
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_Sculpting_T2",
                DisplayName = "Redecorating II",
                Description = "Unlock Tier II - Enhanced hazard variants",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 150,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "PowerTier_Sculpting_T3",
                DisplayName = "Redecorating III",
                Description = "Unlock Tier III - Corrupted hazards",
                Category = UnlockCategory.PowerTier,
                FaeDustCost = 300,
                RequiredUnlock = "PowerTier_Sculpting_T2",
                IsDefault = false
            });

            // Blessing Unlocks
            RegisterUnlock(new UnlockDefinition
            {
                Id = "Blessing_GreedyHeart",
                DisplayName = "Greedy Heart",
                Description = "+50% threads from consumed visitors, -25% starting threads",
                Category = UnlockCategory.Blessing,
                FaeDustCost = 100,
                RequiredAchievement = "ReachEssence500",
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Blessing_PatientHunter",
                DisplayName = "Patient Hunter",
                Description = "Visitors move 15% slower, spawn 20% faster",
                Category = UnlockCategory.Blessing,
                FaeDustCost = 100,
                RequiredAchievement = "Survive10Minutes",
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Blessing_DesperateGrasp",
                DisplayName = "Desperate Grasp",
                Description = "Yoink costs 0 threads when below 25 threads",
                Category = UnlockCategory.Blessing,
                FaeDustCost = 75,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Blessing_VengefulSpirit",
                DisplayName = "Vengeful Spirit",
                Description = "When threads drop below 50, all powers cost 50% less",
                Category = UnlockCategory.Blessing,
                FaeDustCost = 125,
                IsDefault = false
            });

            // Heart Form Unlocks
            RegisterUnlock(new UnlockDefinition
            {
                Id = "HeartForm_HungryHeart",
                DisplayName = "Hungry Heart",
                Description = "Default balanced form with no modifiers",
                Category = UnlockCategory.HeartForm,
                FaeDustCost = 0,
                IsDefault = true
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "HeartForm_RavenousHeart",
                DisplayName = "Ravenous Heart",
                Description = "Heart tongue 50% faster, -20 starting threads",
                Category = UnlockCategory.HeartForm,
                FaeDustCost = 150,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "HeartForm_PatientHeart",
                DisplayName = "Patient Heart",
                Description = "Lanterns 30% more effective, hazards spawn 25% more often",
                Category = UnlockCategory.HeartForm,
                FaeDustCost = 200,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "HeartForm_StarvingHeart",
                DisplayName = "Famished Heart",
                Description = "+50% thread rewards, but 2/sec bleeds away.",
                Category = UnlockCategory.HeartForm,
                FaeDustCost = 250,
                IsDefault = false
            });

            // Prop Mutation Unlocks
            RegisterUnlock(new UnlockDefinition
            {
                Id = "Mutation_GreedyGlow",
                DisplayName = "Greedy Glow",
                Description = "Lantern awards 3x threads to player (6/sec instead of 2/sec)",
                Category = UnlockCategory.PropMutation,
                FaeDustCost = 100,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Mutation_RingTithe",
                DisplayName = "Ring Tithe",
                Description = "Fairy Rings award 50% of drained threads to player",
                Category = UnlockCategory.PropMutation,
                FaeDustCost = 100,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Mutation_DrowningGift",
                DisplayName = "Puka's Portion",
                Description = "Drowned visitors yield 50% threads to you.",
                Category = UnlockCategory.PropMutation,
                FaeDustCost = 100,
                IsDefault = false
            });

            // Challenge Modifier Unlocks
            RegisterUnlock(new UnlockDefinition
            {
                Id = "Challenge_FrugalHeart",
                DisplayName = "Frugal Heart",
                Description = "Powers cost 50% more - 1.25x Fae Dust",
                Category = UnlockCategory.ChallengeModifier,
                FaeDustCost = 50,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Challenge_EndlessTide",
                DisplayName = "Endless Tide",
                Description = "Spawn interval halved - 1.5x Fae Dust",
                Category = UnlockCategory.ChallengeModifier,
                FaeDustCost = 50,
                IsDefault = false
            });

            RegisterUnlock(new UnlockDefinition
            {
                Id = "Challenge_ChampionVisitors",
                DisplayName = "Champion Visitors",
                Description = "10% of visitors are 'elites' (2x stats, 3x reward) - 1.75x Fae Dust",
                Category = UnlockCategory.ChallengeModifier,
                FaeDustCost = 75,
                IsDefault = false
            });
        }

        /// <summary>
        /// Registers an unlock definition.
        /// </summary>
        public void RegisterUnlock(UnlockDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.Id))
            {
                Debug.LogError("[UnlockManager] Cannot register unlock with empty ID");
                return;
            }

            _allUnlocks[definition.Id] = definition;

            // If it's a default unlock, mark it as unlocked
            if (definition.IsDefault)
            {
                _unlockedItems.Add(definition.Id);
            }
        }

        #endregion

        #region Unlock State

        /// <summary>
        /// Checks if an item is unlocked.
        /// </summary>
        public bool IsUnlocked(string unlockId)
        {
            if (string.IsNullOrEmpty(unlockId)) return false;

            // Check if it's a default unlock
            if (_allUnlocks.TryGetValue(unlockId, out var def) && def.IsDefault)
                return true;

            return _unlockedItems.Contains(unlockId);
        }

        /// <summary>
        /// Checks if an item can be unlocked (requirements met, not already unlocked).
        /// </summary>
        public bool CanUnlock(string unlockId, out string reason)
        {
            if (!_allUnlocks.TryGetValue(unlockId, out var definition))
            {
                reason = "Unknown unlock ID";
                return false;
            }

            if (IsUnlocked(unlockId))
            {
                reason = "Already unlocked";
                return false;
            }

            // Check required achievement
            if (!string.IsNullOrEmpty(definition.RequiredAchievement))
            {
                if (MetaProgressionManager.Instance == null ||
                    !MetaProgressionManager.Instance.IsAchievementCompleted(definition.RequiredAchievement))
                {
                    reason = $"Requires achievement: {definition.RequiredAchievement}";
                    return false;
                }
            }

            // Check required unlock
            if (!string.IsNullOrEmpty(definition.RequiredUnlock))
            {
                if (!IsUnlocked(definition.RequiredUnlock))
                {
                    reason = $"Requires unlock: {definition.RequiredUnlock}";
                    return false;
                }
            }

            // Check Fae Dust cost
            if (MetaProgressionManager.Instance == null || !MetaProgressionManager.Instance.CanAfford(definition.FaeDustCost))
            {
                reason = $"Not enough Fae Dust (need {definition.FaeDustCost})";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// Attempts to purchase an unlock with Fae Dust.
        /// </summary>
        public bool TryPurchaseUnlock(string unlockId)
        {
            if (!CanUnlock(unlockId, out string reason))
            {
                return false;
            }

            var definition = _allUnlocks[unlockId];

            // Spend Fae Dust
            if (!MetaProgressionManager.Instance.SpendFaeDust(definition.FaeDustCost, $"Unlock: {definition.DisplayName}"))
            {
                return false;
            }

            // Unlock the item
            _unlockedItems.Add(unlockId);
            SaveUnlockState(unlockId);

            // Also notify MetaProgressionManager
            MetaProgressionManager.Instance?.Unlock(unlockId);

            OnItemUnlocked?.Invoke(unlockId);
            OnUnlockStateChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// Gets all unlocks in a specific category.
        /// </summary>
        public List<UnlockDefinition> GetUnlocksByCategory(UnlockCategory category)
        {
            var result = new List<UnlockDefinition>();
            foreach (var kvp in _allUnlocks)
            {
                if (kvp.Value.Category == category)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets all unlocked items in a specific category.
        /// </summary>
        public List<UnlockDefinition> GetUnlockedByCategory(UnlockCategory category)
        {
            var result = new List<UnlockDefinition>();
            foreach (var kvp in _allUnlocks)
            {
                if (kvp.Value.Category == category && IsUnlocked(kvp.Key))
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the unlock definition for a specific ID.
        /// </summary>
        public UnlockDefinition GetUnlockDefinition(string unlockId)
        {
            _allUnlocks.TryGetValue(unlockId, out var def);
            return def;
        }

        #endregion

        #region Power Tier Helpers

        /// <summary>
        /// Checks if a specific power tier is unlocked.
        /// </summary>
        public bool IsPowerTierUnlocked(string powerType, int tier)
        {
            if (tier <= 1) return true; // Tier 1 is always unlocked

            string unlockId = $"PowerTier_{powerType}_T{tier}";
            return IsUnlocked(unlockId);
        }

        /// <summary>
        /// Gets the maximum unlocked tier for a power.
        /// </summary>
        public int GetMaxUnlockedTier(string powerType)
        {
            for (int tier = 3; tier >= 1; tier--)
            {
                if (IsPowerTierUnlocked(powerType, tier))
                {
                    return tier;
                }
            }
            return 1;
        }

        #endregion

        #region Persistence

        private void LoadUnlockState()
        {
            foreach (var kvp in _allUnlocks)
            {
                string key = PREFS_UNLOCK_PREFIX + kvp.Key;
                if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == 1)
                {
                    _unlockedItems.Add(kvp.Key);
                }
            }
        }

        private void SaveUnlockState(string unlockId)
        {
            string key = PREFS_UNLOCK_PREFIX + unlockId;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Resets all unlock state. Use with caution!
        /// </summary>
        [ContextMenu("Reset All Unlocks")]
        public void ResetAllUnlocks()
        {
            foreach (var kvp in _allUnlocks)
            {
                string key = PREFS_UNLOCK_PREFIX + kvp.Key;
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();

            _unlockedItems.Clear();

            // Re-add default unlocks
            foreach (var kvp in _allUnlocks)
            {
                if (kvp.Value.IsDefault)
                {
                    _unlockedItems.Add(kvp.Key);
                }
            }

            OnUnlockStateChanged?.Invoke();
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Log All Unlocks")]
        public void DebugLogAllUnlocks()
        {
            // Logging removed - use GetUnlocksByCategory() to inspect state programmatically
        }

        [ContextMenu("Debug: Unlock All")]
        public void DebugUnlockAll()
        {
            foreach (var kvp in _allUnlocks)
            {
                if (!IsUnlocked(kvp.Key))
                {
                    _unlockedItems.Add(kvp.Key);
                    SaveUnlockState(kvp.Key);
                }
            }
            OnUnlockStateChanged?.Invoke();
        }

        #endregion
    }
}

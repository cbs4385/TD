using System;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.HeartPowers;
using FaeMaze.Systems;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Manages run-based power tier progression.
    /// Tracks which power tiers are available this run, handles tier upgrade choices,
    /// and integrates with HeartPowerManager to provide active tier levels.
    /// </summary>
    public class PowerProgressionManager : MonoBehaviour
    {
        #region Singleton

        private static PowerProgressionManager _instance;
        public static PowerProgressionManager Instance => _instance;

        #endregion

        #region Events

        /// <summary>Fired when a tier upgrade is available</summary>
        public event Action<int> OnTierUpgradeAvailable;

        /// <summary>Fired when a power tier is upgraded</summary>
        public event Action<HeartPowerType, int> OnPowerTierUpgraded;

        /// <summary>Fired when run tiers are reset</summary>
        public event Action OnRunTiersReset;

        #endregion

        #region Constants

        // Essence thresholds for tier upgrades (relative to starting essence)
        // These trigger tier upgrade opportunities
        private static readonly float[] TIER2_THRESHOLDS = { 1.5f, 3.0f };  // Two chances to get Tier II
        private static readonly float[] TIER3_THRESHOLDS = { 5.0f, 8.0f };  // Two chances to get Tier III

        #endregion

        #region Private Fields

        // Run-specific tier states (reset each run)
        private Dictionary<HeartPowerType, int> _runTiers;

        // Track which thresholds have been crossed this run
        private HashSet<float> _crossedThresholds;

        // Pending upgrade (waiting for player choice)
        private int _pendingUpgradeTier;
        private bool _hasPendingUpgrade;

        // Reference to starting essence for threshold calculations
        private int _startingEssence;

        // Cached array of power types
        private static readonly HeartPowerType[] _allPowerTypes;

        static PowerProgressionManager()
        {
            _allPowerTypes = (HeartPowerType[])Enum.GetValues(typeof(HeartPowerType));
        }

        #endregion

        #region Properties

        /// <summary>Whether there's a pending tier upgrade choice</summary>
        public bool HasPendingUpgrade => _hasPendingUpgrade;

        /// <summary>The tier of the pending upgrade (2 or 3)</summary>
        public int PendingUpgradeTier => _pendingUpgradeTier;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            _runTiers = new Dictionary<HeartPowerType, int>();
            _crossedThresholds = new HashSet<float>();
            ResetRunTiers();
        }

        private void OnEnable()
        {
            // Subscribe to essence changes
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        private void OnDisable()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }
        }

        private void Start()
        {
            // Get starting essence
            _startingEssence = GameSettings.StartingEssence;

            // Re-subscribe if GameController wasn't ready in OnEnable
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        #endregion

        #region Run Lifecycle

        /// <summary>
        /// Resets all run tiers to tier 1 for a new run.
        /// </summary>
        public void ResetRunTiers()
        {
            _runTiers.Clear();
            _crossedThresholds.Clear();
            _hasPendingUpgrade = false;
            _pendingUpgradeTier = 0;

            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                _runTiers[powerType] = 1;
            }

            OnRunTiersReset?.Invoke();
            Debug.Log("[PowerProgression] Run tiers reset to Tier I");
        }

        #endregion

        #region Tier Access

        /// <summary>
        /// Gets the effective tier for a power this run.
        /// Takes into account both run progression and permanent unlocks.
        /// </summary>
        public int GetRunTier(HeartPowerType powerType)
        {
            if (!_runTiers.TryGetValue(powerType, out int runTier))
            {
                runTier = 1;
            }

            // Cap at max unlocked tier from UnlockManager
            if (UnlockManager.Instance != null)
            {
                int maxUnlocked = UnlockManager.Instance.GetMaxUnlockedTier(powerType.ToString());
                return Mathf.Min(runTier, maxUnlocked);
            }

            return runTier;
        }

        /// <summary>
        /// Gets the current run tier without checking unlocks.
        /// </summary>
        public int GetRawRunTier(HeartPowerType powerType)
        {
            return _runTiers.TryGetValue(powerType, out int tier) ? tier : 1;
        }

        /// <summary>
        /// Checks if a power can be upgraded this run.
        /// </summary>
        public bool CanUpgradePower(HeartPowerType powerType, int targetTier)
        {
            int currentTier = GetRunTier(powerType);
            if (targetTier <= currentTier) return false;

            // Check if the tier is unlocked permanently
            if (UnlockManager.Instance != null)
            {
                if (!UnlockManager.Instance.IsPowerTierUnlocked(powerType.ToString(), targetTier))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Tier Upgrades

        /// <summary>
        /// Called when essence changes to check for threshold crossings.
        /// </summary>
        private void OnEssenceChanged(int newEssence)
        {
            if (_startingEssence <= 0) return;
            if (_hasPendingUpgrade) return; // Don't trigger new upgrades while one is pending

            float essenceRatio = (float)newEssence / _startingEssence;

            // Check Tier II thresholds
            foreach (float threshold in TIER2_THRESHOLDS)
            {
                if (essenceRatio >= threshold && !_crossedThresholds.Contains(threshold))
                {
                    _crossedThresholds.Add(threshold);
                    TriggerTierUpgrade(2);
                    return; // Only one upgrade at a time
                }
            }

            // Check Tier III thresholds
            foreach (float threshold in TIER3_THRESHOLDS)
            {
                if (essenceRatio >= threshold && !_crossedThresholds.Contains(threshold))
                {
                    _crossedThresholds.Add(threshold);
                    TriggerTierUpgrade(3);
                    return; // Only one upgrade at a time
                }
            }
        }

        /// <summary>
        /// Triggers a tier upgrade opportunity.
        /// </summary>
        private void TriggerTierUpgrade(int tier)
        {
            // Check if any powers can be upgraded to this tier
            var upgradablePowers = GetUpgradablePowers(tier);
            if (upgradablePowers.Count == 0)
            {
                Debug.Log($"[PowerProgression] Tier {tier} threshold crossed but no upgradable powers");
                return;
            }

            _hasPendingUpgrade = true;
            _pendingUpgradeTier = tier;

            OnTierUpgradeAvailable?.Invoke(tier);
            Debug.Log($"[PowerProgression] Tier {tier} upgrade available! {upgradablePowers.Count} powers can be upgraded");
        }

        /// <summary>
        /// Gets all powers that can be upgraded to the specified tier.
        /// </summary>
        public List<HeartPowerType> GetUpgradablePowers(int targetTier)
        {
            var result = new List<HeartPowerType>();

            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                if (CanUpgradePower(powerType, targetTier))
                {
                    result.Add(powerType);
                }
            }

            return result;
        }

        /// <summary>
        /// Applies the player's tier upgrade choice.
        /// </summary>
        public bool ApplyTierUpgrade(HeartPowerType powerType)
        {
            if (!_hasPendingUpgrade)
            {
                Debug.LogWarning("[PowerProgression] No pending upgrade to apply");
                return false;
            }

            if (!CanUpgradePower(powerType, _pendingUpgradeTier))
            {
                Debug.LogWarning($"[PowerProgression] Cannot upgrade {powerType} to tier {_pendingUpgradeTier}");
                return false;
            }

            // Apply the upgrade
            _runTiers[powerType] = _pendingUpgradeTier;

            // Clear pending state
            int upgradedTier = _pendingUpgradeTier;
            _hasPendingUpgrade = false;
            _pendingUpgradeTier = 0;

            OnPowerTierUpgraded?.Invoke(powerType, upgradedTier);
            Debug.Log($"[PowerProgression] Upgraded {powerType} to Tier {upgradedTier}");

            return true;
        }

        /// <summary>
        /// Skips the current pending upgrade.
        /// </summary>
        public void SkipPendingUpgrade()
        {
            if (_hasPendingUpgrade)
            {
                Debug.Log($"[PowerProgression] Skipped Tier {_pendingUpgradeTier} upgrade");
                _hasPendingUpgrade = false;
                _pendingUpgradeTier = 0;
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Log Run Tiers")]
        public void DebugLogRunTiers()
        {
            Debug.Log("=== Run Power Tiers ===");
            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                int runTier = GetRunTier(powerType);
                int rawTier = GetRawRunTier(powerType);
                int maxUnlocked = UnlockManager.Instance?.GetMaxUnlockedTier(powerType.ToString()) ?? 3;
                Debug.Log($"  {powerType}: Tier {runTier} (raw: {rawTier}, max unlocked: {maxUnlocked})");
            }
        }

        [ContextMenu("Debug: Force Tier 2 Upgrade")]
        public void DebugForceTier2Upgrade()
        {
            TriggerTierUpgrade(2);
        }

        [ContextMenu("Debug: Force Tier 3 Upgrade")]
        public void DebugForceTier3Upgrade()
        {
            TriggerTierUpgrade(3);
        }

        #endregion
    }
}

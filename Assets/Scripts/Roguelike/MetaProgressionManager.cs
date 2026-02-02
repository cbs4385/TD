using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Central manager for meta-progression systems across runs.
    /// Tracks Fae Dust (persistent currency), lifetime statistics, and permanent unlocks.
    /// All data persists via PlayerPrefs.
    /// </summary>
    public class MetaProgressionManager : MonoBehaviour
    {
        #region Singleton

        private static MetaProgressionManager _instance;
        public static MetaProgressionManager Instance => _instance;

        #endregion

        #region Events

        /// <summary>Fired when Fae Dust amount changes</summary>
        public event Action<int> OnFaeDustChanged;

        /// <summary>Fired when a new unlock is acquired</summary>
        public event Action<string> OnUnlockAcquired;

        /// <summary>Fired when an achievement is completed</summary>
        public event Action<string> OnAchievementCompleted;

        #endregion

        #region Constants

        // PlayerPrefs keys
        private const string PREFS_FAE_DUST = "FaeMaze_FaeDust";
        private const string PREFS_LIFETIME_PREFIX = "FaeMaze_Lifetime_";
        private const string PREFS_UNLOCK_PREFIX = "FaeMaze_Unlock_";
        private const string PREFS_ACHIEVEMENT_PREFIX = "FaeMaze_Achievement_";
        private const string PREFS_TOTAL_RUNS = "FaeMaze_TotalRuns";
        private const string PREFS_LAST_DAILY_RUN = "FaeMaze_LastDailyRun";

        // Fae Dust earning rates
        public const int DUST_PER_HEART_CONSUME = 1;
        public const int DUST_PER_MAW_DEVOUR = 2;
        public const int DUST_PER_PROP_DRAIN = 1;
        public const int DUST_TIER_3_REACHED = 10;
        public const int DUST_TIER_5_REACHED = 25;
        public const int DUST_TIER_7_REACHED = 50;
        public const int DUST_SURVIVE_5_MIN = 5;
        public const int DUST_SURVIVE_10_MIN = 15;
        public const int DUST_FIRST_RUN_OF_DAY = 10;

        #endregion

        #region Lifetime Stats

        /// <summary>
        /// Lifetime statistics tracked across all runs.
        /// </summary>
        [Serializable]
        public class LifetimeStats
        {
            public int TotalRuns;
            public int TotalVisitorsConsumedByHeart;
            public int TotalVisitorsDevouredByMaw;
            public int TotalVisitorsDrainedByProps;
            public int TotalVisitorsEscaped;
            public int TotalVisitorsKilledByRedCap;
            public int TotalVisitorsDrownedByPuka;
            public int TotalEssenceEarned;
            public int TotalEssenceSpentOnPowers;
            public int TotalPowerActivations;
            public int TotalPropsPlaced;
            public int MaxEssenceInSingleRun;
            public int MaxVisitorsConsumedInSingleRun;
            public int MaxDifficultyTierReached;
            public float LongestRunSeconds;
            public int HighestDailyScore;

            // Per-power stats
            public int MurmuringPathsActivations;
            public int HeartwardGraspActivations;
            public int DevouringMawActivations;
            public int SculptingActivations;

            // Per-archetype stats
            public int LanternDrunkConsumed;
            public int WaryWayfarerConsumed;
            public int SleepwalkingDevoteeConsumed;
        }

        private LifetimeStats _lifetimeStats;
        public LifetimeStats Stats => _lifetimeStats;

        #endregion

        #region Run Stats (Current Session)

        /// <summary>
        /// Statistics for the current run, used to calculate Fae Dust rewards.
        /// </summary>
        public class CurrentRunStats
        {
            public int VisitorsConsumedByHeart;
            public int VisitorsDevouredByMaw;
            public int VisitorsDrainedByProps;
            public int VisitorsEscaped;
            public int VisitorsKilledByRedCap;
            public int MaxEssence;
            public int MaxDifficultyTier;
            public float RunDurationSeconds;
            public bool ReachedTier3;
            public bool ReachedTier5;
            public bool ReachedTier7;
            public bool Survived5Minutes;
            public bool Survived10Minutes;

            public void Reset()
            {
                VisitorsConsumedByHeart = 0;
                VisitorsDevouredByMaw = 0;
                VisitorsDrainedByProps = 0;
                VisitorsEscaped = 0;
                VisitorsKilledByRedCap = 0;
                MaxEssence = 0;
                MaxDifficultyTier = 1;
                RunDurationSeconds = 0f;
                ReachedTier3 = false;
                ReachedTier5 = false;
                ReachedTier7 = false;
                Survived5Minutes = false;
                Survived10Minutes = false;
            }
        }

        private CurrentRunStats _currentRunStats;
        public CurrentRunStats RunStats => _currentRunStats;

        #endregion

        #region Private Fields

        private int _faeDust;
        private HashSet<string> _unlockedItems;
        private HashSet<string> _completedAchievements;
        private float _dustMultiplier = 1f; // From challenge modifiers

        #endregion

        #region Properties

        /// <summary>Current Fae Dust amount</summary>
        public int FaeDust => _faeDust;

        /// <summary>Total runs completed</summary>
        public int TotalRuns => _lifetimeStats.TotalRuns;

        /// <summary>Current dust multiplier from challenge modifiers</summary>
        public float DustMultiplier
        {
            get => _dustMultiplier;
            set => _dustMultiplier = Mathf.Max(1f, value);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            _currentRunStats = new CurrentRunStats();
            _lifetimeStats = new LifetimeStats();
            _unlockedItems = new HashSet<string>();
            _completedAchievements = new HashSet<string>();

            LoadAllData();
        }

        #endregion

        #region Fae Dust Management

        /// <summary>
        /// Adds Fae Dust, applying the current multiplier.
        /// </summary>
        public void AddFaeDust(int baseAmount, string source = "")
        {
            int finalAmount = Mathf.RoundToInt(baseAmount * _dustMultiplier);
            _faeDust += finalAmount;
            SaveFaeDust();
            OnFaeDustChanged?.Invoke(_faeDust);

            Debug.Log($"[MetaProgression] +{finalAmount} Fae Dust ({source}). Total: {_faeDust}");
        }

        /// <summary>
        /// Spends Fae Dust if sufficient amount is available.
        /// </summary>
        /// <returns>True if purchase successful</returns>
        public bool SpendFaeDust(int amount, string purpose = "")
        {
            if (_faeDust < amount)
            {
                Debug.Log($"[MetaProgression] Cannot spend {amount} Fae Dust - only have {_faeDust}");
                return false;
            }

            _faeDust -= amount;
            SaveFaeDust();
            OnFaeDustChanged?.Invoke(_faeDust);

            Debug.Log($"[MetaProgression] Spent {amount} Fae Dust ({purpose}). Remaining: {_faeDust}");
            return true;
        }

        /// <summary>
        /// Checks if player can afford a purchase.
        /// </summary>
        public bool CanAfford(int amount) => _faeDust >= amount;

        #endregion

        #region Run Lifecycle

        /// <summary>
        /// Called when a new run begins. Resets current run stats.
        /// </summary>
        public void OnRunStart()
        {
            _currentRunStats.Reset();
            _dustMultiplier = 1f;

            // Check for first run of day bonus
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string lastDailyRun = PlayerPrefs.GetString(PREFS_LAST_DAILY_RUN, "");

            if (lastDailyRun != today)
            {
                PlayerPrefs.SetString(PREFS_LAST_DAILY_RUN, today);
                PlayerPrefs.Save();
                AddFaeDust(DUST_FIRST_RUN_OF_DAY, "First run of day");
            }

            Debug.Log("[MetaProgression] Run started");
        }

        /// <summary>
        /// Called when a run ends. Calculates Fae Dust rewards and updates lifetime stats.
        /// </summary>
        public void OnRunEnd()
        {
            // Calculate Fae Dust rewards
            int dustEarned = 0;

            dustEarned += _currentRunStats.VisitorsConsumedByHeart * DUST_PER_HEART_CONSUME;
            dustEarned += _currentRunStats.VisitorsDevouredByMaw * DUST_PER_MAW_DEVOUR;
            dustEarned += _currentRunStats.VisitorsDrainedByProps * DUST_PER_PROP_DRAIN;

            if (_currentRunStats.ReachedTier3) dustEarned += DUST_TIER_3_REACHED;
            if (_currentRunStats.ReachedTier5) dustEarned += DUST_TIER_5_REACHED;
            if (_currentRunStats.ReachedTier7) dustEarned += DUST_TIER_7_REACHED;
            if (_currentRunStats.Survived5Minutes) dustEarned += DUST_SURVIVE_5_MIN;
            if (_currentRunStats.Survived10Minutes) dustEarned += DUST_SURVIVE_10_MIN;

            if (dustEarned > 0)
            {
                AddFaeDust(dustEarned, "Run completion");
            }

            // Update lifetime stats
            _lifetimeStats.TotalRuns++;
            _lifetimeStats.TotalVisitorsConsumedByHeart += _currentRunStats.VisitorsConsumedByHeart;
            _lifetimeStats.TotalVisitorsDevouredByMaw += _currentRunStats.VisitorsDevouredByMaw;
            _lifetimeStats.TotalVisitorsDrainedByProps += _currentRunStats.VisitorsDrainedByProps;
            _lifetimeStats.TotalVisitorsEscaped += _currentRunStats.VisitorsEscaped;
            _lifetimeStats.TotalVisitorsKilledByRedCap += _currentRunStats.VisitorsKilledByRedCap;

            if (_currentRunStats.MaxEssence > _lifetimeStats.MaxEssenceInSingleRun)
                _lifetimeStats.MaxEssenceInSingleRun = _currentRunStats.MaxEssence;

            int totalConsumed = _currentRunStats.VisitorsConsumedByHeart + _currentRunStats.VisitorsDevouredByMaw;
            if (totalConsumed > _lifetimeStats.MaxVisitorsConsumedInSingleRun)
                _lifetimeStats.MaxVisitorsConsumedInSingleRun = totalConsumed;

            if (_currentRunStats.MaxDifficultyTier > _lifetimeStats.MaxDifficultyTierReached)
                _lifetimeStats.MaxDifficultyTierReached = _currentRunStats.MaxDifficultyTier;

            if (_currentRunStats.RunDurationSeconds > _lifetimeStats.LongestRunSeconds)
                _lifetimeStats.LongestRunSeconds = _currentRunStats.RunDurationSeconds;

            SaveLifetimeStats();

            Debug.Log($"[MetaProgression] Run ended. Earned {dustEarned} Fae Dust. Total runs: {_lifetimeStats.TotalRuns}");
        }

        #endregion

        #region Run Event Recording

        /// <summary>Record a visitor consumed by the Heart tongue</summary>
        public void RecordHeartConsume(string archetype = "")
        {
            _currentRunStats.VisitorsConsumedByHeart++;

            // Track per-archetype stats
            switch (archetype.ToLower())
            {
                case "lanterndrunk":
                    _lifetimeStats.LanternDrunkConsumed++;
                    break;
                case "warywayfarer":
                    _lifetimeStats.WaryWayfarerConsumed++;
                    break;
                case "sleepwalkingdevotee":
                    _lifetimeStats.SleepwalkingDevoteeConsumed++;
                    break;
            }
        }

        /// <summary>Record a visitor devoured by Maw power</summary>
        public void RecordMawDevour(string archetype = "")
        {
            _currentRunStats.VisitorsDevouredByMaw++;
        }

        /// <summary>Record a visitor drained by props (with mutation active)</summary>
        public void RecordPropDrain()
        {
            _currentRunStats.VisitorsDrainedByProps++;
        }

        /// <summary>Record a visitor escaped</summary>
        public void RecordEscape()
        {
            _currentRunStats.VisitorsEscaped++;
        }

        /// <summary>Record a visitor killed by RedCap</summary>
        public void RecordRedCapKill()
        {
            _currentRunStats.VisitorsKilledByRedCap++;
        }

        /// <summary>Record current essence (for max tracking)</summary>
        public void RecordEssence(int essence)
        {
            if (essence > _currentRunStats.MaxEssence)
                _currentRunStats.MaxEssence = essence;
        }

        /// <summary>Record difficulty tier reached</summary>
        public void RecordDifficultyTier(int tier)
        {
            if (tier > _currentRunStats.MaxDifficultyTier)
                _currentRunStats.MaxDifficultyTier = tier;

            if (tier >= 3 && !_currentRunStats.ReachedTier3)
                _currentRunStats.ReachedTier3 = true;
            if (tier >= 5 && !_currentRunStats.ReachedTier5)
                _currentRunStats.ReachedTier5 = true;
            if (tier >= 7 && !_currentRunStats.ReachedTier7)
                _currentRunStats.ReachedTier7 = true;
        }

        /// <summary>Record run duration</summary>
        public void RecordRunDuration(float seconds)
        {
            _currentRunStats.RunDurationSeconds = seconds;

            if (seconds >= 300f && !_currentRunStats.Survived5Minutes)
                _currentRunStats.Survived5Minutes = true;
            if (seconds >= 600f && !_currentRunStats.Survived10Minutes)
                _currentRunStats.Survived10Minutes = true;
        }

        /// <summary>Record a power activation</summary>
        public void RecordPowerActivation(string powerType)
        {
            _lifetimeStats.TotalPowerActivations++;

            switch (powerType.ToLower())
            {
                case "murmuringpaths":
                    _lifetimeStats.MurmuringPathsActivations++;
                    break;
                case "heartwardgrasp":
                    _lifetimeStats.HeartwardGraspActivations++;
                    break;
                case "devouringmaw":
                    _lifetimeStats.DevouringMawActivations++;
                    break;
                case "sculpting":
                    _lifetimeStats.SculptingActivations++;
                    break;
            }
        }

        #endregion

        #region Unlock Management

        /// <summary>Check if an item is unlocked</summary>
        public bool IsUnlocked(string unlockId) => _unlockedItems.Contains(unlockId);

        /// <summary>Unlock an item</summary>
        public void Unlock(string unlockId)
        {
            if (_unlockedItems.Contains(unlockId))
                return;

            _unlockedItems.Add(unlockId);
            PlayerPrefs.SetInt(PREFS_UNLOCK_PREFIX + unlockId, 1);
            PlayerPrefs.Save();

            OnUnlockAcquired?.Invoke(unlockId);
            Debug.Log($"[MetaProgression] Unlocked: {unlockId}");
        }

        /// <summary>Get all unlocked items</summary>
        public IReadOnlyCollection<string> GetUnlockedItems() => _unlockedItems;

        #endregion

        #region Achievement Management

        /// <summary>Check if an achievement is completed</summary>
        public bool IsAchievementCompleted(string achievementId) => _completedAchievements.Contains(achievementId);

        /// <summary>Complete an achievement</summary>
        public void CompleteAchievement(string achievementId)
        {
            if (_completedAchievements.Contains(achievementId))
                return;

            _completedAchievements.Add(achievementId);
            PlayerPrefs.SetInt(PREFS_ACHIEVEMENT_PREFIX + achievementId, 1);
            PlayerPrefs.Save();

            OnAchievementCompleted?.Invoke(achievementId);
            Debug.Log($"[MetaProgression] Achievement completed: {achievementId}");
        }

        /// <summary>Get all completed achievements</summary>
        public IReadOnlyCollection<string> GetCompletedAchievements() => _completedAchievements;

        #endregion

        #region Persistence

        private void SaveFaeDust()
        {
            PlayerPrefs.SetInt(PREFS_FAE_DUST, _faeDust);
            PlayerPrefs.Save();
        }

        private void SaveLifetimeStats()
        {
            // Save each stat individually for simplicity and forward compatibility
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "TotalRuns", _lifetimeStats.TotalRuns);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsConsumedByHeart", _lifetimeStats.TotalVisitorsConsumedByHeart);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsDevouredByMaw", _lifetimeStats.TotalVisitorsDevouredByMaw);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsDrainedByProps", _lifetimeStats.TotalVisitorsDrainedByProps);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsEscaped", _lifetimeStats.TotalVisitorsEscaped);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsKilledByRedCap", _lifetimeStats.TotalVisitorsKilledByRedCap);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "VisitorsDrownedByPuka", _lifetimeStats.TotalVisitorsDrownedByPuka);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "EssenceEarned", _lifetimeStats.TotalEssenceEarned);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "EssenceSpentOnPowers", _lifetimeStats.TotalEssenceSpentOnPowers);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "PowerActivations", _lifetimeStats.TotalPowerActivations);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "PropsPlaced", _lifetimeStats.TotalPropsPlaced);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "MaxEssenceInSingleRun", _lifetimeStats.MaxEssenceInSingleRun);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "MaxVisitorsConsumedInSingleRun", _lifetimeStats.MaxVisitorsConsumedInSingleRun);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "MaxDifficultyTierReached", _lifetimeStats.MaxDifficultyTierReached);
            PlayerPrefs.SetFloat(PREFS_LIFETIME_PREFIX + "LongestRunSeconds", _lifetimeStats.LongestRunSeconds);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "HighestDailyScore", _lifetimeStats.HighestDailyScore);

            // Per-power stats
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "MurmuringPathsActivations", _lifetimeStats.MurmuringPathsActivations);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "HeartwardGraspActivations", _lifetimeStats.HeartwardGraspActivations);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "DevouringMawActivations", _lifetimeStats.DevouringMawActivations);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "SculptingActivations", _lifetimeStats.SculptingActivations);

            // Per-archetype stats
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "LanternDrunkConsumed", _lifetimeStats.LanternDrunkConsumed);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "WaryWayfarerConsumed", _lifetimeStats.WaryWayfarerConsumed);
            PlayerPrefs.SetInt(PREFS_LIFETIME_PREFIX + "SleepwalkingDevoteeConsumed", _lifetimeStats.SleepwalkingDevoteeConsumed);

            PlayerPrefs.Save();
        }

        private void LoadAllData()
        {
            // Load Fae Dust
            _faeDust = PlayerPrefs.GetInt(PREFS_FAE_DUST, 0);

            // Load lifetime stats
            _lifetimeStats.TotalRuns = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "TotalRuns", 0);
            _lifetimeStats.TotalVisitorsConsumedByHeart = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsConsumedByHeart", 0);
            _lifetimeStats.TotalVisitorsDevouredByMaw = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsDevouredByMaw", 0);
            _lifetimeStats.TotalVisitorsDrainedByProps = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsDrainedByProps", 0);
            _lifetimeStats.TotalVisitorsEscaped = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsEscaped", 0);
            _lifetimeStats.TotalVisitorsKilledByRedCap = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsKilledByRedCap", 0);
            _lifetimeStats.TotalVisitorsDrownedByPuka = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "VisitorsDrownedByPuka", 0);
            _lifetimeStats.TotalEssenceEarned = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "EssenceEarned", 0);
            _lifetimeStats.TotalEssenceSpentOnPowers = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "EssenceSpentOnPowers", 0);
            _lifetimeStats.TotalPowerActivations = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "PowerActivations", 0);
            _lifetimeStats.TotalPropsPlaced = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "PropsPlaced", 0);
            _lifetimeStats.MaxEssenceInSingleRun = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "MaxEssenceInSingleRun", 0);
            _lifetimeStats.MaxVisitorsConsumedInSingleRun = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "MaxVisitorsConsumedInSingleRun", 0);
            _lifetimeStats.MaxDifficultyTierReached = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "MaxDifficultyTierReached", 0);
            _lifetimeStats.LongestRunSeconds = PlayerPrefs.GetFloat(PREFS_LIFETIME_PREFIX + "LongestRunSeconds", 0f);
            _lifetimeStats.HighestDailyScore = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "HighestDailyScore", 0);

            // Per-power stats
            _lifetimeStats.MurmuringPathsActivations = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "MurmuringPathsActivations", 0);
            _lifetimeStats.HeartwardGraspActivations = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "HeartwardGraspActivations", 0);
            _lifetimeStats.DevouringMawActivations = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "DevouringMawActivations", 0);
            _lifetimeStats.SculptingActivations = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "SculptingActivations", 0);

            // Per-archetype stats
            _lifetimeStats.LanternDrunkConsumed = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "LanternDrunkConsumed", 0);
            _lifetimeStats.WaryWayfarerConsumed = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "WaryWayfarerConsumed", 0);
            _lifetimeStats.SleepwalkingDevoteeConsumed = PlayerPrefs.GetInt(PREFS_LIFETIME_PREFIX + "SleepwalkingDevoteeConsumed", 0);

            // Note: Unlocks and achievements are loaded on-demand from PlayerPrefs
            // using HasKey checks in IsUnlocked/IsAchievementCompleted

            Debug.Log($"[MetaProgression] Loaded: {_faeDust} Fae Dust, {_lifetimeStats.TotalRuns} total runs");
        }

        /// <summary>
        /// Resets all meta-progression data. Use with caution!
        /// </summary>
        [ContextMenu("Reset All Progress")]
        public void ResetAllProgress()
        {
            _faeDust = 0;
            _lifetimeStats = new LifetimeStats();
            _unlockedItems.Clear();
            _completedAchievements.Clear();

            // Clear all related PlayerPrefs
            PlayerPrefs.DeleteKey(PREFS_FAE_DUST);
            PlayerPrefs.DeleteKey(PREFS_TOTAL_RUNS);
            PlayerPrefs.DeleteKey(PREFS_LAST_DAILY_RUN);

            // Note: This doesn't clear all individual lifetime stat keys,
            // but they will be overwritten on next save
            PlayerPrefs.Save();

            Debug.Log("[MetaProgression] All progress reset!");
        }

        #endregion

        #region Debug

        /// <summary>
        /// Adds Fae Dust for testing purposes.
        /// </summary>
        [ContextMenu("Debug: Add 100 Fae Dust")]
        public void DebugAdd100Dust()
        {
            AddFaeDust(100, "Debug");
        }

        /// <summary>
        /// Logs current stats.
        /// </summary>
        [ContextMenu("Debug: Log Stats")]
        public void DebugLogStats()
        {
            Debug.Log($"=== Meta Progression Stats ===\n" +
                $"Fae Dust: {_faeDust}\n" +
                $"Total Runs: {_lifetimeStats.TotalRuns}\n" +
                $"Total Heart Consumes: {_lifetimeStats.TotalVisitorsConsumedByHeart}\n" +
                $"Total Maw Devours: {_lifetimeStats.TotalVisitorsDevouredByMaw}\n" +
                $"Max Essence: {_lifetimeStats.MaxEssenceInSingleRun}\n" +
                $"Max Difficulty: Tier {_lifetimeStats.MaxDifficultyTierReached}\n" +
                $"Longest Run: {_lifetimeStats.LongestRunSeconds:F1}s");
        }

        #endregion
    }
}

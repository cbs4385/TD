using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using FaeMaze.Systems;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Manages Challenge Modifier selection, storage, and effect application.
    /// Challenge Modifiers are optional difficulty increases selected at run start.
    /// Multiple challenges can be active simultaneously for stacking rewards.
    /// </summary>
    public class ChallengeModifierManager : PersistentSingletonMonoBehaviour<ChallengeModifierManager>
    {

        #region Events

        /// <summary>Fired when challenges are set for the current run.</summary>
        public event Action<List<ChallengeModifierDefinition>> OnChallengesSet;

        /// <summary>Fired when challenges are cleared (run end).</summary>
        public event Action OnChallengesCleared;

        #endregion

        #region State

        // All available challenge definitions
        private List<ChallengeModifierDefinition> _allChallenges = new List<ChallengeModifierDefinition>();

        // Currently active challenges for this run
        private List<ChallengeModifierDefinition> _activeChallenges = new List<ChallengeModifierDefinition>();

        // Track unlocked challenges (by type)
        private HashSet<ChallengeModifierType> _unlockedChallenges = new HashSet<ChallengeModifierType>();

        // PlayerPrefs key prefix for challenge unlocks
        private const string CHALLENGE_UNLOCK_KEY_PREFIX = "FaeMaze_Challenge_";

        #endregion

        #region Properties

        /// <summary>Currently active challenges for this run.</summary>
        public IReadOnlyList<ChallengeModifierDefinition> ActiveChallenges => _activeChallenges;

        /// <summary>All registered challenge definitions.</summary>
        public IReadOnlyList<ChallengeModifierDefinition> AllChallenges => _allChallenges;

        /// <summary>True if any challenge is active.</summary>
        public bool HasActiveChallenges => _activeChallenges.Count > 0;

        /// <summary>Number of active challenges.</summary>
        public int ActiveChallengeCount => _activeChallenges.Count;

        #endregion

        #region Unity Lifecycle

        protected override void OnAwake()
        {
            LoadChallenges();
            LoadUnlockedState();
        }

        #endregion

        #region Initialization

        private void LoadChallenges()
        {
            // Load all ChallengeModifierDefinition assets from Resources
            var loaded = Resources.LoadAll<ChallengeModifierDefinition>("Challenges");
            _allChallenges.AddRange(loaded);

            // If no Resources-based challenges, create hardcoded defaults
            if (_allChallenges.Count == 0)
            {
                CreateDefaultChallenges();
            }

        }

        private void CreateDefaultChallenges()
        {
            _allChallenges.Add(CreateChallenge(
                ChallengeModifierType.FrugalHeart,
                "Frugal Heart",
                "Powers cost 50% more threads. Every ability is a sacrifice.",
                faeDustMultiplier: 1.25f,
                powerCostMultiplier: 1.5f,
                cost: 50
            ));

            _allChallenges.Add(CreateChallenge(
                ChallengeModifierType.EndlessTide,
                "Endless Tide",
                "Visitors arrive twice as fast. The forest can barely keep up.",
                faeDustMultiplier: 1.5f,
                spawnIntervalMultiplier: 0.5f,
                cost: 50
            ));

            _allChallenges.Add(CreateChallenge(
                ChallengeModifierType.BlindFaith,
                "Blind Faith",
                "Visitor state indicators are hidden. Trust your instincts.",
                faeDustMultiplier: 1.25f,
                hideIndicators: true,
                cost: 50
            ));

            _allChallenges.Add(CreateChallenge(
                ChallengeModifierType.EssenceDrought,
                "Thread Drought",
                "Hazards no longer share their harvest. Every thread must come from consumption.",
                faeDustMultiplier: 1.5f,
                disablePropEssence: true,
                cost: 50
            ));

            _allChallenges.Add(CreateChallenge(
                ChallengeModifierType.ChampionVisitors,
                "Champion Visitors",
                "10% of visitors are elite champions - tougher, faster, but worth triple.",
                faeDustMultiplier: 1.75f,
                eliteChance: 0.1f,
                eliteStats: 2.0f,
                eliteReward: 3.0f,
                cost: 75
            ));
        }

        private ChallengeModifierDefinition CreateChallenge(
            ChallengeModifierType type,
            string name,
            string description,
            float faeDustMultiplier = 1f,
            float powerCostMultiplier = 1f,
            float spawnIntervalMultiplier = 1f,
            bool hideIndicators = false,
            bool disablePropEssence = false,
            float eliteChance = 0f,
            float eliteStats = 2f,
            float eliteReward = 3f,
            int cost = 50)
        {
            var challenge = ScriptableObject.CreateInstance<ChallengeModifierDefinition>();

            // Use reflection to set private fields
            var typeField = typeof(ChallengeModifierDefinition).GetField("modifierType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nameField = typeof(ChallengeModifierDefinition).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = typeof(ChallengeModifierDefinition).GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var costField = typeof(ChallengeModifierDefinition).GetField("faeDustCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dustMultField = typeof(ChallengeModifierDefinition).GetField("faeDustMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var powerCostField = typeof(ChallengeModifierDefinition).GetField("powerCostMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawnField = typeof(ChallengeModifierDefinition).GetField("spawnIntervalMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hideField = typeof(ChallengeModifierDefinition).GetField("hideVisitorIndicators", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propEssenceField = typeof(ChallengeModifierDefinition).GetField("disablePropEssence", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eliteChanceField = typeof(ChallengeModifierDefinition).GetField("eliteSpawnChance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eliteStatsField = typeof(ChallengeModifierDefinition).GetField("eliteStatMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eliteRewardField = typeof(ChallengeModifierDefinition).GetField("eliteRewardMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            typeField?.SetValue(challenge, type);
            nameField?.SetValue(challenge, name);
            descField?.SetValue(challenge, description);
            costField?.SetValue(challenge, cost);
            dustMultField?.SetValue(challenge, faeDustMultiplier);
            powerCostField?.SetValue(challenge, powerCostMultiplier);
            spawnField?.SetValue(challenge, spawnIntervalMultiplier);
            hideField?.SetValue(challenge, hideIndicators);
            propEssenceField?.SetValue(challenge, disablePropEssence);
            eliteChanceField?.SetValue(challenge, eliteChance);
            eliteStatsField?.SetValue(challenge, eliteStats);
            eliteRewardField?.SetValue(challenge, eliteReward);

            return challenge;
        }

        private void LoadUnlockedState()
        {
            foreach (ChallengeModifierType type in Enum.GetValues(typeof(ChallengeModifierType)))
            {
                if (type == ChallengeModifierType.None) continue;

                string key = CHALLENGE_UNLOCK_KEY_PREFIX + type.ToString();
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    _unlockedChallenges.Add(type);
                }
            }
        }

        #endregion

        #region Unlock Management

        /// <summary>Check if a challenge type is unlocked.</summary>
        public bool IsChallengeUnlocked(ChallengeModifierType type)
        {
            if (type == ChallengeModifierType.None) return false;
            return _unlockedChallenges.Contains(type);
        }

        /// <summary>Unlock a challenge type permanently.</summary>
        public void UnlockChallenge(ChallengeModifierType type)
        {
            if (type == ChallengeModifierType.None) return;

            _unlockedChallenges.Add(type);
            string key = CHALLENGE_UNLOCK_KEY_PREFIX + type.ToString();
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

        }

        /// <summary>Get all unlocked challenges.</summary>
        public List<ChallengeModifierDefinition> GetUnlockedChallenges()
        {
            return _allChallenges.Where(c => IsChallengeUnlocked(c.Type)).ToList();
        }

        /// <summary>Get the number of unlocked challenges.</summary>
        public int GetUnlockedChallengeCount()
        {
            return _unlockedChallenges.Count;
        }

        #endregion

        #region Run Management

        /// <summary>Set challenges for the current run.</summary>
        public void SetChallengesForRun(List<ChallengeModifierDefinition> challenges)
        {
            _activeChallenges = challenges ?? new List<ChallengeModifierDefinition>();
            OnChallengesSet?.Invoke(_activeChallenges);
        }

        /// <summary>Set challenges by type for the current run.</summary>
        public void SetChallengesForRun(List<ChallengeModifierType> types)
        {
            var challenges = new List<ChallengeModifierDefinition>();
            foreach (var type in types)
            {
                var challenge = _allChallenges.FirstOrDefault(c => c.Type == type);
                if (challenge != null)
                {
                    challenges.Add(challenge);
                }
            }
            SetChallengesForRun(challenges);
        }

        /// <summary>Clear all active challenges (called at run end).</summary>
        public void ClearActiveChallenges()
        {
            _activeChallenges.Clear();
            OnChallengesCleared?.Invoke();
        }

        /// <summary>Called when a new run starts.</summary>
        public void OnRunStart()
        {
            // Active challenges should already be set via UI
        }

        /// <summary>Called when a run ends.</summary>
        public void OnRunEnd()
        {
            ClearActiveChallenges();
        }

        #endregion

        #region Effect Queries

        /// <summary>Get the combined Fae Dust multiplier from all active challenges.</summary>
        public float GetFaeDustMultiplier()
        {
            if (_activeChallenges.Count == 0) return 1.0f;

            float multiplier = 1.0f;
            foreach (var challenge in _activeChallenges)
            {
                multiplier *= challenge.FaeDustMultiplier;
            }
            return multiplier;
        }

        /// <summary>Get the combined power cost multiplier from all active challenges.</summary>
        public float GetPowerCostMultiplier()
        {
            if (_activeChallenges.Count == 0) return 1.0f;

            float multiplier = 1.0f;
            foreach (var challenge in _activeChallenges)
            {
                multiplier *= challenge.PowerCostMultiplier;
            }
            return multiplier;
        }

        /// <summary>Get the combined spawn interval multiplier from all active challenges.</summary>
        public float GetSpawnIntervalMultiplier()
        {
            if (_activeChallenges.Count == 0) return 1.0f;

            float multiplier = 1.0f;
            foreach (var challenge in _activeChallenges)
            {
                multiplier *= challenge.SpawnIntervalMultiplier;
            }
            return multiplier;
        }

        /// <summary>Check if visitor indicators should be hidden.</summary>
        public bool ShouldHideVisitorIndicators()
        {
            return _activeChallenges.Any(c => c.HideVisitorIndicators);
        }

        /// <summary>Check if prop essence is disabled.</summary>
        public bool IsPropEssenceDisabled()
        {
            return _activeChallenges.Any(c => c.DisablePropEssence);
        }

        /// <summary>Get the elite spawn chance (highest from active challenges).</summary>
        public float GetEliteSpawnChance()
        {
            if (_activeChallenges.Count == 0) return 0f;
            return _activeChallenges.Max(c => c.EliteSpawnChance);
        }

        /// <summary>Get the elite stat multiplier (highest from active challenges).</summary>
        public float GetEliteStatMultiplier()
        {
            if (_activeChallenges.Count == 0) return 1f;
            var elite = _activeChallenges.FirstOrDefault(c => c.HasEliteVisitors);
            return elite?.EliteStatMultiplier ?? 1f;
        }

        /// <summary>Get the elite reward multiplier (highest from active challenges).</summary>
        public float GetEliteRewardMultiplier()
        {
            if (_activeChallenges.Count == 0) return 1f;
            var elite = _activeChallenges.FirstOrDefault(c => c.HasEliteVisitors);
            return elite?.EliteRewardMultiplier ?? 1f;
        }

        /// <summary>Check if a specific challenge is active.</summary>
        public bool IsChallengeActive(ChallengeModifierType type)
        {
            return _activeChallenges.Any(c => c.Type == type);
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Unlock All Challenges")]
        private void DebugUnlockAll()
        {
            foreach (ChallengeModifierType type in Enum.GetValues(typeof(ChallengeModifierType)))
            {
                if (type != ChallengeModifierType.None)
                    UnlockChallenge(type);
            }
        }

        [ContextMenu("Debug: Log Challenge State")]
        private void DebugLogState()
        {
        }

        [ContextMenu("Reset All Challenge Unlocks")]
        private void ResetAllUnlocks()
        {
            foreach (ChallengeModifierType type in Enum.GetValues(typeof(ChallengeModifierType)))
            {
                string key = CHALLENGE_UNLOCK_KEY_PREFIX + type.ToString();
                PlayerPrefs.DeleteKey(key);
            }
            _unlockedChallenges.Clear();
            PlayerPrefs.Save();
        }

        #endregion
    }
}

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Manages blessing selection, storage, and effect application.
    /// Blessings are selected at run start and persist for the entire run.
    /// </summary>
    public class BlessingManager : MonoBehaviour
    {
        #region Singleton

        private static BlessingManager _instance;
        public static BlessingManager Instance => _instance;

        #endregion

        #region Events

        /// <summary>Fired when a blessing is selected for the current run.</summary>
        public event Action<BlessingDefinition> OnBlessingSelected;

        /// <summary>Fired when blessing selection is cleared (run end).</summary>
        public event Action OnBlessingCleared;

        #endregion

        #region State

        // All available blessing definitions (loaded from Resources or populated by editor)
        private List<BlessingDefinition> _allBlessings = new List<BlessingDefinition>();

        // Currently active blessing for this run (null = none selected)
        private BlessingDefinition _activeBlessing;

        // Track unlocked blessings (by type)
        private HashSet<BlessingType> _unlockedBlessings = new HashSet<BlessingType>();

        // PlayerPrefs key prefix for blessing unlocks
        private const string BLESSING_UNLOCK_KEY_PREFIX = "FaeMaze_Blessing_";

        #endregion

        #region Properties

        /// <summary>The currently active blessing for this run, or null if none.</summary>
        public BlessingDefinition ActiveBlessing => _activeBlessing;

        /// <summary>True if a blessing is currently active.</summary>
        public bool HasActiveBlessing => _activeBlessing != null;

        /// <summary>All registered blessing definitions.</summary>
        public IReadOnlyList<BlessingDefinition> AllBlessings => _allBlessings;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadBlessings();
                LoadUnlockedState();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Initialization

        private void LoadBlessings()
        {
            // Load all BlessingDefinition assets from Resources
            var loaded = Resources.LoadAll<BlessingDefinition>("Blessings");
            _allBlessings.AddRange(loaded);

            // If no Resources-based blessings, create hardcoded defaults
            if (_allBlessings.Count == 0)
            {
                CreateDefaultBlessings();
            }

            Debug.Log($"[BlessingManager] Loaded {_allBlessings.Count} blessings");
        }

        private void CreateDefaultBlessings()
        {
            // Create default blessing definitions programmatically
            // These will be used if no ScriptableObject assets exist

            _allBlessings.Add(CreateBlessing(
                BlessingType.GreedyHeart,
                "Greedy Heart",
                "+50% essence from consumed visitors, -25% starting essence",
                essenceMultiplier: 1.5f,
                startingEssenceMultiplier: 0.75f,
                cost: 100,
                prerequisite: "ReachEssence500"
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.PatientHunter,
                "Patient Hunter",
                "Visitors move 15% slower, spawn 20% faster",
                visitorSpeedMultiplier: 0.85f,
                spawnIntervalMultiplier: 0.8f,
                cost: 100,
                prerequisite: "Survive10Minutes"
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.DesperateGrasp,
                "Desperate Grasp",
                "Yoink! costs 0 essence when below 25 essence",
                cost: 75,
                desperateGraspThreshold: 25
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.SpreadingCorruption,
                "Spreading Corruption",
                "Props affect 25% larger area",
                propRadiusMultiplier: 1.25f,
                cost: 100
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.DevouringHunger,
                "Devouring Hunger",
                "Nom Nom is 50% faster, costs 25% more",
                mawSpeedMultiplier: 1.5f,
                mawCostMultiplier: 1.25f,
                cost: 100
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.ForestsFavor,
                "Forest's Favor",
                "Start with 1 extra Lantern placed",
                extraLanterns: 1,
                cost: 100
            ));

            _allBlessings.Add(CreateBlessing(
                BlessingType.VengefulSpirit,
                "Vengeful Spirit",
                "When essence drops below 50, all powers cost 50% less",
                vengefulThreshold: 50,
                cost: 125
            ));
        }

        private BlessingDefinition CreateBlessing(
            BlessingType type,
            string name,
            string description,
            float essenceMultiplier = 1f,
            float startingEssenceMultiplier = 1f,
            float visitorSpeedMultiplier = 1f,
            float spawnIntervalMultiplier = 1f,
            int desperateGraspThreshold = 0,
            float propRadiusMultiplier = 1f,
            float mawSpeedMultiplier = 1f,
            float mawCostMultiplier = 1f,
            int extraLanterns = 0,
            int vengefulThreshold = 0,
            int cost = 100,
            string prerequisite = "")
        {
            var blessing = ScriptableObject.CreateInstance<BlessingDefinition>();

            // Use reflection to set private fields since we can't use constructor
            var typeField = typeof(BlessingDefinition).GetField("blessingType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nameField = typeof(BlessingDefinition).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = typeof(BlessingDefinition).GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var costField = typeof(BlessingDefinition).GetField("faeDustCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var prereqField = typeof(BlessingDefinition).GetField("prerequisiteAchievement", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var essenceField = typeof(BlessingDefinition).GetField("essenceFromConsumptionMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var startEssenceField = typeof(BlessingDefinition).GetField("startingEssenceMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var visitorSpeedField = typeof(BlessingDefinition).GetField("visitorSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawnField = typeof(BlessingDefinition).GetField("spawnIntervalMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var desperateField = typeof(BlessingDefinition).GetField("desperateGraspThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var propRadiusField = typeof(BlessingDefinition).GetField("propRadiusMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mawSpeedField = typeof(BlessingDefinition).GetField("mawSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var mawCostField = typeof(BlessingDefinition).GetField("mawCostMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lanternField = typeof(BlessingDefinition).GetField("extraStartingLanterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var vengefulField = typeof(BlessingDefinition).GetField("vengefulSpiritThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            typeField?.SetValue(blessing, type);
            nameField?.SetValue(blessing, name);
            descField?.SetValue(blessing, description);
            costField?.SetValue(blessing, cost);
            prereqField?.SetValue(blessing, prerequisite);
            essenceField?.SetValue(blessing, essenceMultiplier);
            startEssenceField?.SetValue(blessing, startingEssenceMultiplier);
            visitorSpeedField?.SetValue(blessing, visitorSpeedMultiplier);
            spawnField?.SetValue(blessing, spawnIntervalMultiplier);
            desperateField?.SetValue(blessing, desperateGraspThreshold);
            propRadiusField?.SetValue(blessing, propRadiusMultiplier);
            mawSpeedField?.SetValue(blessing, mawSpeedMultiplier);
            mawCostField?.SetValue(blessing, mawCostMultiplier);
            lanternField?.SetValue(blessing, extraLanterns);
            vengefulField?.SetValue(blessing, vengefulThreshold);

            return blessing;
        }

        private void LoadUnlockedState()
        {
            foreach (BlessingType type in Enum.GetValues(typeof(BlessingType)))
            {
                if (type == BlessingType.None) continue;

                string key = BLESSING_UNLOCK_KEY_PREFIX + type.ToString();
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    _unlockedBlessings.Add(type);
                }
            }
        }

        #endregion

        #region Unlock Management

        /// <summary>Check if a blessing type is unlocked.</summary>
        public bool IsBlessingUnlocked(BlessingType type)
        {
            return _unlockedBlessings.Contains(type);
        }

        /// <summary>Unlock a blessing type permanently.</summary>
        public void UnlockBlessing(BlessingType type)
        {
            if (type == BlessingType.None) return;

            _unlockedBlessings.Add(type);
            string key = BLESSING_UNLOCK_KEY_PREFIX + type.ToString();
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

            Debug.Log($"[BlessingManager] Unlocked blessing: {type}");
        }

        /// <summary>Get all unlocked blessings.</summary>
        public List<BlessingDefinition> GetUnlockedBlessings()
        {
            return _allBlessings.Where(b => IsBlessingUnlocked(b.Type)).ToList();
        }

        /// <summary>Get a random selection of unlocked blessings for run start.</summary>
        public List<BlessingDefinition> GetRandomBlessingChoices(int count = 3)
        {
            var unlocked = GetUnlockedBlessings();
            if (unlocked.Count <= count)
                return unlocked;

            // Shuffle and take first N
            var shuffled = unlocked.OrderBy(_ => UnityEngine.Random.value).ToList();
            return shuffled.Take(count).ToList();
        }

        #endregion

        #region Run Management

        /// <summary>Select a blessing for the current run.</summary>
        public void SelectBlessingForRun(BlessingDefinition blessing)
        {
            _activeBlessing = blessing;
            Debug.Log($"[BlessingManager] Selected blessing for run: {blessing?.DisplayName ?? "None"}");
            OnBlessingSelected?.Invoke(blessing);
        }

        /// <summary>Select a blessing by type for the current run.</summary>
        public void SelectBlessingForRun(BlessingType type)
        {
            var blessing = _allBlessings.FirstOrDefault(b => b.Type == type);
            SelectBlessingForRun(blessing);
        }

        /// <summary>Clear the active blessing (called at run end).</summary>
        public void ClearActiveBlessing()
        {
            _activeBlessing = null;
            OnBlessingCleared?.Invoke();
        }

        /// <summary>Called when a new run starts.</summary>
        public void OnRunStart()
        {
            // Active blessing should already be selected via UI
            // This is just for any additional initialization
        }

        /// <summary>Called when a run ends.</summary>
        public void OnRunEnd()
        {
            ClearActiveBlessing();
        }

        #endregion

        #region Effect Queries

        /// <summary>Get the essence multiplier from the active blessing.</summary>
        public float GetEssenceFromConsumptionMultiplier()
        {
            return _activeBlessing?.EssenceFromConsumptionMultiplier ?? 1.0f;
        }

        /// <summary>Get the starting essence multiplier from the active blessing.</summary>
        public float GetStartingEssenceMultiplier()
        {
            return _activeBlessing?.StartingEssenceMultiplier ?? 1.0f;
        }

        /// <summary>Get the visitor speed multiplier from the active blessing.</summary>
        public float GetVisitorSpeedMultiplier()
        {
            return _activeBlessing?.VisitorSpeedMultiplier ?? 1.0f;
        }

        /// <summary>Get the spawn interval multiplier from the active blessing.</summary>
        public float GetSpawnIntervalMultiplier()
        {
            return _activeBlessing?.SpawnIntervalMultiplier ?? 1.0f;
        }

        /// <summary>Get the prop radius multiplier from the active blessing.</summary>
        public float GetPropRadiusMultiplier()
        {
            return _activeBlessing?.PropRadiusMultiplier ?? 1.0f;
        }

        /// <summary>Get the Maw speed multiplier from the active blessing.</summary>
        public float GetMawSpeedMultiplier()
        {
            return _activeBlessing?.MawSpeedMultiplier ?? 1.0f;
        }

        /// <summary>Get the Maw cost multiplier from the active blessing.</summary>
        public float GetMawCostMultiplier()
        {
            return _activeBlessing?.MawCostMultiplier ?? 1.0f;
        }

        /// <summary>Get extra starting lanterns from the active blessing.</summary>
        public int GetExtraStartingLanterns()
        {
            return _activeBlessing?.ExtraStartingLanterns ?? 0;
        }

        /// <summary>Check if Desperate Grasp effect should apply at current essence.</summary>
        public bool ShouldGraspBeFree(int currentEssence)
        {
            if (_activeBlessing == null) return false;
            int threshold = _activeBlessing.DesperateGraspThreshold;
            return threshold > 0 && currentEssence < threshold;
        }

        /// <summary>Check if Vengeful Spirit effect should apply at current essence.</summary>
        public bool ShouldPowersCostLess(int currentEssence)
        {
            if (_activeBlessing == null) return false;
            int threshold = _activeBlessing.VengefulSpiritThreshold;
            return threshold > 0 && currentEssence < threshold;
        }

        /// <summary>Get the power cost multiplier considering Vengeful Spirit.</summary>
        public float GetPowerCostMultiplier(int currentEssence)
        {
            if (ShouldPowersCostLess(currentEssence))
                return 0.5f;
            return 1.0f;
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Unlock All Blessings")]
        private void DebugUnlockAll()
        {
            foreach (BlessingType type in Enum.GetValues(typeof(BlessingType)))
            {
                if (type != BlessingType.None)
                    UnlockBlessing(type);
            }
        }

        [ContextMenu("Debug: Log Blessing State")]
        private void DebugLogState()
        {
            Debug.Log($"[BlessingManager] Active: {_activeBlessing?.DisplayName ?? "None"}");
            Debug.Log($"[BlessingManager] Unlocked count: {_unlockedBlessings.Count}");
            foreach (var type in _unlockedBlessings)
                Debug.Log($"  - {type}");
        }

        [ContextMenu("Reset All Blessing Unlocks")]
        private void ResetAllUnlocks()
        {
            foreach (BlessingType type in Enum.GetValues(typeof(BlessingType)))
            {
                string key = BLESSING_UNLOCK_KEY_PREFIX + type.ToString();
                PlayerPrefs.DeleteKey(key);
            }
            _unlockedBlessings.Clear();
            PlayerPrefs.Save();
            Debug.Log("[BlessingManager] Reset all blessing unlocks");
        }

        #endregion
    }
}

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Manages Heart Form selection, storage, and effect application.
    /// Heart Forms are selected at run start and persist for the entire run.
    /// Forms act like "characters" providing different stat modifiers and playstyles.
    /// </summary>
    public class HeartFormManager : MonoBehaviour
    {
        #region Singleton

        private static HeartFormManager _instance;
        public static HeartFormManager Instance => _instance;

        #endregion

        #region Events

        /// <summary>Fired when a Heart Form is selected for the current run.</summary>
        public event Action<HeartFormDefinition> OnFormSelected;

        /// <summary>Fired when form selection is cleared (run end).</summary>
        public event Action OnFormCleared;

        #endregion

        #region State

        // All available Heart Form definitions
        private List<HeartFormDefinition> _allForms = new List<HeartFormDefinition>();

        // Currently active form for this run (never null - defaults to HungryHeart)
        private HeartFormDefinition _activeForm;

        // Track unlocked forms (by type)
        private HashSet<HeartFormType> _unlockedForms = new HashSet<HeartFormType>();

        // PlayerPrefs key prefix for form unlocks
        private const string FORM_UNLOCK_KEY_PREFIX = "FaeMaze_HeartForm_";

        // Accumulated essence decay (for sub-second tracking)
        private float _essenceDecayAccumulator = 0f;

        #endregion

        #region Properties

        /// <summary>The currently active Heart Form for this run.</summary>
        public HeartFormDefinition ActiveForm => _activeForm;

        /// <summary>All registered Heart Form definitions.</summary>
        public IReadOnlyList<HeartFormDefinition> AllForms => _allForms;

        /// <summary>True if a non-default form is active.</summary>
        public bool HasNonDefaultForm => _activeForm != null && _activeForm.Type != HeartFormType.HungryHeart;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                LoadForms();
                LoadUnlockedState();
                SetDefaultForm();
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Handle passive essence decay if active form has it
            if (_activeForm != null && _activeForm.HasPassiveEssenceDecay)
            {
                ApplyPassiveEssenceDecay();
            }
        }

        #endregion

        #region Initialization

        private void LoadForms()
        {
            // Load all HeartFormDefinition assets from Resources
            var loaded = Resources.LoadAll<HeartFormDefinition>("HeartForms");
            _allForms.AddRange(loaded);

            // If no Resources-based forms, create hardcoded defaults
            if (_allForms.Count == 0)
            {
                CreateDefaultForms();
            }

            Debug.Log($"[HeartFormManager] Loaded {_allForms.Count} Heart Forms");
        }

        private void CreateDefaultForms()
        {
            // Create default form definitions programmatically
            // These will be used if no ScriptableObject assets exist

            _allForms.Add(CreateForm(
                HeartFormType.HungryHeart,
                "Hungry Heart",
                "The default form. Balanced and reliable. No frills, no drawbacks.",
                isDefault: true,
                cost: 0
            ));

            _allForms.Add(CreateForm(
                HeartFormType.RavenousHeart,
                "Ravenous Heart",
                "An impatient predator. Your tongue strikes with ferocious speed, but you start hungrier than usual.",
                tongueSpeedMultiplier: 1.5f,
                startingEssenceModifier: -20,
                cost: 150
            ));

            _allForms.Add(CreateForm(
                HeartFormType.PatientHeart,
                "Patient Heart",
                "A cunning trapper. Your lanterns glow brighter, but the forest grows more dangerous hazards.",
                lanternEffectivenessMultiplier: 1.3f,
                hazardSpawnRateMultiplier: 1.25f,
                cost: 200
            ));

            _allForms.Add(CreateForm(
                HeartFormType.FamishedHeart,
                "Famished Heart",
                "An insatiable glutton. Every morsel tastes sweeter, but your hunger is never satisfied.",
                essenceRewardMultiplier: 1.5f,
                passiveEssenceDecay: 2f,
                cost: 250
            ));
        }

        private HeartFormDefinition CreateForm(
            HeartFormType type,
            string name,
            string description,
            float tongueSpeedMultiplier = 1f,
            int startingEssenceModifier = 0,
            float lanternEffectivenessMultiplier = 1f,
            float hazardSpawnRateMultiplier = 1f,
            float essenceRewardMultiplier = 1f,
            float passiveEssenceDecay = 0f,
            bool isDefault = false,
            int cost = 0)
        {
            var form = ScriptableObject.CreateInstance<HeartFormDefinition>();

            // Use reflection to set private fields since we can't use constructor
            var typeField = typeof(HeartFormDefinition).GetField("formType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nameField = typeof(HeartFormDefinition).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = typeof(HeartFormDefinition).GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var costField = typeof(HeartFormDefinition).GetField("faeDustCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var defaultField = typeof(HeartFormDefinition).GetField("isDefault", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tongueField = typeof(HeartFormDefinition).GetField("tongueSpeedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var startEssenceField = typeof(HeartFormDefinition).GetField("startingEssenceModifier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lanternField = typeof(HeartFormDefinition).GetField("lanternEffectivenessMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hazardField = typeof(HeartFormDefinition).GetField("hazardSpawnRateMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rewardField = typeof(HeartFormDefinition).GetField("essenceRewardMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var decayField = typeof(HeartFormDefinition).GetField("passiveEssenceDecayPerSecond", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            typeField?.SetValue(form, type);
            nameField?.SetValue(form, name);
            descField?.SetValue(form, description);
            costField?.SetValue(form, cost);
            defaultField?.SetValue(form, isDefault);
            tongueField?.SetValue(form, tongueSpeedMultiplier);
            startEssenceField?.SetValue(form, startingEssenceModifier);
            lanternField?.SetValue(form, lanternEffectivenessMultiplier);
            hazardField?.SetValue(form, hazardSpawnRateMultiplier);
            rewardField?.SetValue(form, essenceRewardMultiplier);
            decayField?.SetValue(form, passiveEssenceDecay);

            return form;
        }

        private void LoadUnlockedState()
        {
            // HungryHeart is always unlocked
            _unlockedForms.Add(HeartFormType.HungryHeart);

            foreach (HeartFormType type in Enum.GetValues(typeof(HeartFormType)))
            {
                string key = FORM_UNLOCK_KEY_PREFIX + type.ToString();
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    _unlockedForms.Add(type);
                }
            }
        }

        private void SetDefaultForm()
        {
            // Set HungryHeart as the default active form
            _activeForm = _allForms.FirstOrDefault(f => f.Type == HeartFormType.HungryHeart);
            if (_activeForm == null && _allForms.Count > 0)
            {
                _activeForm = _allForms[0];
            }
        }

        #endregion

        #region Unlock Management

        /// <summary>Check if a Heart Form type is unlocked.</summary>
        public bool IsFormUnlocked(HeartFormType type)
        {
            // HungryHeart is always unlocked
            if (type == HeartFormType.HungryHeart) return true;
            return _unlockedForms.Contains(type);
        }

        /// <summary>Unlock a Heart Form type permanently.</summary>
        public void UnlockForm(HeartFormType type)
        {
            _unlockedForms.Add(type);
            string key = FORM_UNLOCK_KEY_PREFIX + type.ToString();
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

            Debug.Log($"[HeartFormManager] Unlocked Heart Form: {type}");
        }

        /// <summary>Get all unlocked Heart Forms.</summary>
        public List<HeartFormDefinition> GetUnlockedForms()
        {
            return _allForms.Where(f => IsFormUnlocked(f.Type)).ToList();
        }

        /// <summary>Get the number of unlocked forms (for UI display).</summary>
        public int GetUnlockedFormCount()
        {
            return _unlockedForms.Count;
        }

        #endregion

        #region Run Management

        /// <summary>Select a Heart Form for the current run.</summary>
        public void SelectFormForRun(HeartFormDefinition form)
        {
            _activeForm = form ?? _allForms.FirstOrDefault(f => f.Type == HeartFormType.HungryHeart);
            _essenceDecayAccumulator = 0f;
            Debug.Log($"[HeartFormManager] Selected form for run: {_activeForm?.DisplayName ?? "None"}");
            OnFormSelected?.Invoke(_activeForm);
        }

        /// <summary>Select a Heart Form by type for the current run.</summary>
        public void SelectFormForRun(HeartFormType type)
        {
            var form = _allForms.FirstOrDefault(f => f.Type == type);
            SelectFormForRun(form);
        }

        /// <summary>Reset to the default form (called at run end).</summary>
        public void ResetToDefaultForm()
        {
            SetDefaultForm();
            _essenceDecayAccumulator = 0f;
            OnFormCleared?.Invoke();
        }

        /// <summary>Called when a new run starts.</summary>
        public void OnRunStart()
        {
            _essenceDecayAccumulator = 0f;
            // Active form should already be selected via UI
        }

        /// <summary>Called when a run ends.</summary>
        public void OnRunEnd()
        {
            ResetToDefaultForm();
        }

        #endregion

        #region Effect Queries

        /// <summary>Get the tongue speed multiplier from the active form.</summary>
        public float GetTongueSpeedMultiplier()
        {
            return _activeForm?.TongueSpeedMultiplier ?? 1.0f;
        }

        /// <summary>Get the starting essence modifier from the active form.</summary>
        public int GetStartingEssenceModifier()
        {
            return _activeForm?.StartingEssenceModifier ?? 0;
        }

        /// <summary>Get the lantern effectiveness multiplier from the active form.</summary>
        public float GetLanternEffectivenessMultiplier()
        {
            return _activeForm?.LanternEffectivenessMultiplier ?? 1.0f;
        }

        /// <summary>Get the hazard spawn rate multiplier from the active form.</summary>
        public float GetHazardSpawnRateMultiplier()
        {
            return _activeForm?.HazardSpawnRateMultiplier ?? 1.0f;
        }

        /// <summary>Get the essence reward multiplier from the active form.</summary>
        public float GetEssenceRewardMultiplier()
        {
            return _activeForm?.EssenceRewardMultiplier ?? 1.0f;
        }

        /// <summary>Get the passive essence decay rate from the active form.</summary>
        public float GetPassiveEssenceDecayRate()
        {
            return _activeForm?.PassiveEssenceDecayPerSecond ?? 0f;
        }

        /// <summary>Check if the active form has passive essence decay.</summary>
        public bool HasPassiveEssenceDecay()
        {
            return _activeForm?.HasPassiveEssenceDecay ?? false;
        }

        #endregion

        #region Passive Essence Decay

        private void ApplyPassiveEssenceDecay()
        {
            if (_activeForm == null || !_activeForm.HasPassiveEssenceDecay) return;

            // Only apply during active gameplay (not paused, not in menus)
            if (Time.timeScale <= 0f) return;

            // Accumulate decay over frames
            _essenceDecayAccumulator += _activeForm.PassiveEssenceDecayPerSecond * Time.deltaTime;

            // Apply whole essence points
            if (_essenceDecayAccumulator >= 1f)
            {
                int decayAmount = Mathf.FloorToInt(_essenceDecayAccumulator);
                _essenceDecayAccumulator -= decayAmount;

                // Find GameController and apply decay
                var gameController = FindFirstObjectByType<Systems.GameController>();
                if (gameController != null)
                {
                    // Use AddEssence with negative value for decay
                    gameController.AddEssence(-decayAmount, Systems.EssenceSource.EssenceDecay);
                }
            }
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Unlock All Forms")]
        private void DebugUnlockAll()
        {
            foreach (HeartFormType type in Enum.GetValues(typeof(HeartFormType)))
            {
                UnlockForm(type);
            }
        }

        [ContextMenu("Debug: Log Form State")]
        private void DebugLogState()
        {
            Debug.Log($"[HeartFormManager] Active: {_activeForm?.DisplayName ?? "None"}");
            Debug.Log($"[HeartFormManager] Unlocked count: {_unlockedForms.Count}");
            foreach (var type in _unlockedForms)
                Debug.Log($"  - {type}");
        }

        [ContextMenu("Reset All Form Unlocks")]
        private void ResetAllUnlocks()
        {
            foreach (HeartFormType type in Enum.GetValues(typeof(HeartFormType)))
            {
                string key = FORM_UNLOCK_KEY_PREFIX + type.ToString();
                PlayerPrefs.DeleteKey(key);
            }
            _unlockedForms.Clear();
            _unlockedForms.Add(HeartFormType.HungryHeart); // HungryHeart always unlocked
            PlayerPrefs.Save();
            Debug.Log("[HeartFormManager] Reset all form unlocks");
        }

        #endregion
    }
}

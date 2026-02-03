using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using FaeMaze.Systems;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Manages Prop Mutation selection, storage, and effect application.
    /// Prop Mutations are selected at run start and modify how props interact with visitors.
    /// Only one mutation can be active per run.
    /// </summary>
    public class PropMutationManager : PersistentSingletonMonoBehaviour<PropMutationManager>
    {

        #region Events

        /// <summary>Fired when a mutation is selected for the current run.</summary>
        public event Action<PropMutationDefinition> OnMutationSelected;

        /// <summary>Fired when mutation selection is cleared (run end).</summary>
        public event Action OnMutationCleared;

        #endregion

        #region State

        // All available mutation definitions
        private List<PropMutationDefinition> _allMutations = new List<PropMutationDefinition>();

        // Currently active mutation for this run (null if none)
        private PropMutationDefinition _activeMutation;

        // Track unlocked mutations (by type)
        private HashSet<PropMutationType> _unlockedMutations = new HashSet<PropMutationType>();

        // PlayerPrefs key prefix for mutation unlocks
        private const string MUTATION_UNLOCK_KEY_PREFIX = "FaeMaze_PropMutation_";

        #endregion

        #region Properties

        /// <summary>The currently active mutation for this run (null if none).</summary>
        public PropMutationDefinition ActiveMutation => _activeMutation;

        /// <summary>All registered mutation definitions.</summary>
        public IReadOnlyList<PropMutationDefinition> AllMutations => _allMutations;

        /// <summary>True if a mutation is active.</summary>
        public bool HasActiveMutation => _activeMutation != null;

        #endregion

        #region Unity Lifecycle

        protected override void OnAwake()
        {
            LoadMutations();
            LoadUnlockedState();
        }

        #endregion

        #region Initialization

        private void LoadMutations()
        {
            // Load all PropMutationDefinition assets from Resources
            var loaded = Resources.LoadAll<PropMutationDefinition>("Mutations");
            _allMutations.AddRange(loaded);

            // If no Resources-based mutations, create hardcoded defaults
            if (_allMutations.Count == 0)
            {
                CreateDefaultMutations();
            }

            Debug.Log($"[PropMutationManager] Loaded {_allMutations.Count} mutations");
        }

        private void CreateDefaultMutations()
        {
            _allMutations.Add(CreateMutation(
                PropMutationType.GreedyGlow,
                "Greedy Glow",
                "Your lanterns burn brighter with greed. Lantern essence yield increased by 50%.",
                lanternMultiplier: 1.5f,
                cost: 100
            ));

            _allMutations.Add(CreateMutation(
                PropMutationType.RingTithe,
                "Ring Tithe",
                "The fairy ring owes you tribute. 50% of essence drained at rings flows to you.",
                ringTithe: 0.5f,
                cost: 100
            ));

            _allMutations.Add(CreateMutation(
                PropMutationType.DrowningGift,
                "Puka's Portion",
                "A dark bargain with the water spirit. 50% of drowned visitor essence is yours.",
                pukaShare: 0.5f,
                cost: 100
            ));
        }

        private PropMutationDefinition CreateMutation(
            PropMutationType type,
            string name,
            string description,
            float lanternMultiplier = 1f,
            float ringTithe = 0f,
            float pukaShare = 0f,
            int cost = 100)
        {
            var mutation = ScriptableObject.CreateInstance<PropMutationDefinition>();

            // Use reflection to set private fields
            var typeField = typeof(PropMutationDefinition).GetField("mutationType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var nameField = typeof(PropMutationDefinition).GetField("displayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var descField = typeof(PropMutationDefinition).GetField("description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var costField = typeof(PropMutationDefinition).GetField("faeDustCost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var lanternField = typeof(PropMutationDefinition).GetField("lanternAwardMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ringField = typeof(PropMutationDefinition).GetField("ringEssenceTithe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var pukaField = typeof(PropMutationDefinition).GetField("pukaEssenceShare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            typeField?.SetValue(mutation, type);
            nameField?.SetValue(mutation, name);
            descField?.SetValue(mutation, description);
            costField?.SetValue(mutation, cost);
            lanternField?.SetValue(mutation, lanternMultiplier);
            ringField?.SetValue(mutation, ringTithe);
            pukaField?.SetValue(mutation, pukaShare);

            return mutation;
        }

        private void LoadUnlockedState()
        {
            foreach (PropMutationType type in Enum.GetValues(typeof(PropMutationType)))
            {
                if (type == PropMutationType.None) continue;

                string key = MUTATION_UNLOCK_KEY_PREFIX + type.ToString();
                if (PlayerPrefs.GetInt(key, 0) == 1)
                {
                    _unlockedMutations.Add(type);
                }
            }
        }

        #endregion

        #region Unlock Management

        /// <summary>Check if a mutation type is unlocked.</summary>
        public bool IsMutationUnlocked(PropMutationType type)
        {
            if (type == PropMutationType.None) return false;
            return _unlockedMutations.Contains(type);
        }

        /// <summary>Unlock a mutation type permanently.</summary>
        public void UnlockMutation(PropMutationType type)
        {
            if (type == PropMutationType.None) return;

            _unlockedMutations.Add(type);
            string key = MUTATION_UNLOCK_KEY_PREFIX + type.ToString();
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();

            Debug.Log($"[PropMutationManager] Unlocked mutation: {type}");
        }

        /// <summary>Get all unlocked mutations.</summary>
        public List<PropMutationDefinition> GetUnlockedMutations()
        {
            return _allMutations.Where(m => IsMutationUnlocked(m.Type)).ToList();
        }

        /// <summary>Get the number of unlocked mutations.</summary>
        public int GetUnlockedMutationCount()
        {
            return _unlockedMutations.Count;
        }

        #endregion

        #region Run Management

        /// <summary>Select a mutation for the current run.</summary>
        public void SelectMutationForRun(PropMutationDefinition mutation)
        {
            _activeMutation = mutation;
            Debug.Log($"[PropMutationManager] Selected mutation for run: {_activeMutation?.DisplayName ?? "None"}");
            OnMutationSelected?.Invoke(_activeMutation);
        }

        /// <summary>Select a mutation by type for the current run.</summary>
        public void SelectMutationForRun(PropMutationType type)
        {
            var mutation = _allMutations.FirstOrDefault(m => m.Type == type);
            SelectMutationForRun(mutation);
        }

        /// <summary>Clear the active mutation (called at run end).</summary>
        public void ClearActiveMutation()
        {
            _activeMutation = null;
            OnMutationCleared?.Invoke();
        }

        /// <summary>Called when a new run starts.</summary>
        public void OnRunStart()
        {
            // Active mutation should already be selected via UI
        }

        /// <summary>Called when a run ends.</summary>
        public void OnRunEnd()
        {
            ClearActiveMutation();
        }

        #endregion

        #region Effect Queries

        /// <summary>Get the lantern essence award multiplier from the active mutation.</summary>
        public float GetLanternAwardMultiplier()
        {
            return _activeMutation?.LanternAwardMultiplier ?? 1.0f;
        }

        /// <summary>Get the fairy ring essence tithe percentage (0-1) from the active mutation.</summary>
        public float GetRingEssenceTithe()
        {
            return _activeMutation?.RingEssenceTithe ?? 0f;
        }

        /// <summary>Get the puka/pond essence share percentage (0-1) from the active mutation.</summary>
        public float GetPukaEssenceShare()
        {
            return _activeMutation?.PukaEssenceShare ?? 0f;
        }

        /// <summary>Check if a specific mutation type is active.</summary>
        public bool IsMutationActive(PropMutationType type)
        {
            return _activeMutation != null && _activeMutation.Type == type;
        }

        #endregion

        #region Debug

        [ContextMenu("Debug: Unlock All Mutations")]
        private void DebugUnlockAll()
        {
            foreach (PropMutationType type in Enum.GetValues(typeof(PropMutationType)))
            {
                if (type != PropMutationType.None)
                    UnlockMutation(type);
            }
        }

        [ContextMenu("Debug: Log Mutation State")]
        private void DebugLogState()
        {
            Debug.Log($"[PropMutationManager] Active: {_activeMutation?.DisplayName ?? "None"}");
            Debug.Log($"[PropMutationManager] Unlocked count: {_unlockedMutations.Count}");
            foreach (var type in _unlockedMutations)
                Debug.Log($"  - {type}");
        }

        [ContextMenu("Reset All Mutation Unlocks")]
        private void ResetAllUnlocks()
        {
            foreach (PropMutationType type in Enum.GetValues(typeof(PropMutationType)))
            {
                string key = MUTATION_UNLOCK_KEY_PREFIX + type.ToString();
                PlayerPrefs.DeleteKey(key);
            }
            _unlockedMutations.Clear();
            PlayerPrefs.Save();
            Debug.Log("[PropMutationManager] Reset all mutation unlocks");
        }

        #endregion
    }
}

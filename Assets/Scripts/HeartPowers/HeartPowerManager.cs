using System;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Central manager for Heart powers.
    /// Manages essence, charges, cooldowns, and power activation.
    /// </summary>
    public class HeartPowerManager : MonoBehaviour
    {
        #region Singleton

        private static HeartPowerManager _instance;
        public static HeartPowerManager Instance => _instance;

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the GameController")]
        private GameController gameController;

        [Header("Power Definitions")]
        [SerializeField]
        [Tooltip("Array of power definitions for each power type")]
        private HeartPowerDefinition[] powerDefinitions;

        [Header("Resource Settings")]
        [SerializeField]
        [Tooltip("Initial Heart charges at start of each wave")]
        private int initialChargesPerWave = 3;

        [SerializeField]
        [Tooltip("Current essence (persistent across waves)")]
        private int currentEssence = 10;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Enable debug logging")]
        private bool debugLog = true;

        #endregion

        #region Private Fields

        private int currentCharges;
        private Dictionary<HeartPowerType, float> cooldownTimers = new Dictionary<HeartPowerType, float>();
        private Dictionary<HeartPowerType, int> powerTiers = new Dictionary<HeartPowerType, int>();
        private Dictionary<HeartPowerType, bool> unlockedPowers = new Dictionary<HeartPowerType, bool>();

        private PathCostModifier pathCostModifier;
        private bool isGameActive = false;

        // Active power effects (for cleanup and state tracking)
        private Dictionary<HeartPowerType, ActivePowerEffect> activePowers = new Dictionary<HeartPowerType, ActivePowerEffect>();

        #endregion

        #region Properties

        /// <summary>Gets the current number of Heart charges</summary>
        public int CurrentCharges => currentCharges;

        /// <summary>Gets the current essence</summary>
        public int CurrentEssence => currentEssence;

        /// <summary>Gets the path cost modifier system</summary>
        public PathCostModifier PathModifier => pathCostModifier;

        /// <summary>Gets the maze grid behaviour</summary>
        public MazeGridBehaviour MazeGrid => mazeGridBehaviour;

        /// <summary>Gets the game controller</summary>
        public GameController GameController => gameController;

        #endregion

        #region Events

        public event Action<HeartPowerType> OnPowerActivated;
        public event Action<int> OnChargesChanged;
        public event Action<int> OnEssenceChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Initialize path cost modifier
            if (mazeGridBehaviour != null)
            {
                pathCostModifier = new PathCostModifier(mazeGridBehaviour);
            }

            // Initialize all powers as locked, tier 1
            foreach (HeartPowerType powerType in Enum.GetValues(typeof(HeartPowerType)))
            {
                cooldownTimers[powerType] = 0f;
                powerTiers[powerType] = 1;
                unlockedPowers[powerType] = true; // Start with all unlocked for testing
            }
        }

        private void Update()
        {
            if (!isGameActive)
            {
                return;
            }

            // Update cooldowns
            foreach (HeartPowerType powerType in Enum.GetValues(typeof(HeartPowerType)))
            {
                if (cooldownTimers[powerType] > 0)
                {
                    cooldownTimers[powerType] -= Time.deltaTime;
                    if (cooldownTimers[powerType] < 0)
                    {
                        cooldownTimers[powerType] = 0;
                    }
                }
            }

            // Cleanup expired path modifiers
            pathCostModifier?.CleanupExpired();

            // Update active power effects
            List<HeartPowerType> toRemove = new List<HeartPowerType>();
            foreach (var kvp in activePowers)
            {
                kvp.Value.Update(Time.deltaTime);
                if (kvp.Value.IsExpired)
                {
                    kvp.Value.OnEnd();
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var powerType in toRemove)
            {
                activePowers.Remove(powerType);
            }
        }

        #endregion

        #region Public Methods - Wave Integration

        /// <summary>
        /// Called at the start of each wave to reset/refill Heart charges.
        /// </summary>
        public void OnWaveStart()
        {
            currentCharges = initialChargesPerWave;
            isGameActive = true;

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Wave started. Charges reset to {currentCharges}");
            }

            OnChargesChanged?.Invoke(currentCharges);
        }

        /// <summary>
        /// Called when a wave ends successfully.
        /// </summary>
        public void OnWaveSuccess()
        {
            // Optionally grant bonus charges or essence
            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Wave succeeded.");
            }
        }

        /// <summary>
        /// Called when a wave fails or game is over.
        /// </summary>
        public void OnWaveFail()
        {
            isGameActive = false;
            CleanupAllEffects();

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Wave failed. Cleaning up effects.");
            }
        }

        #endregion

        #region Public Methods - Power Activation

        /// <summary>
        /// Attempts to activate a targeted Heart power (requires world position).
        /// </summary>
        public bool TryActivatePower(HeartPowerType powerType, Vector3 worldPosition)
        {
            if (!CanActivatePower(powerType, out string reason))
            {
                if (debugLog)
                {
                    Debug.LogWarning($"[HeartPowerManager] Cannot activate {powerType}: {reason}");
                }
                return false;
            }

            HeartPowerDefinition definition = GetPowerDefinition(powerType);
            if (definition == null)
            {
                Debug.LogError($"[HeartPowerManager] No definition found for {powerType}");
                return false;
            }

            // Consume charges and start cooldown
            ConsumeCharges(definition.chargeCost);
            cooldownTimers[powerType] = definition.cooldown;

            // Activate the power
            ActivatePower(powerType, definition, worldPosition);

            OnPowerActivated?.Invoke(powerType);

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Activated {powerType} at {worldPosition}");
            }

            return true;
        }

        /// <summary>
        /// Attempts to activate a global Heart power (no target required).
        /// </summary>
        public bool TryActivatePower(HeartPowerType powerType)
        {
            return TryActivatePower(powerType, Vector3.zero);
        }

        /// <summary>
        /// Checks if a power can be activated.
        /// </summary>
        public bool CanActivatePower(HeartPowerType powerType, out string reason)
        {
            if (!isGameActive)
            {
                reason = "Game not active";
                return false;
            }

            if (!unlockedPowers.GetValueOrDefault(powerType, false))
            {
                reason = "Power not unlocked";
                return false;
            }

            HeartPowerDefinition definition = GetPowerDefinition(powerType);
            if (definition == null)
            {
                reason = "No definition found";
                return false;
            }

            if (currentCharges < definition.chargeCost)
            {
                reason = $"Not enough charges (need {definition.chargeCost}, have {currentCharges})";
                return false;
            }

            if (cooldownTimers.GetValueOrDefault(powerType, 0) > 0)
            {
                reason = $"On cooldown ({cooldownTimers[powerType]:F1}s remaining)";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// Gets the remaining cooldown time for a power.
        /// </summary>
        public float GetCooldownRemaining(HeartPowerType powerType)
        {
            return cooldownTimers.GetValueOrDefault(powerType, 0f);
        }

        /// <summary>
        /// Gets the power definition for a given power type.
        /// </summary>
        public HeartPowerDefinition GetPowerDefinition(HeartPowerType powerType)
        {
            if (powerDefinitions == null)
            {
                return null;
            }

            int currentTier = powerTiers.GetValueOrDefault(powerType, 1);

            foreach (var def in powerDefinitions)
            {
                if (def.powerType == powerType && def.tier == currentTier)
                {
                    return def;
                }
            }

            return null;
        }

        #endregion

        #region Public Methods - Resources

        /// <summary>
        /// Adds essence to the player's pool.
        /// </summary>
        public void AddEssence(int amount)
        {
            currentEssence += amount;
            OnEssenceChanged?.Invoke(currentEssence);

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Added {amount} essence. Total: {currentEssence}");
            }
        }

        /// <summary>
        /// Spends essence (returns true if successful).
        /// </summary>
        public bool SpendEssence(int amount)
        {
            if (currentEssence < amount)
            {
                return false;
            }

            currentEssence -= amount;
            OnEssenceChanged?.Invoke(currentEssence);

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Spent {amount} essence. Remaining: {currentEssence}");
            }

            return true;
        }

        /// <summary>
        /// Adds Heart charges (temporary for current wave).
        /// </summary>
        public void AddCharges(int amount)
        {
            currentCharges += amount;
            OnChargesChanged?.Invoke(currentCharges);

            if (debugLog)
            {
                Debug.Log($"[HeartPowerManager] Added {amount} charges. Total: {currentCharges}");
            }
        }

        #endregion

        #region Private Methods

        private void ConsumeCharges(int amount)
        {
            currentCharges -= amount;
            OnChargesChanged?.Invoke(currentCharges);
        }

        private void ActivatePower(HeartPowerType powerType, HeartPowerDefinition definition, Vector3 worldPosition)
        {
            // Dispatch to specific power implementations
            ActivePowerEffect effect = null;

            switch (powerType)
            {
                case HeartPowerType.HeartbeatOfLonging:
                    effect = new HeartbeatOfLongingEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.MurmuringPaths:
                    effect = new MurmuringPathsEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.DreamSnare:
                    effect = new DreamSnareEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.FeastwardPanic:
                    effect = new FeastwardPanicEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.CovenantWithWisps:
                    effect = new CovenantWithWispsEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.PukasBargain:
                    effect = new PukasBargainEffect(this, definition, worldPosition);
                    break;

                case HeartPowerType.RingOfInvitations:
                    effect = new RingOfInvitationsEffect(this, definition, worldPosition);
                    break;

                default:
                    Debug.LogWarning($"[HeartPowerManager] Power {powerType} not yet implemented");
                    return;
            }

            if (effect != null)
            {
                effect.OnStart();
                if (effect.Duration > 0)
                {
                    activePowers[powerType] = effect;
                }
                else
                {
                    // Instant effect, trigger OnEnd immediately
                    effect.OnEnd();
                }
            }
        }

        private void CleanupAllEffects()
        {
            foreach (var effect in activePowers.Values)
            {
                effect.OnEnd();
            }

            activePowers.Clear();
            pathCostModifier?.ClearAll();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (pathCostModifier == null || !Application.isPlaying)
            {
                return;
            }

            // Draw modified tiles
            foreach (var tile in pathCostModifier.GetModifiedTiles())
            {
                float modifier = pathCostModifier.GetTotalModifier(tile);
                Vector3 worldPos = mazeGridBehaviour.GridToWorld(tile.x, tile.y);

                // Color based on modifier: green = cheaper, red = more expensive
                Color color = modifier < 0 ? Color.green : Color.red;
                color.a = 0.5f;

                Gizmos.color = color;
                Gizmos.DrawCube(worldPos, Vector3.one * mazeGridBehaviour.TileSize * 0.8f);
            }
        }

        #endregion
    }

    #region Helper Classes

    /// <summary>
    /// Base class for active power effects that persist over time.
    /// </summary>
    public abstract class ActivePowerEffect
    {
        protected HeartPowerManager manager;
        protected HeartPowerDefinition definition;
        protected Vector3 targetPosition;
        protected float elapsedTime;

        public float Duration => definition.duration;
        public bool IsExpired => elapsedTime >= Duration && Duration > 0;

        protected ActivePowerEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
        {
            this.manager = manager;
            this.definition = definition;
            this.targetPosition = targetPosition;
            this.elapsedTime = 0f;
        }

        public virtual void OnStart() { }
        public virtual void Update(float deltaTime) { elapsedTime += deltaTime; }
        public virtual void OnEnd() { }
    }

    #endregion
}

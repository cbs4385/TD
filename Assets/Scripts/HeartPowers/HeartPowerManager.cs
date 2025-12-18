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
        #region Static Cache

        // Cached array of all HeartPowerType enum values to avoid repeated Enum.GetValues() calls
        private static readonly HeartPowerType[] _allPowerTypes;

        static HeartPowerManager()
        {
            _allPowerTypes = (HeartPowerType[])Enum.GetValues(typeof(HeartPowerType));
        }

        #endregion

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

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Enable debug logging")]
        private bool debugLog = true;

        [Header("UI Settings")]
        [SerializeField]
        [Tooltip("Automatically create the Heart Powers UI panel if not present")]
        private bool autoCreateUI = true;

        #endregion

        #region Private Fields

        private int currentCharges;
        private Dictionary<HeartPowerType, float> cooldownTimers = new Dictionary<HeartPowerType, float>();
        private Dictionary<HeartPowerType, int> powerTiers = new Dictionary<HeartPowerType, int>();
        private Dictionary<HeartPowerType, bool> unlockedPowers = new Dictionary<HeartPowerType, bool>();

        private PathCostModifier pathCostModifier;
        private HeartPowerTileVisualizer tileVisualizer;
        private bool isGameActive = false;

        // Active power effects (for cleanup and state tracking)
        private Dictionary<HeartPowerType, ActivePowerEffect> activePowers = new Dictionary<HeartPowerType, ActivePowerEffect>();

        // Reusable list for removing expired powers (avoids GC allocation every frame)
        private readonly List<HeartPowerType> _powersToRemove = new List<HeartPowerType>();

        #endregion

        #region Properties

        /// <summary>Gets the current number of Heart charges</summary>
        public int CurrentCharges => currentCharges;

        /// <summary>Gets the current essence from GameController</summary>
        public int CurrentEssence => gameController != null ? gameController.CurrentEssence : 0;

        /// <summary>Gets the path cost modifier system</summary>
        public PathCostModifier PathModifier => pathCostModifier;

        /// <summary>Gets the tile visualizer for Heart Power effects</summary>
        public HeartPowerTileVisualizer TileVisualizer => tileVisualizer;

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

            // Initialize all powers as locked, tier 1 (using cached array)
            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                cooldownTimers[powerType] = 0f;
                powerTiers[powerType] = 1;
                unlockedPowers[powerType] = true; // Start with all unlocked for testing
            }
        }

        private void Start()
        {
            // Find GameController - do this in Start() to ensure it's initialized
            if (gameController == null)
            {
                gameController = GameController.Instance;
                if (gameController == null)
                {
                    gameController = FindFirstObjectByType<GameController>();
                }
            }

            if (gameController == null)
            {
            }
            else if (debugLog)
            {
            }

            // Find MazeGridBehaviour if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            // Initialize path cost modifier
            if (mazeGridBehaviour != null)
            {
                pathCostModifier = new PathCostModifier(mazeGridBehaviour);
            }

            // Initialize tile visualizer
            CreateTileVisualizerIfNeeded();

            // Auto-create UI if enabled and not present
            if (autoCreateUI)
            {
                CreateHeartPowersUIIfNeeded();
            }
        }

        private void CreateTileVisualizerIfNeeded()
        {
            // Check if HeartPowerTileVisualizer already exists
            tileVisualizer = FindFirstObjectByType<HeartPowerTileVisualizer>();
            if (tileVisualizer == null)
            {
                // Create a new GameObject for the tile visualizer
                GameObject visualizerObj = new GameObject("HeartPowerTileVisualizer");
                visualizerObj.transform.SetParent(transform);
                tileVisualizer = visualizerObj.AddComponent<HeartPowerTileVisualizer>();

                if (debugLog)
                {
                }
            }
        }

        private void CreateHeartPowersUIIfNeeded()
        {
            // Check if HeartPowerPanelController already exists
            var existingPanel = FindFirstObjectByType<UI.HeartPowerPanelController>();
            if (existingPanel == null)
            {
                // Create a new GameObject for the panel controller
                GameObject panelObj = new GameObject("HeartPowerPanelController");
                panelObj.transform.SetParent(transform);
                var panelController = panelObj.AddComponent<UI.HeartPowerPanelController>();

                if (debugLog)
                {
                }
            }
        }

        private void Update()
        {
            if (!isGameActive)
            {
                return;
            }

            // Update cooldowns (using cached array to avoid Enum.GetValues allocation)
            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                if (cooldownTimers.TryGetValue(powerType, out float cooldown) && cooldown > 0)
                {
                    cooldownTimers[powerType] = Mathf.Max(0, cooldown - Time.deltaTime);
                }
            }

            // Cleanup expired path modifiers
            pathCostModifier?.CleanupExpired();

            // Update active power effects (using reusable list to avoid GC allocation)
            _powersToRemove.Clear();
            foreach (var kvp in activePowers)
            {
                kvp.Value.Update(Time.deltaTime);
                if (kvp.Value.IsExpired)
                {
                    kvp.Value.OnEnd();
                    _powersToRemove.Add(kvp.Key);
                }
            }

            foreach (var powerType in _powersToRemove)
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
            }
        }

        #endregion

        #region Public Methods - Power Activation

        /// <summary>
        /// Attempts to activate a targeted Heart power (requires world position).
        /// </summary>
        public bool TryActivatePower(HeartPowerType powerType, Vector3 worldPosition)
        {
            if (debugLog)
            {
            }

            if (!CanActivatePower(powerType, out string reason))
            {
                if (debugLog)
                {
                }
                return false;
            }

            HeartPowerDefinition definition = GetPowerDefinition(powerType);
            if (definition == null)
            {
                return false;
            }

            if (debugLog)
            {
            }

            // Consume charges and start cooldown
            ConsumeCharges(definition.chargeCost);
            cooldownTimers[powerType] = definition.cooldown;

            // Activate the power
            ActivatePower(powerType, definition, worldPosition);

            OnPowerActivated?.Invoke(powerType);

            // If this power affects pathfinding, trigger all visitors to recalculate their paths
            if (PowerAffectsPathfinding(powerType))
            {
                TriggerVisitorPathRecalculation(powerType);
            }

            if (debugLog)
            {
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
        /// Adds essence to the player's pool via GameController.
        /// </summary>
        public void AddEssence(int amount)
        {
            if (gameController != null)
            {
                gameController.AddEssence(amount);

                if (debugLog)
                {
                }

                // Notify listeners (for UI updates)
                OnEssenceChanged?.Invoke(CurrentEssence);
            }
        }

        /// <summary>
        /// Spends essence via GameController (returns true if successful).
        /// </summary>
        public bool SpendEssence(int amount)
        {
            if (gameController != null && gameController.TrySpendEssence(amount))
            {
                if (debugLog)
                {
                }

                // Notify listeners (for UI updates)
                OnEssenceChanged?.Invoke(CurrentEssence);
                return true;
            }

            return false;
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
            }
        }

        #endregion

        #region Private Methods

        private void ConsumeCharges(int amount)
        {
            int previousCharges = currentCharges;
            currentCharges -= amount;

            if (debugLog)
            {
            }

            OnChargesChanged?.Invoke(currentCharges);
        }

        private void ActivatePower(HeartPowerType powerType, HeartPowerDefinition definition, Vector3 worldPosition)
        {
            // Dispatch to specific power implementations
            ActivePowerEffect effect = null;

            if (debugLog)
            {
            }

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
                    return;
            }

            if (effect != null)
            {
                if (debugLog)
                {
                }

                effect.OnStart();

                if (effect.Duration > 0)
                {
                    activePowers[powerType] = effect;
                    if (debugLog)
                    {
                    }
                }
                else
                {
                    // Instant effect, trigger OnEnd immediately
                    if (debugLog)
                    {
                    }
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

            // Clear all Lured states when all effects are cleaned up
            var activeVisitors = FaeMaze.Visitors.VisitorController.All;
            if (activeVisitors != null)
            {
                foreach (var visitor in activeVisitors)
                {
                    if (visitor != null && visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Lured)
                    {
                        visitor.SetLured(false);
                    }
                }
            }
        }

        #endregion

        #region Visitor Path Recalculation

        /// <summary>
        /// Checks if a power type affects pathfinding costs and requires visitor path recalculation.
        /// </summary>
        private bool PowerAffectsPathfinding(HeartPowerType powerType)
        {
            switch (powerType)
            {
                case HeartPowerType.HeartbeatOfLonging:
                case HeartPowerType.MurmuringPaths:
                case HeartPowerType.DreamSnare:
                case HeartPowerType.FeastwardPanic:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Triggers all active visitors to recalculate their paths after a power modifies grid costs.
        /// </summary>
        private void TriggerVisitorPathRecalculation(HeartPowerType powerType)
        {
            // Get all active visitors using the static registry
            var activeVisitors = FaeMaze.Visitors.VisitorController.All;

            if (activeVisitors == null || activeVisitors.Count == 0)
            {
                if (debugLog)
                {
                }
                return;
            }

            int recalculatedCount = 0;
            bool isMurmuringPaths = (powerType == HeartPowerType.MurmuringPaths);

            foreach (var visitor in activeVisitors)
            {
                if (visitor != null && visitor.State != FaeMaze.Visitors.VisitorControllerBase.VisitorState.Consumed
                    && visitor.State != FaeMaze.Visitors.VisitorControllerBase.VisitorState.Escaping
                    && visitor.State != FaeMaze.Visitors.VisitorControllerBase.VisitorState.Fascinated)
                {
                    // MurmuringPaths lures visitors toward the Heart
                    if (isMurmuringPaths && visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Walking)
                    {
                        visitor.SetLured(true);  // SetLured internally calls RecalculatePath()
                        recalculatedCount++;
                    }
                    else if (!isMurmuringPaths)
                    {
                        // For other powers, just recalculate paths due to attraction changes
                        visitor.RecalculatePath();
                        recalculatedCount++;
                    }
                }
            }

            if (debugLog)
            {
            }
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

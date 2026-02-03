using System;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Roguelike;
using Object = UnityEngine.Object;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Central manager for Heart powers.
    /// Manages essence, cooldowns, and power activation.
    /// </summary>
    public class HeartPowerManager : SingletonMonoBehaviour<HeartPowerManager>
    {
        #region Static Cache

        // Cached array of all HeartPowerType enum values to avoid repeated Enum.GetValues() calls
        private static readonly HeartPowerType[] _allPowerTypes;

        static HeartPowerManager()
        {
            _allPowerTypes = (HeartPowerType[])Enum.GetValues(typeof(HeartPowerType));
        }

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

        [Header("UI Settings")]
        [SerializeField]
        [Tooltip("Automatically create the Heart Powers UI panel if not present")]
        private bool autoCreateUI = true;

        [Header("Power Prefabs")]
        [SerializeField]
        [Tooltip("Prefab for the grasp hand visual (Heart Power 2) - DEPRECATED, use TonguePrefab")]
        private GameObject graspPrefab;

        [SerializeField]
        [Tooltip("Prefab for the tongue/tentacle visual (Heart Power 2)")]
        private GameObject tonguePrefab;

        [SerializeField]
        [Tooltip("Prefab for the devour visual (Heart Power 3)")]
        private GameObject devourPrefab;

        #endregion

        #region Private Fields

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

        // Factory for creating power effect instances
        private HeartPowerEffectFactory effectFactory;

        #endregion

        #region Properties

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

        /// <summary>Gets the grasp prefab for HeartwardGrasp power - DEPRECATED</summary>
        public GameObject GraspPrefab => graspPrefab;

        /// <summary>Gets the tongue prefab for HeartwardGrasp power</summary>
        public GameObject TonguePrefab => tonguePrefab;

        /// <summary>Gets the devour prefab for DevouringMaw power</summary>
        public GameObject DevourPrefab => devourPrefab;

        #endregion

        #region Events

        public event Action<HeartPowerType> OnPowerActivated;
        public event Action<HeartPowerType> OnPowerDeactivated;
        public event Action<int> OnEssenceChanged;

        #endregion

        #region Unity Lifecycle

        protected override void OnAwake()
        {
            // Initialize the effect factory
            effectFactory = new HeartPowerEffectFactory();

            // Load prefabs dynamically if not assigned via inspector
            LoadPrefabsIfNeeded();

            // Load power definitions from Resources if not set
            LoadPowerDefinitionsFromResources();

            // Initialize all powers as locked, tier 1 (using cached array)
            foreach (HeartPowerType powerType in _allPowerTypes)
            {
                cooldownTimers[powerType] = 0f;
                powerTiers[powerType] = 1;
                unlockedPowers[powerType] = true; // Start with all unlocked for testing
            }
        }

        private void LoadPrefabsIfNeeded()
        {
#if UNITY_EDITOR
            if (graspPrefab == null)
            {
                graspPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/grasp.prefab");
            }
            if (tonguePrefab == null)
            {
                tonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heart tongue.prefab");
            }
            if (devourPrefab == null)
            {
                devourPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/devour.prefab");
            }
#else
            // In builds, use Resources.Load
            if (graspPrefab == null)
            {
                graspPrefab = Resources.Load<GameObject>("Prefabs/Props/grasp");
            }
            if (tonguePrefab == null)
            {
                tonguePrefab = Resources.Load<GameObject>("Prefabs/Tile/heart tongue");
            }
            if (devourPrefab == null)
            {
                devourPrefab = Resources.Load<GameObject>("Prefabs/Props/devour");
            }
#endif
        }

        private void LoadPowerDefinitionsFromResources()
        {
            // If definitions are already assigned via inspector, use those
            if (powerDefinitions != null && powerDefinitions.Length > 0)
            {
                return;
            }

#if UNITY_EDITOR
            // Load all HeartPowerDefinition assets from ScriptableObjects folder using AssetDatabase
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:HeartPowerDefinition", new[] { "Assets/ScriptableObjects/HeartPowers" });
            var loadedDefinitions = new List<HeartPowerDefinition>();

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<HeartPowerDefinition>(path);
                if (def != null)
                {
                    loadedDefinitions.Add(def);
                }
            }

            if (loadedDefinitions.Count > 0)
            {
                powerDefinitions = loadedDefinitions.ToArray();
            }
#endif
        }

        private void Start()
        {
            // Powers should be available as soon as the game scene loads
            // OnWaveFail() will set this to false when the game is over
            isGameActive = true;

            // Find GameController - do this in Start() to ensure it's initialized
            if (gameController == null)
            {
                gameController = GameController.Instance;
                if (gameController == null)
                {
                    gameController = FindFirstObjectByType<GameController>();
                }
            }

            // Find MazeGridBehaviour if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            // Initialize path cost modifier (world-space, no maze reference needed)
            pathCostModifier = new PathCostModifier();

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
                panelObj.AddComponent<UI.HeartPowerPanelController>();
            }
        }

        private static int screenshotCounter = 0;

        private void Update()
        {
            // Screenshot capture - configurable key (always available)
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.ScreenshotBinding))
            {
                CaptureScreenshot();
            }

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
                // For toggle powers WITH cooldown (like DevouringMaw), start cooldown when the effect expires
                // Other toggle powers (MurmuringPaths, HeartwardGrasp, Sculpting) can be reused immediately
                if (IsTogglePowerWithCooldown(powerType))
                {
                    var definition = GetPowerDefinition(powerType);
                    if (definition != null && definition.cooldown > 0)
                    {
                        cooldownTimers[powerType] = definition.cooldown;
                    }
                }

                activePowers.Remove(powerType);
                OnPowerDeactivated?.Invoke(powerType);
            }
        }

        private void CaptureScreenshot()
        {
            StartCoroutine(CaptureScreenshotCoroutine());
        }

        private System.Collections.IEnumerator CaptureScreenshotCoroutine()
        {
            yield return new WaitForEndOfFrame();

            string basePath = FaeMaze.Systems.GameSettings.ScreenshotPath;
            System.IO.Directory.CreateDirectory(basePath);

            screenshotCounter++;
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filename = $"screenshot_{timestamp}_{screenshotCounter:D3}.png";
            string fullPath = System.IO.Path.Combine(basePath, filename);

            Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenshot.Apply();

            byte[] bytes = screenshot.EncodeToPNG();
            System.IO.File.WriteAllBytes(fullPath, bytes);

            Object.Destroy(screenshot);
        }

        #endregion

        #region Public Methods - Wave Integration

        /// <summary>
        /// Called at the start of each wave.
        /// </summary>
        public void OnWaveStart()
        {
            isGameActive = true;
        }

        /// <summary>
        /// Called when a wave fails or game is over.
        /// </summary>
        public void OnWaveFail()
        {
            isGameActive = false;
            CleanupAllEffects();
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
                return false;
            }

            HeartPowerDefinition definition = GetPowerDefinition(powerType);
            if (definition == null)
            {
                return false;
            }

            // Consume essence (accounting for blessing modifiers)
            int effectiveCost = GetEffectivePowerCost(powerType, definition);
            if (effectiveCost > 0)
            {
                SpendEssence(effectiveCost);
            }

            // Only start cooldown for non-toggle powers
            if (!IsTogglePower(powerType))
            {
                cooldownTimers[powerType] = definition.cooldown;
            }

            // Activate the power
            ActivatePower(powerType, definition, worldPosition);

            OnPowerActivated?.Invoke(powerType);

            // Record power activation for meta-progression
            MetaProgressionManager.Instance?.RecordPowerActivation(powerType.ToString());

            // If this power affects pathfinding, trigger all visitors to recalculate their paths
            if (PowerAffectsPathfinding(powerType))
            {
                TriggerVisitorPathRecalculation(powerType);
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

            int effectiveCost = GetEffectivePowerCost(powerType, definition);
            if (effectiveCost > 0 && CurrentEssence < effectiveCost)
            {
                reason = $"Not enough essence (need {effectiveCost}, have {CurrentEssence})";
                return false;
            }

            // Toggle powers (like MurmuringPaths) cannot be activated while already active
            if (IsTogglePower(powerType) && IsPowerActive(powerType))
            {
                reason = "Power is already active";
                return false;
            }

            // Check cooldowns for non-toggle powers OR toggle powers with cooldown (like DevouringMaw)
            bool hasCooldown = !IsTogglePower(powerType) || IsTogglePowerWithCooldown(powerType);
            if (hasCooldown && cooldownTimers.GetValueOrDefault(powerType, 0) > 0)
            {
                reason = $"On cooldown ({cooldownTimers[powerType]:F1}s remaining)";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// Calculates the effective essence cost for a power, accounting for blessing effects.
        /// - Desperate Grasp: HeartwardGrasp costs 0 when below threshold
        /// - Vengeful Spirit: All powers cost 50% less when below threshold
        /// - Devouring Hunger: DevouringMaw costs 25% more
        /// </summary>
        public int GetEffectivePowerCost(HeartPowerType powerType, HeartPowerDefinition definition)
        {
            if (definition == null)
                return 0;

            // Calculate base cost as percentage of starting essence
            int startingEssence = GameSettings.StartingEssence;
            int baseCost;

            // Use percentage-based cost if set, otherwise fall back to legacy fixed cost
            if (definition.essenceCostPercent > 0f)
            {
                baseCost = Mathf.RoundToInt(startingEssence * definition.essenceCostPercent);
            }
            else if (definition.essenceCost > 0)
            {
                // Legacy fallback for old assets
                baseCost = definition.essenceCost;
            }
            else
            {
                return 0;
            }

            int currentEssence = CurrentEssence;

            // Desperate Grasp: HeartwardGrasp costs 0 when below essence threshold
            if (powerType == HeartPowerType.HeartwardGrasp)
            {
                if (BlessingManager.Instance != null && BlessingManager.Instance.ShouldGraspBeFree(currentEssence))
                {
                    return 0;
                }
            }

            // Start with base multiplier of 1.0
            float costMultiplier = 1.0f;

            // Devouring Hunger: DevouringMaw costs 25% more
            if (powerType == HeartPowerType.DevouringMaw)
            {
                costMultiplier *= BlessingManager.Instance?.GetMawCostMultiplier() ?? 1.0f;
            }

            // Vengeful Spirit: All powers cost 50% less when below essence threshold
            costMultiplier *= BlessingManager.Instance?.GetPowerCostMultiplier(currentEssence) ?? 1.0f;

            // Frugal Heart challenge: Powers cost 50% more
            costMultiplier *= ChallengeModifierManager.Instance?.GetPowerCostMultiplier() ?? 1.0f;

            return Mathf.RoundToInt(baseCost * costMultiplier);
        }

        /// <summary>
        /// Checks if a power type is a toggle power (no cooldown, expires on conditions).
        /// </summary>
        public bool IsTogglePower(HeartPowerType powerType)
        {
            return powerType == HeartPowerType.MurmuringPaths ||
                   powerType == HeartPowerType.HeartwardGrasp ||
                   powerType == HeartPowerType.DevouringMaw ||
                   powerType == HeartPowerType.Sculpting;
        }

        /// <summary>
        /// Checks if a toggle power has a cooldown after expiration.
        /// Most toggle powers can be reused immediately; DevouringMaw has a cooldown.
        /// </summary>
        public bool IsTogglePowerWithCooldown(HeartPowerType powerType)
        {
            return powerType == HeartPowerType.DevouringMaw;
        }

        /// <summary>
        /// Checks if a power is currently active.
        /// </summary>
        public bool IsPowerActive(HeartPowerType powerType)
        {
            return activePowers.ContainsKey(powerType);
        }

        /// <summary>
        /// Checks if any power is currently active.
        /// </summary>
        public bool IsAnyPowerActive()
        {
            return activePowers.Count > 0;
        }

        /// <summary>
        /// Gets the remaining cooldown time for a power.
        /// </summary>
        public float GetCooldownRemaining(HeartPowerType powerType)
        {
            return cooldownTimers.GetValueOrDefault(powerType, 0f);
        }

        /// <summary>
        /// Checks if the Sculpting power can be used at the given position.
        /// Returns true if position is on a non-heart node.
        /// </summary>
        public bool CanUseSculptingAt(Vector3 worldPosition)
        {
            var dynamicMazeGrowth = Object.FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth == null) return false;

            int nodeIndex = dynamicMazeGrowth.FindNodeIndexAtPosition(worldPosition);
            // Must be on a node (>= 0) and not the heart node (0)
            return nodeIndex > 0;
        }

        /// <summary>
        /// Checks if the HeartwardGrasp power can be used at the given position.
        /// Returns true if there are wall tiles along the ray from focal point to heart.
        /// </summary>
        public bool CanUseHeartwardGraspAt(Vector3 worldPosition)
        {
            if (MazeGrid == null) return false;

            Vector3 heartPos = new Vector3(MazeGrid.HeartWorldPosition.x, MazeGrid.HeartWorldPosition.y, 0f);
            Vector3 focalPos = new Vector3(worldPosition.x, worldPosition.y, 0f);

            // Don't allow activation at the heart itself
            float distToHeart = Vector3.Distance(heartPos, focalPos);
            if (distToHeart < 3f) return false; // Too close to heart

            Vector3 focalToHeartDir = (heartPos - focalPos).normalized;

            // Raycast from focal toward heart to find wall tiles
            RaycastHit[] hits = Physics.RaycastAll(focalPos, focalToHeartDir, distToHeart, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    return true; // Found at least one wall tile
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the current tier for a power type.
        /// </summary>
        public int GetPowerTier(HeartPowerType powerType)
        {
            return powerTiers.GetValueOrDefault(powerType, 1);
        }

        /// <summary>
        /// Notifies the manager that a visitor was consumed.
        /// Used by toggle powers that expire based on consumption count.
        /// </summary>
        public void NotifyVisitorConsumed()
        {
            // Notify any active toggle power effects about the consumption
            if (activePowers.TryGetValue(HeartPowerType.MurmuringPaths, out var effect))
            {
                if (effect is MurmuringPathsEffect murmuringEffect)
                {
                    murmuringEffect.OnVisitorConsumed();
                }
            }
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

            // Use PowerProgressionManager for run-based tier if available
            int currentTier;
            if (PowerProgressionManager.Instance != null)
            {
                currentTier = PowerProgressionManager.Instance.GetRunTier(powerType);
            }
            else
            {
                currentTier = powerTiers.GetValueOrDefault(powerType, 1);
            }

            foreach (var def in powerDefinitions)
            {
                if (def != null && def.powerType == powerType && def.tier == currentTier)
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
            AddEssence(amount, EssenceSource.HeartPowerBonus, null);
        }

        /// <summary>
        /// Adds essence to the player's pool via GameController with source tracking.
        /// </summary>
        public void AddEssence(int amount, EssenceSource source, string details = null)
        {
            if (gameController != null)
            {
                gameController.AddEssence(amount, source, details);

                // Notify listeners (for UI updates)
                OnEssenceChanged?.Invoke(CurrentEssence);
            }
        }

        /// <summary>
        /// Spends essence via GameController (returns true if successful).
        /// </summary>
        public bool SpendEssence(int amount)
        {
            return SpendEssence(amount, EssenceSource.HeartPowerCost, null);
        }

        /// <summary>
        /// Spends essence via GameController with source tracking (returns true if successful).
        /// </summary>
        public bool SpendEssence(int amount, EssenceSource source, string details = null)
        {
            if (gameController != null && gameController.TrySpendEssence(amount, source, details))
            {
                // Notify listeners (for UI updates)
                OnEssenceChanged?.Invoke(CurrentEssence);
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods

        private void ActivatePower(HeartPowerType powerType, HeartPowerDefinition definition, Vector3 worldPosition)
        {
            // Use factory to create power effect instance
            ActivePowerEffect effect = effectFactory.Create(powerType, this, definition, worldPosition);

            if (effect == null)
            {
                Debug.LogWarning($"[HeartPowerManager] No effect factory registered for {powerType}");
                return;
            }

            if (effect != null)
            {
                effect.OnStart();

                // Toggle powers use consumption-based expiration, not duration
                // They need to be in activePowers even if Duration is 0
                bool isTogglePower = IsTogglePower(powerType);

                if (effect.Duration > 0 || isTogglePower)
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

            // Clear all Lured states when all effects are cleaned up
            var activeVisitors = FaeMaze.Visitors.VisitorRegistry.All;
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
                // Commented out - focusing on powers 2, 8, 9 for now
                // case HeartPowerType.HeartbeatOfLonging:
                case HeartPowerType.MurmuringPaths:
                // case HeartPowerType.DreamSnare:
                // case HeartPowerType.FeastwardPanic:
                case HeartPowerType.HeartwardGrasp:
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
            var activeVisitors = FaeMaze.Visitors.VisitorRegistry.All;

            if (activeVisitors == null || activeVisitors.Count == 0)
            {
                return;
            }

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
                    }
                    else if (!isMurmuringPaths)
                    {
                        // For other powers, just recalculate paths due to attraction changes
                        visitor.RecalculatePath();
                    }
                }
            }
        }

        #endregion

        #region Active Effect Position Queries

        /// <summary>
        /// Returns world positions of all active HeartwardGrasp zones (both grabbing and pushing).
        /// Used by WaryWayfarer for hazard avoidance pathfinding.
        /// </summary>
        public List<Vector3> GetActiveHeartwardGraspPositions()
        {
            var positions = new List<Vector3>();

            if (activePowers.TryGetValue(HeartPowerType.HeartwardGrasp, out var effect) && effect is HeartwardGraspEffect graspEffect)
            {
                var zonePositions = graspEffect.GetZonePositions();
                positions.AddRange(zonePositions);
            }

            return positions;
        }

        /// <summary>
        /// Returns world positions of all active DevouringMaw zones.
        /// Used by WaryWayfarer for hazard avoidance pathfinding.
        /// </summary>
        public List<Vector3> GetActiveDevouringMawPositions()
        {
            var positions = new List<Vector3>();

            if (activePowers.TryGetValue(HeartPowerType.DevouringMaw, out var effect) && effect is DevouringMawEffect mawEffect)
            {
                positions.Add(mawEffect.GetTargetPosition());
            }

            return positions;
        }

        /// <summary>
        /// Returns the target position of the active Murmuring Paths effect (the focal point where the power was activated).
        /// Returns null if no Murmuring Paths effect is active.
        /// </summary>
        public Vector3? GetActiveMurmuringPathsTargetPosition()
        {
            if (activePowers.TryGetValue(HeartPowerType.MurmuringPaths, out var effect) && effect is MurmuringPathsEffect murmuringEffect)
            {
                return murmuringEffect.TargetPosition;
            }
            return null;
        }

        /// <summary>
        /// Returns the furthest tile position from the heart on the active MurmuringPaths fog.
        /// This is the best spawn point for tutorial visitors that need to walk through the fog.
        /// </summary>
        public Vector3? GetActiveMurmuringPathsFurthestPosition()
        {
            if (activePowers.TryGetValue(HeartPowerType.MurmuringPaths, out var effect) && effect is MurmuringPathsEffect murmuringEffect)
            {
                return murmuringEffect.FurthestPosition;
            }
            return null;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Grid-based gizmos removed - using world-space coordinates now
        }

        #endregion

        #region World Offset Handling

        /// <summary>
        /// Applies a world-space offset to active heart power visuals.
        /// </summary>
        public void ApplyWorldOffset(Vector3 worldOffset)
        {
            if (tileVisualizer != null)
            {
                tileVisualizer.ApplyWorldOffset(worldOffset);
            }

            foreach (var effect in activePowers.Values)
            {
                effect?.ApplyWorldOffset(worldOffset);
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

        public virtual float Duration => definition.duration;
        public virtual bool IsExpired => elapsedTime >= Duration && Duration > 0;

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
        public virtual void ApplyWorldOffset(Vector3 worldOffset) { }
    }

    #endregion
}

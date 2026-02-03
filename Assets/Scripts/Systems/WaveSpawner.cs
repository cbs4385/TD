using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Maze;
using FaeMaze.Audio;
using FaeMaze.Visitors;
using FaeMaze.HeartPowers;
using FaeMaze.Tutorial;
using FaeMaze.Roguelike;
using FaeMaze.UI;
using FontStyles = TMPro.FontStyles;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave-based spawning of visitors from spawn points.
    /// Uses world-space coordinates for positioning.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        #region Events

        public event System.Action OnGameOver;

        #endregion

        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        private VisitorController basicVisitorPrefab;

        [SerializeField]
        private MistakingVisitorController mistakingVisitorPrefab;

        [SerializeField]
        private LanternDrunkVisitorController lanternDrunkVisitorPrefab;

        [SerializeField]
        private WaryWayfarerVisitorController waryWayfarerVisitorPrefab;

        [SerializeField]
        private SleepwalkingDevoteeController sleepwalkingVisitorPrefab;

        [SerializeField]
        [Tooltip("The Goblin prefab")]
        private GoblinController goblinPrefab;

        [Header("Spawning Settings")]
        [SerializeField]
        private float baseSpawnInterval = 5.0f;

        [Header("Goblin Settings")]
        [SerializeField]
        private bool enableGoblin = true;

        [Header("UI Configuration")]
        [SerializeField]
        private bool enableWaveSpawnerUI = false;

        [SerializeField]
        private Canvas uiCanvas;

        [SerializeField]
        private int fontSize = 20;

        [SerializeField]
        private Color uiTextColor = Color.white;

        [Header("Auto-Start")]
        [SerializeField]
        private bool autoStartFirstWave = false;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private HeartPowerManager heartPowerManager;
        private bool isSpawning;
        private bool isWaveActive;
        private bool isGameOver;
        private int currentWaveNumber;
        private int visitorsSpawnedThisWave;
        private int totalVisitorsSpawned;
        private List<GameObject> activeVisitors = new List<GameObject>();

        private GoblinController currentGoblin;
        private int startingEssence;
        private float currentSpawnInterval;

        private TextMeshProUGUI visitorCountText;
        private TextMeshProUGUI waveStatusText;
        private TextMeshProUGUI essenceValueText;
        private Slider essenceBar;
        private GameObject uiPanel;

        #endregion

        #region Properties

        public bool IsSpawning => isSpawning;
        public bool IsWaveActive => isWaveActive;
        public bool IsGameOver => isGameOver;
        public int CurrentWaveNumber => currentWaveNumber;
        public int TotalVisitorsSpawned => totalVisitorsSpawned;
        public int ActiveVisitorCount => activeVisitors.Count;
        public bool HasGoblinOnGraph => currentGoblin != null;
        public GoblinController CurrentGoblin => currentGoblin;

        #endregion

        #region Setup Helpers

        public void SetCompletedWaveCount(int completedWaveCount)
        {
            currentWaveNumber = Mathf.Max(0, completedWaveCount);
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (enableWaveSpawnerUI && GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        private void OnDisable()
        {
            if (enableWaveSpawnerUI && GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }
        }

        private void Start()
        {
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null)
            {
                heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            }

            ValidateReferences();
            LoadSettings();

            if (enableWaveSpawnerUI && (visitorCountText == null || waveStatusText == null))
            {
                CreateUI();

                if (GameController.Instance != null)
                {
                    GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                    GameController.Instance.OnEssenceChanged += OnEssenceChanged;
                    OnEssenceChanged(GameController.Instance.CurrentEssence);
                }
            }

            if (autoStartFirstWave)
            {
                // Don't auto-start if tutorial hasn't been completed yet - tutorial controls spawning
                // Check both TutorialCompleted (persistent) AND IsActive (runtime) because:
                // - TutorialCompleted is false until tutorial finishes for the first time
                // - IsActive might not be true yet if tutorial is starting with a delay
                bool tutorialWillRun = !GameSettings.TutorialCompleted && GameSettings.ShowTutorialOnFirstRun;
                bool tutorialIsActive = TutorialManager.Instance != null && TutorialManager.Instance.IsActive;

                if (tutorialWillRun || tutorialIsActive)
                {
                    Debug.Log($"[WaveSpawner] Tutorial controls spawning, skipping auto-start (willRun={tutorialWillRun}, isActive={tutorialIsActive})");
                }
                else
                {
                    // Start via coroutine to allow blessing selection first
                    StartCoroutine(StartWaveWithBlessingSelection());
                }
            }
        }

        /// <summary>
        /// Shows blessing selection UI (if applicable) then starts the first wave.
        /// </summary>
        private IEnumerator StartWaveWithBlessingSelection()
        {
            // Wait for DynamicMazeGrowth initial growth stages to complete
            DynamicMazeGrowth dynamicMazeGrowth = FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth != null && !dynamicMazeGrowth.IsInitialGrowthComplete)
            {
                Debug.Log("[WaveSpawner] Waiting for initial maze growth to complete...");
                while (!dynamicMazeGrowth.IsInitialGrowthComplete)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            // Show blessing selection UI if player has unlocked blessings
            var unlockedBlessings = BlessingManager.Instance?.GetUnlockedBlessings();
            if (unlockedBlessings != null && unlockedBlessings.Count > 0)
            {
                Debug.Log($"[WaveSpawner] Showing blessing selection ({unlockedBlessings.Count} unlocked)");

                // Create or find the BlessingSelectionUI
                var blessingUI = FindFirstObjectByType<BlessingSelectionUI>();
                if (blessingUI == null)
                {
                    GameObject uiObj = new GameObject("BlessingSelectionUI");
                    blessingUI = uiObj.AddComponent<BlessingSelectionUI>();
                }

                // Show the UI and wait for selection
                bool selectionComplete = false;
                blessingUI.Show((selectedBlessing) =>
                {
                    selectionComplete = true;
                    if (selectedBlessing != null)
                    {
                        Debug.Log($"[WaveSpawner] Blessing selected: {selectedBlessing.DisplayName}");
                    }
                    else
                    {
                        Debug.Log("[WaveSpawner] No blessing selected (skipped)");
                    }
                });

                // Wait for selection to complete
                while (!selectionComplete)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.Log("[WaveSpawner] No unlocked blessings, skipping selection");
            }

            // Now start the wave
            StartWave();
        }

        private void LoadSettings()
        {
            baseSpawnInterval = GameSettings.SpawnInterval > 0 ? GameSettings.SpawnInterval : 5.0f;
            currentSpawnInterval = baseSpawnInterval;
            enableGoblin = GameSettings.EnableGoblin;
            startingEssence = GameSettings.StartingEssence;

            Debug.Log($"[WaveSpawner] LoadSettings: enableGoblin={enableGoblin}, startingEssence={startingEssence}, goblinPrefab={(goblinPrefab != null ? goblinPrefab.name : "NULL")}");
        }

        private void Update()
        {
            if (!isWaveActive)
                return;

            // Check for game over (essence depleted)
            if (GameController.Instance != null && GameController.Instance.CurrentEssence <= 0)
            {
                HandleGameOver();
                return;
            }

            // Goblin spawns when: enabled, no current Goblin, and essence >= 2x starting essence
            if (enableGoblin && currentGoblin == null && GameController.Instance != null)
            {
                int currentEssence = GameController.Instance.CurrentEssence;
                int spawnThreshold = startingEssence * 2;

                if (currentEssence >= spawnThreshold)
                {
                    Debug.Log($"[WaveSpawner] Goblin spawn triggered: currentEssence={currentEssence} >= threshold={spawnThreshold}");
                    SpawnGoblin();
                }
            }
            else if (!enableGoblin)
            {
                // Log once when Goblin is disabled (use a static flag to avoid spam)
                LogGoblinDisabledOnce();
            }

            activeVisitors.RemoveAll(v => v == null);

            UpdateUI();
        }

        #endregion

        #region Wave Management

        public bool StartWave()
        {
            if (isSpawning || isWaveActive)
            {
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            if (currentWaveNumber > 0 && mazeGridBehaviour != null)
            {
                mazeGridBehaviour.RegenerateMaze();
            }

            currentWaveNumber++;
            visitorsSpawnedThisWave = 0;
            activeVisitors.Clear();
            isWaveActive = true;
            isGameOver = false;

            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveStart();
            }

            // Clear any existing Goblin from previous games
            if (currentGoblin != null)
            {
                Destroy(currentGoblin.gameObject);
                currentGoblin = null;
            }

            StartCoroutine(SpawnWaveCoroutine());
            return true;
        }

        public void ResetWaveState()
        {
            isGameOver = false;
            isWaveActive = false;

            if (currentGoblin != null)
            {
                Destroy(currentGoblin.gameObject);
                currentGoblin = null;
            }
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;

            // Initialize spawn interval at base value
            currentSpawnInterval = baseSpawnInterval;

            // Spawn visitors continuously while game is active (essence > 0)
            while (isWaveActive)
            {
                // Check if game should end (essence depleted)
                if (GameController.Instance != null && GameController.Instance.CurrentEssence <= 0)
                {
                    break;
                }

                SpawnVisitor();
                visitorsSpawnedThisWave++;

                // Calculate spawn interval using asymptotic growth (approaches max, never exceeds)
                currentSpawnInterval = DifficultyScaling.GetAsymptoticSpawnInterval(
                    totalVisitorsSpawned, baseSpawnInterval);

                // Apply blessing spawn interval multiplier (Patient Hunter makes spawns faster)
                float blessingMultiplier = BlessingManager.Instance?.GetSpawnIntervalMultiplier() ?? 1.0f;
                currentSpawnInterval *= blessingMultiplier;

                // Apply challenge spawn interval multiplier (Endless Tide makes spawns faster)
                float challengeMultiplier = ChallengeModifierManager.Instance?.GetSpawnIntervalMultiplier() ?? 1.0f;
                currentSpawnInterval *= challengeMultiplier;

                // Wait for current interval (minimum 0.1 seconds for safety)
                float waitTime = Mathf.Max(0.1f, currentSpawnInterval);
                yield return new WaitForSeconds(waitTime);
            }

            isSpawning = false;
        }

        #endregion

        #region Visitor Spawning

        private void SpawnVisitor()
        {
            if (GameController.Instance == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Get spawn position from world-space spawn points
            Vector3 spawnWorldPos;
            Vector3 destinationWorldPos;
            char spawnId = '\0';

            if (mazeGridBehaviour.WorldSpaceMazeData != null &&
                mazeGridBehaviour.WorldSpaceMazeData.SpawnPointCount > 0)
            {
                // Get random spawn point from world-space data (positions queried in real-time from transforms)
                var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
                var keys = new List<char>(spawnPoints.Keys);
                int randomIndex = RandomManager.Range(0, keys.Count);
                spawnId = keys[randomIndex];
                spawnWorldPos = spawnPoints[spawnId];

                // Find a different spawn point as destination
                destinationWorldPos = FindDifferentSpawnPoint(spawnWorldPos, spawnPoints);
            }
            else
            {
                // Fallback to heart position if no spawn points
                spawnWorldPos = mazeGridBehaviour.HeartWorldPosition;
                destinationWorldPos = spawnWorldPos;
            }

            // Spawn visitor
            VisitorControllerBase spawnedVisitor = SelectAndSpawnRandomVisitorType(spawnWorldPos);

            if (spawnedVisitor == null)
            {
                return;
            }

            GameObject visitorObject = spawnedVisitor.gameObject;

            // Initialize visitor with GameController reference
            spawnedVisitor.Initialize(GameController.Instance);

            // Apply difficulty tier scaling (essence-threshold based)
            int tier = DifficultyManager.Instance?.CurrentTier ?? 1;
            spawnedVisitor.SetDifficultyTier(tier);

            // Check for elite spawn (ChampionVisitors challenge)
            float eliteChance = ChallengeModifierManager.Instance?.GetEliteSpawnChance() ?? 0f;
            if (eliteChance > 0f && RandomManager.Value < eliteChance)
            {
                float eliteStats = ChallengeModifierManager.Instance?.GetEliteStatMultiplier() ?? 2f;
                float eliteReward = ChallengeModifierManager.Instance?.GetEliteRewardMultiplier() ?? 3f;
                spawnedVisitor.SetElite(eliteStats, eliteReward);
            }

            // Track where visitor spawned from (to prevent retargeting back to origin)
            spawnedVisitor.SetOriginalSpawnPosition(spawnWorldPos);

            // Set destination to a different spawn point (not the heart)
            spawnedVisitor.SetWorldDestination(destinationWorldPos);

            if (spawnedVisitor is VisitorController)
            {
                GameController.Instance.SetLastSpawnedVisitor((VisitorController)spawnedVisitor);
            }

            string visitorType = spawnedVisitor.GetType().Name.Replace("Controller", "");
            visitorObject.name = $"{visitorType}_T{tier}_{totalVisitorsSpawned}_{spawnId}";

            SoundManager.Instance?.PlayVisitorSpawn();

            activeVisitors.Add(visitorObject);
            totalVisitorsSpawned++;
        }

        private VisitorControllerBase SelectAndSpawnRandomVisitorType(Vector3 spawnPosition)
        {
            // Collect all available visitor prefabs
            List<VisitorControllerBase> availableVisitorPrefabs = new List<VisitorControllerBase>();

            if (basicVisitorPrefab != null)
                availableVisitorPrefabs.Add(basicVisitorPrefab);
            if (mistakingVisitorPrefab != null)
                availableVisitorPrefabs.Add(mistakingVisitorPrefab);
            if (lanternDrunkVisitorPrefab != null)
                availableVisitorPrefabs.Add(lanternDrunkVisitorPrefab);
            if (waryWayfarerVisitorPrefab != null)
                availableVisitorPrefabs.Add(waryWayfarerVisitorPrefab);
            if (sleepwalkingVisitorPrefab != null)
                availableVisitorPrefabs.Add(sleepwalkingVisitorPrefab);

            if (availableVisitorPrefabs.Count == 0)
            {
                return null;
            }

            int randomIndex = RandomManager.Range(0, availableVisitorPrefabs.Count);
            VisitorControllerBase selectedPrefab = availableVisitorPrefabs[randomIndex];

            VisitorControllerBase spawnedVisitor = Instantiate(selectedPrefab, spawnPosition, Quaternion.Euler(0, 0, 180));

            return spawnedVisitor;
        }

        /// <summary>
        /// Finds a random spawn point different from the origin spawn point.
        /// Randomizes destinations so visitors path through all parts of the graph.
        /// Falls back to heart position if only one spawn point exists.
        /// </summary>
        private Vector3 FindDifferentSpawnPoint(Vector3 originPos, Dictionary<char, Vector3> spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Count <= 1)
            {
                // Only one spawn point or none - fall back to heart
                return mazeGridBehaviour.HeartWorldPosition;
            }

            // Collect all spawn points that aren't the origin
            var validDestinations = new List<Vector3>();
            foreach (var kvp in spawnPoints)
            {
                Vector3 spawnPos = kvp.Value;

                // Skip if too close to origin (same spawn point)
                float distToOrigin = Vector3.Distance(spawnPos, originPos);
                if (distToOrigin < 1f)
                    continue;

                validDestinations.Add(spawnPos);
            }

            if (validDestinations.Count == 0)
            {
                return mazeGridBehaviour.HeartWorldPosition;
            }

            // Select a random destination from valid spawn points
            int randomIndex = RandomManager.Range(0, validDestinations.Count);
            return validDestinations[randomIndex];
        }

        private void SpawnGoblin()
        {
            Debug.Log($"[WaveSpawner] SpawnGoblin called: goblinPrefab={(goblinPrefab != null ? goblinPrefab.name : "NULL")}, mazeGridBehaviour={(mazeGridBehaviour != null ? "OK" : "NULL")}");

            if (goblinPrefab == null)
            {
                Debug.LogError("[WaveSpawner] SpawnGoblin FAILED: goblinPrefab is NULL!");
                return;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("[WaveSpawner] SpawnGoblin FAILED: mazeGridBehaviour is NULL!");
                return;
            }

            Vector3 spawnWorldPos;

            if (mazeGridBehaviour.WorldSpaceMazeData != null &&
                mazeGridBehaviour.WorldSpaceMazeData.SpawnPointCount > 0)
            {
                var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
                var keys = new List<char>(spawnPoints.Keys);
                int randomIndex = RandomManager.Range(0, keys.Count);
                spawnWorldPos = spawnPoints[keys[randomIndex]];
                Debug.Log($"[WaveSpawner] Goblin spawn position from spawn point: {spawnWorldPos}");
            }
            else
            {
                spawnWorldPos = mazeGridBehaviour.HeartWorldPosition;
                Debug.Log($"[WaveSpawner] Goblin spawn position from heart: {spawnWorldPos}");
            }

            currentGoblin = Instantiate(goblinPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));

            if (currentGoblin == null)
            {
                Debug.LogError("[WaveSpawner] SpawnGoblin FAILED: Instantiate returned NULL!");
                return;
            }

            // Check if GoblinController component exists
            var controller = currentGoblin.GetComponent<GoblinController>();
            if (controller == null)
            {
                Debug.LogError("[WaveSpawner] SpawnGoblin WARNING: Instantiated object has no GoblinController component! Check prefab setup.");
            }
            else
            {
                Debug.Log($"[WaveSpawner] GoblinController component found on instantiated object");
            }

            int tier = DifficultyManager.Instance?.CurrentTier ?? 1;
            currentGoblin.SetDifficultyTier(tier);
            currentGoblin.name = $"Goblin_T{tier}";

            Debug.Log($"[WaveSpawner] Goblin spawned successfully: name={currentGoblin.name}, position={spawnWorldPos}, tier={tier}");
        }

        private static bool _goblinDisabledLogged = false;
        private void LogGoblinDisabledOnce()
        {
            if (!_goblinDisabledLogged)
            {
                Debug.Log("[WaveSpawner] Goblin spawning is DISABLED (enableGoblin=false)");
                _goblinDisabledLogged = true;
            }
        }

        private void HandleGameOver()
        {
            if (!isWaveActive)
                return;

            isWaveActive = false;
            isSpawning = false;
            isGameOver = true;

            if (currentGoblin != null)
            {
                Destroy(currentGoblin.gameObject);
                currentGoblin = null;
            }

            OnGameOver?.Invoke();
        }

        #endregion

        #region UI Management

        private void CreateUI()
        {
            if (uiCanvas == null)
            {
                uiCanvas = FindFirstObjectByType<Canvas>();
                if (uiCanvas == null)
                {
                    GameObject canvasObj = new GameObject("WaveSpawnerCanvas");
                    uiCanvas = canvasObj.AddComponent<Canvas>();
                    uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);

                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }

            uiPanel = new GameObject("WaveInfoPanel");
            uiPanel.transform.SetParent(uiCanvas.transform, false);

            RectTransform panelRect = uiPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            panelRect.sizeDelta = new Vector2(350f, 80f);

            Image panelImage = uiPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            Outline outline = uiPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject statusTextObj = new GameObject("WaveStatusText");
            statusTextObj.transform.SetParent(uiPanel.transform, false);

            RectTransform statusRect = statusTextObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -10f);
            statusRect.sizeDelta = new Vector2(-20f, 30f);

            waveStatusText = statusTextObj.AddComponent<TextMeshProUGUI>();
            waveStatusText.fontSize = fontSize + 4;
            waveStatusText.color = new Color(1f, 0.85f, 0.3f, 1f);
            waveStatusText.alignment = TextAlignmentOptions.Center;
            waveStatusText.fontStyle = FontStyles.Bold;
            waveStatusText.text = "Wave 0";

            GameObject countTextObj = new GameObject("VisitorCountText");
            countTextObj.transform.SetParent(uiPanel.transform, false);

            RectTransform countRect = countTextObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.anchoredPosition = new Vector2(0f, 10f);
            countRect.sizeDelta = new Vector2(-20f, 30f);

            visitorCountText = countTextObj.AddComponent<TextMeshProUGUI>();
            visitorCountText.fontSize = fontSize;
            visitorCountText.color = new Color(uiTextColor.r, uiTextColor.g, uiTextColor.b, 0.9f);
            visitorCountText.alignment = TextAlignmentOptions.Center;
            visitorCountText.text = "Visitors: 0/0 (Active: 0)";

            UpdateEssenceDisplay();
        }

        private void UpdateUI()
        {
            if (!enableWaveSpawnerUI)
                return;

            if (waveStatusText != null)
            {
                if (isGameOver)
                {
                    waveStatusText.text = "Game Over";
                    waveStatusText.color = new Color(1f, 0.3f, 0.3f, 1f);
                }
                else if (!isWaveActive)
                {
                    waveStatusText.text = "Press Start";
                    waveStatusText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                else
                {
                    waveStatusText.text = "Game Active";
                    waveStatusText.color = new Color(0.3f, 1f, 0.3f, 1f);
                }
            }

            if (visitorCountText != null)
            {
                visitorCountText.text = $"Visitors Spawned: {visitorsSpawnedThisWave} (Active: {activeVisitors.Count})";
            }
        }

        private void OnEssenceChanged(int newEssence)
        {
            UpdateEssenceDisplay();
        }

        private void UpdateEssenceDisplay()
        {
            if (GameController.Instance == null)
                return;

            int currentEssence = GameController.Instance.CurrentEssence;

            if (essenceValueText != null)
            {
                essenceValueText.text = $"{currentEssence} / 400";
            }

            if (essenceBar != null)
            {
                essenceBar.value = currentEssence;
            }
        }

        #endregion

        #region Validation

        private bool ValidateReferences()
        {
            if (basicVisitorPrefab == null && mistakingVisitorPrefab == null &&
                lanternDrunkVisitorPrefab == null && waryWayfarerVisitorPrefab == null &&
                sleepwalkingVisitorPrefab == null)
            {
                return false;
            }

            if (mazeGridBehaviour == null)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Debug Methods

        public void SpawnSingleVisitorForDebug()
        {
            if (!ValidateReferences())
            {
                return;
            }

            SpawnVisitor();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return;

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            if (spawnPoints.Count == 0)
                return;

            foreach (var kvp in spawnPoints)
            {
                char spawnId = kvp.Key;
                Vector3 worldPos = kvp.Value;

                Gizmos.color = GetSpawnMarkerColor(spawnId);
                Gizmos.DrawWireSphere(worldPos, 0.5f);

                #if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * 0.7f, spawnId.ToString());
                #endif
            }

            // Draw lines between spawn points
            Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
            var keys = new List<char>(spawnPoints.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                for (int j = i + 1; j < keys.Count; j++)
                {
                    Gizmos.DrawLine(spawnPoints[keys[i]], spawnPoints[keys[j]]);
                }
            }
        }

        private Color GetSpawnMarkerColor(char spawnId)
        {
            switch (spawnId)
            {
                case 'A': return Color.cyan;
                case 'B': return Color.magenta;
                case 'C': return Color.yellow;
                case 'D': return new Color(1f, 0.5f, 0f);
                default: return Color.white;
            }
        }

        #endregion
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Maze;
using FaeMaze.Audio;
using FaeMaze.Visitors;
using FaeMaze.HeartPowers;
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
        [Tooltip("The Red Cap prefab")]
        private RedCapController redCapPrefab;

        [Header("Spawning Settings")]
        [SerializeField]
        private float spawnInterval = 1.0f;

        [Header("Red Cap Settings")]
        [SerializeField]
        private bool enableRedCap = true;

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

        private RedCapController currentRedCap;
        private int startingEssence;

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
        public bool HasRedCapOnGraph => currentRedCap != null;
        public RedCapController CurrentRedCap => currentRedCap;

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
                StartWave();
            }
        }

        private void LoadSettings()
        {
            spawnInterval = GameSettings.SpawnInterval;
            enableRedCap = GameSettings.EnableRedCap;
            startingEssence = GameSettings.StartingEssence;
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

            // RedCap spawns when: enabled, no current RedCap, and essence >= 2x starting essence
            if (enableRedCap && currentRedCap == null && GameController.Instance != null)
            {
                int currentEssence = GameController.Instance.CurrentEssence;
                int spawnThreshold = startingEssence * 2;

                if (currentEssence >= spawnThreshold)
                {
                    SpawnRedCap();
                }
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

            // Clear any existing RedCap from previous games
            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }

            StartCoroutine(SpawnWaveCoroutine());
            return true;
        }

        public void ResetWaveState()
        {
            isGameOver = false;
            isWaveActive = false;

            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }
        }

        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;

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

                yield return new WaitForSeconds(spawnInterval);
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
                int randomIndex = Random.Range(0, keys.Count);
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

            // Track where visitor spawned from (to prevent retargeting back to origin)
            spawnedVisitor.SetOriginalSpawnPosition(spawnWorldPos);

            // Set destination to a different spawn point (not the heart)
            spawnedVisitor.SetWorldDestination(destinationWorldPos);

            if (spawnedVisitor is VisitorController)
            {
                GameController.Instance.SetLastSpawnedVisitor((VisitorController)spawnedVisitor);
            }

            string visitorType = spawnedVisitor.GetType().Name.Replace("Controller", "");
            visitorObject.name = $"{visitorType}_W{currentWaveNumber}_{visitorsSpawnedThisWave}_{spawnId}";

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

            int randomIndex = Random.Range(0, availableVisitorPrefabs.Count);
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
            int randomIndex = Random.Range(0, validDestinations.Count);
            return validDestinations[randomIndex];
        }

        private void SpawnRedCap()
        {
            if (redCapPrefab == null || mazeGridBehaviour == null)
            {
                return;
            }

            Vector3 spawnWorldPos;

            if (mazeGridBehaviour.WorldSpaceMazeData != null &&
                mazeGridBehaviour.WorldSpaceMazeData.SpawnPointCount > 0)
            {
                var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
                var keys = new List<char>(spawnPoints.Keys);
                int randomIndex = Random.Range(0, keys.Count);
                spawnWorldPos = spawnPoints[keys[randomIndex]];
            }
            else
            {
                spawnWorldPos = mazeGridBehaviour.HeartWorldPosition;
            }

            currentRedCap = Instantiate(redCapPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
            currentRedCap.name = $"RedCap_Wave{currentWaveNumber}";

            Debug.Log($"[WaveSpawner] RedCap spawned - essence reached {GameController.Instance?.CurrentEssence ?? 0} (threshold: {startingEssence * 2})");
        }

        private void HandleGameOver()
        {
            if (!isWaveActive)
                return;

            isWaveActive = false;
            isSpawning = false;
            isGameOver = true;

            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }

            Debug.Log("[WaveSpawner] Game Over - Essence depleted");
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

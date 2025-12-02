using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Maze;
using FaeMaze.Audio;
using FaeMaze.Visitors;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave-based spawning of visitors from the entrance to the heart.
    /// Spawns visitors in waves with configurable count and interval.
    /// Includes per-wave timer with pass/fail conditions.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        #region Events

        /// <summary>Invoked when a wave is successfully completed (all visitors cleared in time)</summary>
        public event System.Action OnWaveSuccess;

        /// <summary>Invoked when a wave fails (timer expires before all visitors cleared)</summary>
        public event System.Action OnWaveFailed;

        /// <summary>Invoked when the wave timer updates (passes remaining time)</summary>
        public event System.Action<float> OnWaveTimerUpdate;

        #endregion

        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        [Header("Scene References (Legacy - Optional)")]
        [SerializeField]
        [Tooltip("(LEGACY) The maze entrance where visitors spawn. Leave empty to use spawn markers.")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("(LEGACY) The heart of the maze (destination). Leave empty to use spawn markers.")]
        private HeartOfTheMaze heart;

        [Header("Wave Settings")]
        [SerializeField]
        [Tooltip("Number of visitors to spawn per wave")]
        private int visitorsPerWave = 10;

        [SerializeField]
        [Tooltip("Time interval between spawns (in seconds)")]
        private float spawnInterval = 1.0f;

        [SerializeField]
        [Tooltip("Time budget for each wave (in seconds). Wave fails if not cleared in time.")]
        private float waveDuration = 60f;

        [Header("UI Configuration")]
        [SerializeField]
        [Tooltip("Canvas for UI display (will auto-create if null)")]
        private Canvas uiCanvas;

        [SerializeField]
        [Tooltip("Font size for wave info text")]
        private int fontSize = 20;

        [SerializeField]
        [Tooltip("Color for UI text")]
        private Color uiTextColor = Color.white;

        [SerializeField]
        [Tooltip("Color for timer text when running low")]
        private Color warningColor = Color.red;

        [SerializeField]
        [Tooltip("Time threshold (seconds) to show warning color")]
        private float warningThreshold = 10f;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private bool isSpawning;
        private bool isWaveActive;
        private bool isWaveFailed;
        private int currentWaveNumber;
        private int visitorsSpawnedThisWave;
        private int totalVisitorsSpawned;
        private float waveTimeRemaining;
        private List<VisitorController> activeVisitors = new List<VisitorController>();

        // UI References
        private TextMeshProUGUI timerText;
        private TextMeshProUGUI visitorCountText;
        private TextMeshProUGUI waveStatusText;
        private GameObject uiPanel;

        #endregion

        #region Properties

        /// <summary>Gets whether a wave is currently spawning</summary>
        public bool IsSpawning => isSpawning;

        /// <summary>Gets whether a wave is currently active (including after spawning completes)</summary>
        public bool IsWaveActive => isWaveActive;

        /// <summary>Gets whether the current wave has failed</summary>
        public bool IsWaveFailed => isWaveFailed;

        /// <summary>Gets the current wave number</summary>
        public int CurrentWaveNumber => currentWaveNumber;

        /// <summary>Gets the total number of visitors spawned</summary>
        public int TotalVisitorsSpawned => totalVisitorsSpawned;

        /// <summary>Gets the number of active visitors currently in the maze</summary>
        public int ActiveVisitorCount => activeVisitors.Count;

        /// <summary>Gets the time remaining in the current wave (in seconds)</summary>
        public float WaveTimeRemaining => waveTimeRemaining;

        /// <summary>Gets the configured wave duration (in seconds)</summary>
        public float WaveDuration => waveDuration;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            ValidateReferences();

            // Create UI if needed
            if (timerText == null || visitorCountText == null || waveStatusText == null)
            {
                CreateUI();
            }
        }

        private void Update()
        {
            if (!isWaveActive || isWaveFailed)
                return;

            // Update timer
            waveTimeRemaining -= Time.deltaTime;

            // Check for timeout
            if (waveTimeRemaining <= 0f)
            {
                waveTimeRemaining = 0f;
                HandleWaveFailure();
            }

            // Clean up destroyed visitors from active list
            activeVisitors.RemoveAll(v => v == null);

            // Check for wave success (all visitors cleared)
            if (!isSpawning && activeVisitors.Count == 0 && visitorsSpawnedThisWave > 0)
            {
                HandleWaveSuccess();
            }

            // Update UI
            UpdateUI();

            // Invoke timer update event
            OnWaveTimerUpdate?.Invoke(waveTimeRemaining);
        }

        #endregion

        #region Wave Management

        /// <summary>
        /// Starts spawning a new wave of visitors.
        /// Returns false if wave cannot start (already active or failed state).
        /// </summary>
        public bool StartWave()
        {
            // Prevent starting if already spawning
            if (isSpawning)
            {
                Debug.LogWarning("WaveSpawner: Cannot start wave - already spawning!");
                return false;
            }

            // Prevent starting if wave is already active
            if (isWaveActive)
            {
                Debug.LogWarning("WaveSpawner: Cannot start wave - wave already active!");
                return false;
            }

            // Prevent starting if in failed state
            if (isWaveFailed)
            {
                Debug.LogWarning("WaveSpawner: Cannot start wave - level in failed state!");
                return false;
            }

            if (!ValidateReferences())
            {
                return false;
            }

            // Initialize wave state
            currentWaveNumber++;
            visitorsSpawnedThisWave = 0;
            activeVisitors.Clear();
            isWaveActive = true;
            waveTimeRemaining = waveDuration;

            Debug.Log($"WaveSpawner: Starting Wave {currentWaveNumber} with {visitorsPerWave} visitors and {waveDuration}s time limit");

            StartCoroutine(SpawnWaveCoroutine());
            return true;
        }

        /// <summary>
        /// Resets the failed state to allow starting a new wave.
        /// Call this to retry after failure.
        /// </summary>
        public void ResetFailedState()
        {
            isWaveFailed = false;
            isWaveActive = false;
            Debug.Log("WaveSpawner: Failed state reset - can start new wave");
        }

        /// <summary>
        /// Coroutine that spawns visitors for the current wave.
        /// </summary>
        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;

            for (int i = 0; i < visitorsPerWave; i++)
            {
                SpawnVisitor();
                visitorsSpawnedThisWave++;

                // Wait before spawning next visitor (unless it's the last one)
                if (i < visitorsPerWave - 1)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            isSpawning = false;
        }

        #endregion

        #region Visitor Spawning

        /// <summary>
        /// Spawns a single visitor at a random start spawn point with a path to a different destination spawn point.
        /// Falls back to entrance/heart if spawn markers are not available.
        /// </summary>
        private void SpawnVisitor()
        {
            if (GameController.Instance == null)
            {
                return;
            }

            Vector2Int startPos;
            Vector2Int destPos;
            char startId = '\0';
            char destId = '\0';

            // Try to use spawn marker system first
            if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                // Use random spawn points
                if (!mazeGridBehaviour.TryGetRandomSpawnPair(out startId, out startPos, out destId, out destPos))
                {
                    Debug.LogError("WaveSpawner: Failed to get random spawn pair!");
                    return;
                }
            }
            // Fall back to legacy entrance/heart system
            else if (entrance != null && heart != null)
            {
                startPos = entrance.GridPosition;
                destPos = heart.GridPosition;
                Debug.LogWarning("WaveSpawner: Using legacy entrance/heart system. Consider adding spawn markers (A, B, C, D) to your maze file.");
            }
            else
            {
                Debug.LogError("WaveSpawner: No spawn system available! Need either 2+ spawn markers or entrance/heart references.");
                return;
            }

            // Find path using A*
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(startPos, destPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                Debug.LogWarning($"WaveSpawner: No path found from {startPos} to {destPos}");
                return;
            }

            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(startPos.x, startPos.y);

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);

            // Name includes spawn IDs if using spawn marker system
            if (startId != '\0')
            {
                visitor.gameObject.name = $"Visitor_W{currentWaveNumber}_{visitorsSpawnedThisWave}_{startId}to{destId}";
            }
            else
            {
                visitor.gameObject.name = $"Visitor_Wave{currentWaveNumber}_{visitorsSpawnedThisWave}";
            }

            SoundManager.Instance?.PlayVisitorSpawn();

            GameController.Instance.SetLastSpawnedVisitor(visitor);

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path
            visitor.SetPath(pathNodes);

            // Track active visitor
            activeVisitors.Add(visitor);

            totalVisitorsSpawned++;
        }

        /// <summary>
        /// Handles wave success when all visitors are cleared in time.
        /// </summary>
        private void HandleWaveSuccess()
        {
            if (!isWaveActive || isWaveFailed)
                return;

            isWaveActive = false;
            Debug.Log($"WaveSpawner: Wave {currentWaveNumber} SUCCESS! Cleared with {waveTimeRemaining:F1}s remaining");

            OnWaveSuccess?.Invoke();
        }

        /// <summary>
        /// Handles wave failure when timer expires.
        /// </summary>
        private void HandleWaveFailure()
        {
            if (isWaveFailed)
                return;

            isWaveFailed = true;
            isWaveActive = false;

            // Stop spawning if still active
            if (isSpawning)
            {
                StopAllCoroutines();
                isSpawning = false;
            }

            Debug.LogWarning($"WaveSpawner: Wave {currentWaveNumber} FAILED! Timer expired with {activeVisitors.Count} visitors remaining");

            OnWaveFailed?.Invoke();
        }

        #endregion

        #region UI Management

        /// <summary>
        /// Creates the UI elements for wave display (top-middle of screen).
        /// </summary>
        private void CreateUI()
        {
            // Create or find canvas
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

            // Create panel container - positioned at TOP MIDDLE
            uiPanel = new GameObject("WaveInfoPanel");
            uiPanel.transform.SetParent(uiCanvas.transform, false);

            RectTransform panelRect = uiPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);  // Top-middle anchor
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);  // 10px from top
            panelRect.sizeDelta = new Vector2(350f, 120f);

            Image panelImage = uiPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            // Add outline for better visibility
            Outline outline = uiPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            // Create wave status text (top)
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
            waveStatusText.color = new Color(1f, 0.85f, 0.3f, 1f);  // Gold color
            waveStatusText.alignment = TextAlignmentOptions.Center;
            waveStatusText.fontStyle = FontStyles.Bold;
            waveStatusText.text = "Wave 0";

            // Create timer text (middle)
            GameObject timerTextObj = new GameObject("TimerText");
            timerTextObj.transform.SetParent(uiPanel.transform, false);

            RectTransform timerRect = timerTextObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0f, 0.5f);
            timerRect.anchorMax = new Vector2(1f, 0.5f);
            timerRect.pivot = new Vector2(0.5f, 0.5f);
            timerRect.anchoredPosition = new Vector2(0f, 0f);
            timerRect.sizeDelta = new Vector2(-20f, 35f);

            timerText = timerTextObj.AddComponent<TextMeshProUGUI>();
            timerText.fontSize = fontSize + 6;
            timerText.color = uiTextColor;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.fontStyle = FontStyles.Bold;
            timerText.text = "--:--";

            // Create visitor count text (bottom)
            GameObject countTextObj = new GameObject("VisitorCountText");
            countTextObj.transform.SetParent(uiPanel.transform, false);

            RectTransform countRect = countTextObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 0f);
            countRect.pivot = new Vector2(0.5f, 0f);
            countRect.anchoredPosition = new Vector2(0f, 10f);
            countRect.sizeDelta = new Vector2(-20f, 25f);

            visitorCountText = countTextObj.AddComponent<TextMeshProUGUI>();
            visitorCountText.fontSize = fontSize;
            visitorCountText.color = new Color(uiTextColor.r, uiTextColor.g, uiTextColor.b, 0.9f);
            visitorCountText.alignment = TextAlignmentOptions.Center;
            visitorCountText.text = "Visitors: 0/0 (Active: 0)";

            Debug.Log("WaveSpawner: Auto-created UI elements at top-middle of screen");
        }

        /// <summary>
        /// Updates the UI display with current wave status.
        /// </summary>
        private void UpdateUI()
        {
            if (waveStatusText != null)
            {
                if (isWaveFailed)
                {
                    waveStatusText.text = $"Wave {currentWaveNumber} - FAILED";
                    waveStatusText.color = warningColor;
                }
                else if (!isWaveActive)
                {
                    waveStatusText.text = "No Active Wave";
                    waveStatusText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }
                else
                {
                    waveStatusText.text = $"Wave {currentWaveNumber}";
                    waveStatusText.color = new Color(1f, 0.85f, 0.3f, 1f);
                }
            }

            if (timerText != null)
            {
                if (!isWaveActive)
                {
                    timerText.text = "--:--";
                    timerText.color = uiTextColor;
                }
                else
                {
                    int minutes = Mathf.FloorToInt(waveTimeRemaining / 60f);
                    int seconds = Mathf.FloorToInt(waveTimeRemaining % 60f);
                    timerText.text = $"{minutes:00}:{seconds:00}";

                    // Change color to warning if time is low
                    if (waveTimeRemaining <= warningThreshold)
                    {
                        timerText.color = warningColor;
                    }
                    else
                    {
                        timerText.color = uiTextColor;
                    }
                }
            }

            if (visitorCountText != null)
            {
                int totalForWave = visitorsPerWave;
                visitorCountText.text = $"Visitors: {visitorsSpawnedThisWave}/{totalForWave} (Active: {activeVisitors.Count})";
            }
        }

        #endregion

        #region Validation

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (visitorPrefab == null)
            {
                Debug.LogError("WaveSpawner: Visitor prefab not assigned!");
                isValid = false;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WaveSpawner: MazeGridBehaviour not found in scene!");
                isValid = false;
            }

            // Check if we have either spawn markers or legacy entrance/heart
            if (mazeGridBehaviour != null)
            {
                bool hasSpawnMarkers = mazeGridBehaviour.GetSpawnPointCount() >= 2;
                bool hasLegacySystem = entrance != null && heart != null;

                if (!hasSpawnMarkers && !hasLegacySystem)
                {
                    Debug.LogError("WaveSpawner: No spawn system available! Need either:\n" +
                        "  - 2+ spawn markers (A, B, C, D) in maze file, OR\n" +
                        "  - Entrance and Heart references assigned");
                    isValid = false;
                }
            }

            return isValid;
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// Spawns a single visitor immediately for debug purposes.
        /// </summary>
        public void SpawnSingleVisitorForDebug()
        {
            if (!ValidateReferences())
            {
                Debug.LogWarning("Cannot spawn debug visitor - references not valid");
                return;
            }

            SpawnVisitor();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (mazeGridBehaviour == null)
                return;

            // Draw spawn markers if available
            if (mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();

                // Draw each spawn point
                foreach (var kvp in spawnPoints)
                {
                    char spawnId = kvp.Key;
                    Vector2Int gridPos = kvp.Value;
                    Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

                    // Color based on spawn ID
                    Gizmos.color = GetSpawnMarkerColor(spawnId);
                    Gizmos.DrawWireSphere(worldPos, 0.5f);

                    // Draw label (only in selected gizmos)
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldPos + Vector3.up * 0.7f, spawnId.ToString());
                    #endif
                }

                // Draw lines between all spawn point pairs
                Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
                var keys = new List<char>(spawnPoints.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    for (int j = i + 1; j < keys.Count; j++)
                    {
                        Vector3 pos1 = mazeGridBehaviour.GridToWorld(spawnPoints[keys[i]].x, spawnPoints[keys[i]].y);
                        Vector3 pos2 = mazeGridBehaviour.GridToWorld(spawnPoints[keys[j]].x, spawnPoints[keys[j]].y);
                        Gizmos.DrawLine(pos1, pos2);
                    }
                }
            }
            // Fall back to legacy entrance/heart visualization
            else if (entrance != null && heart != null)
            {
                // Draw line from entrance to heart
                Vector3 entranceWorld = mazeGridBehaviour.GridToWorld(entrance.GridPosition.x, entrance.GridPosition.y);
                Vector3 heartWorld = mazeGridBehaviour.GridToWorld(heart.GridPosition.x, heart.GridPosition.y);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(entranceWorld, heartWorld);

                // Draw spawn position
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(entranceWorld, 0.5f);
            }
        }

        private Color GetSpawnMarkerColor(char spawnId)
        {
            switch (spawnId)
            {
                case 'A': return Color.cyan;
                case 'B': return Color.magenta;
                case 'C': return Color.yellow;
                case 'D': return new Color(1f, 0.5f, 0f); // Orange
                default: return Color.white;
            }
        }

        #endregion
    }
}

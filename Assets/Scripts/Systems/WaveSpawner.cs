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

        [SerializeField]
        [Tooltip("The Red Cap prefab (hostile actor that hunts visitors)")]
        private RedCapController redCapPrefab;

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

        [Header("Red Cap Settings")]
        [SerializeField]
        [Tooltip("Enable Red Cap spawning during waves")]
        private bool enableRedCap = true;

        [SerializeField]
        [Tooltip("Delay (in seconds) before spawning the first Red Cap in a wave")]
        private float redCapSpawnDelay = 60f;

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

        [Header("Auto-Start")]
        [SerializeField]
        [Tooltip("Automatically start first wave on scene start")]
        private bool autoStartFirstWave = false;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private bool isSpawning;
        private bool isWaveActive;
        private bool isWaveFailed;
        private bool isWaveSuccessful;
        private int currentWaveNumber;
        private int visitorsSpawnedThisWave;
        private int totalVisitorsSpawned;
        private float waveTimeRemaining;
        private List<VisitorController> activeVisitors = new List<VisitorController>();

        // Red Cap tracking
        private float redCapSpawnTimer;
        private bool hasSpawnedRedCap;
        private RedCapController currentRedCap;

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

        /// <summary>Gets the time remaining until Red Cap spawns (in seconds). Returns 0 if already spawned.</summary>
        public float RedCapSpawnTimeRemaining => hasSpawnedRedCap ? 0f : Mathf.Max(0f, redCapSpawnTimer);

        /// <summary>Gets whether a Red Cap has been spawned in this wave</summary>
        public bool HasSpawnedRedCap => hasSpawnedRedCap;

        /// <summary>Gets the current Red Cap instance (null if not spawned or destroyed)</summary>
        public RedCapController CurrentRedCap => currentRedCap;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            ValidateReferences();

            // Load settings from GameSettings
            LoadSettings();

            // Create UI if needed
            if (timerText == null || visitorCountText == null || waveStatusText == null)
            {
                CreateUI();
            }

            // Auto-start first wave if enabled
            if (autoStartFirstWave)
            {
                StartWave();
            }
        }

        /// <summary>
        /// Loads settings from GameSettings.
        /// </summary>
        private void LoadSettings()
        {
            visitorsPerWave = GameSettings.VisitorsPerWave;
            spawnInterval = GameSettings.SpawnInterval;
            waveDuration = GameSettings.WaveDuration;
            enableRedCap = GameSettings.EnableRedCap;
            redCapSpawnDelay = GameSettings.RedCapSpawnDelay;
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

            // Update Red Cap spawn timer
            if (enableRedCap && !hasSpawnedRedCap)
            {
                redCapSpawnTimer -= Time.deltaTime;

                if (redCapSpawnTimer <= 0f)
                {
                    SpawnRedCap();
                }
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
                return false;
            }

            // Prevent starting if wave is already active
            if (isWaveActive)
            {
                return false;
            }

            // Prevent starting if in failed state
            if (isWaveFailed)
            {
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
            isWaveSuccessful = false;
            waveTimeRemaining = waveDuration;

            // Initialize Red Cap state
            redCapSpawnTimer = redCapSpawnDelay;
            hasSpawnedRedCap = false;
            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }


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
            isWaveSuccessful = false;
            isWaveActive = false;

            // Clean up Red Cap
            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }
            hasSpawnedRedCap = false;

        }

        /// <summary>
        /// Retries the current wave without incrementing the wave number.
        /// Resets failed state and starts the wave again.
        /// </summary>
        public bool RetryCurrentWave()
        {
            if (!isWaveFailed)
            {
                return false;
            }


            // Reset failed state
            ResetFailedState();

            // Decrement wave number since StartWave() will increment it
            currentWaveNumber--;

            // Start the wave (which will re-increment to current wave number)
            return StartWave();
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
                    return;
                }
            }
            // Fall back to legacy entrance/heart system
            else if (entrance != null && heart != null)
            {
                startPos = entrance.GridPosition;
                destPos = heart.GridPosition;
            }
            else
            {
                return;
            }

            // Find path using A*
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(startPos, destPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
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
        /// Spawns a Red Cap at the entrance location.
        /// </summary>
        private void SpawnRedCap()
        {

            if (redCapPrefab == null)
            {
                hasSpawnedRedCap = true; // Mark as spawned to prevent repeated warnings
                return;
            }


            if (mazeGridBehaviour == null)
            {
                hasSpawnedRedCap = true;
                return;
            }


            // Determine spawn position (use random spawn marker or legacy entrance)
            Vector3 spawnWorldPos;
            Vector2Int spawnGridPos;

            // Try to get a random spawn point first
            if (mazeGridBehaviour.TryGetRandomSpawnPoint(out char spawnId, out spawnGridPos))
            {
                // Use spawn marker
                spawnWorldPos = mazeGridBehaviour.GridToWorld(spawnGridPos.x, spawnGridPos.y);
            }
            else if (entrance != null)
            {
                // Use legacy entrance
                spawnGridPos = entrance.GridPosition;
                spawnWorldPos = mazeGridBehaviour.GridToWorld(spawnGridPos.x, spawnGridPos.y);
            }
            else
            {
                hasSpawnedRedCap = true;
                return;
            }

            // Instantiate Red Cap
            currentRedCap = Instantiate(redCapPrefab, spawnWorldPos, Quaternion.identity);
            currentRedCap.name = $"RedCap_Wave{currentWaveNumber}";

            hasSpawnedRedCap = true;

        }

        /// <summary>
        /// Handles wave success when all visitors are cleared in time.
        /// </summary>
        private void HandleWaveSuccess()
        {
            if (!isWaveActive || isWaveFailed)
                return;

            isWaveActive = false;
            isWaveSuccessful = true;

            // Clean up Red Cap
            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }


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

            // Clean up Red Cap
            if (currentRedCap != null)
            {
                Destroy(currentRedCap.gameObject);
                currentRedCap = null;
            }


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
                else if (isWaveSuccessful)
                {
                    waveStatusText.text = $"Wave {currentWaveNumber} - SUCCESS!";
                    waveStatusText.color = new Color(0.3f, 1f, 0.3f, 1f);  // Green color
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
                isValid = false;
            }

            if (mazeGridBehaviour == null)
            {
                isValid = false;
            }

            // Check if we have either spawn markers or legacy entrance/heart
            if (mazeGridBehaviour != null)
            {
                bool hasSpawnMarkers = mazeGridBehaviour.GetSpawnPointCount() >= 2;
                bool hasLegacySystem = entrance != null && heart != null;

                if (!hasSpawnMarkers && !hasLegacySystem)
                {
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

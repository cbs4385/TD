using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Maze;
using FaeMaze.Audio;
using FaeMaze.Visitors;
using FaeMaze.HeartPowers;
using UnityEngine.TextCore.Text;
using FontStyles = TMPro.FontStyles;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave-based spawning of visitors from entrance spawn points toward exit targets (or the heart as a fallback).
    /// Spawns visitors in waves with configurable count and interval.
    /// Waves complete when all visitors are despawned.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        #region Events

        /// <summary>Invoked when a wave is successfully completed (all visitors cleared)</summary>
        public event System.Action OnWaveSuccess;

        #endregion

        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn (base visitor type, used as fallback)")]
        private VisitorController visitorPrefab;

        [SerializeField]
        [Tooltip("The mistaking visitor prefab (takes wrong turns at branches)")]
        private MistakingVisitorController mistakingVisitorPrefab;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Chance (0-1) to spawn mistaking visitor when available (default 0.8 = 80%)")]
        private float mistakingVisitorChance = 0.8f;

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

        [Header("Red Cap Settings")]
        [SerializeField]
        [Tooltip("Enable Red Cap spawning during waves")]
        private bool enableRedCap = true;

        [SerializeField]
        [Tooltip("Delay (in seconds) before spawning the first Red Cap in a wave")]
        private float redCapSpawnDelay = 60f;

        [Header("UI Configuration")]
        [SerializeField]
        [Tooltip("Enable WaveSpawner UI creation (deprecated - use HeartPowerPanelController instead)")]
        private bool enableWaveSpawnerUI = false;

        [SerializeField]
        [Tooltip("Canvas for UI display (will auto-create if null)")]
        private Canvas uiCanvas;

        [SerializeField]
        [Tooltip("Font size for wave info text")]
        private int fontSize = 20;

        [SerializeField]
        [Tooltip("Color for UI text")]
        private Color uiTextColor = Color.white;

        [Header("Auto-Start")]
        [SerializeField]
        [Tooltip("Automatically start first wave on scene start")]
        private bool autoStartFirstWave = false;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private HeartPowerManager heartPowerManager;
        private bool isSpawning;
        private bool isWaveActive;
        private bool isWaveSuccessful;
        private int currentWaveNumber;
        private int visitorsSpawnedThisWave;
        private int totalVisitorsSpawned;
        private List<GameObject> activeVisitors = new List<GameObject>();

        // Red Cap tracking
        private float redCapSpawnTimer;
        private bool hasSpawnedRedCap;
        private RedCapController currentRedCap;

        // UI References
        private TextMeshProUGUI visitorCountText;
        private TextMeshProUGUI waveStatusText;
        private TextMeshProUGUI essenceValueText;
        private Slider essenceBar;
        private GameObject uiPanel;

        #endregion

        #region Properties

        /// <summary>Gets whether a wave is currently spawning</summary>
        public bool IsSpawning => isSpawning;

        /// <summary>Gets whether a wave is currently active (including after spawning completes)</summary>
        public bool IsWaveActive => isWaveActive;

        /// <summary>Gets the current wave number</summary>
        public int CurrentWaveNumber => currentWaveNumber;

        /// <summary>Gets the total number of visitors spawned</summary>
        public int TotalVisitorsSpawned => totalVisitorsSpawned;

        /// <summary>Gets the number of active visitors currently in the maze</summary>
        public int ActiveVisitorCount => activeVisitors.Count;

        /// <summary>Gets the time remaining until Red Cap spawns (in seconds). Returns 0 if already spawned.</summary>
        public float RedCapSpawnTimeRemaining => hasSpawnedRedCap ? 0f : Mathf.Max(0f, redCapSpawnTimer);

        /// <summary>Gets whether a Red Cap has been spawned in this wave</summary>
        public bool HasSpawnedRedCap => hasSpawnedRedCap;

        /// <summary>Gets the current Red Cap instance (null if not spawned or destroyed)</summary>
        public RedCapController CurrentRedCap => currentRedCap;

        #endregion

        #region Setup Helpers

        /// <summary>
        /// Sets the number of completed waves so the next wave starts from the expected sequence.
        /// </summary>
        /// <param name="completedWaveCount">The last completed wave number.</param>
        public void SetCompletedWaveCount(int completedWaveCount)
        {
            currentWaveNumber = Mathf.Max(0, completedWaveCount);
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to GameController essence changes (only if WaveSpawner UI is enabled)
            if (enableWaveSpawnerUI && GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from GameController (only if WaveSpawner UI is enabled)
            if (enableWaveSpawnerUI && GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }
        }

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            // Find the HeartPowerManager in the scene
            heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null)
            {
                heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            }

            ValidateReferences();

            // Load settings from GameSettings
            LoadSettings();

            // Create UI if needed (deprecated - HeartPowerPanelController now handles all HUD)
            if (enableWaveSpawnerUI && (visitorCountText == null || waveStatusText == null))
            {
                CreateUI();

                // Subscribe to GameController events after UI is created
                if (GameController.Instance != null)
                {
                    GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                    GameController.Instance.OnEssenceChanged += OnEssenceChanged;

                    // Initialize essence display with current value
                    OnEssenceChanged(GameController.Instance.CurrentEssence);
                }
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
            enableRedCap = GameSettings.EnableRedCap;
            redCapSpawnDelay = GameSettings.RedCapSpawnDelay;
        }

        private void Update()
        {
            if (!isWaveActive)
                return;

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
        }

        #endregion

        #region Wave Management

        /// <summary>
        /// Starts spawning a new wave of visitors.
        /// Returns false if wave cannot start (already active).
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

            if (!ValidateReferences())
            {
                return false;
            }

            // Regenerate maze for procedurally generated mazes (not on first wave)
            if (currentWaveNumber > 0 && mazeGridBehaviour != null)
            {
                mazeGridBehaviour.RegenerateMaze();
            }

            // Initialize wave state
            currentWaveNumber++;
            visitorsSpawnedThisWave = 0;
            activeVisitors.Clear();
            isWaveActive = true;
            isWaveSuccessful = false;

            // Notify Heart Power Manager that wave has started
            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveStart();
            }
            else
            {
            }

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
        /// Resets the wave state to allow starting a new wave.
        /// </summary>
        public void ResetWaveState()
        {
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
        /// Falls back to entrance/heart if spawn markers are not available or there is only one marker.
        /// Chooses between mistaking visitor (80% chance) and regular visitor (20% chance) if both prefabs are assigned.
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
            if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 1)
            {
                // Prefer two different spawn points (entry -> exit)
                if (mazeGridBehaviour.TryGetRandomSpawnPair(out startId, out startPos, out destId, out destPos))
                {
                    // Both start and destination are valid spawn markers
                }
                // If only one spawn marker exists, fall back to heart as destination
                else if (mazeGridBehaviour.TryGetRandomSpawnPoint(out startId, out startPos))
                {
                    destPos = mazeGridBehaviour.HeartGridPos;
                }
                else
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

            // Choose which visitor type to spawn
            GameObject visitorObject = null;
            bool isMistakingVisitor = false;

            // If mistaking visitor prefab is assigned, roll for it
            if (mistakingVisitorPrefab != null)
            {
                float roll = Random.value;
                if (roll < mistakingVisitorChance)
                {
                    // Spawn mistaking visitor (rotated 180 degrees on z-axis)
                    MistakingVisitorController mistakingVisitor = Instantiate(mistakingVisitorPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
                    visitorObject = mistakingVisitor.gameObject;
                    isMistakingVisitor = true;

                    // Initialize mistaking visitor
                    mistakingVisitor.Initialize(GameController.Instance);
                    mistakingVisitor.SetPath(pathNodes);
                }
            }

            // Fall back to regular visitor if mistaking visitor wasn't spawned
            if (visitorObject == null && visitorPrefab != null)
            {
                VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
                visitorObject = visitor.gameObject;

                // Initialize regular visitor
                visitor.Initialize(GameController.Instance);
                visitor.SetPath(pathNodes);

                // Only regular visitors are tracked by GameController
                GameController.Instance.SetLastSpawnedVisitor(visitor);
            }

            if (visitorObject == null)
            {
                return;
            }

            // Name includes spawn IDs if using spawn marker system
            string visitorType = isMistakingVisitor ? "MistakingVisitor" : "Visitor";
            if (startId != '\0')
            {
                string destinationSuffix = destId != '\0' ? destId.ToString() : "H";
                visitorObject.name = $"{visitorType}_W{currentWaveNumber}_{visitorsSpawnedThisWave}_{startId}to{destinationSuffix}";
            }
            else
            {
                visitorObject.name = $"{visitorType}_Wave{currentWaveNumber}_{visitorsSpawnedThisWave}";
            }

            SoundManager.Instance?.PlayVisitorSpawn();

            // Track active visitor
            activeVisitors.Add(visitorObject);

            totalVisitorsSpawned++;
        }

        private string FormatPath(List<MazeGrid.MazeNode> pathNodes)
        {
            if (pathNodes == null || pathNodes.Count == 0)
            {
                return "<empty>";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pathNodes.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" -> ");
                }

                var node = pathNodes[i];
                sb.Append(i);
                sb.Append(':');
                sb.Append('(');
                sb.Append(node.x);
                sb.Append(',');
                sb.Append(node.y);
                sb.Append(')');
            }

            return sb.ToString();
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

            // Instantiate Red Cap (rotated 180 degrees on z-axis)
            currentRedCap = Instantiate(redCapPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
            currentRedCap.name = $"RedCap_Wave{currentWaveNumber}";

            hasSpawnedRedCap = true;

        }

        /// <summary>
        /// Handles wave success when all visitors are cleared.
        /// </summary>
        private void HandleWaveSuccess()
        {
            if (!isWaveActive)
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
            panelRect.sizeDelta = new Vector2(350f, 80f);  // Reduced height without timer

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

            // Create visitor count text (bottom)
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

            // Create essence display panel (positioned to the left of wave info)
            GameObject essencePanel = new GameObject("EssencePanelContainer");
            essencePanel.transform.SetParent(uiCanvas.transform, false);

            RectTransform essencePanelRect = essencePanel.AddComponent<RectTransform>();
            essencePanelRect.anchorMin = new Vector2(0.5f, 1f);  // Top-middle anchor
            essencePanelRect.anchorMax = new Vector2(0.5f, 1f);
            essencePanelRect.pivot = new Vector2(1f, 1f);  // Pivot at top-right
            essencePanelRect.anchoredPosition = new Vector2(-185f, -10f);  // To the left of wave panel
            essencePanelRect.sizeDelta = new Vector2(200f, 80f);

            Image essencePanelImage = essencePanel.AddComponent<Image>();
            essencePanelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            Outline essencePanelOutline = essencePanel.AddComponent<Outline>();
            essencePanelOutline.effectColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            essencePanelOutline.effectDistance = new Vector2(2f, -2f);

            // Create essence label (top)
            GameObject essenceLabelObj = new GameObject("EssenceLabel");
            essenceLabelObj.transform.SetParent(essencePanel.transform, false);

            RectTransform essenceLabelRect = essenceLabelObj.AddComponent<RectTransform>();
            essenceLabelRect.anchorMin = new Vector2(0f, 1f);
            essenceLabelRect.anchorMax = new Vector2(1f, 1f);
            essenceLabelRect.pivot = new Vector2(0.5f, 1f);
            essenceLabelRect.anchoredPosition = new Vector2(0f, -5f);
            essenceLabelRect.sizeDelta = new Vector2(-10f, 20f);

            TextMeshProUGUI essenceLabelText = essenceLabelObj.AddComponent<TextMeshProUGUI>();
            essenceLabelText.fontSize = fontSize - 2;
            essenceLabelText.color = new Color(0.6f, 0.8f, 1f, 1f);  // Light blue
            essenceLabelText.alignment = TextAlignmentOptions.Center;
            essenceLabelText.fontStyle = FontStyles.Bold;
            essenceLabelText.text = "Essence";

            // Create essence value text
            GameObject essenceValueObj = new GameObject("EssenceValue");
            essenceValueObj.transform.SetParent(essencePanel.transform, false);

            RectTransform essenceValueRect = essenceValueObj.AddComponent<RectTransform>();
            essenceValueRect.anchorMin = new Vector2(0f, 1f);
            essenceValueRect.anchorMax = new Vector2(1f, 1f);
            essenceValueRect.pivot = new Vector2(0.5f, 1f);
            essenceValueRect.anchoredPosition = new Vector2(0f, -25f);
            essenceValueRect.sizeDelta = new Vector2(-10f, 20f);

            essenceValueText = essenceValueObj.AddComponent<TextMeshProUGUI>();
            essenceValueText.fontSize = fontSize;
            essenceValueText.color = new Color(1f, 0.84f, 0f, 1f);  // Gold
            essenceValueText.alignment = TextAlignmentOptions.Center;
            essenceValueText.fontStyle = FontStyles.Bold;
            essenceValueText.text = "0 / 400";

            // Create essence bar (slider)
            GameObject essenceBarObj = new GameObject("EssenceBar");
            essenceBarObj.transform.SetParent(essencePanel.transform, false);

            RectTransform essenceBarRect = essenceBarObj.AddComponent<RectTransform>();
            essenceBarRect.anchorMin = new Vector2(0f, 0f);
            essenceBarRect.anchorMax = new Vector2(1f, 0f);
            essenceBarRect.pivot = new Vector2(0.5f, 0f);
            essenceBarRect.anchoredPosition = new Vector2(0f, 5f);
            essenceBarRect.sizeDelta = new Vector2(-20f, 20f);

            essenceBar = essenceBarObj.AddComponent<Slider>();
            essenceBar.minValue = 0f;
            essenceBar.maxValue = 400f;
            essenceBar.value = 0f;
            essenceBar.interactable = false;

            // Create slider background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(essenceBarObj.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create slider fill area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(essenceBarObj.transform, false);

            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // Create slider fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);  // Blue fill

            essenceBar.fillRect = fillRect;
            essenceBar.targetGraphic = fillImage;

            // Initialize essence display
            UpdateEssenceDisplay();
        }

        /// <summary>
        /// Updates the UI display with current wave status (deprecated - only used if enableWaveSpawnerUI is true).
        /// </summary>
        private void UpdateUI()
        {
            if (!enableWaveSpawnerUI)
                return;

            if (waveStatusText != null)
            {
                if (isWaveSuccessful)
                {
                    waveStatusText.text = $"Wave {currentWaveNumber} Complete!";
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

            if (visitorCountText != null)
            {
                int totalForWave = visitorsPerWave;
                visitorCountText.text = $"Visitors: {visitorsSpawnedThisWave}/{totalForWave} (Active: {activeVisitors.Count})";
            }
        }

        /// <summary>
        /// Called when essence changes in GameController.
        /// </summary>
        private void OnEssenceChanged(int newEssence)
        {
            UpdateEssenceDisplay();
        }

        /// <summary>
        /// Updates the essence display with current value from GameController.
        /// </summary>
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
            bool isValid = true;

            // Need at least one visitor prefab (regular or mistaking)
            if (visitorPrefab == null && mistakingVisitorPrefab == null)
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

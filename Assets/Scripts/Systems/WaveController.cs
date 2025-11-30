using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Visitors;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Controls visitor spawning with configurable rate and integrated UI display.
    /// Shows visitor count and countdown timer until next spawn.
    /// </summary>
    public class WaveController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Spawn Configuration")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        [SerializeField]
        [Tooltip("Total number of visitors to spawn")]
        private int totalVisitors = 10;

        [SerializeField]
        [Tooltip("Time in seconds between each spawn")]
        private float spawnInterval = 1.5f;

        [Header("UI References (Optional - Auto-creates if null)")]
        [SerializeField]
        [Tooltip("Canvas for UI display (will auto-create if null)")]
        private Canvas uiCanvas;

        [SerializeField]
        [Tooltip("Text element showing visitor count")]
        private TextMeshProUGUI visitorsText;

        [SerializeField]
        [Tooltip("Text element showing countdown timer")]
        private TextMeshProUGUI countdownText;

        [Header("UI Appearance")]
        [SerializeField]
        [Tooltip("Font size for visitor count text")]
        private int visitorsFontSize = 20;

        [SerializeField]
        [Tooltip("Font size for countdown text")]
        private int countdownFontSize = 16;

        [SerializeField]
        [Tooltip("Color for UI text")]
        private Color uiTextColor = Color.white;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private int visitorsSpawned = 0;
        private bool isSpawning = false;
        private float timeUntilNextSpawn = 0f;
        private bool allVisitorsSpawned = false;

        #endregion

        #region Properties

        /// <summary>Gets the number of visitors spawned so far</summary>
        public int VisitorsSpawned => visitorsSpawned;

        /// <summary>Gets the total number of visitors to spawn</summary>
        public int TotalVisitors => totalVisitors;

        /// <summary>Gets whether spawning is currently active</summary>
        public bool IsSpawning => isSpawning;

        /// <summary>Gets the time until next spawn</summary>
        public float TimeUntilNextSpawn => timeUntilNextSpawn;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find maze grid behaviour
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WaveController: MazeGridBehaviour not found in scene!");
                return;
            }

            // Validate visitor prefab
            if (visitorPrefab == null)
            {
                Debug.LogError("WaveController: Visitor prefab not assigned!");
                return;
            }

            // Create UI if needed
            if (visitorsText == null || countdownText == null)
            {
                CreateUI();
            }

            // Start spawning automatically
            StartSpawning();
        }

        private void Update()
        {
            if (!isSpawning || allVisitorsSpawned)
                return;

            // Update UI
            UpdateUI();
        }

        #endregion

        #region Spawning Control

        /// <summary>
        /// Starts the visitor spawning process.
        /// </summary>
        public void StartSpawning()
        {
            if (isSpawning)
            {
                Debug.LogWarning("WaveController: Already spawning!");
                return;
            }

            if (visitorPrefab == null)
            {
                Debug.LogError("WaveController: Cannot start spawning - visitor prefab not assigned!");
                return;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WaveController: Cannot start spawning - MazeGridBehaviour not found!");
                return;
            }

            visitorsSpawned = 0;
            allVisitorsSpawned = false;
            timeUntilNextSpawn = 0f;
            isSpawning = true;

            Debug.Log($"WaveController: Starting spawn of {totalVisitors} visitors at {spawnInterval}s intervals");

            StartCoroutine(SpawnVisitorsCoroutine());
        }

        /// <summary>
        /// Stops the spawning process.
        /// </summary>
        public void StopSpawning()
        {
            if (!isSpawning)
                return;

            isSpawning = false;
            StopAllCoroutines();

            Debug.Log($"WaveController: Spawning stopped at {visitorsSpawned}/{totalVisitors} visitors");
        }

        #endregion

        #region Spawning Logic

        /// <summary>
        /// Coroutine that spawns visitors at the configured interval.
        /// </summary>
        private IEnumerator SpawnVisitorsCoroutine()
        {
            for (int i = 0; i < totalVisitors; i++)
            {
                // Spawn visitor
                SpawnVisitor();
                visitorsSpawned++;

                Debug.Log($"WaveController: Spawned visitor {visitorsSpawned}/{totalVisitors}");

                // Check if this was the last visitor
                if (visitorsSpawned >= totalVisitors)
                {
                    allVisitorsSpawned = true;
                    timeUntilNextSpawn = 0f;

                    // Clear countdown text
                    if (countdownText != null)
                    {
                        countdownText.text = "";
                    }

                    Debug.Log("WaveController: All visitors spawned!");
                    break;
                }

                // Wait before next spawn with countdown
                timeUntilNextSpawn = spawnInterval;
                while (timeUntilNextSpawn > 0f)
                {
                    timeUntilNextSpawn -= Time.deltaTime;
                    yield return null;
                }
            }

            isSpawning = false;
        }

        /// <summary>
        /// Spawns a single visitor at a random spawn point.
        /// </summary>
        private void SpawnVisitor()
        {
            if (GameController.Instance == null)
            {
                Debug.LogError("WaveController: GameController not found!");
                return;
            }

            Vector2Int startPos;
            Vector2Int destPos;

            // Try to use spawn marker system
            if (mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                if (!mazeGridBehaviour.TryGetRandomSpawnPair(out char startId, out startPos, out char destId, out destPos))
                {
                    Debug.LogError("WaveController: Failed to get random spawn pair!");
                    return;
                }
            }
            else
            {
                Debug.LogError("WaveController: Need at least 2 spawn markers (A, B, C, D) in maze!");
                return;
            }

            // Find path using A*
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(startPos, destPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                Debug.LogWarning($"WaveController: No path found from {startPos} to {destPos}");
                return;
            }

            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(startPos.x, startPos.y);

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);
            visitor.gameObject.name = $"Visitor_{visitorsSpawned + 1}";

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path
            visitor.SetPath(pathNodes);
        }

        #endregion

        #region UI Management

        /// <summary>
        /// Creates the UI elements if they don't exist.
        /// </summary>
        private void CreateUI()
        {
            // Create or find canvas
            if (uiCanvas == null)
            {
                uiCanvas = FindFirstObjectByType<Canvas>();
                if (uiCanvas == null)
                {
                    GameObject canvasObj = new GameObject("WaveControllerCanvas");
                    uiCanvas = canvasObj.AddComponent<Canvas>();
                    uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                    CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);

                    canvasObj.AddComponent<GraphicRaycaster>();
                }
            }

            // Create panel container
            GameObject panel = new GameObject("WaveInfoPanel");
            panel.transform.SetParent(uiCanvas.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-10f, -10f);
            panelRect.sizeDelta = new Vector2(250f, 80f);

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Create visitors count text
            GameObject visitorsTextObj = new GameObject("VisitorsText");
            visitorsTextObj.transform.SetParent(panel.transform, false);

            RectTransform visitorsRect = visitorsTextObj.AddComponent<RectTransform>();
            visitorsRect.anchorMin = new Vector2(0f, 1f);
            visitorsRect.anchorMax = new Vector2(1f, 1f);
            visitorsRect.pivot = new Vector2(0.5f, 1f);
            visitorsRect.anchoredPosition = new Vector2(0f, -10f);
            visitorsRect.sizeDelta = new Vector2(-20f, 30f);

            visitorsText = visitorsTextObj.AddComponent<TextMeshProUGUI>();
            visitorsText.fontSize = visitorsFontSize;
            visitorsText.color = uiTextColor;
            visitorsText.alignment = TextAlignmentOptions.Center;
            visitorsText.text = $"Visitors: 0/{totalVisitors}";

            // Create countdown text
            GameObject countdownTextObj = new GameObject("CountdownText");
            countdownTextObj.transform.SetParent(panel.transform, false);

            RectTransform countdownRect = countdownTextObj.AddComponent<RectTransform>();
            countdownRect.anchorMin = new Vector2(0f, 0f);
            countdownRect.anchorMax = new Vector2(1f, 0f);
            countdownRect.pivot = new Vector2(0.5f, 0f);
            countdownRect.anchoredPosition = new Vector2(0f, 10f);
            countdownRect.sizeDelta = new Vector2(-20f, 25f);

            countdownText = countdownTextObj.AddComponent<TextMeshProUGUI>();
            countdownText.fontSize = countdownFontSize;
            countdownText.color = new Color(uiTextColor.r, uiTextColor.g, uiTextColor.b, 0.8f);
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.text = "";

            Debug.Log("WaveController: Auto-created UI elements");
        }

        /// <summary>
        /// Updates the UI display with current spawn progress.
        /// </summary>
        private void UpdateUI()
        {
            if (visitorsText != null)
            {
                visitorsText.text = $"Visitors: {visitorsSpawned}/{totalVisitors}";
            }

            if (countdownText != null && !allVisitorsSpawned)
            {
                if (timeUntilNextSpawn > 0f)
                {
                    countdownText.text = $"Next spawn in: {timeUntilNextSpawn:F1}s";
                }
                else
                {
                    countdownText.text = "Spawning...";
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw spawn info in scene view
            if (Application.isPlaying && isSpawning)
            {
                Gizmos.color = Color.cyan;
                // Could draw spawn points or other debug info here
            }
        }

        #endregion
    }
}

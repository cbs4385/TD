using UnityEngine;
using UnityEngine.UI;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the debug panel UI for toggling visualization features,
    /// adjusting timescale, and spawning test visitors.
    /// </summary>
    public class DebugUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField]
        [Tooltip("Debug panel GameObject (will be shown/hidden with F1)")]
        private GameObject debugPanel;

        [SerializeField]
        [Tooltip("Toggle for grid gizmos visualization")]
        private Toggle gridToggle;

        [SerializeField]
        [Tooltip("Toggle for attraction heatmap visualization")]
        private Toggle heatmapToggle;

        [SerializeField]
        [Tooltip("Slider for adjusting game timescale (0.1 to 2.0)")]
        private Slider timescaleSlider;

        [SerializeField]
        [Tooltip("Button to spawn a test visitor")]
        private Button spawnTestVisitorButton;

        [Header("System References")]
        [SerializeField]
        [Tooltip("Reference to the maze grid behaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the wave spawner")]
        private WaveSpawner waveSpawner;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-find references if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (waveSpawner == null)
            {
                waveSpawner = FindFirstObjectByType<WaveSpawner>();
            }

            // Initialize toggles to true (default debug visualization on)
            if (gridToggle != null)
            {
                gridToggle.isOn = true;
                gridToggle.onValueChanged.AddListener(OnGridToggleChanged);
            }

            if (heatmapToggle != null)
            {
                heatmapToggle.isOn = true;
                heatmapToggle.onValueChanged.AddListener(OnHeatmapToggleChanged);
            }

            // Initialize timescale slider
            if (timescaleSlider != null)
            {
                timescaleSlider.minValue = 0.1f;
                timescaleSlider.maxValue = 2.0f;
                timescaleSlider.value = 1.0f;
                timescaleSlider.onValueChanged.AddListener(OnTimescaleChanged);
            }

            // Initialize spawn button
            if (spawnTestVisitorButton != null)
            {
                spawnTestVisitorButton.onClick.AddListener(OnSpawnTestVisitorClicked);
            }

            // Debug panel starts visible for initial setup
            if (debugPanel != null)
            {
                debugPanel.SetActive(true);
            }
        }

        private void Update()
        {
            // Toggle debug panel with F1 key
            if (Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugPanel();
            }
        }

        private void OnDestroy()
        {
            // Clean up listeners
            if (gridToggle != null)
            {
                gridToggle.onValueChanged.RemoveListener(OnGridToggleChanged);
            }

            if (heatmapToggle != null)
            {
                heatmapToggle.onValueChanged.RemoveListener(OnHeatmapToggleChanged);
            }

            if (timescaleSlider != null)
            {
                timescaleSlider.onValueChanged.RemoveListener(OnTimescaleChanged);
            }

            if (spawnTestVisitorButton != null)
            {
                spawnTestVisitorButton.onClick.RemoveListener(OnSpawnTestVisitorClicked);
            }
        }

        #endregion

        #region UI Callbacks

        /// <summary>
        /// Called when the grid toggle value changes.
        /// </summary>
        private void OnGridToggleChanged(bool value)
        {
            if (mazeGridBehaviour != null)
            {
                mazeGridBehaviour.SetDrawGridGizmos(value);
            }
        }

        /// <summary>
        /// Called when the heatmap toggle value changes.
        /// </summary>
        private void OnHeatmapToggleChanged(bool value)
        {
            if (mazeGridBehaviour != null)
            {
                mazeGridBehaviour.SetDrawAttractionHeatmap(value);
            }
        }

        /// <summary>
        /// Called when the timescale slider value changes.
        /// </summary>
        private void OnTimescaleChanged(float value)
        {
            Time.timeScale = value;
        }

        /// <summary>
        /// Called when the spawn test visitor button is clicked.
        /// </summary>
        private void OnSpawnTestVisitorClicked()
        {
            if (waveSpawner != null)
            {
                waveSpawner.SpawnSingleVisitorForDebug();
            }
            else
            {
                Debug.LogWarning("WaveSpawner reference is not assigned in DebugUIController!");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Toggles the debug panel visibility.
        /// </summary>
        private void ToggleDebugPanel()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }
        }

        #endregion
    }
}

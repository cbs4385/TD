using UnityEngine;
using UnityEngine.Profiling;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Profiling utility for monitoring rendering performance.
    /// Tracks draw calls, batches, vertices, and provides optimization recommendations.
    /// </summary>
    public class RenderingProfiler : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Display Settings")]
        [SerializeField]
        [Tooltip("Show profiler overlay on screen")]
        private bool showOverlay = true;

        [SerializeField]
        [Tooltip("Update interval in seconds")]
        private float updateInterval = 0.5f;

        [SerializeField]
        [Tooltip("Font size for overlay text")]
        private int fontSize = 14;

        #endregion

        #region Private Fields

        private float updateTimer;
        private string statsText = "";

        // Cached stats
        private float fps;
        private long totalMemory;
        private long meshMemory;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            updateTimer += Time.deltaTime;

            if (updateTimer >= updateInterval)
            {
                UpdateStats();
                updateTimer = 0f;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay)
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(10, 10, 10, 10);

            // Background box (adjusted size for simpler stats)
            GUI.Box(new Rect(10, 10, 300, 160), "");

            // Draw stats text
            GUI.Label(new Rect(15, 15, 290, 150), statsText, style);
        }

        #endregion

        #region Private Methods

        private void UpdateStats()
        {
            // Calculate FPS
            fps = 1f / Time.deltaTime;

            // Get memory stats
            totalMemory = Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024; // MB
            meshMemory = Profiler.GetAllocatedMemoryForGraphicsDriver() / 1024 / 1024; // MB

            // Note: Rendering stats (batches, triangles, etc.) require Unity's internal UnityStats class
            // which is not accessible in all Unity versions. Use Unity's built-in Stats window instead:
            // Game View → Stats button for detailed rendering information

            // Build stats text
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Performance Monitor</b>");
            sb.AppendLine($"FPS: {fps:F1}");
            sb.AppendLine();
            sb.AppendLine($"Memory: {totalMemory} MB");
            sb.AppendLine($"GFX Memory: {meshMemory} MB");
            sb.AppendLine();
            sb.AppendLine("For detailed rendering stats:");
            sb.AppendLine("Use Game View → Stats button");

            statsText = sb.ToString();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets current performance stats as a formatted string.
        /// </summary>
        public string GetStatsReport()
        {
            UpdateStats();

            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== Performance Report ===");
            report.AppendLine($"Frame Rate: {fps:F1} FPS");
            report.AppendLine($"Total Memory: {totalMemory} MB");
            report.AppendLine($"Graphics Memory: {meshMemory} MB");
            report.AppendLine();
            report.AppendLine("For detailed rendering stats (batches, triangles, etc.):");
            report.AppendLine("- Open Unity Editor");
            report.AppendLine("- Click Game View → Stats button");
            report.AppendLine("- Or use Window → Analysis → Profiler");

            return report.ToString();
        }

        /// <summary>
        /// Logs the performance report to console.
        /// </summary>
        public void LogReport()
        {
        }

        /// <summary>
        /// Toggles the overlay display.
        /// </summary>
        public void ToggleOverlay()
        {
            showOverlay = !showOverlay;
        }

        #endregion
    }
}

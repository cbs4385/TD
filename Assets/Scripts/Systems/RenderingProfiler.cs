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

        [Header("Warning Thresholds")]
        [SerializeField]
        [Tooltip("Warn if draw calls exceed this value")]
        private int drawCallWarningThreshold = 100;

        [SerializeField]
        [Tooltip("Warn if triangle count exceeds this value")]
        private int triangleWarningThreshold = 100000;

        #endregion

        #region Private Fields

        private float updateTimer;
        private string statsText = "";

        // Cached stats
        private int drawCalls;
        private int batches;
        private int triangles;
        private int vertices;
        private int setPassCalls;
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

            // Background box
            GUI.Box(new Rect(10, 10, 300, 240), "");

            // Draw stats text
            GUI.Label(new Rect(15, 15, 290, 230), statsText, style);
        }

        #endregion

        #region Private Methods

        private void UpdateStats()
        {
            // Get rendering stats
            drawCalls = UnityEngine.Rendering.FrameDebugger.enabled ?
                UnityEngine.Rendering.FrameDebugger.count : 0;
            batches = UnityStats.batches;
            triangles = UnityStats.triangles;
            vertices = UnityStats.vertices;
            setPassCalls = UnityStats.setPassCalls;

            // Calculate FPS
            fps = 1f / Time.deltaTime;

            // Get memory stats
            totalMemory = Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024; // MB
            meshMemory = Profiler.GetAllocatedMemoryForGraphicsDriver() / 1024 / 1024; // MB

            // Build stats text
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Rendering Profiler</b>");
            sb.AppendLine($"FPS: {fps:F1}");
            sb.AppendLine();

            // Draw calls (with warning)
            if (batches > drawCallWarningThreshold)
            {
                sb.AppendLine($"<color=red>Batches: {batches} (HIGH!)</color>");
            }
            else
            {
                sb.AppendLine($"Batches: {batches}");
            }

            sb.AppendLine($"SetPass Calls: {setPassCalls}");

            // Geometry (with warning)
            if (triangles > triangleWarningThreshold)
            {
                sb.AppendLine($"<color=yellow>Triangles: {triangles:N0} (HIGH)</color>");
            }
            else
            {
                sb.AppendLine($"Triangles: {triangles:N0}");
            }

            sb.AppendLine($"Vertices: {vertices:N0}");
            sb.AppendLine();
            sb.AppendLine($"Memory: {totalMemory} MB");
            sb.AppendLine($"GFX Memory: {meshMemory} MB");

            statsText = sb.ToString();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets current batching stats as a formatted string.
        /// </summary>
        public string GetStatsReport()
        {
            UpdateStats();

            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== Rendering Performance Report ===");
            report.AppendLine($"Frame Rate: {fps:F1} FPS");
            report.AppendLine($"Batches: {batches}");
            report.AppendLine($"SetPass Calls: {setPassCalls}");
            report.AppendLine($"Triangles: {triangles:N0}");
            report.AppendLine($"Vertices: {vertices:N0}");
            report.AppendLine($"Total Memory: {totalMemory} MB");
            report.AppendLine($"Graphics Memory: {meshMemory} MB");

            // Recommendations
            report.AppendLine();
            report.AppendLine("=== Recommendations ===");

            if (batches > drawCallWarningThreshold)
            {
                report.AppendLine("- HIGH BATCH COUNT: Enable mesh batching or SRP Batcher");
            }

            if (triangles > triangleWarningThreshold)
            {
                report.AppendLine("- HIGH TRIANGLE COUNT: Consider using LODs or mesh simplification");
            }

            if (setPassCalls > batches * 1.5f)
            {
                report.AppendLine("- HIGH SETPASS CALLS: Reduce material count or use material atlasing");
            }

            return report.ToString();
        }

        /// <summary>
        /// Logs the performance report to console.
        /// </summary>
        public void LogReport()
        {
            Debug.Log(GetStatsReport());
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

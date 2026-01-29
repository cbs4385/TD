using UnityEngine;
using System.Diagnostics;
using System.Collections;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Global frame profiler that tracks time spent in different systems.
    /// Logs warnings when frame time exceeds threshold.
    /// Runs with Script Execution Order -100 to execute before other scripts.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class FrameProfiler : MonoBehaviour
    {
        private static FrameProfiler _instance;
        public static FrameProfiler Instance => _instance;

        private Stopwatch _frameStopwatch;
        private long _lastFrameStartMs;
        private int _frameCount;

        // Track time between Update calls
        private float _lastUpdateTime;

        // Track where we are in frame execution
        private static int _lastLoggedFrame;
        private static string _lastCheckpoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FrameProfiler");
                _instance = go.AddComponent<FrameProfiler>();
            }
        }

        private bool _isValid = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _frameStopwatch = new Stopwatch();
            _lastUpdateTime = Time.realtimeSinceStartup;
            _isValid = true;
        }

        private void Start()
        {
            // Start high-frequency pulse coroutine
            StartCoroutine(HighFrequencyPulse());
        }

        /// <summary>
        /// High-frequency coroutine that tracks timing.
        /// Gap logging disabled to reduce console noise.
        /// </summary>
        private IEnumerator HighFrequencyPulse()
        {
            float lastPulseTime = Time.realtimeSinceStartup;

            while (true)
            {
                float now = Time.realtimeSinceStartup;
                lastPulseTime = now;
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        // Track FixedUpdate timing
        private float _lastFixedUpdateTime;
        private int _fixedUpdateCount;

        private void FixedUpdate()
        {
            if (!_isValid) return;

            float currentTime = Time.realtimeSinceStartup;
            _lastFixedUpdateTime = currentTime;
            _fixedUpdateCount++;
            _lastCheckpoint = $"FrameProfiler.FixedUpdate#{_fixedUpdateCount}";
        }

        private void Update()
        {
            if (!_isValid) return;

            float currentTime = Time.realtimeSinceStartup;
            _lastUpdateTime = currentTime;
            _frameStopwatch.Restart();
            _frameCount = Time.frameCount;
            _fixedUpdateCount = 0;
            _lastCheckpoint = "FrameProfiler.Update";
        }

        private void LateUpdate()
        {
            if (!_isValid) return;

            _frameStopwatch.Stop();
            _lastCheckpoint = "FrameProfiler.LateUpdate";
        }

        /// <summary>
        /// Call this at checkpoints to track execution progress.
        /// Logging disabled to reduce console noise.
        /// </summary>
        public static void Checkpoint(string name)
        {
            _lastCheckpoint = name;
        }
    }
}

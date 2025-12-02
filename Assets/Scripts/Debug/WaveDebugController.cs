using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Debug
{
    /// <summary>
    /// Debug controller for testing wave management system.
    /// Provides keyboard shortcuts to start/stop/retry waves.
    /// </summary>
    public class WaveDebugController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the Wave Manager")]
        private WaveManager waveManager;

        [SerializeField]
        [Tooltip("Reference to the Wave Spawner")]
        private WaveSpawner waveSpawner;

        [Header("Debug Settings")]
        [SerializeField]
        [Tooltip("Enable debug keyboard controls")]
        private bool enableDebugControls = true;

        [SerializeField]
        [Tooltip("Show debug UI overlay")]
        private bool showDebugUI = true;

        #endregion

        #region Private Fields

        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Find references if not assigned
            if (waveManager == null)
            {
                waveManager = FindFirstObjectByType<WaveManager>();
            }

            if (waveSpawner == null)
            {
                waveSpawner = FindFirstObjectByType<WaveSpawner>();
            }
        }

        private void Update()
        {
            if (!enableDebugControls)
                return;

            // F1 - Start Game (Wave 1)
            if (Input.GetKeyDown(KeyCode.F1))
            {
                UnityEngine.Debug.Log("[Debug] F1 - Starting game");
                if (waveManager != null)
                {
                    waveManager.StartGame();
                }
            }

            // F2 - Start Next Wave
            if (Input.GetKeyDown(KeyCode.F2))
            {
                UnityEngine.Debug.Log("[Debug] F2 - Starting next wave");
                if (waveManager != null)
                {
                    waveManager.StartNextWave();
                }
            }

            // F3 - Retry Current Wave
            if (Input.GetKeyDown(KeyCode.F3))
            {
                UnityEngine.Debug.Log("[Debug] F3 - Retrying current wave");
                if (waveManager != null)
                {
                    waveManager.RetryCurrentWave();
                }
            }

            // F4 - Restart Game
            if (Input.GetKeyDown(KeyCode.F4))
            {
                UnityEngine.Debug.Log("[Debug] F4 - Restarting game");
                if (waveManager != null)
                {
                    waveManager.RestartGame();
                }
            }

            // F5 - Reset State
            if (Input.GetKeyDown(KeyCode.F5))
            {
                UnityEngine.Debug.Log("[Debug] F5 - Resetting state");
                if (waveManager != null)
                {
                    waveManager.ResetState();
                }
            }
        }

        private void OnGUI()
        {
            if (!showDebugUI)
                return;

            InitializeStyles();

            // Create debug panel
            GUILayout.BeginArea(new Rect(10, 10, 400, 400));
            GUILayout.BeginVertical("box");

            // Title
            GUILayout.Label("Wave Debug Controller", labelStyle);
            GUILayout.Space(10);

            // Wave Manager Info
            if (waveManager != null)
            {
                GUILayout.Label($"Game Over: {waveManager.IsGameOver}", labelStyle);
                GUILayout.Label($"Last Completed Wave: {waveManager.LastCompletedWave}", labelStyle);
                GUILayout.Label($"Current Retry Count: {waveManager.CurrentRetryCount}", labelStyle);
            }
            else
            {
                GUILayout.Label("Wave Manager: Not Found", labelStyle);
            }

            GUILayout.Space(10);

            // Wave Spawner Info
            if (waveSpawner != null)
            {
                GUILayout.Label($"Current Wave: {waveSpawner.CurrentWaveNumber}", labelStyle);
                GUILayout.Label($"Is Spawning: {waveSpawner.IsSpawning}", labelStyle);
                GUILayout.Label($"Is Wave Active: {waveSpawner.IsWaveActive}", labelStyle);
                GUILayout.Label($"Is Wave Failed: {waveSpawner.IsWaveFailed}", labelStyle);
                GUILayout.Label($"Active Visitors: {waveSpawner.ActiveVisitorCount}", labelStyle);
                GUILayout.Label($"Time Remaining: {waveSpawner.WaveTimeRemaining:F1}s", labelStyle);
            }
            else
            {
                GUILayout.Label("Wave Spawner: Not Found", labelStyle);
            }

            GUILayout.Space(10);

            // Control Buttons
            GUILayout.Label("Keyboard Controls:", labelStyle);
            GUILayout.Label("F1 - Start Game (Wave 1)", labelStyle);
            GUILayout.Label("F2 - Start Next Wave", labelStyle);
            GUILayout.Label("F3 - Retry Current Wave", labelStyle);
            GUILayout.Label("F4 - Restart Game", labelStyle);
            GUILayout.Label("F5 - Reset State", labelStyle);

            GUILayout.Space(10);

            // Manual Buttons
            if (waveManager != null)
            {
                if (GUILayout.Button("Start Game", buttonStyle))
                {
                    waveManager.StartGame();
                }

                if (GUILayout.Button("Start Next Wave", buttonStyle))
                {
                    waveManager.StartNextWave();
                }

                if (GUILayout.Button("Retry Wave", buttonStyle))
                {
                    waveManager.RetryCurrentWave();
                }

                if (GUILayout.Button("Reset State", buttonStyle))
                {
                    waveManager.ResetState();
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion

        #region Helper Methods

        private void InitializeStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 14;
                labelStyle.normal.textColor = Color.white;
            }

            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 14;
            }
        }

        #endregion
    }
}

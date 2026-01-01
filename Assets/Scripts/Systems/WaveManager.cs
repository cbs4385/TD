using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;
using FaeMaze.HeartPowers;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave progression and game-over scenarios based on essence depletion.
    /// Subscribes to WaveSpawner and GameController events to drive the game flow.
    /// Game ends when essence reaches 0; waves continue indefinitely until then.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the WaveSpawner")]
        private WaveSpawner waveSpawner;

        [SerializeField]
        [Tooltip("Reference to the GameController")]
        private GameController gameController;

        [SerializeField]
        [Tooltip("Reference to the HeartPowerManager")]
        private HeartPowerManager heartPowerManager;

        [Header("UI References")]
        [SerializeField]
        [Tooltip("Panel shown when wave is completed")]
        private GameObject waveCompletePanel;

        [SerializeField]
        [Tooltip("Text displaying wave complete message")]
        private TextMeshProUGUI waveCompleteText;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Button to start next wave")]
        private Button nextWaveButton;

        [Header("Wave Progression Settings")]
        [SerializeField]
        [Tooltip("Auto-start next wave after completion (if false, player must click button)")]
        private bool autoStartNextWave = false;

        [SerializeField]
        [Tooltip("Delay before auto-starting next wave (in seconds)")]
        private float autoStartDelay = 2f;

        #endregion

        #region Private Fields

        private int lastCompletedWave = 0;
        private bool isGameOver = false;
        private float autoStartTimer = 0f;
        private bool waitingForAutoStart = false;

        // Persistent wave tracking across scenes
        private static int persistentLastCompletedWave = 0;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked when a wave is successfully completed.
        /// Passes the wave number as a parameter.
        /// </summary>
        public event System.Action<int> OnWaveCompleted;

        /// <summary>
        /// Event invoked when the game is over.
        /// </summary>
        public event System.Action OnGameOver;

        /// <summary>
        /// Event invoked when the game is restarted.
        /// </summary>
        public event System.Action OnGameRestart;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Find references if not assigned
            if (waveSpawner == null)
            {
                waveSpawner = FindFirstObjectByType<WaveSpawner>();
            }

            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (heartPowerManager == null)
            {
                heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            }

            // Carry over persistent wave progress when reloading scenes
            lastCompletedWave = persistentLastCompletedWave;

            // Hide all UI panels initially
            HideAllPanels();

            // Apply persistent progress before any auto-started waves can run
            ApplyPersistentWaveProgress();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Start()
        {
            SetupButtons();
            LoadSettings();
        }

        /// <summary>
        /// Loads settings from GameSettings.
        /// </summary>
        private void LoadSettings()
        {
            autoStartNextWave = GameSettings.AutoStartNextWave;
            autoStartDelay = GameSettings.AutoStartDelay;
        }

        private void Update()
        {
            // Handle auto-start timer
            if (waitingForAutoStart && autoStartNextWave)
            {
                autoStartTimer -= Time.deltaTime;

                if (autoStartTimer <= 0f)
                {
                    waitingForAutoStart = false;
                    StartNextWave();
                }
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            if (waveSpawner != null)
            {
                waveSpawner.OnWaveSuccess += HandleWaveSuccess;
            }

            if (gameController != null)
            {
                gameController.OnEssenceChanged += HandleEssenceChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (waveSpawner != null)
            {
                waveSpawner.OnWaveSuccess -= HandleWaveSuccess;
            }

            if (gameController != null)
            {
                gameController.OnEssenceChanged -= HandleEssenceChanged;
            }
        }

        #endregion

        #region Button Setup

        private void SetupButtons()
        {
            if (nextWaveButton != null)
            {
                nextWaveButton.onClick.AddListener(OnNextWaveClicked);
            }
        }

        #endregion

        #region Event Handlers

        private void HandleWaveSuccess()
        {
            // Check if game is already over (essence depleted during wave)
            if (isGameOver)
            {
                return;
            }

            // Update last completed wave
            lastCompletedWave = waveSpawner.CurrentWaveNumber;

            // Notify Heart Power Manager
            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveSuccess();
            }

            // Track stats
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.RecordWaveReached(waveSpawner.CurrentWaveNumber);
            }

            // Invoke event
            OnWaveCompleted?.Invoke(lastCompletedWave);

            // Persist latest wave progress for scene transitions
            UpdatePersistentWaveProgress();

            // Transition to procedural mazes after the initial FaeMazeScene wave
            if (ShouldTransitionToProceduralScene())
            {
                if (TryLoadProceduralMazeScene())
                {
                    return;
                }
            }

            // Show wave complete UI
            ShowWaveCompletePanel();

            // Handle auto-start
            if (autoStartNextWave)
            {
                autoStartTimer = autoStartDelay;
                waitingForAutoStart = true;
            }
        }

        private void HandleEssenceChanged(int newEssence)
        {
            // Check if essence has reached 0 or below
            if (newEssence <= 0 && !isGameOver)
            {
                HandleGameOver();
            }
        }

        private void HandleGameOver()
        {
            // Mark game as over
            isGameOver = true;

            // Stop auto-start timer
            waitingForAutoStart = false;
            autoStartTimer = 0f;

            // Notify Heart Power Manager
            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveFail();
            }

            // Invoke event
            OnGameOver?.Invoke();

            // Record final wave reached
            if (GameStatsTracker.Instance != null && waveSpawner != null)
            {
                GameStatsTracker.Instance.RecordWaveReached(waveSpawner.CurrentWaveNumber);
            }

            // Load the GameOver scene
            LoadGameOverScene();
        }

        #endregion

        #region Game Flow Logic

        // Game flow logic is now primarily driven by essence depletion (HandleEssenceChanged)
        // and wave completion (HandleWaveSuccess)

        #endregion

        #region UI Display Methods

        private void ShowWaveCompletePanel()
        {
            HideAllPanels();

            if (waveCompletePanel != null)
            {
                waveCompletePanel.SetActive(true);
            }

            if (waveCompleteText != null)
            {
                waveCompleteText.text = $"Wave {lastCompletedWave} Complete!";
            }
        }

        private void HideAllPanels()
        {
            if (waveCompletePanel != null)
            {
                waveCompletePanel.SetActive(false);
            }

            // Cancel auto-start timer if hiding panels
            if (!isGameOver)
            {
                waitingForAutoStart = false;
                autoStartTimer = 0f;
            }
        }

        #endregion

        #region Button Handlers

        private void OnNextWaveClicked()
        {
            StartNextWave();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the first wave. Call this from menus or debug controls.
        /// </summary>
        public void StartGame()
        {
            // Reset game state
            isGameOver = false;
            lastCompletedWave = 0;
            ResetPersistentWaveProgress();

            // Hide all panels
            HideAllPanels();

            // Notify Heart Power Manager
            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveStart();
            }

            // Start first wave
            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        /// <summary>
        /// Starts the next wave in sequence.
        /// </summary>
        public void StartNextWave()
        {
            if (isGameOver)
            {
                return;
            }

            HideAllPanels();

            // Notify Heart Power Manager
            if (heartPowerManager != null)
            {
                heartPowerManager.OnWaveStart();
            }

            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        /// <summary>
        /// Loads the Game Over scene with statistics.
        /// </summary>
        private void LoadGameOverScene()
        {

            // Record final wave reached
            if (GameStatsTracker.Instance != null && waveSpawner != null)
            {
                GameStatsTracker.Instance.RecordWaveReached(waveSpawner.CurrentWaveNumber);
            }

            // Load the GameOver scene
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameOver");
            }
            catch (System.Exception e)
            {
                _ = e;
            }
        }

        /// <summary>
        /// Resets the wave manager state without reloading the scene.
        /// </summary>
        public void ResetState()
        {
            isGameOver = false;
            lastCompletedWave = persistentLastCompletedWave;
            waitingForAutoStart = false;
            autoStartTimer = 0f;

            HideAllPanels();
        }

        #endregion

        #region Scene Transition Helpers

        private void ApplyPersistentWaveProgress()
        {
            if (waveSpawner != null && persistentLastCompletedWave > 0)
            {
                waveSpawner.SetCompletedWaveCount(persistentLastCompletedWave);
            }
        }

        private void UpdatePersistentWaveProgress()
        {
            persistentLastCompletedWave = Mathf.Max(persistentLastCompletedWave, lastCompletedWave);
        }

        private void ResetPersistentWaveProgress()
        {
            persistentLastCompletedWave = 0;
        }

        private bool ShouldTransitionToProceduralScene()
        {
            return SceneManager.GetActiveScene().name == "FaeMazeScene" && lastCompletedWave >= 1;
        }

        private bool TryLoadProceduralMazeScene()
        {
            waitingForAutoStart = false;
            autoStartTimer = 0f;

            if (!IsSceneInBuildSettings("ProceduralMazeScene"))
            {
                return false;
            }

            SceneManager.LoadScene("ProceduralMazeScene");
            return true;
        }

        private bool IsSceneInBuildSettings(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);

                if (name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether the game is currently over.
        /// </summary>
        public bool IsGameOver => isGameOver;

        /// <summary>
        /// Gets the last completed wave number.
        /// </summary>
        public int LastCompletedWave => lastCompletedWave;

        #endregion
    }
}

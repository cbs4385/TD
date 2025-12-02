using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave progression, retry logic, and game-over scenarios.
    /// Subscribes to WaveSpawner events and drives the game flow.
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

        [Header("UI References")]
        [SerializeField]
        [Tooltip("Panel shown when wave is successful")]
        private GameObject waveSuccessPanel;

        [SerializeField]
        [Tooltip("Panel shown when wave fails")]
        private GameObject waveFailedPanel;

        [SerializeField]
        [Tooltip("Panel shown when game is over")]
        private GameObject gameOverPanel;

        [SerializeField]
        [Tooltip("Text displaying wave success message")]
        private TextMeshProUGUI waveSuccessText;

        [SerializeField]
        [Tooltip("Text displaying wave failed message")]
        private TextMeshProUGUI waveFailedText;

        [SerializeField]
        [Tooltip("Text displaying game over message")]
        private TextMeshProUGUI gameOverText;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Button to start next wave")]
        private Button nextWaveButton;

        [SerializeField]
        [Tooltip("Button to retry current wave")]
        private Button retryWaveButton;

        [SerializeField]
        [Tooltip("Button to restart game from wave 1")]
        private Button restartGameButton;

        [SerializeField]
        [Tooltip("Button to return to main menu")]
        private Button mainMenuButton;

        [Header("Wave Progression Settings")]
        [SerializeField]
        [Tooltip("Auto-start next wave after success (if false, player must click button)")]
        private bool autoStartNextWave = false;

        [SerializeField]
        [Tooltip("Delay before auto-starting next wave (in seconds)")]
        private float autoStartDelay = 2f;

        [SerializeField]
        [Tooltip("Maximum number of retries allowed per wave (-1 for unlimited)")]
        private int maxRetriesPerWave = -1;

        [SerializeField]
        [Tooltip("Minimum essence required to continue game")]
        private int minimumEssenceToContinue = 0;

        #endregion

        #region Private Fields

        private int currentRetryCount = 0;
        private int lastCompletedWave = 0;
        private bool isGameOver = false;
        private float autoStartTimer = 0f;
        private bool waitingForAutoStart = false;

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

            // Hide all UI panels initially
            HideAllPanels();
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
                waveSpawner.OnWaveFailed += HandleWaveFailed;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (waveSpawner != null)
            {
                waveSpawner.OnWaveSuccess -= HandleWaveSuccess;
                waveSpawner.OnWaveFailed -= HandleWaveFailed;
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

            if (retryWaveButton != null)
            {
                retryWaveButton.onClick.AddListener(OnRetryWaveClicked);
            }

            if (restartGameButton != null)
            {
                restartGameButton.onClick.AddListener(OnRestartGameClicked);
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
        }

        #endregion

        #region Event Handlers

        private void HandleWaveSuccess()
        {
            Debug.Log($"[WaveManager] Wave {waveSpawner.CurrentWaveNumber} completed successfully!");

            // Update last completed wave
            lastCompletedWave = waveSpawner.CurrentWaveNumber;

            // Reset retry count on success
            currentRetryCount = 0;

            // Invoke event
            OnWaveCompleted?.Invoke(lastCompletedWave);

            // Show success UI
            ShowWaveSuccessPanel();

            // Handle auto-start
            if (autoStartNextWave)
            {
                autoStartTimer = autoStartDelay;
                waitingForAutoStart = true;
            }
        }

        private void HandleWaveFailed()
        {
            Debug.Log($"[WaveManager] Wave {waveSpawner.CurrentWaveNumber} failed!");

            // Check if game over conditions are met
            if (ShouldGameOver())
            {
                ShowGameOverPanel();
                isGameOver = true;
                OnGameOver?.Invoke();
                return;
            }

            // Show retry UI
            ShowWaveFailedPanel();
        }

        #endregion

        #region Game Flow Logic

        private bool ShouldGameOver()
        {
            // Game over if player doesn't have minimum essence
            if (gameController != null && gameController.CurrentEssence < minimumEssenceToContinue)
            {
                Debug.Log($"[WaveManager] Game Over: Not enough essence ({gameController.CurrentEssence} < {minimumEssenceToContinue})");
                return true;
            }

            // Game over if max retries exceeded
            if (maxRetriesPerWave >= 0 && currentRetryCount >= maxRetriesPerWave)
            {
                Debug.Log($"[WaveManager] Game Over: Max retries exceeded ({currentRetryCount} >= {maxRetriesPerWave})");
                return true;
            }

            return false;
        }

        #endregion

        #region UI Display Methods

        private void ShowWaveSuccessPanel()
        {
            HideAllPanels();

            if (waveSuccessPanel != null)
            {
                waveSuccessPanel.SetActive(true);
            }

            if (waveSuccessText != null)
            {
                waveSuccessText.text = $"Wave {lastCompletedWave} Complete!";
            }
        }

        private void ShowWaveFailedPanel()
        {
            HideAllPanels();

            if (waveFailedPanel != null)
            {
                waveFailedPanel.SetActive(true);
            }

            if (waveFailedText != null)
            {
                int retriesRemaining = maxRetriesPerWave >= 0 ? maxRetriesPerWave - currentRetryCount : -1;
                string retriesText = retriesRemaining >= 0 ? $"\nRetries Remaining: {retriesRemaining}" : "";
                waveFailedText.text = $"Wave {waveSpawner.CurrentWaveNumber} Failed!{retriesText}";
            }
        }

        private void ShowGameOverPanel()
        {
            HideAllPanels();

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            if (gameOverText != null)
            {
                gameOverText.text = $"Game Over!\nCompleted Waves: {lastCompletedWave}";
            }
        }

        private void HideAllPanels()
        {
            if (waveSuccessPanel != null)
            {
                waveSuccessPanel.SetActive(false);
            }

            if (waveFailedPanel != null)
            {
                waveFailedPanel.SetActive(false);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            // Cancel auto-start timer
            waitingForAutoStart = false;
            autoStartTimer = 0f;
        }

        #endregion

        #region Button Handlers

        private void OnNextWaveClicked()
        {
            Debug.Log("[WaveManager] Next Wave button clicked");
            StartNextWave();
        }

        private void OnRetryWaveClicked()
        {
            Debug.Log("[WaveManager] Retry Wave button clicked");
            RetryCurrentWave();
        }

        private void OnRestartGameClicked()
        {
            Debug.Log("[WaveManager] Restart Game button clicked");
            RestartGame();
        }

        private void OnMainMenuClicked()
        {
            Debug.Log("[WaveManager] Main Menu button clicked");
            LoadMainMenu();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the first wave. Call this from menus or debug controls.
        /// </summary>
        public void StartGame()
        {
            Debug.Log("[WaveManager] Starting game from wave 1");

            // Reset game state
            isGameOver = false;
            lastCompletedWave = 0;
            currentRetryCount = 0;

            // Hide all panels
            HideAllPanels();

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
                Debug.LogWarning("[WaveManager] Cannot start next wave: Game is over");
                return;
            }

            HideAllPanels();

            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        /// <summary>
        /// Retries the current wave.
        /// </summary>
        public void RetryCurrentWave()
        {
            if (isGameOver)
            {
                Debug.LogWarning("[WaveManager] Cannot retry: Game is over");
                return;
            }

            currentRetryCount++;

            Debug.Log($"[WaveManager] Retrying wave {waveSpawner.CurrentWaveNumber} (Attempt {currentRetryCount + 1})");

            HideAllPanels();

            if (waveSpawner != null)
            {
                waveSpawner.RetryCurrentWave();
            }
        }

        /// <summary>
        /// Restarts the game from wave 1.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[WaveManager] Restarting game");

            // Invoke restart event
            OnGameRestart?.Invoke();

            // Reload the current scene
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }

        /// <summary>
        /// Loads the main menu scene.
        /// </summary>
        public void LoadMainMenu()
        {
            Debug.Log("[WaveManager] Loading main menu");

            // Try to load "MainMenu" scene, fall back to current scene reload if not found
            try
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            catch
            {
                Debug.LogWarning("[WaveManager] MainMenu scene not found, reloading current scene");
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                );
            }
        }

        /// <summary>
        /// Resets the wave manager state without reloading the scene.
        /// </summary>
        public void ResetState()
        {
            Debug.Log("[WaveManager] Resetting state");

            isGameOver = false;
            lastCompletedWave = 0;
            currentRetryCount = 0;
            waitingForAutoStart = false;
            autoStartTimer = 0f;

            HideAllPanels();
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

        /// <summary>
        /// Gets the current retry count.
        /// </summary>
        public int CurrentRetryCount => currentRetryCount;

        #endregion
    }
}

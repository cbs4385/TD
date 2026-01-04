using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the main game UI including essence display and wave controls.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI Elements")]
        [SerializeField]
        [Tooltip("Text element displaying current essence")]
        private TextMeshProUGUI essenceText;

        [SerializeField]
        [Tooltip("Button to start a new wave")]
        private Button startWaveButton;

        [SerializeField]
        [Tooltip("Text element with placement instructions")]
        private TextMeshProUGUI placementInstructionsText;

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the wave spawner")]
        private WaveSpawner waveSpawner;

        [Header("Canvas Settings")]
        [SerializeField]
        [Tooltip("Reference pixels per unit to ensure sprites render at intended UI scale")]
        private float referencePixelsPerUnit = 32f;

        private bool isSubscribedToEssence;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            TrySubscribeToGameController();
        }

        private void OnDisable()
        {
            if (isSubscribedToEssence && GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= UpdateEssence;
                isSubscribedToEssence = false;
            }
        }

        private void Start()
        {
            // Ensure subscription in case GameController wasn't initialized during OnEnable
            TrySubscribeToGameController();

            // Hook up button click event
            if (startWaveButton != null && waveSpawner != null)
            {
                startWaveButton.onClick.AddListener(OnStartWaveClicked);
            }

            ApplyCanvasSettings();

            // Initialize placement instructions
            if (placementInstructionsText != null)
            {
                placementInstructionsText.text = "Click on paths to place Fae Lanterns (Cost: 20 Essence)";
            }
        }

        private void OnDestroy()
        {
            // Clean up button listener
            if (startWaveButton != null)
            {
                startWaveButton.onClick.RemoveListener(OnStartWaveClicked);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the essence text display.
        /// </summary>
        /// <param name="value">The current essence value to display</param>
        public void UpdateEssence(int value)
        {
            if (essenceText != null)
            {
                essenceText.text = $"Essence: {value}";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Called when the Start Wave button is clicked.
        /// </summary>
        private void OnStartWaveClicked()
        {
            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        private void TrySubscribeToGameController()
        {
            if (GameController.Instance == null)
            {
                return;
            }

            if (!isSubscribedToEssence)
            {
                GameController.Instance.OnEssenceChanged += UpdateEssence;
                isSubscribedToEssence = true;
            }

            UpdateEssence(GameController.Instance.CurrentEssence);
        }

        private void ApplyCanvasSettings()
        {
            ApplyCanvasSettings(GetComponentInParent<Canvas>());
        }

        private void ApplyCanvasSettings(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.referencePixelsPerUnit = referencePixelsPerUnit;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.referencePixelsPerUnit = referencePixelsPerUnit;
            }
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Game Over scene UI and statistics display
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Text displaying max wave reached")]
        private TextMeshProUGUI maxWaveText;

        [SerializeField]
        [Tooltip("Text displaying visitors consumed")]
        private TextMeshProUGUI visitorsConsumedText;

        [SerializeField]
        [Tooltip("Text displaying total time played")]
        private TextMeshProUGUI totalTimeText;

        [SerializeField]
        [Tooltip("Text displaying props placed summary")]
        private TextMeshProUGUI propsPlacedText;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Button to return to main menu")]
        private Button mainMenuButton;

        private SceneLoader sceneLoader;

        private void Awake()
        {
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        private void Start()
        {
            SetupButtons();
            DisplayStatistics();
        }

        private void Update()
        {
            // Check for ESC key
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReturnToMainMenu();
            }
        }

        private void SetupButtons()
        {
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void DisplayStatistics()
        {
            if (GameStatsTracker.Instance == null)
            {
                DisplayDefaultStats();
                return;
            }

            var stats = GameStatsTracker.Instance;

            // Max wave reached
            if (maxWaveText != null)
            {
                maxWaveText.text = $"Max Wave Reached: {stats.MaxWaveReached}";
            }

            // Visitors consumed
            if (visitorsConsumedText != null)
            {
                visitorsConsumedText.text = $"Visitors Consumed: {stats.VisitorsConsumed}";
            }

            // Total time played
            if (totalTimeText != null)
            {
                totalTimeText.text = $"Total Time: {stats.GetFormattedTime()}";
            }

            // Props placed
            if (propsPlacedText != null)
            {
                string propsSummary = stats.GetPropsSummary();
                propsPlacedText.text = $"Props Placed:\n{propsSummary}";
            }
        }

        private void DisplayDefaultStats()
        {
            if (maxWaveText != null)
                maxWaveText.text = "Max Wave Reached: 0";

            if (visitorsConsumedText != null)
                visitorsConsumedText.text = "Visitors Consumed: 0";

            if (totalTimeText != null)
                totalTimeText.text = "Total Time: 00:00";

            if (propsPlacedText != null)
                propsPlacedText.text = "Props Placed:\nNo props placed";
        }

        private void ReturnToMainMenu()
        {

            // Reset stats for next game
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.ResetStats();
            }

            sceneLoader.LoadMainMenu();
        }
    }
}

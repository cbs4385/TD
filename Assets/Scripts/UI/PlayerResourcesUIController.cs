using UnityEngine;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the Player Resources Panel UI.
    /// Displays the player's current resources (Essence, etc.) and updates automatically
    /// via event subscriptions to GameController.
    /// </summary>
    public class PlayerResourcesUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Essence")]
        [SerializeField]
        [Tooltip("Text element displaying the current essence value")]
        private TextMeshProUGUI essenceValueText;

        [Header("Future Resources (optional)")]
        [SerializeField]
        [Tooltip("Text element for Suspicion value (future feature)")]
        private TextMeshProUGUI suspicionValueText;

        [SerializeField]
        [Tooltip("Text element for Wave number (future feature)")]
        private TextMeshProUGUI waveValueText;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // Subscribe to essence change events
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged += HandleEssenceChanged;

                // Initialize display with current value
                HandleEssenceChanged(GameController.Instance.CurrentEssence);
            }
            else
            {
                Debug.LogWarning("PlayerResourcesUIController: GameController.Instance is null in OnEnable");
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from essence change events
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= HandleEssenceChanged;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when the essence value changes in GameController.
        /// </summary>
        /// <param name="newValue">The new essence value</param>
        private void HandleEssenceChanged(int newValue)
        {
            if (essenceValueText != null)
            {
                essenceValueText.text = newValue.ToString();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the Suspicion value display (future feature).
        /// </summary>
        /// <param name="value">Suspicion value to display</param>
        public void SetSuspicion(int value)
        {
            if (suspicionValueText != null)
            {
                suspicionValueText.text = value.ToString();
            }
        }

        /// <summary>
        /// Sets the Wave number display (future feature).
        /// </summary>
        /// <param name="value">Wave number to display</param>
        public void SetWave(int value)
        {
            if (waveValueText != null)
            {
                waveValueText.text = value.ToString();
            }
        }

        #endregion
    }
}

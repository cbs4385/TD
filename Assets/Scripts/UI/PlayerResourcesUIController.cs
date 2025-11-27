using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the Player Resources Panel UI.
    /// Displays the player's current resources (Essence, etc.) and updates automatically
    /// via event subscriptions to GameController.
    /// Creates its own UI at runtime if not manually configured.
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

        [Header("Runtime UI Settings")]
        [SerializeField]
        [Tooltip("Auto-create UI at runtime if essence text is not assigned")]
        private bool autoCreateUI = true;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-create UI if needed
            if (autoCreateUI && essenceValueText == null)
            {
                CreateResourcesPanelUI();
            }
        }

        private void OnEnable()
        {
            // Subscribe to essence change events
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged += HandleEssenceChanged;

                // Initialize display with current value
                HandleEssenceChanged(GameController.Instance.CurrentEssence);
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

        #region Private Methods

        /// <summary>
        /// Creates the Resources Panel UI at runtime.
        /// </summary>
        private void CreateResourcesPanelUI()
        {
            // Find or create Canvas
            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas");
                canvasObject.transform.SetParent(transform, false);

                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObject.AddComponent<GraphicRaycaster>();
            }

            // Create Resources Panel
            var panelObject = new GameObject("ResourcesPanel");
            panelObject.transform.SetParent(canvas.transform, false);

            var panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1); // Top-left
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(220, 60);

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black

            // Create Essence Row with Horizontal Layout
            var rowObject = new GameObject("EssenceRow");
            rowObject.transform.SetParent(panelObject.transform, false);

            var rowRect = rowObject.AddComponent<RectTransform>();
            rowRect.anchorMin = Vector2.zero;
            rowRect.anchorMax = Vector2.one;
            rowRect.offsetMin = new Vector2(10, 10);
            rowRect.offsetMax = new Vector2(-10, -10);

            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // Create Essence Label
            var labelObject = new GameObject("EssenceLabel");
            labelObject.transform.SetParent(rowObject.transform, false);

            var labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.text = "Essence:";
            labelText.fontSize = 20;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;

            var labelLayoutElement = labelObject.AddComponent<LayoutElement>();
            labelLayoutElement.preferredWidth = 90;

            // Create Essence Value
            var valueObject = new GameObject("EssenceValue");
            valueObject.transform.SetParent(rowObject.transform, false);

            essenceValueText = valueObject.AddComponent<TextMeshProUGUI>();
            essenceValueText.text = "0";
            essenceValueText.fontSize = 20;
            essenceValueText.fontStyle = FontStyles.Bold;
            essenceValueText.color = new Color(1f, 0.84f, 0f); // Gold
            essenceValueText.alignment = TextAlignmentOptions.MidlineLeft;

            var valueLayoutElement = valueObject.AddComponent<LayoutElement>();
            valueLayoutElement.preferredWidth = 60;

            Debug.Log("PlayerResourcesUIController: Created Resources Panel UI at runtime");
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Props;
using FaeMaze.Systems;
using UnityEngine.TextCore.Text;
using FontStyles = TMPro.FontStyles;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the Build Panel UI for selecting placeable items.
    /// Shows buttons for each item type and updates selection when clicked.
    /// Automatically creates the UI if not manually set up.
    /// </summary>
    public class PlacementUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References (Optional - will auto-create if null)")]
        [SerializeField]
        [Tooltip("Build panel GameObject")]
        private GameObject buildPanel;

        [SerializeField]
        [Tooltip("Button for selecting FaeLantern")]
        private Button lanternButton;

        [SerializeField]
        [Tooltip("Button for selecting FairyRing")]
        private Button fairyRingButton;

        [SerializeField]
        [Tooltip("Button for selecting WillowTheWisp")]
        private Button willowWispButton;

        [SerializeField]
        [Tooltip("Text showing lantern essence cost")]
        private TextMeshProUGUI lanternCostText;

        [SerializeField]
        [Tooltip("Text showing fairy ring essence cost")]
        private TextMeshProUGUI fairyRingCostText;

        [SerializeField]
        [Tooltip("Text showing willow wisp essence cost")]
        private TextMeshProUGUI willowWispCostText;

        [Header("Tooltip")]
        [SerializeField]
        [Tooltip("Tooltip panel GameObject")]
        private GameObject tooltipPanel;

        [SerializeField]
        [Tooltip("Text showing tooltip title")]
        private TextMeshProUGUI tooltipTitleText;

        [SerializeField]
        [Tooltip("Text showing tooltip body (description and cost)")]
        private TextMeshProUGUI tooltipBodyText;

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the prop placement controller")]
        private PropPlacementController propPlacementController;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color for selected button")]
        private Color selectedColor = Color.white;

        [SerializeField]
        [Tooltip("Color for unselected button")]
        private Color deselectedColor = new Color(1f, 1f, 1f, 0.6f);

        #endregion

        #region Private Fields

        private string currentSelectionId;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-find PropPlacementController if not assigned
            if (propPlacementController == null)
            {
                propPlacementController = FindFirstObjectByType<PropPlacementController>();
                if (propPlacementController == null)
                {
                    return;
                }
            }

            // Auto-create build panel if not assigned
            if (buildPanel == null)
            {
                CreateBuildPanelUI();
            }

            // Initialize UI controls
            InitializeControls();
        }

        private void Update()
        {
            // Poll essence and update button states every frame
            UpdateButtonStates();
        }

        /// <summary>
        /// Initializes all UI controls with listeners and default values.
        /// </summary>
        private void InitializeControls()
        {
            // Hide tooltip on startup
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }

            // Add button listeners
            if (lanternButton != null)
            {
                lanternButton.onClick.AddListener(OnLanternButtonClicked);
            }

            if (fairyRingButton != null)
            {
                fairyRingButton.onClick.AddListener(OnFairyRingButtonClicked);
            }

            if (willowWispButton != null)
            {
                willowWispButton.onClick.AddListener(OnWillowWispButtonClicked);
            }

            // Refresh cost labels from placeable items data
            RefreshCostsFromPlaceableItems();

            // Set initial selection visual (match PropPlacementController's default)
            var currentSelection = propPlacementController.GetCurrentSelection();
            if (currentSelection != null)
            {
                UpdateSelectionVisual(currentSelection.id);
            }
            else
            {
                // Default to FaeLantern if nothing selected
                UpdateSelectionVisual("FaeLantern");
            }
        }

        /// <summary>
        /// Updates button interactability based on current Essence.
        /// Buttons are enabled only if the player can afford the item.
        /// </summary>
        private void UpdateButtonStates()
        {
            // Check if GameController is available
            if (GameController.Instance == null)
            {
                return;
            }

            int essence = GameController.Instance.CurrentEssence;

            // Update lantern button
            var lanternItem = propPlacementController?.GetItemById("FaeLantern");
            if (lanternItem != null && lanternButton != null)
            {
                lanternButton.interactable = essence >= lanternItem.essenceCost;
            }

            // Update fairy ring button
            var ringItem = propPlacementController?.GetItemById("FairyRing");
            if (ringItem != null && fairyRingButton != null)
            {
                fairyRingButton.interactable = essence >= ringItem.essenceCost;
            }

            // Update willow wisp button
            var wispItem = propPlacementController?.GetItemById("WillowTheWisp");
            if (wispItem != null && willowWispButton != null)
            {
                willowWispButton.interactable = essence >= wispItem.essenceCost;
            }
        }

        private void OnDestroy()
        {
            // Clean up button listeners
            if (lanternButton != null)
            {
                lanternButton.onClick.RemoveListener(OnLanternButtonClicked);
            }

            if (fairyRingButton != null)
            {
                fairyRingButton.onClick.RemoveListener(OnFairyRingButtonClicked);
            }

            if (willowWispButton != null)
            {
                willowWispButton.onClick.RemoveListener(OnWillowWispButtonClicked);
            }
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Called when the Lantern button is clicked.
        /// </summary>
        private void OnLanternButtonClicked()
        {
            if (propPlacementController != null)
            {
                propPlacementController.SelectItemById("FaeLantern");
                UpdateSelectionVisual("FaeLantern");
            }
        }

        /// <summary>
        /// Called when the FairyRing button is clicked.
        /// </summary>
        private void OnFairyRingButtonClicked()
        {
            if (propPlacementController != null)
            {
                propPlacementController.SelectItemById("FairyRing");
                UpdateSelectionVisual("FairyRing");
            }
        }

        /// <summary>
        /// Called when the WillowWisp button is clicked.
        /// </summary>
        private void OnWillowWispButtonClicked()
        {
            if (propPlacementController != null)
            {
                propPlacementController.SelectItemById("WillowTheWisp");
                UpdateSelectionVisual("WillowTheWisp");
            }
        }

        #endregion

        #region UI Auto-Creation

        /// <summary>
        /// Automatically creates the build panel UI hierarchy.
        /// </summary>
        private void CreateBuildPanelUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            // Create the build panel
            buildPanel = CreatePanel(canvas.transform);

            // Create title
            CreateTitle(buildPanel.transform);

            // Create buttons and labels
            float yPos = -50f;

            // Lantern button
            lanternButton = CreateButton(buildPanel.transform, "FaeLantern", yPos);
            yPos -= 35f;

            lanternCostText = CreateLabel(buildPanel.transform, "20 Essence", yPos);
            yPos -= 60f;

            // Fairy Ring button
            fairyRingButton = CreateButton(buildPanel.transform, "FairyRing", yPos);
            yPos -= 35f;

            fairyRingCostText = CreateLabel(buildPanel.transform, "15 Essence", yPos);
            yPos -= 60f;

            // Willow Wisp button
            willowWispButton = CreateButton(buildPanel.transform, "Willow-the-Wisp", yPos);
            yPos -= 35f;

            willowWispCostText = CreateLabel(buildPanel.transform, "50 Essence", yPos);

        }

        /// <summary>
        /// Creates a Canvas for the UI.
        /// </summary>
        private Canvas CreateCanvas()
        {
            GameObject canvasObj = new GameObject("BuildCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Creates the main panel background.
        /// </summary>
        private GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("BuildPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(10f, 10f);
            rect.sizeDelta = new Vector2(200f, 370f); // Increased height for third button

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            return panel;
        }

        /// <summary>
        /// Creates a title text at the top of the panel.
        /// </summary>
        private void CreateTitle(Transform parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);

            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(180f, 30f);

            TextMeshProUGUI text = titleObj.AddComponent<TextMeshProUGUI>();
            text.text = "BUILD";
            text.fontSize = 18;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        /// <summary>
        /// Creates a button control.
        /// </summary>
        private Button CreateButton(Transform parent, string buttonText, float yPos)
        {
            GameObject buttonObj = new GameObject("Button_" + buttonText);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(180f, 30f);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.5f, 0.8f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Create button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = buttonText;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        /// <summary>
        /// Creates a text label.
        /// </summary>
        private TextMeshProUGUI CreateLabel(Transform parent, string labelText, float yPos)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(180f, 25f);

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            return text;
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// Updates the visual state of buttons to show which item is selected.
        /// </summary>
        /// <param name="id">ID of the selected item</param>
        private void UpdateSelectionVisual(string id)
        {
            currentSelectionId = id;

            // Update lantern button
            if (lanternButton != null)
            {
                var lanternColors = lanternButton.colors;
                lanternColors.normalColor = (id == "FaeLantern") ? selectedColor : deselectedColor;
                lanternColors.highlightedColor = (id == "FaeLantern") ? selectedColor : deselectedColor;
                lanternButton.colors = lanternColors;
            }

            // Update fairy ring button
            if (fairyRingButton != null)
            {
                var ringColors = fairyRingButton.colors;
                ringColors.normalColor = (id == "FairyRing") ? selectedColor : deselectedColor;
                ringColors.highlightedColor = (id == "FairyRing") ? selectedColor : deselectedColor;
                fairyRingButton.colors = ringColors;
            }

            // Update willow wisp button
            if (willowWispButton != null)
            {
                var wispColors = willowWispButton.colors;
                wispColors.normalColor = (id == "WillowTheWisp") ? selectedColor : deselectedColor;
                wispColors.highlightedColor = (id == "WillowTheWisp") ? selectedColor : deselectedColor;
                willowWispButton.colors = wispColors;
            }
        }

        /// <summary>
        /// Refreshes the cost labels by querying PropPlacementController's placeable items.
        /// </summary>
        private void RefreshCostsFromPlaceableItems()
        {
            if (propPlacementController == null)
            {
                return;
            }

            // Get lantern item and update cost
            var lanternItem = propPlacementController.GetItemById("FaeLantern");
            if (lanternItem != null && lanternCostText != null)
            {
                lanternCostText.text = $"{lanternItem.essenceCost} Essence";
            }
            else if (lanternCostText != null)
            {
                lanternCostText.text = "? Essence";
            }

            // Get fairy ring item and update cost
            var ringItem = propPlacementController.GetItemById("FairyRing");
            if (ringItem != null && fairyRingCostText != null)
            {
                fairyRingCostText.text = $"{ringItem.essenceCost} Essence";
            }
            else if (fairyRingCostText != null)
            {
                fairyRingCostText.text = "? Essence";
            }

            // Get willow wisp item and update cost
            var wispItem = propPlacementController.GetItemById("WillowTheWisp");
            if (wispItem != null && willowWispCostText != null)
            {
                willowWispCostText.text = $"{wispItem.essenceCost} Essence";
            }
            else if (willowWispCostText != null)
            {
                willowWispCostText.text = "? Essence";
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually refresh cost labels (useful if essence costs change at runtime).
        /// </summary>
        public void RefreshCostLabels()
        {
            RefreshCostsFromPlaceableItems();
        }

        /// <summary>
        /// Gets the currently selected item ID.
        /// </summary>
        /// <returns>ID of the current selection</returns>
        public string GetCurrentSelectionId()
        {
            return currentSelectionId;
        }

        #endregion

        #region Tooltip Management

        /// <summary>
        /// Shows the tooltip for a specific item at the given screen position.
        /// </summary>
        /// <param name="id">Item ID to show tooltip for</param>
        /// <param name="screenPosition">Screen position for tooltip (usually near cursor)</param>
        public void ShowTooltipForItem(string id, Vector2 screenPosition)
        {
            var item = propPlacementController.GetPlaceableItemById(id);
            if (item == null)
            {
                return;
            }

            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(true);

                // Update title text
                if (tooltipTitleText != null)
                {
                    tooltipTitleText.text = item.displayName;
                }

                // Update body text with description and cost
                if (tooltipBodyText != null)
                {
                    string descriptionText = !string.IsNullOrEmpty(item.description)
                        ? item.description
                        : "No description available.";
                    tooltipBodyText.text = $"{descriptionText}\n\nCost: {item.essenceCost} Essence";
                }

                // Position tooltip near cursor
                var panelRect = (RectTransform)tooltipPanel.transform;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)tooltipPanel.transform.parent,
                    screenPosition,
                    null,
                    out localPos
                );
                panelRect.anchoredPosition = localPos + new Vector2(10f, 10f);
            }
        }

        /// <summary>
        /// Hides the tooltip panel.
        /// </summary>
        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }

        #endregion
    }
}

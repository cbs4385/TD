using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Props;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the Build Panel UI for selecting placeable items.
    /// Shows buttons for each item type and updates selection when clicked.
    /// </summary>
    public class PlacementUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Item Buttons")]
        [SerializeField]
        [Tooltip("Button for selecting FaeLantern")]
        private Button lanternButton;

        [SerializeField]
        [Tooltip("Button for selecting FairyRing")]
        private Button fairyRingButton;

        [Header("Cost Labels")]
        [SerializeField]
        [Tooltip("Text showing lantern essence cost")]
        private TextMeshProUGUI lanternCostText;

        [SerializeField]
        [Tooltip("Text showing fairy ring essence cost")]
        private TextMeshProUGUI fairyRingCostText;

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
            // Validate references
            if (propPlacementController == null)
            {
                propPlacementController = FindFirstObjectByType<PropPlacementController>();
                if (propPlacementController != null)
                {
                    Debug.Log("PlacementUIController: Found PropPlacementController");
                }
                else
                {
                    Debug.LogError("PlacementUIController: PropPlacementController not found!");
                    return;
                }
            }

            // Add button listeners
            if (lanternButton != null)
            {
                lanternButton.onClick.AddListener(OnLanternButtonClicked);
            }
            else
            {
                Debug.LogWarning("PlacementUIController: Lantern button not assigned!");
            }

            if (fairyRingButton != null)
            {
                fairyRingButton.onClick.AddListener(OnFairyRingButtonClicked);
            }
            else
            {
                Debug.LogWarning("PlacementUIController: FairyRing button not assigned!");
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
                Debug.Log("PlacementUIController: Selected FaeLantern");
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
                Debug.Log("PlacementUIController: Selected FairyRing");
            }
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
        }

        /// <summary>
        /// Refreshes the cost labels by querying PropPlacementController's placeable items.
        /// </summary>
        private void RefreshCostsFromPlaceableItems()
        {
            if (propPlacementController == null)
            {
                Debug.LogWarning("PlacementUIController: Cannot refresh costs - PropPlacementController is null!");
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
    }
}

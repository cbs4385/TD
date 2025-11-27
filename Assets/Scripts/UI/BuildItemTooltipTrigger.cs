using UnityEngine;
using UnityEngine.EventSystems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Helper component for triggering tooltips when hovering over build item buttons.
    /// Attach to each button that should show a tooltip.
    /// </summary>
    public class BuildItemTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Serialized Fields

        [SerializeField]
        [Tooltip("Unique identifier for this item (e.g., 'FaeLantern' or 'FairyRing')")]
        private string itemId;

        [SerializeField]
        [Tooltip("Reference to the PlacementUIController")]
        private PlacementUIController placementUIController;

        #endregion

        #region IPointerEnterHandler Implementation

        /// <summary>
        /// Called when the pointer enters the button area.
        /// Shows the tooltip for this item.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (placementUIController != null && !string.IsNullOrEmpty(itemId))
            {
                placementUIController.ShowTooltipForItem(itemId, eventData.position);
            }
        }

        #endregion

        #region IPointerExitHandler Implementation

        /// <summary>
        /// Called when the pointer exits the button area.
        /// Hides the tooltip.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (placementUIController != null)
            {
                placementUIController.HideTooltip();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the item ID for this trigger (useful for dynamic setup).
        /// </summary>
        /// <param name="id">Item ID to set</param>
        public void SetItemId(string id)
        {
            itemId = id;
        }

        /// <summary>
        /// Sets the PlacementUIController reference (useful for dynamic setup).
        /// </summary>
        /// <param name="controller">Controller reference</param>
        public void SetPlacementUIController(PlacementUIController controller)
        {
            placementUIController = controller;
        }

        #endregion
    }
}

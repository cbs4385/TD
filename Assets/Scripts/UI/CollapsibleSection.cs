using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI component for collapsible/expandable sections in menus.
    /// Manages its own LayoutElement preferred height so parent VerticalLayoutGroups
    /// correctly reposition siblings when content is shown/hidden.
    /// </summary>
    [RequireComponent(typeof(LayoutElement))]
    public class CollapsibleSection : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button headerButton;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI arrowText;

        [Header("Settings")]
        [SerializeField] private bool startExpanded = false;

        private bool isExpanded;
        private LayoutElement layoutElement;
        private RectTransform headerRect;
        private RectTransform contentRect;

        private void Awake()
        {
            layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();

            if (headerButton != null)
            {
                headerButton.onClick.AddListener(ToggleExpanded);
                headerRect = headerButton.GetComponent<RectTransform>();
            }

            if (contentPanel != null)
                contentRect = contentPanel.GetComponent<RectTransform>();

            SetExpanded(startExpanded, immediate: true);
        }

        public void ToggleExpanded()
        {
            SetExpanded(!isExpanded);
        }

        public void SetExpanded(bool expanded, bool immediate = false)
        {
            isExpanded = expanded;

            if (contentPanel != null)
            {
                contentPanel.SetActive(isExpanded);
            }

            if (arrowText != null)
            {
                // Use ASCII characters that all fonts support (▼/▶ may not render in LiberationSans)
                arrowText.text = isExpanded ? "v" : ">";
            }

            UpdatePreferredHeight();
        }

        /// <summary>
        /// Calculates the correct height for this section and sets it on the LayoutElement.
        /// When collapsed, height = header only. When expanded, height = header + spacing + content.
        /// Then forces a layout rebuild up the hierarchy.
        /// </summary>
        private void UpdatePreferredHeight()
        {
            float headerHeight = 55f; // Header height (50px) + section VLG spacing (5px)
            if (headerRect != null)
                headerHeight = headerRect.sizeDelta.y + 5f;

            if (!isExpanded || contentRect == null)
            {
                // Collapsed: just the header
                if (layoutElement != null)
                    layoutElement.preferredHeight = headerHeight;
            }
            else
            {
                // Expanded: rebuild content first so its ContentSizeFitter updates
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
                float contentHeight = LayoutUtility.GetPreferredHeight(contentRect);
                if (layoutElement != null)
                    layoutElement.preferredHeight = headerHeight + contentHeight;
            }

            // Rebuild parent layouts upward so they reposition siblings
            Transform current = transform.parent;
            while (current != null)
            {
                RectTransform rect = current as RectTransform;
                if (rect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                }
                current = current.parent;
            }
        }

        public void SetHeaderText(string text)
        {
            if (headerText != null)
            {
                headerText.text = text;
            }
        }
    }
}

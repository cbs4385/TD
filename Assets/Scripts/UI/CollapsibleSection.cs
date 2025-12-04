using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI component for collapsible/expandable sections in menus
    /// </summary>
    public class CollapsibleSection : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button headerButton;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI arrowText;

        [Header("Settings")]
        [SerializeField] private bool startExpanded = true;

        private bool isExpanded;

        private void Awake()
        {
            if (headerButton != null)
            {
                headerButton.onClick.AddListener(ToggleExpanded);
            }

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
                arrowText.text = isExpanded ? "▼" : "▶";
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

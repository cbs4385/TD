using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the essence bar UI at the top of the screen.
    /// The bar auto-scales its maximum to the highest essence value the player has achieved.
    /// </summary>
    public class EssenceBarController : MonoBehaviour
    {
        #region Private Fields

        private GameObject essenceBarPanel;
        private Slider essenceSlider;
        private Image fillImage;
        private TextMeshProUGUI essenceText;
        private TextMeshProUGUI essenceTextShadow;

        // Track the maximum essence achieved for auto-scaling
        private int maxEssenceAchieved;
        private int startingEssence;

        // Colors - candy red health bar style
        private readonly Color fillColorBright = new Color(0.95f, 0.2f, 0.2f, 1f); // Bright candy red
        private readonly Color fillColorDark = new Color(0.6f, 0.05f, 0.05f, 1f); // Dark red for gradient
        private readonly Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f); // Dark gray background
        private readonly Color borderColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Gray border

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Get starting essence - use GameController's current value as the baseline
            if (GameController.Instance != null)
            {
                startingEssence = GameController.Instance.CurrentEssence;
                maxEssenceAchieved = startingEssence;
            }
            else
            {
                // Fallback to GameSettings if GameController not ready
                startingEssence = GameSettings.StartingEssence;
                maxEssenceAchieved = startingEssence;
            }

            CreateEssenceBarUI();
            UpdateDisplay();
        }

        private void OnEnable()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        private void OnDisable()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }
        }

        private void LateUpdate()
        {
            // Ensure we're subscribed even if GameController wasn't ready at OnEnable
            if (GameController.Instance != null && essenceSlider != null)
            {
                // Re-subscribe if needed (handles late initialization)
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Creates or finds the essence bar UI at the top of the screen.
        /// Uses the existing Canvas (GameRoot > Canvas) for proper hierarchy.
        /// </summary>
        private void CreateEssenceBarUI()
        {
            // Find existing canvas (should be GameRoot > Canvas)
            Canvas canvas = UIFactory.FindOrCreateCanvas(this, "EssenceBarCanvas", 100, new Vector2(1920, 1080));

            // Check if EssenceBarPanel already exists (avoid duplicates)
            Transform existingPanel = canvas.transform.Find("EssenceBarPanel");
            if (existingPanel != null)
            {
                essenceBarPanel = existingPanel.gameObject;
                // Find existing components
                essenceSlider = existingPanel.GetComponentInChildren<Slider>();
                essenceText = existingPanel.GetComponentInChildren<TextMeshProUGUI>();
                if (essenceSlider != null)
                {
                    fillImage = essenceSlider.fillRect?.GetComponent<Image>();
                }
                return;
            }

            // Create the panel at top center, half screen width
            essenceBarPanel = new GameObject("EssenceBarPanel");
            essenceBarPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = essenceBarPanel.AddComponent<RectTransform>();
            // Position at top center, taking half the screen width
            panelRect.anchorMin = new Vector2(0.25f, 1f); // Start at 25% from left
            panelRect.anchorMax = new Vector2(0.75f, 1f); // End at 75% from left (50% width)
            panelRect.pivot = new Vector2(0.5f, 1f); // Pivot at top center
            panelRect.anchoredPosition = new Vector2(0f, -10f); // 10px from top
            panelRect.sizeDelta = new Vector2(0f, 32f); // Height 32px, width from anchors

            // Outer border (dark gray frame)
            Image borderImage = essenceBarPanel.AddComponent<Image>();
            borderImage.color = borderColor;

            // Inner background container (provides the dark empty area)
            GameObject bgContainer = new GameObject("BackgroundContainer");
            bgContainer.transform.SetParent(essenceBarPanel.transform, false);

            RectTransform bgContainerRect = bgContainer.AddComponent<RectTransform>();
            bgContainerRect.anchorMin = Vector2.zero;
            bgContainerRect.anchorMax = Vector2.one;
            bgContainerRect.offsetMin = new Vector2(3f, 3f); // 3px border
            bgContainerRect.offsetMax = new Vector2(-3f, -3f);

            Image bgContainerImage = bgContainer.AddComponent<Image>();
            bgContainerImage.color = backgroundColor;

            // Create the slider (inside the background)
            GameObject sliderObj = new GameObject("EssenceSlider");
            sliderObj.transform.SetParent(bgContainer.transform, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = new Vector2(2f, 2f); // Small inner padding
            sliderRect.offsetMax = new Vector2(-2f, -2f);

            essenceSlider = sliderObj.AddComponent<Slider>();
            essenceSlider.minValue = 0f;
            essenceSlider.maxValue = maxEssenceAchieved;
            essenceSlider.value = 0f;
            essenceSlider.interactable = false;
            essenceSlider.direction = Slider.Direction.LeftToRight;

            // Create fill area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);

            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            UIFactory.StretchToFillParent(fillAreaRect);

            // Create the main fill (bright candy red)
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            UIFactory.StretchToFillParent(fillRect);

            fillImage = fillObj.AddComponent<Image>();
            fillImage.color = fillColorBright;

            essenceSlider.fillRect = fillRect;
            essenceSlider.targetGraphic = fillImage;

            // Create highlight overlay (top bright strip for candy effect)
            GameObject highlightObj = new GameObject("Highlight");
            highlightObj.transform.SetParent(fillObj.transform, false);

            RectTransform highlightRect = highlightObj.AddComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0f, 0.6f); // Top 40% of the bar
            highlightRect.anchorMax = new Vector2(1f, 1f);
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;

            Image highlightImage = highlightObj.AddComponent<Image>();
            highlightImage.color = new Color(1f, 0.5f, 0.5f, 0.5f); // Light red semi-transparent highlight

            // Create shadow overlay (bottom dark strip for depth)
            GameObject shadowObj = new GameObject("Shadow");
            shadowObj.transform.SetParent(fillObj.transform, false);

            RectTransform shadowRect = shadowObj.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0f, 0f); // Bottom 30% of the bar
            shadowRect.anchorMax = new Vector2(1f, 0.3f);
            shadowRect.offsetMin = Vector2.zero;
            shadowRect.offsetMax = Vector2.zero;

            Image shadowImage = shadowObj.AddComponent<Image>();
            shadowImage.color = new Color(0f, 0f, 0f, 0.3f); // Semi-transparent black shadow

            // Create essence text overlay (centered on the bar, with shadow for readability)
            GameObject textShadowObj = new GameObject("EssenceTextShadow");
            textShadowObj.transform.SetParent(essenceBarPanel.transform, false);

            RectTransform textShadowRect = textShadowObj.AddComponent<RectTransform>();
            textShadowRect.anchorMin = Vector2.zero;
            textShadowRect.anchorMax = Vector2.one;
            textShadowRect.offsetMin = new Vector2(1f, -1f); // Offset for shadow effect
            textShadowRect.offsetMax = new Vector2(1f, -1f);

            TextMeshProUGUI shadowText = textShadowObj.AddComponent<TextMeshProUGUI>();
            shadowText.text = "0";
            shadowText.fontSize = 16;
            shadowText.fontStyle = TMPro.FontStyles.Bold;
            shadowText.alignment = TextAlignmentOptions.Center;
            shadowText.color = new Color(0f, 0f, 0f, 0.7f); // Dark shadow
            shadowText.raycastTarget = false;

            GameObject textObj = new GameObject("EssenceText");
            textObj.transform.SetParent(essenceBarPanel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            UIFactory.StretchToFillParent(textRect);

            essenceText = textObj.AddComponent<TextMeshProUGUI>();
            essenceText.text = "0";
            essenceText.fontSize = 16;
            essenceText.fontStyle = TMPro.FontStyles.Bold;
            essenceText.alignment = TextAlignmentOptions.Center;
            essenceText.color = Color.white;
            essenceText.raycastTarget = false;

            // Store reference to shadow text for syncing
            essenceTextShadow = shadowText;
        }


        #endregion

        #region Event Handlers

        /// <summary>
        /// Called when essence changes.
        /// </summary>
        private void OnEssenceChanged(int newEssence)
        {
            // Update max essence if we've exceeded it
            if (newEssence > maxEssenceAchieved)
            {
                maxEssenceAchieved = newEssence;
                if (essenceSlider != null)
                {
                    essenceSlider.maxValue = maxEssenceAchieved;
                }
            }

            UpdateDisplay();
        }

        #endregion

        #region Display Updates

        /// <summary>
        /// Updates the essence bar display.
        /// </summary>
        private void UpdateDisplay()
        {
            if (GameController.Instance == null) return;

            int currentEssence = GameController.Instance.CurrentEssence;

            if (essenceSlider != null)
            {
                essenceSlider.value = currentEssence;
            }

            if (essenceText != null)
            {
                essenceText.text = currentEssence.ToString();
            }

            // Keep shadow text in sync
            if (essenceTextShadow != null)
            {
                essenceTextShadow.text = currentEssence.ToString();
            }
        }

        #endregion
    }
}

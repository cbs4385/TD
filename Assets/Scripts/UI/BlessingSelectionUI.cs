using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI for selecting a blessing at the start of a run.
    /// Displays 3 random unlocked blessings for the player to choose from.
    /// </summary>
    public class BlessingSelectionUI : MonoBehaviour
    {
        #region Private Fields

        private Canvas _canvas;
        private GameObject _panel;
        private List<GameObject> _blessingCards = new List<GameObject>();
        private List<BlessingDefinition> _currentChoices = new List<BlessingDefinition>();

        private bool _isVisible;
        private System.Action<BlessingDefinition> _onSelectionComplete;

        // Colors
        private readonly Color _panelColor = new Color(0.1f, 0.05f, 0.15f, 0.95f);
        private readonly Color _cardColor = new Color(0.15f, 0.1f, 0.2f, 1f);
        private readonly Color _cardHoverColor = new Color(0.25f, 0.15f, 0.35f, 1f);
        private readonly Color _titleColor = new Color(0.7f, 0.5f, 1f);
        private readonly Color _descColor = new Color(0.8f, 0.8f, 0.9f);

        #endregion

        #region Public Properties

        public bool IsVisible => _isVisible;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            CreateCanvas();
            CreateUI();
            Hide();
        }

        #endregion

        #region UI Creation

        private void CreateCanvas()
        {
            // Find existing canvas or create new one
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindFirstObjectByType<Canvas>();
            }
            if (_canvas == null)
            {
                GameObject canvasObj = new GameObject("BlessingSelectionCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 200; // Above other UI
                canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        private void CreateUI()
        {
            // Main panel (full screen overlay)
            _panel = new GameObject("BlessingSelectionPanel");
            _panel.transform.SetParent(_canvas.transform, false);

            RectTransform panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelBg = _panel.AddComponent<Image>();
            panelBg.color = _panelColor;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.8f);
            titleRect.anchorMax = new Vector2(0.9f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Choose Your Blessing";
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = _titleColor;

            // Subtitle
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(_panel.transform, false);

            RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.1f, 0.72f);
            subtitleRect.anchorMax = new Vector2(0.9f, 0.8f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.text = "Select a blessing to empower your run, or skip to start without one";
            subtitleText.fontSize = 20;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.6f, 0.6f, 0.7f);

            // Cards container
            GameObject cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(_panel.transform, false);

            RectTransform cardsRect = cardsContainer.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.05f, 0.2f);
            cardsRect.anchorMax = new Vector2(0.95f, 0.7f);
            cardsRect.offsetMin = Vector2.zero;
            cardsRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Skip button
            CreateSkipButton();
        }

        private void CreateSkipButton()
        {
            GameObject skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(_panel.transform, false);

            RectTransform skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.4f, 0.05f);
            skipRect.anchorMax = new Vector2(0.6f, 0.12f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;

            Image skipBg = skipObj.AddComponent<Image>();
            skipBg.color = new Color(0.3f, 0.2f, 0.2f);

            Button skipBtn = skipObj.AddComponent<Button>();
            skipBtn.onClick.AddListener(OnSkipClicked);

            // Add hover effect
            ColorBlock colors = skipBtn.colors;
            colors.normalColor = new Color(0.3f, 0.2f, 0.2f);
            colors.highlightedColor = new Color(0.4f, 0.25f, 0.25f);
            colors.pressedColor = new Color(0.25f, 0.15f, 0.15f);
            skipBtn.colors = colors;

            // Skip text
            GameObject skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);

            RectTransform textRect = skipTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
            skipText.text = "Skip (No Blessing)";
            skipText.fontSize = 24;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.color = new Color(0.7f, 0.5f, 0.5f);
        }

        private GameObject CreateBlessingCard(BlessingDefinition blessing, int index, Transform parent)
        {
            GameObject cardObj = new GameObject($"BlessingCard_{index}");
            cardObj.transform.SetParent(parent, false);

            // Card background
            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = _cardColor;

            // Layout element for proper sizing
            LayoutElement layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1;
            layoutElement.flexibleHeight = 1;

            // Button for selection
            Button cardBtn = cardObj.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => OnBlessingSelected(blessing));

            ColorBlock colors = cardBtn.colors;
            colors.normalColor = _cardColor;
            colors.highlightedColor = _cardHoverColor;
            colors.pressedColor = new Color(0.2f, 0.15f, 0.25f);
            cardBtn.colors = colors;

            // Card content container
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(cardObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.05f, 0.05f);
            contentRect.anchorMax = new Vector2(0.95f, 0.95f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10;
            contentLayout.padding = new RectOffset(10, 10, 15, 15);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // Blessing name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = blessing.DisplayName;
            nameText.fontSize = 28;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = _titleColor;

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 40;

            // Description
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = blessing.Description;
            descText.fontSize = 18;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = _descColor;
            descText.textWrappingMode = TextWrappingModes.Normal;

            LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
            descLayout.flexibleHeight = 1;

            // Effect summary (optional - shows key stats)
            string effectSummary = GetEffectSummary(blessing);
            if (!string.IsNullOrEmpty(effectSummary))
            {
                GameObject effectObj = new GameObject("Effects");
                effectObj.transform.SetParent(contentObj.transform, false);

                TextMeshProUGUI effectText = effectObj.AddComponent<TextMeshProUGUI>();
                effectText.text = effectSummary;
                effectText.fontSize = 14;
                effectText.alignment = TextAlignmentOptions.Center;
                effectText.color = new Color(0.5f, 0.8f, 0.5f);
                effectText.textWrappingMode = TextWrappingModes.Normal;

                LayoutElement effectLayout = effectObj.AddComponent<LayoutElement>();
                effectLayout.preferredHeight = 50;
            }

            return cardObj;
        }

        private string GetEffectSummary(BlessingDefinition blessing)
        {
            var effects = new List<string>();

            if (blessing.AffectsConsumptionEssence)
            {
                float pct = (blessing.EssenceFromConsumptionMultiplier - 1f) * 100f;
                effects.Add($"Consumption: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (blessing.AffectsStartingEssence)
            {
                float pct = (blessing.StartingEssenceMultiplier - 1f) * 100f;
                effects.Add($"Starting: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (blessing.AffectsVisitorSpeed)
            {
                float pct = (blessing.VisitorSpeedMultiplier - 1f) * 100f;
                effects.Add($"Visitor Speed: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (blessing.AffectsSpawnRate)
            {
                float pct = (1f - blessing.SpawnIntervalMultiplier) * 100f; // Inverted: lower interval = faster spawns
                effects.Add($"Spawn Rate: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            return string.Join("\n", effects);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the blessing selection UI with random unlocked blessings.
        /// </summary>
        /// <param name="onComplete">Callback when selection is made (null if skipped)</param>
        public void Show(System.Action<BlessingDefinition> onComplete = null)
        {
            _onSelectionComplete = onComplete;

            // Clear previous cards
            foreach (var card in _blessingCards)
            {
                if (card != null) Destroy(card);
            }
            _blessingCards.Clear();
            _currentChoices.Clear();

            // Get random unlocked blessings
            _currentChoices = BlessingManager.Instance?.GetRandomBlessingChoices(3) ?? new List<BlessingDefinition>();

            // Find cards container
            Transform cardsContainer = _panel.transform.Find("CardsContainer");
            if (cardsContainer == null)
            {
                Debug.LogError("[BlessingSelectionUI] CardsContainer not found!");
                return;
            }

            // Create cards for each blessing
            for (int i = 0; i < _currentChoices.Count; i++)
            {
                var card = CreateBlessingCard(_currentChoices[i], i, cardsContainer);
                _blessingCards.Add(card);
            }

            // Show panel
            _panel.SetActive(true);
            _isVisible = true;

            // Pause game
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hides the blessing selection UI.
        /// </summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
            _isVisible = false;

            // Resume game
            Time.timeScale = 1f;
        }

        #endregion

        #region Event Handlers

        private void OnBlessingSelected(BlessingDefinition blessing)
        {
            Debug.Log($"[BlessingSelectionUI] Selected blessing: {blessing.DisplayName}");

            // Set the blessing in the manager
            BlessingManager.Instance?.SelectBlessingForRun(blessing);

            // Hide UI
            Hide();

            // Notify callback
            _onSelectionComplete?.Invoke(blessing);
        }

        private void OnSkipClicked()
        {
            Debug.Log("[BlessingSelectionUI] Skipped blessing selection");

            // Clear any selected blessing
            BlessingManager.Instance?.ClearActiveBlessing();

            // Hide UI
            Hide();

            // Notify callback with null
            _onSelectionComplete?.Invoke(null);
        }

        #endregion
    }
}

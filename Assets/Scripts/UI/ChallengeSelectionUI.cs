using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI for selecting Challenge Modifiers at the start of a run.
    /// Unlike Blessings, multiple challenges can be selected simultaneously.
    /// Each challenge adds difficulty and stacks Fae Dust rewards.
    /// </summary>
    public class ChallengeSelectionUI : MonoBehaviour
    {
        #region Private Fields

        private Canvas _canvas;
        private GameObject _panel;
        private List<GameObject> _challengeCards = new List<GameObject>();
        private List<ChallengeModifierDefinition> _availableChallenges = new List<ChallengeModifierDefinition>();
        private HashSet<ChallengeModifierDefinition> _selectedChallenges = new HashSet<ChallengeModifierDefinition>();

        private TextMeshProUGUI _totalMultiplierText;
        private bool _isVisible;
        private System.Action<List<ChallengeModifierDefinition>> _onSelectionComplete;

        // Colors (orange/amber theme for challenge/danger)
        private readonly Color _panelColor = new Color(0.1f, 0.08f, 0.05f, 0.95f);
        private readonly Color _cardColor = new Color(0.15f, 0.1f, 0.08f, 1f);
        private readonly Color _cardHoverColor = new Color(0.25f, 0.18f, 0.12f, 1f);
        private readonly Color _cardSelectedColor = new Color(0.35f, 0.2f, 0.1f, 1f);
        private readonly Color _titleColor = new Color(1f, 0.7f, 0.3f); // Orange/gold
        private readonly Color _descColor = new Color(0.85f, 0.8f, 0.75f);
        private readonly Color _rewardColor = new Color(0.3f, 0.9f, 0.5f); // Green for rewards
        private readonly Color _penaltyColor = new Color(0.9f, 0.6f, 0.3f); // Orange for penalties

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
                GameObject canvasObj = new GameObject("ChallengeSelectionCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 220; // Above form and blessing UI
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        private void CreateUI()
        {
            // Main panel (full screen overlay)
            _panel = new GameObject("ChallengeSelectionPanel");
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
            titleRect.anchorMin = new Vector2(0.1f, 0.85f);
            titleRect.anchorMax = new Vector2(0.9f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Challenge the Fates";
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = _titleColor;

            // Subtitle
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(_panel.transform, false);

            RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.1f, 0.78f);
            subtitleRect.anchorMax = new Vector2(0.9f, 0.85f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.text = "Select challenges to increase difficulty. Multiple challenges stack their rewards.";
            subtitleText.fontSize = 20;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.65f, 0.6f, 0.55f);

            // Total multiplier display
            CreateMultiplierDisplay();

            // Cards container (scrollable area for many challenges)
            GameObject scrollViewObj = new GameObject("ScrollView");
            scrollViewObj.transform.SetParent(_panel.transform, false);

            RectTransform scrollRect = scrollViewObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.05f, 0.2f);
            scrollRect.anchorMax = new Vector2(0.95f, 0.7f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            ScrollRect scroll = scrollViewObj.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;

            // Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollViewObj.transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            viewportObj.AddComponent<Image>().color = Color.clear;
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewportRect;

            // Content container
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = contentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.padding = new RectOffset(20, 20, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.content = contentRect;

            // Buttons row
            CreateButtonsRow();

            // Hint text
            CreateHintText();
        }

        private void CreateMultiplierDisplay()
        {
            GameObject multiplierObj = new GameObject("MultiplierDisplay");
            multiplierObj.transform.SetParent(_panel.transform, false);

            RectTransform multiplierRect = multiplierObj.AddComponent<RectTransform>();
            multiplierRect.anchorMin = new Vector2(0.35f, 0.71f);
            multiplierRect.anchorMax = new Vector2(0.65f, 0.78f);
            multiplierRect.offsetMin = Vector2.zero;
            multiplierRect.offsetMax = Vector2.zero;

            Image multiplierBg = multiplierObj.AddComponent<Image>();
            multiplierBg.color = new Color(0.1f, 0.15f, 0.1f, 0.9f);

            _totalMultiplierText = multiplierObj.AddComponent<TextMeshProUGUI>();
            // Will be updated when challenges are selected
        }

        private void CreateButtonsRow()
        {
            // Start button (with challenges)
            GameObject startObj = new GameObject("StartButton");
            startObj.transform.SetParent(_panel.transform, false);

            RectTransform startRect = startObj.AddComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.55f, 0.05f);
            startRect.anchorMax = new Vector2(0.75f, 0.13f);
            startRect.offsetMin = Vector2.zero;
            startRect.offsetMax = Vector2.zero;

            Image startBg = startObj.AddComponent<Image>();
            startBg.color = new Color(0.2f, 0.35f, 0.2f);

            Button startBtn = startObj.AddComponent<Button>();
            startBtn.onClick.AddListener(OnStartClicked);

            ColorBlock startColors = startBtn.colors;
            startColors.normalColor = new Color(0.2f, 0.35f, 0.2f);
            startColors.highlightedColor = new Color(0.25f, 0.45f, 0.25f);
            startColors.pressedColor = new Color(0.15f, 0.25f, 0.15f);
            startBtn.colors = startColors;

            GameObject startTextObj = new GameObject("Text");
            startTextObj.transform.SetParent(startObj.transform, false);

            RectTransform startTextRect = startTextObj.AddComponent<RectTransform>();
            startTextRect.anchorMin = Vector2.zero;
            startTextRect.anchorMax = Vector2.one;
            startTextRect.offsetMin = Vector2.zero;
            startTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI startText = startTextObj.AddComponent<TextMeshProUGUI>();
            startText.text = "Accept Challenge";
            startText.fontSize = 22;
            startText.alignment = TextAlignmentOptions.Center;
            startText.color = new Color(0.6f, 0.9f, 0.6f);

            // Skip button
            GameObject skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(_panel.transform, false);

            RectTransform skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.25f, 0.05f);
            skipRect.anchorMax = new Vector2(0.45f, 0.13f);
            skipRect.offsetMin = Vector2.zero;
            skipRect.offsetMax = Vector2.zero;

            Image skipBg = skipObj.AddComponent<Image>();
            skipBg.color = new Color(0.25f, 0.2f, 0.2f);

            Button skipBtn = skipObj.AddComponent<Button>();
            skipBtn.onClick.AddListener(OnSkipClicked);

            ColorBlock skipColors = skipBtn.colors;
            skipColors.normalColor = new Color(0.25f, 0.2f, 0.2f);
            skipColors.highlightedColor = new Color(0.35f, 0.25f, 0.25f);
            skipColors.pressedColor = new Color(0.2f, 0.15f, 0.15f);
            skipBtn.colors = skipColors;

            GameObject skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);

            RectTransform skipTextRect = skipTextObj.AddComponent<RectTransform>();
            skipTextRect.anchorMin = Vector2.zero;
            skipTextRect.anchorMax = Vector2.one;
            skipTextRect.offsetMin = Vector2.zero;
            skipTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
            skipText.text = "No Challenges";
            skipText.fontSize = 22;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.color = new Color(0.7f, 0.5f, 0.5f);
        }

        private void CreateHintText()
        {
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_panel.transform, false);

            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.14f);
            hintRect.anchorMax = new Vector2(0.9f, 0.19f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "Click challenges to toggle selection. Unlock more challenges at the Shrine.";
            hintText.fontSize = 16;
            hintText.fontStyle = FontStyles.Italic;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(0.45f, 0.4f, 0.35f);
        }

        private GameObject CreateChallengeCard(ChallengeModifierDefinition challenge, int index, Transform parent)
        {
            GameObject cardObj = new GameObject($"ChallengeCard_{index}");
            cardObj.transform.SetParent(parent, false);

            // Card background
            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = _cardColor;

            // Fixed width for horizontal scroll
            LayoutElement layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 280;
            layoutElement.flexibleHeight = 1;

            // Button for toggle selection
            Button cardBtn = cardObj.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => OnChallengeToggled(challenge, cardBg));

            ColorBlock colors = cardBtn.colors;
            colors.normalColor = _cardColor;
            colors.highlightedColor = _cardHoverColor;
            colors.pressedColor = new Color(0.2f, 0.15f, 0.1f);
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
            contentLayout.spacing = 6;
            contentLayout.padding = new RectOffset(8, 8, 10, 10);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // Challenge name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = challenge.DisplayName;
            nameText.fontSize = 24;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = _titleColor;

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 32;

            // Reward multiplier
            GameObject rewardObj = new GameObject("Reward");
            rewardObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI rewardText = rewardObj.AddComponent<TextMeshProUGUI>();
            rewardText.text = $"{challenge.FaeDustMultiplier:F2}x Fae Dust";
            rewardText.fontSize = 18;
            rewardText.fontStyle = FontStyles.Bold;
            rewardText.alignment = TextAlignmentOptions.Center;
            rewardText.color = _rewardColor;

            LayoutElement rewardLayout = rewardObj.AddComponent<LayoutElement>();
            rewardLayout.preferredHeight = 24;

            // Description
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = challenge.Description;
            descText.fontSize = 14;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = _descColor;
            descText.textWrappingMode = TextWrappingModes.Normal;

            LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
            descLayout.flexibleHeight = 1;
            descLayout.preferredHeight = 50;

            // Effect summary
            string effectSummary = challenge.GetEffectSummary();
            if (!string.IsNullOrEmpty(effectSummary))
            {
                GameObject effectObj = new GameObject("Effects");
                effectObj.transform.SetParent(contentObj.transform, false);

                TextMeshProUGUI effectText = effectObj.AddComponent<TextMeshProUGUI>();
                effectText.text = effectSummary;
                effectText.fontSize = 12;
                effectText.alignment = TextAlignmentOptions.Center;
                effectText.color = _penaltyColor;
                effectText.textWrappingMode = TextWrappingModes.Normal;

                LayoutElement effectLayout = effectObj.AddComponent<LayoutElement>();
                effectLayout.preferredHeight = 40;
            }

            // Selection indicator
            GameObject indicatorObj = new GameObject("SelectionIndicator");
            indicatorObj.transform.SetParent(cardObj.transform, false);

            RectTransform indicatorRect = indicatorObj.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.9f, 0.9f);
            indicatorRect.anchorMax = new Vector2(0.98f, 0.98f);
            indicatorRect.offsetMin = Vector2.zero;
            indicatorRect.offsetMax = Vector2.zero;

            Image indicatorImg = indicatorObj.AddComponent<Image>();
            indicatorImg.color = Color.clear; // Hidden until selected
            indicatorObj.name = "SelectionIndicator";

            return cardObj;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the Challenge selection UI with all unlocked challenges.
        /// </summary>
        /// <param name="onComplete">Callback when selection is confirmed (list may be empty)</param>
        public void Show(System.Action<List<ChallengeModifierDefinition>> onComplete = null)
        {
            _onSelectionComplete = onComplete;
            _selectedChallenges.Clear();

            // Clear previous cards
            foreach (var card in _challengeCards)
            {
                if (card != null) Destroy(card);
            }
            _challengeCards.Clear();

            // Get unlocked challenges
            _availableChallenges = ChallengeModifierManager.Instance?.GetUnlockedChallenges() ?? new List<ChallengeModifierDefinition>();

            // If no challenges available, skip UI
            if (_availableChallenges.Count == 0)
            {
                Debug.Log("[ChallengeSelectionUI] No unlocked challenges, skipping");
                _onSelectionComplete?.Invoke(new List<ChallengeModifierDefinition>());
                return;
            }

            // Find content container
            Transform scrollView = _panel.transform.Find("ScrollView");
            Transform viewport = scrollView?.Find("Viewport");
            Transform content = viewport?.Find("Content");

            if (content == null)
            {
                Debug.LogError("[ChallengeSelectionUI] Content container not found!");
                return;
            }

            // Create cards for each challenge
            for (int i = 0; i < _availableChallenges.Count; i++)
            {
                var card = CreateChallengeCard(_availableChallenges[i], i, content);
                _challengeCards.Add(card);
            }

            // Update multiplier display
            UpdateMultiplierDisplay();

            // Show panel
            _panel.SetActive(true);
            _isVisible = true;

            // Pause game
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hides the Challenge selection UI.
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

        private void OnChallengeToggled(ChallengeModifierDefinition challenge, Image cardBg)
        {
            if (_selectedChallenges.Contains(challenge))
            {
                // Deselect
                _selectedChallenges.Remove(challenge);
                cardBg.color = _cardColor;

                // Hide selection indicator
                var card = cardBg.transform.parent;
                var indicator = card?.Find("SelectionIndicator")?.GetComponent<Image>();
                if (indicator != null) indicator.color = Color.clear;

                Debug.Log($"[ChallengeSelectionUI] Deselected: {challenge.DisplayName}");
            }
            else
            {
                // Select
                _selectedChallenges.Add(challenge);
                cardBg.color = _cardSelectedColor;

                // Show selection indicator (checkmark color)
                var card = cardBg.transform.parent;
                var indicator = card?.Find("SelectionIndicator")?.GetComponent<Image>();
                if (indicator != null) indicator.color = _rewardColor;

                Debug.Log($"[ChallengeSelectionUI] Selected: {challenge.DisplayName}");
            }

            UpdateMultiplierDisplay();
        }

        private void UpdateMultiplierDisplay()
        {
            if (_totalMultiplierText == null) return;

            float totalMultiplier = 1.0f;
            foreach (var challenge in _selectedChallenges)
            {
                totalMultiplier *= challenge.FaeDustMultiplier;
            }

            if (_selectedChallenges.Count == 0)
            {
                _totalMultiplierText.text = "No challenges selected (1.00x Fae Dust)";
                _totalMultiplierText.color = new Color(0.5f, 0.5f, 0.5f);
            }
            else
            {
                _totalMultiplierText.text = $"Total: {totalMultiplier:F2}x Fae Dust ({_selectedChallenges.Count} challenge{(_selectedChallenges.Count > 1 ? "s" : "")})";
                _totalMultiplierText.color = _rewardColor;
            }

            _totalMultiplierText.fontSize = 22;
            _totalMultiplierText.alignment = TextAlignmentOptions.Center;
        }

        private void OnStartClicked()
        {
            Debug.Log($"[ChallengeSelectionUI] Starting with {_selectedChallenges.Count} challenges");

            // Set challenges in manager
            var challengeList = _selectedChallenges.ToList();
            ChallengeModifierManager.Instance?.SetChallengesForRun(challengeList);

            // Hide UI
            Hide();

            // Notify callback
            _onSelectionComplete?.Invoke(challengeList);
        }

        private void OnSkipClicked()
        {
            Debug.Log("[ChallengeSelectionUI] Skipped challenge selection");

            // Clear any challenges
            ChallengeModifierManager.Instance?.ClearActiveChallenges();

            // Hide UI
            Hide();

            // Notify callback with empty list
            _onSelectionComplete?.Invoke(new List<ChallengeModifierDefinition>());
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI for selecting a Prop Mutation at the start of a run.
    /// Displays all unlocked mutations for the player to choose from.
    /// Only one mutation can be active per run.
    /// </summary>
    public class PropMutationSelectionUI : MonoBehaviour
    {
        #region Private Fields

        private Canvas _canvas;
        private GameObject _panel;
        private List<GameObject> _mutationCards = new List<GameObject>();

        private bool _isVisible;
        private System.Action<PropMutationDefinition> _onSelectionComplete;

        // Colors (green/nature theme for props)
        private readonly Color _panelColor = new Color(0.05f, 0.1f, 0.08f, 0.95f);
        private readonly Color _cardColor = new Color(0.08f, 0.15f, 0.1f, 1f);
        private readonly Color _cardHoverColor = new Color(0.12f, 0.22f, 0.15f, 1f);
        private readonly Color _titleColor = new Color(0.5f, 0.9f, 0.6f); // Green
        private readonly Color _descColor = new Color(0.8f, 0.85f, 0.8f);
        private readonly Color _effectColor = new Color(0.6f, 0.8f, 0.6f);

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
            _canvas = UIFactory.FindOrCreateCanvas(this, "PropMutationSelectionCanvas", 215, new Vector2(1920, 1080));
        }

        private void CreateUI()
        {
            // Main panel (full screen overlay)
            _panel = UIFactory.CreateFullScreenPanel(_canvas.transform, "PropMutationSelectionPanel", _panelColor);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.82f);
            titleRect.anchorMax = new Vector2(0.9f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Choose Your Mutation";
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = _titleColor;

            // Subtitle
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(_panel.transform, false);

            RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.1f, 0.75f);
            subtitleRect.anchorMax = new Vector2(0.9f, 0.82f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.text = "Mutate the forest's hazards to serve your hunger.";
            subtitleText.fontSize = 20;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.55f, 0.6f, 0.55f);

            // Cards container
            GameObject cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(_panel.transform, false);

            RectTransform cardsRect = cardsContainer.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.05f, 0.2f);
            cardsRect.anchorMax = new Vector2(0.95f, 0.72f);
            cardsRect.offsetMin = Vector2.zero;
            cardsRect.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 25;
            layout.padding = new RectOffset(15, 15, 15, 15);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Skip button
            CreateSkipButton();

            // Hint text
            CreateHintText();
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
            skipBg.color = new Color(0.2f, 0.25f, 0.2f);

            Button skipBtn = skipObj.AddComponent<Button>();
            skipBtn.onClick.AddListener(OnSkipClicked);

            ColorBlock colors = skipBtn.colors;
            colors.normalColor = new Color(0.2f, 0.25f, 0.2f);
            colors.highlightedColor = new Color(0.25f, 0.32f, 0.25f);
            colors.pressedColor = new Color(0.15f, 0.2f, 0.15f);
            skipBtn.colors = colors;

            GameObject skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);

            RectTransform textRect = skipTextObj.AddComponent<RectTransform>();
            UIFactory.StretchToFillParent(textRect);

            TextMeshProUGUI skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
            skipText.text = "No Mutation";
            skipText.fontSize = 22;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.color = new Color(0.6f, 0.65f, 0.6f);
        }

        private void CreateHintText()
        {
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_panel.transform, false);

            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.13f);
            hintRect.anchorMax = new Vector2(0.9f, 0.18f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "Unlock more mutations at the Shrine";
            hintText.fontSize = 16;
            hintText.fontStyle = FontStyles.Italic;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(0.4f, 0.45f, 0.4f);
        }

        private GameObject CreateMutationCard(PropMutationDefinition mutation, int index, Transform parent)
        {
            GameObject cardObj = new GameObject($"MutationCard_{index}");
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
            cardBtn.onClick.AddListener(() => OnMutationSelected(mutation));

            ColorBlock colors = cardBtn.colors;
            colors.normalColor = _cardColor;
            colors.highlightedColor = _cardHoverColor;
            colors.pressedColor = new Color(0.1f, 0.18f, 0.12f);
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

            // Mutation name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = mutation.DisplayName;
            nameText.fontSize = 26;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = _titleColor;

            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.preferredHeight = 35;

            // Description
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = mutation.Description;
            descText.fontSize = 16;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = _descColor;
            descText.textWrappingMode = TextWrappingModes.Normal;

            LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
            descLayout.flexibleHeight = 1;
            descLayout.preferredHeight = 70;

            // Effect summary
            string effectSummary = mutation.GetEffectSummary();
            if (!string.IsNullOrEmpty(effectSummary))
            {
                GameObject effectObj = new GameObject("Effects");
                effectObj.transform.SetParent(contentObj.transform, false);

                TextMeshProUGUI effectText = effectObj.AddComponent<TextMeshProUGUI>();
                effectText.text = effectSummary;
                effectText.fontSize = 14;
                effectText.alignment = TextAlignmentOptions.Center;
                effectText.color = _effectColor;
                effectText.textWrappingMode = TextWrappingModes.Normal;

                LayoutElement effectLayout = effectObj.AddComponent<LayoutElement>();
                effectLayout.preferredHeight = 40;
            }

            return cardObj;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the mutation selection UI with all unlocked mutations.
        /// </summary>
        /// <param name="onComplete">Callback when selection is made (null if skipped)</param>
        public void Show(System.Action<PropMutationDefinition> onComplete = null)
        {
            _onSelectionComplete = onComplete;

            // Clear previous cards
            foreach (var card in _mutationCards)
            {
                if (card != null) Destroy(card);
            }
            _mutationCards.Clear();

            // Get unlocked mutations
            var unlockedMutations = PropMutationManager.Instance?.GetUnlockedMutations() ?? new List<PropMutationDefinition>();

            // If no mutations available, skip UI
            if (unlockedMutations.Count == 0)
            {
                _onSelectionComplete?.Invoke(null);
                return;
            }

            // Find cards container
            Transform cardsContainer = _panel.transform.Find("CardsContainer");
            if (cardsContainer == null) return;

            // Create cards for each mutation
            for (int i = 0; i < unlockedMutations.Count; i++)
            {
                var card = CreateMutationCard(unlockedMutations[i], i, cardsContainer);
                _mutationCards.Add(card);
            }

            // Show panel
            _panel.SetActive(true);
            _isVisible = true;

            // Pause game
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hides the mutation selection UI.
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

        private void OnMutationSelected(PropMutationDefinition mutation)
        {
            // Set the mutation in the manager
            PropMutationManager.Instance?.SelectMutationForRun(mutation);

            // Hide UI
            Hide();

            // Notify callback
            _onSelectionComplete?.Invoke(mutation);
        }

        private void OnSkipClicked()
        {
            // Clear any selected mutation
            PropMutationManager.Instance?.ClearActiveMutation();

            // Hide UI
            Hide();

            // Notify callback with null
            _onSelectionComplete?.Invoke(null);
        }

        #endregion
    }
}

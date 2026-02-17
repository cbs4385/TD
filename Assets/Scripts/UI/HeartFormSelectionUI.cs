using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FaeMaze.Roguelike;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI for selecting a Heart Form at the start of a run.
    /// Displays all unlocked forms for the player to choose from.
    /// Shown before Blessing selection in the run-start flow.
    /// </summary>
    public class HeartFormSelectionUI : MonoBehaviour
    {
        #region Private Fields

        private Canvas _canvas;
        private GameObject _panel;
        private List<GameObject> _formCards = new List<GameObject>();

        private bool _isVisible;
        private System.Action<HeartFormDefinition> _onSelectionComplete;

        // Colors (darker/moodier than blessing UI to differentiate)
        private readonly Color _panelColor = new Color(0.08f, 0.05f, 0.1f, 0.95f);
        private readonly Color _cardColor = new Color(0.12f, 0.08f, 0.15f, 1f);
        private readonly Color _cardHoverColor = new Color(0.2f, 0.12f, 0.25f, 1f);
        private readonly Color _titleColor = new Color(0.9f, 0.4f, 0.5f); // Reddish for "heart"
        private readonly Color _descColor = new Color(0.8f, 0.75f, 0.85f);
        private readonly Color _bonusColor = new Color(0.5f, 0.9f, 0.5f); // Green for bonuses
        private readonly Color _penaltyColor = new Color(0.9f, 0.5f, 0.5f); // Red for penalties

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
            _canvas = UIFactory.FindOrCreateCanvas(this, "HeartFormSelectionCanvas", 210, new Vector2(1920, 1080));
        }

        private void CreateUI()
        {
            // Main panel (full screen overlay)
            _panel = UIFactory.CreateFullScreenPanel(_canvas.transform, "HeartFormSelectionPanel", _panelColor);

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_panel.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.82f);
            titleRect.anchorMax = new Vector2(0.9f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Choose Your Heart";
            titleText.fontSize = 52;
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
            subtitleText.text = "Each heart beats differently. Choose your predator's nature.";
            subtitleText.fontSize = 20;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.6f, 0.55f, 0.65f);

            // Cards container
            GameObject cardsContainer = new GameObject("CardsContainer");
            cardsContainer.transform.SetParent(_panel.transform, false);

            RectTransform cardsRect = cardsContainer.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0.05f, 0.15f);
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

            // Hint text at bottom
            CreateHintText();
        }

        private void CreateHintText()
        {
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_panel.transform, false);

            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.03f);
            hintRect.anchorMax = new Vector2(0.9f, 0.1f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            TextMeshProUGUI hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "Unlock more hearts at the Shrine";
            hintText.fontSize = 16;
            hintText.fontStyle = FontStyles.Italic;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = new Color(0.4f, 0.4f, 0.5f);
        }

        private GameObject CreateFormCard(HeartFormDefinition form, int index, Transform parent)
        {
            GameObject cardObj = new GameObject($"FormCard_{index}");
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
            cardBtn.onClick.AddListener(() => OnFormSelected(form));

            ColorBlock colors = cardBtn.colors;
            colors.normalColor = _cardColor;
            colors.highlightedColor = _cardHoverColor;
            colors.pressedColor = new Color(0.15f, 0.1f, 0.2f);
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
            contentLayout.spacing = 8;
            contentLayout.padding = new RectOffset(8, 8, 12, 12);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // Form name
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(contentObj.transform, false);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = form.DisplayName;
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
            descText.text = form.Description;
            descText.fontSize = 16;
            descText.alignment = TextAlignmentOptions.Center;
            descText.color = _descColor;
            descText.textWrappingMode = TextWrappingModes.Normal;

            LayoutElement descLayout = descObj.AddComponent<LayoutElement>();
            descLayout.flexibleHeight = 1;
            descLayout.preferredHeight = 60;

            // Spacer
            GameObject spacerObj = new GameObject("Spacer");
            spacerObj.transform.SetParent(contentObj.transform, false);
            LayoutElement spacerLayout = spacerObj.AddComponent<LayoutElement>();
            spacerLayout.preferredHeight = 10;

            // Effects list
            CreateEffectsList(form, contentObj.transform);

            return cardObj;
        }

        private void CreateEffectsList(HeartFormDefinition form, Transform parent)
        {
            // Bonuses (green)
            var bonuses = new List<string>();
            var penalties = new List<string>();

            if (form.AffectsTongueSpeed && form.TongueSpeedMultiplier > 1f)
            {
                float pct = (form.TongueSpeedMultiplier - 1f) * 100f;
                bonuses.Add($"+{pct:F0}% Tongue Speed");
            }
            else if (form.AffectsTongueSpeed)
            {
                float pct = (1f - form.TongueSpeedMultiplier) * 100f;
                penalties.Add($"-{pct:F0}% Tongue Speed");
            }

            if (form.AffectsStartingEssence && form.StartingEssenceModifier > 0)
            {
                bonuses.Add($"+{form.StartingEssenceModifier} Starting Threads");
            }
            else if (form.AffectsStartingEssence)
            {
                penalties.Add($"{form.StartingEssenceModifier} Starting Threads");
            }

            if (form.AffectsLanternEffectiveness && form.LanternEffectivenessMultiplier > 1f)
            {
                float pct = (form.LanternEffectivenessMultiplier - 1f) * 100f;
                bonuses.Add($"+{pct:F0}% Lantern Yield");
            }
            else if (form.AffectsLanternEffectiveness)
            {
                float pct = (1f - form.LanternEffectivenessMultiplier) * 100f;
                penalties.Add($"-{pct:F0}% Lantern Yield");
            }

            if (form.AffectsHazardSpawnRate && form.HazardSpawnRateMultiplier < 1f)
            {
                float pct = (1f - form.HazardSpawnRateMultiplier) * 100f;
                bonuses.Add($"-{pct:F0}% Hazard Spawns");
            }
            else if (form.AffectsHazardSpawnRate)
            {
                float pct = (form.HazardSpawnRateMultiplier - 1f) * 100f;
                penalties.Add($"+{pct:F0}% Hazard Spawns");
            }

            if (form.AffectsEssenceRewards && form.EssenceRewardMultiplier > 1f)
            {
                float pct = (form.EssenceRewardMultiplier - 1f) * 100f;
                bonuses.Add($"+{pct:F0}% Thread Rewards");
            }
            else if (form.AffectsEssenceRewards)
            {
                float pct = (1f - form.EssenceRewardMultiplier) * 100f;
                penalties.Add($"-{pct:F0}% Thread Rewards");
            }

            if (form.HasPassiveEssenceDecay)
            {
                penalties.Add($"-{form.PassiveEssenceDecayPerSecond:F0}/sec Thread Decay");
            }

            // Create effects container
            GameObject effectsObj = new GameObject("Effects");
            effectsObj.transform.SetParent(parent, false);

            RectTransform effectsRect = effectsObj.AddComponent<RectTransform>();
            LayoutElement effectsLayout = effectsObj.AddComponent<LayoutElement>();
            effectsLayout.preferredHeight = 80;

            VerticalLayoutGroup effectsVLayout = effectsObj.AddComponent<VerticalLayoutGroup>();
            effectsVLayout.spacing = 4;
            effectsVLayout.childAlignment = TextAnchor.MiddleCenter;
            effectsVLayout.childControlWidth = true;
            effectsVLayout.childControlHeight = false;
            effectsVLayout.childForceExpandWidth = true;
            effectsVLayout.childForceExpandHeight = false;

            // Add bonus lines
            foreach (var bonus in bonuses)
            {
                CreateEffectLine(bonus, _bonusColor, effectsObj.transform);
            }

            // Add penalty lines
            foreach (var penalty in penalties)
            {
                CreateEffectLine(penalty, _penaltyColor, effectsObj.transform);
            }

            // If default form with no effects
            if (bonuses.Count == 0 && penalties.Count == 0)
            {
                CreateEffectLine("No modifiers", new Color(0.5f, 0.5f, 0.6f), effectsObj.transform);
            }
        }

        private void CreateEffectLine(string text, Color color, Transform parent)
        {
            GameObject lineObj = new GameObject("EffectLine");
            lineObj.transform.SetParent(parent, false);

            TextMeshProUGUI lineText = lineObj.AddComponent<TextMeshProUGUI>();
            lineText.text = text;
            lineText.fontSize = 14;
            lineText.alignment = TextAlignmentOptions.Center;
            lineText.color = color;

            LayoutElement lineLayout = lineObj.AddComponent<LayoutElement>();
            lineLayout.preferredHeight = 18;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the Heart Form selection UI with all unlocked forms.
        /// </summary>
        /// <param name="onComplete">Callback when selection is made</param>
        public void Show(System.Action<HeartFormDefinition> onComplete = null)
        {
            _onSelectionComplete = onComplete;

            // Clear previous cards
            foreach (var card in _formCards)
            {
                if (card != null) Destroy(card);
            }
            _formCards.Clear();

            // Get unlocked forms
            var unlockedForms = HeartFormManager.Instance?.GetUnlockedForms() ?? new List<HeartFormDefinition>();

            // If no forms available, skip UI and select default
            if (unlockedForms.Count == 0)
            {
                OnFormSelected(null);
                return;
            }

            // If only one form (just HungryHeart), skip UI and select it
            if (unlockedForms.Count == 1)
            {
                OnFormSelected(unlockedForms[0]);
                return;
            }

            // Find cards container
            Transform cardsContainer = _panel.transform.Find("CardsContainer");
            if (cardsContainer == null) return;

            // Create cards for each form
            for (int i = 0; i < unlockedForms.Count; i++)
            {
                var card = CreateFormCard(unlockedForms[i], i, cardsContainer);
                _formCards.Add(card);
            }

            // Show panel
            _panel.SetActive(true);
            _isVisible = true;
            GameEventLogger.LogUI("HeartFormSelection", "Show", $"forms={unlockedForms.Count}");

            // Pause game
            Time.timeScale = 0f;
        }

        /// <summary>
        /// Hides the Heart Form selection UI.
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

        private void OnFormSelected(HeartFormDefinition form)
        {
            GameEventLogger.LogUI("HeartFormSelection", "Selected", $"{form?.DisplayName ?? "default"}");
            // Set the form in the manager
            HeartFormManager.Instance?.SelectFormForRun(form);

            // Hide UI
            Hide();

            // Notify callback
            _onSelectionComplete?.Invoke(form);
        }

        #endregion
    }
}

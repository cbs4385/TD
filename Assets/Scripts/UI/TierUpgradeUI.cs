using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FaeMaze.HeartPowers;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// UI overlay for presenting power tier upgrade choices.
    /// Appears when PowerProgressionManager triggers a tier upgrade opportunity.
    /// </summary>
    public class TierUpgradeUI : MonoBehaviour
    {
        #region Constants

        private const float PANEL_WIDTH = 600f;
        private const float PANEL_HEIGHT = 400f;
        private const float BUTTON_HEIGHT = 70f;
        private const float BUTTON_SPACING = 10f;
        private const float HEADER_HEIGHT = 60f;

        // Colors
        private static readonly Color PANEL_BG_COLOR = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        private static readonly Color HEADER_COLOR = new Color(0.8f, 0.6f, 0.2f);
        private static readonly Color TIER2_COLOR = new Color(0.3f, 0.6f, 0.9f);
        private static readonly Color TIER3_COLOR = new Color(0.7f, 0.3f, 0.8f);
        private static readonly Color BUTTON_NORMAL_COLOR = new Color(0.2f, 0.25f, 0.3f);
        private static readonly Color BUTTON_HOVER_COLOR = new Color(0.3f, 0.35f, 0.4f);

        #endregion

        #region Private Fields

        private Canvas _canvas;
        private GameObject _panelRoot;
        private TextMeshProUGUI _headerText;
        private RectTransform _buttonContainer;
        private List<Button> _powerButtons = new List<Button>();
        private Button _skipButton;
        private bool _isVisible;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (PowerProgressionManager.Instance != null)
            {
                PowerProgressionManager.Instance.OnTierUpgradeAvailable += ShowUpgradeUI;
                PowerProgressionManager.Instance.OnPowerTierUpgraded += OnUpgradeApplied;
            }
        }

        private void OnDisable()
        {
            if (PowerProgressionManager.Instance != null)
            {
                PowerProgressionManager.Instance.OnTierUpgradeAvailable -= ShowUpgradeUI;
                PowerProgressionManager.Instance.OnPowerTierUpgraded -= OnUpgradeApplied;
            }
        }

        private void Start()
        {
            CreateUI();
            Hide();

            // Re-subscribe in case PowerProgressionManager wasn't ready in OnEnable
            if (PowerProgressionManager.Instance != null)
            {
                PowerProgressionManager.Instance.OnTierUpgradeAvailable -= ShowUpgradeUI;
                PowerProgressionManager.Instance.OnTierUpgradeAvailable += ShowUpgradeUI;
                PowerProgressionManager.Instance.OnPowerTierUpgraded -= OnUpgradeApplied;
                PowerProgressionManager.Instance.OnPowerTierUpgraded += OnUpgradeApplied;
            }
        }

        private void Update()
        {
            // Allow ESC to skip upgrade (using new Input System)
            if (_isVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                OnSkipClicked();
            }
        }

        #endregion

        #region UI Creation

        private void CreateUI()
        {
            // Find or create canvas
            _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                GameObject canvasObj = new GameObject("TierUpgradeCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 100; // Above most UI
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create panel root
            _panelRoot = new GameObject("TierUpgradePanel");
            _panelRoot.transform.SetParent(_canvas.transform, false);

            RectTransform panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

            // Background
            Image panelBg = _panelRoot.AddComponent<Image>();
            panelBg.color = PANEL_BG_COLOR;

            // Header
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(_panelRoot.transform, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.offsetMin = new Vector2(20, -HEADER_HEIGHT);
            headerRect.offsetMax = new Vector2(-20, 0);

            _headerText = headerObj.AddComponent<TextMeshProUGUI>();
            _headerText.fontSize = 32;
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.alignment = TextAlignmentOptions.Center;
            _headerText.color = HEADER_COLOR;
            _headerText.text = "POWER UPGRADE AVAILABLE";

            // Subtitle
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(_panelRoot.transform, false);

            RectTransform subtitleRect = subtitleObj.AddComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0, 1);
            subtitleRect.anchorMax = new Vector2(1, 1);
            subtitleRect.pivot = new Vector2(0.5f, 1);
            subtitleRect.offsetMin = new Vector2(20, -HEADER_HEIGHT - 30);
            subtitleRect.offsetMax = new Vector2(-20, -HEADER_HEIGHT);

            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.fontSize = 18;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = Color.gray;
            subtitleText.text = "Choose a power to upgrade:";

            // Button container
            GameObject containerObj = new GameObject("ButtonContainer");
            containerObj.transform.SetParent(_panelRoot.transform, false);

            _buttonContainer = containerObj.AddComponent<RectTransform>();
            _buttonContainer.anchorMin = new Vector2(0, 0);
            _buttonContainer.anchorMax = new Vector2(1, 1);
            _buttonContainer.offsetMin = new Vector2(20, 70);
            _buttonContainer.offsetMax = new Vector2(-20, -HEADER_HEIGHT - 40);

            // Skip button
            CreateSkipButton();
        }

        private void CreateSkipButton()
        {
            GameObject skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(_panelRoot.transform, false);

            RectTransform skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(0.5f, 0);
            skipRect.anchorMax = new Vector2(0.5f, 0);
            skipRect.pivot = new Vector2(0.5f, 0);
            skipRect.anchoredPosition = new Vector2(0, 15);
            skipRect.sizeDelta = new Vector2(200, 40);

            Image skipBg = skipObj.AddComponent<Image>();
            skipBg.color = new Color(0.3f, 0.3f, 0.3f);

            _skipButton = skipObj.AddComponent<Button>();
            _skipButton.targetGraphic = skipBg;
            _skipButton.onClick.AddListener(OnSkipClicked);

            // Skip button text
            GameObject skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);

            RectTransform skipTextRect = skipTextObj.AddComponent<RectTransform>();
            skipTextRect.anchorMin = Vector2.zero;
            skipTextRect.anchorMax = Vector2.one;
            skipTextRect.offsetMin = Vector2.zero;
            skipTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
            skipText.text = "Skip (ESC)";
            skipText.fontSize = 18;
            skipText.alignment = TextAlignmentOptions.Center;
            skipText.color = Color.white;
        }

        private Button CreatePowerButton(HeartPowerType powerType, int targetTier, int index)
        {
            GameObject buttonObj = new GameObject($"Button_{powerType}");
            buttonObj.transform.SetParent(_buttonContainer, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0, 1);
            buttonRect.anchorMax = new Vector2(1, 1);
            buttonRect.pivot = new Vector2(0.5f, 1);
            float yPos = -(index * (BUTTON_HEIGHT + BUTTON_SPACING));
            buttonRect.offsetMin = new Vector2(0, yPos - BUTTON_HEIGHT);
            buttonRect.offsetMax = new Vector2(0, yPos);

            Image buttonBg = buttonObj.AddComponent<Image>();
            buttonBg.color = BUTTON_NORMAL_COLOR;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonBg;

            // Tier indicator stripe
            GameObject tierStripeObj = new GameObject("TierStripe");
            tierStripeObj.transform.SetParent(buttonObj.transform, false);

            RectTransform stripeRect = tierStripeObj.AddComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0, 0);
            stripeRect.anchorMax = new Vector2(0, 1);
            stripeRect.pivot = new Vector2(0, 0.5f);
            stripeRect.offsetMin = new Vector2(0, 5);
            stripeRect.offsetMax = new Vector2(8, -5);

            Image stripeImage = tierStripeObj.AddComponent<Image>();
            stripeImage.color = targetTier == 2 ? TIER2_COLOR : TIER3_COLOR;

            // Power name text
            GameObject nameObj = new GameObject("PowerName");
            nameObj.transform.SetParent(buttonObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.5f);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0, 0.5f);
            nameRect.offsetMin = new Vector2(20, 0);
            nameRect.offsetMax = new Vector2(-20, -5);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = $"{GetPowerDisplayName(powerType)} → Tier {ToRoman(targetTier)}";
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = Color.white;

            // Description text
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(buttonObj.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(1, 0.5f);
            descRect.pivot = new Vector2(0, 0.5f);
            descRect.offsetMin = new Vector2(20, 5);
            descRect.offsetMax = new Vector2(-20, 0);

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = GetTierDescription(powerType, targetTier);
            descText.fontSize = 14;
            descText.alignment = TextAlignmentOptions.MidlineLeft;
            descText.color = new Color(0.7f, 0.7f, 0.7f);

            // Capture powerType for closure
            HeartPowerType capturedPower = powerType;
            button.onClick.AddListener(() => OnPowerSelected(capturedPower));

            return button;
        }

        #endregion

        #region Show/Hide

        private void ShowUpgradeUI(int tier)
        {
            if (PowerProgressionManager.Instance == null) return;

            // Get upgradable powers
            var upgradablePowers = PowerProgressionManager.Instance.GetUpgradablePowers(tier);
            if (upgradablePowers.Count == 0)
            {
                return;
            }

            // Update header
            _headerText.text = $"TIER {ToRoman(tier)} UPGRADE AVAILABLE";
            _headerText.color = tier == 2 ? TIER2_COLOR : TIER3_COLOR;

            // Clear existing buttons
            ClearPowerButtons();

            // Create buttons for each upgradable power
            for (int i = 0; i < upgradablePowers.Count; i++)
            {
                var button = CreatePowerButton(upgradablePowers[i], tier, i);
                _powerButtons.Add(button);
            }

            _panelRoot.SetActive(true);
            _isVisible = true;

            // Pause the game
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
            _isVisible = false;

            // Resume game
            Time.timeScale = 1f;
        }

        private void ClearPowerButtons()
        {
            foreach (var button in _powerButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }
            _powerButtons.Clear();
        }

        #endregion

        #region Event Handlers

        private void OnPowerSelected(HeartPowerType powerType)
        {
            if (PowerProgressionManager.Instance != null)
            {
                PowerProgressionManager.Instance.ApplyTierUpgrade(powerType);
            }
        }

        private void OnSkipClicked()
        {
            if (PowerProgressionManager.Instance != null)
            {
                PowerProgressionManager.Instance.SkipPendingUpgrade();
            }
            Hide();
        }

        private void OnUpgradeApplied(HeartPowerType powerType, int tier)
        {
            Hide();
            Debug.Log($"[TierUpgradeUI] Applied upgrade: {powerType} to Tier {tier}");
        }

        #endregion

        #region Helpers

        private string GetPowerDisplayName(HeartPowerType powerType)
        {
            return powerType switch
            {
                HeartPowerType.MurmuringPaths => "Spooky Fog",
                HeartPowerType.HeartwardGrasp => "Yoink!",
                HeartPowerType.DevouringMaw => "Nom Nom",
                HeartPowerType.Sculpting => "Redecorating",
                _ => powerType.ToString()
            };
        }

        private string GetTierDescription(HeartPowerType powerType, int tier)
        {
            // TODO: Load these from HeartPowerDefinition.description
            return powerType switch
            {
                HeartPowerType.MurmuringPaths => tier switch
                {
                    2 => "Labyrinth's Memory: Marked visitors prefer Lost state with Heart bias",
                    3 => "Sealed Ways: Toggle mode to create soft barriers",
                    _ => ""
                },
                HeartPowerType.HeartwardGrasp => tier switch
                {
                    2 => "Extended reach and faster retraction",
                    3 => "Multiple simultaneous grabs",
                    _ => ""
                },
                HeartPowerType.DevouringMaw => tier switch
                {
                    2 => "Larger area of effect",
                    3 => "Chain devouring on consume",
                    _ => ""
                },
                HeartPowerType.Sculpting => tier switch
                {
                    2 => "Enhanced prop variants",
                    3 => "Corrupted props with special effects",
                    _ => ""
                },
                _ => ""
            };
        }

        private string ToRoman(int number)
        {
            return number switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                _ => number.ToString()
            };
        }

        #endregion
    }
}

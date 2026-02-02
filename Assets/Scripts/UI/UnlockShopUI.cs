using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// Full-screen UI for browsing and purchasing unlocks with Fae Dust.
    /// Features category tabs and scrollable unlock cards.
    /// </summary>
    public class UnlockShopUI : MonoBehaviour
    {
        #region Constants

        private const float PANEL_PADDING = 20f;
        private const float TAB_HEIGHT = 50f;
        private const float TAB_WIDTH = 140f;
        private const float CARD_HEIGHT = 100f;
        private const float CARD_SPACING = 10f;
        private const float HEADER_HEIGHT = 80f;

        // Colors
        private static readonly Color PANEL_BG_COLOR = new Color(0.08f, 0.08f, 0.12f, 0.98f);
        private static readonly Color TAB_ACTIVE_COLOR = new Color(0.3f, 0.5f, 0.7f);
        private static readonly Color TAB_INACTIVE_COLOR = new Color(0.15f, 0.15f, 0.2f);
        private static readonly Color CARD_BG_COLOR = new Color(0.12f, 0.12f, 0.18f);
        private static readonly Color CARD_LOCKED_COLOR = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color CARD_UNLOCKED_COLOR = new Color(0.15f, 0.25f, 0.2f);
        private static readonly Color FAE_DUST_COLOR = new Color(0.7f, 0.5f, 1f);
        private static readonly Color LOCKED_TEXT_COLOR = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color UNLOCKED_TEXT_COLOR = new Color(0.4f, 0.8f, 0.4f);
        private static readonly Color AFFORDABLE_COLOR = new Color(0.9f, 0.8f, 0.3f);
        private static readonly Color UNAFFORDABLE_COLOR = new Color(0.6f, 0.3f, 0.3f);

        #endregion

        #region Private Fields

        private Canvas _canvas;
        private GameObject _panelRoot;
        private GameObject _contentRoot;
        private TextMeshProUGUI _faeDustText;
        private TextMeshProUGUI _headerText;
        private ScrollRect _scrollRect;
        private RectTransform _scrollContent;

        private Dictionary<UnlockCategory, Button> _tabButtons = new Dictionary<UnlockCategory, Button>();
        private List<GameObject> _cardObjects = new List<GameObject>();
        private UnlockCategory _currentCategory = UnlockCategory.PowerTier;

        private bool _isVisible;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (UnlockManager.Instance != null)
            {
                UnlockManager.Instance.OnUnlockStateChanged += RefreshCurrentCategory;
            }
            if (MetaProgressionManager.Instance != null)
            {
                MetaProgressionManager.Instance.OnFaeDustChanged += OnFaeDustChanged;
            }
        }

        private void OnDisable()
        {
            if (UnlockManager.Instance != null)
            {
                UnlockManager.Instance.OnUnlockStateChanged -= RefreshCurrentCategory;
            }
            if (MetaProgressionManager.Instance != null)
            {
                MetaProgressionManager.Instance.OnFaeDustChanged -= OnFaeDustChanged;
            }
        }

        private void Start()
        {
            CreateUI();
            Hide();
        }

        private void Update()
        {
            // Use new Input System for Escape key
            if (_isVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
            }
        }

        #endregion

        #region UI Creation

        private void CreateUI()
        {
            // Find or create canvas
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindFirstObjectByType<Canvas>();
            }
            if (_canvas == null)
            {
                GameObject canvasObj = new GameObject("ShopCanvas");
                _canvas = canvasObj.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 50;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create panel root
            _panelRoot = new GameObject("UnlockShopPanel");
            _panelRoot.transform.SetParent(_canvas.transform, false);

            RectTransform panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            // Full-screen background
            Image panelBg = _panelRoot.AddComponent<Image>();
            panelBg.color = PANEL_BG_COLOR;

            // Create content container
            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(_panelRoot.transform, false);

            RectTransform contentRect = _contentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.1f, 0.05f);
            contentRect.anchorMax = new Vector2(0.9f, 0.95f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            CreateHeader();
            CreateTabs();
            CreateScrollArea();
            CreateCloseButton();
        }

        private void CreateHeader()
        {
            // Header container
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(_contentRoot.transform, false);

            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.offsetMin = new Vector2(0, -HEADER_HEIGHT);
            headerRect.offsetMax = Vector2.zero;

            // Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(headerObj.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _headerText = titleObj.AddComponent<TextMeshProUGUI>();
            _headerText.text = "THE SHRINE";
            _headerText.fontSize = 42;
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.alignment = TextAlignmentOptions.MidlineLeft;
            _headerText.color = FAE_DUST_COLOR;

            // Fae Dust display
            GameObject dustObj = new GameObject("FaeDust");
            dustObj.transform.SetParent(headerObj.transform, false);

            RectTransform dustRect = dustObj.AddComponent<RectTransform>();
            dustRect.anchorMin = new Vector2(0.5f, 0);
            dustRect.anchorMax = new Vector2(1, 1);
            dustRect.offsetMin = Vector2.zero;
            dustRect.offsetMax = Vector2.zero;

            _faeDustText = dustObj.AddComponent<TextMeshProUGUI>();
            UpdateFaeDustDisplay();
            _faeDustText.fontSize = 32;
            _faeDustText.fontStyle = FontStyles.Bold;
            _faeDustText.alignment = TextAlignmentOptions.MidlineRight;
            _faeDustText.color = FAE_DUST_COLOR;
        }

        private void CreateTabs()
        {
            // Tab container
            GameObject tabsObj = new GameObject("Tabs");
            tabsObj.transform.SetParent(_contentRoot.transform, false);

            RectTransform tabsRect = tabsObj.AddComponent<RectTransform>();
            tabsRect.anchorMin = new Vector2(0, 1);
            tabsRect.anchorMax = new Vector2(1, 1);
            tabsRect.pivot = new Vector2(0.5f, 1);
            tabsRect.offsetMin = new Vector2(0, -HEADER_HEIGHT - TAB_HEIGHT - 10);
            tabsRect.offsetMax = new Vector2(0, -HEADER_HEIGHT - 10);

            // Horizontal layout
            HorizontalLayoutGroup layout = tabsObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            // Create tabs for each category
            CreateTab(tabsObj.transform, UnlockCategory.PowerTier, "Powers");
            CreateTab(tabsObj.transform, UnlockCategory.Blessing, "Blessings");
            CreateTab(tabsObj.transform, UnlockCategory.HeartForm, "Forms");
            CreateTab(tabsObj.transform, UnlockCategory.PropMutation, "Mutations");
            CreateTab(tabsObj.transform, UnlockCategory.ChallengeModifier, "Challenges");
        }

        private void CreateTab(Transform parent, UnlockCategory category, string label)
        {
            GameObject tabObj = new GameObject($"Tab_{category}");
            tabObj.transform.SetParent(parent, false);

            RectTransform tabRect = tabObj.AddComponent<RectTransform>();
            tabRect.sizeDelta = new Vector2(TAB_WIDTH, TAB_HEIGHT);

            Image tabBg = tabObj.AddComponent<Image>();
            tabBg.color = TAB_INACTIVE_COLOR;

            Button tabButton = tabObj.AddComponent<Button>();
            tabButton.targetGraphic = tabBg;

            // Tab text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(tabObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tabText = textObj.AddComponent<TextMeshProUGUI>();
            tabText.text = label;
            tabText.fontSize = 18;
            tabText.fontStyle = FontStyles.Bold;
            tabText.alignment = TextAlignmentOptions.Center;
            tabText.color = Color.white;

            // Store reference and add listener
            _tabButtons[category] = tabButton;
            UnlockCategory capturedCategory = category;
            tabButton.onClick.AddListener(() => SelectCategory(capturedCategory));
        }

        private void CreateScrollArea()
        {
            // Scroll view container
            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(_contentRoot.transform, false);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0);
            scrollRect.anchorMax = new Vector2(1, 1);
            scrollRect.offsetMin = new Vector2(0, 60); // Space for close button
            scrollRect.offsetMax = new Vector2(0, -HEADER_HEIGHT - TAB_HEIGHT - 20);

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.05f, 0.08f, 0.5f);

            _scrollRect = scrollObj.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 30f;

            // Viewport
            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(10, 10);
            viewportRect.offsetMax = new Vector2(-10, -10);

            Image viewportMask = viewportObj.AddComponent<Image>();
            viewportMask.color = Color.white;
            Mask mask = viewportObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _scrollRect.viewport = viewportRect;

            // Content container
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            _scrollContent = contentObj.AddComponent<RectTransform>();
            _scrollContent.anchorMin = new Vector2(0, 1);
            _scrollContent.anchorMax = new Vector2(1, 1);
            _scrollContent.pivot = new Vector2(0.5f, 1);
            _scrollContent.offsetMin = Vector2.zero;
            _scrollContent.offsetMax = Vector2.zero;

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = CARD_SPACING;
            contentLayout.padding = new RectOffset(0, 0, 10, 10);
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;

            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = _scrollContent;
        }

        private void CreateCloseButton()
        {
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(_contentRoot.transform, false);

            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0);
            closeRect.anchorMax = new Vector2(0.5f, 0);
            closeRect.pivot = new Vector2(0.5f, 0);
            closeRect.anchoredPosition = new Vector2(0, 10);
            closeRect.sizeDelta = new Vector2(200, 45);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.4f, 0.2f, 0.2f);

            Button closeButton = closeObj.AddComponent<Button>();
            closeButton.targetGraphic = closeBg;
            closeButton.onClick.AddListener(Hide);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(closeObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI closeText = textObj.AddComponent<TextMeshProUGUI>();
            closeText.text = "Close (ESC)";
            closeText.fontSize = 20;
            closeText.fontStyle = FontStyles.Bold;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
        }

        #endregion

        #region Card Creation

        private void PopulateCards(UnlockCategory category)
        {
            // Clear existing cards
            ClearCards();

            if (UnlockManager.Instance == null) return;

            var unlocks = UnlockManager.Instance.GetUnlocksByCategory(category);

            foreach (var unlock in unlocks)
            {
                CreateUnlockCard(unlock);
            }
        }

        private void ClearCards()
        {
            foreach (var card in _cardObjects)
            {
                if (card != null)
                {
                    Destroy(card);
                }
            }
            _cardObjects.Clear();
        }

        private void CreateUnlockCard(UnlockDefinition unlock)
        {
            bool isUnlocked = UnlockManager.Instance.IsUnlocked(unlock.Id);
            bool canAfford = MetaProgressionManager.Instance?.CanAfford(unlock.FaeDustCost) ?? false;
            bool canUnlock = UnlockManager.Instance.CanUnlock(unlock.Id, out string reason);

            GameObject cardObj = new GameObject($"Card_{unlock.Id}");
            cardObj.transform.SetParent(_scrollContent, false);

            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(0, CARD_HEIGHT);

            LayoutElement layoutElement = cardObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = CARD_HEIGHT;
            layoutElement.preferredHeight = CARD_HEIGHT;

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = isUnlocked ? CARD_UNLOCKED_COLOR : CARD_BG_COLOR;

            // Left stripe (category color)
            GameObject stripeObj = new GameObject("Stripe");
            stripeObj.transform.SetParent(cardObj.transform, false);

            RectTransform stripeRect = stripeObj.AddComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0, 0);
            stripeRect.anchorMax = new Vector2(0, 1);
            stripeRect.pivot = new Vector2(0, 0.5f);
            stripeRect.offsetMin = new Vector2(0, 5);
            stripeRect.offsetMax = new Vector2(6, -5);

            Image stripeImage = stripeObj.AddComponent<Image>();
            stripeImage.color = GetCategoryColor(unlock.Category);

            // Name text
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(cardObj.transform, false);

            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0.55f);
            nameRect.anchorMax = new Vector2(0.65f, 1);
            nameRect.offsetMin = new Vector2(20, 0);
            nameRect.offsetMax = new Vector2(0, -10);

            TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.text = unlock.DisplayName;
            nameText.fontSize = 22;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = isUnlocked ? UNLOCKED_TEXT_COLOR : Color.white;

            // Description text
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(cardObj.transform, false);

            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0, 0);
            descRect.anchorMax = new Vector2(0.65f, 0.55f);
            descRect.offsetMin = new Vector2(20, 10);
            descRect.offsetMax = Vector2.zero;

            TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = unlock.Description;
            descText.fontSize = 14;
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.color = isUnlocked ? new Color(0.6f, 0.8f, 0.6f) : new Color(0.7f, 0.7f, 0.7f);
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.overflowMode = TextOverflowModes.Ellipsis;

            // Right side: cost or unlocked status
            if (isUnlocked)
            {
                CreateUnlockedBadge(cardObj.transform);
            }
            else
            {
                CreatePurchaseButton(cardObj.transform, unlock, canUnlock, canAfford, reason);
            }

            _cardObjects.Add(cardObj);
        }

        private void CreateUnlockedBadge(Transform parent)
        {
            GameObject badgeObj = new GameObject("UnlockedBadge");
            badgeObj.transform.SetParent(parent, false);

            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.7f, 0.3f);
            badgeRect.anchorMax = new Vector2(0.95f, 0.7f);
            badgeRect.offsetMin = Vector2.zero;
            badgeRect.offsetMax = Vector2.zero;

            Image badgeBg = badgeObj.AddComponent<Image>();
            badgeBg.color = new Color(0.2f, 0.4f, 0.2f);

            // Badge text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(badgeObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI badgeText = textObj.AddComponent<TextMeshProUGUI>();
            badgeText.text = "UNLOCKED";
            badgeText.fontSize = 16;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = UNLOCKED_TEXT_COLOR;
        }

        private void CreatePurchaseButton(Transform parent, UnlockDefinition unlock, bool canUnlock, bool canAfford, string reason)
        {
            GameObject buttonObj = new GameObject("PurchaseButton");
            buttonObj.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.7f, 0.2f);
            buttonRect.anchorMax = new Vector2(0.95f, 0.8f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            Image buttonBg = buttonObj.AddComponent<Image>();

            if (canUnlock)
            {
                buttonBg.color = canAfford ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                buttonBg.color = new Color(0.2f, 0.2f, 0.2f);
            }

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonBg;
            button.interactable = canUnlock;

            string capturedId = unlock.Id;
            button.onClick.AddListener(() => OnPurchaseClicked(capturedId));

            // Cost text
            GameObject costObj = new GameObject("Cost");
            costObj.transform.SetParent(buttonObj.transform, false);

            RectTransform costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = Vector2.one;
            costRect.offsetMin = Vector2.zero;
            costRect.offsetMax = Vector2.zero;

            TextMeshProUGUI costText = costObj.AddComponent<TextMeshProUGUI>();

            if (canUnlock || string.IsNullOrEmpty(reason) || reason.StartsWith("Not enough"))
            {
                costText.text = $"{unlock.FaeDustCost} Dust";
                costText.color = canAfford ? AFFORDABLE_COLOR : UNAFFORDABLE_COLOR;
            }
            else
            {
                // Show requirement instead of cost
                costText.text = "Locked";
                costText.color = LOCKED_TEXT_COLOR;
            }

            costText.fontSize = 16;
            costText.fontStyle = FontStyles.Bold;
            costText.alignment = TextAlignmentOptions.Center;

            // Tooltip for locked items
            if (!canUnlock && !string.IsNullOrEmpty(reason))
            {
                // Add requirement text below button
                GameObject reqObj = new GameObject("Requirement");
                reqObj.transform.SetParent(parent, false);

                RectTransform reqRect = reqObj.AddComponent<RectTransform>();
                reqRect.anchorMin = new Vector2(0.7f, 0);
                reqRect.anchorMax = new Vector2(0.95f, 0.2f);
                reqRect.offsetMin = Vector2.zero;
                reqRect.offsetMax = Vector2.zero;

                TextMeshProUGUI reqText = reqObj.AddComponent<TextMeshProUGUI>();
                reqText.text = reason;
                reqText.fontSize = 10;
                reqText.alignment = TextAlignmentOptions.Center;
                reqText.color = new Color(0.6f, 0.4f, 0.4f);
                reqText.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        #endregion

        #region Event Handlers

        private void SelectCategory(UnlockCategory category)
        {
            _currentCategory = category;

            // Update tab visuals
            foreach (var kvp in _tabButtons)
            {
                Image tabImage = kvp.Value.GetComponent<Image>();
                if (tabImage != null)
                {
                    tabImage.color = kvp.Key == category ? TAB_ACTIVE_COLOR : TAB_INACTIVE_COLOR;
                }
            }

            // Repopulate cards
            PopulateCards(category);

            // Reset scroll position
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void OnPurchaseClicked(string unlockId)
        {
            if (UnlockManager.Instance == null) return;

            if (UnlockManager.Instance.TryPurchaseUnlock(unlockId))
            {
                // Success - UI will refresh via event
                Debug.Log($"[UnlockShopUI] Purchased: {unlockId}");
            }
        }

        private void OnFaeDustChanged(int newAmount)
        {
            UpdateFaeDustDisplay();
            RefreshCurrentCategory();
        }

        private void RefreshCurrentCategory()
        {
            PopulateCards(_currentCategory);
        }

        private void UpdateFaeDustDisplay()
        {
            if (_faeDustText != null)
            {
                int dust = MetaProgressionManager.Instance?.FaeDust ?? 0;
                _faeDustText.text = $"Fae Dust: {dust}";
            }
        }

        #endregion

        #region Show/Hide

        public void Show()
        {
            if (_panelRoot == null) return;

            _panelRoot.SetActive(true);
            _isVisible = true;

            // Refresh display
            UpdateFaeDustDisplay();
            SelectCategory(_currentCategory);
        }

        public void Hide()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
            _isVisible = false;
        }

        public void Toggle()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public bool IsVisible => _isVisible;

        #endregion

        #region Helpers

        private Color GetCategoryColor(UnlockCategory category)
        {
            return category switch
            {
                UnlockCategory.PowerTier => new Color(0.4f, 0.6f, 0.9f),    // Blue
                UnlockCategory.Blessing => new Color(0.9f, 0.7f, 0.3f),     // Gold
                UnlockCategory.HeartForm => new Color(0.8f, 0.3f, 0.3f),    // Red
                UnlockCategory.PropMutation => new Color(0.3f, 0.8f, 0.5f), // Green
                UnlockCategory.ChallengeModifier => new Color(0.7f, 0.4f, 0.8f), // Purple
                UnlockCategory.Cosmetic => new Color(0.9f, 0.5f, 0.7f),     // Pink
                _ => Color.gray
            };
        }

        #endregion
    }
}

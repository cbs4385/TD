using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Cameras;
using FaeMaze.Maze;
using FaeMaze.HeartPowers;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Position options for the tutorial dialog panel.
    /// </summary>
    public enum DialogPosition
    {
        Left,
        Right
    }

    /// <summary>
    /// Manages all tutorial UI elements including dialogs, overlays, and highlights.
    /// </summary>
    public class TutorialUIController : MonoBehaviour
    {
        #region Constants

        // Colors
        private static readonly Color DIALOG_BG_COLOR = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private static readonly Color DIALOG_BORDER_COLOR = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color TITLE_COLOR = new Color(1f, 0.85f, 0.3f, 1f); // Gold
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color HIGHLIGHT_COLOR = new Color(1f, 0.85f, 0.3f, 0.8f); // Gold glow
        private static readonly Color BUTTON_NORMAL_COLOR = new Color(0.2f, 0.4f, 0.3f, 1f);
        private static readonly Color BUTTON_HOVER_COLOR = new Color(0.3f, 0.5f, 0.4f, 1f);
        private static readonly Color DIM_OVERLAY_COLOR = new Color(0f, 0f, 0f, 0.75f); // Match highlight mask opacity

        // Sizes
        private const float DIALOG_MIN_HEIGHT = 100f;
        private const float TITLE_FONT_SIZE = 28f;
        private const float BODY_FONT_SIZE = 24f;
        private const float BUTTON_FONT_SIZE = 18f;
        private const float BUTTON_HEIGHT = 40f;
        private const float PADDING = 20f;
        private const float HIGHLIGHT_RING_SIZE = 80f;
        private const float HIGHLIGHT_PULSE_SPEED = 3f;
        private const float HIGHLIGHT_PULSE_SCALE = 0.15f;

        #endregion

        #region Private Fields

        private Canvas canvas;
        private GameObject tutorialRoot;
        private GameObject dimOverlay;
        private GameObject dialogPanel;
        private GameObject highlightRing;        // Rectangular highlight for UI elements
        private GameObject circularHighlight;    // Circular highlight for world objects
        private GameObject arrowIndicator;
        private GameObject uiHighlightMask;      // Semi-transparent mask for UI element highlighting
        private GameObject worldCutoutMask;      // Mask with circular cutout for world object highlighting
        private Image worldCutoutImage;          // Image component for the cutout mask

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI stepCounterText;
        private Button continueButton;
        private Button skipButton;

        private TutorialManager manager;
        private RectTransform highlightTarget;
        private bool isHighlightingWorldPosition;
        private Vector3 worldHighlightPosition;
        private float worldHighlightRadius = 75f; // Radius of the cutout in screen pixels

        // For restoring highlighted UI element
        private RectTransform currentHighlightedElement;

        // Track sibling elements that were pushed behind the mask
        private List<(GameObject obj, Canvas addedCanvas)> siblingsPushedBehind = new List<(GameObject, Canvas)>();

        // HeartPowerManager reference for checking active powers
        private HeartPowerManager heartPowerManager;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            manager = TutorialManager.Instance;
            if (manager == null)
            {
                manager = GetComponent<TutorialManager>();
            }

            if (manager != null)
            {
                manager.OnTutorialStarted += OnTutorialStarted;
                manager.OnStepChanged += OnStepChanged;
                manager.OnTutorialCompleted += OnTutorialCompleted;
            }

            // Get HeartPowerManager reference and subscribe to power events
            heartPowerManager = Object.FindFirstObjectByType<HeartPowerManager>();
            if (heartPowerManager != null)
            {
                heartPowerManager.OnPowerActivated += OnPowerActivated;
                heartPowerManager.OnPowerDeactivated += OnPowerDeactivated;
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnTutorialStarted -= OnTutorialStarted;
                manager.OnStepChanged -= OnStepChanged;
                manager.OnTutorialCompleted -= OnTutorialCompleted;
            }

            if (heartPowerManager != null)
            {
                heartPowerManager.OnPowerActivated -= OnPowerActivated;
                heartPowerManager.OnPowerDeactivated -= OnPowerDeactivated;
            }
        }

        private void Update()
        {
            // Update highlight ring pulse animation (pulse alpha for rectangular highlight)
            if (highlightRing != null && highlightRing.activeSelf)
            {
                // Pulse the alpha of the border images
                float pulseAlpha = 0.7f + Mathf.Sin(Time.unscaledTime * HIGHLIGHT_PULSE_SPEED) * 0.3f;
                foreach (var img in highlightRing.GetComponentsInChildren<Image>())
                {
                    var c = img.color;
                    // Preserve the relative alpha (outer glow is more transparent)
                    float baseAlpha = img.gameObject.name.Contains("Glow") ? 0.4f : 1f;
                    img.color = new Color(c.r, c.g, c.b, baseAlpha * pulseAlpha);
                }
            }

            // Update circular highlight pulse animation
            if (circularHighlight != null && circularHighlight.activeSelf)
            {
                float pulseAlpha = 0.7f + Mathf.Sin(Time.unscaledTime * HIGHLIGHT_PULSE_SPEED) * 0.3f;
                foreach (var img in circularHighlight.GetComponentsInChildren<Image>())
                {
                    var c = img.color;
                    float baseAlpha = img.gameObject.name.Contains("Glow") ? 0.4f : 1f;
                    img.color = new Color(c.r, c.g, c.b, baseAlpha * pulseAlpha);
                }

                // Update position if tracking world position
                if (isHighlightingWorldPosition)
                {
                    UpdateWorldHighlightPosition();
                }
            }

            // Update arrow indicator for world-space targets
            if (arrowIndicator != null && arrowIndicator.activeSelf && isHighlightingWorldPosition)
            {
                UpdateArrowIndicator();
            }
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Creates all tutorial UI elements.
        /// </summary>
        private void CreateTutorialUI()
        {
            // Find or create canvas
            canvas = FindOrCreateCanvas();

            // Create root container
            tutorialRoot = new GameObject("TutorialRoot");
            tutorialRoot.transform.SetParent(canvas.transform, false);
            var rootRect = tutorialRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Create dim overlay (behind dialog, but blocks input)
            CreateDimOverlay();

            // Create UI highlight mask (75% opaque, used when highlighting UI elements)
            CreateUIHighlightMask();

            // Create highlight ring (rectangular for UI elements)
            CreateHighlightRing();

            // Create circular highlight (for world objects like Heart and Focal Point)
            CreateCircularHighlight();

            // Create arrow indicator
            CreateArrowIndicator();

            // Create dialog panel
            CreateDialogPanel();
        }

        private Canvas FindOrCreateCanvas()
        {
            // Create a dedicated tutorial canvas with sorting order above game UI (which is at 100)
            // Sorting order hierarchy:
            //   99: siblings pushed behind mask
            //  100: game UI canvas
            //  200: tutorial canvas (dimOverlay, cutout masks)
            //  201: highlighted UI elements
            //  202: dialog panel, highlight rings
            var canvasGO = new GameObject("TutorialCanvas");
            var newCanvas = canvasGO.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 200; // Above game UI (100)

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            return newCanvas;
        }

        private void CreateDimOverlay()
        {
            dimOverlay = new GameObject("DimOverlay");
            dimOverlay.transform.SetParent(tutorialRoot.transform, false);

            var rect = dimOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = dimOverlay.AddComponent<Image>();
            image.color = DIM_OVERLAY_COLOR;
            image.raycastTarget = true; // Blocks clicks behind it

            dimOverlay.SetActive(false);
        }

        private void CreateUIHighlightMask()
        {
            uiHighlightMask = new GameObject("UIHighlightMask");
            uiHighlightMask.transform.SetParent(tutorialRoot.transform, false);

            var rect = uiHighlightMask.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = uiHighlightMask.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.75f); // 75% opaque black
            image.raycastTarget = false; // Don't block clicks

            uiHighlightMask.SetActive(false);

            // Create world cutout mask (mask with circular hole for world objects)
            worldCutoutMask = new GameObject("WorldCutoutMask");
            worldCutoutMask.transform.SetParent(tutorialRoot.transform, false);

            var cutoutRect = worldCutoutMask.AddComponent<RectTransform>();
            cutoutRect.anchorMin = Vector2.zero;
            cutoutRect.anchorMax = Vector2.one;
            cutoutRect.offsetMin = Vector2.zero;
            cutoutRect.offsetMax = Vector2.zero;

            worldCutoutImage = worldCutoutMask.AddComponent<Image>();
            worldCutoutImage.raycastTarget = false;
            // Sprite will be generated dynamically with cutout position

            worldCutoutMask.SetActive(false);
        }

        /// <summary>
        /// Creates a mask texture with a circular transparent cutout at the specified screen position.
        /// </summary>
        private Sprite CreateCutoutMaskSprite(Vector2 cutoutScreenPos, float cutoutRadius)
        {
            // Use a reasonable resolution for the mask
            int width = Screen.width;
            int height = Screen.height;

            // Scale down for performance (use 1/4 resolution)
            int texWidth = width / 4;
            int texHeight = height / 4;
            float scaleX = (float)texWidth / width;
            float scaleY = (float)texHeight / height;

            var texture = new Texture2D(texWidth, texHeight, TextureFormat.ARGB32, false);
            var pixels = new Color[texWidth * texHeight];

            // Scale cutout position and radius
            Vector2 scaledPos = new Vector2(cutoutScreenPos.x * scaleX, cutoutScreenPos.y * scaleY);
            float scaledRadius = cutoutRadius * Mathf.Min(scaleX, scaleY);

            Color maskColor = new Color(0f, 0f, 0f, 0.75f);

            for (int y = 0; y < texHeight; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), scaledPos);
                    if (dist < scaledRadius - 2)
                    {
                        // Inside cutout - fully transparent
                        pixels[y * texWidth + x] = Color.clear;
                    }
                    else if (dist < scaledRadius + 2)
                    {
                        // Edge - smooth transition
                        float t = (dist - (scaledRadius - 2)) / 4f;
                        pixels[y * texWidth + x] = new Color(0f, 0f, 0f, 0.75f * t);
                    }
                    else
                    {
                        // Outside cutout - full mask
                        pixels[y * texWidth + x] = maskColor;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f));
        }

        private void CreateHighlightRing()
        {
            highlightRing = new GameObject("HighlightRing");
            highlightRing.transform.SetParent(tutorialRoot.transform, false);

            var rect = highlightRing.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(HIGHLIGHT_RING_SIZE, HIGHLIGHT_RING_SIZE);

            // Add Canvas so highlight ring appears above highlighted element and tutorial canvas
            var highlightCanvas = highlightRing.AddComponent<Canvas>();
            highlightCanvas.overrideSorting = true;
            highlightCanvas.sortingOrder = 202; // Same level as dialog
            highlightRing.AddComponent<GraphicRaycaster>();

            // Create rectangular border using 4 edge images
            float borderThickness = 4f;
            float glowThickness = 8f;

            // Inner border (bright)
            CreateRectangularBorder(highlightRing.transform, "InnerBorder", borderThickness, HIGHLIGHT_COLOR, 0f);

            // Outer glow border (semi-transparent)
            CreateRectangularBorder(highlightRing.transform, "OuterGlow", glowThickness,
                new Color(HIGHLIGHT_COLOR.r, HIGHLIGHT_COLOR.g, HIGHLIGHT_COLOR.b, 0.4f), borderThickness + 2f);

            highlightRing.SetActive(false);
        }

        private void CreateCircularHighlight()
        {
            circularHighlight = new GameObject("CircularHighlight");
            circularHighlight.transform.SetParent(tutorialRoot.transform, false);

            var rect = circularHighlight.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(HIGHLIGHT_RING_SIZE, HIGHLIGHT_RING_SIZE);

            // Add Canvas so circular highlight appears above mask and tutorial canvas
            var highlightCanvas = circularHighlight.AddComponent<Canvas>();
            highlightCanvas.overrideSorting = true;
            highlightCanvas.sortingOrder = 202; // Same level as dialog
            circularHighlight.AddComponent<GraphicRaycaster>();

            // Create inner circle ring
            var innerRing = new GameObject("InnerRing");
            innerRing.transform.SetParent(circularHighlight.transform, false);
            var innerRect = innerRing.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = Vector2.zero;
            innerRect.offsetMax = Vector2.zero;
            var innerImg = innerRing.AddComponent<Image>();
            innerImg.sprite = CreateCircleRingSprite(128, 6);
            innerImg.color = HIGHLIGHT_COLOR;
            innerImg.raycastTarget = false;

            // Create outer glow ring
            var outerGlow = new GameObject("OuterGlow");
            outerGlow.transform.SetParent(circularHighlight.transform, false);
            var outerRect = outerGlow.AddComponent<RectTransform>();
            outerRect.anchorMin = new Vector2(-0.1f, -0.1f);
            outerRect.anchorMax = new Vector2(1.1f, 1.1f);
            outerRect.offsetMin = Vector2.zero;
            outerRect.offsetMax = Vector2.zero;
            var outerImg = outerGlow.AddComponent<Image>();
            outerImg.sprite = CreateCircleRingSprite(128, 12);
            outerImg.color = new Color(HIGHLIGHT_COLOR.r, HIGHLIGHT_COLOR.g, HIGHLIGHT_COLOR.b, 0.4f);
            outerImg.raycastTarget = false;

            circularHighlight.SetActive(false);
        }

        /// <summary>
        /// Creates a circle ring sprite programmatically.
        /// </summary>
        private Sprite CreateCircleRingSprite(int size, int thickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var pixels = new Color[size * size];
            float center = size / 2f;
            float outerRadius = size / 2f - 2;
            float innerRadius = outerRadius - thickness;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= outerRadius && dist >= innerRadius)
                    {
                        // Anti-aliasing at edges
                        float alpha = 1f;
                        if (dist > outerRadius - 1.5f) alpha = (outerRadius - dist) / 1.5f;
                        if (dist < innerRadius + 1.5f) alpha = Mathf.Min(alpha, (dist - innerRadius) / 1.5f);
                        pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(alpha));
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Creates a rectangular border using 4 stretched images (top, bottom, left, right).
        /// </summary>
        private void CreateRectangularBorder(Transform parent, string name, float thickness, Color color, float offset)
        {
            // Top edge
            var top = new GameObject(name + "_Top");
            top.transform.SetParent(parent, false);
            var topRect = top.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.offsetMin = new Vector2(-offset, 0);
            topRect.offsetMax = new Vector2(offset, offset);
            topRect.sizeDelta = new Vector2(topRect.sizeDelta.x, thickness);
            var topImg = top.AddComponent<Image>();
            topImg.color = color;
            topImg.raycastTarget = false;

            // Bottom edge
            var bottom = new GameObject(name + "_Bottom");
            bottom.transform.SetParent(parent, false);
            var bottomRect = bottom.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.offsetMin = new Vector2(-offset, -offset);
            bottomRect.offsetMax = new Vector2(offset, 0);
            bottomRect.sizeDelta = new Vector2(bottomRect.sizeDelta.x, thickness);
            var bottomImg = bottom.AddComponent<Image>();
            bottomImg.color = color;
            bottomImg.raycastTarget = false;

            // Left edge
            var left = new GameObject(name + "_Left");
            left.transform.SetParent(parent, false);
            var leftRect = left.AddComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0, 1);
            leftRect.pivot = new Vector2(0f, 0.5f);
            leftRect.offsetMin = new Vector2(-offset, -offset);
            leftRect.offsetMax = new Vector2(0, offset);
            leftRect.sizeDelta = new Vector2(thickness, leftRect.sizeDelta.y);
            var leftImg = left.AddComponent<Image>();
            leftImg.color = color;
            leftImg.raycastTarget = false;

            // Right edge
            var right = new GameObject(name + "_Right");
            right.transform.SetParent(parent, false);
            var rightRect = right.AddComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(1, 0);
            rightRect.anchorMax = new Vector2(1, 1);
            rightRect.pivot = new Vector2(1f, 0.5f);
            rightRect.offsetMin = new Vector2(0, -offset);
            rightRect.offsetMax = new Vector2(offset, offset);
            rightRect.sizeDelta = new Vector2(thickness, rightRect.sizeDelta.y);
            var rightImg = right.AddComponent<Image>();
            rightImg.color = color;
            rightImg.raycastTarget = false;
        }

        private void CreateArrowIndicator()
        {
            arrowIndicator = new GameObject("ArrowIndicator");
            arrowIndicator.transform.SetParent(tutorialRoot.transform, false);

            var rect = arrowIndicator.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(40, 60);

            // Create arrow shape using text (simple approach)
            var text = arrowIndicator.AddComponent<TextMeshProUGUI>();
            text.text = "\u25BC"; // Down arrow unicode
            text.fontSize = 48;
            text.color = HIGHLIGHT_COLOR;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            arrowIndicator.SetActive(false);
        }

        private void CreateDialogPanel()
        {
            dialogPanel = new GameObject("DialogPanel");
            dialogPanel.transform.SetParent(tutorialRoot.transform, false);

            var rect = dialogPanel.AddComponent<RectTransform>();
            // Position in upper-left corner, below the seed/essence bar
            // Values match user's image 3 Inspector: Left: 57, Top: 107, Right: -37, Bottom: -82
            rect.anchorMin = new Vector2(0f, 0.42f); // Bottom at 42% from screen bottom
            rect.anchorMax = new Vector2(0.42f, 1f); // Right at 42% of screen, top at screen top
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(57f, -82f);  // Left, Bottom (from Inspector)
            rect.offsetMax = new Vector2(-37f, -107f); // Right, Top (negated from Inspector)

            // Add Canvas to dialog so it's always on top
            // Tutorial canvas is 200, so dialog needs to be higher (202)
            var dialogCanvas = dialogPanel.AddComponent<Canvas>();
            dialogCanvas.overrideSorting = true;
            dialogCanvas.sortingOrder = 202; // Above tutorial canvas (200) and highlighted elements (201)
            dialogPanel.AddComponent<GraphicRaycaster>();

            // Background
            var bg = dialogPanel.AddComponent<Image>();
            bg.color = DIALOG_BG_COLOR;

            // Border (using outline)
            var outline = dialogPanel.AddComponent<Outline>();
            outline.effectColor = DIALOG_BORDER_COLOR;
            outline.effectDistance = new Vector2(2, 2);

            // Vertical layout - content fills the panel
            var layout = dialogPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)PADDING, (int)PADDING, (int)PADDING, (int)PADDING);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            // No ContentSizeFitter - panel size is controlled by anchors

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(dialogPanel.transform, false);
            titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "Tutorial";
            titleText.fontSize = TITLE_FONT_SIZE;
            titleText.color = TITLE_COLOR;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;

            var titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.minHeight = 36f;

            // Body text
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(dialogPanel.transform, false);
            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyText.text = "";
            bodyText.fontSize = BODY_FONT_SIZE;
            bodyText.color = TEXT_COLOR;
            bodyText.alignment = TextAlignmentOptions.Left;

            var bodyLayout = bodyGO.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1f; // Body text expands to fill available space
            bodyLayout.minHeight = 80f;

            // Button container
            var buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(dialogPanel.transform, false);
            var buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 10f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = false;
            buttonLayout.childControlHeight = false;

            var buttonContainerLayout = buttonContainer.AddComponent<LayoutElement>();
            buttonContainerLayout.minHeight = BUTTON_HEIGHT + 6f;

            // Continue button
            continueButton = CreateButton(buttonContainer.transform, "Continue", OnContinueClicked);

            // Skip button
            skipButton = CreateButton(buttonContainer.transform, "Skip Tutorial", OnSkipClicked);
            var skipColors = skipButton.colors;
            skipColors.normalColor = new Color(0.4f, 0.2f, 0.2f, 1f);
            skipColors.highlightedColor = new Color(0.5f, 0.3f, 0.3f, 1f);
            skipButton.colors = skipColors;

            // Step counter (bottom right)
            var counterGO = new GameObject("StepCounter");
            counterGO.transform.SetParent(dialogPanel.transform, false);
            stepCounterText = counterGO.AddComponent<TextMeshProUGUI>();
            stepCounterText.text = "1/12";
            stepCounterText.fontSize = 12f;
            stepCounterText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            stepCounterText.alignment = TextAlignmentOptions.Right;

            var counterLayout = counterGO.AddComponent<LayoutElement>();
            counterLayout.minHeight = 16f;

            dialogPanel.SetActive(false);
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonGO = new GameObject(label + "Button");
            buttonGO.transform.SetParent(parent, false);

            var rect = buttonGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(140f, BUTTON_HEIGHT);

            var image = buttonGO.AddComponent<Image>();
            image.color = BUTTON_NORMAL_COLOR;

            var button = buttonGO.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.normalColor = BUTTON_NORMAL_COLOR;
            colors.highlightedColor = BUTTON_HOVER_COLOR;
            colors.pressedColor = new Color(0.15f, 0.35f, 0.25f, 1f);
            button.colors = colors;

            // Button text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = BUTTON_FONT_SIZE;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            return button;
        }

        #endregion

        #region Event Handlers

        private void OnTutorialStarted()
        {
            Debug.Log("[TutorialUIController] OnTutorialStarted called");

            if (tutorialRoot == null)
            {
                Debug.Log("[TutorialUIController] Creating tutorial UI");
                CreateTutorialUI();
            }

            tutorialRoot.SetActive(true);
            dialogPanel.SetActive(true);

            // Always show the dim overlay during tutorial for consistent background
            dimOverlay.SetActive(true);

            Debug.Log("[TutorialUIController] Tutorial UI activated");
        }

        private void OnStepChanged(int stepIndex)
        {
            Debug.Log($"[TutorialUIController] OnStepChanged called for stepIndex={stepIndex}");

            var step = manager.CurrentStep;
            if (step == null)
            {
                Debug.LogError("[TutorialUIController] CurrentStep is null!");
                return;
            }

            Debug.Log($"[TutorialUIController] Step: id={step.stepId}, title={step.title}");

            // Ensure dialog panel is visible (may have been hidden by transition overlay)
            if (dialogPanel != null)
            {
                dialogPanel.SetActive(true);
                Debug.Log("[TutorialUIController] Dialog panel set active");
            }

            // Update dialog content
            titleText.text = step.title;
            bodyText.text = step.description;
            stepCounterText.text = $"{stepIndex + 1}/{manager.TotalSteps}";

            // Update button visibility
            bool showContinue = step.triggerType == TutorialTriggerType.ButtonClick;
            continueButton.gameObject.SetActive(showContinue);
            skipButton.gameObject.SetActive(step.allowSkip);

            // Update Continue button interactable state (disabled when a power is active)
            UpdateContinueButtonInteractable();

            // Position dialog on right side if step requests it or if final step
            bool isLastStep = (stepIndex == manager.TotalSteps - 1);
            bool useRightSide = step.dialogOnRight || isLastStep;
            PositionDialog(useRightSide ? DialogPosition.Right : DialogPosition.Left);

            // Update highlights (dimOverlay stays active throughout tutorial)
            UpdateHighlight(step);

            Debug.Log($"[TutorialUIController] OnStepChanged complete. dialogPanel.activeSelf={dialogPanel?.activeSelf}");
        }

        private void OnTutorialCompleted()
        {
            // Restore any highlighted elements before hiding
            RestoreHighlightedElement();

            if (tutorialRoot != null)
            {
                tutorialRoot.SetActive(false);
            }
        }

        private void OnContinueClicked()
        {
            manager.AdvanceStep();
        }

        private void OnSkipClicked()
        {
            manager.SkipTutorial();
        }

        private void OnPowerActivated(HeartPowerType powerType)
        {
            // Disable Continue button while any power is active
            UpdateContinueButtonInteractable();
        }

        private void OnPowerDeactivated(HeartPowerType powerType)
        {
            // Re-enable Continue button when power deactivates (if no other powers active)
            UpdateContinueButtonInteractable();
        }

        /// <summary>
        /// Updates the Continue button interactable state based on whether any power is active.
        /// Only shows the button if the current step requires a ButtonClick trigger AND no power is active.
        /// </summary>
        private void UpdateContinueButtonInteractable()
        {
            if (continueButton == null) return;

            // Only show Continue button if:
            // 1. The current step requires ButtonClick to advance
            // 2. No power is currently active
            bool stepRequiresButton = manager?.CurrentStep?.triggerType == TutorialTriggerType.ButtonClick;
            bool anyPowerActive = heartPowerManager != null && heartPowerManager.IsAnyPowerActive();
            bool showButton = stepRequiresButton && !anyPowerActive;

            continueButton.interactable = showButton;
            continueButton.gameObject.SetActive(showButton);

            // Update button visual feedback
            var colors = continueButton.colors;
            colors.normalColor = showButton ? BUTTON_NORMAL_COLOR : new Color(0.3f, 0.3f, 0.3f, 1f);
            continueButton.colors = colors;
        }

        /// <summary>
        /// Shows or hides a transition overlay during camera transitions.
        /// When shown, displays only the dim overlay without the dialog panel.
        /// </summary>
        /// <param name="show">True to show the transition overlay, false to hide it.</param>
        public void ShowTransitionOverlay(bool show)
        {
            Debug.Log($"[TutorialUIController] ShowTransitionOverlay({show})");

            if (tutorialRoot == null)
            {
                Debug.Log("[TutorialUIController] Creating tutorial UI");
                CreateTutorialUI();
            }

            if (show)
            {
                // Show dim overlay, hide dialog
                tutorialRoot.SetActive(true);
                dimOverlay.SetActive(true);
                dialogPanel.SetActive(false);

                // Hide all highlights
                highlightRing.SetActive(false);
                circularHighlight.SetActive(false);
                arrowIndicator.SetActive(false);
                uiHighlightMask.SetActive(false);
                worldCutoutMask.SetActive(false);

                Debug.Log("[TutorialUIController] Transition overlay shown (dialog hidden)");
            }
            else
            {
                // Hide the transition overlay (dialog will be shown by OnStepChanged)
                dimOverlay.SetActive(false);
                Debug.Log("[TutorialUIController] Transition overlay hidden (dimOverlay off)");
            }
        }

        /// <summary>
        /// Positions the dialog panel on the left or right side of the screen.
        /// </summary>
        private void PositionDialog(DialogPosition position)
        {
            if (dialogPanel == null) return;

            var rect = dialogPanel.GetComponent<RectTransform>();
            if (rect == null) return;

            if (position == DialogPosition.Right)
            {
                // Position in upper-right corner, mirroring the left position
                rect.anchorMin = new Vector2(0.58f, 0.42f); // Left at 58% from screen left
                rect.anchorMax = new Vector2(1f, 1f);       // Right at screen right, top at screen top
                rect.pivot = new Vector2(1f, 1f);           // Pivot at top-right
                rect.offsetMin = new Vector2(37f, -82f);    // Left, Bottom (mirrored from left position)
                rect.offsetMax = new Vector2(-57f, -107f);  // Right, Top (mirrored from left position)
            }
            else
            {
                // Position in upper-left corner (default)
                rect.anchorMin = new Vector2(0f, 0.42f);
                rect.anchorMax = new Vector2(0.42f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.offsetMin = new Vector2(57f, -82f);
                rect.offsetMax = new Vector2(-37f, -107f);
            }
        }

        #endregion

        #region Highlight System

        private void UpdateHighlight(TutorialStep step)
        {
            // Restore any previously highlighted UI elements
            RestoreHighlightedElement();

            // Reset state - hide all highlights (but keep dimOverlay visible)
            highlightRing.SetActive(false);
            circularHighlight.SetActive(false);
            arrowIndicator.SetActive(false);
            uiHighlightMask.SetActive(false);
            worldCutoutMask.SetActive(false);
            isHighlightingWorldPosition = false;

            switch (step.highlightType)
            {
                case TutorialHighlightType.UIElement:
                    // Rectangular UI highlight uses dimOverlay
                    dimOverlay.SetActive(true);
                    HighlightUIElement(step.highlightTargetName);
                    break;

                case TutorialHighlightType.UIElementCircular:
                    // Circular UI element uses worldCutoutMask with cutout at element position
                    dimOverlay.SetActive(false);
                    HighlightUIElementCircular(step.highlightTargetName);
                    break;

                case TutorialHighlightType.WorldPosition:
                    // World position uses worldCutoutMask instead of dimOverlay
                    dimOverlay.SetActive(false);
                    HighlightWorldPosition(step.worldHighlightPosition);
                    break;

                case TutorialHighlightType.FocalPoint:
                    dimOverlay.SetActive(true);
                    HighlightFocalPoint();
                    break;

                case TutorialHighlightType.None:
                default:
                    // No highlight = no overlay, let player see the game clearly
                    dimOverlay.SetActive(false);
                    break;
            }
        }

        private void HighlightUIElement(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return;

            // Search for UI element by name across ALL canvases in the scene
            RectTransform target = null;
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                target = FindUIElementRecursive(c.transform, elementName);
                if (target != null) break;
            }

            if (target == null)
            {
                Debug.LogWarning($"[Tutorial] Could not find UI element: {elementName}");
                return;
            }

            highlightTarget = target;
            currentHighlightedElement = target;

            // dimOverlay is already active (shown throughout tutorial), no need for separate mask

            // Add a Canvas component to the target to raise it above the mask
            // This preserves the element's position/size while changing render order
            var targetCanvas = target.gameObject.GetComponent<Canvas>();
            if (targetCanvas == null)
            {
                targetCanvas = target.gameObject.AddComponent<Canvas>();
            }
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = 201; // Above tutorial canvas (100) but mask will be at 99

            // Need GraphicRaycaster for the element to remain interactive
            if (target.gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                target.gameObject.AddComponent<GraphicRaycaster>();
            }

            highlightRing.SetActive(true);
            // Sorting order handles layering: mask(100) < element(101) < highlight/dialog(102)

            // Force layout rebuild to ensure accurate measurements
            Canvas.ForceUpdateCanvases();

            // Get the actual rendered bounds of the target in screen space
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right

            // Convert world corners to screen points
            Camera cam = null; // null for Screen Space Overlay canvases
            var targetParentCanvas = target.GetComponentInParent<Canvas>();
            if (targetParentCanvas != null && targetParentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = targetParentCanvas.worldCamera;
            }

            // Calculate screen-space bounds
            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            // Calculate center and size in screen space
            Vector2 screenCenter = (screenMin + screenMax) * 0.5f;
            Vector2 screenSize = new Vector2(Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));

            // Convert screen position to local position in tutorial canvas
            var highlightRect = highlightRing.GetComponent<RectTransform>();
            var tutorialRootRect = tutorialRoot.GetComponent<RectTransform>();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tutorialRootRect,
                screenCenter,
                null, // Tutorial canvas is Screen Space Overlay
                out Vector2 localCenter
            );

            // Account for canvas scaler - get the scale factor
            var scaler = canvas.GetComponent<CanvasScaler>();
            float scaleFactor = canvas.scaleFactor;

            // Position and size the highlight
            highlightRect.anchoredPosition = localCenter;
            float padding = 6f;
            highlightRect.sizeDelta = new Vector2(screenSize.x / scaleFactor + padding, screenSize.y / scaleFactor + padding);
        }

        /// <summary>
        /// Restores a previously highlighted UI element by removing the added Canvas.
        /// </summary>
        private void RestoreHighlightedElement()
        {
            if (currentHighlightedElement != null)
            {
                // Remove the Canvas we added (if it was added by us)
                var targetCanvas = currentHighlightedElement.gameObject.GetComponent<Canvas>();
                if (targetCanvas != null && targetCanvas.overrideSorting && targetCanvas.sortingOrder == 201)
                {
                    // Remove GraphicRaycaster we added
                    var raycaster = currentHighlightedElement.gameObject.GetComponent<GraphicRaycaster>();
                    if (raycaster != null)
                    {
                        Destroy(raycaster);
                    }
                    Destroy(targetCanvas);
                }

                currentHighlightedElement = null;
            }

            // Restore siblings that were pushed behind the mask
            foreach (var (obj, addedCanvas) in siblingsPushedBehind)
            {
                if (obj != null)
                {
                    if (addedCanvas != null)
                    {
                        // We added this canvas, so destroy it
                        Destroy(addedCanvas);
                    }
                    else
                    {
                        // We only modified an existing canvas - reset its sorting
                        var existingCanvas = obj.GetComponent<Canvas>();
                        if (existingCanvas != null)
                        {
                            existingCanvas.overrideSorting = false;
                        }
                    }
                }
            }
            siblingsPushedBehind.Clear();
        }

        /// <summary>
        /// Highlights a circular UI element (like power buttons) using a cutout mask.
        /// Other UI elements remain occluded behind the mask.
        /// </summary>
        private void HighlightUIElementCircular(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return;

            // Search for UI element by name across ALL canvases in the scene
            RectTransform target = null;
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                target = FindUIElementRecursive(c.transform, elementName);
                if (target != null) break;
            }

            if (target == null)
            {
                Debug.LogWarning($"[Tutorial] Could not find UI element: {elementName}");
                return;
            }

            highlightTarget = target;
            currentHighlightedElement = target;

            // Push sibling elements BEHIND the mask (sorting order 99, below tutorial canvas at 100)
            // This ensures only the highlighted element appears above the overlay
            Transform parent = target.transform.parent;
            if (parent != null)
            {
                foreach (Transform sibling in parent)
                {
                    if (sibling == target.transform) continue; // Skip the highlighted element

                    // Check if sibling already has a Canvas - if so, don't add another
                    var siblingCanvas = sibling.gameObject.GetComponent<Canvas>();
                    if (siblingCanvas == null)
                    {
                        siblingCanvas = sibling.gameObject.AddComponent<Canvas>();
                        siblingCanvas.overrideSorting = true;
                        siblingCanvas.sortingOrder = 99; // Below tutorial canvas (200) and game UI (100)
                        siblingsPushedBehind.Add((sibling.gameObject, siblingCanvas));
                    }
                    else if (!siblingCanvas.overrideSorting)
                    {
                        // Canvas exists but isn't overriding - make it override at low sort order
                        siblingCanvas.overrideSorting = true;
                        siblingCanvas.sortingOrder = 99;
                        siblingsPushedBehind.Add((sibling.gameObject, null)); // null means we didn't add it, just modified
                    }
                }
            }

            // Raise the target element above the mask
            var targetCanvas = target.gameObject.GetComponent<Canvas>();
            if (targetCanvas == null)
            {
                targetCanvas = target.gameObject.AddComponent<Canvas>();
            }
            targetCanvas.overrideSorting = true;
            targetCanvas.sortingOrder = 201;

            if (target.gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                target.gameObject.AddComponent<GraphicRaycaster>();
            }

            // Force layout rebuild
            Canvas.ForceUpdateCanvases();

            // Get screen position of the element center
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Camera cam = null;
            var targetParentCanvas = target.GetComponentInParent<Canvas>();
            if (targetParentCanvas != null && targetParentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = targetParentCanvas.worldCamera;
            }

            Vector2 screenMin = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 screenMax = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            Vector2 screenCenter = (screenMin + screenMax) * 0.5f;
            Vector2 screenSize = new Vector2(Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));

            // Use the larger dimension as diameter for the circular cutout
            float cutoutRadius = Mathf.Max(screenSize.x, screenSize.y) * 0.55f; // Slightly larger than element

            // Create cutout mask at screen position
            var sprite = CreateCutoutMaskSprite(screenCenter, cutoutRadius);
            worldCutoutImage.sprite = sprite;
            worldCutoutImage.color = Color.white;
            worldCutoutMask.SetActive(true);

            // Position circular highlight ring at the element
            var tutorialRootRect = tutorialRoot.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tutorialRootRect,
                screenCenter,
                null,
                out Vector2 localCenter
            );

            var highlightRect = circularHighlight.GetComponent<RectTransform>();
            highlightRect.anchoredPosition = localCenter;

            // Size the circular highlight to match the cutout
            float scaleFactor = canvas.scaleFactor;
            float highlightSize = (cutoutRadius * 2f) / scaleFactor;
            highlightRect.sizeDelta = new Vector2(highlightSize, highlightSize);

            circularHighlight.SetActive(true);
        }

        private void HighlightWorldPosition(Vector3 worldPos)
        {
            // If position is zero, try to get Heart position
            if (worldPos == Vector3.zero)
            {
                var heart = FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
                if (heart != null)
                {
                    worldPos = heart.transform.position;
                }
            }

            worldHighlightPosition = worldPos;
            worldHighlightRadius = 80f; // Radius for Heart cutout
            isHighlightingWorldPosition = true;

            // Use circular highlight for world objects
            var highlightRect = circularHighlight.GetComponent<RectTransform>();
            highlightRect.sizeDelta = new Vector2(160f, 160f); // Size for Heart highlight ring

            circularHighlight.SetActive(true);

            // Show cutout mask and update position
            ShowWorldCutoutMask();
            UpdateWorldHighlightPosition();
        }

        private void HighlightFocalPoint()
        {
            // The focal point indicator is always at screen center
            // Don't use the cutout mask - just highlight screen center with the circular ring
            // The focal point effect (spiral bolts) may be occluded by terrain, but the ring shows where it is

            // Use circular highlight for focal point, positioned at screen center
            var highlightRect = circularHighlight.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.anchoredPosition = Vector2.zero;
            highlightRect.sizeDelta = new Vector2(120f, 120f);

            circularHighlight.SetActive(true);

            // Don't track world position - focal point highlight stays at screen center
            isHighlightingWorldPosition = false;
        }

        /// <summary>
        /// Shows the world cutout mask with a hole at the current world highlight position.
        /// </summary>
        private void ShowWorldCutoutMask()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldHighlightPosition);
            if (screenPos.z < 0) return; // Behind camera

            // Create mask with cutout at screen position
            var sprite = CreateCutoutMaskSprite(new Vector2(screenPos.x, screenPos.y), worldHighlightRadius);
            worldCutoutImage.sprite = sprite;
            worldCutoutImage.color = Color.white; // Sprite contains the color
            worldCutoutMask.SetActive(true);
        }

        /// <summary>
        /// Shows the world cutout mask with a hole at screen center (for focal point).
        /// </summary>
        private void ShowWorldCutoutMaskAtScreenCenter()
        {
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

            // Create mask with cutout at screen center
            var sprite = CreateCutoutMaskSprite(screenCenter, worldHighlightRadius);
            worldCutoutImage.sprite = sprite;
            worldCutoutImage.color = Color.white;
            worldCutoutMask.SetActive(true);

            // Position circular highlight at screen center
            var highlightRect = circularHighlight.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.anchoredPosition = Vector2.zero;
        }

        private void UpdateWorldHighlightPosition()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldHighlightPosition);

            // Check if position is in front of camera
            if (screenPos.z < 0)
            {
                circularHighlight.SetActive(false);
                arrowIndicator.SetActive(false);
                return;
            }

            // Convert to canvas space
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                tutorialRoot.GetComponent<RectTransform>(),
                screenPos,
                null,
                out Vector2 localPoint
            );

            // Update circular highlight position (used for world objects)
            if (circularHighlight.activeSelf)
            {
                var highlightRect = circularHighlight.GetComponent<RectTransform>();
                highlightRect.anchoredPosition = localPoint;
            }

            if (arrowIndicator.activeSelf)
            {
                var arrowRect = arrowIndicator.GetComponent<RectTransform>();
                arrowRect.anchoredPosition = localPoint + new Vector2(0, 50);
            }
        }

        private void UpdateArrowIndicator()
        {
            // Animate arrow bobbing
            if (arrowIndicator == null) return;

            float bob = Mathf.Sin(Time.unscaledTime * 4f) * 5f;
            var rect = arrowIndicator.GetComponent<RectTransform>();
            var pos = rect.anchoredPosition;
            pos.y += bob * Time.unscaledDeltaTime;
        }

        private RectTransform FindUIElementRecursive(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent.GetComponent<RectTransform>();
            }

            foreach (Transform child in parent)
            {
                var result = FindUIElementRecursive(child, name);
                if (result != null) return result;
            }

            return null;
        }

        #endregion
    }
}

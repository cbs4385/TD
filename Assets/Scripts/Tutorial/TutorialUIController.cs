using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Cameras;
using FaeMaze.Maze;

namespace FaeMaze.Tutorial
{
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
        private static readonly Color DIM_OVERLAY_COLOR = new Color(0f, 0f, 0f, 0.6f);

        // Sizes
        private const float DIALOG_WIDTH = 600f;
        private const float DIALOG_MIN_HEIGHT = 200f;
        private const float TITLE_FONT_SIZE = 28f;
        private const float BODY_FONT_SIZE = 20f;
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
        private GameObject highlightRing;
        private GameObject arrowIndicator;

        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI stepCounterText;
        private Button continueButton;
        private Button skipButton;

        private TutorialManager manager;
        private RectTransform highlightTarget;
        private bool isHighlightingWorldPosition;
        private Vector3 worldHighlightPosition;

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
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnTutorialStarted -= OnTutorialStarted;
                manager.OnStepChanged -= OnStepChanged;
                manager.OnTutorialCompleted -= OnTutorialCompleted;
            }
        }

        private void Update()
        {
            // Update highlight ring pulse animation
            if (highlightRing != null && highlightRing.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * HIGHLIGHT_PULSE_SPEED) * HIGHLIGHT_PULSE_SCALE;
                highlightRing.transform.localScale = Vector3.one * pulse;

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

            // Create highlight ring
            CreateHighlightRing();

            // Create arrow indicator
            CreateArrowIndicator();

            // Create dialog panel
            CreateDialogPanel();
        }

        private Canvas FindOrCreateCanvas()
        {
            // Try to find existing game canvas
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return c;
                }
            }

            // Create new canvas
            var canvasGO = new GameObject("TutorialCanvas");
            var newCanvas = canvasGO.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 100; // Above other UI

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

        private void CreateHighlightRing()
        {
            highlightRing = new GameObject("HighlightRing");
            highlightRing.transform.SetParent(tutorialRoot.transform, false);

            var rect = highlightRing.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(HIGHLIGHT_RING_SIZE, HIGHLIGHT_RING_SIZE);

            // Create ring using UI Image with circular sprite
            var image = highlightRing.AddComponent<Image>();
            image.color = HIGHLIGHT_COLOR;
            image.raycastTarget = false;

            // Create circle sprite programmatically
            image.sprite = CreateCircleSprite(64, 8);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;

            // Add outline effect via second ring
            var outerRing = new GameObject("OuterRing");
            outerRing.transform.SetParent(highlightRing.transform, false);
            var outerRect = outerRing.AddComponent<RectTransform>();
            outerRect.anchorMin = Vector2.zero;
            outerRect.anchorMax = Vector2.one;
            outerRect.offsetMin = new Vector2(-10, -10);
            outerRect.offsetMax = new Vector2(10, 10);

            var outerImage = outerRing.AddComponent<Image>();
            outerImage.color = new Color(HIGHLIGHT_COLOR.r, HIGHLIGHT_COLOR.g, HIGHLIGHT_COLOR.b, 0.3f);
            outerImage.sprite = CreateCircleSprite(64, 4);
            outerImage.raycastTarget = false;

            highlightRing.SetActive(false);
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
            rect.anchorMin = new Vector2(0.5f, 0.15f);
            rect.anchorMax = new Vector2(0.5f, 0.15f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(DIALOG_WIDTH, DIALOG_MIN_HEIGHT);

            // Background
            var bg = dialogPanel.AddComponent<Image>();
            bg.color = DIALOG_BG_COLOR;

            // Border (using outline)
            var outline = dialogPanel.AddComponent<Outline>();
            outline.effectColor = DIALOG_BORDER_COLOR;
            outline.effectDistance = new Vector2(2, 2);

            // Vertical layout
            var layout = dialogPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)PADDING, (int)PADDING, (int)PADDING, (int)PADDING);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = dialogPanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
            titleLayout.minHeight = 40f;

            // Body text
            var bodyGO = new GameObject("Body");
            bodyGO.transform.SetParent(dialogPanel.transform, false);
            bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            bodyText.text = "";
            bodyText.fontSize = BODY_FONT_SIZE;
            bodyText.color = TEXT_COLOR;
            bodyText.alignment = TextAlignmentOptions.Left;

            var bodyLayout = bodyGO.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 60f;
            bodyLayout.flexibleHeight = 1f;

            // Button container
            var buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(dialogPanel.transform, false);
            var buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = false;
            buttonLayout.childControlHeight = false;

            var buttonContainerLayout = buttonContainer.AddComponent<LayoutElement>();
            buttonContainerLayout.minHeight = BUTTON_HEIGHT + 10f;

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
            stepCounterText.fontSize = 16f;
            stepCounterText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            stepCounterText.alignment = TextAlignmentOptions.Right;

            var counterLayout = counterGO.AddComponent<LayoutElement>();
            counterLayout.minHeight = 20f;

            dialogPanel.SetActive(false);
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonGO = new GameObject(label + "Button");
            buttonGO.transform.SetParent(parent, false);

            var rect = buttonGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, BUTTON_HEIGHT);

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

        /// <summary>
        /// Creates a circle ring sprite programmatically.
        /// </summary>
        private Sprite CreateCircleSprite(int size, int thickness)
        {
            var texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            var pixels = new Color[size * size];
            float center = size / 2f;
            float outerRadius = size / 2f - 1;
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
                        if (dist > outerRadius - 1) alpha = outerRadius - dist;
                        if (dist < innerRadius + 1) alpha = Mathf.Min(alpha, dist - innerRadius);
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

        #endregion

        #region Event Handlers

        private void OnTutorialStarted()
        {
            if (tutorialRoot == null)
            {
                CreateTutorialUI();
            }

            tutorialRoot.SetActive(true);
            dialogPanel.SetActive(true);
        }

        private void OnStepChanged(int stepIndex)
        {
            var step = manager.CurrentStep;
            if (step == null) return;

            // Update dialog content
            titleText.text = step.title;
            bodyText.text = step.description;
            stepCounterText.text = $"{stepIndex + 1}/{manager.TotalSteps}";

            // Update button visibility
            bool showContinue = step.triggerType == TutorialTriggerType.ButtonClick;
            continueButton.gameObject.SetActive(showContinue);
            skipButton.gameObject.SetActive(step.allowSkip);

            // Update dim overlay
            dimOverlay.SetActive(step.pauseGame);

            // Update highlights
            UpdateHighlight(step);
        }

        private void OnTutorialCompleted()
        {
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

        #endregion

        #region Highlight System

        private void UpdateHighlight(TutorialStep step)
        {
            // Reset state
            highlightRing.SetActive(false);
            arrowIndicator.SetActive(false);
            isHighlightingWorldPosition = false;

            switch (step.highlightType)
            {
                case TutorialHighlightType.UIElement:
                    HighlightUIElement(step.highlightTargetName);
                    break;

                case TutorialHighlightType.WorldPosition:
                    HighlightWorldPosition(step.worldHighlightPosition);
                    break;

                case TutorialHighlightType.FocalPoint:
                    HighlightFocalPoint();
                    break;

                case TutorialHighlightType.None:
                default:
                    break;
            }
        }

        private void HighlightUIElement(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return;

            // Search for UI element by name
            var target = FindUIElementRecursive(canvas.transform, elementName);
            if (target == null)
            {
                Debug.LogWarning($"[Tutorial] Could not find UI element: {elementName}");
                return;
            }

            highlightTarget = target;
            highlightRing.SetActive(true);

            // Position highlight over target
            var highlightRect = highlightRing.GetComponent<RectTransform>();
            highlightRect.position = target.position;

            // Size to match target (with padding)
            float maxDim = Mathf.Max(target.rect.width, target.rect.height) + 40f;
            highlightRect.sizeDelta = new Vector2(maxDim, maxDim);
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
            isHighlightingWorldPosition = true;

            arrowIndicator.SetActive(true);
            UpdateWorldHighlightPosition();
        }

        private void HighlightFocalPoint()
        {
            // Find the focal point indicator in the scene
            var focalGlow = FindFirstObjectByType<FocalPointGlow>();
            if (focalGlow != null)
            {
                worldHighlightPosition = focalGlow.transform.position;
                isHighlightingWorldPosition = true;
                highlightRing.SetActive(true);
                UpdateWorldHighlightPosition();
            }
            else
            {
                // Fallback: highlight screen center
                var highlightRect = highlightRing.GetComponent<RectTransform>();
                highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
                highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
                highlightRect.anchoredPosition = Vector2.zero;
                highlightRing.SetActive(true);
            }
        }

        private void UpdateWorldHighlightPosition()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldHighlightPosition);

            // Check if position is in front of camera
            if (screenPos.z < 0)
            {
                highlightRing.SetActive(false);
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

            if (highlightRing.activeSelf)
            {
                var highlightRect = highlightRing.GetComponent<RectTransform>();
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

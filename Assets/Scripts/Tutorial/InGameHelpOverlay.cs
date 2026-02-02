using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Quick-reference help overlay accessible via F1 during gameplay.
    /// Shows controls, powers, and prop effects without pausing the game.
    /// </summary>
    public class InGameHelpOverlay : MonoBehaviour
    {
        #region Constants

        private static readonly Color OVERLAY_BG_COLOR = new Color(0.05f, 0.05f, 0.1f, 0.92f);
        private static readonly Color HEADER_COLOR = new Color(1f, 0.85f, 0.3f, 1f); // Gold
        private static readonly Color SUBHEADER_COLOR = new Color(0.7f, 0.85f, 1f, 1f); // Light blue
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color DIM_TEXT_COLOR = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color KEY_COLOR = new Color(0.4f, 0.9f, 0.5f, 1f); // Green for keys

        private const float OVERLAY_WIDTH_PERCENT = 0.85f;
        private const float OVERLAY_HEIGHT_PERCENT = 0.85f;
        private const float HEADER_SIZE = 32f;
        private const float SUBHEADER_SIZE = 24f;
        private const float BODY_SIZE = 18f;
        private const float PADDING = 30f;
        private const float COLUMN_SPACING = 60f;

        #endregion

        #region Singleton

        private static InGameHelpOverlay _instance;
        public static InGameHelpOverlay Instance => _instance;

        #endregion

        #region Private Fields

        private Canvas canvas;
        private GameObject overlayRoot;
        private bool isVisible;

        #endregion

        #region Properties

        public bool IsVisible => isVisible;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles the help overlay visibility.
        /// </summary>
        public void Toggle()
        {
            if (isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// Shows the help overlay.
        /// </summary>
        public void Show()
        {
            if (overlayRoot == null)
            {
                CreateOverlay();
            }

            overlayRoot.SetActive(true);
            isVisible = true;
        }

        /// <summary>
        /// Hides the help overlay.
        /// </summary>
        public void Hide()
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
            isVisible = false;
        }

        #endregion

        #region UI Creation

        private void CreateOverlay()
        {
            // Find or create canvas
            canvas = FindOrCreateCanvas();

            // Create overlay root
            overlayRoot = new GameObject("HelpOverlay");
            overlayRoot.transform.SetParent(canvas.transform, false);

            var rootRect = overlayRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Click-to-close background
            var bgButton = overlayRoot.AddComponent<Button>();
            bgButton.onClick.AddListener(Hide);
            var bgImage = overlayRoot.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);

            // Content panel (centered, sized to percentage of screen)
            var contentPanel = new GameObject("ContentPanel");
            contentPanel.transform.SetParent(overlayRoot.transform, false);

            var contentRect = contentPanel.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(
                Screen.width * OVERLAY_WIDTH_PERCENT,
                Screen.height * OVERLAY_HEIGHT_PERCENT
            );

            var contentBg = contentPanel.AddComponent<Image>();
            contentBg.color = OVERLAY_BG_COLOR;

            // Add outline
            var outline = contentPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, 2);

            // Create content layout
            CreateHelpContent(contentPanel.transform);

            // Close instruction at bottom
            CreateCloseInstruction(contentPanel.transform);

            overlayRoot.SetActive(false);
        }

        private Canvas FindOrCreateCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.sortingOrder >= 100)
                {
                    return c;
                }
            }

            // Create high-priority canvas for help overlay
            var canvasGO = new GameObject("HelpOverlayCanvas");
            var newCanvas = canvasGO.AddComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 150;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            return newCanvas;
        }

        private void CreateHelpContent(Transform parent)
        {
            // Main title
            CreateText(parent, "FAEMAZE QUICK REFERENCE", HEADER_SIZE, HEADER_COLOR,
                new Vector2(0, -PADDING), TextAnchor.UpperCenter, fontStyle: FontStyles.Bold);

            // Two-column layout
            float leftX = -350f;
            float rightX = 150f;
            float startY = -80f;
            float lineHeight = 26f;
            float sectionSpacing = 40f;

            // LEFT COLUMN: Controls
            float y = startY;

            y = CreateSection(parent, "CAMERA CONTROLS", leftX, y, new string[]
            {
                $"<color=#{ColorToHex(KEY_COLOR)}>W/A/S/D</color> or <color=#{ColorToHex(KEY_COLOR)}>Arrow Keys</color>  Move camera",
                $"<color=#{ColorToHex(KEY_COLOR)}>Scroll Wheel</color>  Zoom in/out",
                $"<color=#{ColorToHex(KEY_COLOR)}>Right-click + Drag</color>  Orbit around focal point",
                $"<color=#{ColorToHex(KEY_COLOR)}>Middle-click + Drag</color>  Pan camera",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.CameraFocusHeartBinding)}</color>  Focus on Heart",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.CameraFocusEntranceBinding)}</color>  Focus on Entrance",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.CameraFocusVisitorBinding)}</color>  Focus on Visitor"
            }, lineHeight);

            y -= sectionSpacing;

            y = CreateSection(parent, "HEART POWERS", leftX, y, new string[]
            {
                $"<color=#{ColorToHex(KEY_COLOR)}>1</color>  Spooky Fog (100 threads)",
                "    Creates fog that confuses visitors toward Heart",
                $"<color=#{ColorToHex(KEY_COLOR)}>2</color>  Yoink! (10 threads)",
                "    Tongue grabs visitors from walls",
                $"<color=#{ColorToHex(KEY_COLOR)}>3</color>  Nom Nom (50 threads)",
                "    Mouth consumes visitors at focal point",
                $"<color=#{ColorToHex(KEY_COLOR)}>4</color>  Redecorating (free)",
                "    Open menu to place/change props"
            }, lineHeight);

            // RIGHT COLUMN: Props and Gameplay
            y = startY;

            y = CreateSection(parent, "SCULPT MENU", rightX, y, new string[]
            {
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.SculptPondBinding)}</color>  Place Pond",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.SculptLanternBinding)}</color>  Place Lantern",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.SculptRingBinding)}</color>  Place Fairy Ring",
                $"<color=#{ColorToHex(KEY_COLOR)}>{GetBinding(GameSettings.SculptRemoveBinding)}</color>  Remove Prop",
                "<color=#888888>Click center to cancel</color>"
            }, lineHeight);

            y -= sectionSpacing;

            y = CreateSection(parent, "PROPS", rightX, y, new string[]
            {
                "<color=#4488FF>Pond</color>  Drowns visitors (Puka hazard)",
                "<color=#FFDD44>Lantern</color>  Fascinates, drains threads over time",
                "<color=#88FF88>Fairy Ring</color>  Traps visitors, drains threads"
            }, lineHeight);

            y -= sectionSpacing;

            y = CreateSection(parent, "GOAL", rightX, y, new string[]
            {
                "Capture visitors before they escape!",
                "Threads drain over time - survive as long as possible.",
                "The Heart's tongue catches nearby visitors.",
                "Use powers to guide or consume visitors.",
                "",
                "<color=#FF6666>Goblin</color> appears when threads are high - kills visitors!"
            }, lineHeight);
        }

        private float CreateSection(Transform parent, string header, float x, float y,
            string[] lines, float lineHeight)
        {
            // Section header
            CreateText(parent, header, SUBHEADER_SIZE, SUBHEADER_COLOR,
                new Vector2(x, y), TextAnchor.UpperLeft, fontStyle: FontStyles.Bold);

            y -= lineHeight + 5f;

            // Section content
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    CreateRichText(parent, line, BODY_SIZE, TEXT_COLOR,
                        new Vector2(x + 10, y), TextAnchor.UpperLeft);
                }
                y -= lineHeight;
            }

            return y;
        }

        private void CreateText(Transform parent, string text, float fontSize, Color color,
            Vector2 position, TextAnchor anchor, FontStyles fontStyle = FontStyles.Normal)
        {
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600, fontSize + 10);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = fontStyle;
            tmp.alignment = AnchorToAlignment(anchor);
            tmp.raycastTarget = false;
        }

        private void CreateRichText(Transform parent, string text, float fontSize, Color color,
            Vector2 position, TextAnchor anchor)
        {
            var textGO = new GameObject("RichText");
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(450, fontSize + 10);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.richText = true;
            tmp.alignment = AnchorToAlignment(anchor);
            tmp.raycastTarget = false;
        }

        private void CreateCloseInstruction(Transform parent)
        {
            var textGO = new GameObject("CloseInstruction");
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, PADDING);
            rect.sizeDelta = new Vector2(400, 30);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Press F1 or click anywhere to close";
            tmp.fontSize = 18f;
            tmp.color = DIM_TEXT_COLOR;
            tmp.fontStyle = FontStyles.Italic;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        #endregion

        #region Helpers

        private TextAlignmentOptions AnchorToAlignment(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.TopLeft
            };
        }

        private string ColorToHex(Color color)
        {
            return ColorUtility.ToHtmlStringRGB(color);
        }

        private string GetBinding(string binding)
        {
            if (string.IsNullOrEmpty(binding)) return "?";

            // Clean up binding display
            if (binding.StartsWith("Alpha"))
                return binding.Substring(5);
            if (binding.StartsWith("Mouse"))
                return "Mouse " + binding.Substring(5);

            return binding;
        }

        #endregion
    }
}

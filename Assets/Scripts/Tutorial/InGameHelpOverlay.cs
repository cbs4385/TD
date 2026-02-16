using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.UI;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Quick-reference help overlay accessible via F1 during gameplay.
    /// Shows controls, powers, and prop effects without pausing the game.
    /// Rebuilds content each time it's shown so bindings are always current.
    /// </summary>
    public class InGameHelpOverlay : SingletonMonoBehaviour<InGameHelpOverlay>
    {
        #region Constants

        private static readonly Color OVERLAY_BG_COLOR = new Color(0.05f, 0.05f, 0.1f, 0.92f);
        private static readonly Color HEADER_COLOR = new Color(1f, 0.85f, 0.3f, 1f); // Gold
        private static readonly Color SUBHEADER_COLOR = new Color(0.7f, 0.85f, 1f, 1f); // Light blue
        private static readonly Color TEXT_COLOR = Color.white;
        private static readonly Color DIM_TEXT_COLOR = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color KEY_COLOR = new Color(0.4f, 0.9f, 0.5f, 1f); // Green for keys

        private const float HEADER_SIZE = 26f;
        private const float SUBHEADER_SIZE = 18f;
        private const float BODY_SIZE = 14f;
        private const float PADDING = 16f;
        private const float LINE_HEIGHT = 18f;
        private const float SECTION_SPACING = 8f;

        #endregion

        #region Private Fields

        private Canvas canvas;
        private GameObject overlayRoot;
        private GameObject contentContainer; // Destroyed and rebuilt each Show()
        private bool isVisible;

        #endregion

        #region Properties

        public bool IsVisible => isVisible;

        #endregion

        #region Unity Lifecycle

        // Singleton lifecycle handled by base class

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
        /// Shows the help overlay. Rebuilds content each time to reflect current bindings.
        /// </summary>
        public void Show()
        {
            if (overlayRoot == null)
            {
                CreateOverlayRoot();
            }

            // Destroy old content and rebuild with current bindings
            if (contentContainer != null)
            {
                Destroy(contentContainer);
            }
            CreateContent();

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

        private void CreateOverlayRoot()
        {
            // Find or create canvas
            canvas = FindOrCreateCanvas();

            // Create overlay root (persistent - not destroyed between shows)
            overlayRoot = new GameObject("HelpOverlay");
            overlayRoot.transform.SetParent(canvas.transform, false);

            var rootRect = overlayRoot.AddComponent<RectTransform>();
            UIFactory.StretchToFillParent(rootRect);

            // Click-to-close background
            var bgButton = overlayRoot.AddComponent<Button>();
            bgButton.onClick.AddListener(Hide);
            var bgImage = overlayRoot.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);

            overlayRoot.SetActive(false);
        }

        private void CreateContent()
        {
            // Content panel (centered, using anchor-based sizing for proper scaling)
            contentContainer = new GameObject("ContentPanel");
            contentContainer.transform.SetParent(overlayRoot.transform, false);

            var contentRect = contentContainer.AddComponent<RectTransform>();
            // Use anchors for responsive sizing (10% margin on each side)
            contentRect.anchorMin = new Vector2(0.08f, 0.05f);
            contentRect.anchorMax = new Vector2(0.92f, 0.95f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var contentBg = contentContainer.AddComponent<Image>();
            contentBg.color = OVERLAY_BG_COLOR;

            // Add outline
            var outline = contentContainer.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, 2);

            // Create content layout with current bindings
            CreateHelpContent(contentContainer.transform);

            // Close instruction at bottom
            CreateCloseInstruction(contentContainer.transform);
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
            var newCanvas = UIFactory.CreateOverlayCanvas("HelpOverlayCanvas", 150, new Vector2(1920, 1080));
            var scaler = newCanvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 0.5f;

            return newCanvas;
        }

        private void CreateHelpContent(Transform parent)
        {
            string kc = ColorToHex(KEY_COLOR);

            // Main title
            CreateText(parent, "FAEMAZE QUICK REFERENCE", HEADER_SIZE, HEADER_COLOR,
                new Vector2(0, -PADDING), TextAnchor.UpperCenter, fontStyle: FontStyles.Bold);

            // Three-column layout for better fit
            float col1X = -420f;
            float col2X = -50f;
            float col3X = 260f;
            float startY = -50f;

            // COLUMN 1: Camera Controls + Heart Powers
            float y = startY;

            string moveForward = Bind(GameSettings.CameraMoveForwardBinding);
            string moveBack = Bind(GameSettings.CameraMoveBackwardBinding);
            string strafeLeft = Bind(GameSettings.CameraStrafeLeftBinding);
            string strafeRight = Bind(GameSettings.CameraStrafeRightBinding);
            string rotateLeft = Bind(GameSettings.CameraTurnLeftBinding);
            string rotateRight = Bind(GameSettings.CameraTurnRightBinding);
            string angleUp = Bind(GameSettings.CameraAngleUpBinding);
            string angleDown = Bind(GameSettings.CameraAngleDownBinding);
            string orbit = Bind(GameSettings.CameraOrbitBinding);
            string pan = Bind(GameSettings.CameraPanBinding);
            string focusHeart = Bind(GameSettings.CameraFocusHeartBinding);
            string focusEntrance = Bind(GameSettings.CameraFocusEntranceBinding);
            string focusVisitor = Bind(GameSettings.CameraFocusVisitorBinding);

            y = CreateSection(parent, "CAMERA", col1X, y, new string[]
            {
                $"<color=#{kc}>{moveForward}/{moveBack}</color>  Forward/Back",
                $"<color=#{kc}>{strafeLeft}/{strafeRight}</color>  Strafe",
                $"<color=#{kc}>{rotateLeft}/{rotateRight}</color>  Rotate",
                $"<color=#{kc}>Scroll Wheel</color>  Zoom",
                $"<color=#{kc}>{angleUp}/{angleDown}</color>  View Angle",
                $"<color=#{kc}>{orbit} + Drag</color>  Orbit",
                $"<color=#{kc}>{pan} + Drag</color>  Pan",
                $"<color=#{kc}>{focusHeart}</color>  Focus Heart",
                $"<color=#{kc}>{focusEntrance}</color>  Focus Entrance",
                $"<color=#{kc}>{focusVisitor}</color>  Focus Visitor"
            });

            y -= SECTION_SPACING;

            string p1 = Bind(GameSettings.HeartPower1Binding);
            string p2 = Bind(GameSettings.HeartPower2Binding);
            string p3 = Bind(GameSettings.HeartPower3Binding);
            string p4 = Bind(GameSettings.HeartPower4Binding);
            string p5 = Bind(GameSettings.HeartPower5Binding);

            y = CreateSection(parent, "HEART POWERS", col1X, y, new string[]
            {
                $"<color=#{kc}>{p1}</color>  Spooky Fog — confuse toward Heart",
                $"<color=#{kc}>{p2}</color>  Yoink! — tongue grabs from walls",
                $"<color=#{kc}>{p3}</color>  Nom Nom — consume at focal point",
                $"<color=#{kc}>{p4}</color>  Redecorating — place/change hazards",
                $"<color=#{kc}>{p5}</color>  Misdirect — lure down chosen edge"
            });

            // COLUMN 2: Sculpt Menu + Hazards
            y = startY;

            string sculptPond = Bind(GameSettings.SculptPondBinding);
            string sculptLantern = Bind(GameSettings.SculptLanternBinding);
            string sculptRing = Bind(GameSettings.SculptRingBinding);
            string sculptRemove = Bind(GameSettings.SculptRemoveBinding);

            y = CreateSection(parent, "SCULPT MENU", col2X, y, new string[]
            {
                $"<color=#{kc}>{sculptPond}</color>  Place Pond",
                $"<color=#{kc}>{sculptLantern}</color>  Place Lantern",
                $"<color=#{kc}>{sculptRing}</color>  Place Fairy Ring",
                $"<color=#{kc}>{sculptRemove}</color>  Remove Hazard",
                "<color=#888888>Click center to cancel</color>"
            });

            y -= SECTION_SPACING;

            y = CreateSection(parent, "HAZARDS", col2X, y, new string[]
            {
                "<color=#4488FF>Pond</color>  Drowns visitors (Puka)",
                "<color=#FFDD44>Lantern</color>  Fascinates, drains threads",
                "<color=#88FF88>Fairy Ring</color>  Traps, drains threads"
            });

            // COLUMN 3: Goal + Tips
            y = startY;

            y = CreateSection(parent, "GOAL", col3X, y, new string[]
            {
                "Capture visitors before escape!",
                "Threads drain over time.",
                "Heart's tongue catches nearby.",
                "Use powers to guide or consume.",
                "",
                "<color=#FF6666>Goblin</color> appears at high threads!"
            });

            y -= SECTION_SPACING;

            y = CreateSection(parent, "TIPS", col3X, y, new string[]
            {
                "Position focal point wisely",
                "Combine powers for effect",
                "Watch thread drain rate",
                "Place hazards on busy paths"
            });
        }

        private float CreateSection(Transform parent, string header, float x, float y,
            string[] lines)
        {
            // Section header
            CreateText(parent, header, SUBHEADER_SIZE, SUBHEADER_COLOR,
                new Vector2(x, y), TextAnchor.UpperLeft, fontStyle: FontStyles.Bold);

            y -= LINE_HEIGHT + 2f;

            // Section content
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    CreateRichText(parent, line, BODY_SIZE, TEXT_COLOR,
                        new Vector2(x + 8, y), TextAnchor.UpperLeft);
                }
                y -= LINE_HEIGHT;
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
            rect.sizeDelta = new Vector2(800, fontSize + 10);

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
            rect.sizeDelta = new Vector2(380, fontSize + 10);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.richText = true;
            tmp.alignment = AnchorToAlignment(anchor);
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Truncate;
        }

        private void CreateCloseInstruction(Transform parent)
        {
            var textGO = new GameObject("CloseInstruction");
            textGO.transform.SetParent(parent, false);

            var rect = textGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0, 10);
            rect.sizeDelta = new Vector2(400, 30);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Press F1 or click anywhere to close";
            tmp.fontSize = 16f;
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

        /// <summary>
        /// Get the display name for a binding using InputBindingHelper.
        /// </summary>
        private string Bind(string binding)
        {
            return InputBindingHelper.GetDisplayName(binding);
        }

        #endregion
    }
}

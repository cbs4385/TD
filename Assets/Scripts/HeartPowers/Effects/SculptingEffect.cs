using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Roguelike;
using ForestMaze;

namespace FaeMaze.HeartPowers
{
    #region Sculpting

    /// <summary>
    /// Sculpting power allows the player to change the prop type of a node.
    /// When activated on a node, presents a circular menu with options:
    /// - Center: Cancel (red)
    /// - Top: Remove prop (earth texture color)
    /// - Left: Pond
    /// - Bottom: Fae Lantern
    /// - Right: Fairy Ring
    /// Only works when focal point is on a node, not an edge.
    /// </summary>
    public class SculptingEffect : ActivePowerEffect
    {
        // Static instance for tutorial access
        public static SculptingEffect ActiveInstance { get; private set; }

        // Menu state
        private bool menuActive = false;

        /// <summary>
        /// Returns true if the sculpt menu is currently open.
        /// </summary>
        public bool IsMenuActive => menuActive;
        private int targetNodeIndex = -1;
        private Vector3 menuPosition;

        // UI elements
        private GameObject menuContainer;
        private Canvas menuCanvas;
        private UnityEngine.UI.Button centerButton;
        private UnityEngine.UI.Button topButton;
        private UnityEngine.UI.Button leftButton;
        private UnityEngine.UI.Button bottomButton;
        private UnityEngine.UI.Button rightButton;

        // Visual constants - proportions relative to menu size
        private const float MENU_SCREEN_HEIGHT_FRACTION = 0.5f;  // Menu is 50% of screen height
        private const float BUTTON_SIZE_FRACTION = 0.30f;        // Buttons are 30% of menu size
        private const float CENTER_BUTTON_FRACTION = 0.22f;      // Center button is 22% of menu size
        private const float MENU_RADIUS_FRACTION = 0.33f;        // Button positions at 33% from center

        // Colors for button backgrounds
        private static readonly Color CancelColor = new Color(0.7f, 0.15f, 0.15f, 1f);     // Red
        private static readonly Color RemoveColor = new Color(0.45f, 0.35f, 0.25f, 1f);    // Earth brown
        private static readonly Color PondColor = new Color(0.2f, 0.35f, 0.7f, 1f);        // Blue water
        private static readonly Color LanternColor = new Color(0.85f, 0.65f, 0.15f, 1f);   // Golden
        private static readonly Color RingColor = new Color(0.55f, 0.2f, 0.7f, 1f);        // Purple

        // Reference to DynamicMazeGrowth for prop manipulation
        private DynamicMazeGrowth dynamicMazeGrowth;

        // Track if we've applied an action
        private bool actionApplied = false;

        // Tutorial mode - only allow lantern selection
        private bool tutorialLanternOnlyMode = false;

        public SculptingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        /// <summary>
        /// Override IsExpired - expires when action is applied or menu is cancelled
        /// </summary>
        public override bool IsExpired => actionApplied;

        /// <summary>
        /// Highlights only the lantern button and disables all other buttons.
        /// Used by the tutorial to guide the player to select the lantern.
        /// Also disables keyboard shortcuts for non-lantern options.
        /// </summary>
        public void HighlightLanternButtonOnly()
        {
            if (!menuActive) return;

            // Enable tutorial mode - blocks non-lantern keyboard shortcuts
            tutorialLanternOnlyMode = true;

            // Visually dim and disable all buttons except lantern (bottomButton)
            DimAndDisableButton(centerButton);
            DimAndDisableButton(topButton);
            DimAndDisableButton(leftButton);
            DimAndDisableButton(rightButton);

            // Keep lantern button enabled and add a pulsing highlight effect
            if (bottomButton != null)
            {
                bottomButton.interactable = true;

                // Add a pulsing glow effect to the lantern button
                var buttonImage = bottomButton.GetComponent<UnityEngine.UI.Image>();
                if (buttonImage != null)
                {
                    // Create a coroutine host for the pulse effect
                    var pulseHost = bottomButton.gameObject.AddComponent<ButtonPulseEffect>();
                    pulseHost.StartPulse(buttonImage, LanternColor);
                }
            }
        }

        /// <summary>
        /// Dims a button visually and disables interaction.
        /// </summary>
        private void DimAndDisableButton(UnityEngine.UI.Button button)
        {
            if (button == null) return;

            button.interactable = false;

            // Visually dim the button by reducing alpha/brightness
            var bgImage = button.GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                Color dimColor = bgImage.color;
                dimColor.a = 0.3f; // Reduce opacity significantly
                dimColor.r *= 0.5f;
                dimColor.g *= 0.5f;
                dimColor.b *= 0.5f;
                bgImage.color = dimColor;
            }

            // Also dim the border (parent object)
            var borderImage = button.transform.parent?.GetComponent<UnityEngine.UI.Image>();
            if (borderImage != null)
            {
                Color dimBorderColor = borderImage.color;
                dimBorderColor.a = 0.3f;
                borderImage.color = dimBorderColor;
            }

            // Dim content image if present
            var contentImage = button.transform.Find("Content")?.GetComponent<UnityEngine.UI.Image>();
            if (contentImage != null)
            {
                Color dimContentColor = contentImage.color;
                dimContentColor.a = 0.3f;
                contentImage.color = dimContentColor;
            }
        }

        public override void OnStart()
        {
            // Set static instance for tutorial access
            ActiveInstance = this;

            // Find DynamicMazeGrowth
            dynamicMazeGrowth = Object.FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth == null)
            {
                actionApplied = true;
                return;
            }

            // Check if target position is on a node
            targetNodeIndex = dynamicMazeGrowth.FindNodeIndexAtPosition(targetPosition);
            if (targetNodeIndex < 0)
            {
                // Not on a node - cancel silently
                actionApplied = true;
                return;
            }

            // Block activation on node 0 (the heart/seed node)
            if (targetNodeIndex == 0)
            {
                actionApplied = true;
                return;
            }

            // Store menu position
            menuPosition = targetPosition;

            // Create the circular menu
            CreateCircularMenu();
            menuActive = true;
        }

        public override void OnEnd()
        {
            DestroyMenu();
            menuActive = false;

            // Clear static instance
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (!menuActive)
                return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            // Check for escape key to cancel (disabled in tutorial mode)
            if (!tutorialLanternOnlyMode && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelMenu();
                return;
            }

            // Keyboard/gamepad shortcuts for sculpt menu options (check all 3 columns)
            // In tutorial mode, only lantern shortcut is allowed
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.SculptPondBinding, GameSettings.SculptPondAltBinding, GameSettings.SculptPondTertiaryBinding))
            {
                OnPondClicked();
                return;
            }
            if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.SculptLanternBinding, GameSettings.SculptLanternAltBinding, GameSettings.SculptLanternTertiaryBinding))
            {
                OnLanternClicked();
                return;
            }
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.SculptRingBinding, GameSettings.SculptRingAltBinding, GameSettings.SculptRingTertiaryBinding))
            {
                OnRingClicked();
                return;
            }
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.SculptRemoveBinding, GameSettings.SculptRemoveAltBinding, GameSettings.SculptRemoveTertiaryBinding))
            {
                OnRemoveClicked();
                return;
            }
        }

        private void CreateCircularMenu()
        {
            // Ensure EventSystem exists for button interaction
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create prop preview textures
            CreatePropPreviews();

            // Create a circular sprite for button masks
            Sprite circleSprite = CreateCircleSprite(64);

            // Create container
            menuContainer = new GameObject("SculptingMenu");

            // Create SCREEN-SPACE OVERLAY canvas (always on top, proper UI)
            menuCanvas = menuContainer.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 200; // High priority to be on top

            // Add CanvasScaler for consistent sizing across resolutions
            var scaler = menuContainer.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Add GraphicRaycaster for button interaction
            menuContainer.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Get the canvas RectTransform
            RectTransform canvasRect = menuCanvas.GetComponent<RectTransform>();

            // Calculate sizes based on reference resolution height (50% of screen height for entire menu)
            // Use reference height (1080) since CanvasScaler is set to ScaleWithScreenSize
            float referenceHeight = 1080f;
            float menuSize = referenceHeight * MENU_SCREEN_HEIGHT_FRACTION;
            float menuRadius = menuSize * MENU_RADIUS_FRACTION;
            float buttonSize = menuSize * BUTTON_SIZE_FRACTION;
            float centerButtonSize = menuSize * CENTER_BUTTON_FRACTION;

            // Find PowerButton_3 (Sculpting button) to center the menu directly above it
            // Default to screen center (will be converted to canvas coordinates below)
            Vector2 screenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GameObject powerButton = GameObject.Find("PowerButton_3");

            if (powerButton != null)
            {
                RectTransform powerButtonRect = powerButton.GetComponent<RectTransform>();
                if (powerButtonRect != null)
                {
                    // Get the screen position of the power button (world corners = screen coords for overlay canvas)
                    Vector3[] corners = new Vector3[4];
                    powerButtonRect.GetWorldCorners(corners);
                    // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right

                    Vector2 buttonCenter = new Vector2((corners[0].x + corners[2].x) / 2f, (corners[0].y + corners[2].y) / 2f);
                    float powerButtonTop = corners[1].y;

                    // Position menu so the bottom sculpt button sits just above the power button
                    // Need to calculate in screen pixels first, then convert to canvas coordinates
                    float gap = 15f; // Gap in screen pixels
                    // In screen space: menu center Y = powerButtonTop + gap + (distance from menu center to bottom of bottom button)
                    // The bottom button center is at menuRadius below menu center, and button extends buttonSize/2 below that
                    // So menu center Y = powerButtonTop + gap + menuRadius + buttonSize/2
                    // But menuRadius and buttonSize are in reference resolution (1080p), need to scale
                    float scaleFactor = Screen.height / 1080f;
                    float scaledMenuRadius = menuRadius * scaleFactor;
                    float scaledButtonSize = buttonSize * scaleFactor;
                    float menuCenterY = powerButtonTop + gap + scaledMenuRadius + scaledButtonSize * 0.5f;
                    screenPos = new Vector2(buttonCenter.x, menuCenterY);
                }
            }

            // Convert screen position to canvas local position
            // The canvas uses CanvasScaler with reference 1920x1080, so we need to scale
            float canvasScaleX = 1920f / Screen.width;
            float canvasScaleY = 1080f / Screen.height;
            Vector2 canvasPos = new Vector2(screenPos.x * canvasScaleX, screenPos.y * canvasScaleY);

            // Create a panel at the calculated position
            GameObject panelObj = new GameObject("MenuPanel");
            panelObj.transform.SetParent(canvasRect, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = canvasPos;
            panelRect.sizeDelta = new Vector2(menuSize, menuSize);

            // Create circular buttons around center (no labels, with preview images)
            // Center button (Cancel - red X)
            centerButton = CreateCircularButton(panelRect, Vector2.zero, centerButtonSize, CancelColor, circleSprite, null, "X", OnCancelClicked);

            // Top button (Remove) - uses earth ground texture
            Sprite removeSprite = propPreviewTextures != null && propPreviewTextures[0] != null
                ? Sprite.Create(propPreviewTextures[0], new Rect(0, 0, propPreviewTextures[0].width, propPreviewTextures[0].height), new Vector2(0.5f, 0.5f))
                : null;
            topButton = CreateCircularButton(panelRect, new Vector2(0, menuRadius), buttonSize, RemoveColor, circleSprite, removeSprite, null, OnRemoveClicked);

            // Left button (Pond)
            Sprite pondSprite = propPreviewTextures != null && propPreviewTextures[1] != null
                ? Sprite.Create(propPreviewTextures[1], new Rect(0, 0, propPreviewTextures[1].width, propPreviewTextures[1].height), new Vector2(0.5f, 0.5f))
                : null;
            leftButton = CreateCircularButton(panelRect, new Vector2(-menuRadius, 0), buttonSize, PondColor, circleSprite, pondSprite, null, OnPondClicked);

            // Bottom button (Lantern)
            Sprite lanternSprite = propPreviewTextures != null && propPreviewTextures[2] != null
                ? Sprite.Create(propPreviewTextures[2], new Rect(0, 0, propPreviewTextures[2].width, propPreviewTextures[2].height), new Vector2(0.5f, 0.5f))
                : null;
            bottomButton = CreateCircularButton(panelRect, new Vector2(0, -menuRadius), buttonSize, LanternColor, circleSprite, lanternSprite, null, OnLanternClicked);

            // Right button (Ring)
            Sprite ringSprite = propPreviewTextures != null && propPreviewTextures[3] != null
                ? Sprite.Create(propPreviewTextures[3], new Rect(0, 0, propPreviewTextures[3].width, propPreviewTextures[3].height), new Vector2(0.5f, 0.5f))
                : null;
            rightButton = CreateCircularButton(panelRect, new Vector2(menuRadius, 0), buttonSize, RingColor, circleSprite, ringSprite, null, OnRingClicked);
        }

        private Sprite CreateCircleSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        // Anti-aliased edge
                        float alpha = Mathf.Clamp01(radius - dist + 1f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Stored textures loaded from files
        private Texture2D[] propPreviewTextures;

        private void CreatePropPreviews()
        {
            // Load pre-saved preview textures from Assets/Textures/PropPreviews/
            // These are screenshots taken from the editor with correct orientations
            propPreviewTextures = new Texture2D[4];

            // 0: Remove - earth ground texture
            propPreviewTextures[0] = Resources.Load<Texture2D>("EarthenGroundTexture");

            // 1: Pond preview
            propPreviewTextures[1] = Resources.Load<Texture2D>("Textures/PropPreviews/pond_preview");

            // 2: Lantern preview
            propPreviewTextures[2] = Resources.Load<Texture2D>("Textures/PropPreviews/lantern_preview");

            // 3: Ring preview
            propPreviewTextures[3] = Resources.Load<Texture2D>("Textures/PropPreviews/ring_preview");
        }

        private UnityEngine.UI.Button CreateCircularButton(RectTransform parent, Vector2 position, float size, Color bgColor, Sprite circleMask, Sprite contentSprite, string fallbackText, UnityEngine.Events.UnityAction onClick)
        {
            // Create outer border circle (slightly larger)
            GameObject borderObj = new GameObject($"CircularButtonBorder");
            borderObj.transform.SetParent(parent, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchoredPosition = position;
            float borderSize = size + 6f; // 3px border on each side
            borderRect.sizeDelta = new Vector2(borderSize, borderSize);

            // Border image - white/light contrasting color
            var borderImage = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImage.sprite = circleMask;
            borderImage.color = new Color(0.9f, 0.9f, 0.9f, 1f); // Light border
            borderImage.type = UnityEngine.UI.Image.Type.Simple;
            borderImage.preserveAspect = true;
            borderImage.raycastTarget = false;

            // Create main button as child
            GameObject buttonObj = new GameObject($"CircularButton");
            buttonObj.transform.SetParent(borderObj.transform, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);

            // Add circular mask for content clipping
            var mask = buttonObj.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = true;

            // Background circle image
            var bgImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.sprite = circleMask;
            bgImage.color = bgColor;
            bgImage.type = UnityEngine.UI.Image.Type.Simple;
            bgImage.preserveAspect = true;

            // Add button component
            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bgImage;

            // Set button colors with hover/press effects
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            button.onClick.AddListener(onClick);

            // Add content image (prop preview) if provided
            if (contentSprite != null)
            {
                GameObject contentObj = new GameObject("Content");
                contentObj.transform.SetParent(buttonObj.transform, false);

                RectTransform contentRect = contentObj.AddComponent<RectTransform>();
                // Fill the button area - images should be pre-cropped to fit the circle
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;

                var contentImage = contentObj.AddComponent<UnityEngine.UI.Image>();
                contentImage.sprite = contentSprite;
                contentImage.preserveAspect = false; // Stretch to fill circular mask area
                contentImage.raycastTarget = false;
            }
            else if (!string.IsNullOrEmpty(fallbackText))
            {
                // Fallback text (for cancel button)
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                text.text = fallbackText;
                text.fontSize = size * 0.5f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = Color.white;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.raycastTarget = false;
            }

            return button;
        }

        private void DestroyMenu()
        {
            if (menuContainer != null)
            {
                Object.Destroy(menuContainer);
                menuContainer = null;
            }

            // Note: propPreviewTextures are asset references, don't destroy them
            propPreviewTextures = null;

            menuCanvas = null;
            centerButton = null;
            topButton = null;
            leftButton = null;
            bottomButton = null;
            rightButton = null;
        }

        private void CancelMenu()
        {
            actionApplied = true;
        }

        private void OnCancelClicked()
        {
            CancelMenu();
        }

        private void OnRemoveClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, null);
            }
            actionApplied = true;
        }

        private void OnPondClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.Pond);
            }
            actionApplied = true;
        }

        private void OnLanternClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FaeLantern);
            }
            actionApplied = true;
        }

        private void OnRingClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FairyRing);
            }
            actionApplied = true;
        }

        /// <summary>
        /// Spawns a fog effect that expands from node center to cover the node, then fades out.
        /// Uses a circular quad with the PowerFog shader, similar to MurmuringPaths effect.
        /// </summary>
        private void SpawnSmokeEffect(Vector3 nodeCenter)
        {
            const float NODE_RADIUS = 3.0f;
            const float SMOKE_DURATION = 0.8f;
            const float FOG_Z = -0.3f; // Above ground plane (-Z is up)

            // Play sculpt sound effect
            PlaySculptSound(nodeCenter);

            // Start the fog animation coroutine
            manager.StartCoroutine(AnimateSculptingFog(nodeCenter, NODE_RADIUS, SMOKE_DURATION, FOG_Z));
        }

        /// <summary>
        /// Plays the sculpt sound effect at the given position.
        /// </summary>
        private void PlaySculptSound(Vector3 position)
        {
            float volume = FaeMaze.Systems.GameSettings.SculptVolume * FaeMaze.Systems.GameSettings.SfxVolume;
            if (volume <= 0f) return;

            AudioClip sculptClip = Resources.Load<AudioClip>("Audio/SFX/sculpt");
            if (sculptClip != null)
            {
                // Create a temporary audio source for 3D positional audio
                GameObject audioObj = new GameObject("SculptSound");
                audioObj.transform.position = position;
                AudioSource audioSource = audioObj.AddComponent<AudioSource>();
                audioSource.clip = sculptClip;
                audioSource.spatialBlend = 1f; // 3D audio
                audioSource.volume = volume;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 15f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.Play();
                Object.Destroy(audioObj, sculptClip.length + 0.1f);
            }
        }

        /// <summary>
        /// Animates an expanding fog ring with particles on the leading edge.
        /// The ring expands from center to node edge, then fades out.
        /// Inner edge tapers to transparency for a soft billowing look.
        /// </summary>
        private System.Collections.IEnumerator AnimateSculptingFog(Vector3 nodeCenter, float targetRadius, float duration, float fogZ)
        {
            // Smoke colors - pale cream/tan
            Color smokeColor = new Color(0.85f, 0.80f, 0.72f, 0.85f);
            Color smokeColorDark = new Color(0.70f, 0.65f, 0.58f, 0.85f);

            const float RING_THICKNESS = 1.2f; // Width of the fog ring
            const int PARTICLE_COUNT = 200; // Particles on leading edge - dense cloud

            // Create container
            GameObject container = new GameObject("SculptingFogContainer");
            container.transform.position = new Vector3(nodeCenter.x, nodeCenter.y, fogZ);

            // Create the fog ring mesh
            GameObject fogRing = new GameObject("SculptingFogRing");
            fogRing.transform.SetParent(container.transform);
            fogRing.transform.localPosition = Vector3.zero;
            fogRing.transform.rotation = Quaternion.identity;

            MeshFilter meshFilter = fogRing.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = fogRing.AddComponent<MeshRenderer>();

            // Create material using PowerFog shader
            var shader = Shader.Find("Custom/PowerFog");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material fogMaterial = new Material(shader);
            fogMaterial.SetColor("_FogColor", smokeColor);
            fogMaterial.SetColor("_FogColorDark", smokeColorDark);
            fogMaterial.SetColor("_GlowColor", new Color(1f, 0.98f, 0.95f, 0.5f));
            fogMaterial.SetFloat("_WaveProgress", 0.5f);
            fogMaterial.SetVector("_HeartPosition", new Vector4(nodeCenter.x, nodeCenter.y, 0, 0));
            fogMaterial.SetVector("_FurthestPosition", new Vector4(nodeCenter.x, nodeCenter.y, 0, 0));

            // Cloud settings for billowy look - more detail and variation
            fogMaterial.SetFloat("_CloudScale", 12.0f);  // Smaller cloud features for more detail
            fogMaterial.SetFloat("_CloudDetail", 3.5f);  // More detail layers
            fogMaterial.SetFloat("_CloudDensity", 1.1f); // Denser clouds
            fogMaterial.SetFloat("_CloudSharpness", 2.0f); // Softer edges for billowy look
            fogMaterial.SetFloat("_WindSpeed", 0.12f);   // Slightly faster animation

            // Create radial gradient texture for inner edge fade (black at center, white at edge)
            // This makes the path mask fade from 0 (inner) to 1 (outer)
            Texture2D gradientTex = CreateRadialGradientTexture(64);
            fogMaterial.SetTexture("_PathMask", gradientTex);

            meshRenderer.material = fogMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // Create particle system for leading edge
            GameObject particleObj = new GameObject("LeadingEdgeParticles");
            particleObj.transform.SetParent(container.transform);
            particleObj.transform.localPosition = Vector3.zero;
            particleObj.transform.rotation = Quaternion.identity;

            ParticleSystem particles = particleObj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f); // Much smaller particles
            main.startColor = new Color(0.95f, 0.92f, 0.88f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000; // Many more particles allowed
            main.gravityModifier = 0f;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = PARTICLE_COUNT * 8; // Very dense emission for cloud effect

            // Shape will be updated each frame to match ring's leading edge
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.radiusThickness = 0.6f; // Broader distribution across ring
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f); // Emit in XY plane

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(0.6f, 1.2f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.98f, 0.96f, 0.93f), 0f),
                    new GradientColorKey(new Color(0.90f, 0.86f, 0.80f), 0.5f),
                    new GradientColorKey(new Color(0.80f, 0.75f, 0.68f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Noise for organic movement
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 2f;
            noise.scrollSpeed = 0.5f;

            // Particle renderer
            var particleRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 101;

            var particleMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));
            particleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            particleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            particleMat.SetInt("_ZWrite", 0);
            particleMat.renderQueue = 3001;
            particleRenderer.material = particleMat;

            particles.Play();

            // Animation timing
            float expandDuration = duration * 0.5f;   // Expand phase
            float fadeDuration = duration * 0.5f;     // Fade phase

            float elapsed = 0f;
            float currentInnerRadius = 0f;
            float currentOuterRadius = RING_THICKNESS * 0.5f;

            // Phase 1: Expand ring outward
            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDuration);
                float easedT = 1f - (1f - t) * (1f - t); // Ease out

                // Ring expands: inner and outer both grow, maintaining thickness
                currentOuterRadius = Mathf.Lerp(RING_THICKNESS * 0.5f, targetRadius, easedT);
                currentInnerRadius = Mathf.Max(0f, currentOuterRadius - RING_THICKNESS);

                // Update ring mesh with UVs that encode radial position for gradient sampling
                meshFilter.mesh = CreateRingMeshWithGradientUVs(32, currentInnerRadius, currentOuterRadius);

                // Update particle emission radius to match leading edge
                shape.radius = currentOuterRadius;

                yield return null;
            }

            // Stop particle emission, let existing particles fade
            emission.rateOverTime = 0;

            // Phase 2: Fade out the ring
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float alpha = 1f - (t * t); // Ease in fade

                smokeColor.a = 0.85f * alpha;
                smokeColorDark.a = 0.85f * alpha;
                fogMaterial.SetColor("_FogColor", smokeColor);
                fogMaterial.SetColor("_FogColorDark", smokeColorDark);

                yield return null;
            }

            // Cleanup
            Object.Destroy(gradientTex);
            Object.Destroy(fogMaterial);
            Object.Destroy(particleMat);
            Object.Destroy(container);
        }

        /// <summary>
        /// Creates a radial gradient texture where center is black (0) and edge is white (1).
        /// Used for inner edge fade on the fog ring.
        /// </summary>
        private Texture2D CreateRadialGradientTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalized = Mathf.Clamp01(dist / maxDist);

                    // Gradient from 0 (center) to 1 (edge) with strong ease for very soft inner fade
                    // Use quartic (power of 4) for much more gradual inner edge
                    float value = normalized * normalized * normalized * normalized;
                    tex.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Creates a ring mesh with UVs that map to a radial gradient texture.
        /// Inner edge UV samples from center (black), outer edge samples from edge (white).
        /// </summary>
        private Mesh CreateRingMeshWithGradientUVs(int segments, float innerRadius, float outerRadius)
        {
            Mesh mesh = new Mesh();
            mesh.name = "RingMeshGradient";

            int vertexCount = segments * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 6];

            // Calculate UV radius based on actual ring geometry
            // We want inner vertices to sample from inner part of gradient, outer from outer part
            float uvInnerRadius = innerRadius / (outerRadius > 0.001f ? outerRadius : 1f) * 0.5f;
            float uvOuterRadius = 0.5f; // Edge of texture

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner vertex
                vertices[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
                // UV maps to inner ring of gradient texture
                uvs[i * 2] = new Vector2(cos * uvInnerRadius + 0.5f, sin * uvInnerRadius + 0.5f);

                // Outer vertex
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
                // UV maps to outer edge of gradient texture
                uvs[i * 2 + 1] = new Vector2(cos * uvOuterRadius + 0.5f, sin * uvOuterRadius + 0.5f);

                // Two triangles per segment
                int nextI = (i + 1) % segments;
                triangles[i * 6] = i * 2;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = nextI * 2 + 1;

                triangles[i * 6 + 3] = i * 2;
                triangles[i * 6 + 4] = nextI * 2 + 1;
                triangles[i * 6 + 5] = nextI * 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }

    /// <summary>
    /// Simple MonoBehaviour component to pulse a button's color for highlighting.
    /// </summary>
    public class ButtonPulseEffect : MonoBehaviour
    {
        private UnityEngine.UI.Image targetImage;
        private Color baseColor;
        private float pulseSpeed = 2f;
        private float pulseIntensity = 0.3f;

        public void StartPulse(UnityEngine.UI.Image image, Color baseCol)
        {
            targetImage = image;
            baseColor = baseCol;
        }

        private void Update()
        {
            if (targetImage == null) return;

            // Pulse brightness using sine wave
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
            float brightness = 1f + pulse * pulseIntensity;

            // Apply brighter color
            Color pulsedColor = new Color(
                Mathf.Min(baseColor.r * brightness, 1f),
                Mathf.Min(baseColor.g * brightness, 1f),
                Mathf.Min(baseColor.b * brightness, 1f),
                baseColor.a
            );

            targetImage.color = pulsedColor;
        }
    }

    #endregion
}

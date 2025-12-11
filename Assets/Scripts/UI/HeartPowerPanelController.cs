using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FaeMaze.HeartPowers;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the Heart Powers panel UI - creates a button panel along the bottom
    /// of the screen for activating Heart Powers.
    /// Automatically creates the UI if not manually set up.
    /// </summary>
    public class HeartPowerPanelController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References (Optional - will auto-create if null)")]
        [SerializeField]
        [Tooltip("Heart Powers panel GameObject")]
        private GameObject heartPowersPanel;

        [Header("System References")]
        [SerializeField]
        [Tooltip("Reference to the HeartPowerManager")]
        private HeartPowerManager heartPowerManager;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Toggle panel visibility with F2 key")]
        private bool enableToggle = true;

        #endregion

        #region Private Fields

        private Button[] powerButtons = new Button[7];
        private Image[] buttonImages = new Image[7];
        private TextMeshProUGUI[] buttonLabels = new TextMeshProUGUI[7];
        private TextMeshProUGUI[] cooldownTexts = new TextMeshProUGUI[7];
        private TextMeshProUGUI chargesText;
        private TextMeshProUGUI essenceText;

        private readonly string[] powerNames = new string[]
        {
            "1: Heartbeat\nof Longing",
            "2: Murmuring\nPaths",
            "3: Dream\nSnare",
            "4: Feastward\nPanic",
            "5: Covenant\nwith Wisps",
            "6: Puka's\nBargain",
            "7: Ring of\nInvitations"
        };

        private readonly HeartPowerType[] powerTypes = new HeartPowerType[]
        {
            HeartPowerType.HeartbeatOfLonging,
            HeartPowerType.MurmuringPaths,
            HeartPowerType.DreamSnare,
            HeartPowerType.FeastwardPanic,
            HeartPowerType.CovenantWithWisps,
            HeartPowerType.PukasBargain,
            HeartPowerType.RingOfInvitations
        };

        // ROYGBIV spectrum colors for each power
        private readonly Color[] roygbivColors = new Color[]
        {
            new Color(0.8f, 0.1f, 0.1f, 1f),  // Power 1: Deep Red
            new Color(1.0f, 0.5f, 0.0f, 1f),  // Power 2: Warm Orange
            new Color(1.0f, 0.9f, 0.1f, 1f),  // Power 3: Bright Yellow
            new Color(0.2f, 0.8f, 0.2f, 1f),  // Power 4: Vivid Green
            new Color(0.2f, 0.5f, 1.0f, 1f),  // Power 5: Cool Blue
            new Color(0.3f, 0.0f, 0.5f, 1f),  // Power 6: Indigo
            new Color(0.6f, 0.2f, 0.8f, 1f)   // Power 7: Vibrant Violet
        };

        // Base colors (darker versions for inactive state)
        private readonly Color[] baseColors = new Color[]
        {
            new Color(0.3f, 0.05f, 0.05f, 1f),  // Dim Red
            new Color(0.35f, 0.18f, 0.0f, 1f),  // Dim Orange
            new Color(0.35f, 0.32f, 0.05f, 1f), // Dim Yellow
            new Color(0.08f, 0.3f, 0.08f, 1f),  // Dim Green
            new Color(0.08f, 0.18f, 0.35f, 1f), // Dim Blue
            new Color(0.12f, 0.0f, 0.2f, 1f),   // Dim Indigo
            new Color(0.22f, 0.08f, 0.3f, 1f)   // Dim Violet
        };

        // Glow animation tracking
        private float[] glowPhase = new float[7];
        private float[] glowIntensity = new float[7];
        private float[] flashIntensity = new float[7]; // Flash effect when power is activated
        private float glowSpeed = 2.0f;
        private float glowPulseSpeed = 3.0f;
        private float flashDecaySpeed = 5.0f;

        private Camera mainCamera;

        // Targeting mode state
        private bool isTargetingMode = false;
        private HeartPowerType? pendingPowerType = null;
        private Texture2D originalCursorTexture = null;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            mainCamera = Camera.main;

            // Auto-find HeartPowerManager if not assigned
            if (heartPowerManager == null)
            {
                heartPowerManager = HeartPowerManager.Instance;
                if (heartPowerManager == null)
                {
                    heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
                }
            }

            if (heartPowerManager == null)
            {
                Debug.LogError("[HeartPowerPanel] HeartPowerManager not found! Panel will not function correctly.");
                return;
            }

            // Auto-create panel if not assigned
            if (heartPowersPanel == null)
            {
                CreateHeartPowersPanelUI();
            }

            // Initialize UI controls
            InitializeControls();

            // Initialize glow effects with staggered phases
            for (int i = 0; i < glowPhase.Length; i++)
            {
                glowPhase[i] = i * 0.5f; // Stagger initial phases
                glowIntensity[i] = 0.0f; // Start with no glow (will increase when powers are ready)
                flashIntensity[i] = 0.0f; // No flash initially
            }

            Debug.Log("[HeartPowerPanel] Heart Powers panel initialized with ROYGBIV glow effects");
            Debug.Log($"[HeartPowerPanel] Initial state - Charges: {heartPowerManager.CurrentCharges}, Essence: {heartPowerManager.CurrentEssence}");
        }

        private void Update()
        {
            // Toggle panel with F2 key using new Input System
            if (enableToggle && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                TogglePanel();
            }

            // Update cooldown displays and button states
            UpdateButtonStates();

            // Update ROYGBIV glow effects
            UpdateGlowEffects();

            // Handle keyboard shortcuts (1-7 keys)
            HandleKeyboardInput();

            // Handle targeting mode for targeted powers
            HandleTargetingMode();
        }

        private void OnEnable()
        {
            // Subscribe to HeartPowerManager events
            if (heartPowerManager != null)
            {
                heartPowerManager.OnChargesChanged += UpdateChargesDisplay;
                heartPowerManager.OnEssenceChanged += UpdateEssenceDisplay;
                heartPowerManager.OnPowerActivated += OnPowerActivated;
            }

            // Also subscribe to GameController essence changes for real-time updates
            SubscribeToGameControllerEvents();
        }

        private void SubscribeToGameControllerEvents()
        {
            // Try to find GameController if not found yet
            if (GameController.Instance != null)
            {
                // Unsubscribe first to avoid double subscription
                GameController.Instance.OnEssenceChanged -= UpdateEssenceDisplay;
                // Subscribe
                GameController.Instance.OnEssenceChanged += UpdateEssenceDisplay;
            }
        }

        private void OnDisable()
        {
            if (heartPowerManager != null)
            {
                heartPowerManager.OnChargesChanged -= UpdateChargesDisplay;
                heartPowerManager.OnEssenceChanged -= UpdateEssenceDisplay;
                heartPowerManager.OnPowerActivated -= OnPowerActivated;
            }

            // Unsubscribe from GameController
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= UpdateEssenceDisplay;
            }
        }

        private void OnDestroy()
        {
            // Clean up button listeners
            for (int i = 0; i < powerButtons.Length; i++)
            {
                if (powerButtons[i] != null)
                {
                    int index = i; // Capture for closure
                    powerButtons[i].onClick.RemoveAllListeners();
                }
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all UI controls with default values and listeners.
        /// </summary>
        private void InitializeControls()
        {
            // Setup button listeners
            int successCount = 0;
            for (int i = 0; i < powerButtons.Length; i++)
            {
                if (powerButtons[i] != null)
                {
                    int index = i; // Capture for closure
                    powerButtons[i].onClick.AddListener(() => OnPowerButtonClicked(index));

                    // Force buttons to be interactable for testing - they'll be updated in UpdateButtonStates
                    powerButtons[i].interactable = true;

                    successCount++;
                    Debug.Log($"[HeartPowerPanel] Button {i} listener added successfully, interactable={powerButtons[i].interactable}");
                }
                else
                {
                    Debug.LogWarning($"[HeartPowerPanel] Button {i} is null, skipping listener setup");
                }
            }

            Debug.Log($"[HeartPowerPanel] Initialized {successCount}/{powerButtons.Length} button listeners");

            // Ensure we're subscribed to GameController events
            SubscribeToGameControllerEvents();

            // Initialize resource displays
            UpdateResourceDisplays();

            // Panel starts visible
            if (heartPowersPanel != null)
            {
                heartPowersPanel.SetActive(true);
                Debug.Log("[HeartPowerPanel] Panel set to active (visible)");
            }

            // Debug: Check game state and essence connection
            if (heartPowerManager != null)
            {
                Debug.Log($"[HeartPowerPanel] Game state check:");
                Debug.Log($"[HeartPowerPanel]   Charges: {heartPowerManager.CurrentCharges}");
                Debug.Log($"[HeartPowerPanel]   Essence (from HeartPowerManager): {heartPowerManager.CurrentEssence}");
                if (GameController.Instance != null)
                {
                    Debug.Log($"[HeartPowerPanel]   Essence (from GameController): {GameController.Instance.CurrentEssence}");
                }
                else
                {
                    Debug.LogWarning($"[HeartPowerPanel]   GameController.Instance is NULL!");
                }

                for (int i = 0; i < powerTypes.Length; i++)
                {
                    bool canActivate = heartPowerManager.CanActivatePower(powerTypes[i], out string reason);
                    Debug.Log($"[HeartPowerPanel]   Power {i} ({powerTypes[i]}): canActivate={canActivate}, reason='{reason}'");
                }
            }
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Automatically creates the Heart Powers panel UI hierarchy.
        /// </summary>
        private void CreateHeartPowersPanelUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[HeartPowerPanel] No existing Canvas found, creating new one");
                canvas = CreateCanvas();
            }
            else
            {
                Debug.Log($"[HeartPowerPanel] Using existing Canvas: {canvas.name}");

                // Ensure the existing canvas has a GraphicRaycaster for button clicks
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                    Debug.Log("[HeartPowerPanel] Added GraphicRaycaster to existing Canvas");
                }
                else
                {
                    Debug.Log($"[HeartPowerPanel] Existing Canvas already has GraphicRaycaster (blockingObjects: {raycaster.blockingObjects})");
                }
            }

            // Create the panel at the bottom
            heartPowersPanel = CreatePanel(canvas.transform);

            // Create title
            CreateTitle(heartPowersPanel.transform);

            // Create resource displays (top of panel)
            float yPos = -10f;
            chargesText = CreateResourceLabel(heartPowersPanel.transform, "Heart Charges: 3", yPos, -250f);
            essenceText = CreateResourceLabel(heartPowersPanel.transform, "Essence: 10", yPos, 250f);

            // Create power buttons in a horizontal row (centered in panel)
            float buttonWidth = 160f;
            float buttonHeight = 100f;
            float spacing = 5f;
            float totalWidth = (buttonWidth * 7) + (spacing * 6);
            float startX = -totalWidth / 2f + buttonWidth / 2f;
            float buttonYPos = -75f; // Center buttons vertically in panel

            Debug.Log($"[HeartPowerPanel] Creating 7 buttons: width={buttonWidth}, height={buttonHeight}, totalWidth={totalWidth}");

            for (int i = 0; i < 7; i++)
            {
                float xPos = startX + (i * (buttonWidth + spacing));
                powerButtons[i] = CreatePowerButton(heartPowersPanel.transform, i, xPos, buttonYPos, buttonWidth, buttonHeight);
                Debug.Log($"[HeartPowerPanel] Created button {i} at x={xPos}, y={buttonYPos}");
            }
        }

        /// <summary>
        /// Creates a Canvas for the UI.
        /// </summary>
        private Canvas CreateCanvas()
        {
            GameObject canvasObj = new GameObject("HeartPowersCanvas");
            canvasObj.transform.SetParent(null, false); // Don't parent to this controller

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // High priority to be on top

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            Debug.Log("[HeartPowerPanel] Created new Canvas with GraphicRaycaster, sortingOrder=100");

            return canvas;
        }

        /// <summary>
        /// Creates the main panel background at the bottom of the screen.
        /// </summary>
        private GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("HeartPowersPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f); // Bottom center
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(1200f, 150f); // Taller for larger buttons

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.15f, 0.05f, 0.2f, 0.9f); // Dark purple/magenta tint

            Debug.Log($"[HeartPowerPanel] Created panel with size: {rect.sizeDelta}");

            return panel;
        }

        /// <summary>
        /// Creates a title text at the top of the panel.
        /// </summary>
        private void CreateTitle(Transform parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);

            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -5f);
            rect.sizeDelta = new Vector2(1180f, 25f);

            TextMeshProUGUI text = titleObj.AddComponent<TextMeshProUGUI>();
            text.text = "HEART POWERS";
            text.fontSize = 20;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.7f, 0.9f, 1f); // Light magenta
        }

        /// <summary>
        /// Creates a resource display label.
        /// </summary>
        private TextMeshProUGUI CreateResourceLabel(Transform parent, string text, float yPos, float xOffset)
        {
            GameObject labelObj = new GameObject("ResourceLabel_" + text);
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(xOffset, yPos);
            rect.sizeDelta = new Vector2(280f, 25f);

            TextMeshProUGUI textComp = labelObj.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 16;
            textComp.fontStyle = FontStyles.Bold;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = new Color(1f, 0.84f, 0f); // Gold color

            return textComp;
        }

        /// <summary>
        /// Creates a power button with label and cooldown text.
        /// </summary>
        private Button CreatePowerButton(Transform parent, int index, float xPos, float yPos, float width, float height)
        {
            GameObject buttonObj = new GameObject($"PowerButton_{index}");
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(xPos, yPos);
            rect.sizeDelta = new Vector2(width, height);

            Image image = buttonObj.AddComponent<Image>();
            image.color = baseColors[index]; // Set initial ROYGBIV base color
            image.raycastTarget = true; // Ensure it can be clicked

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = true; // Ensure button is interactable

            // Store image reference for glow animations
            buttonImages[index] = image;

            // Add navigation to make it clear it's a button
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            // Create button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5f, 25f);
            textRect.offsetMax = new Vector2(-5f, -5f);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = powerNames[index];
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.fontStyle = FontStyles.Normal;
            text.raycastTarget = false; // Don't block button clicks

            buttonLabels[index] = text;

            // Create cooldown text overlay
            GameObject cooldownObj = new GameObject("Cooldown");
            cooldownObj.transform.SetParent(buttonObj.transform, false);

            RectTransform cooldownRect = cooldownObj.AddComponent<RectTransform>();
            cooldownRect.anchorMin = Vector2.zero;
            cooldownRect.anchorMax = Vector2.one;
            cooldownRect.offsetMin = Vector2.zero;
            cooldownRect.offsetMax = Vector2.zero;

            TextMeshProUGUI cooldownText = cooldownObj.AddComponent<TextMeshProUGUI>();
            cooldownText.text = "";
            cooldownText.fontSize = 32;
            cooldownText.fontStyle = FontStyles.Bold;
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.color = new Color(1f, 0.3f, 0.3f, 1f);
            cooldownText.raycastTarget = false; // Don't block button clicks

            cooldownTexts[index] = cooldownText;
            cooldownObj.SetActive(false);

            Debug.Log($"[HeartPowerPanel] Button {index} created: pos=({xPos},{yPos}), size=({width}x{height}), interactable={button.interactable}");

            return button;
        }

        #endregion

        #region Button Callbacks

        /// <summary>
        /// Called when a power button is clicked.
        /// </summary>
        private void OnPowerButtonClicked(int index)
        {
            Debug.Log($"[HeartPowerPanel] ★★★ BUTTON CLICK DETECTED ★★★ Index: {index}");

            if (heartPowerManager == null)
            {
                Debug.LogWarning("[HeartPowerPanel] HeartPowerManager not found!");
                return;
            }

            HeartPowerType powerType = powerTypes[index];

            Debug.Log($"[HeartPowerPanel] Button clicked for power: {powerType} (Index: {index})");

            // Check if this is a targeted power
            bool isTargetedPower = IsTargetedPower(powerType);

            if (isTargetedPower)
            {
                // Enter targeting mode - wait for player to click on the grid
                EnterTargetingMode(powerType);
            }
            else
            {
                // Non-targeted powers activate immediately
                Debug.Log($"[HeartPowerPanel] Activating global power");
                bool success = heartPowerManager.TryActivatePower(powerType);

                if (success)
                {
                    Debug.Log($"[HeartPowerPanel] ✓ Power {powerType} activated successfully!");
                }
                else
                {
                    heartPowerManager.CanActivatePower(powerType, out string reason);
                    Debug.LogWarning($"[HeartPowerPanel] ✗ Failed to activate power {powerType}. Reason: {reason}");
                }
            }
        }

        /// <summary>
        /// Called when a power is successfully activated.
        /// </summary>
        private void OnPowerActivated(HeartPowerType powerType)
        {
            Debug.Log($"[HeartPowerPanel] Power activated event received: {powerType}");

            // Find which button corresponds to this power and trigger flash effect
            for (int i = 0; i < powerTypes.Length; i++)
            {
                if (powerTypes[i] == powerType)
                {
                    // Trigger a bright flash effect
                    flashIntensity[i] = 1.5f; // Extra bright flash
                    Debug.Log($"[HeartPowerPanel] Triggering ROYGBIV flash for {powerNames[i]} (color: {roygbivColors[i]})");
                    break;
                }
            }
        }

        #endregion

        #region Keyboard Input

        /// <summary>
        /// Handles keyboard shortcuts for activating powers (1-7 keys).
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(0);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(1);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(2);
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(3);
            }
            else if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(4);
            }
            else if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(5);
            }
            else if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame)
            {
                OnPowerButtonClicked(6);
            }
        }

        #endregion

        #region Targeting Mode

        /// <summary>
        /// Checks if a power type requires targeting.
        /// </summary>
        private bool IsTargetedPower(HeartPowerType powerType)
        {
            return powerType == HeartPowerType.MurmuringPaths ||
                   powerType == HeartPowerType.DreamSnare ||
                   powerType == HeartPowerType.PukasBargain ||
                   powerType == HeartPowerType.FeastwardPanic;
        }

        /// <summary>
        /// Enters targeting mode for a power that requires targeting.
        /// </summary>
        private void EnterTargetingMode(HeartPowerType powerType)
        {
            // First check if power can be activated
            if (!heartPowerManager.CanActivatePower(powerType, out string reason))
            {
                Debug.LogWarning($"[HeartPowerPanel] Cannot activate {powerType}: {reason}");
                return;
            }

            isTargetingMode = true;
            pendingPowerType = powerType;

            // Change cursor to indicate targeting mode (crosshair if available)
            // Note: Unity's Cursor.SetCursor requires a texture. We'll just log for now.
            Debug.Log($"[HeartPowerPanel] ⊕ Entered targeting mode for {powerType}. Click on the map to target, or press ESC to cancel.");
        }

        /// <summary>
        /// Exits targeting mode without activating the power.
        /// </summary>
        private void ExitTargetingMode(bool cancelled = false)
        {
            if (isTargetingMode)
            {
                if (cancelled)
                {
                    Debug.Log($"[HeartPowerPanel] ✗ Targeting cancelled for {pendingPowerType}");
                }

                isTargetingMode = false;
                pendingPowerType = null;

                // Restore cursor if we changed it
                // Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        /// <summary>
        /// Handles targeting mode input in the Update loop.
        /// Listens for mouse clicks to select target position.
        /// </summary>
        private void HandleTargetingMode()
        {
            if (!isTargetingMode || !pendingPowerType.HasValue)
            {
                return;
            }

            // Check for ESC key to cancel targeting
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitTargetingMode(cancelled: true);
                return;
            }

            // Check for left mouse button click
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Get mouse position in screen space
                Vector3 mouseScreenPos = Mouse.current.position.ReadValue();

                // Convert to world position
                if (mainCamera == null)
                {
                    Debug.LogError("[HeartPowerPanel] Main camera is null, cannot convert mouse position!");
                    ExitTargetingMode(cancelled: true);
                    return;
                }

                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
                mouseWorldPos.z = 0; // 2D game on Z=0 plane

                Debug.Log($"[HeartPowerPanel] Mouse clicked at screen: {mouseScreenPos}, world: {mouseWorldPos}");

                // Validate the position is on the grid - NO FALLBACK
                if (heartPowerManager.MazeGrid == null)
                {
                    Debug.LogError("[HeartPowerPanel] MazeGrid is null, cannot validate target position!");
                    ExitTargetingMode(cancelled: true);
                    return;
                }

                if (!heartPowerManager.MazeGrid.WorldToGrid(mouseWorldPos, out int gridX, out int gridY))
                {
                    // Invalid position - cancel with user feedback
                    Debug.LogWarning($"[HeartPowerPanel] ✗ Invalid target position {mouseWorldPos} - not on the grid. Please click on a valid tile.");
                    // Don't exit targeting mode - let player try again
                    return;
                }

                // Valid grid position found!
                Vector2Int gridPos = new Vector2Int(gridX, gridY);
                Vector3 targetWorldPos = heartPowerManager.MazeGrid.GridToWorld(gridX, gridY);

                Debug.Log($"[HeartPowerPanel] ✓ Valid target selected: Grid {gridPos}, World {targetWorldPos}");

                // Try to activate the power
                bool success = heartPowerManager.TryActivatePower(pendingPowerType.Value, targetWorldPos);

                if (success)
                {
                    Debug.Log($"[HeartPowerPanel] ✓ Power {pendingPowerType.Value} activated successfully at {gridPos}!");
                    ExitTargetingMode(cancelled: false);
                }
                else
                {
                    heartPowerManager.CanActivatePower(pendingPowerType.Value, out string reason);
                    Debug.LogWarning($"[HeartPowerPanel] ✗ Failed to activate power {pendingPowerType.Value} at {gridPos}. Reason: {reason}");
                    ExitTargetingMode(cancelled: true);
                }
            }
        }

        #endregion

        #region Display Updates

        /// <summary>
        /// Updates all resource displays.
        /// </summary>
        private void UpdateResourceDisplays()
        {
            if (heartPowerManager != null)
            {
                UpdateChargesDisplay(heartPowerManager.CurrentCharges);

                // Get essence from GameController directly as source of truth
                int essence = 0;
                if (GameController.Instance != null)
                {
                    essence = GameController.Instance.CurrentEssence;
                }
                else if (heartPowerManager.GameController != null)
                {
                    essence = heartPowerManager.GameController.CurrentEssence;
                }

                UpdateEssenceDisplay(essence);
            }
        }

        /// <summary>
        /// Updates the charges display.
        /// </summary>
        private void UpdateChargesDisplay(int charges)
        {
            if (chargesText != null)
            {
                chargesText.text = $"Heart Charges: {charges}";
                Debug.Log($"[HeartPowerPanel] Charges updated: {charges}");
            }
        }

        /// <summary>
        /// Updates the essence display.
        /// </summary>
        private void UpdateEssenceDisplay(int essence)
        {
            if (essenceText != null)
            {
                essenceText.text = $"Essence: {essence}";
                Debug.Log($"[HeartPowerPanel] Essence updated: {essence}");
            }
        }

        /// <summary>
        /// Updates button states and cooldown displays.
        /// </summary>
        private void UpdateButtonStates()
        {
            if (heartPowerManager == null) return;

            for (int i = 0; i < powerButtons.Length; i++)
            {
                if (powerButtons[i] == null) continue;

                HeartPowerType powerType = powerTypes[i];

                // Check if power can be activated
                bool canActivate = heartPowerManager.CanActivatePower(powerType, out string reason);

                // Keep buttons always clickable so we can see debug messages
                // But provide visual feedback about availability
                powerButtons[i].interactable = true;

                // Update cooldown display
                float cooldownRemaining = heartPowerManager.GetCooldownRemaining(powerType);
                if (cooldownRemaining > 0)
                {
                    cooldownTexts[i].text = $"{cooldownRemaining:F1}s";
                    cooldownTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    cooldownTexts[i].gameObject.SetActive(false);
                }

                // Update glow intensity based on availability
                // Glow effects will be applied by UpdateGlowEffects()
                if (canActivate)
                {
                    // Power is ready - increase glow intensity
                    glowIntensity[i] = Mathf.Lerp(glowIntensity[i], 1.0f, Time.deltaTime * glowSpeed);
                }
                else
                {
                    // Power not ready - decrease glow intensity
                    glowIntensity[i] = Mathf.Lerp(glowIntensity[i], 0.0f, Time.deltaTime * glowSpeed);
                }
            }
        }

        /// <summary>
        /// Updates the ROYGBIV glow effects for each power button.
        /// Each button pulses with its assigned color from the spectrum.
        /// </summary>
        private void UpdateGlowEffects()
        {
            for (int i = 0; i < buttonImages.Length; i++)
            {
                if (buttonImages[i] == null) continue;

                // Decay flash intensity over time
                if (flashIntensity[i] > 0)
                {
                    flashIntensity[i] = Mathf.Max(0, flashIntensity[i] - Time.deltaTime * flashDecaySpeed);
                }

                // Update glow phase with staggered timing for visual variety
                // Each button starts at a different phase for a cascading effect
                float phaseOffset = i * 0.5f; // Stagger by 0.5 seconds
                glowPhase[i] = Time.time * glowPulseSpeed + phaseOffset;

                // Calculate pulse using sine wave (0 to 1)
                float pulse = (Mathf.Sin(glowPhase[i]) + 1f) * 0.5f;

                // Smooth the pulse to make it more gentle
                pulse = Mathf.Pow(pulse, 0.7f);

                // Interpolate between base color and full ROYGBIV color
                // glowIntensity controls how bright the effect is (0 = dim base, 1 = full glow)
                Color baseColor = baseColors[i];
                Color glowColor = roygbivColors[i];

                // Calculate pulsing color
                Color pulsingColor = Color.Lerp(
                    baseColor,
                    Color.Lerp(baseColor, glowColor, pulse),
                    glowIntensity[i]
                );

                // Add flash effect (brightens the color significantly when activated)
                Color finalColor = Color.Lerp(
                    pulsingColor,
                    glowColor * 1.3f, // Extra bright for flash
                    flashIntensity[i]
                );

                // Apply the color to the button image
                buttonImages[i].color = finalColor;
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Toggles the panel visibility.
        /// Cancels targeting mode if active to avoid stuck state.
        /// </summary>
        private void TogglePanel()
        {
            // Cancel any active targeting mode
            if (isTargetingMode)
            {
                ExitTargetingMode(cancelled: true);
            }

            if (heartPowersPanel != null)
            {
                bool newState = !heartPowersPanel.activeSelf;
                heartPowersPanel.SetActive(newState);
                Debug.Log($"[HeartPowerPanel] Panel toggled: {(newState ? "visible" : "hidden")}");
            }
        }

        /// <summary>
        /// Gets the mouse position in world space.
        /// For targeted powers, this should ideally be implemented with a proper targeting mode.
        /// Currently uses a fallback to Heart position if mouse position is invalid.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null || heartPowerManager == null)
            {
                return Vector3.zero;
            }

            // Get mouse screen position
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();

            // Convert to world position
            // For orthographic camera, we need to match the camera's z-plane
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0; // Assuming 2D game on Z=0 plane

            Debug.Log($"[HeartPowerPanel] Mouse screen pos: {mouseScreenPos}, world pos: {mouseWorldPos}");

            // Validate the position is on the grid
            if (heartPowerManager.MazeGrid != null)
            {
                if (!heartPowerManager.MazeGrid.WorldToGrid(mouseWorldPos, out int gx, out int gy))
                {
                    // Invalid position - use Heart position as fallback
                    Debug.LogWarning($"[HeartPowerPanel] Mouse position {mouseWorldPos} is not on grid, using Heart position as fallback");
                    Vector2Int heartPos = heartPowerManager.MazeGrid.HeartGridPos;
                    mouseWorldPos = heartPowerManager.MazeGrid.GridToWorld(heartPos.x, heartPos.y);
                }
            }

            return mouseWorldPos;
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FaeMaze.HeartPowers;
using FaeMaze.Systems;
using FaeMaze.Cameras;
using UnityEngine.TextCore.Text;
using FontStyles = TMPro.FontStyles;

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

        [SerializeField]
        [Tooltip("Reference to the CameraController3D")]
        private CameraController3D cameraController;

        [SerializeField]
        [Tooltip("Reference to the WaveSpawner")]
        private Systems.WaveSpawner waveSpawner;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Toggle panel visibility with F2 key")]
        private bool enableToggle = true;

        #endregion

        #region Private Fields

        private Button[] powerButtons = new Button[9];
        private Image[] buttonImages = new Image[9];
        private TextMeshProUGUI[] buttonLabels = new TextMeshProUGUI[9];
        private TextMeshProUGUI[] cooldownTexts = new TextMeshProUGUI[9];

        // Right panel UI elements
        private TextMeshProUGUI waveText;
        private TextMeshProUGUI essenceValueText;
        private Slider essenceBar;

        private readonly string[] powerNames = new string[]
        {
            "1: Heartbeat\nof Longing",
            "2: Murmuring\nPaths",
            "3: Dream\nSnare",
            "4: Feastward\nPanic",
            "5: Covenant\nwith Wisps",
            "6: Puka's\nBargain",
            "7: Ring of\nInvitations",
            "8: Heartward\nGrasp",
            "9: Devouring\nMaw"
        };

        private readonly HeartPowerType[] powerTypes = new HeartPowerType[]
        {
            HeartPowerType.HeartbeatOfLonging,
            HeartPowerType.MurmuringPaths,
            HeartPowerType.DreamSnare,
            HeartPowerType.FeastwardPanic,
            HeartPowerType.CovenantWithWisps,
            HeartPowerType.PukasBargain,
            HeartPowerType.RingOfInvitations,
            HeartPowerType.HeartwardGrasp,
            HeartPowerType.DevouringMaw
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
            new Color(0.6f, 0.2f, 0.8f, 1f),  // Power 7: Vibrant Violet
            new Color(0.9f, 0.1f, 0.5f, 1f),  // Power 8: Crimson
            new Color(0.5f, 0.0f, 0.2f, 1f)   // Power 9: Dark Burgundy
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
            new Color(0.22f, 0.08f, 0.3f, 1f),  // Dim Violet
            new Color(0.32f, 0.05f, 0.18f, 1f), // Dim Crimson
            new Color(0.18f, 0.0f, 0.08f, 1f)   // Dim Dark Burgundy
        };

        // Glow animation tracking
        private float[] glowPhase = new float[9];
        private float[] glowIntensity = new float[9];
        private float[] flashIntensity = new float[9]; // Flash effect when power is activated
        private float glowSpeed = 2.0f;
        private float glowPulseSpeed = 3.0f;
        private float flashDecaySpeed = 5.0f;

        private Camera mainCamera;

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
                return;
            }

            // Auto-find CameraController3D if not assigned
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController3D>();
            }

            // Auto-find WaveSpawner if not assigned
            if (waveSpawner == null)
            {
                waveSpawner = FindFirstObjectByType<Systems.WaveSpawner>();
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

            // Handle keyboard shortcuts (1-9 keys)
            HandleKeyboardInput();

            // Handle targeting mode for targeted powers
            HandleTargetingMode();

            // Update wave and essence displays
            UpdateWaveAndEssenceDisplays();
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
                }
                else
                {
                }
            }


            // Ensure we're subscribed to GameController events
            SubscribeToGameControllerEvents();

            // Initialize resource displays
            UpdateResourceDisplays();

            // Panel starts visible
            if (heartPowersPanel != null)
            {
                heartPowersPanel.SetActive(true);
            }

            // Debug: Check game state and essence connection
            if (heartPowerManager != null)
            {
                if (GameController.Instance != null)
                {
                }
                else
                {
                }

                for (int i = 0; i < powerTypes.Length; i++)
                {
                    bool canActivate = heartPowerManager.CanActivatePower(powerTypes[i], out string reason);
                }
            }
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Automatically creates the Heart Powers panel UI hierarchy.
        /// Creates a unified HUD bar spanning the bottom of the screen with:
        /// - Left half: 9 heart power buttons
        /// - Right half: wave count and essence display with slider
        /// </summary>
        private void CreateHeartPowersPanelUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }
            else
            {
                // Ensure the existing canvas has a GraphicRaycaster for button clicks
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                }
            }

            // Create the panel spanning the bottom
            heartPowersPanel = CreatePanel(canvas.transform);
            float panelHeight = Mathf.Max(200f, 1080f * 0.05f);

            // Left half: Create power buttons in a compact horizontal row
            float leftPadding = 10f;
            float buttonSpacing = 4f;
            float buttonHeight = panelHeight - 20f; // Leave 10px padding top/bottom
            // Calculate button width to fit 9 buttons in left half (assume half screen = 960px)
            float leftHalfWidth = 960f; // Half of 1920 reference resolution
            float buttonWidth = (leftHalfWidth - leftPadding * 2 - buttonSpacing * 8) / 9f;
            float buttonsStartX = -960f + leftPadding; // Start from left edge of screen
            float buttonYPos = panelHeight / 2f;

            for (int i = 0; i < 9; i++)
            {
                float xPos = buttonsStartX + (i * (buttonWidth + buttonSpacing)) + buttonWidth / 2f;
                powerButtons[i] = CreatePowerButton(heartPowersPanel.transform, i, xPos, buttonYPos, buttonWidth, buttonHeight);
            }

            // Right half: Create wave and essence display
            CreateRightPanelUI(heartPowersPanel.transform, panelHeight);
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


            return canvas;
        }

        /// <summary>
        /// Creates the main panel background spanning the entire bottom of the screen.
        /// Height is the larger of 5% of viewport or 200px.
        /// </summary>
        private GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("HeartPowersPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f); // Bottom left
            rect.anchorMax = new Vector2(1f, 0f); // Bottom right (spans full width)
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 0f); // Aligned to bottom

            // Use larger of 5% viewport or 200px for height (with reference resolution 1920x1080, 5% of height = 54px, so use 200px)
            float panelHeight = Mathf.Max(200f, 1080f * 0.05f);
            rect.sizeDelta = new Vector2(0f, panelHeight); // Width 0 means it uses anchors (full width)

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.15f, 0.05f, 0.2f, 0.9f); // Dark purple/magenta tint


            return panel;
        }

        /// <summary>
        /// Creates the right half of the bottom panel with wave and essence displays.
        /// </summary>
        private void CreateRightPanelUI(Transform parent, float panelHeight)
        {
            float rightHalfStartX = 0f; // Right half starts at center
            float padding = 20f;
            float elementSpacing = 10f;

            // Create container for right panel elements
            GameObject rightContainer = new GameObject("RightPanelContainer");
            rightContainer.transform.SetParent(parent, false);

            RectTransform containerRect = rightContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0f); // Center bottom
            containerRect.anchorMax = new Vector2(1f, 1f); // Right top (right half of panel)
            containerRect.offsetMin = new Vector2(padding, padding);
            containerRect.offsetMax = new Vector2(-padding, -padding);

            // Create Wave display (top of right panel)
            GameObject waveObj = new GameObject("WaveDisplay");
            waveObj.transform.SetParent(rightContainer.transform, false);

            RectTransform waveRect = waveObj.AddComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0f, 0.65f);
            waveRect.anchorMax = new Vector2(1f, 1f);
            waveRect.offsetMin = Vector2.zero;
            waveRect.offsetMax = Vector2.zero;

            waveText = waveObj.AddComponent<TextMeshProUGUI>();
            waveText.text = "Wave 0";
            waveText.fontSize = 24;
            waveText.fontStyle = FontStyles.Bold;
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.color = new Color(1f, 0.85f, 0.3f, 1f); // Gold

            // Create Essence Label
            GameObject essenceLabelObj = new GameObject("EssenceLabel");
            essenceLabelObj.transform.SetParent(rightContainer.transform, false);

            RectTransform essenceLabelRect = essenceLabelObj.AddComponent<RectTransform>();
            essenceLabelRect.anchorMin = new Vector2(0f, 0.35f);
            essenceLabelRect.anchorMax = new Vector2(1f, 0.6f);
            essenceLabelRect.offsetMin = Vector2.zero;
            essenceLabelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI essenceLabelText = essenceLabelObj.AddComponent<TextMeshProUGUI>();
            essenceLabelText.text = "Essence";
            essenceLabelText.fontSize = 16;
            essenceLabelText.fontStyle = FontStyles.Bold;
            essenceLabelText.alignment = TextAlignmentOptions.Center;
            essenceLabelText.color = new Color(0.6f, 0.8f, 1f, 1f); // Light blue

            // Create Essence Value Text
            GameObject essenceValueObj = new GameObject("EssenceValue");
            essenceValueObj.transform.SetParent(rightContainer.transform, false);

            RectTransform essenceValueRect = essenceValueObj.AddComponent<RectTransform>();
            essenceValueRect.anchorMin = new Vector2(0f, 0.15f);
            essenceValueRect.anchorMax = new Vector2(1f, 0.35f);
            essenceValueRect.offsetMin = Vector2.zero;
            essenceValueRect.offsetMax = Vector2.zero;

            essenceValueText = essenceValueObj.AddComponent<TextMeshProUGUI>();
            essenceValueText.text = "0 / 400";
            essenceValueText.fontSize = 20;
            essenceValueText.fontStyle = FontStyles.Bold;
            essenceValueText.alignment = TextAlignmentOptions.Center;
            essenceValueText.color = new Color(1f, 0.84f, 0f, 1f); // Gold

            // Create Essence Bar (slider)
            GameObject essenceBarObj = new GameObject("EssenceBar");
            essenceBarObj.transform.SetParent(rightContainer.transform, false);

            RectTransform essenceBarRect = essenceBarObj.AddComponent<RectTransform>();
            essenceBarRect.anchorMin = new Vector2(0f, 0f);
            essenceBarRect.anchorMax = new Vector2(1f, 0.12f);
            essenceBarRect.offsetMin = Vector2.zero;
            essenceBarRect.offsetMax = Vector2.zero;

            essenceBar = essenceBarObj.AddComponent<Slider>();
            essenceBar.minValue = 0f;
            essenceBar.maxValue = 400f;
            essenceBar.value = 0f;
            essenceBar.interactable = false;

            // Create slider background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(essenceBarObj.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create slider fill area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(essenceBarObj.transform, false);

            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            // Create slider fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.7f, 1f, 1f); // Blue fill

            essenceBar.fillRect = fillRect;
            essenceBar.targetGraphic = fillImage;
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

            // Create button text (compact label showing just the number)
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(1f, 1f);
            textRect.offsetMax = new Vector2(-1f, -1f);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = (index + 1).ToString(); // Just show the number 1-9
            text.fontSize = Mathf.Max(8, width * 0.4f); // Scale font with button size
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
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
            cooldownText.fontSize = Mathf.Max(6, width * 0.3f); // Scale with button size
            cooldownText.fontStyle = FontStyles.Bold;
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.color = new Color(1f, 0.3f, 0.3f, 1f);
            cooldownText.raycastTarget = false; // Don't block button clicks

            cooldownTexts[index] = cooldownText;
            cooldownObj.SetActive(false);


            return button;
        }

        #endregion

        #region Button Callbacks

        /// <summary>
        /// Called when a power button is clicked.
        /// All powers now target the focal point tile automatically.
        /// </summary>
        private void OnPowerButtonClicked(int index)
        {
            if (heartPowerManager == null)
            {
                Debug.LogWarning("[HeartPowerPanelController] OnPowerButtonClicked: HeartPowerManager is null");
                return;
            }

            HeartPowerType powerType = powerTypes[index];

            if (powerType == HeartPowerType.HeartwardGrasp || powerType == HeartPowerType.DevouringMaw)
            {
                Debug.Log($"[HeartPowerPanelController] Button clicked for {powerType} (index {index})");
            }

            // Get the focal point position from the camera controller
            Vector3 targetPosition = GetFocalPointPosition();

            if (powerType == HeartPowerType.HeartwardGrasp || powerType == HeartPowerType.DevouringMaw)
            {
                Debug.Log($"[HeartPowerPanelController] Focal point position: {targetPosition}");
            }

            // All powers now activate at the focal point
            heartPowerManager.TryActivatePower(powerType, targetPosition);
        }

        /// <summary>
        /// Gets the focal point position from the camera controller.
        /// Falls back to the Heart position if camera controller is not available.
        /// </summary>
        private Vector3 GetFocalPointPosition()
        {
            if (cameraController != null)
            {
                return cameraController.FocalPointPosition;
            }

            // Fallback to Heart position if camera controller is not available
            if (GameController.Instance != null && GameController.Instance.Heart != null)
            {
                return GameController.Instance.Heart.transform.position;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Called when a power is successfully activated.
        /// </summary>
        private void OnPowerActivated(HeartPowerType powerType)
        {

            // Find which button corresponds to this power and trigger flash effect
            for (int i = 0; i < powerTypes.Length; i++)
            {
                if (powerTypes[i] == powerType)
                {
                    // Trigger a bright flash effect
                    flashIntensity[i] = 1.5f; // Extra bright flash
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
            else if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame)
            {
                Debug.Log("[HeartPowerPanelController] Keyboard 8 pressed - HeartwardGrasp");
                OnPowerButtonClicked(7);
            }
            else if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame)
            {
                Debug.Log("[HeartPowerPanelController] Keyboard 9 pressed - DevouringMaw");
                OnPowerButtonClicked(8);
            }
        }

        #endregion

        #region Targeting Mode (Disabled - All powers now target focal point)

        /// <summary>
        /// Handles targeting mode input in the Update loop.
        /// DISABLED: All powers now automatically target the focal point tile.
        /// </summary>
        private void HandleTargetingMode()
        {
            // No-op: Targeting mode is disabled. All powers now automatically target the focal point.
        }

        #endregion

        #region Display Updates

        /// <summary>
        /// Updates wave and essence displays in the right panel.
        /// </summary>
        private void UpdateWaveAndEssenceDisplays()
        {
            // Update wave display
            if (waveText != null && waveSpawner != null)
            {
                int currentWave = waveSpawner.CurrentWaveNumber;
                if (waveSpawner.IsWaveActive)
                {
                    waveText.text = $"Wave {currentWave}";
                }
                else
                {
                    waveText.text = currentWave > 0 ? $"Wave {currentWave} Complete" : "No Active Wave";
                }
            }

            // Update essence display
            if (GameController.Instance != null)
            {
                int essence = GameController.Instance.CurrentEssence;

                if (essenceValueText != null)
                {
                    essenceValueText.text = $"{essence} / 400";
                }

                if (essenceBar != null)
                {
                    essenceBar.value = essence;
                }
            }
        }

        /// <summary>
        /// Updates all resource displays (called from events).
        /// </summary>
        private void UpdateResourceDisplays()
        {
            UpdateWaveAndEssenceDisplays();
        }

        /// <summary>
        /// Updates the charges display (event handler, no UI element for charges anymore).
        /// </summary>
        private void UpdateChargesDisplay(int charges)
        {
            // Charges are no longer displayed in the UI
        }

        /// <summary>
        /// Updates the essence display (event handler).
        /// </summary>
        private void UpdateEssenceDisplay(int essence)
        {
            UpdateWaveAndEssenceDisplays();
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
        /// </summary>
        private void TogglePanel()
        {
            if (heartPowersPanel != null)
            {
                bool newState = !heartPowersPanel.activeSelf;
                heartPowersPanel.SetActive(newState);
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


            // Validate the position is on the grid
            if (heartPowerManager.MazeGrid != null)
            {
                if (!heartPowerManager.MazeGrid.WorldToGrid(mouseWorldPos, out int gx, out int gy))
                {
                    // Invalid position - use Heart position as fallback
                    Vector2Int heartPos = heartPowerManager.MazeGrid.HeartGridPos;
                    mouseWorldPos = heartPowerManager.MazeGrid.GridToWorld(heartPos.x, heartPos.y);
                }
            }

            return mouseWorldPos;
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
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
    public class HeartPowerPanelController : MonoBehaviour, IReadyReporter
    {
        public bool IsReady { get; private set; }

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

        // 5 active powers (MurmuringPaths, HeartwardGrasp, DevouringMaw, Sculpting, Misdirect)
        private Button[] powerButtons = new Button[5];
        private Image[] buttonImages = new Image[5];
        private Image[] iconImages = new Image[5];
        private TextMeshProUGUI[] buttonLabels = new TextMeshProUGUI[5];

        // Right panel UI elements
        private TextMeshProUGUI runTimerText;
        private float runStartTime;
        private bool runTimerActive;

        private readonly string[] powerNames = new string[]
        {
            "Spooky\nFog",
            "Yoink!",
            "Nom\nNom",
            "Redecorating",
            "Misdirect"
        };

        private readonly HeartPowerType[] powerTypes = new HeartPowerType[]
        {
            HeartPowerType.MurmuringPaths,
            HeartPowerType.HeartwardGrasp,
            HeartPowerType.DevouringMaw,
            HeartPowerType.Sculpting,
            HeartPowerType.Misdirect
        };

        // ROYGBIV spectrum colors for each power (5 active powers)
        private readonly Color[] roygbivColors = new Color[]
        {
            new Color(1.0f, 0.5f, 0.0f, 1f),  // MurmuringPaths: Warm Orange
            new Color(0.9f, 0.1f, 0.5f, 1f),  // HeartwardGrasp: Crimson
            new Color(0.5f, 0.0f, 0.2f, 1f),  // DevouringMaw: Dark Burgundy
            new Color(0.2f, 0.6f, 0.4f, 1f),  // Sculpting: Forest Green
            new Color(0.2f, 0.7f, 0.9f, 1f)   // Misdirect: Cyan/Teal
        };

        // Base colors (darker versions for inactive state)
        private readonly Color[] baseColors = new Color[]
        {
            new Color(0.35f, 0.18f, 0.0f, 1f),  // Dim Orange (MurmuringPaths)
            new Color(0.32f, 0.05f, 0.18f, 1f), // Dim Crimson (HeartwardGrasp)
            new Color(0.18f, 0.0f, 0.08f, 1f),  // Dim Dark Burgundy (DevouringMaw)
            new Color(0.08f, 0.22f, 0.15f, 1f), // Dim Forest Green (Sculpting)
            new Color(0.08f, 0.25f, 0.32f, 1f)  // Dim Cyan/Teal (Misdirect)
        };

        // Glow animation tracking (5 active powers)
        private float[] glowPhase = new float[5];
        private float[] glowIntensity = new float[5];
        private float[] flashIntensity = new float[5]; // Flash effect when power is activated
        private bool[] isActivePower = new bool[5]; // Track which toggle powers are currently active
        private float glowSpeed = 2.0f;
        private float glowPulseSpeed = 3.0f;
        private float flashDecaySpeed = 5.0f;

        // Tutorial highlight lock - when set, that button is frozen at peak brightness
        private int lockedAtPeakButtonIndex = -1;

        // Tutorial power lock - when true, all powers are disabled unless explicitly unlocked
        private bool tutorialPowersLocked = false;
        private HashSet<int> tutorialUnlockedPowers = new HashSet<int>();

        private Camera mainCamera;

        // Power button sprites (loaded at runtime)
        private Sprite[] powerButtonSprites = new Sprite[5];

        // Circular sprite for round button backgrounds
        private Sprite circleSprite;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            mainCamera = Camera.main;

            // Load power button sprites
            LoadPowerButtonSprites();

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

            // Update button labels to reflect current bindings
            RefreshButtonLabels();

            // Initialize glow effects with staggered phases
            for (int i = 0; i < glowPhase.Length; i++)
            {
                glowPhase[i] = i * 0.5f; // Stagger initial phases
                glowIntensity[i] = 0.0f; // Start with no glow (will increase when powers are ready)
                flashIntensity[i] = 0.0f; // No flash initially
            }

            IsReady = true;
        }

        private void Update()
        {
            // Toggle panel with F2 key using new Input System
            if (enableToggle && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                TogglePanel();
            }

            // Update button states
            UpdateButtonStates();

            // Update ROYGBIV glow effects
            UpdateGlowEffects();

            // Handle keyboard shortcuts (1-9 keys)
            HandleKeyboardInput();

            // Handle targeting mode for targeted powers
            HandleTargetingMode();

            // Update run timer display
            UpdateRunTimerDisplay();
        }

        private void OnEnable()
        {
            // Subscribe to HeartPowerManager events
            if (heartPowerManager != null)
            {
                heartPowerManager.OnEssenceChanged += UpdateEssenceDisplay;
                heartPowerManager.OnPowerActivated += OnPowerActivated;
                heartPowerManager.OnPowerDeactivated += OnPowerDeactivated;
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
                heartPowerManager.OnEssenceChanged -= UpdateEssenceDisplay;
                heartPowerManager.OnPowerActivated -= OnPowerActivated;
                heartPowerManager.OnPowerDeactivated -= OnPowerDeactivated;
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

        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Loads sprites for power buttons from the Resources/PowerModels folder.
        /// Also creates a procedural circle sprite for round button backgrounds.
        /// </summary>
        private void LoadPowerButtonSprites()
        {
            // Resource names (without extension, relative to Resources folder)
            string[] resourceNames = new string[]
            {
                "PowerModels/path",
                "PowerModels/grasp",
                "PowerModels/devour",
                "PowerModels/sculpt",
                "PowerModels/misdirect"
            };

            for (int i = 0; i < 5; i++)
            {
                // Try loading as Sprite first
                powerButtonSprites[i] = Resources.Load<Sprite>(resourceNames[i]);

                // If that fails, try loading as Texture2D and create sprite manually
                if (powerButtonSprites[i] == null)
                {
                    Texture2D tex = Resources.Load<Texture2D>(resourceNames[i]);
                    if (tex != null)
                    {
                        powerButtonSprites[i] = Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f),
                            100f
                        );
                    }
                }
            }

            // Create a procedural circle sprite for round button backgrounds
            circleSprite = CreateCircleSprite(128);
        }

        /// <summary>
        /// Creates a procedural circular sprite texture.
        /// </summary>
        private Sprite CreateCircleSprite(int resolution)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            float center = resolution / 2f;
            float radius = center - 1f; // Leave 1px for anti-aliasing

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Anti-aliased circle edge
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// Creates the Heart Powers UI under the existing Canvas.
        /// </summary>
        private void CreateHeartPowersPanelUI()
        {
            // Find existing canvas (should be GameRoot > Canvas)
            Canvas canvas = UIFactory.FindOrCreateCanvas(this, "HeartPowersCanvas", 100, new Vector2(1920, 1080));
            if (canvas != null)
            {
                // Ensure the existing canvas has a GraphicRaycaster for button clicks
                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null)
                {
                    raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
                    raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
                }
            }

            // Check if HeartPowersPanel already exists (avoid duplicates on scene reload)
            Transform existingPanel = canvas.transform.Find("HeartPowersPanel");
            if (existingPanel != null)
            {
                heartPowersPanel = existingPanel.gameObject;
                FindExistingButtons(canvas.transform);
                return;
            }

            // Create UI at runtime
            CreateHeartPowersPanelRuntime(canvas);
        }

        /// <summary>
        /// Finds existing buttons in the canvas hierarchy.
        /// </summary>
        private void FindExistingButtons(Transform canvasTransform)
        {
            for (int i = 0; i < 5; i++)
            {
                Transform btn = canvasTransform.Find($"PowerButton_{i}");
                if (btn != null)
                {
                    powerButtons[i] = btn.GetComponent<Button>();
                    buttonImages[i] = btn.GetComponent<Image>();
                    Transform iconTransform = btn.Find("Icon");
                    if (iconTransform != null)
                    {
                        iconImages[i] = iconTransform.GetComponent<Image>();
                    }
                }
            }
        }

        /// <summary>
        /// Creates the Heart Powers UI at runtime (fallback when prefabs not available).
        /// </summary>
        private void CreateHeartPowersPanelRuntime(Canvas canvas)
        {
            // Create the panel container
            heartPowersPanel = new GameObject("HeartPowersPanel");
            heartPowersPanel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = heartPowersPanel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f); // Bottom left
            panelRect.anchorMax = new Vector2(1f, 0f); // Bottom right
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 250f); // Height for buttons (raised to y=140 center)

            // Button settings - 3x scale (50 base * 3 = 150)
            float buttonSize = 150f;
            float buttonSpacing = 40f;
            float bottomPadding = 65f; // Button center at y=140 (65 + 150/2 = 140)
            float leftPadding = 30f;

            // Create 5 power buttons in a horizontal row at bottom-left
            for (int i = 0; i < 5; i++)
            {
                float xPos = leftPadding + (i * (buttonSize + buttonSpacing)) + buttonSize / 2f;
                float yPos = bottomPadding + buttonSize / 2f;
                powerButtons[i] = CreatePowerButton(canvas.transform, i, xPos, yPos, buttonSize, buttonSize);
            }

            // Create wave display and essence bar controller
            CreateRightPanelUI(canvas.transform, buttonSize);
        }


        /// <summary>
        /// Creates or finds the run timer display at bottom-right and essence bar controller.
        /// If a RunTimer GameObject already exists in the canvas hierarchy, it will be used.
        /// </summary>
        private void CreateRightPanelUI(Transform parent, float buttonSize)
        {
            // Try to find existing RunTimer first (supports editor-created static UI)
            GameObject timerObj = null;
            Transform existingTimer = parent.Find("RunTimer");
            if (existingTimer != null)
            {
                timerObj = existingTimer.gameObject;
                runTimerText = timerObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Create run timer display at bottom-right
                timerObj = new GameObject("RunTimer");
                timerObj.transform.SetParent(parent, false);

                RectTransform timerRect = timerObj.AddComponent<RectTransform>();
                timerRect.anchorMin = new Vector2(1f, 0f); // Bottom right
                timerRect.anchorMax = new Vector2(1f, 0f);
                timerRect.pivot = new Vector2(1f, 0f);
                timerRect.anchoredPosition = new Vector2(-30f, 20f); // 30px from right, 20px from bottom
                timerRect.sizeDelta = new Vector2(200f, 50f);

                runTimerText = timerObj.AddComponent<TextMeshProUGUI>();
                runTimerText.text = "00:00.00";
                runTimerText.fontSize = 24;
                runTimerText.fontStyle = FontStyles.Bold;
                runTimerText.alignment = TextAlignmentOptions.MidlineRight;
                runTimerText.color = new Color(1f, 0.85f, 0.3f, 1f); // Gold
            }

            // Initialize timer (starts when first wave begins)
            runStartTime = Time.time;
            runTimerActive = false;

            // Create EssenceBarController for the top-of-screen essence bar
            CreateEssenceBarController();
        }

        /// <summary>
        /// Creates the EssenceBarController component for the top-of-screen essence bar.
        /// </summary>
        private void CreateEssenceBarController()
        {
            // Check if one already exists
            EssenceBarController existingController = FindFirstObjectByType<EssenceBarController>();
            if (existingController != null) return;

            // Create a new GameObject for the EssenceBarController
            GameObject essenceBarControllerObj = new GameObject("EssenceBarController");
            essenceBarControllerObj.AddComponent<EssenceBarController>();
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
        /// Creates a round power button with icon sprite and circular background.
        /// All buttons are round with a colored circular background for glow effects.
        /// </summary>
        private Button CreatePowerButton(Transform parent, int index, float xPos, float yPos, float width, float height)
        {
            GameObject buttonObj = new GameObject($"PowerButton_{index}");
            buttonObj.transform.SetParent(parent, false);

            // All buttons are square/round
            float buttonSize = height;

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            // Anchor to bottom-left corner
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, yPos);
            rect.sizeDelta = new Vector2(buttonSize, buttonSize);

            // Create circular background (for glow effects and clickable area)
            Image bgImage = buttonObj.AddComponent<Image>();
            if (circleSprite != null)
            {
                bgImage.sprite = circleSprite;
            }
            bgImage.color = baseColors[index];
            bgImage.raycastTarget = true;

            // Store background image for glow effects
            buttonImages[index] = bgImage;

            // Create the icon sprite on top if available
            bool hasSprite = powerButtonSprites[index] != null;
            if (hasSprite)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(buttonObj.transform, false);

                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                // Center the icon in the button
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);

                // No offset - icons should be centered in their source images
                // If an icon appears off-center, the PNG file should be recropped
                iconRect.anchoredPosition = Vector2.zero;

                // Icon size is 80% of button size, with per-icon multipliers
                float[] iconSizeMultipliers = { 1.0f, 1.5f, 1.0f, 1.0f, 1.0f }; // Power 2 (grasp) is 50% larger
                float iconSize = buttonSize * 0.8f * iconSizeMultipliers[index];
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);

                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = powerButtonSprites[index];
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;

                // Use Sliced or Simple image type to ensure proper scaling
                iconImage.type = Image.Type.Simple;

                // Store icon image for tinting when unavailable
                iconImages[index] = iconImage;
            }

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = bgImage;
            button.interactable = true;

            // Disable navigation
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            // Create button text (hotkey number outside bottom-right of button)
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            // Position number outside the button, bottom-right corner
            textRect.anchorMin = new Vector2(1f, 0f);
            textRect.anchorMax = new Vector2(1f, 0f);
            textRect.pivot = new Vector2(0f, 1f); // Anchor from top-left of text
            textRect.anchoredPosition = new Vector2(5f, -5f); // 5px right and 5px below button edge
            textRect.sizeDelta = new Vector2(40f, 40f);

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = GetPrimaryBindingLabel(index);
            text.fontSize = Mathf.Max(10, buttonSize * 0.3f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;

            buttonLabels[index] = text;



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
                return;
            }

            // Block power activation during tutorial until explicitly unlocked
            if (tutorialPowersLocked && !tutorialUnlockedPowers.Contains(index))
            {
                return;
            }

            HeartPowerType powerType = powerTypes[index];

            // Get the focal point position from the camera controller
            Vector3 targetPosition = GetFocalPointPosition();

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

        /// <summary>
        /// Called when a toggle power is deactivated.
        /// </summary>
        private void OnPowerDeactivated(HeartPowerType powerType)
        {
            // Find which button corresponds to this power
            for (int i = 0; i < powerTypes.Length; i++)
            {
                if (powerTypes[i] == powerType)
                {
                    // Trigger a dim flash to indicate deactivation
                    flashIntensity[i] = 0.5f;
                    break;
                }
            }
        }

        #endregion

        #region Keyboard Input

        /// <summary>
        /// Handles keyboard/gamepad shortcuts for activating powers using configurable bindings.
        /// </summary>
        private void HandleKeyboardInput()
        {
            // Power 1: Spooky Fog
            if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower1Binding, GameSettings.HeartPower1AltBinding, GameSettings.HeartPower1TertiaryBinding))
            {
                OnPowerButtonClicked(0);
            }
            // Power 2: Yoink!
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower2Binding, GameSettings.HeartPower2AltBinding, GameSettings.HeartPower2TertiaryBinding))
            {
                OnPowerButtonClicked(1);
            }
            // Power 3: Nom Nom
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower3Binding, GameSettings.HeartPower3AltBinding, GameSettings.HeartPower3TertiaryBinding))
            {
                OnPowerButtonClicked(2);
            }
            // Power 4: Redecorating
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower4Binding, GameSettings.HeartPower4AltBinding, GameSettings.HeartPower4TertiaryBinding))
            {
                OnPowerButtonClicked(3);
            }
            // Power 5: Misdirect
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower5Binding, GameSettings.HeartPower5AltBinding, GameSettings.HeartPower5TertiaryBinding))
            {
                OnPowerButtonClicked(4);
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
        /// Updates run timer display in the right panel.
        /// Essence bar is now handled by EssenceBarController at the top of the screen.
        /// </summary>
        private void UpdateRunTimerDisplay()
        {
            // Start timer when first wave becomes active
            if (!runTimerActive && waveSpawner != null && waveSpawner.IsWaveActive)
            {
                runStartTime = Time.time;
                runTimerActive = true;
            }

            // Update run timer display
            if (runTimerText != null)
            {
                float elapsedTime = runTimerActive ? Time.time - runStartTime : 0f;
                int minutes = Mathf.FloorToInt(elapsedTime / 60f);
                int seconds = Mathf.FloorToInt(elapsedTime % 60f);
                int centiseconds = Mathf.FloorToInt((elapsedTime % 1f) * 100f);
                runTimerText.text = $"{minutes:D2}:{seconds:D2}.{centiseconds:D2}";
            }
        }

        /// <summary>
        /// Updates all resource displays (called from events).
        /// </summary>
        private void UpdateResourceDisplays()
        {
            UpdateRunTimerDisplay();
        }

        /// <summary>
        /// Updates the essence display (event handler - kept for event subscription compatibility).
        /// Actual essence display is now handled by EssenceBarController.
        /// </summary>
        private void UpdateEssenceDisplay(int essence)
        {
            // No-op: Essence display is now handled by EssenceBarController
        }

        /// <summary>
        /// Updates button states.
        /// </summary>
        private void UpdateButtonStates()
        {
            if (heartPowerManager == null) return;

            // Get focal point position for position-dependent checks
            Vector3 focalPointPos = GetFocalPointPosition();

            for (int i = 0; i < powerButtons.Length; i++)
            {
                if (powerButtons[i] == null) continue;

                HeartPowerType powerType = powerTypes[i];

                // Check if power can be activated
                bool canActivate = heartPowerManager.CanActivatePower(powerType, out string reason);
                bool isTogglePower = heartPowerManager.IsTogglePower(powerType);
                bool isActive = heartPowerManager.IsPowerActive(powerType);

                // Special check for Sculpting - requires valid node position
                bool sculptingPositionValid = true;
                if (powerType == HeartPowerType.Sculpting && !isActive)
                {
                    sculptingPositionValid = heartPowerManager.CanUseSculptingAt(focalPointPos);
                }

                // Special check for HeartwardGrasp - requires wall tiles along ray to heart
                bool graspPositionValid = true;
                if (powerType == HeartPowerType.HeartwardGrasp && !isActive)
                {
                    graspPositionValid = heartPowerManager.CanUseHeartwardGraspAt(focalPointPos);
                }

                // Keep buttons always clickable so we can see debug messages
                // But provide visual feedback about availability
                powerButtons[i].interactable = true;

                // Track active state for glow effects (tutorial-locked powers don't show as active)
                bool isTutorialLocked = tutorialPowersLocked && !tutorialUnlockedPowers.Contains(i);
                isActivePower[i] = isTogglePower && isActive && !isTutorialLocked;


                // Determine if power is truly available (including position checks)
                bool fullyAvailable = canActivate && sculptingPositionValid && graspPositionValid;

                // During tutorial, locked powers that haven't been unlocked should appear consistently unavailable
                if (tutorialPowersLocked && !tutorialUnlockedPowers.Contains(i))
                {
                    fullyAvailable = false;
                }

                // Update glow intensity based on availability
                // Use unscaledDeltaTime so glow updates continue during pause (e.g., tutorial)
                float dt = Time.unscaledDeltaTime * glowSpeed;

                // Glow effects will be applied by UpdateGlowEffects()
                if (isTogglePower && isActive && fullyAvailable)
                {
                    // Active toggle power (and not tutorial-locked) - keep high glow intensity
                    glowIntensity[i] = Mathf.Lerp(glowIntensity[i], 1.2f, dt);
                }
                else if (fullyAvailable)
                {
                    // Power is ready - increase glow intensity
                    glowIntensity[i] = Mathf.Lerp(glowIntensity[i], 1.0f, dt);
                }
                else
                {
                    // Power not ready - decrease glow intensity
                    glowIntensity[i] = Mathf.Lerp(glowIntensity[i], 0.3f, dt);
                }
            }
        }

        /// <summary>
        /// Updates the ROYGBIV glow effects for each power button.
        /// Each button pulses with its assigned color from the spectrum.
        /// Unavailable powers show flat grey instead of pulsing colors.
        /// Active toggle powers show flat (non-pulsing) background color.
        /// </summary>
        private void UpdateGlowEffects()
        {
            // Flat grey color for unavailable powers
            Color unavailableGrey = new Color(0.25f, 0.25f, 0.25f, 1f);

            for (int i = 0; i < buttonImages.Length; i++)
            {
                if (buttonImages[i] == null) continue;

                // Decay flash intensity over time
                if (flashIntensity[i] > 0)
                {
                    flashIntensity[i] = Mathf.Max(0, flashIntensity[i] - Time.deltaTime * flashDecaySpeed);
                }

                // Check if this button is locked at peak brightness (for tutorial)
                bool isLockedAtPeak = (i == lockedAtPeakButtonIndex);

                // Check if power is unavailable (low glow intensity means not ready)
                bool isUnavailable = glowIntensity[i] < 0.5f;

                Color finalColor;

                if (isLockedAtPeak)
                {
                    // Button is locked at peak brightness for tutorial highlighting
                    // Use full ROYGBIV color at maximum brightness
                    finalColor = roygbivColors[i];
                }
                else if (isUnavailable && flashIntensity[i] <= 0)
                {
                    // Power is unavailable - use flat grey (no pulsing)
                    finalColor = unavailableGrey;
                }
                else if (isActivePower[i])
                {
                    // Power is currently active - use flat (non-pulsing) ROYGBIV color
                    finalColor = roygbivColors[i];
                }
                else
                {
                    // Update glow phase with staggered timing for visual variety
                    // Each button starts at a different phase for a cascading effect
                    // Use unscaledTime so animation continues during pause (e.g., tutorial)
                    float phaseOffset = i * 0.5f; // Stagger by 0.5 seconds
                    glowPhase[i] = Time.unscaledTime * glowPulseSpeed + phaseOffset;

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
                    finalColor = Color.Lerp(
                        pulsingColor,
                        glowColor * 1.3f, // Extra bright for flash
                        flashIntensity[i]
                    );
                }

                // Apply the color to the button image
                buttonImages[i].color = finalColor;

                // Tint icon to match availability - dark when unavailable, white when active
                if (iconImages[i] != null)
                {
                    if (isUnavailable && !isLockedAtPeak && flashIntensity[i] <= 0)
                    {
                        iconImages[i].color = new Color(0.4f, 0.4f, 0.4f, 1f);
                    }
                    else
                    {
                        iconImages[i].color = Color.white;
                    }
                }
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
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            // Get mouse screen position
            Vector3 mouseScreenPos = Mouse.current.position.ReadValue();

            // Convert to world position
            // For orthographic camera, we need to match the camera's z-plane
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0; // Assuming 2D game on Z=0 plane

            return mouseWorldPos;
        }

        /// <summary>
        /// Gets the display label for a power button's primary binding.
        /// </summary>
        private string GetPrimaryBindingLabel(int index)
        {
            string binding = index switch
            {
                0 => GameSettings.HeartPower1Binding,
                1 => GameSettings.HeartPower2Binding,
                2 => GameSettings.HeartPower3Binding,
                3 => GameSettings.HeartPower4Binding,
                4 => GameSettings.HeartPower5Binding,
                _ => ""
            };
            return InputBindingHelper.GetDisplayName(binding);
        }

        /// <summary>
        /// Refreshes button labels to reflect current binding settings.
        /// Call this after bindings change (e.g., returning from Options).
        /// </summary>
        public void RefreshButtonLabels()
        {
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                if (buttonLabels[i] != null)
                {
                    buttonLabels[i].text = GetPrimaryBindingLabel(i);
                }
            }
        }

        /// <summary>
        /// Gets the current pulse brightness (0 to 1) for a specific power button.
        /// Used by tutorial system to sync pause with maximum brightness.
        /// </summary>
        /// <param name="buttonIndex">Power button index (0-3)</param>
        /// <returns>Current pulse value from 0 (dimmest) to 1 (brightest)</returns>
        public float GetButtonPulseBrightness(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= 5) return 0f;

            // Calculate the same way as UpdateGlowEffects()
            // Uses unscaledTime to match the glow animation (works during pause)
            float phaseOffset = buttonIndex * 0.5f;
            float phase = Time.unscaledTime * glowPulseSpeed + phaseOffset;
            float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;
            // Apply same smoothing
            pulse = Mathf.Pow(pulse, 0.7f);
            return pulse;
        }

        /// <summary>
        /// Calculates the time (in seconds) until a specific power button reaches peak brightness.
        /// Used by tutorial system to wait for optimal visual moment before pausing.
        /// </summary>
        /// <param name="buttonIndex">Power button index (0-3)</param>
        /// <returns>Time in seconds until next peak brightness (0 to period/2)</returns>
        public float GetTimeUntilPeakBrightness(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= 5) return 0f;

            // Calculate current phase (uses unscaledTime to match glow animation)
            float phaseOffset = buttonIndex * 0.5f;
            float currentPhase = Time.unscaledTime * glowPulseSpeed + phaseOffset;

            // Sine peaks at π/2, 5π/2, 9π/2, etc. (π/2 + n*2π)
            // Normalize current phase to 0 to 2π range
            float normalizedPhase = currentPhase % (2f * Mathf.PI);
            if (normalizedPhase < 0) normalizedPhase += 2f * Mathf.PI;

            // Find distance to next peak (π/2)
            float targetPhase = Mathf.PI / 2f;
            float phaseToGo;

            if (normalizedPhase <= targetPhase)
            {
                // Peak is ahead in this cycle
                phaseToGo = targetPhase - normalizedPhase;
            }
            else
            {
                // Peak is in the next cycle
                phaseToGo = (2f * Mathf.PI - normalizedPhase) + targetPhase;
            }

            // Convert phase to time
            return phaseToGo / glowPulseSpeed;
        }

        /// <summary>
        /// Gets whether a power button is currently at or near peak brightness.
        /// </summary>
        /// <param name="buttonIndex">Power button index (0-3)</param>
        /// <param name="threshold">Brightness threshold (0.9 = 90% of max)</param>
        /// <returns>True if button is at or above threshold brightness</returns>
        public bool IsButtonAtPeakBrightness(int buttonIndex, float threshold = 0.9f)
        {
            return GetButtonPulseBrightness(buttonIndex) >= threshold;
        }

        /// <summary>
        /// Locks a specific power button at peak brightness for tutorial highlighting.
        /// While locked, the button will always render at maximum glow regardless of the animation cycle.
        /// </summary>
        /// <param name="buttonIndex">Power button index (0-3) to lock, or -1 to unlock all</param>
        public void LockButtonAtPeakBrightness(int buttonIndex)
        {
            lockedAtPeakButtonIndex = buttonIndex;
        }

        /// <summary>
        /// Unlocks any button that was locked at peak brightness.
        /// The button will resume normal glow animation.
        /// </summary>
        public void UnlockButtonBrightness()
        {
            lockedAtPeakButtonIndex = -1;
        }

        /// <summary>
        /// Locks all power buttons during tutorial. No power can be activated until
        /// explicitly unlocked via EnablePowerForTutorial().
        /// </summary>
        public void SetTutorialPowerLock(bool locked)
        {
            tutorialPowersLocked = locked;
            if (!locked)
            {
                tutorialUnlockedPowers.Clear();
            }
        }

        /// <summary>
        /// Unlocks a specific power button during tutorial, allowing the player to activate it.
        /// Clears all previously unlocked powers first so only one power is ever available at a time.
        /// </summary>
        public void EnablePowerForTutorial(int powerIndex)
        {
            tutorialUnlockedPowers.Clear();
            tutorialUnlockedPowers.Add(powerIndex);
        }

        /// <summary>
        /// Re-locks ALL power buttons during tutorial.
        /// Used after a power activation trigger fires to ensure no powers remain unlocked.
        /// </summary>
        public void DisableAllPowersForTutorial()
        {
            tutorialUnlockedPowers.Clear();
        }

        #endregion
    }
}

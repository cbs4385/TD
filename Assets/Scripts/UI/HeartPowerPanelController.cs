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

            Debug.Log("[HeartPowerPanel] Heart Powers panel initialized");
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

            // Handle keyboard shortcuts (1-7 keys)
            HandleKeyboardInput();
        }

        private void OnEnable()
        {
            if (heartPowerManager != null)
            {
                heartPowerManager.OnChargesChanged += UpdateChargesDisplay;
                heartPowerManager.OnEssenceChanged += UpdateEssenceDisplay;
                heartPowerManager.OnPowerActivated += OnPowerActivated;
            }

            // Also subscribe to GameController essence changes for real-time updates
            if (GameController.Instance != null)
            {
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
                    successCount++;
                    Debug.Log($"[HeartPowerPanel] Button {i} listener added successfully");
                }
                else
                {
                    Debug.LogWarning($"[HeartPowerPanel] Button {i} is null, skipping listener setup");
                }
            }

            Debug.Log($"[HeartPowerPanel] Initialized {successCount}/{powerButtons.Length} button listeners");

            // Initialize resource displays
            UpdateResourceDisplays();

            // Panel starts visible
            if (heartPowersPanel != null)
            {
                heartPowersPanel.SetActive(true);
                Debug.Log("[HeartPowerPanel] Panel set to active (visible)");
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
            canvas.sortingOrder = 1; // Lower priority, let other UI be on top

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;

            Debug.Log("[HeartPowerPanel] Created new Canvas with GraphicRaycaster, sortingOrder=1");

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
            image.color = new Color(0.4f, 0.2f, 0.5f, 1f); // Purple
            image.raycastTarget = true; // Ensure it can be clicked

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = true; // Ensure button is interactable

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
            text.enableWordWrapping = true;
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

            // Get mouse world position for targeted powers
            Vector3 targetPosition = GetMouseWorldPosition();

            // Check if this is a targeted power
            bool isTargetedPower = powerType == HeartPowerType.MurmuringPaths ||
                                   powerType == HeartPowerType.DreamSnare ||
                                   powerType == HeartPowerType.PukasBargain ||
                                   powerType == HeartPowerType.FeastwardPanic;

            bool success;
            if (isTargetedPower)
            {
                Debug.Log($"[HeartPowerPanel] Activating targeted power at world position: {targetPosition}");
                success = heartPowerManager.TryActivatePower(powerType, targetPosition);
            }
            else
            {
                Debug.Log($"[HeartPowerPanel] Activating global power");
                success = heartPowerManager.TryActivatePower(powerType);
            }

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

        /// <summary>
        /// Called when a power is successfully activated.
        /// </summary>
        private void OnPowerActivated(HeartPowerType powerType)
        {
            Debug.Log($"[HeartPowerPanel] Power activated event received: {powerType}");
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

        #region Display Updates

        /// <summary>
        /// Updates all resource displays.
        /// </summary>
        private void UpdateResourceDisplays()
        {
            if (heartPowerManager != null)
            {
                UpdateChargesDisplay(heartPowerManager.CurrentCharges);
                UpdateEssenceDisplay(heartPowerManager.CurrentEssence);
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
                powerButtons[i].interactable = canActivate;

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

                // Visual feedback: dim button when not available
                Image buttonImage = powerButtons[i].GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = canActivate
                        ? new Color(0.4f, 0.2f, 0.5f, 1f)  // Normal purple
                        : new Color(0.2f, 0.1f, 0.25f, 0.7f); // Dimmed
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
                Debug.Log($"[HeartPowerPanel] Panel toggled: {(newState ? "visible" : "hidden")}");
            }
        }

        /// <summary>
        /// Gets the mouse position in world space.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 mousePos = Mouse.current.position.ReadValue();
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(mousePos);
        }

        #endregion
    }
}

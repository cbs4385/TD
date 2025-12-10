using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// UI controller for Heart powers - displays power buttons, cooldowns, and resources.
    /// </summary>
    public class HeartPowerUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References (Optional - will auto-create if null)")]
        [SerializeField]
        [Tooltip("Reference to the HeartPowerManager (will auto-find if null)")]
        private HeartPowerManager heartPowerManager;

        [Header("Resource Display (Optional - will auto-create if null)")]
        [SerializeField]
        [Tooltip("Text displaying current Heart charges")]
        private TextMeshProUGUI chargesText;

        [SerializeField]
        [Tooltip("Text displaying current essence")]
        private TextMeshProUGUI essenceText;

        [SerializeField]
        [Tooltip("Panel containing the resource display")]
        private GameObject resourcePanel;

        [Header("Power Buttons")]
        [SerializeField]
        [Tooltip("Button for Heartbeat of Longing (Key: 1)")]
        private Button heartbeatButton;

        [SerializeField]
        [Tooltip("Button for Murmuring Paths (Key: 2)")]
        private Button murmuringButton;

        [SerializeField]
        [Tooltip("Button for Dream Snare (Key: 3)")]
        private Button dreamSnareButton;

        [SerializeField]
        [Tooltip("Button for Feastward Panic (Key: 4)")]
        private Button feastwardButton;

        [SerializeField]
        [Tooltip("Button for Covenant with Wisps (Key: 5)")]
        private Button covenantButton;

        [SerializeField]
        [Tooltip("Button for Puka's Bargain (Key: 6)")]
        private Button pukaButton;

        [SerializeField]
        [Tooltip("Button for Ring of Invitations (Key: 7)")]
        private Button ringButton;

        [Header("Button Text Elements")]
        [SerializeField]
        [Tooltip("Text elements for displaying cooldowns on buttons")]
        private TextMeshProUGUI[] buttonCooldownTexts;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Enable keyboard shortcuts (1-7)")]
        private bool enableKeyboardShortcuts = true;

        #endregion

        #region Private Fields

        private Dictionary<HeartPowerType, Button> powerButtons = new Dictionary<HeartPowerType, Button>();
        private Dictionary<HeartPowerType, TextMeshProUGUI> cooldownTexts = new Dictionary<HeartPowerType, TextMeshProUGUI>();
        private Camera mainCamera;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            mainCamera = Camera.main;

            // Find HeartPowerManager if not assigned
            if (heartPowerManager == null)
            {
                heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            }

            // Map buttons to power types
            if (heartbeatButton != null)
                powerButtons[HeartPowerType.HeartbeatOfLonging] = heartbeatButton;
            if (murmuringButton != null)
                powerButtons[HeartPowerType.MurmuringPaths] = murmuringButton;
            if (dreamSnareButton != null)
                powerButtons[HeartPowerType.DreamSnare] = dreamSnareButton;
            if (feastwardButton != null)
                powerButtons[HeartPowerType.FeastwardPanic] = feastwardButton;
            if (covenantButton != null)
                powerButtons[HeartPowerType.CovenantWithWisps] = covenantButton;
            if (pukaButton != null)
                powerButtons[HeartPowerType.PukasBargain] = pukaButton;
            if (ringButton != null)
                powerButtons[HeartPowerType.RingOfInvitations] = ringButton;

            // Map cooldown texts
            if (buttonCooldownTexts != null && buttonCooldownTexts.Length >= 7)
            {
                cooldownTexts[HeartPowerType.HeartbeatOfLonging] = buttonCooldownTexts[0];
                cooldownTexts[HeartPowerType.MurmuringPaths] = buttonCooldownTexts[1];
                cooldownTexts[HeartPowerType.DreamSnare] = buttonCooldownTexts[2];
                cooldownTexts[HeartPowerType.FeastwardPanic] = buttonCooldownTexts[3];
                cooldownTexts[HeartPowerType.CovenantWithWisps] = buttonCooldownTexts[4];
                cooldownTexts[HeartPowerType.PukasBargain] = buttonCooldownTexts[5];
                cooldownTexts[HeartPowerType.RingOfInvitations] = buttonCooldownTexts[6];
            }

            SetupButtons();
        }

        private void OnEnable()
        {
            // Auto-create resource display if not assigned
            if (chargesText == null || essenceText == null)
            {
                CreateResourceDisplayUI();
            }

            if (heartPowerManager != null)
            {
                heartPowerManager.OnChargesChanged += UpdateChargesDisplay;
                heartPowerManager.OnEssenceChanged += UpdateEssenceDisplay;
            }
        }

        private void OnDisable()
        {
            if (heartPowerManager != null)
            {
                heartPowerManager.OnChargesChanged -= UpdateChargesDisplay;
                heartPowerManager.OnEssenceChanged -= UpdateEssenceDisplay;
            }
        }

        private void Start()
        {
            UpdateResourceDisplays();
        }

        private void Update()
        {
            // Update cooldown displays
            UpdateCooldownDisplays();

            // Handle keyboard shortcuts
            if (enableKeyboardShortcuts)
            {
                HandleKeyboardInput();
            }
        }

        #endregion

        #region Button Setup

        private void SetupButtons()
        {
            if (heartbeatButton != null)
                heartbeatButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.HeartbeatOfLonging));
            if (murmuringButton != null)
                murmuringButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.MurmuringPaths));
            if (dreamSnareButton != null)
                dreamSnareButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.DreamSnare));
            if (feastwardButton != null)
                feastwardButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.FeastwardPanic));
            if (covenantButton != null)
                covenantButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.CovenantWithWisps));
            if (pukaButton != null)
                pukaButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.PukasBargain));
            if (ringButton != null)
                ringButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.RingOfInvitations));
        }

        #endregion

        #region Power Activation

        private void OnPowerButtonClicked(HeartPowerType powerType)
        {
            ActivatePower(powerType);
        }

        private void ActivatePower(HeartPowerType powerType)
        {
            if (heartPowerManager == null)
            {
                return;
            }

            // For targeted powers, use mouse position
            Vector3 targetPosition = GetMouseWorldPosition();

            // Check if power requires targeting
            bool isTargetedPower = powerType == HeartPowerType.MurmuringPaths ||
                                   powerType == HeartPowerType.DreamSnare ||
                                   powerType == HeartPowerType.PukasBargain ||
                                   powerType == HeartPowerType.FeastwardPanic;

            if (isTargetedPower)
            {
                heartPowerManager.TryActivatePower(powerType, targetPosition);
            }
            else
            {
                heartPowerManager.TryActivatePower(powerType);
            }
        }

        #endregion

        #region Keyboard Input

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.HeartbeatOfLonging);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.MurmuringPaths);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.DreamSnare);
            }
            else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.FeastwardPanic);
            }
            else if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.CovenantWithWisps);
            }
            else if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.PukasBargain);
            }
            else if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame)
            {
                ActivatePower(HeartPowerType.RingOfInvitations);
            }
        }

        #endregion

        #region Display Updates

        private void UpdateResourceDisplays()
        {
            if (heartPowerManager != null)
            {
                UpdateChargesDisplay(heartPowerManager.CurrentCharges);
                UpdateEssenceDisplay(heartPowerManager.CurrentEssence);
            }
        }

        private void UpdateChargesDisplay(int charges)
        {
            if (chargesText != null)
            {
                chargesText.text = $"Heart Charges: {charges}";
            }
        }

        private void UpdateEssenceDisplay(int essence)
        {
            if (essenceText != null)
            {
                essenceText.text = $"Essence: {essence}";
            }
        }

        private void UpdateCooldownDisplays()
        {
            if (heartPowerManager == null)
            {
                return;
            }

            foreach (var kvp in powerButtons)
            {
                HeartPowerType powerType = kvp.Key;
                Button button = kvp.Value;

                if (button == null)
                {
                    continue;
                }

                // Update button interactability
                bool canActivate = heartPowerManager.CanActivatePower(powerType, out string reason);
                button.interactable = canActivate;

                // Update cooldown text
                if (cooldownTexts.TryGetValue(powerType, out TextMeshProUGUI cooldownText) && cooldownText != null)
                {
                    float cooldownRemaining = heartPowerManager.GetCooldownRemaining(powerType);
                    if (cooldownRemaining > 0)
                    {
                        cooldownText.text = $"{cooldownRemaining:F1}s";
                        cooldownText.gameObject.SetActive(true);
                    }
                    else
                    {
                        cooldownText.gameObject.SetActive(false);
                    }
                }
            }
        }

        #endregion

        #region Utility

        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(mousePos);
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Automatically creates the resource display UI if not manually set up.
        /// </summary>
        private void CreateResourceDisplayUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            // Create the resource panel
            resourcePanel = CreateResourcePanel(canvas.transform);

            // Create resource texts
            float yPos = -15f;
            chargesText = CreateResourceText(resourcePanel.transform, "Heart Charges: 0", yPos);
            yPos -= 35f;
            essenceText = CreateResourceText(resourcePanel.transform, "Essence: 0", yPos);
        }

        /// <summary>
        /// Creates a Canvas for the UI.
        /// </summary>
        private Canvas CreateCanvas()
        {
            GameObject canvasObj = new GameObject("HeartPowerCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1; // Render on top of other UI

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Creates the resource display panel.
        /// </summary>
        private GameObject CreateResourcePanel(Transform parent)
        {
            GameObject panel = new GameObject("HeartPowerResourceDisplay");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.sizeDelta = new Vector2(250f, 100f);

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            return panel;
        }

        /// <summary>
        /// Creates a text element for displaying resource values.
        /// </summary>
        private TextMeshProUGUI CreateResourceText(Transform parent, string text, float yPos)
        {
            GameObject textObj = new GameObject("ResourceText");
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(230f, 30f);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return tmp;
        }

        #endregion
    }
}

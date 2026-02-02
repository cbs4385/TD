using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using FaeMaze.Cameras;
using FaeMaze.Systems;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// UI controller for Heart powers - displays power buttons, cooldowns, and resources.
    /// </summary>
    public class HeartPowerUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the HeartPowerManager")]
        private HeartPowerManager heartPowerManager;

        [SerializeField]
        [Tooltip("Reference to the CameraController3D")]
        private CameraController3D cameraController;

        [Header("Resource Display")]
        [SerializeField]
        [Tooltip("Text displaying current essence")]
        private TextMeshProUGUI essenceText;

        [Header("Power Buttons")]
        [SerializeField]
        [Tooltip("Button for Murmuring Paths (Key: 1)")]
        private Button murmuringButton;

        [SerializeField]
        [Tooltip("Button for Heartward Grasp (Key: 8)")]
        private Button graspButton;

        [SerializeField]
        [Tooltip("Button for Devouring Maw (Key: 9)")]
        private Button devourButton;

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

            // Find CameraController3D if not assigned
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController3D>();
            }

            // Map buttons to power types
            if (murmuringButton != null)
                powerButtons[HeartPowerType.MurmuringPaths] = murmuringButton;
            if (graspButton != null)
                powerButtons[HeartPowerType.HeartwardGrasp] = graspButton;
            if (devourButton != null)
                powerButtons[HeartPowerType.DevouringMaw] = devourButton;

            // Map cooldown texts
            if (buttonCooldownTexts != null && buttonCooldownTexts.Length >= 3)
            {
                cooldownTexts[HeartPowerType.MurmuringPaths] = buttonCooldownTexts[0];
                cooldownTexts[HeartPowerType.HeartwardGrasp] = buttonCooldownTexts[1];
                cooldownTexts[HeartPowerType.DevouringMaw] = buttonCooldownTexts[2];
            }

            SetupButtons();
        }

        private void OnEnable()
        {
            if (heartPowerManager != null)
            {
                heartPowerManager.OnEssenceChanged += UpdateEssenceDisplay;
            }
        }

        private void OnDisable()
        {
            if (heartPowerManager != null)
            {
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

            // Handle targeting mode for targeted powers
            HandleTargetingMode();
        }

        #endregion

        #region Button Setup

        private void SetupButtons()
        {
            if (murmuringButton != null)
                murmuringButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.MurmuringPaths));
            if (graspButton != null)
                graspButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.HeartwardGrasp));
            if (devourButton != null)
                devourButton.onClick.AddListener(() => OnPowerButtonClicked(HeartPowerType.DevouringMaw));
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

        #endregion

        #region Keyboard Input

        private void HandleKeyboardInput()
        {
            // Use InputBindingHelper for configurable key/mouse bindings
            string power1Binding = GameSettings.HeartPower1Binding;
            if (InputBindingHelper.WasBindingPressedThisFrame(power1Binding))
            {
                ActivatePower(HeartPowerType.MurmuringPaths);
            }
            else if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.HeartPower2Binding))
            {
                ActivatePower(HeartPowerType.HeartwardGrasp);
            }
            else if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.HeartPower3Binding))
            {
                ActivatePower(HeartPowerType.DevouringMaw);
            }
            else if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.HeartPower4Binding))
            {
                ActivatePower(HeartPowerType.Sculpting);
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

        private void UpdateResourceDisplays()
        {
            if (heartPowerManager != null)
            {
                UpdateEssenceDisplay(heartPowerManager.CurrentEssence);
            }
        }

        private void UpdateEssenceDisplay(int essence)
        {
            if (essenceText != null)
            {
                essenceText.text = $"Threads: {essence}";
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

        /// <summary>
        /// Gets the mouse position in world space.
        /// Converts screen coordinates to world coordinates for 2D orthographic camera.
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            // Get mouse screen position using new Input System
            Vector3 mouseScreenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector3.zero;

            // Convert to world position for orthographic camera
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0; // Ensure Z=0 for 2D game

            return mouseWorldPos;
        }

        #endregion
    }
}

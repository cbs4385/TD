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
    /// UI controller for Heart powers - displays power buttons and resources.
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

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Enable keyboard shortcuts (1-7)")]
        private bool enableKeyboardShortcuts = true;

        #endregion

        #region Private Fields

        private Dictionary<HeartPowerType, Button> powerButtons = new Dictionary<HeartPowerType, Button>();
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
            // Update button interactability
            UpdateButtonStates();

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

        // Gamepad debug logging for power bindings
        private static float lastPowerBindingLog = 0f;
        private const float POWER_BINDING_LOG_INTERVAL = 10f;

        private void HandleKeyboardInput()
        {
            // Periodically log what binding strings are configured for powers
            if (Time.time - lastPowerBindingLog > POWER_BINDING_LOG_INTERVAL)
            {
                lastPowerBindingLog = Time.time;
                Debug.Log($"[HeartPowerUI] Current power bindings: " +
                          $"P1='{GameSettings.HeartPower1Binding}' (isGamepad={InputBindingHelper.IsGamepadBinding(GameSettings.HeartPower1Binding)}), " +
                          $"P2='{GameSettings.HeartPower2Binding}' (isGamepad={InputBindingHelper.IsGamepadBinding(GameSettings.HeartPower2Binding)}), " +
                          $"P3='{GameSettings.HeartPower3Binding}' (isGamepad={InputBindingHelper.IsGamepadBinding(GameSettings.HeartPower3Binding)}), " +
                          $"P4='{GameSettings.HeartPower4Binding}' (isGamepad={InputBindingHelper.IsGamepadBinding(GameSettings.HeartPower4Binding)})");
            }

            // Use InputBindingHelper for configurable key/mouse bindings
            // Check all three columns (primary, alt, tertiary) for each power
            if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower1Binding, GameSettings.HeartPower1AltBinding, GameSettings.HeartPower1TertiaryBinding))
            {
                Debug.Log($"[HeartPowerUI] Power 1 (MurmuringPaths) ACTIVATED");
                ActivatePower(HeartPowerType.MurmuringPaths);
            }
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower2Binding, GameSettings.HeartPower2AltBinding, GameSettings.HeartPower2TertiaryBinding))
            {
                Debug.Log($"[HeartPowerUI] Power 2 (HeartwardGrasp) ACTIVATED");
                ActivatePower(HeartPowerType.HeartwardGrasp);
            }
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower3Binding, GameSettings.HeartPower3AltBinding, GameSettings.HeartPower3TertiaryBinding))
            {
                Debug.Log($"[HeartPowerUI] Power 3 (DevouringMaw) ACTIVATED");
                ActivatePower(HeartPowerType.DevouringMaw);
            }
            else if (InputBindingHelper.WasAnyBindingPressedThisFrame(
                GameSettings.HeartPower4Binding, GameSettings.HeartPower4AltBinding, GameSettings.HeartPower4TertiaryBinding))
            {
                Debug.Log($"[HeartPowerUI] Power 4 (Sculpting) ACTIVATED via binding '{GameSettings.HeartPower4Binding}'");
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

        private void UpdateButtonStates()
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

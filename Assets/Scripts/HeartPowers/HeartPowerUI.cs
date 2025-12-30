using UnityEngine;
using UnityEngine.UI;
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
        [Tooltip("Text displaying current Heart charges")]
        private TextMeshProUGUI chargesText;

        [SerializeField]
        [Tooltip("Text displaying current essence")]
        private TextMeshProUGUI essenceText;

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
            if (graspButton != null)
                powerButtons[HeartPowerType.HeartwardGrasp] = graspButton;
            if (devourButton != null)
                powerButtons[HeartPowerType.DevouringMaw] = devourButton;

            // Map cooldown texts
            if (buttonCooldownTexts != null && buttonCooldownTexts.Length >= 9)
            {
                cooldownTexts[HeartPowerType.HeartbeatOfLonging] = buttonCooldownTexts[0];
                cooldownTexts[HeartPowerType.MurmuringPaths] = buttonCooldownTexts[1];
                cooldownTexts[HeartPowerType.DreamSnare] = buttonCooldownTexts[2];
                cooldownTexts[HeartPowerType.FeastwardPanic] = buttonCooldownTexts[3];
                cooldownTexts[HeartPowerType.CovenantWithWisps] = buttonCooldownTexts[4];
                cooldownTexts[HeartPowerType.PukasBargain] = buttonCooldownTexts[5];
                cooldownTexts[HeartPowerType.RingOfInvitations] = buttonCooldownTexts[6];
                cooldownTexts[HeartPowerType.HeartwardGrasp] = buttonCooldownTexts[7];
                cooldownTexts[HeartPowerType.DevouringMaw] = buttonCooldownTexts[8];
            }

            SetupButtons();
        }

        private void OnEnable()
        {
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

            // Handle targeting mode for targeted powers
            HandleTargetingMode();
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
            bool success = heartPowerManager.TryActivatePower(powerType, targetPosition);

            if (success)
            {
                Debug.Log($"[HeartPowerUI] Activated {powerType} at focal point position {targetPosition}");
            }
            else
            {
                heartPowerManager.CanActivatePower(powerType, out string reason);
                Debug.Log($"[HeartPowerUI] Failed to activate {powerType}: {reason}");
            }
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
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                ActivatePower(HeartPowerType.HeartbeatOfLonging);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                ActivatePower(HeartPowerType.MurmuringPaths);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                ActivatePower(HeartPowerType.DreamSnare);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                ActivatePower(HeartPowerType.FeastwardPanic);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5))
            {
                ActivatePower(HeartPowerType.CovenantWithWisps);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6))
            {
                ActivatePower(HeartPowerType.PukasBargain);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7))
            {
                ActivatePower(HeartPowerType.RingOfInvitations);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8))
            {
                ActivatePower(HeartPowerType.HeartwardGrasp);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
            {
                ActivatePower(HeartPowerType.DevouringMaw);
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

            // Get mouse screen position
            Vector3 mouseScreenPos = Input.mousePosition;

            // Convert to world position for orthographic camera
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, mainCamera.nearClipPlane));
            mouseWorldPos.z = 0; // Ensure Z=0 for 2D game

            return mouseWorldPos;
        }

        #endregion
    }
}

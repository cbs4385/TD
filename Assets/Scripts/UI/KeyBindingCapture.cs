using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using System;

namespace FaeMaze.UI
{
    /// <summary>
    /// Component that handles "press any key" input capture for key binding UI.
    /// Uses a separate checkbox (Toggle) to activate capture mode, avoiding conflicts with left-click binding.
    /// The checkbox activates capture mode, then the next input is captured and bound.
    ///
    /// Features:
    /// - Left toggle checkbox: click to enter capture mode, then press any key to bind
    /// - Clear button (X): appears on the RIGHT side when a binding is set, click to clear
    /// - Text display: shows current binding or "Press any key..." during capture
    /// </summary>
    public class KeyBindingCapture : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Text element to show current binding")]
        private TextMeshProUGUI bindingText;

        [SerializeField]
        [Tooltip("Toggle/checkbox to activate capture mode")]
        private Toggle captureToggle;

        [SerializeField]
        [Tooltip("Button to clear the current binding (optional, created at runtime if not assigned)")]
        private Button clearButton;

        [Header("Display")]
        [SerializeField]
        [Tooltip("Text to display while waiting for input")]
        private string capturePrompt = "Press any key...";

        [SerializeField]
        [Tooltip("Text to display when no binding is set")]
        private string unsetText = "-";

        [Header("Styling")]
        [SerializeField]
        private Color normalTextColor = Color.white;

        [SerializeField]
        private Color captureTextColor = new Color(1f, 0.8f, 0.3f, 1f); // Yellow-orange for capture mode

        [SerializeField]
        private Color unsetTextColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray for unset

        [SerializeField]
        private Color clearButtonColor = new Color(0.6f, 0.2f, 0.2f, 1f); // Dark red for clear button

        private string currentBinding;
        private bool isCapturing = false;
        private GameObject clearButtonObj; // Runtime-created clear button

        /// <summary>
        /// Event fired when a new binding is captured.
        /// Parameter is the binding string (e.g., "W", "Mouse1", "F12").
        /// Empty string means binding was cleared.
        /// </summary>
        public event Action<string> OnBindingCaptured;

        private void Awake()
        {
            // Find toggle if not assigned - look for Toggle component on this object or children
            if (captureToggle == null)
            {
                captureToggle = GetComponentInChildren<Toggle>(true);
            }

            if (captureToggle != null)
            {
                captureToggle.onValueChanged.AddListener(OnToggleChanged);
                captureToggle.isOn = false;
            }

            // Find clear button if not assigned
            if (clearButton == null)
            {
                // Look for a button named "ClearButton" in children
                foreach (Transform child in transform)
                {
                    if (child.name == "ClearButton")
                    {
                        clearButton = child.GetComponent<Button>();
                        break;
                    }
                }
            }

            // Create clear button at runtime if not found
            if (clearButton == null)
            {
                CreateClearButton();
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearClicked);
            }

            // Initial visibility update
            UpdateClearButtonVisibility();
        }

        private void OnDestroy()
        {
            if (captureToggle != null)
            {
                captureToggle.onValueChanged.RemoveListener(OnToggleChanged);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(OnClearClicked);
            }
        }

        /// <summary>
        /// Creates a clear button (X) on the right side of the binding display.
        /// </summary>
        private void CreateClearButton()
        {
            // Find the background/container for the binding text
            Transform bgTransform = null;
            foreach (Transform child in transform)
            {
                if (child.name == "Background" || child.GetComponent<Image>() != null)
                {
                    // Skip the toggle
                    if (child.GetComponent<Toggle>() != null || child.name == "CaptureToggle")
                        continue;
                    bgTransform = child;
                    break;
                }
            }

            if (bgTransform == null)
            {
                // Fall back to this transform if no background found
                bgTransform = transform;
            }

            clearButtonObj = new GameObject("ClearButton");
            clearButtonObj.transform.SetParent(bgTransform, false);

            RectTransform clearRect = clearButtonObj.AddComponent<RectTransform>();
            clearRect.anchorMin = new Vector2(1, 0.5f);
            clearRect.anchorMax = new Vector2(1, 0.5f);
            clearRect.pivot = new Vector2(1, 0.5f);
            clearRect.anchoredPosition = new Vector2(-4, 0);
            clearRect.sizeDelta = new Vector2(20, 20);

            Image clearBg = clearButtonObj.AddComponent<Image>();
            clearBg.color = clearButtonColor;

            clearButton = clearButtonObj.AddComponent<Button>();
            clearButton.targetGraphic = clearBg;

            // Add X text
            GameObject xTextObj = new GameObject("XText");
            xTextObj.transform.SetParent(clearButtonObj.transform, false);

            RectTransform xRect = xTextObj.AddComponent<RectTransform>();
            xRect.anchorMin = Vector2.zero;
            xRect.anchorMax = Vector2.one;
            xRect.offsetMin = Vector2.zero;
            xRect.offsetMax = Vector2.zero;

            TextMeshProUGUI xText = xTextObj.AddComponent<TextMeshProUGUI>();
            xText.text = "X";
            xText.fontSize = 14;
            xText.fontStyle = FontStyles.Bold;
            xText.alignment = TextAlignmentOptions.Center;
            xText.color = Color.white;
        }

        /// <summary>
        /// Updates the visibility of the clear button based on whether a binding is set.
        /// </summary>
        private void UpdateClearButtonVisibility()
        {
            bool hasBinding = !string.IsNullOrEmpty(currentBinding);
            bool showClearButton = hasBinding && !isCapturing;

            if (clearButton != null)
            {
                clearButton.gameObject.SetActive(showClearButton);
            }
            else if (clearButtonObj != null)
            {
                clearButtonObj.SetActive(showClearButton);
            }
        }

        /// <summary>
        /// Called when the clear button is clicked.
        /// </summary>
        private void OnClearClicked()
        {
            ClearBinding();
        }

        /// <summary>
        /// Clears the current binding.
        /// </summary>
        public void ClearBinding()
        {
            currentBinding = "";
            UpdateDisplayText();
            UpdateClearButtonVisibility();
            OnBindingCaptured?.Invoke("");
        }

        /// <summary>
        /// Initialize the control with a binding value.
        /// Pass null or empty string to show unset state.
        /// </summary>
        public void SetBinding(string binding)
        {
            currentBinding = binding ?? "";
            UpdateDisplayText();
        }

        /// <summary>
        /// Get the current binding value.
        /// </summary>
        public string GetBinding()
        {
            return currentBinding;
        }

        /// <summary>
        /// Check if currently in capture mode.
        /// </summary>
        public bool IsCapturing => isCapturing;

        private void OnToggleChanged(bool isOn)
        {
            if (isOn)
            {
                StartCapture();
            }
            else
            {
                // If toggle is turned off manually, cancel capture
                if (isCapturing)
                {
                    CancelCapture();
                }
            }
        }

        private void StartCapture()
        {
            if (isCapturing)
                return;

            isCapturing = true;
            UpdateDisplayText();

            // Notify OptionsManager that we're capturing (for exclusive capture handling)
            var optionsManager = FindFirstObjectByType<OptionsManager>();
            if (optionsManager != null)
            {
                optionsManager.OnBindingCaptureStarted(this);
            }
        }

        /// <summary>
        /// Cancel capture mode and restore previous binding.
        /// Called by OptionsManager when Escape is pressed or another capture starts.
        /// </summary>
        public void CancelCapture()
        {
            if (!isCapturing)
                return;

            isCapturing = false;

            // Turn off the toggle without triggering another callback
            if (captureToggle != null && captureToggle.isOn)
            {
                captureToggle.SetIsOnWithoutNotify(false);
            }

            UpdateDisplayText();
        }

        // Threshold for detecting stick movement as a binding
        private const float STICK_THRESHOLD = 0.7f;

        private void Update()
        {
            if (!isCapturing)
                return;

            // Check for Escape to cancel
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelCapture();
                return;
            }

            // Check gamepad inputs (before mouse/keyboard for better responsiveness)
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                // Check gamepad buttons
                if (gamepad.buttonSouth.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadButtonSouth");
                    return;
                }
                if (gamepad.buttonNorth.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadButtonNorth");
                    return;
                }
                if (gamepad.buttonEast.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadButtonEast");
                    return;
                }
                if (gamepad.buttonWest.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadButtonWest");
                    return;
                }
                if (gamepad.leftShoulder.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadLeftShoulder");
                    return;
                }
                if (gamepad.rightShoulder.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadRightShoulder");
                    return;
                }
                if (gamepad.leftTrigger.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadLeftTrigger");
                    return;
                }
                if (gamepad.rightTrigger.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadRightTrigger");
                    return;
                }
                if (gamepad.leftStickButton.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadLeftStickPress");
                    return;
                }
                if (gamepad.rightStickButton.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadRightStickPress");
                    return;
                }
                if (gamepad.startButton.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadStart");
                    return;
                }
                if (gamepad.selectButton.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadSelect");
                    return;
                }

                // Check D-pad
                if (gamepad.dpad.up.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadDpadUp");
                    return;
                }
                if (gamepad.dpad.down.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadDpadDown");
                    return;
                }
                if (gamepad.dpad.left.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadDpadLeft");
                    return;
                }
                if (gamepad.dpad.right.wasPressedThisFrame)
                {
                    CompleteCapture("GamepadDpadRight");
                    return;
                }

                // Check analog sticks (directional - requires holding past threshold)
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                Vector2 rightStick = gamepad.rightStick.ReadValue();

                // Left stick directions
                if (leftStick.y > STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadLeftStickUp");
                    return;
                }
                if (leftStick.y < -STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadLeftStickDown");
                    return;
                }
                if (leftStick.x < -STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadLeftStickLeft");
                    return;
                }
                if (leftStick.x > STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadLeftStickRight");
                    return;
                }

                // Right stick directions
                if (rightStick.y > STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadRightStickUp");
                    return;
                }
                if (rightStick.y < -STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadRightStickDown");
                    return;
                }
                if (rightStick.x < -STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadRightStickLeft");
                    return;
                }
                if (rightStick.x > STICK_THRESHOLD)
                {
                    CompleteCapture("GamepadRightStickRight");
                    return;
                }
            }

            // Check mouse buttons
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                // Left click - now works cleanly since checkbox activation is separate
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    CompleteCapture("Mouse0");
                    return;
                }
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    CompleteCapture("Mouse1");
                    return;
                }
                if (mouse.middleButton.wasPressedThisFrame)
                {
                    CompleteCapture("Mouse2");
                    return;
                }
                if (mouse.forwardButton.wasPressedThisFrame)
                {
                    CompleteCapture("Mouse3");
                    return;
                }
                if (mouse.backButton.wasPressedThisFrame)
                {
                    CompleteCapture("Mouse4");
                    return;
                }
            }

            // Check all keyboard keys
            if (keyboard != null)
            {
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape)
                    {
                        string binding = FaeMaze.Systems.InputBindingHelper.KeyToBindingString(key.keyCode);
                        CompleteCapture(binding);
                        return;
                    }
                }
            }
        }

        private void CompleteCapture(string newBinding)
        {
            isCapturing = false;
            currentBinding = newBinding;

            // Turn off the toggle without triggering another callback
            if (captureToggle != null && captureToggle.isOn)
            {
                captureToggle.SetIsOnWithoutNotify(false);
            }

            UpdateDisplayText();
            OnBindingCaptured?.Invoke(newBinding);

            // Notify OptionsManager that capture completed
            var optionsManager = FindFirstObjectByType<OptionsManager>();
            if (optionsManager != null)
            {
                optionsManager.OnBindingCaptureCompleted(this);
            }
        }

        private void UpdateDisplayText()
        {
            if (bindingText == null)
                return;

            if (isCapturing)
            {
                bindingText.text = capturePrompt;
                bindingText.color = captureTextColor;
            }
            else if (string.IsNullOrEmpty(currentBinding))
            {
                bindingText.text = unsetText;
                bindingText.color = unsetTextColor;
            }
            else
            {
                bindingText.text = FaeMaze.Systems.InputBindingHelper.GetDisplayName(currentBinding);
                bindingText.color = normalTextColor;
            }

            UpdateClearButtonVisibility();
        }
    }
}

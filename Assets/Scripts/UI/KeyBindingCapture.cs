using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using System;
using System.Collections.Generic;

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

        // Combo capture support
        private const float COMBO_WINDOW = 0.2f; // Seconds to wait for additional simultaneous inputs
        private List<string> comboBuffer = new List<string>();
        private float comboTimer = 0f;
        private bool isAccumulatingCombo = false;

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
            ResetComboState();

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

            // Check for Escape to cancel (always immediate, never part of a combo)
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ResetComboState();
                CancelCapture();
                return;
            }

            // If we're accumulating a combo, check for new inputs to add
            if (isAccumulatingCombo)
            {
                // Collect any newly pressed inputs this frame
                CollectNewlyPressedInputs();

                // Count down the combo window timer
                comboTimer -= Time.unscaledDeltaTime;

                // Update display to show combo-in-progress
                UpdateComboDisplayText();

                // Finalize when the combo window expires
                if (comboTimer <= 0f)
                {
                    FinalizeCombo();
                    return;
                }

                // Also finalize if all combo keys have been released (user let go quickly)
                if (!AnyComboKeyStillHeld())
                {
                    FinalizeCombo();
                    return;
                }

                return;
            }

            // Not yet accumulating - detect the first input to start combo accumulation
            string firstInput = DetectNewlyPressedInput();
            if (firstInput != null)
            {
                // Also collect any OTHER inputs already held this frame (e.g., Shift was held before T was pressed)
                comboBuffer.Clear();

                // First add any modifier keys / buttons that are already held (but not the one just pressed)
                CollectHeldModifiers(firstInput);

                // Add the newly pressed input
                if (!comboBuffer.Contains(firstInput))
                    comboBuffer.Add(firstInput);

                // Start the combo accumulation window
                isAccumulatingCombo = true;
                comboTimer = COMBO_WINDOW;

                UpdateComboDisplayText();
            }
        }

        /// <summary>
        /// Detect a single newly-pressed input this frame (keyboard, gamepad, or mouse).
        /// Returns the binding string, or null if nothing was pressed.
        /// </summary>
        private string DetectNewlyPressedInput()
        {
            // Check gamepad inputs first
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                string gpInput = DetectNewlyPressedGamepadInput(gamepad);
                if (gpInput != null) return gpInput;
            }

            // Check mouse buttons
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) return "Mouse0";
                if (mouse.rightButton.wasPressedThisFrame) return "Mouse1";
                if (mouse.middleButton.wasPressedThisFrame) return "Mouse2";
                if (mouse.forwardButton.wasPressedThisFrame) return "Mouse3";
                if (mouse.backButton.wasPressedThisFrame) return "Mouse4";
            }

            // Check keyboard keys
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape)
                    {
                        return FaeMaze.Systems.InputBindingHelper.KeyToBindingString(key.keyCode);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Detect a newly pressed gamepad button/direction this frame.
        /// </summary>
        private string DetectNewlyPressedGamepadInput(Gamepad gamepad)
        {
            // Face buttons
            if (gamepad.buttonSouth.wasPressedThisFrame) return "GamepadButtonSouth";
            if (gamepad.buttonNorth.wasPressedThisFrame) return "GamepadButtonNorth";
            if (gamepad.buttonEast.wasPressedThisFrame) return "GamepadButtonEast";
            if (gamepad.buttonWest.wasPressedThisFrame) return "GamepadButtonWest";
            // Shoulder buttons
            if (gamepad.leftShoulder.wasPressedThisFrame) return "GamepadLeftShoulder";
            if (gamepad.rightShoulder.wasPressedThisFrame) return "GamepadRightShoulder";
            if (gamepad.leftTrigger.wasPressedThisFrame) return "GamepadLeftTrigger";
            if (gamepad.rightTrigger.wasPressedThisFrame) return "GamepadRightTrigger";
            // Stick buttons
            if (gamepad.leftStickButton.wasPressedThisFrame) return "GamepadLeftStickPress";
            if (gamepad.rightStickButton.wasPressedThisFrame) return "GamepadRightStickPress";
            // Menu buttons
            if (gamepad.startButton.wasPressedThisFrame) return "GamepadStart";
            if (gamepad.selectButton.wasPressedThisFrame) return "GamepadSelect";
            // D-pad
            if (gamepad.dpad.up.wasPressedThisFrame) return "GamepadDpadUp";
            if (gamepad.dpad.down.wasPressedThisFrame) return "GamepadDpadDown";
            if (gamepad.dpad.left.wasPressedThisFrame) return "GamepadDpadLeft";
            if (gamepad.dpad.right.wasPressedThisFrame) return "GamepadDpadRight";

            // Analog sticks (directional threshold)
            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            if (leftStick.y > STICK_THRESHOLD) return "GamepadLeftStickUp";
            if (leftStick.y < -STICK_THRESHOLD) return "GamepadLeftStickDown";
            if (leftStick.x < -STICK_THRESHOLD) return "GamepadLeftStickLeft";
            if (leftStick.x > STICK_THRESHOLD) return "GamepadLeftStickRight";
            if (rightStick.y > STICK_THRESHOLD) return "GamepadRightStickUp";
            if (rightStick.y < -STICK_THRESHOLD) return "GamepadRightStickDown";
            if (rightStick.x < -STICK_THRESHOLD) return "GamepadRightStickLeft";
            if (rightStick.x > STICK_THRESHOLD) return "GamepadRightStickRight";

            return null;
        }

        /// <summary>
        /// Collect any modifier keys or buttons that are currently held but not already in the combo buffer.
        /// Called when the first key is detected to pick up modifiers that were pressed before the main key.
        /// </summary>
        private void CollectHeldModifiers(string excludeBinding)
        {
            // Keyboard modifiers
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftShiftKey.isPressed) TryAddToCombo("LeftShift", excludeBinding);
                if (keyboard.rightShiftKey.isPressed) TryAddToCombo("RightShift", excludeBinding);
                if (keyboard.leftCtrlKey.isPressed) TryAddToCombo("LeftControl", excludeBinding);
                if (keyboard.rightCtrlKey.isPressed) TryAddToCombo("RightControl", excludeBinding);
                if (keyboard.leftAltKey.isPressed) TryAddToCombo("LeftAlt", excludeBinding);
                if (keyboard.rightAltKey.isPressed) TryAddToCombo("RightAlt", excludeBinding);
            }

            // Gamepad shoulder/trigger modifiers (common combo modifiers)
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.leftShoulder.isPressed) TryAddToCombo("GamepadLeftShoulder", excludeBinding);
                if (gamepad.rightShoulder.isPressed) TryAddToCombo("GamepadRightShoulder", excludeBinding);
                if (gamepad.leftTrigger.isPressed) TryAddToCombo("GamepadLeftTrigger", excludeBinding);
                if (gamepad.rightTrigger.isPressed) TryAddToCombo("GamepadRightTrigger", excludeBinding);
            }
        }

        /// <summary>
        /// During combo accumulation, collect any newly pressed inputs this frame and add to the buffer.
        /// </summary>
        private void CollectNewlyPressedInputs()
        {
            // Check gamepad
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                string gpInput = DetectNewlyPressedGamepadInput(gamepad);
                if (gpInput != null && !comboBuffer.Contains(gpInput))
                {
                    comboBuffer.Add(gpInput);
                    comboTimer = COMBO_WINDOW; // Reset window for each new input
                }
            }

            // Check mouse
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) TryAddToComboAndResetTimer("Mouse0");
                if (mouse.rightButton.wasPressedThisFrame) TryAddToComboAndResetTimer("Mouse1");
                if (mouse.middleButton.wasPressedThisFrame) TryAddToComboAndResetTimer("Mouse2");
                if (mouse.forwardButton.wasPressedThisFrame) TryAddToComboAndResetTimer("Mouse3");
                if (mouse.backButton.wasPressedThisFrame) TryAddToComboAndResetTimer("Mouse4");
            }

            // Check keyboard
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key != null && key.wasPressedThisFrame && key.keyCode != Key.Escape)
                    {
                        string binding = FaeMaze.Systems.InputBindingHelper.KeyToBindingString(key.keyCode);
                        TryAddToComboAndResetTimer(binding);
                    }
                }
            }
        }

        /// <summary>
        /// Add a binding to the combo buffer if not already present. Resets the combo timer on success.
        /// </summary>
        private void TryAddToComboAndResetTimer(string binding)
        {
            if (!comboBuffer.Contains(binding))
            {
                comboBuffer.Add(binding);
                comboTimer = COMBO_WINDOW;
            }
        }

        /// <summary>
        /// Add a binding to the combo buffer if it differs from the excluded binding.
        /// </summary>
        private void TryAddToCombo(string binding, string excludeBinding)
        {
            if (binding != excludeBinding && !comboBuffer.Contains(binding))
            {
                comboBuffer.Add(binding);
            }
        }

        /// <summary>
        /// Check if any key in the combo buffer is still physically held.
        /// </summary>
        private bool AnyComboKeyStillHeld()
        {
            foreach (string binding in comboBuffer)
            {
                if (FaeMaze.Systems.InputBindingHelper.IsBindingPressed(binding))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Sort combo buffer to put modifiers first, then action keys.
        /// This ensures consistent ordering (e.g., "LeftShift+W" not "W+LeftShift").
        /// </summary>
        private void SortComboBuffer()
        {
            comboBuffer.Sort((a, b) => GetBindingPriority(a).CompareTo(GetBindingPriority(b)));
        }

        /// <summary>
        /// Returns sort priority for a binding. Lower = earlier in combo string.
        /// Modifiers come first, then gamepad shoulders/triggers, then regular keys.
        /// </summary>
        private int GetBindingPriority(string binding)
        {
            // Keyboard modifiers first
            if (binding == "LeftControl" || binding == "RightControl") return 0;
            if (binding == "LeftAlt" || binding == "RightAlt") return 1;
            if (binding == "LeftShift" || binding == "RightShift") return 2;
            // Gamepad shoulders/triggers next
            if (binding == "GamepadLeftShoulder" || binding == "GamepadRightShoulder") return 3;
            if (binding == "GamepadLeftTrigger" || binding == "GamepadRightTrigger") return 4;
            // Everything else
            return 10;
        }

        /// <summary>
        /// Finalize the combo buffer into a binding string and complete the capture.
        /// </summary>
        private void FinalizeCombo()
        {
            if (comboBuffer.Count == 0)
            {
                ResetComboState();
                return;
            }

            // Sort modifiers first for consistent display
            SortComboBuffer();

            // Join with "+" for combo, or just the single key
            string finalBinding;
            if (comboBuffer.Count == 1)
            {
                finalBinding = comboBuffer[0];
            }
            else
            {
                finalBinding = string.Join("+", comboBuffer);
            }

            ResetComboState();
            CompleteCapture(finalBinding);
        }

        /// <summary>
        /// Reset combo accumulation state.
        /// </summary>
        private void ResetComboState()
        {
            isAccumulatingCombo = false;
            comboBuffer.Clear();
            comboTimer = 0f;
        }

        /// <summary>
        /// Update the display text to show the combo being built (e.g., "L Shift + ..." while waiting).
        /// </summary>
        private void UpdateComboDisplayText()
        {
            if (bindingText == null || comboBuffer.Count == 0)
                return;

            // Sort for display
            var displayBuffer = new List<string>(comboBuffer);
            displayBuffer.Sort((a, b) => GetBindingPriority(a).CompareTo(GetBindingPriority(b)));

            // Build display string showing accumulated keys
            var parts = new string[displayBuffer.Count];
            for (int i = 0; i < displayBuffer.Count; i++)
            {
                parts[i] = FaeMaze.Systems.InputBindingHelper.GetDisplayName(displayBuffer[i]);
            }
            string comboDisplay = string.Join(" + ", parts) + " + ...";
            bindingText.text = comboDisplay;
            bindingText.color = captureTextColor;
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

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

        [Header("Display")]
        [SerializeField]
        [Tooltip("Text to display while waiting for input")]
        private string capturePrompt = "Press any key...";

        [Header("Styling")]
        [SerializeField]
        private Color normalTextColor = Color.white;

        [SerializeField]
        private Color captureTextColor = new Color(1f, 0.8f, 0.3f, 1f); // Yellow-orange for capture mode

        private string currentBinding;
        private bool isCapturing = false;

        /// <summary>
        /// Event fired when a new binding is captured.
        /// Parameter is the binding string (e.g., "W", "Mouse1", "F12").
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
        }

        private void OnDestroy()
        {
            if (captureToggle != null)
            {
                captureToggle.onValueChanged.RemoveListener(OnToggleChanged);
            }
        }

        /// <summary>
        /// Initialize the control with a binding value.
        /// </summary>
        public void SetBinding(string binding)
        {
            currentBinding = binding;
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

            // Check mouse buttons first (more specific)
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
            else
            {
                bindingText.text = FaeMaze.Systems.InputBindingHelper.GetDisplayName(currentBinding);
                bindingText.color = normalTextColor;
            }
        }
    }
}

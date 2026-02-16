using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Static utility class for handling input bindings.
    /// Supports both keyboard keys and mouse buttons via string-based bindings.
    /// </summary>
    public static class InputBindingHelper
    {
        // Threshold for gamepad stick input detection
        private const float STICK_THRESHOLD = 0.5f;


        /// <summary>
        /// Check if ANY of the provided bindings is currently pressed.
        /// Use for checking primary + alt + tertiary bindings together.
        /// </summary>
        public static bool IsAnyBindingPressed(string binding1, string binding2, string binding3)
        {
            return IsBindingPressed(binding1) || IsBindingPressed(binding2) || IsBindingPressed(binding3);
        }

        /// <summary>
        /// Check if ANY of the provided bindings was just pressed this frame.
        /// Use for checking primary + alt + tertiary bindings together.
        /// </summary>
        public static bool WasAnyBindingPressedThisFrame(string binding1, string binding2, string binding3)
        {
            return WasBindingPressedThisFrame(binding1) || WasBindingPressedThisFrame(binding2) || WasBindingPressedThisFrame(binding3);
        }

        /// <summary>
        /// Check if ANY of the provided bindings was just released this frame.
        /// Use for checking primary + alt + tertiary bindings together.
        /// </summary>
        public static bool WasAnyBindingReleasedThisFrame(string binding1, string binding2, string binding3)
        {
            return WasBindingReleasedThisFrame(binding1) || WasBindingReleasedThisFrame(binding2) || WasBindingReleasedThisFrame(binding3);
        }

        /// <summary>
        /// Check if a binding string represents a combo (e.g., "LeftShift+W" or "GamepadLeftShoulder+GamepadButtonWest").
        /// </summary>
        public static bool IsComboBinding(string binding)
        {
            return !string.IsNullOrEmpty(binding) && binding.Contains("+");
        }

        /// <summary>
        /// Check if binding is currently pressed (for hold actions like camera movement).
        /// Supports combo bindings with "+" separator (all parts must be pressed).
        /// </summary>
        public static bool IsBindingPressed(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

            // Handle combo bindings (e.g., "LeftShift+W", "GamepadLeftShoulder+GamepadButtonWest")
            if (IsComboBinding(binding))
            {
                string[] parts = binding.Split('+');
                foreach (string part in parts)
                {
                    if (!IsBindingPressed(part.Trim()))
                        return false;
                }
                return true;
            }

            // Check mouse buttons
            if (IsMouseButton(binding))
            {
                int mouseIndex = GetMouseButtonIndex(binding);
                Mouse mouse = Mouse.current;
                if (mouse == null)
                    return false;

                return mouseIndex switch
                {
                    0 => mouse.leftButton.isPressed,
                    1 => mouse.rightButton.isPressed,
                    2 => mouse.middleButton.isPressed,
                    3 => mouse.forwardButton.isPressed,
                    4 => mouse.backButton.isPressed,
                    _ => false
                };
            }

            // Check gamepad bindings
            if (IsGamepadBinding(binding))
            {
                return IsGamepadBindingPressed(binding);
            }

            // Check keyboard keys
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key? key = ParseKey(binding);
            if (key.HasValue)
            {
                KeyControl keyControl = keyboard[key.Value];
                return keyControl != null && keyControl.isPressed;
            }

            return false;
        }

        /// <summary>
        /// Check if binding was just pressed this frame (for trigger actions like powers).
        /// Supports combo bindings: all parts must be held, and at least one must have been pressed this frame.
        /// </summary>
        public static bool WasBindingPressedThisFrame(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

            // Handle combo bindings: all parts held, at least one newly pressed this frame
            if (IsComboBinding(binding))
            {
                string[] parts = binding.Split('+');
                bool allHeld = true;
                bool anyNewlyPressed = false;
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (!IsBindingPressed(trimmed))
                    {
                        allHeld = false;
                        break;
                    }
                    if (WasBindingPressedThisFrame(trimmed))
                        anyNewlyPressed = true;
                }
                return allHeld && anyNewlyPressed;
            }

            // Check mouse buttons
            if (IsMouseButton(binding))
            {
                int mouseIndex = GetMouseButtonIndex(binding);
                Mouse mouse = Mouse.current;
                if (mouse == null)
                    return false;

                bool result = mouseIndex switch
                {
                    0 => mouse.leftButton.wasPressedThisFrame,
                    1 => mouse.rightButton.wasPressedThisFrame,
                    2 => mouse.middleButton.wasPressedThisFrame,
                    3 => mouse.forwardButton.wasPressedThisFrame,
                    4 => mouse.backButton.wasPressedThisFrame,
                    _ => false
                };

                return result;
            }

            // Check gamepad bindings
            if (IsGamepadBinding(binding))
            {
                return WasGamepadBindingPressedThisFrame(binding);
            }

            // Check keyboard keys
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key? key = ParseKey(binding);
            if (key.HasValue)
            {
                KeyControl keyControl = keyboard[key.Value];
                return keyControl != null && keyControl.wasPressedThisFrame;
            }

            return false;
        }

        /// <summary>
        /// Check if binding was just released this frame (for drag end detection).
        /// Supports combo bindings: fires when any part is released while others were held.
        /// </summary>
        public static bool WasBindingReleasedThisFrame(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

            // Handle combo bindings: at least one part released, rest were held
            if (IsComboBinding(binding))
            {
                string[] parts = binding.Split('+');
                bool anyReleased = false;
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (WasBindingReleasedThisFrame(trimmed))
                        anyReleased = true;
                }
                return anyReleased;
            }

            // Check mouse buttons
            if (IsMouseButton(binding))
            {
                int mouseIndex = GetMouseButtonIndex(binding);
                Mouse mouse = Mouse.current;
                if (mouse == null)
                    return false;

                return mouseIndex switch
                {
                    0 => mouse.leftButton.wasReleasedThisFrame,
                    1 => mouse.rightButton.wasReleasedThisFrame,
                    2 => mouse.middleButton.wasReleasedThisFrame,
                    3 => mouse.forwardButton.wasReleasedThisFrame,
                    4 => mouse.backButton.wasReleasedThisFrame,
                    _ => false
                };
            }

            // Check gamepad bindings
            if (IsGamepadBinding(binding))
            {
                return WasGamepadBindingReleasedThisFrame(binding);
            }

            // Check keyboard keys
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            Key? key = ParseKey(binding);
            if (key.HasValue)
            {
                KeyControl keyControl = keyboard[key.Value];
                return keyControl != null && keyControl.wasReleasedThisFrame;
            }

            return false;
        }

        /// <summary>
        /// Get human-readable display name for a binding.
        /// </summary>
        public static string GetDisplayName(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return "None";

            // Handle combo display names (e.g., "LeftShift+W" -> "L Shift + W")
            if (IsComboBinding(binding))
            {
                string[] parts = binding.Split('+');
                string[] displayParts = new string[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    displayParts[i] = GetDisplayName(parts[i].Trim());
                }
                return string.Join(" + ", displayParts);
            }

            // Mouse buttons
            if (IsMouseButton(binding))
            {
                int index = GetMouseButtonIndex(binding);
                return index switch
                {
                    0 => "Left Click",
                    1 => "Right Click",
                    2 => "Middle Click",
                    3 => "Mouse 4",
                    4 => "Mouse 5",
                    _ => binding
                };
            }

            // Handle gamepad bindings
            if (IsGamepadBinding(binding))
            {
                return binding switch
                {
                    // Face buttons (using generic names that work for Xbox/PlayStation)
                    "GamepadButtonSouth" => "A / Cross",
                    "GamepadButtonNorth" => "Y / Triangle",
                    "GamepadButtonEast" => "B / Circle",
                    "GamepadButtonWest" => "X / Square",
                    // Shoulder buttons
                    "GamepadLeftShoulder" => "LB / L1",
                    "GamepadRightShoulder" => "RB / R1",
                    "GamepadLeftTrigger" => "LT / L2",
                    "GamepadRightTrigger" => "RT / R2",
                    // Stick buttons
                    "GamepadLeftStickPress" => "L Stick Press",
                    "GamepadRightStickPress" => "R Stick Press",
                    // Menu buttons
                    "GamepadStart" => "Start / Options",
                    "GamepadSelect" => "Select / Share",
                    // D-pad
                    "GamepadDpadUp" => "D-pad Up",
                    "GamepadDpadDown" => "D-pad Down",
                    "GamepadDpadLeft" => "D-pad Left",
                    "GamepadDpadRight" => "D-pad Right",
                    // Left stick directions
                    "GamepadLeftStickUp" => "L Stick Up",
                    "GamepadLeftStickDown" => "L Stick Down",
                    "GamepadLeftStickLeft" => "L Stick Left",
                    "GamepadLeftStickRight" => "L Stick Right",
                    // Right stick directions
                    "GamepadRightStickUp" => "R Stick Up",
                    "GamepadRightStickDown" => "R Stick Down",
                    "GamepadRightStickLeft" => "R Stick Left",
                    "GamepadRightStickRight" => "R Stick Right",
                    _ => binding
                };
            }

            // Handle common KeyCode/Key names
            return binding switch
            {
                "Alpha0" => "0",
                "Alpha1" => "1",
                "Alpha2" => "2",
                "Alpha3" => "3",
                "Alpha4" => "4",
                "Alpha5" => "5",
                "Alpha6" => "6",
                "Alpha7" => "7",
                "Alpha8" => "8",
                "Alpha9" => "9",
                "Digit0" => "0",
                "Digit1" => "1",
                "Digit2" => "2",
                "Digit3" => "3",
                "Digit4" => "4",
                "Digit5" => "5",
                "Digit6" => "6",
                "Digit7" => "7",
                "Digit8" => "8",
                "Digit9" => "9",
                "LeftShift" => "L Shift",
                "RightShift" => "R Shift",
                "LeftControl" => "L Ctrl",
                "RightControl" => "R Ctrl",
                "LeftAlt" => "L Alt",
                "RightAlt" => "R Alt",
                "UpArrow" => "Up",
                "DownArrow" => "Down",
                "LeftArrow" => "Left",
                "RightArrow" => "Right",
                "Return" => "Enter",
                "Escape" => "Esc",
                "Backspace" => "Backspace",
                "Delete" => "Del",
                "Space" => "Space",
                "Tab" => "Tab",
                "PageUp" => "Page Up",
                "PageDown" => "Page Down",
                _ => binding
            };
        }

        /// <summary>
        /// Parse KeyCode from a binding string.
        /// Returns KeyCode.None if it's a mouse button or unrecognized.
        /// </summary>
        public static KeyCode ParseKeyCode(string binding)
        {
            if (string.IsNullOrEmpty(binding) || IsMouseButton(binding))
                return KeyCode.None;

            // Try to parse as KeyCode enum
            if (System.Enum.TryParse<KeyCode>(binding, true, out KeyCode result))
                return result;

            // Handle new Input System key names that differ from KeyCode
            return binding switch
            {
                "Digit0" => KeyCode.Alpha0,
                "Digit1" => KeyCode.Alpha1,
                "Digit2" => KeyCode.Alpha2,
                "Digit3" => KeyCode.Alpha3,
                "Digit4" => KeyCode.Alpha4,
                "Digit5" => KeyCode.Alpha5,
                "Digit6" => KeyCode.Alpha6,
                "Digit7" => KeyCode.Alpha7,
                "Digit8" => KeyCode.Alpha8,
                "Digit9" => KeyCode.Alpha9,
                _ => KeyCode.None
            };
        }

        /// <summary>
        /// Parse a Key enum from binding string (new Input System).
        /// </summary>
        public static Key? ParseKey(string binding)
        {
            if (string.IsNullOrEmpty(binding) || IsMouseButton(binding))
                return null;

            // Handle common mappings from KeyCode names to Key enum
            Key? mappedKey = binding switch
            {
                // Alpha keys (KeyCode names) to Digit keys
                "Alpha0" => Key.Digit0,
                "Alpha1" => Key.Digit1,
                "Alpha2" => Key.Digit2,
                "Alpha3" => Key.Digit3,
                "Alpha4" => Key.Digit4,
                "Alpha5" => Key.Digit5,
                "Alpha6" => Key.Digit6,
                "Alpha7" => Key.Digit7,
                "Alpha8" => Key.Digit8,
                "Alpha9" => Key.Digit9,
                // Arrow keys
                "UpArrow" => Key.UpArrow,
                "DownArrow" => Key.DownArrow,
                "LeftArrow" => Key.LeftArrow,
                "RightArrow" => Key.RightArrow,
                // Modifiers
                "LeftShift" => Key.LeftShift,
                "RightShift" => Key.RightShift,
                "LeftControl" => Key.LeftCtrl,
                "RightControl" => Key.RightCtrl,
                "LeftAlt" => Key.LeftAlt,
                "RightAlt" => Key.RightAlt,
                // Common keys
                "Return" => Key.Enter,
                "Escape" => Key.Escape,
                "Backspace" => Key.Backspace,
                "Delete" => Key.Delete,
                "Space" => Key.Space,
                "Tab" => Key.Tab,
                _ => null
            };

            if (mappedKey.HasValue)
                return mappedKey;

            // Try direct enum parse
            if (System.Enum.TryParse<Key>(binding, true, out Key result))
                return result;

            return null;
        }

        /// <summary>
        /// Check if binding represents a mouse button.
        /// </summary>
        public static bool IsMouseButton(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

            return binding.StartsWith("Mouse", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get mouse button index (0-4) from binding string.
        /// Returns -1 if not a valid mouse button binding.
        /// </summary>
        public static int GetMouseButtonIndex(string binding)
        {
            if (!IsMouseButton(binding))
                return -1;

            string indexPart = binding.Substring(5);
            if (int.TryParse(indexPart, out int index) && index >= 0 && index <= 4)
                return index;

            return -1;
        }

        /// <summary>
        /// Convert a Key enum to a binding string.
        /// </summary>
        public static string KeyToBindingString(Key key)
        {
            // Map new Input System Key enum to our binding format
            // We use KeyCode names for consistency with existing settings
            return key switch
            {
                Key.Digit0 => "Alpha0",
                Key.Digit1 => "Alpha1",
                Key.Digit2 => "Alpha2",
                Key.Digit3 => "Alpha3",
                Key.Digit4 => "Alpha4",
                Key.Digit5 => "Alpha5",
                Key.Digit6 => "Alpha6",
                Key.Digit7 => "Alpha7",
                Key.Digit8 => "Alpha8",
                Key.Digit9 => "Alpha9",
                Key.UpArrow => "UpArrow",
                Key.DownArrow => "DownArrow",
                Key.LeftArrow => "LeftArrow",
                Key.RightArrow => "RightArrow",
                Key.LeftShift => "LeftShift",
                Key.RightShift => "RightShift",
                Key.LeftCtrl => "LeftControl",
                Key.RightCtrl => "RightControl",
                Key.LeftAlt => "LeftAlt",
                Key.RightAlt => "RightAlt",
                Key.Enter => "Return",
                Key.Escape => "Escape",
                Key.Backspace => "Backspace",
                Key.Delete => "Delete",
                Key.Space => "Space",
                Key.Tab => "Tab",
                _ => key.ToString()
            };
        }

        /// <summary>
        /// Convert a mouse button index (0-4) to a binding string.
        /// </summary>
        public static string MouseButtonToBindingString(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex > 4)
                return null;
            return $"Mouse{buttonIndex}";
        }

        /// <summary>
        /// Convert legacy KeyCode to binding string.
        /// </summary>
        public static string KeyCodeToBindingString(KeyCode keyCode)
        {
            return keyCode.ToString();
        }

        /// <summary>
        /// Check if binding represents a gamepad input.
        /// </summary>
        public static bool IsGamepadBinding(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

            return binding.StartsWith("Gamepad", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a gamepad binding is currently pressed.
        /// </summary>
        private static bool IsGamepadBindingPressed(string binding)
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return false;

            bool result = binding switch
            {
                // Face buttons
                "GamepadButtonSouth" => gamepad.buttonSouth.isPressed,
                "GamepadButtonNorth" => gamepad.buttonNorth.isPressed,
                "GamepadButtonEast" => gamepad.buttonEast.isPressed,
                "GamepadButtonWest" => gamepad.buttonWest.isPressed,
                // Shoulder buttons
                "GamepadLeftShoulder" => gamepad.leftShoulder.isPressed,
                "GamepadRightShoulder" => gamepad.rightShoulder.isPressed,
                "GamepadLeftTrigger" => gamepad.leftTrigger.isPressed,
                "GamepadRightTrigger" => gamepad.rightTrigger.isPressed,
                // Stick buttons
                "GamepadLeftStickPress" => gamepad.leftStickButton.isPressed,
                "GamepadRightStickPress" => gamepad.rightStickButton.isPressed,
                // Menu buttons
                "GamepadStart" => gamepad.startButton.isPressed,
                "GamepadSelect" => gamepad.selectButton.isPressed,
                // D-pad
                "GamepadDpadUp" => gamepad.dpad.up.isPressed,
                "GamepadDpadDown" => gamepad.dpad.down.isPressed,
                "GamepadDpadLeft" => gamepad.dpad.left.isPressed,
                "GamepadDpadRight" => gamepad.dpad.right.isPressed,
                // Left stick directions (analog threshold)
                "GamepadLeftStickUp" => gamepad.leftStick.ReadValue().y > STICK_THRESHOLD,
                "GamepadLeftStickDown" => gamepad.leftStick.ReadValue().y < -STICK_THRESHOLD,
                "GamepadLeftStickLeft" => gamepad.leftStick.ReadValue().x < -STICK_THRESHOLD,
                "GamepadLeftStickRight" => gamepad.leftStick.ReadValue().x > STICK_THRESHOLD,
                // Right stick directions (analog threshold)
                "GamepadRightStickUp" => gamepad.rightStick.ReadValue().y > STICK_THRESHOLD,
                "GamepadRightStickDown" => gamepad.rightStick.ReadValue().y < -STICK_THRESHOLD,
                "GamepadRightStickLeft" => gamepad.rightStick.ReadValue().x < -STICK_THRESHOLD,
                "GamepadRightStickRight" => gamepad.rightStick.ReadValue().x > STICK_THRESHOLD,
                _ => false
            };

            return result;
        }

        /// <summary>
        /// Check if a gamepad binding was just pressed this frame.
        /// For analog stick directions, this checks if the stick just crossed the threshold.
        /// </summary>
        private static bool WasGamepadBindingPressedThisFrame(string binding)
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return false;

            bool result = binding switch
            {
                // Face buttons
                "GamepadButtonSouth" => gamepad.buttonSouth.wasPressedThisFrame,
                "GamepadButtonNorth" => gamepad.buttonNorth.wasPressedThisFrame,
                "GamepadButtonEast" => gamepad.buttonEast.wasPressedThisFrame,
                "GamepadButtonWest" => gamepad.buttonWest.wasPressedThisFrame,
                // Shoulder buttons
                "GamepadLeftShoulder" => gamepad.leftShoulder.wasPressedThisFrame,
                "GamepadRightShoulder" => gamepad.rightShoulder.wasPressedThisFrame,
                "GamepadLeftTrigger" => gamepad.leftTrigger.wasPressedThisFrame,
                "GamepadRightTrigger" => gamepad.rightTrigger.wasPressedThisFrame,
                // Stick buttons
                "GamepadLeftStickPress" => gamepad.leftStickButton.wasPressedThisFrame,
                "GamepadRightStickPress" => gamepad.rightStickButton.wasPressedThisFrame,
                // Menu buttons
                "GamepadStart" => gamepad.startButton.wasPressedThisFrame,
                "GamepadSelect" => gamepad.selectButton.wasPressedThisFrame,
                // D-pad
                "GamepadDpadUp" => gamepad.dpad.up.wasPressedThisFrame,
                "GamepadDpadDown" => gamepad.dpad.down.wasPressedThisFrame,
                "GamepadDpadLeft" => gamepad.dpad.left.wasPressedThisFrame,
                "GamepadDpadRight" => gamepad.dpad.right.wasPressedThisFrame,
                // For analog sticks, we use the current pressed state since there's no "wasPressedThisFrame" for analog
                // This means holding the stick will register as "pressed" - may want to add proper edge detection later
                "GamepadLeftStickUp" => gamepad.leftStick.ReadValue().y > STICK_THRESHOLD,
                "GamepadLeftStickDown" => gamepad.leftStick.ReadValue().y < -STICK_THRESHOLD,
                "GamepadLeftStickLeft" => gamepad.leftStick.ReadValue().x < -STICK_THRESHOLD,
                "GamepadLeftStickRight" => gamepad.leftStick.ReadValue().x > STICK_THRESHOLD,
                "GamepadRightStickUp" => gamepad.rightStick.ReadValue().y > STICK_THRESHOLD,
                "GamepadRightStickDown" => gamepad.rightStick.ReadValue().y < -STICK_THRESHOLD,
                "GamepadRightStickLeft" => gamepad.rightStick.ReadValue().x < -STICK_THRESHOLD,
                "GamepadRightStickRight" => gamepad.rightStick.ReadValue().x > STICK_THRESHOLD,
                _ => false
            };

            return result;
        }

        /// <summary>
        /// Check if a gamepad binding was just released this frame.
        /// </summary>
        private static bool WasGamepadBindingReleasedThisFrame(string binding)
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
                return false;

            bool result = binding switch
            {
                // Face buttons
                "GamepadButtonSouth" => gamepad.buttonSouth.wasReleasedThisFrame,
                "GamepadButtonNorth" => gamepad.buttonNorth.wasReleasedThisFrame,
                "GamepadButtonEast" => gamepad.buttonEast.wasReleasedThisFrame,
                "GamepadButtonWest" => gamepad.buttonWest.wasReleasedThisFrame,
                // Shoulder buttons
                "GamepadLeftShoulder" => gamepad.leftShoulder.wasReleasedThisFrame,
                "GamepadRightShoulder" => gamepad.rightShoulder.wasReleasedThisFrame,
                "GamepadLeftTrigger" => gamepad.leftTrigger.wasReleasedThisFrame,
                "GamepadRightTrigger" => gamepad.rightTrigger.wasReleasedThisFrame,
                // Stick buttons
                "GamepadLeftStickPress" => gamepad.leftStickButton.wasReleasedThisFrame,
                "GamepadRightStickPress" => gamepad.rightStickButton.wasReleasedThisFrame,
                // Menu buttons
                "GamepadStart" => gamepad.startButton.wasReleasedThisFrame,
                "GamepadSelect" => gamepad.selectButton.wasReleasedThisFrame,
                // D-pad
                "GamepadDpadUp" => gamepad.dpad.up.wasReleasedThisFrame,
                "GamepadDpadDown" => gamepad.dpad.down.wasReleasedThisFrame,
                "GamepadDpadLeft" => gamepad.dpad.left.wasReleasedThisFrame,
                "GamepadDpadRight" => gamepad.dpad.right.wasReleasedThisFrame,
                // Analog sticks - release = dropping below threshold
                "GamepadLeftStickUp" => gamepad.leftStick.ReadValue().y <= STICK_THRESHOLD,
                "GamepadLeftStickDown" => gamepad.leftStick.ReadValue().y >= -STICK_THRESHOLD,
                "GamepadLeftStickLeft" => gamepad.leftStick.ReadValue().x >= -STICK_THRESHOLD,
                "GamepadLeftStickRight" => gamepad.leftStick.ReadValue().x <= STICK_THRESHOLD,
                "GamepadRightStickUp" => gamepad.rightStick.ReadValue().y <= STICK_THRESHOLD,
                "GamepadRightStickDown" => gamepad.rightStick.ReadValue().y >= -STICK_THRESHOLD,
                "GamepadRightStickLeft" => gamepad.rightStick.ReadValue().x >= -STICK_THRESHOLD,
                "GamepadRightStickRight" => gamepad.rightStick.ReadValue().x <= STICK_THRESHOLD,
                _ => false
            };

            return result;
        }

    }
}

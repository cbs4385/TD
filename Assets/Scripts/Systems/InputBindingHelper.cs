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
        /// <summary>
        /// Check if binding is currently pressed (for hold actions like camera movement).
        /// </summary>
        public static bool IsBindingPressed(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

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

            // Fallback to legacy Input system for KeyCode
            KeyCode keyCode = ParseKeyCode(binding);
            if (keyCode != KeyCode.None)
            {
                return Input.GetKey(keyCode);
            }

            return false;
        }

        /// <summary>
        /// Check if binding was just pressed this frame (for trigger actions like powers).
        /// </summary>
        public static bool WasBindingPressedThisFrame(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

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

            // Fallback to legacy Input system for KeyCode
            KeyCode keyCode = ParseKeyCode(binding);
            if (keyCode != KeyCode.None)
            {
                return Input.GetKeyDown(keyCode);
            }

            return false;
        }

        /// <summary>
        /// Check if binding was just released this frame (for drag end detection).
        /// </summary>
        public static bool WasBindingReleasedThisFrame(string binding)
        {
            if (string.IsNullOrEmpty(binding))
                return false;

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

            // Fallback to legacy Input system for KeyCode
            KeyCode keyCode = ParseKeyCode(binding);
            if (keyCode != KeyCode.None)
            {
                return Input.GetKeyUp(keyCode);
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
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Attach to any GameObject in the game scene to continuously monitor gamepad input.
    /// Logs raw gamepad state whenever any button/stick is active.
    /// Also logs gamepad connect/disconnect events.
    ///
    /// DELETE THIS COMPONENT when gamepad debugging is complete.
    /// </summary>
    public class GamepadDebugLogger : MonoBehaviour
    {
        private float lastRawLog = 0f;
        private const float RAW_LOG_INTERVAL = 0.5f; // Log raw state every 0.5s when active

        private bool wasGamepadConnected = false;
        private string lastGamepadName = "";

        private void OnEnable()
        {
            // Subscribe to device change events
            InputSystem.onDeviceChange += OnDeviceChange;

            // Log initial state
            LogGamepadConnectionState("OnEnable");
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad gamepad)
            {
                Debug.Log($"[GamepadDebug] DEVICE EVENT: {change} - name='{gamepad.name}', " +
                          $"displayName='{gamepad.displayName}', " +
                          $"deviceId={gamepad.deviceId}, " +
                          $"description='{gamepad.description}'");

                LogGamepadConnectionState($"After {change}");
            }
        }

        private void LogGamepadConnectionState(string context)
        {
            Gamepad gamepad = Gamepad.current;
            bool isConnected = gamepad != null;

            Debug.Log($"[GamepadDebug] Connection state ({context}): " +
                      $"Gamepad.current={(isConnected ? $"'{gamepad.name}'" : "NULL")}, " +
                      $"Gamepad.all.Count={Gamepad.all.Count}");

            if (isConnected)
            {
                Debug.Log($"[GamepadDebug] Current gamepad details: " +
                          $"name='{gamepad.name}', " +
                          $"displayName='{gamepad.displayName}', " +
                          $"deviceId={gamepad.deviceId}, " +
                          $"enabled={gamepad.enabled}, " +
                          $"added={gamepad.added}");
            }

            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                var gp = Gamepad.all[i];
                Debug.Log($"[GamepadDebug]   Gamepad[{i}]: name='{gp.name}', " +
                          $"displayName='{gp.displayName}', " +
                          $"deviceId={gp.deviceId}, " +
                          $"enabled={gp.enabled}");
            }
        }

        private void Update()
        {
            // Detect connection changes
            Gamepad gamepad = Gamepad.current;
            bool isConnected = gamepad != null;
            string currentName = isConnected ? gamepad.name : "";

            if (isConnected != wasGamepadConnected || currentName != lastGamepadName)
            {
                Debug.Log($"[GamepadDebug] CONNECTION CHANGE: was={wasGamepadConnected}('{lastGamepadName}'), " +
                          $"now={isConnected}('{currentName}')");
                wasGamepadConnected = isConnected;
                lastGamepadName = currentName;
                LogGamepadConnectionState("Update connection change");
            }

            if (gamepad == null)
                return;

            // Check if ANY button or stick is active
            bool anyActive = false;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // Buttons - log wasPressedThisFrame for one-shot events
            if (gamepad.buttonSouth.wasPressedThisFrame) { sb.Append(" [A/Cross PRESSED]"); anyActive = true; }
            if (gamepad.buttonNorth.wasPressedThisFrame) { sb.Append(" [Y/Triangle PRESSED]"); anyActive = true; }
            if (gamepad.buttonEast.wasPressedThisFrame) { sb.Append(" [B/Circle PRESSED]"); anyActive = true; }
            if (gamepad.buttonWest.wasPressedThisFrame) { sb.Append(" [X/Square PRESSED]"); anyActive = true; }
            if (gamepad.leftShoulder.wasPressedThisFrame) { sb.Append(" [LB/L1 PRESSED]"); anyActive = true; }
            if (gamepad.rightShoulder.wasPressedThisFrame) { sb.Append(" [RB/R1 PRESSED]"); anyActive = true; }
            if (gamepad.leftTrigger.wasPressedThisFrame) { sb.Append($" [LT/L2 PRESSED val={gamepad.leftTrigger.ReadValue():F2}]"); anyActive = true; }
            if (gamepad.rightTrigger.wasPressedThisFrame) { sb.Append($" [RT/R2 PRESSED val={gamepad.rightTrigger.ReadValue():F2}]"); anyActive = true; }
            if (gamepad.leftStickButton.wasPressedThisFrame) { sb.Append(" [LStick PRESSED]"); anyActive = true; }
            if (gamepad.rightStickButton.wasPressedThisFrame) { sb.Append(" [RStick PRESSED]"); anyActive = true; }
            if (gamepad.startButton.wasPressedThisFrame) { sb.Append(" [Start PRESSED]"); anyActive = true; }
            if (gamepad.selectButton.wasPressedThisFrame) { sb.Append(" [Select PRESSED]"); anyActive = true; }
            if (gamepad.dpad.up.wasPressedThisFrame) { sb.Append(" [DpadUp PRESSED]"); anyActive = true; }
            if (gamepad.dpad.down.wasPressedThisFrame) { sb.Append(" [DpadDown PRESSED]"); anyActive = true; }
            if (gamepad.dpad.left.wasPressedThisFrame) { sb.Append(" [DpadLeft PRESSED]"); anyActive = true; }
            if (gamepad.dpad.right.wasPressedThisFrame) { sb.Append(" [DpadRight PRESSED]"); anyActive = true; }

            // Log button presses immediately (every frame they're pressed)
            if (anyActive)
            {
                Debug.Log($"[GamepadDebug] BUTTON THIS FRAME:{sb} (gamepad='{gamepad.name}', frame={Time.frameCount})");
            }

            // Log held state and sticks less frequently
            if (Time.time - lastRawLog < RAW_LOG_INTERVAL)
                return;

            bool anyHeld = false;
            System.Text.StringBuilder heldSb = new System.Text.StringBuilder();

            if (gamepad.buttonSouth.isPressed) { heldSb.Append(" A/Cross"); anyHeld = true; }
            if (gamepad.buttonNorth.isPressed) { heldSb.Append(" Y/Triangle"); anyHeld = true; }
            if (gamepad.buttonEast.isPressed) { heldSb.Append(" B/Circle"); anyHeld = true; }
            if (gamepad.buttonWest.isPressed) { heldSb.Append(" X/Square"); anyHeld = true; }
            if (gamepad.leftShoulder.isPressed) { heldSb.Append(" LB"); anyHeld = true; }
            if (gamepad.rightShoulder.isPressed) { heldSb.Append(" RB"); anyHeld = true; }
            if (gamepad.leftTrigger.isPressed) { heldSb.Append($" LT({gamepad.leftTrigger.ReadValue():F2})"); anyHeld = true; }
            if (gamepad.rightTrigger.isPressed) { heldSb.Append($" RT({gamepad.rightTrigger.ReadValue():F2})"); anyHeld = true; }
            if (gamepad.dpad.up.isPressed) { heldSb.Append(" DUp"); anyHeld = true; }
            if (gamepad.dpad.down.isPressed) { heldSb.Append(" DDown"); anyHeld = true; }
            if (gamepad.dpad.left.isPressed) { heldSb.Append(" DLeft"); anyHeld = true; }
            if (gamepad.dpad.right.isPressed) { heldSb.Append(" DRight"); anyHeld = true; }

            Vector2 leftStick = gamepad.leftStick.ReadValue();
            Vector2 rightStick = gamepad.rightStick.ReadValue();
            if (leftStick.magnitude > 0.15f)
            {
                heldSb.Append($" LStick({leftStick.x:F2},{leftStick.y:F2})");
                anyHeld = true;
            }
            if (rightStick.magnitude > 0.15f)
            {
                heldSb.Append($" RStick({rightStick.x:F2},{rightStick.y:F2})");
                anyHeld = true;
            }

            if (anyHeld)
            {
                lastRawLog = Time.time;
                Debug.Log($"[GamepadDebug] HELD:{heldSb} (gamepad='{gamepad.name}')");
            }
        }
    }
}

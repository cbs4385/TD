using UnityEngine;
using UnityEngine.InputSystem;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Simple orthographic camera controller with arrow key movement, scroll wheel zoom,
    /// and pitch/yaw rotation controls.
    /// Uses the project's -Z up coordinate system (XY plane is the playing surface).
    /// </summary>
    public class SimpleCameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Camera pan speed in units per second")]
        private float panSpeed = 10f;

        [Header("Rotation Settings")]
        [SerializeField]
        [Tooltip("Rotation speed in degrees per second")]
        private float rotationSpeed = 90f;

        [SerializeField]
        [Tooltip("Minimum pitch angle (looking down toward +Z)")]
        private float minPitch = -89f;

        [SerializeField]
        [Tooltip("Maximum pitch angle (looking up toward -Z)")]
        private float maxPitch = 89f;

        [Header("Zoom Settings")]
        [SerializeField]
        [Tooltip("Minimum orthographic size (zoomed in)")]
        private float minZoom = 1f;

        [SerializeField]
        [Tooltip("Maximum orthographic size (zoomed out)")]
        private float maxZoom = 20f;

        [SerializeField]
        [Tooltip("Zoom speed multiplier")]
        private float zoomSpeed = 2f;

        private Camera cam;
        private float currentYaw;
        private float currentPitch;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            // Initialize from current rotation
            Vector3 euler = transform.eulerAngles;
            currentYaw = euler.y;
            currentPitch = euler.x;

            // Normalize pitch to -180 to 180 range
            if (currentPitch > 180f)
            {
                currentPitch -= 360f;
            }
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            HandleMovement();
            HandleRotation();
            HandleZoom();
        }

        private void HandleMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector3 movement = Vector3.zero;

            // Arrow keys and WASD for movement in XY plane
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
            {
                movement.y += 1f;
            }
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
            {
                movement.y -= 1f;
            }
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            {
                movement.x += 1f;
            }
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
            {
                movement.x -= 1f;
            }

            if (movement.sqrMagnitude > 0.001f)
            {
                movement.Normalize();
                transform.position += movement * panSpeed * Time.deltaTime;
            }
        }

        private void HandleRotation()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float yawInput = 0f;
            float pitchInput = 0f;

            // Q/E for yaw (rotation around Y axis - left/right look)
            if (keyboard.qKey.isPressed)
            {
                yawInput -= 1f;
            }
            if (keyboard.eKey.isPressed)
            {
                yawInput += 1f;
            }

            // R/F for pitch (rotation around X axis - up/down look)
            if (keyboard.rKey.isPressed)
            {
                pitchInput -= 1f;
            }
            if (keyboard.fKey.isPressed)
            {
                pitchInput += 1f;
            }

            if (Mathf.Abs(yawInput) > 0.001f || Mathf.Abs(pitchInput) > 0.001f)
            {
                currentYaw += yawInput * rotationSpeed * Time.deltaTime;
                currentPitch += pitchInput * rotationSpeed * Time.deltaTime;

                // Clamp pitch to prevent flipping
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

                // Apply rotation
                transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            }
        }

        private void HandleZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !cam.orthographic)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            // Scroll up = zoom in (decrease orthographic size)
            // Scroll down = zoom out (increase orthographic size)
            float newSize = cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime * 10f;
            cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
        }
    }
}

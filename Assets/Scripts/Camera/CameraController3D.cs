using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Systems;
using FaeMaze.Visitors;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Controls a perspective 3D camera with orbit, dolly, and pan controls.
    /// Includes collision-based zoom limits and maintains focus on maze objects.
    /// </summary>
    public class CameraController3D : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        private float panSpeed = 10f;

        [SerializeField]
        private float orbitSpeed = 100f;

        [SerializeField]
        private float dollySpeed = 5f;

        [Header("Orbit Constraints")]
        [SerializeField]
        [Tooltip("Minimum pitch angle in degrees (looking down)")]
        private float minPitch = 10f;

        [SerializeField]
        [Tooltip("Maximum pitch angle in degrees (looking up)")]
        private float maxPitch = 80f;

        [Header("Distance Constraints")]
        [SerializeField]
        [Tooltip("Minimum distance from focus point")]
        private float minDistance = 5f;

        [SerializeField]
        [Tooltip("Maximum distance from focus point")]
        private float maxDistance = 30f;

        [Header("Collision Settings")]
        [SerializeField]
        [Tooltip("Enable collision-based zoom limiting")]
        private bool enableCollisionDetection = true;

        [SerializeField]
        [Tooltip("Layer mask for camera collision")]
        private LayerMask collisionLayers = -1;

        [SerializeField]
        [Tooltip("Radius for collision sphere")]
        private float collisionRadius = 0.5f;

        [Header("References")]
        [SerializeField]
        private MazeGridBehaviour mazeGridBehaviour;

        #endregion

        #region Private Fields

        private Camera cam;

        // Orbit state
        private Vector3 focusPoint;
        private float currentYaw;
        private float currentPitch = 45f;
        private float currentDistance = 15f;

        // Mouse drag state
        private bool isOrbiting;
        private bool isPanning;
        private Vector3 lastMouseWorldPosition;

        // Focus state
        private bool isFocusing;
        private Vector3 focusTargetPosition;
        private float focusLerpSpeed = 10f;
        private VisitorController focusVisitor;
        private float trackingLogInterval = 0.5f;
        private float trackingLogTimer;
        private bool trackingVisitorLostLogged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            cam = GetComponent<Camera>();

            // Ensure camera is perspective
            if (cam != null && cam.orthographic)
            {
                cam.orthographic = false;
                cam.fieldOfView = 60f;
            }

            // Initialize focus point at current position
            focusPoint = transform.position;
            focusPoint.z = 0f; // Keep focus on XY plane
        }

        private void Start()
        {
            // Position camera based on initial orbit parameters
            UpdateCameraPosition();
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            HandleFocusShortcuts();
            HandleKeyboardPan();
            HandleMouseControls();
            HandleScrollZoom();
            HandleFocusMovement();
            ClampToMazeBounds();
            UpdateCameraPosition();
        }

        #endregion

        #region Input Handling

        private void HandleKeyboardPan()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || isOrbiting)
            {
                return;
            }

            Vector2 movement = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                movement.y += 1f;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                movement.y -= 1f;
            }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                movement.x += 1f;
            }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                movement.x -= 1f;
            }

            if (movement.sqrMagnitude <= 0f)
            {
                return;
            }

            // Pan relative to camera orientation (but only in XY plane)
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 delta = (right * movement.x + forward * movement.y) * panSpeed * Time.deltaTime;
            focusPoint += delta;
        }

        private void HandleFocusShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                FocusOnHeart(true);
            }

            if (keyboard.digit2Key.wasPressedThisFrame)
            {
                FocusOnEntrance(true);
            }

            if (keyboard.digit3Key.wasPressedThisFrame && GameController.Instance != null)
            {
                VisitorController lastVisitor = GameController.Instance.LastSpawnedVisitor;
                if (lastVisitor != null)
                {
                    FocusOnVisitor(lastVisitor, false);
                }
            }
        }

        private void HandleMouseControls()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Right mouse button = orbit
            if (mouse.rightButton.wasPressedThisFrame)
            {
                isOrbiting = true;
            }
            if (mouse.rightButton.wasReleasedThisFrame)
            {
                isOrbiting = false;
            }

            // Middle mouse button = pan
            if (mouse.middleButton.wasPressedThisFrame)
            {
                isPanning = true;
                lastMouseWorldPosition = GetMouseWorldPosition();
            }
            if (mouse.middleButton.wasReleasedThisFrame)
            {
                isPanning = false;
            }

            // Handle orbit drag
            if (isOrbiting && mouse.rightButton.isPressed)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                currentYaw += mouseDelta.x * orbitSpeed * Time.deltaTime;
                currentPitch -= mouseDelta.y * orbitSpeed * Time.deltaTime;
                // Removed pitch clamping - camera can now rotate freely
                // currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }

            // Handle pan drag
            if (isPanning && mouse.middleButton.isPressed)
            {
                Vector3 currentMouseWorldPosition = GetMouseWorldPosition();
                Vector3 delta = lastMouseWorldPosition - currentMouseWorldPosition;
                focusPoint += delta;
                lastMouseWorldPosition = GetMouseWorldPosition();
            }
        }

        private void HandleScrollZoom()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            // Dolly in/out
            float zoomFactor = Mathf.Exp(-scroll * dollySpeed * Time.deltaTime);
            currentDistance = currentDistance * zoomFactor;
            // Removed distance clamping - camera can now zoom freely
            // currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        private Vector3 GetMouseWorldPosition()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return Vector3.zero;
            }

            Vector2 mousePosition = mouse.position.ReadValue();

            // Raycast to find intersection with Z=0 plane (maze ground)
            Ray ray = cam.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0f));
            Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);

            if (groundPlane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return focusPoint;
        }

        private void HandleFocusMovement()
        {
            if (!isFocusing)
            {
                return;
            }

            if (focusVisitor != null)
            {
                focusTargetPosition = new Vector3(
                    focusVisitor.transform.position.x,
                    focusVisitor.transform.position.y,
                    0f);

                trackingLogTimer -= Time.deltaTime;
                if (trackingLogTimer <= 0f)
                {
                    trackingLogTimer = trackingLogInterval;
                }

                trackingVisitorLostLogged = false;
            }
            else if (!trackingVisitorLostLogged)
            {
                trackingVisitorLostLogged = true;
            }

            Vector3 currentPosition = focusPoint;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, focusTargetPosition, focusLerpSpeed * Time.deltaTime);
            focusPoint = newPosition;

            if (focusVisitor == null && Vector3.SqrMagnitude(newPosition - focusTargetPosition) < 0.0001f)
            {
                isFocusing = false;
            }
        }

        #endregion

        #region Camera Positioning

        private void UpdateCameraPosition()
        {
            // Calculate camera position based on orbit parameters
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 direction = rotation * Vector3.back;

            // Apply collision detection
            float finalDistance = currentDistance;
            if (enableCollisionDetection)
            {
                finalDistance = GetCollisionAdjustedDistance(direction);
            }

            Vector3 desiredPosition = focusPoint + direction * finalDistance;
            transform.position = desiredPosition;
            transform.LookAt(focusPoint);
        }

        private float GetCollisionAdjustedDistance(Vector3 direction)
        {
            // Raycast from focus point outward to check for obstacles
            RaycastHit hit;
            if (Physics.SphereCast(
                focusPoint,
                collisionRadius,
                direction,
                out hit,
                currentDistance,
                collisionLayers))
            {
                // Reduce distance to avoid collision
                return Mathf.Max(hit.distance - collisionRadius, minDistance);
            }

            return currentDistance;
        }

        #endregion

        #region Bounds Handling

        private void ClampToMazeBounds()
        {
            if (!TryGetMazeDimensions(out Vector3 origin, out float width, out float height))
            {
                return;
            }

            // Allow camera to move ±10 units beyond maze edges
            float padding = 10f;

            // Clamp focus point to maze bounds with padding
            Vector3 clampedFocus = focusPoint;
            clampedFocus.x = Mathf.Clamp(clampedFocus.x, origin.x - padding, origin.x + width + padding);
            clampedFocus.y = Mathf.Clamp(clampedFocus.y, origin.y - padding, origin.y + height + padding);
            clampedFocus.z = 0f; // Keep on maze plane
            focusPoint = clampedFocus;
        }

        private bool TryGetMazeDimensions(out Vector3 origin, out float width, out float height)
        {
            origin = Vector3.zero;
            width = 0f;
            height = 0f;

            if (mazeGridBehaviour == null)
            {
                return false;
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            if (grid == null)
            {
                return false;
            }

            origin = mazeGridBehaviour.GridToWorld(0, 0);
            width = grid.Width;
            height = grid.Height;
            return true;
        }

        #endregion

        #region Focus Controls

        /// <summary>
        /// Focuses the camera on the given world position.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos, bool instant = false, float lerpSpeed = 10f)
        {
            focusVisitor = null;
            Vector3 targetPosition = new Vector3(worldPos.x, worldPos.y, 0f);
            focusTargetPosition = targetPosition;

            if (instant)
            {
                focusPoint = targetPosition;
                isFocusing = false;
                ClampToMazeBounds();
            }
            else
            {
                focusTargetPosition = targetPosition;
                focusLerpSpeed = Mathf.Max(lerpSpeed, 0f);
                isFocusing = true;
            }
        }

        /// <summary>
        /// Focuses on the Heart of the Maze.
        /// </summary>
        public void FocusOnHeart(bool instant = false)
        {
            if (GameController.Instance == null || GameController.Instance.Heart == null)
            {
                return;
            }

            FocusOnPosition(GameController.Instance.Heart.transform.position, instant);
        }

        /// <summary>
        /// Focuses on the Maze Entrance.
        /// </summary>
        public void FocusOnEntrance(bool instant = false)
        {
            if (GameController.Instance == null || GameController.Instance.Entrance == null)
            {
                return;
            }

            FocusOnPosition(GameController.Instance.Entrance.transform.position, instant);
        }

        /// <summary>
        /// Focuses on the given visitor.
        /// </summary>
        public void FocusOnVisitor(VisitorController visitor, bool instant = false)
        {
            if (visitor == null)
            {
                return;
            }

            FocusOnPosition(visitor.transform.position, instant);
            focusVisitor = visitor;
            focusLerpSpeed = Mathf.Max(focusLerpSpeed, 0f);
            isFocusing = true;
            trackingLogTimer = 0f;
            trackingVisitorLostLogged = false;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw focus point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(focusPoint, 0.5f);

            // Draw camera direction line
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(focusPoint, transform.position);
            }
        }

        #endregion
    }
}

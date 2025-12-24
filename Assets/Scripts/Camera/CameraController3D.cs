using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Systems;
using FaeMaze.Visitors;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Controls a perspective 3D camera with orbit, dolly, and pan controls.
    /// Orbits on a horizontal circle (XZ plane) around a focal tile while always looking at the focus point.
    /// Includes collision-based zoom limits and maintains focus on maze objects.
    /// </summary>
    public class CameraController3D : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Focal Point Settings")]
        [SerializeField]
        [Tooltip("Enable third-person focal point controls")]
        private bool useFocalPointMode = true;

        [SerializeField]
        [Tooltip("Step distance for moving the focal point forward/backward per key press")]
        private float focalMoveSpeed = 1f;

        [SerializeField]
        [Tooltip("Degrees to rotate the focal point per key press")]
        private float focalTurnSpeed = 90f;

        [SerializeField]
        [Tooltip("Constant camera distance behind the focal point")]
        private float focalFollowDistance = 3f;

        [SerializeField]
        [Tooltip("Constant camera height above the focal point")]
        private float focalHeightOffset = 2f;

        [SerializeField]
        [Tooltip("Optional transform to use as the focal point (otherwise created at runtime)")]
        private Transform focalPointTransform;

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

        [SerializeField]
        [Tooltip("Enable distance constraints")]
        private bool enableDistanceConstraints = false;

        [Header("Auto Orbit")]
        [SerializeField]
        [Tooltip("Enable continuous automatic orbiting at orbitSpeed")]
        private bool autoOrbit = false;

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

        [Header("Focus Settings")]
        [SerializeField]
        [Tooltip("Default focus point if no tile is selected")]
        private Vector3 defaultFocusPoint = Vector3.zero;

        [SerializeField]
        [Tooltip("Optional default focus transform")]
        private Transform defaultFocusTransform;

        [Header("References")]
        [SerializeField]
        private MazeGridBehaviour mazeGridBehaviour;

        #endregion

        #region Private Fields

        private Camera cam;

        // Orbit state - cached for drift-free computation
        private Vector3 _focusPoint;
        private float _yawDeg;
        private float _pitchDeg;
        private float _radius;

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

        // Focal point state
        private bool focalPointInitialized;
        private bool focalCameraPoseInitialized;

        // Debugging
        private float rollLogTimer;
        private const float RollLogInterval = 0.5f;
        private bool loggedMazeUpFlip;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the current focus point in world space.
        /// </summary>
        public Vector3 FocusPoint => _focusPoint;

        /// <summary>
        /// Gets or sets the auto orbit enabled state.
        /// </summary>
        public bool AutoOrbit
        {
            get => autoOrbit;
            set => autoOrbit = value;
        }

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

            // Initialize focus point
            if (defaultFocusTransform != null)
            {
                _focusPoint = defaultFocusTransform.position;
                _focusPoint.z = 0f; // Keep focus on XY plane
            }
            else
            {
                _focusPoint = defaultFocusPoint;
                _focusPoint.z = 0f;
            }

            // Compute initial yaw, pitch, radius from current transform
            Vector3 toCamera = transform.position - _focusPoint;
            _radius = toCamera.magnitude;

            // Calculate yaw and pitch from the offset vector
            // Yaw is rotation around Y-axis (horizontal angle)
            _yawDeg = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;

            // Pitch is angle from horizontal plane
            float horizontalDist = Mathf.Sqrt(toCamera.x * toCamera.x + toCamera.z * toCamera.z);
            _pitchDeg = Mathf.Atan2(toCamera.y, horizontalDist) * Mathf.Rad2Deg;

            // Clamp initial values
            if (enableDistanceConstraints)
            {
                _radius = Mathf.Clamp(_radius, minDistance, maxDistance);
            }
        }

        private void Start()
        {
            if (useFocalPointMode)
            {
                InitializeFocalPoint();
                TryConfigureInitialFocalCameraPose();
                _focusPoint = focalPointTransform != null ? focalPointTransform.position : _focusPoint;
                return;
            }

            // Position camera based on initial orbit parameters
            UpdateCameraPosition();
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            if (useFocalPointMode)
            {
                if (!focalPointInitialized)
                {
                    InitializeFocalPoint();
                }

                TryConfigureInitialFocalCameraPose();

                HandleFocalPointInput();
                UpdateFocalPointCameraPosition();
                return;
            }

            HandleFocusShortcuts();
            HandleKeyboardInput();
            HandleMouseControls();
            HandleScrollZoom();
            HandleFocusMovement();
            HandleAutoOrbit();
            UpdateCameraPosition();
        }

        #endregion

        #region Input Handling

        private void HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (isOrbiting)
            {
                return;
            }

            // Cancel focus when user manually controls camera
            if (isFocusing && (keyboard.wKey.isPressed || keyboard.sKey.isPressed ||
                               keyboard.aKey.isPressed || keyboard.dKey.isPressed ||
                               keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed ||
                               keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed))
            {
                isFocusing = false;
            }

            // W/S: Move focus point forward/backward
            float forwardInput = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                forwardInput += 1f;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                forwardInput -= 1f;
            }

            if (Mathf.Abs(forwardInput) > 0.001f)
            {
                // Move focus point along camera forward direction (projected on XY plane)
                Vector3 forward = transform.forward;
                forward.z = 0f; // Project onto XY plane
                forward.Normalize();

                Vector3 movement = forward * forwardInput * panSpeed * Time.deltaTime;
                _focusPoint += movement;
            }

            // A/D or ←/→: Orbit yaw (keyboard orbit)
            float yawInput = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                yawInput += 1f;
            }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                yawInput -= 1f;
            }

            if (Mathf.Abs(yawInput) > 0.001f)
            {
                _yawDeg += yawInput * orbitSpeed * Time.deltaTime;
            }
        }

        private void HandleFocalPointInput()
        {
            if (!useFocalPointMode || focalPointTransform == null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            int moveInput = 0;
            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                moveInput += 1;
            }

            if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                moveInput -= 1;
            }

            int turnInput = 0;
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                turnInput -= 1;
            }

            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                turnInput += 1;
            }

            if (moveInput != 0)
            {
                Vector3 forward = focalPointTransform.forward;
                forward.z = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    forward.Normalize();
                }
                else
                {
                    forward = Vector3.right;
                }

                focalPointTransform.position += forward * moveInput * focalMoveSpeed;
                Vector3 planarPosition = focalPointTransform.position;
                planarPosition.z = 0f;
                focalPointTransform.position = planarPosition;
            }

            if (turnInput != 0)
            {
                Vector3 up = GetMazeUpDirection();
                focalPointTransform.Rotate(up, turnInput * focalTurnSpeed, Space.World);
            }
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
                _yawDeg += mouseDelta.x * orbitSpeed * Time.deltaTime;
                _pitchDeg -= mouseDelta.y * orbitSpeed * Time.deltaTime;
                _pitchDeg = Mathf.Clamp(_pitchDeg, minPitch, maxPitch);

                // Cancel auto focus when orbiting manually
                if (isFocusing)
                {
                    isFocusing = false;
                }
            }

            // Handle pan drag
            if (isPanning && mouse.middleButton.isPressed)
            {
                Vector3 currentMouseWorldPosition = GetMouseWorldPosition();
                Vector3 delta = lastMouseWorldPosition - currentMouseWorldPosition;
                _focusPoint += delta;
                lastMouseWorldPosition = GetMouseWorldPosition();

                // Cancel auto focus when panning manually
                if (isFocusing)
                {
                    isFocusing = false;
                }
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
            _radius = _radius * zoomFactor;

            if (enableDistanceConstraints)
            {
                _radius = Mathf.Clamp(_radius, minDistance, maxDistance);
            }
        }

        private void HandleAutoOrbit()
        {
            if (!autoOrbit)
            {
                return;
            }

            // Continuously orbit at orbitSpeed degrees per second
            _yawDeg += orbitSpeed * Time.deltaTime;
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

            return _focusPoint;
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

            Vector3 currentPosition = _focusPoint;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, focusTargetPosition, focusLerpSpeed * Time.deltaTime);
            _focusPoint = newPosition;

            if (focusVisitor == null && Vector3.SqrMagnitude(newPosition - focusTargetPosition) < 0.0001f)
            {
                isFocusing = false;
            }
        }

        #endregion

        #region Focal Point Mode

        private void InitializeFocalPoint()
        {
            if (focalPointInitialized)
            {
                return;
            }

            if (focalPointTransform == null)
            {
                GameObject focalPointObj = new GameObject("Focal Point");
                focalPointTransform = focalPointObj.transform;
            }

            if (GameController.Instance == null || GameController.Instance.Heart == null)
            {
                // Wait until the heart exists so we can place the focal point correctly.
                return;
            }

            Vector3 startPosition = GameController.Instance.Heart.transform.position;
            startPosition.z = 0f;

            Vector3 facingDirection = GetPathDirectionToHeart();
            if (facingDirection.sqrMagnitude < 0.0001f)
            {
                facingDirection = Vector3.up;
            }

            focalPointTransform.SetPositionAndRotation(
                startPosition,
                Quaternion.LookRotation(facingDirection, GetMazeUpDirection()));

            Debug.Log($"[CameraController3D] Focal point initialized at {startPosition} with forward {facingDirection} and up {GetMazeUpDirection()}");

            _focusPoint = startPosition;
            focalPointInitialized = true;
        }

        private void TryConfigureInitialFocalCameraPose()
        {
            if (focalCameraPoseInitialized || !focalPointInitialized || focalPointTransform == null)
            {
                return;
            }

            Vector3 facingDirection = GetPathDirectionToHeart();
            if (facingDirection.sqrMagnitude < 0.0001f)
            {
                facingDirection = Vector3.up;
            }

            Vector3 offset = -facingDirection.normalized * 3f + Vector3.back * 3f;

            Vector3 desiredPosition = focalPointTransform.position + offset;
            Vector3 worldUp = Vector3.forward;
            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(focalPointTransform.position - desiredPosition, worldUp);

            Vector3 euler = transform.rotation.eulerAngles;
            euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);
            Debug.Log($"[CameraController3D] Initial focal camera pose -> position: {transform.position}, lookTarget: {focalPointTransform.position}, forward: {facingDirection}, up: {worldUp}, euler: {euler}");

            focalFollowDistance = 3f;
            focalHeightOffset = -3f;

            focalCameraPoseInitialized = true;
        }

        private Vector3 GetPathDirectionToHeart()
        {
            if (GameController.Instance == null)
            {
                return Vector3.zero;
            }

            var entrance = GameController.Instance.Entrance;
            var heart = GameController.Instance.Heart;

            if (entrance == null || heart == null)
            {
                return Vector3.zero;
            }

            if (mazeGridBehaviour != null)
            {
                List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
                bool pathFound = GameController.Instance.TryFindPath(entrance.GridPosition, heart.GridPosition, pathNodes);
                if (pathFound && pathNodes.Count >= 2)
                {
                    MazeGrid.MazeNode lastNode = pathNodes[pathNodes.Count - 1];
                    MazeGrid.MazeNode previousNode = pathNodes[pathNodes.Count - 2];

                    Vector3 lastWorld = mazeGridBehaviour.NodeToWorld(lastNode);
                    Vector3 previousWorld = mazeGridBehaviour.NodeToWorld(previousNode);
                    Vector3 direction = lastWorld - previousWorld;
                    direction.z = 0f;

                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        direction.Normalize();
                        return direction;
                    }
                }
            }

            Vector3 fallbackDirection = heart.transform.position - entrance.transform.position;
            fallbackDirection.z = 0f;
            if (fallbackDirection.sqrMagnitude < 0.0001f)
            {
                return Vector3.up;
            }

            return fallbackDirection.normalized;
        }

        private void UpdateFocalPointCameraPosition()
        {
            if (!useFocalPointMode || focalPointTransform == null)
            {
                return;
            }

            Vector3 forward = focalPointTransform.forward;
            forward.z = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
            }
            else
            {
                forward = Vector3.right;
            }

            Vector3 worldUp = Vector3.forward;
            Vector3 offset = -forward * focalFollowDistance + worldUp * focalHeightOffset;

            Vector3 desiredPosition = focalPointTransform.position + offset;

            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(focalPointTransform.position - transform.position, worldUp);
            _focusPoint = focalPointTransform.position;

            rollLogTimer -= Time.deltaTime;
            if (rollLogTimer <= 0f)
            {
                rollLogTimer = RollLogInterval;
                Vector3 euler = transform.rotation.eulerAngles;
                if (Mathf.Abs(euler.z) > 1f && Mathf.Abs(euler.z - 360f) > 1f)
                {
                    Debug.Log($"[CameraController3D] Focal follow roll detected -> rollZ: {euler.z:F2}, pos: {transform.position}, forward: {forward}, up: {worldUp}, offset: {offset}");
                }
            }
        }

        private Vector3 GetMazeUpDirection()
        {
            if (mazeGridBehaviour == null)
            {
                return Vector3.forward;
            }

            Vector3 mazeUp = mazeGridBehaviour.MazeUpDirection;
            if (mazeUp.sqrMagnitude < 0.0001f)
            {
                return Vector3.forward;
            }

            // Always prefer the positive Z-facing up direction so the camera isn't rolled 180°
            // when the maze is mirrored through the XY plane.
            if (Vector3.Dot(mazeUp, Vector3.forward) < 0f)
            {
                mazeUp = -mazeUp;
                if (!loggedMazeUpFlip)
                {
                    loggedMazeUpFlip = true;
                    Debug.Log($"[CameraController3D] Maze up vector flipped toward +Z: {mazeUp}");
                }
            }

            return mazeUp.normalized;
        }

        #endregion

        #region Camera Positioning

        private void UpdateCameraPosition()
        {
            // Compute position from yaw/pitch/radius (drift-free)
            Quaternion rot = Quaternion.Euler(_pitchDeg, _yawDeg, 0f);
            Vector3 offset = rot * new Vector3(0, 0, -_radius);

            // Apply collision detection to shorten radius if needed
            float finalRadius = _radius;
            if (enableCollisionDetection)
            {
                finalRadius = GetCollisionAdjustedDistance(offset.normalized);
                offset = offset.normalized * finalRadius;
            }

            // Set camera position
            Vector3 desiredPosition = _focusPoint + offset;
            transform.position = desiredPosition;

            // Always look at focus point with Vector3.up as world up
            transform.LookAt(_focusPoint, Vector3.up);
        }

        private float GetCollisionAdjustedDistance(Vector3 direction)
        {
            // Raycast from focus point outward to check for obstacles
            RaycastHit hit;
            if (Physics.SphereCast(
                _focusPoint,
                collisionRadius,
                direction,
                out hit,
                _radius,
                collisionLayers))
            {
                // Reduce distance to avoid collision
                return Mathf.Max(hit.distance - collisionRadius, minDistance);
            }

            return _radius;
        }

        #endregion

        #region Focus Controls

        /// <summary>
        /// Sets the camera focus point (alias for FocusOnPosition).
        /// </summary>
        public void SetFocusPoint(Vector3 worldPos, bool instant = false, float lerpSpeed = 10f)
        {
            FocusOnPosition(worldPos, instant, lerpSpeed);
        }

        /// <summary>
        /// Focuses the camera on the given world position.
        /// Preserves yaw/pitch and recomputes radius from current camera position for stability.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos, bool instant = false, float lerpSpeed = 10f)
        {
            focusVisitor = null;
            Vector3 targetPosition = new Vector3(worldPos.x, worldPos.y, 0f);
            focusTargetPosition = targetPosition;

            if (instant)
            {
                // Preserve yaw/pitch, recompute radius for stability
                Vector3 oldToCamera = transform.position - _focusPoint;
                _focusPoint = targetPosition;

                // Recompute radius from current camera distance to new focus point
                Vector3 newToCamera = transform.position - _focusPoint;
                _radius = newToCamera.magnitude;

                if (enableDistanceConstraints)
                {
                    _radius = Mathf.Clamp(_radius, minDistance, maxDistance);
                }

                isFocusing = false;
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
            Gizmos.DrawWireSphere(_focusPoint, 0.5f);

            // Draw camera direction line
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(_focusPoint, transform.position);
            }

            // Draw orbit circle in XZ plane
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                int segments = 32;
                float angleStep = 360f / segments;
                Vector3 prevPoint = Vector3.zero;

                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    float x = _focusPoint.x + Mathf.Cos(angle) * _radius * Mathf.Cos(_pitchDeg * Mathf.Deg2Rad);
                    float z = _focusPoint.z + Mathf.Sin(angle) * _radius * Mathf.Cos(_pitchDeg * Mathf.Deg2Rad);
                    float y = _focusPoint.y + _radius * Mathf.Sin(_pitchDeg * Mathf.Deg2Rad);

                    Vector3 point = new Vector3(x, y, z);

                    if (i > 0)
                    {
                        Gizmos.DrawLine(prevPoint, point);
                    }

                    prevPoint = point;
                }
            }
        }

        #endregion
    }
}

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
        [Tooltip("Camera viewing angle in degrees (15 = low angle, 89 = near top-down)")]
        [Range(15f, 89f)]
        private float focalViewAngle = 45f;

        [SerializeField]
        [Tooltip("Speed at which scroll wheel changes viewing angle")]
        private float angleChangeSpeed = 5f;

        [SerializeField]
        [Tooltip("Maximum zoom distance along focal axis (Shift+Scroll)")]
        private float maxFocalZoomDistance = 3f;

        [SerializeField]
        [Tooltip("Speed of zoom along focal axis")]
        private float focalZoomSpeed = 0.5f;

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
        private float minPitch = 5f;

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
        private Vector2 lastMouseScreenPosition; // For focal point mode panning (screen-space tracking)

        // Focus state
        private bool isFocusing;
        private Vector3 focusTargetPosition;
        private float focusLerpSpeed = 10f;
        private VisitorControllerBase focusVisitor;
        private float trackingLogInterval = 0.5f;
        private float trackingLogTimer;
        private bool trackingVisitorLostLogged;

        // Focal point state
        private bool focalPointInitialized;
        private bool focalCameraPoseInitialized;
        private float focalZoomOffset = 0f; // Current zoom offset along focal axis (0 = minimum/start, positive = further away)

        // Focal point smooth movement state (for tutorial transitions)
        private bool isFocalPointMoving;
        private Vector3 focalPointMoveTarget;
        private float focalPointMoveSpeed = 5f;

        // Debugging
        private float rollLogTimer;
        private const float RollLogInterval = 0.5f;
        private bool loggedMazeUpFlip;

        // Focus cycling state
        private int currentPortalIndex = -1;
        private int currentVisitorIndex = -1;
        private DynamicMazeGrowth dynamicMazeGrowth;

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

        /// <summary>
        /// Gets the focal point transform (for third-person camera mode).
        /// </summary>
        public Transform FocalPointTransform => focalPointTransform;

        /// <summary>
        /// Gets the focal point position in world space.
        /// </summary>
        public Vector3 FocalPointPosition => focalPointTransform != null ? focalPointTransform.position : _focusPoint;

        /// <summary>
        /// Gets whether the focal point is currently moving to a target position.
        /// </summary>
        public bool IsFocalPointMoving => isFocalPointMoving;

        /// <summary>
        /// Applies a world-space offset to the camera and its focus targets.
        /// Keeps the relative camera orbit configuration intact.
        /// </summary>
        /// <param name="worldOffset">World-space offset to apply.</param>
        public void ApplyWorldOffset(Vector3 worldOffset)
        {
            if (worldOffset == Vector3.zero)
            {
                return;
            }

            _focusPoint += worldOffset;
            focusTargetPosition += worldOffset;

            if (focalPointTransform != null)
            {
                focalPointTransform.position += worldOffset;
            }

            transform.position += worldOffset;
        }

        /// <summary>
        /// Applies camera settings from GameSettings at runtime.
        /// Call this after changing settings to apply them without reloading the scene.
        /// </summary>
        public void ApplySettingsFromGameSettings()
        {
            focalMoveSpeed = GameSettings.FocusSpeed;
            panSpeed = GameSettings.CameraPanSpeed;
            dollySpeed = GameSettings.CameraZoomSpeed;
            minDistance = GameSettings.CameraMinZoom;
            maxDistance = GameSettings.CameraMaxZoom;

            if (cam != null)
            {
                cam.fieldOfView = GameSettings.CameraFieldOfView;
            }
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
            // Load camera settings from GameSettings
            focalMoveSpeed = GameSettings.FocusSpeed;
            panSpeed = GameSettings.CameraPanSpeed;
            dollySpeed = GameSettings.CameraZoomSpeed;
            minDistance = GameSettings.CameraMinZoom;
            maxDistance = GameSettings.CameraMaxZoom;

            if (cam != null)
            {
                cam.fieldOfView = GameSettings.CameraFieldOfView;
            }

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

            // Handle focus shortcuts regardless of camera mode
            HandleFocusShortcuts();

            if (useFocalPointMode)
            {
                if (!focalPointInitialized)
                {
                    InitializeFocalPoint();
                }

                TryConfigureInitialFocalCameraPose();

                // Handle smooth focal point movement (e.g., from tutorial transitions)
                UpdateFocalPointSmoothMovement();

                HandleFocalPointInput();
                HandleScrollZoom();
                UpdateFocalPointCameraPosition();
                return;
            }
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
            if (isOrbiting)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            // Cancel focus when user manually controls camera
            bool anyMovementPressed =
                InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveForwardBinding) ||
                InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveBackwardBinding) ||
                InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnLeftBinding) ||
                InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnRightBinding) ||
                (keyboard != null && (keyboard.upArrowKey.isPressed || keyboard.downArrowKey.isPressed ||
                                      keyboard.leftArrowKey.isPressed || keyboard.rightArrowKey.isPressed));

            if (isFocusing && anyMovementPressed)
            {
                isFocusing = false;
            }

            // W/S (or configured binding): Move focus point forward/backward
            float forwardInput = 0f;
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveForwardBinding) ||
                (keyboard != null && keyboard.upArrowKey.isPressed))
            {
                forwardInput += 1f;
            }
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveBackwardBinding) ||
                (keyboard != null && keyboard.downArrowKey.isPressed))
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

            // A/D or ←/→ (or configured binding): Orbit yaw (keyboard orbit)
            float yawInput = 0f;
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnRightBinding) ||
                (keyboard != null && keyboard.rightArrowKey.isPressed))
            {
                yawInput += 1f;
            }
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnLeftBinding) ||
                (keyboard != null && keyboard.leftArrowKey.isPressed))
            {
                yawInput -= 1f;
            }

            if (Mathf.Abs(yawInput) > 0.001f)
            {
                float yawDelta = yawInput * orbitSpeed * Time.deltaTime;
                _yawDeg += yawDelta;

            }
        }

        private void HandleFocalPointInput()
        {
            if (!useFocalPointMode || focalPointTransform == null)
            {
                return;
            }

            // Don't allow user input until focal point is fully initialized
            // This prevents user rotation from being overwritten by late initialization
            if (!focalPointInitialized)
            {
                return;
            }

            // Handle mouse controls for focal point mode
            HandleFocalPointMouseControls();

            // Use configurable bindings + arrow key fallback
            Keyboard keyboard = Keyboard.current;
            float moveInput = 0f;
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveForwardBinding) ||
                (keyboard != null && keyboard.upArrowKey.isPressed))
            {
                moveInput += 1f;
            }

            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraMoveBackwardBinding) ||
                (keyboard != null && keyboard.downArrowKey.isPressed))
            {
                moveInput -= 1f;
            }

            float turnInput = 0f;
            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnLeftBinding) ||
                (keyboard != null && keyboard.leftArrowKey.isPressed))
            {
                turnInput -= 1f;
            }

            if (InputBindingHelper.IsBindingPressed(GameSettings.CameraTurnRightBinding) ||
                (keyboard != null && keyboard.rightArrowKey.isPressed))
            {
                turnInput += 1f;
            }

            if (Mathf.Abs(moveInput) > 0.001f)
            {
                // Calculate forward direction from focal point's yaw angle (rotation around Z axis)
                // This ensures movement is always in the direction the focal point is "facing"
                float yawAngle = focalPointTransform.eulerAngles.z;
                float yawRad = yawAngle * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Cos(yawRad), Mathf.Sin(yawRad), 0f);

                focalPointTransform.position += forward * moveInput * focalMoveSpeed * Time.deltaTime;
                Vector3 planarPosition = focalPointTransform.position;
                planarPosition.z = 0f;
                focalPointTransform.position = planarPosition;

                // Constrain focal point to within 1 tile of maze bounds
                ConstrainFocalPointToMazeBounds();
            }

            if (Mathf.Abs(turnInput) > 0.001f)
            {
                Vector3 up = GetMazeUpDirection();
                float yawDelta = turnInput * focalTurnSpeed * Time.deltaTime;


                focalPointTransform.Rotate(up, yawDelta, Space.World);

            }
        }

        /// <summary>
        /// Handles mouse orbit and pan controls in focal point mode.
        /// Right-click drag: rotate focal point (orbit camera around focal point)
        /// Middle-click drag: pan focal point position
        /// </summary>
        private void HandleFocalPointMouseControls()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Orbit control (configurable, default: right mouse button)
            // In focal point mode, this rotates the focal point direction
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraOrbitBinding))
            {
                isOrbiting = true;
            }
            if (InputBindingHelper.WasBindingReleasedThisFrame(GameSettings.CameraOrbitBinding))
            {
                isOrbiting = false;
            }

            // Pan control (configurable, default: middle mouse button)
            // In focal point mode, this moves the focal point position
            // We use SCREEN-SPACE tracking to avoid feedback loops (camera moves when focal point moves)
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraPanBinding))
            {
                isPanning = true;
                lastMouseScreenPosition = mouse.position.ReadValue();
            }
            if (InputBindingHelper.WasBindingReleasedThisFrame(GameSettings.CameraPanBinding))
            {
                isPanning = false;
            }

            // Handle orbit drag - rotate focal point direction
            if (isOrbiting && InputBindingHelper.IsBindingPressed(GameSettings.CameraOrbitBinding))
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                if (Mathf.Abs(mouseDelta.x) > 0.001f)
                {
                    Vector3 up = GetMazeUpDirection();
                    // Negative because dragging right should rotate the view left (focal point rotates right)
                    float yawDelta = -mouseDelta.x * orbitSpeed * Time.deltaTime;
                    focalPointTransform.Rotate(up, yawDelta, Space.World);
                }
            }

            // Handle pan drag - move focal point position using SCREEN-SPACE delta
            // This avoids the feedback loop where moving the focal point moves the camera,
            // which changes what GetMouseWorldPosition() returns, causing oscillation.
            if (isPanning && InputBindingHelper.IsBindingPressed(GameSettings.CameraPanBinding))
            {
                Vector2 currentMouseScreen = mouse.position.ReadValue();
                Vector2 screenDelta = currentMouseScreen - lastMouseScreenPosition;

                if (screenDelta.sqrMagnitude > 0.001f)
                {
                    // Convert screen delta to world delta using camera's orientation
                    // Get camera's right and up vectors projected onto the XY plane
                    Vector3 camRight = transform.right;
                    Vector3 camUp = transform.up;

                    // Project onto XY plane (Z=0)
                    camRight.z = 0f;
                    camUp.z = 0f;
                    camRight.Normalize();
                    camUp.Normalize();

                    // Scale factor: how many world units per screen pixel
                    // This depends on camera distance and FOV
                    float distanceToFocal = Vector3.Distance(transform.position, focalPointTransform.position);
                    float worldUnitsPerPixel = distanceToFocal * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f / Screen.height;

                    // Calculate world delta (negative because dragging moves view, not focal point)
                    Vector3 worldDelta = (-camRight * screenDelta.x - camUp * screenDelta.y) * worldUnitsPerPixel;

                    // Apply movement
                    focalPointTransform.position += worldDelta;

                    // Keep focal point on the XY plane
                    Vector3 planarPosition = focalPointTransform.position;
                    planarPosition.z = 0f;
                    focalPointTransform.position = planarPosition;

                    // Constrain to maze bounds
                    ConstrainFocalPointToMazeBounds();
                }

                // Update last screen position for next frame
                lastMouseScreenPosition = currentMouseScreen;
            }
        }

        private void HandleFocusShortcuts()
        {
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraFocusHeartBinding))
            {
                FocusOnHeart(true);
            }

            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraFocusEntranceBinding))
            {
                CycleToNextPortal();
            }

            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraFocusVisitorBinding))
            {
                CycleToNextVisitor();
            }
        }

        private void HandleMouseControls()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Orbit control (configurable, default: right mouse button)
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraOrbitBinding))
            {
                isOrbiting = true;
            }
            if (InputBindingHelper.WasBindingReleasedThisFrame(GameSettings.CameraOrbitBinding))
            {
                isOrbiting = false;
            }

            // Pan control (configurable, default: middle mouse button)
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraPanBinding))
            {
                isPanning = true;
                lastMouseWorldPosition = GetMouseWorldPosition();
            }
            if (InputBindingHelper.WasBindingReleasedThisFrame(GameSettings.CameraPanBinding))
            {
                isPanning = false;
            }

            // Handle orbit drag
            if (isOrbiting && InputBindingHelper.IsBindingPressed(GameSettings.CameraOrbitBinding))
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
            if (isPanning && InputBindingHelper.IsBindingPressed(GameSettings.CameraPanBinding))
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

            // In focal point mode, handle scroll differently
            if (useFocalPointMode)
            {
                Keyboard keyboard = Keyboard.current;
                bool shiftHeld = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

                if (shiftHeld)
                {
                    // Shift+Scroll: zoom along focal axis using logarithmic scale
                    // Scale scroll input by 10 and apply on log scale for smooth zoom feel
                    // Positive scroll (toward user) = zoom in (decrease offset, move closer)
                    // Negative scroll (away from user) = zoom out (increase offset, move further)
                    float scaledScroll = scroll * 10f * focalZoomSpeed * 0.01f;

                    // Use logarithmic scaling: convert current offset to log space, modify, convert back
                    // Add 1 to avoid log(0), so offset 0 maps to log(1)=0
                    float logOffset = Mathf.Log(focalZoomOffset + 1f);
                    logOffset -= scaledScroll;
                    logOffset = Mathf.Clamp(logOffset, 0f, Mathf.Log(maxFocalZoomDistance + 1f));
                    focalZoomOffset = Mathf.Exp(logOffset) - 1f;
                    focalZoomOffset = Mathf.Clamp(focalZoomOffset, 0f, maxFocalZoomDistance);
                }
                else
                {
                    // Normal scroll: change viewing angle
                    // Positive scroll = increase angle (move toward top-down)
                    // Negative scroll = decrease angle (move toward level)
                    focalViewAngle += scroll * angleChangeSpeed;
                    focalViewAngle = Mathf.Clamp(focalViewAngle, 40f, 85f); // 40°-85° range
                }
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

        /// <summary>
        /// Updates smooth movement of the focal point toward a target position.
        /// Uses unscaled delta time so it works during pause (Time.timeScale = 0).
        /// </summary>
        private void UpdateFocalPointSmoothMovement()
        {
            if (!isFocalPointMoving || focalPointTransform == null) return;

            Vector3 currentPos = focalPointTransform.position;
            Vector3 targetPos = focalPointMoveTarget;

            // Use MoveTowards for consistent speed
            // IMPORTANT: Use unscaledDeltaTime so movement works during pause
            Vector3 newPos = Vector3.MoveTowards(currentPos, targetPos, focalPointMoveSpeed * Time.unscaledDeltaTime);
            focalPointTransform.position = newPos;
            _focusPoint = newPos;

            // Check if we've arrived
            float distSq = Vector3.SqrMagnitude(newPos - targetPos);
            if (distSq < 0.01f)
            {
                focalPointTransform.position = targetPos;
                _focusPoint = targetPos;
                isFocalPointMoving = false;
            }
        }

        private void InitializeFocalPoint()
        {
            if (focalPointInitialized)
            {
                return;
            }


            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (focalPointTransform == null)
            {
                GameObject focalPointObj = new GameObject("Focal Point");
                focalPointTransform = focalPointObj.transform;
            }

            Vector3 startPosition;
            Quaternion startRotation;

            bool heartReady = GameController.Instance != null && GameController.Instance.Heart != null;

            if (heartReady)
            {
                startPosition = GameController.Instance.Heart.transform.position;
                startPosition.z = 0f;

                Vector3 facingDirection = GetPathDirectionToHeart();

                if (facingDirection.sqrMagnitude < 0.0001f)
                {
                    facingDirection = Vector3.up;
                }

                Vector3 mazeUp = GetMazeUpDirection();

                startRotation = Quaternion.LookRotation(facingDirection, mazeUp);
            }
            else if (mazeGridBehaviour != null && mazeGridBehaviour.WorldSpaceMazeData != null)
            {
                startPosition = mazeGridBehaviour.HeartWorldPosition;
                startPosition.z = 0f;

                Vector3 mazeForward = mazeGridBehaviour.MazeUpDirection;
                if (mazeForward.sqrMagnitude < 0.0001f)
                {
                    mazeForward = Vector3.up;
                }

                startRotation = Quaternion.LookRotation(mazeForward, GetMazeUpDirection());
            }
            else
            {
                // Wait until the maze is generated or the heart exists so we can place the focal point correctly.
                return;
            }

            focalPointTransform.SetPositionAndRotation(startPosition, startRotation);

            // Add pulsing lime green glow to the focal point
            AddFocalPointGlow();

            _focusPoint = startPosition;
            focalPointInitialized = true;
        }

        private void AddFocalPointGlow()
        {
            if (focalPointTransform == null)
            {
                return;
            }

            // Check if glow already exists
            FocalPointGlow existingGlow = focalPointTransform.GetComponent<FocalPointGlow>();
            if (existingGlow != null)
            {
                return;
            }

            // Add the glow component
            FocalPointGlow glow = focalPointTransform.gameObject.AddComponent<FocalPointGlow>();
            glow.SetFocalPointTransform(focalPointTransform);
            glow.SetMazeGridBehaviour(mazeGridBehaviour);

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
                // Heart/Entrance not ready yet, try again next frame
                return;
            }

            Vector3 offset = -facingDirection.normalized * 3f + Vector3.back * 3f;

            Vector3 desiredPosition = focalPointTransform.position + offset;
            Vector3 worldUp = GetMazeUpDirection();
            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(focalPointTransform.position - desiredPosition, worldUp);

            Vector3 euler = transform.rotation.eulerAngles;
            euler.z = 0f;
            transform.rotation = Quaternion.Euler(euler);

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

            // Use direct direction from entrance to heart (world-space only)
            Vector3 direction = heart.transform.position - entrance.transform.position;
            direction.z = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector3.up;
            }

            return direction.normalized;
        }

        private void UpdateFocalPointCameraPosition()
        {
            if (!useFocalPointMode || focalPointTransform == null)
            {
                return;
            }

            // Get the focal point's rotation around the Z axis (maze up axis)
            // This represents which direction the focal point is "facing" in the XY plane
            float yawAngle = focalPointTransform.eulerAngles.z;

            // Convert yaw angle to a direction vector in the XY plane
            // In Unity, positive Z rotation goes counter-clockwise when viewed from above
            // We want the direction the focal point is facing
            float yawRad = yawAngle * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Cos(yawRad), Mathf.Sin(yawRad), 0f);


            // Use the actual maze up direction instead of hardcoded Vector3.forward
            Vector3 worldUp = GetMazeUpDirection();

            // Calculate camera position based on viewing angle
            // 45 degrees = angled view
            // 89 degrees = nearly above focal point (near top-down view)
            float angleRad = focalViewAngle * Mathf.Deg2Rad;
            // Apply focalZoomOffset to camera distance (0 = closest/start position, positive = further away)
            float cameraDistance = 5f + focalZoomOffset;
            float horizontalDistance = cameraDistance * Mathf.Cos(angleRad);
            float verticalOffset = cameraDistance * Mathf.Sin(angleRad);

            Vector3 offset = -forward * horizontalDistance + worldUp * verticalOffset;

            // Ensure camera is on the negative Z side (behind the XY plane)
            if (offset.z > 0)
            {
                offset.z = -offset.z;
            }

            Vector3 desiredPosition = focalPointTransform.position + offset;

            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(focalPointTransform.position - transform.position, worldUp);

            // Apply 180-degree Z-axis rotation correction
            Vector3 correctedEuler = transform.rotation.eulerAngles;
            correctedEuler.z += 180f;
            transform.rotation = Quaternion.Euler(correctedEuler);

            _focusPoint = focalPointTransform.position;

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

            // Return the maze up direction as-is for this game's -Z up coordinate system
            // Don't flip it - trust what MazeGridBehaviour tells us
            return mazeUp.normalized;
        }

        private void ConstrainFocalPointToMazeBounds()
        {
            if (mazeGridBehaviour == null || focalPointTransform == null)
            {
                return;
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            if (mazeData == null)
            {
                return;
            }

            Vector3 currentPos = focalPointTransform.position;
            Bounds bounds = mazeData.Bounds;

            // Add a small margin (1 tile worth) around the bounds
            float margin = mazeGridBehaviour.TileSize;
            float minX = bounds.min.x - margin;
            float maxX = bounds.max.x + margin;
            float minY = bounds.min.y - margin;
            float maxY = bounds.max.y + margin;

            // Clamp position to bounds
            currentPos.x = Mathf.Clamp(currentPos.x, minX, maxX);
            currentPos.y = Mathf.Clamp(currentPos.y, minY, maxY);

            focalPointTransform.position = currentPos;
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
        /// In focal point mode, moves the focal point transform to the target position.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos, bool instant = false, float lerpSpeed = 10f)
        {
            focusVisitor = null;
            Vector3 targetPosition = new Vector3(worldPos.x, worldPos.y, 0f);
            focusTargetPosition = targetPosition;

            // In focal point mode, move the focal point transform
            if (useFocalPointMode && focalPointTransform != null)
            {
                if (instant)
                {
                    focalPointTransform.position = targetPosition;
                    _focusPoint = targetPosition;
                    isFocalPointMoving = false;
                }
                else
                {
                    // Start smooth movement to target
                    focalPointMoveTarget = targetPosition;
                    focalPointMoveSpeed = Mathf.Max(lerpSpeed, 1f);
                    isFocalPointMoving = true;
                }
                isFocusing = false;
                return;
            }

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
        /// Sets the focal point rotation to look along a specific direction (in XY plane).
        /// In focal point mode, this rotates the focal point transform around the Z axis.
        /// </summary>
        /// <param name="direction">The direction to look along (in XY plane)</param>
        public void SetFocalPointDirection(Vector2 direction)
        {
            if (!useFocalPointMode || focalPointTransform == null) return;
            if (direction.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            focalPointTransform.rotation = Quaternion.Euler(0f, 0f, angle);
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
        public void FocusOnVisitor(VisitorControllerBase visitor, bool instant = false)
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

        /// <summary>
        /// Cycles to the next portal and focuses on it.
        /// If no portals exist, falls back to the original entrance.
        /// </summary>
        public void CycleToNextPortal()
        {
            // Lazy-init DynamicMazeGrowth reference
            if (dynamicMazeGrowth == null && mazeGridBehaviour != null)
            {
                dynamicMazeGrowth = mazeGridBehaviour.GetComponent<DynamicMazeGrowth>();
            }

            // Get current portal positions
            List<Vector3> portalPositions = null;
            if (dynamicMazeGrowth != null)
            {
                portalPositions = dynamicMazeGrowth.GetPortalPositions();
            }

            // If no portals, fall back to original entrance
            if (portalPositions == null || portalPositions.Count == 0)
            {
                FocusOnEntrance(true);
                currentPortalIndex = -1;
                return;
            }

            // Cycle to next portal
            currentPortalIndex = (currentPortalIndex + 1) % portalPositions.Count;
            Vector3 portalPos = portalPositions[currentPortalIndex];
            FocusOnPosition(portalPos, true);

            // Clear visitor tracking since we're focusing on a portal
            focusVisitor = null;
        }

        /// <summary>
        /// Cycles to the next visitor and focuses on them.
        /// If no visitors exist, does nothing.
        /// </summary>
        public void CycleToNextVisitor()
        {
            var allVisitors = VisitorRegistry.All;
            if (allVisitors == null || allVisitors.Count == 0)
            {
                currentVisitorIndex = -1;
                return;
            }

            // Cycle to next visitor
            currentVisitorIndex = (currentVisitorIndex + 1) % allVisitors.Count;

            // Get the visitor at the current index
            var visitor = allVisitors[currentVisitorIndex];
            if (visitor != null)
            {
                FocusOnPosition(visitor.transform.position, false);
                focusVisitor = visitor as VisitorControllerBase;
                focusLerpSpeed = 10f;
                isFocusing = true;
                trackingLogTimer = 0f;
                trackingVisitorLostLogged = false;
            }
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

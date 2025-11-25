using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Systems;
using FaeMaze.Visitors;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Controls an orthographic camera with keyboard and mouse input while keeping it within maze bounds.
    /// </summary>
    public class CameraController2D : MonoBehaviour
    {
        #region Serialized Fields

        [SerializeField]
        private float panSpeed = 10f;

        [SerializeField]
        private float zoomSpeed = 5f;

        [SerializeField]
        private float minOrthographicSize = 3f;

        [SerializeField]
        private float maxOrthographicSize = 20f;

        [SerializeField]
        private MazeGridBehaviour mazeGridBehaviour;

        #endregion

        #region Private Fields

        private Camera cam;
        private bool isDragging;
        private Vector3 lastMouseWorldPosition;
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
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            HandleFocusShortcuts();
            HandleKeyboardPan();
            HandleMouseDrag();
            HandleZoom();
            HandleFocusMovement();
            ClampToMazeBounds();
        }

        #endregion

        #region Input Handling

        private void HandleKeyboardPan()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
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

            Vector3 delta = new Vector3(movement.x, movement.y, 0f) * panSpeed * Time.deltaTime;
            transform.position += delta;
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
                    Debug.Log($"[CameraController2D] Shortcut 3: focusing last visitor '{lastVisitor.name}' at {lastVisitor.transform.position} (active={lastVisitor.isActiveAndEnabled}).");
                }
                else
                {
                    Debug.Log("[CameraController2D] Shortcut 3 pressed but no last visitor is stored.");
                }
            }
        }

        private void HandleMouseDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool dragButtonDown = mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;
            bool dragButtonHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            bool dragButtonUp = mouse.rightButton.wasReleasedThisFrame || mouse.middleButton.wasReleasedThisFrame;

            if (dragButtonDown)
            {
                isDragging = true;
                lastMouseWorldPosition = GetMouseWorldPosition();
            }

            if (dragButtonUp)
            {
                isDragging = false;
            }

            if (isDragging && dragButtonHeld)
            {
                Vector3 currentMouseWorldPosition = GetMouseWorldPosition();
                Vector3 delta = currentMouseWorldPosition - lastMouseWorldPosition;
                transform.position -= delta;
                lastMouseWorldPosition = currentMouseWorldPosition;
            }
        }

        private void HandleZoom()
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

            float zoomFactor = Mathf.Exp(scroll * zoomSpeed * Time.deltaTime);
            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize / zoomFactor,
                minOrthographicSize,
                maxOrthographicSize);
        }

        private Vector3 GetMouseWorldPosition()
        {
            Mouse mouse = Mouse.current;
            Vector2 mousePosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return cam.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, 0f));
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
                    transform.position.z);

                trackingLogTimer -= Time.deltaTime;
                if (trackingLogTimer <= 0f)
                {
                    Debug.Log($"[CameraController2D] Tracking visitor '{focusVisitor.name}': visitorPos={focusVisitor.transform.position}, cameraPos={transform.position}, target={focusTargetPosition}.");
                    trackingLogTimer = trackingLogInterval;
                }

                trackingVisitorLostLogged = false;
            }
            else if (!trackingVisitorLostLogged)
            {
                Debug.Log($"[CameraController2D] Visitor focus target lost; continuing toward last target {focusTargetPosition}.");
                trackingVisitorLostLogged = true;
            }

            Vector3 currentPosition = transform.position;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, focusTargetPosition, focusLerpSpeed * Time.deltaTime);
            transform.position = newPosition;

            if (focusVisitor == null && Vector3.SqrMagnitude(newPosition - focusTargetPosition) < 0.0001f)
            {
                isFocusing = false;
            }
        }

        #endregion

        #region Bounds Handling

        private void ClampToMazeBounds()
        {
            if (!TryGetMazeDimensions(out Vector3 origin, out float width, out float height))
            {
                return;
            }

            float verticalExtent = cam.orthographicSize;
            float horizontalExtent = cam.orthographicSize * cam.aspect;

            Vector3 position = transform.position;

            if (width >= horizontalExtent * 2f)
            {
                float minX = origin.x + horizontalExtent;
                float maxX = origin.x + width - horizontalExtent;
                position.x = Mathf.Clamp(position.x, minX, maxX);
            }
            else
            {
                position.x = origin.x + width / 2f;
            }

            if (height >= verticalExtent * 2f)
            {
                float minY = origin.y + verticalExtent;
                float maxY = origin.y + height - verticalExtent;
                position.y = Mathf.Clamp(position.y, minY, maxY);
            }
            else
            {
                position.y = origin.y + height / 2f;
            }

            transform.position = position;
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
            Vector3 targetPosition = new Vector3(worldPos.x, worldPos.y, transform.position.z);
            focusTargetPosition = targetPosition;

            if (instant)
            {
                transform.position = targetPosition;
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

            Debug.Log($"[CameraController2D] FocusOnVisitor {(instant ? "instant" : "smooth")} for '{visitor.name}' at {visitor.transform.position}.");
        }

        #endregion
    }
}

using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Camera
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

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("CameraController2D requires a Camera component on the same GameObject.");
            }
        }

        private void Update()
        {
            if (cam == null)
            {
                return;
            }

            HandleKeyboardPan();
            HandleMouseDrag();
            HandleZoom();
            ClampToMazeBounds();
        }

        #endregion

        #region Input Handling

        private void HandleKeyboardPan()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 delta = new Vector3(horizontal, vertical, 0f) * panSpeed * Time.deltaTime;
            transform.position += delta;
        }

        private void HandleMouseDrag()
        {
            bool dragButtonDown = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            bool dragButtonHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            bool dragButtonUp = Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2);

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
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            cam.orthographicSize -= scroll * zoomSpeed * Time.deltaTime;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
        }

        private Vector3 GetMouseWorldPosition()
        {
            return cam.ScreenToWorldPoint(Input.mousePosition);
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
    }
}

using UnityEngine;
using FaeMaze.Maze;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Manages camera focus targeting - casts a ray from screen center to determine
    /// the focused tile and provides targeting for heart powers and interactions.
    /// </summary>
    public class CameraFocusTargeting : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("The main camera (will auto-find if not set)")]
        private Camera mainCamera;

        [SerializeField]
        [Tooltip("Reference to the maze grid for tile lookups")]
        private MazeGridBehaviour mazeGrid;

        [Header("Visual Feedback")]
        [SerializeField]
        [Tooltip("Prefab for the focus highlight (instantiated at focused tile)")]
        private GameObject focusHighlightPrefab;

        [SerializeField]
        [Tooltip("Color for valid target highlight")]
        private Color validTargetColor = new Color(1f, 1f, 1f, 0.5f);

        [SerializeField]
        [Tooltip("Color for invalid target highlight")]
        private Color invalidTargetColor = new Color(1f, 0f, 0f, 0.3f);

        [Header("Raycast Settings")]
        [SerializeField]
        [Tooltip("Maximum raycast distance")]
        private float maxRaycastDistance = 1000f;

        [SerializeField]
        [Tooltip("Layer mask for raycasting (tiles)")]
        private LayerMask tileLayerMask = -1; // Everything by default

        #endregion

        #region Private Fields

        private GameObject focusHighlightInstance;
        private MeshRenderer focusHighlightRenderer;
        private Vector2Int? currentFocusedTile = null;
        private Vector3? currentFocusedWorldPosition = null;
        private bool isHighlightVisible = false;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently focused tile in grid coordinates (null if none).
        /// </summary>
        public Vector2Int? FocusedTile => currentFocusedTile;

        /// <summary>
        /// Gets the currently focused world position (null if none).
        /// </summary>
        public Vector3? FocusedWorldPosition => currentFocusedWorldPosition;

        /// <summary>
        /// Gets whether a tile is currently focused.
        /// </summary>
        public bool HasFocusedTile => currentFocusedTile.HasValue;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-find camera if not set
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Auto-find maze grid if not set
            if (mazeGrid == null)
            {
                mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            }

            // Create focus highlight if prefab is set
            if (focusHighlightPrefab != null)
            {
                focusHighlightInstance = Instantiate(focusHighlightPrefab);
                focusHighlightInstance.name = "FocusHighlight";
                focusHighlightRenderer = focusHighlightInstance.GetComponent<MeshRenderer>();
                focusHighlightInstance.SetActive(false);
            }
        }

        private void Update()
        {
            UpdateFocusedTile();
            UpdateHighlightVisuals();
        }

        private void OnDestroy()
        {
            if (focusHighlightInstance != null)
            {
                Destroy(focusHighlightInstance);
            }
        }

        #endregion

        #region Focus Detection

        /// <summary>
        /// Updates the currently focused tile by raycasting from screen center.
        /// </summary>
        private void UpdateFocusedTile()
        {
            if (mainCamera == null)
            {
                currentFocusedTile = null;
                currentFocusedWorldPosition = null;
                return;
            }

            // Get screen center
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            // Cast ray from screen center
            Ray ray = mainCamera.ScreenPointToRay(screenCenter);

            // Raycast to find tile
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, tileLayerMask))
            {
                // Hit something - check if it's on the maze grid
                Vector3 hitWorldPos = hit.point;
                hitWorldPos.z = 0f; // Ensure Z=0 for 2D maze plane

                if (mazeGrid != null && mazeGrid.WorldToGrid(hitWorldPos, out int gridX, out int gridY))
                {
                    currentFocusedTile = new Vector2Int(gridX, gridY);
                    currentFocusedWorldPosition = mazeGrid.GridToWorld(gridX, gridY);
                    isHighlightVisible = true;
                    return;
                }
            }

            // No valid tile found
            currentFocusedTile = null;
            currentFocusedWorldPosition = null;
            isHighlightVisible = false;
        }

        /// <summary>
        /// Updates the visual highlight to show the focused tile.
        /// </summary>
        private void UpdateHighlightVisuals()
        {
            if (focusHighlightInstance == null)
            {
                return;
            }

            if (isHighlightVisible && currentFocusedWorldPosition.HasValue)
            {
                // Show highlight at focused tile
                focusHighlightInstance.SetActive(true);
                Vector3 highlightPos = currentFocusedWorldPosition.Value;
                highlightPos.z = -0.1f; // Slightly in front of tiles
                focusHighlightInstance.transform.position = highlightPos;

                // Update color based on validity (can override in future for power targeting)
                if (focusHighlightRenderer != null)
                {
                    // Default to valid color (other systems can change this)
                    focusHighlightRenderer.material.color = validTargetColor;
                }
            }
            else
            {
                // Hide highlight when no tile is focused
                focusHighlightInstance.SetActive(false);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the highlight color (useful for showing valid/invalid targeting).
        /// </summary>
        public void SetHighlightColor(Color color)
        {
            if (focusHighlightRenderer != null)
            {
                focusHighlightRenderer.material.color = color;
            }
        }

        /// <summary>
        /// Temporarily shows the highlight as valid (green).
        /// </summary>
        public void ShowValidTarget()
        {
            SetHighlightColor(validTargetColor);
        }

        /// <summary>
        /// Temporarily shows the highlight as invalid (red).
        /// </summary>
        public void ShowInvalidTarget()
        {
            SetHighlightColor(invalidTargetColor);
        }

        /// <summary>
        /// Gets the focused tile and world position. Returns true if a tile is focused.
        /// </summary>
        public bool TryGetFocusedTile(out Vector2Int gridPos, out Vector3 worldPos)
        {
            if (currentFocusedTile.HasValue && currentFocusedWorldPosition.HasValue)
            {
                gridPos = currentFocusedTile.Value;
                worldPos = currentFocusedWorldPosition.Value;
                return true;
            }

            gridPos = Vector2Int.zero;
            worldPos = Vector3.zero;
            return false;
        }

        #endregion
    }
}

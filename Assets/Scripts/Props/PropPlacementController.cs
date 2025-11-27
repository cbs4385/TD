using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Audio;

namespace FaeMaze.Props
{
    /// <summary>
    /// Handles player placement of props on the maze grid by spending essence.
    /// Tracks tile occupancy to prevent duplicate props on the same tile.
    /// Supports multiple placeable item types (FaeLantern, FairyRing, etc.).
    /// </summary>
    public class PropPlacementController : MonoBehaviour
    {
        #region Data Structures

        /// <summary>
        /// Build mode state for prop placement.
        /// </summary>
        public enum BuildModeState
        {
            Inactive,
            Active
        }

        /// <summary>
        /// Defines a placeable item type with its properties.
        /// </summary>
        [System.Serializable]
        public class PlaceableItem
        {
            [Tooltip("Unique identifier for this item (e.g., 'FaeLantern', 'FairyRing')")]
            public string id;

            [Tooltip("Display name shown in UI")]
            public string displayName;

            [Tooltip("Prefab to instantiate when placed")]
            public GameObject prefab;

            [Tooltip("Essence cost to place this item")]
            public int essenceCost;

            [Header("Preview Settings")]
            [Tooltip("Sprite to use for preview (optional; falls back to prefab's sprite)")]
            public Sprite previewSprite;

            [Tooltip("Color for the preview ghost (semi-transparent recommended)")]
            public Color previewColor = new Color(1f, 1f, 1f, 0.5f);
        }

        #endregion

        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the maze grid behaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Placeable Items")]
        [SerializeField]
        [Tooltip("List of all placeable item types")]
        private List<PlaceableItem> placeableItems = new List<PlaceableItem>();

        [Header("Preview")]
        [SerializeField]
        [Tooltip("Parent transform for preview objects (optional; will create if null)")]
        private Transform previewRoot;

        [SerializeField]
        [Tooltip("Color tint for invalid placement (e.g., non-walkable tiles)")]
        private Color invalidPlacementColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("Build Mode")]
        [SerializeField]
        [Tooltip("Current build mode state")]
        private BuildModeState buildModeState = BuildModeState.Active;

        [Header("Cursor")]
        [SerializeField]
        [Tooltip("Cursor texture to use in build mode")]
        private Texture2D buildCursorTexture;

        [SerializeField]
        [Tooltip("Hotspot for build cursor (usually center or top-left)")]
        private Vector2 buildCursorHotspot = Vector2.zero;

        [SerializeField]
        [Tooltip("Default cursor texture (null = system default)")]
        private Texture2D defaultCursorTexture;

        [SerializeField]
        [Tooltip("Hotspot for default cursor")]
        private Vector2 defaultCursorHotspot = Vector2.zero;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private Dictionary<Vector2Int, GameObject> occupiedTiles;
        private PlaceableItem currentSelection;

        // Preview fields
        private GameObject previewInstance;
        private SpriteRenderer previewSpriteRenderer;
        private LineRenderer previewRadiusRenderer;
        private float previewRadius;
        private bool isPreviewValid;

        #endregion

        #region Properties

        /// <summary>Gets the currently selected placeable item</summary>
        public PlaceableItem CurrentSelection => currentSelection;

        /// <summary>Gets the list of all placeable items</summary>
        public List<PlaceableItem> PlaceableItems => placeableItems;

        /// <summary>Gets whether the controller is currently in build mode</summary>
        public bool IsInBuildMode => buildModeState == BuildModeState.Active;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Get main camera
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("PropPlacementController: Main camera not found!");
            }

            // Initialize occupancy tracking
            occupiedTiles = new Dictionary<Vector2Int, GameObject>();

            // Validate references
            if (mazeGridBehaviour == null)
            {
                Debug.LogError("PropPlacementController: MazeGridBehaviour is not assigned!");
            }

            // Create preview root if not assigned
            if (previewRoot == null)
            {
                GameObject rootObj = new GameObject("PlacementPreviewRoot");
                previewRoot = rootObj.transform;
            }

            // Set default selection (first item in list for backward compatibility)
            if (placeableItems.Count > 0)
            {
                SelectItemById(placeableItems[0].id); // Use SelectItemById to trigger preview creation
            }
            else
            {
                Debug.LogWarning("PropPlacementController: No placeable items configured!");
            }

            // Set initial cursor
            UpdateCursor();
        }

        private void Update()
        {
            // Handle cancel keys (Escape or Right-click)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ExitBuildMode();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ExitBuildMode();
                return;
            }

            // Early exit if not in build mode
            if (buildModeState != BuildModeState.Active)
            {
                return;
            }

            // Update preview position
            UpdatePreviewPosition();

            // Check for left mouse button click using new Input System
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlaceProp();
            }
        }

        #endregion

        #region Selection Management

        /// <summary>
        /// Selects a placeable item by its ID.
        /// This will be called by UI in future tasks.
        /// </summary>
        /// <param name="id">Unique identifier of the item to select</param>
        public void SelectItemById(string id)
        {
            PlaceableItem item = placeableItems.FirstOrDefault(p => p.id == id);
            if (item != null)
            {
                currentSelection = item;
                Debug.Log($"PropPlacementController: Selected '{currentSelection.displayName}' (cost: {currentSelection.essenceCost})");

                // Create or update preview for new selection
                CreateOrUpdatePreview();
            }
            else
            {
                Debug.LogWarning($"PropPlacementController: No placeable item found with id '{id}'");
            }
        }

        /// <summary>
        /// Gets the currently selected placeable item.
        /// </summary>
        /// <returns>The current selection, or null if none selected</returns>
        public PlaceableItem GetCurrentSelection()
        {
            return currentSelection;
        }

        /// <summary>
        /// Gets a placeable item by its ID.
        /// </summary>
        /// <param name="id">Unique identifier of the item</param>
        /// <returns>The item if found, null otherwise</returns>
        public PlaceableItem GetItemById(string id)
        {
            return placeableItems.FirstOrDefault(p => p.id == id);
        }

        /// <summary>
        /// Gets all placeable items as a read-only list.
        /// Useful for UI to iterate through all available items.
        /// </summary>
        /// <returns>Read-only list of all placeable items</returns>
        public IReadOnlyList<PlaceableItem> GetAllPlaceableItems()
        {
            return placeableItems.AsReadOnly();
        }

        #endregion

        #region Placement Logic

        /// <summary>
        /// Attempts to place the currently selected prop at the mouse cursor position.
        /// </summary>
        private void TryPlaceProp()
        {
            // Early exit if no items configured - avoid spam warnings
            if (placeableItems.Count == 0)
            {
                return; // Already warned in Start()
            }

            // Validate preconditions
            if (currentSelection == null)
            {
                Debug.LogWarning("PropPlacementController: No item selected for placement!");
                return;
            }

            if (mainCamera == null || mazeGridBehaviour == null)
            {
                return;
            }

            if (currentSelection.prefab == null)
            {
                Debug.LogError($"PropPlacementController: Prefab not assigned for '{currentSelection.displayName}'!");
                return;
            }

            // Get mouse position using new Input System
            if (Mouse.current == null)
            {
                return;
            }

            // Get mouse position in world space
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorldPos.z = 0; // Ensure z is 0 for 2D

            // Convert world position to grid coordinates
            if (!mazeGridBehaviour.WorldToGrid(mouseWorldPos, out int gridX, out int gridY))
            {
                return;
            }

            Vector2Int gridPos = new Vector2Int(gridX, gridY);

            // Check if tile is already occupied
            if (occupiedTiles.ContainsKey(gridPos))
            {
                Debug.Log($"PropPlacementController: Tile ({gridX}, {gridY}) is already occupied");
                return;
            }

            // Get the maze node at this position
            MazeGrid grid = mazeGridBehaviour.Grid;
            if (grid == null)
            {
                Debug.LogError("PropPlacementController: MazeGrid is null!");
                return;
            }

            MazeGrid.MazeNode node = grid.GetNode(gridX, gridY);
            if (node == null)
            {
                Debug.LogWarning($"PropPlacementController: No node at ({gridX}, {gridY})");
                return;
            }

            // Check if tile is walkable
            if (!node.walkable)
            {
                Debug.Log($"PropPlacementController: Tile ({gridX}, {gridY}) is not walkable");
                return;
            }

            // Try to spend essence
            if (GameController.Instance == null)
            {
                Debug.LogError("PropPlacementController: GameController instance is null!");
                return;
            }

            if (!GameController.Instance.TrySpendEssence(currentSelection.essenceCost))
            {
                Debug.Log($"PropPlacementController: Not enough essence (need {currentSelection.essenceCost})");
                return;
            }

            // Place the prop
            PlaceProp(gridPos, currentSelection);
        }

        /// <summary>
        /// Places a prop at the specified grid position.
        /// </summary>
        /// <param name="gridPos">Grid coordinates where the prop should be placed</param>
        /// <param name="item">The placeable item to instantiate</param>
        private void PlaceProp(Vector2Int gridPos, PlaceableItem item)
        {
            // Get world position for placement
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

            // Instantiate the prop
            GameObject prop = Instantiate(item.prefab, worldPos, Quaternion.identity);
            prop.name = $"{item.id}_{gridPos.x}_{gridPos.y}";

            Debug.Log($"PropPlacementController: Placed '{item.displayName}' at ({gridPos.x}, {gridPos.y})");

            // Mark tile as occupied
            occupiedTiles[gridPos] = prop;

            // Play placement sound
            SoundManager.Instance?.PlayLanternPlaced();

            // The MazeAttractor or other components on the prop will automatically
            // apply their effects in their Start() methods
        }

        /// <summary>
        /// Removes a prop from occupancy tracking (useful if props are destroyed).
        /// </summary>
        /// <param name="gridPos">Grid position to free up</param>
        public void RemoveProp(Vector2Int gridPos)
        {
            if (occupiedTiles.ContainsKey(gridPos))
            {
                GameObject prop = occupiedTiles[gridPos];
                Debug.Log($"PropPlacementController: Removed prop from ({gridPos.x}, {gridPos.y})");
                occupiedTiles.Remove(gridPos);
            }
        }

        /// <summary>
        /// Checks if a tile is occupied by a prop.
        /// </summary>
        /// <param name="gridPos">Grid position to check</param>
        /// <returns>True if occupied, false otherwise</returns>
        public bool IsTileOccupied(Vector2Int gridPos)
        {
            return occupiedTiles.ContainsKey(gridPos);
        }

        /// <summary>
        /// Gets the prop GameObject at the specified grid position.
        /// </summary>
        /// <param name="gridPos">Grid position to query</param>
        /// <returns>The prop GameObject if found, null otherwise</returns>
        public GameObject GetPropAt(Vector2Int gridPos)
        {
            return occupiedTiles.TryGetValue(gridPos, out GameObject prop) ? prop : null;
        }

        #endregion

        #region Preview Management

        /// <summary>
        /// Creates or updates the preview instance for the currently selected item.
        /// </summary>
        private void CreateOrUpdatePreview()
        {
            if (currentSelection == null)
            {
                HidePreview();
                return;
            }

            // Create preview instance if it doesn't exist
            if (previewInstance == null)
            {
                previewInstance = new GameObject("PlacementPreview");
                previewInstance.transform.SetParent(previewRoot);

                // Add SpriteRenderer
                previewSpriteRenderer = previewInstance.AddComponent<SpriteRenderer>();
                previewSpriteRenderer.sortingOrder = 100; // High order to render on top

                // Create radius indicator as child
                GameObject radiusObj = new GameObject("RadiusIndicator");
                radiusObj.transform.SetParent(previewInstance.transform);
                radiusObj.transform.localPosition = Vector3.zero;

                previewRadiusRenderer = radiusObj.AddComponent<LineRenderer>();
                ConfigureRadiusRenderer();
            }

            // Determine sprite to use
            Sprite sprite = currentSelection.previewSprite;
            if (sprite == null && currentSelection.prefab != null)
            {
                // Try to get sprite from prefab's SpriteRenderer
                SpriteRenderer prefabSpriteRenderer = currentSelection.prefab.GetComponent<SpriteRenderer>();
                if (prefabSpriteRenderer != null)
                {
                    sprite = prefabSpriteRenderer.sprite;
                }
            }

            if (previewSpriteRenderer != null)
            {
                previewSpriteRenderer.sprite = sprite;
                previewSpriteRenderer.color = currentSelection.previewColor;
            }

            // Determine radius from MazeAttractor if present
            previewRadius = 0f;
            if (currentSelection.prefab != null)
            {
                MazeAttractor attractor = currentSelection.prefab.GetComponent<MazeAttractor>();
                if (attractor != null)
                {
                    previewRadius = attractor.Radius;
                }
            }

            UpdateRadiusVisualization();
        }

        /// <summary>
        /// Configures the LineRenderer for the radius indicator.
        /// </summary>
        private void ConfigureRadiusRenderer()
        {
            if (previewRadiusRenderer == null) return;

            previewRadiusRenderer.loop = true;
            previewRadiusRenderer.useWorldSpace = false;
            previewRadiusRenderer.startWidth = 0.05f;
            previewRadiusRenderer.endWidth = 0.05f;
            previewRadiusRenderer.material = new Material(Shader.Find("Sprites/Default"));
            previewRadiusRenderer.startColor = new Color(1f, 1f, 1f, 0.3f);
            previewRadiusRenderer.endColor = new Color(1f, 1f, 1f, 0.3f);
            previewRadiusRenderer.sortingOrder = 99;
        }

        /// <summary>
        /// Updates the radius visualization circle.
        /// </summary>
        private void UpdateRadiusVisualization()
        {
            if (previewRadiusRenderer == null || previewRadius <= 0f)
            {
                if (previewRadiusRenderer != null)
                {
                    previewRadiusRenderer.enabled = false;
                }
                return;
            }

            previewRadiusRenderer.enabled = true;

            // Create circle points
            int segments = 32;
            previewRadiusRenderer.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * previewRadius;
                float y = Mathf.Sin(angle) * previewRadius;
                previewRadiusRenderer.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        /// <summary>
        /// Updates the preview position to follow the mouse cursor.
        /// </summary>
        private void UpdatePreviewPosition()
        {
            // Hide preview if no selection or mouse is over UI
            if (currentSelection == null || previewInstance == null)
            {
                HidePreview();
                return;
            }

            // Check if mouse is over UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HidePreview();
                return;
            }

            // Ensure we have required components
            if (mainCamera == null || mazeGridBehaviour == null || Mouse.current == null)
            {
                HidePreview();
                return;
            }

            // Get mouse position in world space
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            mouseWorldPos.z = 0f;

            // Convert to grid coordinates
            if (!mazeGridBehaviour.WorldToGrid(mouseWorldPos, out int gridX, out int gridY))
            {
                HidePreview();
                return;
            }

            Vector2Int gridPos = new Vector2Int(gridX, gridY);

            // Check if tile is valid for placement
            bool isValid = IsTileValidForPlacement(gridPos);
            isPreviewValid = isValid;

            // Position preview at grid cell
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);
            previewInstance.transform.position = targetWorldPos;

            // Show preview
            if (!previewInstance.activeSelf)
            {
                previewInstance.SetActive(true);
            }

            // Update color based on validity
            if (previewSpriteRenderer != null)
            {
                previewSpriteRenderer.color = isValid ? currentSelection.previewColor : invalidPlacementColor;
            }

            // Update radius indicator color
            if (previewRadiusRenderer != null)
            {
                Color radiusColor = isValid ? new Color(1f, 1f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f, 0.3f);
                previewRadiusRenderer.startColor = radiusColor;
                previewRadiusRenderer.endColor = radiusColor;
            }
        }

        /// <summary>
        /// Checks if a tile is valid for placement.
        /// </summary>
        private bool IsTileValidForPlacement(Vector2Int gridPos)
        {
            // Check if tile is occupied
            if (occupiedTiles.ContainsKey(gridPos))
            {
                return false;
            }

            // Check if tile is walkable
            if (mazeGridBehaviour.Grid == null)
            {
                return false;
            }

            MazeGrid.MazeNode node = mazeGridBehaviour.Grid.GetNode(gridPos.x, gridPos.y);
            if (node == null || !node.walkable)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Hides the preview instance.
        /// </summary>
        private void HidePreview()
        {
            if (previewInstance != null && previewInstance.activeSelf)
            {
                previewInstance.SetActive(false);
            }
        }

        #endregion

        #region Build Mode Management

        /// <summary>
        /// Enters build mode, enabling placement and showing custom cursor.
        /// </summary>
        public void EnterBuildMode()
        {
            buildModeState = BuildModeState.Active;
            UpdateCursor();
            Debug.Log("PropPlacementController: Entered build mode");
        }

        /// <summary>
        /// Exits build mode, clearing selection and restoring default cursor.
        /// </summary>
        public void ExitBuildMode()
        {
            buildModeState = BuildModeState.Inactive;
            ClearSelection();
            UpdateCursor();
            Debug.Log("PropPlacementController: Exited build mode");
        }

        /// <summary>
        /// Clears the current selection and hides the preview.
        /// </summary>
        public void ClearSelection()
        {
            currentSelection = null;
            HidePreview();
        }

        /// <summary>
        /// Updates the cursor based on build mode state.
        /// Handles null textures gracefully by using system default.
        /// </summary>
        private void UpdateCursor()
        {
            if (buildModeState == BuildModeState.Active && buildCursorTexture != null)
            {
                Cursor.SetCursor(buildCursorTexture, buildCursorHotspot, CursorMode.Auto);
            }
            else
            {
                // Set to default cursor (null = system default)
                Cursor.SetCursor(defaultCursorTexture, defaultCursorHotspot, CursorMode.Auto);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (mazeGridBehaviour == null || occupiedTiles == null)
                return;

            // Draw occupied tiles
            Gizmos.color = Color.red;
            foreach (var kvp in occupiedTiles)
            {
                Vector3 worldPos = mazeGridBehaviour.GridToWorld(kvp.Key.x, kvp.Key.y);
                Gizmos.DrawWireSphere(worldPos, 0.3f);
            }

            // Draw current selection info
            if (currentSelection != null)
            {
                Gizmos.color = Color.cyan;
                // Could draw selection indicator near mouse position in future
            }
        }

        #endregion
    }
}

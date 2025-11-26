using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
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

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private Dictionary<Vector2Int, GameObject> occupiedTiles;
        private PlaceableItem currentSelection;

        #endregion

        #region Properties

        /// <summary>Gets the currently selected placeable item</summary>
        public PlaceableItem CurrentSelection => currentSelection;

        /// <summary>Gets the list of all placeable items</summary>
        public List<PlaceableItem> PlaceableItems => placeableItems;

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

            // Set default selection (first item in list for backward compatibility)
            if (placeableItems.Count > 0)
            {
                currentSelection = placeableItems[0];
                Debug.Log($"PropPlacementController: Default selection set to '{currentSelection.displayName}'");
            }
            else
            {
                Debug.LogWarning("PropPlacementController: No placeable items configured!");
            }
        }

        private void Update()
        {
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

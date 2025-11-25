using System.Collections.Generic;
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
    /// </summary>
    public class PropPlacementController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the maze grid behaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("The FaeLantern prefab to place")]
        private GameObject faeLanternPrefab;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Essence cost to place a FaeLantern")]
        private int faeLanternCost = 20;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private Dictionary<Vector2Int, GameObject> occupiedTiles;

        #endregion

        #region Properties

        /// <summary>Gets the cost to place a FaeLantern</summary>
        public int FaeLanternCost => faeLanternCost;

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

            if (faeLanternPrefab == null)
            {
                Debug.LogError("PropPlacementController: FaeLantern prefab is not assigned!");
            }

        }

        private void Update()
        {
            // Check for left mouse button click using new Input System
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryPlaceLantern();
            }
        }

        #endregion

        #region Placement Logic

        /// <summary>
        /// Attempts to place a FaeLantern at the mouse cursor position.
        /// </summary>
        private void TryPlaceLantern()
        {
            if (mainCamera == null || mazeGridBehaviour == null || faeLanternPrefab == null)
            {
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
                return;
            }

            // Try to spend essence
            if (GameController.Instance == null)
            {
                Debug.LogError("PropPlacementController: GameController instance is null!");
                return;
            }

            if (!GameController.Instance.TrySpendEssence(faeLanternCost))
            {
                return;
            }

            // Place the lantern
            PlaceLantern(gridPos);
        }

        /// <summary>
        /// Places a FaeLantern at the specified grid position.
        /// </summary>
        /// <param name="gridPos">Grid coordinates where the lantern should be placed</param>
        private void PlaceLantern(Vector2Int gridPos)
        {
            // Get world position for placement
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

            // Instantiate the lantern
            GameObject lantern = Instantiate(faeLanternPrefab, worldPos, Quaternion.identity);
            lantern.name = $"FaeLantern_{gridPos.x}_{gridPos.y}";

            SoundManager.Instance?.PlayLanternPlaced();

            // Mark tile as occupied
            occupiedTiles[gridPos] = lantern;

            SoundManager.Instance?.PlayLanternPlaced();

            // The MazeAttractor component on the lantern will automatically apply attraction in its Start() method
        }

        /// <summary>
        /// Removes a prop from occupancy tracking (useful if props are destroyed).
        /// </summary>
        /// <param name="gridPos">Grid position to free up</param>
        public void RemoveProp(Vector2Int gridPos)
        {
            if (occupiedTiles.ContainsKey(gridPos))
            {
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
        }

        #endregion
    }
}

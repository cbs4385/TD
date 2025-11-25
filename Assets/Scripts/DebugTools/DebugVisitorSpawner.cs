using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Visitors;

namespace FaeMaze.DebugTools
{
    /// <summary>
    /// Debug utility for spawning visitors at the entrance.
    /// Press Space to spawn a visitor that walks in a straight line to the heart.
    /// </summary>
    public class DebugVisitorSpawner : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("The maze entrance where visitors spawn")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("The heart of the maze (destination)")]
        private HeartOfTheMaze heart;

        [Header("Spawn Settings")]
        [SerializeField]
        [Tooltip("Key to press to spawn a visitor")]
        private KeyCode spawnKey = KeyCode.Space;

        [SerializeField]
        [Tooltip("Offset from entrance to spawn visitor (to avoid overlapping)")]
        private Vector3 spawnOffset = Vector3.zero;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private int visitorSpawnCount = 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("DebugVisitorSpawner: Could not find MazeGridBehaviour in scene!");
            }

            ValidateReferences();
        }

        private void Update()
        {
            // Check for spawn key press using new Input System
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                SpawnVisitor();
            }
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawns a visitor at the entrance with an A* path to the heart.
        /// </summary>
        public void SpawnVisitor()
        {
            if (!ValidateReferences())
            {
                Debug.LogError("DebugVisitorSpawner: Cannot spawn visitor - missing references!");
                return;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("DebugVisitorSpawner: MazeGridBehaviour is null!");
                return;
            }

            // Get grid positions
            Vector2Int entrancePos = entrance.GridPosition;
            Vector2Int heartPos = heart.GridPosition;

            Debug.Log($"Spawning visitor: Entrance at {entrancePos}, Heart at {heartPos}");

            // Find path using A* through GameController
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(entrancePos, heartPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                Debug.LogWarning($"DebugVisitorSpawner: No path found from {entrancePos} to {heartPos}!");
                return;
            }

            Debug.Log($"A* pathfinding found path with {pathNodes.Count} nodes");

            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(entrancePos.x, entrancePos.y) + spawnOffset;

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);
            visitor.gameObject.name = $"Visitor_{visitorSpawnCount++}";

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path (using MazeNode list directly)
            visitor.SetPath(pathNodes);

            Debug.Log($"Visitor spawned at {spawnWorldPos} with {pathNodes.Count} waypoints");
        }

        /// <summary>
        /// Creates a straight-line path between two grid positions.
        /// Uses Bresenham's line algorithm for grid-based line drawing.
        /// </summary>
        private List<Vector2Int> CreateStraightLinePath(Vector2Int start, Vector2Int end)
        {
            List<Vector2Int> path = new List<Vector2Int>();

            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);

            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;

            int err = dx - dy;

            int x = x0;
            int y = y0;

            // Add points along the line
            while (true)
            {
                path.Add(new Vector2Int(x, y));

                // Check if we've reached the end
                if (x == x1 && y == y1)
                {
                    break;
                }

                int e2 = 2 * err;

                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            Debug.Log($"Created straight-line path with {path.Count} points from {start} to {end}");
            return path;
        }

        #endregion

        #region Validation

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (visitorPrefab == null)
            {
                Debug.LogError("DebugVisitorSpawner: Visitor prefab is not assigned!");
                isValid = false;
            }

            if (entrance == null)
            {
                Debug.LogError("DebugVisitorSpawner: Entrance is not assigned!");
                isValid = false;
            }

            if (heart == null)
            {
                Debug.LogError("DebugVisitorSpawner: Heart is not assigned!");
                isValid = false;
            }

            return isValid;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (entrance != null && heart != null && mazeGridBehaviour != null)
            {
                // Draw line from entrance to heart
                Vector3 entranceWorld = mazeGridBehaviour.GridToWorld(entrance.GridPosition.x, entrance.GridPosition.y);
                Vector3 heartWorld = mazeGridBehaviour.GridToWorld(heart.GridPosition.x, heart.GridPosition.y);

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(entranceWorld, heartWorld);

                // Draw spawn position
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(entranceWorld + spawnOffset, 0.3f);
            }
        }

        #endregion
    }
}

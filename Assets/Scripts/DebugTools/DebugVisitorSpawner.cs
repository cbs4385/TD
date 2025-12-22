using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Audio;
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
                enabled = false;
                return;
            }

            ValidateReferences();
        }

        private void Update()
        {
            // Check for Space key press using new Input System
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
                return;
            }

            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Get grid positions
            Vector2Int entrancePos = entrance.GridPosition;
            Vector2Int heartPos = heart.GridPosition;


            // Find path using A* through GameController
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(entrancePos, heartPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                return;
            }


            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(entrancePos.x, entrancePos.y) + spawnOffset;

            // Instantiate visitor (rotated 180 degrees on z-axis)
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
            visitor.gameObject.name = $"Visitor_{visitorSpawnCount++}";

            SoundManager.Instance?.PlayVisitorSpawn();

            GameController.Instance.SetLastSpawnedVisitor(visitor);

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path (using MazeNode list directly)
            visitor.SetPath(pathNodes);

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

            return path;
        }

        #endregion

        #region Validation

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (visitorPrefab == null)
            {
                isValid = false;
            }

            if (entrance == null)
            {
                isValid = false;
            }

            if (heart == null)
            {
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

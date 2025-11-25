using System.Linq;
using UnityEngine;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// MonoBehaviour wrapper for the MazeGrid data structure.
    /// Handles grid initialization from text file, world-space conversions, and registration with GameController.
    ///
    /// Coordinate system:
    /// - X increases to the right
    /// - Y increases downward (lines[0] is Y=0, top of maze)
    /// - Grid origin (0,0) is at top-left of the maze
    /// </summary>
    public class MazeGridBehaviour : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Maze File")]
        [SerializeField]
        [Tooltip("Text file containing the maze layout")]
        private TextAsset mazeFile;

        [Header("References")]
        [SerializeField]
        [Tooltip("Transform acting as the origin point for world-to-grid conversions")]
        private Transform mazeOrigin;

        [SerializeField]
        [Tooltip("Reference to the MazeEntrance component")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("Reference to the HeartOfTheMaze component")]
        private HeartOfTheMaze heart;

        #endregion

        #region Private Fields

        private MazeGrid grid;
        private int width;
        private int height;
        private Vector2Int entranceGridPos;
        private Vector2Int heartGridPos;

        #endregion

        #region Properties

        /// <summary>Gets the underlying MazeGrid data structure</summary>
        public MazeGrid Grid => grid;

        /// <summary>Gets the entrance grid position</summary>
        public Vector2Int EntranceGridPos => entranceGridPos;

        /// <summary>Gets the heart grid position</summary>
        public Vector2Int HeartGridPos => heartGridPos;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Validate references
            if (mazeOrigin == null)
            {
                Debug.LogError("MazeOrigin is not assigned in MazeGridBehaviour!");
                mazeOrigin = transform; // Fallback to self
            }

            // Initialize from file
            InitializeFromFile();
        }

        private void Start()
        {
            // Register with GameController (all Awake() calls are done by now)
            if (GameController.Instance != null)
            {
                GameController.Instance.RegisterMazeGrid(grid);
            }
            else
            {
                Debug.LogError("GameController instance not found! Cannot register MazeGrid.");
            }

            // Position the entrance and heart objects after everything is initialized
            PositionEntranceAndHeart();
        }

        #endregion

        #region Initialization

        private void InitializeFromFile()
        {
            // Validate maze file
            if (mazeFile == null)
            {
                Debug.LogError("MazeFile is not assigned in MazeGridBehaviour! Cannot initialize maze.");
                return;
            }


            // Parse the text file
            var lines = mazeFile.text
                .Replace("\r", string.Empty)
                .Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (lines.Length == 0)
            {
                Debug.LogError("Maze file is empty!");
                return;
            }

            // Determine dimensions
            height = lines.Length;
            width = lines.Max(line => line.Length);


            // Create the grid
            grid = new MazeGrid(width, height);

            // Track if we found entrance
            bool foundEntrance = false;

            // Parse each character
            for (int y = 0; y < height; y++)
            {
                string line = lines[y];
                for (int x = 0; x < line.Length; x++)
                {
                    char c = line[x];

                    switch (c)
                    {
                        case '.':
                            // Pathway - walkable
                            grid.SetWalkable(x, y, true);
                            break;

                        case '#':
                            // Wall - not walkable
                            grid.SetWalkable(x, y, false);
                            break;

                        case 'E':
                            // Entrance - walkable and mark position
                            grid.SetWalkable(x, y, true);
                            if (!foundEntrance)
                            {
                                entranceGridPos = new Vector2Int(x, y);
                                foundEntrance = true;
                            }
                            break;

                        case 'H':
                            // Heart marker (optional) - walkable
                            grid.SetWalkable(x, y, true);
                            break;

                        default:
                            // Unknown character - treat as wall and log warning
                            Debug.LogWarning($"Unknown character '{c}' at ({x}, {y}), treating as wall");
                            grid.SetWalkable(x, y, false);
                            break;
                    }
                }

                // Fill remaining cells in short lines with walls
                for (int x = line.Length; x < width; x++)
                {
                    grid.SetWalkable(x, y, false);
                }
            }

            // Validate entrance
            if (!foundEntrance)
            {
                Debug.LogError("No entrance ('E') found in maze file!");
                entranceGridPos = new Vector2Int(0, 0);
            }

            // Find heart position
            FindHeartPosition();

            // Log diagnostic info
        }

        private void FindHeartPosition()
        {
            // Start from approximate center
            int centerX = width / 2;
            int centerY = height / 2;


            // Check if center is walkable
            if (grid.GetNode(centerX, centerY)?.walkable == true)
            {
                heartGridPos = new Vector2Int(centerX, centerY);
                return;
            }

            // Search outward in expanding rings (BFS-style)
            for (int radius = 1; radius < Mathf.Max(width, height); radius++)
            {
                // Check points in a ring around center
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // Only check ring perimeter, not interior
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;

                        int checkX = centerX + dx;
                        int checkY = centerY + dy;

                        if (grid.InBounds(checkX, checkY))
                        {
                            var node = grid.GetNode(checkX, checkY);
                            if (node != null && node.walkable)
                            {
                                heartGridPos = new Vector2Int(checkX, checkY);
                                return;
                            }
                        }
                    }
                }
            }

            // Fallback - should never reach here if maze has any walkable tiles
            Debug.LogError("Could not find any walkable tile for heart position!");
            heartGridPos = entranceGridPos;
        }

        private void PositionEntranceAndHeart()
        {
            // Position entrance object
            if (entrance != null)
            {
                Vector3 entranceWorldPos = GridToWorld(entranceGridPos.x, entranceGridPos.y);
                entrance.transform.position = entranceWorldPos;
                entrance.SetGridPosition(entranceGridPos);
            }
            else
            {
                Debug.LogWarning("Entrance component not assigned in MazeGridBehaviour!");
            }

            // Position heart object
            if (heart != null)
            {
                Vector3 heartWorldPos = GridToWorld(heartGridPos.x, heartGridPos.y);
                heart.transform.position = heartWorldPos;
                heart.SetGridPosition(heartGridPos);
            }
            else
            {
                Debug.LogWarning("Heart component not assigned in MazeGridBehaviour!");
            }
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Converts grid coordinates to world position.
        /// </summary>
        /// <param name="x">Grid X coordinate</param>
        /// <param name="y">Grid Y coordinate</param>
        /// <returns>World position corresponding to the grid cell</returns>
        public Vector3 GridToWorld(int x, int y)
        {
            if (mazeOrigin == null)
            {
                Debug.LogWarning("MazeOrigin is null! Using Vector3.zero as origin.");
                return new Vector3(x, y, 0);
            }

            return mazeOrigin.position + new Vector3(x, y, 0);
        }

        /// <summary>
        /// Converts world position to grid coordinates.
        /// </summary>
        /// <param name="worldPos">World position to convert</param>
        /// <param name="x">Output grid X coordinate</param>
        /// <param name="y">Output grid Y coordinate</param>
        /// <returns>True if the position maps to a valid grid cell, false otherwise</returns>
        public bool WorldToGrid(Vector3 worldPos, out int x, out int y)
        {
            if (mazeOrigin == null)
            {
                Debug.LogWarning("MazeOrigin is null! Cannot convert world to grid.");
                x = 0;
                y = 0;
                return false;
            }

            // Calculate relative position from origin
            Vector3 localPos = worldPos - mazeOrigin.position;

            // Round to nearest integer coordinates
            x = Mathf.RoundToInt(localPos.x);
            y = Mathf.RoundToInt(localPos.y);

            // Check if in bounds
            return grid != null && grid.InBounds(x, y);
        }

        /// <summary>
        /// Converts world position to grid coordinates (alternative version using flooring).
        /// </summary>
        /// <param name="worldPos">World position to convert</param>
        /// <param name="x">Output grid X coordinate</param>
        /// <param name="y">Output grid Y coordinate</param>
        /// <returns>True if the position maps to a valid grid cell, false otherwise</returns>
        public bool WorldToGridFloor(Vector3 worldPos, out int x, out int y)
        {
            if (mazeOrigin == null)
            {
                Debug.LogWarning("MazeOrigin is null! Cannot convert world to grid.");
                x = 0;
                y = 0;
                return false;
            }

            // Calculate relative position from origin
            Vector3 localPos = worldPos - mazeOrigin.position;

            // Floor to integer coordinates
            x = Mathf.FloorToInt(localPos.x);
            y = Mathf.FloorToInt(localPos.y);

            // Check if in bounds
            return grid != null && grid.InBounds(x, y);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (grid == null || mazeOrigin == null)
                return;

            // Draw grid bounds
            Gizmos.color = Color.yellow;
            Vector3 center = mazeOrigin.position + new Vector3(width / 2f, height / 2f, 0);
            Vector3 size = new Vector3(width, height, 0);
            Gizmos.DrawWireCube(center, size);

            // Draw entrance position
            Gizmos.color = Color.green;
            Vector3 entrancePos = GridToWorld(entranceGridPos.x, entranceGridPos.y);
            Gizmos.DrawWireSphere(entrancePos, 0.5f);

            // Draw heart position
            Gizmos.color = Color.red;
            Vector3 heartPos = GridToWorld(heartGridPos.x, heartGridPos.y);
            Gizmos.DrawWireSphere(heartPos, 0.5f);
        }

        #endregion
    }
}

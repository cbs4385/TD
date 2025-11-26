using System.Collections.Generic;
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

        [Header("Debug Visualization")]
        [SerializeField]
        private bool drawGridGizmos = true;

        [SerializeField]
        private bool drawAttractionHeatmap = true;

        #endregion

        #region Private Fields

        private MazeGrid grid;
        private int width;
        private int height;
        private Vector2Int entranceGridPos;
        private Vector2Int heartGridPos;
        private Dictionary<char, Vector2Int> spawnPoints = new Dictionary<char, Vector2Int>();

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

            // Track if we found entrance and heart
            bool foundEntrance = false;
            bool foundHeart = false;

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
                            // Entrance - walkable and mark position (backwards compatibility)
                            grid.SetWalkable(x, y, true);
                            if (!foundEntrance)
                            {
                                entranceGridPos = new Vector2Int(x, y);
                                foundEntrance = true;
                            }
                            break;

                        case 'H':
                            // Heart marker - walkable and mark position
                            grid.SetWalkable(x, y, true);
                            heartGridPos = new Vector2Int(x, y);
                            foundHeart = true;
                            Debug.Log($"MazeGridBehaviour: Found heart marker 'H' at ({x}, {y})");
                            break;

                        case 'A':
                        case 'B':
                        case 'C':
                        case 'D':
                            // Spawn markers - walkable and store position
                            grid.SetWalkable(x, y, true);
                            if (!spawnPoints.ContainsKey(c))
                            {
                                spawnPoints[c] = new Vector2Int(x, y);
                                Debug.Log($"MazeGridBehaviour: Found spawn point '{c}' at ({x}, {y})");
                            }
                            else
                            {
                                Debug.LogWarning($"MazeGridBehaviour: Duplicate spawn marker '{c}' at ({x}, {y}), ignoring");
                            }
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

            // Validate entrance (only required for legacy system)
            if (!foundEntrance)
            {
                // If using spawn markers, entrance is not required
                if (spawnPoints.Count >= 2)
                {
                    Debug.Log("MazeGridBehaviour: No 'E' entrance marker found, using spawn marker system.");
                    entranceGridPos = new Vector2Int(0, 0); // Not used with spawn markers
                }
                else
                {
                    Debug.LogError("No entrance ('E') found in maze file! Required for legacy entrance/heart system.");
                    entranceGridPos = new Vector2Int(0, 0);
                }
            }

            // Find heart position (only if 'H' marker not found)
            if (!foundHeart)
            {
                Debug.LogWarning("No heart marker ('H') found in maze file. Using center fallback.");
                FindHeartPosition();
            }

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

        private void OnDrawGizmos()
        {
            if (!drawGridGizmos || grid == null || mazeOrigin == null)
            {
                return;
            }

            Color originalColor = Gizmos.color;

            float maxAttraction = 0f;
            if (drawAttractionHeatmap)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        var node = grid.GetNode(x, y);
                        if (node != null)
                        {
                            maxAttraction = Mathf.Max(maxAttraction, node.attraction);
                        }
                    }
                }

                if (Mathf.Approximately(maxAttraction, 0f))
                {
                    maxAttraction = 1f;
                }
            }

            Color walkableBaseColor = new Color(0.2f, 0.8f, 0.2f, 0.35f);
            Color blockedColor = new Color(0.6f, 0.1f, 0.1f, 0.4f);
            Vector3 cellSize = new Vector3(0.95f, 0.95f, 0.1f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var node = grid.GetNode(x, y);
                    if (node == null)
                    {
                        continue;
                    }

                    Vector3 cellCenter = GridToWorld(x, y);

                    if (node.walkable)
                    {
                        Color tileColor = walkableBaseColor;

                        if (drawAttractionHeatmap)
                        {
                            float t = Mathf.InverseLerp(0f, maxAttraction, node.attraction);
                            tileColor = Color.Lerp(walkableBaseColor, Color.cyan, t);
                        }

                        Gizmos.color = tileColor;
                        Gizmos.DrawCube(cellCenter, cellSize);
                    }
                    else
                    {
                        Gizmos.color = blockedColor;
                        Gizmos.DrawCube(cellCenter, cellSize);
                    }
                }
            }

            // Highlight entrance and heart positions
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            Gizmos.DrawWireCube(GridToWorld(entranceGridPos.x, entranceGridPos.y), Vector3.one * 1.1f);

            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(GridToWorld(heartGridPos.x, heartGridPos.y), Vector3.one * 1.1f);

            Gizmos.color = originalColor;
        }

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

        #region Debug Controls

        public void SetDrawGridGizmos(bool value)
        {
            drawGridGizmos = value;
        }

        public void SetDrawAttractionHeatmap(bool value)
        {
            drawAttractionHeatmap = value;
        }

        #endregion

        #region Spawn Point API

        /// <summary>
        /// Gets the grid position of a specific spawn point.
        /// </summary>
        /// <param name="spawnId">The spawn marker character (A, B, C, D)</param>
        /// <param name="position">Output position if found</param>
        /// <returns>True if spawn point exists, false otherwise</returns>
        public bool TryGetSpawnPoint(char spawnId, out Vector2Int position)
        {
            return spawnPoints.TryGetValue(spawnId, out position);
        }

        /// <summary>
        /// Gets the grid position of a specific spawn point.
        /// Throws exception if spawn point doesn't exist.
        /// </summary>
        /// <param name="spawnId">The spawn marker character (A, B, C, D)</param>
        /// <returns>Grid position of the spawn point</returns>
        public Vector2Int GetSpawnPoint(char spawnId)
        {
            if (!spawnPoints.ContainsKey(spawnId))
            {
                Debug.LogError($"MazeGridBehaviour: Spawn point '{spawnId}' not found!");
                return Vector2Int.zero;
            }
            return spawnPoints[spawnId];
        }

        /// <summary>
        /// Gets all spawn points as a read-only dictionary.
        /// </summary>
        /// <returns>Dictionary mapping spawn IDs to grid positions</returns>
        public IReadOnlyDictionary<char, Vector2Int> GetAllSpawnPoints()
        {
            return spawnPoints;
        }

        /// <summary>
        /// Gets a random spawn point from all available spawn markers.
        /// </summary>
        /// <param name="spawnId">Output spawn ID that was selected</param>
        /// <param name="position">Output grid position</param>
        /// <returns>True if at least one spawn point exists, false otherwise</returns>
        public bool TryGetRandomSpawnPoint(out char spawnId, out Vector2Int position)
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("MazeGridBehaviour: No spawn points available!");
                spawnId = '\0';
                position = Vector2Int.zero;
                return false;
            }

            // Get random spawn point
            var keys = new List<char>(spawnPoints.Keys);
            int randomIndex = Random.Range(0, keys.Count);
            spawnId = keys[randomIndex];
            position = spawnPoints[spawnId];
            return true;
        }

        /// <summary>
        /// Gets two different random spawn points for start and destination.
        /// </summary>
        /// <param name="startId">Output start spawn ID</param>
        /// <param name="startPos">Output start grid position</param>
        /// <param name="destId">Output destination spawn ID</param>
        /// <param name="destPos">Output destination grid position</param>
        /// <returns>True if at least two different spawn points exist, false otherwise</returns>
        public bool TryGetRandomSpawnPair(out char startId, out Vector2Int startPos, out char destId, out Vector2Int destPos)
        {
            if (spawnPoints.Count < 2)
            {
                Debug.LogWarning($"MazeGridBehaviour: Need at least 2 spawn points, found {spawnPoints.Count}");
                startId = '\0';
                startPos = Vector2Int.zero;
                destId = '\0';
                destPos = Vector2Int.zero;
                return false;
            }

            // Get list of spawn IDs
            var keys = new List<char>(spawnPoints.Keys);

            // Pick random start
            int startIndex = Random.Range(0, keys.Count);
            startId = keys[startIndex];
            startPos = spawnPoints[startId];

            // Pick random destination (different from start)
            int destIndex;
            do
            {
                destIndex = Random.Range(0, keys.Count);
            } while (destIndex == startIndex);

            destId = keys[destIndex];
            destPos = spawnPoints[destId];

            return true;
        }

        /// <summary>
        /// Gets the number of spawn points detected in the maze.
        /// </summary>
        public int GetSpawnPointCount()
        {
            return spawnPoints.Count;
        }

        #endregion
    }
}

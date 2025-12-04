using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Data structure representing a grid-based maze for pathfinding and attraction effects.
    /// This is a plain C# class, not a MonoBehaviour.
    /// </summary>
    public class MazeGrid
    {
        #region Nested Types

        /// <summary>
        /// Represents a single node/cell in the maze grid.
        /// </summary>
        public class MazeNode
        {
            /// <summary>X coordinate in grid space</summary>
            public int x;

            /// <summary>Y coordinate in grid space</summary>
            public int y;

            /// <summary>Whether this node can be walked through</summary>
            public bool walkable;

            /// <summary>Base movement cost for this node (default 1.0)</summary>
            public float baseCost;

            /// <summary>Attraction value applied by Fae props (default 0.0)</summary>
            public float attraction;

            /// <summary>Tile content symbol for rendering/debugging (e.g. '#', '.', ';', '~', 'H').</summary>
            public char symbol;

            /// <summary>Underlying terrain classification for this tile.</summary>
            public TileType terrain;

            /// <summary>Indicates this tile is the maze heart.</summary>
            public bool isHeart;

            public MazeNode(int x, int y)
            {
                this.x = x;
                this.y = y;
                this.walkable = true;
                this.baseCost = 1.0f;
                this.attraction = 0.0f;
                this.symbol = '#';
                this.terrain = TileType.TreeBramble;
                this.isHeart = false;
            }
        }

        #endregion

        #region Constants

        private const float MIN_MOVE_COST = 0.1f;

        #endregion

        #region Private Fields

        private readonly int width;
        private readonly int height;
        private readonly MazeNode[,] nodes;

        #endregion

        #region Properties

        /// <summary>Gets the width of the maze grid</summary>
        public int Width => width;

        /// <summary>Gets the height of the maze grid</summary>
        public int Height => height;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new maze grid with the specified dimensions.
        /// All nodes are initialized as walkable by default.
        /// </summary>
        /// <param name="width">Width of the grid</param>
        /// <param name="height">Height of the grid</param>
        public MazeGrid(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.nodes = new MazeNode[width, height];

            // Initialize all nodes
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    nodes[x, y] = new MazeNode(x, y);
                }
            }

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the node at the specified grid coordinates.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>The MazeNode at the specified position, or null if out of bounds</returns>
        public MazeNode GetNode(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return null;
            }

            return nodes[x, y];
        }

        /// <summary>
        /// Checks if the specified coordinates are within the grid bounds.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>True if the coordinates are within bounds</returns>
        public bool InBounds(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        /// <summary>
        /// Sets whether a node is walkable or blocked.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="walkable">True if walkable, false if blocked</param>
        public void SetWalkable(int x, int y, bool walkable)
        {
            MazeNode node = GetNode(x, y);
            if (node != null)
            {
                node.walkable = walkable;
            }
        }

        /// <summary>
        /// Adds attraction value to a node. Positive values make the node more attractive,
        /// reducing movement cost. Negative values increase movement cost.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="value">Attraction value to add</param>
        public void AddAttraction(int x, int y, float value)
        {
            MazeNode node = GetNode(x, y);
            if (node != null)
            {
                node.attraction += value;
            }
        }

        /// <summary>
        /// Gets the movement cost for a node, factoring in base cost and attraction.
        /// Movement cost = baseCost - attraction, clamped to a minimum value.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>The movement cost, or float.MaxValue if node is unwalkable or out of bounds</returns>
        public float GetMoveCost(int x, int y)
        {
            MazeNode node = GetNode(x, y);

            if (node == null || !node.walkable)
            {
                return float.MaxValue;
            }

            float cost = node.baseCost - node.attraction;
            return Mathf.Max(cost, MIN_MOVE_COST);
        }

        /// <summary>
        /// Resets all attraction values to zero across the entire grid.
        /// Useful for recalculating attraction from scratch.
        /// </summary>
        public void ClearAllAttraction()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    nodes[x, y].attraction = 0.0f;
                }
            }
        }

        /// <summary>
        /// Gets diagnostic information about the grid.
        /// </summary>
        /// <returns>String with grid statistics</returns>
        public string GetGridInfo()
        {
            int walkableCount = 0;
            int blockedCount = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (nodes[x, y].walkable)
                        walkableCount++;
                    else
                        blockedCount++;
                }
            }

            return $"MazeGrid [{width}x{height}]: {walkableCount} walkable, {blockedCount} blocked";
        }

        /// <summary>
        /// Performs a flood-fill BFS to find all walkable tiles reachable from an origin
        /// within a maximum number of steps, respecting walls and non-walkable tiles.
        /// </summary>
        /// <param name="originX">Starting X coordinate</param>
        /// <param name="originY">Starting Y coordinate</param>
        /// <param name="maxSteps">Maximum distance in grid steps</param>
        /// <returns>HashSet of reachable grid positions</returns>
        public HashSet<Vector2Int> FloodFillReachable(int originX, int originY, int maxSteps)
        {
            // Use the overload with radius = maxSteps for backward compatibility
            return FloodFillReachable(originX, originY, maxSteps, maxSteps);
        }

        /// <summary>
        /// Performs a flood-fill BFS to find all walkable tiles reachable from an origin.
        /// Stops when either the radius or step limit is reached.
        /// </summary>
        /// <param name="originX">Starting X coordinate</param>
        /// <param name="originY">Starting Y coordinate</param>
        /// <param name="radius">Maximum Manhattan distance from origin</param>
        /// <param name="maxFloodFillSteps">Maximum number of BFS steps</param>
        /// <returns>HashSet of reachable grid positions</returns>
        public HashSet<Vector2Int> FloodFillReachable(int originX, int originY, int radius, int maxFloodFillSteps)
        {
            HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();

            // Validate origin
            if (!InBounds(originX, originY))
            {
                return reachable;
            }

            var originNode = GetNode(originX, originY);
            if (originNode == null || !originNode.walkable)
            {
                return reachable;
            }

            // BFS queue: (position, step_count)
            Queue<(Vector2Int pos, int steps)> queue = new Queue<(Vector2Int, int)>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            Vector2Int origin = new Vector2Int(originX, originY);
            queue.Enqueue((origin, 0));
            visited.Add(origin);

            // 4-directional neighbors
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0)   // Left
            };

            while (queue.Count > 0)
            {
                var (currentPos, currentSteps) = queue.Dequeue();
                reachable.Add(currentPos);

                // Don't explore beyond max flood-fill steps
                if (currentSteps >= maxFloodFillSteps)
                {
                    continue;
                }

                // Explore neighbors
                foreach (var dir in directions)
                {
                    Vector2Int neighborPos = currentPos + dir;

                    // Skip if already visited
                    if (visited.Contains(neighborPos))
                        continue;

                    // Check bounds
                    if (!InBounds(neighborPos.x, neighborPos.y))
                        continue;

                    // Check walkability
                    var neighborNode = GetNode(neighborPos.x, neighborPos.y);
                    if (neighborNode == null || !neighborNode.walkable)
                        continue;

                    // Check Manhattan distance from origin (stop when radius is exceeded)
                    int manhattanDist = Mathf.Abs(neighborPos.x - originX) + Mathf.Abs(neighborPos.y - originY);
                    if (manhattanDist > radius)
                        continue;

                    // Add to queue
                    visited.Add(neighborPos);
                    queue.Enqueue((neighborPos, currentSteps + 1));
                }
            }

            return reachable;
        }

        #endregion
    }
}

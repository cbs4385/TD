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
        /// Represents a single node/cell in the maze grid with 3D support.
        /// </summary>
        public class MazeNode
        {
            /// <summary>X coordinate in grid space</summary>
            public int x;

            /// <summary>Y coordinate in grid space</summary>
            public int y;

            /// <summary>Z coordinate/layer in grid space (0 = ground level)</summary>
            public int z;

            /// <summary>Height of this tile in world units (for rendering and pathfinding)</summary>
            public float height;

            /// <summary>Whether this node can be walked through</summary>
            public bool walkable;

            /// <summary>Base movement cost for this node (default 1.0)</summary>
            public float baseCost;

            /// <summary>Movement speed multiplier based on terrain (default 1.0)</summary>
            public float speedMultiplier;

            /// <summary>Attraction value applied by Fae props (default 0.0)</summary>
            public float attraction;

            /// <summary>Tile content symbol for rendering/debugging (e.g. '#', '.', ';', '~', 'H').</summary>
            public char symbol;

            /// <summary>Underlying terrain classification for this tile.</summary>
            public TileType terrain;

            /// <summary>Indicates this tile is the maze heart.</summary>
            public bool isHeart;

            /// <summary>List of connected nodes for multi-level pathfinding (stairs, ramps, etc.)</summary>
            public List<MazeNode> customConnections;

            public MazeNode(int x, int y, int z = 0)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.height = 0f;
                this.walkable = true;
                this.baseCost = 1.0f;
                this.speedMultiplier = 1.0f;
                this.attraction = 0.0f;
                this.symbol = '#';
                this.terrain = TileType.TreeBramble;
                this.isHeart = false;
                this.customConnections = new List<MazeNode>();
            }

            /// <summary>
            /// Sets terrain type and automatically applies terrain-based properties.
            /// </summary>
            public void SetTerrain(TileType terrainType)
            {
                this.terrain = terrainType;
                TerrainProperties.TerrainData data = TerrainProperties.GetTerrainData(terrainType);
                this.walkable = data.walkable;
                this.baseCost = data.pathCost;
                this.speedMultiplier = data.speedMultiplier;
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
        /// Movement cost = baseCost - (attraction * attractionMultiplier), clamped to a minimum value.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="attractionMultiplier">Multiplier for attraction effect (1.0 = normal, -1.0 = inverted, 0 = ignore)</param>
        /// <returns>The movement cost, or float.MaxValue if node is unwalkable or out of bounds</returns>
        public float GetMoveCost(int x, int y, float attractionMultiplier = 1.0f)
        {
            MazeNode node = GetNode(x, y);

            if (node == null || !node.walkable)
            {
                return float.MaxValue;
            }

            float cost = node.baseCost - (node.attraction * attractionMultiplier);
            return Mathf.Max(cost, MIN_MOVE_COST);
        }

        /// <summary>
        /// Gets the movement speed multiplier for a node based on its terrain type.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>The speed multiplier (1.0 = normal, &lt;1.0 = slower, &gt;1.0 = faster), or 0 if unwalkable</returns>
        public float GetSpeedMultiplier(int x, int y)
        {
            MazeNode node = GetNode(x, y);

            if (node == null || !node.walkable)
            {
                return 0f;
            }

            return node.speedMultiplier;
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
        /// Adds a custom connection between two nodes for multi-level pathfinding.
        /// Useful for stairs, ramps, teleporters, or other non-standard connections.
        /// </summary>
        /// <param name="fromNode">Source node</param>
        /// <param name="toNode">Target node</param>
        /// <param name="bidirectional">If true, adds connection in both directions</param>
        public void AddCustomConnection(MazeNode fromNode, MazeNode toNode, bool bidirectional = true)
        {
            if (fromNode == null || toNode == null)
            {
                return;
            }

            if (!fromNode.customConnections.Contains(toNode))
            {
                fromNode.customConnections.Add(toNode);
            }

            if (bidirectional && !toNode.customConnections.Contains(fromNode))
            {
                toNode.customConnections.Add(fromNode);
            }
        }

        /// <summary>
        /// Gets all neighbors of a node including custom connections.
        /// Supports both 2D (4-directional) and 3D (with custom connections) pathfinding.
        /// </summary>
        /// <param name="node">The node to get neighbors for</param>
        /// <param name="includeCustomConnections">Whether to include custom connections (stairs, ramps, etc.)</param>
        /// <returns>List of neighbor nodes</returns>
        public List<MazeNode> GetNeighbors(MazeNode node, bool includeCustomConnections = true)
        {
            List<MazeNode> neighbors = new List<MazeNode>();

            if (node == null)
            {
                return neighbors;
            }

            // 4-directional neighbors (up, down, left, right)
            int[] dx = { 0, 0, -1, 1 };
            int[] dy = { -1, 1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int neighborX = node.x + dx[i];
                int neighborY = node.y + dy[i];

                if (InBounds(neighborX, neighborY))
                {
                    MazeNode neighbor = GetNode(neighborX, neighborY);
                    if (neighbor != null && neighbor.walkable)
                    {
                        neighbors.Add(neighbor);
                    }
                }
            }

            // Add custom connections (stairs, ramps, etc.)
            if (includeCustomConnections && node.customConnections != null)
            {
                foreach (var connection in node.customConnections)
                {
                    if (connection != null && connection.walkable && !neighbors.Contains(connection))
                    {
                        neighbors.Add(connection);
                    }
                }
            }

            return neighbors;
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
        /// Supports 3D pathfinding with custom connections.
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

            // BFS queue: (node, step_count)
            Queue<(MazeNode node, int steps)> queue = new Queue<(MazeNode, int)>();
            HashSet<MazeNode> visited = new HashSet<MazeNode>();

            queue.Enqueue((originNode, 0));
            visited.Add(originNode);

            while (queue.Count > 0)
            {
                var (currentNode, currentSteps) = queue.Dequeue();
                reachable.Add(new Vector2Int(currentNode.x, currentNode.y));

                // Don't explore beyond max flood-fill steps
                if (currentSteps >= maxFloodFillSteps)
                {
                    continue;
                }

                // Get neighbors including custom connections (stairs, ramps)
                List<MazeNode> neighbors = GetNeighbors(currentNode, true);

                foreach (var neighborNode in neighbors)
                {
                    // Skip if already visited
                    if (visited.Contains(neighborNode))
                        continue;

                    // Check Manhattan distance from origin (stop when radius is exceeded)
                    int manhattanDist = Mathf.Abs(neighborNode.x - originX) + Mathf.Abs(neighborNode.y - originY);
                    if (manhattanDist > radius)
                        continue;

                    // Add to queue
                    visited.Add(neighborNode);
                    queue.Enqueue((neighborNode, currentSteps + 1));
                }
            }

            return reachable;
        }

        #endregion
    }
}

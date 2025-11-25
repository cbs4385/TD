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

            public MazeNode(int x, int y)
            {
                this.x = x;
                this.y = y;
                this.walkable = true;
                this.baseCost = 1.0f;
                this.attraction = 0.0f;
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

            Debug.Log($"MazeGrid created: {width}x{height} ({width * height} nodes)");
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
                Debug.LogWarning($"Attempted to access node out of bounds: ({x}, {y})");
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

        #endregion
    }
}

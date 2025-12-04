using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Implements A* pathfinding over the MazeGrid.
    /// Calculates paths considering tile costs and attraction values.
    /// </summary>
    public class MazePathfinder
    {
        #region Private Classes

        /// <summary>
        /// Internal node class for A* pathfinding algorithm.
        /// </summary>
        private class PathNode
        {
            public int x;
            public int y;
            public float gCost; // Cost from start to this node
            public float hCost; // Heuristic cost from this node to end
            public float fCost => gCost + hCost; // Total cost
            public PathNode parent;

            public PathNode(int x, int y)
            {
                this.x = x;
                this.y = y;
                this.gCost = float.MaxValue;
                this.hCost = 0;
                this.parent = null;
            }
        }

        #endregion

        #region Private Fields

        private readonly MazeGrid grid;
        private readonly Dictionary<int, PathNode> allNodes; // Cache of PathNode objects
        private readonly List<PathNode> openSet;
        private readonly HashSet<int> openSetLookup;
        private readonly HashSet<int> closedSet;

        // Neighbor offsets (4-directional: up, right, down, left)
        private static readonly int[] dx = { 0, 1, 0, -1 };
        private static readonly int[] dy = { -1, 0, 1, 0 };

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new pathfinder for the given maze grid.
        /// </summary>
        /// <param name="grid">The maze grid to pathfind over</param>
        public MazePathfinder(MazeGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            this.grid = grid;
            this.allNodes = new Dictionary<int, PathNode>();
            this.openSet = new List<PathNode>();
            this.openSetLookup = new HashSet<int>();
            this.closedSet = new HashSet<int>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to find a path from start to end using A* algorithm.
        /// </summary>
        /// <param name="startX">Start X coordinate</param>
        /// <param name="startY">Start Y coordinate</param>
        /// <param name="endX">End X coordinate</param>
        /// <param name="endY">End Y coordinate</param>
        /// <param name="resultPath">Output list of MazeNodes forming the path</param>
        /// <returns>True if path was found, false otherwise</returns>
        public bool TryFindPath(int startX, int startY, int endX, int endY, List<MazeGrid.MazeNode> resultPath)
        {
            if (grid == null)
            {
                return false;
            }

            // Validate coordinates
            if (!grid.InBounds(startX, startY))
            {
                return false;
            }

            if (!grid.InBounds(endX, endY))
            {
                return false;
            }

            // Check if start and end are walkable
            var startNode = grid.GetNode(startX, startY);
            var endNode = grid.GetNode(endX, endY);

            if (startNode == null || !startNode.walkable)
            {
                return false;
            }

            if (endNode == null || !endNode.walkable)
            {
                return false;
            }

            // Clear previous search data
            ClearSearchData();

            // Run A* algorithm
            PathNode pathEndNode = FindPath(startX, startY, endX, endY);

            // Build result path if found
            if (pathEndNode != null)
            {
                BuildResultPath(pathEndNode, resultPath);
                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - A* Algorithm

        private PathNode FindPath(int startX, int startY, int endX, int endY)
        {
            // Create start node
            PathNode startPathNode = GetOrCreatePathNode(startX, startY);
            startPathNode.gCost = 0;
            startPathNode.hCost = CalculateHeuristic(startX, startY, endX, endY);
            startPathNode.parent = null;

            // Add to open set
            openSet.Add(startPathNode);
            openSetLookup.Add(GetNodeKey(startX, startY));

            // A* main loop
            while (openSet.Count > 0)
            {
                // Get node with lowest fCost
                PathNode currentNode = GetLowestFCostNode();

                // Check if we reached the end
                if (currentNode.x == endX && currentNode.y == endY)
                {
                    return currentNode;
                }

                // Move current node from open to closed set
                int currentKey = GetNodeKey(currentNode.x, currentNode.y);
                openSet.Remove(currentNode);
                openSetLookup.Remove(currentKey);
                closedSet.Add(currentKey);

                // Process neighbors
                ProcessNeighbors(currentNode, endX, endY);
            }

            // No path found
            return null;
        }

        private void ProcessNeighbors(PathNode currentNode, int endX, int endY)
        {
            for (int i = 0; i < 4; i++)
            {
                int neighborX = currentNode.x + dx[i];
                int neighborY = currentNode.y + dy[i];

                // Check if neighbor is valid
                if (!grid.InBounds(neighborX, neighborY))
                    continue;

                int neighborKey = GetNodeKey(neighborX, neighborY);

                // Skip if already in closed set
                if (closedSet.Contains(neighborKey))
                    continue;

                // Check if neighbor is walkable
                var mazeNode = grid.GetNode(neighborX, neighborY);
                if (mazeNode == null || !mazeNode.walkable)
                    continue;

                // Calculate costs
                float movementCost = grid.GetMoveCost(neighborX, neighborY);
                float tentativeGCost = currentNode.gCost + movementCost;

                // Get or create neighbor PathNode
                PathNode neighborPathNode = GetOrCreatePathNode(neighborX, neighborY);

                // Check if this path is better
                bool isInOpenSet = openSetLookup.Contains(neighborKey);

                if (!isInOpenSet || tentativeGCost < neighborPathNode.gCost)
                {
                    // Update neighbor
                    neighborPathNode.gCost = tentativeGCost;
                    neighborPathNode.hCost = CalculateHeuristic(neighborX, neighborY, endX, endY);
                    neighborPathNode.parent = currentNode;

                    // Add to open set if not already there
                    if (!isInOpenSet)
                    {
                        openSet.Add(neighborPathNode);
                        openSetLookup.Add(neighborKey);
                    }
                }
            }
        }

        private PathNode GetLowestFCostNode()
        {
            PathNode lowestNode = openSet[0];
            float lowestFCost = lowestNode.fCost;

            for (int i = 1; i < openSet.Count; i++)
            {
                float fCost = openSet[i].fCost;
                if (fCost < lowestFCost || (fCost == lowestFCost && openSet[i].hCost < lowestNode.hCost))
                {
                    lowestNode = openSet[i];
                    lowestFCost = fCost;
                }
            }

            return lowestNode;
        }

        #endregion

        #region Private Methods - Helpers

        private float CalculateHeuristic(int x1, int y1, int x2, int y2)
        {
            // Manhattan distance
            return Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
        }

        private int GetNodeKey(int x, int y)
        {
            // Encode x and y into a single int key
            // Assumes grid size < 65536 (16 bits per coordinate)
            return (y << 16) | x;
        }

        private PathNode GetOrCreatePathNode(int x, int y)
        {
            int key = GetNodeKey(x, y);

            if (!allNodes.TryGetValue(key, out PathNode node))
            {
                node = new PathNode(x, y);
                allNodes[key] = node;
            }
            else
            {
                // Reset costs for reuse
                node.gCost = float.MaxValue;
                node.hCost = 0;
                node.parent = null;
            }

            return node;
        }

        private void BuildResultPath(PathNode endNode, List<MazeGrid.MazeNode> resultPath)
        {
            resultPath.Clear();

            // Trace back from end to start
            PathNode current = endNode;
            while (current != null)
            {
                var mazeNode = grid.GetNode(current.x, current.y);
                if (mazeNode != null)
                {
                    resultPath.Add(mazeNode);
                }
                current = current.parent;
            }

            // Reverse to get start-to-end order
            resultPath.Reverse();
        }

        private void ClearSearchData()
        {
            openSet.Clear();
            openSetLookup.Clear();
            closedSet.Clear();
            // Note: We keep allNodes dictionary for node reuse
        }

        #endregion
    }
}

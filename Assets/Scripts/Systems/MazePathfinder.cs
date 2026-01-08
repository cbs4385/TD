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
        /// Internal node class for A* pathfinding algorithm with 3D support.
        /// </summary>
        private class PathNode
        {
            public MazeGrid.MazeNode mazeNode; // Reference to the actual maze node
            public float gCost; // Cost from start to this node
            public float hCost; // Heuristic cost from this node to end
            public float fCost => gCost + hCost; // Total cost
            public PathNode parent;

            public PathNode(MazeGrid.MazeNode mazeNode)
            {
                this.mazeNode = mazeNode;
                this.gCost = float.MaxValue;
                this.hCost = 0;
                this.parent = null;
            }

            // Convenience properties for backward compatibility
            public int x => mazeNode.x;
            public int y => mazeNode.y;
            public int z => mazeNode.z;
        }

        #endregion

        #region Private Fields

        private readonly MazeGrid grid;
        private readonly Dictionary<long, PathNode> allNodes; // Cache of PathNode objects (now uses long key for 3D)
        private readonly List<PathNode> openSet;
        private readonly HashSet<long> openSetLookup;
        private readonly HashSet<long> closedSet;
        private readonly bool logTileSelection;

        // Heuristic scaling factor used for fCost tie-breaking toward the goal.
        // Keep this small and positive so equal-cost nodes prefer direct progress,
        // otherwise A* will pick arbitrary directions and can create zig-zag paths.
        private const float HEURISTIC_SCALE = 1.0f;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new pathfinder for the given maze grid.
        /// </summary>
        /// <param name="grid">The maze grid to pathfind over</param>
        public MazePathfinder(MazeGrid grid, bool logTileSelection = false)
        {
            if (grid == null)
            {
                return;
            }

            this.grid = grid;
            this.allNodes = new Dictionary<long, PathNode>();
            this.openSet = new List<PathNode>();
            this.openSetLookup = new HashSet<long>();
            this.closedSet = new HashSet<long>();
            this.logTileSelection = logTileSelection;
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
        /// <param name="attractionMultiplier">Multiplier for attraction effect (1.0 = normal, -1.0 = inverted, 0 = ignore)</param>
        /// <returns>True if path was found, false otherwise</returns>
        public bool TryFindPath(int startX, int startY, int endX, int endY, List<MazeGrid.MazeNode> resultPath, float attractionMultiplier = 1.0f)
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

            // Run A* algorithm with attraction multiplier
            PathNode pathEndNode = FindPath(startX, startY, endX, endY, attractionMultiplier);

            // Build result path if found
            if (pathEndNode != null)
            {
                BuildResultPath(pathEndNode, resultPath);

                // Debug: Check if path contains any water tiles
                int waterTileCount = 0;
                foreach (var node in resultPath)
                {
                    if (node.terrain == TileType.Water)
                    {
                        waterTileCount++;
                    }
                }
                if (waterTileCount > 0)
                {
                }

                return true;
            }

            return false;
        }

        #endregion

        #region Private Methods - A* Algorithm

        private PathNode FindPath(int startX, int startY, int endX, int endY, float attractionMultiplier)
        {
            // Get start and end maze nodes
            MazeGrid.MazeNode startMazeNode = grid.GetNode(startX, startY);
            if (startMazeNode == null)
            {
                return null;
            }

            // Create start node
            PathNode startPathNode = GetOrCreatePathNode(startMazeNode);
            startPathNode.gCost = 0;
            startPathNode.hCost = CalculateHeuristic(startX, startY, endX, endY);
            startPathNode.parent = null;

            // Add to open set
            openSet.Add(startPathNode);
            openSetLookup.Add(GetNodeKey(startMazeNode));

            // A* main loop
            while (openSet.Count > 0)
            {
                // Get node with lowest fCost
                PathNode currentNode = GetLowestFCostNode();
                if (logTileSelection)
                {
                    Debug.Log(
                        $"Pathfinding select node ({currentNode.x}, {currentNode.y}, {currentNode.z}) " +
                        $"g={currentNode.gCost:F2} h={currentNode.hCost:F2} f={currentNode.fCost:F2} " +
                        $"open={openSet.Count} closed={closedSet.Count}");
                }

                // Check if we reached the end
                if (currentNode.x == endX && currentNode.y == endY)
                {
                    return currentNode;
                }

                // Move current node from open to closed set
                long currentKey = GetNodeKey(currentNode.mazeNode);
                openSet.Remove(currentNode);
                openSetLookup.Remove(currentKey);
                closedSet.Add(currentKey);

                // Process neighbors with attraction multiplier
                ProcessNeighbors(currentNode, endX, endY, attractionMultiplier);
            }

            // No path found
            return null;
        }

        private void ProcessNeighbors(PathNode currentNode, int endX, int endY, float attractionMultiplier)
        {
            // Get neighbors including custom connections (stairs, ramps, etc.)
            List<MazeGrid.MazeNode> neighbors = grid.GetNeighbors(currentNode.mazeNode, true);

            foreach (var neighborMazeNode in neighbors)
            {
                long neighborKey = GetNodeKey(neighborMazeNode);

                // Skip if already in closed set
                if (closedSet.Contains(neighborKey))
                {
                    if (logTileSelection)
                    {
                        Debug.Log(
                            $"Pathfinding skip neighbor ({neighborMazeNode.x}, {neighborMazeNode.y}, {neighborMazeNode.z}) " +
                            $"from ({currentNode.x}, {currentNode.y}, {currentNode.z}): closed set");
                    }
                    continue;
                }

                // Check if neighbor is walkable (should already be filtered by GetNeighbors, but double-check)
                if (!neighborMazeNode.walkable)
                {
                    if (logTileSelection)
                    {
                        Debug.Log(
                            $"Pathfinding skip neighbor ({neighborMazeNode.x}, {neighborMazeNode.y}, {neighborMazeNode.z}) " +
                            $"from ({currentNode.x}, {currentNode.y}, {currentNode.z}): not walkable");
                    }
                    continue;
                }

                // Calculate costs with attraction multiplier based on visitor state
                float movementCost = grid.GetMoveCost(neighborMazeNode.x, neighborMazeNode.y, attractionMultiplier);
                float rawCost = neighborMazeNode.baseCost - (neighborMazeNode.attraction * attractionMultiplier);

                // Check if this is a diagonal movement (Manhattan distance > 1)
                int dx = Mathf.Abs(neighborMazeNode.x - currentNode.x);
                int dy = Mathf.Abs(neighborMazeNode.y - currentNode.y);
                bool isDiagonalMove = (dx + dy) == 2;

                if (isDiagonalMove)
                {
                    // Diagonal movement is √2 ≈ 1.414 times longer
                    movementCost *= 1.414f;
                }

                // Add extra cost for vertical movement (stairs/ramps)
                float heightDifference = Mathf.Abs(neighborMazeNode.height - currentNode.mazeNode.height);
                if (heightDifference > 0.01f)
                {
                    // Stairs/ramps cost more than flat movement
                    movementCost += heightDifference * 0.5f;
                }

                float tentativeGCost = currentNode.gCost + movementCost;

                // Get or create neighbor PathNode
                PathNode neighborPathNode = GetOrCreatePathNode(neighborMazeNode);

                // Check if this path is better
                bool isInOpenSet = openSetLookup.Contains(neighborKey);

                if (logTileSelection)
                {
                    Debug.Log(
                        $"Pathfinding evaluate neighbor ({neighborMazeNode.x}, {neighborMazeNode.y}, {neighborMazeNode.z}) " +
                        $"from ({currentNode.x}, {currentNode.y}, {currentNode.z}) " +
                        $"base={neighborMazeNode.baseCost:F2} attraction={neighborMazeNode.attraction:F2} " +
                        $"multiplier={attractionMultiplier:F2} rawCost={rawCost:F2} " +
                        $"moveCost={movementCost:F2} diagonal={isDiagonalMove} heightDiff={heightDifference:F2} " +
                        $"tentativeG={tentativeGCost:F2} prevG={neighborPathNode.gCost:F2} inOpen={isInOpenSet}");
                }

                if (!isInOpenSet || tentativeGCost < neighborPathNode.gCost)
                {
                    // Update neighbor
                    neighborPathNode.gCost = tentativeGCost;
                    neighborPathNode.hCost = CalculateHeuristic(neighborMazeNode.x, neighborMazeNode.y, endX, endY);
                    neighborPathNode.parent = currentNode;

                    // Add to open set if not already there
                    if (!isInOpenSet)
                    {
                        openSet.Add(neighborPathNode);
                        openSetLookup.Add(neighborKey);
                    }

                    if (logTileSelection)
                    {
                        string updateReason = isInOpenSet ? "improved" : "added";
                        Debug.Log(
                            $"Pathfinding {updateReason} node ({neighborMazeNode.x}, {neighborMazeNode.y}, {neighborMazeNode.z}) " +
                            $"g={neighborPathNode.gCost:F2} h={neighborPathNode.hCost:F2} f={neighborPathNode.fCost:F2} " +
                            $"parent=({currentNode.x}, {currentNode.y}, {currentNode.z})");
                    }
                }
                else if (logTileSelection)
                {
                    Debug.Log(
                        $"Pathfinding keep existing node ({neighborMazeNode.x}, {neighborMazeNode.y}, {neighborMazeNode.z}) " +
                        $"g={neighborPathNode.gCost:F2} <= tentativeG={tentativeGCost:F2}");
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
            // Manhattan distance scaled to gently bias node selection toward the goal
            // when fCost ties occur, keeping paths straighter and avoiding zig-zagging.
            float manhattanDistance = Mathf.Abs(x1 - x2) + Mathf.Abs(y1 - y2);
            return manhattanDistance * HEURISTIC_SCALE;
        }

        private long GetNodeKey(MazeGrid.MazeNode node)
        {
            // Encode x, y, z into a single long key
            // Uses 20 bits per coordinate (supports up to 1 million per dimension)
            return ((long)node.z << 40) | ((long)node.y << 20) | (long)node.x;
        }

        private PathNode GetOrCreatePathNode(MazeGrid.MazeNode mazeNode)
        {
            long key = GetNodeKey(mazeNode);

            if (!allNodes.TryGetValue(key, out PathNode node))
            {
                node = new PathNode(mazeNode);
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
                if (current.mazeNode != null)
                {
                    resultPath.Add(current.mazeNode);
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

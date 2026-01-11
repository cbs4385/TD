using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.Cameras;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages dynamic maze growth by adding new nodes at open endpoints every 30 seconds.
    /// Handles portal placement/removal and spawn point updates.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class DynamicMazeGrowth : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Growth Settings")]
        [SerializeField]
        [Tooltip("Time in seconds between maze growth cycles")]
        private float growthInterval = 30f;

        [SerializeField]
        [Tooltip("Enable automatic maze growth")]
        private bool autoGrowth = true;

        [Header("Portal Settings")]
        [SerializeField]
        [Tooltip("Portal prefab to place at open endpoints")]
        private GameObject portalPrefab;

        [SerializeField]
        [Tooltip("Height offset for portal placement (world units)")]
        private float portalHeightOffset = 0f;

        [Header("References")]
        [SerializeField]
        [Tooltip("Parent transform for spawned portals")]
        private Transform portalsParent;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private MazeRenderer mazeRenderer;
        private float nextGrowthTime;

        private const float NodeRadius = 12.0f;
        private const float PathRadius = 2.0f;
        private const float WallBuffer = 4.0f;

        // Track portals at each spawn point
        private Dictionary<char, GameObject> spawnPointPortals = new Dictionary<char, GameObject>();

        // Track available spawn IDs
        private char[] availableSpawnIds = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'I', 'J', 'K', 'L', 'M', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
        private int nextSpawnIdIndex = 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();
            mazeRenderer = GetComponent<MazeRenderer>();
        }

        private void Start()
        {
            // Create portals parent if not assigned
            if (portalsParent == null)
            {
                GameObject portalsObj = new GameObject("Portals");
                portalsObj.transform.SetParent(transform);
                portalsObj.transform.localPosition = Vector3.zero;
                portalsParent = portalsObj.transform;
            }

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            // Initialize portals at existing spawn points
            InitializeSpawnPointPortals();

            // Schedule first growth
            if (autoGrowth)
            {
                nextGrowthTime = Time.time + growthInterval;
            }
        }

        private void Update()
        {
            if (autoGrowth && Time.time >= nextGrowthTime)
            {
                GrowMaze();
                nextGrowthTime = Time.time + growthInterval;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Creates portals at all existing spawn points.
        /// If no spawn points exist, creates them from edge walkable tiles.
        /// </summary>
        private void InitializeSpawnPointPortals()
        {
            if (mazeGridBehaviour == null)
            {
                return;
            }

            if (portalPrefab == null)
            {
                return;
            }

            var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();

            // If no spawn points exist, create them from edge walkable tiles
            if (spawnPoints.Count == 0)
            {
                CreateInitialSpawnPoints();
                spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
            }

            foreach (var kvp in spawnPoints)
            {
                char spawnId = kvp.Key;
                Vector2Int gridPos = kvp.Value;

                CreatePortalAtSpawnPoint(spawnId, gridPos);
            }

            // Track which spawn ID to use next
            nextSpawnIdIndex = spawnPoints.Count;

        }

        /// <summary>
        /// Creates initial spawn points from edge walkable tiles if none exist.
        /// Note: Grid now includes buffer space, so edges are at GRID_BUFFER and (dimension - GRID_BUFFER - 1).
        /// </summary>
        private void CreateInitialSpawnPoints()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            List<Vector2Int> edgeWalkableTiles = new List<Vector2Int>();

            int width = mazeGridBehaviour.Grid.Width;
            int height = mazeGridBehaviour.Grid.Height;

            // Content edges are at GRID_BUFFER and (dimension - GRID_BUFFER - 1)
            int leftEdge = MazeGrid.GRID_BUFFER;
            int rightEdge = width - MazeGrid.GRID_BUFFER - 1;
            int topEdge = MazeGrid.GRID_BUFFER;
            int bottomEdge = height - MazeGrid.GRID_BUFFER - 1;

            // Find all walkable tiles on the content edges
            for (int x = leftEdge; x <= rightEdge; x++)
            {
                // Top edge
                var topNode = mazeGridBehaviour.Grid.GetNode(x, topEdge);
                if (topNode != null && topNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(x, topEdge));
                }

                // Bottom edge
                var bottomNode = mazeGridBehaviour.Grid.GetNode(x, bottomEdge);
                if (bottomNode != null && bottomNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(x, bottomEdge));
                }
            }

            for (int y = topEdge + 1; y < bottomEdge; y++)
            {
                // Left edge
                var leftNode = mazeGridBehaviour.Grid.GetNode(leftEdge, y);
                if (leftNode != null && leftNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(leftEdge, y));
                }

                // Right edge
                var rightNode = mazeGridBehaviour.Grid.GetNode(rightEdge, y);
                if (rightNode != null && rightNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(rightEdge, y));
                }
            }

            if (edgeWalkableTiles.Count == 0)
            {
                return;
            }

            // Select up to 4 evenly distributed edge tiles as spawn points
            int numSpawnPoints = Mathf.Min(4, edgeWalkableTiles.Count);
            for (int i = 0; i < numSpawnPoints; i++)
            {
                int index = (i * edgeWalkableTiles.Count) / numSpawnPoints;
                Vector2Int pos = edgeWalkableTiles[index];
                char spawnId = availableSpawnIds[i];

                // Update the tile symbol
                UpdateTileSymbol(pos, spawnId);

            }

            // Rebuild spawn points dictionary
            RebuildSpawnPointsDictionary();
        }

        #endregion

        #region Maze Growth

        /// <summary>
        /// Grows the maze by adding a new node at one of the open endpoints.
        /// Uses the same generation logic as initial maze creation via PlanarForestMazeGenerator.Step().
        /// </summary>
        public void GrowMaze()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                Debug.LogWarning("[DynamicGrowth] MazeGridBehaviour or Grid is null");
                return;
            }

            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null)
            {
                Debug.LogWarning("[DynamicGrowth] ForestMapState is null - make sure planar generator is being used");
                return;
            }

            if (forestMapState.Frontier.Count == 0)
            {
                Debug.Log("[DynamicGrowth] No frontier edges available for growth");
                return;
            }

            int frontierCountBefore = forestMapState.Frontier.Count;
            Debug.Log($"[DynamicGrowth] Starting growth cycle with {frontierCountBefore} frontier edges");

            // Capture frontier edges BEFORE growth to identify new edges later
            HashSet<int> frontierBeforeGrowth = new HashSet<int>(forestMapState.Frontier);

            // Use the same Step() method as initial generation to add a new node
            int nodeCountBefore = forestMapState.Nodes.Count;
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            if (!success)
            {
                Debug.LogWarning("[DynamicGrowth] Step() failed - no valid placement found");
                return;
            }

            // Get the newly created node (last node in the list)
            int newNodeId = nodeCountBefore; // Nodes are added sequentially
            var newNode = forestMapState.Nodes[newNodeId];

            // Log the angles of edges connected to the new node
            float scale = forestMapState.Scale;
            Vector2 offset = forestMapState.Offset;
            Vector2 nodeGridPos = newNode.Position * scale + offset;
            Debug.Log($"[DynamicGrowth] Successfully created node {newNodeId} at position ({newNode.Position.x:F2}, {newNode.Position.y:F2})");
            Debug.Log($"[DynamicGrowth] Node {newNodeId} maps to grid ({nodeGridPos.x:F2}, {nodeGridPos.y:F2}) [scale={scale:F2}, offset=({offset.x:F2}, {offset.y:F2})]");
            Debug.Log($"[DynamicGrowth] Node {newNodeId} has {newNode.IncidentEdges.Count} edges at angles: {string.Join(", ", newNode.UsedAngles.Select(a => $"{a * Mathf.Rad2Deg:F1}°"))}");
            Debug.Log($"[DynamicGrowth] Frontier count: {frontierCountBefore} → {forestMapState.Frontier.Count}");

            // Check if grid expansion is needed before rasterization
            var grid = mazeGridBehaviour.Grid;
            int minBorderTiles = 5; // Minimum border tiles around maze content
            bool gridExpanded = false;

            float rasterMargin = (NodeRadius + PathRadius + WallBuffer) * scale;

            // Calculate grid extents of new node and connected edges (reuse nodeGridPos from above)
            float minX = nodeGridPos.x;
            float maxX = nodeGridPos.x;
            float minY = nodeGridPos.y;
            float maxY = nodeGridPos.y;

            var edgesToRasterize = forestMapState.Edges.Where(e =>
                e.NodeA == newNodeId || (e.NodeB.HasValue && e.NodeB.Value == newNodeId)
            );

            foreach (var edge in edgesToRasterize)
            {
                foreach (var point in edge.PolylinePoints)
                {
                    Vector2 gridPoint = point * scale + offset;
                    minX = Mathf.Min(minX, gridPoint.x);
                    maxX = Mathf.Max(maxX, gridPoint.x);
                    minY = Mathf.Min(minY, gridPoint.y);
                    maxY = Mathf.Max(maxY, gridPoint.y);
                }
            }

            int minGridX = Mathf.FloorToInt(minX - rasterMargin);
            int maxGridX = Mathf.CeilToInt(maxX + rasterMargin);
            int minGridY = Mathf.FloorToInt(minY - rasterMargin);
            int maxGridY = Mathf.CeilToInt(maxY + rasterMargin);

            // Check if expansion needed (ensure minBorderTiles on all sides)
            int expandLeft = Mathf.Max(0, minBorderTiles - minGridX);
            int expandRight = Mathf.Max(0, maxGridX + minBorderTiles - (grid.Width - 1));
            int expandTop = Mathf.Max(0, minBorderTiles - minGridY);
            int expandBottom = Mathf.Max(0, maxGridY + minBorderTiles - (grid.Height - 1));

            if (expandLeft > 0 || expandRight > 0 || expandTop > 0 || expandBottom > 0)
            {
                Debug.Log($"[DynamicGrowth] Grid expansion needed: L={expandLeft}, R={expandRight}, T={expandTop}, B={expandBottom}");
                ExpandGrid(ref grid, expandLeft, expandRight, expandTop, expandBottom, ref forestMapState);
                ApplyGridExpansionOffset(expandLeft, expandTop);
                ApplyGridExpansionOffsetToEntities(expandLeft, expandTop);
                gridExpanded = true;
            }

            // Rasterize the new node and its edges to the grid
            char[,] gridArray = new char[grid.Height, grid.Width];

            // Copy current grid state
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    gridArray[y, x] = node != null ? node.symbol : '#';
                }
            }

            // Rasterize new node
            ForestMaze.PlanarForestMazeGenerator.RasterizeNodesToGrid(
                forestMapState,
                gridArray,
                new List<int> { newNodeId },
                grid.Width,
                grid.Height
            );

            // Update the grid with new walkable tiles
            // CRITICAL: Only ADD new content, never REMOVE existing walkable paths
            List<Vector2Int> newWalkableTiles = new List<Vector2Int>();
            int preservedPathCount = 0;
            int preservedVoidCount = 0;
            int addedPathCount = 0;
            int addedWallCount = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node != null)
                    {
                        char existingSymbol = node.symbol;
                        char newSymbol = gridArray[y, x];
                        bool wasWalkable = node.walkable;

                        // Skip updating void tiles - preserve the optimization
                        if (existingSymbol == ' ' && newSymbol == '#')
                        {
                            preservedVoidCount++;
                            continue; // Don't convert void back to walls
                        }

                        // NEVER overwrite existing walkable paths with walls
                        // Only add new content or update void/wall areas
                        if (IsWalkableSymbol(existingSymbol))
                        {
                            preservedPathCount++;
                            // Keep existing walkable content, don't overwrite with walls
                            continue;
                        }

                        // Now we can safely update non-walkable tiles
                        node.symbol = newSymbol;
                        if (newSymbol == '.' || newSymbol == 'N')
                        {
                            node.walkable = true;
                            node.SetTerrain(TileType.Path);

                            // Track newly added walkable tiles
                            newWalkableTiles.Add(new Vector2Int(x, y));
                            addedPathCount++;
                        }
                        else if (newSymbol == '#')
                        {
                            // Add walls only where there wasn't walkable content
                            node.walkable = false;
                            node.SetTerrain(TileType.TreeBramble);
                            addedWallCount++;
                        }
                    }
                }
            }

            Debug.Log($"[DynamicGrowth] Grid merge: preserved {preservedPathCount} path cells, {preservedVoidCount} void cells | added {addedPathCount} paths, {addedWallCount} walls");

            // Ensure wall borders around newly added walkable content
            EnsureWallBordersAroundTiles(newWalkableTiles, 3);

            // Mark endpoints of partial frontier edges as walkable so spawn points can be placed there
            // ONLY process NEW edges created during this growth cycle, not existing frontier edges
            MarkPartialEdgeEndpointsAsWalkable(frontierBeforeGrowth);

            // Remove old spawn point portals and rebuild from frontier
            // This updates spawn points but doesn't signal visitors yet
            var removedSpawnPoints = RebuildSpawnPointsFromFrontier();

            // Refresh the maze renderer to show new tiles
            if (mazeRenderer != null)
            {
                mazeRenderer.RefreshMaze();
            }

            // Trigger all active visitors to recalculate their paths
            // This ensures the new spawn points are fully integrated before visitors try to path to them
            TriggerVisitorPathRecalculation();

            // NOW signal visitors to retarget from removed exits
            // The new spawn points are fully walkable and pathfindable at this point
            if (removedSpawnPoints != null && removedSpawnPoints.Count > 0)
            {
                SignalVisitorsToRetargetFromRemovedExits(removedSpawnPoints);
            }

            Debug.Log("[DynamicGrowth] Growth cycle complete");
        }

        /// <summary>
        /// Updates a tile's symbol without changing other properties.
        /// </summary>
        private void UpdateTileSymbol(Vector2Int gridPos, char symbol)
        {
            if (!mazeGridBehaviour.Grid.InBounds(gridPos.x, gridPos.y))
                return;

            var node = mazeGridBehaviour.Grid.GetNode(gridPos.x, gridPos.y);
            if (node != null)
            {
                node.symbol = symbol;
            }
        }

        /// <summary>
        /// Checks if a character represents a spawn point (uppercase letter except H and N).
        /// </summary>
        private bool IsSpawnPointChar(char c)
        {
            return char.IsUpper(c) && c != 'H' && c != 'N';
        }

        /// <summary>
        /// Checks if a character represents walkable terrain (paths, nodes, or spawn points).
        /// </summary>
        private bool IsWalkableSymbol(char c)
        {
            return c == '.' || c == 'N' || c == 'H' || IsSpawnPointChar(c);
        }

        /// <summary>
        /// Expands the grid in the specified directions to accommodate new nodes near boundaries.
        /// </summary>
        private void ExpandGrid(ref MazeGrid grid, int expandLeft, int expandRight, int expandTop, int expandBottom, ref ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            int oldWidth = grid.Width;
            int oldHeight = grid.Height;
            int newWidth = oldWidth + expandLeft + expandRight;
            int newHeight = oldHeight + expandTop + expandBottom;

            Debug.Log($"[DynamicGrowth] Expanding grid from {oldWidth}x{oldHeight} to {newWidth}x{newHeight}");

            // Create new larger grid
            var newGrid = new MazeGrid(newWidth, newHeight);

            // Copy old grid data with offset
            for (int y = 0; y < oldHeight; y++)
            {
                for (int x = 0; x < oldWidth; x++)
                {
                    var oldNode = grid.GetNode(x, y);
                    if (oldNode != null)
                    {
                        int newX = x + expandLeft;
                        int newY = y + expandTop;
                        var newNode = newGrid.GetNode(newX, newY);
                        if (newNode != null)
                        {
                            newNode.walkable = oldNode.walkable;
                            newNode.symbol = oldNode.symbol;
                            newNode.SetTerrain(oldNode.terrain);
                        }
                    }
                }
            }

            // Update ForestMapState offset to account for grid expansion
            forestMapState.Offset += new Vector2(expandLeft, expandTop);

            // Replace grid in MazeGridBehaviour using reflection
            var gridField = typeof(MazeGridBehaviour).GetField("grid",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var widthField = typeof(MazeGridBehaviour).GetField("width",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var heightField = typeof(MazeGridBehaviour).GetField("height",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (gridField != null)
            {
                gridField.SetValue(mazeGridBehaviour, newGrid);
            }
            if (widthField != null)
            {
                widthField.SetValue(mazeGridBehaviour, newWidth);
            }
            if (heightField != null)
            {
                heightField.SetValue(mazeGridBehaviour, newHeight);
            }

            // Update local reference
            grid = newGrid;

            Debug.Log($"[DynamicGrowth] Grid expansion complete, offset adjusted by ({expandLeft}, {expandTop})");
        }

        /// <summary>
        /// Offsets the camera and focal point to keep them stationary relative to the maze when the grid expands.
        /// </summary>
        private void ApplyGridExpansionOffset(int expandLeft, int expandTop)
        {
            if (expandLeft == 0 && expandTop == 0)
            {
                return;
            }

            float tileSize = mazeGridBehaviour != null ? mazeGridBehaviour.TileSize : 1f;
            Vector3 worldOffset = new Vector3(expandLeft * tileSize, expandTop * tileSize, 0f);

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraController3D cameraController = mainCamera.GetComponent<CameraController3D>();
            if (cameraController != null)
            {
                cameraController.ApplyWorldOffset(worldOffset);
                return;
            }

            mainCamera.transform.position += worldOffset;

            GameObject focalPointObject = GameObject.Find("Focal Point");
            if (focalPointObject != null)
            {
                focalPointObject.transform.position += worldOffset;
            }
        }

        /// <summary>
        /// Offsets grid-aligned entities to keep them anchored to the same tiles after expansion.
        /// </summary>
        private void ApplyGridExpansionOffsetToEntities(int expandLeft, int expandTop)
        {
            if (expandLeft == 0 && expandTop == 0)
            {
                return;
            }

            float tileSize = mazeGridBehaviour != null ? mazeGridBehaviour.TileSize : 1f;
            Vector3 worldOffset = new Vector3(expandLeft * tileSize, expandTop * tileSize, 0f);
            Vector2Int gridOffset = new Vector2Int(expandLeft, expandTop);

            var heart = FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
            if (heart != null)
            {
                heart.transform.position += worldOffset;
            }

            var visitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (visitors != null)
            {
                foreach (var visitor in visitors)
                {
                    if (visitor != null)
                    {
                        visitor.transform.position += worldOffset;
                        visitor.ApplyGridOffset(gridOffset);
                        // Don't flag path recalculation here - it will be triggered after
                        // grid is fully updated and UI is refreshed in GrowMaze()
                    }
                }
            }

            foreach (var lantern in FindObjectsByType<FaeMaze.Props.FaeLantern>(FindObjectsSortMode.None))
            {
                lantern.transform.position += worldOffset;
            }

            foreach (var ring in FindObjectsByType<FaeMaze.Props.FairyRing>(FindObjectsSortMode.None))
            {
                ring.transform.position += worldOffset;
            }

            foreach (var puka in FindObjectsByType<FaeMaze.Props.PukaHazard>(FindObjectsSortMode.None))
            {
                puka.transform.position += worldOffset;
            }

            foreach (var wisp in FindObjectsByType<FaeMaze.Props.WillowTheWisp>(FindObjectsSortMode.None))
            {
                wisp.transform.position += worldOffset;
            }

            var heartPowerManager = FaeMaze.HeartPowers.HeartPowerManager.Instance;
            if (heartPowerManager != null)
            {
                heartPowerManager.ApplyGridExpansionOffset(worldOffset, gridOffset);
            }
        }

        /// <summary>
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Removes portals for completed edges and creates portals for partial edges.
        /// Returns list of removed spawn points for deferred visitor retargeting.
        /// </summary>
        private List<Vector2Int> RebuildSpawnPointsFromFrontier()
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return new List<Vector2Int>();

            var grid = mazeGridBehaviour.Grid;
            float scale = forestMapState.Scale;
            Vector2 offset = forestMapState.Offset;

            // Capture current spawn points before clearing
            var oldSpawnPoints = new Dictionary<Vector2Int, char>();
            if (mazeGridBehaviour != null)
            {
                var currentSpawns = mazeGridBehaviour.GetAllSpawnPoints();
                if (currentSpawns != null)
                {
                    foreach (var kvp in currentSpawns)
                    {
                        oldSpawnPoints[kvp.Value] = kvp.Key;
                    }
                }
            }

            // Clear ALL existing portals and debug visualizations
            // This ensures we remove portals for edges that have been completed
            var portalsToRemove = new List<char>(spawnPointPortals.Keys);
            foreach (char spawnId in portalsToRemove)
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Also clear any remaining debug columns that might have been orphaned
            if (portalsParent != null)
            {
                foreach (Transform child in portalsParent)
                {
                    if (child != null && child.name.StartsWith("Portal_") &&
                        (child.name.Contains("_SpawnToNode") || child.name.Contains("_XAxis")))
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }

            // Clear all spawn point characters from grid
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node != null && IsSpawnPointChar(node.symbol))
                    {
                        node.symbol = '.';
                    }
                }
            }

            // Reset spawn ID index
            nextSpawnIdIndex = 0;

            // Place spawn points at partial edge endpoints
            int spawnIndex = 0;
            foreach (int edgeId in forestMapState.Frontier)
            {
                var edge = forestMapState.Edges[edgeId];
                if (!edge.Partial || edge.PolylinePoints.Count == 0) continue;

                // Find the endpoint (last point) of the partial edge
                var connectedNode = forestMapState.Nodes[edge.NodeA];
                Vector2 nodeCenter = connectedNode.Position * scale + offset;
                Vector2Int nodeCenterGrid = new Vector2Int(Mathf.RoundToInt(nodeCenter.x), Mathf.RoundToInt(nodeCenter.y));

                Debug.Log($"[DynamicGrowth] Edge {edgeId}: Node {edge.NodeA} at grid {nodeCenterGrid}, {edge.PolylinePoints.Count} polyline points");

                // Get the endpoint (last point in polyline)
                Vector2 endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1] * scale + offset;
                Vector2Int endpointGrid = new Vector2Int(Mathf.RoundToInt(endpoint.x), Mathf.RoundToInt(endpoint.y));

                Debug.Log($"[DynamicGrowth] Endpoint at grid {endpointGrid}");

                // Get a reference point from the middle of the polyline (known to be reachable)
                Vector2 referencePoint = edge.PolylinePoints[edge.PolylinePoints.Count / 2] * scale + offset;
                Vector2Int referenceGrid = new Vector2Int(Mathf.RoundToInt(referencePoint.x), Mathf.RoundToInt(referencePoint.y));

                // Flood fill from reference point to find all reachable cells
                var reachableCells = grid.FloodFillReachable(referenceGrid.x, referenceGrid.y, 100, 10000);
                Debug.Log($"[DynamicGrowth] Found {reachableCells.Count} reachable cells from reference point {referenceGrid}");

                // Find the walkable point CLOSEST to the endpoint that is also REACHABLE
                Vector2Int spawnGridPos = endpointGrid;
                int walkableCount = 0;
                int reachableCount = 0;
                float minDistanceToEndpoint = float.MaxValue;

                // Search within a small radius around the endpoint
                const int searchRadius = 3;
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    for (int dx = -searchRadius; dx <= searchRadius; dx++)
                    {
                        int px = endpointGrid.x + dx;
                        int py = endpointGrid.y + dy;

                        if (px >= 0 && px < grid.Width && py >= 0 && py < grid.Height)
                        {
                            var node = grid.GetNode(px, py);
                            if (node != null && node.walkable)
                            {
                                walkableCount++;

                                // Verify this cell is reachable from the path network
                                Vector2Int cellPos = new Vector2Int(px, py);
                                if (reachableCells.Contains(cellPos))
                                {
                                    reachableCount++;

                                    // Find CLOSEST reachable cell to the endpoint
                                    float distanceToEndpoint = (px - endpointGrid.x) * (px - endpointGrid.x) + (py - endpointGrid.y) * (py - endpointGrid.y);
                                    Debug.Log($"[DynamicGrowth]   Cell ({px},{py}) walkable AND reachable, distance to endpoint={Mathf.Sqrt(distanceToEndpoint):F2}");
                                    if (distanceToEndpoint < minDistanceToEndpoint)
                                    {
                                        minDistanceToEndpoint = distanceToEndpoint;
                                        spawnGridPos = new Vector2Int(px, py);
                                    }
                                }
                                else
                                {
                                    Debug.Log($"[DynamicGrowth]   Cell ({px},{py}) walkable but NOT reachable from path network");
                                }
                            }
                        }
                    }
                }

                Debug.Log($"[DynamicGrowth] Search results: {walkableCount} walkable, {reachableCount} reachable");

                // Only place spawn point if we found at least one reachable tile
                if (reachableCount > 0 && grid.InBounds(spawnGridPos.x, spawnGridPos.y))
                {
                    char spawnId = GetNextAvailableSpawnId();
                    if (spawnId == '\0') break; // No more spawn IDs available

                    UpdateTileSymbol(spawnGridPos, spawnId);
                    CreatePortalAtSpawnPoint(spawnId, spawnGridPos, nodeCenterGrid);

                    Debug.Log($"[DynamicGrowth] Placed spawn point '{spawnId}' at {spawnGridPos} (distance to endpoint={Mathf.Sqrt(minDistanceToEndpoint):F2}, {reachableCount}/{walkableCount} reachable/walkable cells)");
                    spawnIndex++;
                }
                else
                {
                    Debug.LogWarning($"[DynamicGrowth] Could not place spawn point for edge {edgeId} - no reachable walkable cells found near endpoint {endpointGrid}");
                }
            }

            // Rebuild the spawn points dictionary
            RebuildSpawnPointsDictionary();

            // Find removed spawn points and signal visitors to retarget
            var newSpawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
            var removedSpawnPoints = new List<Vector2Int>();

            foreach (var oldSpawnPos in oldSpawnPoints.Keys)
            {
                bool stillExists = false;
                if (newSpawnPoints != null)
                {
                    foreach (var newSpawnPos in newSpawnPoints.Values)
                    {
                        if (newSpawnPos == oldSpawnPos)
                        {
                            stillExists = true;
                            break;
                        }
                    }
                }

                if (!stillExists)
                {
                    removedSpawnPoints.Add(oldSpawnPos);
                    Debug.Log($"[DynamicGrowth] Spawn point removed at {oldSpawnPos}");
                }
            }

            // Return removed spawn points for deferred visitor retargeting
            // (will be called from GrowMaze after all updates complete)
            return removedSpawnPoints;
        }

        /// <summary>
        /// Rebuilds the spawn points dictionary by scanning the grid for spawn markers.
        /// </summary>
        private void RebuildSpawnPointsDictionary()
        {
            // Access the private spawnPoints field through reflection
            var spawnPointsField = typeof(MazeGridBehaviour).GetField("spawnPoints",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (spawnPointsField == null)
            {
                return;
            }

            var spawnPoints = new Dictionary<char, Vector2Int>();

            // Scan the entire grid for spawn markers
            for (int y = 0; y < mazeGridBehaviour.Grid.Height; y++)
            {
                for (int x = 0; x < mazeGridBehaviour.Grid.Width; x++)
                {
                    var node = mazeGridBehaviour.Grid.GetNode(x, y);
                    if (node != null)
                    {
                        char symbol = node.symbol;
                        // Check if it's a spawn point (uppercase letter except H and N)
                        if (char.IsUpper(symbol) && symbol != 'H' && symbol != 'N')
                        {
                            spawnPoints[symbol] = new Vector2Int(x, y);
                        }
                    }
                }
            }

            // Update the maze grid behaviour's spawn points
            spawnPointsField.SetValue(mazeGridBehaviour, spawnPoints);
        }

        /// <summary>
        /// Triggers all active visitors to recalculate their paths after maze growth.
        /// This prevents visitors from getting stuck trying to reach spawn points that no longer exist.
        /// </summary>
        private void TriggerVisitorPathRecalculation()
        {
            var allVisitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (allVisitors == null)
            {
                return;
            }

            int recalculatedCount = 0;
            foreach (var visitor in allVisitors)
            {
                if (visitor != null)
                {
                    // Immediately recalculate paths instead of flagging
                    // This prevents visitors from trying to move with stale paths
                    visitor.RecalculatePath();
                    recalculatedCount++;
                }
            }

            if (recalculatedCount > 0)
            {
                Debug.Log($"[DynamicGrowth] Recalculated paths for {recalculatedCount} visitor(s) after grid update");
            }
        }

        /// <summary>
        /// Signals visitors targeting removed spawn points to retarget from those positions.
        /// This allows visitors to select a new destination based on walking distance from the removed exit.
        /// </summary>
        private void SignalVisitorsToRetargetFromRemovedExits(List<Vector2Int> removedSpawnPoints)
        {
            var allVisitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (allVisitors == null || removedSpawnPoints == null || removedSpawnPoints.Count == 0)
            {
                return;
            }

            int retargetedCount = 0;
            foreach (var visitor in allVisitors)
            {
                if (visitor != null)
                {
                    // Check if visitor's destination matches any removed spawn point
                    foreach (var removedExit in removedSpawnPoints)
                    {
                        if (visitor.RetargetFromRemovedExit(removedExit))
                        {
                            retargetedCount++;
                            break; // Only retarget once per visitor
                        }
                    }
                }
            }

            if (retargetedCount > 0)
            {
                Debug.Log($"[DynamicGrowth] Retargeted {retargetedCount} visitor(s) from removed exit positions");
            }
        }

        #endregion

        #region Portal Management

        /// <summary>
        /// Creates a portal at the specified spawn point.
        /// </summary>
        private void CreatePortalAtSpawnPoint(char spawnId, Vector2Int gridPos)
        {
            CreatePortalAtSpawnPoint(spawnId, gridPos, null);
        }

        private void CreatePortalAtSpawnPoint(char spawnId, Vector2Int gridPos, Vector2Int? targetNodeCenter)
        {
            if (portalPrefab == null)
            {
                return;
            }

            // Remove existing portal at this spawn ID if it exists
            if (spawnPointPortals.ContainsKey(spawnId))
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Calculate direction to nearest walkable tile (toward maze interior)
            Vector3? targetOverride = null;
            if (targetNodeCenter.HasValue)
            {
                targetOverride = mazeGridBehaviour.GridToWorld(targetNodeCenter.Value.x, targetNodeCenter.Value.y);
            }

            Vector2Int directionToMaze = GetDirectionToNearestWalkableTile(
                gridPos,
                out Vector3 facingVector,
                out Vector3 targetWorldPos,
                targetOverride);
            Vector3 directionToMaze3D = new Vector3(directionToMaze.x, directionToMaze.y, 0f).normalized;

            // Base world position at grid center
            Vector3 baseWorldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y, -portalHeightOffset);
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

            // Calculate portal position: center at wall edge toward maze interior
            Vector3 wallDirection = directionToMaze3D;
            float tileSize = mazeGridBehaviour.TileSize;

            // Position portal center at the wall edge of the tile
            Vector3 finalWorldPos = baseWorldPos + wallDirection * (tileSize * 0.5f);

            // Create rotation: +X axis points toward the node center vector (spawn -> node).
            Vector3 rotationVector = (targetWorldPos - spawnWorldPos).normalized;
            if (rotationVector == Vector3.zero)
            {
                rotationVector = facingVector;
            }

            Quaternion rotation;
            if (rotationVector != Vector3.zero)
            {
                float angle = Mathf.Atan2(rotationVector.y, rotationVector.x) * Mathf.Rad2Deg;
                rotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                rotation = Quaternion.identity;
            }

            // Apply maze coordinate system rotation (-90 around X to match 2D plane)
            rotation = rotation * Quaternion.Euler(-90f, 0f, 0f);

            // Instantiate portal
            GameObject portal = Instantiate(portalPrefab, finalWorldPos, rotation, portalsParent);
            portal.name = $"Portal_{spawnId}";

            // Track portal
            spawnPointPortals[spawnId] = portal;

            CreateDebugColumn(spawnWorldPos, targetWorldPos, Color.blue, $"Portal_{spawnId}_SpawnToNode");
            CreateDebugColumn(portal.transform.position, portal.transform.position + portal.transform.right * 2f,
                Color.red, $"Portal_{spawnId}_XAxis");

        }

        private void CreateDebugColumn(Vector3 start, Vector3 end, Color color, string name)
        {
            Vector3 flatStart = new Vector3(start.x, start.y, -0.5f);
            Vector3 flatEnd = new Vector3(end.x, end.y, -0.5f);
            Vector3 direction = flatEnd - flatStart;
            float length = direction.magnitude;

            if (length <= Mathf.Epsilon)
            {
                return;
            }

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = name;
            column.transform.SetParent(portalsParent, true);
            column.transform.position = flatStart + direction * 0.5f;
            column.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            column.transform.localScale = new Vector3(0.05f, length * 0.5f, 0.05f);

            Renderer renderer = column.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = CreateDebugMaterial(color);
                if (material != null)
                {
                    renderer.material = material;
                }
            }
        }

        private Material CreateDebugMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            else
            {
                material.color = color;
            }

            return material;
        }

        /// <summary>
        /// Removes the portal at the specified spawn point.
        /// </summary>
        private void RemovePortalAtSpawnPoint(char spawnId)
        {
            if (spawnPointPortals.TryGetValue(spawnId, out GameObject portal))
            {
                if (portal != null)
                {
                    // Use DestroyImmediate to ensure portal is removed before creating new ones
                    // This prevents duplicate portals when GrowMaze() is called rapidly
                    DestroyImmediate(portal);
                }
                spawnPointPortals.Remove(spawnId);
            }
        }

        /// <summary>
        /// Calculates the direction from a spawn point toward the nearest connected walkable tile.
        /// This is used to orient portals so they face inward toward the maze.
        /// Returns a unit direction vector (normalized grid coordinates).
        /// </summary>
        private Vector2Int GetDirectionToNearestWalkableTile(
            Vector2Int gridPos,
            out Vector3 facingVector,
            out Vector3 targetWorldPos,
            Vector3? targetOverride = null)
        {
            facingVector = Vector3.zero;
            targetWorldPos = Vector3.zero;

            // Check all 4 orthogonal directions for walkable tiles
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0),  // Left
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1)   // Down
            };

            Vector2Int heartPos = mazeGridBehaviour.HeartGridPos;
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);
            Vector3 heartWorldPos = mazeGridBehaviour.GridToWorld(heartPos.x, heartPos.y);
            targetWorldPos = targetOverride ?? heartWorldPos;
            if (TryGetNearestNodeCenterWorldPos(gridPos, out Vector3 nodeCenterWorldPos))
            {
                targetWorldPos = targetOverride ?? nodeCenterWorldPos;
            }
            Vector2 toHeart = new Vector2(heartWorldPos.x - spawnWorldPos.x, heartWorldPos.y - spawnWorldPos.y);
            float toHeartMagnitude = toHeart.sqrMagnitude > 0f ? toHeart.magnitude : 0f;
            float bestDot = float.NegativeInfinity;
            bool foundAdjacent = false;
            Vector2Int bestDirection = Vector2Int.zero;
            float bestHeartDistance = float.PositiveInfinity;

            foreach (var dir in directions)
            {
                Vector2Int checkPos = gridPos + dir;
                if (!mazeGridBehaviour.Grid.InBounds(checkPos.x, checkPos.y))
                {
                    continue;
                }

                var node = mazeGridBehaviour.Grid.GetNode(checkPos.x, checkPos.y);
                if (node == null || !node.walkable)
                {
                    continue;
                }

                Vector3 candidateWorldPos = mazeGridBehaviour.GridToWorld(checkPos.x, checkPos.y);
                Vector2 candidateVector = new Vector2(candidateWorldPos.x - spawnWorldPos.x, candidateWorldPos.y - spawnWorldPos.y);
                float dot = 0f;
                if (toHeartMagnitude > 0f && candidateVector.sqrMagnitude > 0f)
                {
                    Vector2 normalizedToHeart = toHeart / toHeartMagnitude;
                    Vector2 normalizedCandidate = candidateVector.normalized;
                    dot = Vector2.Dot(normalizedCandidate, normalizedToHeart);
                }

                float heartDistance = Vector2.Distance(new Vector2(candidateWorldPos.x, candidateWorldPos.y),
                    new Vector2(heartWorldPos.x, heartWorldPos.y));

                if (!foundAdjacent || dot > bestDot || (Mathf.Approximately(dot, bestDot) && heartDistance < bestHeartDistance))
                {
                    bestDot = dot;
                    bestHeartDistance = heartDistance;
                    bestDirection = dir;
                    foundAdjacent = true;
                }
            }

            if (foundAdjacent)
            {
                Vector2Int targetGridPos = gridPos + bestDirection;
                facingVector = (mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y) - spawnWorldPos).normalized;
                return bestDirection;
            }

            Vector2Int? nearestWalkable = null;
            int nearestDistance = int.MaxValue;
            int maxSearchRadius = 3;

            for (int radius = 2; radius <= maxSearchRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        Vector2Int checkPos = new Vector2Int(gridPos.x + dx, gridPos.y + dy);
                        if (!mazeGridBehaviour.Grid.InBounds(checkPos.x, checkPos.y))
                        {
                            continue;
                        }

                        var node = mazeGridBehaviour.Grid.GetNode(checkPos.x, checkPos.y);
                        if (node != null && node.walkable)
                        {
                            int distance = dx * dx + dy * dy;
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestWalkable = checkPos;
                            }
                        }
                    }
                }
            }

            if (nearestWalkable.HasValue)
            {
                Vector2Int offset = nearestWalkable.Value - gridPos;
                // Normalize to unit vector
                int nx = offset.x == 0 ? 0 : (offset.x > 0 ? 1 : -1);
                int ny = offset.y == 0 ? 0 : (offset.y > 0 ? 1 : -1);
                Vector2Int direction = new Vector2Int(nx, ny);
                facingVector = (mazeGridBehaviour.GridToWorld(nearestWalkable.Value.x, nearestWalkable.Value.y) - spawnWorldPos).normalized;
                return direction;
            }

            // Fallback: face toward the heart of the maze
            Vector2Int fallback = new Vector2Int(heartPos.x - gridPos.x, heartPos.y - gridPos.y);
            int fx = fallback.x == 0 ? 0 : (fallback.x > 0 ? 1 : -1);
            int fy = fallback.y == 0 ? 0 : (fallback.y > 0 ? 1 : -1);
            Vector2Int fallbackDir = new Vector2Int(fx, fy);
            facingVector = (targetWorldPos - spawnWorldPos).normalized;

            return fallbackDir;
        }

        private bool TryGetNearestNodeCenterWorldPos(Vector2Int gridPos, out Vector3 nodeWorldPos)
        {
            nodeWorldPos = Vector3.zero;
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return false;
            }

            int width = mazeGridBehaviour.Grid.Width;
            int height = mazeGridBehaviour.Grid.Height;
            bool[,] visited = new bool[width, height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            queue.Enqueue(gridPos);
            visited[gridPos.x, gridPos.y] = true;

            Vector2Int[] directions =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                var node = mazeGridBehaviour.Grid.GetNode(current.x, current.y);
                if (node != null && (node.symbol == 'N' || node.symbol == 'H'))
                {
                    nodeWorldPos = mazeGridBehaviour.GridToWorld(current.x, current.y);
                    return true;
                }

                foreach (var dir in directions)
                {
                    Vector2Int next = current + dir;
                    if (!mazeGridBehaviour.Grid.InBounds(next.x, next.y))
                    {
                        continue;
                    }

                    if (visited[next.x, next.y])
                    {
                        continue;
                    }

                    var nextNode = mazeGridBehaviour.Grid.GetNode(next.x, next.y);
                    if (nextNode == null || !nextNode.walkable)
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the next available spawn ID from the pool.
        /// </summary>
        private char GetNextAvailableSpawnId()
        {
            if (nextSpawnIdIndex >= availableSpawnIds.Length)
            {
                return '\0';
            }

            return availableSpawnIds[nextSpawnIdIndex++];
        }

        /// <summary>
        /// Ensures wall borders around the specified walkable tiles.
        /// Converts void (empty) tiles within borderWidth to walls.
        /// </summary>
        private void EnsureWallBordersAroundTiles(List<Vector2Int> walkableTiles, int borderWidth)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            var grid = mazeGridBehaviour.Grid;
            HashSet<Vector2Int> tilesToConvert = new HashSet<Vector2Int>();

            // For each walkable tile, mark all void tiles within borderWidth
            foreach (var tile in walkableTiles)
            {
                for (int dy = -borderWidth; dy <= borderWidth; dy++)
                {
                    for (int dx = -borderWidth; dx <= borderWidth; dx++)
                    {
                        int x = tile.x + dx;
                        int y = tile.y + dy;

                        if (x >= 0 && x < grid.Width && y >= 0 && y < grid.Height)
                        {
                            int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                            if (distance <= borderWidth)
                            {
                                var node = grid.GetNode(x, y);
                                if (node != null && node.symbol == ' ') // Void tile
                                {
                                    tilesToConvert.Add(new Vector2Int(x, y));
                                }
                            }
                        }
                    }
                }
            }

            // Convert void tiles to walls
            foreach (var pos in tilesToConvert)
            {
                var node = grid.GetNode(pos.x, pos.y);
                if (node != null)
                {
                    node.symbol = '#';
                    node.SetTerrain(TileType.TreeBramble);
                    node.walkable = false;
                }
            }

            if (tilesToConvert.Count > 0)
            {
                Debug.Log($"[DynamicGrowth] Added {tilesToConvert.Count} wall tiles for borders around new content");
            }
        }

        /// <summary>
        /// Marks the endpoints of partial frontier edges as walkable.
        /// This ensures spawn points can be placed at the far end of partial edges.
        /// Creates a continuous walkable path from the polyline to the endpoint.
        /// </summary>
        /// <param name="excludeEdges">Optional set of edge IDs to exclude (existing edges that shouldn't be re-processed)</param>
        private void MarkPartialEdgeEndpointsAsWalkable(HashSet<int> excludeEdges = null)
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return;

            var grid = mazeGridBehaviour.Grid;
            float scale = forestMapState.Scale;
            Vector2 offset = forestMapState.Offset;

            int markedCount = 0;
            int skippedCount = 0;
            foreach (int edgeId in forestMapState.Frontier)
            {
                // Skip existing edges if excludeEdges is provided (during growth)
                if (excludeEdges != null && excludeEdges.Contains(edgeId))
                {
                    skippedCount++;
                    continue;
                }

                var edge = forestMapState.Edges[edgeId];
                if (!edge.Partial || edge.PolylinePoints.Count < 2) continue;

                // Get the last two points in the polyline
                Vector2 secondToLastPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 2] * scale + offset;
                Vector2 endPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1] * scale + offset;

                int startX = Mathf.RoundToInt(secondToLastPoint.x);
                int startY = Mathf.RoundToInt(secondToLastPoint.y);
                int endX = Mathf.RoundToInt(endPoint.x);
                int endY = Mathf.RoundToInt(endPoint.y);

                // Draw a walkable line from the second-to-last point to the endpoint
                // This ensures continuous connectivity
                int dx = Mathf.Abs(endX - startX);
                int dy = Mathf.Abs(endY - startY);
                int sx = startX < endX ? 1 : -1;
                int sy = startY < endY ? 1 : -1;
                int err = dx - dy;

                int x = startX;
                int y = startY;

                while (true)
                {
                    // Mark only the current cell as walkable (1-cell wide path)
                    if (x >= 0 && x < grid.Width && y >= 0 && y < grid.Height)
                    {
                        var node = grid.GetNode(x, y);
                        if (node != null && !node.walkable)
                        {
                            node.walkable = true;
                            node.symbol = '.';
                            node.SetTerrain(TileType.Path);
                            markedCount++;
                        }
                    }

                    // Check if we've reached the endpoint
                    if (x == endX && y == endY)
                        break;

                    // Bresenham's line algorithm step
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

                Debug.Log($"[DynamicGrowth] Created walkable path for partial edge {edgeId} from ({startX},{startY}) to ({endX},{endY})");
            }

            if (markedCount > 0 || skippedCount > 0)
            {
                Debug.Log($"[DynamicGrowth] Processed edge endpoints: {markedCount} cells marked walkable, {skippedCount} existing edges skipped");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Manually triggers maze growth (ignores timer).
        /// </summary>
        public void TriggerManualGrowth()
        {
            GrowMaze();
        }

        /// <summary>
        /// Enables or disables automatic maze growth.
        /// </summary>
        public void SetAutoGrowth(bool enabled)
        {
            autoGrowth = enabled;
            if (enabled)
            {
                nextGrowthTime = Time.time + growthInterval;
            }
        }

        /// <summary>
        /// Sets the growth interval in seconds.
        /// </summary>
        public void SetGrowthInterval(float seconds)
        {
            growthInterval = Mathf.Max(1f, seconds);
        }

        #endregion
    }
}

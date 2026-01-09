using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Maze;

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

            // Find all walkable tiles on the edges
            for (int x = 0; x < width; x++)
            {
                // Top edge
                var topNode = mazeGridBehaviour.Grid.GetNode(x, 0);
                if (topNode != null && topNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(x, 0));
                }

                // Bottom edge
                var bottomNode = mazeGridBehaviour.Grid.GetNode(x, height - 1);
                if (bottomNode != null && bottomNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(x, height - 1));
                }
            }

            for (int y = 1; y < height - 1; y++)
            {
                // Left edge
                var leftNode = mazeGridBehaviour.Grid.GetNode(0, y);
                if (leftNode != null && leftNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(0, y));
                }

                // Right edge
                var rightNode = mazeGridBehaviour.Grid.GetNode(width - 1, y);
                if (rightNode != null && rightNode.walkable)
                {
                    edgeWalkableTiles.Add(new Vector2Int(width - 1, y));
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
            Debug.Log($"[DynamicGrowth] Successfully created node {newNodeId} at position ({newNode.Position.x:F2}, {newNode.Position.y:F2})");
            Debug.Log($"[DynamicGrowth] Node {newNodeId} has {newNode.IncidentEdges.Count} edges at angles: {string.Join(", ", newNode.UsedAngles.Select(a => $"{a * Mathf.Rad2Deg:F1}°"))}");
            Debug.Log($"[DynamicGrowth] Frontier count: {frontierCountBefore} → {forestMapState.Frontier.Count}");

            // Rasterize the new node and its edges to the existing grid
            var grid = mazeGridBehaviour.Grid;
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
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node != null)
                    {
                        node.symbol = gridArray[y, x];
                        if (gridArray[y, x] == '.' || gridArray[y, x] == 'N')
                        {
                            node.walkable = true;
                            node.SetTerrain(TileType.Path);
                        }
                    }
                }
            }

            // Remove old spawn point portals that are no longer partial edges
            RebuildSpawnPointsFromFrontier();

            // Refresh the maze renderer to show new tiles
            if (mazeRenderer != null)
            {
                mazeRenderer.RefreshMaze();
            }

            // Trigger all active visitors to recalculate their paths
            TriggerVisitorPathRecalculation();

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
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Removes portals for completed edges and creates portals for partial edges.
        /// </summary>
        private void RebuildSpawnPointsFromFrontier()
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return;

            var grid = mazeGridBehaviour.Grid;
            float scale = forestMapState.Scale;
            Vector2 offset = forestMapState.Offset;

            // Clear all existing spawn points from grid
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node != null && IsSpawnPointChar(node.symbol))
                    {
                        RemovePortalAtSpawnPoint(node.symbol);
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

                // Get the endpoint in grid coordinates
                Vector2 endPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1] * scale + offset;
                int ex = Mathf.RoundToInt(endPoint.x);
                int ey = Mathf.RoundToInt(endPoint.y);

                if (ex >= 0 && ex < grid.Width && ey >= 0 && ey < grid.Height)
                {
                    char spawnId = GetNextAvailableSpawnId();
                    if (spawnId == '\0') break; // No more spawn IDs available

                    UpdateTileSymbol(new Vector2Int(ex, ey), spawnId);

                    // Get connected node for portal orientation
                    var connectedNode = forestMapState.Nodes[edge.NodeA];
                    Vector2 nodeCenter = connectedNode.Position * scale + offset;
                    Vector2Int nodeCenterGrid = new Vector2Int(Mathf.RoundToInt(nodeCenter.x), Mathf.RoundToInt(nodeCenter.y));

                    CreatePortalAtSpawnPoint(spawnId, new Vector2Int(ex, ey), nodeCenterGrid);

                    spawnIndex++;
                }
            }

            // Rebuild the spawn points dictionary
            RebuildSpawnPointsDictionary();
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
                    visitor.RecalculatePath();
                    recalculatedCount++;
                }
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
                    Destroy(portal);
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

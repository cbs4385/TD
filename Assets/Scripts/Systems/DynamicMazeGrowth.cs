using System.Collections;
using System.Collections.Generic;
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

            // Log maze grid info
            if (mazeGridBehaviour != null && mazeGridBehaviour.Grid != null)
            {
                int spawnCount = mazeGridBehaviour.GetSpawnPointCount();
                Debug.Log($"[DynamicMazeGrowth] Maze initialized with {mazeGridBehaviour.Grid.Width}x{mazeGridBehaviour.Grid.Height} grid, {spawnCount} spawn points");

                var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
                foreach (var kvp in spawnPoints)
                {
                    Debug.Log($"[DynamicMazeGrowth] Spawn point '{kvp.Key}' at grid ({kvp.Value.x}, {kvp.Value.y})");
                }
            }
            else
            {
                Debug.LogError("[DynamicMazeGrowth] MazeGridBehaviour or Grid is null!");
            }

            // Initialize portals at existing spawn points
            InitializeSpawnPointPortals();

            // Schedule first growth
            if (autoGrowth)
            {
                nextGrowthTime = Time.time + growthInterval;
                Debug.Log($"[DynamicMazeGrowth] Auto-growth enabled. Next growth in {growthInterval} seconds");
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
                Debug.LogError("[DynamicMazeGrowth] MazeGridBehaviour is null! Cannot initialize portals.");
                return;
            }

            if (portalPrefab == null)
            {
                Debug.LogError("[DynamicMazeGrowth] Portal prefab is null! Portals will not be created. Please assign portal prefab in inspector.");
                return;
            }

            var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
            Debug.Log($"[DynamicMazeGrowth] Found {spawnPoints.Count} spawn points in maze grid");

            // If no spawn points exist, create them from edge walkable tiles
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("[DynamicMazeGrowth] No spawn points found! Creating spawn points from edge walkable tiles.");
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

            Debug.Log($"[DynamicMazeGrowth] Initialized {spawnPointPortals.Count} portals at spawn points");
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
                Debug.LogError("[DynamicMazeGrowth] No edge walkable tiles found! Cannot create spawn points.");
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

                Debug.Log($"[DynamicMazeGrowth] Created initial spawn point '{spawnId}' at ({pos.x}, {pos.y})");
            }

            // Rebuild spawn points dictionary
            RebuildSpawnPointsDictionary();

            Debug.Log($"[DynamicMazeGrowth] Created {numSpawnPoints} initial spawn points from edge tiles");
        }

        #endregion

        #region Maze Growth

        /// <summary>
        /// Grows the maze by adding a new node at one of the open endpoints.
        /// </summary>
        public void GrowMaze()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                Debug.LogWarning("[DynamicMazeGrowth] Cannot grow maze - grid not initialized");
                return;
            }

            // Get all current spawn points (open endpoints)
            var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("[DynamicMazeGrowth] Cannot grow maze - no spawn points available");
                return;
            }

            // Select a random spawn point to expand from
            var spawnPointsList = new List<KeyValuePair<char, Vector2Int>>(spawnPoints);
            int randomIndex = Random.Range(0, spawnPointsList.Count);
            var selectedSpawnPoint = spawnPointsList[randomIndex];
            char selectedSpawnId = selectedSpawnPoint.Key;
            Vector2Int selectedGridPos = selectedSpawnPoint.Value;

            Debug.Log($"[DynamicMazeGrowth] Growing maze from spawn point '{selectedSpawnId}' at grid position ({selectedGridPos.x}, {selectedGridPos.y})");

            // Remove portal from the selected spawn point
            RemovePortalAtSpawnPoint(selectedSpawnId);

            // Mark the tile as no longer a visitor entrance/exit
            UpdateTileSymbol(selectedGridPos, '.');

            // Generate new node and paths
            List<Vector2Int> newEndpoints = GenerateNewNodeFromEndpoint(selectedGridPos);

            // Assign spawn IDs to new endpoints and create portals
            foreach (var endpoint in newEndpoints)
            {
                char newSpawnId = GetNextAvailableSpawnId();
                if (newSpawnId != '\0')
                {
                    // Update tile symbol to spawn ID
                    UpdateTileSymbol(endpoint, newSpawnId);

                    // Create portal at new endpoint
                    CreatePortalAtSpawnPoint(newSpawnId, endpoint);

                    Debug.Log($"[DynamicMazeGrowth] Created new spawn point '{newSpawnId}' at ({endpoint.x}, {endpoint.y})");
                }
            }

            // Rebuild spawn points dictionary
            RebuildSpawnPointsDictionary();

            // Refresh the maze renderer to show new tiles
            if (mazeRenderer != null)
            {
                mazeRenderer.RefreshMaze();
            }

            Debug.Log($"[DynamicMazeGrowth] Maze growth complete. Added {newEndpoints.Count} new endpoints.");
        }

        /// <summary>
        /// Generates a new node with paths branching from the given endpoint.
        /// Returns list of new open endpoints created.
        /// </summary>
        private List<Vector2Int> GenerateNewNodeFromEndpoint(Vector2Int fromGridPos)
        {
            List<Vector2Int> newEndpoints = new List<Vector2Int>();

            // Determine direction to expand (away from maze center)
            Vector2Int heartPos = mazeGridBehaviour.HeartGridPos;
            Vector2Int direction = new Vector2Int(
                fromGridPos.x - heartPos.x,
                fromGridPos.y - heartPos.y
            );

            // Normalize to one of 4 cardinal directions
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                direction = new Vector2Int(direction.x > 0 ? 1 : -1, 0);
            }
            else
            {
                direction = new Vector2Int(0, direction.y > 0 ? 1 : -1);
            }

            // Calculate node position (3-5 tiles away from endpoint)
            int pathLength = Random.Range(3, 6);
            Vector2Int nodeGridPos = fromGridPos + direction * pathLength;

            // Ensure node position is within grid bounds
            nodeGridPos.x = Mathf.Clamp(nodeGridPos.x, 2, mazeGridBehaviour.Grid.Width - 3);
            nodeGridPos.y = Mathf.Clamp(nodeGridPos.y, 2, mazeGridBehaviour.Grid.Height - 3);

            // Carve path from endpoint to node
            CarvePath(fromGridPos, nodeGridPos);

            // Carve node clearing (3x3 area)
            CarveNodeClearing(nodeGridPos);

            // Create 1-3 new branches from this node
            int numBranches = Random.Range(1, 4);
            List<Vector2Int> usedDirections = new List<Vector2Int> { -direction }; // Can't go back

            for (int i = 0; i < numBranches; i++)
            {
                // Get random direction that hasn't been used
                Vector2Int branchDir = GetRandomUnusedDirection(usedDirections);
                if (branchDir == Vector2Int.zero)
                    break;

                usedDirections.Add(branchDir);

                // Create branch path
                int branchLength = Random.Range(3, 6);
                Vector2Int branchEndpoint = nodeGridPos + branchDir * branchLength;

                // Ensure endpoint is within bounds
                branchEndpoint.x = Mathf.Clamp(branchEndpoint.x, 1, mazeGridBehaviour.Grid.Width - 2);
                branchEndpoint.y = Mathf.Clamp(branchEndpoint.y, 1, mazeGridBehaviour.Grid.Height - 2);

                // Carve branch
                CarvePath(nodeGridPos, branchEndpoint);

                newEndpoints.Add(branchEndpoint);
            }

            return newEndpoints;
        }

        /// <summary>
        /// Carves a path between two grid positions.
        /// </summary>
        private void CarvePath(Vector2Int from, Vector2Int to)
        {
            // Use Bresenham's line algorithm to carve a straight path
            int x0 = from.x;
            int y0 = from.y;
            int x1 = to.x;
            int y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Carve 2-tile wide path (current tile + one adjacent)
                SetTileWalkable(x0, y0, '.');

                // Add width by carving adjacent tiles
                if (dx > dy)
                {
                    // Horizontal path - add vertical width
                    SetTileWalkable(x0, y0 + 1, '.');
                    SetTileWalkable(x0, y0 - 1, '.');
                }
                else
                {
                    // Vertical path - add horizontal width
                    SetTileWalkable(x0 + 1, y0, '.');
                    SetTileWalkable(x0 - 1, y0, '.');
                }

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        /// <summary>
        /// Carves a circular clearing around a node position.
        /// </summary>
        private void CarveNodeClearing(Vector2Int center)
        {
            int radius = 2;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Only carve if within circular radius
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        SetTileWalkable(center.x + dx, center.y + dy, '.');
                    }
                }
            }
        }

        /// <summary>
        /// Sets a tile to be walkable with the given symbol.
        /// </summary>
        private void SetTileWalkable(int x, int y, char symbol)
        {
            if (!mazeGridBehaviour.Grid.InBounds(x, y))
                return;

            var node = mazeGridBehaviour.Grid.GetNode(x, y);
            if (node != null)
            {
                node.walkable = true;
                node.symbol = symbol;
                node.SetTerrain(TileType.Path);
            }
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
        /// Gets a random cardinal direction that hasn't been used yet.
        /// </summary>
        private Vector2Int GetRandomUnusedDirection(List<Vector2Int> usedDirections)
        {
            List<Vector2Int> directions = new List<Vector2Int>
            {
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0),  // Left
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1)   // Down
            };

            // Remove used directions
            directions.RemoveAll(dir => usedDirections.Contains(dir));

            if (directions.Count == 0)
                return Vector2Int.zero;

            return directions[Random.Range(0, directions.Count)];
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
                Debug.LogError("[DynamicMazeGrowth] Could not access spawnPoints field");
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

            Debug.Log($"[DynamicMazeGrowth] Rebuilt spawn points dictionary with {spawnPoints.Count} entries");
        }

        #endregion

        #region Portal Management

        /// <summary>
        /// Creates a portal at the specified spawn point.
        /// </summary>
        private void CreatePortalAtSpawnPoint(char spawnId, Vector2Int gridPos)
        {
            if (portalPrefab == null)
            {
                Debug.LogWarning("[DynamicMazeGrowth] Cannot create portal - prefab not assigned");
                return;
            }

            // Remove existing portal at this spawn ID if it exists
            if (spawnPointPortals.ContainsKey(spawnId))
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Calculate direction to nearest walkable tile (toward maze interior)
            Vector2Int directionToMaze = GetDirectionToNearestWalkableTile(gridPos, out Vector3 facingVector);
            Vector3 directionToMaze3D = new Vector3(directionToMaze.x, directionToMaze.y, 0f).normalized;

            // Base world position at grid center
            Vector3 baseWorldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y, -portalHeightOffset);
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

            // Calculate portal position: center at wall edge toward maze interior
            Vector3 wallDirection = directionToMaze3D;
            float tileSize = mazeGridBehaviour.TileSize;

            // Position portal center at the wall edge of the tile
            Vector3 finalWorldPos = baseWorldPos + wallDirection * (tileSize * 0.5f);

            // Create rotation: +X axis points in direction visitors will travel (into maze)
            // This matches the visitor spawn direction
            Quaternion rotation;
            if (facingVector != Vector3.zero)
            {
                float angle = Mathf.Atan2(facingVector.y, facingVector.x) * Mathf.Rad2Deg;
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

            CreateDebugColumn(spawnWorldPos, mazeGridBehaviour.GridToWorld(gridPos.x + directionToMaze.x, gridPos.y + directionToMaze.y),
                Color.blue, $"Portal_{spawnId}_SpawnToNode");
            CreateDebugColumn(portal.transform.position, portal.transform.position + portal.transform.right * 2f,
                Color.red, $"Portal_{spawnId}_XAxis");

            Debug.Log($"[DynamicMazeGrowth] Created portal '{spawnId}' at grid ({gridPos.x}, {gridPos.y}), world pos {finalWorldPos}, +X facing {facingVector} (toward maze)");
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

                Debug.Log($"[DynamicMazeGrowth] Removed portal at spawn point '{spawnId}'");
            }
        }

        /// <summary>
        /// Calculates the direction from a spawn point toward the nearest connected walkable tile.
        /// This is used to orient portals so they face inward toward the maze.
        /// Returns a unit direction vector (normalized grid coordinates).
        /// </summary>
        private Vector2Int GetDirectionToNearestWalkableTile(Vector2Int gridPos, out Vector3 facingVector)
        {
            facingVector = Vector3.zero;

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
                Vector2Int direction = NormalizeToCardinal(offset);
                facingVector = (mazeGridBehaviour.GridToWorld(nearestWalkable.Value.x, nearestWalkable.Value.y)
                    - mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y)).normalized;
                Debug.LogWarning($"[DynamicMazeGrowth] No adjacent walkable tile for spawn point at ({gridPos.x}, {gridPos.y}); using nearest walkable tile at ({nearestWalkable.Value.x}, {nearestWalkable.Value.y})");
                return direction;
            }

            // Fallback: face toward the heart of the maze
            Vector2Int fallbackDir = NormalizeToCardinal(new Vector2Int(heartPos.x - gridPos.x, heartPos.y - gridPos.y));
            facingVector = (mazeGridBehaviour.GridToWorld(heartPos.x, heartPos.y)
                - mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y)).normalized;

            Debug.LogWarning($"[DynamicMazeGrowth] No adjacent walkable tile found for spawn point at ({gridPos.x}, {gridPos.y}), facing toward heart");
            return fallbackDir;
        }

        private Vector2Int NormalizeToCardinal(Vector2Int direction)
        {
            // Normalize to primary direction (prefer cardinal over diagonal)
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return new Vector2Int(direction.x > 0 ? 1 : -1, 0);
            }

            if (direction.y != 0)
            {
                return new Vector2Int(0, direction.y > 0 ? 1 : -1);
            }

            return new Vector2Int(1, 0); // Default to right if no direction
        }

        /// <summary>
        /// Gets the next available spawn ID from the pool.
        /// </summary>
        private char GetNextAvailableSpawnId()
        {
            if (nextSpawnIdIndex >= availableSpawnIds.Length)
            {
                Debug.LogWarning("[DynamicMazeGrowth] Ran out of available spawn IDs");
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

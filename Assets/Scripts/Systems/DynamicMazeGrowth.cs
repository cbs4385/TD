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
        private const int NodeRadiusTiles = 3;
        private const int EndpointClearanceRadius = 2;

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
        /// </summary>
        public void GrowMaze()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            // Get all current spawn points (open endpoints)
            var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();
            if (spawnPoints.Count == 0)
            {
                return;
            }

            // Select a random spawn point to expand from, retrying if a chosen endpoint is blocked.
            var spawnPointsList = new List<KeyValuePair<char, Vector2Int>>(spawnPoints);
            int startIndex = Random.Range(0, spawnPointsList.Count);
            Vector2Int nodeCenter = Vector2Int.zero;
            List<Vector2Int> newEndpoints = null;
            char selectedSpawnId = '\0';
            Vector2Int selectedGridPos = Vector2Int.zero;
            bool grewSuccessfully = false;

            for (int attempt = 0; attempt < spawnPointsList.Count; attempt++)
            {
                int index = (startIndex + attempt) % spawnPointsList.Count;
                var selectedSpawnPoint = spawnPointsList[index];
                selectedSpawnId = selectedSpawnPoint.Key;
                selectedGridPos = selectedSpawnPoint.Value;

                if (TryGenerateNewNodeFromEndpoint(selectedGridPos, out nodeCenter, out newEndpoints))
                {
                    grewSuccessfully = true;
                    break;
                }
            }

            if (!grewSuccessfully)
            {
                return;
            }

            // Remove portal from the selected spawn point
            RemovePortalAtSpawnPoint(selectedSpawnId);

            // Mark the tile as no longer a visitor entrance/exit
            UpdateTileSymbol(selectedGridPos, '.');

            // Assign spawn IDs to new endpoints and create portals
            foreach (var endpoint in newEndpoints)
            {
                char newSpawnId = GetNextAvailableSpawnId();
                if (newSpawnId != '\0')
                {
                    // Update tile symbol to spawn ID
                    UpdateTileSymbol(endpoint, newSpawnId);

                    // Create portal at new endpoint
                    CreatePortalAtSpawnPoint(newSpawnId, endpoint, nodeCenter);

                }
            }

            // Rebuild spawn points dictionary
            RebuildSpawnPointsDictionary();

            // Refresh the maze renderer to show new tiles
            if (mazeRenderer != null)
            {
                mazeRenderer.RefreshMaze();
            }

            // Trigger all active visitors to recalculate their paths
            // This is necessary because spawn points have changed - old destinations may no longer be valid spawn points
            TriggerVisitorPathRecalculation();

        }

        /// <summary>
        /// Generates a new node with paths branching from the given endpoint.
        /// Returns list of new open endpoints created.
        /// </summary>
        private bool TryGenerateNewNodeFromEndpoint(
            Vector2Int fromGridPos,
            out Vector2Int nodeGridPos,
            out List<Vector2Int> newEndpoints)
        {
            newEndpoints = new List<Vector2Int>();

            Vector2Int direction = GetOutwardDirectionFromEndpoint(fromGridPos);
            if (!TryPlaceNodeFromEndpoint(fromGridPos, direction, out nodeGridPos))
            {
                return false;
            }

            // Create 1-4 new branches from this node
            int numBranches = Random.Range(1, 5);
            List<Vector2Int> usedDirections = new List<Vector2Int> { -direction }; // Can't go back

            for (int i = 0; i < numBranches; i++)
            {
                if (!TryCreateBranch(nodeGridPos, usedDirections, out Vector2Int branchEndpoint, out Vector2Int branchDir))
                {
                    continue;
                }

                usedDirections.Add(branchDir);
                newEndpoints.Add(branchEndpoint);
            }

            SetTileWalkable(nodeGridPos.x, nodeGridPos.y, 'N');
            return true;
        }

        private bool TryPlaceNodeFromEndpoint(Vector2Int fromGridPos, Vector2Int outwardDirection, out Vector2Int nodeGridPos)
        {
            nodeGridPos = Vector2Int.zero;
            List<Vector2Int> candidateDirections = BuildRotatedDirections(outwardDirection);
            const int extraPlacementDistance = 4;

            foreach (var direction in candidateDirections)
            {
                for (int distance = NodeRadiusTiles; distance <= NodeRadiusTiles + extraPlacementDistance; distance++)
                {
                    Vector2Int candidateCenter = fromGridPos + direction * distance;

                    if (!IsNodeCenterInBounds(candidateCenter))
                    {
                        continue;
                    }

                    if (!IsPathClear(fromGridPos, candidateCenter, fromGridPos, fromGridPos, EndpointClearanceRadius))
                    {
                        continue;
                    }

                    if (!IsClearingAreaClear(candidateCenter, fromGridPos, 1))
                    {
                        continue;
                    }

                    nodeGridPos = candidateCenter;
                    SetTileWalkable(fromGridPos.x, fromGridPos.y, '.');
                    CarvePath(fromGridPos, nodeGridPos);
                    CarveNodeClearing(nodeGridPos);
                    return true;
                }
            }

            return false;
        }

        private bool TryCreateBranch(
            Vector2Int nodeGridPos,
            List<Vector2Int> usedDirections,
            out Vector2Int branchEndpoint,
            out Vector2Int branchDir)
        {
            branchEndpoint = Vector2Int.zero;
            branchDir = Vector2Int.zero;

            List<Vector2Int> candidateDirections = GetUnusedDirections(usedDirections);
            if (candidateDirections.Count == 0)
            {
                return false;
            }

            for (int attempt = 0; attempt < candidateDirections.Count; attempt++)
            {
                Vector2Int direction = candidateDirections[Random.Range(0, candidateDirections.Count)];
                candidateDirections.Remove(direction);

                for (int lengthAttempt = 0; lengthAttempt < 4; lengthAttempt++)
                {
                    int branchLength = Random.Range(3, 11);
                    Vector2Int candidateEndpoint = nodeGridPos + direction * (NodeRadiusTiles + branchLength);

                    if (!IsEndpointInBounds(candidateEndpoint))
                    {
                        continue;
                    }

                    if (!IsPathClear(nodeGridPos, candidateEndpoint, nodeGridPos, nodeGridPos, NodeRadiusTiles))
                    {
                        continue;
                    }

                    CarvePath(nodeGridPos, candidateEndpoint);
                    branchEndpoint = candidateEndpoint;
                    branchDir = direction;
                    return true;
                }
            }

            return false;
        }

        private Vector2Int GetOutwardDirectionFromEndpoint(Vector2Int endpoint)
        {
            Vector2Int[] directions =
            {
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, -1)
            };

            Vector2Int bestDirection = Vector2Int.zero;
            int bestClearance = -1;

            foreach (var direction in directions)
            {
                Vector2Int neighbor = endpoint + direction;
                if (!mazeGridBehaviour.Grid.InBounds(neighbor.x, neighbor.y))
                {
                    continue;
                }

                var neighborNode = mazeGridBehaviour.Grid.GetNode(neighbor.x, neighbor.y);
                if (neighborNode != null && neighborNode.walkable)
                {
                    continue;
                }

                int clearance = 0;
                for (int step = 1; step <= NodeRadiusTiles + 2; step++)
                {
                    Vector2Int check = endpoint + direction * step;
                    if (!mazeGridBehaviour.Grid.InBounds(check.x, check.y))
                    {
                        break;
                    }

                    var node = mazeGridBehaviour.Grid.GetNode(check.x, check.y);
                    if (node != null && node.walkable)
                    {
                        break;
                    }

                    clearance = step;
                }

                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    bestDirection = direction;
                }
            }

            if (bestClearance >= 0)
            {
                return bestDirection;
            }

            foreach (var direction in directions)
            {
                Vector2Int neighbor = endpoint + direction;
                if (!mazeGridBehaviour.Grid.InBounds(neighbor.x, neighbor.y))
                {
                    continue;
                }

                var node = mazeGridBehaviour.Grid.GetNode(neighbor.x, neighbor.y);
                if (node != null && node.walkable)
                {
                    return endpoint - neighbor;
                }
            }

            Vector2Int heartPos = mazeGridBehaviour.HeartGridPos;
            Vector2Int fallback = new Vector2Int(endpoint.x - heartPos.x, endpoint.y - heartPos.y);

            if (Mathf.Abs(fallback.x) > Mathf.Abs(fallback.y))
            {
                return new Vector2Int(fallback.x > 0 ? 1 : -1, 0);
            }

            return new Vector2Int(0, fallback.y > 0 ? 1 : -1);
        }

        private List<Vector2Int> BuildRotatedDirections(Vector2Int startDirection)
        {
            Vector2Int normalizedStart = NormalizeToCardinal(startDirection);
            List<Vector2Int> directions = new List<Vector2Int> { normalizedStart };

            Vector2Int right = RotateRight(normalizedStart);
            Vector2Int left = RotateLeft(normalizedStart);
            Vector2Int back = -normalizedStart;

            directions.Add(right);
            directions.Add(left);
            if (!directions.Contains(back))
            {
                directions.Add(back);
            }

            return directions.Distinct().ToList();
        }

        private List<Vector2Int> GetUnusedDirections(List<Vector2Int> usedDirections)
        {
            List<Vector2Int> directions = new List<Vector2Int>
            {
                // Cardinal directions
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0),  // Left
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                // Diagonal directions
                new Vector2Int(1, 1),   // Up-Right
                new Vector2Int(1, -1),  // Down-Right
                new Vector2Int(-1, 1),  // Up-Left
                new Vector2Int(-1, -1)  // Down-Left
            };

            directions.RemoveAll(dir => usedDirections.Contains(dir));
            return directions;
        }

        private Vector2Int RotateRight(Vector2Int direction)
        {
            return new Vector2Int(direction.y, -direction.x);
        }

        private Vector2Int RotateLeft(Vector2Int direction)
        {
            return new Vector2Int(-direction.y, direction.x);
        }

        private bool IsNodeCenterInBounds(Vector2Int center)
        {
            int minX = NodeRadiusTiles;
            int minY = NodeRadiusTiles;
            int maxX = mazeGridBehaviour.Grid.Width - 1 - NodeRadiusTiles;
            int maxY = mazeGridBehaviour.Grid.Height - 1 - NodeRadiusTiles;

            return center.x >= minX && center.x <= maxX && center.y >= minY && center.y <= maxY;
        }

        private bool IsEndpointInBounds(Vector2Int endpoint)
        {
            return endpoint.x >= 1 &&
                   endpoint.x <= mazeGridBehaviour.Grid.Width - 2 &&
                   endpoint.y >= 1 &&
                   endpoint.y <= mazeGridBehaviour.Grid.Height - 2;
        }

        private bool IsPathClear(
            Vector2Int from,
            Vector2Int to,
            Vector2Int allowedWalkable,
            Vector2Int? allowedCenter,
            int allowedRadius)
        {
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
                if (!IsPathCellClear(x0, y0, allowedWalkable, allowedCenter, allowedRadius, dx > dy))
                {
                    return false;
                }

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

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

            return true;
        }

        private bool IsPathCellClear(
            int x,
            int y,
            Vector2Int allowedWalkable,
            Vector2Int? allowedCenter,
            int allowedRadius,
            bool horizontalBias)
        {
            if (!IsCellClear(x, y, allowedWalkable, allowedCenter, allowedRadius))
            {
                return false;
            }

            if (horizontalBias)
            {
                if (!IsCellClear(x, y + 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x, y - 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x + 1, y + 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x - 1, y + 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x + 1, y - 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x - 1, y - 1, allowedWalkable, allowedCenter, allowedRadius))
                {
                    return false;
                }
            }
            else
            {
                if (!IsCellClear(x + 1, y, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x - 1, y, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x + 1, y + 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x - 1, y + 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x + 1, y - 1, allowedWalkable, allowedCenter, allowedRadius) ||
                    !IsCellClear(x - 1, y - 1, allowedWalkable, allowedCenter, allowedRadius))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsClearingAreaClear(Vector2Int center, Vector2Int allowedWalkable, int buffer)
        {
            int radius = NodeRadiusTiles;
            int outerRadius = radius + Mathf.Max(0, buffer);
            for (int dy = -outerRadius; dy <= outerRadius; dy++)
            {
                for (int dx = -outerRadius; dx <= outerRadius; dx++)
                {
                    if (dx * dx + dy * dy > outerRadius * outerRadius)
                    {
                        continue;
                    }

                    int x = center.x + dx;
                    int y = center.y + dy;
                    if (!IsCellClear(x, y, allowedWalkable, null, 0))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsCellClear(
            int x,
            int y,
            Vector2Int allowedWalkable,
            Vector2Int? allowedCenter,
            int allowedRadius)
        {
            if (!mazeGridBehaviour.Grid.InBounds(x, y))
            {
                return false;
            }

            if (x == allowedWalkable.x && y == allowedWalkable.y)
            {
                return true;
            }

            if (allowedCenter.HasValue)
            {
                int dx = x - allowedCenter.Value.x;
                int dy = y - allowedCenter.Value.y;
                if (dx * dx + dy * dy <= allowedRadius * allowedRadius)
                {
                    return true;
                }
            }

            var node = mazeGridBehaviour.Grid.GetNode(x, y);
            return node == null || !node.walkable;
        }

        /// <summary>
        /// Carves a narrow path between two grid positions using linear interpolation.
        /// Creates a 2-tile wide corridor to enable diagonal pathfinding without massive open areas.
        /// </summary>
        private void CarvePath(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;

            // For diagonal paths, use linear interpolation with narrow corridor
            if (dx > 0 && dy > 0)
            {
                // Calculate number of steps based on the distance
                int steps = Mathf.Max(dx, dy);
                int offsetX = 0;
                int offsetY = 0;

                if (dx >= dy)
                {
                    offsetY = sy == 0 ? 1 : sy;
                }
                else
                {
                    offsetX = sx == 0 ? 1 : sx;
                }

                for (int i = 0; i <= steps; i++)
                {
                    // Linear interpolation
                    float t = steps > 0 ? (float)i / steps : 0;
                    int x = Mathf.RoundToInt(from.x + t * (to.x - from.x));
                    int y = Mathf.RoundToInt(from.y + t * (to.y - from.y));

                    // Draw a 2-tile wide diagonal corridor
                    SetTileWalkable(x, y, '.');
                    SetTileWalkable(x + offsetX, y + offsetY, '.');
                }
            }
            else if (dx > dy)
            {
                // Primarily horizontal corridor - use Bresenham-style with perpendicular width
                int err = dx - dy;
                int x0 = from.x;
                int y0 = from.y;

                while (true)
                {
                    SetTileWalkable(x0, y0, '.');
                    SetTileWalkable(x0, y0 + 1, '.'); // Perpendicular width

                    if (x0 == to.x && y0 == to.y)
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
            else
            {
                // Primarily vertical corridor - use Bresenham-style with perpendicular width
                int err = dx - dy;
                int x0 = from.x;
                int y0 = from.y;

                while (true)
                {
                    SetTileWalkable(x0, y0, '.');
                    SetTileWalkable(x0 + 1, y0, '.'); // Perpendicular width

                    if (x0 == to.x && y0 == to.y)
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
        }

        /// <summary>
        /// Carves a circular clearing around a node position.
        /// </summary>
        private void CarveNodeClearing(Vector2Int center)
        {
            int radius = NodeRadiusTiles;
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
        /// Gets a random direction (cardinal or diagonal) that hasn't been used yet.
        /// </summary>
        private Vector2Int GetRandomUnusedDirection(List<Vector2Int> usedDirections)
        {
            List<Vector2Int> directions = new List<Vector2Int>
            {
                // Cardinal directions
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0),  // Left
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                // Diagonal directions
                new Vector2Int(1, 1),   // Up-Right
                new Vector2Int(1, -1),  // Down-Right
                new Vector2Int(-1, 1),  // Up-Left
                new Vector2Int(-1, -1)  // Down-Left
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
                Vector2Int direction = NormalizeDirection(offset);
                facingVector = (mazeGridBehaviour.GridToWorld(nearestWalkable.Value.x, nearestWalkable.Value.y) - spawnWorldPos).normalized;
                return direction;
            }

            // Fallback: face toward the heart of the maze
            Vector2Int fallbackDir = NormalizeDirection(new Vector2Int(heartPos.x - gridPos.x, heartPos.y - gridPos.y));
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

        private Vector2Int NormalizeDirection(Vector2Int direction)
        {
            // Normalize to unit vector, preserving diagonal directions
            int dx = direction.x;
            int dy = direction.y;

            // Normalize to -1, 0, or 1 for each component
            int nx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int ny = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            return new Vector2Int(nx, ny);
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

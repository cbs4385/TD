using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ForestMaze;

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
        private float growthInterval = 10f;

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

        private const float NodeRadius = 3.0f;  // Logical radius (matches NODE_RADIUS in PlanarForestMazeGenerator)
        private const float PathRadius = 0.5f;  // Logical path radius
        private const float WallBuffer = 1.0f;  // Logical wall buffer

        // Track portals at each spawn point
        private Dictionary<char, GameObject> spawnPointPortals = new Dictionary<char, GameObject>();

        // Track portal wall objects (blocking walls at frontier endpoints)
        private List<GameObject> portalWalls = new List<GameObject>();

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

            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
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
        /// Initializes spawn points and portals using the SAME logic as growth cycles.
        /// This ensures consistent behavior between initialization and dynamic growth.
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

            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null || forestMapState.Frontier.Count == 0)
            {
                // Debug.LogWarning("[DynamicGrowth] No ForestMapState or frontier edges - skipping spawn point initialization");
                return;
            }

            // Pure world-space - no grid-based connectivity needed
            RebuildSpawnPointsFromFrontier();
        }

        #endregion

        #region Maze Growth

        /// <summary>
        /// Grows the maze by adding a new node at one of the open endpoints.
        /// Uses the same generation logic as initial maze creation via PlanarForestMazeGenerator.Step().
        /// Pure world-space implementation - no grid-based code.
        /// </summary>
        public void GrowMaze()
        {
            if (mazeGridBehaviour == null)
            {
                // Debug.LogWarning("[DynamicGrowth] MazeGridBehaviour is null");
                return;
            }

            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null)
            {
                // Debug.LogWarning("[DynamicGrowth] ForestMapState is null - make sure planar generator is being used");
                return;
            }

            if (forestMapState.Frontier.Count == 0)
            {
                return;
            }

            // Pure world-space growth
            GrowMazeWorldSpace(forestMapState);
        }

        /// <summary>
        /// World-space version of GrowMaze.
        /// Works directly with world-space coordinates without any grid-based operations.
        /// Uses incremental rendering updates to avoid rebuilding the entire maze.
        /// </summary>
        private void GrowMazeWorldSpace(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            // Track frontier edge indices before the step to identify consumed spawn point
            // Frontier is a HashSet<int> containing edge indices
            var frontierIndicesBefore = new HashSet<int>(forestMapState.Frontier);
            int nodeCountBefore = forestMapState.Nodes.Count;
            int edgeCountBefore = forestMapState.Edges.Count;

            // Store endpoint positions for all frontier edges BEFORE step
            var frontierEndpoints = new Dictionary<int, Vector3>();
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (edgeIndex >= 0 && edgeIndex < forestMapState.Edges.Count)
                {
                    var edge = forestMapState.Edges[edgeIndex];
                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count > 0)
                    {
                        var endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                        frontierEndpoints[edgeIndex] = new Vector3(endpoint.x, endpoint.y, 0);
                    }
                }
            }

            // Use the same Step() method as initial generation to add a new node
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            if (!success)
            {
                // Debug.LogWarning("[DynamicGrowth] Step() failed - no valid placement found");
                return;
            }

            // Get the newly created node
            int newNodeId = nodeCountBefore;
            var newNode = forestMapState.Nodes[newNodeId];

            // Find the CONSUMED spawn point - the edge that was in frontier before but not after
            Vector3 consumedSpawnPos = Vector3.zero;
            int consumedEdgeIndex = -1;
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (!forestMapState.Frontier.Contains(edgeIndex))
                {
                    // This edge was consumed (removed from frontier)
                    consumedEdgeIndex = edgeIndex;
                    if (frontierEndpoints.TryGetValue(edgeIndex, out Vector3 endpoint))
                    {
                        consumedSpawnPos = endpoint;
                        // Debug.Log($"[DynamicGrowth] Consumed spawn at edge {edgeIndex}, position {consumedSpawnPos}");
                    }
                    break;
                }
            }

            // Find new/modified edges - ONLY the completed edge and new partial edges from this growth
            var newEdges = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();

            // Add the completed edge (was partial, now complete)
            if (consumedEdgeIndex >= 0 && consumedEdgeIndex < forestMapState.Edges.Count)
            {
                newEdges.Add(forestMapState.Edges[consumedEdgeIndex]);
            }

            // Add truly new edges (index >= edgeCountBefore)
            // Note: Merge and spacing operations now only modify these new edges,
            // never existing edges. So we don't need special tracking for wall regeneration.
            for (int i = edgeCountBefore; i < forestMapState.Edges.Count; i++)
            {
                newEdges.Add(forestMapState.Edges[i]);
            }

            // Capture old spawn point positions BEFORE regenerating WorldSpaceMazeData
            // GenerateFromGraph creates a new object with empty spawn points
            var oldSpawnPositions = new List<Vector3>();
            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                // Query real-time positions from portal transforms
                var positions = worldSpaceData.GetSpawnPointPositions();
                oldSpawnPositions.AddRange(positions.Values);
            }

            // Regenerate world-space maze data from updated graph
            // This is needed for pathfinding to work correctly
            if (worldSpaceData != null)
            {
                int oldTileCount = worldSpaceData.Tiles.Count;
                worldSpaceData = ForestMaze.WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, mazeGridBehaviour.WorldSpaceTileSize);

                var worldSpaceDataField = typeof(MazeGridBehaviour).GetField("worldSpaceMazeData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (worldSpaceDataField != null)
                {
                    worldSpaceDataField.SetValue(mazeGridBehaviour, worldSpaceData);
                }

                int walkableCount = worldSpaceData.Tiles.Count(t => t.Walkable);
                // Debug.Log($"[DynamicGrowth] Regenerated WorldSpaceMazeData: {oldTileCount} -> {worldSpaceData.Tiles.Count} tiles ({walkableCount} walkable)");
            }

            // Rebuild portals from frontier edges using world-space coordinates
            // This also registers spawn points and signals affected visitors to retarget
            // Pass the captured old spawn positions since WorldSpaceMazeData was regenerated
            RebuildSpawnPointsFromFrontier(oldSpawnPositions);

            // Note: TriggerVisitorPathRecalculation removed - it was redundant because:
            // 1. Affected visitors get new paths through SignalAffectedVisitorsToRetarget()
            // 2. Unaffected visitors have valid destinations and paths (maze only expands)

            // Use INCREMENTAL rendering updates instead of full rebuild
            if (mazeRenderer != null)
            {
                // 1. Remove walls near the consumed spawn point (to open the passage)
                // Use smaller radius (2f) to avoid removing node walls too far from the passage
                if (consumedSpawnPos != Vector3.zero)
                {
                    mazeRenderer.RemoveWallsNearPosition(consumedSpawnPos, 2f);
                }

                // 2. Remove walls and path tiles at the new node position (node radius + buffer)
                Vector3 newNodeWorldPos = new Vector3(newNode.Position.x, newNode.Position.y, 0);
                mazeRenderer.RemoveWallsNearPosition(newNodeWorldPos, 5f); // Node radius is ~3, plus border
                mazeRenderer.RemovePathTilesNearPosition(newNodeWorldPos, 4f); // Clear edge tiles to make room for node tiles

                // 3. Remove walls along all new edge paths (sample along segments, not just control points)
                // Only remove walls that are ON the walkable path - not the wall borders
                float wallRemovalStepSize = 0.5f; // Sample every 0.5 units for dense coverage
                float wallRemovalRadius = 0.7f; // Just slightly larger than pathHalfWidth (0.5) to clear the path
                float nodeBorderProtectionRadius = 4.5f; // nodeRadius (3) + wallBorder (3 * 0.3 = 0.9) + buffer

                foreach (var edge in newEdges)
                {
                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
                    {
                        for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                        {
                            Vector2 start = edge.PolylinePoints[i];
                            Vector2 end = edge.PolylinePoints[i + 1];
                            float segmentLength = Vector2.Distance(start, end);
                            int numSamples = Mathf.Max(2, Mathf.CeilToInt(segmentLength / wallRemovalStepSize));

                            for (int j = 0; j <= numSamples; j++)
                            {
                                float t = (float)j / numSamples;
                                Vector2 samplePoint = Vector2.Lerp(start, end, t);

                                // Skip wall removal if this point is near any existing node's border ring
                                // to protect existing node walls from being removed
                                bool nearExistingNode = false;
                                foreach (var node in forestMapState.Nodes)
                                {
                                    // Protect all nodes except the new one (whose walls will be added fresh)
                                    if (node.Id != newNode.Id)
                                    {
                                        float distToNode = Vector2.Distance(samplePoint, node.Position);
                                        if (distToNode < nodeBorderProtectionRadius)
                                        {
                                            nearExistingNode = true;
                                            break;
                                        }
                                    }
                                }

                                if (nearExistingNode)
                                    continue;

                                Vector3 pointWorldPos = new Vector3(samplePoint.x, samplePoint.y, 0);
                                mazeRenderer.RemoveWallsNearPosition(pointWorldPos, wallRemovalRadius);
                            }
                        }
                    }
                }

                // 4. Add tiles for the new node
                mazeRenderer.AddNodeTilesIncremental(newNode);

                // 5. Add tiles for new edges
                mazeRenderer.AddEdgeTilesIncremental(newEdges);

                // 6. Add walls around the new elements
                mazeRenderer.AddWallsIncremental(newEdges, newNode);
            }
        }

        /// <summary>
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Removes portals for completed edges and creates portals for partial edges.
        /// </summary>
        private void RebuildSpawnPointsFromFrontier()
        {
            // Called during initialization - no old spawn positions to track
            RebuildSpawnPointsFromFrontierWorldSpace(null);
        }

        /// <summary>
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Accepts pre-captured old spawn positions for detecting removed spawn points.
        /// </summary>
        private void RebuildSpawnPointsFromFrontier(List<Vector3> oldSpawnPositions)
        {
            RebuildSpawnPointsFromFrontierWorldSpace(oldSpawnPositions);
        }

        /// <summary>
        /// World-space version of RebuildSpawnPointsFromFrontier.
        /// Graph positions ARE world positions - no transforms needed.
        /// For frontier edges, the polyline defines the path - no flood fill needed.
        /// </summary>
        /// <param name="oldSpawnPositions">Pre-captured spawn positions from before WorldSpaceMazeData regeneration, or null for initialization</param>
        private void RebuildSpawnPointsFromFrontierWorldSpace(List<Vector3> oldSpawnPositions)
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return;

            // Use provided old spawn positions, or empty list if not provided (initialization)
            if (oldSpawnPositions == null)
            {
                oldSpawnPositions = new List<Vector3>();
            }

            // Clear spawn points in world-space data before registering new ones
            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                worldSpaceData.ClearSpawnPoints();
                // Debug.Log($"[DynamicGrowth] Cleared spawn points, frontier has {forestMapState.Frontier.Count} edges");
            }

            // Clear ALL existing portals
            var portalsToRemove = new List<char>(spawnPointPortals.Keys);
            foreach (char spawnId in portalsToRemove)
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Clear any orphaned portal objects
            if (portalsParent != null)
            {
                List<Transform> toDestroy = new List<Transform>();
                foreach (Transform child in portalsParent)
                {
                    if (child != null && child.name.StartsWith("Portal_"))
                    {
                        toDestroy.Add(child);
                    }
                }
                foreach (var child in toDestroy)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // Clear ALL existing portal walls (blocking walls at frontier endpoints)
            foreach (var wallObj in portalWalls)
            {
                if (wallObj != null)
                {
                    DestroyImmediate(wallObj);
                }
            }
            portalWalls.Clear();

            // Reset spawn ID index
            nextSpawnIdIndex = 0;

            // Place portals at partial edge endpoints (the actual frontier)
            int portalCount = 0;
            foreach (int edgeId in forestMapState.Frontier)
            {
                var edge = forestMapState.Edges[edgeId];
                if (!edge.Partial || edge.PolylinePoints.Count == 0) continue;

                // Get the connected node center (already in world space)
                var connectedNode = forestMapState.Nodes[edge.NodeA];
                Vector3 nodeCenterWorld = new Vector3(connectedNode.Position.x, connectedNode.Position.y, 0f);

                // Use the LAST polyline point (the actual frontier endpoint, already in world space)
                Vector2 endpointPos = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                Vector3 endpointWorld = new Vector3(endpointPos.x, endpointPos.y, 0f);

                // Calculate the actual path direction at the endpoint (from second-to-last to last point)
                // This handles curved edges correctly, unlike using the line from node center
                Vector2 directionOutward;
                if (edge.PolylinePoints.Count >= 2)
                {
                    Vector2 prevPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 2];
                    directionOutward = (endpointPos - prevPoint).normalized;
                }
                else
                {
                    // Fallback to node-to-endpoint direction
                    directionOutward = (endpointPos - connectedNode.Position).normalized;
                }

                // Wall is placed 0.5 units PAST the endpoint (away from node, to connect with surrounding walls)
                Vector3 wallOffset = new Vector3(directionOutward.x, directionOutward.y, 0f) * 0.5f;
                Vector3 wallWorldPos = endpointWorld + wallOffset;

                // Portal is placed 0.7 units INSIDE the path (toward node, at inside edge of final tile)
                Vector3 portalOffset = new Vector3(-directionOutward.x, -directionOutward.y, 0f) * 0.7f;
                Vector3 portalWorldPos = endpointWorld + portalOffset;

                // Get next spawn ID
                char spawnId = GetNextAvailableSpawnId();
                if (spawnId == '\0')
                {
                    // Debug.LogWarning("[DynamicGrowth] No more spawn IDs available");
                    break;
                }

                // Calculate orientation for wall (perpendicular to path direction to block the path)
                // Add 90 degrees to rotate from along-path to across-path orientation
                float orientationDegrees = Mathf.Atan2(directionOutward.y, directionOutward.x) * Mathf.Rad2Deg + 90f;

                // Calculate perpendicular direction for three-tile-wide wall section
                Vector3 perpendicular = new Vector3(-directionOutward.y, directionOutward.x, 0f);
                float tileSize = mazeGridBehaviour.WorldSpaceTileSize;

                // Create THREE walls PAST the portal to fully block the path exit (center, left, right)
                // Track these walls so they can be removed when frontier changes
                if (mazeRenderer != null)
                {
                    var wall1 = mazeRenderer.CreateWallAtPosition(wallWorldPos, orientationDegrees);
                    var wall2 = mazeRenderer.CreateWallAtPosition(wallWorldPos + perpendicular * tileSize, orientationDegrees);
                    var wall3 = mazeRenderer.CreateWallAtPosition(wallWorldPos - perpendicular * tileSize, orientationDegrees);
                    if (wall1 != null) portalWalls.Add(wall1);
                    if (wall2 != null) portalWalls.Add(wall2);
                    if (wall3 != null) portalWalls.Add(wall3);
                }

                // Create portal at the frontier endpoint
                // Pass the direction INTO the maze (opposite of outward) for facing
                Vector3 directionIntoMaze = new Vector3(-directionOutward.x, -directionOutward.y, 0f);
                CreatePortalAtWorldPosition(spawnId, portalWorldPos, directionIntoMaze);

                portalCount++;
            }

            // Log all registered spawn points before signaling visitors
            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            var finalSpawnPoints = mazeData?.GetSpawnPointPositions();
            if (finalSpawnPoints != null)
            {
                var spawnInfo = string.Join(", ", finalSpawnPoints.Select(kvp => $"{kvp.Key}:{kvp.Value:F1}"));
                // Debug.Log($"[DynamicGrowth] Registered {finalSpawnPoints.Count} spawn points: [{spawnInfo}]");
            }

            // Identify which spawn points were removed (old positions not present in new spawn points)
            var removedSpawnPositions = new List<Vector3>();
            var newSpawnPositions = finalSpawnPoints != null
                ? new HashSet<Vector3>(finalSpawnPoints.Values)
                : new HashSet<Vector3>();

            foreach (var oldPos in oldSpawnPositions)
            {
                // Check if this old spawn position is still present (within tolerance)
                bool stillExists = false;
                foreach (var newPos in newSpawnPositions)
                {
                    if (Vector3.Distance(oldPos, newPos) < 2f)
                    {
                        stillExists = true;
                        break;
                    }
                }
                if (!stillExists)
                {
                    removedSpawnPositions.Add(oldPos);
                }
            }

            if (removedSpawnPositions.Count > 0)
            {
                // Debug.Log($"[DynamicGrowth] {removedSpawnPositions.Count} spawn points were removed, retargeting affected visitors only");
            }

            // Only signal visitors whose destination was at a removed spawn point
            SignalAffectedVisitorsToRetarget(removedSpawnPositions);
        }

        /// <summary>
        /// Creates a portal at a specific world-space position with a facing direction.
        /// Also registers the spawn point at the portal's actual final position.
        /// </summary>
        /// <param name="spawnId">The spawn point ID</param>
        /// <param name="worldPos">The base world position for the portal</param>
        /// <param name="facingDirection">Normalized direction the portal should face (into the maze)</param>
        private void CreatePortalAtWorldPosition(char spawnId, Vector3 worldPos, Vector3 facingDirection)
        {
            if (portalPrefab == null)
            {
                // Debug.LogWarning($"[DynamicGrowth] CreatePortalAtWorldPosition: portalPrefab is null, cannot create portal {spawnId}");
                return;
            }

            // Remove existing portal at this spawn ID if it exists
            if (spawnPointPortals.ContainsKey(spawnId))
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Use the facing direction directly (already normalized)
            Vector3 direction = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : Vector3.right;

            // Apply height offset (Z coordinate) - no translation, portal stays at exact position
            Vector3 finalWorldPos = new Vector3(worldPos.x, worldPos.y, -portalHeightOffset);

            // Create rotation: +X axis points in the facing direction (into the maze)
            float zAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Apply Z rotation first (facing direction), then X=-90 to lay flat on XY plane
            Quaternion rotation = Quaternion.Euler(0f, 0f, zAngle) * Quaternion.Euler(-90f, 0f, 0f);

            // Create portal and set parent first, then set world position explicitly
            GameObject portal = Instantiate(portalPrefab);
            portal.name = $"Portal_{spawnId}";

            if (portalsParent != null)
            {
                portal.transform.SetParent(portalsParent, worldPositionStays: false);
            }

            // Set world position and rotation explicitly AFTER parenting
            portal.transform.position = finalWorldPos;
            portal.transform.rotation = rotation;

            // Track portal
            spawnPointPortals[spawnId] = portal;

            // Register spawn point with the portal's transform - position is queried in real-time
            var worldSpaceData = mazeGridBehaviour?.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                worldSpaceData.RegisterSpawnPoint(spawnId, portal.transform);
                // Debug.Log($"[DynamicGrowth] Registered spawn {spawnId} with portal transform at {portal.transform.position}");
            }

            // Create debug visualization
            CreateDebugColumn(worldPos, worldPos + direction * 2f, Color.blue, $"Portal_{spawnId}_FacingDir");
            CreateDebugColumn(portal.transform.position, portal.transform.position + portal.transform.right * 2f,
                Color.red, $"Portal_{spawnId}_XAxis");
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

        }

        /// <summary>
        /// Signals all visitors to recalculate their paths.
        /// Called after spawn points are updated.
        /// </summary>
        private void SignalVisitorsToRetarget()
        {
            var allVisitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (allVisitors == null)
            {
                return;
            }

            int retargetedCount = 0;
            foreach (var visitor in allVisitors)
            {
                if (visitor != null)
                {
                    // Retarget to nearest spawn point by walking distance
                    visitor.RetargetToNearestSpawn();
                    retargetedCount++;
                }
            }

        }

        /// <summary>
        /// Signals only visitors whose destination was at a removed spawn point to retarget.
        /// This avoids expensive pathfinding for visitors whose destinations are still valid.
        /// </summary>
        private void SignalAffectedVisitorsToRetarget(List<Vector3> removedSpawnPositions)
        {
            var allVisitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (allVisitors == null)
            {
                return;
            }

            // If no spawn points were removed, no visitors need to retarget
            if (removedSpawnPositions == null || removedSpawnPositions.Count == 0)
            {
                // Debug.Log($"[DynamicGrowth] No spawn points removed, skipping visitor retargeting");
                return;
            }

            int retargetedCount = 0;
            int skippedCount = 0;
            foreach (var visitor in allVisitors)
            {
                if (visitor == null) continue;

                // Get visitor's current destination
                Vector3 visitorDest = visitor.GetCurrentDestination();

                // Check if visitor's destination was at one of the removed spawn points
                bool destinationWasRemoved = false;
                foreach (var removedPos in removedSpawnPositions)
                {
                    if (Vector3.Distance(visitorDest, removedPos) < 3f)
                    {
                        destinationWasRemoved = true;
                        break;
                    }
                }

                if (destinationWasRemoved)
                {
                    // Retarget to nearest spawn point by walking distance
                    visitor.RetargetToNearestSpawn();
                    retargetedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            // Debug.Log($"[DynamicGrowth] Retargeted {retargetedCount} visitors with removed destinations, skipped {skippedCount} visitors with valid destinations");
        }

        #endregion

        #region Portal Management

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
            else
            {
                // Debug.LogWarning($"[DynamicGrowth] Attempted to remove portal '{spawnId}' but it was not found in dictionary");
            }
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

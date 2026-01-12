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
                Debug.LogWarning("[DynamicGrowth] No ForestMapState or frontier edges - skipping spawn point initialization");
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
                Debug.LogWarning("[DynamicGrowth] MazeGridBehaviour is null");
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
            // Track frontier edges before the step to identify consumed spawn point
            // Frontier is a HashSet<Edge>, so we copy it directly
            var frontierBefore = new HashSet<ForestMaze.PlanarForestMazeGenerator.Edge>(forestMapState.Frontier);
            int nodeCountBefore = forestMapState.Nodes.Count;
            int edgeCountBefore = forestMapState.Edges.Count;

            // Find the consumed spawn point position BEFORE the step
            Vector3 consumedSpawnPos = Vector3.zero;
            foreach (var edge in frontierBefore)
            {
                if (edge.PolylinePoints != null && edge.PolylinePoints.Count > 0)
                {
                    // The endpoint of a partial edge is the spawn point
                    var endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                    consumedSpawnPos = new Vector3(endpoint.x, endpoint.y, 0);
                    break; // Step will consume one of the frontier edges
                }
            }

            // Use the same Step() method as initial generation to add a new node
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            if (!success)
            {
                Debug.LogWarning("[DynamicGrowth] Step() failed - no valid placement found");
                return;
            }

            // Get the newly created node
            int newNodeId = nodeCountBefore;
            var newNode = forestMapState.Nodes[newNodeId];

            // Find new/modified edges (edges added after the step or changed from partial to complete)
            var newEdges = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();
            for (int i = 0; i < forestMapState.Edges.Count; i++)
            {
                var edge = forestMapState.Edges[i];
                // New edges (index >= edgeCountBefore)
                if (i >= edgeCountBefore)
                {
                    newEdges.Add(edge);
                }
                // Or edges that were in frontier before but now completed
                else if (frontierBefore.Contains(edge) && !forestMapState.Frontier.Contains(edge))
                {
                    newEdges.Add(edge);
                }
            }

            // Also add the new partial edges (current frontier edges that weren't there before)
            foreach (var edge in forestMapState.Frontier)
            {
                if (!frontierBefore.Contains(edge) && !newEdges.Contains(edge))
                {
                    newEdges.Add(edge);
                }
            }

            Debug.Log($"[DynamicGrowth] Added node {newNodeId}, {newEdges.Count} new/modified edges");

            // Regenerate world-space maze data from updated graph
            // This is needed for pathfinding to work correctly
            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                worldSpaceData = ForestMaze.WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, mazeGridBehaviour.WorldSpaceTileSize);

                var worldSpaceDataField = typeof(MazeGridBehaviour).GetField("worldSpaceMazeData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (worldSpaceDataField != null)
                {
                    worldSpaceDataField.SetValue(mazeGridBehaviour, worldSpaceData);
                }
            }

            // Rebuild portals from frontier edges using world-space coordinates
            // This also registers spawn points and signals visitors to retarget
            RebuildSpawnPointsFromFrontier();

            // Trigger visitor path recalculation after spawn points are registered
            TriggerVisitorPathRecalculation();

            // Use INCREMENTAL rendering updates instead of full rebuild
            if (mazeRenderer != null)
            {
                // 1. Remove walls near the consumed spawn point (to open the passage)
                if (consumedSpawnPos != Vector3.zero)
                {
                    mazeRenderer.RemoveWallsNearPosition(consumedSpawnPos, 3f);
                }

                // 2. Add tiles for the new node
                mazeRenderer.AddNodeTilesIncremental(newNode);

                // 3. Add tiles for new/modified edges
                mazeRenderer.AddEdgeTilesIncremental(newEdges);

                // 4. Add walls around the new elements
                mazeRenderer.AddWallsIncremental(newEdges, newNode);
            }
        }

        /// <summary>
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Removes portals for completed edges and creates portals for partial edges.
        /// </summary>
        private void RebuildSpawnPointsFromFrontier()
        {
            // Pure world-space implementation
            RebuildSpawnPointsFromFrontierWorldSpace();
        }

        /// <summary>
        /// World-space version of RebuildSpawnPointsFromFrontier.
        /// Graph positions ARE world positions - no transforms needed.
        /// For frontier edges, the polyline defines the path - no flood fill needed.
        /// </summary>
        private void RebuildSpawnPointsFromFrontierWorldSpace()
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return;

            // Clear spawn points in world-space data before registering new ones
            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                worldSpaceData.ClearSpawnPoints();
                Debug.Log($"[DynamicGrowth] Cleared spawn points, frontier has {forestMapState.Frontier.Count} edges");
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
                    Debug.LogWarning("[DynamicGrowth] No more spawn IDs available");
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

                // Mark wall positions as unwalkable in world-space data
                // (worldSpaceData already defined in outer scope)
                if (worldSpaceData != null)
                {
                    worldSpaceData.MarkUnwalkable(new Vector2(wallWorldPos.x, wallWorldPos.y));
                    worldSpaceData.MarkUnwalkable(new Vector2((wallWorldPos + perpendicular * tileSize).x, (wallWorldPos + perpendicular * tileSize).y));
                    worldSpaceData.MarkUnwalkable(new Vector2((wallWorldPos - perpendicular * tileSize).x, (wallWorldPos - perpendicular * tileSize).y));
                    worldSpaceData.RegisterSpawnPoint(spawnId, portalWorldPos);
                }

                // Create portal at the frontier endpoint
                CreatePortalAtWorldPosition(spawnId, portalWorldPos, nodeCenterWorld);

                portalCount++;
            }

            // Log all registered spawn points before signaling visitors
            var finalSpawnPoints = mazeGridBehaviour.WorldSpaceMazeData?.SpawnPoints;
            if (finalSpawnPoints != null)
            {
                var spawnInfo = string.Join(", ", finalSpawnPoints.Select(kvp => $"{kvp.Key}:{kvp.Value:F1}"));
                Debug.Log($"[DynamicGrowth] Registered {finalSpawnPoints.Count} spawn points: [{spawnInfo}]");
            }

            // Signal all visitors to recalculate paths based on the updated graph
            SignalVisitorsToRetarget();
        }

        /// <summary>
        /// Creates a portal at a specific world-space position (no grid coordinates).
        /// </summary>
        private void CreatePortalAtWorldPosition(char spawnId, Vector3 worldPos, Vector3 nodeCenterWorld)
        {
            if (portalPrefab == null)
            {
                Debug.LogWarning($"[DynamicGrowth] CreatePortalAtWorldPosition: portalPrefab is null, cannot create portal {spawnId}");
                return;
            }

            // Remove existing portal at this spawn ID if it exists
            if (spawnPointPortals.ContainsKey(spawnId))
            {
                RemovePortalAtSpawnPoint(spawnId);
            }

            // Calculate direction from portal to node center (for facing)
            Vector3 directionToNode = (nodeCenterWorld - worldPos).normalized;
            if (directionToNode == Vector3.zero)
            {
                directionToNode = Vector3.right; // Default facing
            }

            // Apply height offset (Z coordinate) and translate 0.6 units toward node center
            Vector3 finalWorldPos = new Vector3(worldPos.x, worldPos.y, -portalHeightOffset);
            finalWorldPos += new Vector3(directionToNode.x, directionToNode.y, 0f) * 0.6f;

            // Create rotation: +X axis points toward the node center
            float zAngle = Mathf.Atan2(directionToNode.y, directionToNode.x) * Mathf.Rad2Deg;

            // Apply Z rotation first (facing direction), then X=-90 to lay flat on XY plane
            // Quaternion multiplication order: A * B applies B first, then A
            // So Euler(0,0,zAngle) * Euler(-90,0,0) applies X=-90 first, then Z=zAngle in world space
            Quaternion rotation = Quaternion.Euler(0f, 0f, zAngle) * Quaternion.Euler(-90f, 0f, 0f);

            // Create portal and set parent first, then set world position explicitly
            // This avoids SetParent worldPositionStays issues with non-identity parent transforms
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

            // Create debug visualization
            CreateDebugColumn(worldPos, nodeCenterWorld, Color.blue, $"Portal_{spawnId}_ToNode");
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
                Debug.LogWarning($"[DynamicGrowth] Attempted to remove portal '{spawnId}' but it was not found in dictionary");
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

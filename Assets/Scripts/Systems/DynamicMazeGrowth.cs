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

        private const float NodeRadius = 3.0f;  // Logical radius (matches NODE_RADIUS in PlanarForestMazeGenerator)
        private const float PathRadius = 0.5f;  // Logical path radius
        private const float WallBuffer = 1.0f;  // Logical wall buffer

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

            Debug.Log($"[DynamicGrowth] Initializing spawn points from {forestMapState.Frontier.Count} frontier edges");

            // Pure world-space - no grid-based connectivity needed
            RebuildSpawnPointsFromFrontier();
            Debug.Log("[DynamicGrowth] Initialization complete using world-space coordinates");
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
                Debug.Log("[DynamicGrowth] No frontier edges available for growth");
                return;
            }

            // Pure world-space growth
            GrowMazeWorldSpace(forestMapState);
        }

        /// <summary>
        /// World-space version of GrowMaze.
        /// Works directly with world-space coordinates without any grid-based operations.
        /// </summary>
        private void GrowMazeWorldSpace(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            int frontierCountBefore = forestMapState.Frontier.Count;
            Debug.Log($"[DynamicGrowth-WorldSpace] Starting growth cycle with {frontierCountBefore} frontier edges");

            // Use the same Step() method as initial generation to add a new node
            int nodeCountBefore = forestMapState.Nodes.Count;
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            if (!success)
            {
                Debug.LogWarning("[DynamicGrowth-WorldSpace] Step() failed - no valid placement found");
                return;
            }

            // Get the newly created node
            int newNodeId = nodeCountBefore;
            var newNode = forestMapState.Nodes[newNodeId];

            // Pure world-space - no scale/offset transforms needed
            // Graph positions ARE world positions

            Debug.Log($"[DynamicGrowth-WorldSpace] Created node {newNodeId} at world position ({newNode.Position.x:F2}, {newNode.Position.y:F2})");
            Debug.Log($"[DynamicGrowth-WorldSpace] Frontier count: {frontierCountBefore} → {forestMapState.Frontier.Count}");

            // Regenerate world-space maze data from updated graph
            var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                // Update world-space data from the graph
                // The graph has already been updated by Step()
                worldSpaceData = ForestMaze.WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, mazeGridBehaviour.WorldSpaceTileSize);

                // Update the reference in MazeGridBehaviour using reflection
                var worldSpaceDataField = typeof(MazeGridBehaviour).GetField("worldSpaceMazeData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (worldSpaceDataField != null)
                {
                    worldSpaceDataField.SetValue(mazeGridBehaviour, worldSpaceData);
                }
            }

            // Rebuild portals from frontier edges using world-space coordinates
            var removedSpawnPoints = RebuildSpawnPointsFromFrontier();

            // Refresh the maze renderer to show new geometry
            if (mazeRenderer != null)
            {
                mazeRenderer.RefreshMaze();
            }

            // Trigger visitor path recalculation
            TriggerVisitorPathRecalculation();

            // Signal visitors to retarget from removed exits
            if (removedSpawnPoints != null && removedSpawnPoints.Count > 0)
            {
                SignalVisitorsToRetargetFromRemovedExits(removedSpawnPoints);
            }

            Debug.Log("[DynamicGrowth-WorldSpace] Growth cycle complete");
        }

        /// <summary>
        /// Rebuilds spawn points from the frontier edges in the ForestMapState.
        /// Removes portals for completed edges and creates portals for partial edges.
        /// Returns list of removed spawn points for deferred visitor retargeting.
        /// </summary>
        private List<Vector2Int> RebuildSpawnPointsFromFrontier()
        {
            // Pure world-space implementation - no legacy grid code
            return RebuildSpawnPointsFromFrontierWorldSpace();
        }

        /// <summary>
        /// World-space version of RebuildSpawnPointsFromFrontier.
        /// Graph positions ARE world positions - no transforms needed.
        /// For frontier edges, the polyline defines the path - no flood fill needed.
        /// </summary>
        private List<Vector2Int> RebuildSpawnPointsFromFrontierWorldSpace()
        {
            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null) return new List<Vector2Int>();

            // No scale/offset/tileSize - graph positions ARE world positions

            // Clear ALL existing portals
            var portalsToRemove = new List<char>(spawnPointPortals.Keys);
            Debug.Log($"[DynamicGrowth-WorldSpace] Portal dictionary contains {spawnPointPortals.Count} entries before clearing: [{string.Join(", ", portalsToRemove.Select(id => $"'{id}'"))}]");
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
                if (toDestroy.Count > 0)
                {
                    Debug.Log($"[DynamicGrowth-WorldSpace] Destroyed {toDestroy.Count} orphaned portal objects");
                }
            }

            // Reset spawn ID index
            nextSpawnIdIndex = 0;

            // Place portals at partial edge endpoints
            // Graph positions ARE world positions - no transform needed
            int portalCount = 0;
            foreach (int edgeId in forestMapState.Frontier)
            {
                var edge = forestMapState.Edges[edgeId];
                if (!edge.Partial || edge.PolylinePoints.Count == 0) continue;

                // Get the connected node center - graph position IS world position
                var connectedNode = forestMapState.Nodes[edge.NodeA];
                Vector3 nodeCenterWorld = new Vector3(connectedNode.Position.x, connectedNode.Position.y, 0f);

                // Get the endpoint (last point in polyline) - graph position IS world position
                Vector2 endpointPos = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                Vector3 endpointWorld = new Vector3(endpointPos.x, endpointPos.y, 0f);

                Debug.Log($"[DynamicGrowth-WorldSpace] Edge {edgeId}: Node {edge.NodeA} at world {nodeCenterWorld}, endpoint at world {endpointWorld}, {edge.PolylinePoints.Count} polyline points");

                // Get next spawn ID
                char spawnId = GetNextAvailableSpawnId();
                if (spawnId == '\0')
                {
                    Debug.LogWarning("[DynamicGrowth-WorldSpace] No more spawn IDs available");
                    break;
                }

                // Create portal at the world-space endpoint position
                // No grid tests needed - the polyline IS the path, endpoint is always reachable
                CreatePortalAtWorldPosition(spawnId, endpointWorld, nodeCenterWorld);

                // Also update spawn points in the world-space data
                var worldSpaceData = mazeGridBehaviour.WorldSpaceMazeData;
                if (worldSpaceData != null)
                {
                    // Register the spawn point at this world position
                    worldSpaceData.RegisterSpawnPoint(spawnId, endpointWorld);
                }

                Debug.Log($"[DynamicGrowth-WorldSpace] Placed portal '{spawnId}' at world position {endpointWorld}");
                portalCount++;
            }

            Debug.Log($"[DynamicGrowth-WorldSpace] Created {portalCount} portals for {forestMapState.Frontier.Count} frontier edges");
            Debug.Log($"[DynamicGrowth-WorldSpace] Portal dictionary now contains {spawnPointPortals.Count} entries: [{string.Join(", ", spawnPointPortals.Keys.Select(id => $"'{id}'"))}]");

            // In pure world-space mode, we don't track grid positions for retargeting
            // Visitors will recalculate paths based on the updated graph
            return new List<Vector2Int>();
        }

        /// <summary>
        /// Creates a portal at a specific world-space position (no grid coordinates).
        /// </summary>
        private void CreatePortalAtWorldPosition(char spawnId, Vector3 worldPos, Vector3 nodeCenterWorld)
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

            // Calculate direction from portal to node center (for facing)
            Vector3 directionToNode = (nodeCenterWorld - worldPos).normalized;
            if (directionToNode == Vector3.zero)
            {
                directionToNode = Vector3.right; // Default facing
            }

            // Apply height offset
            Vector3 finalWorldPos = new Vector3(worldPos.x, worldPos.y, -portalHeightOffset);

            // Create rotation: +X axis points toward the node center
            float angle = Mathf.Atan2(directionToNode.y, directionToNode.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            // Apply maze coordinate system rotation (-90 around X to match 2D plane)
            rotation = rotation * Quaternion.Euler(-90f, 0f, 0f);

            // Instantiate portal
            GameObject portal = Instantiate(portalPrefab, finalWorldPos, rotation, portalsParent);
            portal.name = $"Portal_{spawnId}";

            // Track portal
            spawnPointPortals[spawnId] = portal;

            Debug.Log($"[DynamicGrowth-WorldSpace] Created portal '{spawnId}' at world {finalWorldPos}, facing toward {nodeCenterWorld}");

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
                    Debug.Log($"[DynamicGrowth] Removing portal '{spawnId}' at {portal.transform.position}");
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

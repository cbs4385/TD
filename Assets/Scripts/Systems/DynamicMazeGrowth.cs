using System.Collections;
using System.Collections.Generic;
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

        [SerializeField]
        [Tooltip("Number of growth stages to complete before the game/UI starts")]
        private int initialGrowthStages = 5;

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
        private int completedInitialGrowthStages = 0;
        private bool initialGrowthComplete = false;

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

        #region Events

        /// <summary>
        /// Event invoked when all initial growth stages have completed.
        /// Subscribe to this event to know when the maze is ready for gameplay.
        /// </summary>
        public event System.Action OnInitialGrowthComplete;

        #endregion

        #region Properties

        /// <summary>
        /// Returns true when all initial growth stages have completed and the maze is ready for gameplay.
        /// </summary>
        public bool IsInitialGrowthComplete => initialGrowthComplete;

        /// <summary>
        /// Returns the number of initial growth stages that have been completed.
        /// </summary>
        public int CompletedInitialGrowthStages => completedInitialGrowthStages;

        /// <summary>
        /// Returns the total number of initial growth stages configured.
        /// </summary>
        public int TotalInitialGrowthStages => initialGrowthStages;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();
            mazeRenderer = GetComponent<MazeRenderer>();

            // Create portals parent early so it's available for initial growth
            if (portalsParent == null)
            {
                GameObject portalsObj = new GameObject("Portals");
                portalsObj.transform.SetParent(transform);
                portalsObj.transform.localPosition = Vector3.zero;
                portalsParent = portalsObj.transform;
            }

            // Run initial growth stages synchronously BEFORE the first frame renders
            if (mazeGridBehaviour != null && mazeGridBehaviour.ForestMapState != null && initialGrowthStages > 0)
            {
                RunInitialGrowthStagesSynchronous();
            }
            else if (initialGrowthStages <= 0)
            {
                initialGrowthComplete = true;
            }
        }

        private void Start()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                return;
            }

            // Initialize portals at existing spawn points (after initial growth is complete)
            InitializeSpawnPointPortals();

            // Fire the initial growth complete event (deferred to Start so listeners can subscribe in Awake)
            if (initialGrowthComplete)
            {
                OnInitialGrowthComplete?.Invoke();
            }

            // Schedule regular auto-growth
            if (autoGrowth)
            {
                nextGrowthTime = Time.time + growthInterval;
            }
        }

        /// <summary>
        /// Runs the configured number of initial growth stages SYNCHRONOUSLY before the first frame.
        /// This ensures the maze is fully grown before anything is rendered.
        /// </summary>
        private void RunInitialGrowthStagesSynchronous()
        {
            Debug.Log($"[DynamicGrowth] Running {initialGrowthStages} initial growth stages synchronously...");

            var forestMapState = mazeGridBehaviour.ForestMapState;

            for (int i = 0; i < initialGrowthStages; i++)
            {
                if (forestMapState.Frontier.Count == 0)
                {
                    Debug.Log($"[DynamicGrowth] No more frontier edges at stage {i + 1}. Stopping early.");
                    break;
                }

                Debug.Log($"[DynamicGrowth] Initial growth stage {i + 1}/{initialGrowthStages}");
                GrowMazeSynchronous(forestMapState);
                completedInitialGrowthStages = i + 1;
            }

            Debug.Log($"[DynamicGrowth] All {completedInitialGrowthStages} initial growth stages complete (synchronous).");
            initialGrowthComplete = true;
        }

        /// <summary>
        /// Synchronous version of maze growth for initial stages.
        /// Runs all growth logic in a single frame without yielding.
        /// </summary>
        private void GrowMazeSynchronous(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            // Track state before step
            var frontierIndicesBefore = new HashSet<int>(forestMapState.Frontier);
            int nodeCountBefore = forestMapState.Nodes.Count;
            int edgeCountBefore = forestMapState.Edges.Count;

            // Store endpoint positions and frontier directions for all frontier edges BEFORE step
            var frontierEndpoints = new Dictionary<int, Vector3>();
            var frontierDirections = new Dictionary<int, Vector3>();
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (edgeIndex >= 0 && edgeIndex < forestMapState.Edges.Count)
                {
                    var edge = forestMapState.Edges[edgeIndex];
                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
                    {
                        var endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                        var prevPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 2];
                        frontierEndpoints[edgeIndex] = new Vector3(endpoint.x, endpoint.y, 0);
                        Vector2 dir = (endpoint - prevPoint).normalized;
                        frontierDirections[edgeIndex] = new Vector3(dir.x, dir.y, 0);
                    }
                }
            }

            // Step the graph
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);
            if (!success)
            {
                Debug.Log("[DynamicGrowth] Synchronous growth step failed.");
                return;
            }

            // Get the newly created node
            int newNodeId = nodeCountBefore;
            var newNode = forestMapState.Nodes[newNodeId];

            // Find the CONSUMED spawn point
            Vector3 consumedSpawnPos = Vector3.zero;
            Vector3 consumedFrontierDir = Vector3.zero;
            int consumedEdgeIndex = -1;
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (!forestMapState.Frontier.Contains(edgeIndex))
                {
                    consumedEdgeIndex = edgeIndex;
                    if (frontierEndpoints.TryGetValue(edgeIndex, out Vector3 endpoint))
                    {
                        consumedSpawnPos = endpoint;
                    }
                    if (frontierDirections.TryGetValue(edgeIndex, out Vector3 dir))
                    {
                        consumedFrontierDir = dir;
                    }
                    break;
                }
            }

            // Find new/modified edges
            ForestMaze.PlanarForestMazeGenerator.Edge completedEdge = null;
            var newEdges = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();

            if (consumedEdgeIndex >= 0 && consumedEdgeIndex < forestMapState.Edges.Count)
            {
                completedEdge = forestMapState.Edges[consumedEdgeIndex];
            }

            // Identify cross-connection target nodes
            var crossConnectionTargetNodes = new List<ForestMaze.PlanarForestMazeGenerator.Node>();
            for (int i = edgeCountBefore; i < forestMapState.Edges.Count; i++)
            {
                var edge = forestMapState.Edges[i];
                newEdges.Add(edge);

                if (!edge.Partial && edge.NodeB.HasValue && edge.NodeB.Value < nodeCountBefore)
                {
                    var targetNode = forestMapState.Nodes[edge.NodeB.Value];
                    crossConnectionTargetNodes.Add(targetNode);
                }
            }

            // All edges that need path tiles
            var allEdgesForPathTiles = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();
            if (completedEdge != null) allEdgesForPathTiles.Add(completedEdge);
            allEdgesForPathTiles.AddRange(newEdges);

            // Regenerate world-space maze data
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

            // Render the new elements (synchronous)
            if (mazeRenderer != null)
            {
                // Remove end cap walls
                if (consumedSpawnPos != Vector3.zero && consumedFrontierDir != Vector3.zero)
                {
                    mazeRenderer.RemoveWallsPastEndpoint(consumedSpawnPos, consumedFrontierDir, 2.5f, -0.3f);
                }

                // Remove existing walls along the new edge paths BEFORE adding new walls
                // This clears walls from previous edges that would intersect with new paths
                foreach (var newEdge in newEdges)
                {
                    if (newEdge.PolylinePoints != null && newEdge.PolylinePoints.Count >= 2)
                    {
                        mazeRenderer.RemoveWallsAlongPolyline(newEdge.PolylinePoints, PathRadius + 0.5f);
                    }
                }

                // Regenerate cross-connection walls
                foreach (var targetNode in crossConnectionTargetNodes)
                {
                    mazeRenderer.RegenerateNodeWalls(targetNode, forestMapState.Edges);
                }

                // Remove path tiles around new node
                Vector3 newNodeWorldPos = new Vector3(newNode.Position.x, newNode.Position.y, 0);
                mazeRenderer.RemovePathTilesNearPosition(newNodeWorldPos, 4f);

                // Add edge tiles
                mazeRenderer.AddEdgeTilesIncremental(allEdgesForPathTiles);

                // Add node tiles
                mazeRenderer.AddNodeTilesIncremental(newNode);

                // Add walls (synchronous version)
                mazeRenderer.AddWallsIncremental(newEdges, newNode);
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
                return;
            }

            var forestMapState = mazeGridBehaviour.ForestMapState;
            if (forestMapState == null)
            {
                return;
            }

            if (forestMapState.Frontier.Count == 0)
            {
                return;
            }

            // Pure world-space growth
            StartCoroutine(GrowMazeWorldSpaceAsync(forestMapState));
        }

        /// <summary>
        /// World-space version of GrowMaze (coroutine-based for non-blocking execution).
        /// Works directly with world-space coordinates without any grid-based operations.
        /// Uses incremental rendering updates to avoid rebuilding the entire maze.
        /// Spreads rendering work across multiple frames to prevent game lockup.
        /// </summary>
        private IEnumerator GrowMazeWorldSpaceAsync(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            float startTime = Time.realtimeSinceStartup;
            float lastTime = startTime;
            Debug.Log($"[DynamicGrowth] Growth step START at {startTime:F4}s");

            // Track frontier edge indices before the step to identify consumed spawn point
            // Frontier is a HashSet<int> containing edge indices
            var frontierIndicesBefore = new HashSet<int>(forestMapState.Frontier);
            int nodeCountBefore = forestMapState.Nodes.Count;
            int edgeCountBefore = forestMapState.Edges.Count;

            // Store endpoint positions and frontier directions for all frontier edges BEFORE step
            var frontierEndpoints = new Dictionary<int, Vector3>();
            var frontierDirections = new Dictionary<int, Vector3>();
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (edgeIndex >= 0 && edgeIndex < forestMapState.Edges.Count)
                {
                    var edge = forestMapState.Edges[edgeIndex];
                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
                    {
                        var endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                        var prevPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 2];
                        frontierEndpoints[edgeIndex] = new Vector3(endpoint.x, endpoint.y, 0);
                        Vector2 dir = (endpoint - prevPoint).normalized;
                        frontierDirections[edgeIndex] = new Vector3(dir.x, dir.y, 0);
                    }
                }
            }

            float preStepTime = Time.realtimeSinceStartup;
            Debug.Log($"[Timing] Pre-step setup: {(preStepTime - lastTime) * 1000:F2}ms");
            lastTime = preStepTime;

            // Use the same Step() method as initial generation to add a new node
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            float postStepTime = Time.realtimeSinceStartup;
            Debug.Log($"[Timing] Step(): {(postStepTime - lastTime) * 1000:F2}ms");
            lastTime = postStepTime;

            if (!success)
            {
                float failedEndTime = Time.realtimeSinceStartup;
                Debug.Log($"[DynamicGrowth] Growth step END (failed) at {failedEndTime:F4}s (duration: {(failedEndTime - startTime) * 1000:F2}ms)");
                yield break;
            }

            // Get the newly created node
            int newNodeId = nodeCountBefore;
            var newNode = forestMapState.Nodes[newNodeId];

            // Find the CONSUMED spawn point - the edge that was in frontier before but not after
            Vector3 consumedSpawnPos = Vector3.zero;
            Vector3 consumedFrontierDir = Vector3.zero;
            int consumedEdgeIndex = -1;
            foreach (int edgeIndex in frontierIndicesBefore)
            {
                if (!forestMapState.Frontier.Contains(edgeIndex))
                {
                    consumedEdgeIndex = edgeIndex;
                    if (frontierEndpoints.TryGetValue(edgeIndex, out Vector3 endpoint))
                    {
                        consumedSpawnPos = endpoint;
                    }
                    if (frontierDirections.TryGetValue(edgeIndex, out Vector3 dir))
                    {
                        consumedFrontierDir = dir;
                    }
                    break;
                }
            }

            // Find new/modified edges - separate completed edge from truly new edges
            // The completed edge already has walls rendered - we only need to render path tiles for it
            // Truly new edges need both path tiles AND walls
            ForestMaze.PlanarForestMazeGenerator.Edge completedEdge = null;
            var newEdges = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();

            // The completed edge (was partial, now complete) - already has walls, just needs path tiles
            if (consumedEdgeIndex >= 0 && consumedEdgeIndex < forestMapState.Edges.Count)
            {
                completedEdge = forestMapState.Edges[consumedEdgeIndex];
            }

            // Truly new edges (index >= edgeCountBefore) - need both path tiles AND walls
            // Also identify cross-connection target nodes (existing nodes that receive a new edge)
            var crossConnectionTargetNodes = new List<ForestMaze.PlanarForestMazeGenerator.Node>();
            for (int i = edgeCountBefore; i < forestMapState.Edges.Count; i++)
            {
                var edge = forestMapState.Edges[i];
                newEdges.Add(edge);

                // Check if this is a cross-connection to an existing node
                // Cross-connection: NodeB is an existing node (not the new node, not a partial edge)
                if (!edge.Partial && edge.NodeB.HasValue && edge.NodeB.Value < nodeCountBefore)
                {
                    // This edge connects to an existing node - we need to regenerate that node's walls
                    // The edge has already been added to NodeB's EdgeAngles by PlanarForestMazeGenerator
                    var targetNode = forestMapState.Nodes[edge.NodeB.Value];
                    crossConnectionTargetNodes.Add(targetNode);
                    Debug.Log($"[CrossConnection] Edge {edge.Id}: NodeA={edge.NodeA} -> NodeB={edge.NodeB.Value} (existing). Will regenerate NodeB walls.");
                }
            }

            // All edges that need path tiles rendered (completed + new)
            var allEdgesForPathTiles = new List<ForestMaze.PlanarForestMazeGenerator.Edge>();
            if (completedEdge != null) allEdgesForPathTiles.Add(completedEdge);
            allEdgesForPathTiles.AddRange(newEdges);

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
                worldSpaceData = ForestMaze.WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, mazeGridBehaviour.WorldSpaceTileSize);

                var worldSpaceDataField = typeof(MazeGridBehaviour).GetField("worldSpaceMazeData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (worldSpaceDataField != null)
                {
                    worldSpaceDataField.SetValue(mazeGridBehaviour, worldSpaceData);
                }
            }

            float postGenerateTime = Time.realtimeSinceStartup;
            Debug.Log($"[Timing] GenerateFromGraph: {(postGenerateTime - lastTime) * 1000:F2}ms");
            lastTime = postGenerateTime;

            // Rebuild portals from frontier edges using world-space coordinates
            // This also registers spawn points and signals affected visitors to retarget
            // Pass the captured old spawn positions since WorldSpaceMazeData was regenerated
            RebuildSpawnPointsFromFrontier(oldSpawnPositions);

            float postRebuildTime = Time.realtimeSinceStartup;
            Debug.Log($"[Timing] RebuildSpawnPoints: {(postRebuildTime - lastTime) * 1000:F2}ms");
            lastTime = postRebuildTime;

            // Yield after graph/portal setup to spread work across frames
            yield return null;

            // Use INCREMENTAL rendering updates (coroutine-based for non-blocking)
            if (mazeRenderer != null)
            {
                float renderStart = Time.realtimeSinceStartup;

                // Only remove the portal end cap walls that are "past" the endpoint (in frontier direction)
                // This preserves side walls along the path while removing the U-shaped end cap
                if (consumedSpawnPos != Vector3.zero && consumedFrontierDir != Vector3.zero)
                {
                    mazeRenderer.RemoveWallsPastEndpoint(consumedSpawnPos, consumedFrontierDir, 2.5f, -0.3f);
                }

                // Remove existing walls along the new edge paths BEFORE adding new walls
                // This clears walls from previous edges that would intersect with new paths
                foreach (var newEdge in newEdges)
                {
                    if (newEdge.PolylinePoints != null && newEdge.PolylinePoints.Count >= 2)
                    {
                        mazeRenderer.RemoveWallsAlongPolyline(newEdge.PolylinePoints, PathRadius + 0.5f);
                    }
                }

                // Regenerate walls for cross-connection target nodes (existing nodes that received a new edge)
                // This removes all walls around the node and re-renders them with proper edge angle clearance
                Debug.Log($"[CrossConnection] Regenerating walls for {crossConnectionTargetNodes.Count} cross-connection target nodes");
                foreach (var targetNode in crossConnectionTargetNodes)
                {
                    Debug.Log($"[CrossConnection] Regenerating walls for Node {targetNode.Id} at {targetNode.Position}");
                    mazeRenderer.RegenerateNodeWalls(targetNode, forestMapState.Edges);
                }

                // Remove path tiles only around the NEW node center (to place cylinder)
                Vector3 newNodeWorldPos = new Vector3(newNode.Position.x, newNode.Position.y, 0);
                mazeRenderer.RemovePathTilesNearPosition(newNodeWorldPos, 4f);

                float afterRemove = Time.realtimeSinceStartup;
                Debug.Log($"[Timing]   End cap removal: {(afterRemove - renderStart) * 1000:F2}ms");

                // Yield after removals
                yield return null;

                // Add edge tiles for ALL edges (completed + new) - completed edge needs path extended
                mazeRenderer.AddEdgeTilesIncremental(allEdgesForPathTiles);

                float afterAddEdges = Time.realtimeSinceStartup;
                Debug.Log($"[Timing]   AddEdgeTilesIncremental: {(afterAddEdges - afterRemove) * 1000:F2}ms");

                // Add tiles for the new node AFTER edges
                // Node cylinder visually covers edge tiles, node area marks positions as occupied
                mazeRenderer.AddNodeTilesIncremental(newNode);

                float afterAddNode = Time.realtimeSinceStartup;
                Debug.Log($"[Timing]   AddNodeTilesIncremental: {(afterAddNode - afterAddEdges) * 1000:F2}ms");

                // Yield before wall generation (the slowest part)
                yield return null;

                // Add walls around the new elements (using async version)
                yield return StartCoroutine(mazeRenderer.AddWallsIncrementalAsync(newEdges, newNode));

                float afterAddWalls = Time.realtimeSinceStartup;
                Debug.Log($"[Timing]   AddWallsIncrementalAsync: {(afterAddWalls - afterAddEdges) * 1000:F2}ms");
            }

            float postRenderTime = Time.realtimeSinceStartup;
            Debug.Log($"[Timing] Incremental rendering: {(postRenderTime - lastTime) * 1000:F2}ms");

            float endTime = Time.realtimeSinceStartup;
            Debug.Log($"[DynamicGrowth] Growth step END at {endTime:F4}s (duration: {(endTime - startTime) * 1000:F2}ms)");
            yield break;
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
            foreach (int edgeId in forestMapState.Frontier)
            {
                var edge = forestMapState.Edges[edgeId];
                if (!edge.Partial || edge.PolylinePoints.Count == 0) continue;

                // Get the connected node (already in world space)
                var connectedNode = forestMapState.Nodes[edge.NodeA];

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
                Vector3 directionIntoMaze = new Vector3(-directionOutward.x, -directionOutward.y, 0f);
                CreatePortalAtWorldPosition(spawnId, portalWorldPos, directionIntoMaze);
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            var finalSpawnPoints = mazeData?.GetSpawnPointPositions();

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

            var worldSpaceData = mazeGridBehaviour?.WorldSpaceMazeData;
            if (worldSpaceData != null)
            {
                worldSpaceData.RegisterSpawnPoint(spawnId, portal.transform);
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

            foreach (var visitor in allVisitors)
            {
                if (visitor != null)
                {
                    visitor.RecalculatePath();
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

            foreach (var visitor in allVisitors)
            {
                if (visitor != null)
                {
                    visitor.RetargetToNearestSpawn();
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

            if (removedSpawnPositions == null || removedSpawnPositions.Count == 0)
            {
                return;
            }

            foreach (var visitor in allVisitors)
            {
                if (visitor == null) continue;

                Vector3 visitorDest = visitor.GetCurrentDestination();

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
                    // Retarget from the old destination position (where the consumed portal was)
                    // This finds the nearest spawn from where the visitor was heading,
                    // not from where they currently are
                    visitor.RetargetToNearestSpawnFrom(visitorDest);
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

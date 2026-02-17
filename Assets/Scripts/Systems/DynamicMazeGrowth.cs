using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForestMaze;
using FaeMaze.Props;
using FaeMaze.Roguelike;
using FaeMaze.Utilities;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages dynamic maze growth by adding new nodes at open endpoints every 30 seconds.
    /// Handles portal placement/removal and spawn point updates.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class DynamicMazeGrowth : MonoBehaviour
    {
        #region Enums

        /// <summary>
        /// Types of props that can be spawned at node centers during maze growth.
        /// </summary>
        public enum NodePropType
        {
            Pond,           // Standalone pond (no Puka)
            FairyRing,
            FaeLantern,
            WillowTheWisp,
            Puka            // Pond with Puka inside
        }

        #endregion

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

        [Header("Node Props")]
        [SerializeField]
        [Tooltip("FairyRing prefab to place at the center of each node")]
        private GameObject fairyRingPrefab;

        [SerializeField]
        [Tooltip("WillowTheWisp prefab to place at the center of each node during growth")]
        private GameObject wispPrefab;

        [SerializeField]
        [Tooltip("FaeLantern prefab to place at the center of each node")]
        private GameObject lanternPrefab;

        [SerializeField]
        [Tooltip("PukaHazard prefab to place at the center of each node (not currently used)")]
        private GameObject pukaHazardPrefab;

        [SerializeField]
        [Tooltip("Kelpie model prefab with animations (kelpie_react.glb) (not currently used)")]
        private GameObject kelpieModelPrefab;

        [SerializeField]
        [Tooltip("Animator Controller for the kelpie model (with ArmatureAction states) (not currently used)")]
        private RuntimeAnimatorController kelpieAnimatorController;

        [SerializeField]
        [Tooltip("Pond prefab to place underneath each PukaHazard (not currently used)")]
        private GameObject pondPrefab;


        [SerializeField]
        [Tooltip("Z position for Ponds (default 0)")]
        private float pondZPosition = 0f;

        [Header("References")]
        [SerializeField]
        [Tooltip("Parent transform for spawned portals")]
        private Transform portalsParent;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private MazeRenderer mazeRenderer;
        private WorldSpaceMazeRenderer worldSpaceRenderer;
        private bool useWorldSpaceRenderer = false;
        private float nextGrowthTime;
        private int completedInitialGrowthStages = 0;
        private bool initialGrowthComplete = false;

        private const float NodeRadius = 3.0f;  // Logical radius (matches NODE_RADIUS in PlanarForestMazeGenerator)
        private const float PathRadius = 0.5f;  // Logical path radius
        private const float WallBuffer = 1.0f;  // Logical wall buffer

        // Track portals at each spawn point
        private Dictionary<char, GameObject> spawnPointPortals = new Dictionary<char, GameObject>();

        // Track PukaHazards at each node (by node index) - not currently used
        private Dictionary<int, GameObject> nodePukas = new Dictionary<int, GameObject>();
        private Transform pukasParent;

        // Track FairyRings at each node (by node index)
        private Dictionary<int, GameObject> nodeFairyRings = new Dictionary<int, GameObject>();
        private Transform fairyRingsParent;

        // Track WillowTheWisps at each node (by node index)
        private Dictionary<int, GameObject> nodeWisps = new Dictionary<int, GameObject>();
        private Transform wispsParent;

        // Track FaeLanterns at each node (by node index)
        private Dictionary<int, GameObject> nodeLanterns = new Dictionary<int, GameObject>();
        private Transform lanternsParent;

        // Track Ponds at each node (by node index)
        private Dictionary<int, GameObject> nodePonds = new Dictionary<int, GameObject>();
        private Transform pondsParent;

        // Unified tracking: maps node index to the type of prop spawned there
        private Dictionary<int, NodePropType> nodeProps = new Dictionary<int, NodePropType>();

        // Factory for prop spawning
        private NodePropFactory propFactory;

        // Track node center colliders (block visitors from walking into node centers)
        private Dictionary<int, GameObject> nodeCenterColliders = new Dictionary<int, GameObject>();
        private Transform nodeCenterCollidersParent;
        private const float NODE_CENTER_COLLIDER_RADIUS = 1.0f;  // Radius of collider blocking node center (matches tile walkability)


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

            // Check for WorldSpaceMazeRenderer first (preferred)
            worldSpaceRenderer = GetComponent<WorldSpaceMazeRenderer>();
            if (worldSpaceRenderer != null)
            {
                useWorldSpaceRenderer = true;
            }
            else
            {
                mazeRenderer = GetComponent<MazeRenderer>();
            }

            // Create portals parent early so it's available for initial growth
            if (portalsParent == null)
            {
                GameObject portalsObj = new GameObject("Portals");
                portalsObj.transform.SetParent(transform);
                portalsObj.transform.localPosition = Vector3.zero;
                portalsParent = portalsObj.transform;
            }

            // Create pukas parent for organizing PukaHazards (not currently used)
            if (pukasParent == null)
            {
                GameObject pukasObj = new GameObject("NodePukas");
                pukasObj.transform.SetParent(transform);
                pukasObj.transform.localPosition = Vector3.zero;
                pukasParent = pukasObj.transform;
            }

            // Create fairy rings parent for organizing FairyRings
            if (fairyRingsParent == null)
            {
                GameObject fairyRingsObj = new GameObject("NodeFairyRings");
                fairyRingsObj.transform.SetParent(transform);
                fairyRingsObj.transform.localPosition = Vector3.zero;
                fairyRingsParent = fairyRingsObj.transform;
            }

            // Create wisps parent for organizing WillowTheWisps
            if (wispsParent == null)
            {
                GameObject wispsObj = new GameObject("NodeWisps");
                wispsObj.transform.SetParent(transform);
                wispsObj.transform.localPosition = Vector3.zero;
                wispsParent = wispsObj.transform;
            }

            // Create lanterns parent for organizing FaeLanterns
            if (lanternsParent == null)
            {
                GameObject lanternsObj = new GameObject("NodeLanterns");
                lanternsObj.transform.SetParent(transform);
                lanternsObj.transform.localPosition = Vector3.zero;
                lanternsParent = lanternsObj.transform;
            }

            // Create ponds parent for organizing Ponds
            if (pondsParent == null)
            {
                GameObject pondsObj = new GameObject("NodePonds");
                pondsObj.transform.SetParent(transform);
                pondsObj.transform.localPosition = Vector3.zero;
                pondsParent = pondsObj.transform;
            }

            // Create node center colliders parent for organizing collision blockers
            if (nodeCenterCollidersParent == null)
            {
                GameObject collidersObj = new GameObject("NodeCenterColliders");
                collidersObj.transform.SetParent(transform);
                collidersObj.transform.localPosition = Vector3.zero;
                nodeCenterCollidersParent = collidersObj.transform;
            }

            // Initialize prop factory with spawner methods
            propFactory = new NodePropFactory();
            propFactory.RegisterSpawners(
                pondSpawner: SpawnStandalonePondAtNode,
                fairyRingSpawner: SpawnFairyRingAtNode,
                lanternSpawner: SpawnLanternAtNode,
                wispSpawner: SpawnWispAtNode,
                pukaSpawner: SpawnPondPropAtNode
            );

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
            var forestMapState = mazeGridBehaviour.ForestMapState;

            for (int i = 0; i < initialGrowthStages; i++)
            {
                if (forestMapState.Frontier.Count == 0)
                {
                    break;
                }

                GrowMazeSynchronous(forestMapState);
                completedInitialGrowthStages = i + 1;
            }

            initialGrowthComplete = true;
        }

        /// <summary>
        /// Synchronous version of maze growth for initial stages.
        /// Runs the async coroutine to completion in a single frame.
        /// </summary>
        private void GrowMazeSynchronous(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState)
        {
            // Run the async coroutine to completion synchronously
            var enumerator = GrowMazeWorldSpaceAsync(forestMapState, synchronous: true);
            while (enumerator.MoveNext())
            {
                // Consume all yields without waiting
            }
        }

        private void Update()
        {
            if (autoGrowth && Time.time >= nextGrowthTime)
            {
                GrowMaze();
                nextGrowthTime = Time.time + growthInterval;
            }

            // Note: PukaHazards handle their own visual effects via their Update method
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
        /// <param name="forestMapState">The maze state to grow</param>
        /// <param name="synchronous">If true, skips yields and runs synchronously (for initial growth)</param>
        private IEnumerator GrowMazeWorldSpaceAsync(ForestMaze.PlanarForestMazeGenerator.ForestMapState forestMapState, bool synchronous = false)
        {
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

            // Use the same Step() method as initial generation to add a new node
            bool success = ForestMaze.PlanarForestMazeGenerator.Step(forestMapState);

            if (!success)
            {
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

            // Rebuild portals from frontier edges using world-space coordinates
            // This also registers spawn points and signals affected visitors to retarget
            // Pass the captured old spawn positions since WorldSpaceMazeData was regenerated
            // Skip portal rebuild during synchronous initial growth (done once in Start)
            if (!synchronous)
            {
                RebuildSpawnPointsFromFrontier(oldSpawnPositions);
            }

            // Yield after graph/portal setup to spread work across frames
            if (!synchronous) yield return null;

            // Use INCREMENTAL rendering updates (coroutine-based for non-blocking)
            if (useWorldSpaceRenderer && worldSpaceRenderer != null)
            {
                // WorldSpaceMazeRenderer uses shapes - refresh the entire visualization
                // The WorldSpaceMazeRenderer regenerates from the updated GraphState
                worldSpaceRenderer.RefreshMaze();
                if (!synchronous) yield return null;
            }
            else if (mazeRenderer != null)
            {
                // NOTE: Wall overlap is allowed! Wall models may overlap with each other and with paths.
                // Wall removal is handled by Unity physics collision detection, NOT by manual intersection checks.
                // Portal end cap walls are part of edge wall containers and destroyed via physics collision checks.
                // Do NOT use RemoveWallsPastEndpoint or RemoveWallsAlongPolyline - they will throw exceptions.

                // Regenerate walls for cross-connection target nodes (existing nodes that received a new edge)
                // This removes all walls around the node and re-renders them with proper edge angle clearance
                foreach (var targetNode in crossConnectionTargetNodes)
                {
                    #pragma warning disable CS0618 // Type or member is obsolete
                    mazeRenderer.RegenerateNodeWalls(targetNode, forestMapState.Edges);
                    #pragma warning restore CS0618
                }

                // Remove path tiles only around the NEW node center (to place cylinder)
                Vector3 newNodeWorldPos = new Vector3(newNode.Position.x, newNode.Position.y, 0);
                mazeRenderer.RemovePathTilesNearPosition(newNodeWorldPos, 4f);

                // Yield after removals
                if (!synchronous) yield return null;

                // Add edge tiles for ALL edges (completed + new) - completed edge needs path extended
                mazeRenderer.AddEdgeTilesIncremental(allEdgesForPathTiles);

                // Add tiles for the new node AFTER edges
                // Node cylinder visually covers edge tiles, node area marks positions as occupied
                mazeRenderer.AddNodeTilesIncremental(newNode);

                // ARCHITECTURE: After adding new path tiles and node, re-trigger collision checks
                // on the completed edge's wall container. Its walls may now collide with the new node.
                if (consumedEdgeIndex >= 0)
                {
                    mazeRenderer.TriggerEdgeWallCollisionChecks(consumedEdgeIndex);
                }

                // Also recheck walls around the new node position
                WallCollisionChecker.RecheckWallsAroundNode(newNode.Position, 2.5f);

                // Recheck walls along new edges
                foreach (var edge in newEdges)
                {
                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
                    {
                        WallCollisionChecker.RecheckWallsAlongPath(edge.PolylinePoints, 1.5f);
                    }
                }

                // Add walls around the new elements
                if (synchronous)
                {
                    // Synchronous version for initial growth
                    mazeRenderer.AddWallsIncremental(newEdges, newNode);
                }
                else
                {
                    // Yield before wall generation (the slowest part)
                    yield return null;

                    // Async version for runtime growth
                    yield return StartCoroutine(mazeRenderer.AddWallsIncrementalAsync(newEdges, newNode));
                }
            }

            // Spawn FaeLantern at the new node center
            SpawnPukaAtNode(newNode, newNodeId);

            // Notify visitors who can see the growth location (skip during initial sync growth)
            if (!synchronous)
            {
                Vector3 growthPosition = new Vector3(newNode.Position.x, newNode.Position.y, 0);
                DazeVisitorsWhoCanSeeGrowth(growthPosition);
            }

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

            // Note: Frontier end cap walls are now managed by MazeRenderer edge wall containers
            // They are automatically removed when the edge wall container is regenerated

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

                // Portal is placed 0.95 units INSIDE the path (toward node, away from endcap wall)
                Vector3 portalOffset = new Vector3(-directionOutward.x, -directionOutward.y, 0f) * 0.95f;
                Vector3 portalWorldPos = endpointWorld + portalOffset;

                // Get next spawn ID
                char spawnId = GetNextAvailableSpawnId();
                if (spawnId == '\0')
                {
                    break;
                }

                // Create portal at the frontier endpoint
                // Note: Frontier end cap walls are handled by MazeRenderer.RenderFrontierEndCap
                // as part of the edge wall container - no duplicate wall generation needed here
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

        /// <summary>
        /// Dazes visitors who can see the maze growth location.
        /// Uses a simple visibility check based on distance and line of sight.
        /// </summary>
        /// <param name="growthPosition">The world position where maze growth occurred</param>
        /// <param name="maxViewDistance">Maximum distance at which visitors can see growth (default 25 units)</param>
        /// <param name="dazeDuration">How long visitors remain dazed (default 15 seconds)</param>
        private void DazeVisitorsWhoCanSeeGrowth(Vector3 growthPosition, float maxViewDistance = 25f, float dazeDuration = 15f)
        {
            var allVisitors = FaeMaze.Visitors.VisitorRegistry.All;
            if (allVisitors == null)
            {
                return;
            }

            int dazedCount = 0;
            foreach (var visitor in allVisitors)
            {
                if (visitor == null) continue;

                Vector3 visitorPos = visitor.transform.position;
                float distance = Vector3.Distance(visitorPos, growthPosition);

                // Check if within view distance
                if (distance > maxViewDistance)
                    continue;

                // Simple line-of-sight check using raycast
                // Cast from visitor toward growth position
                Vector3 direction = (growthPosition - visitorPos).normalized;
                float rayDistance = distance;

                // Use a layermask to only check against walls/obstacles
                // If the ray reaches the growth position without hitting a wall, the visitor can see it
                int wallLayer = LayerMask.GetMask("Wall", "Obstacle");
                if (!Physics.Raycast(visitorPos, direction, rayDistance, wallLayer))
                {
                    // Visitor can see the growth - daze them
                    visitor.OnWitnessMazeGrowth(dazeDuration);
                    dazedCount++;
                }
            }

        }

        #endregion

        #region Portal Management

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

        #region Node Prop Spawning

        /// <summary>
        /// Gets a random prop type based on weighted chances.
        /// Base chances: 50% Pond, 20% Lantern, 15% Ring, 10% Puka, 5% Wisp
        /// Wisp only spawns if there's already a Puka in the maze.
        /// If no Puka exists, Wisp's 5% is added to Puka's chance (15% total).
        /// </summary>
        private NodePropType GetRandomPropType()
        {
            // Check if any Puka exists in the maze
            bool hasPuka = nodePukas.Count > 0;

            // Get Heart Form hazard spawn rate multiplier (increases Ring and Puka chances)
            float hazardMultiplier = HeartFormManager.Instance?.GetHazardSpawnRateMultiplier() ?? 1.0f;

            // Base weights: Pond 50%, Lantern 20%, Ring 15%, Puka 10%, Wisp 5%
            // Hazard multiplier boosts Ring and Puka weights
            float pondWeight = 50f;
            float lanternWeight = 20f;
            float ringWeight = 15f * hazardMultiplier;
            float pukaWeight = (hasPuka ? 10f : 15f) * hazardMultiplier; // Absorbs Wisp's 5% if no Puka
            float wispWeight = hasPuka ? 5f : 0f;

            // Normalize weights to 100
            float totalWeight = pondWeight + lanternWeight + ringWeight + pukaWeight + wispWeight;
            float roll = RandomManager.Value * totalWeight;

            // Select based on cumulative thresholds
            if (roll < pondWeight)
            {
                return NodePropType.Pond;
            }
            else if (roll < pondWeight + lanternWeight)
            {
                return NodePropType.FaeLantern;
            }
            else if (roll < pondWeight + lanternWeight + ringWeight)
            {
                return NodePropType.FairyRing;
            }
            else if (roll < pondWeight + lanternWeight + ringWeight + pukaWeight)
            {
                return NodePropType.Puka;
            }
            else
            {
                return NodePropType.WillowTheWisp;
            }
        }

        /// <summary>
        /// Spawns a prop at the center of the specified node during dynamic growth.
        /// Uses weighted random selection: 50% Pond, 20% Lantern, 15% Ring, 10% Puka, 5% Wisp.
        /// Wisp only spawns if there's already a Puka; otherwise Puka gets 15% chance.
        /// </summary>
        /// <param name="node">The node to spawn the prop at</param>
        /// <param name="nodeIndex">The index of the node in the ForestMapState</param>
        private void SpawnNodeProp(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if any prop already exists at this node
            if (nodeProps.ContainsKey(nodeIndex))
            {
                return;
            }

            // Skip the seed node (node 0) - that's where the Heart is
            if (nodeIndex == 0)
            {
                return;
            }

            // Get a random prop type based on weighted chances
            NodePropType propType = GetRandomPropType();

            // Use factory to spawn the prop
            bool success = propFactory.Spawn(propType, node, nodeIndex);

            // Track what was spawned
            if (success)
            {
                nodeProps[nodeIndex] = propType;
            }
        }

        /// <summary>
        /// Legacy method name preserved for compatibility with growth calls.
        /// </summary>
        private void SpawnPukaAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            SpawnNodeProp(node, nodeIndex);
        }

        /// <summary>
        /// Spawns a Pond with a Puka (kelpie) at the center of the specified node.
        /// </summary>
        /// <returns>True if spawn was successful</returns>
        private bool SpawnPondPropAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if pond already exists at this node
            if (nodePonds.ContainsKey(nodeIndex))
            {
                return false;
            }

            // Try to load prefab if not assigned
            if (pondPrefab == null)
            {
                pondPrefab = Resources.Load<GameObject>("Prefabs/Tile/Pond");
            }

            if (pondPrefab == null)
            {
                return false;
            }

            // Calculate world position at node center with specified Z
            Vector3 pondPos = new Vector3(node.Position.x, node.Position.y, pondZPosition);

            // Instantiate the pond
            GameObject pond = Instantiate(pondPrefab);
            pond.name = $"Pond_Node{nodeIndex}";
            pond.transform.position = pondPos;

            if (pondsParent != null)
            {
                pond.transform.SetParent(pondsParent, worldPositionStays: true);
            }

            // Track the pond
            nodePonds[nodeIndex] = pond;

            // Now spawn the PukaHazard (kelpie) inside the pond
            SpawnPukaHazardInPond(node, nodeIndex, pondPos);

            return true;
        }

        /// <summary>
        /// Spawns a standalone Pond (without Puka) at the center of the specified node.
        /// </summary>
        /// <returns>True if spawn was successful</returns>
        private bool SpawnStandalonePondAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if pond already exists at this node
            if (nodePonds.ContainsKey(nodeIndex))
            {
                return false;
            }

            // Try to load prefab if not assigned
            if (pondPrefab == null)
            {
                pondPrefab = Resources.Load<GameObject>("Prefabs/Tile/Pond");
            }

            if (pondPrefab == null)
            {
                return false;
            }

            // Calculate world position at node center with specified Z
            Vector3 pondPos = new Vector3(node.Position.x, node.Position.y, pondZPosition);

            // Instantiate the pond (without Puka)
            GameObject pond = Instantiate(pondPrefab);
            pond.name = $"Pond_Node{nodeIndex}";
            pond.transform.position = pondPos;

            if (pondsParent != null)
            {
                pond.transform.SetParent(pondsParent, worldPositionStays: true);
            }

            // Track the pond
            nodePonds[nodeIndex] = pond;

            // Add node center collider to block visitors from walking into the pond
            CreateNodeCenterCollider(node, nodeIndex);

            // No Puka spawned - this is a standalone decorative pond
            return true;
        }

        /// <summary>
        /// Spawns a PukaHazard (kelpie) inside a pond at the specified position.
        /// </summary>
        private void SpawnPukaHazardInPond(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex, Vector3 pondPos)
        {
            // Skip if puka already exists at this node
            if (nodePukas.ContainsKey(nodeIndex))
            {
                return;
            }

            // Try to load kelpie model prefab if not assigned
            if (kelpieModelPrefab == null)
            {
                kelpieModelPrefab = Resources.Load<GameObject>("Animations/Kelpie/kelpie_react");
            }

            if (kelpieModelPrefab == null)
            {
                return;
            }

            // Try to load animator controller if not assigned
            if (kelpieAnimatorController == null)
            {
                kelpieAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/Kelpie/kelpie_react");
            }

            // Instantiate the kelpie model directly (it contains the mesh and animations)
            GameObject puka = Instantiate(kelpieModelPrefab);
            puka.name = $"Puka_Node{nodeIndex}";

            // Set position - puka should be at same position as pond
            puka.transform.position = pondPos;

            if (pukasParent != null)
            {
                puka.transform.SetParent(pukasParent, worldPositionStays: true);
            }

            // Assign animator controller if available
            var animator = puka.GetComponent<Animator>();
            if (animator != null && kelpieAnimatorController != null)
            {
                animator.runtimeAnimatorController = kelpieAnimatorController;
            }

            // Add PukaHazard component
            var pukaHazard = puka.GetComponent<FaeMaze.Props.PukaHazard>();
            if (pukaHazard == null)
            {
                pukaHazard = puka.AddComponent<FaeMaze.Props.PukaHazard>();
            }

            // Track the puka
            nodePukas[nodeIndex] = puka;
        }

        /// <summary>
        /// Spawns a FairyRing at the center of the specified node.
        /// </summary>
        /// <returns>True if spawn was successful</returns>
        private bool SpawnFairyRingAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if fairy ring already exists at this node
            if (nodeFairyRings.ContainsKey(nodeIndex))
            {
                return false;
            }

            // Try to load FairyRing prefab if not assigned
            if (fairyRingPrefab == null)
            {
                fairyRingPrefab = Resources.Load<GameObject>("Prefabs/Props/ring");
            }

            if (fairyRingPrefab == null)
            {
                return false;
            }

            // Calculate world position at node center - offset Z to place in front of node cylinder
            Vector3 ringPos = new Vector3(node.Position.x, node.Position.y, -0.2f);

            // Instantiate the fairy ring
            GameObject ring = Instantiate(fairyRingPrefab);
            ring.name = $"FairyRing_Node{nodeIndex}";
            ring.transform.position = ringPos;

            string ringLabel = EntityLabels.GetPropLabel(ring);
            GameEventLogger.LogPropSpawn(ringLabel, ringPos);
            FaeMaze.Visitors.VisitorStateIndicator.ShowPropLabel(ring.transform, ringLabel);

            if (fairyRingsParent != null)
            {
                ring.transform.SetParent(fairyRingsParent, worldPositionStays: true);
            }

            // ALWAYS ensure the ring has the proper trigger collider and Rigidbody,
            // even if the prefab already has a FairyRing component.
            // GetComponent<Collider>() would find child colliders (like the animated spheres)
            // which are too small and not at the right position for detection.

            // Check if a SphereCollider already exists on the ROOT (not children)
            var existingRootSphere = ring.GetComponent<SphereCollider>();
            if (existingRootSphere == null)
            {
                // Add a new SphereCollider to the root specifically for the FairyRing trigger
                var triggerCollider = ring.AddComponent<SphereCollider>();
                triggerCollider.isTrigger = true;
                // Radius 2.5 reaches visitors in the walkable ring (1.0 to 3.0 from node center)
                triggerCollider.radius = 2.5f;
                triggerCollider.center = Vector3.zero;
            }
            else
            {
                // Ensure existing sphere is properly configured
                existingRootSphere.isTrigger = true;
                if (existingRootSphere.radius < 2.0f)
                {
                    existingRootSphere.radius = 2.5f;
                }
            }

            // Ensure the ring has a kinematic Rigidbody for trigger events to work
            // Unity requires at least one colliding object to have a Rigidbody for OnTriggerEnter
            ring.AddKinematicRigidbody();

            // Add FairyRing component if not present (for entrancement behavior)
            var fairyRing = ring.GetComponent<FaeMaze.Props.FairyRing>();
            if (fairyRing == null)
            {
                fairyRing = ring.AddComponent<FaeMaze.Props.FairyRing>();
            }

            // Track the fairy ring
            nodeFairyRings[nodeIndex] = ring;

            // Add node center collider to block visitors from walking into the ring
            CreateNodeCenterCollider(node, nodeIndex);

            return true;
        }

        /// <summary>
        /// Spawns a FaeLantern at the center of the specified node.
        /// </summary>
        /// <returns>True if spawn was successful</returns>
        private bool SpawnLanternAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if lantern already exists at this node
            if (nodeLanterns.ContainsKey(nodeIndex))
            {
                return false;
            }

            // Try to load FaeLantern prefab if not assigned
            if (lanternPrefab == null)
            {
                lanternPrefab = Resources.Load<GameObject>("Prefabs/Props/lantern2");
            }

            if (lanternPrefab == null) return false;

            // Calculate world position at node center - offset Z to place in front of node cylinder
            Vector3 lanternPos = new Vector3(node.Position.x, node.Position.y, -0.5f);

            // Instantiate the lantern
            GameObject lantern = Instantiate(lanternPrefab);
            lantern.name = $"Lantern_Node{nodeIndex}";
            lantern.transform.position = lanternPos;

            string lanternLabel = EntityLabels.GetPropLabel(lantern);
            GameEventLogger.LogPropSpawn(lanternLabel, lanternPos);
            FaeMaze.Visitors.VisitorStateIndicator.ShowPropLabel(lantern.transform, lanternLabel);

            // Lantern prefab has 180° X rotation to orient the model correctly.
            // Do NOT set rotation to identity — the prefab rotation is needed.

            if (lanternsParent != null)
            {
                lantern.transform.SetParent(lanternsParent, worldPositionStays: true);
            }

            // Add FaeLantern component if not present
            var lanternComponent = lantern.GetComponent<FaeMaze.Props.FaeLantern>();
            if (lanternComponent == null)
            {
                lanternComponent = lantern.AddComponent<FaeMaze.Props.FaeLantern>();
            }

            // Ensure the component is enabled (registers in FaeLantern.All via OnEnable)
            if (!lanternComponent.enabled)
            {
                lanternComponent.enabled = true;
            }

            // Track the lantern
            nodeLanterns[nodeIndex] = lantern;

            // Add node center collider to block visitors from walking into the lantern
            CreateNodeCenterCollider(node, nodeIndex);

            return true;
        }

        /// <summary>
        /// Spawns a WillowTheWisp at the center of the specified node.
        /// </summary>
        /// <returns>True if spawn was successful</returns>
        private bool SpawnWispAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if wisp already exists at this node
            if (nodeWisps.ContainsKey(nodeIndex))
            {
                return false;
            }

            // Try to load WillowTheWisp prefab if not assigned
            if (wispPrefab == null)
            {
                wispPrefab = Resources.Load<GameObject>("Prefabs/Props/WillowTheWisp");
            }

            if (wispPrefab == null)
            {
                return false;
            }

            // Calculate world position at node center - offset Z to place in front of node cylinder
            Vector3 wispPos = new Vector3(node.Position.x, node.Position.y, -0.5f);

            // Instantiate the wisp
            GameObject wisp = Instantiate(wispPrefab);
            wisp.name = $"Wisp_Node{nodeIndex}";
            wisp.transform.position = wispPos;

            string wispLabel = EntityLabels.GetPropLabel(wisp);
            GameEventLogger.LogPropSpawn(wispLabel, wispPos);
            FaeMaze.Visitors.VisitorStateIndicator.ShowPropLabel(wisp.transform, wispLabel);

            if (wispsParent != null)
            {
                wisp.transform.SetParent(wispsParent, worldPositionStays: true);
            }

            // Add WillowTheWisp component if not present
            var wispComponent = wisp.GetComponent<FaeMaze.Props.WillowTheWisp>();
            if (wispComponent == null)
            {
                var collider = wisp.GetComponent<Collider>();
                if (collider == null)
                {
                    var sphereCollider = wisp.AddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                    sphereCollider.radius = 0.4f;
                    sphereCollider.center = Vector3.zero; // XY-plane collision
                }
                wisp.AddKinematicRigidbody();
                wispComponent = wisp.AddComponent<FaeMaze.Props.WillowTheWisp>();
            }

            // Track the wisp
            nodeWisps[nodeIndex] = wisp;

            // Add node center collider to block visitors from walking into the wisp
            CreateNodeCenterCollider(node, nodeIndex);

            return true;
        }

        /// <summary>
        /// Creates a solid collider at the node center to prevent visitors from walking into the middle.
        /// Should be called for all nodes except heart (node 0) and Puka nodes.
        /// </summary>
        /// <param name="node">The node to add a collider to</param>
        /// <param name="nodeIndex">The index of the node</param>
        private void CreateNodeCenterCollider(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if collider already exists
            if (nodeCenterColliders.ContainsKey(nodeIndex))
            {
                return;
            }

            // Create collider game object
            GameObject colliderObj = new GameObject($"NodeCenterCollider_{nodeIndex}");
            colliderObj.transform.position = new Vector3(node.Position.x, node.Position.y, 0f);

            if (nodeCenterCollidersParent != null)
            {
                colliderObj.transform.SetParent(nodeCenterCollidersParent, worldPositionStays: true);
            }

            // Add a CapsuleCollider oriented along Z axis (vertical in world-space)
            // This creates a cylinder that blocks movement in the XY plane
            // Note: This is a TRIGGER collider - it doesn't physically block movement.
            // Movement blocking is handled by code in VisitorControllerBase.IsBlockedByNodeCenter().
            // Using a trigger prevents visitors from getting permanently stuck if they somehow
            // end up inside the blocked area (e.g., from spline interpolation cutting corners).
            CapsuleCollider capsule = colliderObj.AddComponent<CapsuleCollider>();
            capsule.radius = NODE_CENTER_COLLIDER_RADIUS;
            capsule.height = 4f;  // Tall enough to block any Z position visitors might be at
            capsule.direction = 2;  // Z-axis alignment
            capsule.isTrigger = true;  // Trigger, not solid - code handles blocking

            // Track the collider
            nodeCenterColliders[nodeIndex] = colliderObj;
        }

        /// <summary>
        /// Removes the node center collider for a specific node.
        /// Called when removing props (e.g., via Sculpting power).
        /// </summary>
        /// <param name="nodeIndex">The index of the node</param>
        private void RemoveNodeCenterCollider(int nodeIndex)
        {
            if (nodeCenterColliders.TryGetValue(nodeIndex, out GameObject colliderObj))
            {
                if (colliderObj != null)
                {
                    Destroy(colliderObj);
                }
                nodeCenterColliders.Remove(nodeIndex);
            }
        }

        #region PukaHazard Spawning (Not Currently Used)

        /// <summary>
        /// Spawns a PukaHazard and Pond at the center of the specified node.
        /// NOTE: This method is preserved for future use but not currently called.
        /// </summary>
        /// <param name="node">The node to spawn the puka at</param>
        /// <param name="nodeIndex">The index of the node in the ForestMapState</param>
        private void SpawnPukaHazardAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if puka already exists at this node
            if (nodePukas.ContainsKey(nodeIndex))
                return;

            // Skip the seed node (node 0) - that's where the Heart is
            if (nodeIndex == 0)
                return;

            // Spawn the pond first (underneath the puka)
            SpawnPondAtNode(node, nodeIndex);

            // Try to load kelpie model prefab if not assigned
            if (kelpieModelPrefab == null)
            {
                kelpieModelPrefab = Resources.Load<GameObject>("Animations/Kelpie/kelpie_react");
            }

            if (kelpieModelPrefab == null)
            {
                return;
            }

            // Try to load animator controller if not assigned
            if (kelpieAnimatorController == null)
            {
                kelpieAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/Kelpie/kelpie_react");
            }

            // Calculate world position at node center - use same Z as pond (0) for alignment
            Vector3 pukaPos = new Vector3(node.Position.x, node.Position.y, pondZPosition);

            // Instantiate the kelpie model directly (it contains the mesh and animations)
            GameObject puka = Instantiate(kelpieModelPrefab);
            puka.name = $"Puka_Node{nodeIndex}";

            // Set position - puka should be at same position as pond
            puka.transform.position = pukaPos;

            if (pukasParent != null)
            {
                puka.transform.SetParent(pukasParent, worldPositionStays: true);
            }

            // Assign animator controller if available
            var animator = puka.GetComponent<Animator>();
            if (animator != null && kelpieAnimatorController != null)
            {
                animator.runtimeAnimatorController = kelpieAnimatorController;
            }

            // Add PukaHazard component
            var pukaHazard = puka.GetComponent<FaeMaze.Props.PukaHazard>();
            if (pukaHazard == null)
            {
                pukaHazard = puka.AddComponent<FaeMaze.Props.PukaHazard>();
            }

            // Track the puka
            nodePukas[nodeIndex] = puka;
        }

        #endregion

        /// <summary>
        /// Spawns a Pond at the center of the specified node.
        /// </summary>
        /// <param name="node">The node to spawn the pond at</param>
        /// <param name="nodeIndex">The index of the node in the ForestMapState</param>
        private void SpawnPondAtNode(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            // Skip if pond already exists at this node
            if (nodePonds.ContainsKey(nodeIndex))
                return;

            // Try to load prefab if not assigned
            if (pondPrefab == null)
            {
                pondPrefab = Resources.Load<GameObject>("Prefabs/Tile/Pond");
            }

            if (pondPrefab == null)
                return;

            // Calculate world position at node center with specified Z
            Vector3 pondPos = new Vector3(node.Position.x, node.Position.y, pondZPosition);

            // Instantiate the pond - use prefab's transform settings (scale, rotation)
            GameObject pond = Instantiate(pondPrefab);
            pond.name = $"Pond_Node{nodeIndex}";

            // Set position while preserving prefab's local transform (scale, rotation)
            pond.transform.position = pondPos;

            string pondLabel = EntityLabels.GetPropLabel(pond);
            GameEventLogger.LogPropSpawn(pondLabel, pondPos);
            FaeMaze.Visitors.VisitorStateIndicator.ShowPropLabel(pond.transform, pondLabel);

            if (pondsParent != null)
            {
                pond.transform.SetParent(pondsParent, worldPositionStays: true);
            }

            // Track the pond
            nodePonds[nodeIndex] = pond;
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

        /// <summary>
        /// Gets the type of prop currently spawned at the specified node index.
        /// </summary>
        /// <returns>The prop type, or null if no prop is spawned at this node</returns>
        public NodePropType? GetNodePropType(int nodeIndex)
        {
            if (nodeProps.TryGetValue(nodeIndex, out NodePropType propType))
            {
                return propType;
            }
            return null;
        }

        /// <summary>
        /// Removes any existing prop at the specified node index.
        /// </summary>
        /// <returns>True if a prop was removed, false if no prop existed</returns>
        public bool RemovePropFromNode(int nodeIndex)
        {
            bool removed = false;

            // Remove from unified tracking
            if (nodeProps.ContainsKey(nodeIndex))
            {
                nodeProps.Remove(nodeIndex);
            }

            // Remove pond
            if (nodePonds.TryGetValue(nodeIndex, out GameObject pond))
            {
                if (pond != null) Destroy(pond);
                nodePonds.Remove(nodeIndex);
                removed = true;
            }

            // Remove fairy ring
            if (nodeFairyRings.TryGetValue(nodeIndex, out GameObject ring))
            {
                if (ring != null) Destroy(ring);
                nodeFairyRings.Remove(nodeIndex);
                removed = true;
            }

            // Remove lantern
            if (nodeLanterns.TryGetValue(nodeIndex, out GameObject lantern))
            {
                if (lantern != null) Destroy(lantern);
                nodeLanterns.Remove(nodeIndex);
                removed = true;
            }

            // Remove wisp
            if (nodeWisps.TryGetValue(nodeIndex, out GameObject wisp))
            {
                if (wisp != null) Destroy(wisp);
                nodeWisps.Remove(nodeIndex);
                removed = true;
            }

            // Remove puka
            if (nodePukas.TryGetValue(nodeIndex, out GameObject puka))
            {
                if (puka != null) Destroy(puka);
                nodePukas.Remove(nodeIndex);
                removed = true;
            }

            // Remove node center collider
            RemoveNodeCenterCollider(nodeIndex);

            return removed;
        }

        /// <summary>
        /// Sets the prop type at the specified node index.
        /// Removes any existing prop first, then spawns the new one.
        /// </summary>
        /// <param name="nodeIndex">The index of the node to modify</param>
        /// <param name="propType">The type of prop to spawn, or null to just remove existing</param>
        /// <returns>True if the operation was successful</returns>
        public bool SetNodeProp(int nodeIndex, NodePropType? propType)
        {
            var forestMapState = mazeGridBehaviour?.ForestMapState;
            if (forestMapState == null || nodeIndex < 0 || nodeIndex >= forestMapState.Nodes.Count)
            {
                return false;
            }

            // Remove existing prop
            RemovePropFromNode(nodeIndex);

            // If propType is null, we just wanted to remove the prop
            if (!propType.HasValue)
            {
                return true;
            }

            // Get the node
            var node = forestMapState.Nodes[nodeIndex];

            // Spawn the new prop using factory
            bool success = propFactory.Spawn(propType.Value, node, nodeIndex);

            // Track what was spawned
            if (success)
            {
                nodeProps[nodeIndex] = propType.Value;
            }

            return success;
        }

        /// <summary>
        /// Finds the node index at or near the specified world position.
        /// </summary>
        /// <param name="worldPosition">The world position to search near</param>
        /// <param name="searchRadius">Maximum distance to search</param>
        /// <returns>The node index, or -1 if no node found</returns>
        public int FindNodeIndexAtPosition(Vector3 worldPosition, float searchRadius = 3f)
        {
            var mazeData = mazeGridBehaviour?.WorldSpaceMazeData;
            if (mazeData == null) return -1;

            Vector2 targetPos2D = new Vector2(worldPosition.x, worldPosition.y);
            var nearbyTiles = mazeData.GetTilesNear(targetPos2D, searchRadius);

            // Find the nearest tile that belongs to a node
            float minDist = float.MaxValue;
            int foundNodeIndex = -1;

            foreach (var tile in nearbyTiles)
            {
                if (!tile.Walkable || tile.NodeIndex < 0) continue;

                float dist = Vector2.Distance(targetPos2D, tile.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    foundNodeIndex = tile.NodeIndex;
                }
            }

            return foundNodeIndex;
        }

        /// <summary>
        /// Checks if a node has a prop placed at it.
        /// </summary>
        public bool HasPropAtNode(int nodeIndex)
        {
            return nodeProps.ContainsKey(nodeIndex);
        }

        /// <summary>
        /// Gets the prop type at a node, or null if no prop is present.
        /// </summary>
        public NodePropType? GetPropTypeAtNode(int nodeIndex)
        {
            return nodeProps.TryGetValue(nodeIndex, out var propType) ? propType : null;
        }

        /// <summary>
        /// Checks if the specified world position is on a node (not an edge).
        /// </summary>
        public bool IsPositionOnNode(Vector3 worldPosition, float searchRadius = 3f)
        {
            return FindNodeIndexAtPosition(worldPosition, searchRadius) >= 0;
        }

        /// <summary>
        /// Gets the center position of a node in world space.
        /// </summary>
        /// <param name="nodeIndex">The node index</param>
        /// <returns>The node center position, or null if node not found</returns>
        public Vector3? GetNodeCenterPosition(int nodeIndex)
        {
            var mapState = mazeGridBehaviour?.ForestMapState;
            if (mapState == null || nodeIndex < 0 || nodeIndex >= mapState.Nodes.Count)
                return null;

            var node = mapState.Nodes[nodeIndex];
            return new Vector3(node.Position.x, node.Position.y, 0);
        }

        /// <summary>
        /// Gets the world positions of all active portals (spawn points).
        /// Used by camera focus cycling.
        /// </summary>
        /// <returns>List of portal world positions</returns>
        public List<Vector3> GetPortalPositions()
        {
            var positions = new List<Vector3>();
            foreach (var kvp in spawnPointPortals)
            {
                if (kvp.Value != null)
                {
                    positions.Add(kvp.Value.transform.position);
                }
            }
            return positions;
        }

        /// <summary>
        /// Gets the number of active portals.
        /// </summary>
        public int PortalCount => spawnPointPortals.Count;

        #endregion
    }
}

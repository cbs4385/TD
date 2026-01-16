using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using ForestMaze;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Willow-the-Wisp that is an antagonist working against the player.
    /// Two states: Idle (travels between adjacent nodes) and Reacting (lures visitors to nearest PukaHazard).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class WillowTheWisp : MonoBehaviour
    {
        #region Enums

        public enum WispState
        {
            Idle,       // Traveling between adjacent nodes
            Approaching,// Moving to a valid path tile near the visitor before luring
            Reacting    // Luring a visitor to the nearest PukaHazard
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed when idle")]
        private float idleSpeed = 4f;

        [SerializeField]
        [Tooltip("Movement speed when reacting/luring")]
        private float reactingSpeed = 3f;

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.5f;

        [Header("Detection Settings")]
        [SerializeField]
        [Tooltip("Distance to detect visitors and trigger reacting state")]
        private float visitorDetectionRadius = 3f;

        [SerializeField]
        [Tooltip("How often to scan for visitors (seconds)")]
        private float visitorScanInterval = 0.25f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the wisp sprite")]
        private Color wispColor = new Color(0.9f, 1f, 0.4f, 1f);

        [SerializeField]
        [Tooltip("Size of the wisp sprite")]
        private float wispSize = 0.5f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 16;

        [SerializeField]
        [Tooltip("Enable pulsing glow effect")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 3f;

        [SerializeField]
        [Tooltip("Pulse magnitude")]
        private float pulseMagnitude = 0.15f;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations")]
        private bool useProceduralSprite = false;

        [Header("Model Settings")]
        [SerializeField]
        [Tooltip("Model prefab to spawn for the Willow-the-Wisp visuals")]
        private GameObject wispModelPrefab;

        [SerializeField]
        [Tooltip("Animator controller to apply to the spawned model")]
        private RuntimeAnimatorController wispController;

        [Header("3D Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing 3D point light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow (pastel blue)")]
        private Color glowColor = new Color(0.7f, 0.85f, 1f, 1f);

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 3f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz")]
        private float glowFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 1.5f;

        #endregion

        #region Private Fields

        private WispState state;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private SpriteRenderer spriteRenderer;
        private Rigidbody rb;
        private Animator animator;
        private Vector3 baseScale;
        private Vector3 initialScale;

        // Node navigation - 4-node Catmull-Rom spline system: P0 -> P1 -> P2 -> P3
        // Wisp travels smoothly from P1 to P2, with P0 and P3 influencing the curve shape
        // When P2 is reached, shift all nodes forward: P0=P1, P1=P2, P2=P3, pick new P3
        // This provides C1 continuity (smooth velocity) at each node transition
        private int[] splineNodeIndices = new int[4] { -1, -1, -1, -1 }; // P0, P1, P2, P3
        private Vector3[] splineNodePositions = new Vector3[4];
        private PlanarForestMazeGenerator.ForestMapState mapState;

        // Catmull-Rom spline navigation
        // We interpolate from P1 (t=0) to P2 (t=1)
        private float splineProgress = 0f;
        private float splineSegmentLength = 1f;

        // Catmull-Rom tension parameter (0.5 = centripetal, gives nice curves without loops)
        private const float catmullRomTension = 0.5f;

        // Visitor tracking
        private VisitorControllerBase targetVisitor;
        private float visitorScanTimer;

        // Target PukaHazard when luring
        private PukaHazard targetPuka;

        // Approaching state tracking
        private Vector3 approachTargetPosition;
        private bool approachingPhaseRotating; // false = moving toward visitor, true = rotating to find walkable
        private float approachRotationAngle; // Current rotation angle around visitor (degrees)
        private const float approachRotationRadius = 0.5f; // Distance from visitor when rotating
        private const float approachRotationSpeed = 180f; // Degrees per second

        // Reacting state pathfinding (tile-based for visitor-valid paths)
        private List<Vector3> reactingWorldPath = new List<Vector3>(); // Walkable tile path to Puka
        private int reactingWorldPathIndex = 0; // Current index in the world path

        // Legacy node-based pathfinding (kept for idle state)
        private List<int> reactingPath = new List<int>(); // Path of node indices from current to Puka
        private int reactingPathIndex = 0; // Current index in the path
        private Vector3 reactingCurrentTarget; // Current movement target position

        private const string DirectionParameter = "Direction";
        private GameObject modelInstance;
        private Light glowLight;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the wisp</summary>
        public WispState State => state;

        /// <summary>Gets whether this wisp is currently luring a visitor (only in Reacting state)</summary>
        public bool IsLuring => state == WispState.Reacting && targetVisitor != null;

        /// <summary>Gets the world position of this wisp</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Gets the target PukaHazard the wisp is leading to</summary>
        public PukaHazard TargetPuka => targetPuka;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = WispState.Idle;
            initialScale = transform.localScale;
            SetupModel();
            SetupSpriteRenderer();
            SetupColliders();
            SetupGlowLight();
            animator = GetComponentInChildren<Animator>(true);
            ApplyAnimatorController();
        }

        private void Start()
        {
            AcquireDependencies();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
                ApplyAnimatorController();
            }

            if (!AcquireDependencies())
            {
                Debug.LogWarning($"[Wisp:{name}] Start: Dependencies not ready");
                return;
            }

            // Initialize the 4-node Catmull-Rom spline system
            InitializeCatmullRomSpline();
        }

        private void Update()
        {
            if (!AcquireDependencies())
            {
                return;
            }

            if (enablePulse && spriteRenderer != null)
            {
                UpdatePulse();
            }

            if (enableGlow && glowLight != null)
            {
                UpdateGlowPulse();
            }

            // Update state-specific behavior
            if (state == WispState.Idle)
            {
                UpdateIdle();
            }
            else if (state == WispState.Approaching)
            {
                UpdateApproaching();
            }
            else if (state == WispState.Reacting)
            {
                UpdateReacting();
            }
        }

        private bool AcquireDependencies()
        {
            bool ready = true;

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
                if (mazeGridBehaviour != null)
                {
                    mapState = mazeGridBehaviour.ForestMapState;
                }
            }

            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (mazeGridBehaviour == null || gameController == null || mapState == null)
            {
                ready = false;
            }

            return ready;
        }

        #endregion

        #region Node Navigation

        /// <summary>
        /// Finds the closest node to the wisp's current position.
        /// </summary>
        private int FindClosestNode()
        {
            if (mapState == null || mapState.Nodes.Count == 0)
                return 0;

            float closestDistance = float.MaxValue;
            int closestNode = 0;

            for (int i = 0; i < mapState.Nodes.Count; i++)
            {
                var node = mapState.Nodes[i];
                float distance = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.y),
                    node.Position
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestNode = i;
                }
            }

            return closestNode;
        }

        /// <summary>
        /// Gets the world position of a node by index.
        /// </summary>
        private Vector3 GetNodePosition(int nodeIndex)
        {
            if (mapState == null || nodeIndex < 0 || nodeIndex >= mapState.Nodes.Count)
                return transform.position;

            var node = mapState.Nodes[nodeIndex];
            return new Vector3(node.Position.x, node.Position.y, transform.position.z);
        }

        /// <summary>
        /// Gets the neighbor node indices for a given node.
        /// </summary>
        private List<int> GetNeighborNodes(int nodeIndex)
        {
            List<int> neighbors = new List<int>();

            if (mapState == null || nodeIndex < 0 || nodeIndex >= mapState.Nodes.Count)
            {
                Debug.LogWarning($"[Wisp:{name}] GetNeighborNodes: Invalid nodeIndex={nodeIndex} (mapState={(mapState == null ? "null" : $"valid, {mapState.Nodes.Count} nodes")})");
                return neighbors;
            }

            var node = mapState.Nodes[nodeIndex];
            int skippedPartial = 0;
            int skippedInvalid = 0;

            foreach (int edgeId in node.IncidentEdges)
            {
                if (edgeId < 0 || edgeId >= mapState.Edges.Count)
                {
                    skippedInvalid++;
                    continue;
                }

                var edge = mapState.Edges[edgeId];

                // Skip partial edges (no NodeB)
                if (edge.Partial || edge.NodeB == null)
                {
                    skippedPartial++;
                    continue;
                }

                // Get the other node in this edge
                int otherNode = edge.NodeA == nodeIndex ? edge.NodeB.Value : edge.NodeA;
                if (!neighbors.Contains(otherNode))
                {
                    neighbors.Add(otherNode);
                }
            }

            if (neighbors.Count == 0)
            {
                Debug.LogWarning($"[Wisp:{name}] GetNeighborNodes: Node {nodeIndex} has {node.IncidentEdges.Count} edges but 0 valid neighbors (skipped: {skippedPartial} partial, {skippedInvalid} invalid)");
            }

            return neighbors;
        }

        /// <summary>
        /// Initializes the 4-node Catmull-Rom spline system.
        /// P0 influences entry tangent, P1 is current position, P2 is target, P3 influences exit tangent.
        /// </summary>
        private void InitializeCatmullRomSpline()
        {
            // Find closest node as P1 (where we start)
            int p1 = FindClosestNode();
            var p1Neighbors = GetNeighborNodes(p1);

            if (p1Neighbors.Count == 0)
            {
                Debug.LogWarning($"[Wisp:{name}] InitializeCatmullRomSpline: Node {p1} has no neighbors!");
                return;
            }

            // Pick P2 (next target) randomly from P1's neighbors
            int p2 = p1Neighbors[Random.Range(0, p1Neighbors.Count)];

            // Pick P0 (previous node for tangent) - any neighbor of P1 that isn't P2
            // If only one neighbor, use P1's position mirrored across itself
            int p0 = p1;
            foreach (int neighbor in p1Neighbors)
            {
                if (neighbor != p2)
                {
                    p0 = neighbor;
                    break;
                }
            }

            // Pick P3 (future node for tangent) - any neighbor of P2 that isn't P1
            var p2Neighbors = GetNeighborNodes(p2);
            int p3 = p2;
            foreach (int neighbor in p2Neighbors)
            {
                if (neighbor != p1)
                {
                    p3 = neighbor;
                    break;
                }
            }
            // If P2 only connects to P1, use P2 as P3 (will create straight exit tangent)
            if (p3 == p2 && p2Neighbors.Count > 0)
            {
                p3 = p2Neighbors[0];
            }

            // Set all indices and positions
            splineNodeIndices[0] = p0;
            splineNodeIndices[1] = p1;
            splineNodeIndices[2] = p2;
            splineNodeIndices[3] = p3;

            for (int i = 0; i < 4; i++)
            {
                splineNodePositions[i] = GetNodePosition(splineNodeIndices[i]);
            }

            // Calculate segment length for speed normalization
            splineSegmentLength = EstimateCatmullRomLength(splineNodePositions[0], splineNodePositions[1], splineNodePositions[2], splineNodePositions[3]);
            splineProgress = 0f;
        }

        /// <summary>
        /// Picks a new P3 node as a neighbor of P2, avoiding P1.
        /// </summary>
        private int PickNextNode(int currentNode, int avoidNode)
        {
            var neighbors = GetNeighborNodes(currentNode);

            if (neighbors.Count == 0)
            {
                return currentNode; // Dead end
            }

            // Filter out the node we want to avoid (to prevent immediate backtracking)
            List<int> validNeighbors = new List<int>();
            foreach (int neighbor in neighbors)
            {
                if (neighbor != avoidNode)
                {
                    validNeighbors.Add(neighbor);
                }
            }

            // If all neighbors are the avoid node, allow backtracking
            if (validNeighbors.Count == 0)
            {
                return neighbors[Random.Range(0, neighbors.Count)];
            }

            return validNeighbors[Random.Range(0, validNeighbors.Count)];
        }

        /// <summary>
        /// Advances to the next spline segment when P2 is reached.
        /// Shifts all nodes forward: P0=P1, P1=P2, P2=P3, pick new P3.
        /// This maintains C1 continuity because the outgoing tangent at old P2
        /// (influenced by old P1 and P3) becomes the incoming tangent at new P1.
        /// </summary>
        private void AdvanceSplineSegment()
        {
            // Shift nodes forward
            splineNodeIndices[0] = splineNodeIndices[1]; // Old P1 becomes new P0
            splineNodeIndices[1] = splineNodeIndices[2]; // Old P2 becomes new P1 (where we are)
            splineNodeIndices[2] = splineNodeIndices[3]; // Old P3 becomes new P2 (next target)

            // Pick new P3 (neighbor of new P2, avoiding new P1 to prevent sharp reversal)
            splineNodeIndices[3] = PickNextNode(splineNodeIndices[2], splineNodeIndices[1]);

            // Update positions
            for (int i = 0; i < 4; i++)
            {
                splineNodePositions[i] = GetNodePosition(splineNodeIndices[i]);
            }

            // Recalculate segment length
            splineSegmentLength = EstimateCatmullRomLength(splineNodePositions[0], splineNodePositions[1], splineNodePositions[2], splineNodePositions[3]);
            splineProgress = 0f;
        }

        /// <summary>
        /// Finds a path from startNode to targetNode using BFS.
        /// Returns a list of node indices representing the path, or empty list if no path found.
        /// </summary>
        private List<int> FindPathToNode(int startNode, int targetNode)
        {
            if (mapState == null || startNode < 0 || targetNode < 0)
                return new List<int>();

            if (startNode == targetNode)
                return new List<int> { startNode };

            // BFS
            Queue<int> queue = new Queue<int>();
            Dictionary<int, int> cameFrom = new Dictionary<int, int>(); // child -> parent

            queue.Enqueue(startNode);
            cameFrom[startNode] = -1; // Mark start as visited with no parent

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                if (current == targetNode)
                {
                    // Reconstruct path
                    List<int> path = new List<int>();
                    int node = targetNode;
                    while (node != -1)
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }
                    path.Reverse();
                    return path;
                }

                foreach (int neighbor in GetNeighborNodes(current))
                {
                    if (!cameFrom.ContainsKey(neighbor))
                    {
                        cameFrom[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // No path found
            Debug.LogWarning($"[Wisp:{name}] FindPathToNode: No path from {startNode} to {targetNode}");
            return new List<int>();
        }

        /// <summary>
        /// Finds the node closest to the given world position.
        /// </summary>
        private int FindNodeNearPosition(Vector3 worldPos)
        {
            if (mapState == null || mapState.Nodes.Count == 0)
                return -1;

            float closestDistance = float.MaxValue;
            int closestNode = 0;

            for (int i = 0; i < mapState.Nodes.Count; i++)
            {
                var node = mapState.Nodes[i];
                float distance = Vector2.Distance(
                    new Vector2(worldPos.x, worldPos.y),
                    node.Position
                );

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestNode = i;
                }
            }

            return closestNode;
        }

        #endregion

        #region Idle State

        private void UpdateIdle()
        {
            // Scan for visitors periodically
            visitorScanTimer += Time.deltaTime;
            if (visitorScanTimer >= visitorScanInterval)
            {
                visitorScanTimer = 0f;
                ScanForNearbyVisitors();
            }

            // If we have a visitor in range, transition to reacting
            if (targetVisitor != null)
            {
                return;
            }

            // Check if we have a valid spline setup
            if (splineNodeIndices[1] < 0 || mapState == null)
            {
                Debug.LogWarning($"[Wisp:{name}] UpdateIdle: Invalid state - P1={splineNodeIndices[1]}, mapState={(mapState == null ? "null" : "valid")}");
                return;
            }

            // Always move along the Catmull-Rom spline
            UpdateSplineMovement();
        }

        /// <summary>
        /// Updates movement along the Catmull-Rom spline.
        /// The wisp travels from P1 (t=0) to P2 (t=1).
        /// When P2 is reached (t >= 1), advance to the next segment.
        /// </summary>
        private void UpdateSplineMovement()
        {
            // Progress along spline based on speed (normalized by segment length)
            float speedFactor = idleSpeed / Mathf.Max(0.1f, splineSegmentLength);
            splineProgress += speedFactor * Time.deltaTime;

            // Check if we've reached P2 (t >= 1)
            if (splineProgress >= 1f)
            {
                // Advance to next segment (this resets progress to 0)
                AdvanceSplineSegment();
            }

            // Calculate position on Catmull-Rom spline
            Vector3 newPosition = EvaluateCatmullRom(
                splineNodePositions[0],
                splineNodePositions[1],
                splineNodePositions[2],
                splineNodePositions[3],
                splineProgress
            );

            // Calculate direction for animator
            Vector3 direction = (newPosition - transform.position).normalized;
            UpdateAnimatorDirection(direction);

            // Apply movement
            if (rb != null)
            {
                rb.MovePosition(newPosition);
            }
            else
            {
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// Evaluates a point on a Catmull-Rom spline.
        /// The spline passes through P1 at t=0 and P2 at t=1.
        /// P0 and P3 influence the tangents at P1 and P2 respectively.
        /// </summary>
        private Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // Catmull-Rom spline formula (uniform parameterization)
            // Using the standard centripetal formulation for smoother curves
            float t2 = t * t;
            float t3 = t2 * t;

            // Catmull-Rom basis matrix coefficients
            // P(t) = 0.5 * ((2*P1) + (-P0 + P2)*t + (2*P0 - 5*P1 + 4*P2 - P3)*t^2 + (-P0 + 3*P1 - 3*P2 + P3)*t^3)
            Vector3 result = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );

            return result;
        }

        /// <summary>
        /// Estimates the length of a Catmull-Rom spline segment using sampling.
        /// </summary>
        private float EstimateCatmullRomLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 10)
        {
            float length = 0f;
            Vector3 prev = p1; // Start at P1 (t=0)

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 current = EvaluateCatmullRom(p0, p1, p2, p3, t);
                length += Vector3.Distance(prev, current);
                prev = current;
            }

            return Mathf.Max(0.1f, length); // Ensure non-zero length
        }

        /// <summary>
        /// Scans for visitors within detection radius.
        /// </summary>
        private void ScanForNearbyVisitors()
        {
            VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in allVisitors)
            {
                if (visitor == null)
                    continue;

                // Check distance first for debug output
                float distance = Vector3.Distance(transform.position, visitor.transform.position);

                // Skip visitors not in a valid state
                if (!IsVisitorLurable(visitor.State))
                {
                    continue;
                }

                // Skip visitors already being lured by another wisp
                var followWisp = visitor.GetComponent<FollowWispBehavior>();
                if (followWisp != null && followWisp.IsFollowing)
                {
                    continue;
                }

                if (distance <= visitorDetectionRadius)
                {
                    // Found a visitor within range, transition to approaching
                    StartApproaching(visitor);
                    return;
                }
            }
        }

        private bool IsVisitorLurable(VisitorControllerBase.VisitorState visitorState)
        {
            // Note: Fascinated visitors are circling a fairy ring and cannot be lured
            return visitorState == VisitorControllerBase.VisitorState.Walking
                || visitorState == VisitorControllerBase.VisitorState.Frightened;
        }

        #endregion

        #region Approaching State

        /// <summary>
        /// Starts approaching a visitor. Phase 1: Move directly toward visitor until intersection.
        /// Phase 2: Rotate around visitor until on a valid walkable tile. Then transition to Reacting.
        /// </summary>
        private void StartApproaching(VisitorControllerBase visitor)
        {
            // Final check: ensure visitor is still lurable before committing to approach
            if (!IsVisitorLurable(visitor.State))
            {
                return;
            }

            // Check if visitor is already being followed by another wisp
            var followWisp = visitor.GetComponent<FollowWispBehavior>();
            if (followWisp != null && followWisp.IsFollowing)
            {
                return;
            }

            targetVisitor = visitor;

            // Start in phase 1 - moving directly toward visitor
            approachingPhaseRotating = false;
            approachRotationAngle = 0f;

            state = WispState.Approaching;
        }

        #endregion

        #region Reacting State

        /// <summary>
        /// Transitions to the Reacting state after approaching the visitor.
        /// Calculates a walkable path from current position to nearest Puka and starts leading the visitor.
        /// </summary>
        private void TransitionToReacting()
        {
            // Find the nearest PukaHazard to lead the visitor to
            targetPuka = FindNearestPukaHazard();

            if (targetPuka == null)
            {
                // No Puka found, return to idle
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            // Use the visitor's pathfinding to build a walkable path to the Puka
            // This ensures the wisp stays on paths that visitors can follow
            reactingWorldPath = targetVisitor.BuildWorldPath(transform.position, targetPuka.WorldPosition);
            reactingWorldPathIndex = 0;

            if (reactingWorldPath == null || reactingWorldPath.Count == 0)
            {
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            state = WispState.Reacting;

            // NOW notify the visitor to follow this wisp (we're near them on a walkable tile)
            var followWisp = targetVisitor.gameObject.GetComponent<FollowWispBehavior>();
            if (followWisp == null)
            {
                followWisp = targetVisitor.gameObject.AddComponent<FollowWispBehavior>();
            }
            followWisp.StartFollowing(this);
        }

        /// <summary>
        /// Finds the nearest walkable tile near the visitor's position.
        /// </summary>
        private Vector3 FindNearestWalkableTileNearVisitor()
        {
            if (targetVisitor == null || mazeGridBehaviour == null)
                return transform.position;

            Vector3 visitorPos = targetVisitor.transform.position;
            Vector3 bestPosition = visitorPos;
            float bestDistance = float.MaxValue;

            // Check in a grid pattern around the visitor to find the nearest walkable tile
            float searchRadius = 3f;
            float stepSize = 0.5f;

            for (float dx = -searchRadius; dx <= searchRadius; dx += stepSize)
            {
                for (float dy = -searchRadius; dy <= searchRadius; dy += stepSize)
                {
                    Vector3 testPos = new Vector3(visitorPos.x + dx, visitorPos.y + dy, transform.position.z);

                    if (mazeGridBehaviour.IsWalkableAtWorldPos(testPos))
                    {
                        float distToWisp = Vector3.Distance(transform.position, testPos);
                        if (distToWisp < bestDistance)
                        {
                            bestDistance = distToWisp;
                            bestPosition = testPos;
                        }
                    }
                }
            }

            return bestPosition;
        }

        /// <summary>
        /// Update logic for the Approaching state.
        /// Phase 1: Move directly toward visitor until within rotation radius.
        /// Phase 2: Rotate around visitor until on a valid walkable tile, then transition to Reacting.
        /// </summary>
        private void UpdateApproaching()
        {
            // Check if visitor is still valid
            if (targetVisitor == null)
            {
                ReturnToIdle();
                return;
            }

            // Check if visitor is still lurable - if not, return to idle instead of reacting
            if (!IsVisitorLurable(targetVisitor.State))
            {
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            Vector3 visitorPos = targetVisitor.transform.position;
            float distanceToVisitor = Vector3.Distance(transform.position, visitorPos);
            bool isOnWalkable = mazeGridBehaviour != null && mazeGridBehaviour.IsWalkableAtWorldPos(transform.position);

            if (!approachingPhaseRotating)
            {
                // Phase 1: Move directly toward visitor
                MoveDirectlyTowardTarget(visitorPos, idleSpeed);

                // Check if we've reached the rotation radius
                if (distanceToVisitor <= approachRotationRadius)
                {
                    // Check if we're already on a walkable tile
                    if (isOnWalkable)
                    {
                        TransitionToReacting();
                        return;
                    }

                    // Switch to rotation phase
                    approachingPhaseRotating = true;

                    // Calculate initial rotation angle from current position relative to visitor
                    Vector3 offset = transform.position - visitorPos;
                    approachRotationAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                }
            }
            else
            {
                // Phase 2: Rotate around visitor until on walkable tile
                approachRotationAngle += approachRotationSpeed * Time.deltaTime;

                // Keep angle in reasonable range
                if (approachRotationAngle >= 360f)
                    approachRotationAngle -= 360f;

                // Calculate new position on circle around visitor
                float angleRad = approachRotationAngle * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angleRad) * approachRotationRadius,
                    Mathf.Sin(angleRad) * approachRotationRadius,
                    0f
                );
                Vector3 newPosition = visitorPos + offset;
                newPosition.z = transform.position.z; // Keep same Z

                // Update animator direction based on movement
                Vector3 direction = (newPosition - transform.position).normalized;
                UpdateAnimatorDirection(direction);

                // Move to new position
                if (rb != null)
                {
                    rb.MovePosition(newPosition);
                }
                else
                {
                    transform.position = newPosition;
                }

                // Check if now on a walkable tile
                bool nowOnWalkable = mazeGridBehaviour != null && mazeGridBehaviour.IsWalkableAtWorldPos(newPosition);

                if (nowOnWalkable)
                {
                    TransitionToReacting();
                }
            }
        }

        private void UpdateReacting()
        {
            // Check if visitor is still valid
            if (targetVisitor == null)
            {
                ReturnToIdle();
                return;
            }

            // Check if visitor is no longer in a lurable state (consumed, etc.)
            if (!IsVisitorLurable(targetVisitor.State) && targetVisitor.State != VisitorControllerBase.VisitorState.Escaping)
            {
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            // Check if visitor is being drowned by the Puka (success!)
            if (targetVisitor.State == VisitorControllerBase.VisitorState.Escaping)
            {
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            // Check if Puka is still valid
            if (targetPuka == null)
            {
                // Puka was destroyed, find a new one and recalculate path
                targetPuka = FindNearestPukaHazard();
                if (targetPuka == null)
                {
                    ReleaseVisitor();
                    ReturnToIdle();
                    return;
                }

                // Recalculate walkable path to new Puka using visitor's pathfinding
                reactingWorldPath = targetVisitor.BuildWorldPath(transform.position, targetPuka.WorldPosition);
                reactingWorldPathIndex = 0;
            }

            // Check if we have a valid path
            if (reactingWorldPath == null || reactingWorldPath.Count == 0)
            {
                ReleaseVisitor();
                ReturnToIdle();
                return;
            }

            // Check if we've reached the end of the path
            if (reactingWorldPathIndex >= reactingWorldPath.Count)
            {
                // At the Puka, wait for capture
                return;
            }

            // Get current target waypoint
            Vector3 currentTarget = reactingWorldPath[reactingWorldPathIndex];

            // Check distance to visitor - if too far, stop and wait
            Vector3 visitorPos = targetVisitor.transform.position;
            float distanceToVisitor = Vector3.Distance(transform.position, visitorPos);

            // Use a maximum lead distance - wisp should never get further than this from the visitor
            // This prevents the wisp from running off and leaving the visitor behind
            float maxLeadDistance = visitorDetectionRadius * 0.75f; // Stay within 75% of detection radius

            if (distanceToVisitor > maxLeadDistance)
            {
                // Too far from visitor - stop and wait for them to catch up
                // Update animator to idle
                UpdateAnimatorDirection(Vector3.zero);
                return;
            }

            // Check if wisp is ahead of the visitor (toward the Puka)
            // Use dot product: if visitor-to-wisp direction aligns with visitor-to-puka direction, wisp is ahead
            Vector3 visitorToWisp = (transform.position - visitorPos).normalized;
            Vector3 visitorToPuka = (targetPuka.WorldPosition - visitorPos).normalized;
            float dotProduct = Vector3.Dot(visitorToWisp, visitorToPuka);

            // Also check if wisp is far enough ahead (at least 1 unit in front)
            bool isAheadOfVisitor = dotProduct > 0.5f && distanceToVisitor > 1f;

            // Calculate desired speed based on position relative to visitor
            float luringSpeed;
            if (isAheadOfVisitor)
            {
                // When ahead, match visitor speed to maintain lead distance
                // If getting close to max distance, slow down even more
                float distanceRatio = distanceToVisitor / maxLeadDistance;
                if (distanceRatio > 0.8f)
                {
                    // Very close to max distance - slow down significantly
                    luringSpeed = targetVisitor.MoveSpeed * 0.5f;
                }
                else
                {
                    // Ahead but not too far - match visitor speed
                    luringSpeed = targetVisitor.MoveSpeed;
                }
            }
            else
            {
                // Behind visitor - move faster to get ahead
                luringSpeed = reactingSpeed;
            }

            MoveDirectlyTowardTarget(currentTarget, luringSpeed);

            // Check if we've reached the current waypoint
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget);
            if (distanceToTarget <= waypointReachedDistance)
            {
                // Advance to next waypoint
                reactingWorldPathIndex++;
            }
        }

        /// <summary>
        /// Finds the nearest PukaHazard to lure the visitor to.
        /// </summary>
        private PukaHazard FindNearestPukaHazard()
        {
            PukaHazard nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var puka in PukaHazard.All)
            {
                if (puka == null || puka.State != PukaHazard.PukaState.Idle)
                    continue;

                float distance = Vector3.Distance(transform.position, puka.WorldPosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = puka;
                }
            }

            return nearest;
        }

        private void ReleaseVisitor()
        {
            if (targetVisitor != null)
            {
                var followWisp = targetVisitor.GetComponent<FollowWispBehavior>();
                if (followWisp != null)
                {
                    followWisp.StopFollowing();
                }
            }
            targetVisitor = null;
            targetPuka = null;
        }

        private void ReturnToIdle()
        {
            state = WispState.Idle;
            targetVisitor = null;
            targetPuka = null;

            // Clear approaching state
            approachTargetPosition = Vector3.zero;
            approachingPhaseRotating = false;
            approachRotationAngle = 0f;

            // Reset scan timer to prevent immediate re-targeting
            visitorScanTimer = 0f;

            // Clear reacting path state (both legacy node-based and new world path)
            reactingPath.Clear();
            reactingPathIndex = 0;
            reactingCurrentTarget = Vector3.zero;
            reactingWorldPath.Clear();
            reactingWorldPathIndex = 0;

            // Reset spline state and reinitialize from current position
            splineProgress = 0f;

            // Initialize spline from current position
            InitializeCatmullRomSpline();
        }

        #endregion

        #region Movement

        /// <summary>
        /// Moves toward target ignoring walkability (for Idle state - can pass through trees).
        /// </summary>
        private void MoveDirectlyTowardTarget(Vector3 targetPosition, float speed)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime
            );

            UpdateAnimatorDirection(direction);

            if (rb != null)
            {
                rb.MovePosition(newPosition);
            }
            else
            {
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// Moves toward target respecting walkability (for Reacting state - must follow valid path tiles).
        /// If currently stuck in a non-walkable position, will move directly toward nearest walkable tile.
        /// </summary>
        private void MoveAlongPathTowardTarget(Vector3 targetPosition, float speed)
        {
            // First check if we're currently in a walkable position
            bool currentPosWalkable = mazeGridBehaviour == null || mazeGridBehaviour.IsWalkableAtWorldPos(transform.position);

            if (!currentPosWalkable)
            {
                // We're stuck in a non-walkable position - find nearest walkable tile and move directly toward it
                Vector3 escapeTarget = FindNearestWalkableTile();
                MoveDirectlyTowardTarget(escapeTarget, speed);
                return;
            }

            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime
            );

            // Check walkability before moving
            if (mazeGridBehaviour == null || mazeGridBehaviour.IsWalkableAtWorldPos(newPosition))
            {
                UpdateAnimatorDirection(direction);

                if (rb != null)
                {
                    rb.MovePosition(newPosition);
                }
                else
                {
                    transform.position = newPosition;
                }
            }
        }

        /// <summary>
        /// Finds the nearest walkable tile to the wisp's current position.
        /// </summary>
        private Vector3 FindNearestWalkableTile()
        {
            if (mazeGridBehaviour == null)
                return transform.position;

            Vector3 bestPosition = transform.position;
            float bestDistance = float.MaxValue;

            // Check in a grid pattern around current position
            float searchRadius = 3f;
            float stepSize = 0.5f;

            for (float dx = -searchRadius; dx <= searchRadius; dx += stepSize)
            {
                for (float dy = -searchRadius; dy <= searchRadius; dy += stepSize)
                {
                    Vector3 testPos = new Vector3(transform.position.x + dx, transform.position.y + dy, transform.position.z);

                    if (mazeGridBehaviour.IsWalkableAtWorldPos(testPos))
                    {
                        float dist = Vector3.Distance(transform.position, testPos);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestPosition = testPos;
                        }
                    }
                }
            }

            return bestPosition;
        }

        private void UpdateAnimatorDirection(Vector3 direction)
        {
            if (animator == null)
                return;

            if (direction.sqrMagnitude < 0.0001f)
            {
                animator.SetInteger(DirectionParameter, 0); // Idle
                return;
            }

            int directionValue;
            if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
            {
                directionValue = direction.y > 0f ? 1 : 2; // Up : Down
            }
            else
            {
                directionValue = direction.x < 0f ? 3 : 4; // Left : Right
            }

            animator.SetInteger(DirectionParameter, directionValue);
        }

        #endregion

        #region Visual Setup

        private void SetupSpriteRenderer()
        {
            if (wispModelPrefab != null || modelInstance != null)
            {
                baseScale = new Vector3(wispSize, wispSize, 1f);
                transform.localScale = baseScale;
                spriteRenderer = null;
                return;
            }

            spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
                gameObject,
                createProceduralSprite: useProceduralSprite,
                useSoftEdges: true,
                resolution: 32,
                pixelsPerUnit: 32
            );

            ApplySpriteSettings();
        }

        private void ApplySpriteSettings()
        {
            if (spriteRenderer == null)
            {
                baseScale = initialScale;
                transform.localScale = baseScale;
                return;
            }

            baseScale = useProceduralSprite
                ? new Vector3(wispSize, wispSize, 1f)
                : initialScale;

            ProceduralSpriteFactory.ApplySpriteSettings(
                spriteRenderer,
                wispColor,
                sortingOrder,
                applyScale: false
            );
            transform.localScale = baseScale;
        }

        private void SetupColliders()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.radius = 0.4f;
            collider.isTrigger = true;
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        private void SetupGlowLight()
        {
            if (!enableGlow)
                return;

            glowLight = GetComponent<Light>();
            if (glowLight == null)
            {
                glowLight = gameObject.AddComponent<Light>();
            }

            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowRange;
            glowLight.intensity = glowMaxIntensity;
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;
            glowLight.shadows = LightShadows.None;
        }

        private void UpdateGlowPulse()
        {
            float angle = Time.time * glowFrequency * 2f * Mathf.PI;
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);
            glowLight.intensity = intensity;
        }

        private void SetupModel()
        {
            if (modelInstance != null)
            {
                return;
            }

            SpriteRenderer sprite = GetComponent<SpriteRenderer>();

            if (transform.childCount > 0)
            {
                var childAnimator = GetComponentInChildren<Animator>(true);
                if (childAnimator != null && childAnimator.gameObject != gameObject)
                {
                    modelInstance = childAnimator.gameObject;
                    animator = childAnimator;

                    if (sprite != null)
                    {
                        sprite.enabled = false;
                    }
                    return;
                }
            }

            if (wispModelPrefab == null)
            {
                return;
            }

            var instantiatedObject = (GameObject)Instantiate((UnityEngine.Object)wispModelPrefab, transform);
            if (instantiatedObject == null)
            {
                return;
            }

            modelInstance = instantiatedObject;
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            var modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
            if (modelAnimator != null)
            {
                animator = modelAnimator;
            }

            if (sprite != null)
            {
                sprite.enabled = false;
            }
        }

        private void ApplyAnimatorController()
        {
            if (animator == null || wispController == null)
            {
                return;
            }

            animator.runtimeAnimatorController = wispController;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw detection radius
            Gizmos.color = new Color(0.9f, 1f, 0.4f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, visitorDetectionRadius);

            // Draw 4-node Catmull-Rom spline when idle
            if (state == WispState.Idle && splineNodeIndices[1] >= 0)
            {
                // Draw the Catmull-Rom spline (from P1 to P2)
                Gizmos.color = Color.cyan;
                Vector3 prev = splineNodePositions[1]; // Start at P1
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    Vector3 current = EvaluateCatmullRom(
                        splineNodePositions[0],
                        splineNodePositions[1],
                        splineNodePositions[2],
                        splineNodePositions[3],
                        t
                    );
                    Gizmos.DrawLine(prev, current);
                    prev = current;
                }

                // Draw P0 (influences entry tangent) - gray/faded
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                Gizmos.DrawWireSphere(splineNodePositions[0], 0.2f);

                // Draw P1 (current start) - blue
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(splineNodePositions[1], 0.3f);

                // Draw P2 (target) - green
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(splineNodePositions[2], 0.3f);

                // Draw P3 (influences exit tangent) - gray/faded
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                Gizmos.DrawWireSphere(splineNodePositions[3], 0.2f);

                // Draw current progress point on spline
                Gizmos.color = Color.white;
                Vector3 currentSplinePos = EvaluateCatmullRom(
                    splineNodePositions[0],
                    splineNodePositions[1],
                    splineNodePositions[2],
                    splineNodePositions[3],
                    splineProgress
                );
                Gizmos.DrawWireSphere(currentSplinePos, 0.15f);

                // Draw tangent direction at current position (yellow line)
                Gizmos.color = Color.yellow;
                float dt = 0.01f;
                Vector3 tangentEnd = EvaluateCatmullRom(
                    splineNodePositions[0],
                    splineNodePositions[1],
                    splineNodePositions[2],
                    splineNodePositions[3],
                    Mathf.Min(1f, splineProgress + dt)
                );
                Vector3 tangentDir = (tangentEnd - currentSplinePos).normalized;
                Gizmos.DrawLine(currentSplinePos, currentSplinePos + tangentDir * 0.5f);
            }

            // Draw approaching state visualization
            if (state == WispState.Approaching && targetVisitor != null)
            {
                Vector3 visitorPos = targetVisitor.transform.position;

                if (approachingPhaseRotating)
                {
                    // Draw rotation circle around visitor
                    Gizmos.color = Color.yellow;
                    DrawGizmoCircle(visitorPos, approachRotationRadius, 32);

                    // Draw current rotation angle indicator
                    Gizmos.color = Color.white;
                    float angleRad = approachRotationAngle * Mathf.Deg2Rad;
                    Vector3 anglePos = visitorPos + new Vector3(
                        Mathf.Cos(angleRad) * approachRotationRadius,
                        Mathf.Sin(angleRad) * approachRotationRadius,
                        0f
                    );
                    Gizmos.DrawLine(visitorPos, anglePos);
                    Gizmos.DrawWireSphere(anglePos, 0.15f);
                }
                else
                {
                    // Draw line directly to visitor (phase 1)
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(transform.position, visitorPos);
                }
            }

            // Draw line to target visitor when reacting or approaching
            if ((state == WispState.Reacting || state == WispState.Approaching) && targetVisitor != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }

            // Draw line to target Puka when reacting
            if (state == WispState.Reacting && targetPuka != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, targetPuka.WorldPosition);
            }
        }

        private void DrawGizmoCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                );
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }

        #endregion
    }
}

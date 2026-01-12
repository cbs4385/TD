using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Base class for visitor controllers providing shared movement, pathfinding, and fascination logic.
    /// Derived classes implement specific detour behaviors (confusion, missteps, etc.).
    /// Supports optional archetype configuration for behavior customization.
    /// </summary>
    public abstract class VisitorControllerBase : MonoBehaviour, IArchetypedVisitor
    {
        #region Enums

        public enum VisitorState
        {
            Idle,
            Walking,
            Fascinated,
            Confused,
            Frightened,
            Mesmerized,    // New: entranced/hypnotized state
            Lost,          // New: wandering aimlessly state
            Lured,         // New: drawn toward the Heart by Murmuring Paths
            Consumed,
            Escaping
        }

        #endregion

        #region Serialized Fields

        [Header("Archetype Configuration")]
        [SerializeField]
        [Tooltip("Optional archetype configuration defining behavioral parameters (fascination, confusion, rewards, etc.)")]
        protected VisitorArchetypeConfig config;

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed in units per second")]
        protected float moveSpeed = 3f;

        [Header("Path Following")]
        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        protected float waypointReachedDistance = 0.05f;

        [SerializeField]
        [Tooltip("Enable verbose logging for visitor pathfinding diagnostics")]
        protected bool logVisitorPathfinding = false;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use 3D model instead of sprite-based rendering")]
        protected bool use3DModel = true;

        [SerializeField]
        [Tooltip("3D model prefab to instantiate for this visitor")]
        protected GameObject modelPrefab;

        [SerializeField]
        [Tooltip("Color of the visitor sprite (2D mode only)")]
        protected Color visitorColor = new Color(0.3f, 0.6f, 1f, 1f);

        [SerializeField]
        [Tooltip("Desired world-space diameter (in Unity units) for procedural visitors")]
        protected float visitorSize = 30.0f;

        [SerializeField]
        [Tooltip("Pixels per unit for procedural visitor sprites (match imported visitor assets)")]
        protected int proceduralPixelsPerUnit = 32;

        [SerializeField]
        [Tooltip("Sprite rendering layer order (2D mode only)")]
        protected int sortingOrder = 15;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations (2D mode only)")]
        protected bool useProceduralSprite = false;

        [Header("State Duration Settings")]
        [SerializeField]
        [Tooltip("Default duration for Mesmerized state (seconds)")]
        protected float mesmerizedDuration = 5f;

        [SerializeField]
        [Tooltip("Default duration for Lost state (seconds)")]
        protected float lostDuration = 10f;

        [SerializeField]
        [Tooltip("Default duration for Frightened state (seconds)")]
        protected float frightenedDuration = 3f;

        [Header("Red Cap Detection")]
        [SerializeField]
        [Tooltip("Distance to detect Red Caps and become frightened")]
        protected float redCapDetectionRadius = 5f;

        [SerializeField]
        [Tooltip("How often to check for nearby Red Caps (seconds)")]
        protected float redCapDetectionInterval = 0.5f;

        [Header("Lost Mode Settings")]
        [SerializeField]
        [Tooltip("Minimum detour path length for Lost state")]
        protected int minLostDistance = 10;

        [SerializeField]
        [Tooltip("Maximum detour path length for Lost state")]
        protected int maxLostDistance = 20;

        #endregion

        #region Protected Fields

        protected bool hasLoggedPathIssue;
        protected VisitorState state;
        protected Animator animator;
        protected GameController gameController;
        protected MazeGridBehaviour mazeGridBehaviour;
        protected bool isEntranced;
        protected float speedMultiplier = 1f;

        // Rendering
        protected SpriteRenderer spriteRenderer;
        protected GameObject modelInstance;
        protected Quaternion modelBaseRotation;
        protected bool modelBaseRotationCaptured;

        // 3D physics
        protected Rigidbody rb3D;

        protected Vector2 authoredSpriteWorldSize;
        protected Vector3 originalDestination;
        protected bool usesSpawnMarkerDestination;

        protected bool isCalculatingPath;

        // Fascination state (for FaeLantern)
        protected bool isFascinated;
        protected Vector3 fascinationLanternPosition;
        protected bool hasReachedLantern;
        protected float fascinationTimer;
        protected FaeMaze.Props.FaeLantern currentFaeLantern;

        // Cooldown tracking per lantern (prevents immediate re-triggering)
        protected Dictionary<FaeMaze.Props.FaeLantern, float> lanternCooldowns;

        protected Vector3 initialScale;

        protected const string DirectionParameter = "Direction";
        protected const int IdleDirection = 0;
        protected const float MovementEpsilonSqr = 0.0001f;
        protected const float StallLoggingDelaySeconds = 0.35f;
        protected const float StallRouteLogDumpDelaySeconds = 10f;

        // Cached direction to prevent animation flickering when movement delta is small
        protected int lastDirection = IdleDirection;
        protected int currentAnimatorDirection = IdleDirection;

        protected int waypointsTraversedSinceSpawn;
        protected int lastLoggedWaypointIndex = -1;
        protected float stalledDuration;
        protected bool hasLoggedCurrentStall;
        protected bool hasMovedSignificantly;
        protected bool isCurrentlyStalled;
        protected bool hasDumpedStallRouteLog;

        // State tracking for path recalculation
        protected VisitorState previousState = VisitorState.Idle;
        protected bool pendingPathRecalculation;

        // State duration tracking (for timed states like Mesmerized, Lost, Frightened, etc.)
        protected VisitorState currentTimedState = VisitorState.Idle;
        protected float currentStateDuration;
        protected float currentStateTimer;
        protected bool isMesmerized;
        protected bool isLost;
        protected bool isFrightened;
        protected bool isLured;

        // Red Cap detection tracking
        protected float redCapDetectionTimer;

        // Confusion state (simplified for world-space navigation)
        protected bool isConfused;

        // World-space navigation
        protected Vector3 worldDestination;
        protected List<Vector3> worldPath;
        protected int worldPathIndex;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public abstract VisitorState State { get; }

        /// <summary>Gets the current move speed</summary>
        public abstract float MoveSpeed { get; }

        /// <summary>Gets whether this visitor is entranced by a Fairy Ring</summary>
        public abstract bool IsEntranced { get; }

        /// <summary>Gets or sets the speed multiplier applied to movement</summary>
        public abstract float SpeedMultiplier { get; set; }

        /// <summary>Gets whether this visitor is fascinated by a FaeLantern</summary>
        public abstract bool IsFascinated { get; }

        /// <summary>Gets the visitor's archetype (from config if available)</summary>
        public VisitorArchetype Archetype => config != null ? config.Archetype : VisitorArchetype.LanternDrunk;

        /// <summary>Gets the visitor's archetype configuration</summary>
        public VisitorArchetypeConfig ArchetypeConfig => config;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            state = VisitorState.Idle;
            lanternCooldowns = new Dictionary<FaeMaze.Props.FaeLantern, float>();
            initialScale = transform.localScale;

            // Look for Animator on this GameObject or children (for Blender imports)
            animator = GetComponentInChildren<Animator>();

            // Look for SpriteRenderer
            if (useProceduralSprite)
            {
                // Will be created by SetupSpriteRenderer
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            else
            {
                // Use existing SpriteRenderer (may be on child object for Blender imports)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Setup visual representation based on mode
            if (use3DModel)
            {
                Setup3DModel();
            }
            else
            {
                CacheAuthoredSpriteSize();
                SetupSpriteRenderer();
            }

            SetupPhysics();

            // Initialize animator direction if animator is present
            if (animator != null)
            {
                SetAnimatorDirection(IdleDirection);
            }

            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            hasMovedSignificantly = false;
            isCurrentlyStalled = false;
            hasDumpedStallRouteLog = false;

            // Apply archetype-specific settings if config is available
            if (config != null)
            {
                moveSpeed = config.BaseSpeed;
                mesmerizedDuration = config.InitialMesmerizedDuration;
                frightenedDuration = config.FrightenedDuration;
                minLostDistance = Mathf.RoundToInt(config.LostDetourMin);
                maxLostDistance = Mathf.RoundToInt(config.LostDetourMax);
            }

            // Apply player's speed setting (as multiplier, default 3f = 1x speed)
            moveSpeed *= (GameSettings.VisitorSpeed / 3f);
        }

        protected virtual void OnEnable()
        {
            // Register with the visitor registry for efficient lookups
            VisitorRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            // Unregister from the visitor registry
            VisitorRegistry.Unregister(this);
        }

        protected virtual void Update()
        {
            if (pendingPathRecalculation)
            {
                pendingPathRecalculation = false;
                UpdateDestinationIfExitRemoved();
                RecalculatePath();
            }

            // Update state duration timers for timed states
            if (currentTimedState != VisitorState.Idle && currentStateDuration > 0)
            {
                currentStateTimer -= Time.deltaTime;
                if (currentStateTimer <= 0f)
                {
                    OnStateExpired(currentTimedState);
                }
            }

            // Update lantern cooldowns
            if (lanternCooldowns != null && lanternCooldowns.Count > 0)
            {
                List<FaeMaze.Props.FaeLantern> lanternsToUpdate = new List<FaeMaze.Props.FaeLantern>(lanternCooldowns.Keys);
                foreach (var lantern in lanternsToUpdate)
                {
                    if (lantern != null)
                    {
                        lanternCooldowns[lantern] -= Time.deltaTime;
                        if (lanternCooldowns[lantern] <= 0f)
                        {
                            lanternCooldowns.Remove(lantern);
                        }
                    }
                }
            }

            // Check for nearby Red Caps
            redCapDetectionTimer -= Time.deltaTime;
            if (redCapDetectionTimer <= 0f)
            {
                redCapDetectionTimer = redCapDetectionInterval;
                CheckForNearbyRedCaps();
            }

            // Check for FaeLantern influence (world-space detection)
            if (IsMovementState(state))
            {
                bool pausedAtLantern = isFascinated && hasReachedLantern && fascinationTimer > 0;
                if (!pausedAtLantern)
                {
                    CheckFaeLanternInfluence();
                }
            }

            // Handle fascination timer (2-second pause at lantern)
            if (isFascinated && hasReachedLantern && fascinationTimer > 0)
            {
                fascinationTimer -= Time.deltaTime;
                SetAnimatorDirection(IdleDirection);
                return; // Don't move while fascinated timer is active
            }

            if (IsMovementState(state))
            {
                if (!isCalculatingPath)
                {
                    UpdateWalking();
                }
            }
        }

        #endregion

        #region Helper Methods

        public void FlagPathRecalculation()
        {
            pendingPathRecalculation = true;
        }

        /// <summary>
        /// Applies a world-space offset to the visitor's destination.
        /// Used when maze coordinates are shifted.
        /// </summary>
        public void ApplyWorldOffset(Vector3 worldOffset)
        {
            if (worldOffset == Vector3.zero)
            {
                return;
            }

            originalDestination += worldOffset;

            if (fascinationLanternPosition != Vector3.zero)
            {
                fascinationLanternPosition += worldOffset;
            }

            if (worldPath != null)
            {
                for (int i = 0; i < worldPath.Count; i++)
                {
                    worldPath[i] += worldOffset;
                }
            }

            worldDestination += worldOffset;
        }

        private void UpdateDestinationIfExitRemoved()
        {
            // In world-space mode, navigate to heart if destination is invalid
            if (mazeGridBehaviour != null)
            {
                SetWorldDestination(mazeGridBehaviour.HeartWorldPosition);
            }
        }

        /// <summary>
        /// Retargets visitor to the heart. Called when exits are removed.
        /// </summary>
        public void RetargetToHeart()
        {
            if (mazeGridBehaviour != null)
            {
                Debug.Log($"[{name}] Retargeting to heart");
                SetWorldDestination(mazeGridBehaviour.HeartWorldPosition);
            }
        }

        private string FormatWorldPath(List<Vector3> candidatePath)
        {
            if (candidatePath == null || candidatePath.Count == 0)
            {
                return "<empty>";
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < candidatePath.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" -> ");
                }

                sb.Append(i);
                sb.Append(':');
                sb.Append(candidatePath[i].ToString("F1"));
            }

            return sb.ToString();
        }

        private bool ShouldLogVisitorPath()
        {
            return false;
        }

        /// <summary>
        /// Checks if the animator has a parameter with the given name.
        /// </summary>
        private bool HasAnimatorParameter(string parameterName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            foreach (var param in animator.parameters)
            {
                if (param.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        protected void LogVisitorPath(string message)
        {
            // Logging disabled in world-space mode
        }

        protected bool LogVisitorPathWarning(string message)
        {
            return false;
        }

        private void UpdatePathLoggingOnMovement(Vector3 previousPosition, Vector3 currentPosition)
        {
            float deltaSqr = (currentPosition - previousPosition).sqrMagnitude;

            if (!hasMovedSignificantly && deltaSqr > MovementEpsilonSqr)
            {
                hasMovedSignificantly = true;
            }

            bool isStationary = deltaSqr <= MovementEpsilonSqr;

            if (isStationary)
            {
                isCurrentlyStalled = true;
                stalledDuration += Time.deltaTime;
            }
            else
            {
                isCurrentlyStalled = false;
                hasMovedSignificantly = true;

                stalledDuration = 0f;
                hasLoggedCurrentStall = false;
                hasDumpedStallRouteLog = false;
            }
        }

        protected bool IsMovementState(VisitorState visitorState)
        {
            return visitorState == VisitorState.Walking
                || visitorState == VisitorState.Fascinated
                || visitorState == VisitorState.Confused
                || visitorState == VisitorState.Frightened
                || visitorState == VisitorState.Mesmerized
                || visitorState == VisitorState.Lost
                || visitorState == VisitorState.Lured;
        }

        protected virtual void RefreshStateFromFlags()
        {
            // Terminal states that cannot be overridden
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Timed states take priority (in order of precedence)
            if (isMesmerized)
            {
                state = VisitorState.Mesmerized;
            }
            else if (isFrightened)
            {
                state = VisitorState.Frightened;
            }
            else if (isLost)
            {
                state = VisitorState.Lost;
            }
            else if (isFascinated)
            {
                state = VisitorState.Fascinated;
            }
            else if (isLured)
            {
                state = VisitorState.Lured;
            }
            else if (state == VisitorState.Confused)
            {
                // Confused state managed by derived classes, don't override
                return;
            }
            else
            {
                state = VisitorState.Walking;
            }
        }

        #endregion

        #region Archetype-Aware Behavior Methods

        /// <summary>
        /// Gets the fascination chance for this visitor.
        /// Override to apply additional modifiers (e.g., from Heart powers).
        /// </summary>
        public virtual float GetFascinationChance()
        {
            return config != null ? config.FascinationChance : 0.5f;
        }

        /// <summary>
        /// Gets the fascination duration range for this visitor.
        /// </summary>
        public virtual (float min, float max) GetFascinationDuration()
        {
            if (config != null)
                return (config.FascinationDurationMin, config.FascinationDurationMax);
            return (2f, 5f);
        }

        /// <summary>
        /// Gets the confusion/misstep chance at intersections for this visitor.
        /// </summary>
        public virtual float GetConfusionChance()
        {
            return config != null ? config.ConfusionIntersectionChance : 0.25f;
        }

        /// <summary>
        /// Gets the frightened speed multiplier for this visitor.
        /// </summary>
        public virtual float GetFrightenedSpeedMultiplier()
        {
            return config != null ? config.FrightenedSpeedMultiplier : 1.2f;
        }

        /// <summary>
        /// Returns whether frightened visitors of this type prefer exits over the heart.
        /// </summary>
        public virtual bool ShouldFrightenedPreferExit()
        {
            return config != null && config.FrightenedPrefersExit;
        }

        /// <summary>
        /// Gets the essence reward for consuming this visitor.
        /// </summary>
        public virtual int GetEssenceReward()
        {
            return config != null ? config.EssenceReward : 100;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the visitor with a reference to the game controller.
        /// </summary>
        public virtual void Initialize(GameController controller)
        {
            gameController = controller;

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }
        }

        /// <summary>
        /// Initializes the visitor using the static GameController instance.
        /// </summary>
        public virtual void Initialize()
        {
            Initialize(GameController.Instance);
        }

        #endregion

        #region Path Management

        /// <summary>
        /// Sets a world-space destination for the visitor to walk toward.
        /// Uses world-space navigation along the graph edges.
        /// </summary>
        public virtual void SetWorldDestination(Vector3 destination)
        {
            worldDestination = destination;
            originalDestination = destination;
            worldPathIndex = 0;

            // Reset state tracking
            waypointsTraversedSinceSpawn = 0;
            ResetDetourState();
            hasLoggedPathIssue = false;
            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            hasMovedSignificantly = false;
            isCurrentlyStalled = false;
            hasDumpedStallRouteLog = false;
            previousState = state;

            // Build world-space path from current position to destination
            worldPath = BuildWorldPath(transform.position, destination);

            if (worldPath != null && worldPath.Count > 0)
            {
                state = VisitorState.Walking;

                if (ShouldLogVisitorPath())
                {
                    LogVisitorPath($"SetWorldDestination to {destination}. Path has {worldPath.Count} waypoints.");
                }
            }
            else
            {
                Debug.LogWarning($"[{name}] SetWorldDestination: No path found from {transform.position} to {destination}");
                state = VisitorState.Idle;
            }
        }

        /// <summary>
        /// Builds a world-space path from start to end using graph edges.
        /// Returns a list of world positions to traverse.
        /// </summary>
        protected virtual List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                // Fallback: direct path
                return new List<Vector3> { end };
            }

            var graphState = mazeGridBehaviour.ForestMapState;
            var result = new List<Vector3>();

            // Convert Vector3 to Vector2 for node lookup (positions are already in world space)
            Vector2 startPos2D = new Vector2(start.x, start.y);
            Vector2 endPos2D = new Vector2(end.x, end.y);

            // Find nearest node to start
            int startNodeIndex = FindNearestNodeIndex(graphState, startPos2D);

            // Find nearest node to end (usually the heart at index 0)
            int endNodeIndex = FindNearestNodeIndex(graphState, endPos2D);

            if (startNodeIndex < 0 || endNodeIndex < 0)
            {
                // Fallback: direct path
                return new List<Vector3> { end };
            }

            // BFS to find path through nodes
            var nodePath = FindNodePath(graphState, startNodeIndex, endNodeIndex);

            if (nodePath == null || nodePath.Count == 0)
            {
                // Fallback: direct path
                return new List<Vector3> { end };
            }

            // Convert node path to world positions following edge polylines
            // Add start position first
            result.Add(start);

            // Check if start is on a partial edge endpoint (spawn point)
            // If so, add that partial edge's polyline (reversed from endpoint toward connected node)
            ForestMaze.PlanarForestMazeGenerator.Edge startPartialEdge = FindPartialEdgeAtPosition(graphState, startPos2D);
            if (startPartialEdge != null && startPartialEdge.PolylinePoints.Count > 0)
            {
                // Add polyline points from endpoint toward connected node (reversed order)
                // Polyline points are already in world space
                for (int p = startPartialEdge.PolylinePoints.Count - 2; p >= 0; p--)
                {
                    var pt = startPartialEdge.PolylinePoints[p];
                    Vector3 worldPt = new Vector3(pt.x, pt.y, start.z);
                    result.Add(worldPt);
                }
            }

            // For each pair of consecutive nodes, find the connecting edge and add its polyline points
            for (int i = 0; i < nodePath.Count - 1; i++)
            {
                int nodeA = nodePath[i];
                int nodeB = nodePath[i + 1];

                // Find the edge connecting these nodes
                ForestMaze.PlanarForestMazeGenerator.Edge connectingEdge = null;
                bool reversePolyline = false;

                foreach (var edge in graphState.Edges)
                {
                    if (!edge.Partial && edge.NodeB.HasValue)
                    {
                        if (edge.NodeA == nodeA && edge.NodeB.Value == nodeB)
                        {
                            connectingEdge = edge;
                            reversePolyline = false;
                            break;
                        }
                        else if (edge.NodeA == nodeB && edge.NodeB.Value == nodeA)
                        {
                            connectingEdge = edge;
                            reversePolyline = true;
                            break;
                        }
                    }
                }

                if (connectingEdge != null && connectingEdge.PolylinePoints.Count > 0)
                {
                    // Add polyline points in correct order (already in world space)
                    if (reversePolyline)
                    {
                        for (int p = connectingEdge.PolylinePoints.Count - 1; p >= 0; p--)
                        {
                            var pt = connectingEdge.PolylinePoints[p];
                            Vector3 worldPt = new Vector3(pt.x, pt.y, start.z);
                            result.Add(worldPt);
                        }
                    }
                    else
                    {
                        for (int p = 0; p < connectingEdge.PolylinePoints.Count; p++)
                        {
                            var pt = connectingEdge.PolylinePoints[p];
                            Vector3 worldPt = new Vector3(pt.x, pt.y, start.z);
                            result.Add(worldPt);
                        }
                    }
                }
                else
                {
                    // Fallback: add node position directly (already in world space)
                    var node = graphState.Nodes[nodeB];
                    Vector3 worldPt = new Vector3(node.Position.x, node.Position.y, start.z);
                    result.Add(worldPt);
                }
            }

            // Ensure we end exactly at the destination
            if (result.Count > 0 && Vector3.Distance(result[result.Count - 1], end) > 0.1f)
            {
                result.Add(end);
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest node index to a position.
        /// </summary>
        protected int FindNearestNodeIndex(ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 position)
        {
            int nearest = -1;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < state.Nodes.Count; i++)
            {
                float dist = Vector2.Distance(state.Nodes[i].Position, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Finds a path through nodes using BFS.
        /// </summary>
        protected List<int> FindNodePath(ForestMaze.PlanarForestMazeGenerator.ForestMapState state, int startNode, int endNode)
        {
            if (startNode == endNode)
            {
                return new List<int> { startNode };
            }

            // Build adjacency from edges
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < state.Nodes.Count; i++)
            {
                adjacency[i] = new List<int>();
            }

            foreach (var edge in state.Edges)
            {
                if (!edge.Partial && edge.NodeB.HasValue) // Only use complete edges
                {
                    adjacency[edge.NodeA].Add(edge.NodeB.Value);
                    adjacency[edge.NodeB.Value].Add(edge.NodeA);
                }
            }

            // BFS
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            var parent = new Dictionary<int, int>();

            queue.Enqueue(startNode);
            visited.Add(startNode);
            parent[startNode] = -1;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                if (current == endNode)
                {
                    // Reconstruct path
                    var path = new List<int>();
                    int node = endNode;
                    while (node != -1)
                    {
                        path.Add(node);
                        node = parent[node];
                    }
                    path.Reverse();
                    return path;
                }

                foreach (int neighbor in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Finds a partial edge whose endpoint is near the given graph position.
        /// Used to determine if a visitor is at a spawn point on a frontier edge.
        /// </summary>
        protected ForestMaze.PlanarForestMazeGenerator.Edge FindPartialEdgeAtPosition(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 graphPosition)
        {
            const float EndpointTolerance = 2.0f; // Graph units tolerance for matching

            foreach (var edge in state.Edges)
            {
                if (!edge.Partial || edge.PolylinePoints.Count == 0)
                    continue;

                // Check if the position matches the endpoint of this partial edge
                Vector2 endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                float distance = Vector2.Distance(endpoint, graphPosition);

                if (distance < EndpointTolerance)
                {
                    return edge;
                }
            }

            return null;
        }

        #endregion

        #region Movement

        protected void UpdateAnimatorDirection(Vector2 movement)
        {
            // Apply smooth rotation for any model (2D or 3D) to face movement direction
            // All rotation is around Z axis only, applied on top of the model's base rotation
            if (movement.sqrMagnitude > MovementEpsilonSqr)
            {
                // Calculate Z rotation angle for model facing direction
                // Model default (0° Z rotation) faces Down (-Y), so:
                // - Down (-Y): 0°, Up (+Y): 180°, Left (-X): 90°, Right (+X): -90°
                // Formula derived from discrete direction mappings
                float angle = -Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg - 90f;

                // Direction rotation is Z-axis only
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, angle);

                if (use3DModel && modelInstance != null && modelBaseRotationCaptured)
                {
                    // Apply direction rotation on top of the model's base rotation (preserves X/Y orientation)
                    Quaternion targetRotation = directionRotation * modelBaseRotation;
                    modelInstance.transform.localRotation = Quaternion.Slerp(
                        modelInstance.transform.localRotation,
                        targetRotation,
                        Time.deltaTime * 10f
                    );
                }
                else if (use3DModel && modelInstance != null)
                {
                    // Fallback if base rotation not captured - apply Z rotation directly
                    Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
                    modelInstance.transform.localRotation = Quaternion.Slerp(
                        modelInstance.transform.localRotation,
                        targetRotation,
                        Time.deltaTime * 10f
                    );
                }
                else if (use3DModel)
                {
                    // Apply Z rotation to visitor transform (no model instance)
                    Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
                    transform.localRotation = Quaternion.Slerp(
                        transform.localRotation,
                        targetRotation,
                        Time.deltaTime * 10f
                    );
                }
                else if (animator != null)
                {
                    // Apply Z rotation to animator transform
                    Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
                    animator.transform.localRotation = Quaternion.Slerp(
                        animator.transform.localRotation,
                        targetRotation,
                        Time.deltaTime * 10f
                    );
                }
            }

            SetAnimatorDirection(GetDirectionFromMovement(movement));
        }

        /// <summary>
        /// Allows external behaviours (e.g., wisp-following) to update the animator's facing direction.
        /// </summary>
        /// <param name="movement">The movement or desired facing vector.</param>
        public void ApplyExternalAnimatorDirection(Vector2 movement)
        {
            UpdateAnimatorDirection(movement);
        }

        protected void SetAnimatorDirection(int direction)
        {
            if (animator == null)
            {
                return;
            }

            // For 3D models with humanoid rigs, use Speed parameter instead of Direction
            if (use3DModel)
            {
                // Set Speed parameter for blend trees (common in humanoid animations)
                // 0 = idle, 1 = walking/running
                float speed = direction == IdleDirection ? 0f : 1f;

                // Check if the animator has the Speed parameter
                if (HasAnimatorParameter("Speed"))
                {
                    animator.SetFloat("Speed", speed);
                }

                // Also set Direction parameter if it exists (for compatibility)
                if (currentAnimatorDirection != direction && HasAnimatorParameter(DirectionParameter))
                {
                    animator.SetInteger(DirectionParameter, direction);
                    currentAnimatorDirection = direction;
                }

                // Rotation is handled in UpdateAnimatorDirection for smooth 3D rotation
                return;
            }

            // 2D sprite-based animation with Direction parameter
            if (currentAnimatorDirection != direction && HasAnimatorParameter(DirectionParameter))
            {
                animator.SetInteger(DirectionParameter, direction);
                currentAnimatorDirection = direction;
            }

            // Rotate the visual model to face the correct direction (2D sprites only)
            // Only rotate if not using procedural sprites
            // Apply rotation every frame to ensure it's set (handles initialization and state changes)
            if (!useProceduralSprite && animator != null)
            {
                // For Idle state, use the last movement direction to maintain facing
                int rotationDirection = direction;
                if (rotationDirection == IdleDirection && lastDirection != IdleDirection)
                {
                    rotationDirection = lastDirection;
                }
                // If still idle (never moved), default to facing down
                if (rotationDirection == IdleDirection)
                {
                    rotationDirection = 2; // Down
                }

                float zRotation = 0f;
                switch (rotationDirection)
                {
                    case 1: // Up (+Y) - swapped with Down due to Blender animation orientation
                        zRotation = 180f;
                        break;
                    case 2: // Down (-Y) - swapped with Up due to Blender animation orientation
                        zRotation = 0f;
                        break;
                    case 3: // Left (-X)
                        zRotation = -90f;
                        break;
                    case 4: // Right (+X)
                        zRotation = 90f;
                        break;
                }

                // Apply rotation to the animator's transform (the child visual object)
                // Only rotate around Z axis to point forward axis towards movement direction
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, zRotation);
                animator.transform.localRotation = directionRotation;
            }
        }

        protected int GetDirectionFromMovement(Vector2 movement)
        {
            // Use a higher threshold based on movement speed to avoid flickering
            float movementThreshold = moveSpeed * Time.deltaTime * 0.1f;
            float movementThresholdSqr = movementThreshold * movementThreshold;

            // If movement is below threshold but we're walking, retain the last direction
            if (movement.sqrMagnitude <= movementThresholdSqr)
            {
                // Only return idle if we're actually stopped (not in an active movement state)
                if (!IsMovementState(state))
                {
                    return IdleDirection;
                }

                // While walking with small movement delta, retain last direction
                return lastDirection;
            }

            // Movement is significant - calculate new direction
            float absX = Mathf.Abs(movement.x);
            float absY = Mathf.Abs(movement.y);

            // Require a clear dominant axis to prevent flickering when values are close
            float axisDifference = Mathf.Abs(absX - absY);
            float axisMin = Mathf.Min(absX, absY);

            if (axisDifference < axisMin * 0.2f && lastDirection != IdleDirection)
            {
                // Axes are too close - retain last direction to prevent flickering
                return lastDirection;
            }

            int newDirection;
            if (absY >= absX)
            {
                // Vertical movement dominant
                newDirection = movement.y > 0f ? 1 : 2; // 1 = Up, 2 = Down
            }
            else
            {
                // Horizontal movement dominant
                newDirection = movement.x < 0f ? 3 : 4; // 3 = Left, 4 = Right
            }

            // Update cached direction (only cache non-idle directions)
            if (newDirection != IdleDirection)
            {
                lastDirection = newDirection;
            }

            return newDirection;
        }

        protected virtual void UpdateWalking()
        {
            // All navigation is now world-space based
            UpdateWorldSpaceWalking();
        }

        /// <summary>
        /// Handles movement in world-space navigation mode.
        /// Walks along the world path toward the destination.
        /// </summary>
        protected virtual void UpdateWorldSpaceWalking()
        {
            if (worldPath == null || worldPath.Count == 0)
            {
                state = VisitorState.Idle;
                SetAnimatorDirection(IdleDirection);
                return;
            }

            if (worldPathIndex >= worldPath.Count)
            {
                // Path complete - arrived at destination
                OnWorldSpacePathComplete();
                return;
            }

            // Get current target waypoint
            Vector3 targetWorldPos = worldPath[worldPathIndex];
            float effectiveSpeed = moveSpeed * speedMultiplier;

            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            Vector3 movementDelta = newPosition - transform.position;
            UpdateAnimatorDirection(movementDelta);

            // Apply movement using 3D physics
            if (rb3D != null)
            {
                rb3D.MovePosition(newPosition);
                Physics.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if we've reached the waypoint
            float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
            if (distanceToTarget < waypointReachedDistance)
            {
                waypointsTraversedSinceSpawn++;

                // Allow derived classes to handle detour logic at waypoints
                HandleDetourAtWaypoint();
            }
        }

        /// <summary>
        /// Called when visitor reaches a waypoint. Override in derived classes
        /// to implement detour behaviors (confusion, missteps, etc.).
        /// Base implementation just advances to the next waypoint.
        /// </summary>
        protected virtual void HandleDetourAtWaypoint()
        {
            // Default: just advance to next waypoint
            if (worldPath != null && worldPathIndex < worldPath.Count)
            {
                worldPathIndex++;
                if (worldPathIndex >= worldPath.Count)
                {
                    OnPathCompleted();
                }
            }
        }

        /// <summary>
        /// Called when the visitor completes their path.
        /// Delegates to OnWorldSpacePathComplete for destination handling.
        /// </summary>
        protected virtual void OnPathCompleted()
        {
            OnWorldSpacePathComplete();
        }

        /// <summary>
        /// Called when the visitor reaches its world-space destination.
        /// </summary>
        protected virtual void OnWorldSpacePathComplete()
        {
            if (ShouldLogVisitorPath())
            {
                LogVisitorPath($"reached world-space destination at {worldDestination}");
            }

            // Check if at the heart (destination is near the heart)
            if (mazeGridBehaviour != null)
            {
                float distToHeart = Vector3.Distance(transform.position, mazeGridBehaviour.HeartWorldPosition);
                if (distToHeart < 5f) // Within 5 units of heart
                {
                    // Visitor has reached the heart - trigger consumed
                    state = VisitorState.Consumed;
                    OnConsumedByHeart();
                    return;
                }
            }

            // Otherwise, just become idle
            state = VisitorState.Idle;
        }

        /// <summary>
        /// Called when a visitor is consumed by the Heart of the Maze.
        /// Override in derived classes for specific behavior.
        /// </summary>
        protected virtual void OnConsumedByHeart()
        {
            // Award essence if using heart destination (not spawn marker escape)
            if (gameController != null)
            {
                int essenceReward = config != null ? config.EssenceReward : 10;
                gameController.AddEssence(essenceReward);
            }

            // Destroy the visitor
            Destroy(gameObject, 0.1f);
        }

        /// <summary>
        /// Called when visitor is consumed by the heart.
        /// Delegates to Heart for essence reward and destruction.
        /// </summary>
        protected virtual void HandleConsumption()
        {
            if (gameController != null && gameController.Heart != null)
            {
                gameController.Heart.OnVisitorConsumed(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region FaeLantern Detection

        protected void ClearLanternInteraction()
        {
            if (currentFaeLantern != null)
            {
                currentFaeLantern.SetIdleDirection();
            }

            currentFaeLantern = null;
            fascinationLanternPosition = Vector3.zero;
        }

        /// <summary>
        /// Checks if the visitor has entered any FaeLantern's influence area using world-space distance.
        /// </summary>
        protected virtual void CheckFaeLanternInfluence()
        {
            // Check all active FaeLanterns using world-space distance
            foreach (var lantern in FaeMaze.Props.FaeLantern.All)
            {
                if (lantern == null)
                    continue;

                // Check if visitor is within lantern's influence radius (world-space)
                float distanceToLantern = Vector3.Distance(transform.position, lantern.transform.position);
                if (distanceToLantern <= lantern.InfluenceRadius)
                {
                    EnterFaeInfluence(lantern);
                    break; // Only one lantern can capture a visitor
                }
            }
        }

        /// <summary>
        /// Called when a visitor enters a FaeLantern's influence area.
        /// Uses archetype-specific fascination parameters if config is available.
        /// </summary>
        protected virtual void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern)
        {
            Vector3 lanternWorldPos = lantern.transform.position;

            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && Vector3.Distance(fascinationLanternPosition, lanternWorldPos) < 0.1f)
                return;

            // Use archetype-specific cooldown if config available, otherwise use lantern's cooldown
            float cooldown = config != null ? config.FascinationCooldown : lantern.CooldownSec;

            // Check cooldown (prevents immediate re-triggering)
            if (lanternCooldowns.ContainsKey(lantern) && lanternCooldowns[lantern] > 0f)
            {
                return;
            }

            // Use archetype-specific fascination chance
            float fascinationChance = GetFascinationChance();
            float roll = Random.value;
            if (roll > fascinationChance)
            {
                // Set cooldown even on failed proc to prevent spam checks
                lanternCooldowns[lantern] = cooldown;
                return;
            }

            // Allow re-fascination by a different lantern
            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lanternWorldPos;
            hasReachedLantern = false;
            fascinationTimer = 0f; // Will be set when reaching lantern

            // Set archetype-specific cooldown for this lantern
            lanternCooldowns[lantern] = cooldown;

            // Reset detour state
            ResetDetourState();

            // Navigate to lantern using world-space path
            worldPath = BuildWorldPath(transform.position, lanternWorldPos);
            worldPathIndex = 0;
            RefreshStateFromFlags();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stops the visitor's movement.
        /// </summary>
        public virtual void Stop()
        {
            state = VisitorState.Idle;
            SetAnimatorDirection(IdleDirection);
        }

        /// <summary>
        /// Resumes the visitor's movement if they have a world path.
        /// </summary>
        public virtual void Resume()
        {
            if (worldPath != null && worldPath.Count > 0 && worldPathIndex < worldPath.Count)
            {
                RefreshStateFromFlags();
            }
        }

        /// <summary>
        /// Checks if a world-space path exists between two positions.
        /// </summary>
        protected bool HasWorldPath(Vector3 start, Vector3 destination)
        {
            var testPath = BuildWorldPath(start, destination);
            return testPath != null && testPath.Count > 0;
        }

        /// <summary>
        /// Gets the destination for the current visitor state.
        /// </summary>
        protected virtual Vector3 GetDestinationForCurrentState()
        {
            // Default to heart as destination
            if (mazeGridBehaviour != null)
            {
                return mazeGridBehaviour.HeartWorldPosition;
            }
            return originalDestination;
        }

        /// <summary>
        /// Recalculates the path to the current destination.
        /// </summary>
        public virtual void RecalculatePath()
        {
            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Fascinated visitors use special behavior, not recalculation
            if (isFascinated)
            {
                return;
            }

            isCalculatingPath = true;

            Vector3 destination = GetDestinationForCurrentState();

            worldPath = BuildWorldPath(transform.position, destination);
            worldPathIndex = 0;
            worldDestination = destination;

            if (worldPath != null && worldPath.Count > 0)
            {
                RefreshStateFromFlags();
            }
            else
            {
                Debug.LogWarning($"[{name}] RecalculatePath: No path found from {transform.position} to {destination}");
            }

            isCalculatingPath = false;
        }

        /// <summary>
        /// Sets the entranced state of this visitor.
        /// </summary>
        public virtual void SetEntranced(bool value)
        {
            if (isEntranced != value)
            {
                isEntranced = value;
            }
        }

        /// <summary>
        /// Sets the visitor to Mesmerized state for a specified duration.
        /// </summary>
        public virtual void SetMesmerized(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = mesmerizedDuration;
            }

            isMesmerized = true;
            SetTimedState(VisitorState.Mesmerized, duration);
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Sets the visitor to Lost state for a specified duration.
        /// In world-space mode, this triggers a path recalculation.
        /// </summary>
        public virtual void SetLost(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = lostDuration;
            }

            isLost = true;
            SetTimedState(VisitorState.Lost, duration);
            RefreshStateFromFlags();

            // In world-space mode, just recalculate path (no grid-based detours)
            RecalculatePath();
        }

        /// <summary>
        /// Sets the visitor to Frightened state for a specified duration.
        /// </summary>
        public virtual void SetFrightened(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = frightenedDuration;
            }

            isFrightened = true;
            SetTimedState(VisitorState.Frightened, duration);
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Sets the visitor to Lured state, drawn toward the Heart.
        /// </summary>
        public virtual void SetLured(bool value)
        {
            if (isLured != value)
            {
                isLured = value;
                RefreshStateFromFlags();

                if (value)
                {
                    RecalculatePath();
                }
            }
        }

        /// <summary>
        /// Checks for nearby Red Caps and triggers frightened state if detected.
        /// </summary>
        protected virtual void CheckForNearbyRedCaps()
        {
            if (isFrightened || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            RedCapController[] redCaps = FindObjectsByType<RedCapController>(FindObjectsSortMode.None);

            foreach (var redCap in redCaps)
            {
                if (redCap == null || redCap.gameObject == null)
                    continue;

                float distance = Vector3.Distance(transform.position, redCap.transform.position);

                if (distance <= redCapDetectionRadius)
                {
                    SetFrightened(frightenedDuration);
                    return;
                }
            }
        }

        /// <summary>
        /// Internal method to set a timed state with duration tracking.
        /// </summary>
        protected virtual void SetTimedState(VisitorState timedState, float duration)
        {
            currentTimedState = timedState;
            currentStateDuration = duration;
            currentStateTimer = duration;
        }

        /// <summary>
        /// Called when a timed state expires.
        /// </summary>
        protected virtual void OnStateExpired(VisitorState expiredState)
        {
            switch (expiredState)
            {
                case VisitorState.Mesmerized:
                    isMesmerized = false;
                    break;
                case VisitorState.Lost:
                    isLost = false;
                    break;
                case VisitorState.Frightened:
                    isFrightened = false;
                    break;
            }

            currentTimedState = VisitorState.Idle;
            currentStateDuration = 0f;
            currentStateTimer = 0f;

            RefreshStateFromFlags();
        }

        /// <summary>
        /// Forces the visitor to escape immediately.
        /// </summary>
        public virtual void ForceEscape()
        {
            state = VisitorState.Escaping;
            SetAnimatorDirection(IdleDirection);

            isFascinated = false;
            hasReachedLantern = false;
            ClearLanternInteraction();

            if (spriteRenderer != null)
            {
                Color escapingColor = visitorColor;
                escapingColor.a = 0.3f;
                spriteRenderer.color = escapingColor;
            }

            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// Makes this visitor fascinated by a FaeLantern at the given world position.
        /// </summary>
        public virtual void BecomeFascinated(Vector3 lanternWorldPosition)
        {
            if (!IsMovementState(state))
            {
                return;
            }

            isFascinated = true;
            fascinationLanternPosition = lanternWorldPosition;
            hasReachedLantern = false;
            RefreshStateFromFlags();

            ResetDetourState();
            waypointsTraversedSinceSpawn = 0;

            worldPath = BuildWorldPath(transform.position, lanternWorldPosition);
            worldPathIndex = 0;
        }

        #endregion

        #region Sprite Setup

        protected virtual void SetupSpriteRenderer()
        {
            // Only create procedural sprite if enabled
            if (useProceduralSprite)
            {
                spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
                    gameObject,
                    createProceduralSprite: true,
                    useSoftEdges: false,
                    resolution: 32,
                    pixelsPerUnit: proceduralPixelsPerUnit
                );
            }
            // Otherwise spriteRenderer should already be found via GetComponentInChildren in Awake

            ApplySpriteSettings();
        }

        protected virtual void ApplySpriteSettings()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = visitorColor;
            spriteRenderer.sortingOrder = sortingOrder;

            if (useProceduralSprite)
            {
                float baseSpriteSize = spriteRenderer.sprite != null
                    ? Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y)
                    : 1f;

                if (baseSpriteSize <= 0f)
                {
                    baseSpriteSize = 1f;
                }

                float targetWorldSize = visitorSize > 0f
                    ? visitorSize
                    : Mathf.Max(authoredSpriteWorldSize.x, authoredSpriteWorldSize.y);

                if (targetWorldSize > 0f)
                {
                    float scale = targetWorldSize / baseSpriteSize;
                    transform.localScale = new Vector3(scale, scale, 1f);
                }
                else
                {
                    transform.localScale = initialScale;
                }
            }
            else
            {
                transform.localScale = initialScale;
            }
        }

        protected void CacheAuthoredSpriteSize()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                authoredSpriteWorldSize = Vector2.zero;
                return;
            }

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            authoredSpriteWorldSize = new Vector2(
                spriteSize.x * transform.localScale.x,
                spriteSize.y * transform.localScale.y
            );
        }

        protected virtual void SetupPhysics()
        {
            // Always use 3D physics

            // Remove any existing 2D physics components
            Rigidbody2D existingRb2D = GetComponent<Rigidbody2D>();
            if (existingRb2D != null)
            {
                existingRb2D.simulated = false;
                DestroyImmediate(existingRb2D);
            }

            Collider2D[] existingColliders2D = GetComponents<Collider2D>();
            foreach (var col2D in existingColliders2D)
            {
                if (col2D != null)
                {
                    col2D.enabled = false;
                    DestroyImmediate(col2D);
                }
            }

            // Setup 3D physics - use existing or add new
            rb3D = GetComponent<Rigidbody>();
            if (rb3D == null)
            {
                rb3D = gameObject.AddComponent<Rigidbody>();
            }

            rb3D.isKinematic = true;
            rb3D.useGravity = false;

            // Use existing 3D collider (SphereCollider or CapsuleCollider) or add CapsuleCollider
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();

            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
            }
            else if (capsuleCollider != null)
            {
                capsuleCollider.isTrigger = true;
            }
            else
            {
                // Add CapsuleCollider if no 3D collider exists
                capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.height = 1.8f;
                capsuleCollider.radius = 0.3f;
                capsuleCollider.center = new Vector3(0, 0.9f, 0);
                capsuleCollider.isTrigger = true;
            }
        }

        protected virtual void Setup3DModel()
        {
            if (modelPrefab == null)
            {
                return;
            }

            // Instantiate the model prefab
            modelInstance = Instantiate(modelPrefab, transform);
            modelInstance.transform.localPosition = Vector3.zero;
            // Keep prefab's original rotation (e.g., X:90 for models designed for XY plane)
            // Capture base rotation so directional Z rotation can be applied on top of it
            modelBaseRotation = modelInstance.transform.localRotation;
            modelBaseRotationCaptured = true;
            modelInstance.transform.localScale = Vector3.one;

            // Look for Animator in the model (should be on root or child)
            if (animator == null)
            {
                animator = modelInstance.GetComponentInChildren<Animator>();
            }

            // Disable any sprite renderers if present
            SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
            foreach (var sprite in sprites)
            {
                sprite.enabled = false;
            }
        }

        #endregion

        #region Detour State

        /// <summary>
        /// Resets detour-specific state when starting a new path or becoming fascinated.
        /// Derived classes should clear confusion flags, misstep tracking, etc.
        /// </summary>
        protected virtual void ResetDetourState()
        {
            // Base implementation - just clear confusion flag
            isConfused = false;
        }

        /// <summary>
        /// Randomly decides whether to recover from confusion.
        /// </summary>
        protected void DecideRecoveryFromConfusion()
        {
            float roll = Random.value;
            bool recover = roll <= 0.5f;
            isConfused = !recover;
        }

        #endregion

        #region Gizmos

        protected virtual void OnDrawGizmos()
        {
            // Draw world-space path
            if (worldPath != null && worldPath.Count > 0)
            {
                Gizmos.color = Color.cyan;

                for (int i = 0; i < worldPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                }

                // Draw current target
                if (IsMovementState(state) && worldPathIndex < worldPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(worldPath[worldPathIndex], 0.3f);
                }
            }

            // Draw destination
            if (originalDestination != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(originalDestination, 0.5f);
            }
        }

        #endregion
    }
}

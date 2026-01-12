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
        private bool hasLoggedFirstRotation;

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

        protected const float MovementEpsilonSqr = 0.0001f;
        protected const float StallLoggingDelaySeconds = 0.35f;
        protected const float StallRouteLogDumpDelaySeconds = 10f;

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
        protected Vector3 originalSpawnPosition; // Where the visitor spawned from (to avoid returning there)
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
                SetWorldDestination(mazeGridBehaviour.HeartWorldPosition);
            }
        }

        /// <summary>
        /// Sets the original spawn position for this visitor.
        /// Used to prevent visitors from retargeting back to where they spawned.
        /// </summary>
        public void SetOriginalSpawnPosition(Vector3 position)
        {
            originalSpawnPosition = position;
        }

        /// <summary>
        /// Retargets visitor to the nearest spawn point by walking distance.
        /// Excludes the original spawn point to prevent visitors from going backwards.
        /// If no valid spawn points are available, falls back to the heart.
        /// </summary>
        public void RetargetToNearestSpawn()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                Debug.Log($"[Pathfinding] {name}: RetargetToNearestSpawn - no maze data, falling back to heart");
                RetargetToHeart();
                return;
            }

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.SpawnPoints;
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                Debug.Log($"[Pathfinding] {name}: RetargetToNearestSpawn - no spawn points available, falling back to heart");
                RetargetToHeart();
                return;
            }

            // Log available spawn points
            var spawnInfo = string.Join(", ", spawnPoints.Select(kvp => $"{kvp.Key}"));
            Debug.Log($"[Pathfinding] {name}: RetargetToNearestSpawn - considering {spawnPoints.Count} spawn points: [{spawnInfo}]");

            Vector3 currentPos = transform.position;
            Vector3 bestSpawn = Vector3.zero;
            float shortestWalkingDist = float.MaxValue;
            int validSpawnsConsidered = 0;

            foreach (var kvp in spawnPoints)
            {
                Vector3 spawnPos = kvp.Value;

                // Skip spawn points too close to original spawn position (prevent going backwards)
                if (originalSpawnPosition != Vector3.zero)
                {
                    float distToOriginal = Vector3.Distance(spawnPos, originalSpawnPosition);
                    if (distToOriginal < 2f)
                    {
                        Debug.Log($"[Pathfinding] {name}: Skipping spawn {kvp.Key} at {spawnPos} - too close to original spawn {originalSpawnPosition}");
                        continue;
                    }
                }

                // Calculate walking distance by building a path
                var testPath = BuildWorldPath(currentPos, spawnPos);
                if (testPath == null || testPath.Count == 0)
                    continue;

                // Calculate total path length
                float pathLength = 0f;
                Vector3 prevPoint = currentPos;
                foreach (var point in testPath)
                {
                    pathLength += Vector3.Distance(prevPoint, point);
                    prevPoint = point;
                }

                validSpawnsConsidered++;
                if (pathLength < shortestWalkingDist)
                {
                    shortestWalkingDist = pathLength;
                    bestSpawn = spawnPos;
                }
            }

            // If no valid spawn found (all too close to origin), fall back to heart
            if (bestSpawn == Vector3.zero || validSpawnsConsidered == 0)
            {
                Debug.Log($"[Pathfinding] {name}: No valid spawn points found (excluding origin), falling back to heart");
                RetargetToHeart();
                return;
            }

            Debug.Log($"[Pathfinding] {name}: Retargeting to nearest spawn at {bestSpawn} (walking dist: {shortestWalkingDist:F1}, considered {validSpawnsConsidered} spawns)");
            SetWorldDestination(bestSpawn);
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
            Debug.Log($"[Pathfinding] {name}: SetWorldDestination called with destination {destination}");

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
        /// Constructs path in 1-unit increments along walkable edge polylines.
        /// Does NOT interpolate through unwalkable terrain - only follows edges.
        /// </summary>
        protected virtual List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                Debug.Log($"[Pathfinding] {name}: No maze data, using direct path to {end}");
                return new List<Vector3> { end };
            }

            var graphState = mazeGridBehaviour.ForestMapState;
            var result = new List<Vector3>();

            Vector2 startPos2D = new Vector2(start.x, start.y);
            Vector2 endPos2D = new Vector2(end.x, end.y);

            // Find nearest point on any edge for both start and end
            Vector2 nearestStartOnEdge;
            ForestMaze.PlanarForestMazeGenerator.Edge startEdge;
            int startEdgeSegmentIndex;
            FindNearestPointOnAnyEdge(graphState, startPos2D, out nearestStartOnEdge, out startEdge, out startEdgeSegmentIndex);

            Vector2 nearestEndOnEdge;
            ForestMaze.PlanarForestMazeGenerator.Edge endEdge;
            int endEdgeSegmentIndex;
            FindNearestPointOnAnyEdge(graphState, endPos2D, out nearestEndOnEdge, out endEdge, out endEdgeSegmentIndex);

            Debug.Log($"[Pathfinding] {name}: Building path from {start} to {end}");

            // Check distance from start to nearest edge point
            float distToNearestEdge = Vector2.Distance(startPos2D, nearestStartOnEdge);

            // Only add start position if we're close to the edge (within 2 units - on walkable terrain)
            // Do NOT interpolate through potentially unwalkable terrain
            if (distToNearestEdge < 2f)
            {
                // Close to edge - safe to start from current position
                result.Add(start);
                Debug.Log($"[Pathfinding] {name}: Start is close to edge (dist={distToNearestEdge:F1}), starting from current position");
            }
            else
            {
                // Far from edge - visitor is off the walkable path
                // Start from the nearest edge point to avoid crossing walls
                Debug.LogWarning($"[Pathfinding] {name}: Start is far from edge (dist={distToNearestEdge:F1}), snapping to nearest edge point {nearestStartOnEdge}");
                result.Add(new Vector3(nearestStartOnEdge.x, nearestStartOnEdge.y, start.z));
            }

            // Find nodes connected to start and end edges
            int startNodeIndex = startEdge != null ? startEdge.NodeA : FindNearestNodeIndex(graphState, startPos2D);
            int endNodeIndex = endEdge != null ? endEdge.NodeA : FindNearestNodeIndex(graphState, endPos2D);

            // If on partial edge, the connected node is NodeA
            if (startEdge != null && startEdge.Partial)
            {
                startNodeIndex = startEdge.NodeA;
            }
            if (endEdge != null && endEdge.Partial)
            {
                endNodeIndex = endEdge.NodeA;
            }

            Debug.Log($"[Pathfinding] {name}: Start node: {startNodeIndex}, End node: {endNodeIndex}");

            if (startNodeIndex < 0 || endNodeIndex < 0)
            {
                Debug.LogWarning($"[Pathfinding] {name}: Invalid node indices, cannot build path");
                return result;
            }

            // If start is on a partial edge, follow it to the node first
            if (startEdge != null && startEdge.Partial && startEdge.PolylinePoints.Count > 0)
            {
                Debug.Log($"[Pathfinding] {name}: Following partial start edge to node {startEdge.NodeA}");
                AddEdgePolylineToPath(result, startEdge, nearestStartOnEdge, true, start.z); // true = toward node
            }

            // BFS to find path through nodes
            var nodePath = FindNodePath(graphState, startNodeIndex, endNodeIndex);

            if (nodePath == null || nodePath.Count == 0)
            {
                Debug.LogWarning($"[Pathfinding] {name}: No node path found from node {startNodeIndex} to {endNodeIndex}");
                // Don't interpolate through potentially unwalkable terrain
                // Just end at the nearest edge point we found
                return result;
            }

            Debug.Log($"[Pathfinding] {name}: Node path: [{string.Join(" -> ", nodePath)}]");

            // Follow edges between consecutive nodes
            for (int i = 0; i < nodePath.Count - 1; i++)
            {
                int nodeA = nodePath[i];
                int nodeB = nodePath[i + 1];

                var connectingEdge = FindConnectingEdge(graphState, nodeA, nodeB, out bool reverse);

                if (connectingEdge != null && connectingEdge.PolylinePoints.Count > 0)
                {
                    AddEdgePolylineToPathBetweenNodes(result, connectingEdge, reverse, start.z);
                }
                else
                {
                    // No connecting edge found - this shouldn't happen in a valid graph
                    Debug.LogWarning($"[Pathfinding] {name}: No connecting edge between nodes {nodeA} and {nodeB}");
                }
            }

            // If end is on a partial edge, follow from node to endpoint
            if (endEdge != null && endEdge.Partial && endEdge.PolylinePoints.Count > 0)
            {
                Debug.Log($"[Pathfinding] {name}: Following partial end edge from node to destination");
                AddEdgePolylineToPath(result, endEdge, nearestEndOnEdge, false, start.z); // false = away from node
            }

            // Only add final destination if it's close to our last path point (within 2 units - on walkable terrain)
            // Do NOT interpolate through potentially unwalkable terrain
            Vector3 lastPoint = result.Count > 0 ? result[result.Count - 1] : start;
            float distToEnd = Vector3.Distance(lastPoint, end);
            if (distToEnd > 0.1f && distToEnd < 2f)
            {
                // Close enough - safe to add as final waypoint
                result.Add(end);
            }
            else if (distToEnd >= 2f)
            {
                Debug.LogWarning($"[Pathfinding] {name}: Final destination {end} is far from last path point {lastPoint} (dist={distToEnd:F1}), not interpolating through potential walls");
            }

            Debug.Log($"[Pathfinding] {name}: Final path has {result.Count} waypoints");
            return result;
        }

        /// <summary>
        /// Finds the nearest point on any edge polyline to the given position.
        /// </summary>
        private void FindNearestPointOnAnyEdge(ForestMaze.PlanarForestMazeGenerator.ForestMapState state,
            Vector2 position, out Vector2 nearestPoint, out ForestMaze.PlanarForestMazeGenerator.Edge nearestEdge,
            out int segmentIndex)
        {
            nearestPoint = position;
            nearestEdge = null;
            segmentIndex = -1;
            float minDist = float.MaxValue;

            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 closest = ClosestPointOnSegment(position, segStart, segEnd);
                    float dist = Vector2.Distance(position, closest);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestPoint = closest;
                        nearestEdge = edge;
                        segmentIndex = i;
                    }
                }
            }

            // Also check node positions (visitor might be at a node)
            foreach (var node in state.Nodes)
            {
                float dist = Vector2.Distance(position, node.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPoint = node.Position;
                    // Keep nearestEdge as null to indicate we're at a node
                }
            }
        }

        /// <summary>
        /// Finds the closest point on a line segment to a given point.
        /// </summary>
        private Vector2 ClosestPointOnSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 seg = segEnd - segStart;
            float segLengthSq = seg.sqrMagnitude;
            if (segLengthSq < 0.0001f)
                return segStart;

            float t = Mathf.Clamp01(Vector2.Dot(point - segStart, seg) / segLengthSq);
            return segStart + t * seg;
        }

        /// <summary>
        /// Adds interpolated points along the ACTUAL edge polyline segment.
        /// Unlike straight-line interpolation, this follows the actual walkable path.
        /// </summary>
        private void AddInterpolatedPathAlongEdge(List<Vector3> path, Vector3 from, Vector3 to, float stepSize, float z,
            ForestMaze.PlanarForestMazeGenerator.ForestMapState graphState)
        {
            // Find the edge that contains both from and to points
            Vector2 from2D = new Vector2(from.x, from.y);
            Vector2 to2D = new Vector2(to.x, to.y);

            foreach (var edge in graphState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                // Check if both points are on this edge
                bool fromOnEdge = false;
                bool toOnEdge = false;
                int fromSegment = -1;
                int toSegment = -1;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    float distFrom = DistanceToSegment(from2D, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);
                    float distTo = DistanceToSegment(to2D, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);

                    if (distFrom < 1.5f && !fromOnEdge)
                    {
                        fromOnEdge = true;
                        fromSegment = i;
                    }
                    if (distTo < 1.5f && !toOnEdge)
                    {
                        toOnEdge = true;
                        toSegment = i;
                    }
                }

                if (fromOnEdge && toOnEdge && fromSegment >= 0 && toSegment >= 0)
                {
                    // Both points on same edge - follow the polyline between them
                    if (fromSegment <= toSegment)
                    {
                        for (int i = fromSegment + 1; i <= toSegment + 1 && i < edge.PolylinePoints.Count; i++)
                        {
                            path.Add(new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z));
                        }
                    }
                    else
                    {
                        for (int i = fromSegment; i >= toSegment && i >= 0; i--)
                        {
                            path.Add(new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z));
                        }
                    }
                    return;
                }
            }

            // Fallback: just add the target point directly (should rarely happen)
            path.Add(to);
        }

        /// <summary>
        /// Distance from point to line segment.
        /// </summary>
        private float DistanceToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 seg = segEnd - segStart;
            float segLengthSq = seg.sqrMagnitude;
            if (segLengthSq < 0.0001f)
                return Vector2.Distance(point, segStart);
            float t = Mathf.Clamp01(Vector2.Dot(point - segStart, seg) / segLengthSq);
            Vector2 projection = segStart + t * seg;
            return Vector2.Distance(point, projection);
        }

        /// <summary>
        /// Finds the edge connecting two nodes.
        /// </summary>
        private ForestMaze.PlanarForestMazeGenerator.Edge FindConnectingEdge(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, int nodeA, int nodeB, out bool reverse)
        {
            reverse = false;
            foreach (var edge in state.Edges)
            {
                if (!edge.Partial && edge.NodeB.HasValue)
                {
                    if (edge.NodeA == nodeA && edge.NodeB.Value == nodeB)
                    {
                        reverse = false;
                        return edge;
                    }
                    else if (edge.NodeA == nodeB && edge.NodeB.Value == nodeA)
                    {
                        reverse = true;
                        return edge;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Adds edge polyline points to the path directly without interpolation.
        /// Uses exact polyline vertices which are guaranteed to be on walkable terrain.
        /// </summary>
        private void AddEdgePolylineToPath(List<Vector3> path, ForestMaze.PlanarForestMazeGenerator.Edge edge,
            Vector2 startPoint, bool towardNode, float z)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return;

            // Find which segment contains the start point
            int startSegment = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
            {
                Vector2 closest = ClosestPointOnSegment(startPoint, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);
                float dist = Vector2.Distance(startPoint, closest);
                if (dist < minDist)
                {
                    minDist = dist;
                    startSegment = i;
                }
            }

            if (towardNode)
            {
                // Go from startPoint toward polyline[0] (the node)
                // Add each polyline vertex directly - these are on the walkable path
                for (int i = startSegment; i >= 0; i--)
                {
                    Vector3 pt = new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z);
                    path.Add(pt);
                }
            }
            else
            {
                // Go from startPoint toward polyline[Count-1] (the endpoint)
                for (int i = startSegment + 1; i < edge.PolylinePoints.Count; i++)
                {
                    Vector3 pt = new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z);
                    path.Add(pt);
                }
            }
        }

        /// <summary>
        /// Adds edge polyline points between two nodes directly without interpolation.
        /// Uses exact polyline vertices which are guaranteed to be on walkable terrain.
        /// </summary>
        private void AddEdgePolylineToPathBetweenNodes(List<Vector3> path, ForestMaze.PlanarForestMazeGenerator.Edge edge,
            bool reverse, float z)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return;

            if (reverse)
            {
                // Go from last point to first (skip last since that's where we came from)
                for (int i = edge.PolylinePoints.Count - 2; i >= 0; i--)
                {
                    Vector3 pt = new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z);
                    path.Add(pt);
                }
            }
            else
            {
                // Go from first to last (skip first since that's where we came from)
                for (int i = 1; i < edge.PolylinePoints.Count; i++)
                {
                    Vector3 pt = new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z);
                    path.Add(pt);
                }
            }
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

        /// <summary>
        /// Finds a complete edge whose endpoint (either first or last polyline point) is near the given position.
        /// Used to handle visitors at positions that were previously partial edge endpoints but became complete after growth.
        /// </summary>
        protected ForestMaze.PlanarForestMazeGenerator.Edge FindCompleteEdgeAtEndpoint(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 graphPosition)
        {
            const float EndpointTolerance = 2.0f; // Graph units tolerance for matching

            foreach (var edge in state.Edges)
            {
                // Only check complete edges (those with both NodeA and NodeB)
                if (edge.Partial || !edge.NodeB.HasValue || edge.PolylinePoints.Count == 0)
                    continue;

                // Check if position matches either endpoint of this complete edge
                Vector2 firstPoint = edge.PolylinePoints[0];
                Vector2 lastPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];

                float distToFirst = Vector2.Distance(firstPoint, graphPosition);
                float distToLast = Vector2.Distance(lastPoint, graphPosition);

                if (distToFirst < EndpointTolerance || distToLast < EndpointTolerance)
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
            // Apply smooth rotation for model to face movement direction
            // All rotation is around world Z axis only (XY plane movement)
            // IMPORTANT: Rotate the game object (parent), not the model child.
            // The model has its own local rotation that orients it correctly;
            // rotating the model's local Z would rotate around the model's tilted Z axis, not world Z.
            if (movement.sqrMagnitude > MovementEpsilonSqr)
            {
                // Calculate Z rotation angle for facing direction in world space
                float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;

                // Log rotation details on first movement and periodically
                bool shouldLog = !hasLoggedFirstRotation || Time.frameCount % 300 == 0;
                float zBefore = transform.eulerAngles.z;

                // Apply Z rotation to the game object (this visitor's transform)
                // This rotates around world Z, making the model face the direction of travel
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10f
                );

                if (shouldLog)
                {
                    hasLoggedFirstRotation = true;
                }
            }
        }

        /// <summary>
        /// Allows external behaviours (e.g., wisp-following) to update the animator's facing direction.
        /// </summary>
        /// <param name="movement">The movement or desired facing vector.</param>
        public void ApplyExternalAnimatorDirection(Vector2 movement)
        {
            UpdateAnimatorDirection(movement);
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

            // Use path direction (toward target waypoint) for facing, not frame movement delta
            // This ensures correct orientation even when movement is small
            Vector3 pathDirection = (targetWorldPos - transform.position).normalized;
            Vector2 facingDirection = new Vector2(pathDirection.x, pathDirection.y);
            UpdateAnimatorDirection(facingDirection);

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
        /// Returns the originally set destination, not the heart.
        /// </summary>
        protected virtual Vector3 GetDestinationForCurrentState()
        {
            // Return the world destination that was set via SetWorldDestination
            // Only fall back to heart if no destination was ever set
            if (worldDestination != Vector3.zero)
            {
                return worldDestination;
            }
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

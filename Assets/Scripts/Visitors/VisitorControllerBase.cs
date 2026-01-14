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
            Dazed,         // Stunned from witnessing maze growth
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
        protected const float FascinationSpeedMultiplier = 0.5f; // 50% speed when approaching lantern

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
        protected bool isDazed;

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

            // Handle fascination timer (pause at lantern)
            if (isFascinated && hasReachedLantern)
            {
                if (fascinationTimer > 0)
                {
                    fascinationTimer -= Time.deltaTime;
                    return; // Don't move while fascinated timer is active
                }
                else
                {
                    // Fascination timer ended - resume wandering
                    EndFascination();
                }
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
        /// Retargets visitor to the nearest spawn point by walking distance from current position.
        /// Excludes the original spawn point to prevent visitors from going backwards.
        /// If no valid spawn points are available, falls back to the heart.
        /// </summary>
        public void RetargetToNearestSpawn()
        {
            RetargetToNearestSpawnFrom(transform.position);
        }

        /// <summary>
        /// Retargets visitor to the nearest spawn point by walking distance from a specified position.
        /// This is used when a destination portal is consumed - we want the nearest spawn from
        /// where the visitor was heading, not from where they currently are.
        /// Excludes the original spawn point to prevent visitors from going backwards.
        /// If no valid spawn points are available, falls back to the heart.
        /// </summary>
        /// <param name="fromPosition">The position to measure walking distance from</param>
        public void RetargetToNearestSpawnFrom(Vector3 fromPosition)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                RetargetToHeart();
                return;
            }

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            if (spawnPoints.Count == 0)
            {
                RetargetToHeart();
                return;
            }

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
                        continue;
                    }
                }

                // Calculate walking distance by building a path from the specified position
                var testPath = BuildWorldPath(fromPosition, spawnPos);
                if (testPath == null || testPath.Count == 0)
                    continue;

                // Calculate total path length
                float pathLength = 0f;
                Vector3 prevPoint = fromPosition;
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
                RetargetToHeart();
                return;
            }

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
            // Dazed has highest priority - visitor is stunned and cannot act
            if (isDazed)
            {
                state = VisitorState.Dazed;
            }
            else if (isMesmerized)
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
            }
            else
            {
                state = VisitorState.Idle;
            }
        }

        /// <summary>
        /// Builds a world-space path from start to end using BFS through walkable tiles.
        /// Uses actual tile positions from WorldSpaceMazeData to guarantee paths stay on walkable terrain.
        /// </summary>
        protected virtual List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return new List<Vector3> { end };
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            var result = new List<Vector3>();

            Vector2 startPos2D = new Vector2(start.x, start.y);
            Vector2 endPos2D = new Vector2(end.x, end.y);

            // Find nearest walkable tile to start position
            var startTile = FindNearestWalkableTile(mazeData, startPos2D);
            if (startTile == null)
            {
                return new List<Vector3> { end };
            }

            // Find nearest walkable tile to end position
            var endTile = FindNearestWalkableTile(mazeData, endPos2D);
            if (endTile == null)
            {
                return new List<Vector3> { end };
            }

            // BFS through walkable tiles
            var tilePath = FindTilePath(mazeData, startTile, endTile);

            if (tilePath == null || tilePath.Count == 0)
            {
                // Return empty list to indicate failure - caller should handle fallback
                return new List<Vector3>();
            }

            // Convert tile path to world positions
            // Add current position first if close to start tile
            float distToStartTile = Vector2.Distance(startPos2D, startTile.Position);
            if (distToStartTile < 1.5f)
            {
                result.Add(start);
            }

            // Add all tile positions
            foreach (var tile in tilePath)
            {
                result.Add(new Vector3(tile.Position.x, tile.Position.y, start.z));
            }

            // Add final destination if close to end tile
            float distFromLastTile = Vector2.Distance(endPos2D, endTile.Position);
            if (distFromLastTile > 0.1f && distFromLastTile < 1.5f)
            {
                result.Add(end);
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest walkable tile to a position.
        /// </summary>
        private ForestMaze.WorldSpaceTile FindNearestWalkableTile(ForestMaze.WorldSpaceMazeData mazeData, Vector2 position)
        {
            float searchRadius = 5f;
            float minDist = float.MaxValue;
            ForestMaze.WorldSpaceTile nearest = null;

            // Expand search radius if needed
            for (int attempt = 0; attempt < 3 && nearest == null; attempt++)
            {
                var nearbyTiles = mazeData.GetTilesNear(position, searchRadius);
                foreach (var tile in nearbyTiles)
                {
                    if (!tile.Walkable) continue;

                    float dist = Vector2.Distance(position, tile.Position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = tile;
                    }
                }
                searchRadius *= 2f;
            }

            return nearest;
        }

        /// <summary>
        /// Finds a path through walkable tiles using A* algorithm.
        /// Uses Euclidean distance as heuristic to prefer geometrically shorter paths.
        /// This ensures visitors cut through nodes rather than going around them.
        /// </summary>
        private List<ForestMaze.WorldSpaceTile> FindTilePath(ForestMaze.WorldSpaceMazeData mazeData,
            ForestMaze.WorldSpaceTile startTile, ForestMaze.WorldSpaceTile endTile)
        {
            try
            {
                if (startTile == endTile)
                {
                    return new List<ForestMaze.WorldSpaceTile> { startTile };
                }

                // A* algorithm using distance-based costs
                // gScore = actual distance traveled from start
                // hScore = Euclidean distance to goal (heuristic)
                // fScore = gScore + hScore (priority)

                var gScore = new Dictionary<ForestMaze.WorldSpaceTile, float>();
                var fScore = new Dictionary<ForestMaze.WorldSpaceTile, float>();
                var parent = new Dictionary<ForestMaze.WorldSpaceTile, ForestMaze.WorldSpaceTile>();
                var closedSet = new HashSet<ForestMaze.WorldSpaceTile>();

                // Open set as sorted list (simple priority queue)
                var openSet = new List<ForestMaze.WorldSpaceTile>();

                gScore[startTile] = 0f;
                fScore[startTile] = Vector2.Distance(startTile.Position, endTile.Position);
                openSet.Add(startTile);
                parent[startTile] = null;

                // Tiles are placed at ~0.5 unit intervals along curves and 1.0 in nodes
                // Use 1.42 (sqrt(2)) to connect diagonal neighbors but not jump across paths
                float neighborRadius = mazeData.TileSize * 1.42f;

                int iterations = 0;
                int maxIterations = 50000; // Safety limit

                while (openSet.Count > 0 && iterations < maxIterations)
                {
                    iterations++;

                    // Find node with lowest fScore in open set
                    ForestMaze.WorldSpaceTile current = null;
                    float lowestF = float.MaxValue;
                    foreach (var tile in openSet)
                    {
                        float f = fScore.TryGetValue(tile, out float fs) ? fs : float.MaxValue;
                        if (f < lowestF)
                        {
                            lowestF = f;
                            current = tile;
                        }
                    }

                    if (current == null) break;

                    // Check if reached destination
                    float distToEnd = Vector2.Distance(current.Position, endTile.Position);
                    if (distToEnd < mazeData.TileSize * 0.5f || current == endTile)
                    {
                        // Reconstruct path
                        var path = new List<ForestMaze.WorldSpaceTile>();
                        var node = current;
                        while (node != null)
                        {
                            path.Add(node);
                            parent.TryGetValue(node, out node);
                        }
                        path.Reverse();

                        return path;
                    }

                    openSet.Remove(current);
                    closedSet.Add(current);

                    // Get neighboring walkable tiles
                    var neighbors = mazeData.GetTilesNear(current.Position, neighborRadius);

                    float currentG = gScore.TryGetValue(current, out float cg) ? cg : float.MaxValue;

                    foreach (var neighbor in neighbors)
                    {
                        if (!neighbor.Walkable) continue;
                        if (closedSet.Contains(neighbor)) continue;

                        // Must be within neighbor radius (world-space distance)
                        float stepDist = Vector2.Distance(current.Position, neighbor.Position);
                        if (stepDist > neighborRadius) continue;

                        // Topology check: verify tiles are connected through graph structure
                        // This prevents jumping between parallel paths that happen to be close
                        if (!mazeData.AreTilesConnected(current, neighbor))
                            continue;

                        // Calculate tentative gScore (actual distance traveled)
                        float tentativeG = currentG + stepDist;

                        float neighborG = gScore.TryGetValue(neighbor, out float ng) ? ng : float.MaxValue;
                        if (tentativeG < neighborG)
                        {
                            // This path is better
                            parent[neighbor] = current;
                            gScore[neighbor] = tentativeG;
                            fScore[neighbor] = tentativeG + Vector2.Distance(neighbor.Position, endTile.Position);

                            if (!openSet.Contains(neighbor))
                            {
                                openSet.Add(neighbor);
                            }
                        }
                    }
                }

                return null; // No path found
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if there's a clear line-of-sight between two positions through walkable tiles.
        /// Samples points along the line and verifies each is near a walkable tile.
        /// </summary>
        private bool HasLineOfSight(ForestMaze.WorldSpaceMazeData mazeData, Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            if (distance < 0.1f) return true; // Same position

            // Sample at intervals smaller than tile size to catch walls
            float sampleInterval = mazeData.TileSize * 0.4f;
            int numSamples = Mathf.CeilToInt(distance / sampleInterval);
            numSamples = Mathf.Max(numSamples, 2); // At least check midpoint

            float checkRadius = mazeData.TileSize * 0.6f; // Slightly smaller than tile to be strict

            for (int i = 1; i < numSamples; i++) // Skip start point (i=0), we know it's walkable
            {
                float t = (float)i / numSamples;
                Vector2 samplePos = Vector2.Lerp(from, to, t);

                // Check if this sample point is near a walkable tile
                var nearbyTiles = mazeData.GetTilesNear(samplePos, checkRadius);
                bool foundWalkable = false;
                foreach (var tile in nearbyTiles)
                {
                    if (tile.Walkable && Vector2.Distance(samplePos, tile.Position) <= checkRadius)
                    {
                        foundWalkable = true;
                        break;
                    }
                }

                if (!foundWalkable)
                {
                    return false; // Path crosses unwalkable area
                }
            }

            return true;
        }

        /// <summary>
        /// Simplifies a tile path by removing intermediate collinear points.
        /// Keeps direction changes and samples at regular intervals for smooth movement.
        /// </summary>
        private List<ForestMaze.WorldSpaceTile> SimplifyTilePath(List<ForestMaze.WorldSpaceTile> path)
        {
            if (path == null || path.Count <= 2)
                return path;

            var simplified = new List<ForestMaze.WorldSpaceTile>();
            simplified.Add(path[0]);

            const float angleThreshold = 15f * Mathf.Deg2Rad; // Keep points where direction changes by more than 15 degrees
            const int maxSkip = 5; // Don't skip more than 5 consecutive tiles

            int skipCount = 0;
            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector2 dirPrev = (path[i].Position - path[i - 1].Position).normalized;
                Vector2 dirNext = (path[i + 1].Position - path[i].Position).normalized;

                float angle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dirPrev, dirNext), -1f, 1f));

                // Keep this point if direction changes significantly or we've skipped too many
                if (angle > angleThreshold || skipCount >= maxSkip)
                {
                    simplified.Add(path[i]);
                    skipCount = 0;
                }
                else
                {
                    skipCount++;
                }
            }

            simplified.Add(path[path.Count - 1]);
            return simplified;
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
        /// Also adds interpolated points along each segment for smooth movement.
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

            float stepSize = 1f; // Interpolation step size in world units

            if (towardNode)
            {
                // Go from startPoint toward polyline[0] (the node)
                // Iterate through segments in reverse, adding interpolated points
                Vector3 lastPoint = path.Count > 0 ? path[path.Count - 1] : new Vector3(startPoint.x, startPoint.y, z);

                for (int seg = startSegment; seg >= 0; seg--)
                {
                    Vector2 segStart = edge.PolylinePoints[seg + 1]; // Going in reverse
                    Vector2 segEnd = edge.PolylinePoints[seg];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
            else
            {
                // Go from startPoint toward polyline[Count-1] (the endpoint)
                for (int seg = startSegment; seg < edge.PolylinePoints.Count - 1; seg++)
                {
                    Vector2 segStart = edge.PolylinePoints[seg];
                    Vector2 segEnd = edge.PolylinePoints[seg + 1];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Adds edge polyline points between two nodes with interpolation.
        /// Uses interpolated points along polyline segments for smooth movement.
        /// </summary>
        private void AddEdgePolylineToPathBetweenNodes(List<Vector3> path, ForestMaze.PlanarForestMazeGenerator.Edge edge,
            bool reverse, float z)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return;

            float stepSize = 1f; // Interpolation step size in world units

            if (reverse)
            {
                // Go from last point to first (skip last since that's where we came from)
                for (int seg = edge.PolylinePoints.Count - 2; seg >= 0; seg--)
                {
                    Vector2 segStart = edge.PolylinePoints[seg + 1];
                    Vector2 segEnd = edge.PolylinePoints[seg];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
            else
            {
                // Go from first to last (skip first since that's where we came from)
                for (int seg = 0; seg < edge.PolylinePoints.Count - 1; seg++)
                {
                    Vector2 segStart = edge.PolylinePoints[seg];
                    Vector2 segEnd = edge.PolylinePoints[seg + 1];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    // Skip first point on first segment since we're already there
                    int startJ = (seg == 0) ? 1 : 0;
                    for (int j = startJ; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
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
        /// Checks if the visitor is currently at or near a node (intersection).
        /// Nodes are where paths meet and visitors can make wrong turn decisions.
        /// </summary>
        /// <param name="nodeProximityThreshold">Distance threshold to consider "at" a node (default 2.5 units)</param>
        /// <returns>True if visitor is near a node</returns>
        protected bool IsAtNode(float nodeProximityThreshold = 2.5f)
        {
            if (mazeGridBehaviour == null)
                return false;

            var graphState = mazeGridBehaviour.ForestMapState;
            if (graphState == null || graphState.Nodes.Count == 0)
                return false;

            Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
            float closestDist = float.MaxValue;
            int closestNodeId = -1;

            for (int i = 0; i < graphState.Nodes.Count; i++)
            {
                var node = graphState.Nodes[i];
                float dist = Vector2.Distance(node.Position, currentPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestNodeId = i;
                }

                if (dist <= nodeProximityThreshold)
                {
                    if (logVisitorPathfinding)
                        Debug.Log($"[Confusion:IsAtNode] YES - At node {i} (dist: {dist:F2} <= {nodeProximityThreshold})");
                    return true;
                }
            }

            // Only log "not at node" occasionally to avoid spam
            if (logVisitorPathfinding && Time.frameCount % 60 == 0)
                Debug.Log($"[Confusion:IsAtNode] NO - Nearest node {closestNodeId} at dist {closestDist:F2} (threshold: {nodeProximityThreshold})");

            return false;
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
        /// Uses continuous movement that doesn't pause at waypoints.
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

            // Log fascinated movement periodically
            if (isFascinated && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[Fascination] Walking - pathIndex: {worldPathIndex}/{worldPath.Count}, pos: {transform.position}, target: {worldPath[worldPathIndex]}, dest: {worldDestination}");
            }

            // Use 50% speed if fascinated and walking to lantern
            float effectiveSpeed = moveSpeed * speedMultiplier;
            if (isFascinated && !hasReachedLantern)
            {
                effectiveSpeed *= FascinationSpeedMultiplier;
            }
            float remainingDistance = effectiveSpeed * Time.deltaTime;

            // Move continuously, consuming distance across multiple waypoints if needed
            while (remainingDistance > 0f && worldPathIndex < worldPath.Count)
            {
                Vector3 targetWorldPos = worldPath[worldPathIndex];
                float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);

                if (distanceToTarget <= remainingDistance)
                {
                    // We can reach (or pass) this waypoint - move to it and continue
                    remainingDistance -= distanceToTarget;
                    transform.position = targetWorldPos;
                    waypointsTraversedSinceSpawn++;

                    // Allow derived classes to handle detour logic at waypoints
                    // This may change the path, so we need to check bounds after
                    HandleDetourAtWaypoint();

                    // If path was changed or we've reached the end, stop consuming distance
                    if (worldPath == null || worldPathIndex >= worldPath.Count)
                    {
                        break;
                    }
                }
                else
                {
                    // Can't reach the next waypoint yet - move toward it
                    Vector3 direction = (targetWorldPos - transform.position).normalized;
                    transform.position += direction * remainingDistance;
                    remainingDistance = 0f;
                }
            }

            // Update facing direction based on current target waypoint (or last movement direction)
            if (worldPath != null && worldPathIndex < worldPath.Count)
            {
                Vector3 targetWorldPos = worldPath[worldPathIndex];
                Vector3 pathDirection = (targetWorldPos - transform.position).normalized;
                if (pathDirection.sqrMagnitude > 0.001f)
                {
                    Vector2 facingDirection = new Vector2(pathDirection.x, pathDirection.y);
                    UpdateAnimatorDirection(facingDirection);
                }
            }

            // Sync physics if using rigidbody
            if (rb3D != null)
            {
                rb3D.MovePosition(transform.position);
                Physics.SyncTransforms();
            }

            // Check if we've completed the path
            if (worldPathIndex >= worldPath.Count)
            {
                OnWorldSpacePathComplete();
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

            Debug.Log($"[Fascination] OnWorldSpacePathComplete - isFascinated: {isFascinated}, hasReachedLantern: {hasReachedLantern}, position: {transform.position}, destination: {worldDestination}");

            // Check if fascinated - visitor has reached the lantern stop position
            if (isFascinated && !hasReachedLantern)
            {
                hasReachedLantern = true;
                // Get fascination duration from the current lantern if available
                float duration = 2f; // Default 2 seconds
                if (currentFaeLantern != null)
                {
                    duration = currentFaeLantern.FascinationDuration;
                }
                fascinationTimer = duration;
                state = VisitorState.Idle; // Idle at the lantern
                Vector2 posXY = new Vector2(transform.position.x, transform.position.y);
                Vector2 lanternXY = new Vector2(fascinationLanternPosition.x, fascinationLanternPosition.y);
                Debug.Log($"[Fascination] Visitor reached lantern stop position. Final position: {transform.position}, XY Distance to lantern: {Vector2.Distance(posXY, lanternXY):F3}, Timer: {fascinationTimer}s");
                return;
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

                // Check if at a portal (destination spawn point) - visitor exits the maze
                if (IsAtPortal())
                {
                    state = VisitorState.Escaping;
                    OnExitedThroughPortal();
                    return;
                }
            }

            // Otherwise, just become idle
            state = VisitorState.Idle;
        }

        /// <summary>
        /// Checks if the visitor is at a portal (spawn point) position.
        /// </summary>
        protected virtual bool IsAtPortal()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return false;

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            Vector3 currentPos = transform.position;

            foreach (var kvp in spawnPoints)
            {
                float distToSpawn = Vector3.Distance(currentPos, kvp.Value);
                if (distToSpawn < 3f) // Within 3 units of a portal
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Called when a visitor successfully exits the maze through a portal.
        /// The visitor leaves the game without awarding essence to the player.
        /// </summary>
        protected virtual void OnExitedThroughPortal()
        {
            // Visitor escaped - no essence reward for the player
            // Visual feedback could be added here (particle effect, sound, etc.)

            // Destroy the visitor
            Destroy(gameObject, 0.1f);
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
        /// Gets the current destination position for this visitor.
        /// Used by external systems to check if destination is still valid.
        /// </summary>
        public Vector3 GetCurrentDestination()
        {
            return worldDestination;
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
        /// Called when the visitor witnesses maze growth.
        /// Sets the visitor to Dazed state for a duration, stopping movement.
        /// </summary>
        /// <param name="duration">How long the visitor remains dazed (default 15 seconds)</param>
        public virtual void OnWitnessMazeGrowth(float duration = 15f)
        {
            // Don't daze if already in a terminal state
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            isDazed = true;
            SetTimedState(VisitorState.Dazed, duration);
            RefreshStateFromFlags();
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
                case VisitorState.Dazed:
                    isDazed = false;
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
        /// The visitor will slowly walk (taking 2 seconds) to stop 1.5 units away from the lantern and idle there.
        /// </summary>
        public virtual void BecomeFascinated(Vector3 lanternWorldPosition)
        {
            if (!IsMovementState(state))
            {
                Debug.Log($"[Fascination] BecomeFascinated called but not in movement state: {state}");
                return;
            }

            Debug.Log($"[Fascination] BecomeFascinated START - Visitor at {transform.position}, Lantern at {lanternWorldPosition}");

            isFascinated = true;
            fascinationLanternPosition = lanternWorldPosition;
            hasReachedLantern = false;
            RefreshStateFromFlags();

            ResetDetourState();
            waypointsTraversedSinceSpawn = 0;

            // Calculate stop position 1.5 units away from the lantern (XY plane only, ignore Z)
            Vector2 lanternXY = new Vector2(lanternWorldPosition.x, lanternWorldPosition.y);
            Vector2 visitorXY = new Vector2(transform.position.x, transform.position.y);
            Vector2 directionToLanternXY = (lanternXY - visitorXY).normalized;
            Vector2 stopPositionXY = lanternXY - directionToLanternXY * 1.5f;
            Vector3 stopPosition = new Vector3(stopPositionXY.x, stopPositionXY.y, transform.position.z);

            Debug.Log($"[Fascination] Direction to lantern (XY): {directionToLanternXY}, Stop position: {stopPosition}");

            // Build path to the stop position
            worldPath = BuildWorldPath(transform.position, stopPosition);
            worldPathIndex = 0;

            // CRITICAL: The lantern should NEVER be a waypoint. The path should terminate
            // at the stop location (1.5 units from lantern in XY plane). Remove ALL waypoints that are
            // within the stop distance of the lantern, then add the stop position as the final waypoint.
            const float stopDistance = 1.5f;
            if (worldPath != null && worldPath.Count > 0)
            {
                // Find the first waypoint that gets too close to the lantern (XY distance only)
                int firstBadIndex = -1;
                for (int i = 0; i < worldPath.Count; i++)
                {
                    Vector2 waypointXY = new Vector2(worldPath[i].x, worldPath[i].y);
                    float distToLanternXY = Vector2.Distance(waypointXY, lanternXY);
                    if (distToLanternXY <= stopDistance)
                    {
                        firstBadIndex = i;
                        break;
                    }
                }

                if (firstBadIndex >= 0)
                {
                    Vector2 badWaypointXY = new Vector2(worldPath[firstBadIndex].x, worldPath[firstBadIndex].y);
                    Debug.Log($"[Fascination] Trimming path at index {firstBadIndex} (waypoint at XY dist {Vector2.Distance(badWaypointXY, lanternXY):F2} from lantern)");
                    // Remove all waypoints from firstBadIndex onward (these are too close to lantern)
                    worldPath.RemoveRange(firstBadIndex, worldPath.Count - firstBadIndex);
                }

                // Ensure the stop position is the final waypoint
                if (worldPath.Count == 0 || Vector3.Distance(worldPath[worldPath.Count - 1], stopPosition) > 0.01f)
                {
                    worldPath.Add(stopPosition);
                }
                Debug.Log($"[Fascination] Final path has {worldPath.Count} waypoints, ending at stop position");
            }

            // Set destination to the stop position
            worldDestination = stopPosition;

            // Log the path that was built
            Debug.Log($"[Fascination] Path built with {worldPath?.Count ?? 0} waypoints");
            if (worldPath != null && worldPath.Count > 0)
            {
                Debug.Log($"[Fascination] Path: {FormatWorldPath(worldPath)}");
                Debug.Log($"[Fascination] First waypoint: {worldPath[0]}, Last waypoint: {worldPath[worldPath.Count - 1]}");
                Vector2 lastWaypointXY = new Vector2(worldPath[worldPath.Count - 1].x, worldPath[worldPath.Count - 1].y);
                Debug.Log($"[Fascination] XY Distance from last waypoint to stopPosition: {Vector2.Distance(lastWaypointXY, stopPositionXY):F3}");
                Debug.Log($"[Fascination] XY Distance from last waypoint to lantern: {Vector2.Distance(lastWaypointXY, lanternXY):F3}");
            }
        }

        /// <summary>
        /// Ends the fascination state and resumes toward the original destination.
        /// Called when the fascination timer expires.
        /// </summary>
        protected virtual void EndFascination()
        {
            Debug.Log($"[Fascination] EndFascination - resuming from {transform.position} toward original destination {originalDestination}");

            // Set cooldown BEFORE clearing the lantern reference, to prevent immediate re-fascination
            if (currentFaeLantern != null)
            {
                float cooldown = config != null ? config.FascinationCooldown : currentFaeLantern.CooldownSec;
                lanternCooldowns[currentFaeLantern] = cooldown;
                Debug.Log($"[Fascination] Set cooldown for lantern: {cooldown}s");
            }

            isFascinated = false;
            hasReachedLantern = false;
            fascinationTimer = 0f;
            ClearLanternInteraction();

            // Resume from stop point toward the original destination
            if (mazeGridBehaviour != null && mazeGridBehaviour.WorldSpaceMazeData != null && originalDestination != Vector3.zero)
            {
                worldPath = BuildWorldPath(transform.position, originalDestination);
                worldPathIndex = 0;
                worldDestination = originalDestination;
                state = VisitorState.Walking;
                Debug.Log($"[Fascination] Resumed path has {worldPath?.Count ?? 0} waypoints to {originalDestination}");
            }
            else
            {
                // Fallback: pick a random destination if no original destination
                Vector3 randomDestination = GetRandomWanderDestination();
                worldPath = BuildWorldPath(transform.position, randomDestination);
                worldPathIndex = 0;
                state = VisitorState.Walking;
                Debug.Log($"[Fascination] No original destination, wandering to {randomDestination}");
            }
        }

        /// <summary>
        /// Gets a random walkable node position for wandering after fascination ends.
        /// </summary>
        protected virtual Vector3 GetRandomWanderDestination()
        {
            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;

            // Collect all node tiles (junctions/clearings) as potential destinations
            var nodeTiles = new List<ForestMaze.WorldSpaceTile>();
            foreach (var tile in mazeData.Tiles)
            {
                if (tile.Category == ForestMaze.WorldSpaceTile.TileCategory.Node && tile.Walkable)
                {
                    nodeTiles.Add(tile);
                }
            }

            if (nodeTiles.Count > 0)
            {
                // Pick a random node
                int randomIndex = Random.Range(0, nodeTiles.Count);
                Vector2 nodePos = nodeTiles[randomIndex].Position;
                return new Vector3(nodePos.x, nodePos.y, transform.position.z);
            }

            // Fallback: return current position if no nodes found
            return transform.position;
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

        /// <summary>
        /// Builds a confusion detour path that visits at least 2 random nodes before
        /// returning to the correct path toward the destination.
        /// </summary>
        /// <param name="minDetourNodes">Minimum number of random nodes to visit (default 2)</param>
        /// <returns>True if a detour path was successfully built</returns>
        protected bool BuildConfusionDetourPath(int minDetourNodes = 2)
        {
            if (mazeGridBehaviour == null)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning("[Confusion:Detour] No mazeGridBehaviour");
                return false;
            }

            var graphState = mazeGridBehaviour.ForestMapState;
            if (graphState == null || graphState.Nodes.Count < 3)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning($"[Confusion:Detour] Invalid graph state - Nodes: {graphState?.Nodes.Count ?? 0}");
                return false;
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            if (mazeData == null)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning("[Confusion:Detour] No WorldSpaceMazeData");
                return false;
            }

            // Find the nearest node to our current position
            Vector2 currentPos2D = new Vector2(transform.position.x, transform.position.y);
            int nearestNodeId = FindNearestNodeIndex(graphState, currentPos2D);
            if (nearestNodeId < 0)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning("[Confusion:Detour] Could not find nearest node");
                return false;
            }

            if (logVisitorPathfinding)
                Debug.Log($"[Confusion:Detour] Starting from node {nearestNodeId} at {graphState.Nodes[nearestNodeId].Position}");

            // Build a list of detour nodes by randomly walking the graph
            var detourNodeIds = new List<int>();
            var visitedNodeIds = new HashSet<int>();
            int currentNodeId = nearestNodeId;
            visitedNodeIds.Add(currentNodeId);

            // Walk randomly through the graph for at least minDetourNodes steps
            for (int i = 0; i < minDetourNodes + Random.Range(0, 2); i++)
            {
                var currentNode = graphState.Nodes[currentNodeId];

                // Get connected nodes via incident edges
                var connectedNodeIds = new List<int>();
                foreach (int edgeId in currentNode.IncidentEdges)
                {
                    if (edgeId < 0 || edgeId >= graphState.Edges.Count)
                        continue;

                    var edge = graphState.Edges[edgeId];

                    // Get the other node of this edge
                    int otherNodeId = -1;
                    if (edge.NodeA == currentNodeId && edge.NodeB.HasValue)
                        otherNodeId = edge.NodeB.Value;
                    else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNodeId)
                        otherNodeId = edge.NodeA;

                    if (otherNodeId >= 0 && !visitedNodeIds.Contains(otherNodeId))
                    {
                        connectedNodeIds.Add(otherNodeId);
                    }
                }

                if (connectedNodeIds.Count == 0)
                {
                    // No unvisited neighbors - allow revisiting
                    foreach (int edgeId in currentNode.IncidentEdges)
                    {
                        if (edgeId < 0 || edgeId >= graphState.Edges.Count)
                            continue;

                        var edge = graphState.Edges[edgeId];
                        int otherNodeId = -1;
                        if (edge.NodeA == currentNodeId && edge.NodeB.HasValue)
                            otherNodeId = edge.NodeB.Value;
                        else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNodeId)
                            otherNodeId = edge.NodeA;

                        if (otherNodeId >= 0)
                            connectedNodeIds.Add(otherNodeId);
                    }
                }

                if (connectedNodeIds.Count == 0)
                    break;

                // Pick a random connected node
                int randomIndex = Random.Range(0, connectedNodeIds.Count);
                int nextNodeId = connectedNodeIds[randomIndex];

                detourNodeIds.Add(nextNodeId);
                visitedNodeIds.Add(nextNodeId);
                currentNodeId = nextNodeId;
            }

            if (detourNodeIds.Count < minDetourNodes)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning($"[Confusion:Detour] Not enough detour nodes found: {detourNodeIds.Count} < {minDetourNodes}");
                return false;
            }

            if (logVisitorPathfinding)
            {
                string nodeList = string.Join(" -> ", detourNodeIds);
                Debug.Log($"[Confusion:Detour] Detour nodes selected: {nodeList}");
            }

            // Build path: current position -> each detour node -> final destination
            var fullPath = new List<Vector3>();
            Vector3 pathStart = transform.position;

            // Path to each detour node
            foreach (int nodeId in detourNodeIds)
            {
                var node = graphState.Nodes[nodeId];
                Vector3 nodeWorldPos = new Vector3(node.Position.x, node.Position.y, 0);

                var segmentPath = BuildWorldPath(pathStart, nodeWorldPos);
                if (segmentPath != null && segmentPath.Count > 0)
                {
                    // Skip first point if it's too close to last point in fullPath (avoid duplicates)
                    int startIdx = (fullPath.Count > 0 && segmentPath.Count > 0 &&
                                   Vector3.Distance(fullPath[fullPath.Count - 1], segmentPath[0]) < 0.5f) ? 1 : 0;

                    for (int i = startIdx; i < segmentPath.Count; i++)
                    {
                        fullPath.Add(segmentPath[i]);
                    }
                }
                pathStart = nodeWorldPos;
            }

            // Finally, path from last detour node to destination
            var finalSegment = BuildWorldPath(pathStart, worldDestination);
            if (finalSegment != null && finalSegment.Count > 0)
            {
                int startIdx = (fullPath.Count > 0 && finalSegment.Count > 0 &&
                               Vector3.Distance(fullPath[fullPath.Count - 1], finalSegment[0]) < 0.5f) ? 1 : 0;

                for (int i = startIdx; i < finalSegment.Count; i++)
                {
                    fullPath.Add(finalSegment[i]);
                }
            }

            if (fullPath.Count == 0)
            {
                if (logVisitorPathfinding)
                    Debug.LogWarning("[Confusion:Detour] Failed to build full path - empty result");
                return false;
            }

            // Set the new detour path
            worldPath = fullPath;
            worldPathIndex = 0;

            if (logVisitorPathfinding)
                Debug.Log($"[Confusion:Detour] SUCCESS - Detour path built with {fullPath.Count} waypoints through {detourNodeIds.Count} nodes");

            return true;
        }

        #endregion

        #region Gizmos

        protected virtual void OnDrawGizmos()
        {
            // Draw world-space path with waypoint spheres
            if (worldPath != null && worldPath.Count > 0)
            {
                // Draw path lines in cyan
                Gizmos.color = Color.cyan;
                for (int i = 0; i < worldPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                }

                // Draw every waypoint as a small sphere
                // Already traversed waypoints in gray
                // Upcoming waypoints in magenta
                // Current target in yellow (larger)
                for (int i = 0; i < worldPath.Count; i++)
                {
                    if (i < worldPathIndex)
                    {
                        // Already passed - gray
                        Gizmos.color = Color.gray;
                        Gizmos.DrawWireSphere(worldPath[i], 0.15f);
                    }
                    else if (i == worldPathIndex && IsMovementState(state))
                    {
                        // Current target - yellow (larger)
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(worldPath[i], 0.4f);
                        Gizmos.DrawSphere(worldPath[i], 0.2f);
                    }
                    else
                    {
                        // Upcoming waypoints - magenta
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(worldPath[i], 0.2f);
                    }
                }

                // Draw waypoint indices as text labels (every 10th waypoint for clarity)
                #if UNITY_EDITOR
                for (int i = 0; i < worldPath.Count; i += 10)
                {
                    UnityEditor.Handles.Label(worldPath[i] + Vector3.up * 0.5f, i.ToString());
                }
                #endif
            }

            // Draw destination
            if (originalDestination != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(originalDestination, 0.5f);
            }

            // Draw visitor position and direction
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }

        #endregion
    }
}

using System.Collections.Generic;
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

        /// <summary>
        /// Represents a visited tile in the fascinated random walk with its unexplored neighbors.
        /// </summary>
        protected class FascinatedPathNode
        {
            public Vector2Int Position { get; set; }
            public List<Vector2Int> UnexploredNeighbors { get; set; }

            public FascinatedPathNode(Vector2Int position, List<Vector2Int> unexploredNeighbors)
            {
                Position = position;
                UnexploredNeighbors = new List<Vector2Int>(unexploredNeighbors);
            }

            public bool HasUnexploredNeighbors => UnexploredNeighbors.Count > 0;

            public Vector2Int PopNextNeighbor()
            {
                if (UnexploredNeighbors.Count == 0)
                    throw new System.InvalidOperationException("No unexplored neighbors to pop");

                Vector2Int next = UnexploredNeighbors[0];
                UnexploredNeighbors.RemoveAt(0);
                return next;
            }
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

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use 3D model instead of sprite-based rendering")]
        protected bool use3DModel = false;

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

        protected List<Vector2Int> path;
        protected int currentPathIndex;
        protected bool hasLoggedPathIssue;
        protected VisitorState state;
        protected Animator animator;
        protected GameController gameController;
        protected MazeGridBehaviour mazeGridBehaviour;
        protected bool isEntranced;
        protected float speedMultiplier = 1f;

        // 2D rendering and physics
        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D rb2D;

        // 3D rendering and physics
        protected GameObject modelInstance;
        protected Rigidbody rb3D;

        protected Vector2 authoredSpriteWorldSize;
        protected Vector2Int originalDestination;

        protected bool isCalculatingPath;

        // Fascination state (for FaeLantern)
        protected bool isFascinated;
        protected Vector2Int fascinationLanternPosition;
        protected bool hasReachedLantern;
        protected float fascinationTimer;
        protected FaeMaze.Props.FaeLantern currentFaeLantern;

        // Cooldown tracking per lantern (prevents immediate re-triggering)
        protected Dictionary<FaeMaze.Props.FaeLantern, float> lanternCooldowns;

        protected Vector3 initialScale;

        // Track last 10 tiles reached to prevent short-term backtracking
        protected Queue<Vector2Int> recentlyReachedTiles;
        protected const int MAX_RECENT_TILES = 10;

        // Track visited tiles during fascinated random walk as a tree structure
        protected List<FascinatedPathNode> fascinatedPathNodes;

        protected const string DirectionParameter = "Direction";
        protected const int IdleDirection = 0;
        protected const float MovementEpsilonSqr = 0.0001f;
        protected const float StallLoggingDelaySeconds = 0.35f;

        // Cached direction to prevent animation flickering when movement delta is small
        protected int lastDirection = IdleDirection;
        protected int currentAnimatorDirection = IdleDirection;

        protected int waypointsTraversedSinceSpawn;
        protected int lastLoggedWaypointIndex = -1;
        protected float stalledDuration;
        protected bool hasLoggedCurrentStall;
        protected bool hasMovedSignificantly;
        protected bool isCurrentlyStalled;

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

        // Lost segment tracking (for exploratory detours in Lost state)
        protected bool lostSegmentActive;
        protected int lostSegmentEndIndex;

        // Confusion segment tracking (for wrong-turn detours at intersections)
        protected bool isConfused;
        protected bool confusionSegmentActive;
        protected int confusionSegmentEndIndex;
        protected int confusionStepsTarget;
        protected int confusionStepsTaken;

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
            recentlyReachedTiles = new Queue<Vector2Int>();
            fascinatedPathNodes = new List<FascinatedPathNode>();
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

            // Apply archetype-specific settings if config is available
            if (config != null)
            {
                moveSpeed = config.BaseSpeed;
                mesmerizedDuration = config.InitialMesmerizedDuration;
                frightenedDuration = config.FrightenedDuration;
                minLostDistance = Mathf.RoundToInt(config.LostDetourMin);
                maxLostDistance = Mathf.RoundToInt(config.LostDetourMax);
            }
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

            // Check for FaeLantern influence (grid-based detection)
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

        private string FormatPath(List<Vector2Int> candidatePath)
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
                sb.Append(candidatePath[i]);
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
            if (animator == null || string.IsNullOrEmpty(parameterName))
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

        /// <summary>
        /// Emits a warning describing obvious path integrity issues at the current stall location.
        /// This bypasses the normal logging gate so stalled visitors always surface invalid routes.
        /// </summary>
        private void LogStallPathIssues(Vector2Int currentGrid)
        {
            if (path == null || path.Count == 0 || mazeGridBehaviour == null)
            {
                return;
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            List<string> issues = new List<string>();

            int targetIndex = Mathf.Clamp(currentPathIndex, 0, path.Count - 1);
            Vector2Int target = path[targetIndex];
            int manhattanToTarget = Mathf.Abs(currentGrid.x - target.x) + Mathf.Abs(currentGrid.y - target.y);
            if (manhattanToTarget > 1)
            {
                issues.Add($"stalling away from next waypoint {target} (index {targetIndex})");
            }

            if (grid != null)
            {
                MazeGrid.MazeNode targetNode = grid.GetNode(target.x, target.y);
                if (targetNode == null || !targetNode.walkable)
                {
                    string reason = targetNode == null ? "missing" : "not walkable";
                    issues.Add($"next waypoint {target} is {reason}");
                }
            }

            for (int i = targetIndex + 1; i < path.Count; i++)
            {
                Vector2Int prev = path[i - 1];
                Vector2Int next = path[i];
                int manhattan = Mathf.Abs(prev.x - next.x) + Mathf.Abs(prev.y - next.y);
                if (manhattan != 1)
                {
                    issues.Add($"non-adjacent hop between {prev} (index {i - 1}) and {next} (index {i})");
                }

                if (grid != null)
                {
                    MazeGrid.MazeNode node = grid.GetNode(next.x, next.y);
                    if (node == null || !node.walkable)
                    {
                        string reason = node == null ? "missing" : "not walkable";
                        issues.Add($"waypoint {next} at index {i} is {reason}");
                    }
                }
            }

            if (issues.Count > 0)
            {
                hasLoggedPathIssue = true;
            }
        }

        private void LogVisitorPath(string message)
        {
            // Logging disabled
        }

        private bool LogVisitorPathWarning(string message)
        {
            return false;
        }

        private void UpdatePathLoggingOnMovement(Vector3 previousPosition, Vector3 currentPosition)
        {
            float deltaSqr = (currentPosition - previousPosition).sqrMagnitude;

            bool gridResolved = false;
            bool remainedInSameCell = false;

            int prevX = 0;
            int prevY = 0;
            int curX = 0;
            int curY = 0;

            if (mazeGridBehaviour != null && mazeGridBehaviour.WorldToGrid(previousPosition, out prevX, out prevY) && mazeGridBehaviour.WorldToGrid(currentPosition, out curX, out curY))
            {
                gridResolved = true;
                remainedInSameCell = prevX == curX && prevY == curY;

                // Consider reaching a new cell as significant movement even if the delta is tiny (e.g., pathfinding nudges)
                if (!remainedInSameCell)
                {
                    hasMovedSignificantly = true;
                }
            }

            // If grid resolution fails, still consider any measurable displacement as movement so stalls after visible motion log correctly
            if (!hasMovedSignificantly && deltaSqr > MovementEpsilonSqr)
            {
                hasMovedSignificantly = true;
            }

            bool isStationary = deltaSqr <= MovementEpsilonSqr;

            if (isStationary)
            {
                isCurrentlyStalled = true;
                stalledDuration += Time.deltaTime;

                bool canReportStall = hasMovedSignificantly || stalledDuration >= StallLoggingDelaySeconds;

                if (canReportStall && !hasLoggedCurrentStall && stalledDuration >= StallLoggingDelaySeconds)
                {
                    hasLoggedCurrentStall = true;

                    Vector2Int stalledGrid = Vector2Int.zero;
                    bool resolvedGrid = gridResolved;
                    if (resolvedGrid)
                    {
                        stalledGrid = new Vector2Int(remainedInSameCell ? prevX : curX, remainedInSameCell ? prevY : curY);
                    }
                    else if (mazeGridBehaviour != null && mazeGridBehaviour.WorldToGrid(currentPosition, out int stalledX, out int stalledY))
                    {
                        resolvedGrid = true;
                        stalledGrid = new Vector2Int(stalledX, stalledY);
                    }

                    Vector2Int targetWaypoint = (path != null && currentPathIndex < path.Count) ? path[currentPathIndex] : Vector2Int.zero;
                    if (resolvedGrid)
                    {
                        LogStallPathIssues(stalledGrid);
                    }
                }
            }
            else
            {
                isCurrentlyStalled = false;
                hasMovedSignificantly = true;

                stalledDuration = 0f;
                hasLoggedCurrentStall = false;
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
            return config != null ? config.EssenceReward : 1;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the visitor with a reference to the game controller.
        /// </summary>
        public virtual void Initialize(GameController controller)
        {
            gameController = controller;

            if (gameController != null && gameController.MazeGrid != null)
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
        /// Sets the path for the visitor to follow and begins walking.
        /// </summary>
        public virtual void SetPath(List<Vector2Int> gridPath)
        {
            if (gridPath == null || gridPath.Count == 0)
            {
                return;
            }

            if (ShouldLogVisitorPath())
            {
                LogVisitorPath($"SetPath(List<Vector2Int>) with {gridPath.Count} waypoint(s). Provided path: {FormatPath(gridPath)}. Current world position: {transform.position}.");
            }

            originalDestination = gridPath[gridPath.Count - 1];
            waypointsTraversedSinceSpawn = 0;
            ResetDetourState();
            hasLoggedPathIssue = false;
            lastLoggedWaypointIndex = -1;
            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            hasMovedSignificantly = false;
            isCurrentlyStalled = false;

            RecalculatePath();
        }

        /// <summary>
        /// Sets the path using MazeNode objects.
        /// </summary>
        public virtual void SetPath(List<MazeGrid.MazeNode> nodePath)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                return;
            }

            List<Vector2Int> gridPath = new List<Vector2Int>();
            foreach (var node in nodePath)
            {
                gridPath.Add(new Vector2Int(node.x, node.y));
            }

            if (gridPath.Count > 0)
            {
                originalDestination = gridPath[gridPath.Count - 1];
            }

            if (ShouldLogVisitorPath())
            {
                LogVisitorPath($"SetPath(List<MazeNode>) with {gridPath.Count} waypoint(s). Provided path: {FormatPath(gridPath)}. Current world position: {transform.position}.");
            }

            waypointsTraversedSinceSpawn = 0;
            ResetDetourState();
            hasLoggedPathIssue = false;
            lastLoggedWaypointIndex = -1;
            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            isCurrentlyStalled = false;

            RecalculatePath();
        }

        /// <summary>
        /// Logs warnings for obvious path integrity problems such as gaps or blocked nodes.
        /// Helps diagnose invalid paths produced by pathfinding before movement begins.
        /// </summary>
        private void LogPathIntegrityIssues(List<Vector2Int> candidatePath, Vector2Int currentPos, string context)
        {
            if (candidatePath == null || candidatePath.Count == 0 || mazeGridBehaviour == null)
            {
                return;
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            List<string> issues = new List<string>();

            // Verify starting step from the visitor's resolved grid position
            Vector2Int firstWaypoint = candidatePath[0];
            int distanceFromCurrent = Mathf.Abs(currentPos.x - firstWaypoint.x) + Mathf.Abs(currentPos.y - firstWaypoint.y);
            if (distanceFromCurrent > 1)
            {
                issues.Add($"start {firstWaypoint} is not adjacent to current grid position {currentPos}");
            }

            if (grid != null)
            {
                var firstNode = grid.GetNode(firstWaypoint.x, firstWaypoint.y);
                if (firstNode == null || !firstNode.walkable)
                {
                    string reason = firstNode == null ? "missing" : "not walkable";
                    issues.Add($"starting waypoint {firstWaypoint} is {reason}");
                }
            }

            // Validate each subsequent hop for adjacency and walkability
            for (int i = 1; i < candidatePath.Count; i++)
            {
                Vector2Int previous = candidatePath[i - 1];
                Vector2Int waypoint = candidatePath[i];
                int manhattan = Mathf.Abs(previous.x - waypoint.x) + Mathf.Abs(previous.y - waypoint.y);

                if (manhattan != 1)
                {
                    issues.Add($"non-adjacent step between {previous} (index {i - 1}) and {waypoint} (index {i})");
                }

                if (grid != null)
                {
                    MazeGrid.MazeNode node = grid.GetNode(waypoint.x, waypoint.y);
                    if (node == null || !node.walkable)
                    {
                        string reason = node == null ? "missing" : "not walkable";
                        issues.Add($"waypoint {waypoint} at index {i} is {reason}");
                    }
                }
            }

            if (issues.Count > 0)
            {
                string pathString = FormatPath(candidatePath);
                if (LogVisitorPathWarning($"{context}: detected {issues.Count} path issue(s). Current grid: {currentPos}. Path length: {candidatePath.Count}. Issues: {string.Join("; ", issues)}. Path: {pathString}."))
                {
                    hasLoggedPathIssue = true;
                }
            }
        }

        #endregion

        #region Movement

        protected void UpdateAnimatorDirection(Vector2 movement)
        {
            // For 3D models, also handle rotation towards movement direction
            if (use3DModel && modelInstance != null && movement.sqrMagnitude > MovementEpsilonSqr)
            {
                // Rotate 3D model to face movement direction
                Vector3 movementDir = new Vector3(movement.x, movement.y, 0f).normalized;

                // Convert XY movement to 3D rotation (Y-axis up)
                // In 3D with top-down view, X/Y movement maps to X/Z in 3D space
                Vector3 facing = new Vector3(movementDir.x, 0f, movementDir.y);

                if (facing.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
                    modelInstance.transform.rotation = Quaternion.Slerp(
                        modelInstance.transform.rotation,
                        targetRotation,
                        Time.deltaTime * 10f // Smooth rotation speed
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
            if (currentAnimatorDirection != direction)
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
                // Convert from Blender Y-up to Unity top-down 2D:
                // - Model +Z aligns with World -Z (perpendicular to screen, away from camera)
                // - Model -Y aligns with direction of travel (in XY movement plane)
                // Base: X: 90° tips model flat, Y: 180° flips it to face camera
                // Direction: Rotate around game Z to orient model -Y toward movement direction
                //   Up (+Y): Z: 180°, Right (+X): Z: 90°, Left (-X): Z: -90°, Down (-Y): Z: 0°
                Quaternion baseRotation = Quaternion.Euler(90f, 180f, 0f);
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, zRotation);
                animator.transform.localRotation = directionRotation * baseRotation;
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
            if (path == null || path.Count == 0)
            {
                state = VisitorState.Idle;
                SetAnimatorDirection(IdleDirection);
                return;
            }

            if (mazeGridBehaviour == null)
            {
                return;
            }

            Vector3 previousPosition = transform.position;

            // Bounds check for currentPathIndex
            if (currentPathIndex >= path.Count)
            {
                currentPathIndex = path.Count - 1;

                // If fascinated, clear fascination state since we can't continue
                if (isFascinated && hasReachedLantern)
                {
                    isFascinated = false;
                    hasReachedLantern = false;
                    ClearLanternInteraction();
                    fascinatedPathNodes.Clear();
                }
            }

            // Get current target waypoint
            Vector2Int targetGridPos = path[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            if (currentPathIndex != lastLoggedWaypointIndex)
            {
                LogVisitorPath($"moving toward waypoint {targetGridPos} (index {currentPathIndex + 1}/{path.Count}). Current grid index: {currentPathIndex}. Path: {FormatPath(path)}.");
                lastLoggedWaypointIndex = currentPathIndex;
            }

            // Validate that the next tile is reachable from the current grid position
            if (mazeGridBehaviour.WorldToGrid(transform.position, out int currentGridX, out int currentGridY))
            {
                Vector2Int currentGridPos = new Vector2Int(currentGridX, currentGridY);
                int manhattan = Mathf.Abs(currentGridPos.x - targetGridPos.x) + Mathf.Abs(currentGridPos.y - targetGridPos.y);

                if (manhattan > 1 && !hasLoggedPathIssue)
                {
                    string pathString = FormatPath(path);
                    if (LogVisitorPathWarning($"is trying to step from {currentGridPos} to non-adjacent waypoint {targetGridPos} at index {currentPathIndex}. Path length: {path.Count}. Path: {pathString}."))
                    {
                        hasLoggedPathIssue = true;
                    }
                }
            }
            else if (!hasLoggedPathIssue)
            {
                string pathString = FormatPath(path);
                if (LogVisitorPathWarning($"could not resolve its current grid position while targeting waypoint {targetGridPos} at index {currentPathIndex}. Path length: {path.Count}. Path: {pathString}."))
                {
                    hasLoggedPathIssue = true;
                }
            }

            // Move toward target (apply speed multiplier from terrain)
            float terrainSpeedMultiplier = 1f;
            MazeGrid mazeGrid = mazeGridBehaviour.Grid;
            if (mazeGrid != null)
            {
                MazeGrid.MazeNode targetNode = mazeGrid.GetNode(targetGridPos.x, targetGridPos.y);
                if (targetNode == null || !targetNode.walkable)
                {
                    if (!hasLoggedPathIssue)
                    {
                        string reason = targetNode == null ? "missing" : "not walkable";
                        string terrainType = targetNode != null ? targetNode.terrain.ToString() : "unknown";
                        if (LogVisitorPathWarning($"cannot move to waypoint {targetGridPos} at index {currentPathIndex} because node is {reason} (terrain: {terrainType}, walkable: {targetNode?.walkable}). Path length: {path.Count}."))
                        {
                            hasLoggedPathIssue = true;
                        }
                    }

                    UpdatePathLoggingOnMovement(previousPosition, transform.position);
                    return; // Non-walkable nodes remain blocked
                }

                // Debug log when moving to water tiles
                if (targetNode.terrain == TileType.Water)
                {
                }

                terrainSpeedMultiplier = mazeGrid.GetSpeedMultiplier(targetGridPos.x, targetGridPos.y);
            }

            terrainSpeedMultiplier = Mathf.Max(terrainSpeedMultiplier, 0.01f); // Prevent zero speed
            float effectiveSpeed = moveSpeed * speedMultiplier * terrainSpeedMultiplier;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            Vector3 movementDelta = newPosition - transform.position;
            UpdateAnimatorDirection(movementDelta);

            // Use appropriate physics system based on mode
            if (use3DModel)
            {
                // 3D physics
                if (rb3D != null)
                {
                    rb3D.MovePosition(newPosition);
                    Physics.SyncTransforms();
                }
                else
                {
                    transform.position = newPosition;
                }
            }
            else
            {
                // 2D physics
                if (rb2D != null)
                {
                    rb2D.MovePosition(newPosition);
                    Physics2D.SyncTransforms();
                }
                else
                {
                    transform.position = newPosition;
                }
            }

            UpdatePathLoggingOnMovement(previousPosition, transform.position);

            // Check if we've reached the waypoint
            float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
            if (distanceToTarget < waypointReachedDistance)
            {
                OnWaypointReached();
            }
        }

        protected virtual void OnWaypointReached()
        {
            Vector2Int currentWaypoint = path[currentPathIndex];

            LogVisitorPath($"reached waypoint {currentWaypoint} (index {currentPathIndex + 1}/{path.Count}). Waypoints traversed since spawn: {waypointsTraversedSinceSpawn}.");

            // Add to recently reached tiles queue (maintain last 10)
            if (recentlyReachedTiles != null)
            {
                recentlyReachedTiles.Enqueue(currentWaypoint);

                // Remove oldest tiles if we exceed the maximum
                while (recentlyReachedTiles.Count > MAX_RECENT_TILES)
                {
                    recentlyReachedTiles.Dequeue();
                }
            }

            // Increment waypoint counter
            waypointsTraversedSinceSpawn++;

            // Check if fascinated visitor reached the lantern
            if (isFascinated && !hasReachedLantern && currentPathIndex < path.Count)
            {
                if (currentWaypoint == fascinationLanternPosition)
                {
                    hasReachedLantern = true;

                    // Start fascination timer
                    if (currentFaeLantern != null)
                    {
                        fascinationTimer = currentFaeLantern.FascinationDuration;
                    }
                    else
                    {
                        fascinationTimer = 2f; // Fallback
                    }

                    SetAnimatorDirection(IdleDirection);
                    return; // Don't increment or handle detour
                }
                else
                {
                    // Fascinated visitor reached intermediate waypoint on path to lantern
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        OnPathCompleted();
                    }
                    return; // Don't call RecalculatePath for fascinated visitors
                }
            }

            // For fascinated visitors doing random walk, handle path extension
            if (isFascinated && hasReachedLantern && fascinationTimer <= 0)
            {
                // Check if current waypoint is the original destination
                if (currentWaypoint == originalDestination)
                {
                    OnPathCompleted();
                    return;
                }

                // Check if fascinated visitor wandered onto ANY spawn point (exit)
                if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2)
                {
                    if (mazeGridBehaviour.IsSpawnPoint(currentWaypoint))
                    {
                        ForceEscape();
                        return;
                    }
                }

                currentPathIndex++;
                HandleFascinatedRandomWalk();
                return; // Don't call RecalculatePath for fascinated random walk
            }

            // Normal waypoint handling - call detour logic hook
            HandleDetourAtWaypoint();
        }

        protected virtual void OnPathCompleted()
        {
            LogVisitorPath($"completed path at world {transform.position}. Waypoints traversed: {waypointsTraversedSinceSpawn}. Path length: {path?.Count ?? 0}.");

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;
            ClearLanternInteraction();

            // Check if we're using the new spawn marker system
            bool isUsingSpawnMarkers = mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2;

            if (isUsingSpawnMarkers)
            {
                // ESCAPE: Visitor reached destination spawn point
                state = VisitorState.Escaping;
                SetAnimatorDirection(IdleDirection);

                // Visual feedback: fade to transparent
                if (spriteRenderer != null)
                {
                    Color escapingColor = visitorColor;
                    escapingColor.a = 0.3f;
                    spriteRenderer.color = escapingColor;
                }

                Destroy(gameObject, 0.2f);
            }
            else
            {
                // LEGACY CONSUMED: Visitor reached the heart
                state = VisitorState.Consumed;
                SetAnimatorDirection(IdleDirection);

                // Derived classes handle consumption logic (awards essence, stats, etc.)
                HandleConsumption();
            }
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
            fascinationLanternPosition = Vector2Int.zero;
        }

        /// <summary>
        /// Checks if the visitor has entered any FaeLantern's influence area.
        /// </summary>
        protected virtual void CheckFaeLanternInfluence()
        {
            if (mazeGridBehaviour == null)
                return;

            // Get current grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
                return;

            Vector2Int currentGridPos = new Vector2Int(x, y);

            // Check all active FaeLanterns
            foreach (var lantern in FaeMaze.Props.FaeLantern.All)
            {
                if (lantern == null)
                    continue;

                // Check if this cell is in the lantern's influence
                if (lantern.IsCellInInfluence(currentGridPos))
                {
                    EnterFaeInfluence(lantern, currentGridPos);
                    break; // Only one lantern can capture a visitor
                }
            }
        }

        /// <summary>
        /// Called when a visitor enters a FaeLantern's influence area.
        /// Uses archetype-specific fascination parameters if config is available.
        /// </summary>
        protected virtual void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern, Vector2Int visitorGridPosition)
        {
            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && fascinationLanternPosition == lantern.GridPosition)
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
            fascinationLanternPosition = lantern.GridPosition;
            hasReachedLantern = false;
            fascinationTimer = 0f; // Will be set when reaching lantern

            // Set archetype-specific cooldown for this lantern
            lanternCooldowns[lantern] = cooldown;

            // Clear path nodes from previous fascination
            fascinatedPathNodes.Clear();

            // Abandon current path and reset detour state
            path = null;
            currentPathIndex = 0;
            ResetDetourState();

            // Calculate path to lantern
            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                // Fascinated visitors use normal attraction (they're already mesmerized)
                if (gameController.TryFindPath(currentPos, fascinationLanternPosition, pathToLantern, 1.0f) && pathToLantern.Count > 0)
                {
                    // Convert to Vector2Int path
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    // Find starting waypoint
                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        // Try to find current grid position in the path
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (path[i] == currentPos)
                            {
                                currentPathIndex = i;
                                break;
                            }
                        }

                        // If very close to current tile center, advance to next
                        Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                        float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                        if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                        {
                            currentPathIndex++;
                        }
                    }

                    RefreshStateFromFlags();
                }
                else
                {
                    isFascinated = false;
                    hasReachedLantern = false;
                    ClearLanternInteraction();
                    RefreshStateFromFlags();
                }
            }
        }

        #endregion

        #region Fascinated Random Walk

        /// <summary>
        /// Handles random walk behavior for fascinated visitors after they've reached the lantern.
        /// </summary>
        protected virtual void HandleFascinatedRandomWalk()
        {
            // Only extend path if we're near the end (within 3 waypoints)
            int waypointsRemaining = path.Count - currentPathIndex;
            if (waypointsRemaining > 3)
            {
                return; // Still have enough waypoints ahead
            }

            // Get the actual current position
            Vector2Int currentPos;
            if (currentPathIndex == 0 && path.Count > 0)
            {
                currentPos = path[0];
            }
            else if (currentPathIndex > 0 && currentPathIndex < path.Count)
            {
                currentPos = path[currentPathIndex - 1];
            }
            else if (currentPathIndex >= path.Count && path.Count > 0)
            {
                currentPos = path[path.Count - 1];
            }
            else
            {
                return;
            }

            // Initialize path nodes on first call after reaching lantern
            if (fascinatedPathNodes.Count == 0)
            {
                // Get all walkable neighbors
                List<Vector2Int> neighbors = GetWalkableNeighbors(currentPos);

                // Exclude the tile we came from
                if (currentPathIndex > 1)
                {
                    Vector2Int previousTile = path[currentPathIndex - 2];
                    neighbors.Remove(previousTile);
                }

                // Shuffle remaining neighbors randomly
                ShuffleList(neighbors);

                // Create initial node at lantern position
                FascinatedPathNode initialNode = new FascinatedPathNode(currentPos, neighbors);
                fascinatedPathNodes.Add(initialNode);
            }

            // Get current node (last in list = current position)
            FascinatedPathNode currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];

            // Check if current node has unexplored neighbors
            while (!currentNode.HasUnexploredNeighbors && fascinatedPathNodes.Count > 0)
            {
                // Dead end - backtrack by removing current node
                fascinatedPathNodes.RemoveAt(fascinatedPathNodes.Count - 1);

                if (fascinatedPathNodes.Count == 0)
                {
                    // Exhausted all paths - visitor has fully explored
                    OnPathCompleted();
                    return;
                }

                // Move to parent node
                currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];
                Vector2Int backtrackPos = currentNode.Position;

                // Add backtrack waypoint if we're not already there
                if (backtrackPos != currentPos)
                {
                    path.Add(backtrackPos);
                    currentPos = backtrackPos;
                }
            }

            if (fascinatedPathNodes.Count == 0)
            {
                return; // Fully explored
            }

            // Current node has unexplored neighbors - pick the next one
            Vector2Int nextPos = currentNode.PopNextNeighbor();

            // Get neighbors of next position and shuffle them
            List<Vector2Int> nextNeighbors = GetWalkableNeighbors(nextPos);

            // Build set of visited positions for filtering
            HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
            foreach (var node in fascinatedPathNodes)
            {
                visitedPositions.Add(node.Position);
            }

            // Remove neighbors that we've already visited
            nextNeighbors.RemoveAll(n => visitedPositions.Contains(n));
            ShuffleList(nextNeighbors);

            // Create new node for next position
            FascinatedPathNode nextNode = new FascinatedPathNode(nextPos, nextNeighbors);
            fascinatedPathNodes.Add(nextNode);

            // Add to movement path
            path.Add(nextPos);
        }

        /// <summary>
        /// Shuffles a list in place using Fisher-Yates algorithm.
        /// </summary>
        protected void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        #endregion

        #region Neighbor Queries

        /// <summary>
        /// Gets all walkable neighbor positions for a grid position.
        /// </summary>
        protected List<Vector2Int> GetWalkableNeighbors(Vector2Int gridPos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            // Check 4 cardinal directions
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0)   // Left
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = gridPos + dir;
                var node = mazeGridBehaviour.Grid.GetNode(neighborPos.x, neighborPos.y);

                if (node != null && node.walkable)
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
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
        /// Resumes the visitor's movement if they have a path.
        /// </summary>
        public virtual void Resume()
        {
            if (path != null && path.Count > 0 && currentPathIndex < path.Count)
            {
                RefreshStateFromFlags();
            }
        }

        /// <summary>
        /// Attempts to find a path from start to destination using A*.
        /// Returns true if successful, with the path in the out parameter.
        /// </summary>
        protected bool TryFindPathToDestination(Vector2Int start, Vector2Int destination, out List<Vector2Int> pathResult)
        {
            pathResult = null;

            if (gameController == null)
            {
                return false;
            }

            // Use state-based attraction multiplier for pathfinding
            float attractionMultiplier = GetAttractionMultiplier();

            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (!gameController.TryFindPath(start, destination, pathNodes, attractionMultiplier) || pathNodes.Count == 0)
            {
                return false;
            }

            pathResult = new List<Vector2Int>();
            foreach (var node in pathNodes)
            {
                pathResult.Add(new Vector2Int(node.x, node.y));
            }

            return true;
        }

        /// <summary>
        /// Gets the attraction multiplier for pathfinding based on current visitor state.
        /// Different states perceive tile attractions differently:
        /// - Walking (1.0): Normal sensitivity - attractions lure, repulsions repel
        /// - Frightened (-1.0): INVERTED - attractive tiles become repulsive (flee from lures!)
        /// - Confused (0.5): REDUCED sensitivity - less affected by Heart Powers
        /// - Lost (0.3): MINIMAL sensitivity - mostly ignores attractions while wandering
        /// - Fascinated/Mesmerized: Don't use standard pathfinding (special behavior)
        /// </summary>
        protected virtual float GetAttractionMultiplier()
        {
            switch (state)
            {
                case VisitorState.Frightened:
                    // Frightened visitors FLEE from attractive tiles (inverted)
                    return -1.0f;

                case VisitorState.Confused:
                    // Confused visitors are less affected by attractions
                    return 0.5f;

                case VisitorState.Lost:
                    // Lost visitors mostly ignore attractions while wandering
                    return 0.3f;

                case VisitorState.Lured:
                    // Lured visitors are HIGHLY sensitive to attractions (following Murmuring Paths)
                    return 1.0f;

                case VisitorState.Walking:
                case VisitorState.Idle:
                case VisitorState.Escaping:
                default:
                    // Normal attraction behavior
                    return 1.0f;

                case VisitorState.Fascinated:
                case VisitorState.Mesmerized:
                case VisitorState.Consumed:
                    // These states don't use standard pathfinding
                    // (Fascinated uses random walk, others are stationary/removed)
                    return 1.0f;
            }
        }

        /// <summary>
        /// Gets the destination for the current visitor state.
        /// Override in derived classes to add state-specific routing logic.
        /// </summary>
        protected virtual Vector2Int GetDestinationForCurrentState(Vector2Int currentPos)
        {
            // Default routing logic based on state
            switch (state)
            {
                case VisitorState.Fascinated:
                    // Fascinated visitors path to the lantern
                    return fascinationLanternPosition;

                case VisitorState.Lured:
                    // Lured visitors path to the Heart (drawn by Murmuring Paths)
                    if (gameController != null)
                    {
                        var heart = gameController.Heart;
                        if (heart != null && mazeGridBehaviour != null)
                        {
                            if (mazeGridBehaviour.WorldToGrid(heart.transform.position, out int hx, out int hy))
                            {
                                return new Vector2Int(hx, hy);
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                    }
                    // Fallback to original destination if Heart not found
                    return originalDestination;

                case VisitorState.Mesmerized:
                    // Mesmerized visitors don't move (duration-based)
                    return currentPos;

                case VisitorState.Frightened:
                    // Frightened visitors try to escape - find farthest border tile
                    // This is a simplified implementation; can be overridden for custom behavior
                    return originalDestination;

                case VisitorState.Lost:
                    // Lost visitors eventually head to destination but may take detours
                    // Base behavior is to use original destination
                    return originalDestination;

                case VisitorState.Confused:
                case VisitorState.Walking:
                case VisitorState.Idle:
                default:
                    // Normal states use the original destination
                    return originalDestination;
            }
        }

        /// <summary>
        /// Recalculates the path to the current state-appropriate destination.
        /// </summary>
        public virtual void RecalculatePath()
        {
            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Fascinated visitors use random walk, not A* recalculation
            if (isFascinated)
            {
                return;
            }

            isCalculatingPath = true;

            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                LogVisitorPathWarning($"could not resolve current grid while recalculating path.");
                isCalculatingPath = false;
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);
            Vector2Int destination = GetDestinationForCurrentState(currentPos);
            float attractionMultiplier = GetAttractionMultiplier();

            LogVisitorPath($"recalculating path from {currentPos} to {destination} (state: {state}, attraction multiplier: {attractionMultiplier:F1}x).");

            if (!TryFindPathToDestination(currentPos, destination, out List<Vector2Int> newPath))
            {
                if (!hasLoggedPathIssue)
                {
                    if (LogVisitorPathWarning($"could not find path from {currentPos} to destination {destination} (state: {state})."))
                    {
                        hasLoggedPathIssue = true;
                    }
                }
                isCalculatingPath = false;
                return;
            }

            LogVisitorPath($"recalculated path length {newPath.Count}. Path: {FormatPath(newPath)}.");

            // Debug: Check if path goes through any tiles with attraction
            int attractiveTileCount = 0;
            float maxAttraction = 0f;
            Vector2Int maxAttractionTile = Vector2Int.zero;

            foreach (var tile in newPath)
            {
                var node = mazeGridBehaviour.Grid.GetNode(tile.x, tile.y);
                if (node != null && Mathf.Abs(node.attraction) > 0.01f)
                {
                    attractiveTileCount++;
                    float moveCost = mazeGridBehaviour.Grid.GetMoveCost(tile.x, tile.y, attractionMultiplier);

                    if (Mathf.Abs(node.attraction) > Mathf.Abs(maxAttraction))
                    {
                        maxAttraction = node.attraction;
                        maxAttractionTile = tile;
                    }
                }
            }

            if (attractiveTileCount > 0)
            {
            }
            else
            {
            }

            path = newPath;
            recentlyReachedTiles.Clear();
            if (path.Count > 0)
            {
                recentlyReachedTiles.Enqueue(path[0]);
            }

            currentPathIndex = path.Count > 1 ? 1 : 0;
            hasLoggedPathIssue = false;
            lastLoggedWaypointIndex = -1;

            LogVisitorPath($"set currentPathIndex to {currentPathIndex}. Target waypoint: {(currentPathIndex < path.Count ? path[currentPathIndex].ToString() : "<none>")}.");

            LogPathIntegrityIssues(path, currentPos, "recalculated path");

            if (path.Count <= 1)
            {
                isCalculatingPath = false;
                OnPathCompleted();
                return;
            }

            RefreshStateFromFlags();
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
        /// <param name="duration">Duration in seconds (0 or negative = use default)</param>
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
        /// </summary>
        /// <param name="duration">Duration in seconds (0 or negative = use default)</param>
        public virtual void SetLost(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = lostDuration;
            }

            isLost = true;
            SetTimedState(VisitorState.Lost, duration);
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Sets the visitor to Frightened state for a specified duration.
        /// </summary>
        /// <param name="duration">Duration in seconds (0 or negative = use default)</param>
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
        /// Sets the visitor to Lured state, drawn toward the Heart by Murmuring Paths.
        /// This state lasts as long as the power is active (managed externally).
        /// </summary>
        public virtual void SetLured(bool value)
        {
            if (isLured != value)
            {
                isLured = value;
                RefreshStateFromFlags();

                // Recalculate path when lured state changes
                if (value)
                {
                    RecalculatePath();
                }
            }
        }

        /// <summary>
        /// Checks for nearby Red Caps and triggers frightened state if one is detected within range.
        /// Only checks if visitor is in an active movement state and not already frightened.
        /// </summary>
        protected virtual void CheckForNearbyRedCaps()
        {
            // Don't check if already frightened, consumed, or escaping
            if (isFrightened || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Find all Red Caps in the scene
            RedCapController[] redCaps = FindObjectsByType<RedCapController>(FindObjectsSortMode.None);

            foreach (var redCap in redCaps)
            {
                if (redCap == null || redCap.gameObject == null)
                    continue;

                // Check distance to this Red Cap
                float distance = Vector3.Distance(transform.position, redCap.transform.position);

                if (distance <= redCapDetectionRadius)
                {
                    // Red Cap is nearby! Become frightened
                    SetFrightened(frightenedDuration);
                    return; // Only need to detect one
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
        /// Called when a timed state expires. Clears the state flag and reverts to normal behavior.
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

            // Clear timed state tracking
            currentTimedState = VisitorState.Idle;
            currentStateDuration = 0f;
            currentStateTimer = 0f;

            // Refresh state to revert to Walking or other active state
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Forces the visitor to escape immediately.
        /// </summary>
        public virtual void ForceEscape()
        {
            state = VisitorState.Escaping;
            SetAnimatorDirection(IdleDirection);

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;
            ClearLanternInteraction();

            // Visual feedback
            if (spriteRenderer != null)
            {
                Color escapingColor = visitorColor;
                escapingColor.a = 0.3f;
                spriteRenderer.color = escapingColor;
            }

            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// Makes this visitor fascinated by a FaeLantern.
        /// </summary>
        public virtual void BecomeFascinated(Vector2Int lanternGridPosition)
        {
            if (!IsMovementState(state))
            {
                return; // Only actively moving visitors can be fascinated
            }

            isFascinated = true;
            fascinationLanternPosition = lanternGridPosition;
            hasReachedLantern = false;
            RefreshStateFromFlags();

            // Discard current path and reset detour state
            path = null;
            currentPathIndex = 0;
            ResetDetourState();
            waypointsTraversedSinceSpawn = 0;

            // Get current position and pathfind to lantern
            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                // Fascinated visitors use normal attraction (they're already mesmerized)
                if (gameController.TryFindPath(currentPos, lanternGridPosition, pathToLantern, 1.0f) && pathToLantern.Count > 0)
                {
                    // Convert to Vector2Int path
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    // Find starting waypoint without backtracking
                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        // Try to find current grid position in the path
                        int currentGridIndex = -1;
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (path[i] == currentPos)
                            {
                                currentGridIndex = i;
                                break;
                            }
                        }

                        if (currentGridIndex >= 0)
                        {
                            currentPathIndex = currentGridIndex;

                            // If very close to center, advance to next waypoint
                            Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                            float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                            if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                            {
                                currentPathIndex++;
                            }
                        }
                        else
                        {
                            // Current tile not in path - use closest waypoint with forward bias
                            Vector3 currentWorldPos = transform.position;
                            float closestDist = float.MaxValue;

                            for (int i = 0; i < path.Count; i++)
                            {
                                Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                                float dist = Vector3.Distance(currentWorldPos, waypointWorldPos);

                                // Prefer waypoints ahead by biasing distance
                                float biasedDist = dist - (i * 0.01f);

                                if (biasedDist < closestDist)
                                {
                                    closestDist = biasedDist;
                                    currentPathIndex = i;
                                }
                            }

                            if (currentPathIndex < path.Count - 1 && closestDist < waypointReachedDistance)
                            {
                                currentPathIndex++;
                            }
                        }

                        hasLoggedPathIssue = false;
                        LogPathIntegrityIssues(path, currentPos, "fascination path");
                    }
                }
            }
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
            if (use3DModel)
            {
                // Setup 3D physics
                rb3D = GetComponent<Rigidbody>();
                if (rb3D == null)
                {
                    rb3D = gameObject.AddComponent<Rigidbody>();
                }

                rb3D.isKinematic = true;
                rb3D.useGravity = false;

                // Add CapsuleCollider for trigger detection (better for humanoid characters)
                CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();
                if (capsuleCollider == null)
                {
                    capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
                    capsuleCollider.height = 1.8f; // Typical humanoid height
                    capsuleCollider.radius = 0.3f;
                    capsuleCollider.center = new Vector3(0, 0.9f, 0); // Center at waist
                }

                capsuleCollider.isTrigger = true;
            }
            else
            {
                // Setup 2D physics
                rb2D = GetComponent<Rigidbody2D>();
                if (rb2D == null)
                {
                    rb2D = gameObject.AddComponent<Rigidbody2D>();
                }

                rb2D.bodyType = RigidbodyType2D.Kinematic;
                rb2D.gravityScale = 0f;

                // Add CircleCollider2D for trigger detection
                CircleCollider2D collider = GetComponent<CircleCollider2D>();
                if (collider == null)
                {
                    collider = gameObject.AddComponent<CircleCollider2D>();
                }

                collider.radius = 0.3f;
                collider.isTrigger = true;
            }
        }

        protected virtual void Setup3DModel()
        {
            if (modelPrefab == null)
            {
                Debug.LogWarning($"[VisitorControllerBase] use3DModel is true but modelPrefab is null on {gameObject.name}");
                return;
            }

            // Instantiate the model prefab
            modelInstance = Instantiate(modelPrefab, transform);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
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

        #region Abstract Methods - Detour Behavior Hooks

        /// <summary>
        /// Determines whether a detour should be attempted at the current waypoint based on state.
        /// Override to add custom detour logic for specific states.
        /// </summary>
        /// <returns>True if a detour should be attempted, false to use normal pathfinding</returns>
        protected virtual bool ShouldAttemptDetour(Vector2Int currentPos)
        {
            // Handle active lost segments
            if (lostSegmentActive)
            {
                // Check if we've completed the lost segment
                if (currentPathIndex <= lostSegmentEndIndex)
                {
                    // Still within lost segment - no new detour
                    return false;
                }

                // Lost segment completed - clear tracking and continue to recovery path
                lostSegmentActive = false;
                LogVisitorPath($"completed lost segment at index {currentPathIndex}. Continuing to destination.");
                currentPathIndex++;
                RefreshStateFromFlags();
                return false;
            }

            // Base implementation checks for state-specific detour conditions
            switch (state)
            {
                case VisitorState.Mesmerized:
                    // Mesmerized visitors don't move
                    return false;

                case VisitorState.Frightened:
                    // Frightened visitors may take evasive detours
                    // Base behavior: just recalculate path
                    return false;

                case VisitorState.Lost:
                    // Lost visitors take random detours at intersections
                    return IsAtIntersection(currentPos);

                case VisitorState.Confused:
                    // Confused state handled by derived classes
                    return true;

                case VisitorState.Fascinated:
                case VisitorState.Walking:
                case VisitorState.Idle:
                default:
                    // Normal movement states don't detour
                    return false;
            }
        }

        /// <summary>
        /// Checks if current position is an intersection (2+ walkable neighbors).
        /// </summary>
        protected bool IsAtIntersection(Vector2Int position)
        {
            List<Vector2Int> neighbors = GetWalkableNeighbors(position);

            // Exclude previous tile if we have path context
            if (path != null && currentPathIndex > 0 && currentPathIndex < path.Count)
            {
                Vector2Int previousTile = path[currentPathIndex - 1];
                neighbors.Remove(previousTile);
            }

            return neighbors.Count >= 2;
        }

        /// <summary>
        /// Called when visitor reaches a waypoint. Handles state-aware routing and detour logic.
        /// Derived classes can override to add custom detour behaviors.
        /// </summary>
        protected virtual void HandleDetourAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
            {
                return;
            }

            // Get current position
            if (path == null || currentPathIndex >= path.Count)
            {
                RecalculatePath();
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            // Check if current state wants to attempt a detour
            if (ShouldAttemptDetour(currentPos))
            {
                // Let derived class handle state-specific detour logic
                HandleStateSpecificDetour(currentPos);
            }
            else
            {
                // No detour - recalculate path to current state's destination
                RecalculatePath();
            }
        }

        /// <summary>
        /// Handles state-specific detour logic. Override in derived classes.
        /// Base implementation provides Lost state behavior.
        /// </summary>
        protected virtual void HandleStateSpecificDetour(Vector2Int currentPos)
        {
            // Base implementation for Lost state
            if (state == VisitorState.Lost && IsAtIntersection(currentPos))
            {
                // Get eligible neighbors (exclude incoming tile and planned forward waypoint)
                List<Vector2Int> neighbors = GetWalkableNeighbors(currentPos);

                // Exclude incoming tile
                if (currentPathIndex > 0 && currentPathIndex < path.Count)
                {
                    Vector2Int incomingTile = path[currentPathIndex - 1];
                    neighbors.Remove(incomingTile);
                }

                // Exclude planned forward waypoint
                if (currentPathIndex + 1 < path.Count)
                {
                    Vector2Int forwardWaypoint = path[currentPathIndex + 1];
                    neighbors.Remove(forwardWaypoint);
                }

                if (neighbors.Count == 0)
                {
                    // No valid detour directions - recalculate to destination
                    RecalculatePath();
                    return;
                }

                // Roll random detour length
                int detourLength = Random.Range(minLostDistance, maxLostDistance + 1);

                // Pick random neighbor to start detour
                Vector2Int detourStart = neighbors[Random.Range(0, neighbors.Count)];

                // Build exploratory segment
                List<Vector2Int> lostPath = BuildLostPath(currentPos, detourStart, detourLength, recentlyReachedTiles);

                if (lostPath == null || lostPath.Count == 0)
                {
                    // Couldn't build lost path - recalculate to destination
                    RecalculatePath();
                    return;
                }

                // Get destination for current state
                Vector2Int lostEnd = lostPath[lostPath.Count - 1];
                Vector2Int destination = GetDestinationForCurrentState(lostEnd);

                // Build recovery path from end of lost segment to destination
                if (!TryFindPathToDestination(lostEnd, destination, out List<Vector2Int> recoveryPath))
                {
                    // Couldn't find recovery path - recalculate to destination
                    RecalculatePath();
                    return;
                }

                // Merge lost path with recovery path
                List<Vector2Int> fullPath = new List<Vector2Int>();
                fullPath.AddRange(lostPath);

                // Skip first element of recovery path (duplicate of lostEnd)
                for (int i = 1; i < recoveryPath.Count; i++)
                {
                    fullPath.Add(recoveryPath[i]);
                }

                // Set the new path
                path = fullPath;
                currentPathIndex = 1; // Start at second position (first is currentPos)

                // Track the lost segment
                lostSegmentActive = true;
                lostSegmentEndIndex = lostPath.Count - 1;

                LogVisitorPath($"started lost detour: segment length {lostPath.Count}, total path {fullPath.Count}. Lost end: {lostEnd}, destination: {destination}.");
                return;
            }

            // Default fallback: recalculate to destination
            RecalculatePath();
        }

        /// <summary>
        /// Builds an exploratory path for Lost state wandering.
        /// Avoids recently visited tiles, current-segment repeats, and straight-line dead ends.
        /// </summary>
        /// <param name="startPos">Starting position</param>
        /// <param name="detourStart">First step of detour</param>
        /// <param name="stepsTarget">Target number of steps to take</param>
        /// <param name="recentTiles">Recently visited tiles to avoid</param>
        /// <returns>List of positions forming the lost path, or null if unable to build</returns>
        protected virtual List<Vector2Int> BuildLostPath(Vector2Int startPos, Vector2Int detourStart, int stepsTarget, Queue<Vector2Int> recentTiles)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return null;
            }

            List<Vector2Int> lostPath = new List<Vector2Int>();
            HashSet<Vector2Int> recentSet = recentTiles != null ? new HashSet<Vector2Int>(recentTiles) : new HashSet<Vector2Int>();
            HashSet<Vector2Int> currentSegmentVisited = new HashSet<Vector2Int>();

            Vector2Int current = detourStart;
            lostPath.Add(current);
            currentSegmentVisited.Add(current);

            const int MAX_ITERATIONS = 250;
            int iterations = 0;
            int stepsTaken = 1;

            while (stepsTaken < stepsTarget && iterations < MAX_ITERATIONS)
            {
                iterations++;

                // Get walkable neighbors
                List<Vector2Int> neighbors = GetWalkableNeighbors(current);

                // Exclude previous tile (no immediate backtracking)
                if (lostPath.Count > 1)
                {
                    Vector2Int previousTile = lostPath[lostPath.Count - 2];
                    neighbors.Remove(previousTile);
                }

                // Filter out recently visited tiles
                List<Vector2Int> validNeighbors = new List<Vector2Int>();
                foreach (var neighbor in neighbors)
                {
                    // Avoid recently reached tiles (from overall tracking)
                    if (recentSet.Contains(neighbor))
                        continue;

                    // Avoid tiles visited in current segment
                    if (currentSegmentVisited.Contains(neighbor))
                        continue;

                    // Check for straight-line dead ends
                    // A straight-line dead end is when the neighbor only has 1 walkable neighbor (excluding current)
                    List<Vector2Int> neighborNeighbors = GetWalkableNeighbors(neighbor);
                    neighborNeighbors.Remove(current); // Exclude current position
                    if (neighborNeighbors.Count == 0)
                    {
                        // This is a dead end - skip it
                        continue;
                    }

                    validNeighbors.Add(neighbor);
                }

                // If no valid neighbors, we're stuck - return what we have
                if (validNeighbors.Count == 0)
                {
                    break;
                }

                // Pick a random valid neighbor
                Vector2Int nextPos = validNeighbors[Random.Range(0, validNeighbors.Count)];
                lostPath.Add(nextPos);
                currentSegmentVisited.Add(nextPos);
                current = nextPos;
                stepsTaken++;
            }

            // Return path if we made at least some progress
            return lostPath.Count > 1 ? lostPath : null;
        }

        /// <summary>
        /// Resets detour-specific state when starting a new path or becoming fascinated.
        /// Derived classes should clear confusion flags, misstep tracking, etc.
        /// </summary>
        protected abstract void ResetDetourState();

        #endregion

        #region Confusion System (Shared)

        /// <summary>
        /// Gets all tiles that have been traversed so far (from spawn to current position).
        /// Used to prevent backtracking when building confusion paths.
        /// </summary>
        protected HashSet<Vector2Int> GetTraversedTiles()
        {
            HashSet<Vector2Int> traversed = new HashSet<Vector2Int>();

            if (path == null || path.Count == 0)
            {
                return traversed;
            }

            // Add all tiles from start up to and including current index
            for (int i = 0; i <= currentPathIndex && i < path.Count; i++)
            {
                traversed.Add(path[i]);
            }

            return traversed;
        }

        /// <summary>
        /// Checks if a position is walkable on the maze grid.
        /// </summary>
        protected bool IsWalkable(Vector2Int position)
        {
            var node = mazeGridBehaviour.Grid?.GetNode(position.x, position.y);
            return node != null && node.walkable;
        }

        /// <summary>
        /// Checks if a dead end is visible from startPos in the forward direction.
        /// Returns true if corridor continues straight ahead to a dead end without branches.
        /// </summary>
        protected bool IsDeadEndVisible(Vector2Int startPos, Vector2Int forwardDir)
        {
            if (forwardDir == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int previous = startPos;
            Vector2Int current = startPos + forwardDir;

            while (true)
            {
                var node = mazeGridBehaviour.Grid?.GetNode(current.x, current.y);
                if (node == null || !node.walkable)
                {
                    return false; // Wall encountered before a dead end
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(current);
                neighbors.Remove(previous);

                if (neighbors.Count == 0)
                {
                    return true; // Only way out is back where we came from
                }

                if (neighbors.Count > 1)
                {
                    return false; // Branch or corner blocks line of sight
                }

                Vector2Int nextForward = neighbors[0] - current;
                if (nextForward != forwardDir)
                {
                    return false; // Would require turning a corner
                }

                previous = current;
                current += forwardDir;
            }
        }

        /// <summary>
        /// Builds a confusion path following the detour direction for stepsTarget tiles.
        /// Avoids backtracking to already-traversed tiles and prevents loops.
        /// </summary>
        protected List<Vector2Int> BuildConfusionPath(Vector2Int currentPos, Vector2Int detourStart, int stepsTarget, HashSet<Vector2Int> traversedTiles)
        {
            List<Vector2Int> confusionPath = new List<Vector2Int>();

            Vector2Int previousPos = currentPos;
            Vector2Int nextPos = detourStart;
            Vector2Int forwardDir = detourStart - currentPos;

            int safetyLimit = 250;
            int iterations = 0;

            // Track tiles in the confusion path to avoid loops within the detour
            HashSet<Vector2Int> confusionPathSet = new HashSet<Vector2Int>();

            while (iterations < safetyLimit && confusionPath.Count < stepsTarget)
            {
                if (!IsWalkable(nextPos))
                {
                    break;
                }

                // Check if this tile was already traversed before confusion started
                if (traversedTiles.Contains(nextPos))
                {
                    break; // Would backtrack to an earlier position
                }

                // Check if we're creating a loop within this confusion path
                if (confusionPathSet.Contains(nextPos))
                {
                    break;
                }

                confusionPath.Add(nextPos);
                confusionPathSet.Add(nextPos);
                confusionStepsTaken = confusionPath.Count;

                if (IsDeadEndVisible(nextPos, forwardDir))
                {
                    break;
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(nextPos);
                neighbors.Remove(previousPos); // Don't go immediately backward

                // Remove any neighbors that would cause backtracking to already-traversed tiles
                neighbors.RemoveAll(n => traversedTiles.Contains(n));

                // Also avoid creating loops within the confusion path itself
                neighbors.RemoveAll(n => confusionPathSet.Contains(n));

                if (neighbors.Count == 0)
                {
                    break; // Cannot continue without backtracking
                }

                Vector2Int preferredForward = nextPos + forwardDir;
                Vector2Int chosenNext = neighbors.Contains(preferredForward) ? preferredForward : neighbors[Random.Range(0, neighbors.Count)];

                forwardDir = chosenNext - nextPos;
                previousPos = nextPos;
                nextPos = chosenNext;
                iterations++;
            }

            return confusionPath;
        }

        /// <summary>
        /// Begins a confusion segment at a decision point (intersection).
        /// Builds a detour path in the wrong direction, then plans recovery to destination.
        /// </summary>
        protected void BeginConfusionSegment(Vector2Int currentPos, Vector2Int detourStart)
        {
            // Use config-based detour lengths (or defaults from base class)
            int minDist = Mathf.RoundToInt(config != null ? config.LostDetourMin : minLostDistance);
            int maxDist = Mathf.RoundToInt(config != null ? config.LostDetourMax : maxLostDistance);
            int stepsTarget = Mathf.Clamp(Random.Range(minDist, maxDist + 1), minDist, maxDist);

            // Use recently reached tiles (last 10) to prevent short-term backtracking
            HashSet<Vector2Int> traversedTiles = new HashSet<Vector2Int>(recentlyReachedTiles ?? new Queue<Vector2Int>());

            List<Vector2Int> confusionPath = BuildConfusionPath(currentPos, detourStart, stepsTarget, traversedTiles);

            if (confusionPath.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Validate confusion path adjacency
            for (int i = 1; i < confusionPath.Count; i++)
            {
                int dist = Mathf.Abs(confusionPath[i].x - confusionPath[i - 1].x) + Mathf.Abs(confusionPath[i].y - confusionPath[i - 1].y);
                if (dist != 1)
                {
                    RecalculatePath();
                    return;
                }
            }

            Vector2Int confusionEnd = confusionPath[confusionPath.Count - 1];

            // Get recovery destination based on current state
            Vector2Int recoveryDestination = GetDestinationForCurrentState(confusionEnd);

            // Find path from confusion end to recovery destination
            if (!TryFindPathToDestination(confusionEnd, recoveryDestination, out List<Vector2Int> recoveryPath))
            {
                RecalculatePath();
                return;
            }

            // Validate recovery path adjacency
            for (int i = 1; i < recoveryPath.Count; i++)
            {
                int dist = Mathf.Abs(recoveryPath[i].x - recoveryPath[i - 1].x) + Mathf.Abs(recoveryPath[i].y - recoveryPath[i - 1].y);
                if (dist != 1)
                {
                    RecalculatePath();
                    return;
                }
            }

            // Build the combined path: current position + confusion detour + recovery path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);

            // Validate currentPos is adjacent to first confusion tile
            if (confusionPath.Count > 0)
            {
                int distToFirst = Mathf.Abs(currentPos.x - confusionPath[0].x) + Mathf.Abs(currentPos.y - confusionPath[0].y);
                if (distToFirst != 1)
                {
                    RecalculatePath();
                    return;
                }
            }

            // Add all confusion path tiles
            newPath.AddRange(confusionPath);

            // Validate connection between confusion end and recovery start
            if (recoveryPath.Count > 0 && recoveryPath[0] != confusionEnd)
            {
                RecalculatePath();
                return;
            }

            // Add recovery path tiles, skipping the first one since it duplicates confusionEnd
            for (int i = 1; i < recoveryPath.Count; i++)
            {
                newPath.Add(recoveryPath[i]);
            }

            // Final validation of complete path
            for (int i = 1; i < newPath.Count; i++)
            {
                int dist = Mathf.Abs(newPath[i].x - newPath[i - 1].x) + Mathf.Abs(newPath[i].y - newPath[i - 1].y);
                if (dist != 1)
                {
                    RecalculatePath();
                    return;
                }
            }

            path = newPath;
            currentPathIndex = 1; // Start at index 1 since index 0 is current position

            confusionSegmentActive = true;
            confusionSegmentEndIndex = confusionPath.Count;
            confusionStepsTarget = stepsTarget;
            confusionStepsTaken = 0;
            isConfused = true;

            RefreshStateFromFlags();
        }

        /// <summary>
        /// Decides whether visitor recovers from confusion.
        /// 50% chance to clear confusion flag.
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
            // Draw current path
            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = Color.cyan;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                    Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }

                // Draw current target
                if (IsMovementState(state) && currentPathIndex < path.Count)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 target = mazeGridBehaviour.GridToWorld(path[currentPathIndex].x, path[currentPathIndex].y);
                    Gizmos.DrawWireSphere(target, 0.3f);
                }
            }
        }

        #endregion
    }
}

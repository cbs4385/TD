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
    /// </summary>
    public abstract class VisitorControllerBase : MonoBehaviour
    {
        #region Enums

        public enum VisitorState
        {
            Idle,
            Walking,
            Fascinated,
            Confused,
            Frightened,
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
        [Tooltip("Color of the visitor sprite")]
        protected Color visitorColor = new Color(0.3f, 0.6f, 1f, 1f);

        [SerializeField]
        [Tooltip("Desired world-space diameter (in Unity units) for procedural visitors")]
        protected float visitorSize = 30.0f;

        [SerializeField]
        [Tooltip("Pixels per unit for procedural visitor sprites (match imported visitor assets)")]
        protected int proceduralPixelsPerUnit = 32;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        protected int sortingOrder = 15;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations")]
        protected bool useProceduralSprite = false;

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
        protected SpriteRenderer spriteRenderer;
        protected Rigidbody2D rb;
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
        protected bool isPathLoggingActive;

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

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            state = VisitorState.Idle;
            recentlyReachedTiles = new Queue<Vector2Int>();
            fascinatedPathNodes = new List<FascinatedPathNode>();
            lanternCooldowns = new Dictionary<FaeMaze.Props.FaeLantern, float>();
            initialScale = transform.localScale;
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            CacheAuthoredSpriteSize();
            SetupSpriteRenderer();
            SetupPhysics();
            SetAnimatorDirection(IdleDirection);

            stalledDuration = 0f;
            isPathLoggingActive = false;
        }

        protected virtual void Update()
        {
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
            return isPathLoggingActive;
        }

        private void LogVisitorPath(string message)
        {
            if (ShouldLogVisitorPath())
            {
                Debug.Log($"[VisitorPath] {name} {message}", this);
            }
        }

        private bool LogVisitorPathWarning(string message)
        {
            if (ShouldLogVisitorPath())
            {
                Debug.LogWarning($"[VisitorPath] {name} {message}", this);
                return true;
            }

            return false;
        }

        private void UpdatePathLoggingOnMovement(Vector3 previousPosition, Vector3 currentPosition)
        {
            float deltaSqr = (currentPosition - previousPosition).sqrMagnitude;

            if (deltaSqr <= MovementEpsilonSqr)
            {
                stalledDuration += Time.deltaTime;

                if (!isPathLoggingActive && stalledDuration >= StallLoggingDelaySeconds)
                {
                    isPathLoggingActive = true;

                    Vector2Int stalledGrid = Vector2Int.zero;
                    int stalledX = 0;
                    int stalledY = 0;
                    bool resolvedGrid = mazeGridBehaviour != null && mazeGridBehaviour.WorldToGrid(currentPosition, out stalledX, out stalledY);
                    if (resolvedGrid)
                    {
                        stalledGrid = new Vector2Int(stalledX, stalledY);
                    }

                    LogVisitorPath($"stalled for {stalledDuration:F2}s at grid {(resolvedGrid ? stalledGrid.ToString() : "<unknown>")}. Path length: {path?.Count ?? 0}. Path: {FormatPath(path)}.");
                }
            }
            else
            {
                if (isPathLoggingActive)
                {
                    LogVisitorPath($"resumed movement after stalling for {stalledDuration:F2}s.");
                }

                stalledDuration = 0f;
                isPathLoggingActive = false;
            }
        }

        protected bool IsMovementState(VisitorState visitorState)
        {
            return visitorState == VisitorState.Walking
                || visitorState == VisitorState.Fascinated
                || visitorState == VisitorState.Confused
                || visitorState == VisitorState.Frightened;
        }

        protected virtual void RefreshStateFromFlags()
        {
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            if (isFascinated)
            {
                state = VisitorState.Fascinated;
            }
            else if (state == VisitorState.Frightened || state == VisitorState.Confused)
            {
                return;
            }
            else
            {
                state = VisitorState.Walking;
            }
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
            isPathLoggingActive = false;

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
            isPathLoggingActive = false;

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
            SetAnimatorDirection(GetDirectionFromMovement(movement));
        }

        protected void SetAnimatorDirection(int direction)
        {
            // Guard against redundant animator parameter writes
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(DirectionParameter, direction);
                currentAnimatorDirection = direction;
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

            // Move toward target (apply speed multiplier adjusted by tile cost)
            float moveCost = 1f;
            MazeGrid mazeGrid = mazeGridBehaviour.Grid;
            if (mazeGrid != null)
            {
                MazeGrid.MazeNode targetNode = mazeGrid.GetNode(targetGridPos.x, targetGridPos.y);
                if (targetNode == null || !targetNode.walkable)
                {
                    if (!hasLoggedPathIssue)
                    {
                        string reason = targetNode == null ? "missing" : "not walkable";
                        if (LogVisitorPathWarning($"cannot move to waypoint {targetGridPos} at index {currentPathIndex} because node is {reason}. Path length: {path.Count}."))
                        {
                            hasLoggedPathIssue = true;
                        }
                    }

                    UpdatePathLoggingOnMovement(previousPosition, transform.position);
                    return; // Non-walkable nodes remain blocked
                }

                moveCost = mazeGrid.GetMoveCost(targetGridPos.x, targetGridPos.y);
            }

            moveCost = Mathf.Max(moveCost, Mathf.Epsilon); // Prevent division by zero
            float effectiveSpeed = (moveSpeed * speedMultiplier) / moveCost;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            Vector3 movementDelta = newPosition - transform.position;
            UpdateAnimatorDirection(movementDelta);

            // Use Rigidbody2D.MovePosition for proper trigger detection
            if (rb != null)
            {
                rb.MovePosition(newPosition);
                // Manually sync transforms with physics system to ensure trigger detection works
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
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
        /// Derived classes override to implement specific consumption behavior.
        /// </summary>
        protected virtual void HandleConsumption()
        {
            // Default: just destroy the visitor
            Destroy(gameObject);
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
        /// </summary>
        protected virtual void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern, Vector2Int visitorGridPosition)
        {
            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && fascinationLanternPosition == lantern.GridPosition)
                return;

            // Check cooldown (prevents immediate re-triggering)
            if (lanternCooldowns.ContainsKey(lantern) && lanternCooldowns[lantern] > 0f)
            {
                return;
            }

            // Check proc chance (probability of fascination)
            float roll = Random.value;
            if (roll > lantern.ProcChance)
            {
                // Set cooldown even on failed proc to prevent spam checks
                lanternCooldowns[lantern] = lantern.CooldownSec;
                return;
            }

            // Allow re-fascination by a different lantern
            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lantern.GridPosition;
            hasReachedLantern = false;
            fascinationTimer = 0f; // Will be set when reaching lantern

            // Set cooldown for this lantern
            lanternCooldowns[lantern] = lantern.CooldownSec;

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
                if (gameController.TryFindPath(currentPos, fascinationLanternPosition, pathToLantern) && pathToLantern.Count > 0)
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
        /// Recalculates the path to the original destination.
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
                LogVisitorPathWarning($"could not resolve current grid while recalculating path to {originalDestination}.");
                isCalculatingPath = false;
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);
            List<MazeGrid.MazeNode> newPathNodes = new List<MazeGrid.MazeNode>();

            LogVisitorPath($"recalculating path from {currentPos} to {originalDestination}.");

            if (!gameController.TryFindPath(currentPos, originalDestination, newPathNodes) || newPathNodes.Count == 0)
            {
                if (!hasLoggedPathIssue)
                {
                    if (LogVisitorPathWarning($"could not find path from {currentPos} to destination {originalDestination}."))
                    {
                        hasLoggedPathIssue = true;
                    }
                }
                isCalculatingPath = false;
                return;
            }

            List<Vector2Int> newPath = new List<Vector2Int>();
            foreach (var node in newPathNodes)
            {
                newPath.Add(new Vector2Int(node.x, node.y));
            }

            LogVisitorPath($"recalculated path length {newPath.Count}. Path: {FormatPath(newPath)}.");

            path = newPath;
            recentlyReachedTiles.Clear();
            if (path.Count > 0)
            {
                recentlyReachedTiles.Enqueue(path[0]);
            }

            currentPathIndex = path.Count > 1 ? 1 : 0;
            hasLoggedPathIssue = false;
            lastLoggedWaypointIndex = -1;

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
                if (gameController.TryFindPath(currentPos, lanternGridPosition, pathToLantern) && pathToLantern.Count > 0)
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
            spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
                gameObject,
                createProceduralSprite: useProceduralSprite,
                useSoftEdges: false,
                resolution: 32,
                pixelsPerUnit: proceduralPixelsPerUnit
            );

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
            // Add Rigidbody2D for trigger collisions
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // Add CircleCollider2D for trigger detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.radius = 0.3f;
            collider.isTrigger = true;
        }

        #endregion

        #region Abstract Methods - Detour Behavior Hooks

        /// <summary>
        /// Called when visitor reaches a waypoint. Derived classes implement specific detour logic.
        /// This is where confusion, missteps, or other detour behaviors are triggered.
        /// Default behavior is to recalculate the path.
        /// </summary>
        protected abstract void HandleDetourAtWaypoint();

        /// <summary>
        /// Resets detour-specific state when starting a new path or becoming fascinated.
        /// Derived classes should clear confusion flags, misstep tracking, etc.
        /// </summary>
        protected abstract void ResetDetourState();

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

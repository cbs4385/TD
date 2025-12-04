using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// A visitor variant that takes intentional missteps at branching points.
    /// At any tile with multiple unwalked adjacent tiles, there's a 20% chance
    /// the visitor will choose an incorrect direction. Once on a mistaken path,
    /// they continue until reaching another branching point, where they recalculate
    /// an A* path to the destination and apply the misstep chance again.
    ///
    /// Functionally identical to VisitorController except for the misstep behavior.
    /// All other behaviors (animations, speed, fascination states, etc.) are preserved.
    /// </summary>
    public class MistakingVisitorController : MonoBehaviour
    {
        #region Enums

        public enum VisitorState
        {
            Idle,
            Walking,
            Consumed,
            Escaping
        }

        /// <summary>
        /// Represents a visited tile in the fascinated random walk with its unexplored neighbors.
        /// </summary>
        private class FascinatedPathNode
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
        private float moveSpeed = 3f;

        [Header("Path Following")]
        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [Header("Misstep Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) for visitor to take a wrong turn at branches")]
        [Range(0f, 1f)]
        private float misstepChance = 0.2f;

        [SerializeField]
        [Tooltip("Enable misstep behavior")]
        private bool misstepEnabled = true;

        [SerializeField]
        [Tooltip("Draw misstep paths in scene view for debugging")]
        private bool debugMisstepGizmos;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the visitor sprite")]
        private Color visitorColor = new Color(0.3f, 0.6f, 1f, 1f);

        [SerializeField]
        [Tooltip("Desired world-space diameter (in Unity units) for procedural visitors")]
        private float visitorSize = 30.0f;

        [SerializeField]
        [Tooltip("Pixels per unit for procedural visitor sprites")]
        private int proceduralPixelsPerUnit = 32;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations")]
        private bool useProceduralSprite = false;

        #endregion

        #region Private Fields

        private List<Vector2Int> path;
        private int currentPathIndex;
        private VisitorState state;
        private Animator animator;
        private GameController gameController;
        private MazeGridBehaviour mazeGridBehaviour;
        private bool isEntranced;
        private float speedMultiplier = 1f;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Vector2 authoredSpriteWorldSize;
        private Vector2Int originalDestination;

        private bool isCalculatingPath;

        // Fascination state (for FaeLantern)
        private bool isFascinated;
        private Vector2Int fascinationLanternPosition;
        private bool hasReachedLantern;
        private float fascinationTimer;
        private FaeMaze.Props.FaeLantern currentFaeLantern;
        private Dictionary<FaeMaze.Props.FaeLantern, float> lanternCooldowns;
        private List<FascinatedPathNode> fascinatedPathNodes;

        private Vector3 initialScale;
        private Queue<Vector2Int> recentlyReachedTiles;
        private const int MAX_RECENT_TILES = 10;

        private const string DirectionParameter = "Direction";
        private const int IdleDirection = 0;
        private const float MovementEpsilonSqr = 0.0001f;

        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        // Misstep tracking
        private bool isOnMisstepPath;
        private HashSet<Vector2Int> walkedTiles;
        private int misstepSegmentStartIndex;
        private int waypointsTraversedSinceSpawn;

        #endregion

        #region Properties

        public VisitorState State => state;
        public float MoveSpeed => moveSpeed;
        public bool IsEntranced => isEntranced;
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }
        public bool IsFascinated => isFascinated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = VisitorState.Idle;
            recentlyReachedTiles = new Queue<Vector2Int>();
            fascinatedPathNodes = new List<FascinatedPathNode>();
            lanternCooldowns = new Dictionary<FaeMaze.Props.FaeLantern, float>();
            walkedTiles = new HashSet<Vector2Int>();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;

            initialScale = transform.localScale;
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            CacheAuthoredSpriteSize();
            SetupSpriteRenderer();
            SetupPhysics();
            SetAnimatorDirection(IdleDirection);
        }

        private void Update()
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

            // Check for FaeLantern influence
            if (state == VisitorState.Walking)
            {
                bool pausedAtLantern = isFascinated && hasReachedLantern && fascinationTimer > 0;
                if (!pausedAtLantern)
                {
                    CheckFaeLanternInfluence();
                }
            }

            // Handle fascination timer
            if (isFascinated && hasReachedLantern && fascinationTimer > 0)
            {
                fascinationTimer -= Time.deltaTime;
                SetAnimatorDirection(IdleDirection);
                return;
            }

            if (state == VisitorState.Walking)
            {
                if (!isCalculatingPath)
                {
                    UpdateWalking();
                }
            }
        }

        #endregion

        #region Initialization

        public void Initialize(GameController controller)
        {
            gameController = controller;

            if (gameController != null && gameController.MazeGrid != null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }
        }

        public void Initialize()
        {
            Initialize(GameController.Instance);
        }

        #endregion

        #region Path Management

        public void SetPath(List<Vector2Int> gridPath)
        {
            if (gridPath == null || gridPath.Count == 0)
            {
                return;
            }

            originalDestination = gridPath[gridPath.Count - 1];
            waypointsTraversedSinceSpawn = 0;
            walkedTiles.Clear();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;

            RecalculatePath();
        }

        public void SetPath(List<MazeGrid.MazeNode> nodePath)
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

            waypointsTraversedSinceSpawn = 0;
            walkedTiles.Clear();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;

            RecalculatePath();
        }

        #endregion

        #region Movement

        private void UpdateWalking()
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

            if (currentPathIndex >= path.Count)
            {
                currentPathIndex = path.Count - 1;

                if (isFascinated && hasReachedLantern)
                {
                    isFascinated = false;
                    hasReachedLantern = false;
                    ClearLanternInteraction();
                    fascinatedPathNodes.Clear();
                }
            }

            Vector2Int targetGridPos = path[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            float moveCost = 1f;
            MazeGrid mazeGrid = mazeGridBehaviour.Grid;
            if (mazeGrid != null)
            {
                MazeGrid.MazeNode targetNode = mazeGrid.GetNode(targetGridPos.x, targetGridPos.y);
                if (targetNode == null || !targetNode.walkable)
                {
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

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
            if (distanceToTarget < waypointReachedDistance)
            {
                OnWaypointReached();
            }
        }

        /// <summary>
        /// Called when visitor reaches a waypoint.
        /// Implements misstep logic at branching points.
        /// </summary>
        private void OnWaypointReached()
        {
            Vector2Int currentWaypoint = path[currentPathIndex];

            // Add to recently reached tiles queue
            if (recentlyReachedTiles != null)
            {
                recentlyReachedTiles.Enqueue(currentWaypoint);
                while (recentlyReachedTiles.Count > MAX_RECENT_TILES)
                {
                    recentlyReachedTiles.Dequeue();
                }
            }

            // Track as walked for misstep system
            walkedTiles.Add(currentWaypoint);
            waypointsTraversedSinceSpawn++;

            // Handle fascinated visitors (preserve base behavior)
            if (isFascinated && !hasReachedLantern && currentPathIndex < path.Count)
            {
                if (currentWaypoint == fascinationLanternPosition)
                {
                    hasReachedLantern = true;
                    if (currentFaeLantern != null)
                    {
                        fascinationTimer = currentFaeLantern.FascinationDuration;
                    }
                    else
                    {
                        fascinationTimer = 2f;
                    }
                    SetAnimatorDirection(IdleDirection);
                    return;
                }
                else
                {
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        OnPathCompleted();
                    }
                    return;
                }
            }

            // Handle fascinated random walk
            if (isFascinated && hasReachedLantern && fascinationTimer <= 0)
            {
                if (currentWaypoint == originalDestination)
                {
                    OnPathCompleted();
                    return;
                }

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
                return;
            }

            // Check if reached destination
            if (currentWaypoint == originalDestination)
            {
                OnPathCompleted();
                return;
            }

            // Check if reached any exit
            if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                if (mazeGridBehaviour.IsSpawnPoint(currentWaypoint))
                {
                    OnPathCompleted();
                    return;
                }
            }

            // Apply misstep logic at branching points
            HandleMisstepAtWaypoint();
        }

        private void OnPathCompleted()
        {
            isFascinated = false;
            hasReachedLantern = false;
            ClearLanternInteraction();

            bool isUsingSpawnMarkers = mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2;

            if (isUsingSpawnMarkers)
            {
                state = VisitorState.Escaping;
                SetAnimatorDirection(IdleDirection);

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
                state = VisitorState.Consumed;
                SetAnimatorDirection(IdleDirection);

                // Track consumption stats
                if (FaeMaze.Systems.GameStatsTracker.Instance != null)
                {
                    FaeMaze.Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
                }

                // Add essence to game controller
                if (gameController != null && gameController.Heart != null)
                {
                    gameController.AddEssence(gameController.Heart.EssencePerVisitor);
                }

                // Play consumption sound
                FaeMaze.Audio.SoundManager.Instance?.PlayVisitorConsumed();

                // Destroy the visitor
                Destroy(gameObject);
            }
        }

        private void UpdateAnimatorDirection(Vector2 movement)
        {
            SetAnimatorDirection(GetDirectionFromMovement(movement));
        }

        private void SetAnimatorDirection(int direction)
        {
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(DirectionParameter, direction);
                currentAnimatorDirection = direction;
            }
        }

        private int GetDirectionFromMovement(Vector2 movement)
        {
            float movementThreshold = moveSpeed * Time.deltaTime * 0.1f;
            float movementThresholdSqr = movementThreshold * movementThreshold;

            if (movement.sqrMagnitude <= movementThresholdSqr)
            {
                if (state != VisitorState.Walking)
                {
                    return IdleDirection;
                }
                return lastDirection;
            }

            float absX = Mathf.Abs(movement.x);
            float absY = Mathf.Abs(movement.y);

            float axisDifference = Mathf.Abs(absX - absY);
            float axisMin = Mathf.Min(absX, absY);

            if (axisDifference < axisMin * 0.2f && lastDirection != IdleDirection)
            {
                return lastDirection;
            }

            int newDirection;
            if (absY >= absX)
            {
                newDirection = movement.y > 0f ? 1 : 2;
            }
            else
            {
                newDirection = movement.x < 0f ? 3 : 4;
            }

            if (newDirection != IdleDirection)
            {
                lastDirection = newDirection;
            }

            return newDirection;
        }

        #endregion

        #region Misstep System

        /// <summary>
        /// Handles misstep decision at waypoint.
        /// Detects branching points (tiles with 2+ unwalked neighbors) and rolls for misstep.
        /// If on misstep path and reached branch, recalculates A* to destination.
        /// </summary>
        private void HandleMisstepAtWaypoint()
        {
            if (!misstepEnabled || mazeGridBehaviour == null || gameController == null)
            {
                RecalculatePath();
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            // Get all unwalked adjacent tiles
            List<Vector2Int> unwalkedNeighbors = GetUnwalkedNeighbors(currentPos);

            // Not a branch if fewer than 2 unwalked neighbors
            if (unwalkedNeighbors.Count < 2)
            {
                // If on misstep, continue until branch
                if (isOnMisstepPath)
                {
                    // Just advance to next waypoint
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        // Reached end of misstep path unexpectedly - recalculate
                        isOnMisstepPath = false;
                        RecalculatePath();
                    }
                    return;
                }

                // Not on misstep and not a branch - normal recalculate
                RecalculatePath();
                return;
            }

            // This is a branching point with 2+ unwalked neighbors
            if (isOnMisstepPath)
            {
                // End misstep - recalculate A* to destination
                isOnMisstepPath = false;
                RecalculatePath();
                // Don't return - check for new misstep below
            }

            // After recalculating, recheck unwalked neighbors for new misstep decision
            unwalkedNeighbors = GetUnwalkedNeighbors(currentPos);
            if (unwalkedNeighbors.Count < 2)
            {
                return; // No longer a branch after recalc
            }

            // Roll for misstep
            float roll = Random.value;
            if (roll > misstepChance)
            {
                // No misstep - path already recalculated above or will be recalculated
                if (!isOnMisstepPath)
                {
                    RecalculatePath();
                }
                return;
            }

            // Misstep triggered!
            TakeMisstep(currentPos, unwalkedNeighbors);
        }

        /// <summary>
        /// Gets all walkable neighbor tiles that have not been walked.
        /// Used to identify branching points.
        /// </summary>
        private List<Vector2Int> GetUnwalkedNeighbors(Vector2Int gridPos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

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

                if (node != null && node.walkable && !walkedTiles.Contains(neighborPos))
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Executes a misstep by choosing wrong direction at branch.
        /// Selects randomly among unwalked neighbors, biased against immediate backtracking.
        /// Builds path following chosen direction until next branch.
        /// </summary>
        private void TakeMisstep(Vector2Int currentPos, List<Vector2Int> unwalkedNeighbors)
        {
            if (unwalkedNeighbors.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Determine optimal next step from A*
            Vector2Int? optimalNext = GetOptimalNextStep(currentPos);

            // Filter out optimal direction to get "wrong" choices
            List<Vector2Int> wrongChoices = new List<Vector2Int>(unwalkedNeighbors);
            if (optimalNext.HasValue)
            {
                wrongChoices.Remove(optimalNext.Value);
            }

            if (wrongChoices.Count == 0)
            {
                wrongChoices = new List<Vector2Int>(unwalkedNeighbors);
            }

            // Bias against backtracking to immediately previous tile
            Vector2Int? previousTile = null;
            if (currentPathIndex > 0 && currentPathIndex < path.Count)
            {
                previousTile = path[currentPathIndex - 1];
            }

            if (previousTile.HasValue && wrongChoices.Count > 1)
            {
                wrongChoices.Remove(previousTile.Value);
            }

            // Choose random wrong direction
            Vector2Int chosenWrongStep = wrongChoices[Random.Range(0, wrongChoices.Count)];

            // Build path following this wrong direction
            List<Vector2Int> misstepPath = BuildMisstepPath(currentPos, chosenWrongStep);

            if (misstepPath.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Set the misstep path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);
            newPath.AddRange(misstepPath);

            path = newPath;
            currentPathIndex = 0;
            isOnMisstepPath = true;
            misstepSegmentStartIndex = 1;

            state = VisitorState.Walking;
        }

        /// <summary>
        /// Gets the optimal next step from current position using A*.
        /// Returns null if path cannot be calculated.
        /// </summary>
        private Vector2Int? GetOptimalNextStep(Vector2Int currentPos)
        {
            List<MazeGrid.MazeNode> optimalPath = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, originalDestination, optimalPath) && optimalPath.Count > 1)
            {
                return new Vector2Int(optimalPath[1].x, optimalPath[1].y);
            }
            return null;
        }

        /// <summary>
        /// Builds path following mistaken direction until reaching next branch.
        /// Continues chosen direction, avoiding backtracking and loops.
        /// Stops at tile with 2+ unwalked neighbors (branching point).
        /// </summary>
        private List<Vector2Int> BuildMisstepPath(Vector2Int startPos, Vector2Int firstStep)
        {
            List<Vector2Int> misstepPath = new List<Vector2Int>();
            HashSet<Vector2Int> misstepPathSet = new HashSet<Vector2Int>();

            Vector2Int previousPos = startPos;
            Vector2Int currentPos = firstStep;
            Vector2Int currentDirection = firstStep - startPos;

            int safetyLimit = 200;
            int iterations = 0;

            while (iterations < safetyLimit)
            {
                iterations++;

                // Validate walkable
                var node = mazeGridBehaviour.Grid?.GetNode(currentPos.x, currentPos.y);
                if (node == null || !node.walkable)
                {
                    break;
                }

                // Check if backtracking to walked tile
                if (walkedTiles.Contains(currentPos))
                {
                    misstepPath.Add(currentPos);
                    break;
                }

                // Check for loop in misstep path
                if (misstepPathSet.Contains(currentPos))
                {
                    break;
                }

                // Add to misstep path
                misstepPath.Add(currentPos);
                misstepPathSet.Add(currentPos);

                // Check if branching point
                List<Vector2Int> unwalkedNeighbors = GetUnwalkedNeighborsExcluding(currentPos, misstepPathSet);
                unwalkedNeighbors.Remove(previousPos);

                if (unwalkedNeighbors.Count >= 2)
                {
                    // Reached branch - stop
                    break;
                }

                if (unwalkedNeighbors.Count == 0)
                {
                    // Dead end
                    break;
                }

                // Continue in same direction if possible
                Vector2Int preferredNext = currentPos + currentDirection;
                Vector2Int nextPos;

                if (unwalkedNeighbors.Contains(preferredNext))
                {
                    nextPos = preferredNext;
                }
                else
                {
                    nextPos = unwalkedNeighbors[0];
                    currentDirection = nextPos - currentPos;
                }

                previousPos = currentPos;
                currentPos = nextPos;
            }

            return misstepPath;
        }

        /// <summary>
        /// Gets unwalked neighbors excluding tiles in provided set.
        /// Used during misstep path building to avoid loops.
        /// </summary>
        private List<Vector2Int> GetUnwalkedNeighborsExcluding(Vector2Int gridPos, HashSet<Vector2Int> excludeSet)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = gridPos + dir;
                var node = mazeGridBehaviour.Grid.GetNode(neighborPos.x, neighborPos.y);

                if (node != null && node.walkable &&
                    !walkedTiles.Contains(neighborPos) &&
                    !excludeSet.Contains(neighborPos))
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
        }

        private List<Vector2Int> GetWalkableNeighbors(Vector2Int gridPos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
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

        #region FaeLantern Detection

        private void ClearLanternInteraction()
        {
            if (currentFaeLantern != null)
            {
                currentFaeLantern.SetIdleDirection();
            }

            currentFaeLantern = null;
            fascinationLanternPosition = Vector2Int.zero;
        }

        private void CheckFaeLanternInfluence()
        {
            if (mazeGridBehaviour == null)
                return;

            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
                return;

            Vector2Int currentGridPos = new Vector2Int(x, y);

            foreach (var lantern in FaeMaze.Props.FaeLantern.All)
            {
                if (lantern == null)
                    continue;

                if (lantern.IsCellInInfluence(currentGridPos))
                {
                    EnterFaeInfluence(lantern, currentGridPos);
                    break;
                }
            }
        }

        private void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern, Vector2Int visitorGridPosition)
        {
            if (isFascinated && currentFaeLantern == lantern && fascinationLanternPosition == lantern.GridPosition)
                return;

            if (lanternCooldowns.ContainsKey(lantern) && lanternCooldowns[lantern] > 0f)
            {
                return;
            }

            float roll = Random.value;
            if (roll > lantern.ProcChance)
            {
                lanternCooldowns[lantern] = lantern.CooldownSec;
                return;
            }

            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lantern.GridPosition;
            hasReachedLantern = false;
            fascinationTimer = 0f;

            lanternCooldowns[lantern] = lantern.CooldownSec;
            fascinatedPathNodes.Clear();

            path = null;
            currentPathIndex = 0;
            isOnMisstepPath = false;

            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                if (gameController.TryFindPath(currentPos, fascinationLanternPosition, pathToLantern) && pathToLantern.Count > 0)
                {
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (path[i] == currentPos)
                            {
                                currentPathIndex = i;
                                break;
                            }
                        }

                        Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                        float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                        if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                        {
                            currentPathIndex++;
                        }
                    }

                    state = VisitorState.Walking;
                }
                else
                {
                    isFascinated = false;
                    hasReachedLantern = false;
                    ClearLanternInteraction();
                }
            }
        }

        #endregion

        #region Fascinated Random Walk

        private void HandleFascinatedRandomWalk()
        {
            int waypointsRemaining = path.Count - currentPathIndex;
            if (waypointsRemaining > 3)
            {
                return;
            }

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

            if (fascinatedPathNodes.Count == 0)
            {
                List<Vector2Int> neighbors = GetWalkableNeighbors(currentPos);

                if (currentPathIndex > 1)
                {
                    Vector2Int previousTile = path[currentPathIndex - 2];
                    neighbors.Remove(previousTile);
                }

                ShuffleList(neighbors);

                FascinatedPathNode initialNode = new FascinatedPathNode(currentPos, neighbors);
                fascinatedPathNodes.Add(initialNode);
            }

            FascinatedPathNode currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];

            while (!currentNode.HasUnexploredNeighbors && fascinatedPathNodes.Count > 0)
            {
                fascinatedPathNodes.RemoveAt(fascinatedPathNodes.Count - 1);

                if (fascinatedPathNodes.Count == 0)
                {
                    OnPathCompleted();
                    return;
                }

                currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];
                Vector2Int backtrackPos = currentNode.Position;

                if (backtrackPos != currentPos)
                {
                    path.Add(backtrackPos);
                    currentPos = backtrackPos;
                }
            }

            if (fascinatedPathNodes.Count == 0)
            {
                return;
            }

            Vector2Int nextPos = currentNode.PopNextNeighbor();
            List<Vector2Int> nextNeighbors = GetWalkableNeighbors(nextPos);

            HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
            foreach (var node in fascinatedPathNodes)
            {
                visitedPositions.Add(node.Position);
            }

            nextNeighbors.RemoveAll(n => visitedPositions.Contains(n));
            ShuffleList(nextNeighbors);

            FascinatedPathNode nextNode = new FascinatedPathNode(nextPos, nextNeighbors);
            fascinatedPathNodes.Add(nextNode);

            path.Add(nextPos);
        }

        private void ShuffleList<T>(List<T> list)
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

        #region Public Methods

        public void Stop()
        {
            state = VisitorState.Idle;
            SetAnimatorDirection(IdleDirection);
        }

        public void Resume()
        {
            if (path != null && path.Count > 0 && currentPathIndex < path.Count)
            {
                state = VisitorState.Walking;
            }
        }

        /// <summary>
        /// Recalculates A* path to original destination from current position.
        /// Used when visitor needs to find new route (after misstep or prop placement).
        /// </summary>
        public void RecalculatePath()
        {
            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            if (isFascinated)
            {
                return;
            }

            isCalculatingPath = true;

            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                isCalculatingPath = false;
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);
            List<MazeGrid.MazeNode> newPathNodes = new List<MazeGrid.MazeNode>();

            if (!gameController.TryFindPath(currentPos, originalDestination, newPathNodes) || newPathNodes.Count == 0)
            {
                isCalculatingPath = false;
                return;
            }

            List<Vector2Int> newPath = new List<Vector2Int>();
            foreach (var node in newPathNodes)
            {
                newPath.Add(new Vector2Int(node.x, node.y));
            }

            path = newPath;
            recentlyReachedTiles.Clear();
            if (path.Count > 0)
            {
                recentlyReachedTiles.Enqueue(path[0]);
            }

            currentPathIndex = path.Count > 1 ? 1 : 0;

            if (path.Count <= 1)
            {
                isCalculatingPath = false;
                OnPathCompleted();
                return;
            }

            if (state != VisitorState.Walking)
            {
                state = VisitorState.Walking;
            }
            isCalculatingPath = false;
        }

        public void SetEntranced(bool value)
        {
            if (isEntranced != value)
            {
                isEntranced = value;
            }
        }

        public void ForceEscape()
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

        public void BecomeFascinated(Vector2Int lanternGridPosition)
        {
            if (state != VisitorState.Walking)
            {
                return;
            }

            isFascinated = true;
            fascinationLanternPosition = lanternGridPosition;
            hasReachedLantern = false;

            path = null;
            currentPathIndex = 0;
            isOnMisstepPath = false;
            waypointsTraversedSinceSpawn = 0;

            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                if (gameController.TryFindPath(currentPos, lanternGridPosition, pathToLantern) && pathToLantern.Count > 0)
                {
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
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

                            Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                            float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                            if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                            {
                                currentPathIndex++;
                            }
                        }
                        else
                        {
                            Vector3 currentWorldPos = transform.position;
                            float closestDist = float.MaxValue;

                            for (int i = 0; i < path.Count; i++)
                            {
                                Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                                float dist = Vector3.Distance(currentWorldPos, waypointWorldPos);

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
                    }
                }
            }
        }

        #endregion

        #region Visual Setup

        private void CreateVisualSprite()
        {
            spriteRenderer.sprite = CreateCircleSprite(32);
            ApplySpriteSettings();
        }

        private void SetupSpriteRenderer()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (useProceduralSprite)
            {
                CreateVisualSprite();
            }
            else
            {
                ApplySpriteSettings();
            }
        }

        private void ApplySpriteSettings()
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

        private void CacheAuthoredSpriteSize()
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

        private void SetupPhysics()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.radius = 0.3f;
            collider.isTrigger = true;
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                proceduralPixelsPerUnit
            );
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = Color.cyan;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                    Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }

                // Draw misstep segment in red
                if (debugMisstepGizmos && isOnMisstepPath && misstepSegmentStartIndex >= 0)
                {
                    Gizmos.color = Color.red;

                    for (int i = misstepSegmentStartIndex; i < path.Count - 1; i++)
                    {
                        Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                        Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                        Gizmos.DrawLine(start, end);
                    }
                }

                // Draw current target
                if (state == VisitorState.Walking && currentPathIndex < path.Count)
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

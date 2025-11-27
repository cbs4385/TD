using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Controls a visitor's movement through the maze.
    /// Visitors follow a path of grid nodes.
    /// When using spawn markers: visitors escape at the destination (no essence).
    /// When using legacy heart: visitors are consumed at the heart (awards essence).
    /// </summary>
    public class VisitorController : MonoBehaviour
    {
        #region Enums

        public enum VisitorState
        {
            Idle,
            Walking,
            Consumed,
            Escaping
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

        [SerializeField]
        [Tooltip("How often to recalculate path (in seconds) to react to new attractors")]
        private float pathRecalculationInterval = 2.0f;

        [Header("Confusion Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) for visitor to get confused at intersections")]
        [Range(0f, 1f)]
        private float confusionChance = 0.25f;

        [SerializeField]
        [Tooltip("Whether confusion is enabled")]
        private bool confusionEnabled = true;

        [SerializeField]
        [Tooltip("Minimum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int minConfusionDistance = 15;

        [SerializeField]
        [Tooltip("Maximum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int maxConfusionDistance = 20;

        [SerializeField]
        [Tooltip("Draw confusion segments in the scene view for debugging")]
        private bool debugConfusionGizmos;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the visitor sprite")]
        private Color visitorColor = new Color(0.3f, 0.6f, 1f, 1f); // Light blue

        [SerializeField]
        [Tooltip("Size of the visitor sprite")]
        private float visitorSize = 0.6f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        #endregion

        #region Private Fields

        private List<Vector2Int> path;
        private int currentPathIndex;
        private VisitorState state;
        private GameController gameController;
        private MazeGridBehaviour mazeGridBehaviour;
        private bool isEntranced;
        private float speedMultiplier = 1f;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Vector2Int originalDestination; // Store original destination for confusion recovery

        private bool isConfused;
        private bool confusionSegmentActive;
        private int confusionSegmentEndIndex;
        private int confusionStepsTarget;
        private int confusionStepsTaken;

        private float timeSinceLastRecalculation;

        // Fascination state (for FaeLantern)
        private bool isFascinated;
        private Vector2Int fascinationLanternPosition;
        private bool hasReachedLantern;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public VisitorState State => state;

        /// <summary>Gets the current move speed</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>Gets whether this visitor is entranced by a Fairy Ring</summary>
        public bool IsEntranced => isEntranced;

        /// <summary>Gets or sets the speed multiplier applied to movement</summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }

        /// <summary>Gets whether this visitor is fascinated by a FaeLantern</summary>
        public bool IsFascinated => isFascinated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = VisitorState.Idle;
            isConfused = confusionEnabled;
            CreateVisualSprite();
        }

        private void CreateVisualSprite()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a simple circle sprite for the visitor
            spriteRenderer.sprite = CreateCircleSprite(32);
            spriteRenderer.color = visitorColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(visitorSize, visitorSize, 1f);

            // Add Rigidbody2D for trigger collisions with MazeAttractors
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic so we control movement manually
                rb.gravityScale = 0f; // No gravity for 2D top-down
            }

            // Add CircleCollider2D for trigger detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.3f; // Small radius for visitor collision
                collider.isTrigger = true; // Enable trigger events
            }
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle
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
                size
            );
        }

        private void Update()
        {
            if (state == VisitorState.Walking)
            {
                UpdateWalking();

                // Periodically recalculate path to react to new attractors
                timeSinceLastRecalculation += Time.deltaTime;
                if (timeSinceLastRecalculation >= pathRecalculationInterval)
                {
                    RecalculatePath();
                    timeSinceLastRecalculation = 0f;
                }
            }
            else if (state == VisitorState.Escaping)
            {
                // Optionally: add escape animation/effects here
                // Currently handled in OnPathCompleted with fade and delayed destroy
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the visitor with a reference to the game controller.
        /// </summary>
        /// <param name="controller">The game controller instance</param>
        public void Initialize(GameController controller)
        {
            gameController = controller;

            if (gameController != null && gameController.MazeGrid != null)
            {
                // Find the MazeGridBehaviour in the scene
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

                if (mazeGridBehaviour == null)
                {
                    Debug.LogError("VisitorController: Could not find MazeGridBehaviour in scene!");
                }
            }
            else
            {
                Debug.LogWarning("VisitorController: GameController or MazeGrid is null during initialization!");
            }
        }

        /// <summary>
        /// Initializes the visitor using the static GameController instance.
        /// </summary>
        public void Initialize()
        {
            Initialize(GameController.Instance);
        }

        #endregion

        #region Path Management

        /// <summary>
        /// Sets the path for the visitor to follow and begins walking.
        /// </summary>
        /// <param name="gridPath">List of grid coordinates forming the path</param>
        public void SetPath(List<Vector2Int> gridPath)
        {
            if (gridPath == null || gridPath.Count == 0)
            {
                Debug.LogError("VisitorController: Cannot set null or empty path!");
                return;
            }

            path = new List<Vector2Int>(gridPath);
            currentPathIndex = 0;
            state = VisitorState.Walking;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;

            // Store original destination for confusion recovery
            if (path.Count > 0)
            {
                originalDestination = path[path.Count - 1];
            }
        }

        /// <summary>
        /// Sets the path using MazeNode objects.
        /// </summary>
        /// <param name="nodePath">List of MazeNode objects forming the path</param>
        public void SetPath(List<MazeGrid.MazeNode> nodePath)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                Debug.LogError("VisitorController: Cannot set null or empty path!");
                return;
            }

            path = new List<Vector2Int>();
            foreach (var node in nodePath)
            {
                path.Add(new Vector2Int(node.x, node.y));
            }

            currentPathIndex = 0;
            state = VisitorState.Walking;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;

            // Store original destination for confusion recovery
            if (path.Count > 0)
            {
                originalDestination = path[path.Count - 1];
            }
        }

        #endregion

        #region Movement

        private void UpdateWalking()
        {
            if (mazeGridBehaviour == null)
            {
                Debug.LogError("VisitorController: MazeGridBehaviour is null, cannot convert grid to world!");
                return;
            }

            // Handle fascinated visitors with direct steering toward lantern
            if (isFascinated && !hasReachedLantern)
            {
                UpdateFascinatedSteering();
                return;
            }

            // Normal pathfinding mode
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("VisitorController: No path set but state is Walking!");
                state = VisitorState.Idle;
                return;
            }

            // Get current target waypoint
            Vector2Int targetGridPos = path[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            // Move toward target (apply speed multiplier)
            float effectiveSpeed = moveSpeed * speedMultiplier;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            // Use Rigidbody2D.MovePosition for proper trigger detection
            if (rb != null)
            {
                rb.MovePosition(newPosition);
                // Manually sync transforms with physics system to ensure trigger detection works
                // This is necessary when AutoSyncTransforms is disabled in Physics2D settings
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if we've reached the waypoint
            float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
            if (distanceToTarget < waypointReachedDistance)
            {
                OnWaypointReached();
            }
        }

        /// <summary>
        /// Handles direct steering movement toward the lantern when fascinated.
        /// Uses simplified pathfinding that ignores the prior A* plan.
        /// </summary>
        private void UpdateFascinatedSteering()
        {
            // Get lantern world position
            Vector3 lanternWorldPos = mazeGridBehaviour.GridToWorld(fascinationLanternPosition.x, fascinationLanternPosition.y);

            // Move directly toward lantern (simplified steering, not A* based)
            float effectiveSpeed = moveSpeed * speedMultiplier;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                lanternWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            // Use Rigidbody2D.MovePosition for proper trigger detection
            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if we've reached the lantern
            float distanceToLantern = Vector3.Distance(transform.position, lanternWorldPos);
            if (distanceToLantern < waypointReachedDistance)
            {
                // Reached the lantern!
                hasReachedLantern = true;
                Debug.Log($"[Fascination] '{gameObject.name}' REACHED lantern at {fascinationLanternPosition}! Switching to intersection-based random walk...");

                // Initialize random walk with current position
                if (mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
                {
                    Vector2Int currentPos = new Vector2Int(currentX, currentY);
                    path = new List<Vector2Int> { currentPos };
                    currentPathIndex = 0;
                }
            }
        }

        private void OnWaypointReached()
        {
            // Handle confusion or fascinated random walk at waypoint
            HandleConfusionAtWaypoint();

            currentPathIndex++;

            // Check if we've reached the end of the path
            if (currentPathIndex >= path.Count)
            {
                OnPathCompleted();
            }
        }

        private void OnPathCompleted()
        {
            // With spawn marker system: visitors escape (no essence awarded)
            // With legacy heart system: visitors are consumed (essence awarded)

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;

            // Check if we're using the new spawn marker system
            bool isUsingSpawnMarkers = mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2;

            if (isUsingSpawnMarkers)
            {
                // ESCAPE: Visitor reached destination spawn point
                state = VisitorState.Escaping;

                // Visual feedback: fade to transparent
                if (spriteRenderer != null)
                {
                    Color escapingColor = visitorColor;
                    escapingColor.a = 0.3f; // Fade out
                    spriteRenderer.color = escapingColor;
                }

                // Log escape
                Debug.Log($"{gameObject.name} escaped to destination spawn point (no essence awarded)");

                // Destroy visitor after short delay for visual effect
                Destroy(gameObject, 0.2f);
            }
            else
            {
                // LEGACY CONSUMED: Visitor reached the heart
                state = VisitorState.Consumed;

                // Notify the Heart that this visitor has arrived (awards essence)
                if (gameController != null && gameController.Heart != null)
                {
                    gameController.Heart.OnVisitorConsumed(this);
                }
                else
                {
                    Debug.LogWarning("VisitorController: Could not notify Heart - reference is null. Destroying self.");
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region Confusion System

        /// <summary>
        /// Attempts to trigger confusion at the current waypoint if it's an intersection.
        /// For fascinated visitors who have reached the lantern, implements random walk behavior.
        /// </summary>
        private void HandleConfusionAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
            {
                return;
            }

            // Handle fascinated visitors who have reached the lantern
            if (isFascinated && hasReachedLantern)
            {
                Debug.Log($"[Fascination] '{gameObject.name}' at waypoint {currentPathIndex}/{path.Count} - handling random walk (confusion disabled)");
                HandleFascinatedRandomWalk();
                return;
            }

            if (!confusionEnabled || currentPathIndex >= path.Count - 1)
            {
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            if (confusionSegmentActive)
            {
                // End of segment reached? Allow recovery logic and new decisions afterward.
                if (currentPathIndex > confusionSegmentEndIndex)
                {
                    confusionSegmentActive = false;
                    DecideRecoveryFromConfusion();
                }
                else
                {
                    return; // Still traversing a confusion segment; no new detours.
                }
            }

            if (!isConfused)
            {
                return; // Currently navigating normally.
            }

            Vector2Int nextPos = path[currentPathIndex + 1];

            // Check if current position is an intersection (2+ walkable neighbors excluding the tile we arrived from)
            List<Vector2Int> walkableNeighbors = GetWalkableNeighbors(currentPos);

            // Exclude the tile we just came from to find forward options
            if (currentPathIndex > 0)
            {
                walkableNeighbors.Remove(path[currentPathIndex - 1]);
            }

            if (walkableNeighbors.Count < 2)
            {
                return; // Not an intersection
            }

            // Roll for confusion
            if (Random.value > confusionChance)
            {
                return; // No confusion this time
            }

            // Get confused - pick a non-forward direction
            List<Vector2Int> detourDirections = new List<Vector2Int>();
            foreach (var neighbor in walkableNeighbors)
            {
                if (neighbor != nextPos) // Don't pick the intended next waypoint
                {
                    detourDirections.Add(neighbor);
                }
            }

            if (detourDirections.Count == 0)
            {
                return; // No detour options
            }

            // Pick random detour direction
            Vector2Int detourStart = detourDirections[Random.Range(0, detourDirections.Count)];

            BeginConfusionSegment(currentPos, detourStart);
        }

        /// <summary>
        /// Gets all walkable neighbor positions for a grid position.
        /// </summary>
        private List<Vector2Int> GetWalkableNeighbors(Vector2Int gridPos)
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

        private void BeginConfusionSegment(Vector2Int currentPos, Vector2Int detourStart)
        {
            int stepsTarget = Mathf.Clamp(Random.Range(minConfusionDistance, maxConfusionDistance + 1), minConfusionDistance, maxConfusionDistance);

            List<Vector2Int> confusionPath = BuildConfusionPath(currentPos, detourStart, stepsTarget);

            if (confusionPath.Count == 0)
            {
                return;
            }

            Vector2Int confusionEnd = confusionPath[confusionPath.Count - 1];
            List<MazeGrid.MazeNode> recoveryPathNodes = new List<MazeGrid.MazeNode>();

            if (!gameController.TryFindPath(confusionEnd, originalDestination, recoveryPathNodes) || recoveryPathNodes.Count == 0)
            {
                Debug.LogWarning($"{gameObject.name}: Confusion failed - no recovery path from {confusionEnd}");
                return;
            }

            List<Vector2Int> recoveryPath = new List<Vector2Int>();
            foreach (var node in recoveryPathNodes)
            {
                recoveryPath.Add(new Vector2Int(node.x, node.y));
            }

            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);
            newPath.AddRange(confusionPath);

            int recoveryStartIndex = (recoveryPath.Count > 0 && recoveryPath[0] == confusionEnd) ? 1 : 0;
            for (int i = recoveryStartIndex; i < recoveryPath.Count; i++)
            {
                newPath.Add(recoveryPath[i]);
            }

            path = newPath;
            currentPathIndex = 0;

            confusionSegmentActive = true;
            confusionSegmentEndIndex = confusionPath.Count;
            confusionStepsTarget = stepsTarget;
            confusionStepsTaken = 0;
            isConfused = true;

            if (debugConfusionGizmos)
            {
                Debug.Log($"{gameObject.name} took a wrong turn from {currentPos} (target {stepsTarget} steps, heading to {detourStart})");
            }
        }

        private List<Vector2Int> BuildConfusionPath(Vector2Int currentPos, Vector2Int detourStart, int stepsTarget)
        {
            List<Vector2Int> confusionPath = new List<Vector2Int>();

            Vector2Int previousPos = currentPos;
            Vector2Int nextPos = detourStart;
            Vector2Int forwardDir = detourStart - currentPos;

            int safetyLimit = 250;
            int iterations = 0;

            while (iterations < safetyLimit && confusionPath.Count < stepsTarget)
            {
                if (!IsWalkable(nextPos))
                {
                    break;
                }

                confusionPath.Add(nextPos);
                confusionStepsTaken = confusionPath.Count;

                if (IsDeadEndVisible(nextPos, forwardDir))
                {
                    break;
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(nextPos);
                neighbors.Remove(previousPos);

                if (neighbors.Count == 0)
                {
                    break; // Cannot continue this direction
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

        private bool IsWalkable(Vector2Int position)
        {
            var node = mazeGridBehaviour.Grid?.GetNode(position.x, position.y);
            return node != null && node.walkable;
        }

        private bool IsDeadEndVisible(Vector2Int startPos, Vector2Int forwardDir)
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

        private void DecideRecoveryFromConfusion()
        {
            bool recover = Random.value <= 0.5f;
            isConfused = !recover;
        }

        /// <summary>
        /// Handles intersection-based random walk behavior for fascinated visitors.
        /// Travels straight until reaching an intersection, then picks a random forward direction.
        /// Avoids immediate backtracking to the previous tile.
        /// </summary>
        private void HandleFascinatedRandomWalk()
        {
            // Only extend path if we're near the end (within 3 waypoints)
            int waypointsRemaining = path.Count - currentPathIndex;
            if (waypointsRemaining > 3)
            {
                return; // Still have enough waypoints ahead
            }

            Vector2Int currentPos = path[currentPathIndex];

            // Get walkable neighbors
            List<Vector2Int> walkableNeighbors = GetWalkableNeighbors(currentPos);

            // Exclude the tile we just came from (avoid immediate backtracking)
            Vector2Int previousTile = Vector2Int.zero;
            bool hasPrevious = false;
            if (currentPathIndex > 0)
            {
                previousTile = path[currentPathIndex - 1];
                walkableNeighbors.Remove(previousTile);
                hasPrevious = true;
            }

            if (walkableNeighbors.Count == 0)
            {
                Debug.Log($"[Fascination] '{gameObject.name}' intersection walk - dead end at {currentPos}, cannot extend path");
                return; // Dead end - let visitor reach end of path
            }

            // Determine current direction (if we have a previous tile)
            Vector2Int currentDirection = Vector2Int.zero;
            if (hasPrevious)
            {
                currentDirection = currentPos - previousTile;
            }

            // Check if we're at an intersection (2+ forward options)
            bool isIntersection = walkableNeighbors.Count >= 2;

            // Pick next tile
            Vector2Int nextTile;
            if (isIntersection)
            {
                // At intersection: pick a random forward option
                nextTile = walkableNeighbors[Random.Range(0, walkableNeighbors.Count)];
                Debug.Log($"[Fascination] '{gameObject.name}' at INTERSECTION {currentPos} with {walkableNeighbors.Count} options - picked {nextTile}");
            }
            else if (walkableNeighbors.Count == 1)
            {
                // Only one way forward: continue straight
                nextTile = walkableNeighbors[0];
            }
            else
            {
                return; // Should not happen
            }

            // Build a straight path until the next intersection
            List<Vector2Int> straightPath = BuildStraightPathToIntersection(currentPos, nextTile);

            if (straightPath.Count == 0)
            {
                Debug.Log($"[Fascination] '{gameObject.name}' intersection walk - failed to build path from {currentPos} toward {nextTile}");
                return;
            }

            // Extend the current path
            foreach (var waypoint in straightPath)
            {
                path.Add(waypoint);
            }

            Debug.Log($"[Fascination] '{gameObject.name}' intersection walk - extended path by {straightPath.Count} tiles toward {nextTile}");
        }

        /// <summary>
        /// Builds a straight path segment from current position until reaching an intersection or dead end.
        /// Follows the chosen direction without turning until forced to by maze geometry.
        /// </summary>
        private List<Vector2Int> BuildStraightPathToIntersection(Vector2Int currentPos, Vector2Int nextPos)
        {
            List<Vector2Int> straightPath = new List<Vector2Int>();

            Vector2Int previousPos = currentPos;
            Vector2Int current = nextPos;

            int safetyLimit = 100;
            int iterations = 0;

            while (iterations < safetyLimit)
            {
                if (!IsWalkable(current))
                {
                    break;
                }

                straightPath.Add(current);

                // Get neighbors for next step
                List<Vector2Int> neighbors = GetWalkableNeighbors(current);
                neighbors.Remove(previousPos); // Don't go backward

                if (neighbors.Count == 0)
                {
                    // Dead end - stop here
                    break;
                }
                else if (neighbors.Count == 1)
                {
                    // Only one way forward - continue straight
                    previousPos = current;
                    current = neighbors[0];
                }
                else
                {
                    // Intersection reached - stop here so we can make a new decision
                    // The intersection tile is already added to the path
                    break;
                }

                iterations++;
            }

            return straightPath;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stops the visitor's movement.
        /// </summary>
        public void Stop()
        {
            state = VisitorState.Idle;
        }

        /// <summary>
        /// Resumes the visitor's movement if they have a path.
        /// </summary>
        public void Resume()
        {
            if (path != null && path.Count > 0 && currentPathIndex < path.Count)
            {
                state = VisitorState.Walking;
            }
        }

        /// <summary>
        /// Recalculates the path to the original destination.
        /// Used when new attractors (lanterns) are placed to update the visitor's route.
        /// </summary>
        public void RecalculatePath()
        {
            if (state != VisitorState.Walking || gameController == null)
            {
                return; // Only recalculate if actively walking
            }

            if (path == null || path.Count == 0)
            {
                return; // No original path to recalculate
            }

            // Skip recalculation if fascinated
            // Fascinated visitors either head to lantern or wander randomly - no recalc to original destination
            if (isFascinated)
            {
                Debug.Log($"[Fascination] '{gameObject.name}' path recalc SKIPPED - visitor is fascinated (hasReachedLantern={hasReachedLantern})");
                return;
            }

            // Get current position in grid coordinates
            if (mazeGridBehaviour == null || !mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                return; // Can't determine current position
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Store old path length for comparison
            int oldPathLength = path.Count - currentPathIndex;

            // Find new path to original destination
            List<MazeGrid.MazeNode> newPathNodes = new List<MazeGrid.MazeNode>();
            if (!gameController.TryFindPath(currentPos, originalDestination, newPathNodes) || newPathNodes.Count == 0)
            {
                return; // Couldn't find new path, keep following old one
            }

            // Convert to Vector2Int path
            List<Vector2Int> newPath = new List<Vector2Int>();
            foreach (var node in newPathNodes)
            {
                newPath.Add(new Vector2Int(node.x, node.y));
            }

            // Find the closest waypoint in the new path to our current position
            int startIndex = 0;
            if (newPath.Count > 1)
            {
                Vector3 currentWorldPos = transform.position;
                float closestDist = float.MaxValue;

                for (int i = 0; i < newPath.Count; i++)
                {
                    Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(newPath[i].x, newPath[i].y);
                    float dist = Vector3.Distance(currentWorldPos, waypointWorldPos);

                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        startIndex = i;
                    }
                }

                // If we're very close to the closest waypoint (already reached it), start from the next one
                if (startIndex < newPath.Count - 1 && closestDist < waypointReachedDistance)
                {
                    startIndex++;
                }
            }

            int newPathLength = newPath.Count - startIndex;

            // Update path
            path = newPath;
            currentPathIndex = startIndex;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
        }

        /// <summary>
        /// Sets the entranced state of this visitor.
        /// Entranced visitors are affected by Fairy Rings.
        /// </summary>
        /// <param name="value">True to mark as entranced, false to clear</param>
        public void SetEntranced(bool value)
        {
            if (isEntranced != value)
            {
                isEntranced = value;
            }
        }

        /// <summary>
        /// Forces the visitor to escape immediately.
        /// Used when visitor needs to despawn without awarding essence.
        /// </summary>
        public void ForceEscape()
        {
            state = VisitorState.Escaping;

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;

            // Visual feedback
            if (spriteRenderer != null)
            {
                Color escapingColor = visitorColor;
                escapingColor.a = 0.3f;
                spriteRenderer.color = escapingColor;
            }

            Debug.Log($"{gameObject.name} forced to escape (no essence awarded)");
            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// Makes this visitor fascinated by a FaeLantern.
        /// Fascinated visitors immediately discard their A* path and use direct steering to the lantern.
        /// After reaching the lantern, they will travel straight and turn at intersections randomly.
        /// </summary>
        /// <param name="lanternGridPosition">Grid position of the lantern</param>
        public void BecomeFascinated(Vector2Int lanternGridPosition)
        {
            if (state != VisitorState.Walking)
            {
                Debug.Log($"[Fascination] '{gameObject.name}' cannot be fascinated - state is {state}, not Walking");
                return; // Only walking visitors can be fascinated
            }

            isFascinated = true;
            fascinationLanternPosition = lanternGridPosition;
            hasReachedLantern = false;

            // Immediately discard any existing A* path - DO NOT recalculate or reuse TryFindPath
            // Clear current waypoint list
            path = null;
            currentPathIndex = 0;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;

            Debug.Log($"[Fascination] '{gameObject.name}' became FASCINATED by lantern at {lanternGridPosition}! Cleared A* path, switching to direct steering mode.");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
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

                if (debugConfusionGizmos && confusionSegmentEndIndex > 0)
                {
                    Gizmos.color = Color.magenta;
                    int lastConfusionIndex = Mathf.Min(confusionSegmentEndIndex, path.Count - 1);

                    for (int i = 0; i < lastConfusionIndex; i++)
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

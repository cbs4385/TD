using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Willow-the-Wisp that wanders the maze and lures visitors to the Heart of the Maze.
    /// Wanders at 2x visitor speed when alone, slows to visitor speed when leading.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class WillowTheWisp : MonoBehaviour
    {
        #region Enums

        public enum WispState
        {
            Wandering,  // Randomly wandering the maze (no visitors in range)
            Chasing,    // Actively pursuing a target visitor
            Leading     // Leading a captured visitor to the heart
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Base movement speed (2x visitor speed when wandering)")]
        private float wanderSpeed = 6f; // 2x the default visitor speed of 3

        [SerializeField]
        [Tooltip("Speed when chasing a visitor")]
        private float chaseSpeed = 5f; // Slightly faster than visitors

        [SerializeField]
        [Tooltip("Speed when leading a visitor (matches visitor speed)")]
        private float leadSpeed = 3f; // Matches visitor speed

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [SerializeField]
        [Tooltip("Distance to capture a visitor when chasing")]
        private float captureDistance = 0.4f;

        [Header("Influence Settings")]
        [SerializeField]
        [Tooltip("Detection radius in grid tiles (Manhattan distance)")]
        private int detectionRadius = 8;

        [SerializeField]
        [Tooltip("Maximum flood-fill steps for influence area calculation")]
        private int maxFloodFillSteps = 30;

        [SerializeField]
        [Tooltip("How often to recalculate influence area (seconds)")]
        private float influenceRecalculateInterval = 2f;

        [SerializeField]
        [Tooltip("How often to scan for visitors (seconds)")]
        private float visitorScanInterval = 0.5f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the wisp sprite")]
        private Color wispColor = new Color(0.9f, 1f, 0.4f, 1f); // Yellow-green glow

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

        #endregion

        #region Private Fields

        private WispState state;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Animator animator;
        private Vector3 baseScale;
        private Vector3 initialScale;

        // Influence area
        private Vector2Int gridPosition;
        private HashSet<Vector2Int> influenceCells;
        private float influenceRecalculateTimer;

        // Visitor detection and targeting
        private VisitorController targetVisitor;
        private float visitorScanTimer;

        // Wandering path
        private List<Vector2Int> wanderPath;
        private int currentPathIndex;

        // Visitor being led
        private VisitorController followingVisitor;

        // Target destination (Heart of the Maze)
        private Vector2Int heartGridPosition;

        private const string DirectionParameter = "Direction";

        #endregion

        #region Properties

        /// <summary>Gets the current state of the wisp</summary>
        public WispState State => state;

        /// <summary>Gets whether this wisp is currently leading a visitor</summary>
        public bool IsLeading => state == WispState.Leading && followingVisitor != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = WispState.Wandering;
            initialScale = transform.localScale;
            SetupSpriteRenderer();
            SetupColliders();
            animator = GetComponent<Animator>();
        }

        private void Start()
        {
            // Find references
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            gameController = GameController.Instance;

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WillowTheWisp: Could not find MazeGridBehaviour!");
                return;
            }

            if (gameController == null)
            {
                Debug.LogError("WillowTheWisp: Could not find GameController!");
                return;
            }

            // Get heart position
            if (gameController.Heart != null)
            {
                heartGridPosition = gameController.Heart.GridPosition;
            }
            else
            {
                Debug.LogWarning("WillowTheWisp: Heart not found! Will try to locate it later.");
            }

            // Ensure wisp starts on a walkable tile
            EnsureWalkablePosition();

            // Calculate initial influence area
            CalculateInfluenceArea();

            // Start wandering
            GenerateRandomWanderPath();
        }

        private void Update()
        {
            if (enablePulse && spriteRenderer != null)
            {
                UpdatePulse();
            }

            // Periodically recalculate influence area
            influenceRecalculateTimer += Time.deltaTime;
            if (influenceRecalculateTimer >= influenceRecalculateInterval)
            {
                influenceRecalculateTimer = 0f;
                CalculateInfluenceArea();
            }

            // Periodically scan for visitors (except when already leading)
            if (state != WispState.Leading)
            {
                visitorScanTimer += Time.deltaTime;
                if (visitorScanTimer >= visitorScanInterval)
                {
                    visitorScanTimer = 0f;
                    ScanForVisitors();
                }
            }

            // Update state-specific behavior
            if (state == WispState.Wandering)
            {
                UpdateWandering();
            }
            else if (state == WispState.Chasing)
            {
                UpdateChasing();
            }
            else if (state == WispState.Leading)
            {
                UpdateLeading();
            }
        }

        #endregion

        #region Influence and Detection

        /// <summary>
        /// Calculates the flood-fill influence area for visitor detection.
        /// </summary>
        private void CalculateInfluenceArea()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
                return;

            // Convert world position to grid coordinates
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                return;
            }

            gridPosition = new Vector2Int(x, y);

            // Use flood-fill to get reachable tiles
            influenceCells = mazeGridBehaviour.Grid.FloodFillReachable(x, y, detectionRadius, maxFloodFillSteps);
        }

        /// <summary>
        /// Scans for visitors within the influence area and picks the best target.
        /// Prioritizes the closest visitor with the least status effects.
        /// </summary>
        private void ScanForVisitors()
        {
            if (influenceCells == null || influenceCells.Count == 0)
                return;

            // Find all visitors in the scene
            VisitorController[] allVisitors = FindObjectsByType<VisitorController>(FindObjectsSortMode.None);
            if (allVisitors.Length == 0)
                return;

            // Filter visitors that are within influence area and walkable
            List<VisitorController> candidateVisitors = new List<VisitorController>();
            foreach (var visitor in allVisitors)
            {
                // Skip if not walking
                if (visitor.State != VisitorController.VisitorState.Walking)
                    continue;

                // Skip if already following a wisp
                var followWisp = visitor.GetComponent<FollowWispBehavior>();
                if (followWisp != null && followWisp.IsFollowing)
                    continue;

                // Check if visitor is in influence area
                if (mazeGridBehaviour.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    Vector2Int visitorGridPos = new Vector2Int(vx, vy);
                    if (influenceCells.Contains(visitorGridPos))
                    {
                        candidateVisitors.Add(visitor);
                    }
                }
            }

            if (candidateVisitors.Count == 0)
            {
                // No visitors in range, return to wandering if we were chasing
                if (state == WispState.Chasing)
                {
                    Debug.Log("WillowTheWisp: Lost target visitor, returning to wandering");
                    ReturnToWandering();
                }
                return;
            }

            // Pick the best target: closest visitor with least status effects
            VisitorController bestTarget = FindBestTarget(candidateVisitors);

            if (bestTarget != null)
            {
                // Start chasing if we were wandering, or update target if already chasing
                if (state == WispState.Wandering || targetVisitor != bestTarget)
                {
                    StartChasing(bestTarget);
                }
            }
        }

        /// <summary>
        /// Finds the best visitor to target based on distance and status effects.
        /// Prioritizes: least affected visitor, then closest.
        /// </summary>
        private VisitorController FindBestTarget(List<VisitorController> candidates)
        {
            if (candidates.Count == 0)
                return null;

            VisitorController bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var visitor in candidates)
            {
                // Calculate status effect count (lower is better)
                int statusCount = 0;
                if (visitor.IsFascinated) statusCount++;
                if (visitor.IsEntranced) statusCount++;

                // Calculate distance to wisp
                float distance = Vector3.Distance(transform.position, visitor.transform.position);

                // Score: prioritize fewer status effects, then closer distance
                // Weight status effects heavily (multiply by 100 to make it dominant)
                float score = (statusCount * 100f) + distance;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = visitor;
                }
            }

            return bestTarget;
        }

        #endregion

        #region Visual Setup

        private void CreateVisualSprite()
        {
            // Create a glowing circle sprite
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

            spriteRenderer.color = wispColor;
            spriteRenderer.sortingOrder = sortingOrder;

            baseScale = useProceduralSprite
                ? new Vector3(wispSize, wispSize, 1f)
                : initialScale;
            transform.localScale = baseScale;
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle with soft edges (glow effect)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
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

        private void SetupColliders()
        {
            // Rigidbody2D for physics
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // Trigger collider for visitor detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }
            collider.radius = 0.4f;
            collider.isTrigger = true;
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        #endregion

        #region Wandering Behavior

        private void GenerateRandomWanderPath()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Get current grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Debug.LogWarning("WillowTheWisp: Could not convert world position to grid!");
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Pick a random walkable tile as destination
            Vector2Int randomDest = GetRandomWalkableTile();
            if (randomDest == currentPos)
            {
                // Try again in a moment
                Invoke(nameof(GenerateRandomWanderPath), 1f);
                return;
            }

            // Find path to random destination
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, randomDest, pathNodes) && pathNodes.Count > 0)
            {
                wanderPath = new List<Vector2Int>();
                foreach (var node in pathNodes)
                {
                    wanderPath.Add(new Vector2Int(node.x, node.y));
                }

                currentPathIndex = 0;
                Debug.Log($"WillowTheWisp: Generated wander path with {wanderPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"WillowTheWisp: Failed to find wander path from {currentPos} to {randomDest}");
                // Try again
                Invoke(nameof(GenerateRandomWanderPath), 1f);
            }
        }

        private Vector2Int GetRandomWalkableTile()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
                return Vector2Int.zero;

            var grid = mazeGridBehaviour.Grid;
            int maxAttempts = 50;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                int x = Random.Range(0, grid.Width);
                int y = Random.Range(0, grid.Height);

                var node = grid.GetNode(x, y);
                if (node != null && node.walkable)
                {
                    return new Vector2Int(x, y);
                }

                attempts++;
            }

            // Fallback: return current position
            if (mazeGridBehaviour.WorldToGrid(transform.position, out int cx, out int cy))
            {
                return new Vector2Int(cx, cy);
            }

            return Vector2Int.zero;
        }

        /// <summary>
        /// Ensures the wisp is positioned on a walkable tile. If not, finds the nearest walkable tile.
        /// </summary>
        private void EnsureWalkablePosition()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
                return;

            var grid = mazeGridBehaviour.Grid;

            // Get current grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Debug.LogWarning("WillowTheWisp: Could not convert world position to grid, moving to random walkable tile");
                Vector2Int randomPos = GetRandomWalkableTile();
                transform.position = mazeGridBehaviour.GridToWorld(randomPos.x, randomPos.y);
                return;
            }

            // Check if current position is walkable
            var currentNode = grid.GetNode(currentX, currentY);
            if (currentNode != null && currentNode.walkable)
            {
                Debug.Log($"WillowTheWisp: Starting at walkable position ({currentX}, {currentY})");
                return;
            }

            // Current position is not walkable, find nearest walkable tile
            Debug.LogWarning($"WillowTheWisp: Start position ({currentX}, {currentY}) is not walkable! Finding nearest walkable tile...");

            Vector2Int nearestWalkable = FindNearestWalkableTile(currentX, currentY);
            if (nearestWalkable != new Vector2Int(currentX, currentY))
            {
                Vector3 newWorldPos = mazeGridBehaviour.GridToWorld(nearestWalkable.x, nearestWalkable.y);
                transform.position = newWorldPos;
                Debug.Log($"WillowTheWisp: Moved to nearest walkable tile ({nearestWalkable.x}, {nearestWalkable.y})");
            }
            else
            {
                // Couldn't find nearby walkable tile, use random one
                Debug.LogWarning("WillowTheWisp: Could not find nearby walkable tile, moving to random location");
                Vector2Int randomPos = GetRandomWalkableTile();
                transform.position = mazeGridBehaviour.GridToWorld(randomPos.x, randomPos.y);
            }
        }

        /// <summary>
        /// Finds the nearest walkable tile using a spiral search pattern.
        /// </summary>
        private Vector2Int FindNearestWalkableTile(int startX, int startY)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
                return new Vector2Int(startX, startY);

            var grid = mazeGridBehaviour.Grid;
            int maxRadius = 10;

            // Spiral search outward from start position
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        // Only check tiles on the current radius perimeter
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                            continue;

                        int checkX = startX + dx;
                        int checkY = startY + dy;

                        // Check bounds
                        if (checkX < 0 || checkX >= grid.Width || checkY < 0 || checkY >= grid.Height)
                            continue;

                        var node = grid.GetNode(checkX, checkY);
                        if (node != null && node.walkable)
                        {
                            return new Vector2Int(checkX, checkY);
                        }
                    }
                }
            }

            // Fallback
            return new Vector2Int(startX, startY);
        }

        private void UpdateWandering()
        {
            if (wanderPath == null || wanderPath.Count == 0)
            {
                GenerateRandomWanderPath();
                return;
            }

            if (currentPathIndex >= wanderPath.Count)
            {
                // Reached end of wander path, generate new one
                GenerateRandomWanderPath();
                return;
            }

            // Move toward current waypoint
            Vector2Int targetGridPos = wanderPath[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            UpdateAnimatorDirection(targetWorldPos - transform.position);

            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                wanderSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if waypoint reached
            float distance = Vector3.Distance(transform.position, targetWorldPos);
            if (distance < waypointReachedDistance)
            {
                currentPathIndex++;
            }
        }

        #endregion

        #region Chasing Behavior

        /// <summary>
        /// Starts actively chasing a target visitor.
        /// </summary>
        private void StartChasing(VisitorController visitor)
        {
            if (visitor == null)
                return;

            Debug.Log($"WillowTheWisp: Started chasing visitor {visitor.name}");

            targetVisitor = visitor;
            state = WispState.Chasing;
            wanderPath = null; // Clear wander path
            currentPathIndex = 0;
        }

        /// <summary>
        /// Updates the chasing behavior: pursue the target visitor directly.
        /// </summary>
        private void UpdateChasing()
        {
            // Check if target is still valid
            if (targetVisitor == null || targetVisitor.State != VisitorController.VisitorState.Walking)
            {
                Debug.Log("WillowTheWisp: Target visitor lost or invalid, returning to wandering");
                ReturnToWandering();
                return;
            }

            // Check if visitor is already following a wisp
            var followWisp = targetVisitor.GetComponent<FollowWispBehavior>();
            if (followWisp != null && followWisp.IsFollowing)
            {
                Debug.Log("WillowTheWisp: Target visitor captured by another wisp, returning to wandering");
                ReturnToWandering();
                return;
            }

            // Calculate distance to target
            float distance = Vector3.Distance(transform.position, targetVisitor.transform.position);

            UpdateAnimatorDirection(targetVisitor.transform.position - transform.position);

            // Check if close enough to capture
            if (distance <= captureDistance)
            {
                CaptureVisitor(targetVisitor);
                return;
            }

            // Move directly toward visitor
            Vector3 direction = (targetVisitor.transform.position - transform.position).normalized;
            Vector3 newPosition = transform.position + direction * chaseSpeed * Time.deltaTime;

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }
        }

        #endregion

        #region Leading Behavior

        private void CaptureVisitor(VisitorController visitor)
        {
            Debug.Log($"WillowTheWisp: Captured visitor {visitor.name}!");

            followingVisitor = visitor;
            state = WispState.Leading;

            // Notify visitor to follow this wisp
            var followWisp = visitor.gameObject.GetComponent<FollowWispBehavior>();
            if (followWisp == null)
            {
                followWisp = visitor.gameObject.AddComponent<FollowWispBehavior>();
            }
            followWisp.StartFollowing(this);

            // Generate path to heart
            GeneratePathToHeart();
        }

        private void GeneratePathToHeart()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Update heart position if we didn't have it before
            if (gameController.Heart != null)
            {
                heartGridPosition = gameController.Heart.GridPosition;
            }
            else
            {
                Debug.LogError("WillowTheWisp: Cannot lead to heart - heart not found!");
                ReturnToWandering();
                return;
            }

            // Get current position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Debug.LogWarning("WillowTheWisp: Could not convert position to grid!");
                ReturnToWandering();
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Find path to heart
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, heartGridPosition, pathNodes) && pathNodes.Count > 0)
            {
                wanderPath = new List<Vector2Int>();
                foreach (var node in pathNodes)
                {
                    wanderPath.Add(new Vector2Int(node.x, node.y));
                }

                currentPathIndex = 0;
                Debug.Log($"WillowTheWisp: Leading visitor to heart with path of {wanderPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"WillowTheWisp: Failed to find path to heart from {currentPos}");
                ReturnToWandering();
            }
        }

        private void UpdateLeading()
        {
            // Check if visitor is still following
            if (followingVisitor == null || followingVisitor.State == VisitorController.VisitorState.Consumed)
            {
                Debug.Log("WillowTheWisp: Visitor consumed or lost, returning to wandering");
                ReturnToWandering();
                return;
            }

            if (wanderPath == null || wanderPath.Count == 0)
            {
                GeneratePathToHeart();
                return;
            }

            if (currentPathIndex >= wanderPath.Count)
            {
                // Reached heart - visitor should be consumed soon
                Debug.Log("WillowTheWisp: Reached heart, returning to wandering");
                ReturnToWandering();
                return;
            }

            // Move toward current waypoint at visitor speed
            Vector2Int targetGridPos = wanderPath[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            UpdateAnimatorDirection(targetWorldPos - transform.position);

            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                leadSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if waypoint reached
            float distance = Vector3.Distance(transform.position, targetWorldPos);
            if (distance < waypointReachedDistance)
            {
                currentPathIndex++;
            }
        }

        private void ReturnToWandering()
        {
            state = WispState.Wandering;
            targetVisitor = null;
            followingVisitor = null;
            wanderPath = null;
            currentPathIndex = 0;

            // Generate new random wander path
            GenerateRandomWanderPath();
        }

        private void UpdateAnimatorDirection(Vector3 direction)
        {
            if (animator == null)
                return;

            // Avoid updating when there's no meaningful movement direction
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

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw influence area
            if (influenceCells != null && influenceCells.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = new Color(0.9f, 1f, 0.4f, 0.1f); // Semi-transparent yellow-green
                foreach (var cell in influenceCells)
                {
                    Vector3 worldPos = mazeGridBehaviour.GridToWorld(cell.x, cell.y);
                    Gizmos.DrawCube(worldPos, new Vector3(0.8f, 0.8f, 0.1f));
                }
            }

            // Draw current path
            if (wanderPath != null && wanderPath.Count > 0 && mazeGridBehaviour != null)
            {
                // Color based on state
                if (state == WispState.Leading)
                    Gizmos.color = Color.yellow;
                else if (state == WispState.Chasing)
                    Gizmos.color = Color.red;
                else
                    Gizmos.color = Color.green;

                for (int i = 0; i < wanderPath.Count - 1; i++)
                {
                    Vector3 start = mazeGridBehaviour.GridToWorld(wanderPath[i].x, wanderPath[i].y);
                    Vector3 end = mazeGridBehaviour.GridToWorld(wanderPath[i + 1].x, wanderPath[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }

                // Draw current target
                if (currentPathIndex < wanderPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 target = mazeGridBehaviour.GridToWorld(wanderPath[currentPathIndex].x, wanderPath[currentPathIndex].y);
                    Gizmos.DrawWireSphere(target, 0.2f);
                }
            }

            // Draw line to target visitor when chasing
            if (state == WispState.Chasing && targetVisitor != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }

            // Draw line to following visitor when leading
            if (state == WispState.Leading && followingVisitor != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, followingVisitor.transform.position);
            }
        }

        #endregion
    }
}

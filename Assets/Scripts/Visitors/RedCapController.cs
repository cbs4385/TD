using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Red Cap - A hostile actor that hunts visitors and drains essence.
    /// Moves faster than visitors, actively stalks them, and penalizes the player
    /// when catching one.
    /// </summary>
    public class RedCapController : MonoBehaviour
    {
        #region Enums

        public enum RedCapState
        {
            Idle,
            Hunting,
            Returning
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed multiplier relative to visitor speed (1.25 = 25% faster)")]
        private float speedMultiplier = 1.25f;

        [SerializeField]
        [Tooltip("Base movement speed in units per second")]
        private float baseMoveSpeed = 3f;

        [Header("Hunting Settings")]
        [SerializeField]
        [Tooltip("How often to update target selection (in seconds)")]
        private float targetUpdateInterval = 0.5f;

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [SerializeField]
        [Tooltip("Detection radius for visitor contact (collision)")]
        private float contactRadius = 0.3f;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Essence penalty multiplier when catching a visitor (2.0 = double the normal reward)")]
        private float essencePenaltyMultiplier = 2.0f;

        [SerializeField]
        [Tooltip("Base essence value per visitor (should match HeartOfTheMaze setting)")]
        private int baseEssencePerVisitor = 10;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use procedural sprite if true, otherwise relies on child SpriteRenderer/Animator")]
        private bool useProceduralSprite = false;

        [SerializeField]
        [Tooltip("Color of the Red Cap (used for procedural sprite)")]
        private Color redCapColor = new Color(0.8f, 0.1f, 0.1f, 1f); // Dark red

        [SerializeField]
        [Tooltip("Size of the Red Cap sprite")]
        private float redCapSize = 1.2f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Animator parameter name for direction")]
        private string directionParameterName = "Direction";

        #endregion

        #region Private Fields

        private RedCapState state = RedCapState.Idle;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private List<Vector2Int> currentPath = new List<Vector2Int>();
        private int currentWaypointIndex;
        private VisitorController targetVisitor;
        private float targetUpdateTimer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private float moveSpeed;
        private bool initialized;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Red Cap</summary>
        public RedCapState State => state;

        /// <summary>Gets the current target visitor</summary>
        public VisitorController TargetVisitor => targetVisitor;

        /// <summary>Gets the calculated move speed</summary>
        public float MoveSpeed => moveSpeed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Calculate actual move speed
            moveSpeed = baseMoveSpeed * speedMultiplier;
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!AcquireDependencies())
            {
                Debug.LogWarning($"[RedCap] Failed to acquire dependencies. GameController: {gameController != null}, MazeGrid: {mazeGridBehaviour != null}");
                return;
            }

            TryInitialize();

            if (state == RedCapState.Hunting)
            {
                UpdateTargetSelection();
                FollowPath();
                CheckForVisitorContact();
            }
            else
            {
                Debug.LogWarning($"[RedCap] Not hunting. Current state: {state}, Initialized: {initialized}");
            }
        }

        private void TryInitialize()
        {
            if (initialized)
            {
                return;
            }

            Debug.Log($"[RedCap] Attempting initialization...");

            // Find required components
            AcquireDependencies();
            animator = GetComponent<Animator>();

            if (gameController == null || mazeGridBehaviour == null)
            {
                Debug.LogWarning($"[RedCap] Cannot initialize - missing dependencies. GameController: {gameController != null}, MazeGrid: {mazeGridBehaviour != null}");
                return;
            }

            // Create visual representation only if using procedural sprite
            if (useProceduralSprite)
            {
                CreateProceduralVisual();
                Debug.Log($"[RedCap] Created procedural visual");
            }
            else
            {
                // Get existing SpriteRenderer if not using procedural
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                Debug.Log($"[RedCap] Using existing sprite renderer: {spriteRenderer != null}");
            }

            // Initialize animator direction
            if (animator != null)
            {
                SetAnimatorDirection(IdleDirection);
                Debug.Log($"[RedCap] Animator found and initialized");
            }
            else
            {
                Debug.LogWarning($"[RedCap] No Animator component found");
            }

            // Start hunting
            state = RedCapState.Hunting;
            initialized = true;
            Debug.Log($"[RedCap] Initialization complete! State set to Hunting");
        }

        private bool AcquireDependencies()
        {
            bool ready = true;

            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (gameController == null || mazeGridBehaviour == null)
            {
                ready = false;
            }

            return ready;
        }

        #endregion

        #region Hunting Behavior

        /// <summary>
        /// Updates target selection at regular intervals.
        /// Finds the closest visitor or switches to a closer one.
        /// </summary>
        private void UpdateTargetSelection()
        {
            targetUpdateTimer -= Time.deltaTime;

            if (targetUpdateTimer <= 0f)
            {
                targetUpdateTimer = targetUpdateInterval;

                // Find all visitors in the scene
                VisitorController[] allVisitors = FindObjectsByType<VisitorController>(FindObjectsSortMode.None);

                Debug.Log($"[RedCap] Target update - Found {allVisitors.Length} visitors");

                if (allVisitors.Length == 0)
                {
                    targetVisitor = null;
                    currentPath.Clear();
                    state = RedCapState.Idle;
                    Debug.LogWarning($"[RedCap] No visitors found, switching to Idle state");
                    return;
                }

                // Find closest visitor
                VisitorController closestVisitor = null;
                float closestDistance = float.MaxValue;

                foreach (var visitor in allVisitors)
                {
                    if (visitor == null || visitor.gameObject == null)
                        continue;

                    float distance = Vector3.Distance(transform.position, visitor.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestVisitor = visitor;
                    }
                }

                // Update target if we found a new one
                if (closestVisitor != targetVisitor)
                {
                    Debug.Log($"[RedCap] New target acquired: {closestVisitor?.gameObject.name} at distance {closestDistance:F2}");
                    targetVisitor = closestVisitor;
                    RecalculatePathToTarget();
                }
            }
        }

        /// <summary>
        /// Recalculates the path to the current target visitor.
        /// </summary>
        private void RecalculatePathToTarget()
        {
            if (targetVisitor == null || gameController == null || mazeGridBehaviour == null)
            {
                Debug.LogWarning($"[RedCap] Cannot recalculate path - Target: {targetVisitor != null}, GameController: {gameController != null}, MazeGrid: {mazeGridBehaviour != null}");
                currentPath.Clear();
                return;
            }

            // Get current grid position
            int currentX, currentY;
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out currentX, out currentY))
            {
                Debug.LogError($"[RedCap] Failed to convert RedCap world position {transform.position} to grid");
                currentPath.Clear();
                return;
            }
            Vector2Int currentGridPos = new Vector2Int(currentX, currentY);

            // Get target grid position
            int targetX, targetY;
            if (!mazeGridBehaviour.WorldToGrid(targetVisitor.transform.position, out targetX, out targetY))
            {
                Debug.LogError($"[RedCap] Failed to convert target visitor position {targetVisitor.transform.position} to grid");
                currentPath.Clear();
                return;
            }
            Vector2Int targetGridPos = new Vector2Int(targetX, targetY);

            Debug.Log($"[RedCap] Calculating path from grid {currentGridPos} to {targetGridPos}");

            // Find path using GameController's pathfinding
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentGridPos, targetGridPos, pathNodes))
            {
                currentPath.Clear();
                foreach (var node in pathNodes)
                {
                    currentPath.Add(new Vector2Int(node.x, node.y));
                }

                currentWaypointIndex = 0;
                Debug.Log($"[RedCap] Path found! {currentPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"[RedCap] No path found from {currentGridPos} to {targetGridPos}");
                currentPath.Clear();
            }
        }

        /// <summary>
        /// Follows the current path toward the target.
        /// </summary>
        private void FollowPath()
        {
            if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
            {
                Debug.Log($"[RedCap] No valid path - PathCount: {currentPath.Count}, WaypointIndex: {currentWaypointIndex}");
                // No path or reached end - recalculate
                RecalculatePathToTarget();

                // If we still don't have a path, move directly toward the visitor as a fallback
                if (currentPath.Count == 0 && targetVisitor != null)
                {
                    Debug.Log($"[RedCap] Using direct movement fallback toward {targetVisitor.gameObject.name}");
                    MoveDirectlyToward(targetVisitor.transform.position);
                }
                return;
            }

            // Get current waypoint
            Vector2Int waypointGridPos = currentPath[currentWaypointIndex];
            Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(waypointGridPos.x, waypointGridPos.y);

            // Move toward waypoint
            Vector3 direction = (waypointWorldPos - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;

            Debug.Log($"[RedCap] Following path - Waypoint {currentWaypointIndex}/{currentPath.Count}: {waypointGridPos}, Distance: {Vector3.Distance(transform.position, waypointWorldPos):F3}, Moving: {movement.magnitude:F3}");

            // Update animation direction based on movement
            UpdateAnimationDirection(direction);

            // Check if reached waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, waypointWorldPos);
            if (distanceToWaypoint < waypointReachedDistance)
            {
                Debug.Log($"[RedCap] Reached waypoint {currentWaypointIndex}");
                currentWaypointIndex++;

                // Recalculate path periodically to adjust for moving target
                if (currentWaypointIndex >= currentPath.Count)
                {
                    Debug.Log($"[RedCap] Completed path, recalculating...");
                    RecalculatePathToTarget();
                }
            }
        }

        private void MoveDirectlyToward(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;
            Debug.Log($"[RedCap] Direct movement - Target: {targetPosition}, Distance: {Vector3.Distance(transform.position, targetPosition):F3}, Movement: {movement.magnitude:F3}");
            UpdateAnimationDirection(direction);
        }

        /// <summary>
        /// Updates the animation direction based on movement vector.
        /// </summary>
        private void UpdateAnimationDirection(Vector3 movement)
        {
            if (animator == null)
                return;

            // Calculate direction from movement (similar to VisitorController)
            int direction = GetDirectionFromMovement(new Vector2(movement.x, movement.y));
            SetAnimatorDirection(direction);
        }

        /// <summary>
        /// Gets the direction enum from a movement vector.
        /// </summary>
        private int GetDirectionFromMovement(Vector2 movement)
        {
            float movementThreshold = 0.01f;

            if (movement.sqrMagnitude <= movementThreshold * movementThreshold)
            {
                return lastDirection; // Retain last direction when not moving much
            }

            float absX = Mathf.Abs(movement.x);
            float absY = Mathf.Abs(movement.y);

            // Determine dominant axis
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

            if (newDirection != IdleDirection)
            {
                lastDirection = newDirection;
            }

            return newDirection;
        }

        /// <summary>
        /// Sets the animator direction parameter.
        /// </summary>
        private void SetAnimatorDirection(int direction)
        {
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }
        }

        /// <summary>
        /// Checks if the Red Cap is in contact with a visitor.
        /// </summary>
        private void CheckForVisitorContact()
        {
            if (targetVisitor == null)
            {
                Debug.LogWarning($"[RedCap] CheckForVisitorContact called but targetVisitor is null");
                return;
            }

            float distance = Vector3.Distance(transform.position, targetVisitor.transform.position);

            if (distance <= contactRadius)
            {
                Debug.Log($"[RedCap] CONTACT! Capturing visitor {targetVisitor.gameObject.name} at distance {distance:F3}");
                CaptureVisitor(targetVisitor);
            }
        }

        /// <summary>
        /// Captures a visitor, despawns them, and charges the essence penalty.
        /// </summary>
        /// <param name="visitor">The visitor to capture</param>
        private void CaptureVisitor(VisitorController visitor)
        {
            if (visitor == null)
            {
                Debug.LogWarning($"[RedCap] CaptureVisitor called with null visitor");
                return;
            }

            Debug.Log($"[RedCap] Capturing visitor: {visitor.gameObject.name}");

            // Calculate essence penalty
            int essencePenalty = Mathf.RoundToInt(baseEssencePerVisitor * essencePenaltyMultiplier);
            Debug.Log($"[RedCap] Applying essence penalty: -{essencePenalty}");

            // Deduct essence from player
            if (gameController != null)
            {
                // Use TrySpendEssence to deduct, but we want to force the deduction even if not enough
                // So we'll use AddEssence with negative value to bypass the spending check
                gameController.AddEssence(-essencePenalty);
                Debug.Log($"[RedCap] Essence penalty applied successfully");
            }
            else
            {
                Debug.LogWarning($"[RedCap] Cannot apply essence penalty - GameController is null");
            }

            // Despawn the visitor
            Debug.Log($"[RedCap] Destroying visitor gameobject");
            Destroy(visitor.gameObject);

            // Clear target and find a new one
            targetVisitor = null;
            currentPath.Clear();
            Debug.Log($"[RedCap] Capture complete, target cleared");
        }

        #endregion

        #region Visual

        /// <summary>
        /// Creates the procedural visual representation of the Red Cap.
        /// </summary>
        private void CreateProceduralVisual()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a sprite (simple circle for now)
            spriteRenderer.sprite = CreateCircleSprite(32);
            spriteRenderer.color = redCapColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(redCapSize, redCapSize, 1f);

            // Add CircleCollider2D for trigger detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = contactRadius;
                collider.isTrigger = true;
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

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw Red Cap position
            Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw contact radius
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, contactRadius);

            // Draw path if available
            if (currentPath != null && currentPath.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);

                for (int i = currentWaypointIndex; i < currentPath.Count; i++)
                {
                    Vector3 worldPos = mazeGridBehaviour.GridToWorld(currentPath[i].x, currentPath[i].y);
                    Gizmos.DrawSphere(worldPos, 0.1f);

                    if (i > currentWaypointIndex)
                    {
                        Vector3 prevWorldPos = mazeGridBehaviour.GridToWorld(currentPath[i - 1].x, currentPath[i - 1].y);
                        Gizmos.DrawLine(prevWorldPos, worldPos);
                    }
                    else if (i == currentWaypointIndex)
                    {
                        Gizmos.DrawLine(transform.position, worldPos);
                    }
                }
            }

            // Draw line to target visitor
            if (targetVisitor != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }
        }

        #endregion
    }
}

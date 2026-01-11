using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;
using ForestMaze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Red Cap - A hostile actor that hunts visitors and drains essence.
    /// Moves faster than visitors, actively stalks them, and penalizes the player
    /// when catching one.
    /// Uses world-space navigation to pursue targets.
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
        private List<Vector3> worldPath = new List<Vector3>();
        private int currentWaypointIndex;
        private VisitorControllerBase targetVisitor;
        private float targetUpdateTimer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private float moveSpeed;
        private bool initialized;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        // Base rotation from prefab (captured at initialization)
        private Quaternion baseRotation;
        private bool baseRotationCaptured = false;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Red Cap</summary>
        public RedCapState State => state;

        /// <summary>Gets the current target visitor</summary>
        public VisitorControllerBase TargetVisitor => targetVisitor;

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
                return;
            }

            TryInitialize();

            if (state == RedCapState.Hunting)
            {
                UpdateTargetSelection();
                FollowPath();
                CheckForVisitorContact();
            }
        }

        private void TryInitialize()
        {
            if (initialized)
            {
                return;
            }

            // Find required components
            AcquireDependencies();
            // Look for Animator on this GameObject or children (for Blender imports)
            animator = GetComponentInChildren<Animator>();

            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Create visual representation only if using procedural sprite
            if (useProceduralSprite)
            {
                CreateProceduralVisual();
            }
            else
            {
                // Get existing SpriteRenderer if not using procedural
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Initialize animator direction
            if (animator != null)
            {
                // Capture the base rotation from the prefab before applying any directional rotation
                if (!baseRotationCaptured)
                {
                    baseRotation = animator.transform.localRotation;
                    baseRotationCaptured = true;
                }

                SetAnimatorDirection(IdleDirection);
            }

            // Start hunting
            state = RedCapState.Hunting;
            initialized = true;
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
                VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

                if (allVisitors.Length == 0)
                {
                    targetVisitor = null;
                    worldPath.Clear();
                    state = RedCapState.Idle;
                    return;
                }

                // Find closest visitor
                VisitorControllerBase closestVisitor = null;
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
                    targetVisitor = closestVisitor;
                    RecalculatePathToTarget();
                }
            }
        }

        /// <summary>
        /// Recalculates the path to the current target visitor using world-space pathfinding.
        /// </summary>
        private void RecalculatePathToTarget()
        {
            if (targetVisitor == null || mazeGridBehaviour == null)
            {
                worldPath.Clear();
                return;
            }

            // Use world-space pathfinding
            worldPath = BuildWorldPath(transform.position, targetVisitor.transform.position);
            currentWaypointIndex = 0;
        }

        /// <summary>
        /// Builds a world-space path from start to end using graph edges.
        /// Returns a list of world positions to traverse.
        /// </summary>
        private List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.PlanarForestMazeGenerator.ForestMapState == null)
            {
                // Fallback: direct path
                return new List<Vector3> { end };
            }

            var graphState = mazeGridBehaviour.PlanarForestMazeGenerator.ForestMapState;
            var result = new List<Vector3>();

            // Find nearest node to start
            int startNodeIndex = FindNearestNodeIndex(graphState, new Vector2(start.x, start.y));
            int endNodeIndex = FindNearestNodeIndex(graphState, new Vector2(end.x, end.y));

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
            for (int i = 0; i < nodePath.Count - 1; i++)
            {
                int fromNode = nodePath[i];
                int toNode = nodePath[i + 1];

                // Find edge between these nodes
                var edgePoints = GetEdgePoints(graphState, fromNode, toNode);
                if (edgePoints != null)
                {
                    result.AddRange(edgePoints);
                }
            }

            // Add final destination
            result.Add(end);

            return result;
        }

        /// <summary>
        /// Finds the nearest node index to a world position.
        /// </summary>
        private int FindNearestNodeIndex(PlanarForestMazeGenerator.ForestMapState graphState, Vector2 worldPos)
        {
            if (graphState.Nodes == null || graphState.Nodes.Count == 0)
            {
                return -1;
            }

            int nearestIndex = -1;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < graphState.Nodes.Count; i++)
            {
                var node = graphState.Nodes[i];
                float dist = Vector2.Distance(worldPos, node.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        /// <summary>
        /// BFS to find path through graph nodes.
        /// </summary>
        private List<int> FindNodePath(PlanarForestMazeGenerator.ForestMapState graphState, int startIndex, int endIndex)
        {
            if (startIndex == endIndex)
            {
                return new List<int> { startIndex };
            }

            var visited = new HashSet<int>();
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { startIndex });
            visited.Add(startIndex);

            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                int currentNode = currentPath[currentPath.Count - 1];

                // Get neighbors from edges
                foreach (var edge in graphState.Edges)
                {
                    if (edge.Partial || !edge.NodeB.HasValue)
                        continue;

                    int neighborIndex = -1;
                    if (edge.NodeA == currentNode && !visited.Contains(edge.NodeB.Value))
                    {
                        neighborIndex = edge.NodeB.Value;
                    }
                    else if (edge.NodeB.Value == currentNode && !visited.Contains(edge.NodeA))
                    {
                        neighborIndex = edge.NodeA;
                    }

                    if (neighborIndex >= 0)
                    {
                        var newPath = new List<int>(currentPath) { neighborIndex };

                        if (neighborIndex == endIndex)
                        {
                            return newPath;
                        }

                        visited.Add(neighborIndex);
                        queue.Enqueue(newPath);
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Gets the world-space points along an edge between two nodes.
        /// </summary>
        private List<Vector3> GetEdgePoints(PlanarForestMazeGenerator.ForestMapState graphState, int fromNode, int toNode)
        {
            foreach (var edge in graphState.Edges)
            {
                if (edge.Partial || !edge.NodeB.HasValue)
                    continue;

                bool matchesForward = edge.NodeA == fromNode && edge.NodeB.Value == toNode;
                bool matchesReverse = edge.NodeA == toNode && edge.NodeB.Value == fromNode;

                if (matchesForward || matchesReverse)
                {
                    var points = new List<Vector3>();

                    if (edge.PolylinePoints != null && edge.PolylinePoints.Count > 0)
                    {
                        // Use polyline points
                        if (matchesReverse)
                        {
                            // Reverse the order for traversal
                            for (int i = edge.PolylinePoints.Count - 1; i >= 0; i--)
                            {
                                points.Add(new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, 0));
                            }
                        }
                        else
                        {
                            foreach (var pt in edge.PolylinePoints)
                            {
                                points.Add(new Vector3(pt.x, pt.y, 0));
                            }
                        }
                    }
                    else
                    {
                        // Direct line from node to node
                        var targetNode = matchesForward ? graphState.Nodes[toNode] : graphState.Nodes[fromNode];
                        points.Add(new Vector3(targetNode.Position.x, targetNode.Position.y, 0));
                    }

                    return points;
                }
            }

            return null;
        }

        /// <summary>
        /// Follows the current path toward the target.
        /// </summary>
        private void FollowPath()
        {
            if (worldPath.Count == 0 || currentWaypointIndex >= worldPath.Count)
            {
                // No path or reached end - recalculate
                RecalculatePathToTarget();

                // If we still don't have a path, move directly toward the visitor as a fallback
                if (worldPath.Count == 0 && targetVisitor != null)
                {
                    MoveDirectlyToward(targetVisitor.transform.position);
                }
                return;
            }

            // Get current waypoint in world space
            Vector3 waypointWorldPos = worldPath[currentWaypointIndex];

            // Move toward waypoint
            Vector3 direction = (waypointWorldPos - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // Update animation direction based on movement
            UpdateAnimationDirection(direction);

            // Check if reached waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, waypointWorldPos);
            if (distanceToWaypoint < waypointReachedDistance)
            {
                currentWaypointIndex++;

                // Recalculate path periodically to adjust for moving target
                if (currentWaypointIndex >= worldPath.Count)
                {
                    RecalculatePathToTarget();
                }
            }
        }

        private void MoveDirectlyToward(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;
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
        /// Sets the animator direction parameter and rotates model to face direction of motion.
        /// </summary>
        private void SetAnimatorDirection(int direction)
        {
            // Guard against redundant animator parameter writes
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }

            // Rotate the model to face the direction of motion
            if (!useProceduralSprite && animator != null && baseRotationCaptured)
            {
                // Determine which direction to use for rotation
                int rotationDirection = direction;
                if (rotationDirection == IdleDirection && lastDirection != IdleDirection)
                {
                    rotationDirection = lastDirection;
                }
                // Default to facing down if never moved
                if (rotationDirection == IdleDirection)
                {
                    rotationDirection = 2; // Down
                }

                // Calculate Z-axis rotation based on movement direction
                // This rotation is applied on top of the base rotation from the prefab
                float zRotation = 0f;
                switch (rotationDirection)
                {
                    case 1: // Up (+Y in world)
                        zRotation = 180f;
                        break;
                    case 2: // Down (-Y in world)
                        zRotation = 0f;
                        break;
                    case 3: // Left (-X in world)
                        zRotation = 90f;
                        break;
                    case 4: // Right (+X in world)
                        zRotation = -90f;
                        break;
                }

                // Apply directional rotation on top of the base rotation from the prefab
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, zRotation);
                animator.transform.localRotation = directionRotation * baseRotation;
            }
        }

        /// <summary>
        /// Checks if the Red Cap is in contact with a visitor.
        /// </summary>
        private void CheckForVisitorContact()
        {
            if (targetVisitor == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, targetVisitor.transform.position);

            if (distance <= contactRadius)
            {
                CaptureVisitor(targetVisitor);
            }
        }

        /// <summary>
        /// Captures a visitor, despawns them, and charges the essence penalty.
        /// </summary>
        /// <param name="visitor">The visitor to capture</param>
        private void CaptureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Calculate essence penalty
            int essencePenalty = Mathf.RoundToInt(baseEssencePerVisitor * essencePenaltyMultiplier);

            // Deduct essence from player
            if (gameController != null)
            {
                // Use TrySpendEssence to deduct, but we want to force the deduction even if not enough
                // So we'll use AddEssence with negative value to bypass the spending check
                gameController.AddEssence(-essencePenalty);
            }

            // Despawn the visitor
            Destroy(visitor.gameObject);

            // Clear target and find a new one
            targetVisitor = null;
            worldPath.Clear();
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

            // Draw path in world space
            if (worldPath != null && worldPath.Count > 0)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);

                for (int i = currentWaypointIndex; i < worldPath.Count; i++)
                {
                    Vector3 worldPos = worldPath[i];
                    Gizmos.DrawSphere(worldPos, 0.1f);

                    if (i > currentWaypointIndex)
                    {
                        Vector3 prevWorldPos = worldPath[i - 1];
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

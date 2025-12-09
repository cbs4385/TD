using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Props;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Kelpie - A water spirit that lures visitors toward Puka hazards.
    /// Moves near Puka hazards and attempts to lead visitors into danger.
    /// Works in tandem with PukaHazard to create deadly water-based traps.
    /// </summary>
    public class KelpieController : MonoBehaviour
    {
        #region Enums

        public enum KelpieState
        {
            Idle,
            PatrollingNearPuka,
            LuringVisitor
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed in units per second")]
        private float moveSpeed = 2.5f;

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [SerializeField]
        [Tooltip("Maximum distance from assigned Puka hazard")]
        private float maxDistanceFromPuka = 5f;

        [Header("Luring Settings")]
        [SerializeField]
        [Tooltip("How often to update visitor targeting (in seconds)")]
        private float targetUpdateInterval = 0.5f;

        [SerializeField]
        [Tooltip("Maximum distance to detect visitors for luring")]
        private float visitorDetectionRadius = 8f;

        [SerializeField]
        [Tooltip("How close the Kelpie tries to get to visitors when luring")]
        private float luringDistance = 1.5f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use procedural sprite if true, otherwise relies on child SpriteRenderer/Animator")]
        private bool useProceduralSprite = false;

        [SerializeField]
        [Tooltip("Color of the Kelpie (used for procedural sprite)")]
        private Color kelpieColor = new Color(0.1f, 0.6f, 0.8f, 1f); // Aqua blue

        [SerializeField]
        [Tooltip("Size of the Kelpie sprite")]
        private float kelpieSize = 1.0f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Animator parameter name for direction")]
        private string directionParameterName = "Direction";

        #endregion

        #region Private Fields

        private KelpieState state = KelpieState.Idle;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private PukaHazard assignedPuka;
        private List<Vector2Int> currentPath = new List<Vector2Int>();
        private int currentWaypointIndex;
        private VisitorControllerBase targetVisitor;
        private float targetUpdateTimer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private bool initialized;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Kelpie</summary>
        public KelpieState State => state;

        /// <summary>Gets the assigned Puka hazard</summary>
        public PukaHazard AssignedPuka => assignedPuka;

        /// <summary>Gets the current target visitor</summary>
        public VisitorControllerBase TargetVisitor => targetVisitor;

        #endregion

        #region Unity Lifecycle

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

            if (!initialized)
            {
                return;
            }

            // Find a Puka to work with if we don't have one
            if (assignedPuka == null)
            {
                AssignNearestPuka();
            }

            // Update behavior based on state
            switch (state)
            {
                case KelpieState.PatrollingNearPuka:
                    PatrolNearPuka();
                    CheckForNearbyVisitors();
                    break;

                case KelpieState.LuringVisitor:
                    LureVisitorToPuka();
                    break;
            }
        }

        private void TryInitialize()
        {
            if (initialized)
            {
                return;
            }

            Debug.Log($"[Kelpie] Attempting initialization...");

            // Find required components
            AcquireDependencies();
            animator = GetComponent<Animator>();

            if (gameController == null || mazeGridBehaviour == null)
            {
                Debug.LogWarning($"[Kelpie] Cannot initialize - missing dependencies. GameController: {gameController != null}, MazeGrid: {mazeGridBehaviour != null}");
                return;
            }

            // Create visual representation only if using procedural sprite
            if (useProceduralSprite)
            {
                CreateProceduralVisual();
                Debug.Log($"[Kelpie] Created procedural visual");
            }
            else
            {
                // Get existing SpriteRenderer if not using procedural
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                Debug.Log($"[Kelpie] Using existing sprite renderer: {spriteRenderer != null}");
            }

            // Initialize animator direction
            if (animator != null)
            {
                SetAnimatorDirection(IdleDirection);
                Debug.Log($"[Kelpie] Animator found and initialized");
            }
            else
            {
                Debug.LogWarning($"[Kelpie] No Animator component found");
            }

            // Start patrolling
            state = KelpieState.PatrollingNearPuka;
            initialized = true;
            Debug.Log($"[Kelpie] Initialization complete! State set to PatrollingNearPuka");
        }

        private bool AcquireDependencies()
        {
            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            return gameController != null && mazeGridBehaviour != null;
        }

        #endregion

        #region Puka Assignment

        /// <summary>
        /// Assigns the nearest Puka hazard to this Kelpie.
        /// </summary>
        private void AssignNearestPuka()
        {
            var allPukas = PukaHazard.All;

            if (allPukas.Count == 0)
            {
                return;
            }

            PukaHazard nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var puka in allPukas)
            {
                if (puka == null) continue;

                float distance = Vector3.Distance(transform.position, puka.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = puka;
                }
            }

            assignedPuka = nearest;
        }

        #endregion

        #region Patrolling Behavior

        /// <summary>
        /// Patrols in the vicinity of the assigned Puka hazard.
        /// </summary>
        private void PatrolNearPuka()
        {
            if (assignedPuka == null)
            {
                return;
            }

            // If we're far from the Puka, move back toward it
            float distanceToPuka = Vector3.Distance(transform.position, assignedPuka.transform.position);

            if (distanceToPuka > maxDistanceFromPuka)
            {
                // Move directly toward the Puka
                Vector3 direction = (assignedPuka.transform.position - transform.position).normalized;
                Vector3 movement = direction * moveSpeed * Time.deltaTime;
                transform.position += movement;
                UpdateAnimationDirection(direction);
            }
            else if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
            {
                // Pick a random patrol point near the Puka
                PickRandomPatrolPoint();
            }
            else
            {
                // Follow current patrol path
                FollowPath();
            }
        }

        /// <summary>
        /// Picks a random point near the Puka to patrol to.
        /// </summary>
        private void PickRandomPatrolPoint()
        {
            if (assignedPuka == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Get current position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                return;
            }
            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Pick a random point near the Puka
            Vector2Int pukaPos = assignedPuka.GridPosition;
            int radius = Mathf.FloorToInt(maxDistanceFromPuka);

            Vector2Int targetPos = new Vector2Int(
                pukaPos.x + Random.Range(-radius, radius + 1),
                pukaPos.y + Random.Range(-radius, radius + 1)
            );

            // Try to find a path to the target
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, targetPos, pathNodes))
            {
                currentPath.Clear();
                foreach (var node in pathNodes)
                {
                    currentPath.Add(new Vector2Int(node.x, node.y));
                }
                currentWaypointIndex = 0;
            }
        }

        #endregion

        #region Luring Behavior

        /// <summary>
        /// Checks for nearby visitors to lure toward the Puka.
        /// </summary>
        private void CheckForNearbyVisitors()
        {
            targetUpdateTimer -= Time.deltaTime;

            if (targetUpdateTimer <= 0f)
            {
                targetUpdateTimer = targetUpdateInterval;

                // Find all visitors in the scene
                VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

                Debug.Log($"[Kelpie] Scanning for visitors - Found {allVisitors.Length} visitors");

                if (allVisitors.Length == 0)
                {
                    targetVisitor = null;
                    return;
                }

                // Find closest visitor within detection radius
                VisitorControllerBase closestVisitor = null;
                float closestDistance = float.MaxValue;

                foreach (var visitor in allVisitors)
                {
                    if (visitor == null || visitor.gameObject == null)
                        continue;

                    float distance = Vector3.Distance(transform.position, visitor.transform.position);
                    if (distance <= visitorDetectionRadius && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestVisitor = visitor;
                    }
                }

                // If we found a visitor, switch to luring mode
                if (closestVisitor != null && closestVisitor != targetVisitor)
                {
                    Debug.Log($"[Kelpie] New target acquired: {closestVisitor.gameObject.name} at distance {closestDistance:F2}, switching to LuringVisitor state");
                    targetVisitor = closestVisitor;
                    state = KelpieState.LuringVisitor;
                }
            }
        }

        /// <summary>
        /// Lures the target visitor toward the Puka by positioning between visitor and safety.
        /// </summary>
        private void LureVisitorToPuka()
        {
            // Check if we still have a valid target
            if (targetVisitor == null || targetVisitor.gameObject == null)
            {
                Debug.Log($"[Kelpie] Lost target visitor, returning to patrol");
                targetVisitor = null;
                state = KelpieState.PatrollingNearPuka;
                currentPath.Clear();
                return;
            }

            // Check if visitor is too far away
            float distanceToVisitor = Vector3.Distance(transform.position, targetVisitor.transform.position);
            if (distanceToVisitor > visitorDetectionRadius * 1.5f)
            {
                // Lost the visitor, go back to patrolling
                Debug.Log($"[Kelpie] Visitor too far away ({distanceToVisitor:F2}), returning to patrol");
                targetVisitor = null;
                state = KelpieState.PatrollingNearPuka;
                currentPath.Clear();
                return;
            }

            // Position ourselves between the visitor and safety, leading them toward the Puka
            // Move toward a position near the visitor, but closer to the Puka
            Vector3 visitorPos = targetVisitor.transform.position;
            Vector3 pukaPos = assignedPuka != null ? assignedPuka.transform.position : transform.position;

            // Calculate a luring position between visitor and Puka
            Vector3 directionToPuka = (pukaPos - visitorPos).normalized;
            Vector3 luringPosition = visitorPos + directionToPuka * luringDistance;

            // Move toward the luring position
            Vector3 direction = (luringPosition - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;

            Debug.Log($"[Kelpie] Luring {targetVisitor.gameObject.name}, moving {movement.magnitude:F3} units, direction: {direction}");
            UpdateAnimationDirection(direction);
        }

        #endregion

        #region Pathfinding

        /// <summary>
        /// Follows the current path.
        /// </summary>
        private void FollowPath()
        {
            if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count)
            {
                return;
            }

            // Get current waypoint
            Vector2Int waypointGridPos = currentPath[currentWaypointIndex];
            Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(waypointGridPos.x, waypointGridPos.y);

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
            }
        }

        #endregion

        #region Animation

        /// <summary>
        /// Updates the animation direction based on movement vector.
        /// </summary>
        private void UpdateAnimationDirection(Vector3 movement)
        {
            if (animator == null)
            {
                Debug.LogWarning($"[Kelpie] Animator is null, cannot update animation direction");
                return;
            }

            // Calculate direction from movement
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
                Debug.Log($"[Kelpie] Setting animator direction to {direction} (0=idle, 1=up, 2=down, 3=left, 4=right)");
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }
        }

        #endregion

        #region Visual

        /// <summary>
        /// Creates the procedural visual representation of the Kelpie.
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
            spriteRenderer.color = kelpieColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(kelpieSize, kelpieSize, 1f);
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
            // Draw Kelpie position
            Gizmos.color = new Color(0.1f, 0.6f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw detection radius
            Gizmos.color = new Color(0.1f, 0.8f, 0.8f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, visitorDetectionRadius);

            // Draw line to assigned Puka
            if (assignedPuka != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                Gizmos.DrawLine(transform.position, assignedPuka.transform.position);

                // Draw max distance circle around Puka
                Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
                Gizmos.DrawWireSphere(assignedPuka.transform.position, maxDistanceFromPuka);
            }

            // Draw line to target visitor
            if (targetVisitor != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.7f);
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }

            // Draw patrol path
            if (currentPath != null && currentPath.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);

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
        }

        #endregion
    }
}

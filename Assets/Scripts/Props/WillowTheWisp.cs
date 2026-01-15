using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Willow-the-Wisp that wanders the maze and lures visitors to the Heart of the Maze.
    /// Uses world-space coordinates for all movement and detection.
    /// Wanders at 2x visitor speed when alone, slows to visitor speed when leading.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
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
        [Tooltip("Detection radius in world units")]
        private float detectionRadius = 8f;

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

        [Header("Model Settings")]
        [SerializeField]
        [Tooltip("Model prefab to spawn for the Willow-the-Wisp visuals")]
        private GameObject wispModelPrefab;

        [SerializeField]
        [Tooltip("Animator controller to apply to the spawned model")]
        private RuntimeAnimatorController wispController;

        [Header("3D Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing 3D point light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow (pastel blue)")]
        private Color glowColor = new Color(0.7f, 0.85f, 1f, 1f); // Pastel blue

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 3f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz")]
        private float glowFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 1.5f;

        [Header("Wandering Settings")]
        [SerializeField]
        [Tooltip("Time between wander direction changes (seconds)")]
        private float wanderDirectionChangeInterval = 2f;

        [SerializeField]
        [Tooltip("Distance to check ahead for walkability")]
        private float walkabilityCheckDistance = 0.5f;

        #endregion

        #region Private Fields

        private WispState state;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private SpriteRenderer spriteRenderer;
        private Rigidbody rb;
        private Animator animator;
        private Vector3 baseScale;
        private Vector3 initialScale;

        // Visitor detection and targeting
        private VisitorController targetVisitor;
        private float visitorScanTimer;

        // Wandering
        private Vector3 wanderDirection;
        private float wanderDirectionTimer;

        // Visitor being led
        private VisitorController followingVisitor;

        // Target destination (Heart of the Maze)
        private Vector3 heartWorldPosition;

        private const string DirectionParameter = "Direction";
        private GameObject modelInstance;
        private Light glowLight;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the wisp</summary>
        public WispState State => state;

        /// <summary>Gets whether this wisp is currently leading a visitor</summary>
        public bool IsLeading => state == WispState.Leading && followingVisitor != null;

        /// <summary>Gets the world position of this wisp</summary>
        public Vector3 WorldPosition => transform.position;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = WispState.Wandering;
            initialScale = transform.localScale;
            SetupModel();
            SetupSpriteRenderer();
            SetupColliders();
            SetupGlowLight();
            animator = GetComponentInChildren<Animator>(true);
            ApplyAnimatorController();
        }

        private void Start()
        {
            // Find references
            AcquireDependencies();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
                ApplyAnimatorController();
            }

            if (!AcquireDependencies())
            {
                return;
            }

            // Get heart position in world space
            if (mazeGridBehaviour != null)
            {
                heartWorldPosition = mazeGridBehaviour.HeartWorldPosition;
            }

            // Ensure wisp starts on a walkable position
            EnsureWalkablePosition();

            // Start wandering with a random direction
            PickRandomWanderDirection();
        }

        private void Update()
        {
            if (!AcquireDependencies())
            {
                return;
            }

            if (enablePulse && spriteRenderer != null)
            {
                UpdatePulse();
            }

            if (enableGlow && glowLight != null)
            {
                UpdateGlowPulse();
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

        private bool AcquireDependencies()
        {
            bool ready = true;

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (mazeGridBehaviour == null || gameController == null)
            {
                ready = false;
            }

            return ready;
        }

        #endregion

        #region Influence and Detection

        /// <summary>
        /// Checks if a position is within this wisp's detection radius.
        /// </summary>
        public bool IsPositionInDetectionRange(Vector3 worldPos)
        {
            float distance = Vector3.Distance(transform.position, worldPos);
            return distance <= detectionRadius;
        }

        /// <summary>
        /// Scans for visitors within the detection radius and picks the best target.
        /// Prioritizes the closest visitor with the least status effects.
        /// </summary>
        private bool IsVisitorChaseable(FaeMaze.Visitors.VisitorControllerBase.VisitorState state)
        {
            return state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Walking
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Fascinated
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Confused
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Frightened;
        }

        private void ScanForVisitors()
        {
            // Find all visitors in the scene
            VisitorController[] allVisitors = FindObjectsByType<VisitorController>(FindObjectsSortMode.None);
            if (allVisitors.Length == 0)
                return;

            // Filter visitors that are within detection radius
            List<VisitorController> candidateVisitors = new List<VisitorController>();
            foreach (var visitor in allVisitors)
            {
                // Skip if not walking
                if (!IsVisitorChaseable(visitor.State))
                    continue;

                // Skip if already following a wisp
                var followWisp = visitor.GetComponent<FollowWispBehavior>();
                if (followWisp != null && followWisp.IsFollowing)
                    continue;

                // Check if visitor is in detection range using world-space distance
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= detectionRadius)
                {
                    candidateVisitors.Add(visitor);
                }
            }

            if (candidateVisitors.Count == 0)
            {
                // No visitors in range, return to wandering if we were chasing
                if (state == WispState.Chasing)
                {
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
                // IsFascinated covers both fascination and entrancement (consolidated states)
                int statusCount = 0;
                if (visitor.IsFascinated) statusCount++;

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

        private void SetupSpriteRenderer()
        {
            // Check if we have a model (either embedded child or from wispModelPrefab)
            if (wispModelPrefab != null || modelInstance != null)
            {
                // Model-driven visuals; keep scale consistent and skip sprite setup
                baseScale = new Vector3(wispSize, wispSize, 1f);
                transform.localScale = baseScale;
                spriteRenderer = null;
                return;
            }

            spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
                gameObject,
                createProceduralSprite: useProceduralSprite,
                useSoftEdges: true,
                resolution: 32,
                pixelsPerUnit: 32
            );

            ApplySpriteSettings();
        }

        private void ApplySpriteSettings()
        {
            if (spriteRenderer == null)
            {
                baseScale = initialScale;
                transform.localScale = baseScale;
                return;
            }

            baseScale = useProceduralSprite
                ? new Vector3(wispSize, wispSize, 1f)
                : initialScale;

            ProceduralSpriteFactory.ApplySpriteSettings(
                spriteRenderer,
                wispColor,
                sortingOrder,
                applyScale: false
            );
            transform.localScale = baseScale;
        }

        private void SetupColliders()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
            }
            collider.radius = 0.4f;
            collider.isTrigger = true;
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        private void SetupGlowLight()
        {
            if (!enableGlow)
                return;

            // Check if we already have a Light component
            glowLight = GetComponent<Light>();
            if (glowLight == null)
            {
                glowLight = gameObject.AddComponent<Light>();
            }

            // Configure the 3D point light
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowRange;
            glowLight.intensity = glowMaxIntensity;

            // Set light to use realtime mode for URP
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;

            // Disable shadows for performance
            glowLight.shadows = LightShadows.None;
        }

        private void UpdateGlowPulse()
        {
            // Calculate pulsing intensity using sine wave
            float angle = Time.time * glowFrequency * 2f * Mathf.PI;

            // Map sin wave from [-1, 1] to [0, 1]
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;

            // Map to intensity range [min, max]
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);

            glowLight.intensity = intensity;
        }

        #endregion

        #region Wandering Behavior

        private void PickRandomWanderDirection()
        {
            // Pick a random cardinal direction
            int direction = Random.Range(0, 4);
            switch (direction)
            {
                case 0: wanderDirection = Vector3.up; break;
                case 1: wanderDirection = Vector3.down; break;
                case 2: wanderDirection = Vector3.left; break;
                case 3: wanderDirection = Vector3.right; break;
            }

            wanderDirectionTimer = 0f;
        }

        /// <summary>
        /// Ensures the wisp is positioned on a walkable location. If not, finds a nearby walkable position.
        /// </summary>
        private void EnsureWalkablePosition()
        {
            if (mazeGridBehaviour == null)
                return;

            // Check if current position is walkable
            if (mazeGridBehaviour.IsWalkableAtWorldPos(transform.position))
            {
                return;
            }

            // Current position is not walkable, find nearest walkable position
            Vector3 nearestWalkable = FindNearestWalkablePosition(transform.position);
            transform.position = nearestWalkable;
        }

        /// <summary>
        /// Finds the nearest walkable position using a spiral search pattern.
        /// </summary>
        private Vector3 FindNearestWalkablePosition(Vector3 startPos)
        {
            if (mazeGridBehaviour == null)
                return startPos;

            float searchStep = 1f;
            int maxRadius = 10;

            // Spiral search outward from start position
            for (int radius = 1; radius <= maxRadius; radius++)
            {
                float currentRadius = radius * searchStep;

                // Check cardinal directions at this radius
                Vector3[] offsets = new Vector3[]
                {
                    new Vector3(currentRadius, 0, 0),
                    new Vector3(-currentRadius, 0, 0),
                    new Vector3(0, currentRadius, 0),
                    new Vector3(0, -currentRadius, 0),
                    new Vector3(currentRadius, currentRadius, 0),
                    new Vector3(-currentRadius, currentRadius, 0),
                    new Vector3(currentRadius, -currentRadius, 0),
                    new Vector3(-currentRadius, -currentRadius, 0)
                };

                foreach (var offset in offsets)
                {
                    Vector3 checkPos = startPos + offset;
                    if (mazeGridBehaviour.IsWalkableAtWorldPos(checkPos))
                    {
                        return checkPos;
                    }
                }
            }

            // Fallback - return original position
            return startPos;
        }

        private void UpdateWandering()
        {
            // Update direction change timer
            wanderDirectionTimer += Time.deltaTime;
            if (wanderDirectionTimer >= wanderDirectionChangeInterval)
            {
                PickRandomWanderDirection();
            }

            // Check if we can move in the current direction
            Vector3 nextPosition = transform.position + wanderDirection * walkabilityCheckDistance;

            if (mazeGridBehaviour != null && !mazeGridBehaviour.IsWalkableAtWorldPos(nextPosition))
            {
                // Can't move in current direction, pick a new one
                PickRandomWanderDirection();
                return;
            }

            // Move in the wander direction
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                transform.position + wanderDirection,
                wanderSpeed * Time.deltaTime
            );

            UpdateAnimatorDirection(wanderDirection);

            if (rb != null)
            {
                rb.MovePosition(newPosition);
            }
            else
            {
                transform.position = newPosition;
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

            targetVisitor = visitor;
            state = WispState.Chasing;
        }

        /// <summary>
        /// Updates the chasing behavior: pursue the target visitor using direct movement.
        /// </summary>
        private void UpdateChasing()
        {
            // Check if target is still valid
            if (targetVisitor == null || !IsVisitorChaseable(targetVisitor.State))
            {
                ReturnToWandering();
                return;
            }

            // Check if visitor is already following a wisp
            var followWisp = targetVisitor.GetComponent<FollowWispBehavior>();
            if (followWisp != null && followWisp.IsFollowing)
            {
                ReturnToWandering();
                return;
            }

            // Calculate distance to target
            float distance = Vector3.Distance(transform.position, targetVisitor.transform.position);

            // Check if close enough to capture
            if (distance <= captureDistance)
            {
                CaptureVisitor(targetVisitor);
                return;
            }

            // Move toward the visitor
            Vector3 direction = (targetVisitor.transform.position - transform.position).normalized;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetVisitor.transform.position,
                chaseSpeed * Time.deltaTime
            );

            // Check walkability before moving
            if (mazeGridBehaviour == null || mazeGridBehaviour.IsWalkableAtWorldPos(newPosition))
            {
                UpdateAnimatorDirection(direction);

                if (rb != null)
                {
                    rb.MovePosition(newPosition);
                }
                else
                {
                    transform.position = newPosition;
                }
            }
            else
            {
                // Can't reach visitor directly, return to wandering
                ReturnToWandering();
            }
        }

        #endregion

        #region Leading Behavior

        private void CaptureVisitor(VisitorController visitor)
        {
            followingVisitor = visitor;
            state = WispState.Leading;

            // Notify visitor to follow this wisp
            var followWisp = visitor.gameObject.GetComponent<FollowWispBehavior>();
            if (followWisp == null)
            {
                followWisp = visitor.gameObject.AddComponent<FollowWispBehavior>();
            }
            followWisp.StartFollowing(this);

            // Update heart position
            if (mazeGridBehaviour != null)
            {
                heartWorldPosition = mazeGridBehaviour.HeartWorldPosition;
            }
        }

        private void UpdateLeading()
        {
            // Check if visitor is still following
            if (followingVisitor == null || followingVisitor.State == VisitorController.VisitorState.Consumed)
            {
                ReturnToWandering();
                return;
            }

            // Calculate distance to heart
            float distanceToHeart = Vector3.Distance(transform.position, heartWorldPosition);

            // Check if we've reached the heart
            if (distanceToHeart <= waypointReachedDistance)
            {
                // Reached heart - visitor should be consumed soon
                ReturnToWandering();
                return;
            }

            // Move toward the heart at visitor speed
            Vector3 direction = (heartWorldPosition - transform.position).normalized;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                heartWorldPosition,
                leadSpeed * Time.deltaTime
            );

            // Check walkability before moving
            if (mazeGridBehaviour == null || mazeGridBehaviour.IsWalkableAtWorldPos(newPosition))
            {
                UpdateAnimatorDirection(direction);

                if (rb != null)
                {
                    rb.MovePosition(newPosition);
                }
                else
                {
                    transform.position = newPosition;
                }
            }
        }

        private void ReturnToWandering()
        {
            state = WispState.Wandering;
            targetVisitor = null;
            followingVisitor = null;

            // Pick a new random wander direction
            PickRandomWanderDirection();
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

        private void SetupModel()
        {
            if (modelInstance != null)
            {
                return;
            }

            SpriteRenderer sprite = GetComponent<SpriteRenderer>();

            // First check if there's already an embedded model as a child (from prefab)
            if (transform.childCount > 0)
            {
                // Look for a child with an Animator component
                var childAnimator = GetComponentInChildren<Animator>(true);
                if (childAnimator != null && childAnimator.gameObject != gameObject)
                {
                    modelInstance = childAnimator.gameObject;
                    animator = childAnimator;

                    if (sprite != null)
                    {
                        sprite.enabled = false;
                    }
                    return;
                }
            }

            // No embedded model found, try to instantiate from wispModelPrefab
            if (wispModelPrefab == null)
            {
                return;
            }

            // Instantiate using non-generic method to handle FBX references properly
            var instantiatedObject = (GameObject)Instantiate((UnityEngine.Object)wispModelPrefab, transform);
            if (instantiatedObject == null)
            {
                return;
            }

            modelInstance = instantiatedObject;
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            var modelAnimator = modelInstance.GetComponentInChildren<Animator>(true);
            if (modelAnimator != null)
            {
                animator = modelAnimator;
            }

            if (sprite != null)
            {
                sprite.enabled = false;
            }
        }

        private void ApplyAnimatorController()
        {
            if (animator == null || wispController == null)
            {
                return;
            }

            animator.runtimeAnimatorController = wispController;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw detection radius
            Gizmos.color = new Color(0.9f, 1f, 0.4f, 0.1f); // Semi-transparent yellow-green
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Draw current movement direction
            if (state == WispState.Wandering && wanderDirection != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, transform.position + wanderDirection * 2f);
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

            // Draw line to heart when leading
            if (state == WispState.Leading && heartWorldPosition != Vector3.zero)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, heartWorldPosition);
            }
        }

        #endregion
    }
}

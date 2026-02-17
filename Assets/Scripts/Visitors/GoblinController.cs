using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.Systems;
using ForestMaze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Goblin - A hostile actor that hunts visitors and drains essence.
    /// Moves faster than visitors, actively stalks them, and penalizes the player
    /// when catching one.
    /// Uses world-space navigation to pursue targets.
    /// </summary>
    public class GoblinController : MonoBehaviour
    {
        #region Enums

        public enum GoblinState
        {
            Idle,
            Hunting,
            Killing,
            Fleeing
        }

        #endregion

        private string entityLabel;

        /// <summary>Unique label for this Goblin, used in floating text and console logs.</summary>
        public string EntityLabel => entityLabel;

        /// <summary>
        /// Assigns a unique label and shows it as floating text above the Goblin.
        /// </summary>
        public void AssignEntityLabel(string label)
        {
            entityLabel = label;
            FaeMaze.Visitors.VisitorStateIndicator.ShowLabel(transform, label, new Color(1f, 0.4f, 0.4f));
        }

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

        [Header("Killing Settings")]
        [SerializeField]
        [Tooltip("Duration of the killing animation in seconds")]
        private float killingDuration = 1.0f;

        [Header("Frightening Settings")]
        [SerializeField]
        [Tooltip("Radius within which visitors become frightened when they see the Goblin")]
        private float frightenRadius = 5.0f;

        [SerializeField]
        [Tooltip("How often to check for visitors to frighten (in seconds)")]
        private float frightenCheckInterval = 0.25f;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Animator parameter name for direction")]
        private string directionParameterName = "Direction";

        #endregion

        #region Private Fields

        private GoblinState state = GoblinState.Idle;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private List<Vector3> worldPath = new List<Vector3>();
        private int currentWaypointIndex;
        private VisitorControllerBase targetVisitor;
        private float targetUpdateTimer;
        private Animator animator;
        private float moveSpeed;
        private bool initialized;
        private Rigidbody rb3D;

        // Physics-based movement: calculate in Update, apply in FixedUpdate
        private Vector3 desiredPosition;
        private bool hasDesiredPosition;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;
        private bool hasDirectionParameter = false; // Whether animator has Direction parameter

        // Base rotation from prefab (captured at initialization)
        private Quaternion baseRotation;
        private bool baseRotationCaptured = false;

        // Killing state tracking
        private float killingTimer;
        private VisitorControllerBase killingTarget;

        // Frightening tracking
        private float frightenCheckTimer;

        // Starting essence reference (for flee threshold)
        private int startingEssence;

        // Path recalculation tracking
        private float pathRecalculationTimer;
        private const float PATH_RECALCULATION_INTERVAL = 0.25f;
        private Vector3 lastTargetPosition;

        // Pathfinding cost penalties (same as visitors)
        private const float HEART_NODE_PATHFINDING_PENALTY = 20f;

        // Difficulty tier for scaling (essence-threshold based)
        private int difficultyTier = 1;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Goblin</summary>
        public GoblinState State => state;

        /// <summary>Gets the current target visitor</summary>
        public VisitorControllerBase TargetVisitor => targetVisitor;

        /// <summary>Gets the calculated move speed</summary>
        public float MoveSpeed => moveSpeed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Calculate actual move speed (will be scaled by tier when set)
            moveSpeed = baseMoveSpeed * speedMultiplier;
        }

        /// <summary>
        /// Sets the difficulty tier for this Goblin and applies scaling.
        /// Call after instantiation.
        /// </summary>
        public void SetDifficultyTier(int tier)
        {
            difficultyTier = Mathf.Max(1, tier);
            ApplyDifficultyScaling();
        }

        /// <summary>
        /// Applies tier-based difficulty scaling to Goblin parameters.
        /// </summary>
        private void ApplyDifficultyScaling()
        {
            // Apply speed scaling (Goblin gets faster at higher tiers)
            float tierSpeedMultiplier = DifficultyScaling.GetGoblinSpeedMultiplier(difficultyTier);
            moveSpeed = baseMoveSpeed * speedMultiplier * tierSpeedMultiplier;
        }

        /// <summary>
        /// Gets the essence penalty for catching a visitor, scaled by difficulty tier.
        /// </summary>
        public int GetScaledEssencePenalty()
        {
            int basePenalty = Mathf.RoundToInt(baseEssencePerVisitor * essencePenaltyMultiplier);
            return DifficultyScaling.GetScaledEssencePenalty(difficultyTier, basePenalty);
        }

        private void OnEnable()
        {
            GoblinRegistry.Register(this);
        }

        private void OnDisable()
        {
            GoblinRegistry.Unregister(this);
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

            // Check essence threshold - flee if below starting essence
            if (state != GoblinState.Fleeing && state != GoblinState.Killing)
            {
                if (gameController != null && gameController.CurrentEssence < startingEssence)
                {
                    StartFleeing();
                }
            }

            switch (state)
            {
                case GoblinState.Hunting:
                    UpdateTargetSelection();
                    FollowPath();
                    CheckForVisitorContact();
                    CheckForVisitorsToFrighten();
                    break;

                case GoblinState.Killing:
                    UpdateKilling();
                    break;

                case GoblinState.Fleeing:
                    FollowPath();
                    CheckForReachedExit();
                    CheckForVisitorsToFrighten();
                    break;
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

            // Initialize animator direction
            if (animator != null)
            {
                // Capture the base rotation from the prefab before applying any directional rotation
                if (!baseRotationCaptured)
                {
                    baseRotation = animator.transform.localRotation;
                    baseRotationCaptured = true;
                }

                // Check if the animator has a Direction parameter
                hasDirectionParameter = false;
                foreach (var param in animator.parameters)
                {
                    if (param.name == directionParameterName && param.type == AnimatorControllerParameterType.Int)
                    {
                        hasDirectionParameter = true;
                        break;
                    }
                }

                if (hasDirectionParameter)
                {
                    SetAnimatorDirection(IdleDirection);
                }
            }

            // Capture starting essence for flee threshold
            startingEssence = GameSettings.StartingEssence;

            // Setup physics
            SetupPhysics();

            // Start hunting
            state = GoblinState.Hunting;
            initialized = true;
        }

        /// <summary>
        /// Sets up the Rigidbody for physics-based movement.
        /// </summary>
        private void SetupPhysics()
        {
            // Set layer to "Visitor" (layer 6) so Goblin doesn't push visitors via physics
            // Goblin catching visitors is handled via distance checks, not physics collisions
            gameObject.layer = 6;  // Visitor layer

            rb3D = GetComponent<Rigidbody>();
            if (rb3D == null)
            {
                rb3D = gameObject.AddComponent<Rigidbody>();
            }

            // Non-kinematic for physics collision response with solid colliders
            rb3D.isKinematic = false;
            rb3D.useGravity = false;
            rb3D.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            // PERFORMANCE: Use Discrete instead of ContinuousDynamic - continuous is extremely expensive
            // when many colliders are created at once (tongue bone colliders). Discrete is sufficient
            // since entities move slowly and we use MovePosition which handles collision correctly.
            rb3D.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb3D.interpolation = RigidbodyInterpolation.Interpolate;
            rb3D.sleepThreshold = 0f;  // Never sleep - we need MovePosition to always work

            // Add capsule collider if not present
            CapsuleCollider capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider == null)
            {
                capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
                capsuleCollider.radius = 0.15f;
                capsuleCollider.height = 1.0f;
                capsuleCollider.direction = 2;   // Z-axis
                capsuleCollider.center = new Vector3(0f, 0f, -0.5f);
                capsuleCollider.isTrigger = false;
            }
        }

        /// <summary>
        /// FixedUpdate handles physics-based movement.
        /// Movement is calculated in Update() and stored in desiredPosition,
        /// then applied here using MovePosition() which works correctly with non-kinematic Rigidbodies.
        /// </summary>
        private void FixedUpdate()
        {
            if (rb3D != null && hasDesiredPosition)
            {
                // Wake the Rigidbody in case it's sleeping
                if (rb3D.IsSleeping())
                {
                    rb3D.WakeUp();
                }

                rb3D.MovePosition(desiredPosition);
                transform.position = desiredPosition;
                hasDesiredPosition = false;
            }
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
        /// Also recalculates path if target has moved significantly.
        /// </summary>
        private void UpdateTargetSelection()
        {
            targetUpdateTimer -= Time.deltaTime;
            pathRecalculationTimer -= Time.deltaTime;

            bool needsTargetUpdate = targetUpdateTimer <= 0f;
            bool needsPathRecalculation = pathRecalculationTimer <= 0f;

            if (needsTargetUpdate)
            {
                targetUpdateTimer = targetUpdateInterval;

                // Find all visitors in the scene
                VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

                if (allVisitors.Length == 0)
                {
                    targetVisitor = null;
                    worldPath.Clear();
                    state = GoblinState.Idle;
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
                    needsPathRecalculation = false; // Already recalculated
                }
            }

            // Recalculate path if target has moved significantly
            if (needsPathRecalculation && targetVisitor != null)
            {
                pathRecalculationTimer = PATH_RECALCULATION_INTERVAL;

                float targetMovement = Vector3.Distance(targetVisitor.transform.position, lastTargetPosition);
                if (targetMovement > 1.0f) // Target moved more than 1 unit
                {
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

            // Store target position for movement tracking
            lastTargetPosition = targetVisitor.transform.position;

            // Use tile-based world-space pathfinding (same as visitors)
            worldPath = BuildWorldPath(transform.position, targetVisitor.transform.position);
            currentWaypointIndex = 0;
        }

        /// <summary>
        /// Builds a world-space path from start to end using A* through walkable tiles.
        /// Uses the shared MazePathfinding utility for consistent pathfinding behavior.
        /// </summary>
        private List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return new List<Vector3>();
            }

            return MazePathfinding.BuildWorldPath(
                mazeGridBehaviour.WorldSpaceMazeData,
                start,
                end,
                heartNodePenalty: HEART_NODE_PATHFINDING_PENALTY,
                penalizeHeartNode: true
            );
        }

        /// <summary>
        /// Follows the current path toward the target.
        /// Does NOT fall back to direct movement - stays on walkable tiles only.
        /// </summary>
        private void FollowPath()
        {
            if (worldPath.Count == 0 || currentWaypointIndex >= worldPath.Count)
            {
                // No path or reached end - recalculate
                RecalculatePathToTarget();

                // If we still don't have a path, wait (don't cut through walls)
                if (worldPath.Count == 0)
                {
                    return;
                }
            }

            // Get current waypoint in world space
            Vector3 waypointWorldPos = worldPath[currentWaypointIndex];

            // Move toward waypoint
            Vector3 direction = (waypointWorldPos - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            Vector3 newPosition = transform.position + movement;

            // Store position for physics to apply in FixedUpdate
            desiredPosition = newPosition;
            hasDesiredPosition = true;

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
            // Guard against redundant animator parameter writes and missing parameters
            if (animator != null && hasDirectionParameter && currentAnimatorDirection != direction)
            {
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }

            // Rotate the model to face the direction of motion
            if (animator != null && baseRotationCaptured)
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
                // This rotation is applied on top of the base rotation from the prefab.
                // Model's visual forward at rest points +Y, so Up = 0° and Down = 180°.
                float zRotation = 0f;
                switch (rotationDirection)
                {
                    case 1: // Up (+Y in world)
                        zRotation = 0f;
                        break;
                    case 2: // Down (-Y in world)
                        zRotation = 180f;
                        break;
                    case 3: // Left (-X in world)
                        zRotation = -90f;
                        break;
                    case 4: // Right (+X in world)
                        zRotation = 90f;
                        break;
                }

                // Apply directional rotation on top of the base rotation from the prefab
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, zRotation);
                animator.transform.localRotation = directionRotation * baseRotation;
            }
        }

        /// <summary>
        /// Checks if the Goblin is in contact with a visitor.
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
        /// Captures a visitor and starts the killing process.
        /// </summary>
        /// <param name="visitor">The visitor to capture</param>
        private void CaptureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Start killing state
            state = GoblinState.Killing;
            killingTarget = visitor;
            killingTimer = killingDuration;

            // Immobilize the visitor (daze them for the kill duration so they can't move)
            visitor.OnWitnessMazeGrowth(killingDuration + 1f);

            // Stop the Goblin's movement
            worldPath.Clear();
            targetVisitor = null;
        }

        /// <summary>
        /// Updates the killing state, completing the kill after the duration.
        /// </summary>
        private void UpdateKilling()
        {
            killingTimer -= Time.deltaTime;

            if (killingTimer <= 0f)
            {
                CompleteKill();
            }
        }

        /// <summary>
        /// Completes the kill, despawns the visitor, and charges the essence penalty.
        /// </summary>
        private void CompleteKill()
        {
            if (killingTarget != null)
            {
                // Calculate essence penalty (scaled by difficulty tier)
                int essencePenalty = GetScaledEssencePenalty();

                // Deduct essence from player
                if (gameController != null)
                {
                    gameController.AddEssence(-essencePenalty, EssenceSource.GoblinPenalty, $"Visitor killed (tier {difficultyTier})");
                }

                // Track visitor fate - negative essence since it's a penalty
                if (GameStatsTracker.Instance != null)
                {
                    GameStatsTracker.Instance.RecordVisitorFate(killingTarget.Archetype, VisitorFate.GoblinKill, -essencePenalty);
                }

                string victimLabel = killingTarget.EntityLabel ?? killingTarget.gameObject.name;
                if (!string.IsNullOrEmpty(entityLabel))
                {
                    GameEventLogger.LogEnemyEvent(entityLabel, "Killed", $"{victimLabel} penalty=-{essencePenalty}");
                }
                GameEventLogger.LogVisitorFate(victimLabel, "GoblinKill", -essencePenalty);

                // Despawn the visitor
                Destroy(killingTarget.gameObject);
                killingTarget = null;
            }

            // Return to hunting (or flee if essence is low)
            if (gameController != null && gameController.CurrentEssence < startingEssence)
            {
                StartFleeing();
            }
            else
            {
                state = GoblinState.Hunting;
            }
        }

        #endregion

        #region Fleeing Behavior

        /// <summary>
        /// Starts the fleeing state, causing the Goblin to path to an exit.
        /// </summary>
        private void StartFleeing()
        {
            if (state == GoblinState.Fleeing)
                return;

            state = GoblinState.Fleeing;
            targetVisitor = null;

            if (!string.IsNullOrEmpty(entityLabel))
            {
                GameEventLogger.LogEnemyEvent(entityLabel, "Fleeing", "essence low");
            }

            // Find and path to nearest exit
            BuildPathToNearestExit();
        }

        /// <summary>
        /// Builds a path to the nearest exit spawn point.
        /// </summary>
        private void BuildPathToNearestExit()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                worldPath.Clear();
                return;
            }

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            if (spawnPoints.Count == 0)
            {
                worldPath.Clear();
                return;
            }

            // Find nearest spawn point (exit)
            Vector3 nearestExit = Vector3.zero;
            float nearestDist = float.MaxValue;

            foreach (var kvp in spawnPoints)
            {
                float dist = Vector3.Distance(transform.position, kvp.Value);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestExit = kvp.Value;
                }
            }

            // Build path to exit
            worldPath = BuildWorldPath(transform.position, nearestExit);
            currentWaypointIndex = 0;
        }

        /// <summary>
        /// Checks if the Goblin has reached an exit and should despawn.
        /// </summary>
        private void CheckForReachedExit()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return;

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            foreach (var kvp in spawnPoints)
            {
                float dist = Vector3.Distance(transform.position, kvp.Value);
                if (dist < waypointReachedDistance * 2f)
                {
                    // Reached exit - despawn
                    Destroy(gameObject);
                    return;
                }
            }

            // If we've run out of path, rebuild it
            if (worldPath.Count == 0 || currentWaypointIndex >= worldPath.Count)
            {
                BuildPathToNearestExit();
            }
        }

        #endregion

        #region Frightening Visitors

        /// <summary>
        /// Checks for nearby visitors and frightens them.
        /// </summary>
        private void CheckForVisitorsToFrighten()
        {
            frightenCheckTimer -= Time.deltaTime;
            if (frightenCheckTimer > 0f)
                return;

            frightenCheckTimer = frightenCheckInterval;

            // Find all visitors in frighten radius
            VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in allVisitors)
            {
                if (visitor == null || visitor.gameObject == null)
                    continue;

                // Skip the visitor we're currently killing
                if (visitor == killingTarget)
                    continue;

                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= frightenRadius)
                {
                    // Frighten the visitor - they flee away from the Goblin
                    visitor.SetFrightened(transform.position);
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw Goblin position
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

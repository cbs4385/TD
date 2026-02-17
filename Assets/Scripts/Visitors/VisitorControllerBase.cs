using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.HeartPowers;
using FaeMaze.Roguelike;

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
            Fascinated,    // Entranced/hypnotized/mesmerized state (consolidated)
            Confused,      // Lost/wandering aimlessly state (consolidated)
            Frightened,
            Lured,         // Drawn toward the Heart by Murmuring Paths
            Dazed,         // Stunned from witnessing maze growth
            Grabbed,       // Being held by the heart tongue (not yet consumed)
            Consumed,
            Escaping       // Leaving the game (includes drowning)
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

        [SerializeField]
        [Tooltip("Enable verbose logging for visitor pathfinding diagnostics")]
        protected bool logVisitorPathfinding = false;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("3D model prefab to instantiate for this visitor (optional - visitor prefab may BE the model)")]
        protected GameObject modelPrefab;

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

        [Header("Goblin Detection")]
        [SerializeField]
        [Tooltip("Distance to detect Goblins and become frightened")]
        protected float goblinDetectionRadius = 5f;

        [SerializeField]
        [Tooltip("How often to check for nearby Goblins (seconds)")]
        protected float goblinDetectionInterval = 0.5f;

        [Header("Lost Mode Settings")]
        [SerializeField]
        [Tooltip("Minimum detour path length for Lost state")]
        protected int minLostDistance = 10;

        [SerializeField]
        [Tooltip("Maximum detour path length for Lost state")]
        protected int maxLostDistance = 20;

        [Header("Model Orientation")]
        [SerializeField]
        [Tooltip("Degrees to subtract from movement angle to correct model facing direction. " +
                 "Set to 180 for models whose mesh faces backwards without compensation (e.g. nature, lost, sleepy).")]
        protected float modelFacingOffsetDegrees;

        #endregion

        #region Protected Fields

        protected bool hasLoggedPathIssue;
        protected VisitorState state;
        protected string entityLabel;
        protected Animator animator;
        protected GameController gameController;
        protected MazeGridBehaviour mazeGridBehaviour;
        protected float speedMultiplier = 1f;
        protected int difficultyTier = 1;  // Difficulty tier when spawned (essence-threshold based)
        protected bool isElite;              // True if this is an elite visitor (from ChampionVisitors challenge)
        protected float eliteStatMultiplier = 1f;   // Stat multiplier for elite visitors
        protected float eliteRewardMultiplier = 1f; // Essence reward multiplier for elite visitors

        // Rendering
        protected GameObject modelInstance;
        protected Quaternion modelBaseRotation;
        protected bool modelBaseRotationCaptured;
        private bool hasLoggedFirstRotation;

        // 3D physics
        protected Rigidbody rb3D;

        /// <summary>
        /// Returns the actual physics position (rb3D.position) when available, falling back to
        /// transform.position. Use this for ALL movement computation to avoid divergence between
        /// the interpolated visual position (transform.position) and the physics position.
        /// With RigidbodyInterpolation.Interpolate, transform.position lags behind rb3D.position,
        /// causing systematic undershooting and micro-jitter when used for movement calculations.
        /// </summary>
        protected Vector3 PhysicsPosition => rb3D != null ? rb3D.position : transform.position;

        protected Vector3 originalDestination;
        protected bool usesSpawnMarkerDestination;

        protected bool isCalculatingPath;

        // Fascination state (for FaeLantern)
        protected bool isFascinated;
        protected Vector3 fascinationLanternPosition;
        protected bool hasReachedLantern;
        protected float fascinationTimer;
        protected FaeMaze.Props.FaeLantern currentFaeLantern;

        // FairyRing fascination state (circling behavior)
        protected FaeMaze.Props.FairyRing currentFairyRing;
        protected float fairyRingCircleAngle;
        protected float fairyRingFascinationTimer;
        protected bool fairyRingApproaching; // True while moving toward ring, false once circling
        protected FaeMaze.Props.FairyRing immuneToFairyRing; // Ring visitor is immune to (after fascination ends)
        protected const float FairyRingCircleRadius = 1.5f; // Distance from ring center to circle at (must be > 1.0 to be in walkable ring area)
        protected const float FairyRingCircleSpeed = 1.5f; // Radians per second

        // Visitor essence tracking (drained by props, visitor escapes when depleted)
        protected float currentEssence;  // Current essence remaining (starts at GetEssenceReward())
        protected float lanternEssenceAwardAccumulator;  // Accumulates fractional essence to award whole amounts
        protected float ringEssenceDrainPerSecond;    // Loaded from GameSettings
        protected float lanternEssenceDrainPerSecond; // Loaded from GameSettings
        protected float lanternEssenceAwardPerSecond; // Loaded from GameSettings


        // Essence-based opacity (visitors fade as essence drains)
        protected float startingEssence;
        protected Renderer[] cachedRenderers;
        protected bool renderersAreFading;
        protected float lastOpacityUpdate;
        private const float OPACITY_UPDATE_INTERVAL = 0.1f; // Update every 100ms
        private const float MIN_OPACITY = 0.15f; // Don't go fully invisible

        protected Vector3 initialScale;

        protected const float MovementEpsilonSqr = 0.0001f;
        protected const float StallLoggingDelaySeconds = 0.35f;
        protected const float StallRouteLogDumpDelaySeconds = 10f;

        protected int waypointsTraversedSinceSpawn;
        protected int lastLoggedWaypointIndex = -1;
        protected float stalledDuration;
        protected bool hasLoggedCurrentStall;
        protected bool hasMovedSignificantly;
        protected bool isCurrentlyStalled;
        protected bool hasDumpedStallRouteLog;

        // State tracking for path recalculation
        protected VisitorState previousState = VisitorState.Idle;
        protected bool pendingPathRecalculation;

        // State duration tracking (for timed states like Mesmerized, Lost, Frightened, etc.)
        protected VisitorState currentTimedState = VisitorState.Idle;
        protected float currentStateDuration;
        protected float currentStateTimer;
        protected bool isFrightened;
        protected bool isLured;
        protected bool isDazed;
        protected bool isTutorialVisitor;  // Tutorial visitors are immune to frightening and daze
        protected bool isFascinationImmune; // When true, visitor ignores lantern fascination

        // Misdirect state tracking
        protected bool isMisdirected;                // Currently being forced along a misdirect edge
        protected Vector3 misdirectOriginalDestination;  // Saved destination before misdirect override
        protected int misdirectEdgeIndex = -1;       // Edge index being traversed (for backtrack prevention)
        protected HashSet<int> completedMisdirectEdges = new HashSet<int>(); // Edges already traversed via misdirect (anti-backtrack)

        // Frightened state tracking
        protected Vector3 frightSourcePosition;  // Position of what frightened the visitor (to flee away from)
        protected int frightRecoveryNodeCount;   // Number of nodes visited while frightened (for accumulating recovery chance)
        protected float frightenedSpeedMultiplier;       // Loaded from GameSettings
        protected float frightenedRecoveryChancePerNode; // Loaded from GameSettings

        // Goblin detection tracking
        protected float goblinDetectionTimer;

        // Confusion state (simplified for world-space navigation)
        protected bool isConfused;
        protected bool confusionEnabled = true;  // Can be disabled by derived classes


        // World-space navigation
        protected Vector3 worldDestination;
        protected Vector3 originalSpawnPosition; // Where the visitor spawned from (to avoid returning there)
        protected List<Vector3> worldPath;
        protected int worldPathIndex;

        // Graph-based navigation (uses actual maze geometry instead of tiles)
        protected bool useGraphNavigation = false;  // Disabled: graph navigation can drift off walkable areas. Using tile-based instead.
        protected ForestMaze.GraphPath graphPath;
        protected int graphSegmentIndex;           // Current segment in graphPath
        protected float graphSegmentProgress;      // 0-1 progress within current segment
        protected List<Vector2> currentNodeArc;    // Waypoints for current node traversal
        protected int nodeArcIndex;                // Index within currentNodeArc

        // Movement diagnostic tracking - logs when visitor ends up off walkable tiles
        protected Vector3 lastValidPosition;
        protected string lastMoveCause = "Initial";
        protected bool wasOnWalkableTile = true;
        protected float offTileLogCooldown = 0f;
        protected const float OFF_TILE_LOG_INTERVAL = 1.0f; // Only log once per second to avoid spam

        // Stuck detection - logs when visitor is in Walking state but not moving
        protected Vector3 lastStuckCheckPosition;
        protected float stuckCheckTimer = 0f;
        protected float stuckDuration = 0f;
        protected const float STUCK_CHECK_INTERVAL = 0.5f; // Check every 0.5 seconds
        protected const float STUCK_THRESHOLD = 0.1f; // Must move at least 0.1 units per check
        protected const float STUCK_LOG_THRESHOLD = 2.0f; // Log after 2 seconds of being stuck

        // Navigation audit log - tracks events leading to current situation for debugging stuck visitors
        protected struct NavigationEvent
        {
            public float timestamp;        // Time.time when event occurred
            public string eventType;       // "Waypoint", "StateChange", "PathCalc", "Blocked", "MoveCause"
            public string details;         // Event-specific details
            public Vector3 position;       // Position when event occurred
            public int pathIndex;          // worldPathIndex at time of event

            public override string ToString()
            {
                return $"[{timestamp:F2}s] {eventType}: {details} @ ({position.x:F2}, {position.y:F2}) idx={pathIndex}";
            }
        }
        protected List<NavigationEvent> navigationAuditLog = new List<NavigationEvent>();
        protected const int MAX_AUDIT_LOG_ENTRIES = 50; // Keep last N entries to limit memory

        // Pathfinding cost penalties
        // Heart node penalty: visitors strongly prefer paths that don't cross through the heart center
        protected const float HEART_NODE_PATHFINDING_PENALTY = 20f;

        // Catmull-Rom spline smoothing for path following (similar to WillowTheWisp idle behavior)
        // Uses 4 control points: P0 influences entry tangent, P1 is current start, P2 is target, P3 influences exit tangent
        // DISABLED: Spline smoothing caused visitors to drift off walkable tiles and get stuck
        // Now using direct tile-to-tile movement via UpdateDirectWalking()
        protected bool useSplineSmoothing = false;
        protected float splineProgress = 0f;
        protected float splineSegmentLength = 1f;
        protected int splineStartIndex = 0; // Index in worldPath for P1 (current segment start)
        protected Vector3[] splineControlPoints = new Vector3[4]; // P0, P1, P2, P3
        protected bool splineInitialized = false;

        // Physics-based movement: calculate in Update, apply in FixedUpdate
        protected Vector3 desiredPosition;
        protected bool hasDesiredPosition = false;

        // Coasting: when no desiredPosition is pending, allow velocity to persist for
        // a short grace period before zeroing. This prevents micro-stutter from
        // Update/FixedUpdate timing misalignment.
        private int framesWithoutDesiredPosition = 0;


        // State aura visual feedback
        protected GameObject stateAuraObject;
        protected Light stateAuraLight;
        protected VisitorState lastAuraState = VisitorState.Idle;

        protected const float AURA_LIGHT_INTENSITY = 1.5f;
        protected const float AURA_LIGHT_RANGE = 2.0f;
        protected const float AURA_Z_OFFSET = -0.3f; // Above the visitor

        // Animator speed tracking
        protected VisitorState lastAnimatorSpeedState = VisitorState.Idle;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public virtual VisitorState State => state;

        /// <summary>Gets the current move speed</summary>
        public virtual float MoveSpeed => moveSpeed;

        /// <summary>Gets or sets the speed multiplier applied to movement</summary>
        public virtual float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }

        /// <summary>Gets whether this visitor is fascinated (entranced/mesmerized)</summary>
        public virtual bool IsFascinated => isFascinated;

        /// <summary>Gets the FaeLantern currently fascinating this visitor, or null if none</summary>
        public FaeMaze.Props.FaeLantern CurrentFaeLantern => currentFaeLantern;

        /// <summary>Gets the FairyRing currently fascinating this visitor, or null if none</summary>
        public FaeMaze.Props.FairyRing CurrentFairyRing => currentFairyRing;

        /// <summary>Gets the visitor's current essence (remaining value that can be drained)</summary>
        public float CurrentEssence => currentEssence;


        /// <summary>
        /// Deducts essence from the visitor. If essence drops to 0 or below, triggers OnEssenceDepleted.
        /// </summary>
        /// <param name="amount">Amount of essence to deduct</param>
        public void DeductEssence(float amount)
        {
            currentEssence -= amount;
            UpdateEssenceOpacity();
            if (currentEssence <= 0f)
            {
                currentEssence = 0f;
                OnEssenceDepleted();
            }
        }

        /// <summary>Gets the visitor's archetype (from config if available)</summary>
        public VisitorArchetype Archetype => config != null ? config.Archetype : VisitorArchetype.LanternDrunk;

        /// <summary>Gets the visitor's archetype configuration</summary>
        public VisitorArchetypeConfig ArchetypeConfig => config;

        /// <summary>Unique label for this visitor, used in floating text and console logs.</summary>
        public string EntityLabel => entityLabel;

        /// <summary>
        /// Assigns a unique label and shows it as floating text above the visitor.
        /// </summary>
        public void AssignEntityLabel(string label)
        {
            entityLabel = label;
            VisitorStateIndicator.ShowLabel(transform, label, new Color(0f, 1f, 1f));
        }

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            // Load settings from GameSettings (must be first, before any settings are used)
            LoadSettingsFromGameSettings();

            SetState(VisitorState.Idle, "Awake");
            initialScale = transform.localScale;

            // Look for Animator on this GameObject or children (for Blender imports)
            animator = GetComponentInChildren<Animator>();

            // Setup 3D model (visitor prefab is the model or has a model prefab)
            Setup3DModel();

            // Always setup state aura for visual feedback (regardless of model setup method)
            SetupStateAura();

            SetupPhysics();

            // Setup the Detect collider - always try to find it
            // The Detect child may be on the model instance OR directly on this visitor prefab
            SetupDetectCollider();

            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            hasMovedSignificantly = false;
            isCurrentlyStalled = false;
            hasDumpedStallRouteLog = false;

            // Apply archetype-specific settings if config is available
            if (config != null)
            {
                moveSpeed = config.BaseSpeed;
                mesmerizedDuration = config.InitialMesmerizedDuration;
                frightenedDuration = config.FrightenedDuration;
                minLostDistance = Mathf.RoundToInt(config.LostDetourMin);
                maxLostDistance = Mathf.RoundToInt(config.LostDetourMax);
            }

            // Apply player's speed setting (as multiplier, default 3f = 1x speed)
            moveSpeed *= (GameSettings.VisitorSpeed / 3f);

            // Initialize visitor's essence pool (drained by props)
            currentEssence = GetEssenceReward();
            startingEssence = currentEssence;
        }

        /// <summary>
        /// Loads configurable gameplay settings from GameSettings.
        /// Called once at Awake to cache values for performance.
        /// </summary>
        protected virtual void LoadSettingsFromGameSettings()
        {
            // Props - Essence Mechanics
            ringEssenceDrainPerSecond = GameSettings.RingEssenceDrainPerSecond;
            lanternEssenceDrainPerSecond = GameSettings.LanternEssenceDrainPerSecond;
            lanternEssenceAwardPerSecond = GameSettings.LanternEssenceAwardPerSecond;

            // Visitor Behavior - Frightened State
            frightenedSpeedMultiplier = GameSettings.FrightenedSpeedMultiplier;
            frightenedRecoveryChancePerNode = GameSettings.FrightenedRecoveryChance;
        }

        protected virtual void OnEnable()
        {
            // Register with the visitor registry for efficient lookups
            VisitorRegistry.Register(this);

            // Subscribe to frightening events
            HeartOfTheMaze.OnVisitorGrabbed += OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorGrabbedByGrasp += OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorPushedByGrasp += OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorConsumedByMaw += OnWitnessVisitorGrabbed;
        }

        protected virtual void OnDisable()
        {
            // Unregister from the visitor registry
            VisitorRegistry.Unregister(this);

            // Unsubscribe from frightening events
            HeartOfTheMaze.OnVisitorGrabbed -= OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorGrabbedByGrasp -= OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorPushedByGrasp -= OnWitnessVisitorGrabbed;
            HeartPowerEvents.OnVisitorConsumedByMaw -= OnWitnessVisitorGrabbed;
        }

        protected virtual void Update()
        {
            FrameProfiler.Checkpoint($"Visitor.Update.Start({name})");

            var frameTimer = System.Diagnostics.Stopwatch.StartNew();

            if (pendingPathRecalculation)
            {
                pendingPathRecalculation = false;
                UpdateDestinationIfExitRemoved();
                RecalculatePath();
                FrameProfiler.Checkpoint($"Visitor.PathRecalc({name})");
            }

            long pathRecalcMs = frameTimer.ElapsedMilliseconds;
            FrameProfiler.Checkpoint($"Visitor.AfterPathRecalc({name})");

            // Update state duration timers for timed states
            if (currentTimedState != VisitorState.Idle && currentStateDuration > 0)
            {
                currentStateTimer -= Time.deltaTime;
                if (currentStateTimer <= 0f)
                {
                    OnStateExpired(currentTimedState);
                }
            }


            // Check for nearby Goblins
            goblinDetectionTimer -= Time.deltaTime;
            if (goblinDetectionTimer <= 0f)
            {
                goblinDetectionTimer = goblinDetectionInterval;
                CheckForNearbyGoblins();
                CheckForFrighteningEvents();
            }

            FrameProfiler.Checkpoint($"Visitor.AfterGoblinCheck({name})");

            // CRITICAL: Don't process any movement when grabbed, consumed, or escaping
            // The heart controls position directly when grabbed
            if (state == VisitorState.Grabbed || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                UpdateStateAura();
                UpdateAnimatorSpeed();
                return;
            }

            // Handle FairyRing circling (takes priority over lantern fascination)
            if (currentFairyRing != null)
            {
                UpdateFairyRingCircling();
                return; // Don't process other movement while circling
            }

            FrameProfiler.Checkpoint($"Visitor.AfterRingCheck({name})");

            // Handle fascination timer (pause at lantern)
            if (isFascinated && hasReachedLantern)
            {
                // If the lantern was destroyed, end fascination immediately
                if (currentFaeLantern == null)
                {
                    EndFascination();
                }
                else if (fascinationTimer > 0)
                {
                    fascinationTimer -= Time.deltaTime;

                    // Drain visitor essence while at the lantern
                    currentEssence -= lanternEssenceDrainPerSecond * Time.deltaTime;

                    // Update visual opacity based on remaining essence
                    UpdateEssenceOpacity();

                    // Award essence to the player while visitor is at the lantern
                    if (GameController.Instance != null)
                    {
                        // Accumulate fractional essence and award whole amounts
                        // Apply Heart Form lantern effectiveness multiplier
                        float formLanternMultiplier = HeartFormManager.Instance?.GetLanternEffectivenessMultiplier() ?? 1.0f;
                        // Apply Mutation lantern award multiplier (Greedy Glow)
                        float mutationLanternMultiplier = PropMutationManager.Instance?.GetLanternAwardMultiplier() ?? 1.0f;
                        lanternEssenceAwardAccumulator += lanternEssenceAwardPerSecond * formLanternMultiplier * mutationLanternMultiplier * Time.deltaTime;
                        if (lanternEssenceAwardAccumulator >= 1f)
                        {
                            int wholeEssence = Mathf.FloorToInt(lanternEssenceAwardAccumulator);
                            lanternEssenceAwardAccumulator -= wholeEssence;
                            GameController.Instance.AddEssence(wholeEssence, EssenceSource.LanternFascination);
                        }
                    }

                    // Check if visitor's essence is depleted
                    if (currentEssence <= 0f)
                    {
                        currentEssence = 0f;
                        OnEssenceDepleted();
                        return;
                    }

                    // Update visuals before returning — state was set to Idle by EnterFaeInfluence
                    // but aura/animator won't update if we return early and skip the end of Update.
                    UpdateStateAura();
                    UpdateAnimatorSpeed();
                    return; // Don't move while fascinated timer is active
                }
                else
                {
                    // Fascination timer ended - resume wandering
                    EndFascination();
                }
            }

            FrameProfiler.Checkpoint($"Visitor.BeforeMovement({name})");

            if (IsMovementState(state))
            {
                // Don't update normal walking if being lured by a wisp
                var followWisp = GetComponent<FollowWispBehavior>();
                if (followWisp != null && followWisp.IsFollowing)
                {
                    // Wisp is controlling movement
                    UpdateStateAura();
                    UpdateAnimatorSpeed();
                    return;
                }

                FrameProfiler.Checkpoint($"Visitor.BeforeUpdateWalking({name})");


                // Only compute new movement if FixedUpdate has consumed the previous desiredPosition.
                // Without this gate, Update runs ~2x per FixedUpdate at high framerates, and the
                // second call overwrites the first desiredPosition — discarding one frame of movement.
                if (!isCalculatingPath && !hasDesiredPosition)
                {
                    UpdateWalking();
                }

                FrameProfiler.Checkpoint($"Visitor.AfterUpdateWalking({name})");
            }


            // Update state aura visual
            UpdateStateAura();

            // Update animator speed based on state
            UpdateAnimatorSpeed();

        }

        /// <summary>
        /// FixedUpdate handles physics-based movement using forces.
        /// NOTE: We do NOT try to block tongue colliders here - Unity physics cannot
        /// handle fast-moving kinematic colliders. HeartOfTheMaze controls visitor
        /// position directly once grabbed.
        /// </summary>
        protected virtual void FixedUpdate()
        {
            if (rb3D == null) return;

            // NOTE: We do NOT try to depenetrate from tongue colliders here.
            // Unity physics cannot reliably handle fast-moving kinematic colliders.
            // Instead, HeartOfTheMaze controls visitor position directly once grabbed.

            bool canMove = state == VisitorState.Walking || state == VisitorState.Confused ||
                           state == VisitorState.Frightened || state == VisitorState.Lured ||
                           state == VisitorState.Fascinated;

            // Don't apply movement force when grabbed - HeartOfTheMaze controls position
            if (state == VisitorState.Grabbed || state == VisitorState.Consumed)
            {
                hasDesiredPosition = false;
                if (rb3D.linearVelocity.sqrMagnitude > 0.001f)
                {
                    rb3D.linearVelocity = Vector3.zero;
                }
                return;
            }

            // Non-movement states (Idle, Dazed) must not drift from residual velocity.
            // With linearDamping=0, any leftover velocity persists forever unless zeroed.
            if (!canMove)
            {
                hasDesiredPosition = false;
                if (rb3D.linearVelocity.sqrMagnitude > 0.001f)
                {
                    rb3D.linearVelocity = Vector3.zero;
                }
                return;
            }


            if (hasDesiredPosition && canMove)
            {
                framesWithoutDesiredPosition = 0;

                // Calculate desired velocity to reach desiredPosition in one physics step
                Vector3 moveDir = desiredPosition - rb3D.position;
                moveDir.z = 0;  // Only move in XY plane
                float moveDist = moveDir.magnitude;

                if (moveDist > 0.01f)
                {
                    // Target velocity to reach desired position this timestep
                    Vector3 targetVelocity = moveDir / Time.fixedDeltaTime;

                    // Smooth direction transitions for small angle changes.
                    // Without this, VelocityChange creates instant direction snaps at every
                    // waypoint, causing visible jitter even with interpolation enabled.
                    Vector3 currentVel = rb3D.linearVelocity;
                    currentVel.z = 0f;

                    if (currentVel.sqrMagnitude > 0.1f && targetVelocity.sqrMagnitude > 0.1f)
                    {
                        float dot = Vector3.Dot(currentVel.normalized, targetVelocity.normalized);
                        if (dot > 0.7f)  // < ~45° angle change: smooth transition
                        {
                            // Blend direction while preserving target speed
                            float targetSpeed = targetVelocity.magnitude;
                            Vector3 blendedDir = Vector3.Slerp(currentVel.normalized, targetVelocity.normalized, 0.5f);
                            targetVelocity = blendedDir * targetSpeed;
                        }
                        // else: large turn (>45°), use full snap for responsive cornering
                    }

                    Vector3 deltaVelocity = targetVelocity - currentVel;
                    rb3D.AddForce(deltaVelocity, ForceMode.VelocityChange);


                }
                else
                {
                    // Close enough — stop
                    rb3D.linearVelocity = Vector3.zero;
                }

                hasDesiredPosition = false;
            }
            else if (canMove && rb3D.linearVelocity.sqrMagnitude > 0.001f)
            {
                framesWithoutDesiredPosition++;
                // Grace period: allow velocity to coast for 1 physics frame before stopping.
                // This prevents micro-stutter from Update/FixedUpdate timing misalignment
                // where FixedUpdate runs before Update has set a new desiredPosition.
                if (framesWithoutDesiredPosition >= 2)
                {
                    rb3D.linearVelocity = Vector3.zero;
                }
            }
            else
            {
                framesWithoutDesiredPosition = 0;
            }
        }

        /// <summary>
        /// Checks if the visitor is currently on a walkable tile and logs diagnostic info if not.
        /// </summary>
        protected void CheckAndLogWalkability()
        {
            // Decrease cooldown
            if (offTileLogCooldown > 0f)
            {
                offTileLogCooldown -= Time.fixedDeltaTime;
            }

            // Skip check if we don't have maze data yet
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            // Skip if grabbed or consumed - we're supposed to be moved by external forces
            if (state == VisitorState.Grabbed || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Skip if blocked by tongue or node center - visitor is being pushed by collision avoidance
            // These are expected to temporarily push visitors slightly off-path
            if (lastMoveCause != null && (lastMoveCause.Contains("BlockedByTongue") || lastMoveCause.Contains("BlockedByNodeCenter")))
            {
                return;
            }

            Vector3 currentPos = transform.position;

            // Use wider search to find the nearest walkable tile (same as movement validation)
            // But use a slightly stricter tolerance (0.6) for diagnosis - if we're further than this,
            // something is wrong even though movement allows up to 0.8
            ForestMaze.WorldSpaceTile nearestTile;
            bool isTrulyOnTile = IsPositionNearWalkableTile(currentPos, out nearestTile, 0.6f);
            float distanceToTile = nearestTile != null
                ? Vector2.Distance(new Vector2(currentPos.x, currentPos.y), nearestTile.Position)
                : float.MaxValue;
            bool isOnWalkable = nearestTile != null && nearestTile.Walkable;

            if (isTrulyOnTile)
            {
                // Update last valid position when on walkable tile
                lastValidPosition = currentPos;
                wasOnWalkableTile = true;
            }
            else
            {
                // We're off the walkable area
                // Recovery is disabled - it was causing infinite loops and making things worse.
                // Instead, we just log the issue for debugging. The root cause needs to be fixed
                // in the pathfinding/movement code, not papered over with teleportation.

                // Log diagnostic info (rate limited)
                if (offTileLogCooldown <= 0f)
                {
                    offTileLogCooldown = OFF_TILE_LOG_INTERVAL;

                }

                wasOnWalkableTile = false;
            }
        }

        /// <summary>
        /// Records the cause of the current movement for diagnostic tracking.
        /// Call this before setting desiredPosition.
        /// </summary>
        public void RecordMoveCause(string cause)
        {
            lastMoveCause = cause;
        }


        /// <summary>
        /// Records an event to the navigation audit log for debugging stuck visitors.
        /// </summary>
        /// <param name="eventType">Type of event: "Waypoint", "StateChange", "PathCalc", "Blocked", "MoveCause"</param>
        /// <param name="details">Event-specific details</param>
        protected void RecordNavigationEvent(string eventType, string details)
        {
            var navEvent = new NavigationEvent
            {
                timestamp = Time.time,
                eventType = eventType,
                details = details,
                position = transform.position,
                pathIndex = worldPathIndex
            };

            navigationAuditLog.Add(navEvent);

            // Trim old entries to keep memory bounded
            while (navigationAuditLog.Count > MAX_AUDIT_LOG_ENTRIES)
            {
                navigationAuditLog.RemoveAt(0);
            }
        }

        /// <summary>
        /// Formats the navigation audit log for display in stuck diagnostics.
        /// </summary>
        /// <returns>Formatted string of recent navigation events</returns>
        protected string FormatNavigationAuditLog()
        {
            if (navigationAuditLog.Count == 0)
            {
                return "  (no events recorded)";
            }

            StringBuilder sb = new StringBuilder();
            // Show last 15 entries for readability
            int startIdx = Mathf.Max(0, navigationAuditLog.Count - 15);
            for (int i = startIdx; i < navigationAuditLog.Count; i++)
            {
                sb.AppendLine($"  {navigationAuditLog[i]}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Checks if the visitor is stuck (in Walking/Confused state but not moving).
        /// Logs diagnostic info after being stuck for STUCK_LOG_THRESHOLD seconds.
        /// </summary>
        protected void CheckAndLogIfStuck()
        {
            // Only check if in a state where we expect movement
            if (state != VisitorState.Walking && state != VisitorState.Confused && state != VisitorState.Lured)
            {
                // Reset stuck tracking when not in a walking state
                stuckDuration = 0f;
                stuckCheckTimer = 0f;
                return;
            }

            stuckCheckTimer += Time.fixedDeltaTime;

            if (stuckCheckTimer >= STUCK_CHECK_INTERVAL)
            {
                stuckCheckTimer = 0f;

                Vector3 currentPos = transform.position;
                float distanceMoved = Vector3.Distance(currentPos, lastStuckCheckPosition);

                if (distanceMoved < STUCK_THRESHOLD)
                {
                    // Not moving enough - accumulate stuck time
                    stuckDuration += STUCK_CHECK_INTERVAL;

                    if (stuckDuration >= STUCK_LOG_THRESHOLD)
                    {
                        // If stuck due to node center or no valid movement, try to recalculate path
                        if (lastMoveCause != null &&
                            (lastMoveCause.Contains("BlockedByNodeCenter") ||
                             lastMoveCause.Contains("StayInPlace") ||
                             lastMoveCause.Contains("NoRecoveryTile")))
                        {
                            RecalculatePath();
                            splineInitialized = false; // Force spline reinitialization
                        }

                        // Reset to avoid spamming (will log again after another STUCK_LOG_THRESHOLD)
                        stuckDuration = 0f;
                    }
                }
                else
                {
                    // Moving normally - reset stuck tracking
                    stuckDuration = 0f;
                }

                lastStuckCheckPosition = currentPos;
            }
        }

        #endregion

        #region Helper Methods

        public void FlagPathRecalculation()
        {
            pendingPathRecalculation = true;
        }

        /// <summary>
        /// Applies a world-space offset to the visitor's destination.
        /// Used when maze coordinates are shifted.
        /// </summary>
        public void ApplyWorldOffset(Vector3 worldOffset)
        {
            if (worldOffset == Vector3.zero)
            {
                return;
            }

            originalDestination += worldOffset;

            if (fascinationLanternPosition != Vector3.zero)
            {
                fascinationLanternPosition += worldOffset;
            }

            if (worldPath != null)
            {
                for (int i = 0; i < worldPath.Count; i++)
                {
                    worldPath[i] += worldOffset;
                }
            }

            worldDestination += worldOffset;
        }

        private void UpdateDestinationIfExitRemoved()
        {
            // In world-space mode, navigate to heart edge if destination is invalid
            RetargetToHeart();
        }

        /// <summary>
        /// Retargets visitor to approach the heart.
        /// Destination is at the EDGE of the heart node (not the center).
        /// The HeartOfTheMaze will detect the visitor when they enter its detection radius.
        /// </summary>
        public void RetargetToHeart()
        {
            if (mazeGridBehaviour != null)
            {
                Vector3 heartApproachPos = GetHeartApproachPosition();
                SetWorldDestination(heartApproachPos);
            }
        }

        /// <summary>
        /// Gets a position at the edge of the heart node for the visitor to approach.
        /// This is calculated as a point NODE_RADIUS units from heart center in the
        /// direction from heart toward the visitor's current position.
        /// Visitors NEVER path to node centers - they approach the edge and are detected by triggers.
        /// </summary>
        protected Vector3 GetHeartApproachPosition()
        {
            if (mazeGridBehaviour == null)
                return PhysicsPosition;

            Vector3 heartCenter = mazeGridBehaviour.HeartWorldPosition;

            // Direction from heart to visitor (in XY plane)
            Vector2 visitorPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
            Vector2 heartPos2D = new Vector2(heartCenter.x, heartCenter.y);
            Vector2 dirFromHeart = (visitorPos2D - heartPos2D).normalized;

            // If visitor is at heart center, pick an arbitrary direction
            if (dirFromHeart.sqrMagnitude < 0.01f)
            {
                dirFromHeart = Vector2.up;
            }

            // Approach position is inside the heart's detection zone (detection radius = 2.5)
            // Use 2.0 units from center to ensure visitor is clearly inside detection range
            // The walkable ring is 1.0 to 3.0 units from center, so 2.0 is walkable
            const float APPROACH_RADIUS = 2.0f;

            Vector2 approachPos2D = heartPos2D + dirFromHeart * APPROACH_RADIUS;
            return new Vector3(approachPos2D.x, approachPos2D.y, heartCenter.z);
        }

        /// <summary>
        /// Sets the original spawn position for this visitor.
        /// Used to prevent visitors from retargeting back to where they spawned.
        /// </summary>
        public void SetOriginalSpawnPosition(Vector3 position)
        {
            originalSpawnPosition = position;
        }

        /// <summary>
        /// Marks this visitor as a tutorial visitor.
        /// Tutorial visitors are immune to being frightened, ensuring they walk into power zones.
        /// </summary>
        public void SetTutorialVisitor(bool isTutorial)
        {
            isTutorialVisitor = isTutorial;
        }

        /// <summary>
        /// Sets whether this visitor is immune to lantern/ring fascination.
        /// Used on tutorial visitors that need to walk through lantern areas without stopping.
        /// </summary>
        public void SetFascinationImmune(bool immune)
        {
            isFascinationImmune = immune;
        }

        /// <summary>
        /// Called when a visitor enters a misdirect sign trigger. Forces the visitor to
        /// abandon their current path and walk to the far sign position (other end of the edge).
        /// The original destination is saved so it can be restored after completing the edge.
        /// </summary>
        /// <param name="farSignPosition">Position of the sign at the other end of the misdirect edge</param>
        /// <param name="edgeIndex">Index of the misdirect edge (for backtrack prevention)</param>
        public void SetMisdirectDestination(Vector3 farSignPosition, int edgeIndex)
        {
            Debug.Log($"[Misdirect] SetMisdirectDestination: {name} forced to edge {edgeIndex} → ({farSignPosition.x:F1},{farSignPosition.y:F1}) from pos=({PhysicsPosition.x:F1},{PhysicsPosition.y:F1}) state={state} wasMisdirected={isMisdirected}");

            // Save original destination before overriding (only if not already misdirected)
            if (!isMisdirected)
            {
                misdirectOriginalDestination = originalDestination;
                Debug.Log($"[Misdirect] SetMisdirectDestination: {name} saved originalDest=({originalDestination.x:F1},{originalDestination.y:F1})");
            }
            isMisdirected = true;
            misdirectEdgeIndex = edgeIndex;

            // Override destination to the far sign — forces visitor to walk the entire edge
            worldDestination = farSignPosition;
            worldPath = BuildWorldPath(PhysicsPosition, farSignPosition);
            worldPathIndex = 0;
            ResetSplineState();

            Debug.Log($"[Misdirect] SetMisdirectDestination: {name} path built with {worldPath?.Count ?? 0} waypoints");
        }

        /// <summary>
        /// Called when a misdirected visitor reaches the far sign. Restores their original
        /// destination and recalculates the path with the misdirect edge heavily penalized
        /// to prevent backtracking through it.
        /// </summary>
        public void CompleteMisdirect()
        {
            if (!isMisdirected)
            {
                Debug.Log($"[Misdirect] CompleteMisdirect: {name} called but NOT misdirected — ignoring");
                return;
            }

            int completedEdgeIndex = misdirectEdgeIndex;
            Debug.Log($"[Misdirect] CompleteMisdirect: {name} RELEASED from misdirect edge {completedEdgeIndex} at pos=({PhysicsPosition.x:F1},{PhysicsPosition.y:F1}) state={state}");

            isMisdirected = false;
            misdirectEdgeIndex = -1;

            // Remember this edge so all future path calculations penalize it
            completedMisdirectEdges.Add(completedEdgeIndex);

            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return;

            // Build anti-backtrack multipliers: heavily penalize the misdirect edge.
            // Using 1000x penalty makes it a last resort — high enough to avoid
            // backtracking, but still allows pathfinding when no alternative exists.
            var antiBacktrackMultipliers = BuildAntiBacktrackMultipliers(completedEdgeIndex);

            // Determine destination: use original if valid, otherwise pick a random portal
            Vector3 destination = misdirectOriginalDestination;
            if (destination == Vector3.zero)
            {
                destination = PickRandomPortalPosition();
                Debug.Log($"[Misdirect] CompleteMisdirect: {name} no original dest — picked random portal at ({destination.x:F1},{destination.y:F1})");
            }
            else
            {
                Debug.Log($"[Misdirect] CompleteMisdirect: {name} restoring original dest=({destination.x:F1},{destination.y:F1})");
            }

            // Try to build a path that avoids the misdirect edge
            Debug.Log($"[Misdirect] CompleteMisdirect: {name} recalculating path from ({PhysicsPosition.x:F1},{PhysicsPosition.y:F1}) to ({destination.x:F1},{destination.y:F1}) with edge {completedEdgeIndex} penalized 1000x");
            worldPath = ForestMaze.MazePathfinding.BuildWorldPath(
                mazeGridBehaviour.WorldSpaceMazeData,
                PhysicsPosition,
                destination,
                heartNodePenalty: 0f,
                penalizeHeartNode: false,
                edgeCostMultipliers: antiBacktrackMultipliers
            );

            // If no path found (destination unreachable without the misdirect edge),
            // try each portal until we find one that's reachable
            if (worldPath == null || worldPath.Count == 0)
            {
                Debug.Log($"[Misdirect] CompleteMisdirect: {name} no path found — trying alternate portals");
                destination = FindReachablePortal(antiBacktrackMultipliers);
            }

            Debug.Log($"[Misdirect] CompleteMisdirect: {name} FREE to navigate — dest=({destination.x:F1},{destination.y:F1}) pathLen={worldPath?.Count ?? 0} completedEdges=[{string.Join(",", completedMisdirectEdges)}]");

            worldDestination = destination;
            originalDestination = destination;
            worldPathIndex = 0;
            ResetSplineState();

            // Ensure visitor is in a walking state
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Builds edge cost multipliers that block the completed misdirect edge
        /// while preserving any other active misdirect attraction.
        /// </summary>
        private Dictionary<int, float> BuildAntiBacktrackMultipliers(int blockedEdgeIndex)
        {
            var multipliers = new Dictionary<int, float>();

            var existingMultipliers = HeartPowerManager.Instance?.GetMisdirectEdgeCostMultipliers();
            if (existingMultipliers != null)
            {
                foreach (var kvp in existingMultipliers)
                    multipliers[kvp.Key] = kvp.Value;
            }

            // Heavily penalize the completed edge — 1000x makes it a last resort
            // but still allows pathfinding when no alternative route exists
            multipliers[blockedEdgeIndex] = 1000f;

            // Also penalize any previously completed misdirect edges
            foreach (int edgeIdx in completedMisdirectEdges)
                multipliers[edgeIdx] = 1000f;

            return multipliers;
        }

        /// <summary>
        /// Picks a random portal position from the available portals.
        /// Returns Vector3.zero if no portals exist.
        /// </summary>
        private Vector3 PickRandomPortalPosition()
        {
            var dynamicMaze = Object.FindFirstObjectByType<FaeMaze.Systems.DynamicMazeGrowth>();
            if (dynamicMaze == null) return Vector3.zero;

            var portalPositions = dynamicMaze.GetPortalPositions();
            if (portalPositions == null || portalPositions.Count == 0) return Vector3.zero;

            return portalPositions[Random.Range(0, portalPositions.Count)];
        }

        /// <summary>
        /// Tries each portal to find one reachable without crossing blocked edges.
        /// Sets worldPath to the first valid path found.
        /// Returns the portal position, or the current position if none found.
        /// </summary>
        private Vector3 FindReachablePortal(Dictionary<int, float> edgeCostMultipliers)
        {
            var dynamicMaze = Object.FindFirstObjectByType<FaeMaze.Systems.DynamicMazeGrowth>();
            if (dynamicMaze == null) return PhysicsPosition;

            var portalPositions = dynamicMaze.GetPortalPositions();
            if (portalPositions == null || portalPositions.Count == 0) return PhysicsPosition;

            // Shuffle to avoid always trying the same portal first
            for (int i = portalPositions.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = portalPositions[i];
                portalPositions[i] = portalPositions[j];
                portalPositions[j] = temp;
            }

            foreach (var portalPos in portalPositions)
            {
                var path = ForestMaze.MazePathfinding.BuildWorldPath(
                    mazeGridBehaviour.WorldSpaceMazeData,
                    PhysicsPosition,
                    portalPos,
                    heartNodePenalty: 0f,
                    penalizeHeartNode: false,
                    edgeCostMultipliers: edgeCostMultipliers
                );

                if (path != null && path.Count > 0)
                {
                    worldPath = path;
                    return portalPos;
                }
            }

            // No portal reachable — stay put
            return PhysicsPosition;
        }

        /// <summary>
        /// Whether this visitor is currently being forced along a misdirect edge.
        /// </summary>
        public bool IsMisdirected => isMisdirected;

        /// <summary>Whether this visitor is a tutorial visitor (immune to frightening/daze).</summary>
        public bool IsTutorialVisitor => isTutorialVisitor;

        /// <summary>Whether this visitor is immune to lantern fascination.</summary>
        public bool IsFascinationImmune => isFascinationImmune;

        /// <summary>Whether this visitor is currently lured by MurmuringPaths fog.</summary>
        public bool IsLured => isLured;

        /// <summary>
        /// Clears the misdirect state without rebuilding a path or restoring the original destination.
        /// Used when externally overriding the visitor's destination (e.g., tutorial assigning exit portals).
        /// </summary>
        public void ClearMisdirectState()
        {
            isMisdirected = false;
            misdirectEdgeIndex = -1;
            completedMisdirectEdges.Clear();
        }

        /// <summary>
        /// Retargets visitor to the nearest spawn point by walking distance from current position.
        /// Excludes the original spawn point to prevent visitors from going backwards.
        /// If no valid spawn points are available, falls back to the heart.
        /// </summary>
        public void RetargetToNearestSpawn()
        {
            RetargetToNearestSpawnFrom(PhysicsPosition);
        }

        /// <summary>
        /// Retargets visitor to the nearest spawn point by walking distance from a specified position.
        /// This is used when a destination portal is consumed - we want the nearest spawn from
        /// where the visitor was heading, not from where they currently are.
        /// Excludes the original spawn point to prevent visitors from going backwards.
        /// If no valid spawn points are available, falls back to the heart.
        /// </summary>
        /// <param name="fromPosition">The position to measure walking distance from</param>
        public void RetargetToNearestSpawnFrom(Vector3 fromPosition)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                RetargetToHeart();
                return;
            }

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            if (spawnPoints.Count == 0)
            {
                RetargetToHeart();
                return;
            }

            Vector3 bestSpawn = Vector3.zero;
            float shortestWalkingDist = float.MaxValue;
            int validSpawnsConsidered = 0;

            foreach (var kvp in spawnPoints)
            {
                Vector3 spawnPos = kvp.Value;

                // Skip spawn points too close to original spawn position (prevent going backwards)
                if (originalSpawnPosition != Vector3.zero)
                {
                    float distToOriginal = Vector3.Distance(spawnPos, originalSpawnPosition);
                    if (distToOriginal < 2f)
                    {
                        continue;
                    }
                }

                // Calculate walking distance by building a path from the specified position
                var testPath = BuildWorldPath(fromPosition, spawnPos);
                if (testPath == null || testPath.Count == 0)
                    continue;

                // Calculate total path length
                float pathLength = 0f;
                Vector3 prevPoint = fromPosition;
                foreach (var point in testPath)
                {
                    pathLength += Vector3.Distance(prevPoint, point);
                    prevPoint = point;
                }

                validSpawnsConsidered++;
                if (pathLength < shortestWalkingDist)
                {
                    shortestWalkingDist = pathLength;
                    bestSpawn = spawnPos;
                }
            }

            // If no valid spawn found (all too close to origin), fall back to heart
            if (bestSpawn == Vector3.zero || validSpawnsConsidered == 0)
            {
                RetargetToHeart();
                return;
            }

            SetWorldDestination(bestSpawn);
        }

        private string FormatWorldPath(List<Vector3> candidatePath)
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
                sb.Append(candidatePath[i].ToString("F1"));
            }

            return sb.ToString();
        }

        private bool ShouldLogVisitorPath()
        {
            return false;
        }

        protected void LogVisitorPath(string message)
        {
            // Logging disabled in world-space mode
        }

        protected bool LogVisitorPathWarning(string message)
        {
            return false;
        }

        private void UpdatePathLoggingOnMovement(Vector3 previousPosition, Vector3 currentPosition)
        {
            float deltaSqr = (currentPosition - previousPosition).sqrMagnitude;

            if (!hasMovedSignificantly && deltaSqr > MovementEpsilonSqr)
            {
                hasMovedSignificantly = true;
            }

            bool isStationary = deltaSqr <= MovementEpsilonSqr;

            if (isStationary)
            {
                isCurrentlyStalled = true;
                stalledDuration += Time.deltaTime;

                // If stalled for too long (2 seconds), force path recalculation
                // This helps visitors escape when they get stuck against colliders
                if (stalledDuration > 2.0f && !pendingPathRecalculation && worldPath != null && worldPath.Count > 0)
                {
                    pendingPathRecalculation = true;
                    stalledDuration = 0f;  // Reset to avoid repeated recalculations
                }
            }
            else
            {
                isCurrentlyStalled = false;
                hasMovedSignificantly = true;

                stalledDuration = 0f;
                hasLoggedCurrentStall = false;
                hasDumpedStallRouteLog = false;
            }
        }

        protected bool IsMovementState(VisitorState visitorState)
        {
            return visitorState == VisitorState.Walking
                || visitorState == VisitorState.Fascinated
                || visitorState == VisitorState.Confused
                || visitorState == VisitorState.Frightened
                || visitorState == VisitorState.Lured;
        }

        /// <summary>
        /// Checks if a position is near a walkable tile using a wider search radius than GetWorldSpaceTileAt.
        /// Returns true if position is within tolerance of a walkable tile.
        /// </summary>
        /// <param name="position">World position to check</param>
        /// <param name="nearestWalkable">Output: the nearest walkable tile found, or null</param>
        /// <param name="tolerance">How close the position must be to a walkable tile (default 0.8 units)</param>
        protected bool IsPositionNearWalkableTile(Vector3 position, out ForestMaze.WorldSpaceTile nearestWalkable, float tolerance = 0.8f)
        {
            nearestWalkable = null;

            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return false;
            }

            Vector2 pos2D = new Vector2(position.x, position.y);

            // Use a wider search radius (1.5 units) to find nearby tiles
            var nearbyTiles = mazeGridBehaviour.WorldSpaceMazeData.GetTilesNear(pos2D, 1.5f);

            float closestDist = float.MaxValue;
            ForestMaze.WorldSpaceTile closestWalkable = null;

            foreach (var tile in nearbyTiles)
            {
                if (!tile.Walkable) continue;

                float dist = Vector2.Distance(tile.Position, pos2D);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestWalkable = tile;
                }
            }

            nearestWalkable = closestWalkable;

            // Position is valid if it's within tolerance of a walkable tile
            return closestWalkable != null && closestDist <= tolerance;
        }

        /// <summary>
        /// Sets visitor state with logging. All state changes should go through this method
        /// so transitions and their reasons are captured in the console log.
        /// </summary>
        protected void SetState(VisitorState newState, string reason)
        {
            if (state == newState) return;
            VisitorState prev = state;
            state = newState;
            if (!string.IsNullOrEmpty(entityLabel))
            {
                GameEventLogger.LogVisitorStateChange(entityLabel, prev.ToString(), newState.ToString(), reason);
            }
        }

        // DEBUG: Set to true to disable all visitor states except Idle and Walking
        // This helps isolate pathfinding issues from state-related behavior
        protected static bool debugOnlyWalkingState = false;

        protected virtual void RefreshStateFromFlags()
        {
            // Terminal states cannot be overridden by UpdateState logic
            // Grabbed, Consumed, and Escaping are controlled externally (by heart, powers, etc.)
            if (state == VisitorState.Grabbed || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            VisitorState previousState = state;

            // DEBUG: Skip all state logic and just walk (does NOT affect terminal states above)
            if (debugOnlyWalkingState)
            {
                state = VisitorState.Walking;
                if (state != previousState)
                {
                    RecordNavigationEvent("StateChange", $"{previousState} -> {state} (debug override)");
                }
                return;
            }

            // Timed states take priority (in order of precedence)
            // Dazed has highest priority - visitor is stunned and cannot act
            if (isDazed)
            {
                state = VisitorState.Dazed;
            }
            else if (isFrightened)
            {
                state = VisitorState.Frightened;
            }
            else if (isLured)
            {
                // Lured has higher priority than Fascinated so players can rescue visitors from props
                state = VisitorState.Lured;
            }
            else if (isFascinated)
            {
                // Visitor stopped at lantern is Idle (not moving), not Fascinated (a movement state)
                state = hasReachedLantern ? VisitorState.Idle : VisitorState.Fascinated;
            }
            else if (isConfused && confusionEnabled)
            {
                state = VisitorState.Confused;
            }
            else
            {
                state = VisitorState.Walking;
            }

            // Log state change if it occurred
            if (state != previousState)
            {
                string flagReason = isDazed ? "isDazed" : isFrightened ? "isFrightened" : isLured ? "isLured"
                    : isFascinated ? (hasReachedLantern ? "isFascinated+atLantern" : "isFascinated")
                    : (isConfused && confusionEnabled) ? "isConfused" : "defaultWalking";
                if (!string.IsNullOrEmpty(entityLabel))
                {
                    GameEventLogger.LogVisitorStateChange(entityLabel, previousState.ToString(), state.ToString(), flagReason);
                }
                RecordNavigationEvent("StateChange", $"{previousState} -> {state}");
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
        /// Scaled by wave-based difficulty.
        /// </summary>
        public virtual float GetConfusionChance()
        {
            float baseChance = config != null ? config.ConfusionIntersectionChance : 0.25f;
            return DifficultyScaling.GetScaledConfusionChance(difficultyTier, baseChance);
        }

        /// <summary>
        /// Gets the frightened speed multiplier for this visitor.
        /// </summary>
        public virtual float GetFrightenedSpeedMultiplier()
        {
            return config != null ? config.FrightenedSpeedMultiplier : 1.3f;
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
        /// Scaled by tier-based difficulty (higher tiers = more essence from harder visitors).
        /// Elite visitors have additional reward multiplier.
        /// </summary>
        public virtual int GetEssenceReward()
        {
            int baseReward = config != null ? config.EssenceReward : 50;
            int scaledReward = DifficultyScaling.GetScaledEssenceReward(difficultyTier, baseReward);

            // Apply elite reward multiplier if this is an elite visitor
            if (isElite)
            {
                scaledReward = Mathf.RoundToInt(scaledReward * eliteRewardMultiplier);
            }

            return scaledReward;
        }

        /// <summary>
        /// Predicts the visitor's future position based on their current movement direction and speed.
        /// Used by the heart tongue to aim ahead of moving visitors.
        /// </summary>
        /// <param name="timeAhead">Seconds into the future to predict</param>
        /// <returns>Predicted world position</returns>
        public virtual Vector3 GetPredictedPosition(float timeAhead)
        {
            // If no path or at end of path, return current position
            if (worldPath == null || worldPath.Count == 0 || worldPathIndex >= worldPath.Count)
            {
                return PhysicsPosition;
            }

            // Calculate direction toward current waypoint
            Vector3 currentTarget = worldPath[worldPathIndex];
            Vector3 direction = (currentTarget - PhysicsPosition).normalized;

            // If direction is nearly zero (at waypoint), try next waypoint
            if (direction.sqrMagnitude < 0.001f && worldPathIndex + 1 < worldPath.Count)
            {
                currentTarget = worldPath[worldPathIndex + 1];
                direction = (currentTarget - PhysicsPosition).normalized;
            }

            // Calculate effective speed with all state modifiers
            float effectiveSpeed = moveSpeed * speedMultiplier;
            if (isFrightened)
            {
                effectiveSpeed *= frightenedSpeedMultiplier;
            }

            // Predict position
            return PhysicsPosition + direction * effectiveSpeed * timeAhead;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the visitor with a reference to the game controller.
        /// </summary>
        public virtual void Initialize(GameController controller)
        {
            gameController = controller;

            if (mazeGridBehaviour == null)
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

        /// <summary>
        /// Sets the difficulty tier for this visitor and applies scaling.
        /// Call after Initialize() and before the visitor starts moving.
        /// </summary>
        public virtual void SetDifficultyTier(int tier)
        {
            difficultyTier = Mathf.Max(1, tier);
            ApplyDifficultyScaling();
        }

        /// <summary>
        /// Applies tier-based difficulty scaling to visitor parameters.
        /// </summary>
        protected virtual void ApplyDifficultyScaling()
        {
            // Apply speed scaling (visitors get faster at higher tiers)
            float tierSpeedMultiplier = DifficultyScaling.GetVisitorSpeedMultiplier(difficultyTier);
            moveSpeed *= tierSpeedMultiplier;

            // Apply blessing speed multiplier (Patient Hunter makes visitors slower)
            float blessingSpeedMultiplier = BlessingManager.Instance?.GetVisitorSpeedMultiplier() ?? 1.0f;
            moveSpeed *= blessingSpeedMultiplier;
        }

        /// <summary>
        /// Gets the difficulty tier this visitor was spawned at.
        /// </summary>
        public int DifficultyTier => difficultyTier;

        /// <summary>
        /// Gets whether this visitor is an elite (from ChampionVisitors challenge).
        /// </summary>
        public bool IsElite => isElite;

        /// <summary>
        /// Sets this visitor as an elite with enhanced stats and rewards.
        /// Call after SetDifficultyTier().
        /// </summary>
        public virtual void SetElite(float statMultiplier, float rewardMultiplier)
        {
            isElite = true;
            eliteStatMultiplier = statMultiplier;
            eliteRewardMultiplier = rewardMultiplier;

            // Apply elite stat multiplier to speed (stacks with difficulty tier scaling)
            moveSpeed *= statMultiplier;

            // Scale up visual slightly to indicate elite status
            transform.localScale = initialScale * 1.15f;

        }

        #endregion

        #region Path Management

        /// <summary>
        /// Sets a world-space destination for the visitor to walk toward.
        /// Uses world-space navigation along the graph edges.
        /// </summary>
        public virtual void SetWorldDestination(Vector3 destination)
        {
            worldDestination = destination;
            originalDestination = destination;
            worldPathIndex = 0;

            // Reset state tracking
            waypointsTraversedSinceSpawn = 0;
            ResetDetourState();
            hasLoggedPathIssue = false;
            stalledDuration = 0f;
            hasLoggedCurrentStall = false;
            hasMovedSignificantly = false;
            isCurrentlyStalled = false;
            hasDumpedStallRouteLog = false;
            previousState = state;

            // Clear navigation audit log for new destination
            navigationAuditLog.Clear();

            // Build world-space path from current position to destination
            worldPath = BuildWorldPath(PhysicsPosition, destination);
            worldPathIndex = 0;
            ResetSplineState();

            // Record initial path in audit log
            int pathCount = worldPath?.Count ?? 0;
            string firstWp = pathCount > 0 ? $"({worldPath[0].x:F2}, {worldPath[0].y:F2})" : "none";
            RecordNavigationEvent("Init", $"SetDestination to ({destination.x:F2}, {destination.y:F2}), {pathCount} wps, first={firstWp}");

            if (worldPath != null && worldPath.Count > 0)
            {
                SetState(VisitorState.Walking, "SetDestination: path found");
            }
            else
            {
                // No path to assigned destination — try a random portal instead
                NavigateToRandomPortal("SetDestination: no path to original dest");
            }
        }

        /// <summary>
        /// Builds a world-space path from start to end using A* through walkable tiles.
        /// Uses actual tile positions from WorldSpaceMazeData to guarantee paths stay on walkable terrain.
        /// Path is simplified to remove intermediate collinear points for smooth movement.
        /// </summary>
        public virtual List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                // CRITICAL: Return empty list, not { end } - we can't validate if end is walkable
                // An empty path will cause the visitor to stay in place, which is safer than
                // potentially moving through unwalkable areas
                return new List<Vector3>();
            }

            var edgeCostMultipliers = HeartPowerManager.Instance?.GetMisdirectEdgeCostMultipliers();

            // Apply anti-backtrack penalty for misdirect edges this visitor already traversed
            if (completedMisdirectEdges.Count > 0)
            {
                var merged = new Dictionary<int, float>();
                if (edgeCostMultipliers != null)
                {
                    foreach (var kvp in edgeCostMultipliers)
                        merged[kvp.Key] = kvp.Value;
                }
                // Heavily penalize completed edges to prevent backtracking (1000x cost)
                // Using finite penalty instead of float.MaxValue so visitors can still
                // escape if the misdirect edge is the only route to any portal
                foreach (int edgeIdx in completedMisdirectEdges)
                    merged[edgeIdx] = 1000f;
                edgeCostMultipliers = merged;
            }

            var result = ForestMaze.MazePathfinding.BuildWorldPath(
                mazeGridBehaviour.WorldSpaceMazeData,
                start,
                end,
                heartNodePenalty: 0f,
                penalizeHeartNode: false, // Heart node centers are already unwalkable - no penalty needed
                edgeCostMultipliers: edgeCostMultipliers
            );

            // If pathfinding failed, return empty list - don't use unvalidated destination
            if (result.Count == 0)
            {
                return new List<Vector3>();
            }

            return result;
        }

        /// <summary>
        /// Builds a graph-based path using actual maze geometry (polyline paths and circular nodes).
        /// This produces smoother, more natural paths than tile-based pathfinding.
        /// </summary>
        /// <param name="start">Starting world position</param>
        /// <param name="end">Destination world position</param>
        /// <returns>A GraphPath with segments, or null if pathfinding fails</returns>
        protected virtual ForestMaze.GraphPath BuildGraphPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                return null;
            }

            Vector2 start2D = new Vector2(start.x, start.y);
            Vector2 end2D = new Vector2(end.x, end.y);

            var path = ForestMaze.GraphNavigation.FindPath(mazeGridBehaviour.ForestMapState, start2D, end2D);

            if (path == null || !path.IsValid)
            {
                return null;
            }

            return path;
        }

        /// <summary>
        /// Resets the graph navigation state for a new path.
        /// </summary>
        protected virtual void ResetGraphNavigationState()
        {
            graphSegmentIndex = 0;
            graphSegmentProgress = 0f;
            currentNodeArc = null;
            nodeArcIndex = 0;
        }

        /// <summary>
        /// Finds the nearest walkable tile to a position.
        /// Wrapper around MazePathfinding.FindNearestWalkableTile for internal use.
        /// </summary>
        private ForestMaze.WorldSpaceTile FindNearestWalkableTile(ForestMaze.WorldSpaceMazeData mazeData, Vector2 position)
        {
            return ForestMaze.MazePathfinding.FindNearestWalkableTile(mazeData, position);
        }

        /// <summary>
        /// Finds the nearest point on any edge polyline to the given position.
        /// </summary>
        private void FindNearestPointOnAnyEdge(ForestMaze.PlanarForestMazeGenerator.ForestMapState state,
            Vector2 position, out Vector2 nearestPoint, out ForestMaze.PlanarForestMazeGenerator.Edge nearestEdge,
            out int segmentIndex)
        {
            nearestPoint = position;
            nearestEdge = null;
            segmentIndex = -1;
            float minDist = float.MaxValue;

            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 closest = ClosestPointOnSegment(position, segStart, segEnd);
                    float dist = Vector2.Distance(position, closest);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearestPoint = closest;
                        nearestEdge = edge;
                        segmentIndex = i;
                    }
                }
            }

            // Also check node positions (visitor might be at a node)
            foreach (var node in state.Nodes)
            {
                float dist = Vector2.Distance(position, node.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPoint = node.Position;
                    // Keep nearestEdge as null to indicate we're at a node
                }
            }
        }

        /// <summary>
        /// Finds the closest point on a line segment to a given point.
        /// </summary>
        private Vector2 ClosestPointOnSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 seg = segEnd - segStart;
            float segLengthSq = seg.sqrMagnitude;
            if (segLengthSq < 0.0001f)
                return segStart;

            float t = Mathf.Clamp01(Vector2.Dot(point - segStart, seg) / segLengthSq);
            return segStart + t * seg;
        }

        /// <summary>
        /// Adds interpolated points along the ACTUAL edge polyline segment.
        /// Unlike straight-line interpolation, this follows the actual walkable path.
        /// </summary>
        private void AddInterpolatedPathAlongEdge(List<Vector3> path, Vector3 from, Vector3 to, float stepSize, float z,
            ForestMaze.PlanarForestMazeGenerator.ForestMapState graphState)
        {
            // Find the edge that contains both from and to points
            Vector2 from2D = new Vector2(from.x, from.y);
            Vector2 to2D = new Vector2(to.x, to.y);

            foreach (var edge in graphState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                // Check if both points are on this edge
                bool fromOnEdge = false;
                bool toOnEdge = false;
                int fromSegment = -1;
                int toSegment = -1;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    float distFrom = DistanceToSegment(from2D, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);
                    float distTo = DistanceToSegment(to2D, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);

                    if (distFrom < 1.5f && !fromOnEdge)
                    {
                        fromOnEdge = true;
                        fromSegment = i;
                    }
                    if (distTo < 1.5f && !toOnEdge)
                    {
                        toOnEdge = true;
                        toSegment = i;
                    }
                }

                if (fromOnEdge && toOnEdge && fromSegment >= 0 && toSegment >= 0)
                {
                    // Both points on same edge - follow the polyline between them
                    if (fromSegment <= toSegment)
                    {
                        for (int i = fromSegment + 1; i <= toSegment + 1 && i < edge.PolylinePoints.Count; i++)
                        {
                            path.Add(new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z));
                        }
                    }
                    else
                    {
                        for (int i = fromSegment; i >= toSegment && i >= 0; i--)
                        {
                            path.Add(new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, z));
                        }
                    }
                    return;
                }
            }

            // Fallback: just add the target point directly (should rarely happen)
            path.Add(to);
        }

        /// <summary>
        /// Distance from point to line segment.
        /// </summary>
        private float DistanceToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 seg = segEnd - segStart;
            float segLengthSq = seg.sqrMagnitude;
            if (segLengthSq < 0.0001f)
                return Vector2.Distance(point, segStart);
            float t = Mathf.Clamp01(Vector2.Dot(point - segStart, seg) / segLengthSq);
            Vector2 projection = segStart + t * seg;
            return Vector2.Distance(point, projection);
        }

        /// <summary>
        /// Finds the edge connecting two nodes.
        /// </summary>
        private ForestMaze.PlanarForestMazeGenerator.Edge FindConnectingEdge(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, int nodeA, int nodeB, out bool reverse)
        {
            reverse = false;
            foreach (var edge in state.Edges)
            {
                if (!edge.Partial && edge.NodeB.HasValue)
                {
                    if (edge.NodeA == nodeA && edge.NodeB.Value == nodeB)
                    {
                        reverse = false;
                        return edge;
                    }
                    else if (edge.NodeA == nodeB && edge.NodeB.Value == nodeA)
                    {
                        reverse = true;
                        return edge;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Adds edge polyline points to the path directly without interpolation.
        /// Uses exact polyline vertices which are guaranteed to be on walkable terrain.
        /// Also adds interpolated points along each segment for smooth movement.
        /// </summary>
        private void AddEdgePolylineToPath(List<Vector3> path, ForestMaze.PlanarForestMazeGenerator.Edge edge,
            Vector2 startPoint, bool towardNode, float z)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return;

            // Find which segment contains the start point
            int startSegment = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
            {
                Vector2 closest = ClosestPointOnSegment(startPoint, edge.PolylinePoints[i], edge.PolylinePoints[i + 1]);
                float dist = Vector2.Distance(startPoint, closest);
                if (dist < minDist)
                {
                    minDist = dist;
                    startSegment = i;
                }
            }

            float stepSize = 1f; // Interpolation step size in world units

            if (towardNode)
            {
                // Go from startPoint toward polyline[0] (the node)
                // Iterate through segments in reverse, adding interpolated points
                Vector3 lastPoint = path.Count > 0 ? path[path.Count - 1] : new Vector3(startPoint.x, startPoint.y, z);

                for (int seg = startSegment; seg >= 0; seg--)
                {
                    Vector2 segStart = edge.PolylinePoints[seg + 1]; // Going in reverse
                    Vector2 segEnd = edge.PolylinePoints[seg];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
            else
            {
                // Go from startPoint toward polyline[Count-1] (the endpoint)
                for (int seg = startSegment; seg < edge.PolylinePoints.Count - 1; seg++)
                {
                    Vector2 segStart = edge.PolylinePoints[seg];
                    Vector2 segEnd = edge.PolylinePoints[seg + 1];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Adds edge polyline points between two nodes with interpolation.
        /// Uses interpolated points along polyline segments for smooth movement.
        /// </summary>
        private void AddEdgePolylineToPathBetweenNodes(List<Vector3> path, ForestMaze.PlanarForestMazeGenerator.Edge edge,
            bool reverse, float z)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                return;

            float stepSize = 1f; // Interpolation step size in world units

            if (reverse)
            {
                // Go from last point to first (skip last since that's where we came from)
                for (int seg = edge.PolylinePoints.Count - 2; seg >= 0; seg--)
                {
                    Vector2 segStart = edge.PolylinePoints[seg + 1];
                    Vector2 segEnd = edge.PolylinePoints[seg];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    for (int j = 1; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
            else
            {
                // Go from first to last (skip first since that's where we came from)
                for (int seg = 0; seg < edge.PolylinePoints.Count - 1; seg++)
                {
                    Vector2 segStart = edge.PolylinePoints[seg];
                    Vector2 segEnd = edge.PolylinePoints[seg + 1];

                    float segLength = Vector2.Distance(segStart, segEnd);
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segLength / stepSize));

                    // Skip first point on first segment since we're already there
                    int startJ = (seg == 0) ? 1 : 0;
                    for (int j = startJ; j <= numSteps; j++)
                    {
                        float t = (float)j / numSteps;
                        Vector2 interpPoint = Vector2.Lerp(segStart, segEnd, t);
                        path.Add(new Vector3(interpPoint.x, interpPoint.y, z));
                    }
                }
            }
        }

        /// <summary>
        /// Finds the nearest node index to a position.
        /// </summary>
        protected int FindNearestNodeIndex(ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 position)
        {
            int nearest = -1;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < state.Nodes.Count; i++)
            {
                float dist = Vector2.Distance(state.Nodes[i].Position, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Checks if the visitor is currently at or near a node (intersection).
        /// Nodes are where paths meet and visitors can make wrong turn decisions.
        /// </summary>
        /// <param name="nodeProximityThreshold">Distance threshold to consider "at" a node (default 2.5 units)</param>
        /// <returns>True if visitor is near a node</returns>
        protected bool IsAtNode(float nodeProximityThreshold = 2.5f)
        {
            if (mazeGridBehaviour == null)
                return false;

            var graphState = mazeGridBehaviour.ForestMapState;
            if (graphState == null || graphState.Nodes.Count == 0)
                return false;

            Vector2 currentPos = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
            float closestDist = float.MaxValue;
            int closestNodeId = -1;

            for (int i = 0; i < graphState.Nodes.Count; i++)
            {
                var node = graphState.Nodes[i];
                float dist = Vector2.Distance(node.Position, currentPos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestNodeId = i;
                }

                if (dist <= nodeProximityThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a path through nodes using BFS.
        /// </summary>
        protected List<int> FindNodePath(ForestMaze.PlanarForestMazeGenerator.ForestMapState state, int startNode, int endNode)
        {
            if (startNode == endNode)
            {
                return new List<int> { startNode };
            }

            // Build adjacency from edges
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < state.Nodes.Count; i++)
            {
                adjacency[i] = new List<int>();
            }

            foreach (var edge in state.Edges)
            {
                if (!edge.Partial && edge.NodeB.HasValue) // Only use complete edges
                {
                    adjacency[edge.NodeA].Add(edge.NodeB.Value);
                    adjacency[edge.NodeB.Value].Add(edge.NodeA);
                }
            }

            // BFS
            var queue = new Queue<int>();
            var visited = new HashSet<int>();
            var parent = new Dictionary<int, int>();

            queue.Enqueue(startNode);
            visited.Add(startNode);
            parent[startNode] = -1;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                if (current == endNode)
                {
                    // Reconstruct path
                    var path = new List<int>();
                    int node = endNode;
                    while (node != -1)
                    {
                        path.Add(node);
                        node = parent[node];
                    }
                    path.Reverse();
                    return path;
                }

                foreach (int neighbor in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Finds a partial edge whose endpoint is near the given graph position.
        /// Used to determine if a visitor is at a spawn point on a frontier edge.
        /// </summary>
        protected ForestMaze.PlanarForestMazeGenerator.Edge FindPartialEdgeAtPosition(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 graphPosition)
        {
            const float EndpointTolerance = 2.0f; // Graph units tolerance for matching

            foreach (var edge in state.Edges)
            {
                if (!edge.Partial || edge.PolylinePoints.Count == 0)
                    continue;

                // Check if the position matches the endpoint of this partial edge
                Vector2 endpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                float distance = Vector2.Distance(endpoint, graphPosition);

                if (distance < EndpointTolerance)
                {
                    return edge;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a complete edge whose endpoint (either first or last polyline point) is near the given position.
        /// Used to handle visitors at positions that were previously partial edge endpoints but became complete after growth.
        /// </summary>
        protected ForestMaze.PlanarForestMazeGenerator.Edge FindCompleteEdgeAtEndpoint(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState state, Vector2 graphPosition)
        {
            const float EndpointTolerance = 2.0f; // Graph units tolerance for matching

            foreach (var edge in state.Edges)
            {
                // Only check complete edges (those with both NodeA and NodeB)
                if (edge.Partial || !edge.NodeB.HasValue || edge.PolylinePoints.Count == 0)
                    continue;

                // Check if position matches either endpoint of this complete edge
                Vector2 firstPoint = edge.PolylinePoints[0];
                Vector2 lastPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];

                float distToFirst = Vector2.Distance(firstPoint, graphPosition);
                float distToLast = Vector2.Distance(lastPoint, graphPosition);

                if (distToFirst < EndpointTolerance || distToLast < EndpointTolerance)
                {
                    return edge;
                }
            }

            return null;
        }

        #endregion

        #region Movement

        protected void UpdateAnimatorDirection(Vector2 movement)
        {
            // Apply smooth rotation for model to face movement direction
            // All rotation is around world Z axis only (XY plane movement)
            // IMPORTANT: Rotate the game object (parent), not the model child.
            // The model has its own local rotation that orients it correctly;
            // rotating the model's local Z would rotate around the model's tilted Z axis, not world Z.
            if (movement.sqrMagnitude > MovementEpsilonSqr)
            {
                // Calculate Z rotation angle for facing direction in world space
                float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;

                // Compensate for the model's base Z rotation so the visual forward
                // aligns with the movement direction. The model child has a local rotation
                // (e.g. Euler(0, -90, 90)) baked into the prefab — its Z component offsets
                // the visual facing from the parent's Z rotation.
                if (modelBaseRotationCaptured)
                {
                    angle -= modelBaseRotation.eulerAngles.z;
                }

                // Apply Z rotation to the game object (this visitor's transform)
                // This rotates around world Z, making the model face the direction of travel
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * 10f
                );

                if (!hasLoggedFirstRotation)
                {
                    hasLoggedFirstRotation = true;
                }
            }
        }

        /// <summary>
        /// Allows external behaviours (e.g., wisp-following) to update the animator's facing direction.
        /// </summary>
        /// <param name="movement">The movement or desired facing vector.</param>
        public void ApplyExternalAnimatorDirection(Vector2 movement)
        {
            UpdateAnimatorDirection(movement);
        }

        /// <summary>
        /// Sets the visitor's facing direction immediately (no Slerp interpolation).
        /// Compensates for the model's base rotation so the visual forward aligns with the given direction.
        /// Use this when setting facing from external code (e.g., HeartwardGrasp pushing tongue).
        /// </summary>
        public void SetFacingDirectionImmediate(Vector2 direction)
        {
            if (direction.sqrMagnitude <= MovementEpsilonSqr) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (modelBaseRotationCaptured)
            {
                angle -= modelBaseRotation.eulerAngles.z;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }


        protected virtual void UpdateWalking()
        {
            // All navigation is now world-space based
            UpdateWorldSpaceWalking();
        }

        /// <summary>
        /// Handles movement in world-space navigation mode.
        /// Walks along the world path toward the destination.
        /// Uses graph-based navigation (polylines) or tile-based fallback.
        /// </summary>
        protected virtual void UpdateWorldSpaceWalking()
        {
            // Check for valid path (graph or tile-based)
            bool hasGraphPath = graphPath != null && graphPath.IsValid && graphSegmentIndex < graphPath.Segments.Count;
            bool hasTilePath = worldPath != null && worldPath.Count > 0 && worldPathIndex < worldPath.Count;

            if (!hasGraphPath && !hasTilePath)
            {
                NavigateToRandomPortal("UpdateWalking: no valid path");
                return;
            }

            // Calculate effective speed with state-based multipliers
            float effectiveSpeed = moveSpeed * speedMultiplier;
            if (isFrightened)
            {
                effectiveSpeed *= frightenedSpeedMultiplier;
            }

            // Use graph navigation if enabled and path is valid
            if (useGraphNavigation && hasGraphPath)
            {
                UpdateGraphNavigation(effectiveSpeed);
            }
            else if (useSplineSmoothing && hasTilePath && worldPath.Count >= 2)
            {
                UpdateSplineWalking(effectiveSpeed);
            }
            else if (hasTilePath)
            {
                UpdateDirectWalking(effectiveSpeed);
            }

            // Note: Position is applied via FixedUpdate using velocity-based movement
            // This allows Unity physics to naturally block movement at solid colliders

            // Check if we've completed the path
            if (worldPathIndex >= worldPath.Count)
            {
                OnWorldSpacePathComplete();
            }
        }

        /// <summary>
        /// Updates movement using Catmull-Rom spline interpolation for smooth curves.
        /// Similar to WillowTheWisp idle behavior.
        /// </summary>
        protected virtual void UpdateSplineWalking(float effectiveSpeed)
        {
            // RECOVERY CHECK: If current position is far from any walkable tile, actively recover
            // This prevents continued drift when already off-path
            ForestMaze.WorldSpaceTile recoveryTile;
            bool needsRecovery = !IsPositionNearWalkableTile(PhysicsPosition, out recoveryTile, 0.8f);

            // If we didn't find a tile within normal range but need recovery, search wider
            if (needsRecovery && recoveryTile == null && mazeGridBehaviour?.WorldSpaceMazeData != null)
            {
                Vector2 pos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
                // Search up to 10 units - if we're further than that, something is very wrong
                var nearbyTiles = mazeGridBehaviour.WorldSpaceMazeData.GetTilesNear(pos2D, 10f);
                float closestDist = float.MaxValue;
                foreach (var tile in nearbyTiles)
                {
                    if (!tile.Walkable) continue;
                    float dist = Vector2.Distance(tile.Position, pos2D);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        recoveryTile = tile;
                    }
                }
            }

            if (needsRecovery && recoveryTile != null)
            {
                // We're far from walkable area but found a tile - move toward it
                Vector2 currentPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
                Vector2 pullDir = (recoveryTile.Position - currentPos2D).normalized;
                float pullDistance = effectiveSpeed * Time.fixedDeltaTime;
                Vector2 recoveryPos2D = currentPos2D + pullDir * pullDistance;
                Vector3 recoveryPos = new Vector3(recoveryPos2D.x, recoveryPos2D.y, PhysicsPosition.z);

                RecordMoveCause("SplineMovement_Recovery");
                desiredPosition = recoveryPos;
                hasDesiredPosition = true;
                return;
            }
            else if (needsRecovery && recoveryTile == null)
            {
                // No tile found at all - this is bad, but we can't do much
                // Stay in place and hope path recalculation fixes it
                RecordMoveCause("SplineMovement_NoRecoveryTile");
                return;
            }

            // Initialize spline if needed
            if (!splineInitialized)
            {
                InitializeSpline();
                if (!splineInitialized)
                {
                    // Fall back to direct walking if spline can't be initialized
                    UpdateDirectWalking(effectiveSpeed);
                    return;
                }
            }

            // DIVERGENCE CHECK: If visitor has moved away from the current P1 control point,
            // refresh control points to use actual position and reset progress.
            // This catches cases where:
            // 1. Physics pushed the visitor after spline was initialized
            // 2. Previous frame's FixedUpdate moved us to a blocked position
            // This check happens every frame to ensure spline always starts from actual position.
            {
                int p1Index = splineStartIndex;
                if (p1Index >= 0 && p1Index < worldPath.Count)
                {
                    Vector3 currentPos = PhysicsPosition;

                    // Compare with what we set P1 to - if visitor is far from P1, correct now
                    float p1Divergence = Vector3.Distance(
                        new Vector3(splineControlPoints[1].x, splineControlPoints[1].y, 0),
                        new Vector3(currentPos.x, currentPos.y, 0));

                    // If current position is far from P1 control point, refresh AND reset progress
                    // CRITICAL: Must reset progress to 0 because P1 now equals current position,
                    // so we want t=0 (which evaluates to P1) to match where we actually are
                    if (p1Divergence > 0.5f)
                    {
                        UpdateSplineControlPoints();
                        splineProgress = 0f; // CRITICAL: Reset progress so spline starts from current position
                    }
                }
            }

            // Store previous position for direction calculation
            Vector3 previousPosition = PhysicsPosition;

            // Progress along spline based on speed (normalized by segment length)
            float speedFactor = effectiveSpeed / Mathf.Max(0.1f, splineSegmentLength);
            float progressBefore = splineProgress;
            splineProgress += speedFactor * Time.fixedDeltaTime;

            // Handle segment completion and advancement
            while (splineProgress >= 1f && splineStartIndex < worldPath.Count - 1)
            {
                splineProgress -= 1f;
                waypointsTraversedSinceSpawn++;

                // Allow derived classes to handle detour logic at waypoints
                int previousIndex = splineStartIndex;
                HandleDetourAtWaypoint();

                // If path was changed by detour handling, reinitialize spline
                if (worldPath == null || worldPathIndex != previousIndex + 1)
                {
                    splineInitialized = false;
                    return;
                }

                // Advance to next segment
                AdvanceSplineSegment();

                // If spline is no longer valid, stop
                if (!splineInitialized)
                {
                    return;
                }
            }

            // If we've reached the end, smoothly arrive at final position
            if (splineStartIndex >= worldPath.Count - 1)
            {
                // Only snap if we're already very close; otherwise let the spline finish naturally
                Vector3 finalPos = worldPath[worldPath.Count - 1];
                finalPos.z = PhysicsPosition.z; // Preserve Z
                float distToFinal = Vector3.Distance(PhysicsPosition, finalPos);
                if (distToFinal < 0.1f)
                {
                    // Close enough - snap to avoid floating point drift
                    RecordMoveCause("SplineMovement_FinalSnap");
                    desiredPosition = finalPos;
                    hasDesiredPosition = true;
                }
                // Otherwise, the current position is fine - we've arrived
                worldPathIndex = worldPath.Count;
                splineInitialized = false;
                return;
            }

            // Calculate position on Catmull-Rom spline
            Vector3 newPosition = EvaluateCatmullRom(
                splineControlPoints[0],
                splineControlPoints[1],
                splineControlPoints[2],
                splineControlPoints[3],
                Mathf.Clamp01(splineProgress)
            );

            // Preserve Z position
            newPosition.z = PhysicsPosition.z;

            // SANITY CHECK: If the spline calculated a position more than 2 units from current position,
            // something is wrong with the spline control points. Reject the movement and stay in place.
            float distanceFromCurrent = Vector3.Distance(newPosition, PhysicsPosition);
            if (distanceFromCurrent > 2.0f)
            {
                // Force path recalculation
                RecordMoveCause("SplineMovement_InsanePosition");
                splineInitialized = false;
                hasDesiredPosition = false;

                // Immediately recalculate path to try to recover
                RecalculatePath();
                return;
            }

            // Check if movement is blocked by node center collider or tongue
            Vector3 blockedPos;
            bool wasBlocked = false;
            if (IsBlockedByNodeCenter(newPosition, out blockedPos))
            {
                // Blocked by node center - move to blocked position and DON'T advance spline
                RecordMoveCause("SplineMovement_BlockedByNodeCenter");
                desiredPosition = blockedPos;
                hasDesiredPosition = true;
                wasBlocked = true;

                // Refresh spline control points to use blocked position as new P1
                // This prevents sanity check failures on next frame
                UpdateSplineControlPoints();
            }
            // NOTE: Tongue collision handled by Unity physics - no manual check needed
            else
            {
                // Check if the new position would take us off the walkable area
                // Use a tiered approach:
                // - Within 0.5 units: safe, allow normal spline movement
                // - 0.5-0.8 units: constrain toward tile center to prevent drift
                // - Beyond 0.8 units: use direct movement fallback

                ForestMaze.WorldSpaceTile nearestTile;
                bool isNearWalkable = IsPositionNearWalkableTile(newPosition, out nearestTile, 1.0f);

                if (!isNearWalkable || nearestTile == null)
                {
                    // Too far from any walkable tile - use direct movement toward next waypoint
                    if (worldPath != null && splineStartIndex + 1 < worldPath.Count)
                    {
                        Vector3 targetWaypoint = worldPath[splineStartIndex + 1];
                        Vector3 directDir = (targetWaypoint - previousPosition).normalized;
                        Vector3 candidatePosition = previousPosition + directDir * effectiveSpeed * Time.fixedDeltaTime;
                        candidatePosition.z = PhysicsPosition.z;

                        // Validate the direct fallback position too
                        ForestMaze.WorldSpaceTile candidateTile;
                        bool candidateNearWalkable = IsPositionNearWalkableTile(candidatePosition, out candidateTile, 0.8f);

                        if (candidateNearWalkable)
                        {
                            newPosition = candidatePosition;
                            RecordMoveCause("SplineMovement_DirectFallback");
                        }
                        else
                        {
                            newPosition = previousPosition;
                            RecordMoveCause("SplineMovement_StayInPlace");
                            wasBlocked = true;
                        }
                    }
                    else
                    {
                        newPosition = previousPosition;
                        RecordMoveCause("SplineMovement_NoValidTarget");
                    }
                }
                else
                {
                    // We found a nearby walkable tile - check distance and constrain if drifting
                    float distToTile = Vector2.Distance(
                        new Vector2(newPosition.x, newPosition.y),
                        nearestTile.Position);

                    if (distToTile <= 0.5f)
                    {
                        // Safe zone - allow normal spline movement
                        RecordMoveCause("SplineMovement_Normal");
                    }
                    else if (distToTile <= 0.8f)
                    {
                        // Drift zone - constrain toward tile center to prevent further drift
                        // Blend position toward the tile center based on how far we've drifted
                        float driftAmount = (distToTile - 0.5f) / 0.3f; // 0 at 0.5, 1 at 0.8
                        Vector2 tileCenter = nearestTile.Position;
                        Vector2 currentPos2D = new Vector2(newPosition.x, newPosition.y);

                        // Pull toward tile center proportionally to drift
                        Vector2 constrainedPos2D = Vector2.Lerp(currentPos2D, tileCenter, driftAmount * 0.5f);
                        newPosition = new Vector3(constrainedPos2D.x, constrainedPos2D.y, newPosition.z);
                        RecordMoveCause("SplineMovement_Constrained");
                    }
                    else
                    {
                        // Beyond safe drift zone - pull strongly toward tile center
                        Vector2 tileCenter = nearestTile.Position;
                        Vector2 currentPos2D = new Vector2(newPosition.x, newPosition.y);
                        Vector2 pullDir = (tileCenter - currentPos2D).normalized;

                        // Move toward tile center at movement speed
                        float pullDistance = Mathf.Min(effectiveSpeed * Time.fixedDeltaTime, distToTile - 0.4f);
                        Vector2 constrainedPos2D = currentPos2D + pullDir * pullDistance;
                        newPosition = new Vector3(constrainedPos2D.x, constrainedPos2D.y, newPosition.z);
                        RecordMoveCause("SplineMovement_Pulled");
                    }
                }
                desiredPosition = newPosition;
            }
            hasDesiredPosition = true;

            // If blocked, revert the spline progress so we don't skip ahead
            // This prevents the visitor from "teleporting" when the block clears
            if (wasBlocked)
            {
                // speedFactor was already calculated above
                splineProgress -= speedFactor * Time.fixedDeltaTime;
                if (splineProgress < 0f) splineProgress = 0f;
            }

            // Update facing direction based on movement
            // Check magnitude BEFORE normalizing to avoid noise when movement is tiny
            Vector3 moveDirection = newPosition - previousPosition;

            if (moveDirection.sqrMagnitude > 0.0001f)  // Minimum movement threshold
            {
                Vector2 facingDirection = new Vector2(moveDirection.x, moveDirection.y).normalized;
                UpdateAnimatorDirection(facingDirection);
            }

            // Sync worldPathIndex with spline progress for compatibility
            worldPathIndex = splineStartIndex;
        }

        /// <summary>
        /// Updates movement using direct waypoint-to-waypoint walking (original behavior).
        /// Used as fallback when spline smoothing is disabled or not possible.
        /// </summary>
        protected virtual void UpdateDirectWalking(float effectiveSpeed)
        {
            // RECOVERY CHECK: If current position is far from any walkable tile, actively recover
            ForestMaze.WorldSpaceTile recoveryTile;
            bool needsRecovery = !IsPositionNearWalkableTile(PhysicsPosition, out recoveryTile, 0.8f);
            if (needsRecovery && recoveryTile != null)
            {
                // We're far from walkable area but found a tile - move toward it
                Vector2 currentPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
                Vector2 pullDir = (recoveryTile.Position - currentPos2D).normalized;
                float pullDistance = effectiveSpeed * Time.fixedDeltaTime;
                Vector2 recoveryPos2D = currentPos2D + pullDir * pullDistance;
                Vector3 recoveryPos = new Vector3(recoveryPos2D.x, recoveryPos2D.y, PhysicsPosition.z);

                RecordMoveCause("DirectWalking_Recovery");
                desiredPosition = recoveryPos;
                hasDesiredPosition = true;
                return;
            }
            else if (needsRecovery && recoveryTile == null)
            {
                // No tile found at all - stay in place
                RecordMoveCause("DirectWalking_NoRecoveryTile");
                return;
            }

            // Use fixedDeltaTime because UpdateWalking is gated to run once per FixedUpdate cycle.
            // Using Time.deltaTime here would under-calculate distance (render frame ~0.01s vs physics step 0.02s).
            float remainingDistance = effectiveSpeed * Time.fixedDeltaTime;
            Vector3 finalPosition = PhysicsPosition;

            // Move continuously, consuming distance across multiple waypoints if needed
            while (remainingDistance > 0f && worldPathIndex < worldPath.Count)
            {
                Vector3 targetWorldPos = worldPath[worldPathIndex];
                float distanceToTarget = Vector3.Distance(finalPosition, targetWorldPos);

                if (distanceToTarget <= remainingDistance)
                {
                    // Check if movement to waypoint is blocked by node center or tongue
                    Vector3 blockedPos;
                    if (IsBlockedByNodeCenter(targetWorldPos, out blockedPos))
                    {
                        // If we're very close to the blocked waypoint (< 0.1 units), skip it to avoid getting stuck
                        // This happens when a path waypoint is inside or very near a node center
                        if (distanceToTarget < 0.1f && worldPathIndex + 1 < worldPath.Count)
                        {
                            RecordNavigationEvent("Skip", $"Skipping blocked wp[{worldPathIndex}] (dist={distanceToTarget:F3}), advancing to wp[{worldPathIndex + 1}]");
                            worldPathIndex++;
                            continue; // Try next waypoint
                        }
                        // Blocked by node center - move to blocked position and stop
                        RecordNavigationEvent("Blocked", $"NodeCenter blocked wp[{worldPathIndex}], pushed to ({blockedPos.x:F2}, {blockedPos.y:F2})");
                        finalPosition = blockedPos;
                        remainingDistance = 0f;
                        break;
                    }
                    // NOTE: Tongue collision handled by Unity physics - no manual check needed
                    // We can reach (or pass) this waypoint - move to it and continue
                    remainingDistance -= distanceToTarget;
                    finalPosition = targetWorldPos;
                    waypointsTraversedSinceSpawn++;

                    // Allow derived classes to handle detour logic at waypoints
                    // This may change the path, so we need to check bounds after
                    HandleDetourAtWaypoint();

                    // If path was changed or we've reached the end, stop consuming distance
                    if (worldPath == null || worldPathIndex >= worldPath.Count)
                    {
                        break;
                    }
                }
                else
                {
                    // Can't reach the next waypoint yet - move toward it
                    Vector3 direction = (targetWorldPos - finalPosition).normalized;
                    Vector3 targetPos = finalPosition + direction * remainingDistance;

                    // Check if movement is blocked by node center or tongue
                    Vector3 blockedPos;
                    if (IsBlockedByNodeCenter(targetPos, out blockedPos))
                    {
                        // Blocked by node center - move to blocked position instead
                        RecordNavigationEvent("Blocked", $"NodeCenter blocked partial move, pushed to ({blockedPos.x:F2}, {blockedPos.y:F2})");
                        finalPosition = blockedPos;
                    }
                    // NOTE: Tongue collision handled by Unity physics - no manual check needed
                    else
                    {
                        // Validate that target position is near a walkable tile
                        // Use tiered approach same as spline movement
                        ForestMaze.WorldSpaceTile nearestTile;
                        bool isNearWalkable = IsPositionNearWalkableTile(targetPos, out nearestTile, 1.0f);

                        if (!isNearWalkable || nearestTile == null)
                        {
                            // Target position is off walkable area
                            // TRUST THE PATH: If we have a valid waypoint from A*, allow movement toward it
                            // The path was built through walkable tiles, so the waypoint itself is valid
                            // The issue is just that our small intermediate step lands slightly off-tile
                            // Use the WAYPOINT tile check instead of intermediate position
                            ForestMaze.WorldSpaceTile waypointTile;
                            bool waypointNearWalkable = IsPositionNearWalkableTile(targetWorldPos, out waypointTile, 1.0f);

                            if (waypointNearWalkable && waypointTile != null)
                            {
                                // Waypoint is valid - move toward it even though intermediate is off-tile
                                // Move directly toward the nearest walkable tile's center
                                Vector2 targetPos2D = new Vector2(targetPos.x, targetPos.y);
                                Vector2 waypointDir = (waypointTile.Position - targetPos2D).normalized;
                                // Move toward the waypoint tile, constrained to remain near walkable area
                                Vector2 constrainedPos2D = targetPos2D + waypointDir * remainingDistance;
                                finalPosition = new Vector3(constrainedPos2D.x, constrainedPos2D.y, targetPos.z);
                            }
                            else
                            {
                                // Even waypoint is not near walkable - path seems invalid
                                // But the A* algorithm built this path, so trust it
                                // Just move directly toward the waypoint anyway
                                Vector2 currentPos2D = new Vector2(finalPosition.x, finalPosition.y);
                                Vector2 waypointPos2D = new Vector2(targetWorldPos.x, targetWorldPos.y);
                                Vector2 moveDir = (waypointPos2D - currentPos2D).normalized;
                                Vector2 newPos2D = currentPos2D + moveDir * remainingDistance;
                                finalPosition = new Vector3(newPos2D.x, newPos2D.y, finalPosition.z);
                            }
                        }
                        else
                        {
                            // We found a walkable tile nearby (within 1.0 units)
                            // TRUST THE A* PATH: Just move toward the waypoint
                            // The old drift/constraint logic was pulling visitors toward tile centers
                            // that were often in the wrong direction, causing them to get stuck
                            finalPosition = targetPos;
                        }
                    }
                    remainingDistance = 0f;
                }
            }

            // Store final position for physics to apply in FixedUpdate
            RecordMoveCause("WaypointMovement");
            desiredPosition = finalPosition;
            hasDesiredPosition = true;

            // Update facing direction based on current target waypoint (or last movement direction)
            // Check magnitude BEFORE normalizing to avoid noise when at target
            if (worldPath != null && worldPathIndex < worldPath.Count)
            {
                Vector3 targetWorldPos = worldPath[worldPathIndex];
                Vector3 pathDirection = targetWorldPos - PhysicsPosition;
                if (pathDirection.sqrMagnitude > 0.0001f)  // Minimum distance threshold
                {
                    Vector2 facingDirection = new Vector2(pathDirection.x, pathDirection.y).normalized;
                    UpdateAnimatorDirection(facingDirection);
                }
            }
        }

        /// <summary>
        /// Updates movement using graph-based navigation with actual maze geometry.
        /// Uses polylines for edges and arc traversal for nodes.
        /// </summary>
        protected virtual void UpdateGraphNavigation(float effectiveSpeed)
        {
            if (graphPath == null || !graphPath.IsValid || graphSegmentIndex >= graphPath.Segments.Count)
            {
                // No valid graph path - fall back to tile-based
                UpdateDirectWalking(effectiveSpeed);
                return;
            }

            var segment = graphPath.Segments[graphSegmentIndex];
            Vector3 currentPos = PhysicsPosition;
            Vector2 currentPos2D = new Vector2(currentPos.x, currentPos.y);

            if (segment.Type == ForestMaze.SegmentType.Edge)
            {
                UpdateGraphEdgeMovement(segment, effectiveSpeed, currentPos2D);
            }
            else // Node
            {
                UpdateGraphNodeMovement(segment, effectiveSpeed, currentPos2D);
            }
        }

        /// <summary>
        /// Updates movement along an edge segment using the polyline path.
        /// NOTE: We use PolylinePoints exclusively because edge.Curve is a simplified
        /// straight-line Bezier that doesn't match the actual S-curved path where walls are placed.
        /// </summary>
        protected virtual void UpdateGraphEdgeMovement(ForestMaze.GraphPathSegment segment, float effectiveSpeed, Vector2 currentPos2D)
        {
            var mapState = mazeGridBehaviour.ForestMapState;
            if (mapState == null || segment.Index >= mapState.Edges.Count)
            {
                AdvanceGraphSegment();
                return;
            }

            var edge = mapState.Edges[segment.Index];
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
            {
                // No valid polyline - skip to next segment
                AdvanceGraphSegment();
                return;
            }

            // Calculate exit progress (where we're trying to reach on this segment)
            float exitProgress = CalculateEdgeProgressAtPoint(segment, segment.ExitPoint);

            // Calculate progress increment based on speed and segment length (not full edge length)
            // Use the segment's actual length which accounts for partial traversal
            float segmentLength = segment.Length > 0 ? segment.Length : CalculatePolylineLength(edge.PolylinePoints);
            float progressDelta = (effectiveSpeed * Time.fixedDeltaTime) / Mathf.Max(segmentLength, 0.1f);

            // Determine direction based on whether exit progress is greater or less than current
            bool movingForward = exitProgress > graphSegmentProgress;
            if (movingForward)
                graphSegmentProgress += progressDelta;
            else
                graphSegmentProgress -= progressDelta;

            // Check segment completion: have we reached or passed the exit point?
            bool completed = movingForward
                ? (graphSegmentProgress >= exitProgress)
                : (graphSegmentProgress <= exitProgress);

            if (completed)
            {
                // Snap to exit point and advance to next segment
                graphSegmentProgress = exitProgress;
                AdvanceGraphSegment();
                return;
            }

            // Calculate target position on polyline
            Vector2 targetPos2D = ForestMaze.EdgeFollower.GetPosition(edge, graphSegmentProgress);

            // Apply aggressive centerline correction to keep visitors on path
            float lateralOffset = ForestMaze.EdgeFollower.GetLateralOffset(edge, graphSegmentProgress, currentPos2D);
            Vector2 normal = ForestMaze.EdgeFollower.GetNormal(edge, graphSegmentProgress);

            // Correction strength increases with distance from centerline:
            // - Within 0.3 units: 30% correction (gentle)
            // - 0.3-0.6 units: 60% correction (moderate)
            // - Beyond 0.6 units: 100% correction (snap back to path)
            float absOffset = Mathf.Abs(lateralOffset);
            float correctionStrength;
            if (absOffset < 0.3f)
                correctionStrength = 0.3f;
            else if (absOffset < 0.6f)
                correctionStrength = 0.6f;
            else
                correctionStrength = 1.0f;

            targetPos2D -= normal * lateralOffset * correctionStrength;

            // Get facing direction (reversed if moving backward along polyline)
            Vector2 facingDir = ForestMaze.EdgeFollower.GetDirection(edge, graphSegmentProgress, !movingForward);
            UpdateAnimatorDirection(facingDir);

            // Set desired position for physics
            desiredPosition = new Vector3(targetPos2D.x, targetPos2D.y, PhysicsPosition.z);
            hasDesiredPosition = true;
            RecordMoveCause("GraphEdge");
        }

        /// <summary>
        /// Updates movement through a node using arc waypoints along the walkable ring.
        /// </summary>
        protected virtual void UpdateGraphNodeMovement(ForestMaze.GraphPathSegment segment, float effectiveSpeed, Vector2 currentPos2D)
        {
            var mapState = mazeGridBehaviour.ForestMapState;
            if (mapState == null || segment.Index >= mapState.Nodes.Count)
            {
                AdvanceGraphSegment();
                return;
            }

            var node = mapState.Nodes[segment.Index];

            // Initialize node arc waypoints if not done yet
            if (currentNodeArc == null || currentNodeArc.Count == 0)
            {
                currentNodeArc = ForestMaze.NodeTraverser.GetNodeArc(node, segment.EntryPoint, segment.ExitPoint, 4);
                nodeArcIndex = 0;

                if (currentNodeArc.Count == 0)
                {
                    AdvanceGraphSegment();
                    return;
                }
            }

            // Move through arc waypoints
            if (nodeArcIndex >= currentNodeArc.Count)
            {
                // Arc complete - advance to next segment
                AdvanceGraphSegment();
                return;
            }

            Vector2 targetWaypoint = currentNodeArc[nodeArcIndex];
            float distToWaypoint = Vector2.Distance(currentPos2D, targetWaypoint);
            float moveDistance = effectiveSpeed * Time.fixedDeltaTime;

            if (distToWaypoint <= moveDistance)
            {
                // Reached waypoint - move to it and advance
                Vector2 newPos = targetWaypoint;
                nodeArcIndex++;

                // If there are more waypoints, consume remaining distance
                while (nodeArcIndex < currentNodeArc.Count)
                {
                    float remainingDist = moveDistance - distToWaypoint;
                    Vector2 nextWaypoint = currentNodeArc[nodeArcIndex];
                    float nextDist = Vector2.Distance(newPos, nextWaypoint);

                    if (nextDist <= remainingDist)
                    {
                        newPos = nextWaypoint;
                        distToWaypoint = nextDist;
                        nodeArcIndex++;
                    }
                    else
                    {
                        // Move partial distance toward next waypoint
                        Vector2 dir = (nextWaypoint - newPos).normalized;
                        newPos = newPos + dir * remainingDist;
                        break;
                    }
                }

                // Check for node center blocking before setting desired position
                Vector3 targetPos3D = new Vector3(newPos.x, newPos.y, PhysicsPosition.z);
                if (IsBlockedByNodeCenter(targetPos3D, out Vector3 blockedPos))
                {
                    desiredPosition = blockedPos;
                }
                else
                {
                    desiredPosition = targetPos3D;
                }
                hasDesiredPosition = true;

                // Check if arc complete after consuming distance
                if (nodeArcIndex >= currentNodeArc.Count)
                {
                    AdvanceGraphSegment();
                    return; // Exit early - arc is done
                }
            }
            else
            {
                // Move toward waypoint
                Vector2 dir = (targetWaypoint - currentPos2D).normalized;
                Vector2 newPos = currentPos2D + dir * moveDistance;

                // Check for node center blocking before setting desired position
                Vector3 targetPos3D = new Vector3(newPos.x, newPos.y, PhysicsPosition.z);
                if (IsBlockedByNodeCenter(targetPos3D, out Vector3 blockedPos))
                {
                    desiredPosition = blockedPos;
                }
                else
                {
                    desiredPosition = targetPos3D;
                }
                hasDesiredPosition = true;
            }

            // Update facing direction (guard against null after AdvanceGraphSegment)
            if (currentNodeArc != null && nodeArcIndex < currentNodeArc.Count)
            {
                Vector2 facingDir = (currentNodeArc[nodeArcIndex] - currentPos2D).normalized;
                if (facingDir.sqrMagnitude > 0.0001f)
                {
                    UpdateAnimatorDirection(facingDir);
                }
            }

            RecordMoveCause("GraphNode");
        }

        /// <summary>
        /// Advances to the next segment in the graph path.
        /// </summary>
        protected virtual void AdvanceGraphSegment()
        {
            graphSegmentIndex++;
            currentNodeArc = null;
            nodeArcIndex = 0;

            if (graphSegmentIndex < graphPath.Segments.Count)
            {
                var nextSegment = graphPath.Segments[graphSegmentIndex];

                // Initialize progress for next segment
                if (nextSegment.Type == ForestMaze.SegmentType.Edge)
                {
                    // Calculate actual progress at entry point (not just 0 or 1)
                    graphSegmentProgress = CalculateEdgeProgressAtPoint(nextSegment, nextSegment.EntryPoint);
                }
                else
                {
                    graphSegmentProgress = 0f;
                }

                // Fire segment enter event
                OnGraphSegmentEnter(nextSegment);
            }
            else
            {
                // Path complete
                OnGraphPathComplete();
            }
        }

        /// <summary>
        /// Called when entering a new graph segment. Override for segment-based behaviors.
        /// </summary>
        protected virtual void OnGraphSegmentEnter(ForestMaze.GraphPathSegment segment)
        {
            RecordNavigationEvent("SegmentEnter", $"{segment.Type}[{segment.Index}] {(segment.Reversed ? "R" : "")}");

            // For node segments, this is a good place to check for detours/confusion
            if (segment.Type == ForestMaze.SegmentType.Node)
            {
                // Simplified detour handling at node entry
                HandleDetourAtNode(segment.Index);
            }
        }

        /// <summary>
        /// Called when graph path is complete.
        /// </summary>
        protected virtual void OnGraphPathComplete()
        {
            RecordNavigationEvent("PathComplete", "Graph path complete");
            OnWorldSpacePathComplete();
        }

        /// <summary>
        /// Handles detour logic when entering a node during graph navigation.
        /// Simplified version of HandleDetourAtWaypoint for graph-based movement.
        /// </summary>
        protected virtual void HandleDetourAtNode(int nodeIndex)
        {
            // Base implementation does nothing - derived classes can override for confusion/misstep behavior
            // This replaces the per-tile HandleDetourAtWaypoint with per-node events (much less frequent)
        }

        /// <summary>
        /// Calculates the total length of a polyline.
        /// </summary>
        private float CalculatePolylineLength(List<Vector2> points)
        {
            if (points == null || points.Count < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }
            return length;
        }

        /// <summary>
        /// Calculates the curve parameter (0-1) for a point on an edge segment.
        /// This is used to properly initialize progress when entering an edge, especially
        /// for partial edges where the entry point is not at t=0 or t=1.
        /// </summary>
        private float CalculateEdgeProgressAtPoint(ForestMaze.GraphPathSegment segment, Vector2 point)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
                return segment.Reversed ? 1f : 0f;

            var mapState = mazeGridBehaviour.ForestMapState;
            if (segment.Index >= mapState.Edges.Count)
                return segment.Reversed ? 1f : 0f;

            var edge = mapState.Edges[segment.Index];

            // Use EdgeFollower to find the actual curve parameter
            return ForestMaze.EdgeFollower.GetProgress(edge, point);
        }

        /// <summary>
        /// Checks if movement to a target position is blocked by a node center collider.
        /// Node center colliders prevent visitors from walking into the middle of nodes with props.
        /// When blocked, tries to push visitor around the obstacle rather than just stopping.
        /// </summary>
        /// <param name="targetPosition">The position we want to move to</param>
        /// <param name="blockedPosition">If blocked, a position that moves visitor around the obstacle</param>
        /// <returns>True if movement is blocked by node center collider, false otherwise</returns>
        protected bool IsBlockedByNodeCenter(Vector3 targetPosition, out Vector3 blockedPosition)
        {
            blockedPosition = targetPosition;

            // Don't block if we're already grabbed
            if (state == VisitorState.Grabbed) return false;

            Vector3 currentPos = PhysicsPosition;
            Vector3 direction = targetPosition - currentPos;
            float distance = direction.magnitude;

            if (distance < 0.001f) return false;

            // First, check if we're ALREADY inside a node center collider
            // If so, push radially outward from the collider center
            // Note: NodeCenterColliders are triggers, so we need QueryTriggerInteraction.Collide
            //
            // IMPORTANT: Use a small search radius (0.05) just to find nearby colliders, then check
            // actual distance from collider center. Using visitorRadius (0.2) here would effectively
            // expand the blocked zone by 0.2 units, blocking visitors on walkable tiles at 1.0-1.2
            // distance when only tiles < 1.0 should be unwalkable.
            const float nodeCenterBlockRadius = 1.0f; // Must match NODE_CENTER_COLLIDER_RADIUS in DynamicMazeGrowth
            const float searchRadius = 1.5f; // Search area to find nearby node center colliders

            Collider[] currentOverlaps = Physics.OverlapSphere(currentPos, searchRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var col in currentOverlaps)
            {
                if (col != null && col.gameObject.name.StartsWith("NodeCenterCollider_"))
                {
                    // Check actual 2D distance from collider center (node center)
                    Vector3 colliderCenter = col.bounds.center;
                    Vector2 offset2D = new Vector2(currentPos.x - colliderCenter.x, currentPos.y - colliderCenter.y);
                    float distFromCenter = offset2D.magnitude;

                    // Only block if actually inside the blocked radius (not just touching with visitor radius)
                    if (distFromCenter >= nodeCenterBlockRadius)
                    {
                        continue; // Not actually inside the blocked zone
                    }

                    // We're inside a node center exclusion zone - push outward
                    Vector2 pushDir2D = offset2D;

                    if (pushDir2D.sqrMagnitude < 0.001f)
                    {
                        // At center - push in random direction
                        float randomAngle = RandomManager.Value * Mathf.PI * 2f;
                        pushDir2D = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                    }
                    pushDir2D.Normalize();

                    // Push out to just beyond the blocked radius
                    float pushDist = nodeCenterBlockRadius - distFromCenter + 0.1f;
                    blockedPosition = new Vector3(
                        currentPos.x + pushDir2D.x * pushDist,
                        currentPos.y + pushDir2D.y * pushDist,
                        currentPos.z);

                    return true;
                }
            }

            // Note: The second check (line-circle slide around node centers) was removed.
            // The A* path already routes through walkable tiles which avoid node centers.
            // SimplifyTilePath also validates that simplified segments don't cut through node centers.
            // The slide behavior was causing visitors to get stuck in infinite loops near lanterns
            // (slide sideways → off walkable tile → recovery → try waypoint → slide again).

            return false;
        }

        /// <summary>
        /// Calculates the minimum distance from a point to a line segment in 2D.
        /// </summary>
        private float PointToLineSegmentDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float lineLenSq = line.sqrMagnitude;

            if (lineLenSq < 0.0001f)
            {
                // Degenerate line - just return distance to start
                return Vector2.Distance(point, lineStart);
            }

            // Project point onto line, clamped to segment
            float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, line) / lineLenSq);
            Vector2 projection = lineStart + t * line;

            return Vector2.Distance(point, projection);
        }

        // NOTE: IsBlockedByTongue removed - tongue collision is handled by Unity physics
        // The tongue has solid (non-trigger) colliders with kinematic Rigidbodies that
        // naturally block the visitor's Rigidbody through physics collision.

        /// <summary>
        /// Called when visitor reaches a waypoint. Override in derived classes
        /// to implement detour behaviors (confusion, missteps, etc.).
        /// Base implementation just advances to the next waypoint.
        /// </summary>
        protected virtual void HandleDetourAtWaypoint()
        {
            // Default: just advance to next waypoint
            if (worldPath != null && worldPathIndex < worldPath.Count)
            {
                // Record waypoint reached before advancing
                Vector3 reachedWp = worldPath[worldPathIndex];
                RecordNavigationEvent("Waypoint", $"Reached wp[{worldPathIndex}] ({reachedWp.x:F2}, {reachedWp.y:F2})");

                worldPathIndex++;

                if (worldPathIndex >= worldPath.Count)
                {
                    RecordNavigationEvent("PathComplete", "Reached final waypoint");
                    OnPathCompleted();
                }
            }
        }

        #region Catmull-Rom Spline Smoothing

        /// <summary>
        /// Initializes the Catmull-Rom spline for smooth path following.
        /// Sets up control points P0, P1, P2, P3 based on current worldPath.
        /// </summary>
        protected virtual void InitializeSpline()
        {
            if (worldPath == null || worldPath.Count < 2)
            {
                splineInitialized = false;
                return;
            }

            splineStartIndex = worldPathIndex;
            splineProgress = 0f;
            UpdateSplineControlPoints();

            // Skip initial segments that are too short to avoid rotation instability
            while (splineSegmentLength < MIN_SPLINE_SEGMENT_LENGTH && splineStartIndex < worldPath.Count - 1)
            {
                // Snap position to P2 of the short segment before advancing
                Vector3 snapPos = splineControlPoints[2];
                snapPos.z = PhysicsPosition.z;
                RecordMoveCause("SplineInit_ShortSegmentSnap_1");
                desiredPosition = snapPos;
                hasDesiredPosition = true;

                splineStartIndex++;
                splineProgress = 0f;

                if (splineStartIndex >= worldPath.Count - 1)
                {
                    splineInitialized = false;
                    worldPathIndex = worldPath.Count;
                    return;
                }

                UpdateSplineControlPoints();
            }

            splineInitialized = true;
        }

        // Minimum segment length to avoid rotation instability from tiny movements
        protected const float MIN_SPLINE_SEGMENT_LENGTH = 0.15f;

        /// <summary>
        /// Updates the 4 control points for the current spline segment.
        /// P0 = previous point (or extrapolated), P1 = current start, P2 = target, P3 = next point (or extrapolated)
        ///
        /// IMPORTANT: If the visitor's actual position diverges significantly from the path waypoint
        /// (e.g., after being pushed by NodeCenterCollider), P1 is set to the visitor's current position
        /// instead of the path waypoint. This ensures the spline interpolates FROM where the visitor
        /// actually is, preventing the sanity check from failing.
        /// </summary>
        protected virtual void UpdateSplineControlPoints()
        {
            if (worldPath == null || worldPath.Count < 2)
                return;

            int p1Index = splineStartIndex;
            int p2Index = Mathf.Min(p1Index + 1, worldPath.Count - 1);

            // P2 is always the target waypoint
            splineControlPoints[2] = worldPath[p2Index];

            // P1: Check if visitor has diverged from the path waypoint
            // This can happen when pushed by NodeCenterCollider or other physics interactions
            Vector3 pathP1 = worldPath[p1Index];
            Vector3 currentPos = PhysicsPosition;
            float divergence = Vector3.Distance(new Vector3(pathP1.x, pathP1.y, 0), new Vector3(currentPos.x, currentPos.y, 0));

            // If diverged more than 0.5 units, use current position as P1 to create a recovery spline
            // This threshold is lower than the sanity check (2.0 units) to catch divergence early
            const float DIVERGENCE_THRESHOLD = 0.5f;
            bool usingDivergenceRecovery = divergence > DIVERGENCE_THRESHOLD;

            if (usingDivergenceRecovery)
            {
                // Use visitor's actual position as P1
                splineControlPoints[1] = new Vector3(currentPos.x, currentPos.y, pathP1.z);
            }
            else
            {
                // Normal case: use path waypoint as P1
                splineControlPoints[1] = pathP1;
            }

            // P0: previous point or extrapolate backward from P1
            // IMPORTANT: When using divergence recovery, P0 should be extrapolated from current position
            // to ensure the spline starts moving toward P2 immediately, not curving toward old path
            if (usingDivergenceRecovery || p1Index == 0)
            {
                // Extrapolate backward: P0 = P1 - (P2 - P1)
                // This makes the spline start moving directly toward P2
                splineControlPoints[0] = splineControlPoints[1] - (splineControlPoints[2] - splineControlPoints[1]);
            }
            else
            {
                splineControlPoints[0] = worldPath[p1Index - 1];
            }

            // P3: next point after P2 or extrapolate forward
            if (p2Index < worldPath.Count - 1)
            {
                splineControlPoints[3] = worldPath[p2Index + 1];
            }
            else
            {
                // Extrapolate forward: P3 = P2 + (P2 - P1)
                splineControlPoints[3] = splineControlPoints[2] + (splineControlPoints[2] - splineControlPoints[1]);
            }

            // Calculate segment length for speed normalization
            splineSegmentLength = EstimateCatmullRomLength(
                splineControlPoints[0], splineControlPoints[1],
                splineControlPoints[2], splineControlPoints[3]
            );
        }

        /// <summary>
        /// Advances to the next spline segment when current segment is complete.
        /// Skips segments that are too short (P1 and P2 very close) to avoid rotation instability.
        /// </summary>
        protected virtual void AdvanceSplineSegment()
        {
            splineStartIndex++;
            splineProgress = 0f;

            // Check if we've reached the end of the path
            if (splineStartIndex >= worldPath.Count - 1)
            {
                splineInitialized = false;
                worldPathIndex = worldPath.Count;
                return;
            }

            UpdateSplineControlPoints();

            // Skip segments that are too short - these cause rotation instability
            // Keep advancing until we find a segment of adequate length or reach the end
            while (splineSegmentLength < MIN_SPLINE_SEGMENT_LENGTH && splineStartIndex < worldPath.Count - 1)
            {
                // Snap position to P2 of the short segment before advancing
                Vector3 snapPos = splineControlPoints[2];
                snapPos.z = PhysicsPosition.z;
                RecordMoveCause("SplineAdvance_ShortSegmentSnap");
                desiredPosition = snapPos;
                hasDesiredPosition = true;

                splineStartIndex++;
                splineProgress = 0f;

                if (splineStartIndex >= worldPath.Count - 1)
                {
                    splineInitialized = false;
                    worldPathIndex = worldPath.Count;
                    return;
                }

                UpdateSplineControlPoints();
            }
        }

        /// <summary>
        /// Evaluates a point on a Catmull-Rom spline.
        /// The spline passes through P1 at t=0 and P2 at t=1.
        /// P0 and P3 influence the tangents at P1 and P2 respectively.
        /// </summary>
        protected Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            // Catmull-Rom basis matrix coefficients
            // P(t) = 0.5 * ((2*P1) + (-P0 + P2)*t + (2*P0 - 5*P1 + 4*P2 - P3)*t^2 + (-P0 + 3*P1 - 3*P2 + P3)*t^3)
            Vector3 result = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );

            return result;
        }

        /// <summary>
        /// Estimates the length of a Catmull-Rom spline segment using sampling.
        /// </summary>
        protected float EstimateCatmullRomLength(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int samples = 10)
        {
            float length = 0f;
            Vector3 prev = p1; // Start at P1 (t=0)

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector3 current = EvaluateCatmullRom(p0, p1, p2, p3, t);
                length += Vector3.Distance(prev, current);
                prev = current;
            }

            return Mathf.Max(0.1f, length); // Ensure non-zero length
        }

        /// <summary>
        /// Resets the spline state when path changes.
        /// </summary>
        protected virtual void ResetSplineState()
        {
            splineInitialized = false;
            splineProgress = 0f;
            splineStartIndex = 0;
        }

        #endregion

        /// <summary>
        /// Called when the visitor completes their path.
        /// Delegates to OnWorldSpacePathComplete for destination handling.
        /// </summary>
        protected virtual void OnPathCompleted()
        {
            OnWorldSpacePathComplete();
        }

        /// <summary>
        /// Called when the visitor reaches its world-space destination.
        /// </summary>
        protected virtual void OnWorldSpacePathComplete()
        {
            if (ShouldLogVisitorPath())
            {
                LogVisitorPath($"reached world-space destination at {worldDestination}");
            }

            // If misdirected, completing the path means we reached the far sign position
            if (isMisdirected)
            {
                CompleteMisdirect();
                return;
            }

            // Check if frightened - visitor reached a node, handle recovery/fleeing
            if (isFrightened)
            {
                HandleFrightenedAtNode();
                return;
            }

            // Check if at a portal (destination spawn point) - visitor exits the maze
            // Note: Visitors are ONLY consumed by the heart tongue grabbing them.
            // Visitors never path to the heart center - heart node centers are unwalkable.
            if (mazeGridBehaviour != null && IsAtPortal())
            {
                SetState(VisitorState.Escaping, "OnPathComplete: at portal");
                OnExitedThroughPortal();
                return;
            }

            // Not at a portal — pick a random portal and head there
            NavigateToRandomPortal("OnPathComplete: not at portal");
        }

        /// <summary>
        /// Checks if the visitor is at a portal (spawn point) position.
        /// </summary>
        protected virtual bool IsAtPortal()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return false;

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            Vector3 currentPos = PhysicsPosition;

            foreach (var kvp in spawnPoints)
            {
                float distToSpawn = Vector3.Distance(currentPos, kvp.Value);
                if (distToSpawn < 3f) // Within 3 units of a portal
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Called when a visitor successfully exits the maze through a portal.
        /// The visitor leaves the game without awarding essence to the player.
        /// </summary>
        protected virtual void OnExitedThroughPortal()
        {
            // Record fate - escaped with no essence gained
            if (FaeMaze.Systems.GameStatsTracker.Instance != null)
            {
                FaeMaze.Systems.GameStatsTracker.Instance.RecordVisitorFate(Archetype, FaeMaze.Systems.VisitorFate.Escaped, 0);
            }

            if (!string.IsNullOrEmpty(entityLabel))
            {
                GameEventLogger.LogVisitorFate(entityLabel, "Escaped", 0);
            }

            // Destroy the visitor
            Destroy(gameObject, 0.1f);
        }

        /// <summary>
        /// Called when a visitor's essence is depleted (drained to 0 by props).
        /// The visitor is considered escaped and despawns.
        /// </summary>
        protected virtual void OnEssenceDepleted()
        {
            // Determine the fate based on what was draining essence
            FaeMaze.Systems.VisitorFate fate;
            if (currentFairyRing != null)
            {
                fate = FaeMaze.Systems.VisitorFate.FairyRing;
            }
            else if (currentFaeLantern != null)
            {
                fate = FaeMaze.Systems.VisitorFate.Lantern;
            }
            else
            {
                // Fallback - shouldn't happen but default to lantern
                fate = FaeMaze.Systems.VisitorFate.Lantern;
            }

            // Record fate - essence was gained through the prop interaction (tracked via EssenceSource)
            // The essence value here is 0 since essence was gained incrementally during fascination
            if (FaeMaze.Systems.GameStatsTracker.Instance != null)
            {
                FaeMaze.Systems.GameStatsTracker.Instance.RecordVisitorFate(Archetype, fate, 0);
            }

            if (!string.IsNullOrEmpty(entityLabel))
            {
                GameEventLogger.LogVisitorFate(entityLabel, fate.ToString(), 0);
            }

            // Clean up visual effects before destroying
            FaeMaze.Props.LanternStreamerEffect.DestroyStreamers(transform);

            // Clear any fascination state
            isFascinated = false;
            isFrightened = false;
            frightRecoveryNodeCount = 0;
            isLured = false;
            isMisdirected = false;
            misdirectEdgeIndex = -1;
            isConfused = false;
            hasReachedLantern = false;

            // Clear references
            currentFaeLantern = null;
            currentFairyRing = null;
            lanternEssenceAwardAccumulator = 0f;

            SetState(VisitorState.Escaping, "OnExitedThroughPortal");

            // Destroy the visitor
            Destroy(gameObject, 0.1f);
        }

        /// <summary>
        /// Called when a visitor is consumed by the Heart of the Maze.
        /// Override in derived classes for specific behavior.
        /// </summary>
        protected virtual void OnConsumedByHeart()
        {
            // Delegate to HandleConsumption which properly routes through HeartOfTheMaze
            HandleConsumption();
        }

        /// <summary>
        /// Sets the visitor to be grabbed by the heart tongue, stopping all movement.
        /// Called by HeartOfTheMaze when the tongue grabs the visitor.
        /// </summary>
        public virtual void SetGrabbedByHeart()
        {
            // Don't grab if already grabbed, consumed, or escaping
            if (state == VisitorState.Grabbed || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Clean up visual effects
            FaeMaze.Props.LanternStreamerEffect.DestroyStreamers(transform);
            FaeMaze.Props.DazeStreamerEffect.DestroyStreamers(transform);

            // Clear all state flags
            isFascinated = false;
            isFrightened = false;
            frightRecoveryNodeCount = 0;
            isLured = false;
            isDazed = false;
            isMisdirected = false;
            misdirectEdgeIndex = -1;
            isConfused = false;
            currentFairyRing = null;  // Clear fairy ring reference to stop circling
            currentFaeLantern = null; // Clear lantern reference

            // CRITICAL: Clear any pending physics movement to prevent teleporting
            // The heart will control position directly while grabbed
            hasDesiredPosition = false;

            // Remove Z constraint so tongue colliders can push us down during sinking
            // Also zero velocity immediately — with linearDamping=0, residual velocity
            // would keep the visitor sliding through devour/tongue models
            if (rb3D != null)
            {
                rb3D.constraints = RigidbodyConstraints.FreezeRotation;  // Remove FreezePositionZ
                rb3D.linearVelocity = Vector3.zero;
            }

            SetState(VisitorState.Grabbed, "SetGrabbed: tongue contact");
        }

        /// <summary>
        /// Clears the Grabbed state so the visitor can be controlled by normal state logic again.
        /// Called when releasing a visitor from HeartwardGrasp.
        /// </summary>
        public virtual void ClearGrabbedState()
        {
            if (state != VisitorState.Grabbed)
            {
                return;
            }

            // CRITICAL: Sync the Rigidbody position AND rotation with the transform.
            // When grabbed, the visitor is moved via transform.position/rotation, but the Rigidbody's
            // internal tracking may not be updated (especially with interpolation).
            // This prevents the visitor from teleporting back to their pre-grab physics state.
            if (rb3D != null)
            {
                // Force the Rigidbody to match the current transform state
                rb3D.position = transform.position;
                rb3D.rotation = transform.rotation;
                rb3D.linearVelocity = Vector3.zero;
                rb3D.angularVelocity = Vector3.zero;

                // Restore Z freeze constraint (removed during grab for tongue sinking)
                rb3D.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            }

            SetState(VisitorState.Idle, "ClearGrabbedState: released from tongue");
        }

        /// <summary>
        /// Syncs the Rigidbody internal position to match the current transform position.
        /// Call this when moving a grabbed visitor via transform.position to prevent
        /// physics interpolation from teleporting the visitor back to stale positions.
        /// </summary>
        public void SyncRigidbodyPosition()
        {
            if (rb3D != null)
            {
                rb3D.position = transform.position;
            }
        }

        /// <summary>
        /// Disables all Light components on the visitor and its children.
        /// Called when the visitor is grabbed by the heart tongue.
        /// </summary>
        protected virtual void DisableAllLights()
        {
            // Disable the state aura light
            if (stateAuraLight != null)
            {
                stateAuraLight.enabled = false;
            }

            // Disable any other lights on the visitor model (from prefabs, etc.)
            Light[] allLights = GetComponentsInChildren<Light>();
            foreach (Light light in allLights)
            {
                light.enabled = false;
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
                // Still notify HeartPowerManager if possible
                if (HeartPowers.HeartPowerManager.Instance != null)
                {
                    HeartPowers.HeartPowerManager.Instance.NotifyVisitorConsumed();
                }
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
            fascinationLanternPosition = Vector3.zero;
        }

        /// <summary>
        /// Called when a visitor enters a FaeLantern's influence area (via collider trigger).
        /// Uses archetype-specific fascination parameters if config is available.
        /// Tutorial visitors always have 100% fascination chance.
        /// Each lantern can only fascinate one visitor at a time.
        /// </summary>
        public virtual void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern)
        {
            // Only process fascination for movement states and Idle
            if (!IsMovementState(state) && state != VisitorState.Idle)
            {
                return;
            }

            // Don't re-trigger if already paused at a lantern
            if (isFascinated && hasReachedLantern && fascinationTimer > 0)
            {
                return;
            }

            // Fascination-immune, misdirected, or lured visitors ignore lanterns
            if (isFascinationImmune || isMisdirected || isLured)
            {
                return;
            }

            Vector3 lanternWorldPos = lantern.transform.position;

            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && Vector3.Distance(fascinationLanternPosition, lanternWorldPos) < 0.1f)
                return;

            // Lantern can only fascinate one visitor at a time
            if (lantern.HasFascinatedVisitor())
            {
                return;
            }

            // Tutorial visitors always succeed; normal visitors use archetype-specific chance
            if (!isTutorialVisitor)
            {
                float fascinationChance = GetFascinationChance();
                float roll = RandomManager.Value;
                if (roll > fascinationChance)
                {
                    return;
                }
            }

            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lanternWorldPos;
            lanternEssenceAwardAccumulator = 0f;

            if (!string.IsNullOrEmpty(entityLabel))
            {
                string propLabel = EntityLabels.GetPropLabel(lantern.gameObject);
                GameEventLogger.LogVisitorFascination(entityLabel, propLabel);
            }

            // Reset detour state then explicitly clear confusion.
            // VisitorController.ResetDetourState() sets isConfused = confusionEnabled,
            // which would cause RefreshStateFromFlags() to override Idle → Confused.
            ResetDetourState();
            isConfused = false;

            // Stop immediately — visitor becomes idle at current position
            hasReachedLantern = true;
            fascinationTimer = lantern.FascinationDuration;
            SetState(VisitorState.Idle, "EnterFaeInfluence: fascinated by lantern");
            worldPath?.Clear();
            worldPathIndex = 0;
            ResetSplineState();

            // Face toward the lantern
            Vector3 visitorPos = PhysicsPosition;
            Vector2 toLantern = new Vector2(lanternWorldPos.x - visitorPos.x, lanternWorldPos.y - visitorPos.y);
            if (toLantern.sqrMagnitude > 0.01f)
            {
                UpdateAnimatorDirection(toLantern.normalized);
            }

            // Spawn visual streamers
            FaeMaze.Props.LanternStreamerEffect.SpawnStreamers(transform);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Stops the visitor's movement.
        /// </summary>
        public virtual void Stop()
        {
            SetState(VisitorState.Idle, "Stop()");

            // Clear fairy ring fascination so Update doesn't continue circling
            if (currentFairyRing != null)
            {
                isFascinated = false;
                currentFairyRing = null;
                fairyRingFascinationTimer = 0f;
                speedMultiplier = 1f;
            }
        }

        /// <summary>
        /// Resumes the visitor's movement if they have a world path.
        /// </summary>
        public virtual void Resume()
        {
            if (worldPath != null && worldPath.Count > 0 && worldPathIndex < worldPath.Count)
            {
                RefreshStateFromFlags();
            }
        }

        /// <summary>
        /// Checks if a world-space path exists between two positions.
        /// </summary>
        protected bool HasWorldPath(Vector3 start, Vector3 destination)
        {
            var testPath = BuildWorldPath(start, destination);
            return testPath != null && testPath.Count > 0;
        }

        /// <summary>
        /// Gets the destination for the current visitor state.
        /// Lured visitors approach the heart edge. Others return their set destination.
        /// Visitors NEVER path to node centers - they approach the edge.
        /// </summary>
        protected virtual Vector3 GetDestinationForCurrentState()
        {
            // Lured visitors head toward the heart edge (not center)
            if (isLured && mazeGridBehaviour != null)
            {
                return GetHeartApproachPosition();
            }

            // Return the world destination that was set via SetWorldDestination
            // Only fall back to heart edge if no destination was ever set
            if (worldDestination != Vector3.zero)
            {
                return worldDestination;
            }
            if (mazeGridBehaviour != null)
            {
                return GetHeartApproachPosition();
            }
            return originalDestination;
        }

        /// <summary>
        /// Gets the current destination position for this visitor.
        /// Used by external systems to check if destination is still valid.
        /// </summary>
        public Vector3 GetCurrentDestination()
        {
            return worldDestination;
        }

        /// <summary>
        /// Gets the visitor's current world-space path and their current index in it.
        /// Returns null if no path exists.
        /// </summary>
        public List<Vector3> GetCurrentPath(out int currentIndex)
        {
            currentIndex = worldPathIndex;
            return worldPath;
        }

        /// <summary>
        /// Directly sets the visitor's path without recalculating.
        /// Used by external systems (like Kelpie) to modify the visitor's route.
        /// </summary>
        /// <param name="path">The new world-space path to follow</param>
        public virtual void SetPathDirectly(List<Vector3> path)
        {
            if (path == null || path.Count == 0)
            {
                return;
            }

            worldPath = path;
            worldPathIndex = 0;
            ResetSplineState();

            // Update destination to the last point in the path
            if (path.Count > 0)
            {
                worldDestination = path[path.Count - 1];
            }

            RefreshStateFromFlags();
        }

        /// <summary>
        /// Recalculates the path to the current destination.
        /// </summary>
        public virtual void RecalculatePath()
        {
            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Fascinated visitors use special behavior, not recalculation
            if (isFascinated)
            {
                return;
            }

            // Misdirected visitors are being forced along the misdirect edge -- don't override their path
            if (isMisdirected)
            {
                return;
            }

            isCalculatingPath = true;

            Vector3 destination = GetDestinationForCurrentState();

            // Build graph-based path if enabled
            if (useGraphNavigation)
            {
                graphPath = BuildGraphPath(PhysicsPosition, destination);
                ResetGraphNavigationState();

                // If graph path valid, initialize first segment
                if (graphPath != null && graphPath.IsValid && graphPath.Segments.Count > 0)
                {
                    var firstSegment = graphPath.Segments[0];
                    if (firstSegment.Type == ForestMaze.SegmentType.Edge)
                    {
                        // Calculate actual progress at entry point (not just 0 or 1)
                        graphSegmentProgress = CalculateEdgeProgressAtPoint(firstSegment, firstSegment.EntryPoint);
                    }
                    OnGraphSegmentEnter(firstSegment);



                }
                else
                {


                }
            }

            // Also build tile-based path as fallback
            worldPath = BuildWorldPath(PhysicsPosition, destination);
            worldPathIndex = 0;
            worldDestination = destination;
            ResetSplineState();

            // Record path recalculation in audit log
            int graphSegments = graphPath?.Segments?.Count ?? 0;
            int tileWaypoints = worldPath?.Count ?? 0;
            string pathInfo = useGraphNavigation
                ? $"Graph:{graphSegments} segs, Tile:{tileWaypoints} wps"
                : $"Tile:{tileWaypoints} wps";
            RecordNavigationEvent("PathCalc", $"Recalc to ({destination.x:F2}, {destination.y:F2}), {pathInfo}");

            bool hasValidPath = (useGraphNavigation && graphPath != null && graphPath.IsValid) ||
                               (worldPath != null && worldPath.Count > 0);

            if (hasValidPath)
            {
                RefreshStateFromFlags();
            }

            isCalculatingPath = false;
        }

        /// <summary>
        /// Sets the visitor to Fascinated/Mesmerized state for a specified duration.
        /// This is the consolidated state for entranced/hypnotized/mesmerized effects.
        /// </summary>
        public virtual void SetMesmerized(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = mesmerizedDuration;
            }

            isFascinated = true;
            SetTimedState(VisitorState.Fascinated, duration);
            RefreshStateFromFlags();
        }

        /// <summary>
        /// Sets the visitor to Confused/Lost state for a specified duration.
        /// In world-space mode, this triggers a path recalculation.
        /// Alias for compatibility - uses Confused state internally.
        /// </summary>
        public virtual void SetLost(float duration = 0f)
        {
            if (duration <= 0f)
            {
                duration = lostDuration;
            }

            isConfused = true;
            SetTimedState(VisitorState.Confused, duration);
            RefreshStateFromFlags();

            // In world-space mode, just recalculate path (no grid-based detours)
            RecalculatePath();
        }

        /// <summary>
        /// Sets the visitor to Frightened state for a specified duration.
        /// The visitor will flee from their current position (source unknown).
        /// </summary>
        public virtual void SetFrightened(float duration = 0f)
        {
            SetFrightened(PhysicsPosition, duration);
        }

        /// <summary>
        /// Sets the visitor to Frightened state, fleeing from a specific source position.
        /// The visitor will double their speed and move away from the source.
        /// At each node, they have an accumulating 25% chance to recover.
        /// If they reach a portal while frightened, they escape and despawn.
        /// </summary>
        /// <param name="sourcePosition">The position of what frightened the visitor (to flee away from)</param>
        /// <param name="duration">Duration is ignored - frightened state ends via node recovery or portal escape</param>
        public virtual void SetFrightened(Vector3 sourcePosition, float duration = 0f)
        {
            // Already frightened, lured by Murmuring Paths, tutorial visitor, or in terminal state - ignore
            // Lured visitors are immune to fear - they are entranced by the Heart's call
            // Tutorial visitors are immune so they walk into power demonstration zones
            if (isFrightened || isLured || isTutorialVisitor || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // End any active lantern fascination so the lantern is freed for another visitor
            if (isFascinated && currentFaeLantern != null)
            {
                FaeMaze.Props.LanternStreamerEffect.DestroyStreamers(transform);
                isFascinated = false;
                hasReachedLantern = false;
                fascinationTimer = 0f;
                ClearLanternInteraction();
            }

            isFrightened = true;
            frightSourcePosition = sourcePosition;
            frightRecoveryNodeCount = 0;

            // Don't use timed state - frightened ends via node recovery or portal escape
            // SetTimedState clears on timer, but we want manual control
            currentTimedState = VisitorState.Frightened;
            currentStateDuration = float.MaxValue;  // Effectively infinite - recovery handled separately
            currentStateTimer = float.MaxValue;

            RefreshStateFromFlags();

            // Build a path to flee: find a random adjacent node away from the fright source
            BuildFrightenedFleePath();
        }

        /// <summary>
        /// Sets the visitor to Lured state, drawn toward the Heart.
        /// Lured overrides Fascinated state so players can rescue visitors from fairy rings.
        /// </summary>
        public virtual void SetLured(bool value)
        {
            if (isLured != value)
            {
                isLured = value;

                // Lured overrides frightened - the Heart's call is stronger than fear
                if (value && isFrightened)
                {
                    isFrightened = false;
                    frightRecoveryNodeCount = 0;
                }

                // Lured overrides fascination - clear fascination state when becoming lured
                if (value && isFascinated)
                {
                    // Clear lantern fascination
                    if (currentFaeLantern != null)
                    {
                        ClearLanternInteraction();
                    }
                    // Clear fairy ring fascination
                    if (currentFairyRing != null)
                    {
                        currentFairyRing = null;
                        fairyRingFascinationTimer = 0f;
                        speedMultiplier = 1f;
                    }
                    isFascinated = false;
                    hasReachedLantern = false;
                    fascinationTimer = 0f;
                }

                RefreshStateFromFlags();

                if (value)
                {
                    RecalculatePath();
                }
            }
        }

        /// <summary>
        /// Sets the visitor to Escaping state (for drowning by Puka).
        /// The visitor stops all movement and cannot be interacted with.
        /// </summary>
        public virtual void BecomeDrowning()
        {
            // Clear any existing states
            isFascinated = false;
            isFrightened = false;
            frightRecoveryNodeCount = 0;
            isLured = false;
            isMisdirected = false;
            misdirectEdgeIndex = -1;
            isConfused = false;

            // Clear path
            worldPath = null;
            worldPathIndex = 0;
            ResetSplineState();

            SetState(VisitorState.Escaping, "OnEssenceDepleted");
        }

        /// <summary>
        /// Called when the visitor witnesses maze growth.
        /// Sets the visitor to Dazed state for a duration, stopping movement.
        /// </summary>
        /// <param name="duration">How long the visitor remains dazed (default 15 seconds)</param>
        public virtual void OnWitnessMazeGrowth(float duration = 15f)
        {
            // Tutorial visitors are immune to daze so they follow tutorial pathing
            if (isTutorialVisitor) return;

            // Don't daze if already in a terminal state
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            isDazed = true;
            SetTimedState(VisitorState.Dazed, duration);
            RefreshStateFromFlags();
            FaeMaze.Props.DazeStreamerEffect.SpawnStreamers(transform);

            if (!string.IsNullOrEmpty(entityLabel))
            {
                GameEventLogger.LogVisitorDazed(entityLabel, duration);
            }
        }

        /// <summary>
        /// Called when another visitor is grabbed, pushed, or consumed.
        /// If this visitor can see the event, they become frightened and flee.
        /// </summary>
        /// <param name="eventPosition">The world position where the frightening event occurred</param>
        protected virtual void OnWitnessVisitorGrabbed(Vector3 eventPosition)
        {
            // Don't react if already in a terminal or frightened state
            if (isFrightened || state == VisitorState.Consumed || state == VisitorState.Escaping ||
                state == VisitorState.Grabbed || state == VisitorState.Dazed)
            {
                return;
            }

            // Check if within viewing distance (use same radius as red cap detection)
            float distance = Vector3.Distance(PhysicsPosition, eventPosition);
            if (distance > goblinDetectionRadius * 2f) // Double the red cap detection radius for witnessing events
            {
                return;
            }

            // Simple line-of-sight check using raycast in XY plane
            Vector3 direction = eventPosition - PhysicsPosition;
            direction.z = 0; // XY plane only
            float xyDistance = direction.magnitude;

            if (xyDistance > 0.1f)
            {
                // Cast ray to check for walls
                RaycastHit[] hits = Physics.RaycastAll(PhysicsPosition, direction.normalized, xyDistance);
                foreach (var hit in hits)
                {
                    // If we hit a wall before reaching the event, we can't see it
                    if (hit.collider.gameObject.name.Contains("Wall") ||
                        hit.collider.gameObject.name.Contains("wall"))
                    {
                        return;
                    }
                }
            }

            // Visitor can see the event - become frightened
            SetFrightened(eventPosition);
        }

        /// <summary>
        /// Checks for nearby Goblins and triggers frightened state if detected.
        /// </summary>
        protected virtual void CheckForNearbyGoblins()
        {
            // Skip if already frightened, lured (immune to fear), or in terminal state
            if (isFrightened || isLured || state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Use cached registry instead of expensive FindObjectsByType
            var goblins = GoblinRegistry.All;

            foreach (var goblin in goblins)
            {
                if (goblin == null || goblin.gameObject == null)
                    continue;

                float distance = Vector3.Distance(PhysicsPosition, goblin.transform.position);

                if (distance <= goblinDetectionRadius)
                {
                    // Pass the Goblin's position as the fright source
                    SetFrightened(goblin.transform.position);
                    return;
                }
            }
        }

        /// <summary>
        /// Checks for nearby frightening events (Puka drowning, heart tongue, powers, wisps luring).
        /// Uses the FrighteningEventManager to detect events and trigger fear response.
        /// </summary>
        protected virtual void CheckForFrighteningEvents()
        {
            // Skip if already frightened, lured (immune to fear), or in terminal state
            // Note: Drowning state was consolidated into Escaping state
            if (isFrightened || isLured || state == VisitorState.Consumed || state == VisitorState.Escaping ||
                state == VisitorState.Grabbed)
            {
                return;
            }

            // Use the FrighteningEventManager to check for nearby events
            if (FrighteningEventManager.Instance != null)
            {
                FrighteningEventManager.Instance.CheckAndFrightenVisitor(this);
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
        /// Called when a timed state expires.
        /// </summary>
        protected virtual void OnStateExpired(VisitorState expiredState)
        {
            switch (expiredState)
            {
                case VisitorState.Fascinated:
                    isFascinated = false;
                    break;
                case VisitorState.Confused:
                    isConfused = false;
                    break;
                case VisitorState.Frightened:
                    isFrightened = false;
                    frightRecoveryNodeCount = 0;
                    break;
                case VisitorState.Dazed:
                    FaeMaze.Props.DazeStreamerEffect.DestroyStreamers(transform);
                    isDazed = false;
                    break;
            }

            currentTimedState = VisitorState.Idle;
            currentStateDuration = 0f;
            currentStateTimer = 0f;

            RefreshStateFromFlags();

            // Recalculate path after timed state expires, since visitor may have been
            // moved by physics during daze or the old path may be stale
            if (state == VisitorState.Walking || state == VisitorState.Lured)
            {
                RecalculatePath();
            }

        }

        /// <summary>
        /// Forces the visitor to escape immediately.
        /// </summary>
        public virtual void ForceEscape()
        {
            SetState(VisitorState.Escaping, "ForceEscape");

            isFascinated = false;
            isFrightened = false;
            isMisdirected = false;
            misdirectEdgeIndex = -1;
            frightRecoveryNodeCount = 0;
            hasReachedLantern = false;
            ClearLanternInteraction();

            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// Ends the fascination state and resumes toward the original destination.
        /// Called when the fascination timer expires.
        /// </summary>
        protected virtual void EndFascination()
        {
            FaeMaze.Props.LanternStreamerEffect.DestroyStreamers(transform);

            isFascinated = false;
            hasReachedLantern = false;
            fascinationTimer = 0f;
            ClearLanternInteraction();

            // Ensure visitor is on a walkable tile before resuming pathing
            MoveToNearestWalkableTile();

            // Resume toward the most relevant destination:
            // If the visitor was lured (e.g., by MurmuringPaths fog), worldDestination points to the heart.
            // Otherwise, use originalDestination (the exit portal).
            Vector3 resumeDestination = (isLured && worldDestination != Vector3.zero)
                ? worldDestination
                : originalDestination;

            if (mazeGridBehaviour != null && mazeGridBehaviour.WorldSpaceMazeData != null && resumeDestination != Vector3.zero)
            {
                worldPath = BuildWorldPath(PhysicsPosition, resumeDestination);
                worldPathIndex = 0;
                worldDestination = resumeDestination;
                ResetSplineState();
                RefreshStateFromFlags(); // Use flag-based state (Lured if still lured, Walking otherwise)
            }
            else
            {
                NavigateToRandomPortal("EndFascination: no resume destination");
            }
        }

        /// <summary>
        /// Public method to forcibly end lantern fascination.
        /// Called when a lantern is destroyed/disabled while the visitor is fascinated by it.
        /// </summary>
        public virtual void EndLanternFascination()
        {
            if (currentFaeLantern == null && !isFascinated)
            {
                return; // Not fascinated by a lantern
            }

            if (!string.IsNullOrEmpty(entityLabel) && currentFaeLantern != null)
            {
                string propLabel = EntityLabels.GetPropLabel(currentFaeLantern.gameObject);
                GameEventLogger.LogVisitorFascinationEnd(entityLabel, propLabel, "lantern released");
            }

            FaeMaze.Props.LanternStreamerEffect.DestroyStreamers(transform);

            // Don't set cooldown since the lantern is being destroyed
            isFascinated = false;
            hasReachedLantern = false;
            fascinationTimer = 0f;
            ClearLanternInteraction();

            // Ensure visitor is on a walkable tile before resuming pathing
            MoveToNearestWalkableTile();

            // Resume toward the most relevant destination:
            // If lured (e.g., by MurmuringPaths fog), continue toward the heart (worldDestination).
            // Otherwise, revert to the original exit portal destination.
            Vector3 resumeDestination = (isLured && worldDestination != Vector3.zero)
                ? worldDestination
                : originalDestination;

            if (mazeGridBehaviour != null && mazeGridBehaviour.WorldSpaceMazeData != null && resumeDestination != Vector3.zero)
            {
                worldPath = BuildWorldPath(PhysicsPosition, resumeDestination);
                worldPathIndex = 0;
                worldDestination = resumeDestination;
                ResetSplineState();
                RefreshStateFromFlags(); // Use flag-based state (Lured if still lured, Walking otherwise)
            }
            else
            {
                NavigateToRandomPortal("EndLanternFascination: no resume destination");
            }
        }

        /// <summary>
        /// Public method to forcibly end FairyRing fascination.
        /// Called when a FairyRing is destroyed/disabled while the visitor is circling it.
        /// </summary>
        public virtual void EndRingFascination()
        {
            if (currentFairyRing == null)
            {
                return; // Not fascinated by a ring
            }

            if (!string.IsNullOrEmpty(entityLabel))
            {
                string propLabel = EntityLabels.GetPropLabel(currentFairyRing.gameObject);
                GameEventLogger.LogVisitorFascinationEnd(entityLabel, propLabel, "ring released");
            }

            // Clear without setting immunity (ring is being destroyed)
            isFascinated = false;
            currentFairyRing = null;
            fairyRingFascinationTimer = 0f;
            speedMultiplier = 1f;

            // Ensure visitor is on a walkable tile before resuming pathing
            MoveToNearestWalkableTile();

            // Visitor becomes lost after fascination ends
            SetLost();
        }

        /// <summary>
        /// Gets a random walkable node position for wandering after fascination ends.
        /// </summary>
        protected virtual Vector3 GetRandomWanderDestination()
        {
            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;

            // Collect all node tiles (junctions/clearings) as potential destinations
            var nodeTiles = new List<ForestMaze.WorldSpaceTile>();
            foreach (var tile in mazeData.Tiles)
            {
                if (tile.Category == ForestMaze.WorldSpaceTile.TileCategory.Node && tile.Walkable)
                {
                    nodeTiles.Add(tile);
                }
            }

            if (nodeTiles.Count > 0)
            {
                // Pick a random node
                int randomIndex = RandomManager.Range(0, nodeTiles.Count);
                Vector2 nodePos = nodeTiles[randomIndex].Position;
                return new Vector3(nodePos.x, nodePos.y, PhysicsPosition.z);
            }

            // Fallback: return current position if no nodes found
            return PhysicsPosition;
        }

        /// <summary>
        /// Returns a random portal position as a destination.
        /// Visitors without a destination should always head toward an exit portal.
        /// </summary>
        protected Vector3 GetRandomPortalDestination()
        {
            var dynamicMaze = Object.FindFirstObjectByType<FaeMaze.Systems.DynamicMazeGrowth>();
            if (dynamicMaze != null)
            {
                var portals = dynamicMaze.GetPortalPositions();
                if (portals != null && portals.Count > 0)
                {
                    int idx = RandomManager.Range(0, portals.Count);
                    return portals[idx];
                }
            }
            // Fallback to random node if no portals found
            return GetRandomWanderDestination();
        }

        /// <summary>
        /// Assigns a random portal destination and starts walking.
        /// Called whenever a visitor would otherwise become idle with no destination.
        /// </summary>
        protected void NavigateToRandomPortal(string reason)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null) return;

            Vector3 portalDest = GetRandomPortalDestination();
            worldPath = BuildWorldPath(PhysicsPosition, portalDest);
            worldPathIndex = 0;
            worldDestination = portalDest;
            originalDestination = portalDest;
            ResetSplineState();

            if (worldPath != null && worldPath.Count > 0)
            {
                SetState(VisitorState.Walking, $"NavigateToRandomPortal: {reason}");
            }
            else
            {
                SetState(VisitorState.Idle, $"NavigateToRandomPortal: no path to portal ({reason})");
            }
        }

        /// <summary>
        /// Makes this visitor fascinated by a FairyRing. The visitor abandons their path
        /// and circles the ring for the specified duration. When fascination ends, they become lost.
        /// </summary>
        /// <param name="ring">The FairyRing causing fascination</param>
        /// <param name="duration">How long the fascination lasts (seconds)</param>
        /// <param name="slowFactor">Speed multiplier while fascinated (0.5 = 50% speed)</param>
        public virtual void BecomeFascinatedByRing(FaeMaze.Props.FairyRing ring, float duration, float slowFactor)
        {
            if (isFascinationImmune || isMisdirected || isLured) return;

            if (!IsMovementState(state) && state != VisitorState.Idle)
            {
                return;
            }

            // Don't become fascinated if being lured by a wisp
            var followWisp = GetComponent<FaeMaze.Visitors.FollowWispBehavior>();
            if (followWisp != null && followWisp.IsFollowing)
            {
                return;
            }

            // Already fascinated by this ring
            if (currentFairyRing == ring)
            {
                return;
            }

            // Immune to this ring (just finished fascination, haven't left trigger yet)
            if (immuneToFairyRing == ring)
            {
                return;
            }

            // Clear any existing lantern fascination
            if (currentFaeLantern != null)
            {
                ClearLanternInteraction();
            }

            isFascinated = true;
            currentFairyRing = ring;
            fairyRingFascinationTimer = duration;
            speedMultiplier = slowFactor;

            if (!string.IsNullOrEmpty(entityLabel))
            {
                string propLabel = EntityLabels.GetPropLabel(ring.gameObject);
                GameEventLogger.LogVisitorFascination(entityLabel, propLabel);
            }

            // Calculate starting angle based on current position relative to ring
            Vector2 ringPos = new Vector2(ring.transform.position.x, ring.transform.position.y);
            Vector2 visitorPos = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
            Vector2 toVisitor = visitorPos - ringPos;
            fairyRingCircleAngle = Mathf.Atan2(toVisitor.y, toVisitor.x);

            // Check if we need to approach the ring first
            float distanceToRing = toVisitor.magnitude;
            fairyRingApproaching = distanceToRing > FairyRingCircleRadius;

            // Clear current path - we're now circling
            worldPath = null;
            worldPathIndex = 0;
            ResetSplineState();

            RefreshStateFromFlags();
        }

        /// <summary>
        /// Updates the FairyRing circling behavior. Called from Update when fascinated by a ring.
        /// </summary>
        protected virtual void UpdateFairyRingCircling()
        {
            if (currentFairyRing == null)
            {
                EndFairyRingFascination();
                return;
            }

            // Update timer
            fairyRingFascinationTimer -= Time.deltaTime;
            if (fairyRingFascinationTimer <= 0f)
            {
                EndFairyRingFascination();
                return;
            }

            // Drain visitor essence while circling the ring
            float drainAmount = ringEssenceDrainPerSecond * Time.deltaTime;
            currentEssence -= drainAmount;

            // Update visual opacity based on remaining essence
            UpdateEssenceOpacity();

            // Ring Tithe mutation: player receives a portion of drained essence
            float titheRate = PropMutationManager.Instance?.GetRingEssenceTithe() ?? 0f;
            if (titheRate > 0f && GameController.Instance != null)
            {
                int titheAmount = Mathf.FloorToInt(drainAmount * titheRate);
                if (titheAmount > 0)
                {
                    GameController.Instance.AddEssence(titheAmount, EssenceSource.RingTithe);
                }
            }

            if (currentEssence <= 0f)
            {
                // Visitor's essence is depleted - they escape
                currentEssence = 0f;
                OnEssenceDepleted();
                return;
            }

            Vector3 ringCenter = currentFairyRing.transform.position;
            float effectiveSpeed = moveSpeed * speedMultiplier;
            Vector3 oldPosition = PhysicsPosition;

            // Phase 1: Approach the ring until we're at circle radius
            if (fairyRingApproaching)
            {
                // Calculate target position at circle radius along current angle to ring
                float targetX = ringCenter.x + Mathf.Cos(fairyRingCircleAngle) * FairyRingCircleRadius;
                float targetY = ringCenter.y + Mathf.Sin(fairyRingCircleAngle) * FairyRingCircleRadius;
                Vector3 targetPos = new Vector3(targetX, targetY, PhysicsPosition.z);

                Vector3 newPosition = Vector3.MoveTowards(oldPosition, targetPos, effectiveSpeed * Time.deltaTime);

                // Check if we've reached the circle radius
                float distanceToRing = Vector2.Distance(new Vector2(newPosition.x, newPosition.y), new Vector2(ringCenter.x, ringCenter.y));
                if (distanceToRing <= FairyRingCircleRadius + 0.05f)
                {
                    fairyRingApproaching = false;
                }

                // Update animator direction
                Vector3 moveDir = (newPosition - oldPosition).normalized;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    UpdateAnimatorDirection(moveDir);
                }

                RecordMoveCause("FairyRing_Approach");
                desiredPosition = newPosition;
                hasDesiredPosition = true;
                return;
            }

            // Phase 2: Circle around the ring
            // Calculate angular velocity based on speed and radius (arc length = radius * angle)
            // We want to move at effectiveSpeed along the circle, so angular speed = linearSpeed / radius
            float angularSpeed = effectiveSpeed / FairyRingCircleRadius;

            // Update circle angle based on angular velocity
            fairyRingCircleAngle += angularSpeed * Time.deltaTime;
            if (fairyRingCircleAngle > Mathf.PI * 2f)
            {
                fairyRingCircleAngle -= Mathf.PI * 2f;
            }

            // Calculate new position directly on the circle at the new angle (fixed radius)
            float circleNewX = ringCenter.x + Mathf.Cos(fairyRingCircleAngle) * FairyRingCircleRadius;
            float circleNewY = ringCenter.y + Mathf.Sin(fairyRingCircleAngle) * FairyRingCircleRadius;
            Vector3 circleNewPosition = new Vector3(circleNewX, circleNewY, PhysicsPosition.z);

            // Update animator direction based on movement (tangent to circle)
            Vector3 circleMoveDir = (circleNewPosition - oldPosition).normalized;
            if (circleMoveDir.sqrMagnitude > 0.01f)
            {
                UpdateAnimatorDirection(circleMoveDir);
            }

            // Apply movement via physics
            RecordMoveCause("FairyRing_Circle");
            desiredPosition = circleNewPosition;
            hasDesiredPosition = true;
        }

        /// <summary>
        /// Ends FairyRing fascination. The visitor becomes lost and resumes pathing.
        /// </summary>
        protected virtual void EndFairyRingFascination()
        {
            // Set immunity to this ring until visitor leaves the trigger
            immuneToFairyRing = currentFairyRing;

            isFascinated = false;
            currentFairyRing = null;
            fairyRingFascinationTimer = 0f;
            speedMultiplier = 1f;

            // Ensure visitor is on a walkable tile before resuming pathing
            // The circling behavior may have left them in an unwalkable position
            MoveToNearestWalkableTile();

            // Visitor becomes lost after fascination ends
            SetLost();
        }

        /// <summary>
        /// Moves the visitor to the nearest walkable tile if they are currently
        /// in an unwalkable position (e.g., node center).
        /// </summary>
        public virtual void MoveToNearestWalkableTile()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            Vector2 currentPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);

            // Check if current position is on a walkable tile
            var nearbyTiles = mazeData.GetTilesNear(currentPos2D, 0.5f);
            bool isOnWalkable = false;
            foreach (var tile in nearbyTiles)
            {
                if (tile.Walkable && Vector2.Distance(currentPos2D, tile.Position) < 0.5f)
                {
                    isOnWalkable = true;
                    break;
                }
            }

            // If already on walkable tile, no need to move
            if (isOnWalkable)
            {
                return;
            }

            // Find nearest walkable tile and move there
            var nearestTile = FindNearestWalkableTile(mazeData, currentPos2D);
            if (nearestTile != null)
            {
                Vector3 newPos = new Vector3(nearestTile.Position.x, nearestTile.Position.y, PhysicsPosition.z);
                RecordMoveCause("EscapeToNearestWalkable");
                desiredPosition = newPos;
                hasDesiredPosition = true;
            }
        }

        /// <summary>
        /// Returns true if currently fascinated by a FairyRing (circling behavior).
        /// </summary>
        public bool IsFascinatedByRing => currentFairyRing != null;

        /// <summary>
        /// Clears immunity to a FairyRing. Called when the visitor exits the ring's trigger.
        /// </summary>
        /// <param name="ring">The ring to clear immunity for</param>
        public void ClearFairyRingImmunity(FaeMaze.Props.FairyRing ring)
        {
            if (immuneToFairyRing == ring)
            {
                immuneToFairyRing = null;
            }
        }

        #endregion

        #region Physics Setup

        protected virtual void SetupPhysics()
        {
            // Set layer to "Visitor" (layer 6) so visitors don't collide with each other
            // The physics collision matrix has Visitor-Visitor collisions disabled
            SetLayerRecursively(gameObject, 6);  // Set ALL objects to Visitor layer

            rb3D = GetComponent<Rigidbody>();
            if (rb3D == null)
            {
                rb3D = gameObject.AddComponent<Rigidbody>();
            }

            // Non-kinematic for physics collision response with solid colliders
            rb3D.isKinematic = false;
            rb3D.useGravity = false;
            rb3D.mass = 10f;  // Higher mass = collision response has more effect
            rb3D.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;  // Freeze Z to prevent sinking
            rb3D.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;  // Best for dynamic vs kinematic
            rb3D.interpolation = RigidbodyInterpolation.Interpolate;
            rb3D.linearDamping = 0f;  // No damping — velocity is fully controlled each FixedUpdate via VelocityChange
            rb3D.angularDamping = 0f;

            // Remove any legacy colliders on the root GameObject IMMEDIATELY - we use the "Detect" child collider now
            // The "Detect" child is baked into the visitor model prefab with proper collider settings
            // Using DestroyImmediate ensures colliders are gone before any physics calculations
            SphereCollider sphereCollider = GetComponent<SphereCollider>();
            CapsuleCollider rootCapsule = GetComponent<CapsuleCollider>();

            if (sphereCollider != null)
            {
                DestroyImmediate(sphereCollider);
            }
            if (rootCapsule != null)
            {
                DestroyImmediate(rootCapsule);
            }

            // The actual collision collider is on the "Detect" child of the model instance
            // It will be set up in SetupDetectCollider() after the model is instantiated
        }

        /// <summary>
        /// Sets up the Detect collider from the model instance.
        /// Called after Setup3DModel() instantiates the model prefab.
        /// The Detect child has a CapsuleCollider baked into the prefab.
        /// </summary>
        protected virtual void SetupDetectCollider()
        {
            // The Detect collider can be either:
            // 1. A child of modelInstance (if modelPrefab was used)
            // 2. A direct child of this visitor (if the visitor prefab IS the model)
            Transform searchRoot = modelInstance != null ? modelInstance.transform : transform;

            // Find the Detect child in the hierarchy
            Transform detectTransform = searchRoot.Find("Detect");
            if (detectTransform == null)
            {
                // Try searching recursively
                detectTransform = FindChildRecursive(searchRoot, "Detect");
            }

            if (detectTransform == null)
            {
                // Detect child not found — visitor collision detection will not work
                return;
            }

            // Set the Detect object to Visitor layer for collision matrix
            detectTransform.gameObject.layer = 6;  // Visitor layer

            // CRITICAL: Reparent Detect directly under the visitor root and reset transform
            // The Detect may be deep in a rotated hierarchy (e.g., under Armature with 90° rotation).
            // By reparenting to root, we ensure the collider aligns with world axes.
            detectTransform.SetParent(transform, false);  // false = don't preserve world position
            detectTransform.localRotation = Quaternion.identity;
            detectTransform.localPosition = Vector3.zero;
            detectTransform.localScale = Vector3.one * 0.5f;

            // Get the CapsuleCollider on the Detect object
            CapsuleCollider detectCollider = detectTransform.GetComponent<CapsuleCollider>();
            if (detectCollider == null)
            {
                // Detect child has no CapsuleCollider
                return;
            }

            // Ensure it's not a trigger - we need solid collision with tongue
            detectCollider.isTrigger = false;
            detectCollider.enabled = true;

            // CRITICAL: Reconfigure capsule to extend along Z-axis (world vertical)
            // The game uses XY as ground plane and -Z as up (toward camera).
            // Ground is at Z=0, tongue extends horizontally at roughly Z=0.
            // The visitor mesh is rendered above ground (negative Z).
            // We want the capsule to cover from ground level up through the visitor's body.
            //
            // Direction 2 = Z-axis (now aligned with world Z since we reparented to root)
            detectCollider.direction = 2;  // Z-axis
            detectCollider.center = new Vector3(0, 0, -0.5f);  // Centered at Z=-0.5 (above ground)
            detectCollider.height = 1.5f;  // Extends from Z=+0.25 to Z=-1.25
            detectCollider.radius = 0.4f;  // XY radius for body width

            // CRITICAL: Remove any Rigidbody on the Detect child object IMMEDIATELY.
            // When a collider doesn't have its own Rigidbody, Unity treats it as a "compound collider"
            // that uses the parent's Rigidbody. This allows:
            // - Visitor root Rigidbody (non-kinematic) to receive physics responses
            // - Tongue bone colliders (kinematic) to push the visitor
            // If Detect has its own Rigidbody, collision events go to Detect instead of the visitor root!
            // We MUST use DestroyImmediate to ensure the Rigidbody is gone before any physics runs.
            Rigidbody detectRb = detectTransform.GetComponent<Rigidbody>();
            if (detectRb != null)
            {
                DestroyImmediate(detectRb);
            }
        }

        /// <summary>
        /// Recursively searches for a child transform by name.
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Sets the layer of a GameObject and all its children recursively.
        /// Used to ensure all visitor colliders are on the Visitor layer (6).
        /// </summary>
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// Checks if this visitor is currently overlapping any tongue bone colliders.
        /// Uses Physics.OverlapSphere to actively detect colliders rather than relying on collision events.
        /// If overlapping, calculates a push direction away from the nearest collider.
        /// </summary>
        /// <param name="pushDirection">Output: normalized direction to push visitor away from tongue</param>
        /// <param name="pushStrength">Output: strength of push based on overlap depth</param>
        /// <returns>True if overlapping any tongue bone collider</returns>
        protected bool IsOverlappingTongueBoneCollider(out Vector3 pushDirection, out float pushStrength)
        {
            pushDirection = Vector3.zero;
            pushStrength = 0f;

            // Check a small sphere around the visitor's position
            // Use the visitor's actual collider bounds if available
            Vector3 checkPos = PhysicsPosition;
            float checkRadius = 0.5f;  // Approximate visitor radius

            // Find the Detect collider for more accurate bounds
            Transform detectTransform = transform.Find("Detect");
            if (detectTransform == null)
            {
                // Try to find it in children (may be nested in model)
                detectTransform = FindChildRecursive(transform, "Detect");
            }

            if (detectTransform != null)
            {
                CapsuleCollider capsule = detectTransform.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    checkPos = detectTransform.position;
                    checkRadius = capsule.radius * Mathf.Max(detectTransform.lossyScale.x, detectTransform.lossyScale.y);
                }
            }

            // Check for overlapping colliders - use a larger radius to account for Z-level differences
            // IMPORTANT: Use QueryTriggerInteraction.Collide because bone colliders are triggers!
            // Bone colliders may be at different Z levels (e.g., Z=-0.71 while visitor is at Z=0)
            // so we need a search radius that spans the Z gap plus the XY overlap distance
            float searchRadius = checkRadius + 1.5f;  // Increased to account for Z-level differences
            Collider[] overlaps = Physics.OverlapSphere(checkPos, searchRadius, ~0, QueryTriggerInteraction.Collide);

            // Find all tongue bone colliders and calculate combined push direction
            Vector3 accumulatedPush = Vector3.zero;
            int tongueColliderCount = 0;
            float closestDist = float.MaxValue;

            foreach (Collider col in overlaps)
            {
                // Check for both SolidCollider_N (new solid colliders) and BoneCollider_N (legacy/trigger)
                // SolidCollider_N are the solid physics blocking colliders
                // BoneCollider_N are now trigger colliders for detection
                if (col.gameObject.name.StartsWith("SolidCollider_") || col.gameObject.name.StartsWith("BoneCollider_"))
                {
                    // Use transform.position for animated/scaled colliders (NOT bounds.center!)
                    Vector3 colliderWorldPos = col.transform.position;

                    // Calculate 2D distance (XY plane - our play surface)
                    Vector2 visitor2D = new Vector2(checkPos.x, checkPos.y);
                    Vector2 collider2D = new Vector2(colliderWorldPos.x, colliderWorldPos.y);
                    float dist2D = Vector2.Distance(visitor2D, collider2D);

                    // Get the collider's world radius
                    // NOTE: Bone colliders have very small actual radius (0.0045 world units)
                    // but they represent a continuous tongue surface, so we use a larger effective radius
                    SphereCollider sphere = col as SphereCollider;
                    float colliderWorldRadius = 0.15f; // Default
                    if (sphere != null)
                    {
                        float actualRadius = sphere.radius * Mathf.Max(
                            col.transform.lossyScale.x,
                            col.transform.lossyScale.y,
                            col.transform.lossyScale.z);
                        // Use larger effective radius (0.3) to represent tongue surface between colliders
                        // Colliders are spaced every 10 bones, so we need overlap between them
                        colliderWorldRadius = Mathf.Max(actualRadius, 0.3f);
                    }

                    // Calculate how much we're overlapping (penetration depth)
                    float combinedRadius = checkRadius + colliderWorldRadius;
                    float penetration = combinedRadius - dist2D;

                    if (penetration > 0)
                    {
                        // We ARE overlapping - calculate push away from this collider
                        Vector2 pushDir2D = (visitor2D - collider2D);
                        if (pushDir2D.sqrMagnitude > 0.0001f)
                        {
                            pushDir2D = pushDir2D.normalized;
                        }
                        else
                        {
                            // Visitor is exactly at collider center - push in any direction
                            pushDir2D = Vector2.right;
                        }

                        // Weight by penetration depth - deeper overlap = stronger push
                        accumulatedPush += new Vector3(pushDir2D.x, pushDir2D.y, 0) * penetration;
                        tongueColliderCount++;

                        if (dist2D < closestDist)
                        {
                            closestDist = dist2D;
                        }
                    }
                }
            }

            if (tongueColliderCount > 0)
            {
                // Normalize the accumulated push direction
                pushDirection = accumulatedPush.normalized;
                // Push strength based on deepest penetration (clamped to reasonable range)
                pushStrength = Mathf.Clamp(accumulatedPush.magnitude / tongueColliderCount, 0.1f, 2.0f);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Shared handler for tongue contact detection from both collision and trigger events.
        /// Notifies HeartOfTheMaze and HeartwardGrasp when visitor touches tongue colliders.
        /// </summary>
        private void HandleTongueContact(string objectName)
        {
            bool isTongueCollider = objectName.StartsWith("SolidCollider_") ||
                                    objectName.StartsWith("BoneCollider_") ||
                                    objectName == "TipTrigger";

            if (!isTongueCollider) return;

            var heart = FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
            if (heart != null)
            {
                heart.NotifyVisitorTouchedTongue(this);
            }

            var graspEffect = HeartPowers.HeartwardGraspEffect.ActiveInstance;
            if (graspEffect != null)
            {
                graspEffect.NotifyVisitorTouchedGraspTongue(this);
            }
        }

        /// <summary>
        /// Called by Unity physics when this visitor's collider starts touching another solid collider.
        /// </summary>
        protected virtual void OnCollisionEnter(Collision collision)
        {
            HandleTongueContact(collision.gameObject.name);
        }

        /// <summary>
        /// Called by Unity physics when this visitor's collider enters a trigger collider.
        /// </summary>
        protected virtual void OnTriggerEnter(Collider other)
        {
            HandleTongueContact(other.gameObject.name);
        }

        protected virtual void Setup3DModel()
        {
            if (modelPrefab == null)
            {
                // Model is baked into the prefab root.
                // Use the per-prefab modelFacingOffsetDegrees to compensate for model facing.
                // Some models (nature, lost, sleepy) face backwards and need offset=180.
                // Models with complex Armature rotations handle facing internally and need offset=0.
                modelBaseRotation = Quaternion.Euler(0f, 0f, modelFacingOffsetDegrees);
                modelBaseRotationCaptured = true;
                return;
            }

            // Instantiate the model prefab
            modelInstance = Instantiate(modelPrefab, transform);
            modelInstance.transform.localPosition = Vector3.zero;
            // Keep prefab's original rotation (e.g., X:90 for models designed for XY plane)
            // Capture base rotation so directional Z rotation can be applied on top of it
            modelBaseRotation = modelInstance.transform.localRotation;
            modelBaseRotationCaptured = true;
            modelInstance.transform.localScale = Vector3.one;

            // Look for Animator in the model (should be on root or child)
            if (animator == null)
            {
                animator = modelInstance.GetComponentInChildren<Animator>();
            }

            // Cache renderers for opacity effects
            cachedRenderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        }

        #endregion

        #region Essence Opacity

        /// <summary>
        /// Updates the visitor's visual opacity based on current essence vs starting essence.
        /// Called periodically during essence drain to show visitors fading as they lose essence.
        /// </summary>
        protected void UpdateEssenceOpacity()
        {
            // Lazy-initialize renderers if not cached yet (e.g., visitor IS the model, no modelPrefab)
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                cachedRenderers = GetComponentsInChildren<Renderer>(true);
                if (cachedRenderers == null || cachedRenderers.Length == 0) return;
            }
            if (startingEssence <= 0f) return;

            // Throttle updates for performance
            if (Time.time - lastOpacityUpdate < OPACITY_UPDATE_INTERVAL) return;
            lastOpacityUpdate = Time.time;

            float essenceRatio = Mathf.Clamp01(currentEssence / startingEssence);

            // Only start fading once essence drops below 80%
            float opacity;
            if (essenceRatio >= 0.8f)
            {
                // Restore full opacity if essence is above 80%
                if (!renderersAreFading) return;
                opacity = 1f;
            }
            else
            {
                // Map 0.8 → 1.0 opacity, 0.0 → MIN_OPACITY
                opacity = Mathf.Lerp(MIN_OPACITY, 1f, essenceRatio / 0.8f);
            }

            bool shouldFade = opacity < 0.99f;

            foreach (var rend in cachedRenderers)
            {
                if (rend == null) continue;

                // IMPORTANT: rend.materials returns a COPY - we must assign it back after modification
                var materials = rend.materials;
                bool materialsModified = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    if (mat == null) continue;

                    if (shouldFade)
                    {
                        // Switch to transparent rendering mode (URP Lit shader)
                        mat.SetFloat("_Surface", 1); // 1 = Transparent in URP
                        mat.SetFloat("_Blend", 0);   // Alpha blend
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        mat.SetFloat("_ZWrite", 0);  // Disable depth write for transparency
                        mat.SetOverrideTag("RenderType", "Transparent");
                        mat.renderQueue = 3000;
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

                        // Set alpha on base color
                        if (mat.HasProperty("_BaseColor"))
                        {
                            Color c = mat.GetColor("_BaseColor");
                            c.a = opacity;
                            mat.SetColor("_BaseColor", c);
                        }
                        if (mat.HasProperty("_Color"))
                        {
                            Color c = mat.GetColor("_Color");
                            c.a = opacity;
                            mat.SetColor("_Color", c);
                        }
                        materialsModified = true;
                    }
                    else if (renderersAreFading)
                    {
                        // Restore opaque rendering mode
                        mat.SetFloat("_Surface", 0); // 0 = Opaque in URP
                        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetFloat("_ZWrite", 1);  // Re-enable depth write
                        mat.SetOverrideTag("RenderType", "Opaque");
                        mat.renderQueue = -1;
                        mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

                        if (mat.HasProperty("_BaseColor"))
                        {
                            Color c = mat.GetColor("_BaseColor");
                            c.a = 1f;
                            mat.SetColor("_BaseColor", c);
                        }
                        if (mat.HasProperty("_Color"))
                        {
                            Color c = mat.GetColor("_Color");
                            c.a = 1f;
                            mat.SetColor("_Color", c);
                        }
                        materialsModified = true;
                    }
                }

                // Assign modified materials back to renderer
                if (materialsModified)
                {
                    rend.materials = materials;
                }
            }

            renderersAreFading = shouldFade;
        }

        #endregion

        #region State Aura Visual

        /// <summary>
        /// Sets up the state aura light for visual feedback on visitor state.
        /// Creates a point light that changes color based on the visitor's current state.
        /// </summary>
        protected virtual void SetupStateAura()
        {
            if (stateAuraObject != null)
                return;

            stateAuraObject = new GameObject("StateAura");
            stateAuraObject.transform.SetParent(transform);
            stateAuraObject.transform.localPosition = new Vector3(0f, 0f, AURA_Z_OFFSET);

            stateAuraLight = stateAuraObject.AddComponent<Light>();
            stateAuraLight.type = LightType.Point;
            stateAuraLight.range = AURA_LIGHT_RANGE;
            stateAuraLight.intensity = AURA_LIGHT_INTENSITY;
            stateAuraLight.shadows = LightShadows.None;
            stateAuraLight.renderMode = LightRenderMode.Auto;

            // Start with no aura (Walking state has no special color)
            stateAuraLight.enabled = false;
            lastAuraState = VisitorState.Walking;
        }

        /// <summary>
        /// Updates the state aura color based on current visitor state.
        /// Only shows aura for special states (not Walking or Idle).
        /// </summary>
        protected virtual void UpdateStateAura()
        {
            if (stateAuraLight == null)
                return;

            // Only update if state changed
            if (state == lastAuraState)
                return;

            lastAuraState = state;

            // Get color for current state
            Color auraColor = GetStateAuraColor(state);

            if (auraColor.a > 0f)
            {
                stateAuraLight.color = auraColor;
                stateAuraLight.enabled = true;
            }
            else
            {
                // No aura for this state
                stateAuraLight.enabled = false;
            }
        }

        /// <summary>
        /// Gets the aura color for a given visitor state.
        /// Returns transparent color for states that shouldn't show an aura.
        /// </summary>
        protected virtual Color GetStateAuraColor(VisitorState visitorState)
        {
            switch (visitorState)
            {
                case VisitorState.Fascinated:
                    // Pink/magenta - entranced by fae magic
                    return new Color(1f, 0.4f, 0.8f, 1f);

                case VisitorState.Confused:
                    // Yellow/gold - lost and wandering
                    return new Color(1f, 0.85f, 0.2f, 1f);

                case VisitorState.Frightened:
                    // Cyan/pale blue - fear
                    return new Color(0.5f, 0.9f, 1f, 1f);

                case VisitorState.Lured:
                    // Green - drawn toward heart
                    return new Color(0.3f, 1f, 0.5f, 1f);

                case VisitorState.Dazed:
                    // Purple - stunned
                    return new Color(0.7f, 0.4f, 1f, 1f);

                case VisitorState.Walking:
                case VisitorState.Idle:
                case VisitorState.Grabbed:   // Lights disabled directly by HeartOfTheMaze
                case VisitorState.Consumed:
                case VisitorState.Escaping:
                default:
                    // No aura for normal/terminal states
                    return Color.clear;
            }
        }

        /// <summary>
        /// Updates animator playback speed based on visitor state.
        /// Pauses animation when idle, plays at 2x when frightened/fleeing.
        /// </summary>
        protected virtual void UpdateAnimatorSpeed()
        {
            if (animator == null)
                return;

            // Only update if state changed
            if (state == lastAnimatorSpeedState)
                return;

            lastAnimatorSpeedState = state;

            // Set animator speed based on state
            switch (state)
            {
                case VisitorState.Idle:
                case VisitorState.Grabbed:
                case VisitorState.Dazed:
                    // Pause animation when not moving
                    animator.speed = 0f;
                    break;

                case VisitorState.Frightened:
                    // Play at 2x speed when fleeing
                    animator.speed = 2f;
                    break;

                case VisitorState.Walking:
                case VisitorState.Fascinated:
                case VisitorState.Confused:
                case VisitorState.Lured:
                case VisitorState.Escaping:
                case VisitorState.Consumed:
                default:
                    // Normal speed for regular movement
                    animator.speed = 1f;
                    break;
            }
        }

        #endregion

        #region Detour State

        /// <summary>
        /// Resets detour-specific state when starting a new path or becoming fascinated.
        /// Derived classes should clear confusion flags, misstep tracking, etc.
        /// </summary>
        protected virtual void ResetDetourState()
        {
            // Base implementation - just clear confusion flag
            isConfused = false;
        }

        /// <summary>
        /// Randomly decides whether to recover from confusion.
        /// </summary>
        protected void DecideRecoveryFromConfusion()
        {
            float roll = RandomManager.Value;
            bool recover = roll <= 0.5f;
            isConfused = !recover;
        }

        /// <summary>
        /// Builds a confusion detour path that visits at least 2 random nodes before
        /// returning to the correct path toward the destination.
        /// </summary>
        /// <param name="minDetourNodes">Minimum number of random nodes to visit (default 2)</param>
        /// <returns>True if a detour path was successfully built</returns>
        protected bool BuildConfusionDetourPath(int minDetourNodes = 2)
        {
            if (mazeGridBehaviour == null)
            {
                return false;
            }

            var graphState = mazeGridBehaviour.ForestMapState;
            if (graphState == null || graphState.Nodes.Count < 3)
            {
                return false;
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            if (mazeData == null)
            {
                return false;
            }

            // Find the nearest node to our current position
            Vector2 currentPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
            int nearestNodeId = FindNearestNodeIndex(graphState, currentPos2D);
            if (nearestNodeId < 0)
            {
                return false;
            }

            // Build a list of detour nodes by randomly walking the graph
            var detourNodeIds = new List<int>();
            var visitedNodeIds = new HashSet<int>();
            int currentNodeId = nearestNodeId;
            visitedNodeIds.Add(currentNodeId);

            // Walk randomly through the graph for at least minDetourNodes steps
            for (int i = 0; i < minDetourNodes + RandomManager.Range(0, 2); i++)
            {
                var currentNode = graphState.Nodes[currentNodeId];

                // Get connected nodes via incident edges
                var connectedNodeIds = new List<int>();
                foreach (int edgeId in currentNode.IncidentEdges)
                {
                    if (edgeId < 0 || edgeId >= graphState.Edges.Count)
                        continue;

                    var edge = graphState.Edges[edgeId];

                    // Get the other node of this edge
                    int otherNodeId = -1;
                    if (edge.NodeA == currentNodeId && edge.NodeB.HasValue)
                        otherNodeId = edge.NodeB.Value;
                    else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNodeId)
                        otherNodeId = edge.NodeA;

                    if (otherNodeId >= 0 && !visitedNodeIds.Contains(otherNodeId))
                    {
                        connectedNodeIds.Add(otherNodeId);
                    }
                }

                if (connectedNodeIds.Count == 0)
                {
                    // No unvisited neighbors - allow revisiting
                    foreach (int edgeId in currentNode.IncidentEdges)
                    {
                        if (edgeId < 0 || edgeId >= graphState.Edges.Count)
                            continue;

                        var edge = graphState.Edges[edgeId];
                        int otherNodeId = -1;
                        if (edge.NodeA == currentNodeId && edge.NodeB.HasValue)
                            otherNodeId = edge.NodeB.Value;
                        else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNodeId)
                            otherNodeId = edge.NodeA;

                        if (otherNodeId >= 0)
                            connectedNodeIds.Add(otherNodeId);
                    }
                }

                if (connectedNodeIds.Count == 0)
                    break;

                // Pick a random connected node
                int randomIndex = RandomManager.Range(0, connectedNodeIds.Count);
                int nextNodeId = connectedNodeIds[randomIndex];

                detourNodeIds.Add(nextNodeId);
                visitedNodeIds.Add(nextNodeId);
                currentNodeId = nextNodeId;
            }

            if (detourNodeIds.Count < minDetourNodes)
            {
                return false;
            }

            // Build path: current position -> each detour node -> final destination
            var fullPath = new List<Vector3>();
            Vector3 pathStart = PhysicsPosition;

            // Path to each detour node
            foreach (int nodeId in detourNodeIds)
            {
                var node = graphState.Nodes[nodeId];
                Vector3 nodeWorldPos = new Vector3(node.Position.x, node.Position.y, 0);

                var segmentPath = BuildWorldPath(pathStart, nodeWorldPos);
                if (segmentPath != null && segmentPath.Count > 0)
                {
                    // Skip first point if it's too close to last point in fullPath (avoid duplicates)
                    int startIdx = (fullPath.Count > 0 && segmentPath.Count > 0 &&
                                   Vector3.Distance(fullPath[fullPath.Count - 1], segmentPath[0]) < 0.5f) ? 1 : 0;

                    for (int i = startIdx; i < segmentPath.Count; i++)
                    {
                        fullPath.Add(segmentPath[i]);
                    }
                }
                pathStart = nodeWorldPos;
            }

            // Finally, path from last detour node to destination
            var finalSegment = BuildWorldPath(pathStart, worldDestination);
            if (finalSegment != null && finalSegment.Count > 0)
            {
                int startIdx = (fullPath.Count > 0 && finalSegment.Count > 0 &&
                               Vector3.Distance(fullPath[fullPath.Count - 1], finalSegment[0]) < 0.5f) ? 1 : 0;

                for (int i = startIdx; i < finalSegment.Count; i++)
                {
                    fullPath.Add(finalSegment[i]);
                }
            }

            if (fullPath.Count == 0)
            {
                return false;
            }

            // Set the new detour path
            worldPath = fullPath;
            worldPathIndex = 0;
            ResetSplineState();

            return true;
        }

        /// <summary>
        /// Builds a path for a frightened visitor to flee from the fright source.
        /// Picks a random adjacent node (excluding backtracking toward the fright source).
        /// </summary>
        protected virtual void BuildFrightenedFleePath()
        {
            if (mazeGridBehaviour == null)
            {
                return;
            }

            var graphState = mazeGridBehaviour.ForestMapState;
            if (graphState == null || graphState.Nodes.Count == 0)
            {
                return;
            }

            // Find nearest node to current position
            Vector2 currentPos2D = new Vector2(PhysicsPosition.x, PhysicsPosition.y);
            int nearestNodeId = FindNearestNodeIndex(graphState, currentPos2D);
            if (nearestNodeId < 0)
            {
                return;
            }

            // Get destination node (random adjacent node, preferring away from fright source)
            int destNodeId = PickRandomAdjacentNodeAwayFromSource(graphState, nearestNodeId);
            if (destNodeId < 0)
            {
                return;
            }

            var destNode = graphState.Nodes[destNodeId];
            Vector3 destWorldPos = new Vector3(destNode.Position.x, destNode.Position.y, PhysicsPosition.z);

            // Build path to the chosen node
            worldPath = BuildWorldPath(PhysicsPosition, destWorldPos);
            worldPathIndex = 0;
            worldDestination = destWorldPos;
            ResetSplineState();
        }

        /// <summary>
        /// Picks a random adjacent node from the given node, preferring nodes away from the fright source.
        /// Never picks the node that would backtrack toward where the visitor came from.
        /// </summary>
        protected int PickRandomAdjacentNodeAwayFromSource(
            ForestMaze.PlanarForestMazeGenerator.ForestMapState graphState,
            int currentNodeId)
        {
            if (currentNodeId < 0 || currentNodeId >= graphState.Nodes.Count)
                return -1;

            var currentNode = graphState.Nodes[currentNodeId];
            Vector2 currentPos = currentNode.Position;
            Vector2 frightSource2D = new Vector2(frightSourcePosition.x, frightSourcePosition.y);

            // Collect all connected nodes
            var candidates = new List<(int nodeId, float distFromFright)>();

            foreach (int edgeId in currentNode.IncidentEdges)
            {
                if (edgeId < 0 || edgeId >= graphState.Edges.Count)
                    continue;

                var edge = graphState.Edges[edgeId];

                // Get the other node of this edge
                int otherNodeId = -1;
                if (edge.NodeA == currentNodeId && edge.NodeB.HasValue)
                    otherNodeId = edge.NodeB.Value;
                else if (edge.NodeB.HasValue && edge.NodeB.Value == currentNodeId)
                    otherNodeId = edge.NodeA;

                if (otherNodeId < 0 || otherNodeId >= graphState.Nodes.Count)
                    continue;

                var otherNode = graphState.Nodes[otherNodeId];
                float distFromFright = Vector2.Distance(otherNode.Position, frightSource2D);

                candidates.Add((otherNodeId, distFromFright));
            }

            if (candidates.Count == 0)
                return -1;

            // If only one option, take it
            if (candidates.Count == 1)
                return candidates[0].nodeId;

            // Sort by distance from fright source (farthest first)
            candidates.Sort((a, b) => b.distFromFright.CompareTo(a.distFromFright));

            // Remove the candidate closest to fright source (no backtracking)
            // But keep at least one option
            if (candidates.Count > 1)
            {
                candidates.RemoveAt(candidates.Count - 1);
            }

            // Pick randomly from remaining candidates
            int randomIndex = RandomManager.Range(0, candidates.Count);
            return candidates[randomIndex].nodeId;
        }

        /// <summary>
        /// Called when a frightened visitor arrives at a node.
        /// Handles recovery check and picking the next random edge.
        /// </summary>
        protected virtual void HandleFrightenedAtNode()
        {
            frightRecoveryNodeCount++;

            // Check for portal - if at a portal, escape immediately
            if (IsAtPortal())
            {
                SetState(VisitorState.Escaping, "HandleFrightenedAtNode: at portal");
                OnExitedThroughPortal();
                return;
            }

            // Accumulating recovery chance: 25% per node visited
            // At node 1: 25%, node 2: 50%, node 3: 75%, node 4: 100%
            float recoveryChance = frightRecoveryNodeCount * frightenedRecoveryChancePerNode;
            if (RandomManager.Value <= recoveryChance)
            {
                // Recovered from fright
                isFrightened = false;
                frightRecoveryNodeCount = 0;
                currentTimedState = VisitorState.Idle;
                currentStateDuration = 0f;
                currentStateTimer = 0f;
                RefreshStateFromFlags();

                // Resume normal path to destination
                RecalculatePath();
                return;
            }

            // Still frightened - pick a new random edge to flee down
            BuildFrightenedFleePath();
        }

        #endregion

        #region Gizmos

        protected virtual void OnDrawGizmos()
        {
            // Draw graph path with smooth curves (if using graph navigation)
            if (useGraphNavigation && graphPath != null && graphPath.IsValid && mazeGridBehaviour != null)
            {
                var mapState = mazeGridBehaviour.ForestMapState;
                if (mapState != null)
                {
                    DrawGraphPathGizmos(mapState);
                }
            }

            // Draw world-space (tile) path only if NOT using graph navigation or as fallback
            if (!useGraphNavigation && worldPath != null && worldPath.Count > 0)
            {
                // Draw path lines in cyan
                Gizmos.color = Color.cyan;
                for (int i = 0; i < worldPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                }

                // Draw every waypoint as a small sphere
                // Already traversed waypoints in gray
                // Upcoming waypoints in magenta
                // Current target in yellow (larger)
                for (int i = 0; i < worldPath.Count; i++)
                {
                    if (i < worldPathIndex)
                    {
                        // Already passed - gray
                        Gizmos.color = Color.gray;
                        Gizmos.DrawWireSphere(worldPath[i], 0.15f);
                    }
                    else if (i == worldPathIndex && IsMovementState(state))
                    {
                        // Current target - yellow (larger)
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(worldPath[i], 0.4f);
                        Gizmos.DrawSphere(worldPath[i], 0.2f);
                    }
                    else
                    {
                        // Upcoming waypoints - magenta
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(worldPath[i], 0.2f);
                    }
                }

            }

            // Draw destination
            if (originalDestination != Vector3.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(originalDestination, 0.5f);
            }

            // Draw visitor position and direction
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }

        /// <summary>
        /// Draws the graph path with polylines for edges and arcs for nodes.
        /// </summary>
        private void DrawGraphPathGizmos(ForestMaze.PlanarForestMazeGenerator.ForestMapState mapState)
        {
            for (int segIdx = 0; segIdx < graphPath.Segments.Count; segIdx++)
            {
                var segment = graphPath.Segments[segIdx];
                bool isCurrentSegment = segIdx == graphSegmentIndex;
                bool isPastSegment = segIdx < graphSegmentIndex;

                // Color: Green for current, Gray for past, Cyan for future
                if (isPastSegment)
                    Gizmos.color = Color.gray;
                else if (isCurrentSegment)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.cyan;

                if (segment.Type == ForestMaze.SegmentType.Edge)
                {
                    // Draw edge using polyline (the actual path where walls are placed)
                    if (segment.Index < mapState.Edges.Count)
                    {
                        var edge = mapState.Edges[segment.Index];
                        // ALWAYS use polyline - it matches the actual path visitors follow
                        if (edge.PolylinePoints != null && edge.PolylinePoints.Count >= 2)
                        {
                            for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                            {
                                Vector3 p1 = new Vector3(edge.PolylinePoints[i].x, edge.PolylinePoints[i].y, 0f);
                                Vector3 p2 = new Vector3(edge.PolylinePoints[i + 1].x, edge.PolylinePoints[i + 1].y, 0f);
                                Gizmos.DrawLine(p1, p2);
                            }
                        }
                    }

                    // Draw entry/exit points
                    Gizmos.color = isCurrentSegment ? Color.yellow : Color.white;
                    Gizmos.DrawWireSphere(new Vector3(segment.EntryPoint.x, segment.EntryPoint.y, 0f), 0.2f);
                    Gizmos.DrawWireSphere(new Vector3(segment.ExitPoint.x, segment.ExitPoint.y, 0f), 0.2f);
                }
                else // Node segment
                {
                    // Draw node arc
                    if (segment.Index < mapState.Nodes.Count)
                    {
                        var node = mapState.Nodes[segment.Index];

                        // Draw arc through walkable ring
                        var arcPoints = ForestMaze.NodeTraverser.GetNodeArc(node, segment.EntryPoint, segment.ExitPoint, 8);
                        for (int i = 0; i < arcPoints.Count - 1; i++)
                        {
                            Vector3 p1 = new Vector3(arcPoints[i].x, arcPoints[i].y, 0f);
                            Vector3 p2 = new Vector3(arcPoints[i + 1].x, arcPoints[i + 1].y, 0f);
                            Gizmos.DrawLine(p1, p2);
                        }

                        // Draw node center reference
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange
                        Gizmos.DrawWireSphere(new Vector3(node.Position.x, node.Position.y, 0f), 1.0f);
                    }
                }

            }

            // Draw current position on graph if in edge movement
            if (graphSegmentIndex < graphPath.Segments.Count)
            {
                var currentSeg = graphPath.Segments[graphSegmentIndex];
                if (currentSeg.Type == ForestMaze.SegmentType.Edge && currentSeg.Index < mapState.Edges.Count)
                {
                    var edge = mapState.Edges[currentSeg.Index];
                    Vector2 currentGraphPos = ForestMaze.EdgeFollower.GetPosition(edge, graphSegmentProgress);
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(new Vector3(currentGraphPos.x, currentGraphPos.y, 0f), 0.3f);

                }
            }
        }

        #endregion
    }
}

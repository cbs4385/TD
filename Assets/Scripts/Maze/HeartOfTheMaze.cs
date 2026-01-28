using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Visitors;
using System;
using System.Collections.Generic;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the Heart of the Maze - the goal location where visitors are consumed for essence.
    /// Uses a two-part model system: static heartbase ring and animated heart tongue.
    ///
    /// States:
    /// - Idle: Only heartbase is visible. No visitors in detection range.
    /// - Reaching: Tongue spawns and plays reach animation toward detected visitor.
    /// - Grabbing: Tongue plays grab animation, visitor follows grab collider until consumed.
    /// </summary>
    public class HeartOfTheMaze : MonoBehaviour
    {
        #region Static Events and Properties

        /// <summary>
        /// Static event invoked when a visitor is grabbed by the heart tongue.
        /// Nearby visitors can subscribe to this to become frightened when they witness a grab.
        /// Parameter is the world position where the grab occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorGrabbed;

        /// <summary>
        /// Static flag indicating if a tongue is currently active with blocking colliders.
        /// Visitors check this to avoid expensive Physics.OverlapSphere calls when no tongue exists.
        /// </summary>
        public static bool IsTongueActiveWithColliders { get; private set; } = false;

        #endregion

        #region Enums

        private enum HeartState
        {
            Idle,       // Only heartbase visible, no visitors detected
            Reaching,   // Tongue spawned, reach animation playing toward visitor
            Grabbing    // Grab animation playing, visitor following grab collider
        }

        #endregion

        #region Serialized Fields

        [Header("Position Settings")]
        [SerializeField]
        [Tooltip("Automatically position heart from maze data")]
        private bool autoPosition = true;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Amount of essence gained per visitor consumed")]
        private int essencePerVisitor = 10;

        [Header("Model Settings")]
        [SerializeField]
        [Tooltip("Heartbase prefab (static ring)")]
        private GameObject heartBasePrefab;

        [SerializeField]
        [Tooltip("Heart tongue prefab (animated tentacle)")]
        private GameObject heartTonguePrefab;

        [SerializeField]
        [Tooltip("Size/scale of the heart models")]
        private float modelSize = 0.012f;

        [Header("Detection Settings")]
        [SerializeField]
        [Tooltip("Detection radius for visitors (triggers reaching state)")]
        private float detectionRadius = 2.5f;

        [Header("Material Animation Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing emission on materials")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 2f;

        [SerializeField]
        [Tooltip("Pulse intensity multiplier")]
        private float pulseIntensity = 2f;

        [SerializeField]
        [Tooltip("Base emission color for pulsing")]
        private Color emissionColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("3D Lighting Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing 3D point light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow")]
        private Color glowColor = new Color(1f, 0.7f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 10f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz")]
        private float glowFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 2.0f;

        #endregion

        #region Private Fields

        // State machine
        private HeartState currentState = HeartState.Idle;

        // Model instances
        private GameObject heartBaseInstance;
        private GameObject heartTongueInstance;

        // NOTE: Reach/grab trigger colliders have been removed.
        // Visitor blocking is handled entirely by the solid bone colliders (SolidCollider_N)
        // which physically prevent the visitor from moving through the tongue.

        // Visitor tracking
        private VisitorControllerBase targetVisitor;
        private Queue<VisitorControllerBase> pendingVisitors = new Queue<VisitorControllerBase>();

        // Detection collider (larger trigger for visitor detection)
        private SphereCollider detectionCollider;

        // Materials and lighting
        private Light glowLight;
        private MeshRenderer[] meshRenderers;
        private Material[] materials;

        // Tongue phase tracking
        private enum TonguePhase
        {
            Emerging,       // Translating up from z=1 to tip at z=-0.3
            Reaching,       // Rotating bones to point at visitor, extending over lip
            Touching,       // Tip touched visitor, curling into grab while continuing reach
            Pulling,        // Grab complete, translating back down, visitor follows
            Sinking         // Below ground, about to consume
        }
        private TonguePhase tonguePhase = TonguePhase.Emerging;

        // Tongue position and extension
        private float tongueZPosition = 1f;  // Z position of tongue root (1 = below ground, -0.3 = tip above lip)
        private float tongueExtension = 0f;  // How far bones are rotated to reach (0-1)
        private float grabCurlProgress = 0f; // How much the tip has curled for grab (0-1)

        // Tongue movement speeds (increased 4x for snappier feel)
        private const float TONGUE_EMERGE_SPEED = 18.0f;  // Units per second for vertical movement
        private const float TONGUE_EXTEND_SPEED = 3.0f;   // Rate of bone rotation for reaching
        private const float TONGUE_CURL_SPEED = 6.0f;     // Rate of curl for grabbing
        private const float TONGUE_SINK_SPEED = 6.0f;     // Speed when pulling visitor down

        // Tongue geometry constants
        // Full tongue: 540 bones, prefab scale (1, 0.3, 0.3), ~8.1 units total length
        private const float TONGUE_START_Z = 9.0f;        // Starting Z (below ground, full tongue length)
        private const float TONGUE_LIP_Z = -0.25f;        // Z where tip emerges above heartbase lip
        private const int BEND_BONE_COUNT = 3;            // Number of bones for the sharp 90° lip bend
        private const int RECURVE_BONE_COUNT = 5;         // Number of bones for the slight recurve
        private const float RECURVE_ANGLE = 5f;           // Total angle of recurve
        private const float TONGUE_GROUND_Z = 0.0f;       // Ground level
        private const float LIP_BEND_BONE_INDEX = 3;      // Which bone bends at the lip (approximate)
        private const float TIP_SHAFT_TOUCH_DISTANCE = 0.5f;  // Distance threshold for tip touching shaft (triggers pulling)

        // Tongue armature bone references
        private Transform[] tongueBones;  // Array of bone transforms in order from base to tip
        private Vector3[] boneRestPositions;  // Original local positions of bones
        private Quaternion[] boneRestRotations;  // Original local rotations of bones
        private Transform tongueArmatureRoot;  // Root of the armature hierarchy
        private SkinnedMeshRenderer tongueSkinnedRenderer;  // The skinned mesh for the tongue

        // Calculated tongue properties
        private float tongueLength = 0f;  // Total length of armature (sum of bone lengths)

        // Locked visitor angle - set once when reaching starts, used throughout the phase
        private float lockedVisitorAngle = 0f;

        // Curl direction for touching phase: 1 = curl left (CCW), -1 = curl right (CW)
        private int curlDirection = 1;

        // Track if we've made grab contact during curl
        private bool grabContactMade = false;

        // Frozen lip bone index - set when grabbing starts to prevent curl relaxation during sinking
        private int frozenLipBoneIndex = -1;

        // Locked curl bone rotations - frozen when grab bone's parent becomes the lip bone during sinking
        // This allows the curl to hinge up naturally as the lip bone rotates back to vertical
        private Quaternion[] lockedCurlRotations = null;
        private bool curlRotationsLocked = false;

        // Sinking rotation progress - tracks the 90° rotation of the curl section during sinking
        // Goes from 0 (horizontal, toward visitor) to 1 (vertical, pointing down into heart)
        private float sinkingRotationProgress = 0f;

        // Dynamic grab bone index - set when curl completes (bone at lip when curl finishes)
        // This is NOT a fixed offset - it's determined by which bone is at the lip level
        // when the spiral closes and we transition to Pulling phase
        private int dynamicGrabBoneIndex = -1;

        // POOLING: Z position to hide the tongue when not in use
        // This prevents physics broadphase rebuilds when spawning/despawning
        private const float TONGUE_HIDDEN_Z = 1000f;

        // Flag to track if tongue has been pre-created
        private bool tonguePoolInitialized = false;

        // BONE COLLIDERS: Solid colliders on every Nth bone for physics-based blocking
        // Colliders are parented to bones in the prefab - they inherit bone transforms automatically
        // Collider sizing: tapered from base (scale 0.015) to tip (scale 0.003), radius 0.15
        private const int BONE_COLLIDER_SPACING = 10;        // Add collider every 10th bone (54 colliders for 540 bones)
        private const float BONE_COLLIDER_RADIUS = 0.00225f; // Average world radius (0.015 * 0.15 at base)
        private GameObject[] boneColliderObjects = null;  // Collider GameObjects (parented to bones)

        // Curl diameter for wrapping around visitor (horizontal curl in XY plane)
        private const float CURL_DIAMETER = 0.5f;

        // Radial shift during reverse curl to center visitor in the grab curl
        // This shifts the grab point 0.25 units toward the visitor (half the curl diameter)
        private const float GRAB_RADIAL_SHIFT = 0.25f;


        #endregion

        #region Properties

        /// <summary>Gets the essence value per visitor</summary>
        public int EssencePerVisitor => essencePerVisitor;

        /// <summary>Gets the current heart state for debugging</summary>
        public string CurrentStateName => currentState.ToString();

        #endregion

        #region Public Methods

        /// <summary>
        /// Positions the heart using the maze's world-space heart position.
        /// </summary>
        public void PositionFromMazeGrid()
        {
            var mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGridBehaviour == null) return;

            Vector3 worldPos = mazeGridBehaviour.HeartWorldPosition;
            transform.position = worldPos;
        }

        /// <summary>
        /// Called when a visitor is consumed by the heart.
        /// </summary>
        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (visitor == null) return;

            int essence = visitor.GetEssenceReward();

            // Track stats - record visitor fate with essence value
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, VisitorFate.Consumed, essence);
            }

            // Add essence
            if (GameController.Instance != null)
            {
                GameController.Instance.AddEssence(essence, EssenceSource.VisitorConsumedByHeart, $"Reward: {essence}");
            }

            // Notify HeartPowerManager
            if (HeartPowers.HeartPowerManager.Instance != null)
            {
                HeartPowers.HeartPowerManager.Instance.NotifyVisitorConsumed();
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Destroy(visitor.gameObject);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Setup physics components early
            SetupDetectionCollider();
            SetupRigidbody();
        }

        private void Start()
        {
            // FIRST: Position the HeartOfTheMaze at the correct world position
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }

            // THEN: Load prefabs and setup visuals (they will be children at the correct position)
            LoadPrefabs();
            SetupHeartBase();
            SetupGlowLight();

            // PRE-CREATE the tongue instance to avoid physics freezes during gameplay
            // This creates all colliders/rigidbodies BEFORE gameplay starts
            PreCreateTongueInstance();
        }

        private void Update()
        {
            UpdateStateMachine();
            UpdateMaterialPulse();
            UpdateGlowPulse();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check for visitor entering detection zone
            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;

            // Ignore if visitor is already being processed or consumed
            if (visitor.State == VisitorControllerBase.VisitorState.Consumed) return;

            // Add to pending queue if not already tracked
            if (visitor != targetVisitor && !pendingVisitors.Contains(visitor))
            {
                pendingVisitors.Enqueue(visitor);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Remove visitor from pending queue if they leave
            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;

            // Can't easily remove from Queue, but we'll check validity when dequeuing
        }

        #endregion

        #region State Machine

        private void UpdateStateMachine()
        {
            switch (currentState)
            {
                case HeartState.Idle:
                    UpdateIdleState();
                    break;
                case HeartState.Reaching:
                    UpdateReachingState();
                    break;
                case HeartState.Grabbing:
                    UpdateGrabbingState();
                    break;
            }
        }

        private void UpdateIdleState()
        {
            // Check for visitors in detection range
            if (TryGetNextValidVisitor(out VisitorControllerBase visitor))
            {
                targetVisitor = visitor;

                // Detailed logging for tongue trigger
                Vector2 heartPos = new Vector2(transform.position.x, transform.position.y);
                Vector2 visitorPos = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                float distToVisitor = Vector2.Distance(heartPos, visitorPos);

                var path = visitor.GetCurrentPath(out int pathIndex);
                Debug.Log($"[HeartTongue] TRIGGERED by {visitor.name}\n" +
                    $"  Visitor pos: {visitorPos}, dist from heart: {distToVisitor:F2}\n" +
                    $"  Visitor state: {visitor.State}, path length: {path?.Count ?? 0}, path index: {pathIndex}\n" +
                    $"  Detection radius: {detectionRadius}");

                TransitionToReaching();
            }
        }

        private void UpdateReachingState()
        {
            // Check if target visitor is still valid
            // Grabbed state is fine - that means we're holding them
            // Consumed state means they were already processed elsewhere
            if (targetVisitor == null || targetVisitor.State == VisitorControllerBase.VisitorState.Consumed)
            {
                Debug.Log($"[UpdateReachingState] Transitioning to Idle: visitor={targetVisitor != null}, state={targetVisitor?.State}");
                TransitionToIdle();
                return;
            }

            // Log every frame during Emerging/Reaching to catch issues
            if (tonguePhase == TonguePhase.Emerging || tonguePhase == TonguePhase.Reaching)
            {
                Debug.Log($"[UpdateReachingState] Frame {Time.frameCount}: phase={tonguePhase}, tongueZ={tongueZPosition:F2}, heartTongue={heartTongueInstance != null}");
            }

            // Update tongue based on current phase
            UpdateTonguePhase();

            // Apply bone transformations
            ApplyTongueBoneState();

            // Enable bone colliders for physics blocking during Reaching/Touching/Pulling/Sinking
            // Colliders must be active during Reaching so the extending tongue blocks the visitor's path
            // The visitor can't walk through the tongue - they must wait for it to curl around them
            // Only disable colliders during Emerging (tongue is still below ground)
            // NOTE: Colliders are parented to bones and inherit transforms automatically - no position updates needed
            if (tonguePhase == TonguePhase.Reaching || tonguePhase == TonguePhase.Touching || tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
            {
                EnableBoneColliders();
            }
            else
            {
                DisableBoneColliders();
            }

            // Check for phase transitions during Touching phase (curl/spiral)
            // NOTE: Reaching -> Touching transition is handled in UpdateTonguePhase based on
            // full extent (NOT based on collider touching visitor!)
            if (tonguePhase == TonguePhase.Touching)
            {
                // Complete grab when tip touches the shaft (spiral has closed)
                // The tongue keeps extending while curling, creating a growing spiral
                // When the tip curls back and touches the shaft, the loop is complete
                // NOTE: Visitor is constrained by solid bone colliders naturally via physics
                if (IsTipTouchingShaft())
                {
                    Debug.Log($"[TongueTouching] >>> TIP TOUCHED SHAFT - TRANSITION TO GRABBING");

                    // Notify nearby visitors that a grab occurred - they become frightened
                    if (targetVisitor != null)
                    {
                        OnVisitorGrabbed?.Invoke(targetVisitor.transform.position);
                    }

                    // TransitionToGrabbing() will set grabContactMade and call SetGrabbedByHeart()
                    TransitionToGrabbing();
                }
            }
        }

        private void UpdateGrabbingState()
        {
            // Check if target visitor is still valid
            if (targetVisitor == null)
            {
                TransitionToIdle();
                return;
            }

            // Update tongue phase (pulling/sinking)
            UpdateTonguePhase();

            // Apply bone transformations
            ApplyTongueBoneState();

            // Ensure bone colliders are enabled for physics-based visitor blocking
            // NOTE: Colliders are parented to bones and inherit transforms automatically
            EnableBoneColliders();

            // NOTE: Visitor is constrained by colliders naturally, not by code forcing position

            // Check if tongue is fully below ground (sinking complete)
            if (tonguePhase == TonguePhase.Sinking && tongueZPosition >= TONGUE_START_Z)
            {
                OnVisitorConsumed(targetVisitor);
                targetVisitor = null;
                TransitionToIdle();
            }
        }

        private void UpdateTonguePhase()
        {
            if (heartTongueInstance == null)
            {
                Debug.LogWarning("[TonguePhase] heartTongueInstance is NULL!");
                return;
            }

            float dt = Time.deltaTime;
            float zBefore = tongueZPosition;

            switch (tonguePhase)
            {
                case TonguePhase.Emerging:
                    // Move tongue up (-Z) until tip is above lip
                    tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

                    // Calculate where tip would be (tongue extends in local +Y, which after rotation points somewhere)
                    // For now, simple check: when root Z reaches a certain point based on tongue length
                    float tipZ = tongueZPosition - tongueLength;

                    Debug.Log($"[TongueEmerging] Frame {Time.frameCount}: Z={tongueZPosition:F3} (was {zBefore:F3}, delta={tongueZPosition-zBefore:F3}), tipZ={tipZ:F3}, tongueLen={tongueLength:F2}, dt={dt:F4}, speed={TONGUE_EMERGE_SPEED}");

                    if (tipZ <= TONGUE_LIP_Z)
                    {
                        Debug.Log($"[TongueEmerging] >>> TRANSITION TO REACHING: tipZ={tipZ:F2} <= TONGUE_LIP_Z={TONGUE_LIP_Z}");
                        tonguePhase = TonguePhase.Reaching;
                        // Visitor keeps moving until the reach collider actually touches them
                    }
                    break;

                case TonguePhase.Reaching:
                    // Calculate how much horizontal length we have past the lip
                    // The tongue bends 90° at the lip bone. After the bend, the tip extends horizontally.
                    // The "virtual tip Z" if the tongue were straight = tongueZPosition - tongueLength
                    // But once the lip bone bends 90°, that extra length goes horizontal, not vertical.
                    //
                    // Horizontal length = how much tongue length has passed the lip level
                    // = tongueLength - (tongueZPosition - TONGUE_LIP_Z)
                    // = tongueLength - tongueZPosition + TONGUE_LIP_Z
                    //
                    // When tongueZPosition = TONGUE_LIP_Z + tongueLength, horizontal = 0 (tip just at lip)
                    // As tongueZPosition decreases (tongue rises), horizontal increases
                    float horizontalLength = tongueLength - tongueZPosition + TONGUE_LIP_Z;
                    if (horizontalLength < 0) horizontalLength = 0;  // Tip hasn't reached lip yet

                    float zBeforeReaching = tongueZPosition;
                    bool atDetectionRadius = horizontalLength >= detectionRadius;

                    // Keep extending - this continues even after curl starts (in Touching phase)
                    tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

                    // Also ramp up bend progress (controls how bent the lip bone is)
                    tongueExtension += TONGUE_EXTEND_SPEED * dt;
                    tongueExtension = Mathf.Clamp01(tongueExtension);

                    // TRANSITION: Start curling when reaching detectionRadius
                    // The tongue will keep extending during the Touching phase to make a bigger spiral
                    if (atDetectionRadius)
                    {
                        Debug.Log($"[TongueReaching] >>> TRANSITION TO CURLING: horizLen={horizontalLength:F2} >= detectionRadius={detectionRadius}");

                        // Decide curl direction based on visitor angle
                        curlDirection = (lockedVisitorAngle >= 0 && lockedVisitorAngle < 180) ? 1 : -1;
                        tonguePhase = TonguePhase.Touching;  // Touching phase handles the curl/spiral
                    }

                    Debug.Log($"[TongueReaching] Frame {Time.frameCount}: Z={tongueZPosition:F3} (was {zBeforeReaching:F3}, delta={tongueZPosition-zBeforeReaching:F3})\n" +
                        $"  horizLen={horizontalLength:F2} vs detectionRadius={detectionRadius}, atDetectionRadius={atDetectionRadius}\n" +
                        $"  ext={tongueExtension:F2}, dt={dt:F4}, speed={TONGUE_EMERGE_SPEED}");

                    break;

                case TonguePhase.Touching:
                    float zBeforeTouch = tongueZPosition;

                    // KEEP EXTENDING while curling to make a bigger spiral
                    // The tongue extends until the tip touches the shaft
                    tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

                    // Ramp up the spiral curl progress - entire horizontal section spirals
                    // The spiral wraps 400°+ to ensure tip meets shaft, encircling the visitor
                    grabCurlProgress += TONGUE_CURL_SPEED * dt;
                    grabCurlProgress = Mathf.Clamp01(grabCurlProgress);

                    // Check if tip has touched the shaft (triggers transition to Pulling)
                    // We detect this by measuring distance from tip bone to the first horizontal bone
                    bool tipTouchedShaft = IsTipTouchingShaft();

                    Debug.Log($"[TongueTouching] Frame {Time.frameCount}: Z={tongueZPosition:F3} (was {zBeforeTouch:F3}), curlProg={grabCurlProgress:F2}, tipTouchedShaft={tipTouchedShaft}");

                    break;

                case TonguePhase.Pulling:
                    float zBeforePull = tongueZPosition;
                    // HORIZONTAL RETRACTION: Move the tongue down (+Z), but recalculate which bone
                    // is at the lip so the curl stays at lip level. The effect is that the curl
                    // slides horizontally back toward the heart while staying at lip height.
                    //
                    // As tongueZPosition increases, higher-indexed bones reach the lip level.
                    // The bone at lip level becomes the new "lip bone" that bends 90° toward visitor.
                    // This creates the visual effect of the horizontal portion shortening.
                    tongueZPosition += TONGUE_EMERGE_SPEED * dt;

                    // Use the dynamically captured grab bone index (lip bone at curl completion)
                    // NOT a fixed offset from tip
                    float boneSpacingPull = tongueLength / Mathf.Max(1, tongueBones.Length);
                    int grabBoneIndexPull = dynamicGrabBoneIndex >= 0 ? dynamicGrabBoneIndex : tongueBones.Length / 2;

                    // The grab bone's unrotated Z position = tongueZPosition - (grabBoneIndex * boneSpacing)
                    // When this reaches TONGUE_LIP_Z, the grab bone is at the lip level (curl is at heart)
                    float grabBoneZPull = tongueZPosition - (grabBoneIndexPull * boneSpacingPull);

                    Debug.Log($"[TonguePulling] Frame {Time.frameCount}: Z={tongueZPosition:F3} (was {zBeforePull:F3}), grabBoneIdx={grabBoneIndexPull}, grabBoneZ={grabBoneZPull:F2} vs lipZ={TONGUE_LIP_Z}");

                    // When the grab bone reaches the lip level, start sinking with curl rotation
                    if (grabBoneZPull >= TONGUE_LIP_Z)
                    {
                        Debug.Log($"[TonguePulling] >>> TRANSITION TO SINKING");
                        // Freeze the lip bone index now so the curl shape stays stable during sinking
                        FreezeCurrentLipBoneIndex();
                        tonguePhase = TonguePhase.Sinking;
                        sinkingRotationProgress = 0f;
                    }
                    break;

                case TonguePhase.Sinking:
                    float zBeforeSink = tongueZPosition;
                    // Continue moving tongue down (+Z) into the ground
                    tongueZPosition += TONGUE_SINK_SPEED * dt;

                    // Rotate the curl section from horizontal (pointing toward visitor) to vertical (pointing down)
                    // This happens over a short duration as the tongue sinks
                    sinkingRotationProgress += TONGUE_CURL_SPEED * dt;
                    sinkingRotationProgress = Mathf.Clamp01(sinkingRotationProgress);

                    Debug.Log($"[TongueSinking] Frame {Time.frameCount}: Z={tongueZPosition:F3} (was {zBeforeSink:F3}), sinkRotProg={sinkingRotationProgress:F2}, target={TONGUE_START_Z}");
                    break;
            }

            // Update tongue instance position
            Vector3 localPosBefore = heartTongueInstance.transform.localPosition;
            Vector3 localPos = localPosBefore;
            localPos.z = tongueZPosition;
            heartTongueInstance.transform.localPosition = localPos;

            // Log the actual transform update
            if (Time.frameCount % 10 == 0 || tonguePhase == TonguePhase.Emerging)
            {
                Debug.Log($"[TongueTransform] Frame {Time.frameCount}: localPosition changed from {localPosBefore} to {heartTongueInstance.transform.localPosition}, phase={tonguePhase}");
            }

            // NOTE: Do NOT modify heartTongueInstance.transform.localRotation here!
            // The prefab has a rotation baked in that's required for the coordinate system.
        }

        private void TransitionToIdle()
        {
            currentState = HeartState.Idle;
            targetVisitor = null;

            // Reset tongue state
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TONGUE_START_Z;
            tongueExtension = 0f;
            grabCurlProgress = 0f;
            curlDirection = 1;
            grabContactMade = false;
            frozenLipBoneIndex = -1;
            dynamicGrabBoneIndex = -1;  // Reset dynamic grab bone
            lockedCurlRotations = null;
            curlRotationsLocked = false;
            sinkingRotationProgress = 0f;

            // POOLING: Hide tongue at TONGUE_HIDDEN_Z instead of destroying
            // This avoids physics broadphase rebuilds that cause massive freezes
            if (heartTongueInstance != null)
            {
                heartTongueInstance.name = "HeartTongue_Pooled";

                // Disable bone colliders
                DisableBoneColliders();

                // Reset bones to rest pose
                ResetTongueBonesToRest();

                // Move to hidden position
                Vector3 localPos = heartTongueInstance.transform.localPosition;
                localPos.z = TONGUE_HIDDEN_Z;
                heartTongueInstance.transform.localPosition = localPos;

                // NOTE: Keep all references (tongueBones, etc.) - they're reused
            }
        }

        private void TransitionToReaching()
        {
            currentState = HeartState.Reaching;

            // Initialize tongue state
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TONGUE_START_Z;
            tongueExtension = 0f;
            grabCurlProgress = 0f;

            // Calculate the visitor's EXIT POINT from the heart node, not their current position
            // This is where the tongue should aim - visitors walk through the heart, they don't stop
            Vector2 heartPos2D = new Vector2(transform.position.x, transform.position.y);
            Vector2 exitPoint = CalculateVisitorExitPoint(targetVisitor, heartPos2D, detectionRadius);

            // Lock in the direction to the exit point
            Vector2 dirToExit = (exitPoint - heartPos2D).normalized;
            lockedVisitorAngle = Mathf.Atan2(dirToExit.y, dirToExit.x) * Mathf.Rad2Deg;

            Debug.Log($"[HeartTongue] TransitionToReaching for {targetVisitor?.name}\n" +
                $"  Heart pos: {heartPos2D}\n" +
                $"  Exit point: {exitPoint}\n" +
                $"  Locked angle: {lockedVisitorAngle:F1}°\n" +
                $"  Tongue start Z: {TONGUE_START_Z}, lip Z: {TONGUE_LIP_Z}");

            // Spawn tongue
            SpawnTongue();
        }

        /// <summary>
        /// Calculates where the visitor will EXIT the heart node's detection zone.
        /// Uses the visitor's pre-calculated path to find the exact exit point.
        /// </summary>
        /// <param name="visitor">The target visitor</param>
        /// <param name="heartPos">Heart position in 2D</param>
        /// <param name="nodeRadius">Radius of the detection zone</param>
        /// <returns>The 2D point where visitor's path exits the zone</returns>
        private Vector2 CalculateVisitorExitPoint(VisitorControllerBase visitor, Vector2 heartPos, float nodeRadius)
        {
            if (visitor == null)
            {
                Debug.Log($"[ExitPoint] Visitor is null, using right direction");
                return heartPos + Vector2.right * nodeRadius; // Fallback
            }

            // Get the visitor's actual path
            List<Vector3> visitorPath = visitor.GetCurrentPath(out int currentPathIndex);

            if (visitorPath == null || visitorPath.Count == 0 || currentPathIndex >= visitorPath.Count)
            {
                // Fallback: use visitor's current movement direction
                Vector2 visitorPos = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                Vector2 dirFromHeart = (visitorPos - heartPos).normalized;
                Debug.Log($"[ExitPoint] No valid path (path={visitorPath?.Count ?? 0}, index={currentPathIndex}), using direction from heart: {dirFromHeart}");
                return heartPos + dirFromHeart * nodeRadius;
            }

            // DEBUG: Log path points around current index
            Debug.Log($"[ExitPoint] Searching path from index {currentPathIndex} to {visitorPath.Count - 1}");
            for (int dbgI = currentPathIndex; dbgI < Mathf.Min(currentPathIndex + 5, visitorPath.Count); dbgI++)
            {
                Vector2 dbgPt = new Vector2(visitorPath[dbgI].x, visitorPath[dbgI].y);
                float dbgDist = Vector2.Distance(dbgPt, heartPos);
                Debug.Log($"[ExitPoint]   Path[{dbgI}] = {dbgPt}, dist from heart = {dbgDist:F2} (radius={nodeRadius})");
            }

            // Search forward from current path index to find where path exits the detection zone
            Vector2 prevPoint = new Vector2(visitor.transform.position.x, visitor.transform.position.y);

            for (int i = currentPathIndex; i < visitorPath.Count; i++)
            {
                Vector2 pathPoint = new Vector2(visitorPath[i].x, visitorPath[i].y);
                float distFromHeart = Vector2.Distance(pathPoint, heartPos);

                // Found a point outside the zone - calculate exact intersection
                if (distFromHeart > nodeRadius)
                {
                    Debug.Log($"[ExitPoint] Found exit at path[{i}] = {pathPoint}, dist={distFromHeart:F2} > radius={nodeRadius}");
                    Debug.Log($"[ExitPoint]   prevPoint={prevPoint}, segment to pathPoint");

                    // Ray-circle intersection to find exact exit point
                    Vector2 segmentDir = (pathPoint - prevPoint).normalized;
                    Vector2 toCenter = heartPos - prevPoint;

                    // Quadratic formula for ray-circle intersection
                    float a = Vector2.Dot(segmentDir, segmentDir);  // Always 1 for normalized
                    float b = 2f * Vector2.Dot(segmentDir, prevPoint - heartPos);
                    float c = Vector2.Dot(prevPoint - heartPos, prevPoint - heartPos) - nodeRadius * nodeRadius;

                    float discriminant = b * b - 4f * a * c;
                    if (discriminant >= 0)
                    {
                        float sqrtDisc = Mathf.Sqrt(discriminant);
                        // We want t2 (the exit point, farther intersection)
                        float t2 = (-b + sqrtDisc) / (2f * a);
                        if (t2 >= 0)
                        {
                            Vector2 exitPoint = prevPoint + t2 * segmentDir;
                            Debug.Log($"[ExitPoint]   Calculated intersection: t2={t2:F2}, exitPoint={exitPoint}");
                            return exitPoint;
                        }
                    }

                    // Fallback if math fails
                    Debug.Log($"[ExitPoint]   Math failed, using pathPoint directly");
                    return pathPoint;
                }

                prevPoint = pathPoint;
            }

            // Path doesn't exit the zone? Use the last path point direction
            if (visitorPath.Count > 0)
            {
                Vector2 lastPoint = new Vector2(visitorPath[visitorPath.Count - 1].x, visitorPath[visitorPath.Count - 1].y);
                Vector2 dirFromHeart = (lastPoint - heartPos).normalized;
                Debug.Log($"[ExitPoint] Path never exits zone, using last point direction: {dirFromHeart}");
                return heartPos + dirFromHeart * nodeRadius;
            }

            // Final fallback
            Vector2 fallbackDir = (new Vector2(visitor.transform.position.x, visitor.transform.position.y) - heartPos).normalized;
            Debug.Log($"[ExitPoint] Final fallback, using visitor direction from heart: {fallbackDir}");
            return heartPos + fallbackDir * nodeRadius;
        }

        private void TransitionToGrabbing()
        {
            currentState = HeartState.Grabbing;

            // Tongue phase is now Pulling - starts immediately after reverse curl completes
            tonguePhase = TonguePhase.Pulling;

            // The "grab bone" is the bone in the CENTER of the curled section - where the visitor is held.
            // We track when this center point reaches the lip level (curl has been pulled to heart).
            // The curl uses the outer portion of the horizontal tongue section.
            // GRAB_BONE_PERCENTAGE defines how far from the tip the grab bone is (10% = bone 490 for 540 bones)
            int boneCount = tongueBones != null ? tongueBones.Length : 0;

            // Grab bone is ~10% from the tip - the center of the curl where visitor is trapped
            const float GRAB_BONE_PERCENTAGE = 0.10f;  // 10% from tip
            int boneOffset = Mathf.RoundToInt(boneCount * GRAB_BONE_PERCENTAGE);
            dynamicGrabBoneIndex = boneCount - 1 - boneOffset;  // e.g., 540 - 1 - 54 = 485

            Debug.Log($"[TransitionToGrabbing] Dynamic grab bone index = {dynamicGrabBoneIndex} ({GRAB_BONE_PERCENTAGE*100}% from tip, offset={boneOffset})");

            // CRITICAL: Always grab the visitor when transitioning to Grabbing state
            // The spiral has closed around them - they MUST be immobilized
            if (targetVisitor != null)
            {
                grabContactMade = true;
                targetVisitor.SetGrabbedByHeart();
                Debug.Log($"[TransitionToGrabbing] Spiral closed - grabbed {targetVisitor.name}, state should now be Grabbed");

                // Disable all lights on the visitor - they're being consumed
                DisableVisitorLights(targetVisitor);
            }
        }

        private void DisableVisitorLights(VisitorControllerBase visitor)
        {
            if (visitor == null) return;

            Light[] allLights = visitor.GetComponentsInChildren<Light>();
            foreach (Light light in allLights)
            {
                light.enabled = false;
            }
        }

        private void FreezeCurrentLipBoneIndex()
        {
            if (tongueBones == null || tongueBones.Length == 0) return;

            int boneCount = tongueBones.Length;
            float lipWorldZ = transform.position.z + TONGUE_LIP_Z;
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);

            for (int i = boneCount - 1; i >= 0; i--)
            {
                float unrotatedBoneZ = tongueZPosition - (i * boneSpacing);
                if (unrotatedBoneZ > lipWorldZ)
                {
                    frozenLipBoneIndex = i;
                    return;
                }
            }
            frozenLipBoneIndex = 0;
        }

        #endregion

        #region Tongue Management

        private void SpawnTongue()
        {
            Debug.Log($"[SpawnTongue] BEGIN - heartTongueInstance={heartTongueInstance != null}");

            // POOLING: Don't instantiate - just move the pre-created instance into position
            // This avoids physics broadphase rebuilds that cause 10-20+ second freezes
            if (heartTongueInstance == null)
            {
                // Fallback: try to pre-create if it wasn't done in Start
                Debug.Log($"[SpawnTongue] Instance is null, calling PreCreateTongueInstance");
                PreCreateTongueInstance();
                if (heartTongueInstance == null)
                {
                    Debug.LogWarning("[HeartOfTheMaze] Cannot spawn tongue - no instance available!");
                    return;
                }
            }

            heartTongueInstance.name = "HeartTongue_Active";

            // Reset bone poses to rest state before positioning
            ResetTongueBonesToRest();

            // Position tongue at starting Z (below ground, but in play area)
            tongueZPosition = TONGUE_START_Z;
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            Debug.Log($"[SpawnTongue] Before setting Z: localPosition={localPos}, tongueZPosition={tongueZPosition}");
            localPos.z = tongueZPosition;
            heartTongueInstance.transform.localPosition = localPos;
            Debug.Log($"[SpawnTongue] After setting Z: localPosition={heartTongueInstance.transform.localPosition}");

            // Apply initial bone state (all bones at rest, tongue below ground)
            ApplyTongueBoneState();

            Debug.Log($"[SpawnTongue] END - tongueLength={tongueLength:F2}, tongueZPosition={tongueZPosition:F2}, TONGUE_START_Z={TONGUE_START_Z}, TONGUE_LIP_Z={TONGUE_LIP_Z}");
        }

        /// <summary>
        /// Resets all tongue bones to their rest pose.
        /// Called when activating the pooled tongue to ensure clean state.
        /// </summary>
        private void ResetTongueBonesToRest()
        {
            if (tongueBones == null || boneRestPositions == null || boneRestRotations == null) return;

            for (int i = 0; i < tongueBones.Length; i++)
            {
                if (tongueBones[i] != null)
                {
                    tongueBones[i].localPosition = boneRestPositions[i];
                    tongueBones[i].localRotation = boneRestRotations[i];
                }
            }
        }

        private void CalculateTongueLength()
        {
            tongueLength = 0f;
            if (tongueBones == null || tongueBones.Length < 2) return;

            // Calculate actual world-space length by measuring distance from first to last bone
            // This accounts for the model's scale properly
            Vector3 firstBoneWorld = tongueBones[0].position;
            Vector3 lastBoneWorld = tongueBones[tongueBones.Length - 1].position;

            // The tongue extends primarily in one direction - measure the total span
            tongueLength = Vector3.Distance(firstBoneWorld, lastBoneWorld);

            // Add approximate length of the last bone segment (tip extends beyond last bone)
            // Estimate from the average bone spacing
            if (tongueBones.Length > 1)
            {
                float avgBoneSpacing = tongueLength / (tongueBones.Length - 1);
                tongueLength += avgBoneSpacing;
            }
        }

        private void FindTongueBones()
        {
            if (heartTongueInstance == null) return;

            // First, find the SkinnedMeshRenderer - this is the definitive source of bone info
            tongueSkinnedRenderer = heartTongueInstance.GetComponentInChildren<SkinnedMeshRenderer>();

            if (tongueSkinnedRenderer != null && tongueSkinnedRenderer.bones != null && tongueSkinnedRenderer.bones.Length > 0)
            {
                // Use ALL bones from SkinnedMeshRenderer - these are the actual bones that deform the mesh
                // Performance note: bone TRANSFORMS don't cause physics lag, only bone COLLIDERS do (which are disabled)
                tongueBones = tongueSkinnedRenderer.bones;
                tongueArmatureRoot = tongueSkinnedRenderer.rootBone;
            }
            else
            {
                // Fallback: Find all transforms and look for bone naming patterns
                var allTransforms = heartTongueInstance.GetComponentsInChildren<Transform>();
                var boneList = new List<Transform>();

                foreach (var t in allTransforms)
                {
                    // Common bone naming patterns
                    string nameLower = t.name.ToLower();
                    if (nameLower.Contains("bone") || nameLower.Contains("joint") ||
                        nameLower.Contains("armature") || nameLower.Contains("segment") ||
                        nameLower.Contains("ctrl") || nameLower.Contains("rig"))
                    {
                        boneList.Add(t);
                    }
                }

                tongueBones = boneList.ToArray();
            }

            // Store rest poses for all bones we're using
            if (tongueBones != null && tongueBones.Length > 0)
            {
                boneRestPositions = new Vector3[tongueBones.Length];
                boneRestRotations = new Quaternion[tongueBones.Length];

                for (int i = 0; i < tongueBones.Length; i++)
                {
                    if (tongueBones[i] != null)
                    {
                        boneRestPositions[i] = tongueBones[i].localPosition;
                        boneRestRotations[i] = tongueBones[i].localRotation;
                    }
                }
            }
        }

        /// <summary>
        /// Removes any Light components from the tongue model (spotlight, point light, etc).
        /// </summary>
        private void RemoveTongueLights()
        {
            if (heartTongueInstance == null) return;

            Light[] lights = heartTongueInstance.GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                Destroy(light);
            }
        }

        /// <summary>
        /// Applies bone transformations based on current tongue phase and extension.
        ///
        /// The tongue extends from below ground, bends over the heartbase lip, and reaches toward the visitor.
        ///
        /// Strategy:
        /// 1. Bones below the lip stay at rest pose (straight, pointing -Z/up after instance rotation)
        /// 2. Starting at the lip bone, compute the rotation needed to point toward the visitor
        /// 3. For each subsequent bone (toward tip), compute its rotation to maintain alignment toward visitor
        ///
        /// Coordinate system reminder: -Z is UP, XY is the ground plane.
        /// The tongue model extends in local +X (along the bone chain) with base at origin and tip toward +X.
        /// The tongue instance is rotated -90° around Y at spawn, transforming +X to -Z.
        /// So at rest, the tongue points in -Z (up). The lip bone bends it toward the visitor in XY.
        /// </summary>
        private void ApplyTongueBoneState()
        {
            if (tongueBones == null || tongueBones.Length == 0 || heartTongueInstance == null) return;

            int boneCount = tongueBones.Length;

            // During Emerging phase, keep all bones at rest pose (straight tongue)
            if (tonguePhase == TonguePhase.Emerging)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (tongueBones[i] == null) continue;
                    tongueBones[i].localPosition = boneRestPositions[i];
                    tongueBones[i].localRotation = boneRestRotations[i];
                }
                return;
            }

            // For other phases, we need a target visitor
            if (targetVisitor == null) return;

            // NOTE: Do NOT modify heartTongueInstance.transform.localRotation here
            // The prefab has a -90° Y rotation baked in that transforms model +X to world -Z

            // World Z of the lip (in heart's coordinate space)
            float lipWorldZ = transform.position.z + TONGUE_LIP_Z;

            // Calculate lip bone index based on geometry
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);
            int lipBoneIndex;
            if (tonguePhase == TonguePhase.Sinking && frozenLipBoneIndex >= 0)
            {
                lipBoneIndex = frozenLipBoneIndex;
            }
            else
            {
                lipBoneIndex = -1;
                for (int i = boneCount - 1; i >= 0; i--)
                {
                    float unrotatedBoneZ = tongueZPosition - (i * boneSpacing);
                    if (unrotatedBoneZ > lipWorldZ)
                    {
                        lipBoneIndex = i;
                        break;
                    }
                }
            }

            // Count how many bones are above the lip (horizontal section)
            int horizontalBoneCount = (lipBoneIndex >= 0) ? (boneCount - lipBoneIndex) : 0;

            // Debug logging for bone state
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"[ApplyBoneState] Frame {Time.frameCount}: phase={tonguePhase}, lipBoneIndex={lipBoneIndex}, horizontalBones={horizontalBoneCount}\n" +
                    $"  tongueZ={tongueZPosition:F2}, lipWorldZ={lipWorldZ:F2}, boneSpacing={boneSpacing:F4}\n" +
                    $"  grabCurlProgress={grabCurlProgress:F2}, curlDirection={curlDirection}\n" +
                    $"  heartPos={transform.position}, visitorPos={targetVisitor?.transform.position}");
            }

            // bendProgress controls how much the lip bone has bent (0 = vertical, 1 = horizontal)
            float bendProgress;
            if (tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
            {
                bendProgress = 1f;
            }
            else
            {
                bendProgress = Mathf.Clamp01(tongueExtension * 2f);
            }

            // Target direction: toward exit point (for initial reach) or visitor (for curl)
            Vector3 targetDirWorld = new Vector3(
                Mathf.Cos(lockedVisitorAngle * Mathf.Deg2Rad),
                Mathf.Sin(lockedVisitorAngle * Mathf.Deg2Rad),
                0f
            );

            // First pass: set all bones to rest pose
            for (int i = 0; i < boneCount; i++)
            {
                if (tongueBones[i] == null) continue;
                tongueBones[i].localPosition = boneRestPositions[i];
                tongueBones[i].localRotation = boneRestRotations[i];
            }

            Vector3 downDir = Vector3.forward;  // +Z is down
            Vector3 upDir = Vector3.back;  // -Z is up

            // Grab bone index - use dynamic if set, otherwise estimate from lip bone
            int grabBoneIndex = dynamicGrabBoneIndex >= 0 ? dynamicGrabBoneIndex : lipBoneIndex;

            // Calculate the bend zone (lip to lip+BEND_BONE_COUNT)
            int bendEndIndex = Mathf.Min(lipBoneIndex + BEND_BONE_COUNT, boneCount - 1);

            // Second pass: apply rotations from lip to tip
            for (int i = lipBoneIndex; i < boneCount && lipBoneIndex >= 0; i++)
            {
                if (tongueBones[i] == null) continue;

                Quaternion parentWorldRot = tongueBones[i].parent != null ? tongueBones[i].parent.rotation : Quaternion.identity;
                Vector3 boneLocalDir = Vector3.up;  // local +Y points toward next bone
                Vector3 boneWorldDir = parentWorldRot * boneRestRotations[i] * boneLocalDir;

                Vector3 desiredDir;

                // SINKING PHASE: special handling
                int pivotBoneIndex = grabBoneIndex - 1;
                if (tonguePhase == TonguePhase.Sinking && i >= frozenLipBoneIndex && i < pivotBoneIndex)
                {
                    if (i == frozenLipBoneIndex)
                    {
                        desiredDir = Vector3.Slerp(upDir, targetDirWorld, 1f);
                    }
                    else
                    {
                        desiredDir = targetDirWorld;
                    }
                }
                else if (tonguePhase == TonguePhase.Sinking && i == pivotBoneIndex)
                {
                    float t = sinkingRotationProgress;
                    desiredDir = Vector3.Slerp(targetDirWorld, downDir, t);
                }
                else if (tonguePhase == TonguePhase.Sinking && i >= grabBoneIndex && curlRotationsLocked && lockedCurlRotations != null)
                {
                    // Use locked rotations during sinking
                    int pivotIdx = grabBoneIndex - 1;
                    int curlIndex = i - pivotIdx;
                    if (curlIndex >= 0 && curlIndex < lockedCurlRotations.Length)
                    {
                        tongueBones[i].localRotation = lockedCurlRotations[curlIndex];
                        continue;
                    }
                    desiredDir = targetDirWorld;
                }
                else if (i >= lipBoneIndex && i <= bendEndIndex)
                {
                    // Bend zone: 90° from vertical to horizontal
                    float t = bendProgress;
                    desiredDir = Vector3.Slerp(upDir, targetDirWorld, t);
                }
                else if (tonguePhase == TonguePhase.Touching || tonguePhase == TonguePhase.Pulling)
                {
                    // SPIRAL CURL: All horizontal bones participate in the spiral
                    // The spiral wraps around the visitor's position

                    // Calculate this bone's position in the horizontal section (0 = first horizontal bone, 1 = tip)
                    int boneIndexInHorizontal = i - (lipBoneIndex + BEND_BONE_COUNT);
                    int totalHorizontalBones = boneCount - (lipBoneIndex + BEND_BONE_COUNT);
                    float boneProgress = Mathf.Clamp01((float)boneIndexInHorizontal / Mathf.Max(1, totalHorizontalBones - 1));

                    // The spiral should wrap around to form a closed circle
                    // As grabCurlProgress goes 0→1, we curl up to 360°+ so tip meets shaft
                    // Total spiral angle = 400° (slightly more than full circle to ensure closure)
                    float totalSpiralAngle = 400f * grabCurlProgress;

                    // Each bone gets a proportional angle based on its position
                    // Bones closer to the tip curl more (they travel the full spiral)
                    // Bones closer to the lip curl less (they form the outer part of spiral)
                    float boneAngle = boneProgress * totalSpiralAngle * curlDirection;

                    // Apply angle relative to the initial direction toward exit point
                    float rotatedAngle = lockedVisitorAngle + boneAngle;
                    desiredDir = new Vector3(
                        Mathf.Cos(rotatedAngle * Mathf.Deg2Rad),
                        Mathf.Sin(rotatedAngle * Mathf.Deg2Rad),
                        0f  // Stay horizontal in XY plane
                    );

                    // Lock rotations when curl is complete and we're pulling
                    if (tonguePhase == TonguePhase.Pulling && grabCurlProgress >= 1f && !curlRotationsLocked)
                    {
                        if (lipBoneIndex >= grabBoneIndex - 1)
                        {
                            LockCurlBoneRotations(grabBoneIndex, boneCount);
                        }
                    }
                }
                else if (tonguePhase == TonguePhase.Reaching)
                {
                    // During reaching: bones point straight toward exit point (no curl yet)
                    desiredDir = targetDirWorld;
                }
                else
                {
                    // Default: point toward exit
                    desiredDir = targetDirWorld;
                }

                // Compute the rotation needed
                Quaternion worldCorrection = Quaternion.FromToRotation(boneWorldDir, desiredDir);
                Quaternion newLocalRot = Quaternion.Inverse(parentWorldRot) * worldCorrection * parentWorldRot * boneRestRotations[i];
                tongueBones[i].localRotation = newLocalRot;
            }
        }

        /// <summary>
        /// Locks the current local rotations of the pivot bone (grabBoneIndex - 1) and all curl bones (from grabBoneIndex to tip).
        /// Called when the grab bone's parent becomes the lip bone during pulling.
        /// During Sinking, the pivot bone rotates from horizontal to vertical, and all curl bones follow with locked rotations.
        /// </summary>
        private void LockCurlBoneRotations(int grabBoneIndex, int boneCount)
        {
            if (tongueBones == null) return;

            // Include the bone just before grab (the pivot) plus all curl bones
            int pivotBoneIndex = grabBoneIndex - 1;
            int lockedBoneCount = boneCount - pivotBoneIndex;  // Includes pivot bone, grab bone, and all to tip
            lockedCurlRotations = new Quaternion[lockedBoneCount];

            for (int i = 0; i < lockedBoneCount; i++)
            {
                int boneIndex = pivotBoneIndex + i;
                if (boneIndex >= 0 && boneIndex < tongueBones.Length && tongueBones[boneIndex] != null)
                {
                    // Store the current local rotation
                    lockedCurlRotations[i] = tongueBones[boneIndex].localRotation;
                }
                else
                {
                    lockedCurlRotations[i] = Quaternion.identity;
                }
            }

            curlRotationsLocked = true;
        }

        /// <summary>
        /// Check if the tip of the tongue has curled back and touched the shaft.
        /// Uses direct bone position comparison (no colliders needed).
        /// When the tip touches the shaft, the spiral loop is complete.
        /// </summary>
        private bool IsTipTouchingShaft()
        {
            if (tongueBones == null || tongueBones.Length == 0) return false;

            // Get tip bone position directly (last bone in the chain)
            int tipBoneIndex = tongueBones.Length - 1;
            if (tongueBones[tipBoneIndex] == null) return false;
            Vector3 tipPos = tongueBones[tipBoneIndex].position;

            // Calculate the lip bone index (same logic as ApplyTongueBoneState)
            int boneCount = tongueBones.Length;
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);
            int lipBoneIndex = Mathf.FloorToInt((tongueZPosition - TONGUE_LIP_Z) / boneSpacing);
            lipBoneIndex = Mathf.Clamp(lipBoneIndex, 0, boneCount - BEND_BONE_COUNT - 1);

            // The shaft starts at the first horizontal bone (after the bend zone)
            // Check bones from shaft start to about 1/3 of horizontal section (not the curled tip portion)
            int shaftStartIndex = lipBoneIndex + BEND_BONE_COUNT;
            int horizontalBoneCount = boneCount - shaftStartIndex;
            int shaftEndIndex = shaftStartIndex + horizontalBoneCount / 3;  // First 1/3 of horizontal (uncurled shaft)

            // Check if any shaft bone positions are within touching distance
            // Use bone collider radius as touch distance (same size as the colliders on bones)
            float touchRadius = TIP_SHAFT_TOUCH_DISTANCE + BONE_COLLIDER_RADIUS;

            for (int i = shaftStartIndex; i <= shaftEndIndex && i < boneCount; i++)
            {
                if (tongueBones[i] == null) continue;

                Vector3 bonePos = tongueBones[i].position;
                float distance = Vector3.Distance(tipPos, bonePos);

                if (distance <= touchRadius)
                {
                    Debug.Log($"[IsTipTouchingShaft] TIP TOUCHED SHAFT at bone {i}! tipPos={tipPos}, bonePos={bonePos}, dist={distance:F3} <= {touchRadius:F3}");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Visitor Detection

        private bool TryGetNextValidVisitor(out VisitorControllerBase visitor)
        {
            visitor = null;

            // First check pending queue
            while (pendingVisitors.Count > 0)
            {
                var pending = pendingVisitors.Dequeue();
                if (IsVisitorValid(pending))
                {
                    visitor = pending;
                    return true;
                }
            }

            // If no pending visitors, scan detection radius
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius);
            foreach (var col in colliders)
            {
                var v = col.GetComponentInParent<VisitorControllerBase>();
                if (v != null && IsVisitorValid(v))
                {
                    visitor = v;
                    return true;
                }
            }

            return false;
        }

        private bool IsVisitorValid(VisitorControllerBase visitor)
        {
            if (visitor == null) return false;
            if (visitor.gameObject == null) return false;
            if (visitor.State == VisitorControllerBase.VisitorState.Consumed) return false;
            if (visitor.State == VisitorControllerBase.VisitorState.Escaping) return false;

            // Check if within detection radius
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(visitor.transform.position.x, visitor.transform.position.y)
            );

            return distance <= detectionRadius;
        }

        #endregion

        #region Setup Methods

        private void LoadPrefabs()
        {
#if UNITY_EDITOR
            if (heartBasePrefab == null)
            {
                heartBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tile/heartbase.prefab");
            }
            if (heartTonguePrefab == null)
            {
                heartTonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Tile/heart tongue.prefab");
            }
#endif

            if (heartBasePrefab == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Heartbase prefab not found after loading!");
            }
            if (heartTonguePrefab == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Heart tongue prefab not found after loading!");
            }
        }

        private void SetupHeartBase()
        {
            if (heartBasePrefab == null)
            {
                CreateFallbackHeartVisual();
                return;
            }

            // Instantiate heartbase as child - preserve prefab's position and scale
            heartBaseInstance = Instantiate(heartBasePrefab, transform);
            heartBaseInstance.name = "HeartBase";
            // The prefab has z=0.7 with scale=0.1, which positions it correctly at ground level
            // Do NOT override localPosition - the prefab's position is already correct

            // Collect materials for pulsing (skip material replacement for GLB models)
            // CollectMaterials(heartBaseInstance); // Disabled - GLB uses its own shader
        }

        private void CreateFallbackHeartVisual()
        {
            heartBaseInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heartBaseInstance.transform.SetParent(transform);
            heartBaseInstance.transform.localPosition = new Vector3(0, 0, -0.3f);
            heartBaseInstance.transform.localScale = Vector3.one * modelSize * 100f; // Fallback needs bigger scale
            heartBaseInstance.name = "Heart_Fallback";

            MeshRenderer renderer = heartBaseInstance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material heartMat = PBRMaterialFactory.CreateEmissiveMaterial(
                    new Color(0.9f, 0.35f, 0.35f),
                    emissionColor,
                    2.0f
                );
                renderer.material = heartMat;
                materials = new Material[] { heartMat };
                meshRenderers = new MeshRenderer[] { renderer };
            }
        }

        private void CollectMaterials(GameObject modelRoot)
        {
            meshRenderers = modelRoot.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                List<Material> matList = new List<Material>();
                foreach (var renderer in meshRenderers)
                {
                    Material[] pbrMats = new Material[renderer.materials.Length];
                    for (int i = 0; i < renderer.materials.Length; i++)
                    {
                        Material originalMat = renderer.materials[i];
                        Texture baseTexture = null;
                        if (originalMat != null)
                        {
                            baseTexture = originalMat.GetTexture("_MainTex") ?? originalMat.GetTexture("_BaseMap");
                        }

                        Color baseColor = new Color(0.9f, 0.35f, 0.35f);
                        Color brightEmission = new Color(1.0f, 0.6f, 0.2f);

                        pbrMats[i] = PBRMaterialFactory.CreateEmissiveMaterialWithTexture(
                            baseColor, brightEmission, 3.0f, baseTexture, $"Heart_Material_{i}");
                        matList.Add(pbrMats[i]);
                    }
                    renderer.materials = pbrMats;
                }
                materials = matList.ToArray();
            }
        }

        private void SetupDetectionCollider()
        {
            // Create a larger trigger collider for visitor detection
            detectionCollider = gameObject.AddComponent<SphereCollider>();
            detectionCollider.radius = detectionRadius;
            detectionCollider.isTrigger = true;
            detectionCollider.center = Vector3.zero;
        }

        private void SetupGlowLight()
        {
            if (!enableGlow) return;

            glowLight = GetComponent<Light>();
            if (glowLight == null)
            {
                glowLight = gameObject.AddComponent<Light>();
            }

            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowRange;
            glowLight.intensity = glowMaxIntensity;
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;
            glowLight.shadows = LightShadows.None;
        }

        private void SetupRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        /// <summary>
        /// Pre-creates the tongue instance at Start() to avoid physics freezes during gameplay.
        /// The tongue is hidden at TONGUE_HIDDEN_Z until needed.
        /// Adding Rigidbodies/Colliders during gameplay causes Unity's physics broadphase to rebuild,
        /// resulting in 10-20+ second freezes with many visitors.
        /// </summary>
        private void PreCreateTongueInstance()
        {
            if (heartTonguePrefab == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Heart tongue prefab not assigned - cannot pre-create!");
                return;
            }

            if (tonguePoolInitialized) return;

            // Instantiate tongue as child of heart, hidden far underground
            heartTongueInstance = Instantiate(heartTonguePrefab, transform);
            heartTongueInstance.name = "HeartTongue_Pooled";

            // NOTE: No runtime scaling - prefab already has correct scale (1, 0.3, 0.3)
            // Colliders are baked into the prefab with correct radii

            // Position at hidden Z
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = TONGUE_HIDDEN_Z;
            heartTongueInstance.transform.localPosition = localPos;

            // Remove any Light components from the tongue model
            RemoveTongueLights();

            // Find and store bone references (these won't change)
            FindTongueBones();

            // Calculate tongue length (won't change)
            CalculateTongueLength();

            // Find bone colliders that were baked into the prefab by TonguePrefabColliderSetup
            FindBoneColliders();

            tonguePoolInitialized = true;
            Debug.Log($"[HeartOfTheMaze] Pre-created tongue instance with {tongueBones?.Length ?? 0} bones, length={tongueLength:F2}");
        }

        /// <summary>
        /// Finds bone colliders that are baked into the prefab.
        /// The colliders are SolidCollider_N objects created by the editor script TonguePrefabColliderSetup.
        /// Colliders are parented to bones and inherit their transforms automatically.
        /// This avoids creating physics objects at runtime which causes 15+ second freezes.
        /// </summary>
        private void FindBoneColliders()
        {
            if (heartTongueInstance == null) return;

            // Find all SolidCollider_* objects anywhere in the hierarchy (they're parented to bones)
            var colliderList = new List<GameObject>();

            foreach (Transform child in heartTongueInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("SolidCollider_"))
                {
                    colliderList.Add(child.gameObject);
                }
            }

            if (colliderList.Count == 0)
            {
                Debug.LogWarning("[HeartOfTheMaze] No SolidCollider_* objects found in tongue prefab! " +
                    "Run FaeMaze > Setup Tongue Prefab Colliders in Unity to bake them.");
                return;
            }

            boneColliderObjects = colliderList.ToArray();

            // Disable colliders initially (tongue is hidden)
            DisableBoneColliders();

            Debug.Log($"[HeartOfTheMaze] Found {boneColliderObjects.Length} pre-baked bone colliders from prefab");
        }

        /// <summary>
        /// Enables bone colliders for physics blocking.
        /// Colliders are parented to bones and inherit their transforms automatically.
        /// </summary>
        private void EnableBoneColliders()
        {
            if (boneColliderObjects == null) return;

            foreach (var colliderObj in boneColliderObjects)
            {
                if (colliderObj != null)
                {
                    var collider = colliderObj.GetComponent<SphereCollider>();
                    if (collider != null) collider.enabled = true;
                }
            }

            // Set static flag so visitors know to check for tongue collision
            IsTongueActiveWithColliders = true;
        }

        /// <summary>
        /// Disables bone colliders when tongue is not active.
        /// Called when tongue is deactivated or hidden.
        /// </summary>
        private void DisableBoneColliders()
        {
            if (boneColliderObjects == null) return;

            foreach (var colliderObj in boneColliderObjects)
            {
                if (colliderObj != null)
                {
                    var collider = colliderObj.GetComponent<SphereCollider>();
                    if (collider != null) collider.enabled = false;
                }
            }

            // Clear static flag so visitors skip expensive collision checks
            IsTongueActiveWithColliders = false;
        }

        #endregion

        #region Visual Updates

        private void UpdateMaterialPulse()
        {
            if (!enablePulse || materials == null || materials.Length == 0) return;

            // Disable pulse while tongue is active (Reaching or Grabbing states)
            if (currentState == HeartState.Reaching || currentState == HeartState.Grabbing)
            {
                // Set emission to minimum during tongue activity
                foreach (var mat in materials)
                {
                    if (mat == null) continue;
                    mat.SetColor("_EmissionColor", Color.black);
                }
                return;
            }

            float angle = Time.time * pulseSpeed * 2f * Mathf.PI;
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;
            float emissionStrength = normalizedPulse * pulseIntensity;

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                Color finalEmission = emissionColor * emissionStrength;
                mat.SetColor("_EmissionColor", finalEmission);
                mat.EnableKeyword("_EMISSION");
            }
        }

        private void UpdateGlowPulse()
        {
            if (!enableGlow || glowLight == null) return;

            // Disable glow pulse while tongue is active (Reaching or Grabbing states)
            if (currentState == HeartState.Reaching || currentState == HeartState.Grabbing)
            {
                glowLight.intensity = glowMinIntensity * 0.5f;
                return;
            }

            float angle = Time.time * glowFrequency * 2f * Mathf.PI;
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);
            glowLight.intensity = intensity;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw heart center
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw detection radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            DrawCircleGizmo(transform.position, detectionRadius, 32);

            // Draw current state indicator
            switch (currentState)
            {
                case HeartState.Idle:
                    Gizmos.color = Color.green;
                    break;
                case HeartState.Reaching:
                    Gizmos.color = Color.yellow;
                    break;
                case HeartState.Grabbing:
                    Gizmos.color = Color.red;
                    break;
            }
            Gizmos.DrawWireSphere(transform.position + Vector3.back * 0.5f, 0.3f);

            // Draw line to target visitor
            if (targetVisitor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        #endregion

    }
}

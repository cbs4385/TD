using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Visitors;
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
        #region Static Events

        /// <summary>
        /// Static event invoked when a visitor is grabbed by the heart tongue.
        /// Nearby visitors can subscribe to this to become frightened when they witness a grab.
        /// Parameter is the world position where the grab occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorGrabbed;

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

        [SerializeField]
        [Tooltip("Distance from heart at which reach collider triggers (instead of bone position)")]
        private float reachTriggerDistance = 0.3f;

        [SerializeField]
        [Tooltip("Radius of grab collider to match mesh cross-section")]
        private float grabTriggerDistance = 0.3f;

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

        // Collider references on tongue
        private Transform reachColliderTransform;
        private Transform grabColliderTransform;
        private SphereCollider reachCollider;
        private SphereCollider grabCollider;

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

        // Tongue movement speeds
        private const float TONGUE_EMERGE_SPEED = 1.5f;   // Units per second for vertical movement
        private const float TONGUE_EXTEND_SPEED = 1.0f;   // Rate of bone rotation for reaching
        private const float TONGUE_CURL_SPEED = 2.0f;     // Rate of curl for grabbing
        private const float TONGUE_SINK_SPEED = 2.0f;     // Speed when pulling visitor down

        // Tongue geometry constants
        // With uniform 0.3 scale, tongue length is ~8.4 units (28 * 0.3)
        private const float TONGUE_START_Z = 9.0f;        // Starting Z (below ground, fits full tongue length)
        private const float TONGUE_LIP_Z = -0.25f;        // Z where tip emerges above heartbase lip (lowered by half)
        private const int BEND_BONE_COUNT = 3;            // Number of bones for the sharp 90° lip bend (very tight, stays near ground)
        private const int RECURVE_BONE_COUNT = 5;         // Number of bones for the slight recurve
        private const float RECURVE_ANGLE = 5f;           // Total angle of recurve (minimal dip to stay very close to ground)
        private const float TONGUE_GROUND_Z = 0.0f;       // Ground level
        private const float LIP_BEND_BONE_INDEX = 3;      // Which bone bends at the lip (approximate)
        private const float REACH_TOUCH_DISTANCE = 1.0f;   // Distance threshold for reach collider touching visitor
        private const float GRAB_TOUCH_DISTANCE = 0.8f;   // Distance threshold for grab collider touching visitor

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

        // Track if we've started the continuation curl after grab contact
        private bool grabContactMade = false;
        private float reverseCurlProgress = 0f;  // Progress of continuation curl after grab (0-1), adds 0-180° more curl

        // Frozen lip bone index - set when grabbing starts to prevent curl relaxation during sinking
        private int frozenLipBoneIndex = -1;

        // Locked curl bone rotations - frozen when grab bone's parent becomes the lip bone during sinking
        // This allows the curl to hinge up naturally as the lip bone rotates back to vertical
        private Quaternion[] lockedCurlRotations = null;
        private bool curlRotationsLocked = false;
        private int lastLipBoneIndexForCurlLock = -1;  // Track when lip bone reaches grab-1 to trigger lock

        // Sinking rotation progress - tracks the 90° rotation of the curl section during sinking
        // Goes from 0 (horizontal, toward visitor) to 1 (vertical, pointing down into heart)
        private float sinkingRotationProgress = 0f;

        // Grab bone index (where grab collider is attached, offset from tip)
        // For a 0.5 diameter half-circle curl around the visitor:
        // - Arc length = π × radius = π × 0.25 ≈ 0.785 units
        // - With 540 bones over ~8.4 units (scaled), bone spacing ≈ 0.0156 units
        // - Need ~50 bones to form the 0.785 unit arc
        private const int GRAB_BONE_OFFSET = 50;  // ~9% from tip (bone ~490 out of 540)

        // Curl diameter for wrapping around visitor (horizontal curl in XY plane)
        private const float CURL_DIAMETER = 0.5f;

        // Radial shift during reverse curl to center visitor in the grab curl
        // This shifts the grab point 0.25 units toward the visitor (half the curl diameter)
        private const float GRAB_RADIAL_SHIFT = 0.25f;

        // Collision detection flags - set by trigger callbacks on collider GameObjects
        private bool reachTouchedVisitor = false;
        private bool grabTouchedVisitor = false;

        // Previous frame's grab bone rotation - used to calculate rotation delta for visitor
        private Quaternion previousGrabBoneRotation = Quaternion.identity;
        private bool hasPreviousGrabBoneRotation = false;

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
                TransitionToIdle();
                return;
            }

            // Update tongue based on current phase
            UpdateTonguePhase();

            // Apply bone transformations
            ApplyTongueBoneState();

            // Update collider positions to match where the mesh visually is
            UpdateColliderPositions();

            // Check for phase transitions based on collider contact
            if (tonguePhase == TonguePhase.Reaching || tonguePhase == TonguePhase.Touching)
            {
                // Check if grab collider touched - start reverse curl
                if (IsGrabColliderTouchingVisitor() && !grabContactMade && tonguePhase == TonguePhase.Touching)
                {
                    grabContactMade = true;

                    // Stop the visitor's movement when grab contact is made
                    if (targetVisitor != null)
                    {
                        targetVisitor.SetGrabbedByHeart();

                        // Notify nearby visitors that a grab occurred - they become frightened
                        OnVisitorGrabbed?.Invoke(targetVisitor.transform.position);
                    }
                }

                // At the midpoint of the reverse curl, visitor starts tracking the midpoint between grab and reach colliders
                if (grabContactMade && reverseCurlProgress >= 0.5f)
                {
                    UpdateVisitorPositionToGrabCollider();
                }

                // Complete grab when continuation curl is done (full 360° wrap)
                if (grabContactMade && reverseCurlProgress >= 1.0f)
                {
                    TransitionToGrabbing();
                }
                else if (IsReachColliderTouchingVisitor() && tonguePhase == TonguePhase.Reaching)
                {
                    // Decide curl direction: perpendicular to visitor direction, pick one randomly or based on some logic
                    // For now, use a simple rule: curl left (CCW) if visitor is in upper half, right (CW) if lower
                    curlDirection = (lockedVisitorAngle >= 0 && lockedVisitorAngle < 180) ? 1 : -1;
                    tonguePhase = TonguePhase.Touching;
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

            // Update collider positions to match where the mesh visually is
            UpdateColliderPositions();

            // Make visitor follow the grab collider position
            UpdateVisitorPositionToGrabCollider();

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
            if (heartTongueInstance == null) return;

            float dt = Time.deltaTime;

            switch (tonguePhase)
            {
                case TonguePhase.Emerging:
                    // Move tongue up (-Z) until tip is above lip
                    tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

                    // Calculate where tip would be (tongue extends in local +Y, which after rotation points somewhere)
                    // For now, simple check: when root Z reaches a certain point based on tongue length
                    float tipZ = tongueZPosition - tongueLength;
                    if (tipZ <= TONGUE_LIP_Z)
                    {
                        tonguePhase = TonguePhase.Reaching;

                        // Daze the visitor when they see the tongue emerge and bend toward them
                        // This stops their movement so the tongue can reach them
                        if (targetVisitor != null)
                        {
                            targetVisitor.OnWitnessMazeGrowth(30f);  // Long daze - tongue will grab before it expires
                        }
                    }
                    break;

                case TonguePhase.Reaching:
                    // Continue moving tongue upward (-Z) to extend more of it past the lip
                    // This makes the horizontal portion longer, reaching toward the visitor
                    tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

                    // Also ramp up bend progress (controls how bent the lip bone is)
                    tongueExtension += TONGUE_EXTEND_SPEED * dt;
                    tongueExtension = Mathf.Clamp01(tongueExtension);

                    break;

                case TonguePhase.Touching:
                    // Only continue extending the tongue BEFORE grab contact
                    // Once grab collider touches visitor, stop Z translation
                    if (!grabContactMade)
                    {
                        tongueZPosition -= TONGUE_EMERGE_SPEED * dt;
                    }

                    // Ramp up the curl progress for the tip-to-grab section
                    grabCurlProgress += TONGUE_CURL_SPEED * dt;
                    grabCurlProgress = Mathf.Clamp01(grabCurlProgress);

                    // After grab contact, progress the reverse curl (mirror animation)
                    if (grabContactMade)
                    {
                        reverseCurlProgress += TONGUE_CURL_SPEED * dt;
                        reverseCurlProgress = Mathf.Clamp01(reverseCurlProgress);
                    }

                    break;

                case TonguePhase.Pulling:
                    // HORIZONTAL RETRACTION: Move the tongue down (+Z), but recalculate which bone
                    // is at the lip so the curl stays at lip level. The effect is that the curl
                    // slides horizontally back toward the heart while staying at lip height.
                    //
                    // As tongueZPosition increases, higher-indexed bones reach the lip level.
                    // The bone at lip level becomes the new "lip bone" that bends 90° toward visitor.
                    // This creates the visual effect of the horizontal portion shortening.
                    tongueZPosition += TONGUE_EMERGE_SPEED * dt;

                    // Calculate where the grab bone is in Z (geometrically, before rotation)
                    float boneSpacingPull = tongueLength / Mathf.Max(1, tongueBones.Length);
                    int grabBoneIndexPull = tongueBones.Length - 1 - GRAB_BONE_OFFSET;

                    // The grab bone's unrotated Z position = tongueZPosition - (grabBoneIndex * boneSpacing)
                    // When this reaches TONGUE_LIP_Z, the grab bone is at the lip level
                    float grabBoneZPull = tongueZPosition - (grabBoneIndexPull * boneSpacingPull);

                    // When the grab bone reaches the lip level, start sinking with curl rotation
                    if (grabBoneZPull >= TONGUE_LIP_Z)
                    {
                        // Freeze the lip bone index now so the curl shape stays stable during sinking
                        FreezeCurrentLipBoneIndex();
                        tonguePhase = TonguePhase.Sinking;
                        sinkingRotationProgress = 0f;
                    }
                    break;

                case TonguePhase.Sinking:
                    // Continue moving tongue down (+Z) into the ground
                    tongueZPosition += TONGUE_SINK_SPEED * dt;

                    // Rotate the curl section from horizontal (pointing toward visitor) to vertical (pointing down)
                    // This happens over a short duration as the tongue sinks
                    sinkingRotationProgress += TONGUE_CURL_SPEED * dt;
                    sinkingRotationProgress = Mathf.Clamp01(sinkingRotationProgress);
                    break;
            }

            // Update tongue instance position
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = tongueZPosition;
            heartTongueInstance.transform.localPosition = localPos;

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
            reverseCurlProgress = 0f;
            frozenLipBoneIndex = -1;
            lockedCurlRotations = null;
            curlRotationsLocked = false;
            lastLipBoneIndexForCurlLock = -1;
            sinkingRotationProgress = 0f;
            previousGrabBoneRotation = Quaternion.identity;
            hasPreviousGrabBoneRotation = false;

            // Destroy tongue instance
            if (heartTongueInstance != null)
            {
                Destroy(heartTongueInstance);
                heartTongueInstance = null;
                reachColliderTransform = null;
                grabColliderTransform = null;
                reachCollider = null;
                grabCollider = null;
                tongueBones = null;
                boneRestPositions = null;
                boneRestRotations = null;
                tongueArmatureRoot = null;
                tongueSkinnedRenderer = null;
                tongueLength = 0f;
            }

            // Reset collision flags
            reachTouchedVisitor = false;
            grabTouchedVisitor = false;
        }

        private void TransitionToReaching()
        {
            currentState = HeartState.Reaching;

            // Initialize tongue state
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TONGUE_START_Z;
            tongueExtension = 0f;
            grabCurlProgress = 0f;

            // Lock in the visitor direction angle now - this won't change during the reach
            // This prevents the tongue from rotating as different bones become the lip bone
            Vector2 heartPos2D = new Vector2(transform.position.x, transform.position.y);
            Vector2 visitorPos2D = new Vector2(targetVisitor.transform.position.x, targetVisitor.transform.position.y);
            Vector2 dirToVisitor = (visitorPos2D - heartPos2D).normalized;
            lockedVisitorAngle = Mathf.Atan2(dirToVisitor.y, dirToVisitor.x) * Mathf.Rad2Deg;

            // Spawn tongue
            SpawnTongue();
        }

        private void TransitionToGrabbing()
        {
            currentState = HeartState.Grabbing;

            // Tongue phase is now Pulling - starts immediately after reverse curl completes
            tonguePhase = TonguePhase.Pulling;

            // Disable all lights on the visitor - they're being consumed
            if (targetVisitor != null)
            {
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
            if (heartTonguePrefab == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Heart tongue prefab not assigned!");
                return;
            }

            if (heartTongueInstance != null)
            {
                Destroy(heartTongueInstance);
            }

            // Instantiate tongue as child of heart
            heartTongueInstance = Instantiate(heartTonguePrefab, transform);
            heartTongueInstance.name = "HeartTongue_Active";

            // IMPORTANT: Use uniform scale to prevent shearing when bones rotate
            // The prefab has non-uniform scale (1, 0.3, 0.3) which causes visual distortion
            // when the tongue bends. We use uniform scale and adjust constants instead.
            heartTongueInstance.transform.localScale = Vector3.one * 0.3f;

            // Position tongue at starting Z (below ground)
            tongueZPosition = TONGUE_START_Z;
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = tongueZPosition;
            heartTongueInstance.transform.localPosition = localPos;

            // Remove any Light components from the tongue model (spotlight etc)
            RemoveTongueLights();

            // Find and store bone references
            FindTongueBones();

            // Calculate total tongue length from bone positions
            CalculateTongueLength();

            // Find reach and grab collider transforms
            FindTongueColliders();

            // Enable colliders as triggers
            SetupTongueColliders();

            // Apply initial bone state (all bones at rest, tongue below ground)
            ApplyTongueBoneState();
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
                // Use bones from SkinnedMeshRenderer - these are the actual bones that deform the mesh
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

            // Store rest poses for all bones
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

        private void FindTongueColliders()
        {
            if (heartTongueInstance == null) return;

            // Find "reach" and "grab" child objects
            foreach (Transform child in heartTongueInstance.GetComponentsInChildren<Transform>())
            {
                if (child.name == "reach")
                {
                    reachColliderTransform = child;
                    reachCollider = child.GetComponent<SphereCollider>();
                }
                else if (child.name == "grab")
                {
                    grabColliderTransform = child;
                    grabCollider = child.GetComponent<SphereCollider>();
                }
            }

            // If colliders weren't found in prefab, create them dynamically
            if (reachColliderTransform == null)
            {
                GameObject reachObj = new GameObject("reach");
                reachObj.transform.SetParent(heartTongueInstance.transform);
                reachObj.transform.localPosition = Vector3.zero;
                reachObj.transform.localRotation = Quaternion.identity;
                reachColliderTransform = reachObj.transform;
                reachCollider = reachObj.AddComponent<SphereCollider>();
                // Radius will be set in SetupTongueColliders to match mesh
            }

            if (grabColliderTransform == null)
            {
                GameObject grabObj = new GameObject("grab");
                grabObj.transform.SetParent(heartTongueInstance.transform);
                grabObj.transform.localPosition = Vector3.zero;
                grabObj.transform.localRotation = Quaternion.identity;
                grabColliderTransform = grabObj.transform;
                grabCollider = grabObj.AddComponent<SphereCollider>();
                // Radius will be set in SetupTongueColliders to match mesh
            }

            // Note: We don't parent colliders to bones because bone transform.position
            // doesn't reflect where the skinned mesh vertices actually are.
            // Instead, we manually update collider positions each frame in UpdateColliderPositions().
        }

        /// <summary>
        /// Reparents the reach and grab colliders to the correct bones in the armature.
        /// - Reach collider -> last bone (Bone_539, tip), positioned at far end along +X
        /// - Grab collider -> GRAB_BONE_OFFSET bones from end (~25% from tip), positioned at bone origin
        ///
        /// Note: The tongue model has base at origin with bones extending along +X toward the tip.
        /// Bones are named Bone_000 through Bone_539 (540 total bones).
        /// </summary>
        private void ReparentCollidersToArmature()
        {
            if (tongueBones == null || tongueBones.Length == 0)
            {
                Debug.LogWarning("[HeartOfTheMaze] Cannot reparent colliders - no bones found");
                return;
            }

            // Use bone indices directly - model has Bone_000 through Bone_539
            // Last bone (tip) for reach collider
            Transform lastBone = tongueBones[tongueBones.Length - 1];

            // Grab bone at GRAB_BONE_OFFSET from tip (~25% from end)
            int grabBoneIndex = Mathf.Max(0, tongueBones.Length - 1 - GRAB_BONE_OFFSET);
            Transform grabBone = tongueBones[grabBoneIndex];

            // Reparent reach collider to last bone (tip)
            if (reachColliderTransform != null && lastBone != null)
            {
                reachColliderTransform.SetParent(lastBone, false);
                // Position at the far end of the bone (bones extend in local +X)
                reachColliderTransform.localPosition = new Vector3(1.0f, 0, 0);  // Far end of bone along +X
                reachColliderTransform.localRotation = Quaternion.identity;
                reachColliderTransform.localScale = Vector3.one;
            }
            else
            {
                Debug.LogWarning($"[HeartOfTheMaze] Could not reparent reach collider. reachCollider: {reachColliderTransform != null}, lastBone: {lastBone != null}");
            }

            // Reparent grab collider to 4th bone from end (MawSeg_020)
            if (grabColliderTransform != null && grabBone != null)
            {
                grabColliderTransform.SetParent(grabBone, false);
                // Position at the root end of the bone (origin)
                grabColliderTransform.localPosition = new Vector3(0, 0, 0);  // Root end of bone
                grabColliderTransform.localRotation = Quaternion.identity;
                grabColliderTransform.localScale = Vector3.one;
            }
            else
            {
                Debug.LogWarning($"[HeartOfTheMaze] Could not reparent grab collider. grabCollider: {grabColliderTransform != null}, grabBone: {grabBone != null}");
            }
        }

        private void SetupTongueColliders()
        {
            // Set collider radii to match the circular mesh cross-section
            // reachTriggerDistance and grabTriggerDistance are the mesh radii at 0.3 scale
            // (i.e., they already account for the tongue's scale)

            if (reachCollider != null)
            {
                reachCollider.isTrigger = true;
                reachCollider.radius = reachTriggerDistance;

                // Add trigger handler component
                var handler = reachColliderTransform.gameObject.AddComponent<TongueColliderHandler>();
                handler.Initialize(this, true);

                // Collider needs a Rigidbody to receive trigger events
                var rb = reachColliderTransform.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

            }
            if (grabCollider != null)
            {
                grabCollider.isTrigger = true;
                grabCollider.radius = grabTriggerDistance;

                // Add trigger handler component
                var handler = grabColliderTransform.gameObject.AddComponent<TongueColliderHandler>();
                handler.Initialize(this, false);

                // Collider needs a Rigidbody to receive trigger events
                var rb = grabColliderTransform.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

            }
        }

        /// <summary>
        /// Updates the reach and grab collider positions using bone world positions.
        /// We apply rotations to bones in ApplyTongueBoneState(), so their world positions
        /// reflect the deformed tongue shape. The colliders (radius 0.3) are larger than
        /// the bone spacing, so bone positions work fine for collision detection.
        /// </summary>
        private void UpdateColliderPositions()
        {
            if (reachColliderTransform == null || grabColliderTransform == null) return;
            if (tonguePhase == TonguePhase.Emerging) return;  // Colliders stay at origin during emerge
            if (tongueBones == null || tongueBones.Length == 0) return;

            int boneCount = tongueBones.Length;

            // Tip bone (last bone) - reach collider
            int tipBoneIndex = boneCount - 1;
            Transform tipBone = tongueBones[tipBoneIndex];

            // Grab bone (GRAB_BONE_OFFSET bones back from tip) - 25% from end
            int grabBoneIndex = Mathf.Max(0, boneCount - 1 - GRAB_BONE_OFFSET);
            Transform grabBone = tongueBones[grabBoneIndex];

            if (tipBone != null)
            {
                reachColliderTransform.position = tipBone.position;
            }

            if (grabBone != null)
            {
                grabColliderTransform.position = grabBone.position;
            }

        }

        /// <summary>
        /// Called by TongueColliderHandler when reach collider touches a visitor.
        /// </summary>
        public void OnReachColliderTrigger(VisitorControllerBase visitor)
        {
            if (visitor == targetVisitor)
            {
                reachTouchedVisitor = true;
            }
        }

        /// <summary>
        /// Called by TongueColliderHandler when grab collider touches a visitor.
        /// </summary>
        public void OnGrabColliderTrigger(VisitorControllerBase visitor)
        {
            if (visitor == targetVisitor)
            {
                grabTouchedVisitor = true;
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

            // Calculate lip bone index based on geometry, not current bone positions
            // During Pulling, this recalculates dynamically as the tongue retracts - higher-indexed
            // bones become the lip bone as the tongue moves down, shortening the horizontal section.
            // During Sinking, we use the frozen index to keep the curl shape stable as it rotates down.
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);
            int lipBoneIndex;
            if (tonguePhase == TonguePhase.Sinking && frozenLipBoneIndex >= 0)
            {
                // Use frozen lip bone index during sinking to keep curl stable as it rotates
                lipBoneIndex = frozenLipBoneIndex;
            }
            else
            {
                // Calculate dynamically during emerging/reaching/touching/pulling
                // As tongueZPosition increases (during pulling), higher bone indices reach the lip
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

            // bendProgress controls how much the lip bone has bent (0 = vertical, 1 = horizontal)
            // During Pulling/Sinking, keep bendProgress at 1 so the tongue stays bent toward visitor
            float bendProgress;
            if (tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
            {
                bendProgress = 1f;  // Stay fully bent toward visitor
            }
            else
            {
                bendProgress = Mathf.Clamp01(tongueExtension * 2f);
            }

            // Target direction in world space (XY plane, Z=0)
            // During Pulling/Sinking, use the locked visitor angle instead of current visitor position
            //
            // During reverse curl, apply a radial shift to move the grab point perpendicular to visitor direction
            // This centers the visitor within the curl arc (shift 0.25 units = half the curl diameter)
            float radialShiftAngle = 0f;
            if (grabContactMade && (tonguePhase == TonguePhase.Touching || tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking))
            {
                // Shift perpendicular to visitor direction, in the curl direction
                // At ~1.5 units from heart, 0.25 unit shift = atan(0.25/1.5) ≈ 9.5 degrees
                radialShiftAngle = 9.5f * curlDirection * reverseCurlProgress;
            }

            Vector3 targetDirWorld;
            float effectiveAngle = lockedVisitorAngle + radialShiftAngle;
            if (tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
            {
                // Use locked angle with radial shift - visitor position is now following grab collider
                targetDirWorld = new Vector3(
                    Mathf.Cos(effectiveAngle * Mathf.Deg2Rad),
                    Mathf.Sin(effectiveAngle * Mathf.Deg2Rad),
                    0f
                );
            }
            else if (grabContactMade)
            {
                // During Touching with grab contact, use locked angle with radial shift
                targetDirWorld = new Vector3(
                    Mathf.Cos(effectiveAngle * Mathf.Deg2Rad),
                    Mathf.Sin(effectiveAngle * Mathf.Deg2Rad),
                    0f
                );
            }
            else
            {
                Vector3 visitorPos = targetVisitor.transform.position;
                Vector3 heartPos = transform.position;
                targetDirWorld = new Vector3(visitorPos.x - heartPos.x, visitorPos.y - heartPos.y, 0f).normalized;
            }

            // First pass: set all bones to rest pose and apply rotations
            for (int i = 0; i < boneCount; i++)
            {
                if (tongueBones[i] == null) continue;
                tongueBones[i].localPosition = boneRestPositions[i];
                tongueBones[i].localRotation = boneRestRotations[i];
            }

            // Grab bone index is GRAB_BONE_OFFSET bones before the tip
            int grabBoneIndex = boneCount - 1 - GRAB_BONE_OFFSET;

            // Calculate the bend zone: lipBoneIndex to lipBoneIndex + BEND_BONE_COUNT
            // Each bone in this zone contributes a small rotation, creating a smooth curve
            int bendEndIndex = Mathf.Min(lipBoneIndex + BEND_BONE_COUNT, boneCount - 1);
            float anglePerBendBone = 90f / Mathf.Max(1, BEND_BONE_COUNT);  // Total 90° spread across bones

            // Calculate the recurve zone: starts after bend zone, curves back down toward visitor
            int recurveStartIndex = bendEndIndex + 1;
            int recurveEndIndex = Mathf.Min(recurveStartIndex + RECURVE_BONE_COUNT, boneCount - 1);
            float anglePerRecurveBone = RECURVE_ANGLE / Mathf.Max(1, RECURVE_BONE_COUNT);

            // Calculate the direction that points down toward the visitor (from horizontal, dip down)
            // This is the targetDirWorld rotated downward by RECURVE_ANGLE around the perpendicular axis
            Vector3 downDir = Vector3.forward;  // +Z is down
            Vector3 recurveTargetDir = Vector3.Slerp(targetDirWorld, downDir, RECURVE_ANGLE / 90f);

            // Second pass: from lip bone to tip, compute rotation to align toward visitor
            // We process in order so each bone's world rotation is correct before computing the next
            for (int i = lipBoneIndex; i < boneCount && lipBoneIndex >= 0; i++)
            {
                if (tongueBones[i] == null) continue;

                // Get the bone's current world rotation (from parent chain)
                Quaternion parentWorldRot = tongueBones[i].parent != null ? tongueBones[i].parent.rotation : Quaternion.identity;

                // The bone's forward direction in world space
                // From logs: bones extend from z=28 (base) to z=1 (tip), so they point in -Z direction
                // After prefab -90° Y rotation + bone 0's 90° Z rotation, the bone local Y becomes world -Z
                // For bones with identity local rotation, their forward is inherited from parent
                Vector3 boneLocalDir = Vector3.up;  // local +Y points toward next bone (tip direction)
                Vector3 boneWorldDir = parentWorldRot * boneRestRotations[i] * boneLocalDir;

                // Determine what direction this bone should point
                Vector3 desiredDir;

                // SINKING PHASE: The lip bone stays horizontal, the PIVOT BONE (grabBoneIndex - 1) rotates from horizontal to vertical
                // All bones from grabBoneIndex onward use locked rotations and follow the pivot
                int pivotBoneIndex = grabBoneIndex - 1;
                if (tonguePhase == TonguePhase.Sinking && i >= frozenLipBoneIndex && i < pivotBoneIndex)
                {
                    // Bones from lip to just before pivot stay horizontal
                    if (i == frozenLipBoneIndex)
                    {
                        // Lip bone: bent 90° from vertical to horizontal
                        Vector3 upDir = Vector3.back;  // -Z is up
                        desiredDir = Vector3.Slerp(upDir, targetDirWorld, 1f);  // Full 90° bend = horizontal
                    }
                    else
                    {
                        // Bones between lip and pivot: point toward visitor (horizontal)
                        desiredDir = targetDirWorld;
                    }
                }
                else if (tonguePhase == TonguePhase.Sinking && i == pivotBoneIndex)
                {
                    // The PIVOT BONE rotates from horizontal to vertical
                    // sinkingRotationProgress goes 0→1, angle goes from horizontal to down
                    float sinkAngle = 90f * sinkingRotationProgress;  // 0° to 90° rotation downward

                    // Start from horizontal (targetDirWorld), rotate toward down (+Z)
                    // At progress=0: horizontal (toward visitor)
                    // At progress=1: vertical down (+Z)
                    float t = sinkAngle / 90f;
                    desiredDir = Vector3.Slerp(targetDirWorld, downDir, t);
                }
                else if (i >= lipBoneIndex && i <= bendEndIndex)
                {
                    // Bones in the first bend zone: instant 90° bend at lip bone
                    // This works for all phases: Emerging, Reaching, Touching, and Pulling
                    // During Pulling, the lipBoneIndex dynamically increases as the tongue retracts
                    float cumulativeAngle = 90f * bendProgress;

                    // Slerp from vertical (-Z) toward horizontal (targetDirWorld) based on cumulative angle
                    float t = cumulativeAngle / 90f;  // 0 = vertical, 1 = horizontal
                    Vector3 upDir = Vector3.back;  // -Z is up
                    desiredDir = Vector3.Slerp(upDir, targetDirWorld, t);
                }
                else if (i >= recurveStartIndex && i <= recurveEndIndex && tonguePhase != TonguePhase.Sinking)
                {
                    // Bones in the recurve zone: curve from horizontal back down toward visitor
                    // Skip during Sinking - handled above
                    int boneInRecurve = i - recurveStartIndex;
                    float cumulativeRecurveAngle = (boneInRecurve + 1) * anglePerRecurveBone * bendProgress;
                    cumulativeRecurveAngle = Mathf.Min(cumulativeRecurveAngle, RECURVE_ANGLE);

                    // Slerp from horizontal (targetDirWorld) toward downward-angled direction
                    float t = cumulativeRecurveAngle / RECURVE_ANGLE;
                    desiredDir = Vector3.Slerp(targetDirWorld, recurveTargetDir, t);
                }
                else if ((tonguePhase == TonguePhase.Touching || tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
                         && i >= grabBoneIndex && grabCurlProgress > 0)
                {
                    // During Touching/Pulling/Sinking phases: bones from grab collider to tip curl
                    // to form a half-circle (180°) around the visitor's vertical body
                    //
                    // The curl must:
                    // 1. Be HORIZONTAL (parallel to ground, in XY plane)
                    // 2. Form a tight curve with diameter 0.5 around the visitor
                    // 3. Visitor's long axis is always aligned with world Z (vertical)
                    //
                    // Curl rotates around Z axis (vertical), staying in the XY plane

                    // Check if we need to lock curl rotations (when grab bone's parent becomes lip bone)
                    if (!curlRotationsLocked && lipBoneIndex == grabBoneIndex - 1 && lastLipBoneIndexForCurlLock != lipBoneIndex)
                    {
                        LockCurlBoneRotations(grabBoneIndex, boneCount);
                        lastLipBoneIndexForCurlLock = lipBoneIndex;
                    }

                    // If curl rotations are locked, use them directly
                    // During Sinking: all curl bones (from grabBoneIndex onward) use locked rotations
                    // The pivot bone (grabBoneIndex - 1) was handled above and rotates; curl bones follow it
                    if (curlRotationsLocked && lockedCurlRotations != null)
                    {
                        // lockedCurlRotations now starts from pivotBoneIndex (grabBoneIndex - 1)
                        int pivotIdx = grabBoneIndex - 1;
                        int curlIndex = i - pivotIdx;

                        // During Sinking, skip locked rotation for pivot bone (curlIndex=0) - it rotates via desiredDir above
                        // All other bones (curlIndex >= 1) use locked rotations
                        if (tonguePhase == TonguePhase.Sinking && curlIndex == 0)
                        {
                            // Pivot bone was already handled above with desiredDir
                            // This shouldn't happen since i >= grabBoneIndex here, but just in case
                        }
                        else if (curlIndex >= 0 && curlIndex < lockedCurlRotations.Length)
                        {
                            tongueBones[i].localRotation = lockedCurlRotations[curlIndex];
                            continue;
                        }
                    }

                    // Calculate curl in HORIZONTAL plane (XY, parallel to ground)
                    // The curl rotates around the Z axis (vertical/up-down)
                    int bonesInCurl = (boneCount - 1) - grabBoneIndex;

                    // Initial curl: 180° in curlDirection (curling around one side of visitor)
                    float initialCurlAngle = 180f * grabCurlProgress;

                    // After grab contact: curl in the OPPOSITE direction to wrap around the other side
                    // If initial curl went left (CCW), reverse curl goes right (CW) to embrace visitor
                    float reverseCurlAngle = 0f;
                    if (grabContactMade)
                    {
                        // Reverse curl: curl in opposite direction to wrap around visitor
                        // As reverseCurlProgress goes 0→1, we curl 0→180° in -curlDirection
                        reverseCurlAngle = 180f * reverseCurlProgress;
                    }

                    // This bone's contribution to the curl
                    float boneProgress = (float)(i - grabBoneIndex) / Mathf.Max(1, bonesInCurl);

                    // Calculate angle for this bone:
                    // The tongue wraps AROUND the visitor like arms hugging:
                    // - First 180°: curl one direction to get around one side of visitor
                    // - Next 180°: curl OPPOSITE direction to wrap around the other side
                    //
                    // This creates a U-shape that embraces the visitor from both sides
                    float cumulativeAngle;
                    if (!grabContactMade)
                    {
                        // Initial curl only: each bone gets proportional angle in curlDirection
                        // As grabCurlProgress goes 0→1, angle goes 0→180°
                        cumulativeAngle = boneProgress * initialCurlAngle * curlDirection;
                    }
                    else
                    {
                        // After grab contact: MIRROR the curl shape to wrap around the other side
                        //
                        // Initial curl: each bone has angle = boneProgress * 180° * curlDirection
                        // This creates a curve where each bone adds a small positive angle (if curlDirection=1)
                        //
                        // To MIRROR this curve (reflect it), we need to INVERT each bone's relative angle
                        // If bone was at +5° relative to previous, it should become -5° relative
                        //
                        // The cumulative angle at each bone:
                        // - Initial: boneProgress * 180° * curlDirection (e.g., 0°, 36°, 72°, 108°, 144°, 180°)
                        // - Mirrored: boneProgress * 180° * (-curlDirection) (e.g., 0°, -36°, -72°, -108°, -144°, -180°)
                        //
                        // We interpolate from initial to mirrored as reverseCurlProgress goes 0→1
                        float initialBoneAngle = boneProgress * 180f * curlDirection;      // Original curl
                        float mirroredBoneAngle = boneProgress * 180f * (-curlDirection);  // Mirrored curl

                        // Lerp between initial and mirrored based on reverse progress
                        cumulativeAngle = Mathf.Lerp(initialBoneAngle, mirroredBoneAngle, reverseCurlProgress);
                    }

                    // Curl in XY plane (horizontal, parallel to ground)
                    // Use effectiveAngle (which includes radialShiftAngle) to shift curl with rest of tongue
                    float rotatedAngle = effectiveAngle + cumulativeAngle;
                    desiredDir = new Vector3(
                        Mathf.Cos(rotatedAngle * Mathf.Deg2Rad),
                        Mathf.Sin(rotatedAngle * Mathf.Deg2Rad),
                        0f  // Stay in XY plane (horizontal)
                    );
                }
                else
                {
                    // Bones past the recurve zone: maintain the recurve direction (angled down toward visitor)
                    desiredDir = recurveTargetDir;
                }

                // Compute the rotation needed to rotate boneWorldDir to desiredDir
                Quaternion worldCorrection = Quaternion.FromToRotation(boneWorldDir, desiredDir);

                // Convert world correction to local space rotation
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

        private bool IsReachColliderTouchingVisitor()
        {
            return reachTouchedVisitor;
        }

        private bool IsGrabColliderTouchingVisitor()
        {
            return grabTouchedVisitor;
        }

        private void UpdateVisitorPositionToGrabCollider()
        {
            if (targetVisitor == null) return;
            if (grabColliderTransform == null || reachColliderTransform == null) return;

            // Position visitor at the midpoint between grab and reach colliders
            // This centers the visitor within the curl arc
            Vector3 grabPos = grabColliderTransform.position;
            Vector3 reachPos = reachColliderTransform.position;
            Vector3 midpoint = (grabPos + reachPos) * 0.5f;

            // During sinking, the visitor also moves down with the tongue
            // The midpoint Z already includes the sinking motion from bone positions
            targetVisitor.transform.position = midpoint;

            // During Pulling and Sinking, apply the rotation delta from the grab bone to the visitor
            // The visitor follows the same rotational path as the grab point, just offset to the midpoint
            if (tonguePhase == TonguePhase.Pulling || tonguePhase == TonguePhase.Sinking)
            {
                int grabBoneIndex = tongueBones.Length - 1 - GRAB_BONE_OFFSET;
                if (grabBoneIndex >= 0 && grabBoneIndex < tongueBones.Length && tongueBones[grabBoneIndex] != null)
                {
                    Quaternion currentGrabBoneRotation = tongueBones[grabBoneIndex].rotation;

                    if (hasPreviousGrabBoneRotation)
                    {
                        // Calculate the rotation delta: how much the grab bone rotated this frame
                        // Apply the same delta to the visitor so they rotate together
                        Quaternion rotationDelta = currentGrabBoneRotation * Quaternion.Inverse(previousGrabBoneRotation);
                        targetVisitor.transform.rotation = rotationDelta * targetVisitor.transform.rotation;
                    }

                    previousGrabBoneRotation = currentGrabBoneRotation;
                    hasPreviousGrabBoneRotation = true;
                }
            }

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

    /// <summary>
    /// Helper component attached to tongue reach/grab colliders to forward trigger events
    /// back to the HeartOfTheMaze.
    /// </summary>
    public class TongueColliderHandler : MonoBehaviour
    {
        private HeartOfTheMaze heart;
        private bool isReachCollider;

        public void Initialize(HeartOfTheMaze heart, bool isReachCollider)
        {
            this.heart = heart;
            this.isReachCollider = isReachCollider;
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTrigger(other);
        }

        // Use OnTriggerStay as well since kinematic rigidbodies moved via transform
        // may not reliably fire OnTriggerEnter
        private void OnTriggerStay(Collider other)
        {
            HandleTrigger(other);
        }

        private void HandleTrigger(Collider other)
        {
            if (heart == null) return;

            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;

            if (isReachCollider)
            {
                heart.OnReachColliderTrigger(visitor);
            }
            else
            {
                heart.OnGrabColliderTrigger(visitor);
            }
        }
    }
}

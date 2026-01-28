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
    /// Frog Tongue Behavior:
    /// 1. EMERGING: Tongue rises from Z=TONGUE_START_Z toward ground level, tip emerges first
    /// 2. EXTENDING: Lip bone bends 90°, horizontal section grows as tongue continues rising
    ///    - Tongue continuously tracks visitor position
    ///    - When visitor collides with tongue, they become Grabbed
    /// 3. RETRACTING: Reverse the process - tongue descends, horizontal section shrinks
    ///    - Visitor attached to tip follows it back
    ///    - When Z reaches TONGUE_START_Z, visitor is consumed
    /// </summary>
    public class HeartOfTheMaze : MonoBehaviour
    {
        #region Static Events and Properties

        /// <summary>
        /// Static event invoked when a visitor is grabbed by the heart tongue.
        /// Parameter is the world position where the grab occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorGrabbed;

        /// <summary>
        /// Static flag indicating if a tongue is currently active.
        /// </summary>
        public static bool IsTongueActiveWithColliders { get; private set; } = false;

        #endregion

        #region Enums

        private enum HeartState
        {
            Idle,       // Only heartbase visible, no visitors detected
            Reaching,   // Tongue extending toward visitor
            Grabbing    // Visitor grabbed, tongue retracting
        }

        private enum TonguePhase
        {
            Emerging,   // Rising from underground, tip not yet at ground level
            Extending,  // Tip above ground, horizontal section growing toward visitor
            Retracting  // Pulling back with visitor attached
        }

        #endregion

        #region Serialized Fields

        [Header("Position Settings")]
        [SerializeField]
        private bool autoPosition = true;

        [Header("Essence Settings")]
        [SerializeField]
        private int essencePerVisitor = 10;

        [Header("Model Settings")]
        [SerializeField]
        private GameObject heartBasePrefab;

        [SerializeField]
        private GameObject heartTonguePrefab;

        [SerializeField]
        private float modelSize = 0.012f;

        [Header("Detection Settings")]
        [SerializeField]
        private float detectionRadius = 2.5f;

        [Header("Material Animation Settings")]
        [SerializeField]
        private bool enablePulse = true;

        [SerializeField]
        private float pulseSpeed = 2f;

        [SerializeField]
        private float pulseIntensity = 2f;

        [SerializeField]
        private Color emissionColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("3D Lighting Settings")]
        [SerializeField]
        private bool enableGlow = true;

        [SerializeField]
        private Color glowColor = new Color(1f, 0.7f, 0.7f, 1f);

        [SerializeField]
        private float glowRange = 10f;

        [SerializeField]
        private float glowFrequency = 1.5f;

        [SerializeField]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        private float glowMaxIntensity = 2.0f;

        #endregion

        #region Private Fields

        // State machine
        private HeartState currentState = HeartState.Idle;
        private TonguePhase tonguePhase = TonguePhase.Emerging;

        // Model instances
        private GameObject heartBaseInstance;
        private GameObject heartTongueInstance;

        // Visitor tracking
        private VisitorControllerBase targetVisitor;
        private Queue<VisitorControllerBase> pendingVisitors = new Queue<VisitorControllerBase>();

        // Detection collider
        private SphereCollider detectionCollider;

        // Materials and lighting
        private Light glowLight;
        private MeshRenderer[] meshRenderers;
        private Material[] materials;

        // Tongue Z position (controls how much tongue is above ground)
        // High Z = tongue below ground, Low Z = tongue above ground
        // The tongue rises by DECREASING Z, retracts by INCREASING Z
        private float tongueZPosition = TONGUE_START_Z;

        // Tongue movement speeds
        private const float TONGUE_EMERGE_SPEED = 9.0f;   // Units per second for vertical movement
        private const float TONGUE_RETRACT_SPEED = 9.0f;  // Speed when retracting with visitor

        // Tongue geometry constants
        private const float TONGUE_HIDDEN_Z = 1000f;      // Z position when pooled (far underground)
        private const float TONGUE_START_Z = 28.0f;       // Z position to start emerging (must be > tongue length ~27)
        private const float TONGUE_GROUND_Z = 0.0f;       // Ground level (Z=0)
        private const int BEND_BONE_COUNT = 5;            // Number of bones for the 90° bend at ground level

        // Tongue armature
        private Transform[] tongueBones;
        private Vector3[] boneRestPositions;
        private Quaternion[] boneRestRotations;
        private SkinnedMeshRenderer tongueSkinnedRenderer;
        private float tongueLength = 0f;

        // Current target angle (updated every frame during extending)
        private float currentTargetAngle = 0f;

        // Pooling
        private bool tonguePoolInitialized = false;

        // Bone colliders (baked into prefab, used for collision detection)
        private GameObject[] boneColliderObjects = null;

        #endregion

        #region Properties

        public int EssencePerVisitor => essencePerVisitor;
        public string CurrentStateName => currentState.ToString();

        #endregion

        #region Public Methods

        public void PositionFromMazeGrid()
        {
            var mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGridBehaviour == null) return;
            transform.position = mazeGridBehaviour.HeartWorldPosition;
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (visitor == null) return;

            int essence = visitor.GetEssenceReward();

            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, VisitorFate.Consumed, essence);
            }

            if (GameController.Instance != null)
            {
                GameController.Instance.AddEssence(essence, EssenceSource.VisitorConsumedByHeart, $"Reward: {essence}");
            }

            if (HeartPowers.HeartPowerManager.Instance != null)
            {
                HeartPowers.HeartPowerManager.Instance.NotifyVisitorConsumed();
            }

            SoundManager.Instance?.PlayVisitorConsumed();
            Destroy(visitor.gameObject);
        }

        /// <summary>
        /// Called by visitor when they collide with a tongue bone collider.
        /// This is the signal to grab them.
        /// </summary>
        public void NotifyVisitorTouchedTongue(VisitorControllerBase visitor)
        {
            // Only respond during Extending phase
            if (currentState != HeartState.Reaching || tonguePhase != TonguePhase.Extending) return;

            // Only grab our target visitor
            if (visitor != targetVisitor) return;

            TransitionToGrabbing();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupDetectionCollider();
            SetupRigidbody();
        }

        private void Start()
        {
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }

            LoadPrefabs();
            SetupHeartBase();
            SetupGlowLight();
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
            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;
            if (visitor.State == VisitorControllerBase.VisitorState.Consumed) return;

            if (visitor != targetVisitor && !pendingVisitors.Contains(visitor))
            {
                pendingVisitors.Enqueue(visitor);
            }
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
            if (TryGetNextValidVisitor(out VisitorControllerBase visitor))
            {
                targetVisitor = visitor;
                TransitionToReaching();
            }
        }

        private void UpdateReachingState()
        {
            if (targetVisitor == null || targetVisitor.State == VisitorControllerBase.VisitorState.Consumed)
            {
                TransitionToIdle();
                return;
            }

            // Update target angle to track visitor
            UpdateTargetAngle();

            // Update tongue position and phase
            UpdateTongueEmerging();

            // Apply bone rotations based on current state
            ApplyTongueBoneRotations();

            // Update tongue transform
            UpdateTongueTransform();
        }

        private void UpdateGrabbingState()
        {
            if (targetVisitor == null)
            {
                TransitionToIdle();
                return;
            }

            // Retract tongue (increase Z)
            tongueZPosition += TONGUE_RETRACT_SPEED * Time.deltaTime;

            // Apply bone rotations (same as extending, but tongue is descending)
            ApplyTongueBoneRotations();

            // Update tongue transform
            UpdateTongueTransform();

            // Move visitor to follow the tip
            MoveVisitorToTip();

            // Check if fully retracted
            if (tongueZPosition >= TONGUE_START_Z)
            {
                OnVisitorConsumed(targetVisitor);
                targetVisitor = null;
                TransitionToIdle();
            }
        }

        private void UpdateTongueEmerging()
        {
            float dt = Time.deltaTime;

            // Tongue rises by decreasing Z
            tongueZPosition -= TONGUE_EMERGE_SPEED * dt;

            // Calculate how much of the tongue is above ground
            // tongueZPosition is the Z of the tongue BASE
            // The tip is at tongueZPosition - tongueLength (in unrotated space)
            float tipZ = tongueZPosition - tongueLength;

            // Phase transition: Emerging -> Extending when tip reaches ground level
            if (tonguePhase == TonguePhase.Emerging && tipZ <= TONGUE_GROUND_Z)
            {
                tonguePhase = TonguePhase.Extending;
            }
        }

        private void UpdateTargetAngle()
        {
            if (targetVisitor == null) return;

            Vector2 heartPos = new Vector2(transform.position.x, transform.position.y);
            Vector2 visitorPos = new Vector2(targetVisitor.transform.position.x, targetVisitor.transform.position.y);
            Vector2 dirToVisitor = (visitorPos - heartPos).normalized;

            currentTargetAngle = Mathf.Atan2(dirToVisitor.y, dirToVisitor.x) * Mathf.Rad2Deg;
        }

        private void TransitionToIdle()
        {
            currentState = HeartState.Idle;
            targetVisitor = null;
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TONGUE_START_Z;
            currentTargetAngle = 0f;

            if (heartTongueInstance != null)
            {
                heartTongueInstance.name = "HeartTongue_Pooled";
                DisableBoneColliders();
                ResetTongueBonesToRest();

                // Hide tongue at pooled position
                Vector3 localPos = heartTongueInstance.transform.localPosition;
                localPos.z = TONGUE_HIDDEN_Z;
                heartTongueInstance.transform.localPosition = localPos;
            }

            IsTongueActiveWithColliders = false;
        }

        private void TransitionToReaching()
        {
            currentState = HeartState.Reaching;
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TONGUE_START_Z;

            UpdateTargetAngle();

            // Activate tongue from pool
            if (heartTongueInstance != null)
            {
                heartTongueInstance.name = "HeartTongue_Active";
                ResetTongueBonesToRest();
                UpdateTongueTransform();
                EnableBoneColliders();
            }

            IsTongueActiveWithColliders = true;
        }

        private void TransitionToGrabbing()
        {
            currentState = HeartState.Grabbing;
            tonguePhase = TonguePhase.Retracting;

            if (targetVisitor != null)
            {
                targetVisitor.SetGrabbedByHeart();
                OnVisitorGrabbed?.Invoke(targetVisitor.transform.position);
                DisableVisitorLights(targetVisitor);
            }
        }

        private void DisableVisitorLights(VisitorControllerBase visitor)
        {
            if (visitor == null) return;
            foreach (Light light in visitor.GetComponentsInChildren<Light>())
            {
                light.enabled = false;
            }
        }

        #endregion

        #region Tongue Bone Control

        /// <summary>
        /// Updates the tongue instance's local position based on tongueZPosition.
        /// </summary>
        private void UpdateTongueTransform()
        {
            if (heartTongueInstance == null) return;

            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = tongueZPosition;
            heartTongueInstance.transform.localPosition = localPos;
        }

        /// <summary>
        /// Applies bone rotations to create the frog-tongue effect.
        ///
        /// The tongue model extends in local +Y from base (bone 0) to tip (bone N-1).
        /// At rest, the tongue points straight up (-Z in world space due to prefab rotation).
        ///
        /// We calculate which bone is at ground level (the "bend bone") and:
        /// - Bones BELOW ground level: stay at rest pose (pointing up)
        /// - Bones AT the bend: rotate 90° from vertical to horizontal toward visitor
        /// - Bones ABOVE ground level: point horizontally toward visitor
        /// </summary>
        private void ApplyTongueBoneRotations()
        {
            if (tongueBones == null || tongueBones.Length == 0) return;

            int boneCount = tongueBones.Length;
            float boneSpacing = tongueLength / Mathf.Max(1, boneCount);

            // Find which bone is at ground level (Z=0)
            // Bone i's unrotated Z position = tongueZPosition - (i * boneSpacing)
            int groundBoneIndex = -1;
            for (int i = 0; i < boneCount; i++)
            {
                float boneZ = tongueZPosition - (i * boneSpacing);
                if (boneZ <= TONGUE_GROUND_Z)
                {
                    groundBoneIndex = i;
                    break;
                }
            }

            // If no bone has emerged yet, keep all at rest
            if (groundBoneIndex < 0)
            {
                ResetTongueBonesToRest();
                return;
            }

            // Target direction: horizontal toward visitor
            Vector3 targetDirWorld = new Vector3(
                Mathf.Cos(currentTargetAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentTargetAngle * Mathf.Deg2Rad),
                0f
            );

            Vector3 upDir = Vector3.back;  // -Z is up in our coordinate system

            // Apply rotations
            for (int i = 0; i < boneCount; i++)
            {
                if (tongueBones[i] == null) continue;

                // Reset position (bones don't translate, only rotate)
                tongueBones[i].localPosition = boneRestPositions[i];

                if (i < groundBoneIndex)
                {
                    // Below ground: rest pose (pointing up)
                    tongueBones[i].localRotation = boneRestRotations[i];
                }
                else if (i < groundBoneIndex + BEND_BONE_COUNT)
                {
                    // Bend zone: interpolate from vertical to horizontal
                    float t = (float)(i - groundBoneIndex + 1) / BEND_BONE_COUNT;
                    t = Mathf.Clamp01(t);

                    Vector3 desiredDir = Vector3.Slerp(upDir, targetDirWorld, t);
                    ApplyBoneRotationToward(i, desiredDir);
                }
                else
                {
                    // Above bend zone: point horizontally toward visitor
                    ApplyBoneRotationToward(i, targetDirWorld);
                }
            }
        }

        /// <summary>
        /// Rotates a bone so its forward direction (+Y in local space) points toward desiredDir in world space.
        /// </summary>
        private void ApplyBoneRotationToward(int boneIndex, Vector3 desiredDirWorld)
        {
            if (tongueBones[boneIndex] == null) return;

            Transform bone = tongueBones[boneIndex];
            Quaternion parentWorldRot = bone.parent != null ? bone.parent.rotation : Quaternion.identity;

            // Bone's forward direction in local space is +Y
            Vector3 boneLocalForward = Vector3.up;

            // Current world direction of the bone's forward
            Vector3 boneWorldForward = parentWorldRot * boneRestRotations[boneIndex] * boneLocalForward;

            // Compute rotation to align bone's forward with desired direction
            Quaternion worldCorrection = Quaternion.FromToRotation(boneWorldForward, desiredDirWorld);
            Quaternion newLocalRot = Quaternion.Inverse(parentWorldRot) * worldCorrection * parentWorldRot * boneRestRotations[boneIndex];

            bone.localRotation = newLocalRot;
        }

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

        /// <summary>
        /// Moves the grabbed visitor to follow the tongue tip.
        /// </summary>
        private void MoveVisitorToTip()
        {
            if (tongueBones == null || tongueBones.Length == 0 || targetVisitor == null) return;

            int tipBoneIndex = tongueBones.Length - 1;
            if (tongueBones[tipBoneIndex] == null) return;

            Vector3 tipPos = tongueBones[tipBoneIndex].position;

            // Move visitor to tip's XY position, keep their Z
            targetVisitor.transform.position = new Vector3(tipPos.x, tipPos.y, targetVisitor.transform.position.z);
        }

        #endregion

        #region Visitor Detection

        private bool TryGetNextValidVisitor(out VisitorControllerBase visitor)
        {
            visitor = null;

            while (pendingVisitors.Count > 0)
            {
                var pending = pendingVisitors.Dequeue();
                if (IsVisitorValid(pending))
                {
                    visitor = pending;
                    return true;
                }
            }

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
            if (visitor == null || visitor.gameObject == null) return false;
            if (visitor.State == VisitorControllerBase.VisitorState.Consumed) return false;
            if (visitor.State == VisitorControllerBase.VisitorState.Escaping) return false;

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
        }

        private void SetupHeartBase()
        {
            if (heartBasePrefab == null)
            {
                CreateFallbackHeartVisual();
                return;
            }

            heartBaseInstance = Instantiate(heartBasePrefab, transform);
            heartBaseInstance.name = "HeartBase";
        }

        private void CreateFallbackHeartVisual()
        {
            heartBaseInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heartBaseInstance.transform.SetParent(transform);
            heartBaseInstance.transform.localPosition = new Vector3(0, 0, -0.3f);
            heartBaseInstance.transform.localScale = Vector3.one * modelSize * 100f;
            heartBaseInstance.name = "Heart_Fallback";

            MeshRenderer renderer = heartBaseInstance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material heartMat = PBRMaterialFactory.CreateEmissiveMaterial(
                    new Color(0.9f, 0.35f, 0.35f), emissionColor, 2.0f);
                renderer.material = heartMat;
                materials = new Material[] { heartMat };
                meshRenderers = new MeshRenderer[] { renderer };
            }
        }

        private void SetupDetectionCollider()
        {
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

        private void PreCreateTongueInstance()
        {
            if (heartTonguePrefab == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Heart tongue prefab not assigned!");
                return;
            }

            if (tonguePoolInitialized) return;

            heartTongueInstance = Instantiate(heartTonguePrefab, transform);
            heartTongueInstance.name = "HeartTongue_Pooled";

            // Hide at pooled position
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = TONGUE_HIDDEN_Z;
            heartTongueInstance.transform.localPosition = localPos;

            // Remove lights from tongue model
            foreach (Light light in heartTongueInstance.GetComponentsInChildren<Light>())
            {
                Destroy(light);
            }

            // Find bones
            FindTongueBones();
            CalculateTongueLength();
            FindBoneColliders();

            tonguePoolInitialized = true;
        }

        private void FindTongueBones()
        {
            if (heartTongueInstance == null) return;

            tongueSkinnedRenderer = heartTongueInstance.GetComponentInChildren<SkinnedMeshRenderer>();

            if (tongueSkinnedRenderer != null && tongueSkinnedRenderer.bones != null && tongueSkinnedRenderer.bones.Length > 0)
            {
                tongueBones = tongueSkinnedRenderer.bones;
            }
            else
            {
                var boneList = new List<Transform>();
                foreach (var t in heartTongueInstance.GetComponentsInChildren<Transform>())
                {
                    string nameLower = t.name.ToLower();
                    if (nameLower.Contains("bone") || nameLower.Contains("joint"))
                    {
                        boneList.Add(t);
                    }
                }
                tongueBones = boneList.ToArray();
            }

            // Store rest poses
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

        private void CalculateTongueLength()
        {
            tongueLength = 0f;
            if (tongueBones == null || tongueBones.Length < 2) return;

            Vector3 firstBone = tongueBones[0].position;
            Vector3 lastBone = tongueBones[tongueBones.Length - 1].position;
            tongueLength = Vector3.Distance(firstBone, lastBone);

            // Add one more bone segment for the tip
            if (tongueBones.Length > 1)
            {
                tongueLength += tongueLength / (tongueBones.Length - 1);
            }
        }

        private void FindBoneColliders()
        {
            if (heartTongueInstance == null) return;

            var colliderList = new List<GameObject>();
            foreach (Transform child in heartTongueInstance.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("SolidCollider_"))
                {
                    colliderList.Add(child.gameObject);
                }
            }

            boneColliderObjects = colliderList.ToArray();
            DisableBoneColliders();
        }

        private void EnableBoneColliders()
        {
            if (boneColliderObjects == null) return;

            foreach (var obj in boneColliderObjects)
            {
                if (obj != null)
                {
                    var col = obj.GetComponent<SphereCollider>();
                    if (col != null) col.enabled = true;
                }
            }
        }

        private void DisableBoneColliders()
        {
            if (boneColliderObjects == null) return;

            foreach (var obj in boneColliderObjects)
            {
                if (obj != null)
                {
                    var col = obj.GetComponent<SphereCollider>();
                    if (col != null) col.enabled = false;
                }
            }
        }

        #endregion

        #region Visual Updates

        private void UpdateMaterialPulse()
        {
            if (!enablePulse || materials == null || materials.Length == 0) return;

            if (currentState != HeartState.Idle)
            {
                foreach (var mat in materials)
                {
                    if (mat != null) mat.SetColor("_EmissionColor", Color.black);
                }
                return;
            }

            float pulse = (Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI) + 1f) / 2f;
            Color finalEmission = emissionColor * pulse * pulseIntensity;

            foreach (var mat in materials)
            {
                if (mat != null)
                {
                    mat.SetColor("_EmissionColor", finalEmission);
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }

        private void UpdateGlowPulse()
        {
            if (!enableGlow || glowLight == null) return;

            if (currentState != HeartState.Idle)
            {
                glowLight.intensity = glowMinIntensity * 0.5f;
                return;
            }

            float pulse = (Mathf.Sin(Time.time * glowFrequency * 2f * Mathf.PI) + 1f) / 2f;
            glowLight.intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, pulse);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            DrawCircleGizmo(transform.position, detectionRadius, 32);

            Gizmos.color = currentState switch
            {
                HeartState.Idle => Color.green,
                HeartState.Reaching => Color.yellow,
                HeartState.Grabbing => Color.red,
                _ => Color.white
            };
            Gizmos.DrawWireSphere(transform.position + Vector3.back * 0.5f, 0.3f);

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

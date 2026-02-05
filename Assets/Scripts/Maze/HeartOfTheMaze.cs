using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Visitors;
using FaeMaze.Roguelike;
using FaeMaze.HeartPowers;
using FaeMaze.Utilities;
using System;
using System.Collections.Generic;
using static FaeMaze.Systems.FrighteningEventManager;

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
        private float tongueZPosition = TongueUtility.TONGUE_START_Z;

        // Tongue movement speeds - loaded from GameSettings
        private float tongueEmergeSpeed;
        private float tongueRetractSpeed;

        // Tongue armature (consolidated via TongueUtility)
        private TongueBoneData tongueBoneData;

        // Current target angle (updated every frame during extending)
        private float currentTargetAngle = 0f;

        // Pooling
        private bool tonguePoolInitialized = false;

        // Bone colliders (baked into prefab, used for collision detection)
        private GameObject[] boneColliderObjects = null;

        // Frightening event (registered when tongue is active)
        private FrighteningEvent currentFrighteningEvent;

        // Grab-time relative transform: visitor's position and heading relative to tongue at grab moment
        private Vector3 grabPositionOffset;
        private float visitorHeadingAngle;
        private float chainAngleAtGrab;

        // The tongue bend point (where it transitions from vertical to horizontal).
        // Above the HeartBase model top, at the same Z as visitors so the tongue can reach them.
        private const float HEART_TONGUE_BEND_Z = -0.25f;

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

            // Apply blessing and heart form multipliers for consumption essence
            int essence = HeartPowerUtils.CalculateConsumptionEssence(visitor, additionalMultiplier: 1.0f, applyBlessingMultiplier: true);

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
            LoadSettingsFromGameSettings();
            SetupDetectionCollider();
            SetupRigidbody();
        }

        /// <summary>
        /// Loads configurable gameplay settings from GameSettings.
        /// Allows SerializeField values to override if set in Inspector.
        /// </summary>
        private void LoadSettingsFromGameSettings()
        {
            // Load tongue speeds from GameSettings, applying Heart Form multiplier if active
            float formSpeedMultiplier = HeartFormManager.Instance?.GetTongueSpeedMultiplier() ?? 1.0f;
            tongueEmergeSpeed = GameSettings.TongueEmergeSpeed * formSpeedMultiplier;
            tongueRetractSpeed = GameSettings.TongueRetractSpeed * formSpeedMultiplier;

            // Detection radius: use GameSettings if SerializeField is at default (2.5f)
            if (Mathf.Approximately(detectionRadius, 2.5f))
            {
                detectionRadius = GameSettings.HeartDetectionRadius;
            }
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

            // Update tongue transform BEFORE bone rotations so bones[0].position.z is current
            UpdateTongueTransform();

            // Apply bone rotations (FindGroundBoneIndex uses cached Z offsets)
            ApplyTongueBoneRotations();
        }

        private void UpdateGrabbingState()
        {
            if (targetVisitor == null)
            {
                TransitionToIdle();
                return;
            }

            // Retract tongue (increase Z)
            tongueZPosition += tongueRetractSpeed * Time.deltaTime;

            // Update tongue transform BEFORE bone rotations so bones[0].position.z is current
            UpdateTongueTransform();

            // Apply bone rotations (FindGroundBoneIndex uses cached Z offsets)
            ApplyTongueBoneRotations();

            // Move visitor to follow the tip
            MoveVisitorToTip();

            // Check if fully retracted
            if (tongueZPosition >= TongueUtility.TONGUE_START_Z)
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
            tongueZPosition -= tongueEmergeSpeed * dt;

            // Phase transition: Emerging -> Extending when tip bone reaches bend Z
            // Use actual tip bone world Z (not mathematical estimate) to match FindGroundBoneIndex
            float tipZ = tongueBoneData?.Bones != null && tongueBoneData.Bones.Length > 0
                ? tongueBoneData.Bones[tongueBoneData.Bones.Length - 1].position.z
                : tongueZPosition;
            if (tonguePhase == TonguePhase.Emerging && tipZ <= HEART_TONGUE_BEND_Z)
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
            tongueZPosition = TongueUtility.TONGUE_START_Z;
            currentTargetAngle = 0f;

            // Unregister frightening event
            if (currentFrighteningEvent != null && FrighteningEventManager.Instance != null)
            {
                FrighteningEventManager.Instance.UnregisterEvent(currentFrighteningEvent);
                currentFrighteningEvent = null;
            }

            if (heartTongueInstance != null)
            {
                heartTongueInstance.name = "HeartTongue_Pooled";
                DisableBoneColliders();
                ResetTongueBonesToRest();

                // Hide tongue at pooled position
                Vector3 localPos = heartTongueInstance.transform.localPosition;
                localPos.z = TongueUtility.TONGUE_HIDDEN_Z;
                heartTongueInstance.transform.localPosition = localPos;
            }

            IsTongueActiveWithColliders = false;
        }

        private void TransitionToReaching()
        {
            currentState = HeartState.Reaching;
            tonguePhase = TonguePhase.Emerging;
            tongueZPosition = TongueUtility.TONGUE_START_Z;

            UpdateTargetAngle();

            // Register frightening event - nearby visitors will flee from the heart
            if (FrighteningEventManager.Instance != null)
            {
                currentFrighteningEvent = FrighteningEventManager.Instance.RegisterEvent(
                    FrighteningEventManager.EventType.HeartTongue,
                    transform.position,
                    this
                );
            }

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
                TongueUtility.CaptureGrabOffsets(tongueBoneData, targetVisitor.transform,
                    out grabPositionOffset, out visitorHeadingAngle, out chainAngleAtGrab);

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
        /// Delegates to TongueUtility for the actual bone rotation math.
        /// </summary>
        private void ApplyTongueBoneRotations()
        {
            if (tongueBoneData == null || tongueBoneData.Bones == null || tongueBoneData.Bones.Length == 0) return;

            int groundBoneIndex = TongueUtility.FindGroundBoneIndex(tongueBoneData, HEART_TONGUE_BEND_Z, heartTongueInstance);

            if (groundBoneIndex < 0)
            {
                TongueUtility.ResetBonesToRest(tongueBoneData);
                return;
            }

            TongueUtility.ApplyBoneRotations(tongueBoneData, groundBoneIndex, currentTargetAngle);
        }

        private void ResetTongueBonesToRest()
        {
            TongueUtility.ResetBonesToRest(tongueBoneData);
        }

        private void MoveVisitorToTip()
        {
            if (targetVisitor == null) return;
            TongueUtility.MoveVisitorToTip(tongueBoneData, targetVisitor.transform,
                grabPositionOffset, visitorHeadingAngle, chainAngleAtGrab);
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
                heartBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Tile/heartbase.prefab");
            }
            if (heartTonguePrefab == null)
            {
                heartTonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Tile/heart tongue.prefab");
            }
#endif
        }

        private void SetupHeartBase()
        {
            if (heartBasePrefab == null) return;

            heartBaseInstance = Instantiate(heartBasePrefab, transform);
            heartBaseInstance.name = "HeartBase";
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
            glowLight.renderMode = LightRenderMode.ForcePixel;
            glowLight.shadows = LightShadows.None;
        }

        private void SetupRigidbody()
        {
            gameObject.AddKinematicRigidbody();
        }

        private void PreCreateTongueInstance()
        {
            if (heartTonguePrefab == null) return;
            if (tonguePoolInitialized) return;

            heartTongueInstance = Instantiate(heartTonguePrefab, transform);
            heartTongueInstance.name = "HeartTongue_Pooled";

            // Hide at pooled position
            Vector3 localPos = heartTongueInstance.transform.localPosition;
            localPos.z = TongueUtility.TONGUE_HIDDEN_Z;
            heartTongueInstance.transform.localPosition = localPos;

            // Remove lights from tongue model
            foreach (Light light in heartTongueInstance.GetComponentsInChildren<Light>())
            {
                Destroy(light);
            }

            // Find bones and store rest poses
            tongueBoneData = TongueUtility.SetupTongueBones(heartTongueInstance);
            TongueUtility.CacheRestWorldZOffsets(tongueBoneData, heartTongueInstance);
            FindBoneColliders();

            tonguePoolInitialized = true;
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

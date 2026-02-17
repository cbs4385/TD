using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.HeartPowers;
using FaeMaze.Roguelike;
using static FaeMaze.Systems.FrighteningEventManager;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Puka that creates hazards for visitors using world-space coordinates.
    /// When visitors come within range, the Puka becomes active and drags them into the water.
    /// </summary>
    public class PukaHazard : RegistryComponent<PukaHazard>
    {
        #region Enums

        public enum PukaState
        {
            Idle,
            Active
        }

        #endregion

        #region Serialized Fields

        [Header("Kelpie Model")]
        [SerializeField]
        [Tooltip("Reference to the kelpie_active child model that rotates to face visitors")]
        private Transform kelpieActiveModel;

        [Header("Detection Settings")]
        [SerializeField]
        [Tooltip("World-space distance to detect and capture a visitor")]
        private float detectionRadius = 1.5f;

        [SerializeField]
        [Tooltip("How often to scan for adjacent visitors (seconds)")]
        private float scanInterval = 0.5f;

        [Header("Drowning Animation")]
        [SerializeField]
        [Tooltip("Duration of the drag phase (moving toward pond on X/Y plane)")]
        private float dragDuration = 0.333f;

        [SerializeField]
        [Tooltip("Duration of the submerge phase (sinking in Z direction)")]
        private float submergeDuration = 0.333f;

        [SerializeField]
        [Tooltip("How far toward the pond center to drag on X/Y plane")]
        private float dragDistance = 0.5f;

        [SerializeField]
        [Tooltip("Final Z position when visitor is fully submerged")]
        private float submergedZ = 2f;

        [SerializeField]
        [Tooltip("X rotation applied to visitor when knocked down (degrees) - tips forward")]
        private float knockdownXRotation = -90f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw detection radius in Scene view")]
        private bool debugDrawRadius = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private HashSet<GameObject> processedVisitors;
        private float scanTimer;
        private PukaState currentState = PukaState.Idle;
        private Animator animator;
        private Animation legacyAnimation;
        private GameObject currentVictim;
        private PropAudioSource propAudio;
        private FrighteningEvent currentFrighteningEvent;

        #endregion

        #region Properties

        /// <summary>Gets the world position of this Puka</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Gets the current state of the Puka</summary>
        public PukaState State => currentState;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            processedVisitors = new HashSet<GameObject>();

            // Try to find animator on this object or in children
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            // Also look for legacy Animation component
            legacyAnimation = GetComponent<Animation>();
            if (legacyAnimation == null)
            {
                legacyAnimation = GetComponentInChildren<Animation>();
            }
        }

        private void Start()
        {
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            // Try to find kelpie model if not assigned
            // New prefab structure has Armature as direct child
            if (kelpieActiveModel == null)
            {
                kelpieActiveModel = transform.Find("kelpie_react");
                if (kelpieActiveModel == null)
                {
                    kelpieActiveModel = transform.Find("kelpie_active");
                }
                if (kelpieActiveModel == null)
                {
                    kelpieActiveModel = transform.Find("Root");
                }
                if (kelpieActiveModel == null)
                {
                    kelpieActiveModel = transform.Find("Armature");
                }
                // If still null, use this transform itself (the puka node is the rotatable object)
                if (kelpieActiveModel == null && transform.childCount > 0)
                {
                    kelpieActiveModel = transform.GetChild(0);
                }
            }

            // Reset the kelpie model's local transform to (0,0,0) relative to puka node
            if (kelpieActiveModel != null)
            {
                kelpieActiveModel.localPosition = Vector3.zero;
                kelpieActiveModel.localRotation = Quaternion.identity;
            }

            // Also try to find animator/animation on the kelpie model
            if (kelpieActiveModel != null)
            {
                if (animator == null)
                {
                    animator = kelpieActiveModel.GetComponent<Animator>();
                }
                if (legacyAnimation == null)
                {
                    legacyAnimation = kelpieActiveModel.GetComponent<Animation>();
                }
            }

            // Set initial state to idle (static) - freeze the animator
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.speed = 0f;
                // Try new clip name first, then fall back to old name
                if (HasAnimatorState("Idle"))
                    animator.Play("Idle", 0, 0f);
                else
                    animator.Play("ArmatureAction", 0, 0f);
            }

            // Setup audio
            SetupAudio();
        }

        private void SetupAudio()
        {
            propAudio = PropAudioSource.EnsureOnGameObject(gameObject, PropAudioSource.PropSoundType.Pond);
            propAudio.SetMaxDistance(detectionRadius * 4f);
        }

        private void Update()
        {
            // Only scan for visitors when idle
            if (currentState == PukaState.Idle)
            {
                scanTimer += Time.deltaTime;
                if (scanTimer >= scanInterval)
                {
                    scanTimer = 0f;
                    ScanForAdjacentVisitors();
                }
            }

            // Clean up destroyed visitors from processed set
            processedVisitors?.RemoveWhere(v => v == null);
        }

        #endregion

        #region State Management

        /// <summary>
        /// Checks if the animator has a clip with the given name.
        /// </summary>
        private bool HasAnimatorState(string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == stateName)
                    return true;
            }
            return false;
        }

        private void SetState(PukaState newState)
        {
            if (currentState == newState)
                return;

            currentState = newState;

            // When idle, stop animation completely (static pose)
            if (newState == PukaState.Idle && animator != null)
            {
                animator.speed = 0f;
                // Play first frame and freeze - try new name first
                if (HasAnimatorState("Idle"))
                    animator.Play("Idle", 0, 0f);
                else
                    animator.Play("ArmatureAction", 0, 0f);
                return;
            }

            // When active, ensure animator is playing at normal speed
            if (animator != null)
            {
                animator.speed = 1f;
            }

            // Animation clip/state names to try (new naming: React/Idle, old naming: ArmatureAction)
            string[] clipNamesToTry = newState == PukaState.Active
                ? new[] { "React", "ArmatureAction.002", "kelpie_react", "react", "active", "attack" }
                : new[] { "Idle", "ArmatureAction", "ArmatureAction.001", "kelpie_idle", "idle", "default" };

            // Try legacy Animation first (from GLB import)
            if (legacyAnimation != null)
            {
                bool clipFound = false;
                foreach (string clipName in clipNamesToTry)
                {
                    if (legacyAnimation.GetClip(clipName) != null)
                    {
                        legacyAnimation.Play(clipName);
                        clipFound = true;
                        break;
                    }
                }

                if (!clipFound)
                {
                    // Try playing the default/first clip
                    if (legacyAnimation.clip != null)
                    {
                        legacyAnimation.Play(legacyAnimation.clip.name);
                    }
                }
            }
            // Try Animator (Mecanim) - play states by name directly
            else if (animator != null)
            {
                bool stateFound = false;
                foreach (string stateName in clipNamesToTry)
                {
                    // Try to play the state directly - CrossFade will work if state exists
                    try
                    {
                        animator.Play(stateName, 0, 0f);
                        stateFound = true;
                        break;
                    }
                    catch
                    {
                        // State doesn't exist, try next
                    }
                }

                if (!stateFound)
                {
                    // Try to play any available state
                    var clips = animator.runtimeAnimatorController?.animationClips;
                    if (clips != null && clips.Length > 0)
                    {
                        animator.Play(clips[0].name, 0, 0f);
                    }
                }
            }
        }

        #endregion

        #region Visitor Detection and Interaction

        private bool IsVisitorActive(VisitorControllerBase.VisitorState state)
        {
            return state == VisitorControllerBase.VisitorState.Walking
                || state == VisitorControllerBase.VisitorState.Fascinated
                || state == VisitorControllerBase.VisitorState.Confused
                || state == VisitorControllerBase.VisitorState.Frightened;
        }

        /// <summary>
        /// Scans for visitors within detection radius.
        /// </summary>
        private void ScanForAdjacentVisitors()
        {
            if (currentState != PukaState.Idle)
                return;

            // Find all visitor types (using base class to get all subtypes)
            var allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in allVisitors)
            {
                // Unity objects can be in a destroyed state where == null returns false
                // but accessing properties still throws. Use ReferenceEquals + Unity null check.
                if (visitor == null || !visitor)
                    continue;

                // Safely check if already processed
                GameObject visitorGO = visitor.gameObject;
                if (visitorGO == null || processedVisitors.Contains(visitorGO))
                    continue;

                if (!IsVisitorActive(visitor.State))
                    continue;

                // Check world-space distance
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= detectionRadius)
                {
                    CaptureVisitor(visitor);
                    break; // Only capture one visitor at a time
                }
            }
        }

        /// <summary>
        /// Captures a visitor and begins the drowning sequence.
        /// </summary>
        private void CaptureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
                return;

            // Mark as processed so we don't capture again
            processedVisitors.Add(visitor.gameObject);
            currentVictim = visitor.gameObject;

            // Set state to active
            SetState(PukaState.Active);

            // Register frightening event - nearby visitors will flee
            currentFrighteningEvent = HeartPowerUtils.RegisterFrighteningEvent(
                FrighteningEventManager.EventType.PukaDrowning, transform.position, this);

            // Rotate kelpie_active model to face the visitor
            RotateKelpieToFaceVisitor(visitor.transform.position);

            // Start the drowning coroutine
            StartCoroutine(DrownVisitorCoroutine(visitor));
        }

        /// <summary>
        /// Rotates the kelpie_active model to face the visitor.
        /// </summary>
        private void RotateKelpieToFaceVisitor(Vector3 visitorPosition)
        {
            if (kelpieActiveModel == null)
                return;

            // Calculate direction to visitor in XY plane
            Vector3 direction = visitorPosition - transform.position;
            direction.z = 0;

            if (direction.sqrMagnitude > 0.001f)
            {
                // Calculate angle in XY plane
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                // Rotate around world Z axis to face the visitor in XY plane
                kelpieActiveModel.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// Coroutine that handles the visitor drowning animation.
        /// Phase 1: Knockdown (instant rotation) + drag on X/Y plane toward pond
        /// Phase 2: Submerge in Z direction
        /// </summary>
        private IEnumerator DrownVisitorCoroutine(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                SetState(PukaState.Idle);
                yield break;
            }

            GameObject visitorObj = visitor.gameObject;
            Transform visitorTransform = visitor.transform;

            // Set the visitor to Drowning state (stops movement and clears all other states)
            visitor.BecomeDrowning();

            string visitorLabel = visitor.EntityLabel ?? visitor.gameObject.name;
            string propLabel = FaeMaze.Systems.EntityLabels.GetPropLabel(gameObject);
            FaeMaze.Systems.GameEventLogger.LogPropVisitorEvent(propLabel, visitorLabel, "Drowning");

            // Store initial values
            Vector3 initialPosition = visitorTransform.position;
            Quaternion initialRotation = visitorTransform.rotation;

            // Wait a moment before knockdown
            yield return new WaitForSeconds(0.25f);

            // Apply knockdown rotation instantly (tips forward)
            Quaternion knockdownRotation = initialRotation * Quaternion.Euler(knockdownXRotation, 0f, 0f);
            visitorTransform.rotation = knockdownRotation;

            // Wait a moment after knockdown before dragging
            yield return new WaitForSeconds(0.25f);

            // Calculate drag direction on X/Y plane toward pond center
            Vector3 pondPosition = transform.position;
            Vector3 directionToPond = new Vector3(
                pondPosition.x - initialPosition.x,
                pondPosition.y - initialPosition.y,
                0f
            ).normalized;

            // Phase 1: Drag on X/Y plane toward pond
            Vector3 dragStartPos = initialPosition;
            Vector3 dragEndPos = initialPosition + directionToPond * dragDistance;

            float elapsed = 0f;

            while (elapsed < dragDuration)
            {
                if (visitorObj == null)
                {
                    SetState(PukaState.Idle);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dragDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Lerp position on X/Y plane only
                visitorTransform.position = Vector3.Lerp(dragStartPos, dragEndPos, smoothT);

                // Continuously track the visitor with the kelpie model
                RotateKelpieToFaceVisitor(visitorTransform.position);

                yield return null;
            }

            // Ensure we're at the drag end position
            if (visitorObj != null)
            {
                visitorTransform.position = dragEndPos;
            }

            // Phase 2: Submerge in Z direction
            Vector3 submergeStartPos = visitorTransform.position;
            Vector3 submergedPosition = new Vector3(
                submergeStartPos.x,
                submergeStartPos.y,
                submergedZ
            );

            elapsed = 0f;

            while (elapsed < submergeDuration)
            {
                if (visitorObj == null)
                {
                    SetState(PukaState.Idle);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / submergeDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Lerp Z position only
                visitorTransform.position = Vector3.Lerp(submergeStartPos, submergedPosition, smoothT);

                // Continuously track the visitor with the kelpie model
                RotateKelpieToFaceVisitor(visitorTransform.position);

                yield return null;
            }

            // Ensure final position
            if (visitorObj != null)
            {
                visitorTransform.position = submergedPosition;

                // Play sound effect
                FaeMaze.Audio.SoundManager.Instance?.PlayVisitorConsumed();

                // Puka's Portion mutation: player receives a share of drowned visitor's essence
                int essenceAwarded = 0;
                float pukaShare = PropMutationManager.Instance?.GetPukaEssenceShare() ?? 0f;
                if (pukaShare > 0f && visitor != null)
                {
                    int visitorEssence = Mathf.RoundToInt(visitor.GetEssenceReward());
                    essenceAwarded = Mathf.RoundToInt(visitorEssence * pukaShare);
                    if (essenceAwarded > 0)
                    {
                        GameController.Instance?.AddEssence(essenceAwarded, EssenceSource.PukaGift,
                            $"Puka's Portion from {visitor.Archetype}");
                    }
                }

                // Track visitor fate - drowned by Puka/Kelpie
                if (GameStatsTracker.Instance != null)
                {
                    GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, VisitorFate.Drowned, essenceAwarded);
                }

                FaeMaze.Systems.GameEventLogger.LogVisitorFate(
                    visitor.EntityLabel ?? visitor.gameObject.name, "Drowned", essenceAwarded);

                // Destroy the visitor
                Destroy(visitorObj);
            }

            // Unregister frightening event
            HeartPowerUtils.UnregisterFrighteningEvent(ref currentFrighteningEvent);

            // Revert to idle state
            currentVictim = null;
            SetState(PukaState.Idle);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugDrawRadius)
                return;

            // Draw detection radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawRadius)
                return;

            // Draw detection radius (brighter when selected)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Draw this Puka's position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        #endregion
    }
}

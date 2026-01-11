using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Sleepwalking Devotee archetype - begins in a trance,
    /// pulled directly toward the Heart, but reacts strongly to interference.
    /// High-value target for essence rewards.
    /// Uses world-space navigation for all pathfinding.
    /// </summary>
    public class SleepwalkingDevoteeController : VisitorControllerBase
    {
        [Header("Devotee Settings")]
        [SerializeField]
        [Tooltip("Visual feedback for mesmerized state")]
        private bool showMesmerizedGlow = true;

        private bool hasInitialized = false;

        #region Properties

        public override VisitorState State => state;
        public override float MoveSpeed => moveSpeed;
        public override bool IsEntranced => isEntranced;
        public override float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }
        public override bool IsFascinated => isFascinated;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
        }

        protected virtual void Start()
        {
            // Initialize mesmerized state if configured
            if (config != null && config.StartsMesmerized && !hasInitialized)
            {
                InitializeMesmerizedState();
                hasInitialized = true;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Sets up initial mesmerized state for Devotees.
        /// Called at spawn to put them into trance toward the Heart.
        /// Uses world-space navigation.
        /// </summary>
        private void InitializeMesmerizedState()
        {
            if (config.InitialMesmerizedDuration > 0)
            {
                // Set mesmerized state
                isMesmerized = true;
                currentTimedState = VisitorState.Mesmerized;
                currentStateDuration = config.InitialMesmerizedDuration;
                currentStateTimer = currentStateDuration;
                state = VisitorState.Mesmerized;

                // Ensure destination is the Heart using world-space position
                if (mazeGridBehaviour != null)
                {
                    Vector3 heartWorldPos = mazeGridBehaviour.HeartWorldPosition;
                    SetWorldDestination(heartWorldPos);
                }
            }
        }

        #endregion

        #region State Management

        protected override void RefreshStateFromFlags()
        {
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            if (isMesmerized)
            {
                state = VisitorState.Mesmerized;
            }
            else if (isFrightened)
            {
                state = VisitorState.Frightened;
            }
            else if (isLost)
            {
                state = VisitorState.Lost;
            }
            else if (isFascinated)
            {
                state = VisitorState.Fascinated;
            }
            else if (isLured)
            {
                state = VisitorState.Lured;
            }
            else
            {
                state = VisitorState.Walking;
            }
        }

        /// <summary>
        /// Called when a timed state expires.
        /// Devotees transition based on context when mesmerized ends.
        /// </summary>
        protected override void OnStateExpired(VisitorState expiredState)
        {
            if (expiredState == VisitorState.Mesmerized)
            {
                isMesmerized = false;

                // 50% chance to become Lost, 50% to become Walking
                if (Random.value < 0.5f)
                {
                    // Become lost - wander aimlessly
                    float minDuration = config != null ? config.LostDetourMin : 5f;
                    float maxDuration = config != null ? config.LostDetourMax : 12f;
                    float duration = Random.Range(minDuration, maxDuration);
                    SetLost(duration);
                }
                else
                {
                    // Return to Walking toward destination
                    state = VisitorState.Walking;
                    RefreshStateFromFlags();
                    RecalculatePath();
                }
            }
            else
            {
                base.OnStateExpired(expiredState);
            }

            currentTimedState = VisitorState.Idle;
            currentStateDuration = 0f;
            currentStateTimer = 0f;
        }

        #endregion

        #region Mesmerized Behavior

        /// <summary>
        /// While mesmerized, Devotees ignore confusion and head straight to Heart.
        /// </summary>
        public override float GetConfusionChance()
        {
            // No confusion while mesmerized
            if (isMesmerized)
            {
                return 0f;
            }

            return base.GetConfusionChance();
        }

        /// <summary>
        /// Override destination logic - Devotees always head toward Heart when mesmerized.
        /// Uses world-space navigation.
        /// </summary>
        protected override Vector3 GetDestinationForCurrentState()
        {
            if (isMesmerized && mazeGridBehaviour != null)
            {
                return mazeGridBehaviour.HeartWorldPosition;
            }

            // If frightened, still prefer Heart over exits (drawn to it)
            if (state == VisitorState.Frightened && mazeGridBehaviour != null)
            {
                return mazeGridBehaviour.HeartWorldPosition;
            }

            return base.GetDestinationForCurrentState();
        }

        #endregion

        #region Detour Behavior

        /// <summary>
        /// Resets detour state - Devotees don't track confusion.
        /// </summary>
        protected override void ResetDetourState()
        {
            // Devotees don't use confusion tracking
        }

        /// <summary>
        /// Devotees don't take random detours while mesmerized.
        /// </summary>
        protected override void HandleDetourAtWaypoint()
        {
            // No detours while mesmerized - stay on course to Heart
            if (isMesmerized)
            {
                // Continue along path
                if (worldPath != null && worldPathIndex < worldPath.Count)
                {
                    worldPathIndex++;
                    if (worldPathIndex >= worldPath.Count)
                    {
                        OnPathCompleted();
                    }
                }
                return;
            }

            // Use base behavior when not mesmerized
            base.HandleDetourAtWaypoint();
        }

        #endregion

        #region Interference Response

        /// <summary>
        /// When Devotee's trance is broken (e.g., by FairyRing or Lantern),
        /// decide whether to refresh mesmerized or become lost.
        /// </summary>
        public void OnTranceDisturbed(float disturbanceStrength = 0.5f)
        {
            if (!isMesmerized)
            {
                return; // Already awake
            }

            // Higher disturbance = more likely to break trance
            float breakChance = Mathf.Clamp01(disturbanceStrength);

            if (Random.value < breakChance)
            {
                // Trance broken - become Lost
                isMesmerized = false;
                float minDuration = config != null ? config.LostDetourMin : 5f;
                float maxDuration = config != null ? config.LostDetourMax : 12f;
                float lostDuration = Random.Range(minDuration, maxDuration);
                SetLost(lostDuration);
            }
            else
            {
                // Trance refreshed - extend mesmerized duration
                float refreshDuration = config != null ? config.InitialMesmerizedDuration * 0.5f : 5f;
                currentStateTimer += refreshDuration;
                currentStateDuration += refreshDuration;
            }
        }

        #endregion

        #region Visual Feedback

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Draw mesmerized state indicator
            if (showMesmerizedGlow && isMesmerized && Application.isPlaying)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f); // Purple glow
                Gizmos.DrawWireSphere(transform.position, visitorSize * 0.015f);
            }
        }

        #endregion
    }
}

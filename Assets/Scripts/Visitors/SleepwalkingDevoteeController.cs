using UnityEngine;
using FaeMaze.Systems;

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
        /// Called at spawn to put them into trance toward the Heart edge.
        /// Uses world-space navigation. Visitors NEVER path to node centers.
        /// </summary>
        private void InitializeMesmerizedState()
        {
            if (config.InitialMesmerizedDuration > 0)
            {
                // Set fascinated/mesmerized state
                isFascinated = true;
                currentTimedState = VisitorState.Fascinated;
                currentStateDuration = config.InitialMesmerizedDuration;
                currentStateTimer = currentStateDuration;
                state = VisitorState.Fascinated;

                // Ensure destination is the Heart edge (not center)
                if (mazeGridBehaviour != null)
                {
                    Vector3 heartApproachPos = GetHeartApproachPosition();
                    SetWorldDestination(heartApproachPos);
                }
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Called when a timed state expires.
        /// Devotees transition based on context when mesmerized ends.
        /// </summary>
        protected override void OnStateExpired(VisitorState expiredState)
        {
            if (expiredState == VisitorState.Fascinated)
            {
                isFascinated = false;

                // 50% chance to become Confused, 50% to become Walking
                if (RandomManager.Value < 0.5f)
                {
                    // Become confused - wander aimlessly
                    float minDuration = config != null ? config.LostDetourMin : 5f;
                    float maxDuration = config != null ? config.LostDetourMax : 12f;
                    float duration = RandomManager.Range(minDuration, maxDuration);
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
            if (isFascinated)
            {
                return 0f;
            }

            return base.GetConfusionChance();
        }

        /// <summary>
        /// Override destination logic - Devotees always head toward Heart edge when mesmerized.
        /// Uses world-space navigation. Visitors NEVER path to node centers.
        /// </summary>
        protected override Vector3 GetDestinationForCurrentState()
        {
            if (isFascinated && mazeGridBehaviour != null)
            {
                return GetHeartApproachPosition();
            }

            // If frightened, still prefer Heart edge over exits (drawn to it)
            if (state == VisitorState.Frightened && mazeGridBehaviour != null)
            {
                return GetHeartApproachPosition();
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
            if (isFascinated)
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
            if (!isFascinated)
            {
                return; // Already awake
            }

            // Higher disturbance = more likely to break trance
            float breakChance = Mathf.Clamp01(disturbanceStrength);

            if (RandomManager.Value < breakChance)
            {
                // Trance broken - become Lost
                isFascinated = false;
                float minDuration = config != null ? config.LostDetourMin : 5f;
                float maxDuration = config != null ? config.LostDetourMax : 12f;
                float lostDuration = RandomManager.Range(minDuration, maxDuration);
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
            if (showMesmerizedGlow && isFascinated && Application.isPlaying)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f); // Purple glow
                Gizmos.DrawWireSphere(transform.position, 0.4f);
            }
        }

        #endregion
    }
}

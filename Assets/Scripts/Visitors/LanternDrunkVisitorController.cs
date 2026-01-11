using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// LanternDrunk Pilgrim archetype - highly susceptible to fascination and getting lost.
    /// Slow but easy to keep wandering due to high confusion and lantern susceptibility.
    /// Uses world-space navigation for all pathfinding.
    /// </summary>
    public class LanternDrunkVisitorController : VisitorControllerBase
    {
        #region Static Registry

        private static readonly HashSet<LanternDrunkVisitorController> _activeVisitors = new HashSet<LanternDrunkVisitorController>();

        /// <summary>Gets all active LanternDrunk visitors in the scene</summary>
        public static IReadOnlyCollection<LanternDrunkVisitorController> All => _activeVisitors;

        #endregion

        #region Confusion/Lost Fields

        [Header("Confusion Settings")]
        [SerializeField]
        [Tooltip("Whether confusion is enabled")]
        private bool confusionEnabled = true;

        [SerializeField]
        [Tooltip("Draw confusion segments in the scene view for debugging")]
        private bool debugConfusionGizmos;

        // Note: Confusion state fields (isConfused, confusionSegmentActive, etc.)
        // are now in VisitorControllerBase as protected fields

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public override VisitorState State => state;

        /// <summary>Gets the current move speed</summary>
        public override float MoveSpeed => moveSpeed;

        /// <summary>Gets whether this visitor is entranced by a Fairy Ring</summary>
        public override bool IsEntranced => isEntranced;

        /// <summary>Gets or sets the speed multiplier applied to movement</summary>
        public override float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }

        /// <summary>Gets whether this visitor is fascinated by a FaeLantern</summary>
        public override bool IsFascinated => isFascinated;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            isConfused = confusionEnabled;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _activeVisitors.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _activeVisitors.Remove(this);
        }

        #endregion

        #region State Management

        protected override void RefreshStateFromFlags()
        {
            if (state == VisitorState.Consumed || state == VisitorState.Escaping)
            {
                return;
            }

            // Timed states take priority (in order of precedence)
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
            else if (confusionSegmentActive)
            {
                state = VisitorState.Confused;
            }
            else
            {
                state = VisitorState.Walking;
            }
        }

        #endregion

        #region Detour Behavior - High Confusion

        /// <summary>
        /// Resets confusion state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;
            lostSegmentActive = false;
            lostSegmentEndIndex = 0;
        }

        /// <summary>
        /// Determines whether a detour should be attempted based on confusion state.
        /// LanternDrunks have HIGH confusion chance from config.
        /// Uses world-space navigation.
        /// </summary>
        protected override bool ShouldAttemptDetour(Vector2Int currentPos)
        {
            // Check if we're in an active confusion segment
            if (confusionSegmentActive)
            {
                if (currentPathIndex <= confusionSegmentEndIndex)
                {
                    return false; // Don't interrupt active segment
                }

                // Segment complete - end it and allow normal routing
                confusionSegmentActive = false;
                DecideRecoveryFromConfusion();
                currentPathIndex++;
                RefreshStateFromFlags();
                return false;
            }

            // Check base class state-specific detour logic
            bool baseWantsDetour = base.ShouldAttemptDetour(currentPos);

            // Confused state: check if we should trigger confusion detour
            if (state == VisitorState.Confused && isConfused && confusionEnabled)
            {
                // Prevent confusion for first 10 waypoints
                if (waypointsTraversedSinceSpawn < 10)
                {
                    return false;
                }

                // In world-space mode, use world path for checking position
                if (worldPath == null || worldPathIndex >= worldPath.Count - 1)
                {
                    return false;
                }

                // Use archetype-specific confusion chance (HIGH for LanternDrunks)
                float confusionChance = GetConfusionChance();
                return Random.value <= confusionChance;
            }

            return baseWantsDetour;
        }

        /// <summary>
        /// Handles confusion-specific detour logic.
        /// LanternDrunks use config-based detour lengths.
        /// Uses world-space navigation.
        /// </summary>
        protected override void HandleStateSpecificDetour(Vector2Int currentPos)
        {
            // Handle Confused state detours
            if (state == VisitorState.Confused && isConfused && confusionEnabled)
            {
                // In world-space mode, trigger path recalculation
                // The base class will handle building a new world path
                RecalculatePath();
                return;
            }

            // Fallback to base class implementation (handles Lost state, etc.)
            base.HandleStateSpecificDetour(currentPos);
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Draw confusion segment using world path
            if (debugConfusionGizmos && worldPath != null && worldPath.Count > 0 && confusionSegmentEndIndex > 0)
            {
                Gizmos.color = Color.magenta;
                int lastConfusionIndex = Mathf.Min(confusionSegmentEndIndex, worldPath.Count - 1);

                for (int i = 0; i < lastConfusionIndex; i++)
                {
                    Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                }
            }
        }

        #endregion
    }
}

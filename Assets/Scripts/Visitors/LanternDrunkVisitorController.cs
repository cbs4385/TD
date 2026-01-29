using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

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
        private bool _confusionEnabled = true;

        [SerializeField]
        [Tooltip("Draw debug info in the scene view")]
        private bool debugGizmos;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public override VisitorState State => state;

        /// <summary>Gets the current move speed</summary>
        public override float MoveSpeed => moveSpeed;

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
            confusionEnabled = _confusionEnabled;
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

        #region Detour Behavior - High Confusion

        /// <summary>
        /// Resets confusion state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            isConfused = confusionEnabled;
        }

        /// <summary>
        /// Handles detour logic at waypoints using world-space navigation.
        /// LanternDrunks have HIGH confusion chance from config.
        /// </summary>
        protected override void HandleDetourAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Check if state has changed since last waypoint
            if (state != previousState)
            {
                previousState = state;
                RecalculatePath();
                return;
            }

            // Confused state: high chance to take a wrong turn at nodes (intersections)
            // Only check confusion when at a node - getting lost mid-path makes no sense
            if (state == VisitorState.Confused && isConfused && confusionEnabled)
            {
                // Prevent confusion for first 10 waypoints, and only trigger at nodes
                if (waypointsTraversedSinceSpawn >= 10 && worldPath != null && worldPathIndex < worldPath.Count - 1 && IsAtNode())
                {
                    // Use archetype-specific confusion chance (HIGH for LanternDrunks)
                    float confusionChance = GetConfusionChance();
                    if (RandomManager.Value <= confusionChance)
                    {
                        // Confused at intersection! Build a detour path through at least 2 random nodes
                        if (BuildConfusionDetourPath(2))
                        {
                            // 50% chance to recover from confusion after taking the wrong turn
                            DecideRecoveryFromConfusion();
                            RefreshStateFromFlags();
                            return;
                        }
                        else
                        {
                            // Couldn't build detour, just recalculate normal path
                            RecalculatePath();
                        }
                    }
                }
            }

            // Continue along path
            if (worldPath != null && worldPathIndex < worldPath.Count)
            {
                worldPathIndex++;
                if (worldPathIndex >= worldPath.Count)
                {
                    OnPathCompleted();
                }
            }
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (debugGizmos && isConfused && Application.isPlaying)
            {
                Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f); // Yellow-orange for confused lantern drunk
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }

        #endregion
    }
}

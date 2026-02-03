using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Controls a visitor's movement through the maze.
    /// Implements confusion behavior based on archetype config (defaults to 25% chance).
    /// Uses world-space navigation for all pathfinding.
    /// </summary>
    public class VisitorController : VisitorControllerBase
    {
        #region Static Registry

        private static readonly HashSet<VisitorController> _activeVisitors = new HashSet<VisitorController>();

        /// <summary>Gets all active visitors in the scene</summary>
        public static IReadOnlyCollection<VisitorController> All => _activeVisitors;

        #endregion

        #region Confusion-Specific Fields

        [Header("Confusion Settings")]
        [SerializeField]
        [Tooltip("Whether confusion is enabled")]
        private bool _confusionEnabled = true;

        [SerializeField]
        [Tooltip("Draw debug info in the scene view")]
        private bool debugGizmos;

        #endregion

        // Properties inherited from base class: State, MoveSpeed, SpeedMultiplier, IsFascinated

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

        #region Detour Behavior

        /// <summary>
        /// Resets confusion state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            isConfused = confusionEnabled;
        }

        /// <summary>
        /// Handles detour logic at waypoints using world-space navigation.
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

            // Confused state: chance to take a wrong turn at nodes (intersections)
            // Only check confusion when at a node - getting lost mid-path makes no sense
            if (state == VisitorState.Confused && isConfused && confusionEnabled)
            {
                // Prevent confusion for first 10 waypoints, and only trigger at nodes
                bool atNode = IsAtNode();
                bool pastMinWaypoints = waypointsTraversedSinceSpawn >= 10;
                bool hasPathRemaining = worldPath != null && worldPathIndex < worldPath.Count - 1;

                if (pastMinWaypoints && hasPathRemaining && atNode)
                {
                    float confusionChance = GetConfusionChance();
                    float roll = RandomManager.Value;
                    bool triggered = roll <= confusionChance;

                    if (triggered)
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
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for confused
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }

        #endregion
    }
}

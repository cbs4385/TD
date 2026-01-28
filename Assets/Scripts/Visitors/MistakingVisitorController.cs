using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// A visitor variant that takes intentional missteps at branching points.
    /// Uses archetype config for misstep chance (defaults to 25%).
    /// Once on a mistaken path, they continue until reaching another branching point,
    /// where they recalculate to the destination.
    ///
    /// Uses world-space navigation for all pathfinding.
    /// </summary>
    public class MistakingVisitorController : VisitorControllerBase
    {
        #region Misstep-Specific Fields

        [Header("Misstep Settings")]
        [SerializeField]
        [Tooltip("Enable misstep behavior")]
        private bool misstepEnabled = true;

        [SerializeField]
        [Tooltip("Draw misstep paths in scene view for debugging")]
        private bool debugMisstepGizmos;

        // Misstep tracking - using world positions
        private bool isOnMisstepPath;
        private HashSet<Vector3> walkedPositions;
        private int misstepSegmentStartIndex;

        #endregion

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
            walkedPositions = new HashSet<Vector3>();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;
        }

        #endregion

        #region Detour Behavior Implementation

        /// <summary>
        /// Resets misstep state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            walkedPositions.Clear();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;
        }

        /// <summary>
        /// Handles misstep decision at waypoint.
        /// Uses world-space navigation.
        /// </summary>
        protected override void HandleDetourAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Check if state has changed since last waypoint
            if (state != previousState)
            {
                if (debugSplineRotation)
                {
                    Debug.Log($"[SplineDebug] {name} HandleDetourAtWaypoint - STATE CHANGED from {previousState} to {state}, recalculating path");
                }
                previousState = state;
                RecalculatePath();
                return;
            }

            // If on misstep path and at a branch point, exit misstep and recalculate
            if (isOnMisstepPath)
            {
                if (debugSplineRotation)
                {
                    Debug.Log($"[SplineDebug] {name} HandleDetourAtWaypoint - EXIT MISSTEP, recalculating path");
                }
                // End misstep - recalculate to destination
                isOnMisstepPath = false;
                RecalculatePath();
                return;
            }

            // Check for misstep
            if (misstepEnabled && worldPath != null && worldPathIndex < worldPath.Count)
            {
                // Track current position
                walkedPositions.Add(transform.position);

                // Use archetype-specific misstep chance (or default 0.25)
                float misstepChance = GetConfusionChance();
                float roll = Random.value;

                if (roll <= misstepChance)
                {
                    if (debugSplineRotation)
                    {
                        Debug.Log($"[SplineDebug] {name} HandleDetourAtWaypoint - MISSTEP triggered, recalculating path");
                    }
                    // Misstep triggered!
                    isOnMisstepPath = true;
                    misstepSegmentStartIndex = worldPathIndex;

                    // Recalculate path - gives a fresh path which may differ
                    RecalculatePath();
                    return;
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

            // Draw misstep segment in red using world path
            if (debugMisstepGizmos && worldPath != null && worldPath.Count > 0 && isOnMisstepPath && misstepSegmentStartIndex >= 0)
            {
                Gizmos.color = Color.red;

                int endIndex = Mathf.Min(worldPath.Count - 1, misstepSegmentStartIndex + 10);
                for (int i = misstepSegmentStartIndex; i < endIndex; i++)
                {
                    Gizmos.DrawLine(worldPath[i], worldPath[i + 1]);
                }
            }
        }

        #endregion
    }
}

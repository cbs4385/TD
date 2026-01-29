using System.Collections.Generic;
using FaeMaze.Systems;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Wary Wayfarer archetype - cautious and resistant to distraction,
    /// but highly prone to fight-or-flight when threatened.
    /// Repaths to nearest exit when frightened.
    /// Uses world-space navigation for all pathfinding.
    /// </summary>
    public class WaryWayfarerVisitorController : VisitorControllerBase
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

        #region Detour Behavior - World-Space Navigation

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
        /// Handles misstep decision at waypoint using archetype-specific chance.
        /// Wary Wayfarers have LOW misstep chance from config.
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

            // In world-space mode, misstep behavior is simplified
            // Use archetype-specific misstep chance (very low for Wary Wayfarers)
            if (misstepEnabled && !isOnMisstepPath)
            {
                float misstepChance = GetConfusionChance();
                bool shouldMisstep = RandomManager.Value <= misstepChance;

                if (shouldMisstep && worldPath != null && worldPathIndex < worldPath.Count)
                {
                    // Mark current position as walked
                    walkedPositions.Add(transform.position);
                    isOnMisstepPath = true;
                    misstepSegmentStartIndex = worldPathIndex;

                    // Recalculate path
                    RecalculatePath();
                    return;
                }
            }

            // If on misstep path and at a branch point, exit misstep
            if (isOnMisstepPath)
            {
                isOnMisstepPath = false;
                RecalculatePath();
                return;
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

        #region Frightened Override - Prefer Exits

        /// <summary>
        /// Wary Wayfarers repath to nearest exit when frightened.
        /// Uses world-space coordinates.
        /// </summary>
        protected override Vector3 GetDestinationForCurrentState()
        {
            // If frightened and config says to prefer exit, find nearest exit
            if (state == VisitorState.Frightened && ShouldFrightenedPreferExit())
            {
                Vector3 nearestExit = FindNearestExitWorldSpace();
                if (nearestExit != Vector3.zero)
                {
                    return nearestExit;
                }
            }

            return base.GetDestinationForCurrentState();
        }

        /// <summary>
        /// Finds the nearest exit spawn point from current position in world space.
        /// Returns Vector3.zero if no exits found.
        /// </summary>
        private Vector3 FindNearestExitWorldSpace()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return Vector3.zero;

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            if (mazeData.SpawnPointCount == 0)
                return Vector3.zero;

            Vector3 nearestExit = Vector3.zero;
            float shortestDist = float.MaxValue;
            Vector3 currentPos = transform.position;

            // Find nearest spawn point (portals/exits) - positions queried real-time from transforms
            foreach (var kvp in mazeData.GetSpawnPointPositions())
            {
                Vector3 spawnWorldPos = kvp.Value;

                // Skip if this is too close to our original destination
                float distToOriginal = Vector3.Distance(spawnWorldPos, worldDestination);
                if (distToOriginal < 1f)
                    continue;

                float dist = Vector3.Distance(currentPos, spawnWorldPos);
                if (dist < shortestDist)
                {
                    shortestDist = dist;
                    nearestExit = spawnWorldPos;
                }
            }

            return nearestExit;
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Draw misstep path in world space
            if (debugMisstepGizmos && worldPath != null && worldPath.Count > 0 && isOnMisstepPath && misstepSegmentStartIndex >= 0)
            {
                Gizmos.color = Color.yellow;

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

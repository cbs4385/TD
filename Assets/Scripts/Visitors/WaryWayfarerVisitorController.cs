using System.Collections.Generic;
using FaeMaze.Systems;
using FaeMaze.Props;
using FaeMaze.Maze;
using FaeMaze.HeartPowers;
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

        #region Hazard Awareness Fields

        [Header("Hazard Awareness")]
        [SerializeField]
        [Tooltip("Chance to detect and avoid hazards (0-1)")]
        private float hazardDetectionChance = 0.25f;

        [SerializeField]
        [Tooltip("Cost penalty applied to tiles near detected hazards")]
        private float hazardAvoidancePenalty = 20f;

        // Cached hazard zones for current path calculation
        private List<(Vector2 position, float radius)> detectedHazardZones;

        #endregion

        // Properties inherited from base class: State, MoveSpeed, SpeedMultiplier, IsFascinated

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            walkedPositions = new HashSet<Vector3>();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;
            detectedHazardZones = new List<(Vector2, float)>();
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

        #region Hazard Avoidance

        /// <summary>
        /// Override BuildWorldPath to apply hazard avoidance costs when hazards are detected.
        /// Rolls a 25% chance per path calculation to detect hazards.
        /// </summary>
        public override List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return new List<Vector3>();

            // Roll 25% chance to detect hazards for this path calculation
            System.Func<Vector2, float> hazardCost = null;
            if (TryDetectHazards())
            {
                hazardCost = GetHazardCostForPosition;
            }

            return ForestMaze.MazePathfinding.BuildWorldPath(
                mazeGridBehaviour.WorldSpaceMazeData,
                start, end,
                heartNodePenalty: 0f,
                penalizeHeartNode: false,
                hazardCostFunction: hazardCost
            );
        }

        /// <summary>
        /// Rolls 25% chance to detect hazards and collects their positions.
        /// Called each time a path is calculated.
        /// </summary>
        private bool TryDetectHazards()
        {
            // 25% chance to detect hazards this path calculation
            if (RandomManager.Value > hazardDetectionChance)
            {
                return false;
            }

            detectedHazardZones.Clear();

            // PukaHazard (drowning pools) - static registry
            foreach (var puka in PukaHazard.All)
            {
                Vector2 pos = new Vector2(puka.transform.position.x, puka.transform.position.y);
                detectedHazardZones.Add((pos, 1.5f)); // detectionRadius default
            }

            // FaeLantern (fascination zones) - static registry
            foreach (var lantern in FaeLantern.All)
            {
                Vector2 pos = new Vector2(lantern.transform.position.x, lantern.transform.position.y);
                detectedHazardZones.Add((pos, lantern.InfluenceRadius));
            }

            // FairyRing (circling zones) - no static registry, use FindObjectsByType
            var fairyRings = FindObjectsByType<FairyRing>(FindObjectsSortMode.None);
            foreach (var ring in fairyRings)
            {
                Vector2 pos = new Vector2(ring.transform.position.x, ring.transform.position.y);
                // Get radius from collider (SphereCollider typical)
                float radius = 3.0f; // default
                var sphereCol = ring.GetComponent<SphereCollider>();
                if (sphereCol != null)
                {
                    radius = sphereCol.radius * ring.transform.localScale.x;
                }
                detectedHazardZones.Add((pos, radius));
            }

            // HeartOfTheMaze - find single instance
            var heart = FindFirstObjectByType<HeartOfTheMaze>();
            if (heart != null)
            {
                Vector2 pos = new Vector2(heart.transform.position.x, heart.transform.position.y);
                detectedHazardZones.Add((pos, GameSettings.HeartDetectionRadius));
            }

            // Active HeartwardGrasp zones - query HeartPowerManager
            var hpm = HeartPowerManager.Instance;
            if (hpm != null)
            {
                var graspPositions = hpm.GetActiveHeartwardGraspPositions();
                foreach (var graspPos in graspPositions)
                {
                    Vector2 pos = new Vector2(graspPos.x, graspPos.y);
                    detectedHazardZones.Add((pos, GameSettings.HeartwardGraspRadius));
                }

                // Active DevouringMaw zones
                var mawPositions = hpm.GetActiveDevouringMawPositions();
                foreach (var mawPos in mawPositions)
                {
                    Vector2 pos = new Vector2(mawPos.x, mawPos.y);
                    detectedHazardZones.Add((pos, GameSettings.DevouringMawRadius));
                }
            }

            return detectedHazardZones.Count > 0;
        }

        /// <summary>
        /// Calculates additional pathfinding cost for a tile based on proximity to detected hazards.
        /// Uses quadratic falloff - higher cost closer to hazard center.
        /// </summary>
        private float GetHazardCostForPosition(Vector2 tilePos)
        {
            float totalCost = 0f;

            foreach (var (hazardPos, radius) in detectedHazardZones)
            {
                float dist = Vector2.Distance(tilePos, hazardPos);

                // Apply cost if within hazard zone
                if (dist < radius)
                {
                    // Higher cost closer to center (quadratic falloff)
                    float normalizedDist = dist / radius;
                    float costMultiplier = (1f - normalizedDist) * (1f - normalizedDist);
                    totalCost += hazardAvoidancePenalty * costMultiplier;
                }
            }

            return totalCost;
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

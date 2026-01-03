using System.Collections.Generic;
using FaeMaze.Systems;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Wary Wayfarer archetype - cautious and resistant to distraction,
    /// but highly prone to fight-or-flight when threatened.
    /// Repaths to nearest exit when frightened.
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

        // Misstep tracking
        private bool isOnMisstepPath;
        private HashSet<Vector2Int> walkedTiles;
        private int misstepSegmentStartIndex;

        #endregion

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
            walkedTiles = new HashSet<Vector2Int>();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;
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
            else
            {
                state = VisitorState.Walking;
            }
        }

        #endregion

        #region Detour Behavior - Misstep with Archetype Awareness

        /// <summary>
        /// Resets misstep state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            walkedTiles.Clear();
            isOnMisstepPath = false;
            misstepSegmentStartIndex = -1;
            lostSegmentActive = false;
            lostSegmentEndIndex = 0;
        }

        /// <summary>
        /// Handles misstep decision at waypoint using archetype-specific chance.
        /// Wary Wayfarers have LOW misstep chance from config.
        /// </summary>
        protected override void HandleDetourAtWaypoint()
        {
            if (!misstepEnabled || mazeGridBehaviour == null || gameController == null)
            {
                RecalculatePath();
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            // Track as walked for misstep system
            walkedTiles.Add(currentPos);

            // Get all unwalked adjacent tiles
            List<Vector2Int> unwalkedNeighbors = GetUnwalkedNeighbors(currentPos);

            Debug.Log($"[WaryWayfarer:{gameObject.name}] At waypoint {currentPos}, unwalked neighbors: {unwalkedNeighbors.Count}, isOnMisstep: {isOnMisstepPath}, pathIndex: {currentPathIndex}/{path.Count}");

            // Check for dead end (no unwalked neighbors) - always recalculate
            if (unwalkedNeighbors.Count == 0)
            {
                Debug.Log($"[WaryWayfarer:{gameObject.name}] Dead end detected at {currentPos}, recalculating path");
                isOnMisstepPath = false;
                RecalculatePath();
                return;
            }

            // If on misstep path and reached a branch, recalculate to destination
            if (isOnMisstepPath)
            {
                if (unwalkedNeighbors.Count >= 2)
                {
                    Debug.Log($"[WaryWayfarer:{gameObject.name}] Branch detected while on misstep at {currentPos}, exiting misstep path");
                    // Reached a new branch - exit misstep path
                    isOnMisstepPath = false;
                    RecalculatePath();
                }
                else
                {
                    // Continue on misstep path (corridor)
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        Debug.Log($"[WaryWayfarer:{gameObject.name}] Reached end of misstep path unexpectedly, recalculating");
                        // Reached end of misstep path unexpectedly - recalculate
                        isOnMisstepPath = false;
                        RecalculatePath();
                    }
                }
                return;
            }

            // Not a branch if only 1 unwalked neighbor
            if (unwalkedNeighbors.Count == 1)
            {
                // Not on misstep and not a branch - normal recalculate
                RecalculatePath();
                return;
            }

            // Get optimal next step via A*
            Vector2Int? optimalNext = GetOptimalNextStep(currentPos);
            if (!optimalNext.HasValue)
            {
                RecalculatePath();
                return;
            }

            // Use archetype-specific confusion/misstep chance (very low for Wary Wayfarers)
            float misstepChance = GetConfusionChance();
            bool shouldMisstep = Random.value <= misstepChance;

            if (!shouldMisstep)
            {
                // Take optimal path
                return;
            }

            // Take a misstep - choose a non-optimal branch
            List<Vector2Int> misstepCandidates = new List<Vector2Int>(branchNeighbors);
            misstepCandidates.Remove(optimalNext.Value);

            if (misstepCandidates.Count == 0)
            {
                // No wrong choices available
                return;
            }

            Vector2Int chosenMisstep = misstepCandidates[Random.Range(0, misstepCandidates.Count)];

            // Build misstep path
            List<Vector2Int> misstepPath = BuildMisstepPath(currentPos, chosenMisstep);

            if (misstepPath.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Replace current path with misstep path
            path = misstepPath;
            currentPathIndex = 0;
            isOnMisstepPath = true;
            misstepSegmentStartIndex = 0;
        }

        /// <summary>
        /// Gets the optimal next step from current position using A*.
        /// Returns null if path cannot be calculated.
        /// </summary>
        private Vector2Int? GetOptimalNextStep(Vector2Int currentPos)
        {
            List<MazeGrid.MazeNode> optimalPath = new List<MazeGrid.MazeNode>();
            // Use state-based attraction multiplier for optimal path calculation
            float attractionMultiplier = GetAttractionMultiplier();
            if (gameController.TryFindPath(currentPos, originalDestination, optimalPath, attractionMultiplier) && optimalPath.Count > 1)
            {
                return new Vector2Int(optimalPath[1].x, optimalPath[1].y);
            }
            return null;
        }

        /// <summary>
        /// Builds path following mistaken direction until reaching next branch.
        /// </summary>
        private List<Vector2Int> BuildMisstepPath(Vector2Int startPos, Vector2Int firstStep)
        {
            List<Vector2Int> misstepPath = new List<Vector2Int>();
            HashSet<Vector2Int> misstepPathSet = new HashSet<Vector2Int>();

            Vector2Int previousPos = startPos;
            Vector2Int currentPos = firstStep;
            Vector2Int currentDirection = firstStep - startPos;

            misstepPath.Add(startPos);
            misstepPath.Add(firstStep);
            misstepPathSet.Add(startPos);
            misstepPathSet.Add(firstStep);

            int maxIterations = 100;
            int iterations = 0;

            while (iterations < maxIterations)
            {
                List<Vector2Int> unwalkedNeighbors = GetUnwalkedNeighbors(currentPos, misstepPathSet);

                // Reached a dead end or another branch
                if (unwalkedNeighbors.Count == 0 || unwalkedNeighbors.Count >= 2)
                {
                    break;
                }

                // Continue forward (only one unwalked neighbor = corridor)
                Vector2Int nextPos = unwalkedNeighbors[0];
                misstepPath.Add(nextPos);
                misstepPathSet.Add(nextPos);

                currentDirection = nextPos - currentPos;
                previousPos = currentPos;
                currentPos = nextPos;
                iterations++;
            }

            return misstepPath;
        }

        /// <summary>
        /// Gets unwalked neighbors excluding those in the given set.
        /// </summary>
        private List<Vector2Int> GetUnwalkedNeighbors(Vector2Int position, HashSet<Vector2Int> excludeSet = null)
        {
            List<Vector2Int> neighbors = GetWalkableNeighbors(position);
            List<Vector2Int> unwalked = new List<Vector2Int>();

            foreach (var neighbor in neighbors)
            {
                if (!walkedTiles.Contains(neighbor))
                {
                    if (excludeSet == null || !excludeSet.Contains(neighbor))
                    {
                        unwalked.Add(neighbor);
                    }
                }
            }

            return unwalked;
        }

        #endregion

        #region Frightened Override - Prefer Exits

        /// <summary>
        /// Wary Wayfarers repath to nearest exit when frightened.
        /// </summary>
        protected override Vector2Int GetDestinationForCurrentState(Vector2Int currentPos)
        {
            // If frightened and config says to prefer exit, find nearest exit
            if (state == VisitorState.Frightened && ShouldFrightenedPreferExit())
            {
                return FindNearestExit(currentPos);
            }

            // Otherwise use base behavior
            return base.GetDestinationForCurrentState(currentPos);
        }

        /// <summary>
        /// Finds the nearest exit spawn point from current position.
        /// Returns originalDestination if no exits found.
        /// </summary>
        private Vector2Int FindNearestExit(Vector2Int currentPos)
        {
            // Get all spawn points from maze grid
            if (mazeGridBehaviour == null)
                return originalDestination;

            var allSpawns = mazeGridBehaviour.GetAllSpawnPoints();
            if (allSpawns == null || allSpawns.Count < 2)
                return originalDestination; // Need at least entrance and one exit

            Vector2Int nearestExit = originalDestination;
            float shortestDist = float.MaxValue;

            // Find nearest spawn point that isn't the original destination (entrance)
            foreach (var spawn in allSpawns.Values)
            {
                // Skip the entrance (original destination)
                if (spawn == originalDestination)
                    continue;

                // Calculate Manhattan distance
                float dist = Mathf.Abs(spawn.x - currentPos.x) + Mathf.Abs(spawn.y - currentPos.y);
                if (dist < shortestDist)
                {
                    shortestDist = dist;
                    nearestExit = spawn;
                }
            }

            return nearestExit;
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                if (debugMisstepGizmos && isOnMisstepPath && misstepSegmentStartIndex >= 0)
                {
                    Gizmos.color = Color.yellow;

                    for (int i = misstepSegmentStartIndex; i < path.Count - 1; i++)
                    {
                        Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                        Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                        Gizmos.DrawLine(start, end);
                    }
                }
            }
        }

        #endregion
    }
}

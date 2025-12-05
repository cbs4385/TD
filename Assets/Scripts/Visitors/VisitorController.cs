using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Controls a visitor's movement through the maze.
    /// Implements confusion behavior: 25% chance at intersections to take a wrong turn for 15-20 tiles.
    /// When using spawn markers: visitors escape at the destination (no essence).
    /// When using legacy heart: visitors are consumed at the heart (awards essence).
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
        [Tooltip("Chance (0-1) for visitor to get confused at intersections")]
        [Range(0f, 1f)]
        private float confusionChance = 0.25f;

        [SerializeField]
        [Tooltip("Whether confusion is enabled")]
        private bool confusionEnabled = true;

        [SerializeField]
        [Tooltip("Minimum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int minConfusionDistance = 15;

        [SerializeField]
        [Tooltip("Maximum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int maxConfusionDistance = 20;

        [SerializeField]
        [Tooltip("Draw confusion segments in the scene view for debugging")]
        private bool debugConfusionGizmos;

        private bool isConfused;
        private bool confusionSegmentActive;
        private int confusionSegmentEndIndex;
        private int confusionStepsTarget;
        private int confusionStepsTaken;

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

        private void OnEnable()
        {
            _activeVisitors.Add(this);
        }

        private void OnDisable()
        {
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

            if (isFascinated)
            {
                state = VisitorState.Fascinated;
            }
            else if (confusionSegmentActive)
            {
                state = VisitorState.Confused;
            }
            else if (state == VisitorState.Frightened)
            {
                return;
            }
            else
            {
                state = VisitorState.Walking;
            }
        }

        #endregion

        #region Detour Behavior Implementation

        /// <summary>
        /// Resets confusion state when starting a new path or becoming fascinated.
        /// </summary>
        protected override void ResetDetourState()
        {
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;
        }

        /// <summary>
        /// Attempts to trigger confusion at the current waypoint if it's an intersection.
        /// For fascinated visitors who have reached the lantern, implements random walk behavior.
        /// </summary>
        protected override void HandleDetourAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
            {
                return;
            }

            if (!confusionEnabled || currentPathIndex >= path.Count - 1)
            {
                RecalculatePath();
                return;
            }

            // Prevent confusion for the first 10 waypoints after spawning
            if (waypointsTraversedSinceSpawn < 10)
            {
                RecalculatePath();
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            if (confusionSegmentActive)
            {
                // End of segment reached? Allow recovery logic and new decisions afterward.
                if (currentPathIndex > confusionSegmentEndIndex)
                {
                    confusionSegmentActive = false;
                    DecideRecoveryFromConfusion();
                    currentPathIndex++; // Move to next waypoint in recovery path
                    RefreshStateFromFlags();
                    return;
                }
                else
                {
                    currentPathIndex++; // Continue through confusion segment
                    return; // Still traversing a confusion segment; no new detours.
                }
            }

            if (!isConfused)
            {
                RecalculatePath();
                return; // Currently navigating normally.
            }

            Vector2Int nextPos = path[currentPathIndex + 1];

            // Check if current position is an intersection (2+ walkable neighbors excluding the tile we arrived from)
            List<Vector2Int> walkableNeighbors = GetWalkableNeighbors(currentPos);

            // Exclude the tile we just came from to find forward options
            if (currentPathIndex > 0)
            {
                walkableNeighbors.Remove(path[currentPathIndex - 1]);
            }

            if (walkableNeighbors.Count < 2)
            {
                RecalculatePath();
                return; // Not an intersection
            }

            // Roll for confusion
            float roll = Random.value;
            if (roll > confusionChance)
            {
                RecalculatePath();
                return; // No confusion this time
            }

            // Get confused - pick a non-forward direction
            List<Vector2Int> detourDirections = new List<Vector2Int>();
            foreach (var neighbor in walkableNeighbors)
            {
                if (neighbor != nextPos) // Don't pick the intended next waypoint
                {
                    detourDirections.Add(neighbor);
                }
            }

            if (detourDirections.Count == 0)
            {
                RecalculatePath();
                return; // No detour options
            }

            // Pick random detour direction
            Vector2Int detourStart = detourDirections[Random.Range(0, detourDirections.Count)];

            BeginConfusionSegment(currentPos, detourStart);
        }

        #endregion

        #region Confusion System

        /// <summary>
        /// Gets all tiles that have been traversed so far (from spawn to current position).
        /// Used to prevent backtracking when building confusion paths.
        /// </summary>
        private HashSet<Vector2Int> GetTraversedTiles()
        {
            HashSet<Vector2Int> traversed = new HashSet<Vector2Int>();

            if (path == null || path.Count == 0)
            {
                return traversed;
            }

            // Add all tiles from start up to and including current index
            for (int i = 0; i <= currentPathIndex && i < path.Count; i++)
            {
                traversed.Add(path[i]);
            }

            return traversed;
        }

        private void BeginConfusionSegment(Vector2Int currentPos, Vector2Int detourStart)
        {
            int stepsTarget = Mathf.Clamp(Random.Range(minConfusionDistance, maxConfusionDistance + 1), minConfusionDistance, maxConfusionDistance);

            // Use recently reached tiles (last 10) to prevent short-term backtracking
            HashSet<Vector2Int> traversedTiles = new HashSet<Vector2Int>(recentlyReachedTiles ?? new Queue<Vector2Int>());

            List<Vector2Int> confusionPath = BuildConfusionPath(currentPos, detourStart, stepsTarget, traversedTiles);

            if (confusionPath.Count == 0)
            {
                RecalculatePath();
                return;
            }

            Vector2Int confusionEnd = confusionPath[confusionPath.Count - 1];
            List<MazeGrid.MazeNode> recoveryPathNodes = new List<MazeGrid.MazeNode>();

            if (!gameController.TryFindPath(confusionEnd, originalDestination, recoveryPathNodes) || recoveryPathNodes.Count == 0)
            {
                RecalculatePath();
                return;
            }

            List<Vector2Int> recoveryPath = new List<Vector2Int>();
            foreach (var node in recoveryPathNodes)
            {
                recoveryPath.Add(new Vector2Int(node.x, node.y));
            }

            // Build the combined path: current position + confusion detour + recovery path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);

            // Add all confusion path tiles
            newPath.AddRange(confusionPath);

            // Create a set of all tiles in the new path so far to avoid duplicates
            HashSet<Vector2Int> tilesInNewPath = new HashSet<Vector2Int>(newPath);

            // Add recovery path tiles, but skip any that would cause backtracking
            int recoveryStartIndex = (recoveryPath.Count > 0 && recoveryPath[0] == confusionEnd) ? 1 : 0;

            for (int i = recoveryStartIndex; i < recoveryPath.Count; i++)
            {
                Vector2Int recoveryTile = recoveryPath[i];

                // Skip tiles that were already traversed before confusion OR are duplicates in the new path
                if (traversedTiles.Contains(recoveryTile) || tilesInNewPath.Contains(recoveryTile))
                {
                    continue; // Skip this tile to prevent backtracking
                }

                newPath.Add(recoveryTile);
                tilesInNewPath.Add(recoveryTile);
            }

            path = newPath;
            currentPathIndex = 1; // Start at index 1 since index 0 is current position

            confusionSegmentActive = true;
            confusionSegmentEndIndex = confusionPath.Count;
            confusionStepsTarget = stepsTarget;
            confusionStepsTaken = 0;
            isConfused = true;

            Debug.Log($"[VisitorPath] {name} began confusion detour. Path length: {path.Count}. currentPathIndex: {currentPathIndex}. Target waypoint: {path[currentPathIndex]}. Confusion end index: {confusionSegmentEndIndex}.", this);

            RefreshStateFromFlags();
        }

        private List<Vector2Int> BuildConfusionPath(Vector2Int currentPos, Vector2Int detourStart, int stepsTarget, HashSet<Vector2Int> traversedTiles)
        {
            List<Vector2Int> confusionPath = new List<Vector2Int>();

            Vector2Int previousPos = currentPos;
            Vector2Int nextPos = detourStart;
            Vector2Int forwardDir = detourStart - currentPos;

            int safetyLimit = 250;
            int iterations = 0;

            // Track tiles in the confusion path to avoid loops within the detour
            HashSet<Vector2Int> confusionPathSet = new HashSet<Vector2Int>();

            while (iterations < safetyLimit && confusionPath.Count < stepsTarget)
            {
                if (!IsWalkable(nextPos))
                {
                    break;
                }

                // Check if this tile was already traversed before confusion started
                if (traversedTiles.Contains(nextPos))
                {
                    break; // Would backtrack to an earlier position
                }

                // Check if we're creating a loop within this confusion path
                if (confusionPathSet.Contains(nextPos))
                {
                    break;
                }

                confusionPath.Add(nextPos);
                confusionPathSet.Add(nextPos);
                confusionStepsTaken = confusionPath.Count;

                if (IsDeadEndVisible(nextPos, forwardDir))
                {
                    break;
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(nextPos);
                neighbors.Remove(previousPos); // Don't go immediately backward

                // Remove any neighbors that would cause backtracking to already-traversed tiles
                neighbors.RemoveAll(n => traversedTiles.Contains(n));

                // Also avoid creating loops within the confusion path itself
                neighbors.RemoveAll(n => confusionPathSet.Contains(n));

                if (neighbors.Count == 0)
                {
                    break; // Cannot continue without backtracking
                }

                Vector2Int preferredForward = nextPos + forwardDir;
                Vector2Int chosenNext = neighbors.Contains(preferredForward) ? preferredForward : neighbors[Random.Range(0, neighbors.Count)];

                forwardDir = chosenNext - nextPos;
                previousPos = nextPos;
                nextPos = chosenNext;
                iterations++;
            }

            return confusionPath;
        }

        private bool IsWalkable(Vector2Int position)
        {
            var node = mazeGridBehaviour.Grid?.GetNode(position.x, position.y);
            return node != null && node.walkable;
        }

        private bool IsDeadEndVisible(Vector2Int startPos, Vector2Int forwardDir)
        {
            if (forwardDir == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int previous = startPos;
            Vector2Int current = startPos + forwardDir;

            while (true)
            {
                var node = mazeGridBehaviour.Grid?.GetNode(current.x, current.y);
                if (node == null || !node.walkable)
                {
                    return false; // Wall encountered before a dead end
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(current);
                neighbors.Remove(previous);

                if (neighbors.Count == 0)
                {
                    return true; // Only way out is back where we came from
                }

                if (neighbors.Count > 1)
                {
                    return false; // Branch or corner blocks line of sight
                }

                Vector2Int nextForward = neighbors[0] - current;
                if (nextForward != forwardDir)
                {
                    return false; // Would require turning a corner
                }

                previous = current;
                current += forwardDir;
            }
        }

        private void DecideRecoveryFromConfusion()
        {
            float roll = Random.value;
            bool recover = roll <= 0.5f;
            isConfused = !recover;
        }

        #endregion

        #region Consumption Override

        /// <summary>
        /// Handles visitor consumption by the heart.
        /// Notifies the Heart to award essence.
        /// </summary>
        protected override void HandleConsumption()
        {
            if (gameController != null && gameController.Heart != null)
            {
                gameController.Heart.OnVisitorConsumed(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Draw confusion segment
            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                if (debugConfusionGizmos && confusionSegmentEndIndex > 0)
                {
                    Gizmos.color = Color.magenta;
                    int lastConfusionIndex = Mathf.Min(confusionSegmentEndIndex, path.Count - 1);

                    for (int i = 0; i < lastConfusionIndex; i++)
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

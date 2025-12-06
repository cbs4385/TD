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
            lostSegmentActive = false;
            lostSegmentEndIndex = 0;
        }

        /// <summary>
        /// Determines whether a detour should be attempted based on confusion state.
        /// </summary>
        protected override bool ShouldAttemptDetour(Vector2Int currentPos)
        {
            // Check if we're in an active confusion segment
            if (confusionSegmentActive)
            {
                // Still traversing confusion segment
                if (currentPathIndex <= confusionSegmentEndIndex)
                {
                    return false; // Don't interrupt active segment
                }

                // Segment complete - end it and allow normal routing
                confusionSegmentActive = false;
                DecideRecoveryFromConfusion();
                currentPathIndex++; // Move to next waypoint in recovery path
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

                // Check if at intersection
                if (!IsAtIntersection(currentPos))
                {
                    return false;
                }

                // Check if we're near the end of the path
                if (path == null || currentPathIndex >= path.Count - 1)
                {
                    return false;
                }

                // Roll for confusion chance
                return Random.value <= confusionChance;
            }

            return baseWantsDetour;
        }

        /// <summary>
        /// Handles confusion-specific detour logic.
        /// </summary>
        protected override void HandleStateSpecificDetour(Vector2Int currentPos)
        {
            // Handle Confused state detours
            if (state == VisitorState.Confused && isConfused && confusionEnabled)
            {
                // Get next intended position
                if (currentPathIndex + 1 >= path.Count)
                {
                    RecalculatePath();
                    return;
                }

                Vector2Int nextPos = path[currentPathIndex + 1];
                List<Vector2Int> walkableNeighbors = GetWalkableNeighbors(currentPos);

                // Exclude previous tile
                if (currentPathIndex > 0)
                {
                    walkableNeighbors.Remove(path[currentPathIndex - 1]);
                }

                // Get detour directions (non-forward directions)
                List<Vector2Int> detourDirections = new List<Vector2Int>();
                foreach (var neighbor in walkableNeighbors)
                {
                    if (neighbor != nextPos)
                    {
                        detourDirections.Add(neighbor);
                    }
                }

                if (detourDirections.Count == 0)
                {
                    RecalculatePath();
                    return;
                }

                // Pick random detour direction
                Vector2Int detourStart = detourDirections[Random.Range(0, detourDirections.Count)];
                BeginConfusionSegment(currentPos, detourStart);
                return;
            }

            // Fallback to base class implementation (handles Lost state, etc.)
            base.HandleStateSpecificDetour(currentPos);
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

            // Validate confusion path adjacency
            for (int i = 1; i < confusionPath.Count; i++)
            {
                int dist = Mathf.Abs(confusionPath[i].x - confusionPath[i - 1].x) + Mathf.Abs(confusionPath[i].y - confusionPath[i - 1].y);
                if (dist != 1)
                {
                    Debug.LogError($"[VisitorPath] {name} BuildConfusionPath created non-adjacent tiles at indices {i - 1} and {i}: {confusionPath[i - 1]} to {confusionPath[i]} (distance {dist}). Falling back to RecalculatePath.", this);
                    RecalculatePath();
                    return;
                }
            }

            Vector2Int confusionEnd = confusionPath[confusionPath.Count - 1];

            // Get recovery destination based on current state
            Vector2Int recoveryDestination = GetDestinationForCurrentState(confusionEnd);

            // Find path from confusion end to recovery destination
            if (!TryFindPathToDestination(confusionEnd, recoveryDestination, out List<Vector2Int> recoveryPath))
            {
                RecalculatePath();
                return;
            }

            // Validate recovery path adjacency
            for (int i = 1; i < recoveryPath.Count; i++)
            {
                int dist = Mathf.Abs(recoveryPath[i].x - recoveryPath[i - 1].x) + Mathf.Abs(recoveryPath[i].y - recoveryPath[i - 1].y);
                if (dist != 1)
                {
                    Debug.LogError($"[VisitorPath] {name} A* recovery path has non-adjacent tiles at indices {i - 1} and {i}: {recoveryPath[i - 1]} to {recoveryPath[i]} (distance {dist}). Falling back to RecalculatePath.", this);
                    RecalculatePath();
                    return;
                }
            }

            // Build the combined path: current position + confusion detour + recovery path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);

            // Validate currentPos is adjacent to first confusion tile
            if (confusionPath.Count > 0)
            {
                int distToFirst = Mathf.Abs(currentPos.x - confusionPath[0].x) + Mathf.Abs(currentPos.y - confusionPath[0].y);
                if (distToFirst != 1)
                {
                    Debug.LogError($"[VisitorPath] {name} currentPos {currentPos} is not adjacent to first confusion tile {confusionPath[0]} (distance {distToFirst}). Falling back to RecalculatePath.", this);
                    RecalculatePath();
                    return;
                }
            }

            // Add all confusion path tiles
            newPath.AddRange(confusionPath);

            // Validate connection between confusion end and recovery start
            if (recoveryPath.Count > 0 && recoveryPath[0] != confusionEnd)
            {
                int distToRecovery = Mathf.Abs(confusionEnd.x - recoveryPath[0].x) + Mathf.Abs(confusionEnd.y - recoveryPath[0].y);
                Debug.LogError($"[VisitorPath] {name} confusionEnd {confusionEnd} does not match recoveryPath[0] {recoveryPath[0]} (distance {distToRecovery}). Falling back to RecalculatePath.", this);
                RecalculatePath();
                return;
            }

            // Add recovery path tiles, skipping the first one since it duplicates confusionEnd
            for (int i = 1; i < recoveryPath.Count; i++)
            {
                newPath.Add(recoveryPath[i]);
            }

            // Final validation of complete path
            for (int i = 1; i < newPath.Count; i++)
            {
                int dist = Mathf.Abs(newPath[i].x - newPath[i - 1].x) + Mathf.Abs(newPath[i].y - newPath[i - 1].y);
                if (dist != 1)
                {
                    Debug.LogError($"[VisitorPath] {name} final combined path has non-adjacent tiles at indices {i - 1} and {i}: {newPath[i - 1]} to {newPath[i]} (distance {dist}). Falling back to RecalculatePath.", this);
                    RecalculatePath();
                    return;
                }
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

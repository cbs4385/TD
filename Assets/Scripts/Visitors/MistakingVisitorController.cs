using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// A visitor variant that takes intentional missteps at branching points.
    /// At any tile with multiple unwalked adjacent tiles, there's a 20% chance
    /// the visitor will choose an incorrect direction. Once on a mistaken path,
    /// they continue until reaching another branching point, where they recalculate
    /// an A* path to the destination and apply the misstep chance again.
    ///
    /// Functionally identical to VisitorController except for the misstep behavior.
    /// All other behaviors (animations, speed, fascination states, etc.) are preserved.
    /// </summary>
    public class MistakingVisitorController : VisitorControllerBase
    {
        #region Misstep-Specific Fields

        [Header("Misstep Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) for visitor to take a wrong turn at branches")]
        [Range(0f, 1f)]
        private float misstepChance = 0.2f;

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

        #region Detour Behavior Implementation

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
        /// Handles misstep decision at waypoint.
        /// Detects branching points (tiles with 2+ unwalked neighbors) and rolls for misstep.
        /// If on misstep path and reached branch, recalculates A* to destination.
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

            // Not a branch if fewer than 2 unwalked neighbors
            if (unwalkedNeighbors.Count < 2)
            {
                // If on misstep, continue until branch
                if (isOnMisstepPath)
                {
                    // Just advance to next waypoint
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        // Reached end of misstep path unexpectedly - recalculate
                        isOnMisstepPath = false;
                        RecalculatePath();
                    }
                    return;
                }

                // Not on misstep and not a branch - normal recalculate
                RecalculatePath();
                return;
            }

            // This is a branching point with 2+ unwalked neighbors
            if (isOnMisstepPath)
            {
                // End misstep - recalculate A* to destination
                isOnMisstepPath = false;
                RecalculatePath();
                // Don't return - check for new misstep below
            }

            // After recalculating, recheck unwalked neighbors for new misstep decision
            unwalkedNeighbors = GetUnwalkedNeighbors(currentPos);
            if (unwalkedNeighbors.Count < 2)
            {
                return; // No longer a branch after recalc
            }

            // Roll for misstep
            float roll = Random.value;
            if (roll > misstepChance)
            {
                // No misstep - path already recalculated above or will be recalculated
                if (!isOnMisstepPath)
                {
                    RecalculatePath();
                }
                return;
            }

            // Misstep triggered!
            TakeMisstep(currentPos, unwalkedNeighbors);
        }

        #endregion

        #region Misstep System

        /// <summary>
        /// Gets all walkable neighbor tiles that have not been walked.
        /// Used to identify branching points.
        /// </summary>
        private List<Vector2Int> GetUnwalkedNeighbors(Vector2Int gridPos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0)   // Left
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = gridPos + dir;
                var node = mazeGridBehaviour.Grid.GetNode(neighborPos.x, neighborPos.y);

                if (node != null && node.walkable && !walkedTiles.Contains(neighborPos))
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// Executes a misstep by choosing wrong direction at branch.
        /// Selects randomly among unwalked neighbors, biased against immediate backtracking.
        /// Builds path following chosen direction until next branch.
        /// </summary>
        private void TakeMisstep(Vector2Int currentPos, List<Vector2Int> unwalkedNeighbors)
        {
            if (unwalkedNeighbors.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Determine optimal next step from A*
            Vector2Int? optimalNext = GetOptimalNextStep(currentPos);

            // Filter out optimal direction to get "wrong" choices
            List<Vector2Int> wrongChoices = new List<Vector2Int>(unwalkedNeighbors);
            if (optimalNext.HasValue)
            {
                wrongChoices.Remove(optimalNext.Value);
            }

            if (wrongChoices.Count == 0)
            {
                wrongChoices = new List<Vector2Int>(unwalkedNeighbors);
            }

            // Bias against backtracking to immediately previous tile
            Vector2Int? previousTile = null;
            if (currentPathIndex > 0 && currentPathIndex < path.Count)
            {
                previousTile = path[currentPathIndex - 1];
            }

            if (previousTile.HasValue && wrongChoices.Count > 1)
            {
                wrongChoices.Remove(previousTile.Value);
            }

            // Choose random wrong direction
            Vector2Int chosenWrongStep = wrongChoices[Random.Range(0, wrongChoices.Count)];

            // Build path following this wrong direction
            List<Vector2Int> misstepPath = BuildMisstepPath(currentPos, chosenWrongStep);

            if (misstepPath.Count == 0)
            {
                RecalculatePath();
                return;
            }

            // Set the misstep path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);
            newPath.AddRange(misstepPath);

            path = newPath;
            currentPathIndex = 1; // Start at index 1 since index 0 is current position
            isOnMisstepPath = true;
            misstepSegmentStartIndex = 1;

            RefreshStateFromFlags();
        }

        /// <summary>
        /// Gets the optimal next step from current position using A*.
        /// Returns null if path cannot be calculated.
        /// </summary>
        private Vector2Int? GetOptimalNextStep(Vector2Int currentPos)
        {
            List<MazeGrid.MazeNode> optimalPath = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, originalDestination, optimalPath) && optimalPath.Count > 1)
            {
                return new Vector2Int(optimalPath[1].x, optimalPath[1].y);
            }
            return null;
        }

        /// <summary>
        /// Builds path following mistaken direction until reaching next branch.
        /// Continues chosen direction, avoiding backtracking and loops.
        /// Stops at tile with 2+ unwalked neighbors (branching point).
        /// </summary>
        private List<Vector2Int> BuildMisstepPath(Vector2Int startPos, Vector2Int firstStep)
        {
            List<Vector2Int> misstepPath = new List<Vector2Int>();
            HashSet<Vector2Int> misstepPathSet = new HashSet<Vector2Int>();

            Vector2Int previousPos = startPos;
            Vector2Int currentPos = firstStep;
            Vector2Int currentDirection = firstStep - startPos;

            int safetyLimit = 200;
            int iterations = 0;

            while (iterations < safetyLimit)
            {
                iterations++;

                // Validate walkable
                var node = mazeGridBehaviour.Grid?.GetNode(currentPos.x, currentPos.y);
                if (node == null || !node.walkable)
                {
                    break;
                }

                // Check if backtracking to walked tile
                if (walkedTiles.Contains(currentPos))
                {
                    misstepPath.Add(currentPos);
                    break;
                }

                // Check for loop in misstep path
                if (misstepPathSet.Contains(currentPos))
                {
                    break;
                }

                // Add to misstep path
                misstepPath.Add(currentPos);
                misstepPathSet.Add(currentPos);

                // Check if branching point
                List<Vector2Int> unwalkedNeighbors = GetUnwalkedNeighborsExcluding(currentPos, misstepPathSet);
                unwalkedNeighbors.Remove(previousPos);

                if (unwalkedNeighbors.Count >= 2)
                {
                    // Reached branch - stop
                    break;
                }

                if (unwalkedNeighbors.Count == 0)
                {
                    // Dead end
                    break;
                }

                // Continue in same direction if possible
                Vector2Int preferredNext = currentPos + currentDirection;
                Vector2Int nextPos;

                if (unwalkedNeighbors.Contains(preferredNext))
                {
                    nextPos = preferredNext;
                }
                else
                {
                    nextPos = unwalkedNeighbors[0];
                    currentDirection = nextPos - currentPos;
                }

                previousPos = currentPos;
                currentPos = nextPos;
            }

            return misstepPath;
        }

        /// <summary>
        /// Gets unwalked neighbors excluding tiles in provided set.
        /// Used during misstep path building to avoid loops.
        /// </summary>
        private List<Vector2Int> GetUnwalkedNeighborsExcluding(Vector2Int gridPos, HashSet<Vector2Int> excludeSet)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = gridPos + dir;
                var node = mazeGridBehaviour.Grid.GetNode(neighborPos.x, neighborPos.y);

                if (node != null && node.walkable &&
                    !walkedTiles.Contains(neighborPos) &&
                    !excludeSet.Contains(neighborPos))
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
        }

        #endregion

        #region Consumption Override

        /// <summary>
        /// Handles visitor consumption by the heart.
        /// Tracks stats, awards essence directly, and plays sound.
        /// </summary>
        protected override void HandleConsumption()
        {
            // Track consumption stats
            if (FaeMaze.Systems.GameStatsTracker.Instance != null)
            {
                FaeMaze.Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            // Add essence to game controller
            if (gameController != null && gameController.Heart != null)
            {
                gameController.AddEssence(gameController.Heart.EssencePerVisitor);
            }

            // Play consumption sound
            FaeMaze.Audio.SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Destroy(gameObject);
        }

        #endregion

        #region Gizmos

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            // Draw misstep segment in red
            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                if (debugMisstepGizmos && isOnMisstepPath && misstepSegmentStartIndex >= 0)
                {
                    Gizmos.color = Color.red;

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

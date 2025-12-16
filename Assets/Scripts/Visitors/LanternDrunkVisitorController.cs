using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// LanternDrunk Pilgrim archetype - highly susceptible to fascination and getting lost.
    /// Slow but easy to keep wandering due to high confusion and lantern susceptibility.
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

                // Use archetype-specific confusion chance (HIGH for LanternDrunks)
                float confusionChance = GetConfusionChance();
                return Random.value <= confusionChance;
            }

            return baseWantsDetour;
        }

        /// <summary>
        /// Handles confusion-specific detour logic.
        /// LanternDrunks use config-based detour lengths.
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

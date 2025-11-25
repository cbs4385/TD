using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Base class for props that influence visitor pathfinding through attraction.
    /// Attracts visitors by reducing movement cost of nearby tiles.
    /// </summary>
    public class MazeAttractor : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Attraction Settings")]
        [SerializeField]
        [Tooltip("Radius of attraction influence in grid units")]
        private float radius = 3f;

        [SerializeField]
        [Tooltip("Strength of attraction (higher = stronger pull)")]
        private float attractionStrength = 0.5f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw attraction radius in Scene view")]
        private bool showDebugRadius = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour gridBehaviour;
        private Vector2Int gridPosition;
        private bool isApplied = false;

        #endregion

        #region Properties

        /// <summary>Gets the attraction radius</summary>
        public float Radius => radius;

        /// <summary>Gets the attraction strength</summary>
        public float AttractionStrength => attractionStrength;

        /// <summary>Gets the grid position of this attractor</summary>
        public Vector2Int GridPosition => gridPosition;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            gridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (gridBehaviour == null)
            {
                Debug.LogError("MazeAttractor: Could not find MazeGridBehaviour in scene!");
                return;
            }

            // Determine grid position
            if (!gridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                Debug.LogWarning($"MazeAttractor: Position {transform.position} is outside grid bounds!");
                return;
            }

            gridPosition = new Vector2Int(x, y);
            Debug.Log($"MazeAttractor at world {transform.position} -> grid {gridPosition}");

            // Apply attraction
            ApplyAttraction(gridBehaviour);
        }

        private void OnEnable()
        {
            // If already initialized and grid behaviour exists with valid grid, reapply attraction
            if (isApplied && gridBehaviour != null && gridBehaviour.Grid != null)
            {
                ApplyAttraction(gridBehaviour);
            }
        }

        private void OnDisable()
        {
            // For MVP, we don't remove attraction when disabled
            // Could implement removal by storing affected nodes and calling AddAttraction with negative value
            Debug.Log($"MazeAttractor at {gridPosition} disabled (attraction remains on grid)");
        }

        #endregion

        #region Attraction Application

        /// <summary>
        /// Applies attraction to nearby tiles on the maze grid.
        /// Uses distance-based falloff for natural influence.
        /// </summary>
        /// <param name="gridBehaviour">The maze grid behaviour to apply attraction to</param>
        public void ApplyAttraction(MazeGridBehaviour gridBehaviour)
        {
            if (gridBehaviour == null || gridBehaviour.Grid == null)
            {
                Debug.LogError("MazeAttractor: Cannot apply attraction - grid is null!");
                return;
            }

            MazeGrid grid = gridBehaviour.Grid;

            // Calculate grid radius (convert world radius to grid units)
            int gridRadius = Mathf.CeilToInt(radius);

            int affectedCount = 0;
            float totalAttractionApplied = 0f;

            // Apply attraction to tiles within radius
            for (int dx = -gridRadius; dx <= gridRadius; dx++)
            {
                for (int dy = -gridRadius; dy <= gridRadius; dy++)
                {
                    int targetX = gridPosition.x + dx;
                    int targetY = gridPosition.y + dy;

                    // Check bounds
                    if (!grid.InBounds(targetX, targetY))
                        continue;

                    // Check if node is walkable
                    var node = grid.GetNode(targetX, targetY);
                    if (node == null || !node.walkable)
                        continue;

                    // Calculate distance
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Skip if outside radius
                    if (distance > radius)
                        continue;

                    // Calculate attraction with falloff
                    float falloff = Mathf.Clamp01(1f - (distance / radius));
                    float effectiveAttraction = attractionStrength * falloff;

                    // Apply attraction to grid
                    grid.AddAttraction(targetX, targetY, effectiveAttraction);

                    affectedCount++;
                    totalAttractionApplied += effectiveAttraction;
                }
            }

            isApplied = true;

            Debug.Log($"MazeAttractor at {gridPosition}: Applied attraction to {affectedCount} tiles (total: {totalAttractionApplied:F2}, avg: {(affectedCount > 0 ? totalAttractionApplied / affectedCount : 0):F3})");
        }

        /// <summary>
        /// Removes this attractor's influence from the grid.
        /// For MVP, simply clears all attraction on the grid and reapplies other attractors.
        /// </summary>
        public void RemoveAttraction(MazeGridBehaviour gridBehaviour)
        {
            if (gridBehaviour == null || gridBehaviour.Grid == null)
                return;

            // For MVP: Clear all attraction and let other attractors reapply
            // A more sophisticated system would track which attractor modified which tile
            gridBehaviour.Grid.ClearAllAttraction();

            // Find all other attractors and reapply them
            var allAttractors = FindObjectsByType<MazeAttractor>(FindObjectsSortMode.None);
            foreach (var attractor in allAttractors)
            {
                if (attractor != this && attractor.enabled && attractor.isApplied)
                {
                    attractor.ApplyAttraction(gridBehaviour);
                }
            }

            Debug.Log($"MazeAttractor at {gridPosition}: Removed attraction and reapplied {allAttractors.Length - 1} other attractors");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showDebugRadius)
                return;

            // Draw attraction radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange semi-transparent

            // Draw circle at attractor position
            DrawCircle(transform.position, radius, 32);

            // Draw filled circle for visual emphasis
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            DrawFilledCircle(transform.position, radius, 16);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugRadius)
                return;

            // Draw brighter when selected
            Gizmos.color = new Color(1f, 0.7f, 0f, 0.6f);
            DrawCircle(transform.position, radius, 32);

            // Draw grid position if available
            if (gridBehaviour != null && isApplied)
            {
                Vector3 gridWorldPos = gridBehaviour.GridToWorld(gridPosition.x, gridPosition.y);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(gridWorldPos, 0.3f);

                // Draw line from world pos to grid center
                Gizmos.DrawLine(transform.position, gridWorldPos);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        private void DrawFilledCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
                Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);

                Gizmos.DrawLine(center, point1);
                Gizmos.DrawLine(center, point2);
                Gizmos.DrawLine(point1, point2);
            }
        }

        #endregion
    }
}

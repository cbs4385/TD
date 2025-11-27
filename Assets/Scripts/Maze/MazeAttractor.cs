using System.Collections.Generic;
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

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the attractor sprite")]
        private Color spriteColor = new Color(1f, 0.8f, 0.2f, 1f); // Golden yellow

        [SerializeField]
        [Tooltip("Size of the attractor sprite")]
        private float spriteSize = 0.7f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 12;

        [Header("Visitor Interaction")]
        [SerializeField]
        [Tooltip("Slow factor applied to visitors within radius (0.5 = half speed)")]
        private float visitorSlowFactor = 0.5f;

        [SerializeField]
        [Tooltip("Enable trigger-based visitor slowing")]
        private bool enableVisitorSlowing = true;

        [Header("Fascination (FaeLantern)")]
        [SerializeField]
        [Tooltip("Enable fascination mechanic (visitors retarget to lantern then wander)")]
        private bool enableFascination = false;

        [SerializeField]
        [Tooltip("Chance (0-1) for a visitor to become fascinated when entering trigger")]
        [Range(0f, 1f)]
        private float fascinationChance = 0.8f;

        #endregion

        #region Private Fields

        private MazeGridBehaviour gridBehaviour;
        private Vector2Int gridPosition;
        private bool isApplied = false;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D triggerCollider;

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

        private void Awake()
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
        }

        private void Start()
        {
            // Create visual sprite
            CreateVisualSprite();

            // Setup trigger collider for visitor interaction (either slowing or fascination)
            if (enableVisitorSlowing || enableFascination)
            {
                SetupTriggerCollider();
            }

            // Apply attraction BEFORE path recalculation
            // (Done in Start() to ensure MazeGridBehaviour.Awake() has completed and Grid exists)
            if (gridBehaviour != null && gridBehaviour.Grid != null)
            {
                ApplyAttraction(gridBehaviour);
            }
            else
            {
                Debug.LogError("MazeAttractor: Grid not ready in Start(), cannot apply attraction!");
                return;
            }

            // Trigger path recalculation for all active visitors
            // (This happens after attraction is applied)
            RecalculateAllVisitorPaths();
        }

        private void SetupTriggerCollider()
        {
            // Add Rigidbody2D for trigger detection (required for OnTriggerEnter2D to work)
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic - attractor doesn't move
                rb.gravityScale = 0f; // No gravity for 2D top-down
            }

            // Add CircleCollider2D for trigger detection
            triggerCollider = GetComponent<CircleCollider2D>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            triggerCollider.isTrigger = true;
            triggerCollider.radius = radius;

        }

        private void CreateVisualSprite()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a simple circle sprite for the lantern
            spriteRenderer.sprite = CreateLanternSprite(32);
            spriteRenderer.color = spriteColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(spriteSize, spriteSize, 1f);
        }

        private Sprite CreateLanternSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle (simplified lantern shape)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
        }

        // OnEnable/OnDisable removed - Awake() handles initial attraction application
        // No need to reapply on enable since attraction persists on the grid

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if a visitor entered the attraction radius
            var visitor = other.GetComponent<Visitors.VisitorController>();
            if (visitor != null)
            {
                Debug.Log($"[FaeLantern] Visitor '{visitor.name}' entered trigger at {gridPosition} | fascinationEnabled={enableFascination} | alreadyFascinated={visitor.IsFascinated}");

                // Apply fascination if enabled (FaeLantern-specific behavior)
                if (enableFascination && !visitor.IsFascinated)
                {
                    // Roll for fascination
                    float roll = Random.value;
                    bool willFascinate = roll <= fascinationChance;

                    Debug.Log($"[FaeLantern] Fascination roll for '{visitor.name}': roll={roll:F3}, chance={fascinationChance:F3}, result={(willFascinate ? "FASCINATED" : "RESISTED")}");

                    if (willFascinate)
                    {
                        visitor.BecomeFascinated(gridPosition);
                    }
                }

                // Apply slow effect if enabled
                if (enableVisitorSlowing)
                {
                    visitor.SpeedMultiplier = visitorSlowFactor;
                    Debug.Log($"[FaeLantern] Applied slow effect to '{visitor.name}': speedMultiplier={visitorSlowFactor}");
                }
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!enableVisitorSlowing)
                return;

            // Check if a visitor exited the attraction radius
            var visitor = other.GetComponent<Visitors.VisitorController>();
            if (visitor != null)
            {
                // Restore normal speed
                visitor.SpeedMultiplier = 1f;
            }
        }

        #endregion

        #region Attraction Application

        /// <summary>
        /// Applies attraction to nearby tiles on the maze grid.
        /// Uses BFS/flood-fill to propagate influence only along walkable paths.
        /// Distance-based falloff ensures natural influence that respects maze structure.
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

            // Use BFS to propagate attraction through walkable tiles only
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            Dictionary<Vector2Int, float> distances = new Dictionary<Vector2Int, float>();

            // Start from lantern position
            queue.Enqueue(gridPosition);
            distances[gridPosition] = 0f;

            // Cardinal directions for BFS
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0)   // Left
            };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                float currentDistance = distances[current];

                // Apply attraction at current position
                var currentNode = grid.GetNode(current.x, current.y);
                if (currentNode != null && currentNode.walkable)
                {
                    // Calculate attraction with falloff based on path distance
                    float falloff = Mathf.Clamp01(1f - (currentDistance / radius));
                    float effectiveAttraction = attractionStrength * falloff;

                    grid.AddAttraction(current.x, current.y, effectiveAttraction);

                    affectedCount++;
                    totalAttractionApplied += effectiveAttraction;
                }

                // Explore neighbors
                foreach (var dir in directions)
                {
                    Vector2Int neighbor = current + dir;

                    // Check bounds
                    if (!grid.InBounds(neighbor.x, neighbor.y))
                        continue;

                    // Check if node is walkable
                    var neighborNode = grid.GetNode(neighbor.x, neighbor.y);
                    if (neighborNode == null || !neighborNode.walkable)
                        continue;

                    // Calculate new distance (1 unit per step)
                    float newDistance = currentDistance + 1f;

                    // Skip if outside radius
                    if (newDistance > radius)
                        continue;

                    // Skip if already visited with a shorter or equal distance
                    if (distances.ContainsKey(neighbor) && distances[neighbor] <= newDistance)
                        continue;

                    // Mark as visited and enqueue
                    distances[neighbor] = newDistance;
                    queue.Enqueue(neighbor);
                }
            }

            isApplied = true;
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

        }

        #endregion

        #region Visitor Path Recalculation

        /// <summary>
        /// Triggers all active visitors to recalculate their paths.
        /// Called when a new attractor is placed so visitors can take advantage of the new attraction.
        /// </summary>
        private void RecalculateAllVisitorPaths()
        {
            // Find all active visitors in the scene
            var visitors = FindObjectsByType<Visitors.VisitorController>(FindObjectsSortMode.None);

            int recalculatedCount = 0;
            foreach (var visitor in visitors)
            {
                if (visitor != null && visitor.State == Visitors.VisitorController.VisitorState.Walking)
                {
                    visitor.RecalculatePath();
                    recalculatedCount++;
                }
            }
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

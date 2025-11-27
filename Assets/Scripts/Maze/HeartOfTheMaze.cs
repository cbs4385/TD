using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Visitors;
namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the Heart of the Maze - the goal location where visitors are consumed for essence.
    /// </summary>
    public class HeartOfTheMaze : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Grid Position")]
        [SerializeField]
        [Tooltip("X coordinate in the maze grid (auto-set from 'H' marker if autoPosition is true)")]
        private int gridX;

        [SerializeField]
        [Tooltip("Y coordinate in the maze grid (auto-set from 'H' marker if autoPosition is true)")]
        private int gridY;

        [SerializeField]
        [Tooltip("Automatically position heart from 'H' marker in maze file")]
        private bool autoPosition = true;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Amount of essence gained per visitor consumed")]
        private int essencePerVisitor = 10;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the heart marker")]
        private Color markerColor = new Color(1f, 0.2f, 0.2f, 1f); // Bright red

        [SerializeField]
        [Tooltip("Size of the heart marker")]
        private float markerSize = 1.2f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 10;

        [SerializeField]
        [Tooltip("Enable pulsing animation")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 2f;

        [SerializeField]
        [Tooltip("Pulse amount")]
        private float pulseAmount = 0.2f;

        [Header("Attraction Settings")]
        [SerializeField]
        [Tooltip("Radius of attraction influence in grid units")]
        private float attractionRadius = 5f;

        [SerializeField]
        [Tooltip("Strength of attraction (higher = stronger pull)")]
        private float attractionStrength = 2.0f;

        [SerializeField]
        [Tooltip("Enable attraction to draw visitors toward the heart")]
        private bool enableAttraction = true;

        #endregion

        #region Private Fields

        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;

        #endregion

        #region Properties

        /// <summary>Gets the grid position of the heart</summary>
        public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

        /// <summary>Gets the essence value per visitor</summary>
        public int EssencePerVisitor => essencePerVisitor;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the grid position for the heart.
        /// </summary>
        /// <param name="pos">The grid position to set</param>
        public void SetGridPosition(Vector2Int pos)
        {
            gridX = pos.x;
            gridY = pos.y;
        }

        /// <summary>
        /// Positions the heart from the 'H' marker in the maze file.
        /// </summary>
        private void PositionFromMazeGrid()
        {
            // Find MazeGridBehaviour in scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null)
            {
                Debug.LogWarning("HeartOfTheMaze: MazeGridBehaviour not found! Cannot auto-position from 'H' marker.");
                return;
            }

            // Get heart position from maze grid
            Vector2Int heartPos = mazeGridBehaviour.HeartGridPos;

            // Update grid position
            gridX = heartPos.x;
            gridY = heartPos.y;

            // Convert to world position and update transform
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(heartPos.x, heartPos.y);
            transform.position = worldPos;

            Debug.Log($"HeartOfTheMaze: Auto-positioned to grid ({gridX}, {gridY}) at world position {worldPos}");
        }

        /// <summary>
        /// Called when a visitor reaches the heart and is consumed.
        /// </summary>
        /// <param name="visitor">The visitor controller to consume</param>
        public void OnVisitorConsumed(VisitorController visitor)
        {
            if (visitor == null)
            {
                Debug.LogWarning("Attempted to consume null visitor!");
                return;
            }


            // Add essence to game controller
            if (GameController.Instance != null)
            {
                GameController.Instance.AddEssence(essencePerVisitor);
            }
            else
            {
                Debug.LogError("GameController instance is null! Cannot add essence.");
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Destroy(visitor.gameObject);
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-position from maze grid if enabled
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }

            CreateVisualMarker();

            // Apply attraction to draw visitors toward the heart
            if (enableAttraction)
            {
                ApplyAttraction();
            }
        }

        private void Update()
        {
            if (enablePulse && spriteRenderer != null)
            {
                float pulse = Mathf.PingPong(Time.time * pulseSpeed, pulseAmount);
                transform.localScale = baseScale * (1f + pulse);
            }
        }

        private void CreateVisualMarker()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a heart-shaped sprite (simplified as a circle for now)
            spriteRenderer.sprite = CreateHeartSprite(32);
            spriteRenderer.color = markerColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            baseScale = new Vector3(markerSize, markerSize, 1f);
            transform.localScale = baseScale;

            // Add CircleCollider2D for trigger detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true;
            }
        }

        private Sprite CreateHeartSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle (can be enhanced to actual heart shape later)
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if a visitor entered the heart
            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null)
            {
                OnVisitorConsumed(visitor);
            }
        }

        #endregion

        #region Attraction

        /// <summary>
        /// Applies attraction to nearby tiles on the maze grid.
        /// Draws visitors toward the Heart of the Maze.
        /// </summary>
        private void ApplyAttraction()
        {
            // Find the MazeGridBehaviour in the scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("HeartOfTheMaze: Cannot apply attraction - MazeGridBehaviour or Grid is null!");
                return;
            }

            var grid = mazeGridBehaviour.Grid;

            // Calculate grid radius
            int gridRadius = Mathf.CeilToInt(attractionRadius);

            int affectedCount = 0;
            float totalAttractionApplied = 0f;

            // Apply attraction to tiles within radius
            for (int dx = -gridRadius; dx <= gridRadius; dx++)
            {
                for (int dy = -gridRadius; dy <= gridRadius; dy++)
                {
                    int targetX = gridX + dx;
                    int targetY = gridY + dy;

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
                    if (distance > attractionRadius)
                        continue;

                    // Calculate attraction with falloff
                    float falloff = Mathf.Clamp01(1f - (distance / attractionRadius));
                    float effectiveAttraction = attractionStrength * falloff;

                    // Apply attraction to grid
                    grid.AddAttraction(targetX, targetY, effectiveAttraction);

                    affectedCount++;
                    totalAttractionApplied += effectiveAttraction;
                }
            }

            Debug.Log($"HeartOfTheMaze: Applied attraction at grid ({gridX}, {gridY}) - affected {affectedCount} tiles with total attraction {totalAttractionApplied:F2}");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw heart marker in scene view
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw a pulsing effect
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f + pulse);

            // Draw attraction radius if enabled
            if (enableAttraction)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.2f);
                DrawCircleGizmo(transform.position, attractionRadius, 32);
            }
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments)
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

        #endregion
    }
}

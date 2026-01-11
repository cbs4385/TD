using UnityEngine;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the entrance point of the maze.
    /// This is where visitors will spawn or enter the maze.
    /// </summary>
    public class MazeEntrance : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Position Settings")]
        [SerializeField]
        [Tooltip("Automatically position entrance from maze data")]
        private bool autoPosition = true;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Show visual marker (disable when using spawn marker system)")]
        private bool showVisualMarker = false;

        [SerializeField]
        [Tooltip("Color of the entrance marker")]
        private Color markerColor = new Color(0.2f, 1f, 0.2f, 1f); // Bright green

        [SerializeField]
        [Tooltip("Size of the entrance marker")]
        private float markerSize = 0.8f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 10;

        #endregion

        #region Private Fields

        private SpriteRenderer spriteRenderer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-position from maze grid if enabled
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }
        }

        private void Start()
        {
            // Only create visual marker if enabled (legacy mode)
            if (showVisualMarker)
            {
                CreateVisualMarker();
            }
            else
            {
                // Hide the sprite renderer if it exists
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = false;
                }
            }
        }

        /// <summary>
        /// Positions the entrance using the maze's world-space entrance position.
        /// Can be called to reposition after maze regeneration.
        /// </summary>
        public void PositionFromMazeGrid()
        {
            // Find MazeGridBehaviour in scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Get entrance position directly in world space
            Vector3 worldPos = mazeGridBehaviour.EntranceWorldPosition;
            transform.position = worldPos;
        }

        private void CreateVisualMarker()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a simple circle sprite
            spriteRenderer.sprite = CreateCircleSprite(32);
            spriteRenderer.color = markerColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(markerSize, markerSize, 1f);
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

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

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw entrance marker in scene view
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.7f);
        }

        #endregion
    }
}

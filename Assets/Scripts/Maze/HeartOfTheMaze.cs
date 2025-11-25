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
        [Tooltip("X coordinate in the maze grid")]
        private int gridX;

        [SerializeField]
        [Tooltip("Y coordinate in the maze grid")]
        private int gridY;

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
            CreateVisualMarker();
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
        }

        #endregion
    }
}

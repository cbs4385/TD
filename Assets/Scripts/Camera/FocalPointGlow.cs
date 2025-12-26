using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Creates a pulsing lime green glow effect at the focal point tile position.
    /// </summary>
    public class FocalPointGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the focal point transform")]
        private Transform focalPointTransform;

        [Header("Glow Settings")]
        [SerializeField]
        [Tooltip("Lime green color for the glow")]
        private Color glowColor = new Color(0.5f, 1.0f, 0.0f, 0.7f); // Lime green

        [SerializeField]
        [Tooltip("Minimum alpha for pulse (0-1)")]
        private float minAlpha = 0.3f;

        [SerializeField]
        [Tooltip("Maximum alpha for pulse (0-1)")]
        private float maxAlpha = 0.9f;

        [SerializeField]
        [Tooltip("Pulse speed in Hz")]
        private float pulseSpeed = 2.0f;

        [SerializeField]
        [Tooltip("Size of the glow sprite relative to tile size")]
        private float glowSize = 1.2f;

        [SerializeField]
        [Tooltip("Z-offset for rendering (should be slightly above the tile)")]
        private float zOffset = -0.1f;

        #endregion

        #region Private Fields

        private SpriteRenderer glowSprite;
        private float pulsePhase;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find references if not set
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (focalPointTransform == null)
            {
                // Try to find it from CameraController3D
                var cameraController = FindFirstObjectByType<CameraController3D>();
                if (cameraController != null)
                {
                    // We need to access the focal point transform - it's private, so we'll use this GameObject's transform
                    focalPointTransform = transform;
                }
            }

            CreateGlowSprite();
        }

        private void Update()
        {
            if (glowSprite != null && focalPointTransform != null && mazeGridBehaviour != null)
            {
                UpdateGlowPosition();
                UpdateGlowPulse();
            }
        }

        #endregion

        #region Glow Creation

        private void CreateGlowSprite()
        {
            // Create a child GameObject for the glow sprite
            GameObject glowObj = new GameObject("FocalPointGlow");
            glowObj.transform.SetParent(transform, false);

            // Add SpriteRenderer
            glowSprite = glowObj.AddComponent<SpriteRenderer>();
            glowSprite.sprite = CreateCircleSprite();
            glowSprite.color = glowColor;
            glowSprite.sortingOrder = 100; // Render on top of most things

            // Set initial size
            float tileSize = mazeGridBehaviour != null ? mazeGridBehaviour.TileSize : 1.0f;
            glowObj.transform.localScale = Vector3.one * tileSize * glowSize;

            Debug.Log("[FocalPointGlow] Glow sprite created with lime green color");
        }

        /// <summary>
        /// Creates a soft-edged circle sprite for the glow effect.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int resolution = 128;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[resolution * resolution];
            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);
                    float normalizedDistance = distance / radius;

                    // Soft falloff using smoothstep
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
                    alpha = Mathf.Pow(alpha, 2f); // Make it even softer

                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution / 2f
            );
        }

        #endregion

        #region Glow Updates

        private void UpdateGlowPosition()
        {
            // Convert focal point world position to grid coordinates
            Vector3 focalPos = focalPointTransform.position;

            if (mazeGridBehaviour.WorldToGrid(focalPos, out int gridX, out int gridY))
            {
                // Get the center of the tile in world space
                Vector3 tileWorldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);

                // Position the glow at the tile center with z-offset
                if (glowSprite != null)
                {
                    glowSprite.transform.position = new Vector3(
                        tileWorldPos.x,
                        tileWorldPos.y,
                        tileWorldPos.z + zOffset
                    );
                }
            }
        }

        private void UpdateGlowPulse()
        {
            // Update pulse phase
            pulsePhase += Time.deltaTime * pulseSpeed * Mathf.PI * 2f;

            // Calculate alpha using sine wave
            float normalizedPulse = (Mathf.Sin(pulsePhase) + 1f) * 0.5f; // 0 to 1
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, normalizedPulse);

            // Apply alpha to the glow sprite
            if (glowSprite != null)
            {
                Color color = glowColor;
                color.a = alpha;
                glowSprite.color = color;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the focal point transform to track.
        /// </summary>
        public void SetFocalPointTransform(Transform focal)
        {
            focalPointTransform = focal;
        }

        /// <summary>
        /// Sets the maze grid behaviour reference.
        /// </summary>
        public void SetMazeGridBehaviour(MazeGridBehaviour grid)
        {
            mazeGridBehaviour = grid;
        }

        #endregion
    }
}

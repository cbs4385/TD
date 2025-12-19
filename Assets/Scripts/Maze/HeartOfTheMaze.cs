using UnityEngine;
using UnityEngine.Rendering.Universal;
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

        [Header("Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing glow light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the glow (pastel red)")]
        private Color glowColor = new Color(1f, 0.7f, 0.7f, 1f); // Pastel red

        [SerializeField]
        [Tooltip("Radius of the glow effect")]
        private float glowRadius = 5f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz")]
        private float glowFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.15f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 0.3f;

        [Header("Model Settings")]
        [SerializeField]
        [Tooltip("Model prefab to use for the heart visuals (optional)")]
        private GameObject heartModelPrefab;

        [SerializeField]
        [Tooltip("Use the model prefab instead of procedural sprite")]
        private bool useModelPrefab = false;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Enable rotation and Z-axis animation")]
        private bool enableModelAnimation = true;

        [SerializeField]
        [Tooltip("Animation frequency in Hz")]
        private float animationFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum Z position for oscillation")]
        private float minZPosition = -1.3f;

        [SerializeField]
        [Tooltip("Maximum Z position for oscillation")]
        private float maxZPosition = -0.3f;

        #endregion

        #region Private Fields

        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;
        private Light2D glowLight;
        private GameObject modelInstance;

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

            // Get heart position from maze grid
            Vector2Int heartPos = mazeGridBehaviour.HeartGridPos;

            // Update grid position
            gridX = heartPos.x;
            gridY = heartPos.y;

            // Convert to world position and update transform
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(heartPos.x, heartPos.y);
            transform.position = worldPos;

        }

        /// <summary>
        /// Called when a visitor reaches the heart and is consumed.
        /// </summary>
        /// <param name="visitor">The visitor controller to consume</param>
        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Track stats
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            // Add essence to game controller - use archetype-specific reward if available
            if (GameController.Instance != null)
            {
                int essence = visitor.GetEssenceReward();
                GameController.Instance.AddEssence(essence);
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Destroy(visitor.gameObject);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Setup model if using prefab
            SetupModel();

            // Ensure physics setup for reliable trigger callbacks
            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // Setup glow light
            SetupGlowLight();

            // Auto-position from maze grid if enabled
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }

            // Apply attraction to draw visitors toward the heart
            // (Done in Awake() to ensure attraction is applied before any Start() methods)
            if (enableAttraction)
            {
                ApplyAttraction();
            }
        }

        private void Start()
        {
            // Only create visual marker if not using model prefab
            if (!useModelPrefab || modelInstance == null)
            {
                CreateVisualMarker();
            }
            else
            {
                Debug.Log("[HeartOfTheMaze] Start: Skipping CreateVisualMarker because model is being used");
            }
        }

        private void Update()
        {
            if (enablePulse && spriteRenderer != null)
            {
                float pulse = Mathf.PingPong(Time.time * pulseSpeed, pulseAmount);
                transform.localScale = baseScale * (1f + pulse);
            }

            if (enableGlow && glowLight != null)
            {
                UpdateGlowPulse();
            }

            if (enableModelAnimation && modelInstance != null)
            {
                UpdateModelAnimation();
            }
        }

        private void CreateVisualMarker()
        {
            // Declare collider once at method level
            CircleCollider2D collider;

            // If using model prefab, skip procedural sprite creation
            if (useModelPrefab && modelInstance != null)
            {
                baseScale = new Vector3(markerSize, markerSize, 1f);
                transform.localScale = baseScale;

                // Add CircleCollider2D for trigger detection
                collider = GetComponent<CircleCollider2D>();
                if (collider == null)
                {
                    collider = gameObject.AddComponent<CircleCollider2D>();
                    collider.radius = 0.5f;
                    collider.isTrigger = true;
                }
                return;
            }

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
            collider = GetComponent<CircleCollider2D>();
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

        private void SetupModel()
        {
            Debug.Log($"[HeartOfTheMaze] SetupModel called - modelInstance: {(modelInstance != null ? "EXISTS" : "NULL")}, useModelPrefab: {useModelPrefab}, heartModelPrefab: {(heartModelPrefab != null ? "SET" : "NULL")}");

            if (modelInstance != null)
            {
                Debug.Log("[HeartOfTheMaze] SetupModel: modelInstance already exists, returning");
                return;
            }

            // Check if using model prefab
            if (!useModelPrefab || heartModelPrefab == null)
            {
                Debug.Log($"[HeartOfTheMaze] SetupModel: Not using model prefab (useModelPrefab={useModelPrefab}, heartModelPrefab={(heartModelPrefab != null ? "SET" : "NULL")})");
                return;
            }

            Debug.Log("[HeartOfTheMaze] SetupModel: Instantiating model prefab...");

            // Instantiate the model prefab
            var instantiatedObject = (GameObject)Instantiate((UnityEngine.Object)heartModelPrefab, transform);
            if (instantiatedObject == null)
            {
                Debug.LogWarning("[HeartOfTheMaze] Failed to instantiate heart model prefab. Falling back to sprite rendering.");
                useModelPrefab = false;
                return;
            }

            modelInstance = instantiatedObject;
            // Set position with proper Z offset for 2D layering
            // The heartofmaze prefab is designed with Z = -0.3
            modelInstance.transform.localPosition = new Vector3(0, 0, -0.3f);
            // Don't reset rotation and scale - preserve the prefab's configuration
            // modelInstance.transform.localRotation = Quaternion.identity;
            // modelInstance.transform.localScale = Vector3.one;

            Debug.Log($"[HeartOfTheMaze] SetupModel: Model instantiated successfully - {modelInstance.name}");

            // Disable sprite renderer if we have a model
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sprite.enabled = false;
                Debug.Log("[HeartOfTheMaze] SetupModel: Disabled sprite renderer");
            }
        }

        private void SetupGlowLight()
        {
            if (!enableGlow)
                return;

            try
            {
                // Remove any old 3D Light components that might conflict
                var oldLight = GetComponent<Light>();
                if (oldLight != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(oldLight);
#else
                    Destroy(oldLight);
#endif
                }

                // Check if we already have a Light2D component
                glowLight = GetComponent<Light2D>();
                if (glowLight == null)
                {
                    glowLight = gameObject.AddComponent<Light2D>();
                }

                // Configure the 2D light
                glowLight.lightType = Light2D.LightType.Point;
                glowLight.color = glowColor;
                glowLight.pointLightOuterRadius = glowRadius;
                glowLight.intensity = glowMaxIntensity;

                // Additional Light2D settings for proper color rendering
                glowLight.pointLightInnerRadius = 0f;
                glowLight.pointLightInnerAngle = 360f;
                glowLight.pointLightOuterAngle = 360f;

                // Use additive blend style (1) for colored lights instead of multiply (0)
                // Additive blending preserves light colors better
                glowLight.blendStyleIndex = 1;

                Debug.Log($"[HeartOfTheMaze] Light2D configured - Color: {glowLight.color}, Intensity: {glowLight.intensity}, Radius: {glowLight.pointLightOuterRadius}, BlendStyle: {glowLight.blendStyleIndex}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[HeartOfTheMaze] Failed to setup glow light: {e.Message}");
                glowLight = null;
            }
        }

        private void UpdateGlowPulse()
        {
            // Calculate pulsing intensity using sine wave
            // frequency in Hz = cycles per second
            // Time.time * frequency * 2π gives us the angle for sin wave
            float angle = Time.time * glowFrequency * 2f * Mathf.PI;

            // Map sin wave from [-1, 1] to [0, 1]
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;

            // Map to intensity range [min, max]
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);

            glowLight.intensity = intensity;
        }

        private void UpdateModelAnimation()
        {
            // Calculate animation phase using sine wave
            float angle = Time.time * animationFrequency * 2f * Mathf.PI;

            // Rotate around Z axis (full 360-degree rotation)
            float zRotation = Time.time * animationFrequency * 360f;
            modelInstance.transform.localRotation = Quaternion.Euler(
                modelInstance.transform.localRotation.eulerAngles.x,
                modelInstance.transform.localRotation.eulerAngles.y,
                zRotation
            );

            // Oscillate Z position between minZPosition and maxZPosition
            // Map sin wave from [-1, 1] to [minZ, maxZ]
            float normalizedSin = (Mathf.Sin(angle) + 1f) / 2f;
            float zPosition = Mathf.Lerp(maxZPosition, minZPosition, normalizedSin);

            modelInstance.transform.localPosition = new Vector3(
                modelInstance.transform.localPosition.x,
                modelInstance.transform.localPosition.y,
                zPosition
            );
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if a visitor entered the heart
            var visitor = other.GetComponent<VisitorControllerBase>();
            if (visitor != null)
            {
                OnVisitorConsumed(visitor);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Catch any visitors that miss the initial enter event
            var visitor = other.GetComponent<VisitorControllerBase>();
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
        /// Can be called to reapply after maze regeneration.
        /// </summary>
        public void ApplyAttraction()
        {
            // Find the MazeGridBehaviour in the scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null)
            {
                return;
            }

            if (mazeGridBehaviour.Grid == null)
            {
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

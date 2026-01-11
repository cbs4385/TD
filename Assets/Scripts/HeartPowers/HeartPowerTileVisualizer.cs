using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Visualizes Heart Power effects at world positions using ROYGBIV spectrum colors.
    /// Creates glowing overlays on affected positions to show where effects are active
    /// and their intensity.
    /// Works purely with Vector3 world positions.
    /// </summary>
    public class HeartPowerTileVisualizer : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Represents a visual effect at a world position.
        /// </summary>
        private class PositionEffect
        {
            public Vector3 worldPosition;
            public HeartPowerType powerType;
            public float intensity;          // 0-1, how strong the effect is
            public float expirationTime;     // Time.time when effect expires
            public GameObject visualObject;  // The overlay sprite
            public SpriteRenderer spriteRenderer;

            public bool IsExpired => expirationTime > 0 && Time.time >= expirationTime;
        }

        #endregion

        #region Serialized Fields

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Size of the overlay sprite (in world units)")]
        private float overlaySize = 1.0f;

        [SerializeField]
        [Tooltip("Sorting layer name for overlays (e.g., 'Default', 'Terrain', 'Effects')")]
        private string sortingLayerName = "Default";

        [SerializeField]
        [Tooltip("Sorting order within the layer for overlays")]
        private int sortingOrder = 100;

        [SerializeField]
        [Tooltip("Pulse speed for the glow effect")]
        private float pulseSpeed = 2.5f;

        [SerializeField]
        [Tooltip("Minimum alpha for pulsing effect")]
        private float minAlpha = 0.3f;

        [SerializeField]
        [Tooltip("Maximum alpha for pulsing effect")]
        private float maxAlpha = 0.7f;

        #endregion

        #region Private Fields

        private List<PositionEffect> activeEffects = new List<PositionEffect>();
        private GameObject effectsContainer;

        // ROYGBIV spectrum colors matching HeartPowerPanelController
        private readonly Color[] roygbivColors = new Color[]
        {
            new Color(0.8f, 0.1f, 0.1f, 1f),  // Power 1: Deep Red
            new Color(1.0f, 0.5f, 0.0f, 1f),  // Power 2: Warm Orange
            new Color(1.0f, 0.9f, 0.1f, 1f),  // Power 3: Bright Yellow
            new Color(0.2f, 0.8f, 0.2f, 1f),  // Power 4: Vivid Green
            new Color(0.2f, 0.5f, 1.0f, 1f),  // Power 5: Cool Blue
            new Color(0.3f, 0.0f, 0.5f, 1f),  // Power 6: Indigo
            new Color(0.6f, 0.2f, 0.8f, 1f),  // Power 7: Vibrant Violet
            new Color(0.9f, 0.1f, 0.5f, 1f),  // Power 8: Crimson
            new Color(0.5f, 0.0f, 0.2f, 1f)   // Power 9: Dark Burgundy
        };

        // Reusable lists for cleanup
        private readonly List<PositionEffect> _effectsToRemove = new List<PositionEffect>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Create container for all effect visuals
            effectsContainer = new GameObject("HeartPowerEffects");
            effectsContainer.transform.SetParent(transform);
        }

        private void Update()
        {
            UpdateEffects();
            CleanupExpiredEffects();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a visual effect at a world position (alias for AddTileEffectAtWorldPos).
        /// </summary>
        public void AddEffect(Vector3 worldPos, HeartPowerType powerType, float intensity, float duration)
            => AddTileEffectAtWorldPos(worldPos, powerType, intensity, duration);

        /// <summary>
        /// Adds a visual effect at a world position.
        /// </summary>
        /// <param name="worldPos">World position for the effect</param>
        /// <param name="powerType">Which Heart Power is affecting this position</param>
        /// <param name="intensity">Effect strength (0-1)</param>
        /// <param name="duration">How long the effect lasts (0 = permanent)</param>
        public void AddTileEffectAtWorldPos(Vector3 worldPos, HeartPowerType powerType, float intensity, float duration)
        {
            // Check if this power type already has an effect at similar position
            bool foundExisting = false;
            float proximityThreshold = 0.5f;

            foreach (var effect in activeEffects)
            {
                if (effect.powerType == powerType && Vector3.Distance(effect.worldPosition, worldPos) < proximityThreshold)
                {
                    // Update existing effect
                    effect.intensity = intensity;
                    effect.expirationTime = duration > 0 ? Time.time + duration : 0;
                    foundExisting = true;
                    break;
                }
            }

            if (!foundExisting)
            {
                // Create new effect
                PositionEffect newEffect = new PositionEffect
                {
                    worldPosition = worldPos,
                    powerType = powerType,
                    intensity = intensity,
                    expirationTime = duration > 0 ? Time.time + duration : 0
                };

                CreateVisualForEffect(newEffect);
                activeEffects.Add(newEffect);
            }
        }

        /// <summary>
        /// Removes all effects of a specific power type.
        /// </summary>
        public void RemoveEffectsByPowerType(HeartPowerType powerType)
        {
            _effectsToRemove.Clear();

            foreach (var effect in activeEffects)
            {
                if (effect.powerType == powerType)
                {
                    _effectsToRemove.Add(effect);
                }
            }

            foreach (var effect in _effectsToRemove)
            {
                DestroyEffect(effect);
                activeEffects.Remove(effect);
            }
        }

        /// <summary>
        /// Removes all effects.
        /// </summary>
        public void ClearAllEffects()
        {
            foreach (var effect in activeEffects)
            {
                DestroyEffect(effect);
            }

            activeEffects.Clear();
        }

        /// <summary>
        /// Offsets all existing visuals by a world offset (used when the maze expands).
        /// </summary>
        public void ApplyWorldOffset(Vector3 worldOffset)
        {
            foreach (var effect in activeEffects)
            {
                effect.worldPosition += worldOffset;
                if (effect.visualObject != null)
                {
                    effect.visualObject.transform.position += worldOffset;
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a visual overlay for an effect.
        /// </summary>
        private void CreateVisualForEffect(PositionEffect effect)
        {
            Vector3 worldPos = effect.worldPosition;
            worldPos.z = 0; // Ensure sprite is at Z=0 for 2D visibility

            // Create game object for this effect
            GameObject effectObj = new GameObject($"Effect_{effect.powerType}");
            effectObj.transform.SetParent(effectsContainer.transform);
            effectObj.transform.position = worldPos;

            // Add sprite renderer
            SpriteRenderer sr = effectObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = GetColorForPowerType(effect.powerType);
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            // Scale overlay
            effectObj.transform.localScale = Vector3.one * overlaySize;

            // Store references
            effect.visualObject = effectObj;
            effect.spriteRenderer = sr;
        }

        /// <summary>
        /// Updates all active effects (pulsing, etc.)
        /// </summary>
        private void UpdateEffects()
        {
            float pulsePhase = Time.time * pulseSpeed;

            foreach (var effect in activeEffects)
            {
                if (effect.spriteRenderer == null) continue;

                // Calculate pulsing alpha
                float pulse = (Mathf.Sin(pulsePhase) + 1f) * 0.5f; // 0 to 1
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse) * effect.intensity;

                // Apply color with pulsing alpha
                Color color = GetColorForPowerType(effect.powerType);
                color.a = alpha;
                effect.spriteRenderer.color = color;
            }
        }

        /// <summary>
        /// Removes expired effects.
        /// </summary>
        private void CleanupExpiredEffects()
        {
            _effectsToRemove.Clear();

            foreach (var effect in activeEffects)
            {
                if (effect.IsExpired)
                {
                    _effectsToRemove.Add(effect);
                }
            }

            foreach (var effect in _effectsToRemove)
            {
                DestroyEffect(effect);
                activeEffects.Remove(effect);
            }
        }

        /// <summary>
        /// Destroys the visual object for an effect.
        /// </summary>
        private void DestroyEffect(PositionEffect effect)
        {
            if (effect.visualObject != null)
            {
                Destroy(effect.visualObject);
            }
        }

        /// <summary>
        /// Gets the ROYGBIV color for a given power type.
        /// </summary>
        private Color GetColorForPowerType(HeartPowerType powerType)
        {
            int index = (int)powerType;
            if (index >= 0 && index < roygbivColors.Length)
            {
                return roygbivColors[index];
            }
            return Color.magenta; // Fallback
        }

        /// <summary>
        /// Creates a simple circle sprite for overlays.
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[resolution * resolution];
            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f - 2f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float distance = Vector2.Distance(pos, center);

                    // Create soft-edged circle
                    float alpha = 1f - Mathf.Clamp01((distance - radius + 4f) / 4f);
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                resolution
            );
        }

        #endregion
    }
}

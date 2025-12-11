using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Visualizes Heart Power effects on maze tiles using ROYGBIV spectrum colors.
    /// Creates glowing overlays on affected tiles to show where effects are active
    /// and their intensity.
    /// </summary>
    public class HeartPowerTileVisualizer : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Represents a visual effect on a single tile.
        /// </summary>
        private class TileEffect
        {
            public Vector2Int tile;
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
        [Tooltip("Size of the tile overlay sprite (in world units)")]
        private float overlaySize = 1.0f;

        [SerializeField]
        [Tooltip("Sorting layer for tile overlays")]
        private int sortingOrder = 5;

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

        private MazeGridBehaviour mazeGridBehaviour;
        private Dictionary<Vector2Int, List<TileEffect>> tileEffects = new Dictionary<Vector2Int, List<TileEffect>>();
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
            new Color(0.6f, 0.2f, 0.8f, 1f)   // Power 7: Vibrant Violet
        };

        // Reusable lists for cleanup
        private readonly List<Vector2Int> _tilesToRemove = new List<Vector2Int>();
        private readonly List<TileEffect> _effectsToRemove = new List<TileEffect>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("[TileVisualizer] ✗ MazeGridBehaviour not found! Tile effects will not render.");
            }
            else
            {
                Debug.Log("[TileVisualizer] ✓ Found MazeGridBehaviour, ready to visualize tile effects");
            }

            // Create container for all effect visuals
            effectsContainer = new GameObject("HeartPowerTileEffects");
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
        /// Adds a visual effect to a tile.
        /// </summary>
        /// <param name="tile">Grid position of the tile</param>
        /// <param name="powerType">Which Heart Power is affecting this tile</param>
        /// <param name="intensity">Effect strength (0-1)</param>
        /// <param name="duration">How long the effect lasts (0 = permanent)</param>
        public void AddTileEffect(Vector2Int tile, HeartPowerType powerType, float intensity, float duration)
        {
            if (mazeGridBehaviour == null)
            {
                Debug.LogWarning("[TileVisualizer] Cannot add effect - MazeGridBehaviour is null");
                return;
            }

            // Ensure tile effects list exists
            if (!tileEffects.ContainsKey(tile))
            {
                tileEffects[tile] = new List<TileEffect>();
            }

            // Check if this power type is already affecting this tile
            bool foundExisting = false;
            foreach (var effect in tileEffects[tile])
            {
                if (effect.powerType == powerType)
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
                TileEffect newEffect = new TileEffect
                {
                    tile = tile,
                    powerType = powerType,
                    intensity = intensity,
                    expirationTime = duration > 0 ? Time.time + duration : 0
                };

                CreateVisualForEffect(newEffect);
                tileEffects[tile].Add(newEffect);

                Debug.Log($"[TileVisualizer] Added {powerType} effect to tile {tile} (intensity: {intensity}, duration: {duration}s, color: {GetColorForPowerType(powerType)})");
            }
        }

        /// <summary>
        /// Removes all effects of a specific power type.
        /// </summary>
        public void RemoveEffectsByPowerType(HeartPowerType powerType)
        {
            _tilesToRemove.Clear();

            foreach (var kvp in tileEffects)
            {
                _effectsToRemove.Clear();

                foreach (var effect in kvp.Value)
                {
                    if (effect.powerType == powerType)
                    {
                        _effectsToRemove.Add(effect);
                    }
                }

                // Remove the effects
                foreach (var effect in _effectsToRemove)
                {
                    DestroyEffect(effect);
                    kvp.Value.Remove(effect);
                }

                // Mark tile for removal if no effects remain
                if (kvp.Value.Count == 0)
                {
                    _tilesToRemove.Add(kvp.Key);
                }
            }

            // Remove empty tiles
            foreach (var tile in _tilesToRemove)
            {
                tileEffects.Remove(tile);
            }

            Debug.Log($"[TileVisualizer] Removed all {powerType} effects");
        }

        /// <summary>
        /// Removes all tile effects.
        /// </summary>
        public void ClearAllEffects()
        {
            foreach (var kvp in tileEffects)
            {
                foreach (var effect in kvp.Value)
                {
                    DestroyEffect(effect);
                }
            }

            tileEffects.Clear();
            Debug.Log("[TileVisualizer] Cleared all tile effects");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a visual overlay for a tile effect.
        /// </summary>
        private void CreateVisualForEffect(TileEffect effect)
        {
            if (mazeGridBehaviour == null) return;

            // Get world position for this tile
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(effect.tile.x, effect.tile.y);

            // Create game object for this effect
            GameObject effectObj = new GameObject($"TileEffect_{effect.powerType}_{effect.tile}");
            effectObj.transform.SetParent(effectsContainer.transform);
            effectObj.transform.position = worldPos;

            // Add sprite renderer
            SpriteRenderer sr = effectObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = GetColorForPowerType(effect.powerType);
            sr.sortingOrder = sortingOrder;

            // Scale to fit tile
            effectObj.transform.localScale = Vector3.one * overlaySize;

            // Store references
            effect.visualObject = effectObj;
            effect.spriteRenderer = sr;

            Debug.Log($"[TileVisualizer] ✓ Created visual at world pos {worldPos} for tile {effect.tile}, color: {sr.color}, sortingOrder: {sortingOrder}");
        }

        /// <summary>
        /// Updates all active effects (pulsing, etc.)
        /// </summary>
        private void UpdateEffects()
        {
            float pulsePhase = Time.time * pulseSpeed;

            foreach (var kvp in tileEffects)
            {
                foreach (var effect in kvp.Value)
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
        }

        /// <summary>
        /// Removes expired effects.
        /// </summary>
        private void CleanupExpiredEffects()
        {
            _tilesToRemove.Clear();

            foreach (var kvp in tileEffects)
            {
                _effectsToRemove.Clear();

                foreach (var effect in kvp.Value)
                {
                    if (effect.IsExpired)
                    {
                        _effectsToRemove.Add(effect);
                    }
                }

                // Destroy expired effects
                foreach (var effect in _effectsToRemove)
                {
                    DestroyEffect(effect);
                    kvp.Value.Remove(effect);
                }

                // Mark for removal if empty
                if (kvp.Value.Count == 0)
                {
                    _tilesToRemove.Add(kvp.Key);
                }
            }

            // Remove empty tile entries
            foreach (var tile in _tilesToRemove)
            {
                tileEffects.Remove(tile);
            }
        }

        /// <summary>
        /// Destroys the visual object for an effect.
        /// </summary>
        private void DestroyEffect(TileEffect effect)
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
        /// Creates a simple circle sprite for tile overlays.
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

using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Utility factory for creating procedural sprites and applying sprite renderer settings.
    /// Reduces code duplication across components that generate visual sprites at runtime.
    /// </summary>
    public static class ProceduralSpriteFactory
    {
        /// <summary>
        /// Creates a solid circle sprite with the specified resolution.
        /// </summary>
        /// <param name="resolution">Texture size in pixels (width and height)</param>
        /// <param name="pixelsPerUnit">Pixels per Unity unit for the sprite</param>
        /// <returns>A new sprite with a solid white circle</returns>
        public static Sprite CreateSolidCircleSprite(int resolution = 32, int pixelsPerUnit = 32)
        {
            Texture2D texture = new Texture2D(resolution, resolution);
            Color[] pixels = new Color[resolution * resolution];

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f;

            // Create a solid circle
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * resolution + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
        }

        /// <summary>
        /// Creates a soft-edge circle sprite with alpha gradient (glow effect).
        /// </summary>
        /// <param name="resolution">Texture size in pixels (width and height)</param>
        /// <param name="pixelsPerUnit">Pixels per Unity unit for the sprite</param>
        /// <returns>A new sprite with a soft glowing circle</returns>
        public static Sprite CreateSoftCircleSprite(int resolution = 32, int pixelsPerUnit = 32)
        {
            Texture2D texture = new Texture2D(resolution, resolution);
            Color[] pixels = new Color[resolution * resolution];

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f;

            // Create a circle with soft edges (glow effect)
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit
            );
        }

        /// <summary>
        /// Ensures a GameObject has a SpriteRenderer component and optionally sets up a procedural sprite.
        /// </summary>
        /// <param name="gameObject">Target GameObject</param>
        /// <param name="createProceduralSprite">If true, creates and assigns a solid circle sprite</param>
        /// <param name="useSoftEdges">If true, creates a soft-edge sprite instead of solid</param>
        /// <param name="resolution">Texture resolution for procedural sprites</param>
        /// <param name="pixelsPerUnit">Pixels per unit for procedural sprites</param>
        /// <returns>The SpriteRenderer component (existing or newly created)</returns>
        public static SpriteRenderer SetupSpriteRenderer(
            GameObject gameObject,
            bool createProceduralSprite = false,
            bool useSoftEdges = false,
            int resolution = 32,
            int pixelsPerUnit = 32)
        {
            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (createProceduralSprite)
            {
                spriteRenderer.sprite = useSoftEdges
                    ? CreateSoftCircleSprite(resolution, pixelsPerUnit)
                    : CreateSolidCircleSprite(resolution, pixelsPerUnit);
            }

            return spriteRenderer;
        }

        /// <summary>
        /// Applies common sprite settings: color, sorting order, and scale.
        /// </summary>
        /// <param name="spriteRenderer">Target SpriteRenderer</param>
        /// <param name="color">Sprite color tint</param>
        /// <param name="sortingOrder">Sprite rendering layer order</param>
        /// <param name="size">World-space size (applied to transform.localScale)</param>
        /// <param name="applyScale">If true, sets transform.localScale based on size parameter</param>
        public static void ApplySpriteSettings(
            SpriteRenderer spriteRenderer,
            Color color,
            int sortingOrder,
            float size = 1f,
            bool applyScale = true)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;

            if (applyScale)
            {
                spriteRenderer.transform.localScale = new Vector3(size, size, 1f);
            }
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using ForestMaze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Generates a cloud-like fog plane that covers void areas, similar to Civilization's fog of war.
    /// Uses procedural noise to create billowy, atmospheric clouds with pseudo-3D lighting.
    /// </summary>
    public class VoidFogGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The maze grid behaviour to read void areas from")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Fog Colors")]
        [SerializeField]
        [Tooltip("Main fog color (lit areas) - matches forest canopy")]
        private Color fogColor = new Color(0.07f, 0.08f, 0.02f, 1f);

        [SerializeField]
        [Tooltip("Shadow fog color (darker areas) - deep forest shadow")]
        private Color fogColorDark = new Color(0.01f, 0.11f, 0.00f, 1f);

        [Header("Cloud Shape")]
        [SerializeField]
        [Range(0.5f, 10f)]
        [Tooltip("Scale of cloud pattern (lower = larger clouds)")]
        private float cloudScale = 10f;

        [SerializeField]
        [Range(0.5f, 5f)]
        [Tooltip("Amount of fine detail in clouds")]
        private float cloudDetail = 2f;

        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Overall cloud density/coverage")]
        private float cloudDensity = 1f;

        [SerializeField]
        [Range(1f, 10f)]
        [Tooltip("Sharpness of cloud edges (higher = more defined)")]
        private float cloudSharpness = 8f;

        [Header("Animation")]
        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Speed of cloud movement")]
        private float windSpeed = 0.03f;

        [SerializeField]
        [Tooltip("Direction of cloud movement")]
        private Vector2 windDirection = new Vector2(1f, 0.3f);

        [Header("Lighting")]
        [SerializeField]
        [Tooltip("Direction light comes from")]
        private Vector2 lightDirection = new Vector2(0.5f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Strength of cloud shadows")]
        private float shadowStrength = 0.4f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Ambient light level")]
        private float ambientLight = 0.6f;

        [Header("Path Masking")]
        [SerializeField]
        [Tooltip("How far from paths the fog fades (in world units)")]
        private float pathFadeDistance = 3f;

        [SerializeField]
        [Tooltip("Extra padding around the maze bounds")]
        private float boundsPadding = 100f;

        [Header("Positioning")]
        [SerializeField]
        [Tooltip("Z position of fog (at top of wall models, around -1)")]
        private float zPosition = -1f;

        private GameObject fogQuad;
        private Material fogMaterial;
        private Texture2D pathMaskTexture;
        private Bounds mazeBounds;
        private DynamicMazeGrowth dynamicMazeGrowth;
        private int lastKnownTileCount = 0;

        private void Start()
        {
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (mazeGridBehaviour == null)
            {
                enabled = false;
                return;
            }

            // Find DynamicMazeGrowth to detect when maze changes
            dynamicMazeGrowth = mazeGridBehaviour.GetComponent<DynamicMazeGrowth>();

            // Wait for maze to be ready
            StartCoroutine(GenerateFogNextFrame());
        }

        private void Update()
        {
            // Check if maze data has changed (new tiles added)
            if (mazeGridBehaviour != null && mazeGridBehaviour.WorldSpaceMazeData != null)
            {
                int currentTileCount = mazeGridBehaviour.WorldSpaceMazeData.Tiles.Count;
                if (currentTileCount != lastKnownTileCount && lastKnownTileCount > 0)
                {
                    // Maze has grown - regenerate fog
                    lastKnownTileCount = currentTileCount;
                    RegenerateFog();
                }
            }
        }

        private System.Collections.IEnumerator GenerateFogNextFrame()
        {
            // Wait for maze data to be available
            yield return null;
            yield return null;

            GenerateVoidFog();
        }

        private void GenerateVoidFog()
        {
            if (mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            mazeBounds = mazeData.Bounds;

            // Track tile count for change detection
            lastKnownTileCount = mazeData.Tiles.Count;

            // Expand bounds with padding
            mazeBounds.Expand(boundsPadding * 2f);

            // Generate the path mask texture
            GeneratePathMaskTexture(mazeData);

            // Create fog material with mask
            CreateFogMaterial();

            // Create a single large quad covering the entire area
            CreateFogQuad();
        }

        private void GeneratePathMaskTexture(WorldSpaceMazeData mazeData)
        {
            // Texture resolution based on bounds size (2 pixels per world unit)
            int texWidth = Mathf.CeilToInt(mazeBounds.size.x * 2f);
            int texHeight = Mathf.CeilToInt(mazeBounds.size.y * 2f);

            // Clamp to reasonable size
            texWidth = Mathf.Clamp(texWidth, 64, 2048);
            texHeight = Mathf.Clamp(texHeight, 64, 2048);

            pathMaskTexture = new Texture2D(texWidth, texHeight, TextureFormat.R8, false);
            pathMaskTexture.filterMode = FilterMode.Bilinear;
            pathMaskTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texWidth * texHeight];

            // Initialize all pixels to full fog (white = fog visible)
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }

            // Calculate world-to-texture scale
            float worldToTexX = texWidth / mazeBounds.size.x;
            float worldToTexY = texHeight / mazeBounds.size.y;

            // Mark walkable areas as transparent (black = no fog)
            foreach (var tile in mazeData.Tiles)
            {
                if (!tile.Walkable) continue;

                // Convert world position to texture coordinates
                float worldX = tile.Position.x - mazeBounds.min.x;
                float worldY = tile.Position.y - mazeBounds.min.y;

                int centerTexX = Mathf.RoundToInt(worldX * worldToTexX);
                int centerTexY = Mathf.RoundToInt(worldY * worldToTexY);

                // Calculate radius in texture pixels (tile size + fade distance)
                float tileRadius = tile.Size * 0.5f + pathFadeDistance;
                int radiusPixels = Mathf.CeilToInt(tileRadius * Mathf.Max(worldToTexX, worldToTexY));

                // Paint a circular gradient around each walkable tile
                for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                {
                    for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                    {
                        int px = centerTexX + dx;
                        int py = centerTexY + dy;

                        if (px < 0 || px >= texWidth || py < 0 || py >= texHeight)
                            continue;

                        // Calculate distance from tile center in world units
                        float distWorld = Mathf.Sqrt(dx * dx / (worldToTexX * worldToTexX) + dy * dy / (worldToTexY * worldToTexY));

                        // Calculate fog amount based on distance
                        float halfTileSize = tile.Size * 0.5f;
                        float fogAmount;

                        if (distWorld <= halfTileSize)
                        {
                            // Inside tile: no fog
                            fogAmount = 0f;
                        }
                        else if (distWorld <= halfTileSize + pathFadeDistance)
                        {
                            // Fade zone: gradual transition
                            float t = (distWorld - halfTileSize) / pathFadeDistance;
                            fogAmount = Mathf.SmoothStep(0f, 1f, t);
                        }
                        else
                        {
                            // Outside fade zone: full fog
                            fogAmount = 1f;
                        }

                        // Take minimum (most transparent) value
                        int pixelIndex = py * texWidth + px;
                        pixels[pixelIndex].r = Mathf.Min(pixels[pixelIndex].r, fogAmount);
                    }
                }
            }

            pathMaskTexture.SetPixels(pixels);
            pathMaskTexture.Apply();
        }

        private void CreateFogMaterial()
        {
            var shader = Shader.Find("Custom/VoidFogMasked");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            fogMaterial = new Material(shader);
            UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties()
        {
            if (fogMaterial == null) return;

            // Colors
            fogMaterial.SetColor("_FogColor", fogColor);
            fogMaterial.SetColor("_FogColorDark", fogColorDark);

            // Cloud shape
            fogMaterial.SetFloat("_CloudScale", cloudScale);
            fogMaterial.SetFloat("_CloudDetail", cloudDetail);
            fogMaterial.SetFloat("_CloudDensity", cloudDensity);
            fogMaterial.SetFloat("_CloudSharpness", cloudSharpness);

            // Animation
            fogMaterial.SetFloat("_WindSpeed", windSpeed);
            fogMaterial.SetVector("_WindDirection", new Vector4(windDirection.x, windDirection.y, 0, 0));

            // Lighting
            fogMaterial.SetVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, 0, 0));
            fogMaterial.SetFloat("_ShadowStrength", shadowStrength);
            fogMaterial.SetFloat("_AmbientLight", ambientLight);

            // Path mask
            if (pathMaskTexture != null)
            {
                fogMaterial.SetTexture("_PathMask", pathMaskTexture);
            }
        }

        private void CreateFogQuad()
        {
            fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogQuad.name = "VoidFogPlane";
            fogQuad.transform.SetParent(transform);

            // Position and scale to cover entire bounds
            fogQuad.transform.position = new Vector3(mazeBounds.center.x, mazeBounds.center.y, zPosition);
            fogQuad.transform.localScale = new Vector3(mazeBounds.size.x, mazeBounds.size.y, 1f);
            fogQuad.transform.rotation = Quaternion.identity;

            // Remove collider
            var collider = fogQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Apply material
            var renderer = fogQuad.GetComponent<Renderer>();
            renderer.material = fogMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnValidate()
        {
            if (Application.isPlaying && fogMaterial != null)
            {
                UpdateMaterialProperties();
            }
        }

        public void ClearFog()
        {
            if (fogQuad != null)
            {
                Destroy(fogQuad);
                fogQuad = null;
            }
        }

        public void RegenerateFog()
        {
            ClearFog();
            if (pathMaskTexture != null)
            {
                Destroy(pathMaskTexture);
                pathMaskTexture = null;
            }
            GenerateVoidFog();
        }

        private void OnDestroy()
        {
            ClearFog();
            if (fogMaterial != null)
            {
                Destroy(fogMaterial);
            }
            if (pathMaskTexture != null)
            {
                Destroy(pathMaskTexture);
            }
        }
    }
}

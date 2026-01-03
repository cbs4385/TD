using UnityEngine;
using FaeMaze.Maze;
using System.Collections.Generic;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Spawns environment decoration (trees, walls) to fill the entire background plane,
    /// except for the area occupied by maze tiles. Makes decorations near focal point
    /// transparent for better visibility.
    /// </summary>
    public class EnvironmentDecorator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The maze grid behaviour to decorate")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Tree/wall prefab to spawn on non-walkable tiles")]
        private GameObject treePrefab;

        [SerializeField]
        [Tooltip("Focal point for transparency (leave null to use main camera)")]
        private Transform focalPoint;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Z position for spawned decorations")]
        private float zPosition = 0f;

        [SerializeField]
        [Tooltip("Z rotation variance in degrees (+/-)")]
        private float zRotationVariance = 5f;

        [SerializeField]
        [Tooltip("Padding around maze to fill with trees (in tiles)")]
        private int backgroundPadding = 20;

        [SerializeField]
        [Tooltip("Parent transform for spawned decorations")]
        private Transform decorationParent;

        [Header("Background")]
        [SerializeField]
        [Tooltip("Create black backdrop plane below the game")]
        private bool createBlackBackdrop = true;

        [SerializeField]
        [Tooltip("Z position for black backdrop plane (negative = behind/below game)")]
        private float backdropZPosition = -1000f;

        [Header("Transparency Settings")]
        [SerializeField]
        [Tooltip("Radius in tiles for transparency effect")]
        private float transparencyRadius = 3f;

        [SerializeField]
        [Tooltip("Alpha value for decorations within radius (0.25 = 75% transparent)")]
        private float transparentAlpha = 0.25f;

        [SerializeField]
        [Tooltip("Alpha value for decorations outside radius")]
        private float opaqueAlpha = 1f;

        [SerializeField]
        [Tooltip("Smoothness of transition between transparent and opaque")]
        private float transitionSmoothness = 0.5f;

        private class DecorationData
        {
            public GameObject gameObject;
            public Renderer[] renderers;
            public Material[] originalMaterials;
            public Material[] instanceMaterials;
        }

        private List<DecorationData> decorations = new List<DecorationData>();
        private Camera mainCamera;

        private void Start()
        {
            // Find MazeGridBehaviour if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("[EnvironmentDecorator] MazeGridBehaviour not found!");
                enabled = false;
                return;
            }

            if (treePrefab == null)
            {
                Debug.LogError("[EnvironmentDecorator] Tree prefab not assigned!");
                enabled = false;
                return;
            }

            // Create parent if not assigned
            if (decorationParent == null)
            {
                GameObject parentObj = new GameObject("Environment Decorations");
                decorationParent = parentObj.transform;
            }

            // Create black backdrop plane
            if (createBlackBackdrop)
            {
                CreateBlackBackdrop();
            }

            // Wait one frame for maze to be generated
            StartCoroutine(SpawnDecorationsNextFrame());
        }

        private void CreateBlackBackdrop()
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Black Backdrop";
            backdrop.transform.SetParent(decorationParent);

            // Position at backdropZPosition
            backdrop.transform.position = new Vector3(0, 0, backdropZPosition);

            // Make it huge to cover everything
            backdrop.transform.localScale = new Vector3(10000f, 10000f, 1f);

            // Rotate to face camera (pointing along -Z)
            backdrop.transform.rotation = Quaternion.identity;

            // Create black material
            Material blackMat = new Material(Shader.Find("Unlit/Color"));
            blackMat.color = Color.black;
            backdrop.GetComponent<Renderer>().material = blackMat;

            // Remove collider
            Destroy(backdrop.GetComponent<Collider>());
        }

        private System.Collections.IEnumerator SpawnDecorationsNextFrame()
        {
            yield return null; // Wait one frame

            SpawnDecorations();
        }

        private void SpawnDecorations()
        {
            if (mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("[EnvironmentDecorator] Maze grid is null!");
                return;
            }

            int decorationCount = 0;
            int mazeWidth = mazeGridBehaviour.Grid.Width;
            int mazeHeight = mazeGridBehaviour.Grid.Height;

            // Define background area: maze bounds + padding on all sides
            int startX = -backgroundPadding;
            int endX = mazeWidth + backgroundPadding;
            int startY = -backgroundPadding;
            int endY = mazeHeight + backgroundPadding;

            // Fill entire background area with trees, EXCEPT for maze tiles
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    // Check if this position is inside the maze grid bounds
                    bool isInsideMaze = (x >= 0 && x < mazeWidth && y >= 0 && y < mazeHeight);

                    // Only spawn trees OUTSIDE the maze area
                    if (!isInsideMaze)
                    {
                        Vector3 worldPos = mazeGridBehaviour.GridToWorld(x, y);
                        worldPos.z = zPosition;

                        // Rotate +90 on X axis so model up faces world -Z
                        // Add 180 degree Y rotation to flip direction
                        // Add small Z rotation variance (+/- degrees)
                        float zRotation = Random.Range(-zRotationVariance, zRotationVariance);
                        Quaternion rotation = Quaternion.Euler(90f, 180f, zRotation);

                        GameObject decoration = Instantiate(treePrefab, worldPos, rotation, decorationParent);
                        decoration.name = $"Tree_{x}_{y}";

                        // Scale to 0.90 on world Z axis (model's local Y after rotation)
                        decoration.transform.localScale = new Vector3(1f, 0.90f, 1f);

                        // Store decoration data for transparency management
                        DecorationData data = new DecorationData();
                        data.gameObject = decoration;
                        data.renderers = decoration.GetComponentsInChildren<Renderer>();

                        // Create material instances for each renderer
                        if (data.renderers.Length > 0)
                        {
                            data.originalMaterials = new Material[data.renderers.Length];
                            data.instanceMaterials = new Material[data.renderers.Length];

                            for (int i = 0; i < data.renderers.Length; i++)
                            {
                                data.originalMaterials[i] = data.renderers[i].sharedMaterial;
                                data.instanceMaterials[i] = new Material(data.renderers[i].sharedMaterial);
                                data.renderers[i].material = data.instanceMaterials[i];

                                // Enable transparency on the material
                                EnableTransparency(data.instanceMaterials[i]);
                            }
                        }

                        decorations.Add(data);
                        decorationCount++;
                    }
                }
            }

            Debug.Log($"[EnvironmentDecorator] Spawned {decorationCount} decorations");
        }

        private void Update()
        {
            UpdateTransparency();
        }

        /// <summary>
        /// Updates transparency of decorations based on distance from focal point.
        /// </summary>
        private void UpdateTransparency()
        {
            // Get focal point position
            Vector3 focalWorldPos;
            if (focalPoint != null)
            {
                focalWorldPos = focalPoint.position;
            }
            else
            {
                // Use main camera if no focal point specified
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera == null)
                    {
                        return; // No camera available
                    }
                }
                focalWorldPos = mainCamera.transform.position;
            }

            // Convert focal point to grid position
            int focalX, focalY;
            if (!mazeGridBehaviour.WorldToGrid(focalWorldPos, out focalX, out focalY))
            {
                return; // Invalid focal point position
            }
            Vector2Int focalGridPos = new Vector2Int(focalX, focalY);

            // Update each decoration's transparency
            foreach (var decoration in decorations)
            {
                if (decoration.gameObject == null)
                {
                    continue;
                }

                // Get decoration grid position
                Vector3 decorationWorldPos = decoration.gameObject.transform.position;
                int decorationX, decorationY;
                if (!mazeGridBehaviour.WorldToGrid(decorationWorldPos, out decorationX, out decorationY))
                {
                    continue; // Skip invalid positions
                }
                Vector2Int decorationGridPos = new Vector2Int(decorationX, decorationY);

                // Calculate distance in tiles (Manhattan distance)
                float distanceInTiles = Mathf.Abs(focalGridPos.x - decorationGridPos.x) +
                                       Mathf.Abs(focalGridPos.y - decorationGridPos.y);

                // Calculate alpha based on distance
                float alpha = CalculateAlpha(distanceInTiles);

                // Apply alpha to all renderers
                foreach (var material in decoration.instanceMaterials)
                {
                    if (material != null)
                    {
                        Color color = material.color;
                        color.a = alpha;
                        material.color = color;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates alpha value based on distance from focal point.
        /// </summary>
        private float CalculateAlpha(float distanceInTiles)
        {
            if (distanceInTiles <= transparencyRadius)
            {
                // Within radius - use transparent alpha
                return transparentAlpha;
            }
            else if (distanceInTiles <= transparencyRadius + transitionSmoothness)
            {
                // In transition zone - smooth interpolation
                float t = (distanceInTiles - transparencyRadius) / transitionSmoothness;
                return Mathf.Lerp(transparentAlpha, opaqueAlpha, t);
            }
            else
            {
                // Outside radius - fully opaque
                return opaqueAlpha;
            }
        }

        /// <summary>
        /// Enables transparency on a material by setting render mode to Fade.
        /// </summary>
        private void EnableTransparency(Material material)
        {
            // Set render mode to Fade (transparent)
            material.SetFloat("_Mode", 2);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        /// <summary>
        /// Clears all spawned decorations.
        /// </summary>
        public void ClearDecorations()
        {
            // Clean up material instances
            foreach (var decoration in decorations)
            {
                if (decoration.instanceMaterials != null)
                {
                    foreach (var material in decoration.instanceMaterials)
                    {
                        if (material != null)
                        {
                            Destroy(material);
                        }
                    }
                }
            }

            decorations.Clear();

            // Destroy game objects
            if (decorationParent != null)
            {
                foreach (Transform child in decorationParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Regenerates all decorations.
        /// </summary>
        public void RegenerateDecorations()
        {
            ClearDecorations();
            SpawnDecorations();
        }
    }
}

using UnityEngine;
using FaeMaze.Maze;
using System.Collections.Generic;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Spawns environment decoration (trees, walls) to fill the entire background plane,
    /// except for the area occupied by maze tiles. Makes decorations near focal point
    /// transparent for better visibility.
    ///
    /// Performance Optimizations:
    /// - GPU Instancing: Uses MaterialPropertyBlock to maintain instancing while setting per-object alpha
    /// - Reduced Decorations: Background padding reduced from 20 to 5 tiles (~75% fewer objects)
    /// - Frustum Culling: Only updates transparency for decorations near camera focus
    /// - Update Throttling: Updates transparency every N frames instead of every frame
    /// - Static Batching: Decorations marked as static when LOD is disabled
    /// - LOD Support: Optional LOD groups for distance-based quality levels
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

        [Header("LOD Settings (Optional)")]
        [SerializeField]
        [Tooltip("Enable LOD groups for distance-based quality")]
        private bool enableLOD = false;

        [SerializeField]
        [Tooltip("LOD0 prefab (high quality, close range) - leave null to use treePrefab")]
        private GameObject lod0Prefab;

        [SerializeField]
        [Tooltip("LOD1 prefab (medium quality) - leave null to skip")]
        private GameObject lod1Prefab;

        [SerializeField]
        [Tooltip("LOD2 prefab (low quality, far range) - leave null to skip")]
        private GameObject lod2Prefab;

        [SerializeField]
        [Tooltip("LOD0 transition at this percentage of max distance (0-1)")]
        private float lod0Distance = 0.3f;

        [SerializeField]
        [Tooltip("LOD1 transition at this percentage of max distance (0-1)")]
        private float lod1Distance = 0.6f;

        [SerializeField]
        [Tooltip("LOD2 culling at this percentage of max distance (0-1)")]
        private float lod2Distance = 1.0f;

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
        private int backgroundPadding = 5;

        [SerializeField]
        [Tooltip("Parent transform for spawned decorations")]
        private Transform decorationParent;

        [Header("Background")]
        [SerializeField]
        [Tooltip("Create black backdrop plane above the game")]
        private bool createBlackBackdrop = true;

        [SerializeField]
        [Tooltip("Z position for black backdrop plane (positive = above game)")]
        private float backdropZPosition = 1000f;

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

        [Header("Performance Settings")]
        [SerializeField]
        [Tooltip("Update transparency every N frames (1 = every frame, 3 = every 3rd frame)")]
        private int transparencyUpdateFrequency = 3;

        [SerializeField]
        [Tooltip("Mark decorations as static for static batching")]
        private bool markAsStatic = true;

        [SerializeField]
        [Tooltip("Camera frustum padding in tiles for transparency updates")]
        private float frustumPadding = 10f;

        private class DecorationData
        {
            public GameObject gameObject;
            public Renderer[] renderers;
            public MaterialPropertyBlock propertyBlock;
            public Vector2Int gridPosition;
        }

        private List<DecorationData> decorations = new List<DecorationData>();
        private Camera mainCamera;
        private int frameCounter = 0;
        private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");

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

            // Position at backdropZPosition (above game)
            backdrop.transform.position = new Vector3(0, 0, backdropZPosition);

            // Make it huge to cover everything
            backdrop.transform.localScale = new Vector3(10000f, 10000f, 1f);

            // Rotate 180° around Y to face down toward -Z (toward camera/game)
            backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

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

                        // Setup LOD if enabled
                        if (enableLOD)
                        {
                            SetupLODGroup(decoration);
                        }

                        // Mark as static for batching if enabled (only if LOD is disabled)
                        if (markAsStatic && !enableLOD)
                        {
                            decoration.isStatic = true;
                        }

                        // Store decoration data for transparency management
                        DecorationData data = new DecorationData();
                        data.gameObject = decoration;
                        data.renderers = decoration.GetComponentsInChildren<Renderer>();
                        data.gridPosition = new Vector2Int(x, y);
                        data.propertyBlock = new MaterialPropertyBlock();

                        // Enable GPU instancing on materials (uses shared materials)
                        if (data.renderers.Length > 0)
                        {
                            foreach (var renderer in data.renderers)
                            {
                                if (renderer.sharedMaterial != null)
                                {
                                    renderer.sharedMaterial.enableInstancing = true;
                                }
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
            // Only update transparency every N frames for performance
            frameCounter++;
            if (frameCounter >= transparencyUpdateFrequency)
            {
                frameCounter = 0;
                UpdateTransparency();
            }
        }

        /// <summary>
        /// Updates transparency of decorations based on distance from focal point.
        /// Uses frustum culling and MaterialPropertyBlocks for GPU instancing compatibility.
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

            // Calculate frustum bounds in grid coordinates (simple box approximation)
            int minX = Mathf.FloorToInt(focalGridPos.x - frustumPadding);
            int maxX = Mathf.CeilToInt(focalGridPos.x + frustumPadding);
            int minY = Mathf.FloorToInt(focalGridPos.y - frustumPadding);
            int maxY = Mathf.CeilToInt(focalGridPos.y + frustumPadding);

            // Update each decoration's transparency
            foreach (var decoration in decorations)
            {
                if (decoration.gameObject == null || decoration.renderers == null)
                {
                    continue;
                }

                // Frustum culling - skip decorations far from focus
                if (decoration.gridPosition.x < minX || decoration.gridPosition.x > maxX ||
                    decoration.gridPosition.y < minY || decoration.gridPosition.y > maxY)
                {
                    // Outside frustum - set to fully opaque and skip
                    SetDecorationAlpha(decoration, opaqueAlpha);
                    continue;
                }

                // Calculate distance in tiles (Manhattan distance) - use cached grid position
                float distanceInTiles = Mathf.Abs(focalGridPos.x - decoration.gridPosition.x) +
                                       Mathf.Abs(focalGridPos.y - decoration.gridPosition.y);

                // Calculate alpha based on distance
                float alpha = CalculateAlpha(distanceInTiles);

                // Apply alpha using MaterialPropertyBlock for GPU instancing
                SetDecorationAlpha(decoration, alpha);
            }
        }

        /// <summary>
        /// Sets the alpha for a decoration using MaterialPropertyBlock to maintain GPU instancing.
        /// </summary>
        private void SetDecorationAlpha(DecorationData decoration, float alpha)
        {
            if (decoration.propertyBlock == null || decoration.renderers == null)
            {
                return;
            }

            foreach (var renderer in decoration.renderers)
            {
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    // Get current color from shared material
                    Color color = renderer.sharedMaterial.color;
                    color.a = alpha;

                    // Set color via property block to maintain instancing
                    decoration.propertyBlock.SetColor(ColorPropertyID, color);
                    renderer.SetPropertyBlock(decoration.propertyBlock);
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
        /// Sets up LOD group on a decoration GameObject for distance-based quality levels.
        /// </summary>
        private void SetupLODGroup(GameObject decoration)
        {
            LODGroup lodGroup = decoration.AddComponent<LODGroup>();

            // Get renderers from current tree
            Renderer[] lod0Renderers = decoration.GetComponentsInChildren<Renderer>();

            // Build LOD array based on available prefabs
            List<LOD> lods = new List<LOD>();

            // LOD0 - High quality (use current renderers or instantiate lod0Prefab)
            if (lod0Prefab != null)
            {
                GameObject lod0Obj = Instantiate(lod0Prefab, decoration.transform);
                lod0Obj.transform.localPosition = Vector3.zero;
                lod0Obj.transform.localRotation = Quaternion.identity;
                lod0Obj.transform.localScale = Vector3.one;
                lod0Renderers = lod0Obj.GetComponentsInChildren<Renderer>();
            }
            lods.Add(new LOD(lod0Distance, lod0Renderers));

            // LOD1 - Medium quality
            if (lod1Prefab != null)
            {
                GameObject lod1Obj = Instantiate(lod1Prefab, decoration.transform);
                lod1Obj.transform.localPosition = Vector3.zero;
                lod1Obj.transform.localRotation = Quaternion.identity;
                lod1Obj.transform.localScale = Vector3.one;
                Renderer[] lod1Renderers = lod1Obj.GetComponentsInChildren<Renderer>();
                lods.Add(new LOD(lod1Distance, lod1Renderers));
            }

            // LOD2 - Low quality
            if (lod2Prefab != null)
            {
                GameObject lod2Obj = Instantiate(lod2Prefab, decoration.transform);
                lod2Obj.transform.localPosition = Vector3.zero;
                lod2Obj.transform.localRotation = Quaternion.identity;
                lod2Obj.transform.localScale = Vector3.one;
                Renderer[] lod2Renderers = lod2Obj.GetComponentsInChildren<Renderer>();
                lods.Add(new LOD(lod2Distance, lod2Renderers));
            }
            else
            {
                // Add culling level if no LOD2 specified
                lods.Add(new LOD(lod2Distance, new Renderer[0]));
            }

            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Clears all spawned decorations.
        /// </summary>
        public void ClearDecorations()
        {
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

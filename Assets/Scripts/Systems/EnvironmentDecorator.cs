using UnityEngine;
using System.Collections.Generic;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Spawns environment decoration (trees, walls) to fill the entire background plane.
    /// Uses world-space bounds from the maze for positioning.
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
        private bool enableLOD = false;

        [SerializeField]
        private GameObject lod0Prefab;

        [SerializeField]
        private GameObject lod1Prefab;

        [SerializeField]
        private GameObject lod2Prefab;

        [SerializeField]
        private float lod0Distance = 0.3f;

        [SerializeField]
        private float lod1Distance = 0.6f;

        [SerializeField]
        private float lod2Distance = 1.0f;

        [SerializeField]
        [Tooltip("Focal point for transparency")]
        private Transform focalPoint;

        [Header("Settings")]
        [SerializeField]
        private float zPosition = 0f;

        [SerializeField]
        [Tooltip("Padding around maze in world units")]
        private float backgroundPadding = 5f;

        [SerializeField]
        private Transform decorationParent;

        [Header("Background")]
        [SerializeField]
        private bool createBlackBackdrop = false; // Disabled - camera background color (#0a0a0a) handles this

        [SerializeField]
        private float backdropZPosition = 1000f;

        [Header("Transparency Settings")]
        [SerializeField]
        private float transparencyRadius = 3f;

        [SerializeField]
        private float transparentAlpha = 0.25f;

        [SerializeField]
        private float opaqueAlpha = 1f;

        [SerializeField]
        private float transitionSmoothness = 0.5f;

        [Header("Performance Settings")]
        [SerializeField]
        private int transparencyUpdateFrequency = 3;

        [SerializeField]
        private bool markAsStatic = true;

        [SerializeField]
        private float frustumPadding = 10f;

        private class DecorationData
        {
            public GameObject gameObject;
            public Renderer[] renderers;
            public MaterialPropertyBlock propertyBlock;
            public Vector3 worldPosition;
        }

        private List<DecorationData> decorations = new List<DecorationData>();
        private Camera mainCamera;
        private int frameCounter = 0;
        private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");

        private void Start()
        {
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (mazeGridBehaviour == null || treePrefab == null)
            {
                enabled = false;
                return;
            }

            if (decorationParent == null)
            {
                GameObject parentObj = new GameObject("Environment Decorations");
                decorationParent = parentObj.transform;
            }

            if (createBlackBackdrop)
            {
                CreateBlackBackdrop();
            }

            StartCoroutine(SpawnDecorationsNextFrame());
        }

        private void CreateBlackBackdrop()
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Black Backdrop";
            backdrop.transform.SetParent(decorationParent);
            backdrop.transform.position = new Vector3(0, 0, backdropZPosition);
            backdrop.transform.localScale = new Vector3(10000f, 10000f, 1f);
            backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            Material blackMat = new Material(Shader.Find("Unlit/Color"));
            blackMat.color = Color.black;
            backdrop.GetComponent<Renderer>().material = blackMat;
            Destroy(backdrop.GetComponent<Collider>());
        }

        private System.Collections.IEnumerator SpawnDecorationsNextFrame()
        {
            yield return null;
            SpawnDecorations();
        }

        private void SpawnDecorations()
        {
            if (mazeGridBehaviour.ForestMapState == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            int decorationCount = 0;
            var bounds = mazeGridBehaviour.WorldSpaceMazeData.Bounds;
            float tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            // Define background area: maze bounds + padding
            float startX = bounds.min.x - backgroundPadding;
            float endX = bounds.max.x + backgroundPadding;
            float startY = bounds.min.y - backgroundPadding;
            float endY = bounds.max.y + backgroundPadding;

            // Fill background area with trees outside maze bounds
            for (float y = startY; y < endY; y += tileSize)
            {
                for (float x = startX; x < endX; x += tileSize)
                {
                    // Check if position is inside maze bounds
                    bool isInsideMaze = (x >= bounds.min.x && x <= bounds.max.x &&
                                        y >= bounds.min.y && y <= bounds.max.y);

                    // Only spawn trees OUTSIDE the maze area
                    if (!isInsideMaze)
                    {
                        Vector3 worldPos = new Vector3(x, y, zPosition);

                        // Add random jitter
                        worldPos.x += RandomManager.Range(-0.02f, 0.02f);
                        worldPos.y += RandomManager.Range(-0.02f, 0.02f);

                        Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f);

                        GameObject decoration = Instantiate(treePrefab, worldPos, rotation, decorationParent);
                        decoration.name = $"Tree_{x:F0}_{y:F0}";
                        decoration.transform.localScale = new Vector3(tileSize * 0.65f, tileSize * 0.65f, tileSize);

                        if (enableLOD)
                        {
                            SetupLODGroup(decoration);
                        }

                        if (markAsStatic && !enableLOD)
                        {
                            decoration.isStatic = true;
                        }

                        DecorationData data = new DecorationData();
                        data.gameObject = decoration;
                        data.renderers = decoration.GetComponentsInChildren<Renderer>();
                        data.worldPosition = worldPos;
                        data.propertyBlock = new MaterialPropertyBlock();

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
        }

        private void Update()
        {
            frameCounter++;
            if (frameCounter >= transparencyUpdateFrequency)
            {
                frameCounter = 0;
                UpdateTransparency();
            }
        }

        private void UpdateTransparency()
        {
            Vector3 focalWorldPos;
            if (focalPoint != null)
            {
                focalWorldPos = focalPoint.position;
            }
            else
            {
                if (mainCamera == null)
                {
                    mainCamera = Camera.main;
                    if (mainCamera == null) return;
                }
                focalWorldPos = mainCamera.transform.position;
            }

            float worldFrustumPadding = frustumPadding * mazeGridBehaviour.WorldSpaceTileSize;

            foreach (var decoration in decorations)
            {
                if (decoration.gameObject == null || decoration.renderers == null)
                {
                    continue;
                }

                // Frustum culling
                if (Mathf.Abs(decoration.worldPosition.x - focalWorldPos.x) > worldFrustumPadding ||
                    Mathf.Abs(decoration.worldPosition.y - focalWorldPos.y) > worldFrustumPadding)
                {
                    SetDecorationAlpha(decoration, opaqueAlpha);
                    continue;
                }

                // Calculate distance in world units
                float distance = Vector2.Distance(
                    new Vector2(decoration.worldPosition.x, decoration.worldPosition.y),
                    new Vector2(focalWorldPos.x, focalWorldPos.y));

                float worldTransparencyRadius = transparencyRadius * mazeGridBehaviour.WorldSpaceTileSize;
                float alpha = CalculateAlpha(distance, worldTransparencyRadius);
                SetDecorationAlpha(decoration, alpha);
            }
        }

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
                    Color color = renderer.sharedMaterial.color;
                    color.a = alpha;
                    decoration.propertyBlock.SetColor(ColorPropertyID, color);
                    renderer.SetPropertyBlock(decoration.propertyBlock);
                }
            }
        }

        private float CalculateAlpha(float distance, float radius)
        {
            float worldSmoothness = transitionSmoothness * mazeGridBehaviour.WorldSpaceTileSize;

            if (distance <= radius)
            {
                return transparentAlpha;
            }
            else if (distance <= radius + worldSmoothness)
            {
                float t = (distance - radius) / worldSmoothness;
                return Mathf.Lerp(transparentAlpha, opaqueAlpha, t);
            }
            else
            {
                return opaqueAlpha;
            }
        }

        private void SetupLODGroup(GameObject decoration)
        {
            LODGroup lodGroup = decoration.AddComponent<LODGroup>();
            Renderer[] lod0Renderers = decoration.GetComponentsInChildren<Renderer>();
            List<LOD> lods = new List<LOD>();

            if (lod0Prefab != null)
            {
                GameObject lod0Obj = Instantiate(lod0Prefab, decoration.transform);
                lod0Obj.transform.localPosition = Vector3.zero;
                lod0Obj.transform.localRotation = Quaternion.identity;
                lod0Obj.transform.localScale = Vector3.one;
                lod0Renderers = lod0Obj.GetComponentsInChildren<Renderer>();
            }
            lods.Add(new LOD(lod0Distance, lod0Renderers));

            if (lod1Prefab != null)
            {
                GameObject lod1Obj = Instantiate(lod1Prefab, decoration.transform);
                lod1Obj.transform.localPosition = Vector3.zero;
                lod1Obj.transform.localRotation = Quaternion.identity;
                lod1Obj.transform.localScale = Vector3.one;
                lods.Add(new LOD(lod1Distance, lod1Obj.GetComponentsInChildren<Renderer>()));
            }

            if (lod2Prefab != null)
            {
                GameObject lod2Obj = Instantiate(lod2Prefab, decoration.transform);
                lod2Obj.transform.localPosition = Vector3.zero;
                lod2Obj.transform.localRotation = Quaternion.identity;
                lod2Obj.transform.localScale = Vector3.one;
                lods.Add(new LOD(lod2Distance, lod2Obj.GetComponentsInChildren<Renderer>()));
            }
            else
            {
                lods.Add(new LOD(lod2Distance, new Renderer[0]));
            }

            lodGroup.SetLODs(lods.ToArray());
            lodGroup.RecalculateBounds();
        }

        public void ClearDecorations()
        {
            decorations.Clear();
            if (decorationParent != null)
            {
                foreach (Transform child in decorationParent)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        public void RegenerateDecorations()
        {
            ClearDecorations();
            SpawnDecorations();
        }
    }
}

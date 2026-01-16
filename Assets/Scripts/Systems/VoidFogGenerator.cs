using UnityEngine;
using System.Collections.Generic;
using ForestMaze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Generates thick fog quads to fill the interior void spaces of the maze.
    /// These are areas inside the maze bounds but not on walkable paths.
    /// The fog makes wisp lights more dramatic as they illuminate it.
    /// </summary>
    public class VoidFogGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The maze grid behaviour to read void areas from")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Fog Appearance")]
        [SerializeField]
        [Tooltip("Color and opacity of the fog")]
        private Color fogColor = new Color(0.02f, 0.02f, 0.03f, 0.85f);

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("How much fog fades at edges")]
        private float edgeFade = 0.15f;

        [SerializeField]
        [Range(0.1f, 10f)]
        [Tooltip("Scale of noise pattern")]
        private float noiseScale = 2f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Speed of fog animation")]
        private float noiseSpeed = 0.1f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Strength of noise variation")]
        private float noiseStrength = 0.15f;

        [Header("Positioning")]
        [SerializeField]
        [Tooltip("Z position of fog (should be slightly in front of background)")]
        private float zPosition = 50f;

        [SerializeField]
        [Tooltip("Minimum size for a fog quad (in world units)")]
        private float minFogSize = 1f;

        [SerializeField]
        [Tooltip("Buffer distance from walkable paths")]
        private float pathBuffer = 0.5f;

        [Header("Performance")]
        [SerializeField]
        [Tooltip("Grid resolution for detecting void areas (lower = fewer fog quads)")]
        private float detectionGridSize = 2f;

        [SerializeField]
        [Tooltip("Merge nearby fog areas into larger quads")]
        private bool mergeFogAreas = true;

        private GameObject fogParent;
        private Material fogMaterial;
        private List<GameObject> fogQuads = new List<GameObject>();

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

            // Create parent object
            fogParent = new GameObject("VoidFog");
            fogParent.transform.SetParent(transform);

            // Create fog material
            CreateFogMaterial();

            // Wait for maze to be ready
            StartCoroutine(GenerateFogNextFrame());
        }

        private void CreateFogMaterial()
        {
            var shader = Shader.Find("Custom/VoidFog");
            if (shader == null)
            {
                // Fallback to standard transparent shader
                shader = Shader.Find("Sprites/Default");
            }

            fogMaterial = new Material(shader);
            UpdateMaterialProperties();
        }

        private void UpdateMaterialProperties()
        {
            if (fogMaterial == null) return;

            fogMaterial.SetColor("_FogColor", fogColor);
            fogMaterial.SetFloat("_EdgeFade", edgeFade);
            fogMaterial.SetFloat("_NoiseScale", noiseScale);
            fogMaterial.SetFloat("_NoiseSpeed", noiseSpeed);
            fogMaterial.SetFloat("_NoiseStrength", noiseStrength);
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
            var bounds = mazeData.Bounds;
            float tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            // Use a grid to detect void areas
            float gridSize = detectionGridSize * tileSize;

            // Collect all void cells
            List<Vector2> voidCells = new List<Vector2>();

            for (float y = bounds.min.y; y < bounds.max.y; y += gridSize)
            {
                for (float x = bounds.min.x; x < bounds.max.x; x += gridSize)
                {
                    Vector2 cellCenter = new Vector2(x + gridSize * 0.5f, y + gridSize * 0.5f);

                    // Check if this cell is a void (not walkable)
                    if (IsVoidArea(mazeData, cellCenter, gridSize * 0.5f))
                    {
                        voidCells.Add(new Vector2(x, y));
                    }
                }
            }

            if (mergeFogAreas)
            {
                // Merge adjacent void cells into larger rectangles
                var mergedAreas = MergeVoidCells(voidCells, gridSize);
                foreach (var area in mergedAreas)
                {
                    CreateFogQuad(area.center, area.size);
                }
            }
            else
            {
                // Create individual fog quads for each void cell
                foreach (var cell in voidCells)
                {
                    Vector2 center = cell + new Vector2(gridSize * 0.5f, gridSize * 0.5f);
                    CreateFogQuad(center, new Vector2(gridSize, gridSize));
                }
            }
        }

        private bool IsVoidArea(WorldSpaceMazeData mazeData, Vector2 position, float radius)
        {
            // Sample multiple points in the area to check if any are walkable
            Vector2[] checkPoints = new Vector2[]
            {
                position,
                position + new Vector2(radius * 0.5f, 0),
                position + new Vector2(-radius * 0.5f, 0),
                position + new Vector2(0, radius * 0.5f),
                position + new Vector2(0, -radius * 0.5f)
            };

            foreach (var point in checkPoints)
            {
                // If any check point is walkable, this is not a void area
                if (mazeData.IsWalkable(point))
                {
                    return false;
                }

                // Also check nearby tiles to maintain buffer from paths
                var nearbyTiles = mazeData.GetTilesNear(point, pathBuffer * mazeGridBehaviour.WorldSpaceTileSize);
                foreach (var tile in nearbyTiles)
                {
                    if (tile.Walkable)
                    {
                        return false; // Too close to a walkable tile
                    }
                }
            }

            return true;
        }

        private struct FogArea
        {
            public Vector2 center;
            public Vector2 size;
        }

        private List<FogArea> MergeVoidCells(List<Vector2> cells, float cellSize)
        {
            var areas = new List<FogArea>();
            var used = new HashSet<int>();

            for (int i = 0; i < cells.Count; i++)
            {
                if (used.Contains(i)) continue;

                Vector2 minCorner = cells[i];
                Vector2 maxCorner = cells[i] + new Vector2(cellSize, cellSize);

                // Try to expand in X direction
                bool expanded = true;
                while (expanded)
                {
                    expanded = false;

                    // Try to expand right
                    for (int j = 0; j < cells.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        if (Mathf.Abs(cells[j].x - maxCorner.x) < 0.01f &&
                            cells[j].y >= minCorner.y - 0.01f &&
                            cells[j].y + cellSize <= maxCorner.y + 0.01f)
                        {
                            maxCorner.x = cells[j].x + cellSize;
                            used.Add(j);
                            expanded = true;
                        }
                    }

                    // Try to expand up
                    for (int j = 0; j < cells.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        if (Mathf.Abs(cells[j].y - maxCorner.y) < 0.01f &&
                            cells[j].x >= minCorner.x - 0.01f &&
                            cells[j].x + cellSize <= maxCorner.x + 0.01f)
                        {
                            maxCorner.y = cells[j].y + cellSize;
                            used.Add(j);
                            expanded = true;
                        }
                    }
                }

                used.Add(i);

                Vector2 size = maxCorner - minCorner;
                if (size.x >= minFogSize && size.y >= minFogSize)
                {
                    areas.Add(new FogArea
                    {
                        center = (minCorner + maxCorner) * 0.5f,
                        size = size
                    });
                }
            }

            return areas;
        }

        private void CreateFogQuad(Vector2 center, Vector2 size)
        {
            GameObject fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogQuad.name = $"VoidFog_{center.x:F0}_{center.y:F0}";
            fogQuad.transform.SetParent(fogParent.transform);

            // Position the quad
            fogQuad.transform.position = new Vector3(center.x, center.y, zPosition);
            fogQuad.transform.localScale = new Vector3(size.x, size.y, 1f);
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

            fogQuads.Add(fogQuad);
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
            foreach (var quad in fogQuads)
            {
                if (quad != null)
                {
                    Destroy(quad);
                }
            }
            fogQuads.Clear();
        }

        public void RegenerateFog()
        {
            ClearFog();
            GenerateVoidFog();
        }

        private void OnDestroy()
        {
            ClearFog();
            if (fogMaterial != null)
            {
                Destroy(fogMaterial);
            }
            if (fogParent != null)
            {
                Destroy(fogParent);
            }
        }
    }
}

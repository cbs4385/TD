using System.Collections.Generic;
using UnityEngine;
using ForestMaze;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// DEPRECATED: This renderer is superseded by MazeRenderer for visual output.
    /// Previously rendered world-space maze using basic shapes (LineRenderers for edges,
    /// SpriteRenderers for nodes). MazeRenderer now handles all visual rendering with
    /// 3D cylinders for nodes and textured path tiles.
    /// This component may be removed in a future update.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class WorldSpaceMazeRenderer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Prefab Settings")]
        [SerializeField]
        [Tooltip("Prefab/model for wall tiles (trees/brambles)")]
        private GameObject wallPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for path tiles (unused in shape mode)")]
        private GameObject pathPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for node hazards (placed at clearing centers)")]
        private GameObject nodeHazardPrefab;

        [Header("Color Settings")]
        [SerializeField]
        [Tooltip("Color for walkable path/edges")]
        private Color pathColor = Color.white;

        [SerializeField]
        [Tooltip("Color tint for wall tiles")]
        private Color wallColor = Color.black;

        [SerializeField]
        [Tooltip("Color for the heart node")]
        private Color heartColor = new Color(0.9f, 0.35f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Color for node circles")]
        private Color nodeColor = Color.white;

        [Header("Size Settings")]
        [SerializeField]
        [Tooltip("World-space size of a single tile (used for walls and data)")]
        private float tileSize = 1.0f;

        [SerializeField]
        [Tooltip("Tile height (Z thickness for walls)")]
        private float tileHeight = 0.1f;

        [Header("Shape Rendering Settings")]
        [SerializeField]
        [Tooltip("Width of edge lines (LineRenderer width) - should match tile coverage area")]
        private float edgeWidth = 3.5f;

        [SerializeField]
        [Tooltip("Radius of node circles - slightly larger than tile coverage to fill gaps")]
        private float nodeRadius = 4.5f;

        [SerializeField]
        [Tooltip("Z level for path/node visuals (negative = in front)")]
        private float visualZLevel = -0.1f;

        [SerializeField]
        [Tooltip("Negative padding on wall positions to close gaps with path shapes")]
        private float wallPadding = 0.125f;

        [Header("Container Settings")]
        [SerializeField]
        [Tooltip("Parent transform to hold all rendered objects")]
        private Transform tilesParent;

        [Header("Optimization Settings")]
        [SerializeField]
        [Tooltip("Enable mesh batching for wall tiles")]
        private bool enableMeshBatching = true;

        [SerializeField]
        [Tooltip("Maximum tiles per batch")]
        private int batchChunkSize = 100;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private WorldSpaceMazeData mazeData;
        private GameObject tilesContainer;

        // Wall tiles (still rendered as individual tiles)
        private List<GameObject> wallTiles;

        // Shape-based rendering objects
        private List<LineRenderer> edgeLineRenderers = new List<LineRenderer>();
        private List<SpriteRenderer> nodeCircleRenderers = new List<SpriteRenderer>();
        private GameObject shapesContainer;

        // Cached circle sprite for nodes
        private Sprite circleSprite;

        // Shared material for edges
        private Material edgeMaterial;

        #endregion

        #region Properties

        /// <summary>Gets the world-space maze data.</summary>
        public WorldSpaceMazeData MazeData => mazeData;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            pathColor = Color.white;
            wallColor = Color.black;
        }

        private void Start()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Check if we have a valid forest map state
            var forestState = mazeGridBehaviour.ForestMapState;
            if (forestState == null)
            {
                return;
            }

            // Generate world-space maze data from the graph
            GenerateMazeData(forestState);

            // Render the maze
            RenderMaze();
        }

        #endregion

        #region Generation

        /// <summary>
        /// Generates world-space maze data from a planar forest graph state.
        /// </summary>
        public void GenerateMazeData(PlanarForestMazeGenerator.ForestMapState forestState)
        {
            WorldSpaceMazeGenerator.ResetSpawnIdCounter();
            mazeData = WorldSpaceMazeGenerator.GenerateFromGraph(forestState, tileSize);
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Renders the maze using shape-based rendering.
        /// Edges are drawn as LineRenderers, nodes as circle sprites.
        /// Walls are still rendered as individual tiles.
        /// </summary>
        public void RenderMaze()
        {
            if (mazeData == null)
            {
                return;
            }

            Transform mazeOrigin = mazeGridBehaviour != null ? mazeGridBehaviour.MazeOrigin : transform;

            // Create container for all rendered objects
            if (tilesParent == null)
            {
                tilesContainer = new GameObject("WorldSpaceMazeTiles");
                tilesContainer.transform.SetParent(mazeOrigin, worldPositionStays: false);
                tilesContainer.transform.localPosition = Vector3.zero;
                tilesContainer.transform.localRotation = Quaternion.identity;
                tilesContainer.transform.localScale = Vector3.one;
                tilesParent = tilesContainer.transform;
            }

            // Create shapes container for edges and nodes
            shapesContainer = new GameObject("MazeShapes");
            shapesContainer.transform.SetParent(tilesParent);
            shapesContainer.transform.localPosition = Vector3.zero;
            shapesContainer.transform.localRotation = Quaternion.identity;
            shapesContainer.transform.localScale = Vector3.one;

            // Initialize lists
            wallTiles = new List<GameObject>();
            edgeLineRenderers.Clear();
            nodeCircleRenderers.Clear();

            // Create shared resources
            CreateSharedResources();

            // Render edges first (lower layer)
            RenderEdges(mazeOrigin);

            // Render nodes on top of edges (higher layer to cover edge endpoints cleanly)
            RenderNodes(mazeOrigin);

            // Render wall tiles (still use individual tiles for walls)
            RenderWalls(mazeOrigin);

            // Batch wall tiles
            if (enableMeshBatching && wallTiles.Count > 0)
            {
                MeshBatcher.BatchInChunks(wallTiles, tilesParent, batchChunkSize, destroyOriginals: true);
            }
        }

        /// <summary>
        /// Creates shared resources (materials, sprites) for rendering.
        /// </summary>
        private void CreateSharedResources()
        {
            // Create circle sprite for nodes
            if (circleSprite == null)
            {
                circleSprite = CreateFilledCircleSprite();
            }

            // Create material for edges
            if (edgeMaterial == null)
            {
                edgeMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        /// <summary>
        /// Renders all edges using LineRenderers following polyline points.
        /// </summary>
        private void RenderEdges(Transform mazeOrigin)
        {
            var graphState = mazeData.GraphState;
            if (graphState == null) return;

            for (int edgeIndex = 0; edgeIndex < graphState.Edges.Count; edgeIndex++)
            {
                var edge = graphState.Edges[edgeIndex];
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                GameObject edgeObj = new GameObject($"Edge_{edgeIndex}");
                edgeObj.transform.SetParent(shapesContainer.transform);

                LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
                lr.positionCount = edge.PolylinePoints.Count;
                lr.startWidth = edgeWidth;
                lr.endWidth = edgeWidth;
                lr.material = new Material(edgeMaterial);
                lr.startColor = pathColor;
                lr.endColor = pathColor;
                lr.sortingLayerName = "Default";
                lr.sortingOrder = 0; // Below nodes
                lr.useWorldSpace = true;
                lr.numCapVertices = 4; // Rounded caps
                lr.numCornerVertices = 4; // Smooth corners

                for (int i = 0; i < edge.PolylinePoints.Count; i++)
                {
                    Vector3 pos = mazeOrigin.position + new Vector3(
                        edge.PolylinePoints[i].x,
                        edge.PolylinePoints[i].y,
                        visualZLevel
                    );
                    lr.SetPosition(i, pos);
                }

                edgeLineRenderers.Add(lr);
            }
        }

        /// <summary>
        /// Renders all nodes as filled circle sprites.
        /// </summary>
        private void RenderNodes(Transform mazeOrigin)
        {
            var graphState = mazeData.GraphState;
            if (graphState == null) return;

            for (int nodeIndex = 0; nodeIndex < graphState.Nodes.Count; nodeIndex++)
            {
                var node = graphState.Nodes[nodeIndex];

                GameObject nodeObj = new GameObject($"Node_{nodeIndex}");
                nodeObj.transform.SetParent(shapesContainer.transform);

                // Put nodes slightly in front of edges (more negative Z) to cover edge endpoints
                Vector3 nodePos = mazeOrigin.position + new Vector3(
                    node.Position.x,
                    node.Position.y,
                    visualZLevel - 0.01f
                );
                nodeObj.transform.position = nodePos;
                nodeObj.transform.localScale = Vector3.one * nodeRadius * 2f;

                SpriteRenderer sr = nodeObj.AddComponent<SpriteRenderer>();
                sr.sprite = circleSprite;
                sr.color = node.Kind == "root" ? heartColor : nodeColor;
                sr.sortingLayerName = "Default";
                sr.sortingOrder = 1; // Above edges to cover endpoints cleanly

                nodeCircleRenderers.Add(sr);
            }
        }

        /// <summary>
        /// Renders wall tiles using the existing tile-based approach.
        /// </summary>
        private void RenderWalls(Transform mazeOrigin)
        {
            foreach (var tile in mazeData.Tiles)
            {
                if (tile.Category != WorldSpaceTile.TileCategory.Wall) continue;

                CreateWallTile(tile, mazeOrigin);
            }
        }

        /// <summary>
        /// Creates a wall tile at the specified position.
        /// </summary>
        private void CreateWallTile(WorldSpaceTile tile, Transform mazeOrigin)
        {
            // Apply negative padding: shift wall toward path using orientation normal
            // Orientation points outward from path, so we move opposite direction (inward)
            Vector2 inwardDir = new Vector2(Mathf.Cos(tile.Orientation), Mathf.Sin(tile.Orientation));
            Vector2 paddedPosition = tile.Position - inwardDir * wallPadding;

            Vector3 worldPos = mazeOrigin.position + new Vector3(paddedPosition.x, paddedPosition.y, 0);

            Quaternion baseRotation = Quaternion.Euler(-90f, 0f, 0f);
            float orientationDegrees = tile.Orientation * Mathf.Rad2Deg;
            Quaternion tileRotation = Quaternion.Euler(0f, 0f, orientationDegrees) * baseRotation;

            GameObject tileObj;

            if (wallPrefab != null)
            {
                tileObj = Instantiate(wallPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = tileRotation;
                tileObj.transform.localScale = new Vector3(tile.Size * 0.65f, tile.Size * 0.65f, tile.Size);
            }
            else
            {
                tileObj = CreateProceduralWallTile(tile, wallColor);
                tileObj.transform.SetParent(tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = tileRotation;
            }

            tileObj.name = $"Wall_{tile.Position.x:F1}_{tile.Position.y:F1}";
            wallTiles.Add(tileObj);
        }

        /// <summary>
        /// Creates a procedural wall tile.
        /// </summary>
        private GameObject CreateProceduralWallTile(WorldSpaceTile tile, Color color)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.name = $"Wall_{tile.Symbol}";
            tileObj.transform.localScale = new Vector3(tile.Size, tile.Size, tileHeight);

            Material material = PBRMaterialFactory.CreateWallMaterial(color);
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            return tileObj;
        }

        /// <summary>
        /// Creates a filled circle sprite for node rendering.
        /// </summary>
        private Sprite CreateFilledCircleSprite()
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

                    // Create soft-edged filled circle
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

        #region Public Methods

        /// <summary>
        /// Forces a complete re-render of the maze.
        /// </summary>
        public void RefreshMaze()
        {
            // Clear existing rendered objects
            ClearRenderedObjects();

            // Regenerate if we have a forest state
            var forestState = mazeGridBehaviour?.ForestMapState;
            if (forestState != null)
            {
                GenerateMazeData(forestState);
            }

            // Re-render
            RenderMaze();
        }

        /// <summary>
        /// Clears all rendered objects (shapes and walls).
        /// </summary>
        private void ClearRenderedObjects()
        {
            // Clear shape-based objects
            edgeLineRenderers.Clear();
            nodeCircleRenderers.Clear();

            if (shapesContainer != null)
            {
                Destroy(shapesContainer);
                shapesContainer = null;
            }

            // Clear all children of tiles parent
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                {
                    Destroy(child.gameObject);
                }
            }

            wallTiles?.Clear();
        }

        /// <summary>
        /// Adds a new edge to the visual rendering (for dynamic growth).
        /// </summary>
        public void AddEdgeVisual(int edgeIndex)
        {
            if (mazeData?.GraphState == null || shapesContainer == null) return;
            if (edgeIndex < 0 || edgeIndex >= mazeData.GraphState.Edges.Count) return;

            var edge = mazeData.GraphState.Edges[edgeIndex];
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) return;

            Transform mazeOrigin = mazeGridBehaviour != null ? mazeGridBehaviour.MazeOrigin : transform;

            GameObject edgeObj = new GameObject($"Edge_{edgeIndex}");
            edgeObj.transform.SetParent(shapesContainer.transform);

            LineRenderer lr = edgeObj.AddComponent<LineRenderer>();
            lr.positionCount = edge.PolylinePoints.Count;
            lr.startWidth = edgeWidth;
            lr.endWidth = edgeWidth;
            lr.material = new Material(edgeMaterial);
            lr.startColor = pathColor;
            lr.endColor = pathColor;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 0; // Below nodes
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            for (int i = 0; i < edge.PolylinePoints.Count; i++)
            {
                Vector3 pos = mazeOrigin.position + new Vector3(
                    edge.PolylinePoints[i].x,
                    edge.PolylinePoints[i].y,
                    visualZLevel
                );
                lr.SetPosition(i, pos);
            }

            edgeLineRenderers.Add(lr);
        }

        /// <summary>
        /// Adds a new node to the visual rendering (for dynamic growth).
        /// </summary>
        public void AddNodeVisual(int nodeIndex)
        {
            if (mazeData?.GraphState == null || shapesContainer == null) return;
            if (nodeIndex < 0 || nodeIndex >= mazeData.GraphState.Nodes.Count) return;

            var node = mazeData.GraphState.Nodes[nodeIndex];
            Transform mazeOrigin = mazeGridBehaviour != null ? mazeGridBehaviour.MazeOrigin : transform;

            GameObject nodeObj = new GameObject($"Node_{nodeIndex}");
            nodeObj.transform.SetParent(shapesContainer.transform);

            Vector3 nodePos = mazeOrigin.position + new Vector3(
                node.Position.x,
                node.Position.y,
                visualZLevel
            );
            nodeObj.transform.position = nodePos;
            nodeObj.transform.localScale = Vector3.one * nodeRadius * 2f;

            SpriteRenderer sr = nodeObj.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite;
            sr.color = node.Kind == "root" ? heartColor : nodeColor;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 1; // Above edges

            nodeCircleRenderers.Add(sr);
        }

        /// <summary>
        /// Updates an existing edge's polyline points (for dynamic growth).
        /// </summary>
        public void UpdateEdgeVisual(int edgeIndex)
        {
            if (mazeData?.GraphState == null) return;
            if (edgeIndex < 0 || edgeIndex >= mazeData.GraphState.Edges.Count) return;
            if (edgeIndex >= edgeLineRenderers.Count) return;

            var edge = mazeData.GraphState.Edges[edgeIndex];
            var lr = edgeLineRenderers[edgeIndex];
            if (lr == null || edge.PolylinePoints == null) return;

            Transform mazeOrigin = mazeGridBehaviour != null ? mazeGridBehaviour.MazeOrigin : transform;

            lr.positionCount = edge.PolylinePoints.Count;
            for (int i = 0; i < edge.PolylinePoints.Count; i++)
            {
                Vector3 pos = mazeOrigin.position + new Vector3(
                    edge.PolylinePoints[i].x,
                    edge.PolylinePoints[i].y,
                    visualZLevel
                );
                lr.SetPosition(i, pos);
            }
        }

        /// <summary>
        /// Sets the wall prefab used for rendering walls.
        /// </summary>
        public void SetWallPrefab(GameObject prefab)
        {
            wallPrefab = prefab;
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using ForestMaze;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Renders a world-space maze using continuous coordinates and oriented tiles.
    /// Unlike the grid-based MazeRenderer, this renderer positions and rotates
    /// tiles according to their world-space data.
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
        [Tooltip("Prefab/model for path tiles")]
        private GameObject pathPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for node hazards (placed at clearing centers)")]
        private GameObject nodeHazardPrefab;

        [Header("Color Settings")]
        [SerializeField]
        [Tooltip("Color for walkable path tiles")]
        private Color pathColor = Color.white;

        [SerializeField]
        [Tooltip("Color tint for wall tiles")]
        private Color wallColor = Color.black;

        [SerializeField]
        [Tooltip("Color for the heart tile")]
        private Color heartColor = new Color(0.9f, 0.35f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Color for node tiles")]
        private Color nodeColor = Color.white;

        [Header("Size Settings")]
        [SerializeField]
        [Tooltip("World-space size of a single tile")]
        private float tileSize = 1.0f;

        [SerializeField]
        [Tooltip("Tile height (Z thickness)")]
        private float tileHeight = 0.1f;

        [Header("Container Settings")]
        [SerializeField]
        [Tooltip("Parent transform to hold all tile objects")]
        private Transform tilesParent;

        [Header("Optimization Settings")]
        [SerializeField]
        [Tooltip("Enable mesh batching to combine tiles")]
        private bool enableMeshBatching = true;

        [SerializeField]
        [Tooltip("Maximum tiles per batch")]
        private int batchChunkSize = 100;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private WorldSpaceMazeData mazeData;
        private GameObject tilesContainer;

        private List<GameObject> wallTiles;
        private List<GameObject> pathTiles;
        private List<GameObject> nodeTiles;

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
        /// Renders all tiles in the world-space maze.
        /// </summary>
        public void RenderMaze()
        {
            if (mazeData == null || mazeData.Tiles.Count == 0)
            {
                return;
            }

            Transform mazeOrigin = mazeGridBehaviour != null ? mazeGridBehaviour.MazeOrigin : transform;

            // Create container for tiles
            if (tilesParent == null)
            {
                tilesContainer = new GameObject("WorldSpaceMazeTiles");
                tilesContainer.transform.SetParent(mazeOrigin, worldPositionStays: false);
                tilesContainer.transform.localPosition = Vector3.zero;
                tilesContainer.transform.localRotation = Quaternion.identity;
                tilesContainer.transform.localScale = Vector3.one;
                tilesParent = tilesContainer.transform;
            }

            // Initialize batching lists
            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
                nodeTiles = new List<GameObject>();
            }

            // Render each tile
            int renderedCount = 0;
            foreach (var tile in mazeData.Tiles)
            {
                CreateTile3D(tile, mazeOrigin);
                renderedCount++;
            }

            // Perform batching
            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
        }

        /// <summary>
        /// Creates a 3D tile at the specified world-space position with proper orientation.
        /// </summary>
        private void CreateTile3D(WorldSpaceTile tile, Transform mazeOrigin)
        {
            // Convert 2D world position to 3D (X, Y plane with Z as height)
            Vector3 worldPos = mazeOrigin.position + new Vector3(tile.Position.x, tile.Position.y, 0);

            // Calculate rotation from tile orientation
            // The tile orientation is in radians, we convert to a quaternion
            // The base rotation aligns the tile to the XY plane (facing -Z)
            Quaternion baseRotation = Quaternion.Euler(-90f, 0f, 0f);
            // Apply the tile's orientation around the Z axis (which becomes up after base rotation)
            float orientationDegrees = tile.Orientation * Mathf.Rad2Deg;
            Quaternion tileRotation = Quaternion.Euler(0f, 0f, orientationDegrees) * baseRotation;

            GameObject tileObj = null;

            // Determine prefab and color based on tile category
            switch (tile.Category)
            {
                case WorldSpaceTile.TileCategory.Wall:
                    if (wallPrefab != null)
                    {
                        tileObj = Instantiate(wallPrefab, tilesParent);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = tileRotation;
                        tileObj.transform.localScale = new Vector3(tile.Size * 0.65f, tile.Size * 0.65f, tile.Size);
                    }
                    else
                    {
                        tileObj = CreateProceduralTile(tile, wallColor);
                        tileObj.transform.SetParent(tilesParent);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = tileRotation;
                    }
                    wallTiles?.Add(tileObj);
                    break;

                case WorldSpaceTile.TileCategory.Path:
                    Color pathTileColor = tile.Symbol == 'H' ? heartColor : pathColor;
                    if (pathPrefab != null)
                    {
                        tileObj = Instantiate(pathPrefab, tilesParent);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = tileRotation;
                        tileObj.transform.localScale = Vector3.one * tile.Size;
                    }
                    else
                    {
                        tileObj = CreateProceduralTile(tile, pathTileColor);
                        tileObj.transform.SetParent(tilesParent);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = tileRotation;
                    }
                    pathTiles?.Add(tileObj);
                    break;

                case WorldSpaceTile.TileCategory.Node:
                    Color nodeTileColor = nodeColor;
                    if (tile.Symbol == 'H')
                    {
                        nodeTileColor = heartColor;
                    }
                    else if (tile.Symbol == 'N' && nodeHazardPrefab != null)
                    {
                        // Create path base
                        var pathBase = CreateProceduralTile(tile, nodeColor);
                        pathBase.transform.SetParent(tilesParent);
                        pathBase.transform.position = worldPos;
                        pathBase.transform.rotation = tileRotation;
                        nodeTiles?.Add(pathBase);

                        // Add node hazard on top
                        var hazard = Instantiate(nodeHazardPrefab, tilesParent);
                        hazard.transform.position = worldPos;
                        hazard.transform.rotation = tileRotation;
                        hazard.transform.localScale = Vector3.one * tile.Size;
                        tileObj = hazard;
                    }
                    else
                    {
                        tileObj = CreateProceduralTile(tile, nodeTileColor);
                        tileObj.transform.SetParent(tilesParent);
                        tileObj.transform.position = worldPos;
                        tileObj.transform.rotation = tileRotation;
                    }
                    nodeTiles?.Add(tileObj);
                    break;
            }

            if (tileObj != null)
            {
                tileObj.name = $"WorldTile_{tile.Category}_{tile.Position.x:F1}_{tile.Position.y:F1}";
            }
        }

        /// <summary>
        /// Creates a procedural 3D mesh tile with the specified color.
        /// </summary>
        private GameObject CreateProceduralTile(WorldSpaceTile tile, Color color)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.name = $"Tile_{tile.Category}_{tile.Symbol}";

            // Scale: width and height match tile size, thin in depth
            tileObj.transform.localScale = new Vector3(tile.Size, tile.Size, tileHeight);

            // Create material
            Material material = CreateMaterialForTile(tile, color);
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            return tileObj;
        }

        /// <summary>
        /// Creates an appropriate material for the tile.
        /// </summary>
        private Material CreateMaterialForTile(WorldSpaceTile tile, Color color)
        {
            switch (tile.Category)
            {
                case WorldSpaceTile.TileCategory.Wall:
                    return PBRMaterialFactory.CreateWallMaterial(color);

                case WorldSpaceTile.TileCategory.Path:
                    if (tile.Symbol == 'H')
                    {
                        return PBRMaterialFactory.CreateEmissiveMaterial(color, color * 1.5f, 1.0f);
                    }
                    return PBRMaterialFactory.CreatePathMaterial(color);

                case WorldSpaceTile.TileCategory.Node:
                    if (tile.Symbol == 'H')
                    {
                        return PBRMaterialFactory.CreateEmissiveMaterial(color, color * 1.5f, 1.0f);
                    }
                    return PBRMaterialFactory.CreatePathMaterial(color);

                default:
                    return PBRMaterialFactory.CreateLitMaterial(color);
            }
        }

        /// <summary>
        /// Performs mesh batching to reduce draw calls.
        /// </summary>
        private void PerformMeshBatching()
        {
            int totalBatches = 0;

            if (wallTiles != null && wallTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(wallTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            if (pathTiles != null && pathTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(pathTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            if (nodeTiles != null && nodeTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(nodeTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Forces a complete re-render of the maze.
        /// </summary>
        public void RefreshMaze()
        {
            // Clear existing tiles
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                {
                    Destroy(child.gameObject);
                }
            }

            // Regenerate if we have a forest state
            var forestState = mazeGridBehaviour?.ForestMapState;
            if (forestState != null)
            {
                GenerateMazeData(forestState);
            }

            // Re-render
            RenderMaze();
        }

        #endregion
    }
}

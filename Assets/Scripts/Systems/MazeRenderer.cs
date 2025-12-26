using UnityEngine;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Renders the maze grid visually using 3D meshes and prefabs.
    /// Creates a visual representation of walls and pathways.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class MazeRenderer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Prefab Settings")]
        [SerializeField]
        [Tooltip("Prefab/model for wall tiles (trees/brambles)")]
        private GameObject wallPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for undergrowth tiles")]
        private GameObject undergrowthPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for water tiles")]
        private GameObject waterPrefab;

        [Header("Color Settings")]
        [SerializeField]
        [Tooltip("Color for walkable path tiles")]
        private Color pathColor = Color.white;

        [SerializeField]
        [Tooltip("Color tint for wall tiles (used when prefab not available)")]
        private Color wallColor = Color.black;

        [SerializeField]
        [Tooltip("Color for undergrowth tiles (used when prefab not available)")]
        private Color undergrowthColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple

        [SerializeField]
        [Tooltip("Color for water tiles (used when prefab not available)")]
        private Color waterColor = Color.magenta;

        [SerializeField]
        [Tooltip("Color for the heart tile")]
        private Color heartColor = new Color(0.9f, 0.35f, 0.35f, 1f);

        [Header("Container Settings")]
        [SerializeField]
        [Tooltip("Parent transform to hold all tile objects")]
        private Transform tilesParent;

        [Header("Optimization Settings")]
        [SerializeField]
        [Tooltip("Enable mesh batching to combine tiles and reduce draw calls")]
        private bool enableMeshBatching = true;

        [SerializeField]
        [Tooltip("Maximum tiles per batch (to avoid meshes that are too large)")]
        private int batchChunkSize = 100;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private GameObject tilesContainer;

        // Batching collections
        private System.Collections.Generic.List<GameObject> wallTiles;
        private System.Collections.Generic.List<GameObject> undergrowthTiles;
        private System.Collections.Generic.List<GameObject> waterTiles;
        private System.Collections.Generic.List<GameObject> pathTiles;

        #endregion

        #region Public API

        /// <summary>
        /// Indicates whether a wall prefab has been assigned.
        /// </summary>
        public bool HasWallPrefab => wallPrefab != null;

        /// <summary>
        /// Assigns the wall prefab to use when rendering wall tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for walls.</param>
        public void SetWallPrefab(GameObject prefab)
        {
            wallPrefab = prefab;
        }

        /// <summary>
        /// Indicates whether an undergrowth prefab has been assigned.
        /// </summary>
        public bool HasUndergrowthPrefab => undergrowthPrefab != null;

        /// <summary>
        /// Assigns the undergrowth prefab to use when rendering undergrowth tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for undergrowth.</param>
        public void SetUndergrowthPrefab(GameObject prefab)
        {
            undergrowthPrefab = prefab;
        }

        /// <summary>
        /// Indicates whether a water prefab has been assigned.
        /// </summary>
        public bool HasWaterPrefab => waterPrefab != null;

        /// <summary>
        /// Assigns the water prefab to use when rendering water tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for water.</param>
        public void SetWaterPrefab(GameObject prefab)
        {
            waterPrefab = prefab;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Force color values to new defaults (overrides serialized values)
            pathColor = Color.saddleBrown;
            //wallColor = Color.black;
            //undergrowthColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple
            waterColor = Color.magenta;
        }

        private void Start()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                return;
            }

            RenderMaze();
        }

        #endregion

        #region Rendering

        private void RenderMaze()
        {
            if (mazeGridBehaviour.Grid == null)
            {
                return;
            }

            // Log prefab status for debugging
            if (wallPrefab == null)
            {
                // Walls will use procedural meshes when the prefab is not assigned.
            }

            if (undergrowthPrefab == null)
            {
                // Undergrowth will use procedural meshes when the prefab is not assigned.
            }

            if (waterPrefab == null)
            {
                // Water will use procedural meshes when the prefab is not assigned.
            }

            // Create container for tiles if not assigned
            if (tilesParent == null)
            {
                tilesContainer = new GameObject("MazeTiles");
                tilesContainer.transform.SetParent(transform.parent); // Set parent to same level as MazeOrigin
                tilesContainer.transform.position = mazeGridBehaviour.transform.position; // Align with MazeOrigin
                tilesParent = tilesContainer.transform;
            }

            // Initialize batching lists if batching is enabled
            if (enableMeshBatching)
            {
                wallTiles = new System.Collections.Generic.List<GameObject>();
                undergrowthTiles = new System.Collections.Generic.List<GameObject>();
                waterTiles = new System.Collections.Generic.List<GameObject>();
                pathTiles = new System.Collections.Generic.List<GameObject>();
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            int width = grid.Width;
            int height = grid.Height;

            int renderedTiles = 0;

            // Create a 3D tile for each grid cell
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node == null) continue;

                    // Determine color based on tile symbol definitions
                    Color tileColor = GetColorForSymbol(node.symbol, node.walkable);

                    // Create 3D tile
                    CreateTile3D(x, y, node.symbol, tileColor);
                    renderedTiles++;
                }
            }

            // Perform batching after all tiles are created
            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }

        }

        private void CreateTile3D(int gridX, int gridY, char symbol, Color color)
        {
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);
            float tileSize = mazeGridBehaviour.TileSize;

            // Determine if we should use prefabs
            bool useWallPrefab = symbol == '#' && wallPrefab != null;
            bool useUndergrowthPrefab = symbol == ';' && undergrowthPrefab != null;
            bool useWaterPrefab = symbol == '~' && waterPrefab != null;

            // Add random jitter for wall, undergrowth, and water tiles
            if (symbol == '#' || symbol == ';' || symbol == '~')
            {
                float jitterX = Random.Range(-0.02f, 0.02f);
                float jitterY = Random.Range(-0.02f, 0.02f);
                worldPos += new Vector3(jitterX, jitterY, 0f);
            }

            GameObject tileObj = null;

            // Use prefabs if available
            if (useWallPrefab)
            {
                tileObj = Instantiate(wallPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Wall";
                tileObj.transform.position = worldPos;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (useUndergrowthPrefab)
            {
                tileObj = Instantiate(undergrowthPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Undergrowth";
                tileObj.transform.position = worldPos;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (useWaterPrefab)
            {
                tileObj = Instantiate(waterPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Water";
                tileObj.transform.position = worldPos;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else
            {
                // Create procedural mesh tile if no prefab available
                tileObj = CreateProceduralTile(gridX, gridY, symbol, color, tileSize);
                tileObj.transform.SetParent(tilesParent);

                // Position tile with slight Y-offset for floor tiles
                // Tiles are 0.1 units high, so offset by -0.05 to place top surface at grid level
                // For path tiles, we want them visible as ground, so use a small negative offset
                float yOffset = (symbol == '.') ? 0f : 0f; // All tiles at same level for now
                tileObj.transform.position = worldPos + new Vector3(0, yOffset, 0);
            }

            // Add to batching lists if batching is enabled
            if (enableMeshBatching && tileObj != null)
            {
                AddTileToBatchList(symbol, tileObj);
            }
        }

        /// <summary>
        /// Adds a tile to the appropriate batching list based on its symbol.
        /// </summary>
        private void AddTileToBatchList(char symbol, GameObject tileObj)
        {
            switch (symbol)
            {
                case '#': // Wall
                    wallTiles?.Add(tileObj);
                    break;
                case ';': // Undergrowth
                    undergrowthTiles?.Add(tileObj);
                    break;
                case '~': // Water
                    waterTiles?.Add(tileObj);
                    break;
                case '.': // Path
                    pathTiles?.Add(tileObj);
                    break;
                // Don't batch special tiles like Heart
            }
        }

        /// <summary>
        /// Performs mesh batching to combine tiles and reduce draw calls.
        /// </summary>
        private void PerformMeshBatching()
        {
            int totalBatchedTiles = 0;
            int totalBatches = 0;

            // Batch walls
            if (wallTiles != null && wallTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(wallTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
                totalBatchedTiles += wallTiles.Count;
            }

            // Batch undergrowth
            if (undergrowthTiles != null && undergrowthTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(undergrowthTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
                totalBatchedTiles += undergrowthTiles.Count;
            }

            // Batch water
            if (waterTiles != null && waterTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(waterTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
                totalBatchedTiles += waterTiles.Count;
            }

            // Batch paths
            if (pathTiles != null && pathTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(pathTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
                totalBatchedTiles += pathTiles.Count;
            }
        }

        /// <summary>
        /// Creates a procedural 3D mesh tile (cube) with the specified color.
        /// Uses PBR materials with appropriate properties based on tile type.
        /// </summary>
        private GameObject CreateProceduralTile(int gridX, int gridY, char symbol, Color color, float tileSize)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.name = $"Tile_{gridX}_{gridY}_{GetTileTypeName(symbol)}";

            // Scale the cube to fill X/Y plane
            // Wide in X and Y, thin in Z (faces camera directly)
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            // No rotation needed - cube scaled (X, Y, thin Z) naturally fills X/Y plane
            // Front and back faces have normals along ±Z axis, facing the camera

            // Create a PBR material based on tile type
            Material material = CreatePBRMaterialForSymbol(symbol, color);

            // Apply material to the mesh renderer
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            // Note: Position will be set by caller (CreateTile3D)
            // No initial position set here to avoid conflicts

            return tileObj;
        }

        /// <summary>
        /// Creates an appropriate PBR material for the given tile symbol.
        /// </summary>
        private Material CreatePBRMaterialForSymbol(char symbol, Color color)
        {
            switch (symbol)
            {
                case '#': // Wall (tree bramble)
                    return PBRMaterialFactory.CreateWallMaterial(color);

                case ';': // Undergrowth
                    return PBRMaterialFactory.CreateUndergrowthMaterial(color);

                case '~': // Water
                    return PBRMaterialFactory.CreateWaterMaterial(color);

                case '.': // Path
                    return PBRMaterialFactory.CreatePathMaterial(color);

                case 'H': // Heart
                    return PBRMaterialFactory.CreateEmissiveMaterial(
                        color,
                        color * 1.5f, // Slightly brighter emission
                        1.0f
                    );

                default:
                    // Fallback to generic lit material
                    return PBRMaterialFactory.CreateLitMaterial(color);
            }
        }

        /// <summary>
        /// Returns a readable name for the tile type based on symbol.
        /// </summary>
        private string GetTileTypeName(char symbol)
        {
            switch (symbol)
            {
                case '#': return "Wall";
                case ';': return "Undergrowth";
                case '~': return "Water";
                case 'H': return "Heart";
                case '.': return "Path";
                default: return "Unknown";
            }
        }

        private Color GetColorForSymbol(char symbol, bool walkable)
        {
            switch (symbol)
            {
                case '#':
                    return wallColor;
                case ';':
                    return undergrowthColor;
                case '~':
                    return waterColor;
                case 'H':
                    return heartColor;
                case '.':
                    return pathColor;
                default:
                    return walkable ? pathColor : wallColor;
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

            // Re-render
            RenderMaze();
        }

        #endregion
    }
}

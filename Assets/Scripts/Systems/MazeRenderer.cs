using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using ForestMaze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Renders the maze visually using 3D meshes and prefabs.
    /// Supports both grid-based and world-space coordinate modes.
    /// In world-space mode, tiles are oriented along graph elements.
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

        [SerializeField]
        [Tooltip("Prefab/model for node hazards (placed at clearing centers)")]
        private GameObject nodeHazardPrefab;

        [Header("Color Settings")]
        [SerializeField]
        [Tooltip("Color for walkable path tiles")]
        private Color pathColor = Color.white;

        [SerializeField]
        [Tooltip("Color tint for wall tiles (used when prefab not available)")]
        private Color wallColor = Color.black;

        [SerializeField]
        [Tooltip("Color for undergrowth tiles (used when prefab not available)")]
        private Color undergrowthColor = new Color(0.5f, 0f, 0.5f, 1f);

        [SerializeField]
        [Tooltip("Color for water tiles (used when prefab not available)")]
        private Color waterColor = Color.magenta;

        [SerializeField]
        [Tooltip("Color for the heart tile")]
        private Color heartColor = new Color(0.9f, 0.35f, 0.35f, 1f);

        [Header("World-Space Settings")]
        [SerializeField]
        [Tooltip("Tile size in world units")]
        private float worldTileSize = 1.0f;

        [SerializeField]
        [Tooltip("Node column radius (same thickness as path)")]
        private float nodeRadius = 3.0f;

        [SerializeField]
        [Tooltip("Wall border depth in tiles")]
        private int wallBorderDepth = 3;

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
        private List<GameObject> wallTiles;
        private List<GameObject> undergrowthTiles;
        private List<GameObject> waterTiles;
        private List<GameObject> pathTiles;

        // World-space tile tracking
        private HashSet<Vector2Int> occupiedPositions;
        private Dictionary<Vector2Int, List<Vector2>> wallOrientationNormals;

        #endregion

        #region Public API

        public bool HasWallPrefab => wallPrefab != null;
        public void SetWallPrefab(GameObject prefab) => wallPrefab = prefab;
        public bool HasUndergrowthPrefab => undergrowthPrefab != null;
        public void SetUndergrowthPrefab(GameObject prefab) => undergrowthPrefab = prefab;
        public bool HasWaterPrefab => waterPrefab != null;
        public void SetWaterPrefab(GameObject prefab) => waterPrefab = prefab;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            pathColor = Color.saddleBrown;
            waterColor = Color.magenta;
        }

        private void Start()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Use world-space rendering if enabled and we have a forest state
            if (mazeGridBehaviour.UseWorldSpaceCoordinates && mazeGridBehaviour.ForestMapState != null)
            {
                RenderWorldSpaceMaze();
            }
            else
            {
                RenderGridMaze();
            }
        }

        #endregion

        #region World-Space Rendering

        /// <summary>
        /// Renders the maze using world-space coordinates from the planar forest graph.
        /// Tiles are positioned and oriented according to graph elements.
        /// </summary>
        private void RenderWorldSpaceMaze()
        {
            var forestState = mazeGridBehaviour.ForestMapState;
            if (forestState == null)
            {
                Debug.LogError("[MazeRenderer] No ForestMapState available for world-space rendering.");
                return;
            }

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            // Create container for tiles
            CreateTilesContainer(mazeOrigin);

            // Initialize batching lists
            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                undergrowthTiles = new List<GameObject>();
                waterTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
            }

            // Track occupied positions for wall generation
            occupiedPositions = new HashSet<Vector2Int>();
            wallOrientationNormals = new Dictionary<Vector2Int, List<Vector2>>();

            int renderedTiles = 0;

            // Step 1: Render path tiles along edges (oriented along edge direction)
            renderedTiles += RenderEdgePaths(forestState, mazeOrigin);

            // Step 2: Render node columns (circular, radius = nodeRadius)
            renderedTiles += RenderNodeColumns(forestState, mazeOrigin);

            // Step 3: Render wall border (3 deep, normal orientation)
            renderedTiles += RenderWallBorder(mazeOrigin);

            Debug.Log($"[MazeRenderer] World-space rendered {renderedTiles} tiles " +
                $"({forestState.Nodes.Count} nodes, {forestState.Edges.Count} edges)");

            // Perform batching
            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
        }

        /// <summary>
        /// Renders path tiles along each edge, oriented in the direction of the edge.
        /// </summary>
        private int RenderEdgePaths(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;

            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                // Walk along each segment of the polyline
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 start = edge.PolylinePoints[i];
                    Vector2 end = edge.PolylinePoints[i + 1];
                    Vector2 direction = (end - start).normalized;
                    float segmentLength = Vector2.Distance(start, end);

                    // Calculate orientation angle from direction (in degrees for Unity)
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    // Place tiles along the segment
                    int numTiles = Mathf.CeilToInt(segmentLength / worldTileSize);
                    for (int j = 0; j <= numTiles; j++)
                    {
                        float t = numTiles > 0 ? (float)j / numTiles : 0;
                        Vector2 position = Vector2.Lerp(start, end, t);

                        Vector2Int gridPos = ToGridPosition(position);
                        if (occupiedPositions.Contains(gridPos)) continue;

                        // Determine symbol for spawn points at edge endpoints
                        char symbol = '.';
                        if (edge.Partial && j == numTiles)
                        {
                            symbol = GetNextSpawnSymbol();
                        }

                        CreateWorldSpaceTile(position, orientationDegrees, symbol, mazeOrigin, isPath: true);
                        occupiedPositions.Add(gridPos);
                        tileCount++;

                        // Track normal for adjacent wall tiles
                        AddWallNormalsAroundPosition(gridPos, direction);
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Renders circular node columns centered on each node.
        /// </summary>
        private int RenderNodeColumns(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;
            int tilesRadius = Mathf.CeilToInt(nodeRadius / worldTileSize);

            foreach (var node in forestState.Nodes)
            {
                for (int dx = -tilesRadius; dx <= tilesRadius; dx++)
                {
                    for (int dy = -tilesRadius; dy <= tilesRadius; dy++)
                    {
                        Vector2 offset = new Vector2(dx * worldTileSize, dy * worldTileSize);
                        Vector2 position = node.Position + offset;

                        // Check if within circular radius
                        float distance = offset.magnitude;
                        if (distance > nodeRadius) continue;

                        Vector2Int gridPos = ToGridPosition(position);
                        if (occupiedPositions.Contains(gridPos)) continue;

                        // Calculate orientation: radial from center (facing outward)
                        float orientationDegrees = 0f;
                        if (distance > 0.01f)
                        {
                            Vector2 radial = offset.normalized;
                            orientationDegrees = Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg;
                        }

                        // Determine symbol
                        char symbol = '.';
                        if (dx == 0 && dy == 0)
                        {
                            symbol = node.Kind == "root" ? 'H' : 'N';
                        }

                        CreateWorldSpaceTile(position, orientationDegrees, symbol, mazeOrigin, isPath: true);
                        occupiedPositions.Add(gridPos);
                        tileCount++;

                        // Track normal for adjacent wall tiles (radial from node center)
                        if (distance > 0.01f)
                        {
                            Vector2 radialDir = offset.normalized;
                            AddWallNormalsAroundPosition(gridPos, radialDir);
                        }
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Renders wall border tiles around all walkable tiles.
        /// Wall tiles are oriented normal to the graph element they border.
        /// </summary>
        private int RenderWallBorder(Transform mazeOrigin)
        {
            int tileCount = 0;
            var wallPositions = new HashSet<Vector2Int>();

            // Find all positions that need wall tiles
            foreach (var walkablePos in occupiedPositions)
            {
                for (int dx = -wallBorderDepth; dx <= wallBorderDepth; dx++)
                {
                    for (int dy = -wallBorderDepth; dy <= wallBorderDepth; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        Vector2Int wallPos = new Vector2Int(walkablePos.x + dx, walkablePos.y + dy);
                        if (occupiedPositions.Contains(wallPos)) continue;

                        // Check Manhattan distance
                        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (distance > wallBorderDepth) continue;

                        wallPositions.Add(wallPos);

                        // Add orientation normal (pointing from walkable toward wall)
                        if (!wallOrientationNormals.ContainsKey(wallPos))
                        {
                            wallOrientationNormals[wallPos] = new List<Vector2>();
                        }
                        Vector2 normal = new Vector2(dx, dy).normalized;
                        wallOrientationNormals[wallPos].Add(normal);
                    }
                }
            }

            // Create wall tiles with averaged orientations
            foreach (var wallPos in wallPositions)
            {
                Vector2 position = ToWorldPosition(wallPos);

                // Calculate averaged orientation from all normals
                float orientationDegrees = 0f;
                if (wallOrientationNormals.TryGetValue(wallPos, out var normals) && normals.Count > 0)
                {
                    Vector2 avgNormal = Vector2.zero;
                    foreach (var n in normals)
                    {
                        avgNormal += n;
                    }
                    avgNormal /= normals.Count;
                    if (avgNormal.sqrMagnitude > 0.001f)
                    {
                        avgNormal.Normalize();
                        orientationDegrees = Mathf.Atan2(avgNormal.y, avgNormal.x) * Mathf.Rad2Deg;
                    }
                }

                CreateWorldSpaceTile(position, orientationDegrees, '#', mazeOrigin, isPath: false);
                tileCount++;
            }

            return tileCount;
        }

        /// <summary>
        /// Creates a tile at a world-space position with the given orientation.
        /// </summary>
        private void CreateWorldSpaceTile(Vector2 position, float orientationDegrees, char symbol, Transform mazeOrigin, bool isPath)
        {
            Vector3 worldPos = mazeOrigin.position + new Vector3(position.x, position.y, 0);

            // Calculate rotation: base rotation aligns to XY plane, then apply orientation
            Quaternion baseRotation = Quaternion.Euler(-90f, 0f, 0f);
            Quaternion tileRotation = Quaternion.Euler(0f, 0f, orientationDegrees) * baseRotation;

            GameObject tileObj = null;
            Color color = GetColorForSymbol(symbol, isPath);

            // Add random jitter for walls
            if (symbol == '#')
            {
                float jitterX = Random.Range(-0.02f, 0.02f);
                float jitterY = Random.Range(-0.02f, 0.02f);
                worldPos += new Vector3(jitterX, jitterY, 0f);
            }

            if (symbol == '#' && wallPrefab != null)
            {
                tileObj = Instantiate(wallPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = tileRotation;
                tileObj.transform.localScale = new Vector3(worldTileSize * 0.65f, worldTileSize * 0.65f, worldTileSize);
                wallTiles?.Add(tileObj);
            }
            else if (symbol == 'N' && nodeHazardPrefab != null)
            {
                // Path base
                var pathBase = CreateProceduralWorldTile(worldPos, tileRotation, '.', pathColor);
                pathBase.transform.SetParent(tilesParent);
                pathTiles?.Add(pathBase);

                // Node hazard on top
                tileObj = Instantiate(nodeHazardPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = tileRotation;
                tileObj.transform.localScale = Vector3.one * worldTileSize;
            }
            else
            {
                tileObj = CreateProceduralWorldTile(worldPos, tileRotation, symbol, color);
                tileObj.transform.SetParent(tilesParent);

                if (symbol == '#')
                    wallTiles?.Add(tileObj);
                else
                    pathTiles?.Add(tileObj);
            }

            if (tileObj != null)
            {
                tileObj.name = $"WorldTile_{symbol}_{position.x:F1}_{position.y:F1}";
            }
        }

        /// <summary>
        /// Creates a procedural mesh tile at the given world position and rotation.
        /// </summary>
        private GameObject CreateProceduralWorldTile(Vector3 worldPos, Quaternion rotation, char symbol, Color color)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.transform.position = worldPos;
            tileObj.transform.rotation = rotation;
            tileObj.transform.localScale = new Vector3(worldTileSize, worldTileSize, 0.1f);

            Material material = CreatePBRMaterialForSymbol(symbol, color);
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            return tileObj;
        }

        /// <summary>
        /// Adds orientation normals for wall tiles around a given position.
        /// </summary>
        private void AddWallNormalsAroundPosition(Vector2Int centerPos, Vector2 direction)
        {
            // Add normals pointing perpendicular to the direction for adjacent wall positions
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            for (int d = 1; d <= wallBorderDepth; d++)
            {
                // Positions perpendicular to the path direction
                Vector2Int pos1 = new Vector2Int(
                    centerPos.x + Mathf.RoundToInt(perpendicular.x * d),
                    centerPos.y + Mathf.RoundToInt(perpendicular.y * d)
                );
                Vector2Int pos2 = new Vector2Int(
                    centerPos.x - Mathf.RoundToInt(perpendicular.x * d),
                    centerPos.y - Mathf.RoundToInt(perpendicular.y * d)
                );

                if (!wallOrientationNormals.ContainsKey(pos1))
                    wallOrientationNormals[pos1] = new List<Vector2>();
                wallOrientationNormals[pos1].Add(perpendicular);

                if (!wallOrientationNormals.ContainsKey(pos2))
                    wallOrientationNormals[pos2] = new List<Vector2>();
                wallOrientationNormals[pos2].Add(-perpendicular);
            }
        }

        private int spawnSymbolCounter = 0;
        private char GetNextSpawnSymbol()
        {
            char[] validIds = "ABCDEFGIJKLMOPQRSTUVWXYZabcdefgijklmopqrstuvwxyz0123456789".ToCharArray();
            int index = spawnSymbolCounter % validIds.Length;
            spawnSymbolCounter++;
            return validIds[index];
        }

        private Vector2Int ToGridPosition(Vector2 worldPos)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPos.x / worldTileSize),
                Mathf.RoundToInt(worldPos.y / worldTileSize)
            );
        }

        private Vector2 ToWorldPosition(Vector2Int gridPos)
        {
            return new Vector2(gridPos.x * worldTileSize, gridPos.y * worldTileSize);
        }

        #endregion

        #region Grid-Based Rendering (Legacy)

        /// <summary>
        /// Renders the maze using the traditional grid-based approach.
        /// </summary>
        private void RenderGridMaze()
        {
            if (mazeGridBehaviour.Grid == null)
            {
                return;
            }

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            CreateTilesContainer(mazeOrigin);

            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                undergrowthTiles = new List<GameObject>();
                waterTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            int width = grid.Width;
            int height = grid.Height;

            int renderedTiles = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node == null) continue;
                    if (node.symbol == ' ') continue;

                    Color tileColor = GetColorForSymbol(node.symbol, node.walkable);
                    CreateGridTile3D(x, y, node.symbol, tileColor);
                    renderedTiles++;
                }
            }

            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
        }

        private void CreateGridTile3D(int gridX, int gridY, char symbol, Color color)
        {
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);
            float tileSize = mazeGridBehaviour.TileSize;

            bool useWallPrefab = symbol == '#' && wallPrefab != null;
            bool useUndergrowthPrefab = symbol == ';' && undergrowthPrefab != null;
            bool useWaterPrefab = symbol == '~' && waterPrefab != null;
            bool useNodeHazardPrefab = symbol == 'N' && nodeHazardPrefab != null;

            if (symbol == '#' || symbol == ';' || symbol == '~')
            {
                float jitterX = Random.Range(-0.02f, 0.02f);
                float jitterY = Random.Range(-0.02f, 0.02f);
                worldPos += new Vector3(jitterX, jitterY, 0f);
            }

            GameObject tileObj = null;
            Quaternion prefabRotation = Quaternion.Euler(-90f, 0f, 0f);

            if (useWallPrefab)
            {
                tileObj = Instantiate(wallPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Wall";
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = new Vector3(tileSize * 0.65f, tileSize * 0.65f, tileSize);

                if (IsAdjacentToWalkableTile(gridX, gridY))
                {
                    LODGroup lodGroup = tileObj.GetComponentInChildren<LODGroup>();
                    if (lodGroup != null)
                    {
                        lodGroup.ForceLOD(0);
                    }
                }
            }
            else if (useUndergrowthPrefab)
            {
                tileObj = Instantiate(undergrowthPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Undergrowth";
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (useWaterPrefab)
            {
                tileObj = Instantiate(waterPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_Water";
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (useNodeHazardPrefab)
            {
                GameObject pathBase = CreateProceduralGridTile(gridX, gridY, '.', pathColor, tileSize);
                pathBase.transform.SetParent(tilesParent);
                pathBase.transform.position = worldPos;
                AddTileToBatchList('.', pathBase);

                tileObj = Instantiate(nodeHazardPrefab, tilesParent);
                tileObj.name = $"Tile_{gridX}_{gridY}_NodeHazard";
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else
            {
                tileObj = CreateProceduralGridTile(gridX, gridY, symbol, color, tileSize);
                tileObj.transform.SetParent(tilesParent);
                tileObj.transform.position = worldPos;
            }

            if (enableMeshBatching && tileObj != null)
            {
                AddTileToBatchList(symbol, tileObj);
            }
        }

        private GameObject CreateProceduralGridTile(int gridX, int gridY, char symbol, Color color, float tileSize)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.name = $"Tile_{gridX}_{gridY}_{GetTileTypeName(symbol)}";
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            Material material = CreatePBRMaterialForSymbol(symbol, color);
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            return tileObj;
        }

        private bool IsAdjacentToWalkableTile(int gridX, int gridY)
        {
            MazeGrid grid = mazeGridBehaviour.Grid;
            if (grid == null) return false;

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            for (int i = 0; i < 4; i++)
            {
                int nx = gridX + dx[i];
                int ny = gridY + dy[i];

                if (nx >= 0 && nx < grid.Width && ny >= 0 && ny < grid.Height)
                {
                    var node = grid.GetNode(nx, ny);
                    if (node != null && node.walkable)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Shared Methods

        private void CreateTilesContainer(Transform mazeOrigin)
        {
            if (tilesParent == null)
            {
                tilesContainer = new GameObject("MazeTiles");
                tilesContainer.transform.SetParent(mazeOrigin, worldPositionStays: false);
                tilesContainer.transform.localPosition = Vector3.zero;
                tilesContainer.transform.localRotation = Quaternion.identity;
                tilesContainer.transform.localScale = Vector3.one;
                tilesParent = tilesContainer.transform;
            }
        }

        private void AddTileToBatchList(char symbol, GameObject tileObj)
        {
            switch (symbol)
            {
                case '#':
                    wallTiles?.Add(tileObj);
                    break;
                case ';':
                    undergrowthTiles?.Add(tileObj);
                    break;
                case '~':
                    waterTiles?.Add(tileObj);
                    break;
                case '.':
                    pathTiles?.Add(tileObj);
                    break;
            }
        }

        private void PerformMeshBatching()
        {
            int totalBatches = 0;

            if (wallTiles != null && wallTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(wallTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            if (undergrowthTiles != null && undergrowthTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(undergrowthTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            if (waterTiles != null && waterTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(waterTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            if (pathTiles != null && pathTiles.Count > 0)
            {
                var batches = MeshBatcher.BatchInChunks(pathTiles, tilesParent, batchChunkSize, destroyOriginals: true);
                totalBatches += batches.Count;
            }

            Debug.Log($"[MazeRenderer] Created {totalBatches} batched meshes.");
        }

        private Material CreatePBRMaterialForSymbol(char symbol, Color color)
        {
            switch (symbol)
            {
                case '#':
                    return PBRMaterialFactory.CreateWallMaterial(color);
                case ';':
                    return PBRMaterialFactory.CreateUndergrowthMaterial(color);
                case '~':
                    return PBRMaterialFactory.CreateWaterMaterial(color);
                case '.':
                    return PBRMaterialFactory.CreatePathMaterial(color);
                case 'H':
                    return PBRMaterialFactory.CreateEmissiveMaterial(color, color * 1.5f, 1.0f);
                default:
                    return PBRMaterialFactory.CreateLitMaterial(color);
            }
        }

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
                case 'N':
                    return pathColor;
                default:
                    if (char.IsUpper(symbol) && symbol != 'H' && symbol != 'N')
                        return pathColor;
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
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                {
                    Destroy(child.gameObject);
                }
            }

            spawnSymbolCounter = 0;

            if (mazeGridBehaviour.UseWorldSpaceCoordinates && mazeGridBehaviour.ForestMapState != null)
            {
                RenderWorldSpaceMaze();
            }
            else
            {
                RenderGridMaze();
            }
        }

        #endregion
    }
}

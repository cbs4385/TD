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
        [Tooltip("Node column radius in graph units")]
        private float nodeRadius = 3.0f;

        [SerializeField]
        [Tooltip("Wall border depth in graph units")]
        private float wallBorderDepth = 3.0f;

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

        // World-space rendering state
        private float graphScale;
        private Vector2 graphOffset;
        private float tileSize;
        private HashSet<long> occupiedPositions; // All occupied positions using quantized keys
        private HashSet<Vector2Int> occupiedGridCells; // Grid cells with path/node tiles (for legacy compatibility)
        private List<EdgeSegmentData> allEdgeSegments;

        private struct EdgeSegmentData
        {
            public Vector2 StartGraph; // In graph space
            public Vector2 EndGraph;
            public Vector2 Direction;
            public Vector2 Perpendicular;
        }

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
        /// Coordinates are transformed using scale/offset to match the grid system.
        /// </summary>
        private void RenderWorldSpaceMaze()
        {
            var forestState = mazeGridBehaviour.ForestMapState;
            if (forestState == null)
            {
                Debug.LogError("[MazeRenderer] No ForestMapState available for world-space rendering.");
                return;
            }

            // Get transformation parameters
            graphScale = forestState.Scale;
            graphOffset = forestState.Offset;
            tileSize = mazeGridBehaviour.TileSize;

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            CreateTilesContainer(mazeOrigin);

            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                undergrowthTiles = new List<GameObject>();
                waterTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
            }

            // Track all occupied positions using quantized keys for consistent overlap detection
            occupiedPositions = new HashSet<long>();
            occupiedGridCells = new HashSet<Vector2Int>(); // Legacy compatibility
            allEdgeSegments = new List<EdgeSegmentData>();

            int renderedTiles = 0;

            // Step 1: Collect all edge segment data for wall orientation lookup
            CollectEdgeSegments(forestState);

            // Step 2: Render node columns FIRST (circular, radius = nodeRadius)
            // This ensures node columns take priority over edges
            int nodeColumnTiles = RenderNodeColumns(forestState, mazeOrigin);
            renderedTiles += nodeColumnTiles;
            Debug.Log($"[MazeRenderer] Rendered {nodeColumnTiles} node column tiles");

            // Step 3: Render path tiles along edges (oriented along edge direction)
            int edgeTiles = RenderEdgePaths(forestState, mazeOrigin);
            renderedTiles += edgeTiles;
            Debug.Log($"[MazeRenderer] Rendered {edgeTiles} edge path tiles");

            // Step 4: Render wall border (walls cannot overlap path/node tiles)
            int wallTileCount = RenderWallBorder(forestState, mazeOrigin);
            renderedTiles += wallTileCount;
            Debug.Log($"[MazeRenderer] Rendered {wallTileCount} wall tiles");

            Debug.Log($"[MazeRenderer] World-space rendered {renderedTiles} tiles " +
                $"({forestState.Nodes.Count} nodes, {forestState.Edges.Count} edges)");

            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
        }

        /// <summary>
        /// Transforms a graph-space position to world position using floating-point precision.
        /// No grid snapping - true world-space positioning along edges.
        /// </summary>
        private Vector3 GraphToWorldPos(Vector2 graphPos)
        {
            // Transform to grid coordinates (floating-point)
            Vector2 gridPos = graphPos * graphScale + graphOffset;

            // Convert to content-relative coordinates (subtract GRID_BUFFER = 400)
            float contentX = gridPos.x - 400f;
            float contentY = gridPos.y - 400f;

            // Get world position using floating-point coordinates
            Transform origin = mazeGridBehaviour.MazeOrigin ?? transform;
            return origin.position + new Vector3(contentX * tileSize, contentY * tileSize, 0f);
        }

        /// <summary>
        /// Transforms a graph-space position to grid cell coordinates.
        /// </summary>
        private Vector2Int GraphToGridCell(Vector2 graphPos)
        {
            Vector2 gridPos = graphPos * graphScale + graphOffset;
            return new Vector2Int(Mathf.RoundToInt(gridPos.x), Mathf.RoundToInt(gridPos.y));
        }

        /// <summary>
        /// Collects all edge segments for wall orientation lookup.
        /// </summary>
        private void CollectEdgeSegments(PlanarForestMazeGenerator.ForestMapState forestState)
        {
            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 start = edge.PolylinePoints[i];
                    Vector2 end = edge.PolylinePoints[i + 1];
                    Vector2 dir = (end - start).normalized;

                    allEdgeSegments.Add(new EdgeSegmentData
                    {
                        StartGraph = start,
                        EndGraph = end,
                        Direction = dir,
                        Perpendicular = new Vector2(-dir.y, dir.x)
                    });
                }
            }
        }

        /// <summary>
        /// Renders path tiles along each edge, oriented in the direction of the edge.
        /// </summary>
        private int RenderEdgePaths(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;

            // Calculate step size in graph space - use half-step for denser tile placement
            float graphStepSize = 0.5f / graphScale;

            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                // Walk along each segment of the polyline
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 startGraph = edge.PolylinePoints[i];
                    Vector2 endGraph = edge.PolylinePoints[i + 1];
                    Vector2 direction = (endGraph - startGraph).normalized;
                    float segmentLength = Vector2.Distance(startGraph, endGraph);

                    // Orientation in degrees
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    // Place tiles along the segment
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / graphStepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;
                        Vector2 graphPos = Vector2.Lerp(startGraph, endGraph, t);

                        // Check if position already occupied using quantized key
                        long posKey = GetQuantizedKey(graphPos);
                        if (occupiedPositions.Contains(posKey)) continue;

                        // Determine symbol
                        char symbol = '.';
                        if (edge.Partial && j == numSteps)
                        {
                            symbol = GetNextSpawnSymbol();
                        }

                        Vector3 worldPos = GraphToWorldPos(graphPos);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                        occupiedPositions.Add(posKey);
                        occupiedGridCells.Add(GraphToGridCell(graphPos)); // Legacy
                        tileCount++;
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Renders circular node columns centered on each node.
        /// Creates both a solid 3D cylinder and individual tiles for each node.
        /// </summary>
        private int RenderNodeColumns(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;

            // Calculate step size in graph space that corresponds to ~1 grid cell
            float graphStepSize = 1.0f / graphScale;
            int tilesRadius = Mathf.CeilToInt(nodeRadius / graphStepSize);

            Debug.Log($"[MazeRenderer] Node columns: nodeRadius={nodeRadius}, graphScale={graphScale}, " +
                $"graphStepSize={graphStepSize:F3}, tilesRadius={tilesRadius}");

            foreach (var node in forestState.Nodes)
            {
                int nodeTilesBefore = tileCount;

                // Create a solid 3D cylinder at the node center to fill gaps
                CreateNodeColumnCylinder(node, mazeOrigin);

                for (int dx = -tilesRadius; dx <= tilesRadius; dx++)
                {
                    for (int dy = -tilesRadius; dy <= tilesRadius; dy++)
                    {
                        // Offset from node center in graph space
                        Vector2 offsetFromNode = new Vector2(dx * graphStepSize, dy * graphStepSize);
                        float distance = offsetFromNode.magnitude;

                        // Check if within circular radius
                        if (distance > nodeRadius) continue;

                        Vector2 graphPos = node.Position + offsetFromNode;

                        // Check if position already occupied using quantized key
                        long posKey = GetQuantizedKey(graphPos);
                        if (occupiedPositions.Contains(posKey)) continue;

                        // Orientation: tiles face outward radially from node center
                        float orientationDegrees = 0f;
                        if (distance > 0.01f)
                        {
                            Vector2 radial = offsetFromNode.normalized;
                            orientationDegrees = Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg;
                        }

                        // Determine symbol
                        char symbol = '.';
                        if (dx == 0 && dy == 0)
                        {
                            symbol = node.Kind == "root" ? 'H' : 'N';
                        }

                        Vector3 worldPos = GraphToWorldPos(graphPos);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                        occupiedPositions.Add(posKey);
                        occupiedGridCells.Add(GraphToGridCell(graphPos)); // Legacy
                        tileCount++;
                    }
                }

                int nodeTilesRendered = tileCount - nodeTilesBefore;
                Debug.Log($"[MazeRenderer] Node {node.Id} at graph ({node.Position.x:F2}, {node.Position.y:F2}): " +
                    $"rendered {nodeTilesRendered} column tiles + 1 cylinder");
            }

            return tileCount;
        }

        /// <summary>
        /// Creates a 3D cylinder at the node center to fill visual gaps.
        /// Cylinder has radius 3 (in graph units) and height 0.6 (in world units).
        /// Uses the same material as path tiles.
        /// </summary>
        private void CreateNodeColumnCylinder(PlanarForestMazeGenerator.Node node, Transform mazeOrigin)
        {
            // Get world position of node center
            Vector3 worldPos = GraphToWorldPos(node.Position);

            // Create cylinder primitive
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = $"NodeColumn_{node.Id}_{node.Kind}";
            cylinder.transform.SetParent(tilesParent);

            // Position cylinder - offset Z slightly behind tiles so tiles render on top
            cylinder.transform.position = worldPos + new Vector3(0f, 0f, 0.05f);

            // Unity cylinder default: radius 0.5, height 2
            // We want: radius = nodeRadius in graph units converted to world space
            //          height = 0.6 in world units
            // Cylinder lies flat on XY plane, so we need to rotate it
            cylinder.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Rotate to lie flat

            // Calculate world radius: nodeRadius (graph units) * graphScale (grid cells per graph unit) * tileSize (world units per grid cell)
            // But actually, looking at GraphToWorldPos, we skip the graphScale in the final conversion
            // The radius in world units = nodeRadius * tileSize (since we're working in content-relative coords)
            float worldRadius = nodeRadius * tileSize * graphScale;
            float cylinderHeight = 0.6f;

            // Scale: X and Z control diameter (default diameter = 1), Y controls height (default = 2)
            // To get radius R and height H: scaleX = scaleZ = R * 2, scaleY = H / 2
            float diameter = worldRadius * 2f;
            cylinder.transform.localScale = new Vector3(diameter, cylinderHeight / 2f, diameter);

            // Apply path material to match path tiles
            Material pathMaterial = PBRMaterialFactory.CreatePathMaterial(pathColor);
            MeshRenderer renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = pathMaterial;
            }

            // Remove collider (not needed for visual)
            Collider col = cylinder.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            // Add to path tiles for batching
            pathTiles?.Add(cylinder);

            Debug.Log($"[MazeRenderer] Created node column cylinder for node {node.Id} at world ({worldPos.x:F2}, {worldPos.y:F2}), " +
                $"worldRadius={worldRadius:F2}, height={cylinderHeight:F2}");
        }

        /// <summary>
        /// Renders wall border by projecting outward from edges and nodes in world space.
        /// Walls are placed at perpendicular offsets from graph elements using floating-point coordinates.
        /// </summary>
        private int RenderWallBorder(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;
            float graphStepSize = 1.0f / graphScale;

            // Track occupied positions to avoid overlap (use quantized keys for floating-point positions)
            var occupiedWallPositions = new HashSet<long>();

            // Project walls from edges (perpendicular to edge direction)
            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 startGraph = edge.PolylinePoints[i];
                    Vector2 endGraph = edge.PolylinePoints[i + 1];
                    Vector2 direction = (endGraph - startGraph).normalized;
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    float segmentLength = Vector2.Distance(startGraph, endGraph);

                    // Walk along the segment
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / graphStepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;
                        Vector2 centerGraphPos = Vector2.Lerp(startGraph, endGraph, t);

                        // Project walls perpendicular to edge at distances 1, 2, 3 graph units
                        for (int layer = 1; layer <= Mathf.CeilToInt(wallBorderDepth); layer++)
                        {
                            float offset = layer * graphStepSize;

                            // Both sides of the edge
                            foreach (float side in new[] { 1f, -1f })
                            {
                                Vector2 wallGraphPos = centerGraphPos + perpendicular * side * offset;

                                // Check if position overlaps with path/node tiles using quantized key
                                long wallKey = GetQuantizedKey(wallGraphPos);
                                if (occupiedPositions.Contains(wallKey)) continue;

                                // Check if we already placed a wall here
                                if (occupiedWallPositions.Contains(wallKey)) continue;
                                occupiedWallPositions.Add(wallKey);

                                // Orientation perpendicular to edge
                                float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                if (side < 0) orientationDegrees += 180f;

                                Vector3 worldPos = GraphToWorldPos(wallGraphPos);
                                CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                                tileCount++;
                            }
                        }
                    }
                }
            }

            // Project walls from nodes (radially outward beyond the column radius)
            foreach (var node in forestState.Nodes)
            {
                // Place walls in rings around the node, starting just outside nodeRadius
                float startRadius = nodeRadius + graphStepSize;
                float endRadius = nodeRadius + wallBorderDepth;

                // Angular step for good coverage
                int angularSteps = Mathf.CeilToInt(2 * Mathf.PI * endRadius / graphStepSize);

                for (float r = startRadius; r <= endRadius; r += graphStepSize)
                {
                    for (int a = 0; a < angularSteps; a++)
                    {
                        float angle = (float)a / angularSteps * 2 * Mathf.PI;
                        Vector2 radialDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 wallGraphPos = node.Position + radialDir * r;

                        // Check if position overlaps with path/node tiles using quantized key
                        long wallKey = GetQuantizedKey(wallGraphPos);
                        if (occupiedPositions.Contains(wallKey)) continue;

                        // Check if we already placed a wall here
                        if (occupiedWallPositions.Contains(wallKey)) continue;
                        occupiedWallPositions.Add(wallKey);

                        // Orientation: facing outward radially
                        float orientationDegrees = angle * Mathf.Rad2Deg;

                        Vector3 worldPos = GraphToWorldPos(wallGraphPos);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                        tileCount++;
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Gets a quantized key for floating-point positions to detect overlap.
        /// </summary>
        private long GetQuantizedKey(Vector2 graphPos)
        {
            // Quantize to half-step resolution for overlap detection
            float quantizeStep = 0.5f / graphScale;
            int qx = Mathf.RoundToInt(graphPos.x / quantizeStep);
            int qy = Mathf.RoundToInt(graphPos.y / quantizeStep);
            return ((long)qx << 32) | ((long)qy & 0xFFFFFFFFL);
        }

        /// <summary>
        /// Gets wall orientation perpendicular to the tangent of the nearest graph element.
        /// </summary>
        private float GetWallOrientationFromGraph(Vector2 graphPos, PlanarForestMazeGenerator.ForestMapState forestState)
        {
            float minDist = float.MaxValue;
            Vector2 nearestPerpendicular = Vector2.up;

            // Check distance to each edge segment
            foreach (var seg in allEdgeSegments)
            {
                float dist = DistanceToLineSegment(graphPos, seg.StartGraph, seg.EndGraph);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPerpendicular = seg.Perpendicular;
                }
            }

            // Check distance to each node center
            foreach (var node in forestState.Nodes)
            {
                float dist = Vector2.Distance(graphPos, node.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    // For nodes, perpendicular points radially outward
                    Vector2 radial = (graphPos - node.Position).normalized;
                    if (radial.sqrMagnitude > 0.001f)
                        nearestPerpendicular = radial;
                }
            }

            return Mathf.Atan2(nearestPerpendicular.y, nearestPerpendicular.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Distance from point to line segment.
        /// </summary>
        private float DistanceToLineSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 line = end - start;
            float len2 = line.sqrMagnitude;

            if (len2 < 0.0001f) return Vector2.Distance(point, start);

            float t = Mathf.Clamp01(Vector2.Dot(point - start, line) / len2);
            Vector2 projection = start + t * line;
            return Vector2.Distance(point, projection);
        }

        /// <summary>
        /// Creates a tile at a world-space position with the given orientation.
        /// </summary>
        private void CreateWorldSpaceTile(Vector3 worldPos, float orientationDegrees, char symbol, Transform mazeOrigin, bool isWall)
        {
            // For flat tiles on XY plane, rotate only around Z axis
            Quaternion tileRotation = Quaternion.Euler(0f, 0f, orientationDegrees);

            // For wall prefabs (trees): rotate around Z axis to face perpendicular to graph
            // Trees stay upright (Y-up) but rotate to face the direction of orientationDegrees
            Quaternion wallPrefabRotation = Quaternion.Euler(0f, 0f, orientationDegrees);

            // For other prefabs designed Y-up that need to lie flat
            Quaternion flatPrefabRotation = Quaternion.Euler(-90f, 0f, orientationDegrees);

            GameObject tileObj = null;
            Color color = GetColorForSymbol(symbol, !isWall);

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
                tileObj.transform.rotation = wallPrefabRotation; // Rotate around Z to face perpendicular
                tileObj.transform.localScale = new Vector3(tileSize * 0.65f, tileSize * 0.65f, tileSize);
                wallTiles?.Add(tileObj);
            }
            else if (symbol == 'N' && nodeHazardPrefab != null)
            {
                // Path base (flat on XY plane)
                var pathBase = CreateProceduralFlatTile(worldPos, tileRotation, '.', pathColor);
                pathBase.transform.SetParent(tilesParent);
                pathTiles?.Add(pathBase);

                // Node hazard on top (uses flat prefab rotation)
                tileObj = Instantiate(nodeHazardPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = flatPrefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else
            {
                // Procedural tile - flat on XY plane
                tileObj = CreateProceduralFlatTile(worldPos, tileRotation, symbol, color);
                tileObj.transform.SetParent(tilesParent);

                if (symbol == '#')
                    wallTiles?.Add(tileObj);
                else
                    pathTiles?.Add(tileObj);
            }

            if (tileObj != null)
            {
                tileObj.name = $"WorldTile_{symbol}_{worldPos.x:F1}_{worldPos.y:F1}";
            }
        }

        /// <summary>
        /// Creates a flat procedural tile on the XY plane.
        /// </summary>
        private GameObject CreateProceduralFlatTile(Vector3 worldPos, Quaternion rotation, char symbol, Color color)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.transform.position = worldPos;
            tileObj.transform.rotation = rotation;
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            Material material = CreatePBRMaterialForSymbol(symbol, color);
            MeshRenderer renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }

            return tileObj;
        }

        private int spawnSymbolCounter = 0;
        private char GetNextSpawnSymbol()
        {
            char[] validIds = "ABCDEFGIJKLMOPQRSTUVWXYZabcdefgijklmopqrstuvwxyz0123456789".ToCharArray();
            int index = spawnSymbolCounter % validIds.Length;
            spawnSymbolCounter++;
            return validIds[index];
        }

        #endregion

        #region Grid-Based Rendering (Legacy)

        private void RenderGridMaze()
        {
            if (mazeGridBehaviour.Grid == null) return;

            tileSize = mazeGridBehaviour.TileSize;
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

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node == null || node.symbol == ' ') continue;

                    Color tileColor = GetColorForSymbol(node.symbol, node.walkable);
                    CreateGridTile3D(x, y, node.symbol, tileColor);
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

            if (symbol == '#' || symbol == ';' || symbol == '~')
            {
                worldPos += new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(-0.02f, 0.02f), 0f);
            }

            GameObject tileObj = null;
            Quaternion prefabRotation = Quaternion.Euler(-90f, 0f, 0f);

            if (symbol == '#' && wallPrefab != null)
            {
                tileObj = Instantiate(wallPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = new Vector3(tileSize * 0.65f, tileSize * 0.65f, tileSize);
            }
            else if (symbol == ';' && undergrowthPrefab != null)
            {
                tileObj = Instantiate(undergrowthPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (symbol == '~' && waterPrefab != null)
            {
                tileObj = Instantiate(waterPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else if (symbol == 'N' && nodeHazardPrefab != null)
            {
                var pathBase = CreateProceduralGridTile(gridX, gridY, '.', pathColor);
                pathBase.transform.SetParent(tilesParent);
                pathBase.transform.position = worldPos;
                pathTiles?.Add(pathBase);

                tileObj = Instantiate(nodeHazardPrefab, tilesParent);
                tileObj.transform.position = worldPos;
                tileObj.transform.rotation = prefabRotation;
                tileObj.transform.localScale = Vector3.one * tileSize;
            }
            else
            {
                tileObj = CreateProceduralGridTile(gridX, gridY, symbol, color);
                tileObj.transform.SetParent(tilesParent);
                tileObj.transform.position = worldPos;
            }

            if (enableMeshBatching && tileObj != null)
            {
                AddTileToBatchList(symbol, tileObj);
            }
        }

        private GameObject CreateProceduralGridTile(int gridX, int gridY, char symbol, Color color)
        {
            GameObject tileObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tileObj.name = $"Tile_{gridX}_{gridY}_{GetTileTypeName(symbol)}";
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 0.1f);

            Material material = CreatePBRMaterialForSymbol(symbol, color);
            var renderer = tileObj.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.material = material;

            return tileObj;
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
                case '#': wallTiles?.Add(tileObj); break;
                case ';': undergrowthTiles?.Add(tileObj); break;
                case '~': waterTiles?.Add(tileObj); break;
                case '.': pathTiles?.Add(tileObj); break;
            }
        }

        private void PerformMeshBatching()
        {
            int totalBatches = 0;

            if (wallTiles?.Count > 0)
                totalBatches += MeshBatcher.BatchInChunks(wallTiles, tilesParent, batchChunkSize, true).Count;
            if (undergrowthTiles?.Count > 0)
                totalBatches += MeshBatcher.BatchInChunks(undergrowthTiles, tilesParent, batchChunkSize, true).Count;
            if (waterTiles?.Count > 0)
                totalBatches += MeshBatcher.BatchInChunks(waterTiles, tilesParent, batchChunkSize, true).Count;
            if (pathTiles?.Count > 0)
                totalBatches += MeshBatcher.BatchInChunks(pathTiles, tilesParent, batchChunkSize, true).Count;

            Debug.Log($"[MazeRenderer] Created {totalBatches} batched meshes.");
        }

        private Material CreatePBRMaterialForSymbol(char symbol, Color color)
        {
            switch (symbol)
            {
                case '#': return PBRMaterialFactory.CreateWallMaterial(color);
                case ';': return PBRMaterialFactory.CreateUndergrowthMaterial(color);
                case '~': return PBRMaterialFactory.CreateWaterMaterial(color);
                case '.': return PBRMaterialFactory.CreatePathMaterial(color);
                case 'H': return PBRMaterialFactory.CreateEmissiveMaterial(color, color * 1.5f, 1.0f);
                default: return PBRMaterialFactory.CreateLitMaterial(color);
            }
        }

        private string GetTileTypeName(char symbol)
        {
            return symbol switch
            {
                '#' => "Wall",
                ';' => "Undergrowth",
                '~' => "Water",
                'H' => "Heart",
                '.' => "Path",
                _ => "Unknown"
            };
        }

        private Color GetColorForSymbol(char symbol, bool walkable)
        {
            return symbol switch
            {
                '#' => wallColor,
                ';' => undergrowthColor,
                '~' => waterColor,
                'H' => heartColor,
                '.' => pathColor,
                'N' => pathColor,
                _ when char.IsUpper(symbol) && symbol != 'H' && symbol != 'N' => pathColor,
                _ => walkable ? pathColor : wallColor
            };
        }

        #endregion

        #region Public Methods

        public void RefreshMaze()
        {
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                    Destroy(child.gameObject);
            }

            spawnSymbolCounter = 0;

            if (mazeGridBehaviour.UseWorldSpaceCoordinates && mazeGridBehaviour.ForestMapState != null)
                RenderWorldSpaceMaze();
            else
                RenderGridMaze();
        }

        #endregion
    }
}

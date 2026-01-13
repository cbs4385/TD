using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ForestMaze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Renders the maze visually using 3D meshes and prefabs.
    /// Pure world-space mode - tiles are oriented along maze elements.
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
        [Tooltip("Node column cylinder radius in world units (greedy coverage)")]
        private float nodeRadius = 3.0f;

        [SerializeField]
        [Tooltip("Node tile placement radius in world units (should match NODE_RADIUS=3.0 in generator)")]
        private float nodeTileRadius = 3.0f;

        [SerializeField]
        [Tooltip("Wall border depth in world units")]
        private float wallBorderDepth = 3.0f;

        [Header("Container Settings")]
        [SerializeField]
        [Tooltip("Parent transform to hold all tile objects")]
        private Transform tilesParent;

        [Header("Optimization Settings")]
        [Tooltip("Mesh batching disabled - individual tiles needed for pathfinding reference")]
        private bool enableMeshBatching = false; // Removed [SerializeField] so scene value can't override

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
        private float tileSize;
        private List<Vector2> occupiedPositions; // All occupied path positions in world space
        private List<Vector2> occupiedWallPositions; // All occupied wall positions in world space
        private List<EdgeSegmentData> allEdgeSegments;

        // Overlap detection threshold - positions closer than this are considered overlapping
        private const float OVERLAP_THRESHOLD = 0.4f;

        private struct EdgeSegmentData
        {
            public Vector2 Start; // World space position
            public Vector2 End;   // World space position
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

        /// <summary>
        /// Creates a wall tile at a specific world position.
        /// Used by DynamicMazeGrowth to place walls at portal locations.
        /// </summary>
        public GameObject CreateWallAtPosition(Vector3 worldPos, float orientationDegrees)
        {
            if (wallPrefab == null)
            {
                // Debug.LogWarning("[MazeRenderer] Cannot create wall - wallPrefab is null");
                return null;
            }

            Transform parent = mazeGridBehaviour?.MazeOrigin ?? transform;
            GameObject wallObj = Instantiate(wallPrefab, parent);
            wallObj.transform.position = worldPos;
            wallObj.transform.rotation = Quaternion.Euler(0f, 0f, orientationDegrees);
            wallObj.name = $"Wall_Portal_{worldPos.x:F1}_{worldPos.y:F1}";

            // Debug.Log($"[MazeRenderer] Created wall at portal position {worldPos}");
            return wallObj;
        }

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

            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                return;
            }

            // Pure world-space rendering
            RenderWorldSpaceMaze();
        }

        #endregion

        #region World-Space Rendering

        /// <summary>
        /// Renders the maze using world-space coordinates from the planar forest maze.
        /// All positions are in world space - no transforms needed.
        /// </summary>
        private void RenderWorldSpaceMaze()
        {
            var forestState = mazeGridBehaviour.ForestMapState;
            if (forestState == null)
            {
                // Debug.LogError("[MazeRenderer] No ForestMapState available for world-space rendering.");
                return;
            }

            // Pure world-space mode
            tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            CreateTilesContainer(mazeOrigin);

            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                undergrowthTiles = new List<GameObject>();
                waterTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
            }

            // Track all occupied positions using world-space coordinates with distance-based overlap detection
            occupiedPositions = new List<Vector2>();
            occupiedWallPositions = new List<Vector2>();
            allEdgeSegments = new List<EdgeSegmentData>();

            int renderedTiles = 0;

            // Step 1: Collect all edge segment data for wall orientation lookup
            CollectEdgeSegments(forestState);

            // Step 2: Render node columns FIRST (circular, radius = nodeRadius)
            // This ensures node columns take priority over edges
            int nodeColumnTiles = RenderNodeColumns(forestState, mazeOrigin);
            renderedTiles += nodeColumnTiles;
            // Debug.Log($"[MazeRenderer] Rendered {nodeColumnTiles} node column tiles");

            // Step 3: Render path tiles along edges (oriented along edge direction)
            int edgeTiles = RenderEdgePaths(forestState, mazeOrigin);
            renderedTiles += edgeTiles;
            // Debug.Log($"[MazeRenderer] Rendered {edgeTiles} edge path tiles");

            // Step 4: Render wall border
            int wallTileCount = RenderWallBorder(forestState, mazeOrigin);
            renderedTiles += wallTileCount;
            // Debug.Log($"[MazeRenderer] Rendered {wallTileCount} wall tiles");

            // Debug.Log($"[MazeRenderer] World-space rendered {renderedTiles} tiles " +
            //     $"({forestState.Nodes.Count} nodes, {forestState.Edges.Count} edges)");

            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
        }

        /// <summary>
        /// Converts a Vector2 world position to Vector3 (adding Z=0).
        /// </summary>
        private Vector3 ToVector3(Vector2 pos2D)
        {
            return new Vector3(pos2D.x, pos2D.y, 0f);
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
                        Start = start,
                        End = end,
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

            // Use half-step for denser tile placement
            float stepSize = 0.5f;

            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                // Track whether we've placed a tile at the exact endpoint for partial edges
                bool isPartialEdge = edge.Partial;
                Vector2 exactEndpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                bool endpointTilePlaced = false;

                // Walk along each segment of the polyline
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    float segmentLength = Vector2.Distance(segStart, segEnd);

                    // Orientation in degrees
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    bool isLastSegment = (i == edge.PolylinePoints.Count - 2);

                    // Place tiles along the segment using world-space coordinates
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;

                        // Use exact endpoint position for the last point of last segment
                        // This avoids floating-point precision issues with Lerp
                        Vector2 pos2D;
                        bool isExactEndpoint = false;
                        if (isLastSegment && j == numSteps)
                        {
                            pos2D = exactEndpoint;
                            isExactEndpoint = true;
                        }
                        else
                        {
                            pos2D = Vector2.Lerp(segStart, segEnd, t);
                        }

                        // Check if position already occupied using distance-based check
                        // For frontier endpoints, always place the tile (override occupation check)
                        bool forcePlace = isPartialEdge && isExactEndpoint && !endpointTilePlaced;

                        if (!forcePlace && IsPositionOccupied(pos2D, occupiedPositions)) continue;

                        // Determine symbol
                        char symbol = '.';
                        if (isPartialEdge && isExactEndpoint)
                        {
                            symbol = GetNextSpawnSymbol();
                            endpointTilePlaced = true;
                        }

                        Vector3 worldPos = ToVector3(pos2D);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                        occupiedPositions.Add(pos2D);
                        tileCount++;
                    }
                }

                // Fallback: if endpoint tile wasn't placed for partial edge, force place it
                if (isPartialEdge && !endpointTilePlaced)
                {
                    // Calculate orientation from second-to-last to last point
                    Vector2 prevPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 2];
                    Vector2 direction = (exactEndpoint - prevPoint).normalized;
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    Vector3 worldPos = ToVector3(exactEndpoint);
                    char symbol = GetNextSpawnSymbol();
                    CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                    occupiedPositions.Add(exactEndpoint);
                    tileCount++;
                    // Debug.LogWarning($"[MazeRenderer] FALLBACK: Placed endpoint tile for partial edge {edge.Id} at world {worldPos}");
                }
                else if (isPartialEdge)
                {
                    // Log successful endpoint placement for diagnostics
                    // Debug.Log($"[MazeRenderer] Partial edge {edge.Id}: endpoint tile placed at world {ToVector3(exactEndpoint)}");
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Renders circular node columns centered on each node.
        /// Creates a single 3D cylinder for visual representation.
        /// Marks positions as occupied for pathfinding without creating visible tiles.
        /// </summary>
        private int RenderNodeColumns(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            foreach (var node in forestState.Nodes)
            {
                // Create the single 3D cylinder - this is the only visual object for the node
                CreateNodeColumnCylinder(node, mazeOrigin);

                // Mark node area as occupied for pathfinding (no visible tiles needed)
                // Use coarse step for logical positions only
                float logicalStep = 1.0f;
                int logicalRadius = Mathf.CeilToInt(nodeRadius / logicalStep);

                for (int dx = -logicalRadius; dx <= logicalRadius; dx++)
                {
                    for (int dy = -logicalRadius; dy <= logicalRadius; dy++)
                    {
                        Vector2 offset = new Vector2(dx * logicalStep, dy * logicalStep);
                        if (offset.magnitude > nodeRadius) continue;

                        Vector2 pos2D = node.Position + offset;
                        if (!IsPositionOccupied(pos2D, occupiedPositions))
                        {
                            occupiedPositions.Add(pos2D);
                        }
                    }
                }
            }

            return forestState.Nodes.Count; // Return count of cylinders created
        }

        /// <summary>
        /// Creates a 3D cylinder at the node center to fill visual gaps.
        /// Cylinder has radius nodeRadius and thin height (0.3 world units).
        /// Uses the same material as path tiles.
        /// </summary>
        private void CreateNodeColumnCylinder(PlanarForestMazeGenerator.Node node, Transform mazeOrigin)
        {
            // Get world position of node center
            Vector3 worldPos = ToVector3(node.Position);

            // Create cylinder primitive
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = $"NodeColumn_{node.Id}_{node.Kind}";
            cylinder.transform.SetParent(tilesParent);

            // Calculate visual radius
            float worldRadius = nodeRadius * tileSize;

            // Unity cylinder default: radius 0.5 (diameter 1), height 2, oriented along Y axis
            // To create a flat disc on the XY plane:
            // 1. Scale to get the right diameter and thickness
            // 2. Rotate so the circular face is parallel to XY plane (height along Z)

            // Scale: X and Z for diameter, Y for thickness (0.03 for thin disc)
            float diameter = worldRadius * 2f;
            cylinder.transform.localScale = new Vector3(diameter, 0.03f, diameter);

            // Rotate 90° around X so the cylinder lies flat (circular face in XY plane, thickness along Z)
            cylinder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Position cylinder at Z=0 (same plane as tiles)
            cylinder.transform.position = worldPos;

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

            // DO NOT add to pathTiles - keep cylinder as separate object to avoid batching distortion
            // Cylinders cannot be combined with cube meshes properly
        }

        /// <summary>
        /// Renders wall border by projecting outward from edges in world space.
        /// Creates 3 layers of walls on each side of the path.
        /// </summary>
        private int RenderWallBorder(PlanarForestMazeGenerator.ForestMapState forestState, Transform mazeOrigin)
        {
            int tileCount = 0;
            // Step size along the edge for wall placement - dense to avoid gaps
            float stepSize = 0.2f;
            // Path is 1 unit wide, so edges are at 0.5 from center
            float pathHalfWidth = 0.5f;
            int wallDepth = 3; // 3 layers of walls on each side
            float wallSpacing = 0.3f; // Tight spacing - walls can overlap

            // Project walls from edges (perpendicular to edge direction)
            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    float segmentLength = Vector2.Distance(segStart, segEnd);

                    // Walk along the segment
                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;
                        Vector2 centerPos = Vector2.Lerp(segStart, segEnd, t);

                        // Skip positions inside nodes
                        bool insideNode = false;
                        foreach (var node in forestState.Nodes)
                        {
                            if (Vector2.Distance(centerPos, node.Position) < nodeRadius + 0.5f)
                            {
                                insideNode = true;
                                break;
                            }
                        }
                        if (insideNode) continue;

                        // Place 3 layers of walls on each side
                        foreach (float side in new[] { 1f, -1f })
                        {
                            for (int layer = 0; layer < wallDepth; layer++)
                            {
                                // Start at edge of path (pathHalfWidth) and go outward
                                float wallOffset = pathHalfWidth + wallSpacing * (layer + 1);
                                Vector2 wallPos = centerPos + perpendicular * side * wallOffset;

                                // Skip if inside a node
                                bool wallInsideNode = false;
                                foreach (var node in forestState.Nodes)
                                {
                                    if (Vector2.Distance(wallPos, node.Position) < nodeRadius)
                                    {
                                        wallInsideNode = true;
                                        break;
                                    }
                                }
                                if (wallInsideNode) continue;

                                float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                if (side < 0) orientationDegrees += 180f;

                                Vector3 worldPos = ToVector3(wallPos);
                                CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                                tileCount++;
                            }
                        }
                    }
                }
            }

            // Add walls around nodes (3 rings at nodeRadius + offset)
            // Node walls go all the way around - they can overlap with edge walls at junctions
            foreach (var node in forestState.Nodes)
            {
                for (int layer = 0; layer < wallDepth; layer++)
                {
                    // Walls start right at node edge (no pathHalfWidth offset for nodes)
                    float ringRadius = nodeRadius + wallSpacing * (layer + 0.5f);
                    // Dense wall placement around rings - enough to ensure no gaps > 0.3 units
                    int numWalls = Mathf.Max(32, (int)(ringRadius * 2f * Mathf.PI / stepSize));

                    for (int i = 0; i < numWalls; i++)
                    {
                        float angle = (float)i / numWalls * 2f * Mathf.PI;
                        Vector2 wallPos = node.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;

                        // Skip if wall would be ON the edge path (within path radius of centerline)
                        bool onEdgePath = false;
                        foreach (var edge in forestState.Edges)
                        {
                            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;
                            for (int j = 0; j < edge.PolylinePoints.Count - 1; j++)
                            {
                                float distToSeg = DistanceToLineSegment(wallPos, edge.PolylinePoints[j], edge.PolylinePoints[j + 1]);
                                if (distToSeg < pathHalfWidth) // On the path itself
                                {
                                    onEdgePath = true;
                                    break;
                                }
                            }
                            if (onEdgePath) break;
                        }
                        if (onEdgePath) continue;

                        float orientationDegrees = angle * Mathf.Rad2Deg;
                        Vector3 worldPos = ToVector3(wallPos);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                        tileCount++;
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Calculates distance from a point to a line segment.
        /// </summary>
        private float DistanceToLineSegment(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            float lineLength = line.magnitude;
            if (lineLength < 0.001f) return Vector2.Distance(point, lineStart);

            Vector2 lineDir = line / lineLength;
            float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, lineDir) / lineLength);
            Vector2 closestPoint = lineStart + lineDir * lineLength * t;
            return Vector2.Distance(point, closestPoint);
        }

        /// <summary>
        /// Fills gaps along the inner edge of the wall buffer by checking positions adjacent to path tiles.
        /// </summary>
        private int FillInnerEdgeGaps(PlanarForestMazeGenerator.ForestMapState forestState,
            List<Vector2> wallPositions, Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;
            float wallRadius = 0.65f;

            // Walk along each edge segment and check for gaps perpendicular to the path
            foreach (var seg in allEdgeSegments)
            {
                float segmentLength = Vector2.Distance(seg.Start, seg.End);
                int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / (stepSize * 0.5f)));

                for (int j = 0; j <= numSteps; j++)
                {
                    float t = numSteps > 0 ? (float)j / numSteps : 0;
                    Vector2 pathPos = Vector2.Lerp(seg.Start, seg.End, t);

                    // Check positions perpendicular to the edge throughout the wall border depth
                    for (float dist = stepSize; dist <= wallBorderDepth + stepSize; dist += stepSize * 0.5f)
                    {
                        foreach (float side in new[] { 1f, -1f })
                        {
                            Vector2 checkPos = pathPos + seg.Perpendicular * side * dist;

                            // Skip if already occupied by path
                            if (IsPositionOccupied(checkPos, occupiedPositions)) continue;

                            // Skip if inside a node column
                            bool insideNode = false;
                            foreach (var node in forestState.Nodes)
                            {
                                if (Vector2.Distance(checkPos, node.Position) < nodeRadius)
                                {
                                    insideNode = true;
                                    break;
                                }
                            }
                            if (insideNode) continue;

                            // Check if wall would intersect path
                            Vector2? intersection = CheckWallPathIntersection(checkPos, wallRadius);
                            if (intersection.HasValue) continue;

                            // Found a gap - fill it
                            wallPositions.Add(checkPos);
                            float orientationDegrees = Mathf.Atan2(seg.Perpendicular.y, seg.Perpendicular.x) * Mathf.Rad2Deg;
                            if (side < 0) orientationDegrees += 180f;

                            Vector3 worldPos = ToVector3(checkPos);
                            CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                            tileCount++;
                        }
                    }
                }
            }

            // Node gap filling removed - cylinder provides visual boundary

            return tileCount;
        }

        /// <summary>
        /// Fills gaps only around newly added edges and node.
        /// This is a localized version of FillInnerEdgeGaps for incremental updates.
        /// Much faster than the full gap-filling pass since it only processes new elements.
        /// </summary>
        private int FillGapsAroundNewElements(List<PlanarForestMazeGenerator.Edge> newEdges,
            PlanarForestMazeGenerator.Node newNode,
            PlanarForestMazeGenerator.ForestMapState forestState,
            List<Vector2> wallPositions, Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;
            float wallRadius = 0.65f;

            // Fill gaps around new edges only
            if (newEdges != null)
            {
                foreach (var edge in newEdges)
                {
                    if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                        continue;

                    for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                    {
                        Vector2 segStart = edge.PolylinePoints[i];
                        Vector2 segEnd = edge.PolylinePoints[i + 1];
                        Vector2 direction = (segEnd - segStart).normalized;
                        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                        float segmentLength = Vector2.Distance(segStart, segEnd);

                        int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / (stepSize * 0.5f)));

                        for (int j = 0; j <= numSteps; j++)
                        {
                            float t = numSteps > 0 ? (float)j / numSteps : 0;
                            Vector2 pathPos = Vector2.Lerp(segStart, segEnd, t);

                            // Check positions perpendicular to the edge throughout the wall border depth
                            for (float dist = stepSize; dist <= wallBorderDepth + stepSize; dist += stepSize * 0.5f)
                            {
                                foreach (float side in new[] { 1f, -1f })
                                {
                                    Vector2 checkPos = pathPos + perpendicular * side * dist;

                                    // Skip if already occupied by path
                                    if (IsPositionOccupied(checkPos, occupiedPositions)) continue;

                                    // Skip if inside a node column
                                    bool insideNode = false;
                                    foreach (var node in forestState.Nodes)
                                    {
                                        if (Vector2.Distance(checkPos, node.Position) < nodeRadius)
                                        {
                                            insideNode = true;
                                            break;
                                        }
                                    }
                                    if (insideNode) continue;

                                    // Check if wall would intersect path
                                    Vector2? intersection = CheckWallPathIntersection(checkPos, wallRadius);
                                    if (intersection.HasValue) continue;

                                    // Found a gap - fill it
                                    wallPositions.Add(checkPos);
                                    float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                    if (side < 0) orientationDegrees += 180f;

                                    Vector3 worldPos = ToVector3(checkPos);
                                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                                    tileCount++;
                                }
                            }
                        }
                    }
                }
            }

            // Node gap filling is NOT needed - the cylinder provides the visual boundary
            // Only edge walls are needed

            return tileCount;
        }

        /// <summary>
        /// Gets an adjusted wall position that doesn't intersect node columns or path tiles.
        /// If the original position intersects, translates it along the shortest vector toward void.
        /// Returns null if no valid position can be found.
        /// </summary>
        private Vector2? GetAdjustedWallPosition(Vector2 wallPos, Vector2 pushDirection,
            PlanarForestMazeGenerator.ForestMapState forestState, List<Vector2> wallPositions)
        {
            // Use half-unit steps for finer adjustment precision
            float stepSize = 0.5f;
            float nodeBuffer = 0.0f; // No buffer - walls should touch node column edges
            int maxIterations = 15; // Prevent infinite loops

            // Wall model radius (wall prefab is scaled to 0.65 * tileSize)
            float wallRadius = 0.65f;

            Vector2 currentPos = wallPos;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool needsAdjustment = false;
                Vector2 adjustmentVector = Vector2.zero;

                // Wall overlap is allowed - multiple walls can share the same position
                // to ensure complete border coverage

                // Check if wall model would intersect any path tile (check multiple sample points)
                if (!needsAdjustment)
                {
                    Vector2? pathIntersection = CheckWallPathIntersection(currentPos, wallRadius);
                    if (pathIntersection.HasValue)
                    {
                        // Push away from the intersecting path position
                        Vector2 awayFromPath = (currentPos - pathIntersection.Value).normalized;
                        if (awayFromPath.sqrMagnitude < 0.001f)
                            awayFromPath = pushDirection.normalized;
                        adjustmentVector = awayFromPath * stepSize;
                        needsAdjustment = true;
                    }
                }

                // Check if inside any node column
                if (!needsAdjustment)
                {
                    foreach (var node in forestState.Nodes)
                    {
                        float distToNode = Vector2.Distance(currentPos, node.Position);
                        float minDist = nodeRadius + nodeBuffer;

                        if (distToNode < minDist)
                        {
                            // Calculate push direction: away from node center
                            Vector2 awayFromNode = (currentPos - node.Position).normalized;
                            if (awayFromNode.sqrMagnitude < 0.001f)
                                awayFromNode = pushDirection.normalized;

                            // Push out to just beyond the minimum distance
                            float pushDist = minDist - distToNode + stepSize * 0.5f;
                            adjustmentVector = awayFromNode * pushDist;
                            needsAdjustment = true;
                            break;
                        }
                    }
                }

                // Only reject walls for path tiles and node columns (edge path check removed)
                // Wall placement is allowed anywhere else - walls can overlap each other

                if (!needsAdjustment)
                {
                    // Position is valid
                    return currentPos;
                }

                // Apply adjustment
                currentPos += adjustmentVector;
            }

            // Could not find valid position
            return null;
        }

        /// <summary>
        /// Checks if a wall at the given position would intersect any path tile.
        /// Uses distance-based check against all occupied path positions.
        /// Returns the position of the intersecting path tile, or null if no intersection.
        /// </summary>
        private Vector2? CheckWallPathIntersection(Vector2 wallPos, float wallRadius)
        {
            // Check if any occupied path position is within wallRadius of this wall position
            for (int i = 0; i < occupiedPositions.Count; i++)
            {
                Vector2 pathPos = occupiedPositions[i];
                float dist = Vector2.Distance(wallPos, pathPos);
                if (dist < wallRadius)
                {
                    return pathPos; // Found intersection
                }
            }

            return null; // No intersection
        }

        /// <summary>
        /// Simple check if a wall position is valid (for cases where we don't want translation).
        /// </summary>
        private bool IsWallPositionValid(Vector2 wallPos, PlanarForestMazeGenerator.ForestMapState forestState,
            List<Vector2> wallPositions)
        {
            float nodeBuffer = 0.0f; // No buffer - walls should touch node column edges

            // Check if already occupied by path tiles
            if (IsPositionOccupied(wallPos, occupiedPositions)) return false;

            // Check if inside any node column
            foreach (var node in forestState.Nodes)
            {
                float distToNode = Vector2.Distance(wallPos, node.Position);
                if (distToNode < nodeRadius + nodeBuffer)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Fills the outer corner at a bend vertex where two edge segments meet at an angle.
        /// Places wall tiles to cover the triangular gap on the outside of the bend.
        /// </summary>
        private int FillBendCorner(Vector2 bendVertex, Vector2 incomingDir, Vector2 outgoingDir,
            PlanarForestMazeGenerator.ForestMapState forestState, List<Vector2> wallPositions,
            Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;

            // Calculate perpendiculars for both segments
            Vector2 incomingPerp = new Vector2(-incomingDir.y, incomingDir.x);
            Vector2 outgoingPerp = new Vector2(-outgoingDir.y, outgoingDir.x);

            // Calculate the turn angle (cross product sign determines turn direction)
            float cross = incomingDir.x * outgoingDir.y - incomingDir.y * outgoingDir.x;

            // Determine outer side based on turn direction
            // If turning left (cross > 0), the outer corner is on the right (-perp direction)
            // If turning right (cross < 0), the outer corner is on the left (+perp direction)
            float outerSide = cross > 0 ? -1f : 1f;

            // Skip very shallow bends (nearly straight lines) - no corner gap to fill
            float dot = Vector2.Dot(incomingDir, outgoingDir);
            if (dot > 0.95f) return 0; // Less than ~18 degrees bend

            // Calculate the corner fill region by sweeping between the two perpendicular directions
            // Start from the incoming perpendicular and rotate toward the outgoing perpendicular
            Vector2 cornerPerpStart = incomingPerp * outerSide;
            Vector2 cornerPerpEnd = outgoingPerp * outerSide;

            // Calculate angle between the two perpendiculars
            float startAngle = Mathf.Atan2(cornerPerpStart.y, cornerPerpStart.x);
            float endAngle = Mathf.Atan2(cornerPerpEnd.y, cornerPerpEnd.x);

            // Normalize the angle difference to sweep the correct direction
            float angleDiff = endAngle - startAngle;
            if (angleDiff > Mathf.PI) angleDiff -= 2 * Mathf.PI;
            if (angleDiff < -Mathf.PI) angleDiff += 2 * Mathf.PI;

            // Number of angular steps based on bend sharpness
            int angularSteps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(angleDiff) / (Mathf.PI / 8f)));

            // Fill walls in the corner region at multiple layers
            int numLayers = Mathf.CeilToInt(wallBorderDepth / stepSize);

            for (int layer = 1; layer <= numLayers; layer++)
            {
                float offset = layer * stepSize;

                for (int a = 0; a <= angularSteps; a++)
                {
                    float t = angularSteps > 0 ? (float)a / angularSteps : 0;
                    float currentAngle = startAngle + angleDiff * t;
                    Vector2 radiusDir = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));

                    Vector2 wallPos = bendVertex + radiusDir * offset;

                    // Get adjusted position
                    Vector2? adjustedPos = GetAdjustedWallPosition(wallPos, radiusDir, forestState, wallPositions);
                    if (!adjustedPos.HasValue)
                        continue;

                    wallPositions.Add(adjustedPos.Value);

                    // Orientation facing outward from the bend
                    float orientationDegrees = currentAngle * Mathf.Rad2Deg;

                    Vector3 worldPos = ToVector3(adjustedPos.Value);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                    tileCount++;
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Fills the triangular gap between two edges that diverge from a node at a small angle.
        /// Places wall tiles in the region between the two edge directions where normal perpendicular
        /// walls can't be placed because they'd collide with the other edge's path.
        /// </summary>
        private int FillDivergingEdgeGap(Vector2 nodePos, Vector2 dir1, Vector2 dir2,
            PlanarForestMazeGenerator.ForestMapState forestState, List<Vector2> wallPositions,
            Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;

            // Calculate angle between the two edge directions
            float dot = Vector2.Dot(dir1, dir2);
            float cross = dir1.x * dir2.y - dir1.y * dir2.x;

            // Skip if edges are going in very different directions (large angle between them)
            // We only need to fill gaps when edges are close together (small angle)
            if (dot < 0.5f) return 0; // More than 60 degrees apart - no gap fill needed

            // Calculate the angular range to fill
            float angle1 = Mathf.Atan2(dir1.y, dir1.x);
            float angle2 = Mathf.Atan2(dir2.y, dir2.x);

            // Ensure we sweep the smaller arc between the two directions
            float angleDiff = angle2 - angle1;
            if (angleDiff > Mathf.PI) angleDiff -= 2 * Mathf.PI;
            if (angleDiff < -Mathf.PI) angleDiff += 2 * Mathf.PI;

            // Number of angular steps based on gap size
            int angularSteps = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(angleDiff) / (Mathf.PI / 16f)));

            // Fill the gap starting from node edge (nodeRadius) out to wallBorderDepth beyond
            float startRadius = nodeRadius;
            float endRadius = nodeRadius + wallBorderDepth * 1.5f; // Extend a bit further to ensure coverage

            for (float r = startRadius; r <= endRadius; r += stepSize)
            {
                for (int a = 0; a <= angularSteps; a++)
                {
                    float t = angularSteps > 0 ? (float)a / angularSteps : 0;
                    float currentAngle = angle1 + angleDiff * t;
                    Vector2 radiusDir = new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));

                    Vector2 wallPos = nodePos + radiusDir * r;

                    // Get adjusted position
                    Vector2? adjustedPos = GetAdjustedWallPosition(wallPos, radiusDir, forestState, wallPositions);
                    if (!adjustedPos.HasValue)
                        continue;

                    wallPositions.Add(adjustedPos.Value);

                    // Orientation facing outward from node
                    float orientationDegrees = currentAngle * Mathf.Rad2Deg;

                    Vector3 worldPos = ToVector3(adjustedPos.Value);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                    tileCount++;
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Fills gaps between all pairs of diverging edges at each node.
        /// </summary>
        private int FillAllDivergingEdgeGaps(PlanarForestMazeGenerator.ForestMapState forestState,
            List<Vector2> wallPositions, Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;

            foreach (var node in forestState.Nodes)
            {
                // Find all edges connected to this node
                List<Vector2> edgeDirections = new List<Vector2>();

                foreach (var edge in forestState.Edges)
                {
                    if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                    // Check if edge starts or ends at this node
                    Vector2 firstPoint = edge.PolylinePoints[0];
                    Vector2 lastPoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];

                    float distToFirst = Vector2.Distance(firstPoint, node.Position);
                    float distToLast = Vector2.Distance(lastPoint, node.Position);

                    Vector2 direction;
                    if (distToFirst < nodeRadius + 2f)
                    {
                        // Edge starts at this node - direction is from node toward second point
                        if (edge.PolylinePoints.Count >= 2)
                        {
                            direction = (edge.PolylinePoints[1] - node.Position).normalized;
                            edgeDirections.Add(direction);
                        }
                    }
                    else if (distToLast < nodeRadius + 2f)
                    {
                        // Edge ends at this node - direction is from node toward second-to-last point
                        if (edge.PolylinePoints.Count >= 2)
                        {
                            direction = (edge.PolylinePoints[edge.PolylinePoints.Count - 2] - node.Position).normalized;
                            edgeDirections.Add(direction);
                        }
                    }
                }

                // Fill gaps between each pair of adjacent edges (sorted by angle)
                if (edgeDirections.Count >= 2)
                {
                    // Sort by angle
                    edgeDirections.Sort((a, b) =>
                        Mathf.Atan2(a.y, a.x).CompareTo(Mathf.Atan2(b.y, b.x)));

                    // Fill gaps between consecutive edges
                    for (int i = 0; i < edgeDirections.Count; i++)
                    {
                        int nextIdx = (i + 1) % edgeDirections.Count;
                        tileCount += FillDivergingEdgeGap(node.Position, edgeDirections[i], edgeDirections[nextIdx],
                            forestState, wallPositions, mazeOrigin, stepSize);
                    }
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Renders end cap walls at the end of a frontier edge, aligned along the edge direction.
        /// Walls are placed perpendicular to the edge to close off the open end.
        /// </summary>
        private int RenderEdgeEndCap(Vector2 endPoint, Vector2 direction, Vector2 perpendicular,
            PlanarForestMazeGenerator.ForestMapState forestState, List<Vector2> wallPositions,
            Transform mazeOrigin, float stepSize)
        {
            int tileCount = 0;

            // Place walls perpendicular to the edge direction to close off the end
            // Walls are positioned along the perpendicular axis at the endpoint
            int numLayers = Mathf.CeilToInt(wallBorderDepth / stepSize);
            for (int layer = 1; layer <= numLayers; layer++)
            {
                float forwardOffset = layer * stepSize;
                Vector2 capCenterPos = endPoint + direction * forwardOffset;

                // Place walls across the perpendicular width at this forward position
                for (int perpLayer = -numLayers; perpLayer <= numLayers; perpLayer++)
                {
                    float perpOffset = perpLayer * stepSize;
                    Vector2 wallPos = capCenterPos + perpendicular * perpOffset;

                    // Push direction is forward (along edge direction) for end caps
                    Vector2 pushDir = direction;

                    // Get adjusted position (translates away from intersections)
                    Vector2? adjustedPos = GetAdjustedWallPosition(wallPos, pushDir, forestState, wallPositions);
                    if (!adjustedPos.HasValue)
                        continue;

                    wallPositions.Add(adjustedPos.Value);

                    // Orientation: aligned along the edge direction (toward connected end)
                    // This makes walls face perpendicular to the edge, closing off the path
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                    Vector3 worldPos = ToVector3(adjustedPos.Value);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                    tileCount++;
                }
            }

            return tileCount;
        }

        /// <summary>
        /// Checks if a position is already occupied by checking distance to all existing positions.
        /// Returns true if the position overlaps with an existing one.
        /// </summary>
        private bool IsPositionOccupied(Vector2 pos, List<Vector2> positions, float threshold = OVERLAP_THRESHOLD)
        {
            // For performance with large lists, we could use a spatial hash here,
            // but for now we do a linear search which is correct
            for (int i = 0; i < positions.Count; i++)
            {
                if (Vector2.Distance(positions[i], pos) < threshold)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a position to the occupied list if not already occupied.
        /// Returns true if added, false if already occupied.
        /// </summary>
        private bool TryAddOccupiedPosition(Vector2 pos, List<Vector2> positions, float threshold = OVERLAP_THRESHOLD)
        {
            if (IsPositionOccupied(pos, positions, threshold))
                return false;
            positions.Add(pos);
            return true;
        }

        /// <summary>
        /// Gets wall orientation perpendicular to the tangent of the nearest edge or node.
        /// </summary>
        private float GetWallOrientation(Vector2 pos, PlanarForestMazeGenerator.ForestMapState forestState)
        {
            float minDist = float.MaxValue;
            Vector2 nearestPerpendicular = Vector2.up;

            // Check distance to each edge segment
            foreach (var seg in allEdgeSegments)
            {
                float dist = DistanceToLineSegment(pos, seg.Start, seg.End);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPerpendicular = seg.Perpendicular;
                }
            }

            // Check distance to each node center
            foreach (var node in forestState.Nodes)
            {
                float dist = Vector2.Distance(pos, node.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    // For nodes, perpendicular points radially outward
                    Vector2 radial = (pos - node.Position).normalized;
                    if (radial.sqrMagnitude > 0.001f)
                        nearestPerpendicular = radial;
                }
            }

            return Mathf.Atan2(nearestPerpendicular.y, nearestPerpendicular.x) * Mathf.Rad2Deg;
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

            // Debug.Log($"[MazeRenderer] Created {totalBatches} batched meshes.");
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

        /// <summary>
        /// Synchronous maze refresh - destroys and recreates all tiles immediately.
        /// Use RefreshMazeAsync for non-blocking refresh during gameplay.
        /// </summary>
        public void RefreshMaze()
        {
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                    Destroy(child.gameObject);
            }

            spawnSymbolCounter = 0;

            if (mazeGridBehaviour != null && mazeGridBehaviour.ForestMapState != null)
            {
                RenderWorldSpaceMaze();
            }
        }

        /// <summary>
        /// Async maze refresh - builds new tiles invisibly in a coroutine and swaps them in.
        /// Does not block the main thread during tile generation.
        /// </summary>
        public void RefreshMazeAsync()
        {
            StartCoroutine(RefreshMazeCoroutine());
        }

        /// <summary>
        /// Incrementally adds a node (single cylinder, no individual tiles).
        /// Much faster than full refresh - only adds the delta.
        /// </summary>
        public void AddNodeTilesIncremental(ForestMaze.PlanarForestMazeGenerator.Node newNode)
        {
            if (mazeGridBehaviour == null || tilesParent == null)
                return;

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            // Ensure state is initialized
            if (occupiedPositions == null)
                occupiedPositions = new List<Vector2>();

            // Create the single node cylinder - this is the only visual object
            CreateNodeColumnCylinder(newNode, mazeOrigin);

            // Mark node area as occupied for pathfinding (no visible tiles)
            float logicalStep = 1.0f;
            int logicalRadius = Mathf.CeilToInt(nodeRadius / logicalStep);

            for (int dx = -logicalRadius; dx <= logicalRadius; dx++)
            {
                for (int dy = -logicalRadius; dy <= logicalRadius; dy++)
                {
                    Vector2 offset = new Vector2(dx * logicalStep, dy * logicalStep);
                    if (offset.magnitude > nodeRadius) continue;

                    Vector2 pos2D = newNode.Position + offset;
                    if (!IsPositionOccupied(pos2D, occupiedPositions))
                    {
                        occupiedPositions.Add(pos2D);
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Incremental: Added node cylinder for node {newNode.Id}");
        }

        /// <summary>
        /// Incrementally adds tiles for newly added/modified edges.
        /// Uses world-space coordinates with distance-based overlap detection.
        /// </summary>
        public void AddEdgeTilesIncremental(List<ForestMaze.PlanarForestMazeGenerator.Edge> newEdges)
        {
            if (mazeGridBehaviour == null || tilesParent == null || newEdges == null)
                return;

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            if (occupiedPositions == null)
                occupiedPositions = new List<Vector2>();
            if (occupiedWallPositions == null)
                occupiedWallPositions = new List<Vector2>();
            if (allEdgeSegments == null)
                allEdgeSegments = new List<EdgeSegmentData>();

            float stepSize = 0.5f;
            int tilesCreated = 0;

            foreach (var edge in newEdges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                    continue;

                // Collect edge segments for this edge
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 start = edge.PolylinePoints[i];
                    Vector2 end = edge.PolylinePoints[i + 1];
                    Vector2 dir = (end - start).normalized;

                    allEdgeSegments.Add(new EdgeSegmentData
                    {
                        Start = start,
                        End = end,
                        Direction = dir,
                        Perpendicular = new Vector2(-dir.y, dir.x)
                    });
                }

                bool isPartialEdge = edge.Partial;
                Vector2 exactEndpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                bool endpointTilePlaced = false;

                // Place path tiles along the edge
                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    float segmentLength = Vector2.Distance(segStart, segEnd);
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    bool isLastSegment = (i == edge.PolylinePoints.Count - 2);

                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;

                        Vector2 pos2D;
                        bool isExactEndpoint = false;
                        if (isLastSegment && j == numSteps)
                        {
                            pos2D = exactEndpoint;
                            isExactEndpoint = true;
                        }
                        else
                        {
                            pos2D = Vector2.Lerp(segStart, segEnd, t);
                        }

                        bool forcePlace = isPartialEdge && isExactEndpoint && !endpointTilePlaced;

                        if (!forcePlace && IsPositionOccupied(pos2D, occupiedPositions))
                            continue;

                        char symbol = '.';
                        if (isPartialEdge && isExactEndpoint)
                        {
                            symbol = GetNextSpawnSymbol();
                            endpointTilePlaced = true;
                        }

                        Vector3 worldPos = ToVector3(pos2D);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                        occupiedPositions.Add(pos2D);
                        tilesCreated++;
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Incremental: Added {tilesCreated} edge tiles for {newEdges.Count} edges");
        }

        /// <summary>
        /// Adds wall tiles around newly added path tiles.
        /// Uses same parameters as RenderWallBorder for consistency.
        /// </summary>
        public void AddWallsIncremental(List<ForestMaze.PlanarForestMazeGenerator.Edge> newEdges,
            ForestMaze.PlanarForestMazeGenerator.Node newNode)
        {
            if (mazeGridBehaviour == null || tilesParent == null)
                return;

            var forestState = mazeGridBehaviour.ForestMapState;
            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            // Use class-level occupiedWallPositions to avoid duplicating existing walls
            if (occupiedWallPositions == null)
                occupiedWallPositions = new List<Vector2>();

            // Match RenderWallBorder parameters exactly
            float stepSize = 0.2f;
            float pathHalfWidth = 0.5f;
            int wallDepth = 3;
            float wallSpacing = 0.3f; // Tight spacing - walls can overlap
            int wallsCreated = 0;

            // Add walls around new edges
            if (newEdges != null)
            {
                foreach (var edge in newEdges)
                {
                    if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                        continue;

                    for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                    {
                        Vector2 segStart = edge.PolylinePoints[i];
                        Vector2 segEnd = edge.PolylinePoints[i + 1];
                        Vector2 direction = (segEnd - segStart).normalized;
                        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                        float segmentLength = Vector2.Distance(segStart, segEnd);

                        int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                        for (int j = 0; j <= numSteps; j++)
                        {
                            float t = numSteps > 0 ? (float)j / numSteps : 0;
                            Vector2 centerPos = Vector2.Lerp(segStart, segEnd, t);

                            // Skip positions inside nodes
                            bool insideNode = false;
                            foreach (var node in forestState.Nodes)
                            {
                                if (Vector2.Distance(centerPos, node.Position) < nodeRadius + 0.5f)
                                {
                                    insideNode = true;
                                    break;
                                }
                            }
                            if (insideNode) continue;

                            // Place 3 layers of walls on each side
                            foreach (float side in new[] { 1f, -1f })
                            {
                                for (int layer = 0; layer < wallDepth; layer++)
                                {
                                    float wallOffset = pathHalfWidth + wallSpacing * (layer + 1);
                                    Vector2 wallPos = centerPos + perpendicular * side * wallOffset;

                                    // Skip if inside a node
                                    bool wallInsideNode = false;
                                    foreach (var node in forestState.Nodes)
                                    {
                                        if (Vector2.Distance(wallPos, node.Position) < nodeRadius)
                                        {
                                            wallInsideNode = true;
                                            break;
                                        }
                                    }
                                    if (wallInsideNode) continue;

                                    float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                    if (side < 0) orientationDegrees += 180f;

                                    Vector3 worldPos = ToVector3(wallPos);
                                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                                    wallsCreated++;
                                }
                            }
                        }
                    }
                }
            }

            // Add walls around the new node (3 rings)
            // Node walls go all the way around - they can overlap with edge walls at junctions
            if (newNode != null)
            {
                for (int layer = 0; layer < wallDepth; layer++)
                {
                    // Walls start right at node edge (matching RenderWallBorder)
                    float ringRadius = nodeRadius + wallSpacing * (layer + 0.5f);
                    int numWalls = Mathf.Max(32, (int)(ringRadius * 2f * Mathf.PI / stepSize));

                    for (int i = 0; i < numWalls; i++)
                    {
                        float angle = (float)i / numWalls * 2f * Mathf.PI;
                        Vector2 wallPos = newNode.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;

                        // Skip if wall would be ON the edge path (within path radius of centerline)
                        bool onEdgePath = false;
                        foreach (var edge in forestState.Edges)
                        {
                            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;
                            for (int j = 0; j < edge.PolylinePoints.Count - 1; j++)
                            {
                                float distToSeg = DistanceToLineSegment(wallPos, edge.PolylinePoints[j], edge.PolylinePoints[j + 1]);
                                if (distToSeg < pathHalfWidth) // On the path itself
                                {
                                    onEdgePath = true;
                                    break;
                                }
                            }
                            if (onEdgePath) break;
                        }
                        if (onEdgePath) continue;

                        float orientationDegrees = angle * Mathf.Rad2Deg;
                        Vector3 worldPos = ToVector3(wallPos);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                        wallsCreated++;
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Incremental: Added {wallsCreated} wall tiles");
        }

        /// <summary>
        /// Removes wall tiles near a consumed spawn point position.
        /// Called when a frontier edge is consumed and walls blocking it should be removed.
        /// </summary>
        public void RemoveWallsNearPosition(Vector3 position, float radius)
        {
            if (tilesParent == null)
                return;

            int removedCount = 0;
            List<Transform> toRemove = new List<Transform>();

            foreach (Transform child in tilesParent)
            {
                if (child.name.StartsWith("WorldTile_#") || child.name.StartsWith("Wall_"))
                {
                    float dist = Vector3.Distance(child.position, position);
                    if (dist < radius)
                    {
                        toRemove.Add(child);
                    }
                }
            }

            foreach (var t in toRemove)
            {
                // Remove from occupied wall positions if tracking
                Vector2 pos2D = new Vector2(t.position.x, t.position.y);
                if (occupiedWallPositions != null)
                {
                    // Find and remove the position from the list (distance-based)
                    for (int i = occupiedWallPositions.Count - 1; i >= 0; i--)
                    {
                        if (Vector2.Distance(occupiedWallPositions[i], pos2D) < OVERLAP_THRESHOLD)
                        {
                            occupiedWallPositions.RemoveAt(i);
                            break;
                        }
                    }
                }

                Destroy(t.gameObject);
                removedCount++;
            }

            if (removedCount > 0)
            {
                // Debug.Log($"[MazeRenderer] Removed {removedCount} wall tiles near {position}");
            }
        }

        /// <summary>
        /// Removes path tiles (non-wall tiles) within a radius of a position.
        /// Used before adding node tiles to clear edge tiles that would block node tile placement.
        /// </summary>
        public void RemovePathTilesNearPosition(Vector3 position, float radius)
        {
            if (tilesParent == null)
                return;

            int removedCount = 0;
            List<Transform> toRemove = new List<Transform>();

            foreach (Transform child in tilesParent)
            {
                // Remove path tiles (not walls, not node cylinders)
                // Path tiles have names like "WorldTile_.", "WorldTile_H", "WorldTile_N", etc.
                if (child.name.StartsWith("WorldTile_") && !child.name.StartsWith("WorldTile_#"))
                {
                    float dist = Vector3.Distance(child.position, position);
                    if (dist < radius)
                    {
                        toRemove.Add(child);
                    }
                }
            }

            foreach (var t in toRemove)
            {
                // Remove from occupied positions
                Vector2 pos2D = new Vector2(t.position.x, t.position.y);
                if (occupiedPositions != null)
                {
                    // Find and remove the position from the list (distance-based)
                    for (int i = occupiedPositions.Count - 1; i >= 0; i--)
                    {
                        if (Vector2.Distance(occupiedPositions[i], pos2D) < OVERLAP_THRESHOLD)
                        {
                            occupiedPositions.RemoveAt(i);
                            break;
                        }
                    }
                }

                Destroy(t.gameObject);
                removedCount++;
            }

            if (removedCount > 0)
            {
                // Debug.Log($"[MazeRenderer] Removed {removedCount} path tiles near {position}");
            }
        }

        /// <summary>
        /// Coroutine that builds tiles in batches over multiple frames.
        /// Creates tiles invisibly, then swaps the entire container at once.
        /// </summary>
        private IEnumerator RefreshMazeCoroutine()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                yield break;
            }

            var forestState = mazeGridBehaviour.ForestMapState;
            // Debug.Log($"[MazeRenderer] Starting async refresh with {forestState.Nodes.Count} nodes, {forestState.Edges.Count} edges");

            // Create a temporary container for new tiles (invisible at first)
            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;
            GameObject newContainer = new GameObject("MazeTiles_Building");
            newContainer.transform.SetParent(mazeOrigin, worldPositionStays: false);
            newContainer.transform.localPosition = Vector3.zero;
            newContainer.transform.localRotation = Quaternion.identity;
            newContainer.transform.localScale = Vector3.one;

            // Start invisible - we'll swap it in when done
            newContainer.SetActive(false);

            Transform oldTilesParent = tilesParent;
            tilesParent = newContainer.transform;

            // Store current state
            spawnSymbolCounter = 0;
            tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            if (enableMeshBatching)
            {
                wallTiles = new List<GameObject>();
                undergrowthTiles = new List<GameObject>();
                waterTiles = new List<GameObject>();
                pathTiles = new List<GameObject>();
            }

            occupiedPositions = new List<Vector2>();
            occupiedWallPositions = new List<Vector2>();
            allEdgeSegments = new List<EdgeSegmentData>();

            int tilesCreated = 0;
            int batchSize = 50; // Number of tiles per frame

            // Step 1: Collect edge segments
            CollectEdgeSegments(forestState);
            yield return null; // Yield after collecting edges

            // Step 2: Render node columns (cylinder only, no individual tiles)
            foreach (var node in forestState.Nodes)
            {
                // Create the single node cylinder
                CreateNodeColumnCylinder(node, mazeOrigin);
                tilesCreated++;

                // Mark node area as occupied for pathfinding (no visible tiles)
                float logicalStep = 1.0f;
                int logicalRadius = Mathf.CeilToInt(nodeRadius / logicalStep);

                for (int dx = -logicalRadius; dx <= logicalRadius; dx++)
                {
                    for (int dy = -logicalRadius; dy <= logicalRadius; dy++)
                    {
                        Vector2 offset = new Vector2(dx * logicalStep, dy * logicalStep);
                        if (offset.magnitude > nodeRadius) continue;

                        Vector2 pos2D = node.Position + offset;
                        if (!IsPositionOccupied(pos2D, occupiedPositions))
                        {
                            occupiedPositions.Add(pos2D);
                        }
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Async: Created {tilesCreated} node tiles");
            yield return null;

            // Step 3: Render edge paths
            int edgeTilesStart = tilesCreated;
            float edgeStepSize = 0.5f;

            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                bool isPartialEdge = edge.Partial;
                Vector2 exactEndpoint = edge.PolylinePoints[edge.PolylinePoints.Count - 1];
                bool endpointTilePlaced = false;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    float segmentLength = Vector2.Distance(segStart, segEnd);
                    float orientationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    bool isLastSegment = (i == edge.PolylinePoints.Count - 2);

                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / edgeStepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;

                        Vector2 pos2D;
                        bool isExactEndpoint = false;
                        if (isLastSegment && j == numSteps)
                        {
                            pos2D = exactEndpoint;
                            isExactEndpoint = true;
                        }
                        else
                        {
                            pos2D = Vector2.Lerp(segStart, segEnd, t);
                        }

                        bool forcePlace = isPartialEdge && isExactEndpoint && !endpointTilePlaced;

                        if (!forcePlace && IsPositionOccupied(pos2D, occupiedPositions)) continue;

                        char symbol = '.';
                        if (isPartialEdge && isExactEndpoint)
                        {
                            symbol = GetNextSpawnSymbol();
                            endpointTilePlaced = true;
                        }

                        Vector3 worldPos = ToVector3(pos2D);
                        CreateWorldSpaceTile(worldPos, orientationDegrees, symbol, mazeOrigin, isWall: false);
                        occupiedPositions.Add(pos2D);
                        tilesCreated++;

                        if (tilesCreated % batchSize == 0)
                        {
                            yield return null;
                        }
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Async: Created {tilesCreated - edgeTilesStart} edge tiles");
            yield return null;

            // Step 4: Render wall border (simplified - fewer walls during async for speed)
            // Use class-level occupiedWallPositions for consistency
            int wallTilesStart = tilesCreated;
            float wallStepSize = 0.5f;

            foreach (var edge in forestState.Edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2) continue;

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    float segmentLength = Vector2.Distance(segStart, segEnd);

                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / wallStepSize));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;
                        Vector2 centerPos = Vector2.Lerp(segStart, segEnd, t);

                        int numWallLayers = Mathf.CeilToInt(wallBorderDepth / wallStepSize);
                        for (int layer = 1; layer <= numWallLayers; layer++)
                        {
                            float offset = layer * wallStepSize;

                            foreach (float side in new[] { 1f, -1f })
                            {
                                Vector2 wallPos = centerPos + perpendicular * side * offset;
                                Vector2 pushDir = perpendicular * side;

                                Vector2? adjustedPos = GetAdjustedWallPosition(wallPos, pushDir, forestState, occupiedWallPositions);
                                if (!adjustedPos.HasValue) continue;

                                occupiedWallPositions.Add(adjustedPos.Value);

                                float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                if (side < 0) orientationDegrees += 180f;

                                Vector3 worldPos = ToVector3(adjustedPos.Value);
                                CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true);
                                tilesCreated++;

                                if (tilesCreated % batchSize == 0)
                                {
                                    yield return null;
                                }
                            }
                        }
                    }
                }
            }

            // Debug.Log($"[MazeRenderer] Async: Created {tilesCreated - wallTilesStart} wall tiles");
            yield return null;

            // Step 5: Perform mesh batching
            if (enableMeshBatching)
            {
                PerformMeshBatching();
            }
            yield return null;

            // Step 6: Swap containers - make new visible, destroy old
            newContainer.name = "MazeTiles";
            newContainer.SetActive(true);

            if (oldTilesParent != null)
            {
                // Destroy old tiles over a few frames to reduce spike
                StartCoroutine(DestroyOldTilesGradually(oldTilesParent.gameObject));
            }

            // Debug.Log($"[MazeRenderer] Async refresh complete: {tilesCreated} total tiles");
        }

        /// <summary>
        /// Destroys old tile container gradually to avoid frame spike.
        /// </summary>
        private IEnumerator DestroyOldTilesGradually(GameObject oldContainer)
        {
            // First, disable the container to hide it immediately
            oldContainer.SetActive(false);
            yield return null;

            // Destroy children in batches
            Transform oldTransform = oldContainer.transform;
            int destroyBatch = 100;
            int destroyed = 0;

            while (oldTransform.childCount > 0)
            {
                int toDestroy = Mathf.Min(destroyBatch, oldTransform.childCount);
                for (int i = 0; i < toDestroy; i++)
                {
                    if (oldTransform.childCount > 0)
                    {
                        DestroyImmediate(oldTransform.GetChild(0).gameObject);
                        destroyed++;
                    }
                }
                yield return null;
            }

            // Finally destroy the empty container
            Destroy(oldContainer);
            // Debug.Log($"[MazeRenderer] Destroyed {destroyed} old tiles");
        }

        #endregion
    }
}

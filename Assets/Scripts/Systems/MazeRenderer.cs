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
        [Tooltip("Disable maze wall generation (for debugging)")]
        private bool disableWalls = false;

        [SerializeField]
        [Tooltip("Disable end cap walls at portals (for debugging)")]
        private bool disableEndCapWalls = false;

        [SerializeField]
        [Tooltip("Prefab/model for front-rank wall tiles (full detail, LOD0)")]
        private GameObject wallPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for interior wall tiles (low detail, LOD2) - used for walls behind front rank")]
        private GameObject wallPrefabLOD2;

        [SerializeField]
        [Tooltip("Prefab/model for undergrowth tiles")]
        private GameObject undergrowthPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for water tiles")]
        private GameObject waterPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for node hazards (placed at clearing centers)")]
        private GameObject nodeHazardPrefab;

        [SerializeField]
        [Tooltip("Prefab/model for the heart base (static ring, placed at seed node / node 0)")]
        private GameObject heartBasePrefab;

        [SerializeField]
        [Tooltip("Prefab/model for the heart tongue (animated, placed at seed node / node 0)")]
        private GameObject heartTonguePrefab;

        [SerializeField]
        [Tooltip("Use the EarthenRingGround material from the heart prefab for path tiles")]
        private bool useHeartMaterialForPaths = true;

        // Cached material from heart prefab's EarthenRingGround child
        private Material heartGroundMaterial;

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

        // Spatial hash for fast position lookups - stores actual positions per cell for distance checking
        private Dictionary<long, List<Vector2>> occupiedPositionHash;
        private const float POSITION_HASH_CELL_SIZE = 0.5f; // Should be >= OVERLAP_THRESHOLD

        // Wall generation constants - shared between initial render and incremental updates
        private const float WALL_STEP_SIZE = 0.8f; // 4x larger for 75% fewer walls
        // PATH_HALF_WIDTH = 0 keeps walls tight against path edges
        // Path tile colliders are reduced to 2/3 size to prevent false collision on curves
        private const float PATH_HALF_WIDTH = 0f;
        private const int WALL_DEPTH = 3;
        private const float WALL_SPACING = 0.8f; // Increased for more visible wall depth
        private const float EDGE_END_SKIP = 1.0f;
        private const float EDGE_ANGLE_CLEARANCE_DEG = 12.0f;

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
            if (disableEndCapWalls || wallPrefab == null)
            {
                return null;
            }

            Transform parent = mazeGridBehaviour?.MazeOrigin ?? transform;
            GameObject wallObj = Instantiate(wallPrefab, parent);
            wallObj.transform.position = worldPos;
            wallObj.transform.rotation = Quaternion.Euler(0f, 0f, orientationDegrees);
            wallObj.name = $"Wall_Portal_{worldPos.x:F1}_{worldPos.y:F1}";

            return wallObj;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            pathColor = Color.saddleBrown;
            waterColor = Color.magenta;

            // Load heart prefabs dynamically if not assigned via inspector
            if (heartBasePrefab == null)
            {
#if UNITY_EDITOR
                heartBasePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heartbase.prefab");
#endif
            }
            if (heartTonguePrefab == null)
            {
#if UNITY_EDITOR
                heartTonguePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/heart tongue.prefab");
#endif
            }

            // Extract EarthenRingGround material from heart base prefab if available
            if (useHeartMaterialForPaths && heartBasePrefab != null)
            {
                ExtractHeartGroundMaterial();
            }
        }

        /// <summary>
        /// Creates a material using the extracted/modified EarthenGroundTexture.
        /// Falls back to procedural shader if texture is not found.
        /// </summary>
        private void ExtractHeartGroundMaterial()
        {
            // First, try to load the extracted texture file
            Texture2D texture = null;

            #if UNITY_EDITOR
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/EarthenGroundTexture.png");
            #endif

            // Also try Resources folder at runtime
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>("EarthenGroundTexture");
            }

            if (texture != null)
            {
                // Use the texture-based shader
                var shader = Shader.Find("Custom/EarthenGroundTextured");
                if (shader != null)
                {
                    heartGroundMaterial = new Material(shader);
                    heartGroundMaterial.SetTexture("_MainTex", texture);
                    heartGroundMaterial.SetFloat("_TileScale", 0.05f); // Lower = larger texture, higher = more repetition
                    heartGroundMaterial.SetFloat("_Brightness", 1.0f); // Boost brightness to match heart model
                    heartGroundMaterial.SetFloat("_Metallic", 0.0f); // Match glTF-pbrMetallic defaults
                    heartGroundMaterial.SetFloat("_Smoothness", 0.2f); // Matte earthy surface
                    heartGroundMaterial.SetFloat("_EdgeDarkening", 0.0f); // Disabled to match heart material
                    return;
                }
            }

            // Fallback: Try to use the material from the heart base prefab directly
            if (heartBasePrefab != null)
            {
                Transform earthenRing = heartBasePrefab.transform.Find("EarthenRingGround");
                if (earthenRing == null)
                {
                    foreach (Transform child in heartBasePrefab.GetComponentsInChildren<Transform>())
                    {
                        if (child.name == "EarthenRingGround")
                        {
                            earthenRing = child;
                            break;
                        }
                    }
                }

                if (earthenRing != null)
                {
                    var renderer = earthenRing.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        heartGroundMaterial = renderer.sharedMaterial;
                        return;
                    }
                }
            }

            // Fallback: Try to find the procedural EarthenGround shader
            var proceduralShader = Shader.Find("Custom/EarthenGround");
            if (proceduralShader == null)
            {
                return;
            }

            // Create the procedural earthy ground material with lighter, warmer colors
            heartGroundMaterial = new Material(proceduralShader);

            // Boosted brightness to better match the visible EarthenRingGround
            heartGroundMaterial.SetColor("_DeepShadow", new Color(0.38f, 0.32f, 0.28f, 1f));
            heartGroundMaterial.SetColor("_DarkBase", new Color(0.45f, 0.38f, 0.33f, 1f));
            heartGroundMaterial.SetColor("_MidTone", new Color(0.55f, 0.47f, 0.42f, 1f));
            heartGroundMaterial.SetColor("_LightMid", new Color(0.62f, 0.55f, 0.50f, 1f));
            heartGroundMaterial.SetColor("_Highlight", new Color(0.70f, 0.64f, 0.60f, 1f));
            heartGroundMaterial.SetFloat("_NoiseScale", 10f);
            heartGroundMaterial.SetFloat("_DetailScale", 30f);
            heartGroundMaterial.SetFloat("_ColorVariation", 0.7f);
            heartGroundMaterial.SetFloat("_EdgeDarkening", 0.05f);
        }

        private void Start()
        {
            // WorldSpaceMazeRenderer is deprecated - MazeRenderer is now the primary visual renderer
            // This check remains for backwards compatibility during transition
            if (GetComponent<WorldSpaceMazeRenderer>() != null)
            {
                enabled = false;
                return;
            }

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
                return;
            }

            // Pure world-space mode
            tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            CreateTilesContainer(mazeOrigin);

            // Background plane removed - camera background color (#0a0a0a) handles this

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
            occupiedPositionHash = new Dictionary<long, List<Vector2>>();

            // Step 1: Collect all edge segment data for wall orientation lookup
            CollectEdgeSegments(forestState);

            // Step 2: Render path tiles along edges FIRST
            // This ensures edges extend fully into node areas before nodes mark them occupied
            RenderEdgePaths(forestState, mazeOrigin);

            // Step 3: Render node columns (circular, radius = nodeRadius)
            // Node cylinders visually cover the edge tiles underneath
            RenderNodeColumns(forestState, mazeOrigin);

            // Step 4: Render wall border
            RenderWallBorder(forestState, mazeOrigin);

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
                        AddToPositionHash(pos2D);
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
                    AddToPositionHash(exactEndpoint);
                    tileCount++;
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
            for (int nodeIndex = 0; nodeIndex < forestState.Nodes.Count; nodeIndex++)
            {
                var node = forestState.Nodes[nodeIndex];

                // Create the single 3D cylinder - this is the only visual object for the node
                CreateNodeColumnCylinder(node, mazeOrigin);

                // Heart prefabs are now spawned by HeartOfTheMaze component, not MazeRenderer
                // The HeartOfTheMaze handles the heartbase (static ring) and heart tongue (animated)

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
                            AddToPositionHash(pos2D);
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

            // Scale: X and Z for diameter, Y for thickness
            // Path tiles use Z scale = 0.1, which means cube extends ±0.05 from center
            // Cylinder default height is 2, so Y scale = 0.05 gives actual height of 0.1 (matching path tiles)
            float diameter = worldRadius * 2f;
            float cylinderThicknessScale = 0.05f; // Results in 0.1 actual thickness (matches path tile Z=0.1)
            cylinder.transform.localScale = new Vector3(diameter, cylinderThicknessScale, diameter);

            // Rotate 90° around X so the cylinder lies flat (circular face in XY plane, thickness along Z)
            cylinder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Position cylinder so its top face aligns with path tiles' top face
            // Path tiles: center at Z=0, scale Z=0.1, so top at Z=0.05
            // Cylinder: after rotation, Y scale becomes Z thickness. Scale Y=0.05 means height=0.1, so ±0.05 from center
            // Both now have top at Z=0.05 when centered at Z=0
            cylinder.transform.position = worldPos;

            // Apply path material to match path tiles
            Material pathMaterial = GetPathMaterial();
            MeshRenderer renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = pathMaterial;
            }

            // Replace CapsuleCollider with SphereCollider for reliable collision detection.
            // CapsuleCollider behavior is affected by the 90° rotation, making radius calculation complex.
            // SphereCollider with explicit radius in local space (will be scaled by transform) is simpler.
            CapsuleCollider capCol = cylinder.GetComponent<CapsuleCollider>();
            if (capCol != null)
            {
                Object.Destroy(capCol);
            }
            SphereCollider sphereCol = cylinder.AddComponent<SphereCollider>();
            // Local radius 0.5 * scale (diameter) = worldRadius, which is nodeRadius * tileSize
            sphereCol.radius = 0.5f;
            sphereCol.isTrigger = true;
            // Tag the cylinder so WallCollisionChecker can identify it
            cylinder.tag = "MazeNode";

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

            // Render walls along all edges (includes frontier end caps)
            tileCount += RenderEdgeWalls(forestState.Edges, forestState.Nodes, mazeOrigin);

            // Render walls around all nodes
            foreach (var node in forestState.Nodes)
            {
                tileCount += RenderNodeWalls(node, forestState.Edges, mazeOrigin);
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
                    // ARCHITECTURE: Place ALL walls unconditionally.
                    // WallCollisionChecker component handles physics-based collision removal.
                    for (float dist = stepSize; dist <= wallBorderDepth + stepSize; dist += stepSize * 0.5f)
                    {
                        foreach (float side in new[] { 1f, -1f })
                        {
                            Vector2 checkPos = pathPos + seg.Perpendicular * side * dist;

                            // Skip if already occupied by path (this is about path positions, not wall collision)
                            if (IsPositionOccupied(checkPos, occupiedPositions)) continue;

                            // REMOVED: Manual distance checks for insideNode and CheckWallPathIntersection
                            // These violated the architecture. Wall collision removal is handled by
                            // WallCollisionChecker component using Unity physics.

                            // Place the wall - WallCollisionChecker handles collision removal
                            wallPositions.Add(checkPos);
                            float orientationDegrees = Mathf.Atan2(seg.Perpendicular.y, seg.Perpendicular.x) * Mathf.Rad2Deg;
                            if (side < 0) orientationDegrees += 180f;

                            // Determine layer based on distance from path for LOD selection
                            int wallLayer = Mathf.Clamp(Mathf.FloorToInt((dist - stepSize) / WALL_SPACING), 0, WALL_DEPTH - 1);

                            Vector3 worldPos = ToVector3(checkPos);
                            CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
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
                            // ARCHITECTURE: Place ALL walls unconditionally.
                            // WallCollisionChecker component handles physics-based collision removal.
                            for (float dist = stepSize; dist <= wallBorderDepth + stepSize; dist += stepSize * 0.5f)
                            {
                                foreach (float side in new[] { 1f, -1f })
                                {
                                    Vector2 checkPos = pathPos + perpendicular * side * dist;

                                    // Skip if already occupied by path (this is about path positions, not wall collision)
                                    if (IsPositionOccupied(checkPos, occupiedPositions)) continue;

                                    // REMOVED: Manual distance checks for insideNode and CheckWallPathIntersection
                                    // These violated the architecture. Wall collision removal is handled by
                                    // WallCollisionChecker component using Unity physics.

                                    // Place the wall - WallCollisionChecker handles collision removal
                                    wallPositions.Add(checkPos);
                                    float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                                    if (side < 0) orientationDegrees += 180f;

                                    // Determine layer based on distance from path for LOD selection
                                    int wallLayer = Mathf.Clamp(Mathf.FloorToInt((dist - stepSize) / WALL_SPACING), 0, WALL_DEPTH - 1);

                                    Vector3 worldPos = ToVector3(checkPos);
                                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
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
        /// Gets an adjusted wall position.
        /// ARCHITECTURE: Wall overlap IS allowed. Walls are placed unconditionally.
        /// Collision detection and removal is handled by WallCollisionChecker component using Unity physics.
        /// DO NOT add distance checks or intersection tests here - that violates the architecture.
        /// </summary>
        private Vector2? GetAdjustedWallPosition(Vector2 wallPos, Vector2 pushDirection,
            PlanarForestMazeGenerator.ForestMapState forestState, List<Vector2> wallPositions)
        {
            // ARCHITECTURE: Wall overlap is allowed and encouraged.
            // DO NOT check for node column intersection here - that's a manual distance check.
            // DO NOT check for path tile intersection here - that's a manual distance check.
            //
            // Wall collision removal is handled AFTER placement by WallCollisionChecker component
            // which uses Unity physics (OverlapSphere) to detect collisions.
            //
            // Simply return the position - ALL walls get placed, then physics handles removal.
            return wallPos;
        }

        /// <summary>
        /// DEPRECATED: Manual intersection checks are prohibited.
        /// Wall collision removal is handled by WallCollisionChecker using Unity physics.
        /// This method exists only for backwards compatibility and throws an exception.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall intersection checks must use Unity physics (WallCollisionChecker), not manual distance checks. This method will throw.", true)]
        private Vector2? CheckWallPathIntersection(Vector2 wallPos, float wallRadius)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] CheckWallPathIntersection is DEPRECATED. " +
                "Wall collision detection must use Unity physics via WallCollisionChecker component. " +
                "DO NOT use manual distance/intersection checks to determine wall validity.");
        }

        /// <summary>
        /// DEPRECATED: Manual distance checks are prohibited.
        /// Wall collision detection is handled by WallCollisionChecker using Unity physics.
        /// This method exists only for backwards compatibility and throws an exception.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall validity checks must use Unity physics (WallCollisionChecker), not manual distance checks. This method will throw.", true)]
        private bool IsWallPositionValid(Vector2 wallPos, PlanarForestMazeGenerator.ForestMapState forestState,
            List<Vector2> wallPositions)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] IsWallPositionValid is DEPRECATED. " +
                "Wall collision detection must use Unity physics via WallCollisionChecker component. " +
                "DO NOT use manual distance checks to determine wall validity.");
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

                    // Convert 1-based layer to 0-based for LOD selection
                    int wallLayer = Mathf.Clamp(layer - 1, 0, WALL_DEPTH - 1);

                    Vector3 worldPos = ToVector3(adjustedPos.Value);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
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
                // Determine wall layer based on distance from node edge for LOD selection
                int wallLayer = Mathf.Clamp(Mathf.FloorToInt((r - startRadius) / WALL_SPACING), 0, WALL_DEPTH - 1);

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

                    // Orientation facing inward toward node center (add 180 degrees)
                    float orientationDegrees = (currentAngle * Mathf.Rad2Deg) + 180f;

                    Vector3 worldPos = ToVector3(adjustedPos.Value);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
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
        /// Renders complete end cap walls at the end of a frontier edge.
        /// Fills a rectangular area past the frontier endpoint with walls at regular intervals.
        /// Portal at origin, frontierDir is +Y direction (into unexplored area).
        /// </summary>
        private int RenderFrontierEndCap(Vector2 frontierEnd, Vector2 frontierDir, Transform mazeOrigin,
            float stepSize, float pathHalfWidth, int wallDepth, float wallSpacing, GraphElementWallContainer wallContainer)
        {
            int tileCount = 0;
            Vector2 perpendicular = new Vector2(-frontierDir.y, frontierDir.x);

            // Fill a rectangular area past the frontier endpoint
            // Width: from -maxWidth to +maxWidth (covers path + wall depth on each side)
            // Depth: from 0 to extensionDistance (past the endpoint)
            float maxWidth = pathHalfWidth + wallSpacing * wallDepth;
            float extensionDistance = wallSpacing * (wallDepth + 1);

            // Calculate number of steps in each direction
            int widthSteps = Mathf.CeilToInt((maxWidth * 2) / stepSize);
            int depthSteps = Mathf.CeilToInt(extensionDistance / stepSize);

            // Rear wall orientation (faces back toward the path)
            float rearOrientationDegrees = Mathf.Atan2(-frontierDir.y, -frontierDir.x) * Mathf.Rad2Deg;

            // Fill the rectangular area with walls
            for (int depthStep = 0; depthStep <= depthSteps; depthStep++)
            {
                float alongPath = depthStep * stepSize;
                Vector2 rowCenter = frontierEnd + frontierDir * alongPath;

                for (int widthStep = 0; widthStep <= widthSteps; widthStep++)
                {
                    float t = widthSteps > 0 ? (float)widthStep / widthSteps : 0.5f;
                    float xOffset = Mathf.Lerp(-maxWidth, maxWidth, t);
                    Vector2 wallPos = rowCenter + perpendicular * xOffset;

                    // Determine wall layer based on distance from center (for LOD)
                    float distFromCenter = Mathf.Abs(xOffset);
                    int layer = Mathf.Clamp(Mathf.FloorToInt(distFromCenter / wallSpacing), 0, wallDepth - 1);

                    Vector3 worldPos = ToVector3(wallPos);
                    CreateWorldSpaceTile(worldPos, rearOrientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: layer, wallContainer: wallContainer);
                    tileCount++;
                }
            }

            return tileCount;
        }

        /// <summary>
        /// DEPRECATED: Manual distance checks are prohibited.
        /// Wall collision detection is handled by WallCollisionChecker using Unity physics.
        /// This method exists only for backwards compatibility and throws an exception.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall proximity checks must use Unity physics (WallCollisionChecker), not manual distance checks. This method will throw.", true)]
        private bool IsPositionTooCloseToEdge(Vector2 position, PlanarForestMazeGenerator.Edge edgeToSkip,
            IEnumerable<PlanarForestMazeGenerator.Edge> allEdges, float clearance)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] IsPositionTooCloseToEdge is DEPRECATED. " +
                "Wall collision detection must use Unity physics via WallCollisionChecker component. " +
                "DO NOT use manual distance checks to determine wall validity.");
        }

        /// <summary>
        /// Renders walls along edges (perpendicular to edge direction).
        /// Walls are created as children of a GraphElementWallContainer for each edge.
        /// </summary>
        private int RenderEdgeWalls(IEnumerable<PlanarForestMazeGenerator.Edge> edges,
            IEnumerable<PlanarForestMazeGenerator.Node> allNodes, Transform mazeOrigin,
            IEnumerable<PlanarForestMazeGenerator.Edge> allEdges = null)
        {
            int tileCount = 0;
            int edgeIndex = 0;

            foreach (var edge in edges)
            {
                if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                {
                    edgeIndex++;
                    continue;
                }

                // Create wall container for this edge
                Vector2 edgeStart = edge.PolylinePoints[0];
                Vector2 edgeEnd = edge.PolylinePoints[edge.PolylinePoints.Count - 1];

                GameObject containerObj = new GameObject();
                containerObj.transform.SetParent(tilesParent);
                var wallContainer = containerObj.AddComponent<GraphElementWallContainer>();
                wallContainer.InitializeForEdge(edgeIndex, edgeStart, edgeEnd);

                for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                {
                    Vector2 segStart = edge.PolylinePoints[i];
                    Vector2 segEnd = edge.PolylinePoints[i + 1];
                    Vector2 direction = (segEnd - segStart).normalized;
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                    float segmentLength = Vector2.Distance(segStart, segEnd);

                    int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / WALL_STEP_SIZE));
                    for (int j = 0; j <= numSteps; j++)
                    {
                        float t = numSteps > 0 ? (float)j / numSteps : 0;
                        Vector2 centerPos = Vector2.Lerp(segStart, segEnd, t);

                        // ARCHITECTURE: Place ALL walls unconditionally.
                        // WallCollisionChecker handles physics-based collision removal.
                        foreach (float side in new[] { 1f, -1f })
                        {
                            float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                            if (side < 0) orientationDegrees += 180f;

                            for (int layer = 0; layer < WALL_DEPTH; layer++)
                            {
                                float wallOffset = PATH_HALF_WIDTH + WALL_SPACING * (layer + 1);
                                Vector2 wallPos = centerPos + perpendicular * side * wallOffset;

                                Vector3 worldPos = ToVector3(wallPos);
                                CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: layer, wallContainer: wallContainer);
                                tileCount++;
                            }
                        }
                    }
                }

                // Add frontier end cap if this is a partial edge
                if (edge.Partial)
                {
                    int lastIdx = edge.PolylinePoints.Count - 1;
                    Vector2 frontierEnd = edge.PolylinePoints[lastIdx];
                    Vector2 frontierDir = (edge.PolylinePoints[lastIdx] - edge.PolylinePoints[lastIdx - 1]).normalized;

                    tileCount += RenderFrontierEndCap(frontierEnd, frontierDir, mazeOrigin,
                        WALL_STEP_SIZE, PATH_HALF_WIDTH, WALL_DEPTH, WALL_SPACING, wallContainer);
                }

                // Trigger collision checks for walls in this container
                wallContainer.TriggerWallCollisionChecks();

                edgeIndex++;
            }

            return tileCount;
        }

        /// <summary>
        /// Renders wall rings around a single node, with clearance for edge intersections.
        /// Walls are created as children of a GraphElementWallContainer.
        /// </summary>
        private int RenderNodeWalls(PlanarForestMazeGenerator.Node node,
            IEnumerable<PlanarForestMazeGenerator.Edge> allEdges, Transform mazeOrigin)
        {
            int tileCount = 0;
            float edgeAngleClearance = EDGE_ANGLE_CLEARANCE_DEG * Mathf.Deg2Rad;

            // Create wall container for this node
            GameObject containerObj = new GameObject();
            containerObj.transform.SetParent(tilesParent);
            var wallContainer = containerObj.AddComponent<GraphElementWallContainer>();
            wallContainer.InitializeForNode(node.Id, node.Position);

            // Create all wall positions for this node (includes layer for LOD selection)
            var nodeWalls = new List<(Vector2 pos, float angle, int layer)>();
            for (int layer = 0; layer < WALL_DEPTH; layer++)
            {
                float ringRadius = nodeRadius + WALL_SPACING * (layer + 0.5f);
                int numWalls = Mathf.Max(32, (int)(ringRadius * 2f * Mathf.PI / WALL_STEP_SIZE));

                for (int i = 0; i < numWalls; i++)
                {
                    float angle = (float)i / numWalls * 2f * Mathf.PI;
                    Vector2 wallPos = node.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    nodeWalls.Add((wallPos, angle, layer));
                }
            }

            // Use the node's stored UsedAngles directly (much faster than iterating all edges)
            var edgeAngles = new List<float>();
            foreach (var angle in node.UsedAngles)
            {
                float normalizedAngle = angle;
                if (normalizedAngle < 0) normalizedAngle += 2f * Mathf.PI;
                edgeAngles.Add(normalizedAngle);
            }

            // Create walls, skipping those within clearance of any edge angle
            foreach (var (wallPos, wallAngle, wallLayer) in nodeWalls)
            {
                bool inClearance = false;
                foreach (float edgeAngle in edgeAngles)
                {
                    float angleDiff = Mathf.Abs(wallAngle - edgeAngle);
                    if (angleDiff > Mathf.PI)
                        angleDiff = 2f * Mathf.PI - angleDiff;

                    if (angleDiff < edgeAngleClearance)
                    {
                        inClearance = true;
                        break;
                    }
                }

                if (!inClearance)
                {
                    float orientationDegrees = (wallAngle * Mathf.Rad2Deg) + 180f;
                    Vector3 worldPos = ToVector3(wallPos);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer, wallContainer: wallContainer);
                    tileCount++;
                }
            }

            // Trigger collision checks for walls in this container
            wallContainer.TriggerWallCollisionChecks();

            return tileCount;
        }

        /// <summary>
        /// Regenerates walls around a specific node.
        /// DEPRECATED: This method used manual distance-based wall removal which violates the architecture.
        /// Wall removal must ONLY happen via Unity physics collision detection (WallCollisionChecker).
        /// Now simply renders new walls at the node - existing walls with collisions will self-remove via physics.
        /// </summary>
        [System.Obsolete("Wall removal via distance checks is prohibited. Use RenderNodeWalls directly - walls self-remove via WallCollisionChecker.")]
        public void RegenerateNodeWalls(PlanarForestMazeGenerator.Node node, IEnumerable<PlanarForestMazeGenerator.Edge> allEdges)
        {
            // ARCHITECTURE VIOLATION PREVENTION:
            // The old implementation removed walls based on distance checks (dist < maxWallRadius).
            // This violates the rule: walls may ONLY be removed via Unity physics collision detection.
            //
            // New behavior: Just render new walls. The WallCollisionChecker component on each wall
            // will detect physics collisions and destroy walls that overlap with nodes/edges.
            // Existing walls in the area will remain unless they have physics collisions.

            if (tilesParent == null || node == null)
            {
                return;
            }

            // Render walls for this node - WallCollisionChecker handles collision-based removal
            RenderNodeWalls(node, allEdges, tilesParent);
        }

        /// <summary>
        /// Triggers wall collision checks for a specific edge's wall container.
        /// Call this after adding new geometry that may collide with existing walls.
        /// </summary>
        public void TriggerEdgeWallCollisionChecks(int edgeIndex)
        {
            if (tilesParent == null) return;

            string containerName = $"EdgeWalls_{edgeIndex}";
            foreach (Transform child in tilesParent)
            {
                if (child.name == containerName)
                {
                    var wallContainer = child.GetComponent<GraphElementWallContainer>();
                    if (wallContainer != null)
                    {
                        wallContainer.TriggerWallCollisionChecks();
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Triggers wall collision checks for a specific node's wall container.
        /// Call this after adding new geometry that may collide with existing walls.
        /// </summary>
        public void TriggerNodeWallCollisionChecks(int nodeId)
        {
            if (tilesParent == null) return;

            string containerName = $"NodeWalls_{nodeId}";
            foreach (Transform child in tilesParent)
            {
                if (child.name == containerName)
                {
                    var wallContainer = child.GetComponent<GraphElementWallContainer>();
                    if (wallContainer != null)
                    {
                        wallContainer.TriggerWallCollisionChecks();
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Computes a spatial hash key for fast position lookup.
        /// </summary>
        private long GetPositionHashKey(Vector2 pos)
        {
            int x = Mathf.FloorToInt(pos.x / POSITION_HASH_CELL_SIZE);
            int y = Mathf.FloorToInt(pos.y / POSITION_HASH_CELL_SIZE);
            return ((long)x << 32) | (uint)y;
        }

        /// <summary>
        /// Adds a position to the spatial hash.
        /// </summary>
        private void AddToPositionHash(Vector2 pos)
        {
            if (occupiedPositionHash == null)
                occupiedPositionHash = new Dictionary<long, List<Vector2>>();
            long key = GetPositionHashKey(pos);
            if (!occupiedPositionHash.TryGetValue(key, out var list))
            {
                list = new List<Vector2>();
                occupiedPositionHash[key] = list;
            }
            list.Add(pos);
        }

        /// <summary>
        /// Removes a position from the spatial hash (distance-based matching).
        /// </summary>
        private void RemoveFromPositionHash(Vector2 pos, float threshold = OVERLAP_THRESHOLD)
        {
            if (occupiedPositionHash == null || occupiedPositionHash.Count == 0)
                return;

            // Check the cell and adjacent cells for the position
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = Mathf.FloorToInt(pos.x / POSITION_HASH_CELL_SIZE) + dx;
                    int y = Mathf.FloorToInt(pos.y / POSITION_HASH_CELL_SIZE) + dy;
                    long neighborKey = ((long)x << 32) | (uint)y;

                    if (occupiedPositionHash.TryGetValue(neighborKey, out var cellPositions))
                    {
                        for (int i = cellPositions.Count - 1; i >= 0; i--)
                        {
                            if (Vector2.Distance(cellPositions[i], pos) < threshold)
                            {
                                cellPositions.RemoveAt(i);
                                return; // Found and removed
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a position is already occupied using spatial hash (O(1) average).
        /// Returns true if the position overlaps with an existing one.
        /// </summary>
        private bool IsPositionOccupied(Vector2 pos, List<Vector2> positions, float threshold = OVERLAP_THRESHOLD)
        {
            // Use spatial hash for fast lookup if available
            if (occupiedPositionHash != null && occupiedPositionHash.Count > 0)
            {
                // Check the cell and adjacent cells (3x3 neighborhood) because positions near cell boundaries
                // might be in adjacent cells but still within threshold distance
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int x = Mathf.FloorToInt(pos.x / POSITION_HASH_CELL_SIZE) + dx;
                        int y = Mathf.FloorToInt(pos.y / POSITION_HASH_CELL_SIZE) + dy;
                        long neighborKey = ((long)x << 32) | (uint)y;
                        if (occupiedPositionHash.TryGetValue(neighborKey, out var cellPositions))
                        {
                            // Check actual distances to positions in this cell
                            foreach (var existingPos in cellPositions)
                            {
                                if (Vector2.Distance(existingPos, pos) < threshold)
                                    return true;
                            }
                        }
                    }
                }
                return false;
            }

            // Fallback to linear search if hash not initialized
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
        /// <param name="wallLayer">For walls: 0 = front rank (full detail), 1+ = interior (LOD2)</param>
        /// <param name="wallContainer">Optional container for walls - walls will be parented under this</param>
        private void CreateWorldSpaceTile(Vector3 worldPos, float orientationDegrees, char symbol, Transform mazeOrigin, bool isWall, int wallLayer = 0, GraphElementWallContainer wallContainer = null)
        {
            // For flat tiles on XY plane, rotate only around Z axis
            Quaternion tileRotation = Quaternion.Euler(0f, 0f, orientationDegrees);

            // For other prefabs designed Y-up that need to lie flat
            Quaternion flatPrefabRotation = Quaternion.Euler(-90f, 0f, orientationDegrees);

            GameObject tileObj = null;
            Color color = GetColorForSymbol(symbol, !isWall);

            // Skip wall generation if disabled
            if (symbol == '#' && disableWalls)
            {
                return;
            }

            // Add random jitter for walls
            if (symbol == '#')
            {
                float jitterX = Random.Range(-0.02f, 0.02f);
                float jitterY = Random.Range(-0.02f, 0.02f);
                worldPos += new Vector3(jitterX, jitterY, 0f);
            }

            // Determine parent for this tile
            Transform tileParent = (symbol == '#' && wallContainer != null)
                ? wallContainer.transform
                : tilesParent;

            if (symbol == '#' && wallPrefab != null)
            {
                // Use LOD2 prefab for interior walls (layer > 0), full detail for front rank (layer 0)
                GameObject prefabToUse = (wallLayer > 0 && wallPrefabLOD2 != null) ? wallPrefabLOD2 : wallPrefab;
                tileObj = Instantiate(prefabToUse, tileParent);
                tileObj.transform.position = worldPos;
                // Apply Z-axis rotation only so model's X axis aligns radially (toward node center for node walls)
                // Using tileRotation (Z-only) instead of flatPrefabRotation (which tilts on X-axis)
                tileObj.transform.rotation = tileRotation;
                // Wall models use prefab default scale (no additional scaling)
                wallTiles?.Add(tileObj);

                // Ensure wall has a collider for physics-based wall re-checking
                // (needed so WallCollisionChecker.RecheckWallsInArea can find it via OverlapSphere)
                Collider wallCollider = tileObj.GetComponent<Collider>();
                if (wallCollider == null)
                {
                    // Add a sphere collider if no collider exists
                    SphereCollider sphereCol = tileObj.AddComponent<SphereCollider>();
                    sphereCol.radius = 0.3f;
                    sphereCol.isTrigger = true;
                }
                else
                {
                    wallCollider.isTrigger = true;
                }

                // ARCHITECTURE: Add WallCollisionChecker to handle physics-based collision removal.
                // This component will use Unity physics to detect collisions with nodes/edges
                // and destroy the wall if it collides. This is the ONLY valid way to remove walls.
                tileObj.AddComponent<WallCollisionChecker>();
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
                {
                    wallTiles?.Add(tileObj);
                    // Make collider a trigger so it doesn't block movement
                    Collider wallCol = tileObj.GetComponent<Collider>();
                    if (wallCol != null)
                    {
                        wallCol.isTrigger = true;
                    }
                    // ARCHITECTURE: Add WallCollisionChecker for physics-based collision removal
                    tileObj.AddComponent<WallCollisionChecker>();
                }
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

            // Tag path tiles for physics-based wall collision detection.
            // WallCollisionChecker uses OverlapSphere to find path tiles.
            if (symbol == '.' || symbol == 'H' || symbol == 'N')
            {
                tileObj.tag = "MazePath";
                // Reduce collider to 2/3 size to prevent false wall collisions on curves
                // Visual mesh stays full size, only collision detection is smaller
                BoxCollider boxCol = tileObj.GetComponent<BoxCollider>();
                if (boxCol != null)
                {
                    boxCol.size = new Vector3(0.667f, 0.667f, 1f);
                    boxCol.isTrigger = true;
                }
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
        }

        private Material CreatePBRMaterialForSymbol(char symbol, Color color)
        {
            switch (symbol)
            {
                case '#': return PBRMaterialFactory.CreateWallMaterial(color);
                case ';': return PBRMaterialFactory.CreateUndergrowthMaterial(color);
                case '~': return PBRMaterialFactory.CreateWaterMaterial(color);
                case '.': return GetPathMaterial();
                case 'H': return PBRMaterialFactory.CreateEmissiveMaterial(color, color * 1.5f, 1.0f);
                default: return PBRMaterialFactory.CreateLitMaterial(color);
            }
        }

        /// <summary>
        /// Returns the material for path tiles. Uses the heart's EarthenRingGround material if available,
        /// otherwise falls back to the standard path material.
        /// </summary>
        private Material GetPathMaterial()
        {
            if (useHeartMaterialForPaths && heartGroundMaterial != null)
            {
                return heartGroundMaterial;
            }
            return PBRMaterialFactory.CreatePathMaterial(pathColor);
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
                // Signal that maze is regenerating so walls can be destroyed without error
                WallCollisionChecker.IsMazeRegenerating = true;
                foreach (Transform child in tilesParent)
                    Destroy(child.gameObject);
                WallCollisionChecker.IsMazeRegenerating = false;
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
            if (occupiedPositionHash == null)
                occupiedPositionHash = new Dictionary<long, List<Vector2>>();

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
                        AddToPositionHash(pos2D);
                    }
                }
            }
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
            if (occupiedPositionHash == null)
                occupiedPositionHash = new Dictionary<long, List<Vector2>>();

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
                        AddToPositionHash(pos2D);
                        tilesCreated++;
                    }
                }
            }
        }

        /// <summary>
        /// Adds wall tiles around newly added path tiles.
        /// Uses shared helpers for consistency with initial render.
        /// </summary>
        public void AddWallsIncremental(List<ForestMaze.PlanarForestMazeGenerator.Edge> newEdges,
            ForestMaze.PlanarForestMazeGenerator.Node newNode)
        {
            if (mazeGridBehaviour == null || tilesParent == null)
                return;

            var forestState = mazeGridBehaviour.ForestMapState;
            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            // Render walls along new edges (includes frontier end caps)
            // Pass all edges so new walls don't overlap existing edge paths
            if (newEdges != null && newEdges.Count > 0)
            {
                RenderEdgeWalls(newEdges, forestState.Nodes, mazeOrigin, forestState.Edges);
            }

            // Render walls around the new node
            if (newNode != null)
            {
                RenderNodeWalls(newNode, forestState.Edges, mazeOrigin);
            }
        }

        /// <summary>
        /// Async version of AddWallsIncremental that yields periodically to prevent frame lockup.
        /// Spreads wall generation across multiple frames using a time budget per frame.
        /// </summary>
        public IEnumerator AddWallsIncrementalAsync(List<ForestMaze.PlanarForestMazeGenerator.Edge> newEdges,
            ForestMaze.PlanarForestMazeGenerator.Node newNode)
        {
            if (mazeGridBehaviour == null || tilesParent == null)
                yield break;

            var forestState = mazeGridBehaviour.ForestMapState;
            Transform mazeOrigin = mazeGridBehaviour.MazeOrigin ?? transform;

            // Time budget per frame (in seconds) - aim for 60fps means ~16ms per frame
            // Use 8ms to leave headroom for other game logic
            const float frameBudgetMs = 8f;
            float frameStartTime = Time.realtimeSinceStartup;

            // Render walls along new edges (includes frontier end caps)
            // Pass all edges so new walls don't overlap existing edge paths
            if (newEdges != null && newEdges.Count > 0)
            {
                foreach (var edge in newEdges)
                {
                    yield return StartCoroutine(RenderEdgeWallsAsync(edge, forestState.Nodes, mazeOrigin, frameBudgetMs, forestState.Edges));
                }
            }

            // Render walls around the new node
            if (newNode != null)
            {
                yield return StartCoroutine(RenderNodeWallsAsync(newNode, forestState.Edges, mazeOrigin, frameBudgetMs));
            }
        }

        /// <summary>
        /// Async version of RenderEdgeWalls for a single edge.
        /// Walls are created as children of a GraphElementWallContainer.
        /// </summary>
        private IEnumerator RenderEdgeWallsAsync(PlanarForestMazeGenerator.Edge edge,
            IList<PlanarForestMazeGenerator.Node> nodesList, Transform mazeOrigin, float frameBudgetMs,
            IEnumerable<PlanarForestMazeGenerator.Edge> allEdges = null, int edgeIndex = 0)
        {
            if (edge.PolylinePoints == null || edge.PolylinePoints.Count < 2)
                yield break;

            float frameStartTime = Time.realtimeSinceStartup;
            int wallsCreated = 0;

            // Create wall container for this edge
            Vector2 edgeStart = edge.PolylinePoints[0];
            Vector2 edgeEnd = edge.PolylinePoints[edge.PolylinePoints.Count - 1];

            GameObject containerObj = new GameObject();
            containerObj.transform.SetParent(tilesParent);
            var wallContainer = containerObj.AddComponent<GraphElementWallContainer>();
            wallContainer.InitializeForEdge(edgeIndex, edgeStart, edgeEnd);

            // Generate side walls along each segment
            for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
            {
                Vector2 segStart = edge.PolylinePoints[i];
                Vector2 segEnd = edge.PolylinePoints[i + 1];
                Vector2 direction = (segEnd - segStart).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float segmentLength = Vector2.Distance(segStart, segEnd);

                int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / WALL_STEP_SIZE));

                for (int step = 0; step <= numSteps; step++)
                {
                    float t = numSteps > 0 ? (float)step / numSteps : 0;
                    Vector2 centerPos = Vector2.Lerp(segStart, segEnd, t);

                    foreach (int side in new[] { -1, 1 })
                    {
                        float orientationDegrees = Mathf.Atan2(perpendicular.y, perpendicular.x) * Mathf.Rad2Deg;
                        if (side < 0) orientationDegrees += 180f;

                        for (int layer = 0; layer < WALL_DEPTH; layer++)
                        {
                            float wallOffset = PATH_HALF_WIDTH + WALL_SPACING * (layer + 1);
                            Vector2 wallPos = centerPos + perpendicular * side * wallOffset;

                            Vector3 worldPos = ToVector3(wallPos);
                            CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: layer, wallContainer: wallContainer);
                            wallsCreated++;

                            if (wallsCreated % 20 == 0)
                            {
                                float elapsed = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
                                if (elapsed >= frameBudgetMs)
                                {
                                    yield return null;
                                    frameStartTime = Time.realtimeSinceStartup;
                                }
                            }
                        }
                    }
                }
            }

            // Add frontier end cap if this is a partial edge
            if (edge.Partial)
            {
                int lastIdx = edge.PolylinePoints.Count - 1;
                Vector2 frontierEnd = edge.PolylinePoints[lastIdx];
                Vector2 frontierDir = (edge.PolylinePoints[lastIdx] - edge.PolylinePoints[lastIdx - 1]).normalized;
                RenderFrontierEndCap(frontierEnd, frontierDir, mazeOrigin,
                    WALL_STEP_SIZE, PATH_HALF_WIDTH, WALL_DEPTH, WALL_SPACING, wallContainer);
            }

            // Trigger collision checks for walls in this container
            wallContainer.TriggerWallCollisionChecks();
        }

        /// <summary>
        /// Async version of RenderNodeWalls.
        /// Walls are created as children of a GraphElementWallContainer.
        /// </summary>
        private IEnumerator RenderNodeWallsAsync(PlanarForestMazeGenerator.Node node,
            IEnumerable<PlanarForestMazeGenerator.Edge> allEdges, Transform mazeOrigin, float frameBudgetMs)
        {
            float frameStartTime = Time.realtimeSinceStartup;
            int wallsCreated = 0;
            float edgeAngleClearance = EDGE_ANGLE_CLEARANCE_DEG * Mathf.Deg2Rad;

            // Create wall container for this node
            GameObject containerObj = new GameObject();
            containerObj.transform.SetParent(tilesParent);
            var wallContainer = containerObj.AddComponent<GraphElementWallContainer>();
            wallContainer.InitializeForNode(node.Id, node.Position);

            // Create all wall positions for this node
            var nodeWalls = new List<(Vector2 pos, float angle, int layer)>();
            for (int layer = 0; layer < WALL_DEPTH; layer++)
            {
                float ringRadius = nodeRadius + WALL_SPACING * (layer + 0.5f);
                int numWalls = Mathf.Max(32, (int)(ringRadius * 2f * Mathf.PI / WALL_STEP_SIZE));

                for (int i = 0; i < numWalls; i++)
                {
                    float angle = (float)i / numWalls * 2f * Mathf.PI;
                    Vector2 wallPos = node.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                    nodeWalls.Add((wallPos, angle, layer));
                }
            }

            // Get edge angles from node's UsedAngles
            var edgeAngles = new List<float>();
            foreach (var angle in node.UsedAngles)
            {
                float normalizedAngle = angle;
                if (normalizedAngle < 0) normalizedAngle += 2f * Mathf.PI;
                edgeAngles.Add(normalizedAngle);
            }

            // Place walls, skipping positions near edge angles
            foreach (var (wallPos, wallAngle, wallLayer) in nodeWalls)
            {
                bool tooCloseToEdge = false;
                foreach (var edgeAngle in edgeAngles)
                {
                    float angleDiff = Mathf.Abs(wallAngle - edgeAngle);
                    if (angleDiff > Mathf.PI) angleDiff = 2f * Mathf.PI - angleDiff;
                    if (angleDiff < edgeAngleClearance)
                    {
                        tooCloseToEdge = true;
                        break;
                    }
                }

                if (!tooCloseToEdge)
                {
                    float orientationDegrees = (wallAngle * Mathf.Rad2Deg) + 180f;
                    Vector3 worldPos = ToVector3(wallPos);
                    CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer, wallContainer: wallContainer);
                    wallsCreated++;

                    if (wallsCreated % 20 == 0)
                    {
                        float elapsed = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
                        if (elapsed >= frameBudgetMs)
                        {
                            yield return null;
                            frameStartTime = Time.realtimeSinceStartup;
                        }
                    }
                }
            }

            // Trigger collision checks for walls in this container
            wallContainer.TriggerWallCollisionChecks();
        }

        /// <summary>
        /// DEPRECATED: Do NOT use this method! Wall overlap is allowed by design.
        /// Wall removal should be handled by Unity physics collision detection, not manual checks.
        /// This method violates the architectural rule that walls are removed via physics collisions.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall removal must use Unity physics collision detection, not manual checks. This method will throw.", true)]
        public void RemoveWallsNearPosition(Vector3 position, float radius)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] RemoveWallsNearPosition is DEPRECATED. " +
                "Wall removal must be handled by Unity physics collision detection between walls and graph elements. " +
                "Wall models ARE allowed to overlap. Do NOT use manual intersection checks to remove walls.");
        }

        /// <summary>
        /// DEPRECATED: Do NOT use this method! Wall overlap is allowed by design.
        /// Wall removal should be handled by Unity physics collision detection, not manual checks.
        /// This method violates the architectural rule that walls are removed via physics collisions.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall removal must use Unity physics collision detection, not manual checks. This method will throw.", true)]
        public void RemoveWallsNearPositionsBatched(List<Vector3> positions, float radius)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] RemoveWallsNearPositionsBatched is DEPRECATED. " +
                "Wall removal must be handled by Unity physics collision detection between walls and graph elements. " +
                "Wall models ARE allowed to overlap. Do NOT use manual intersection checks to remove walls.");
        }

        /// <summary>
        /// DEPRECATED: Do NOT use this method! Wall overlap is allowed by design.
        /// Wall removal should be handled by Unity physics collision detection, not manual checks.
        /// This method violates the architectural rule that walls are removed via physics collisions.
        /// Portal end cap walls should be managed by the portal wall system, not manual removal.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall removal must use Unity physics collision detection, not manual checks. This method will throw.", true)]
        public void RemoveWallsPastEndpoint(Vector3 endpoint, Vector3 frontierDirection, float radius, float dirThreshold = -0.3f)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] RemoveWallsPastEndpoint is DEPRECATED. " +
                "Wall removal must be handled by Unity physics collision detection between walls and graph elements. " +
                "Wall models ARE allowed to overlap. Portal end cap walls are managed by the portal wall tracking system (portalWalls list in DynamicMazeGrowth).");
        }

        /// <summary>
        /// DEPRECATED: Do NOT use this method! Wall overlap is allowed by design.
        /// Wall removal should be handled by Unity physics collision detection, not manual intersection checks.
        /// This method violates the architectural rule that walls are removed via physics collisions.
        /// </summary>
        [System.Obsolete("VIOLATION: Wall removal must use Unity physics collision detection, not manual checks. This method will throw.", true)]
        public void RemoveWallsAlongPolyline(List<Vector2> polylinePoints, float pathWidth)
        {
            throw new System.InvalidOperationException(
                "[MazeRenderer] RemoveWallsAlongPolyline is DEPRECATED. " +
                "Wall removal must be handled by Unity physics collision detection between walls and graph elements. " +
                "Wall models ARE allowed to overlap. Do NOT use manual intersection checks to remove walls.");
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
                // Remove from occupied positions (both list and hash)
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

                // Also remove from the spatial hash
                RemoveFromPositionHash(pos2D);

                Destroy(t.gameObject);
                removedCount++;
            }

            if (removedCount > 0)
            {
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
            occupiedPositionHash = new Dictionary<long, List<Vector2>>();

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
                            AddToPositionHash(pos2D);
                        }
                    }
                }
            }

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
                        AddToPositionHash(pos2D);
                        tilesCreated++;

                        if (tilesCreated % batchSize == 0)
                        {
                            yield return null;
                        }
                    }
                }
            }

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
                            // Convert 1-based layer to 0-based for LOD selection
                            int wallLayer = Mathf.Clamp(layer - 1, 0, WALL_DEPTH - 1);

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
                                CreateWorldSpaceTile(worldPos, orientationDegrees, '#', mazeOrigin, isWall: true, wallLayer: wallLayer);
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
        }

        /// <summary>
        /// Destroys old tile container gradually to avoid frame spike.
        /// </summary>
        private IEnumerator DestroyOldTilesGradually(GameObject oldContainer)
        {
            // First, disable the container to hide it immediately
            oldContainer.SetActive(false);
            yield return null;

            // Signal that maze is regenerating so walls can be destroyed without error
            WallCollisionChecker.IsMazeRegenerating = true;

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

            WallCollisionChecker.IsMazeRegenerating = false;

            // Finally destroy the empty container
            Destroy(oldContainer);
        }

        #endregion
    }
}

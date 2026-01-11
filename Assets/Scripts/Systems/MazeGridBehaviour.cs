using System.Collections.Generic;
using UnityEngine;
using ForestMaze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// MonoBehaviour wrapper for world-space maze data.
    /// Handles maze initialization from the planar forest generator and world-space coordinate conversions.
    ///
    /// Pure world-space coordinate system:
    /// - Graph positions ARE world positions
    /// - No grid, no rasterization, no coordinate transforms
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MazeGridBehaviour : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Planar Generator Config")]
        [SerializeField]
        [Tooltip("Configuration for planar organic maze generation")]
        private PlanarForestMazeConfig planarGeneratorConfig = PlanarForestMazeConfig.Default();

        [Header("References")]
        [SerializeField]
        [Tooltip("Transform acting as the origin point for world-space conversions")]
        private Transform mazeOrigin;

        [Header("World-Space Settings")]
        [SerializeField]
        [Tooltip("Tile size in world units for spatial lookups")]
        private float worldSpaceTileSize = 1.0f;

        [Header("Orientation")]
        [SerializeField]
        [Tooltip("Mirror the maze and models through the XY plane to correct vertical orientation.")]
        private bool reflectThroughXYPlane = true;

        [Header("Debug Visualization")]
        [SerializeField]
        private bool drawGizmos = true;

        #endregion

        #region Private Fields

        private WorldSpaceMazeData worldSpaceMazeData;
        private PlanarForestMazeGenerator.ForestMapState forestMapState;
        private Vector3 heartWorldPosition;

        #endregion

        #region Properties

        /// <summary>Gets the forest map state for dynamic maze growth.</summary>
        public PlanarForestMazeGenerator.ForestMapState ForestMapState => forestMapState;

        /// <summary>Gets the world-space maze data for tile lookups.</summary>
        public WorldSpaceMazeData WorldSpaceMazeData => worldSpaceMazeData;

        /// <summary>Gets the world-space tile size for spatial queries.</summary>
        public float WorldSpaceTileSize => worldSpaceTileSize;

        /// <summary>Gets the heart position in world space (node 0 / seed node).</summary>
        public Vector3 HeartWorldPosition => heartWorldPosition;

        /// <summary>Gets the up direction for the maze, accounting for XY-plane reflection.</summary>
        public Vector3 MazeUpDirection
        {
            get
            {
                if (mazeOrigin == null)
                {
                    return Vector3.forward;
                }

                Vector3 mazeUp = mazeOrigin.forward;
                if (mazeUp.sqrMagnitude < 0.0001f)
                {
                    return Vector3.forward;
                }

                return mazeUp.normalized;
            }
        }

        /// <summary>Gets the transform acting as the maze origin for world-space conversions.</summary>
        public Transform MazeOrigin => mazeOrigin;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (mazeOrigin == null)
            {
                mazeOrigin = transform;
            }

            ApplyXYPlaneReflection();
            InitializeFromGraph();
        }

        #endregion

        #region Orientation

        private void ApplyXYPlaneReflection()
        {
            if (!reflectThroughXYPlane || mazeOrigin == null)
            {
                return;
            }

            Vector3 scale = mazeOrigin.localScale;
            mazeOrigin.localScale = scale;

            Vector3 originPosition = mazeOrigin.position;
            originPosition.z = Mathf.Abs(originPosition.z);
            mazeOrigin.position = originPosition;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the maze directly from the graph state in pure world-space.
        /// NO grid, NO coordinate transforms - graph positions ARE world positions.
        /// </summary>
        private void InitializeFromGraph()
        {
            // Generate the graph (no rasterization - graph positions are world positions)
            var result = PlanarForestMazeGenerator.GenerateMazeWithState(
                planarGeneratorConfig.gridWidth,  // Used for initial bounds, not grid
                planarGeneratorConfig.gridHeight,
                planarGeneratorConfig.growthTurns,
                planarGeneratorConfig.randomSeed);

            forestMapState = result.state;

            // Generate world-space maze data directly from graph
            WorldSpaceMazeGenerator.ResetSpawnIdCounter();
            worldSpaceMazeData = WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, worldSpaceTileSize);
            worldSpaceMazeData.RecalculateBounds();

            Debug.Log($"[MazeGridBehaviour] Generated world-space maze with {worldSpaceMazeData.Tiles.Count} tiles");

            // Store heart world position (node 0 / root node)
            if (forestMapState.Nodes.Count > 0)
            {
                var seedNode = forestMapState.Nodes[0];
                heartWorldPosition = new Vector3(seedNode.Position.x, seedNode.Position.y, 0f);
                Debug.Log($"[MazeGridBehaviour] Heart at world position {heartWorldPosition}");
            }
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Converts graph-space coordinates to world position.
        /// In pure world-space mode, this just applies the maze origin offset.
        /// </summary>
        public Vector3 GraphToWorld(Vector2 graphPos)
        {
            if (mazeOrigin == null)
            {
                return new Vector3(graphPos.x, graphPos.y, 0f);
            }

            return mazeOrigin.position + new Vector3(graphPos.x, graphPos.y, 0f);
        }

        /// <summary>
        /// Converts world position to graph-space coordinates.
        /// </summary>
        public Vector2 WorldToGraph(Vector3 worldPos)
        {
            if (mazeOrigin == null)
            {
                return new Vector2(worldPos.x, worldPos.y);
            }

            Vector3 localPos = worldPos - mazeOrigin.position;
            return new Vector2(localPos.x, localPos.y);
        }

        /// <summary>
        /// Gets the tile at a world position.
        /// </summary>
        public WorldSpaceTile GetWorldSpaceTileAt(Vector3 worldPos)
        {
            if (worldSpaceMazeData == null)
            {
                return null;
            }

            Vector2 graphPos = WorldToGraph(worldPos);
            var nearbyTiles = worldSpaceMazeData.GetTilesNear(graphPos, worldSpaceTileSize * 0.5f);

            WorldSpaceTile closestTile = null;
            float closestDist = float.MaxValue;

            foreach (var tile in nearbyTiles)
            {
                float dist = Vector2.Distance(tile.Position, graphPos);
                if (dist < closestDist && dist < tile.Size * 0.5f)
                {
                    closestDist = dist;
                    closestTile = tile;
                }
            }

            return closestTile;
        }

        /// <summary>
        /// Checks if a world position is walkable.
        /// </summary>
        public bool IsWalkableAtWorldPos(Vector3 worldPos)
        {
            if (worldSpaceMazeData == null)
            {
                return false;
            }

            Vector2 graphPos = WorldToGraph(worldPos);
            return worldSpaceMazeData.IsWalkable(graphPos);
        }

        #endregion

        #region Legacy Compatibility Methods

        /// <summary>
        /// Converts world position to approximate grid coordinates.
        /// Provided for legacy compatibility with grid-based code.
        /// </summary>
        public bool WorldToGrid(Vector3 worldPos, out int x, out int y)
        {
            Vector2 graphPos = WorldToGraph(worldPos);
            x = Mathf.RoundToInt(graphPos.x / worldSpaceTileSize);
            y = Mathf.RoundToInt(graphPos.y / worldSpaceTileSize);
            return worldSpaceMazeData != null && worldSpaceMazeData.IsWalkable(graphPos);
        }

        /// <summary>
        /// Converts grid coordinates to world position.
        /// Provided for legacy compatibility with grid-based code.
        /// </summary>
        public Vector3 GridToWorld(int x, int y)
        {
            Vector2 graphPos = new Vector2(x * worldSpaceTileSize, y * worldSpaceTileSize);
            return GraphToWorld(graphPos);
        }

        /// <summary>
        /// Checks if a grid position is a spawn point.
        /// </summary>
        public bool IsSpawnPoint(Vector2Int gridPos)
        {
            if (worldSpaceMazeData == null)
            {
                return false;
            }

            Vector2 worldPos = new Vector2(gridPos.x * worldSpaceTileSize, gridPos.y * worldSpaceTileSize);
            foreach (var spawn in worldSpaceMazeData.SpawnPoints.Values)
            {
                if (Vector2.Distance(new Vector2(spawn.x, spawn.y), worldPos) < worldSpaceTileSize * 0.5f)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets all spawn points as grid positions.
        /// </summary>
        public Dictionary<char, Vector2Int> GetAllSpawnPoints()
        {
            var result = new Dictionary<char, Vector2Int>();
            if (worldSpaceMazeData == null)
            {
                return result;
            }

            foreach (var kvp in worldSpaceMazeData.SpawnPoints)
            {
                int x = Mathf.RoundToInt(kvp.Value.x / worldSpaceTileSize);
                int y = Mathf.RoundToInt(kvp.Value.y / worldSpaceTileSize);
                result[kvp.Key] = new Vector2Int(x, y);
            }
            return result;
        }

        /// <summary>
        /// Legacy Grid shim for backwards compatibility with visitor controllers.
        /// </summary>
        public LegacyGridShim Grid => _legacyGridShim ??= new LegacyGridShim(this);
        private LegacyGridShim _legacyGridShim;

        /// <summary>
        /// Shim class providing legacy Grid API for visitor controllers.
        /// </summary>
        public class LegacyGridShim
        {
            private readonly MazeGridBehaviour _maze;

            public LegacyGridShim(MazeGridBehaviour maze)
            {
                _maze = maze;
            }

            /// <summary>
            /// Gets a node at the specified grid position.
            /// Returns a shim node with walkability information.
            /// </summary>
            public LegacyNodeShim GetNode(int x, int y)
            {
                if (_maze.worldSpaceMazeData == null)
                {
                    return null;
                }

                Vector2 worldPos = new Vector2(x * _maze.worldSpaceTileSize, y * _maze.worldSpaceTileSize);
                bool walkable = _maze.worldSpaceMazeData.IsWalkable(worldPos);
                return new LegacyNodeShim(x, y, walkable);
            }

            /// <summary>
            /// Gets the speed multiplier at a grid position.
            /// </summary>
            public float GetSpeedMultiplier(int x, int y)
            {
                return 1.0f; // Default speed in world-space mode
            }

            /// <summary>
            /// Gets the movement cost at a grid position.
            /// </summary>
            public float GetMoveCost(int x, int y, float attractionMultiplier)
            {
                return 1.0f; // Default cost in world-space mode
            }
        }

        /// <summary>
        /// Shim class representing a legacy grid node.
        /// </summary>
        public class LegacyNodeShim
        {
            public int x;
            public int y;
            public bool walkable;
            public TileType terrain = TileType.Path;

            public LegacyNodeShim(int x, int y, bool walkable)
            {
                this.x = x;
                this.y = y;
                this.walkable = walkable;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!drawGizmos || worldSpaceMazeData == null || mazeOrigin == null)
            {
                return;
            }

            Color originalColor = Gizmos.color;

            // Draw heart position
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(heartWorldPosition, 2f);

            // Draw nodes
            if (forestMapState != null)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
                foreach (var node in forestMapState.Nodes)
                {
                    Vector3 nodePos = GraphToWorld(node.Position);
                    Gizmos.DrawWireSphere(nodePos, 1.5f);
                }

                // Draw edges
                Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.5f);
                foreach (var edge in forestMapState.Edges)
                {
                    if (edge.PolylinePoints.Count < 2) continue;

                    for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
                    {
                        Vector3 p1 = GraphToWorld(edge.PolylinePoints[i]);
                        Vector3 p2 = GraphToWorld(edge.PolylinePoints[i + 1]);
                        Gizmos.DrawLine(p1, p2);
                    }
                }
            }

            Gizmos.color = originalColor;
        }

        #endregion

        #region Debug Controls

        public void SetDrawGizmos(bool value)
        {
            drawGizmos = value;
        }

        #endregion

        #region Maze Regeneration

        /// <summary>
        /// Regenerates the maze with a new random seed.
        /// </summary>
        public void RegenerateMaze()
        {
            // Use current time as random seed for variety
            planarGeneratorConfig.randomSeed = System.DateTime.Now.Millisecond + (System.DateTime.Now.Second * 1000);

            // Regenerate maze
            InitializeFromGraph();

            // Reposition heart marker
            var heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
            if (heart != null)
            {
                heart.PositionFromMazeGrid();
                heart.ApplyAttraction();
            }

            // Notify renderer to refresh
            var renderer = GetComponent<MazeRenderer>();
            if (renderer != null)
            {
                renderer.RefreshMaze();
            }
        }

        #endregion

        #region Spawn Point API (World-Space)

        /// <summary>
        /// Gets a spawn point position in world space by ID.
        /// </summary>
        public bool TryGetSpawnPointWorldPos(char spawnId, out Vector3 worldPos)
        {
            worldPos = Vector3.zero;
            if (worldSpaceMazeData == null)
            {
                return false;
            }

            var pos = worldSpaceMazeData.GetSpawnPointPosition(spawnId);
            if (pos.HasValue)
            {
                worldPos = pos.Value;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets all spawn point IDs.
        /// </summary>
        public IEnumerable<char> GetAllSpawnPointIds()
        {
            if (worldSpaceMazeData == null)
            {
                return new List<char>();
            }
            return worldSpaceMazeData.SpawnPoints.Keys;
        }

        /// <summary>
        /// Gets the number of spawn points.
        /// </summary>
        public int GetSpawnPointCount()
        {
            if (worldSpaceMazeData == null)
            {
                return 0;
            }
            return worldSpaceMazeData.SpawnPoints.Count;
        }

        #endregion
    }
}

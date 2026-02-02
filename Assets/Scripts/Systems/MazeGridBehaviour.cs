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
        [Tooltip("Parent transform for maze objects (purely for hierarchy organization)")]
        private Transform mazeOrigin;

        [Header("World-Space Settings")]
        [SerializeField]
        [Tooltip("Tile size in world units for spatial lookups")]
        private float worldSpaceTileSize = 1.0f;

        [Header("Debug Visualization")]
        [SerializeField]
        private bool drawGizmos = true;

        [SerializeField]
        [Tooltip("Draw individual tiles with their edge/node connectivity info")]
        private bool drawTileConnectivity = false;

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

        /// <summary>Gets the tile size (alias for WorldSpaceTileSize).</summary>
        public float TileSize => worldSpaceTileSize;

        /// <summary>Gets the heart position in world space (node 0 / seed node).</summary>
        public Vector3 HeartWorldPosition => heartWorldPosition;

        /// <summary>Gets the entrance (first spawn point) position in world space.</summary>
        public Vector3 EntranceWorldPosition
        {
            get
            {
                if (worldSpaceMazeData != null && worldSpaceMazeData.SpawnPointCount > 0)
                {
                    // Return the first spawn point as the entrance (real-time from transform)
                    var positions = worldSpaceMazeData.GetSpawnPointPositions();
                    foreach (var kvp in positions)
                    {
                        return kvp.Value;
                    }
                }
                return Vector3.zero;
            }
        }

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

            // RandomManager MUST be initialized by GameController (execution order -200) BEFORE this runs (execution order -100)
            // This ensures consistent seed-based maze generation between editor and build
            if (!RandomManager.IsInitialized)
            {
                Debug.LogError("[MazeGridBehaviour] RandomManager not initialized! GameController must run first. Check execution order.");
                // Fallback to prevent crash, but maze may not match expected seed
                RandomManager.Initialize();
            }

            // Get initial seed from RandomManager for consistency
            planarGeneratorConfig.randomSeed = RandomManager.NextSeed();

            InitializeFromGraph();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the maze directly from the graph state in pure world-space.
        /// Graph positions ARE world positions - no transforms needed.
        /// Starts with just the seed (root + one frontier edge) for step-by-step growth debugging.
        /// </summary>
        private void InitializeFromGraph()
        {
            // Generate the graph - start with seed only for step-by-step debugging
            // Dynamic growth will handle adding nodes one at a time
            var result = PlanarForestMazeGenerator.GenerateMazeWithState(
                planarGeneratorConfig.gridWidth,
                planarGeneratorConfig.gridHeight,
                0, // Start with 0 turns (seed only)
                planarGeneratorConfig.randomSeed,
                seedOnly: true);

            forestMapState = result.state;

            // Generate world-space maze data directly from graph
            WorldSpaceMazeGenerator.ResetSpawnIdCounter();
            worldSpaceMazeData = WorldSpaceMazeGenerator.GenerateFromGraph(forestMapState, worldSpaceTileSize);
            worldSpaceMazeData.RecalculateBounds();

            // Store heart world position (node 0 / root node)
            if (forestMapState.Nodes.Count > 0)
            {
                var seedNode = forestMapState.Nodes[0];
                heartWorldPosition = new Vector3(seedNode.Position.x, seedNode.Position.y, 0f);
            }
        }

        #endregion

        #region Coordinate Conversion

        /// <summary>
        /// Converts Vector2 position to Vector3 world position.
        /// Since ForestMapState positions are already in world space, this is a simple type conversion.
        /// </summary>
        public Vector3 GraphToWorld(Vector2 worldPos2D)
        {
            return new Vector3(worldPos2D.x, worldPos2D.y, 0f);
        }

        /// <summary>
        /// Converts Vector3 world position to Vector2.
        /// Since ForestMapState positions are already in world space, this is a simple type conversion.
        /// </summary>
        public Vector2 WorldToGraph(Vector3 worldPos)
        {
            return new Vector2(worldPos.x, worldPos.y);
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

            // Draw tile connectivity info if enabled
            if (drawTileConnectivity)
            {
                DrawTileConnectivityGizmos();
            }

            Gizmos.color = originalColor;
        }

        /// <summary>
        /// Draws gizmos showing tile connectivity information for debugging pathfinding.
        /// Edge tiles: orange, Node tiles: green, with lines showing which tiles connect.
        /// </summary>
        private void DrawTileConnectivityGizmos()
        {
            if (worldSpaceMazeData == null) return;

            float neighborRadius = worldSpaceMazeData.TileSize * 1.42f;

            foreach (var tile in worldSpaceMazeData.Tiles)
            {
                if (!tile.Walkable) continue;

                Vector3 pos = new Vector3(tile.Position.x, tile.Position.y, 0f);

                // Color based on tile type
                if (tile.EdgeIndex >= 0)
                {
                    // Edge tile - orange with different hues per edge
                    float hue = (tile.EdgeIndex * 0.137f) % 1f; // Golden ratio for distinct colors
                    Gizmos.color = Color.HSVToRGB(hue, 0.8f, 0.9f);
                    Gizmos.DrawWireCube(pos, Vector3.one * 0.3f);
                }
                else if (tile.NodeIndex >= 0)
                {
                    // Node tile - green with different hues per node
                    float hue = (tile.NodeIndex * 0.137f + 0.33f) % 1f;
                    Gizmos.color = Color.HSVToRGB(hue, 0.6f, 0.9f);
                    Gizmos.DrawWireSphere(pos, 0.2f);
                }

                // Draw connections to nearby tiles within neighbor radius
                var neighbors = worldSpaceMazeData.GetTilesNear(tile.Position, neighborRadius);
                foreach (var neighbor in neighbors)
                {
                    if (!neighbor.Walkable || neighbor == tile) continue;

                    float dist = Vector2.Distance(tile.Position, neighbor.Position);
                    if (dist > neighborRadius) continue;

                    // Only draw if topologically connected
                    if (worldSpaceMazeData.AreTilesConnected(tile, neighbor))
                    {
                        Vector3 neighborPos = new Vector3(neighbor.Position.x, neighbor.Position.y, 0f);

                        // Different colors for same-edge, same-node, or edge-to-node
                        if (tile.EdgeIndex >= 0 && tile.EdgeIndex == neighbor.EdgeIndex)
                        {
                            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange for same edge
                        }
                        else if (tile.NodeIndex >= 0 && tile.NodeIndex == neighbor.NodeIndex)
                        {
                            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f); // Green for same node
                        }
                        else
                        {
                            Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // Yellow for edge-to-node
                        }

                        Gizmos.DrawLine(pos, neighborPos);
                    }
                }
            }
        }

        #endregion

        #region Debug Controls

        public void SetDrawGizmos(bool value)
        {
            drawGizmos = value;
        }

        /// <summary>
        /// Sets whether to draw grid gizmos (alias for SetDrawGizmos).
        /// </summary>
        public void SetDrawGridGizmos(bool value)
        {
            drawGizmos = value;
        }

        /// <summary>
        /// Sets whether to draw attraction heatmap (no-op in world-space mode).
        /// </summary>
        public void SetDrawAttractionHeatmap(bool value)
        {
            // No-op: Attraction heatmaps not used in world-space coordinate system
        }

        #endregion

        #region Maze Regeneration

        /// <summary>
        /// Regenerates the maze with a new random seed.
        /// </summary>
        public void RegenerateMaze()
        {
            // Get seed from RandomManager for consistency
            planarGeneratorConfig.randomSeed = RandomManager.NextSeed();

            // Regenerate maze
            InitializeFromGraph();

            // Reposition heart marker
            var heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
            if (heart != null)
            {
                heart.PositionFromMazeGrid();
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
            return worldSpaceMazeData.SpawnPointIds;
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
            return worldSpaceMazeData.SpawnPointCount;
        }

        #endregion
    }
}

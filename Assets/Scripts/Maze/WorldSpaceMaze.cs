using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// Represents a single tile in world space with position, orientation, and type information.
    /// Unlike grid-based tiles, these use continuous world coordinates and can be oriented
    /// in any direction.
    /// </summary>
    public class WorldSpaceTile
    {
        /// <summary>World-space position of the tile center</summary>
        public Vector2 Position;

        /// <summary>Orientation angle in radians (0 = facing right/+X, counter-clockwise)</summary>
        public float Orientation;

        /// <summary>Type of tile: Path, Wall, Node</summary>
        public TileCategory Category;

        /// <summary>Whether this tile is walkable</summary>
        public bool Walkable;

        /// <summary>Special marker character (H=heart, N=node hazard, spawn IDs, etc.)</summary>
        public char Symbol;

        /// <summary>For wall tiles: accumulated orientation normals for averaging</summary>
        public List<Vector2> OrientationNormals;

        /// <summary>Size of the tile in world units</summary>
        public float Size;

        public enum TileCategory
        {
            Path,       // Walkable path along edges
            Node,       // Walkable node clearing
            Wall        // Non-walkable border wall
        }

        public WorldSpaceTile(Vector2 position, float orientation, TileCategory category, float size)
        {
            Position = position;
            Orientation = orientation;
            Category = category;
            Walkable = category != TileCategory.Wall;
            Symbol = category == TileCategory.Wall ? '#' : '.';
            Size = size;
            OrientationNormals = new List<Vector2>();
        }

        /// <summary>
        /// Adds an orientation normal for wall tiles. Final orientation is computed
        /// by averaging all normals.
        /// </summary>
        public void AddOrientationNormal(Vector2 normal)
        {
            OrientationNormals.Add(normal.normalized);
        }

        /// <summary>
        /// Computes the final orientation by averaging all accumulated normals.
        /// </summary>
        public void FinalizeOrientation()
        {
            if (OrientationNormals.Count == 0) return;

            Vector2 avgNormal = Vector2.zero;
            foreach (var normal in OrientationNormals)
            {
                avgNormal += normal;
            }
            avgNormal /= OrientationNormals.Count;

            if (avgNormal.sqrMagnitude > 0.001f)
            {
                avgNormal.Normalize();
                Orientation = Mathf.Atan2(avgNormal.y, avgNormal.x);
            }
        }
    }

    /// <summary>
    /// Contains all world-space tiles and graph references for a maze.
    /// This replaces the grid-based system with continuous world coordinates.
    /// </summary>
    public class WorldSpaceMazeData
    {
        /// <summary>All tiles in the maze (path, node, and wall tiles)</summary>
        public List<WorldSpaceTile> Tiles = new List<WorldSpaceTile>();

        /// <summary>Reference to the underlying graph state</summary>
        public PlanarForestMazeGenerator.ForestMapState GraphState;

        /// <summary>World-space bounds of the maze</summary>
        public Bounds WorldBounds;

        /// <summary>World-space bounds of the maze (alias for WorldBounds)</summary>
        public Bounds Bounds => WorldBounds;

        /// <summary>Tile size in world units</summary>
        public float TileSize = 1.0f;

        /// <summary>Path half-width in world units</summary>
        public float PathHalfWidth = 0.5f;

        /// <summary>Node radius in world units</summary>
        public float NodeRadius = 3.0f;

        /// <summary>Wall border depth in tiles</summary>
        public int WallBorderDepth = 3;

        /// <summary>Spawn points mapped by their ID character to portal transform (for real-time position)</summary>
        public Dictionary<char, Transform> SpawnPointTransforms = new Dictionary<char, Transform>();

        /// <summary>Spatial lookup for quick tile access by position</summary>
        private Dictionary<Vector2Int, List<WorldSpaceTile>> _spatialGrid;

        /// <summary>Resolution of the spatial grid (tiles per unit)</summary>
        private const float SPATIAL_GRID_RESOLUTION = 1.0f;

        public WorldSpaceMazeData()
        {
            _spatialGrid = new Dictionary<Vector2Int, List<WorldSpaceTile>>();
        }

        /// <summary>
        /// Adds a tile and registers it in the spatial lookup grid.
        /// </summary>
        public void AddTile(WorldSpaceTile tile)
        {
            Tiles.Add(tile);

            // Add to spatial grid
            Vector2Int gridKey = GetSpatialKey(tile.Position);
            if (!_spatialGrid.ContainsKey(gridKey))
            {
                _spatialGrid[gridKey] = new List<WorldSpaceTile>();
            }
            _spatialGrid[gridKey].Add(tile);
        }

        /// <summary>
        /// Gets tiles near a world position within a given radius.
        /// </summary>
        public List<WorldSpaceTile> GetTilesNear(Vector2 position, float radius)
        {
            var result = new List<WorldSpaceTile>();
            int searchRadius = Mathf.CeilToInt(radius / SPATIAL_GRID_RESOLUTION) + 1;
            Vector2Int centerKey = GetSpatialKey(position);

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    Vector2Int key = new Vector2Int(centerKey.x + dx, centerKey.y + dy);
                    if (_spatialGrid.TryGetValue(key, out var tiles))
                    {
                        foreach (var tile in tiles)
                        {
                            if (Vector2.Distance(tile.Position, position) <= radius)
                            {
                                result.Add(tile);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a position is occupied by a walkable tile.
        /// </summary>
        public bool IsWalkable(Vector2 position)
        {
            var tiles = GetTilesNear(position, TileSize * 0.5f);
            return tiles.Any(t => t.Walkable && Vector2.Distance(t.Position, position) < t.Size * 0.5f);
        }

        /// <summary>
        /// Marks tiles near a position as unwalkable.
        /// Used to block portal locations.
        /// </summary>
        public void MarkUnwalkable(Vector2 position)
        {
            var tiles = GetTilesNear(position, TileSize * 0.5f);
            int markedCount = 0;
            foreach (var tile in tiles)
            {
                if (Vector2.Distance(tile.Position, position) < tile.Size * 0.5f)
                {
                    if (tile.Walkable)
                    {
                        tile.Walkable = false;
                        markedCount++;
                        // UnityEngine.Debug.Log($"[WorldSpaceMazeData] Marked tile at {tile.Position} (category: {tile.Category}) as unwalkable");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a position is occupied by any tile.
        /// </summary>
        public bool HasTileAt(Vector2 position, float tolerance = 0.1f)
        {
            var tiles = GetTilesNear(position, tolerance);
            return tiles.Any(t => Vector2.Distance(t.Position, position) < tolerance);
        }

        private Vector2Int GetSpatialKey(Vector2 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x * SPATIAL_GRID_RESOLUTION),
                Mathf.FloorToInt(position.y * SPATIAL_GRID_RESOLUTION)
            );
        }

        /// <summary>
        /// Gets the number of cells in the spatial grid (for diagnostics).
        /// </summary>
        public int GetSpatialGridCellCount()
        {
            return _spatialGrid?.Count ?? 0;
        }

        /// <summary>
        /// Registers a spawn point with its portal transform for real-time position access.
        /// </summary>
        public void RegisterSpawnPoint(char spawnId, Transform portalTransform)
        {
            SpawnPointTransforms[spawnId] = portalTransform;
        }

        /// <summary>
        /// Removes a spawn point by ID.
        /// </summary>
        public void RemoveSpawnPoint(char spawnId)
        {
            SpawnPointTransforms.Remove(spawnId);
        }

        /// <summary>
        /// Clears all spawn points.
        /// </summary>
        public void ClearSpawnPoints()
        {
            SpawnPointTransforms.Clear();
        }

        /// <summary>
        /// Gets a spawn point world position by ID (queries real-time from transform).
        /// </summary>
        public Vector3? GetSpawnPointPosition(char spawnId)
        {
            if (SpawnPointTransforms.TryGetValue(spawnId, out var transform) && transform != null)
            {
                // Return XY position, zeroing Z for 2D pathfinding
                return new Vector3(transform.position.x, transform.position.y, 0f);
            }
            return null;
        }

        /// <summary>
        /// Gets all spawn point positions (queries real-time from transforms).
        /// Returns dictionary for compatibility with existing iteration patterns.
        /// </summary>
        public Dictionary<char, Vector3> GetSpawnPointPositions()
        {
            var positions = new Dictionary<char, Vector3>();
            foreach (var kvp in SpawnPointTransforms)
            {
                if (kvp.Value != null)
                {
                    positions[kvp.Key] = new Vector3(kvp.Value.position.x, kvp.Value.position.y, 0f);
                }
            }
            return positions;
        }

        /// <summary>
        /// Gets spawn point count.
        /// </summary>
        public int SpawnPointCount => SpawnPointTransforms.Count;

        /// <summary>
        /// Gets spawn point IDs.
        /// </summary>
        public IEnumerable<char> SpawnPointIds => SpawnPointTransforms.Keys;

        /// <summary>
        /// Recalculates the world bounds based on all tiles.
        /// </summary>
        public void RecalculateBounds()
        {
            if (Tiles.Count == 0)
            {
                WorldBounds = new Bounds(Vector3.zero, Vector3.one);
                return;
            }

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var tile in Tiles)
            {
                float halfSize = tile.Size * 0.5f;
                minX = Mathf.Min(minX, tile.Position.x - halfSize);
                maxX = Mathf.Max(maxX, tile.Position.x + halfSize);
                minY = Mathf.Min(minY, tile.Position.y - halfSize);
                maxY = Mathf.Max(maxY, tile.Position.y + halfSize);
            }

            Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0);
            Vector3 size = new Vector3(maxX - minX, maxY - minY, 1);
            WorldBounds = new Bounds(center, size);
        }
    }

    /// <summary>
    /// Generates world-space maze data from a planar forest graph.
    /// Converts nodes and edges into positioned, oriented tiles.
    /// </summary>
    public static class WorldSpaceMazeGenerator
    {
        private const float NODE_RADIUS = 3.0f;
        private const float PATH_WIDTH = 1.0f;
        private const float TILE_SIZE = 1.0f;
        private const int WALL_BORDER_DEPTH = 3;

        /// <summary>
        /// Generates world-space maze data from a planar forest graph state.
        /// </summary>
        public static WorldSpaceMazeData GenerateFromGraph(PlanarForestMazeGenerator.ForestMapState state, float tileSize = 1.0f)
        {
            var data = new WorldSpaceMazeData
            {
                GraphState = state,
                TileSize = tileSize,
                PathHalfWidth = PATH_WIDTH * 0.5f,
                NodeRadius = NODE_RADIUS,
                WallBorderDepth = WALL_BORDER_DEPTH
            };

            // Track which positions have walkable tiles for wall generation
            var walkableTilePositions = new HashSet<Vector2Int>();

            // Step 1: Generate path tiles along edges (oriented along edge direction)
            GenerateEdgeTiles(state, data, walkableTilePositions, tileSize);

            // Step 1b: Generate fill tiles for edge spacing adjustments
            GenerateAdjustmentFillTiles(state, data, walkableTilePositions, tileSize);

            // Step 2: Generate node column tiles (circular, radius 3)
            GenerateNodeTiles(state, data, walkableTilePositions, tileSize);

            // Step 3: Generate wall border tiles (3 deep, normal orientation)
            GenerateWallBorder(data, walkableTilePositions, tileSize, WALL_BORDER_DEPTH);

            // Finalize wall orientations (average normals)
            foreach (var tile in data.Tiles.Where(t => t.Category == WorldSpaceTile.TileCategory.Wall))
            {
                tile.FinalizeOrientation();
            }

            data.RecalculateBounds();

            // Verify spatial grid is populated correctly
            int gridCellCount = data.GetSpatialGridCellCount();
            int walkableCount = data.Tiles.Count(t => t.Walkable);
            // UnityEngine.Debug.Log($"[WorldSpaceMazeGenerator] Generated {data.Tiles.Count} tiles ({walkableCount} walkable) in {gridCellCount} spatial grid cells");

            return data;
        }

        /// <summary>
        /// Maximum gap between tiles along edges (in world units).
        /// Ensures wall coverage has no holes larger than this.
        /// </summary>
        private const float MAX_TILE_GAP = 0.25f;

        /// <summary>
        /// Generates path tiles along each edge, oriented in the direction of the edge.
        /// Uses Bezier curve sampling when available for smooth curves with proper tangent orientation.
        /// Ensures maximum gap of 0.25 units between tiles.
        /// </summary>
        private static void GenerateEdgeTiles(
            PlanarForestMazeGenerator.ForestMapState state,
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize)
        {
            foreach (var edge in state.Edges)
            {
                if (edge.PolylinePoints.Count < 2) continue;

                // If edge has a Bezier curve, sample it directly for smooth orientation
                if (edge.Curve != null)
                {
                    GenerateTilesFromBezierCurve(edge, data, walkablePositions, tileSize);
                }
                else
                {
                    // Fallback to polyline-based generation
                    GenerateTilesFromPolyline(edge, data, walkablePositions, tileSize);
                }
            }
        }

        /// <summary>
        /// Generates tiles by sampling a Bezier curve with proper tangent orientation.
        /// </summary>
        private static void GenerateTilesFromBezierCurve(
            PlanarForestMazeGenerator.Edge edge,
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize)
        {
            // Sample the curve with orientations at max 0.25 unit gaps
            var samples = edge.Curve.SampleWithOrientations(tileSize * 0.5f, MAX_TILE_GAP);

            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 position = samples[i].Position;
                Vector2 tangent = samples[i].Tangent;

                // Calculate orientation from tangent
                float orientation = Mathf.Atan2(tangent.y, tangent.x);

                Vector2Int gridPos = ToGridPosition(position, tileSize);
                if (walkablePositions.Contains(gridPos)) continue;

                var tile = new WorldSpaceTile(position, orientation, WorldSpaceTile.TileCategory.Path, tileSize);

                // Mark special tiles (spawn points on partial edges at endpoint)
                if (edge.Partial && i == samples.Count - 1)
                {
                    tile.Symbol = GetNextSpawnId(data);
                }

                data.AddTile(tile);
                walkablePositions.Add(gridPos);
            }
        }

        /// <summary>
        /// Generates tiles by walking along a polyline (fallback for edges without Bezier curves).
        /// </summary>
        private static void GenerateTilesFromPolyline(
            PlanarForestMazeGenerator.Edge edge,
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize)
        {
            // Use small step size to ensure no gaps larger than MAX_TILE_GAP
            float stepSize = Mathf.Min(tileSize * 0.5f, MAX_TILE_GAP);

            for (int i = 0; i < edge.PolylinePoints.Count - 1; i++)
            {
                Vector2 start = edge.PolylinePoints[i];
                Vector2 end = edge.PolylinePoints[i + 1];
                Vector2 direction = (end - start).normalized;
                float segmentLength = Vector2.Distance(start, end);

                // Calculate orientation angle from direction
                float orientation = Mathf.Atan2(direction.y, direction.x);

                // Place tiles along the segment at step intervals
                int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                for (int j = 0; j <= numSteps; j++)
                {
                    float t = numSteps > 0 ? (float)j / numSteps : 0;
                    Vector2 position = Vector2.Lerp(start, end, t);

                    Vector2Int gridPos = ToGridPosition(position, tileSize);
                    if (walkablePositions.Contains(gridPos)) continue;

                    var tile = new WorldSpaceTile(position, orientation, WorldSpaceTile.TileCategory.Path, tileSize);

                    // Mark special tiles (spawn points on partial edges)
                    bool isLastSegment = (i == edge.PolylinePoints.Count - 2);
                    bool isLastPoint = (j == numSteps);
                    if (edge.Partial && isLastSegment && isLastPoint)
                    {
                        tile.Symbol = GetNextSpawnId(data);
                    }

                    data.AddTile(tile);
                    walkablePositions.Add(gridPos);
                }
            }
        }

        /// <summary>
        /// Generates path tiles along adjustment fill segments.
        /// These fill gaps created when edges are rotated for minimum spacing.
        /// </summary>
        private static void GenerateAdjustmentFillTiles(
            PlanarForestMazeGenerator.ForestMapState state,
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize)
        {
            if (state.AdjustmentFills == null || state.AdjustmentFills.Count == 0)
                return;

            float stepSize = tileSize * 0.5f;

            foreach (var (start, end) in state.AdjustmentFills)
            {
                Vector2 direction = (end - start).normalized;
                float segmentLength = Vector2.Distance(start, end);

                if (segmentLength < 0.1f) continue;

                float orientation = Mathf.Atan2(direction.y, direction.x);

                int numSteps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / stepSize));
                for (int j = 0; j <= numSteps; j++)
                {
                    float t = numSteps > 0 ? (float)j / numSteps : 0;
                    Vector2 position = Vector2.Lerp(start, end, t);

                    Vector2Int gridPos = ToGridPosition(position, tileSize);
                    if (walkablePositions.Contains(gridPos)) continue;

                    var tile = new WorldSpaceTile(position, orientation, WorldSpaceTile.TileCategory.Path, tileSize);
                    data.AddTile(tile);
                    walkablePositions.Add(gridPos);
                }
            }

            // UnityEngine.Debug.Log($"[WorldSpaceMazeGenerator] Generated tiles for {state.AdjustmentFills.Count} adjustment fill segments");
        }

        /// <summary>
        /// Generates circular node column tiles centered on each node.
        /// </summary>
        private static void GenerateNodeTiles(
            PlanarForestMazeGenerator.ForestMapState state,
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize)
        {
            foreach (var node in state.Nodes)
            {
                // Create a circular column of tiles with radius = NODE_RADIUS
                // and same thickness as path (tileSize)
                int tilesRadius = Mathf.CeilToInt(NODE_RADIUS / tileSize);

                for (int dx = -tilesRadius; dx <= tilesRadius; dx++)
                {
                    for (int dy = -tilesRadius; dy <= tilesRadius; dy++)
                    {
                        Vector2 offset = new Vector2(dx * tileSize, dy * tileSize);
                        Vector2 position = node.Position + offset;

                        // Check if within circular radius
                        float distance = offset.magnitude;
                        if (distance > NODE_RADIUS) continue;

                        Vector2Int gridPos = ToGridPosition(position, tileSize);
                        if (walkablePositions.Contains(gridPos)) continue;

                        // Calculate orientation: radial from center (facing outward)
                        // For tiles at center, default to 0
                        float orientation = 0f;
                        if (distance > 0.01f)
                        {
                            Vector2 radial = offset.normalized;
                            orientation = Mathf.Atan2(radial.y, radial.x);
                        }

                        var tile = new WorldSpaceTile(position, orientation, WorldSpaceTile.TileCategory.Node, tileSize);

                        // Mark special node tiles
                        if (dx == 0 && dy == 0)
                        {
                            if (node.Kind == "root")
                            {
                                tile.Symbol = 'H'; // Heart
                            }
                            else
                            {
                                tile.Symbol = 'N'; // Node hazard
                            }
                        }

                        data.AddTile(tile);
                        walkablePositions.Add(gridPos);
                    }
                }
            }
        }

        /// <summary>
        /// Generates wall border tiles around all walkable tiles.
        /// Wall tiles are oriented normal to the graph element they border.
        /// </summary>
        private static void GenerateWallBorder(
            WorldSpaceMazeData data,
            HashSet<Vector2Int> walkablePositions,
            float tileSize,
            int borderDepth)
        {
            // Find all positions that need wall tiles (within borderDepth of walkable tiles)
            var wallPositions = new HashSet<Vector2Int>();
            var positionToNearestWalkable = new Dictionary<Vector2Int, List<Vector2Int>>();

            foreach (var walkablePos in walkablePositions)
            {
                for (int dx = -borderDepth; dx <= borderDepth; dx++)
                {
                    for (int dy = -borderDepth; dy <= borderDepth; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        Vector2Int wallPos = new Vector2Int(walkablePos.x + dx, walkablePos.y + dy);
                        if (walkablePositions.Contains(wallPos)) continue;

                        // Check Manhattan distance
                        int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (distance > borderDepth) continue;

                        wallPositions.Add(wallPos);

                        // Track which walkable tiles this wall is near (for orientation)
                        if (!positionToNearestWalkable.ContainsKey(wallPos))
                        {
                            positionToNearestWalkable[wallPos] = new List<Vector2Int>();
                        }
                        positionToNearestWalkable[wallPos].Add(walkablePos);
                    }
                }
            }

            // Create wall tiles with orientations normal to adjacent walkable tiles
            foreach (var wallPos in wallPositions)
            {
                Vector2 worldPos = ToWorldPosition(wallPos, tileSize);
                var tile = new WorldSpaceTile(worldPos, 0f, WorldSpaceTile.TileCategory.Wall, tileSize);

                // Calculate orientation normals from all adjacent walkable tiles
                if (positionToNearestWalkable.TryGetValue(wallPos, out var nearbyWalkable))
                {
                    foreach (var walkablePos in nearbyWalkable)
                    {
                        // Normal points from walkable toward wall (outward from maze)
                        Vector2 normal = new Vector2(wallPos.x - walkablePos.x, wallPos.y - walkablePos.y);
                        if (normal.sqrMagnitude > 0.001f)
                        {
                            tile.AddOrientationNormal(normal.normalized);
                        }
                    }
                }

                data.AddTile(tile);
            }
        }

        private static Vector2Int ToGridPosition(Vector2 worldPos, float tileSize)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPos.x / tileSize),
                Mathf.RoundToInt(worldPos.y / tileSize)
            );
        }

        private static Vector2 ToWorldPosition(Vector2Int gridPos, float tileSize)
        {
            return new Vector2(gridPos.x * tileSize, gridPos.y * tileSize);
        }

        private static int _spawnIdCounter = 0;
        private static char GetNextSpawnId(WorldSpaceMazeData data)
        {
            // Generate spawn IDs: A-Z (excluding H, N), a-z (excluding h, n), 0-9
            char[] validIds = "ABCDEFGIJKLMOPQRSTUVWXYZabcdefgijklmopqrstuvwxyz0123456789".ToCharArray();

            int index = _spawnIdCounter % validIds.Length;
            _spawnIdCounter++;
            return validIds[index];
        }

        /// <summary>
        /// Resets the spawn ID counter (call before generating a new maze).
        /// </summary>
        public static void ResetSpawnIdCounter()
        {
            _spawnIdCounter = 0;
        }
    }
}

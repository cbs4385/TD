using System.Collections.Generic;
using UnityEngine;

namespace ForestMaze
{
    /// <summary>
    /// Static utility class for maze pathfinding operations.
    /// Provides A* pathfinding through walkable tiles with path simplification.
    /// Used by both VisitorControllerBase and RedCapController.
    /// </summary>
    public static class MazePathfinding
    {
        /// <summary>
        /// Builds a world-space path from start to end using A* through walkable tiles.
        /// The path is simplified to remove intermediate collinear points for smooth movement.
        /// </summary>
        /// <param name="mazeData">The maze data containing tiles and topology</param>
        /// <param name="start">Start position in world space</param>
        /// <param name="end">End position in world space</param>
        /// <param name="heartNodePenalty">Additional cost for traversing heart node tiles (0 to disable)</param>
        /// <param name="penalizeHeartNode">Whether to apply heart node penalty (false if destination is heart)</param>
        /// <returns>List of waypoints from start to end, or empty list if no path found</returns>
        public static List<Vector3> BuildWorldPath(
            WorldSpaceMazeData mazeData,
            Vector3 start,
            Vector3 end,
            float heartNodePenalty = 0f,
            bool penalizeHeartNode = true)
        {
            if (mazeData == null)
            {
                return new List<Vector3>();
            }

            var result = new List<Vector3>();

            Vector2 startPos2D = new Vector2(start.x, start.y);
            Vector2 endPos2D = new Vector2(end.x, end.y);

            // Find nearest walkable tile to start position
            var startTile = FindNearestWalkableTile(mazeData, startPos2D);
            if (startTile == null)
            {
                return new List<Vector3>();
            }

            // Find nearest walkable tile to end position
            var endTile = FindNearestWalkableTile(mazeData, endPos2D);
            if (endTile == null)
            {
                return new List<Vector3>();
            }

            // A* through walkable tiles
            var tilePath = FindTilePath(mazeData, startTile, endTile, heartNodePenalty, penalizeHeartNode);

            if (tilePath == null || tilePath.Count == 0)
            {
                return new List<Vector3>();
            }

            // Path simplification DISABLED - it was causing visitors to get stuck
            // The A* path through tiles is already valid; simplification was removing
            // intermediate waypoints needed for curved paths around obstacles
            // tilePath = SimplifyTilePath(tilePath, mazeData);

            // Convert tile path to world positions
            // Only add current position if it's VERY close to the start tile (on the walkable path)
            // Using 0.6f threshold to ensure we don't add positions that are off the path
            // (which would create a straight line segment through unwalkable terrain)
            float distToStartTile = Vector2.Distance(startPos2D, startTile.Position);
            if (distToStartTile < 0.6f)
            {
                result.Add(start);
            }

            // Add all tile positions
            foreach (var tile in tilePath)
            {
                result.Add(new Vector3(tile.Position.x, tile.Position.y, start.z));
            }

            // Don't add the exact destination point - path should end at the last walkable tile
            // Adding unwalkable destinations (like node centers) causes visitors to get stuck

            return result;
        }

        /// <summary>
        /// Finds the nearest walkable tile to a position.
        /// </summary>
        public static WorldSpaceTile FindNearestWalkableTile(WorldSpaceMazeData mazeData, Vector2 position)
        {
            float searchRadius = 5f;
            float minDist = float.MaxValue;
            WorldSpaceTile nearest = null;

            // Expand search radius if needed
            for (int attempt = 0; attempt < 3 && nearest == null; attempt++)
            {
                var nearbyTiles = mazeData.GetTilesNear(position, searchRadius);
                foreach (var tile in nearbyTiles)
                {
                    if (!tile.Walkable) continue;

                    float dist = Vector2.Distance(position, tile.Position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = tile;
                    }
                }
                searchRadius *= 2f;
            }

            return nearest;
        }

        /// <summary>
        /// Finds a path through walkable tiles using A* algorithm.
        /// Uses Euclidean distance as heuristic to prefer geometrically shorter paths.
        /// Optimized with a SortedSet priority queue for O(n log n) performance.
        /// Uses lazy deletion - stale entries are skipped when popped rather than removed.
        /// </summary>
        private static List<WorldSpaceTile> FindTilePath(
            WorldSpaceMazeData mazeData,
            WorldSpaceTile startTile,
            WorldSpaceTile endTile,
            float heartNodePenalty,
            bool penalizeHeartNode)
        {
            if (startTile == endTile)
            {
                return new List<WorldSpaceTile> { startTile };
            }

            // A* algorithm using distance-based costs
            var gScore = new Dictionary<WorldSpaceTile, float>();
            var parent = new Dictionary<WorldSpaceTile, WorldSpaceTile>();
            var closedSet = new HashSet<WorldSpaceTile>();

            // Use a sorted set as a priority queue (ordered by fScore, then by order for uniqueness)
            // This gives O(log n) insertion and O(log n) removal of min element
            // We use lazy deletion: when we find a better path, we add a new entry
            // and skip stale entries (already in closedSet) when popping
            var openSet = new SortedSet<(float fScore, int order, WorldSpaceTile tile)>();
            int insertOrder = 0; // Tie-breaker for equal fScores

            float startF = Vector2.Distance(startTile.Position, endTile.Position);
            gScore[startTile] = 0f;
            openSet.Add((startF, insertOrder++, startTile));
            parent[startTile] = null;

            // Tiles are placed at ~0.5 unit intervals along curves and 1.0 in nodes
            float neighborRadius = mazeData.TileSize * 1.42f;

            int iterations = 0;
            int maxIterations = 50000;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Get node with lowest fScore - O(log n) with SortedSet
                var minEntry = openSet.Min;
                openSet.Remove(minEntry);
                WorldSpaceTile current = minEntry.tile;

                // Skip if already processed (lazy deletion - this was a stale entry)
                if (closedSet.Contains(current))
                {
                    continue;
                }

                // Check if reached destination
                float distToEnd = Vector2.Distance(current.Position, endTile.Position);
                if (distToEnd < mazeData.TileSize * 0.5f || current == endTile)
                {
                    // Reconstruct path
                    var path = new List<WorldSpaceTile>();
                    var node = current;
                    while (node != null)
                    {
                        path.Add(node);
                        parent.TryGetValue(node, out node);
                    }
                    path.Reverse();
                    return path;
                }

                closedSet.Add(current);

                // Get neighboring walkable tiles
                var neighbors = mazeData.GetTilesNear(current.Position, neighborRadius);
                float currentG = gScore[current];

                foreach (var neighbor in neighbors)
                {
                    if (!neighbor.Walkable) continue;
                    if (closedSet.Contains(neighbor)) continue;

                    // Must be within neighbor radius
                    float stepDist = Vector2.Distance(current.Position, neighbor.Position);
                    if (stepDist > neighborRadius) continue;

                    // Topology check: verify tiles are connected through graph structure
                    if (!mazeData.AreTilesConnected(current, neighbor))
                        continue;

                    // Calculate tentative gScore
                    // Add penalty for heart node tiles to discourage crossing through the heart
                    float stepCost = stepDist;
                    if (penalizeHeartNode && neighbor.NodeIndex == 0 && heartNodePenalty > 0f)
                    {
                        stepCost += heartNodePenalty;
                    }

                    float tentativeG = currentG + stepCost;
                    float neighborG = gScore.TryGetValue(neighbor, out float ng) ? ng : float.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        // Found a better path - update and add to open set
                        // (old entry will be skipped via lazy deletion when we pop it)
                        parent[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float newF = tentativeG + Vector2.Distance(neighbor.Position, endTile.Position);
                        openSet.Add((newF, insertOrder++, neighbor));
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Simplifies a tile path by removing intermediate collinear points.
        /// Keeps direction changes and samples at regular intervals for smooth movement.
        ///
        /// This is critical for smooth navigation - without simplification, paths have
        /// ~0.5 unit spacing between waypoints, causing stilted movement even with spline smoothing.
        /// After simplification, paths have waypoints only at meaningful direction changes.
        ///
        /// IMPORTANT: Uses line-of-sight checks to ensure simplified segments don't pass
        /// through unwalkable terrain (forest, walls, node centers).
        /// </summary>
        private static List<WorldSpaceTile> SimplifyTilePath(List<WorldSpaceTile> path, WorldSpaceMazeData mazeData)
        {
            if (path == null || path.Count <= 2)
                return path;

            var simplified = new List<WorldSpaceTile>();
            simplified.Add(path[0]);

            // Keep points where direction changes by more than 20 degrees
            // This filters out minor curve variations while preserving actual turns
            const float angleThreshold = 20f * Mathf.Deg2Rad;

            // Don't skip more than 10 tiles (~5 world units at 0.5 tile spacing)
            // This ensures spline has control points for long straight sections
            const int maxSkip = 10;

            // Also track cumulative angle change - if many small turns add up, keep a point
            const float cumulativeAngleThreshold = 30f * Mathf.Deg2Rad;

            // For node tiles, use a lower skip count to ensure we keep enough waypoints
            // when navigating around node centers (which have unwalkable centers with props)
            const int maxSkipInNode = 3;

            // Collect node center positions for segment validation
            // Node centers within 1.0 unit are unwalkable (props are placed there)
            const float nodeCenterUnwalkableRadius = 1.0f;
            var nodeCenters = new List<Vector2>();
            if (mazeData?.GraphState?.Nodes != null)
            {
                foreach (var node in mazeData.GraphState.Nodes)
                {
                    // Skip the root/heart node - its center is walkable
                    if (node.Kind != "root")
                    {
                        nodeCenters.Add(node.Position);
                    }
                }
            }

            int skipCount = 0;
            float cumulativeAngle = 0f;

            for (int i = 1; i < path.Count - 1; i++)
            {
                Vector2 dirPrev = (path[i].Position - path[i - 1].Position).normalized;
                Vector2 dirNext = (path[i + 1].Position - path[i].Position).normalized;

                float angle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(dirPrev, dirNext), -1f, 1f));
                cumulativeAngle += angle;

                // Check if this tile is on a node (EdgeIndex == -1 means node tile)
                bool isNodeTile = path[i].EdgeIndex == -1;

                // Determine max skip based on whether we're on a node
                // Node tiles need more frequent waypoints to ensure spline doesn't cut through center
                int currentMaxSkip = isNodeTile ? maxSkipInNode : maxSkip;

                // Keep this point if:
                // 1. Single direction change is significant (sharp turn)
                // 2. Cumulative small changes exceed threshold (gradual curve)
                // 3. We've skipped too many tiles (need control point for spline)
                // 4. This is a node tile and we're transitioning from edge or vice versa
                // 5. Skipping would create a segment that cuts through a node center
                // 6. Skipping would create a segment without line-of-sight (passes through unwalkable terrain)
                bool isTransition = (i > 0 && path[i - 1].EdgeIndex != path[i].EdgeIndex);

                // Check if segment from last kept point to NEXT point would cut through a node center
                bool wouldCutThroughNodeCenter = false;
                if (simplified.Count > 0 && i + 1 < path.Count)
                {
                    Vector2 segStart = simplified[simplified.Count - 1].Position;
                    Vector2 segEnd = path[i + 1].Position;
                    wouldCutThroughNodeCenter = SegmentCutsNodeCenter(segStart, segEnd, nodeCenters, nodeCenterUnwalkableRadius);
                }

                // Check if segment from last kept point to NEXT point has clear line-of-sight
                // (doesn't pass through unwalkable forest/wall tiles)
                bool noLineOfSight = false;
                if (simplified.Count > 0 && i + 1 < path.Count && mazeData != null)
                {
                    Vector2 segStart = simplified[simplified.Count - 1].Position;
                    Vector2 segEnd = path[i + 1].Position;
                    noLineOfSight = !HasLineOfSight(mazeData, segStart, segEnd);
                }

                if (angle > angleThreshold || cumulativeAngle > cumulativeAngleThreshold ||
                    skipCount >= currentMaxSkip || isTransition || wouldCutThroughNodeCenter || noLineOfSight)
                {
                    simplified.Add(path[i]);
                    skipCount = 0;
                    cumulativeAngle = 0f;
                }
                else
                {
                    skipCount++;
                }
            }

            simplified.Add(path[path.Count - 1]);
            return simplified;
        }

        /// <summary>
        /// Checks if a line segment passes through any node center's unwalkable zone.
        /// Uses point-to-line-segment distance calculation.
        /// </summary>
        private static bool SegmentCutsNodeCenter(Vector2 segStart, Vector2 segEnd, List<Vector2> nodeCenters, float radius)
        {
            foreach (var center in nodeCenters)
            {
                float dist = PointToSegmentDistance(center, segStart, segEnd);
                if (dist < radius)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates the minimum distance from a point to a line segment.
        /// </summary>
        private static float PointToSegmentDistance(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 segment = segEnd - segStart;
            float segLenSq = segment.sqrMagnitude;

            if (segLenSq < 0.0001f)
            {
                // Degenerate segment - just return distance to start
                return Vector2.Distance(point, segStart);
            }

            // Project point onto line, clamped to segment
            float t = Mathf.Clamp01(Vector2.Dot(point - segStart, segment) / segLenSq);
            Vector2 projection = segStart + t * segment;

            return Vector2.Distance(point, projection);
        }

        /// <summary>
        /// Checks if there is clear line-of-sight between two points.
        /// Returns true if a straight line between the points stays on walkable terrain.
        /// Uses ray-marching along the segment to check for walkable tiles at each step.
        /// </summary>
        private static bool HasLineOfSight(WorldSpaceMazeData mazeData, Vector2 start, Vector2 end)
        {
            if (mazeData == null) return true;

            Vector2 direction = end - start;
            float distance = direction.magnitude;

            if (distance < 0.01f) return true;

            direction /= distance; // Normalize

            // Step size should be smaller than tile size to catch thin unwalkable areas
            // Tiles are ~0.5 units apart, so step at 0.3 to ensure we don't miss any
            const float stepSize = 0.3f;
            int steps = Mathf.CeilToInt(distance / stepSize);

            // Check points along the segment (skip start and end, they're known walkable)
            for (int i = 1; i < steps; i++)
            {
                float t = i * stepSize;
                if (t >= distance) break;

                Vector2 checkPos = start + direction * t;

                // Find the nearest tile to this position
                var nearestTile = FindNearestTileAtPosition(mazeData, checkPos);

                // If no tile found within reasonable distance, we're in unwalkable terrain (forest)
                if (nearestTile == null)
                {
                    return false;
                }

                // If the nearest tile is not walkable, we're crossing through walls/forest
                if (!nearestTile.Walkable)
                {
                    return false;
                }

                // Check if we're too far from any walkable tile (in the forest gap between paths)
                float distToTile = Vector2.Distance(checkPos, nearestTile.Position);
                // If we're more than 0.75 units from the nearest walkable tile, we're likely in forest
                // This catches cases where the nearest tile is walkable but we're in the gap
                if (distToTile > 0.75f)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds the nearest tile (walkable or not) to a position.
        /// Unlike FindNearestWalkableTile, this finds ANY tile for checking terrain.
        /// </summary>
        private static WorldSpaceTile FindNearestTileAtPosition(WorldSpaceMazeData mazeData, Vector2 position)
        {
            // Use a small search radius - we want to know if there's ANY tile nearby
            const float searchRadius = 1.5f;

            var nearbyTiles = mazeData.GetTilesNear(position, searchRadius);
            if (nearbyTiles == null || nearbyTiles.Count == 0)
            {
                return null;
            }

            float minDist = float.MaxValue;
            WorldSpaceTile nearest = null;

            foreach (var tile in nearbyTiles)
            {
                float dist = Vector2.Distance(position, tile.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = tile;
                }
            }

            return nearest;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.HeartPowers
{
    #region Heartbeat of Longing

    /// <summary>
    /// Amplifies FaeLanterns to pull visitors more strongly and tilt routes through their influence.
    /// Coverage scales with tier: I = 50% map, II = 75% map, III = 100% map (entire map).
    /// Attraction strength diminishes with distance from lantern.
    /// Tier I: Echoing Thrum - Early fascination for visitors approaching lantern areas
    /// Tier II: Hungry Glow - Biases post-fascination routes toward Heart
    /// Tier III: Devouring Chorus - Consumed visitors trigger Heart-ward path bias for others
    /// </summary>
    public class HeartbeatOfLongingEffect : ActivePowerEffect
    {
        private List<FaeLantern> affectedLanterns = new List<FaeLantern>();
        private HashSet<Vector2Int> lanternInfluenceTiles = new HashSet<Vector2Int>();
        private const string ModifierSourceId = "HeartbeatOfLonging";

        public HeartbeatOfLongingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {

            // Mark all FaeLanterns as Heart-linked for duration
            affectedLanterns.AddRange(FaeLantern.All);

            // Apply path cost reduction to all lantern influence tiles
            float tierRadius = GetTierBasedRadius();
            foreach (var lantern in affectedLanterns)
            {
                var influenceTiles = GetLanternInfluenceTilesWithDistances(lantern, tierRadius);
                foreach (var tileData in influenceTiles)
                {
                    Vector2Int tile = tileData.Key;
                    float distanceFromLantern = tileData.Value;

                    lanternInfluenceTiles.Add(tile);

                    // Base attraction strength (param1 = max attraction strength, default -2.0)
                    float baseAttraction = -Mathf.Abs(definition.param1 != 0 ? definition.param1 : 2.0f);

                    // Apply distance-based falloff (closer = stronger attraction)
                    float falloffFactor = 1.0f - (distanceFromLantern / tierRadius); // 1.0 at lantern, 0.0 at edge
                    falloffFactor = Mathf.Clamp01(falloffFactor);

                    float attractionBonus = baseAttraction * falloffFactor;
                    manager.PathModifier.AddModifier(tile, attractionBonus, definition.duration, ModifierSourceId);

                    // Add ROYGBIV tile visual (deep red for Power 1)
                    if (manager.TileVisualizer != null)
                    {
                        // Intensity based on attraction strength with falloff (normalize to 0-1)
                        float intensity = Mathf.Clamp01(Mathf.Abs(attractionBonus) / 5.0f);
                        manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.HeartbeatOfLonging, intensity, definition.duration);
                    }
                }
            }

        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Tier I: Echoing Thrum - Check visitors approaching lantern areas
            if (definition.tier >= 1 && definition.flag1) // flag1 = enable Echoing Thrum
            {
                ApplyEchoingThrum();
            }

            // Tier III: Devouring Chorus - Check for consumed visitors in lantern influence
            if (definition.tier >= 3 && definition.flag2) // flag2 = enable Devouring Chorus
            {
                CheckForDevouringChorus();
            }
        }

        public override void OnEnd()
        {
            // Cleanup path modifiers
            manager.PathModifier.ClearBySource(ModifierSourceId);

            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartbeatOfLonging);
            }

            affectedLanterns.Clear();
            lanternInfluenceTiles.Clear();

        }

        /// <summary>
        /// Calculates tier-based radius for lantern influence.
        /// Tier I: 50% of max map dimension, Tier II: 75%, Tier III: 100% (entire map)
        /// </summary>
        private float GetTierBasedRadius()
        {
            var grid = manager.MazeGrid.Grid;
            float maxDimension = Mathf.Max(grid.Width, grid.Height);

            float coveragePercent = definition.tier switch
            {
                1 => 0.5f,  // 50% coverage
                2 => 0.75f, // 75% coverage
                3 => 1.0f,  // 100% coverage (entire map)
                _ => 0.5f   // Default to tier 1
            };

            float radius = maxDimension * coveragePercent;
            return radius;
        }

        /// <summary>
        /// Gets lantern influence tiles along with their Manhattan distance from the lantern.
        /// Returns Dictionary where Key = tile position, Value = distance from lantern.
        /// </summary>
        private Dictionary<Vector2Int, float> GetLanternInfluenceTilesWithDistances(FaeLantern lantern, float radius)
        {
            Dictionary<Vector2Int, float> tiles = new Dictionary<Vector2Int, float>();
            Vector2Int lanternPos = lantern.GridPosition;
            int radiusInt = Mathf.CeilToInt(radius);

            // Simple radius-based influence using Manhattan distance
            for (int dx = -radiusInt; dx <= radiusInt; dx++)
            {
                for (int dy = -radiusInt; dy <= radiusInt; dy++)
                {
                    float manhattanDistance = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (manhattanDistance <= radius)
                    {
                        Vector2Int tile = new Vector2Int(lanternPos.x + dx, lanternPos.y + dy);
                        if (manager.MazeGrid.Grid.InBounds(tile.x, tile.y))
                        {
                            var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                            if (node != null && node.walkable)
                            {
                                tiles[tile] = manhattanDistance;
                            }
                        }
                    }
                }
            }

            return tiles;
        }

        private HashSet<Vector2Int> GetLanternInfluenceTiles(FaeLantern lantern)
        {
            // Legacy method for backward compatibility - just return keys from new method
            float tierRadius = GetTierBasedRadius();
            var tilesWithDistances = GetLanternInfluenceTilesWithDistances(lantern, tierRadius);
            return new HashSet<Vector2Int>(tilesWithDistances.Keys);
        }

        private void ApplyEchoingThrum()
        {
            // Use visitor registry instead of expensive FindObjectsByType
            var visitors = VisitorRegistry.All;
            int lookAheadSteps = definition.intParam2 > 0 ? definition.intParam2 : 5;

            foreach (var visitor in visitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Fascinated)
                {
                    continue; // Already fascinated or null
                }

                // Check if visitor's path enters lantern influence within N steps
                // (This would require access to the visitor's path - simplified implementation)
                // For now, just check if visitor is near (within 2 tiles of) lantern influence
                Vector2Int visitorGridPos = GetVisitorGridPosition(visitor);
                bool nearInfluence = IsNearLanternInfluence(visitorGridPos, 2);

                if (nearInfluence && Random.value < 0.3f) // param2 = early fascination chance
                {
                    // Apply fascination if the visitor has a public method for it
                    // (Simplified - would need actual visitor state API)
                }
            }
        }

        private void CheckForDevouringChorus()
        {
            // This would need to hook into visitor consumption events
            // For now, this is a placeholder that would be triggered externally
            // when a visitor is consumed during the power duration
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3)
            {
                return;
            }

            // Apply strong Heart-ward bias to all visitors in lantern influence
            // Use visitor registry instead of expensive FindObjectsByType
            var visitors = VisitorRegistry.All;
            foreach (var v in visitors)
            {
                if (v == null || v.State == VisitorControllerBase.VisitorState.Consumed ||
                    v.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                Vector2Int vPos = GetVisitorGridPosition(v);
                if (lanternInfluenceTiles.Contains(vPos))
                {
                    // Apply temporary strong Heart bias (would need visitor API to modify pathfinding)
                }
            }
        }

        private Vector2Int GetVisitorGridPosition(VisitorControllerBase visitor)
        {
            if (manager.MazeGrid.WorldToGrid(visitor.transform.position, out int x, out int y))
            {
                return new Vector2Int(x, y);
            }
            return Vector2Int.zero;
        }

        private bool IsNearLanternInfluence(Vector2Int pos, int range)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                for (int dy = -range; dy <= range; dy++)
                {
                    Vector2Int checkPos = new Vector2Int(pos.x + dx, pos.y + dy);
                    if (lanternInfluenceTiles.Contains(checkPos))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    #endregion

    #region Murmuring Paths

    /// <summary>
    /// Creates corridors of desire or sealing from selected tile to the Heart, tilting pathfinding costs.
    /// Creates a complete path from the clicked tile all the way to the HeartOfMaze.
    /// Tier I: Twin Murmurs - Maintain 2 active segments
    /// Tier II: Labyrinth's Memory - Marked visitors prefer Lost state with Heart bias
    /// Tier III: Sealed Ways - Can increase costs instead to create soft barriers
    /// </summary>
    public class MurmuringPathsEffect : ActivePowerEffect
    {
        private List<Vector2Int> pathSegment = new List<Vector2Int>();
        private const string ModifierSourceId = "MurmuringPaths";
        private static int segmentCounter = 0;
        private string instanceSourceId;
        private GameObject pathVisualObject;
        private LineRenderer pathLineRenderer;
        private float animationTime = 0f;

        public MurmuringPathsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            instanceSourceId = $"{ModifierSourceId}_{segmentCounter++}";
        }

        public override void OnStart()
        {

            // Convert target position to grid, then create a path segment
            if (manager.MazeGrid.WorldToGrid(targetPosition, out int x, out int y))
            {
                Vector2Int startTile = new Vector2Int(x, y);

                pathSegment = GeneratePathSegment(startTile);

                // Determine mode: Lure (default) or Seal (Tier III)
                bool sealMode = definition.tier >= 3 && definition.flag2; // flag2 = seal mode toggle
                float costModifier = sealMode
                    ? Mathf.Abs(definition.param2 != 0 ? definition.param2 : 5.0f)  // Positive = expensive
                    : -Mathf.Abs(definition.param1 != 0 ? definition.param1 : 50.0f); // Negative = attractive (MUCH stronger to force path following)


                // Apply cost modifier to segment tiles
                foreach (var tile in pathSegment)
                {
                    manager.PathModifier.AddModifier(tile, costModifier, definition.duration, instanceSourceId);
                }

                // Create continuous glowing path visualization
                CreatePathVisualization(pathSegment, sealMode);

            }
            else
            {
            }
        }

        public override void OnEnd()
        {
            manager.PathModifier.ClearBySource(instanceSourceId);

            // Remove path visualization
            if (pathVisualObject != null)
            {
                Object.Destroy(pathVisualObject);
                pathVisualObject = null;
                pathLineRenderer = null;
            }

            // Clear Lured state from all visitors
            var activeVisitors = FaeMaze.Visitors.VisitorController.All;
            if (activeVisitors != null)
            {
                foreach (var visitor in activeVisitors)
                {
                    if (visitor != null && visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Lured)
                    {
                        visitor.SetLured(false);
                    }
                }
            }

            pathSegment.Clear();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Animate the path visual with jagged moving edges
            animationTime += deltaTime;
            if (pathLineRenderer != null)
            {
                UpdatePathAnimation();
            }

            // Find all active visitors and check if they're on Murmuring Path tiles
            var activeVisitors = FaeMaze.Visitors.VisitorController.All;
            if (activeVisitors == null || pathSegment == null || pathSegment.Count == 0)
                return;

            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Consumed)
                    continue;

                // Get visitor's current grid position
                if (!manager.MazeGrid.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                    continue;

                Vector2Int visitorTile = new Vector2Int(vx, vy);

                // Check if visitor is on a Murmuring Path tile
                bool onMurmuringPath = pathSegment.Contains(visitorTile);

                // Set Lured state based on whether they're on the path
                if (onMurmuringPath && visitor.State != FaeMaze.Visitors.VisitorControllerBase.VisitorState.Lured)
                {
                    visitor.SetLured(true);
                }
                else if (!onMurmuringPath && visitor.State == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Lured)
                {
                    // Only clear Lured if this effect set it (visitor might be on another Murmuring Path)
                    // For now, we keep them lured until they're far from any path
                    visitor.SetLured(false);
                }
            }
        }

        private List<Vector2Int> GeneratePathSegment(Vector2Int startTile)
        {
            List<Vector2Int> segment = new List<Vector2Int>();


            // Get the heart - try GameController first, then find it dynamically
            FaeMaze.Maze.HeartOfTheMaze heart = null;

            if (manager.GameController != null && manager.GameController.Heart != null)
            {
                heart = manager.GameController.Heart;
            }
            else
            {
                // Fallback: find heart dynamically (for procedurally generated mazes)
                heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
                if (heart != null)
                {
                }
            }

            if (heart == null)
            {
                segment.Add(startTile);
                return segment;
            }

            Vector2Int heartPos = heart.GridPosition;

            // Use A* pathfinding to create path from start tile to heart
            if (manager.GameController == null)
            {
                segment.Add(startTile);
                return segment;
            }

            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = manager.GameController.TryFindPath(startTile, heartPos, pathNodes);


            if (pathFound && pathNodes.Count > 0)
            {
                // Convert MazeNodes to Vector2Int positions
                foreach (var node in pathNodes)
                {
                    segment.Add(new Vector2Int(node.x, node.y));
                }
            }
            else
            {
                segment.Add(startTile); // Fallback: just add the start tile
            }

            return segment;
        }

        private void CreatePathVisualization(List<Vector2Int> path, bool sealMode)
        {
            if (path == null || path.Count == 0 || manager.MazeGrid == null)
                return;

            // Create GameObject for the path visual
            pathVisualObject = new GameObject($"MurmuringPath_{instanceSourceId}");
            pathLineRenderer = pathVisualObject.AddComponent<LineRenderer>();

            // Configure LineRenderer
            pathLineRenderer.startWidth = 0.8f;
            pathLineRenderer.endWidth = 0.8f;
            pathLineRenderer.positionCount = path.Count;

            // Set color (warm orange for lure, reddish for seal)
            Color pathColor = sealMode
                ? new Color(0.8f, 0.1f, 0.1f, 0.7f)  // Deep red for seal mode
                : new Color(1.0f, 0.5f, 0.0f, 0.7f);  // Warm orange for lure mode

            pathLineRenderer.startColor = pathColor;
            pathLineRenderer.endColor = pathColor;

            // Use additive material for glow effect
            pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            pathLineRenderer.material.color = pathColor;

            // Set sorting layer to render above floor but below props
            pathLineRenderer.sortingLayerName = "Default";
            pathLineRenderer.sortingOrder = 5;

            // Set positions from path tiles
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 worldPos = manager.MazeGrid.GridToWorld(path[i].x, path[i].y);
                worldPos.z = -0.1f; // Slightly above floor
                pathLineRenderer.SetPosition(i, worldPos);
            }
        }

        private void UpdatePathAnimation()
        {
            if (pathLineRenderer == null || pathSegment == null || pathSegment.Count == 0)
                return;

            // Create jagged moving edge effect by varying width along the path
            float baseWidth = 0.8f;
            float jaggedAmount = 0.3f;
            float animSpeed = 2.0f;

            // Animate width curve to create moving jagged edges
            AnimationCurve widthCurve = new AnimationCurve();

            for (int i = 0; i < pathSegment.Count; i++)
            {
                float t = (float)i / pathSegment.Count;

                // Create jagged pattern using sine waves at different frequencies
                float jaggedOffset = Mathf.Sin((t * 10.0f) + (animationTime * animSpeed)) * jaggedAmount;
                jaggedOffset += Mathf.Sin((t * 5.0f) - (animationTime * animSpeed * 1.5f)) * jaggedAmount * 0.5f;

                float width = baseWidth + jaggedOffset;
                widthCurve.AddKey(t, width);
            }

            pathLineRenderer.widthCurve = widthCurve;

            // Pulse the overall alpha
            float pulseAlpha = 0.5f + Mathf.Sin(animationTime * 3.0f) * 0.2f;
            Color currentColor = pathLineRenderer.startColor;
            currentColor.a = pulseAlpha;
            pathLineRenderer.startColor = currentColor;
            pathLineRenderer.endColor = currentColor;
        }

        private List<Vector2Int> GetWalkableNeighbors(Vector2Int tile)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();
            Vector2Int[] directions = new Vector2Int[]
            {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            foreach (var dir in directions)
            {
                Vector2Int neighbor = tile + dir;
                var node = manager.MazeGrid.Grid.GetNode(neighbor.x, neighbor.y);
                if (node != null && node.walkable && !pathSegment.Contains(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }
    }

    #endregion

    #region Dream Snare

    /// <summary>
    /// AoE that Mesmerizes visitors, then pushes them into Lost state with Heart bias.
    /// Tier I: Lingering Thorns - Edge tiles become thorn-marked, applying Frightened on step
    /// Tier II: Shared Nightmare - Affected visitors prefer common Lost detour path
    /// Tier III: Marked for Harvest - Bonus essence if consumed, increased penalty if escape
    /// </summary>
    public class DreamSnareEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> affectedVisitors = new HashSet<VisitorControllerBase>();
        private HashSet<Vector2Int> thornTiles = new HashSet<Vector2Int>();
        private Vector2Int centerTile;
        private const string ModifierSourceId = "DreamSnare";

        public DreamSnareEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {

            // Convert target position to grid
            if (!manager.MazeGrid.WorldToGrid(targetPosition, out int x, out int y))
            {
                return;
            }

            centerTile = new Vector2Int(x, y);
            float radius = definition.radius > 0 ? definition.radius : 3f;

            // Add ROYGBIV tile visuals to the AoE (bright yellow for Power 3)
            if (manager.TileVisualizer != null)
            {
                int intRadius = Mathf.CeilToInt(radius);
                for (int dx = -intRadius; dx <= intRadius; dx++)
                {
                    for (int dy = -intRadius; dy <= intRadius; dy++)
                    {
                        int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (manhattanDist <= intRadius)
                        {
                            Vector2Int tile = new Vector2Int(centerTile.x + dx, centerTile.y + dy);
                            var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                            if (node != null && node.walkable)
                            {
                                // Intensity decreases with distance from center
                                float intensity = 1.0f - (manhattanDist / (float)intRadius) * 0.5f;
                                manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.DreamSnare, intensity, definition.duration);
                            }
                        }
                    }
                }
            }

            // Find all visitors in AoE and apply Mesmerized
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            int mesmerizedCount = 0;
            foreach (var visitor in visitors)
            {
                Vector3 visitorPos = visitor.transform.position;
                float distance = Vector3.Distance(targetPosition, visitorPos);

                if (distance <= radius * manager.MazeGrid.TileSize)
                {
                    // Apply Mesmerized state
                    float mesmerizeDuration = definition.param1 > 0 ? definition.param1 : 4f;
                    visitor.SetMesmerized(mesmerizeDuration);
                    affectedVisitors.Add(visitor);
                    mesmerizedCount++;


                    // Tier III: Mark for harvest
                    if (definition.tier >= 3)
                    {
                        // Would need to add a flag to visitor for tracking
                    }
                }
            }

            // Tier I: Create lingering thorn tiles at edge
            if (definition.tier >= 1 && definition.flag1)
            {
                CreateLingeringThorns(radius);
            }

        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Monitor affected visitors for when Mesmerized expires
            // In a full implementation, we'd hook into visitor state change events
            // For now, this is a simplified approach
        }

        public override void OnEnd()
        {
            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DreamSnare);
            }

            thornTiles.Clear();
            affectedVisitors.Clear();
        }

        private void CreateLingeringThorns(float radius)
        {
            // Mark tiles at the edge of the AoE as thorn tiles
            int intRadius = Mathf.CeilToInt(radius);
            for (int dx = -intRadius - 1; dx <= intRadius + 1; dx++)
            {
                for (int dy = -intRadius - 1; dy <= intRadius + 1; dy++)
                {
                    int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (dist >= intRadius && dist <= intRadius + 2)
                    {
                        Vector2Int tile = new Vector2Int(centerTile.x + dx, centerTile.y + dy);
                        var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                        if (node != null && node.walkable)
                        {
                            thornTiles.Add(tile);
                        }
                    }
                }
            }

        }

        public bool IsTile(Vector2Int tile)
        {
            return thornTiles.Contains(tile);
        }

        public void OnVisitorStepOnThornTile(VisitorControllerBase visitor)
        {
            if (!thornTiles.Contains(GetVisitorGridPosition(visitor)))
            {
                return;
            }

            // Apply Frightened state
            float frightenedDuration = definition.param2 > 0 ? definition.param2 : 2f;
            visitor.SetFrightened(frightenedDuration);

        }

        private Vector2Int GetVisitorGridPosition(VisitorControllerBase visitor)
        {
            if (manager.MazeGrid.WorldToGrid(visitor.transform.position, out int x, out int y))
            {
                return new Vector2Int(x, y);
            }
            return Vector2Int.zero;
        }
    }

    #endregion

    #region Feastward Panic

    /// <summary>
    /// Releases a wave of fear that makes everywhere but the Heart feel deadly.
    /// Tier I: Selective Terror - Cone/arc mode instead of global
    /// Tier II: Last Refuge - Visitors about to exit become Fascinated to Heart-closer lanterns
    /// Tier III: Hunger Crescendo - Each consumption extends duration and refunds charge
    /// </summary>
    public class FeastwardPanicEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> frightenedVisitors = new HashSet<VisitorControllerBase>();
        private const string ModifierSourceId = "FeastwardPanic";
        private Vector2Int heartTile;

        public FeastwardPanicEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {

            heartTile = manager.MazeGrid.HeartGridPos;

            // Determine mode: Global or Selective (cone)
            bool selectiveMode = definition.tier >= 1 && definition.flag1; // flag1 = selective terror mode

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            int affectedCount = 0;
            int skippedCount = 0;

            foreach (var visitor in visitors)
            {
                if (visitor.State == VisitorControllerBase.VisitorState.Mesmerized ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    skippedCount++;
                    continue; // Don't affect these states
                }

                bool shouldAffect = true;

                if (selectiveMode)
                {
                    // Check if visitor is in cone from Heart toward target direction
                    shouldAffect = IsInCone(visitor.transform.position);
                }

                if (shouldAffect)
                {
                    // Apply Frightened state
                    float frightenedDuration = definition.duration > 0 ? definition.duration : 5f;
                    visitor.SetFrightened(frightenedDuration);
                    frightenedVisitors.Add(visitor);
                    affectedCount++;

                    // Apply Heart-ward path bias
                    ApplyHeartwardBias(visitor);

                }
            }

        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Tier II: Last Refuge - check visitors about to exit
            if (definition.tier >= 2 && definition.flag2)
            {
                CheckLastRefuge();
            }
        }

        public override void OnEnd()
        {
            frightenedVisitors.Clear();
            manager.PathModifier.ClearBySource(ModifierSourceId);

            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.FeastwardPanic);
            }

        }

        private bool IsInCone(Vector3 visitorWorldPos)
        {
            // Simple cone check: visitor is between Heart and target direction
            Vector3 heartWorldPos = manager.MazeGrid.GridToWorld(heartTile.x, heartTile.y);
            Vector3 directionToTarget = (targetPosition - heartWorldPos).normalized;
            Vector3 directionToVisitor = (visitorWorldPos - heartWorldPos).normalized;

            float angle = Vector3.Angle(directionToTarget, directionToVisitor);
            float coneAngle = definition.param1 > 0 ? definition.param1 : 60f; // param1 = cone angle

            return angle <= coneAngle / 2f;
        }

        private void ApplyHeartwardBias(VisitorControllerBase visitor)
        {
            // Apply cost modifiers to bias paths toward Heart
            Vector2Int visitorTile = GetVisitorGridPosition(visitor);

            // Increase cost for tiles farther from Heart, decrease cost for tiles closer
            int radius = definition.intParam1 > 0 ? definition.intParam1 : 10;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Vector2Int tile = new Vector2Int(visitorTile.x + dx, visitorTile.y + dy);
                    var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                    if (node != null && node.walkable)
                    {
                        float distToHeart = Vector2Int.Distance(tile, heartTile);
                        float distFromVisitor = Vector2Int.Distance(tile, visitorTile);

                        // Closer to Heart = cheaper
                        float costMod = distToHeart * 0.5f - 10f; // Tune this formula
                        manager.PathModifier.AddModifier(tile, costMod, definition.duration,
                            $"{ModifierSourceId}_{visitor.GetInstanceID()}");

                        // Add ROYGBIV tile visual (vivid green for Power 4)
                        if (manager.TileVisualizer != null)
                        {
                            // Intensity based on distance from visitor (closer = stronger effect)
                            float intensity = 1.0f - (distFromVisitor / radius);
                            intensity = Mathf.Clamp01(intensity);
                            manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.FeastwardPanic, intensity, definition.duration);
                        }
                    }
                }
            }
        }

        private void CheckLastRefuge()
        {
            // Would need visitor path analysis to determine if about to exit
            // Simplified implementation placeholder
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !frightenedVisitors.Contains(visitor))
            {
                return;
            }

            // Tier III: Hunger Crescendo - extend duration
            float extensionTime = definition.param3 > 0 ? definition.param3 : 1f;
            elapsedTime -= extensionTime; // Subtract from elapsed to extend duration

            // Optionally refund charge (would need HeartPowerManager API)
            manager.AddCharges(1);

        }

        private Vector2Int GetVisitorGridPosition(VisitorControllerBase visitor)
        {
            if (manager.MazeGrid.WorldToGrid(visitor.transform.position, out int x, out int y))
            {
                return new Vector2Int(x, y);
            }
            return Vector2Int.zero;
        }
    }

    #endregion

    #region Covenant with the Wisps

    /// <summary>
    /// Wisps temporarily obey you, prioritizing marked victims and Heart-preferred routes.
    /// Tier I: Twin Flames - Wisps can lead two visitors at once
    /// Tier II: Shepherd's Call - Place a Wisp Beacon for patrol
    /// Tier III: Burning Tithe - Increased essence and reduced dread for Wisp-delivered visitors
    /// </summary>
    public class CovenantWithWispsEffect : ActivePowerEffect
    {
        private List<WillowTheWisp> affectedWisps = new List<WillowTheWisp>();

        public CovenantWithWispsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            // Find all WillowTheWisp instances
            affectedWisps.AddRange(Object.FindObjectsByType<WillowTheWisp>(FindObjectsSortMode.None));

            // Add ROYGBIV tile visual at beacon position (cool blue for Power 5)
            if (manager.TileVisualizer != null && manager.MazeGrid.WorldToGrid(targetPosition, out int tx, out int ty))
            {
                Vector2Int beaconTile = new Vector2Int(tx, ty);
                int radius = definition.intParam1 > 0 ? definition.intParam1 : 5;

                // Create a circular beacon aura
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (manhattanDist <= radius)
                        {
                            Vector2Int tile = new Vector2Int(beaconTile.x + dx, beaconTile.y + dy);
                            var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                            if (node != null && node.walkable)
                            {
                                // Intensity decreases with distance from beacon
                                float intensity = 1.0f - (manhattanDist / (float)radius) * 0.6f;
                                manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.CovenantWithWisps, intensity, definition.duration);
                            }
                        }
                    }
                }
            }

            foreach (var wisp in affectedWisps)
            {
                // Mark wisp as controlled (would need WillowTheWisp API modifications)

                // Tier I: Enable twin flames (capture 2 visitors)
                if (definition.tier >= 1)
                {
                    // Would need WillowTheWisp.SetMaxCapturedVisitors(2)
                }

                // Tier II: Place beacon if specified
                if (definition.tier >= 2 && definition.flag1)
                {
                    PlaceBeacon(wisp);
                }
            }

        }

        public override void OnEnd()
        {
            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.CovenantWithWisps);
            }

            affectedWisps.Clear();
        }

        private void PlaceBeacon(WillowTheWisp wisp)
        {
            // Set wisp to patrol around target position
            // Would need WillowTheWisp API: SetPatrolBeacon(targetPosition, radius)
        }

        public void OnWispDeliverVisitor(WillowTheWisp wisp, VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !affectedWisps.Contains(wisp))
            {
                return;
            }

            // Tier III: Burning Tithe - bonus essence
            int bonusEssence = definition.intParam1 > 0 ? definition.intParam1 : 2;
            manager.AddEssence(bonusEssence);

        }
    }

    #endregion

    #region Puka's Bargain

    /// <summary>
    /// Bribes a Puka: less random drowning, more helpful teleportation near Heart.
    /// Tier I: Chosen Channels - Mark Pact Pools as preferred teleport targets
    /// Tier II: Drowning Debt - Death emits fear AoE with Heart-ward bias
    /// Tier III: Undertow - Once per wave, teleport visitor adjacent to Heart entrance
    /// </summary>
    public class PukasBargainEffect : ActivePowerEffect
    {
        private PukaHazard targetPuka;
        private HashSet<Vector2Int> pactPools = new HashSet<Vector2Int>();
        private bool undertowUsed = false;

        public PukasBargainEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            // Find nearest PukaHazard to target position
            var pukas = Object.FindObjectsByType<PukaHazard>(FindObjectsSortMode.None);
            float minDist = float.MaxValue;

            foreach (var puka in pukas)
            {
                float dist = Vector3.Distance(puka.transform.position, targetPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    targetPuka = puka;
                }
            }

            if (targetPuka == null)
            {
                return;
            }

            // Tier I: Find and mark pact pools (water tiles closer to Heart)
            if (definition.tier >= 1)
            {
                IdentifyPactPools();
            }

        }

        public override void OnEnd()
        {
            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.PukasBargain);
            }

            pactPools.Clear();
        }

        private void IdentifyPactPools()
        {
            // Find water tiles closer to Heart
            Vector2Int heartTile = manager.MazeGrid.HeartGridPos;
            int searchRadius = definition.intParam1 > 0 ? definition.intParam1 : 15;

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                for (int dy = -searchRadius; dy <= searchRadius; dy++)
                {
                    Vector2Int tile = new Vector2Int(heartTile.x + dx, heartTile.y + dy);
                    var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);

                    if (node != null && node.terrain == TileType.Water)
                    {
                        float distToHeart = Vector2Int.Distance(tile, heartTile);
                        if (distToHeart < searchRadius / 2f) // Closer half
                        {
                            pactPools.Add(tile);

                            // Add ROYGBIV tile visual (indigo for Power 6)
                            if (manager.TileVisualizer != null)
                            {
                                // Intensity based on proximity to Heart (closer = stronger)
                                float intensity = 1.0f - (distToHeart / (searchRadius / 2f)) * 0.4f;
                                manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.PukasBargain, intensity, definition.duration);
                            }
                        }
                    }
                }
            }

        }

        public Vector2Int? GetPreferredTeleportTarget()
        {
            // Tier III: Undertow - once per wave, return Heart-adjacent tile
            if (definition.tier >= 3 && !undertowUsed && Random.value < 0.2f) // 20% chance
            {
                undertowUsed = true;
                Vector2Int heartTile = manager.MazeGrid.HeartGridPos;

                // Find adjacent water tile
                Vector2Int[] adjacents = new Vector2Int[]
                {
                    heartTile + Vector2Int.up,
                    heartTile + Vector2Int.down,
                    heartTile + Vector2Int.left,
                    heartTile + Vector2Int.right
                };

                foreach (var adj in adjacents)
                {
                    var node = manager.MazeGrid.Grid.GetNode(adj.x, adj.y);
                    if (node != null && node.terrain == TileType.Water)
                    {
                        return adj;
                    }
                }
            }

            // Otherwise, prefer pact pools
            if (pactPools.Count > 0)
            {
                int randomIndex = Random.Range(0, pactPools.Count);
                return pactPools.ElementAt(randomIndex);
            }

            return null;
        }

        public float GetKillChanceModifier()
        {
            // Reduce kill chance during bargain
            return definition.param1 > 0 ? definition.param1 : 0.5f; // Default 50% reduction
        }

        public void OnPukaKillVisitor(Vector2Int killLocation)
        {
            if (definition.tier < 2)
            {
                return;
            }

            // Tier II: Drowning Debt - emit fear AoE
            float aoeRadius = definition.param2 > 0 ? definition.param2 : 3f;
            Vector3 killWorldPos = manager.MazeGrid.GridToWorld(killLocation.x, killLocation.y);

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var visitor in visitors)
            {
                float dist = Vector3.Distance(visitor.transform.position, killWorldPos);
                if (dist <= aoeRadius * manager.MazeGrid.TileSize)
                {
                    visitor.SetFrightened(3f);
                }
            }
        }
    }

    #endregion

    #region Ring of Invitations

    /// <summary>
    /// FairyRings become irresistible invitations that redirect pilgrims toward Heart.
    /// Tier I: Stacked Circles - Spawn illusory rings along Heart-ward paths
    /// Tier II: Circle Remembered - Entranced visitors prefer routes through rings
    /// Tier III: Closing the Dance - Visitors inside rings when power ends become Mesmerized
    /// </summary>
    public class RingOfInvitationsEffect : ActivePowerEffect
    {
        private List<FairyRing> affectedRings = new List<FairyRing>();
        private HashSet<VisitorControllerBase> entrancedVisitors = new HashSet<VisitorControllerBase>();

        public RingOfInvitationsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            // Find all FairyRings
            affectedRings.AddRange(Object.FindObjectsByType<FairyRing>(FindObjectsSortMode.None));

            foreach (var ring in affectedRings)
            {
                // Mark ring as Heart-tuned (would need FairyRing API modifications)

                // Add ROYGBIV tile visuals around each ring (vibrant violet for Power 7)
                if (manager.TileVisualizer != null && manager.MazeGrid.WorldToGrid(ring.transform.position, out int rx, out int ry))
                {
                    Vector2Int ringTile = new Vector2Int(rx, ry);
                    int ringRadius = definition.intParam2 > 0 ? definition.intParam2 : 3;

                    // Create a circular aura around the ring
                    for (int dx = -ringRadius; dx <= ringRadius; dx++)
                    {
                        for (int dy = -ringRadius; dy <= ringRadius; dy++)
                        {
                            int manhattanDist = Mathf.Abs(dx) + Mathf.Abs(dy);
                            if (manhattanDist <= ringRadius)
                            {
                                Vector2Int tile = new Vector2Int(ringTile.x + dx, ringTile.y + dy);
                                var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                                if (node != null && node.walkable)
                                {
                                    // Intensity strongest at ring center
                                    float intensity = 1.0f - (manhattanDist / (float)ringRadius) * 0.5f;
                                    manager.TileVisualizer.AddTileEffect(tile, HeartPowerType.RingOfInvitations, intensity, definition.duration);
                                }
                            }
                        }
                    }
                }

                // Tier I: Spawn illusory rings
                if (definition.tier >= 1)
                {
                    SpawnIllusoryRings(ring);
                }
            }

        }

        public override void OnEnd()
        {
            // Tier III: Closing the Dance - Mesmerize visitors inside rings
            if (definition.tier >= 3)
            {
                ApplyClosingDance();
            }

            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.RingOfInvitations);
            }

            affectedRings.Clear();
            entrancedVisitors.Clear();
        }

        private void SpawnIllusoryRings(FairyRing sourceRing)
        {
            // Spawn 1-2 temporary ring colliders along Heart-ward paths
            // Would need to instantiate temporary FairyRing objects
            int count = definition.intParam1 > 0 ? definition.intParam1 : 2;

            // Implementation would create temporary trigger colliders
        }

        private void ApplyClosingDance()
        {
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var ring in affectedRings)
            {
                foreach (var visitor in visitors)
                {
                    // Check if visitor is inside ring's trigger
                    float dist = Vector3.Distance(visitor.transform.position, ring.transform.position);
                    float ringRadius = 1f; // Would get from FairyRing

                    if (dist <= ringRadius)
                    {
                        float mesmerizeDuration = definition.param1 > 0 ? definition.param1 : 3f;
                        visitor.SetMesmerized(mesmerizeDuration);
                    }
                }
            }
        }

        public void OnVisitorEntranced(VisitorControllerBase visitor)
        {
            entrancedVisitors.Add(visitor);

            // Tier II: Circle Remembered - would need to modify visitor's pathfinding preferences
            if (definition.tier >= 2)
            {
            }
        }
    }

    #endregion

    #region Heartward Grasp

    /// <summary>
    /// Pulls a visitor through a wall toward the Heart.
    /// Tier I: Extended Reach - Increased pull range
    /// Tier II: Relentless Grasp - Stronger path bias after pull
    /// Tier III: Crushing Embrace - Applies Mesmerized after pull
    /// </summary>
    public class HeartwardGraspEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            InitialPause,      // 0.0 - 0.75s: Visitor stopped
            PullToActivation,  // 0.75 - 1.0s: Pull visitor toward activation tile
            Repositioning,     // 1.0s: Reposition visitor to activation, instant
            PushToDestination, // 1.0 - 1.25s: Push visitor to destination
            FinalPause,        // 1.25 - 2.0s: Wait for grasp animation to finish
            Complete           // 2.0s+: Cleanup
        }

        private GameObject graspVisual;
        private VisitorControllerBase targetVisitor;
        private Vector2Int activationTile;      // Where power was activated (focal point)
        private Vector2Int visitorStartTile;    // Where visitor starts
        private Vector2Int pullDestination;     // First walkable tile along activation→heart
        private const string ModifierSourceId = "HeartwardGrasp";

        private AnimationPhase currentPhase = AnimationPhase.InitialPause;
        private float phaseStartTime = 0f;
        private Vector3 lerpStartPosition;
        private Vector3 lerpEndPosition;
        private bool visitorMovementStopped = false;

        // Grasp visual animation tracking
        private Vector3 graspBasePosition;      // Grasp stays at activation tile
        private Vector3 graspAnimationDirection;

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            Debug.Log($"[HeartwardGrasp] OnStart - Target position: {targetPosition}");

            // Convert target position to grid - this is the activation tile (focal point)
            if (!manager.MazeGrid.WorldToGrid(targetPosition, out int ax, out int ay))
            {
                Debug.Log("[HeartwardGrasp] Failed to convert target position to grid");
                return;
            }

            activationTile = new Vector2Int(ax, ay);
            Vector2Int heartTile = manager.MazeGrid.HeartGridPos;
            Debug.Log($"[HeartwardGrasp] Activation tile: {activationTile}, Heart tile: {heartTile}");

            // Find nearest visitor within range of activation tile
            int pullRange = definition.param1 > 0 ? (int)definition.param1 : 3;
            targetVisitor = FindNearestVisitor(activationTile, pullRange);

            if (targetVisitor == null)
            {
                Debug.Log($"[HeartwardGrasp] No visitor found within {pullRange} tiles of activation point");
                return;
            }

            Debug.Log($"[HeartwardGrasp] Found visitor: {targetVisitor.name}");

            // Get visitor's current position
            if (!manager.MazeGrid.WorldToGrid(targetVisitor.transform.position, out int vx, out int vy))
            {
                Debug.Log("[HeartwardGrasp] Failed to convert visitor position to grid");
                return;
            }

            visitorStartTile = new Vector2Int(vx, vy);
            Debug.Log($"[HeartwardGrasp] Visitor start tile: {visitorStartTile}");

            // Find destination: first walkable tile along activation→heart vector
            pullDestination = FindFirstWalkableAlongVector(activationTile, heartTile);

            if (pullDestination == Vector2Int.zero)
            {
                Debug.Log("[HeartwardGrasp] No walkable destination tile found along activation→heart vector");
                return;
            }

            Debug.Log($"[HeartwardGrasp] Pull destination: {pullDestination} along activation→heart vector");

            // Spawn grasp prefab at activation tile, pointing toward visitor
            SpawnGraspPrefab(activationTile, visitorStartTile);

            // Stop visitor movement
            StopVisitor();

            // Start animation sequence
            currentPhase = AnimationPhase.InitialPause;
            phaseStartTime = elapsedTime;

            Debug.Log("[HeartwardGrasp] Animation sequence started");
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (targetVisitor == null || currentPhase == AnimationPhase.Complete)
            {
                return;
            }

            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case AnimationPhase.InitialPause:
                    // Wait 0.75 seconds
                    if (phaseElapsed >= 0.75f)
                    {
                        // Start pull toward activation tile
                        currentPhase = AnimationPhase.PullToActivation;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = targetVisitor.transform.position;
                        lerpEndPosition = manager.MazeGrid.GridToWorld(activationTile.x, activationTile.y);
                        Debug.Log($"[HeartwardGrasp] Phase: PullToActivation from {lerpStartPosition} to {lerpEndPosition}");
                    }
                    break;

                case AnimationPhase.PullToActivation:
                    // Translate visitor toward activation tile over 0.25 seconds
                    float pullT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pullT);

                    if (phaseElapsed >= 0.25f)
                    {
                        // Instantly reposition visitor to activation tile
                        currentPhase = AnimationPhase.Repositioning;
                        Vector3 activationPosition = manager.MazeGrid.GridToWorld(activationTile.x, activationTile.y);
                        targetVisitor.transform.position = activationPosition;
                        Debug.Log($"[HeartwardGrasp] Repositioned visitor to activation tile {activationTile}");

                        // Reorient grasp toward destination
                        UpdateGraspDirection(activationTile, pullDestination);

                        // Immediately start push to destination
                        currentPhase = AnimationPhase.PushToDestination;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = activationPosition;
                        lerpEndPosition = manager.MazeGrid.GridToWorld(pullDestination.x, pullDestination.y);
                        Debug.Log($"[HeartwardGrasp] Phase: PushToDestination from {lerpStartPosition} to {lerpEndPosition}");
                    }
                    break;

                case AnimationPhase.PushToDestination:
                    // Translate visitor to destination over 0.25 seconds
                    float pushT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pushT);

                    if (phaseElapsed >= 0.25f)
                    {
                        // Start final pause
                        currentPhase = AnimationPhase.FinalPause;
                        phaseStartTime = elapsedTime;
                        targetVisitor.RecalculatePath();
                        ApplyHeartwardBias();
                        ApplyTierEffects();
                        Debug.Log("[HeartwardGrasp] Phase: FinalPause");
                    }
                    break;

                case AnimationPhase.FinalPause:
                    // Wait 0.75 seconds for grasp animation to finish
                    if (phaseElapsed >= 0.75f)
                    {
                        // Cleanup and complete
                        currentPhase = AnimationPhase.Complete;
                        ResumeVisitor();

                        if (graspVisual != null)
                        {
                            Object.Destroy(graspVisual);
                            graspVisual = null;
                        }

                        Debug.Log("[HeartwardGrasp] Animation complete");
                    }
                    break;
            }

            // Animate grasp visual if it exists
            if (graspVisual != null)
            {
                UpdateGraspVisualAnimation();
            }
        }

        private void UpdateGraspVisualAnimation()
        {
            // First half animation (0.0 - 1.0s): extend, hold, retract
            // Second half animation (1.0 - 2.0s): extend, hold, retract
            float animTime = elapsedTime;
            Vector3 offset = Vector3.zero;

            if (animTime < 1.0f)
            {
                // First half: 0.0-0.25s extend, 0.25-0.75s hold, 0.75-1.0s retract
                if (animTime < 0.25f)
                {
                    // Extend toward visitor
                    float t = animTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (animTime < 0.75f)
                {
                    // Hold extended
                    offset = graspAnimationDirection;
                }
                else
                {
                    // Retract back
                    float t = (1.0f - animTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }
            else if (animTime < 2.0f)
            {
                // Second half: 1.0-1.25s extend, 1.25-1.75s hold, 1.75-2.0s retract
                float secondHalfTime = animTime - 1.0f;

                if (secondHalfTime < 0.25f)
                {
                    // Extend toward destination
                    float t = secondHalfTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (secondHalfTime < 0.75f)
                {
                    // Hold extended
                    offset = graspAnimationDirection;
                }
                else
                {
                    // Retract back
                    float t = (1.0f - secondHalfTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }

            graspVisual.transform.position = graspBasePosition + offset;
        }

        public override void OnEnd()
        {
            // Resume visitor if still paused
            if (targetVisitor != null && visitorMovementStopped)
            {
                ResumeVisitor();
            }

            // Clean up path modifiers
            manager.PathModifier.ClearBySource(ModifierSourceId);

            // Clean up visual
            if (graspVisual != null)
            {
                Object.Destroy(graspVisual);
                graspVisual = null;
            }

            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartwardGrasp);
            }
        }

        private void StopVisitor()
        {
            if (targetVisitor == null || visitorMovementStopped)
            {
                return;
            }

            // Stop visitor in place
            targetVisitor.Stop();
            visitorMovementStopped = true;
            Debug.Log("[HeartwardGrasp] Visitor movement stopped");
        }

        private void ResumeVisitor()
        {
            if (targetVisitor == null || !visitorMovementStopped)
            {
                return;
            }

            // Resume visitor movement
            targetVisitor.Resume();
            visitorMovementStopped = false;
            Debug.Log("[HeartwardGrasp] Visitor movement resumed");
        }

        private void SpawnGraspPrefab(Vector2Int tile, Vector2Int pointToward)
        {
            // Load grasp prefab
            GameObject graspPrefab = Resources.Load<GameObject>("Prefabs/Props/Grasp/grasp");

            if (graspPrefab == null)
            {
                Debug.LogWarning("[HeartwardGrasp] Could not load grasp prefab from Resources/Prefabs/Props/Grasp/grasp");
                return;
            }

            // Get world position for grasp tile
            Vector3 graspPosition = manager.MazeGrid.GridToWorld(tile.x, tile.y);
            graspPosition.z = 0f; // At sprite layer for visibility

            // Calculate direction from grasp to point toward (visitor)
            Vector3 pointTowardPosition = manager.MazeGrid.GridToWorld(pointToward.x, pointToward.y);
            Vector3 direction = pointTowardPosition - graspPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            else
            {
                direction.Normalize();
            }

            // Create rotation with Y-axis pointing toward visitor
            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

            // Instantiate grasp prefab
            graspVisual = Object.Instantiate(graspPrefab, graspPosition, rotation);
            graspVisual.name = "GraspEffect";

            // Store base position (grasp stays at activation tile) and animation direction
            graspBasePosition = graspPosition;
            graspAnimationDirection = direction;

            Debug.Log($"[HeartwardGrasp] Spawned grasp prefab at {graspPosition} pointing toward {pointTowardPosition}");
        }

        private void UpdateGraspDirection(Vector2Int from, Vector2Int to)
        {
            if (graspVisual == null)
            {
                return;
            }

            // Calculate new direction
            Vector3 fromPosition = manager.MazeGrid.GridToWorld(from.x, from.y);
            Vector3 toPosition = manager.MazeGrid.GridToWorld(to.x, to.y);
            Vector3 direction = toPosition - fromPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);
                graspVisual.transform.rotation = rotation;

                // Update animation direction for second half
                graspAnimationDirection = direction;

                Debug.Log($"[HeartwardGrasp] Reoriented grasp toward destination");
            }
        }

        private Vector2Int FindFirstWalkableAlongVector(Vector2Int from, Vector2Int to)
        {
            // Calculate direction vector from activation to heart
            Vector2 direction = new Vector2(to.x - from.x, to.y - from.y);
            float magnitude = direction.magnitude;

            if (magnitude < 0.01f)
            {
                return Vector2Int.zero;
            }

            direction /= magnitude;

            // Calculate perpendicular vector for margin checking (±1 tile)
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            // Search for first walkable tiles past a wall (stop as soon as found)
            int maxSearchDistance = 10;
            bool foundWall = false;
            List<Vector2Int> walkableTiles = new List<Vector2Int>();
            List<Vector2Int> walkableTilesPastWall = new List<Vector2Int>();

            for (int step = 1; step <= maxSearchDistance; step++)
            {
                Vector2 centerPos = new Vector2(from.x, from.y) + direction * step;

                // Check center tile and ±1 perpendicular
                Vector2Int[] candidates = new Vector2Int[]
                {
                    new Vector2Int(Mathf.RoundToInt(centerPos.x), Mathf.RoundToInt(centerPos.y)), // Center
                    new Vector2Int(Mathf.RoundToInt(centerPos.x + perpendicular.x), Mathf.RoundToInt(centerPos.y + perpendicular.y)), // +1 perpendicular
                    new Vector2Int(Mathf.RoundToInt(centerPos.x - perpendicular.x), Mathf.RoundToInt(centerPos.y - perpendicular.y))  // -1 perpendicular
                };

                // Check if any candidate is a wall
                bool stepHasWall = false;
                foreach (var candidateTile in candidates)
                {
                    var node = manager.MazeGrid.Grid.GetNode(candidateTile.x, candidateTile.y);
                    if (node != null && !node.walkable)
                    {
                        stepHasWall = true;
                    }
                    else if (node != null && node.walkable)
                    {
                        // Collect walkable tiles
                        if (!walkableTiles.Contains(candidateTile))
                        {
                            walkableTiles.Add(candidateTile);
                            if (foundWall)
                            {
                                walkableTilesPastWall.Add(candidateTile);
                            }
                        }
                    }
                }

                if (stepHasWall)
                {
                    foundWall = true;
                }

                // Stop immediately once we've found walkable tiles past a wall
                if (walkableTilesPastWall.Count > 0)
                {
                    break;
                }
            }

            // Prefer tiles past a wall, otherwise use any walkable tile found
            List<Vector2Int> eligibleTiles = walkableTilesPastWall.Count > 0 ? walkableTilesPastWall : walkableTiles;

            if (eligibleTiles.Count == 0)
            {
                Debug.LogWarning($"[HeartwardGrasp] No walkable tiles found within {maxSearchDistance} tiles toward heart");
                return Vector2Int.zero;
            }

            // Return the tile closest to the heart
            Vector2Int closestTile = eligibleTiles[0];
            float closestDistance = Vector2.Distance(closestTile, to);

            foreach (var tile in eligibleTiles)
            {
                float distance = Vector2.Distance(tile, to);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tile;
                }
            }

            string context = walkableTilesPastWall.Count > 0 ? "past wall" : "no wall found";
            Debug.Log($"[HeartwardGrasp] Found destination at {closestTile} ({context}, closest to heart from {eligibleTiles.Count} options)");
            return closestTile;
        }

        private void ApplyTierEffects()
        {
            if (targetVisitor == null)
            {
                return;
            }

            // Tier III: Apply Mesmerized state
            if (definition.tier >= 3 && definition.flag1)
            {
                float mesmerizeDuration = definition.param3 > 0 ? definition.param3 : 3f;
                targetVisitor.SetMesmerized(mesmerizeDuration);
                Debug.Log($"[HeartwardGrasp] Applied Mesmerized for {mesmerizeDuration}s (Tier III)");
            }
        }

        private VisitorControllerBase FindNearestVisitor(Vector2Int targetTile, int range)
        {
            // Use visitor registry instead of FindObjectsByType
            var visitors = VisitorRegistry.All;
            VisitorControllerBase nearest = null;
            float minDistance = float.MaxValue;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                // Get visitor grid position
                if (!manager.MazeGrid.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    continue;
                }

                Vector2Int visitorTile = new Vector2Int(vx, vy);

                // Check if within range (Manhattan distance)
                int distance = Mathf.Abs(visitorTile.x - targetTile.x) + Mathf.Abs(visitorTile.y - targetTile.y);

                if (distance <= range && distance < minDistance)
                {
                    nearest = visitor;
                    minDistance = distance;
                }
            }

            return nearest;
        }

        private void ApplyHeartwardBias()
        {
            if (targetVisitor == null)
            {
                return;
            }

            // Get bias strength from definition (param2 = bias strength, default -3.0)
            // Tier II increases strength
            float biasStrength = definition.param2 != 0 ? definition.param2 : -3.0f;

            if (definition.tier >= 2 && definition.flag2) // flag2 = relentless grasp
            {
                biasStrength *= 2.0f; // Double strength for Tier II
            }

            biasStrength = -Mathf.Abs(biasStrength); // Ensure negative (attractive)

            // Apply path cost modifier in a radius around the visitor
            Vector2Int heartTile = manager.MazeGrid.HeartGridPos;
            int biasRadius = definition.intParam1 > 0 ? definition.intParam1 : 8;

            // Get visitor's current position
            if (!manager.MazeGrid.WorldToGrid(targetVisitor.transform.position, out int vx, out int vy))
            {
                return;
            }

            Vector2Int visitorTile = new Vector2Int(vx, vy);

            for (int dx = -biasRadius; dx <= biasRadius; dx++)
            {
                for (int dy = -biasRadius; dy <= biasRadius; dy++)
                {
                    Vector2Int tile = new Vector2Int(visitorTile.x + dx, visitorTile.y + dy);
                    var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);

                    if (node != null && node.walkable)
                    {
                        float distToHeart = Vector2Int.Distance(tile, heartTile);
                        float distFromVisitor = Vector2Int.Distance(tile, visitorTile);

                        // Tiles closer to Heart get stronger bias
                        float normalizedDist = 1.0f - Mathf.Clamp01(distToHeart / biasRadius);
                        float costMod = biasStrength * normalizedDist;

                        manager.PathModifier.AddModifier(tile, costMod, definition.duration,
                            $"{ModifierSourceId}_{targetVisitor.GetInstanceID()}");
                    }
                }
            }
        }
    }

    #endregion

    #region Devouring Maw

    /// <summary>
    /// Instantly consumes a visitor on the targeted tile, granting essence.
    /// Tier I: Echoing Terror - Applies fear to nearby visitors
    /// Tier II: Draining Embrace - Slows nearby visitors briefly
    /// Tier III: Soul Harvest - Extra essence and temporary charge bonus
    /// </summary>
    public class DevouringMawEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            Pause,              // 0.0 - 0.75s: Visitor paused
            SinkAndDevour,      // 0.75s+: Visitor sinks into ground, devour prefab spawns
            Complete            // Visitor consumed and destroyed
        }

        private GameObject devourVisual;
        private VisitorControllerBase consumedVisitor;
        private Vector2Int targetTile;
        private AnimationPhase currentPhase;
        private float phaseStartTime;
        private Vector3 visitorStartPosition;
        private bool hasConsumedVisitor;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            Debug.Log($"[DevouringMaw] OnStart - Target position: {targetPosition}");

            // Convert target position to grid
            if (!manager.MazeGrid.WorldToGrid(targetPosition, out int tx, out int ty))
            {
                Debug.Log("[DevouringMaw] Failed to convert target position to grid");
                return;
            }

            targetTile = new Vector2Int(tx, ty);
            Debug.Log($"[DevouringMaw] Target tile: {targetTile}");

            // Find visitor on the targeted tile
            VisitorControllerBase targetVisitor = FindVisitorOnTile(targetTile);

            if (targetVisitor == null)
            {
                Debug.Log("[DevouringMaw] No visitor found on target tile");
                return;
            }

            Debug.Log($"[DevouringMaw] Found visitor on tile: {targetVisitor.name}");
            consumedVisitor = targetVisitor;

            // Stop the visitor movement
            consumedVisitor.Stop();
            visitorStartPosition = consumedVisitor.transform.position;

            // Initialize animation state
            currentPhase = AnimationPhase.Pause;
            phaseStartTime = 0f;
            hasConsumedVisitor = false;

            // Add tile visualizer effect
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffect(targetTile, HeartPowerType.DevouringMaw, 1.0f, 2.0f);
            }

            // Tier I: Apply fear to nearby visitors
            if (definition.tier >= 1 && definition.flag1)
            {
                ApplyEchoingTerror(targetTile);
            }

            // Tier II: Slow nearby visitors
            if (definition.tier >= 2 && definition.flag2)
            {
                ApplyDrainingEmbrace(targetTile);
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (consumedVisitor == null)
            {
                Debug.Log($"[DevouringMaw] Update called but consumedVisitor is null at {elapsedTime}s");
                return;
            }

            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case AnimationPhase.Pause:
                    // Visitor is paused for 0.75 seconds
                    if (phaseElapsed >= 0.75f)
                    {
                        // Transition to sink and devour phase
                        currentPhase = AnimationPhase.SinkAndDevour;
                        phaseStartTime = elapsedTime;

                        Debug.Log($"[DevouringMaw] Transitioning to SinkAndDevour phase at {elapsedTime}s");

                        // Spawn devour prefab at target tile
                        InstantiateDevourVisual(targetTile);

                        Debug.Log($"[DevouringMaw] Starting sink animation at {elapsedTime}s");
                    }
                    break;

                case AnimationPhase.SinkAndDevour:
                    // Sink the visitor 1 unit into the ground over a brief duration
                    float sinkDuration = 0.5f;
                    float sinkT = Mathf.Clamp01(phaseElapsed / sinkDuration);

                    // Lerp Z position from start to -1
                    Vector3 sinkPosition = visitorStartPosition;
                    sinkPosition.z = Mathf.Lerp(visitorStartPosition.z, visitorStartPosition.z - 1f, sinkT);
                    consumedVisitor.transform.position = sinkPosition;

                    if (phaseElapsed < 0.1f) // Log once at the start
                    {
                        Debug.Log($"[DevouringMaw] SinkAndDevour - sinkT: {sinkT:F2}, position: {sinkPosition}, startZ: {visitorStartPosition.z}");
                    }

                    // Clean up devour visual after 1 second (at 1.75s total time)
                    if (devourVisual != null && elapsedTime >= 1.75f)
                    {
                        Object.Destroy(devourVisual);
                        devourVisual = null;
                        Debug.Log($"[DevouringMaw] Despawning devour prefab at {elapsedTime}s");
                    }

                    // After sink animation completes, consume the visitor
                    if (sinkT >= 1f && !hasConsumedVisitor)
                    {
                        hasConsumedVisitor = true;

                        // Consume the visitor (grant essence and destroy)
                        Debug.Log($"[DevouringMaw] Consuming visitor at {elapsedTime}s, final position: {consumedVisitor.transform.position}");
                        ConsumeVisitor(consumedVisitor);

                        // Tier III: Extra essence and charge bonus
                        if (definition.tier >= 3)
                        {
                            ApplySoulHarvest();
                        }

                        currentPhase = AnimationPhase.Complete;
                        Debug.Log($"[DevouringMaw] Transitioning to Complete phase at {elapsedTime}s");
                    }
                    break;

                case AnimationPhase.Complete:
                    // Animation complete, nothing to do
                    break;
            }
        }

        public override void OnEnd()
        {
            // Clean up visual
            if (devourVisual != null)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }

            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DevouringMaw);
            }
        }

        private VisitorControllerBase FindVisitorOnTile(Vector2Int tile)
        {
            // Use visitor registry
            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                // Get visitor grid position
                if (!manager.MazeGrid.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    continue;
                }

                Vector2Int visitorTile = new Vector2Int(vx, vy);

                // Check if visitor is on target tile
                if (visitorTile == tile)
                {
                    return visitor;
                }
            }

            return null;
        }

        private void InstantiateDevourVisual(Vector2Int tile)
        {
            // Load devour prefab from Resources folder
            GameObject devourPrefab = Resources.Load<GameObject>("Prefabs/Props/devour");

            if (devourPrefab == null)
            {
                Debug.LogError("[DevouringMaw] Failed to load devour prefab from Resources/Prefabs/Props/devour");
                return;
            }

            // Instantiate at tile position
            Vector3 worldPos = manager.MazeGrid.GridToWorld(tile.x, tile.y);
            worldPos.z = 0f; // At floor level

            devourVisual = Object.Instantiate(devourPrefab, worldPos, Quaternion.identity);
            Debug.Log($"[DevouringMaw] Spawned devour prefab at {worldPos}");
        }

        private void ConsumeVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Get essence reward
            int essence = visitor.GetEssenceReward();

            // Add essence to GameController
            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(essence);
            }

            // Track stats
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            // Play sound
            SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Object.Destroy(visitor.gameObject);
        }

        private void ApplyEchoingTerror(Vector2Int centerTile)
        {
            // Get fear radius from definition (param1 = fear radius, default 3)
            int fearRadius = definition.param1 > 0 ? (int)definition.param1 : 3;
            float fearDuration = definition.param2 > 0 ? definition.param2 : 3f;

            // Find all visitors within radius
            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping ||
                    visitor == consumedVisitor) // Don't affect the consumed visitor
                {
                    continue;
                }

                // Get visitor grid position
                if (!manager.MazeGrid.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    continue;
                }

                Vector2Int visitorTile = new Vector2Int(vx, vy);

                // Check if within radius (Manhattan distance)
                int distance = Mathf.Abs(visitorTile.x - centerTile.x) + Mathf.Abs(visitorTile.y - centerTile.y);

                if (distance <= fearRadius)
                {
                    // Apply Frightened state
                    visitor.SetFrightened(fearDuration);
                }
            }
        }

        private void ApplyDrainingEmbrace(Vector2Int centerTile)
        {
            // Get slow radius and duration from definition
            int slowRadius = definition.intParam1 > 0 ? definition.intParam1 : 3;
            float slowDuration = definition.param3 > 0 ? definition.param3 : 4f;

            // Find all visitors within radius
            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping ||
                    visitor == consumedVisitor)
                {
                    continue;
                }

                // Get visitor grid position
                if (!manager.MazeGrid.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    continue;
                }

                Vector2Int visitorTile = new Vector2Int(vx, vy);

                // Check if within radius
                int distance = Mathf.Abs(visitorTile.x - centerTile.x) + Mathf.Abs(visitorTile.y - centerTile.y);

                if (distance <= slowRadius)
                {
                    // Apply slow (using Mesmerize as slow effect)
                    visitor.SetMesmerized(slowDuration);
                }
            }
        }

        private void ApplySoulHarvest()
        {
            // Extra essence bonus
            int bonusEssence = definition.intParam2 > 0 ? definition.intParam2 : 3;

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(bonusEssence);
            }

            // Temporary charge bonus
            manager.AddCharges(1);
        }
    }

    #endregion
}

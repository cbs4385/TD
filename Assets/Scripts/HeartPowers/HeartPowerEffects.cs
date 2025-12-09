using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;

namespace FaeMaze.HeartPowers
{
    #region Heartbeat of Longing

    /// <summary>
    /// Amplifies FaeLanterns to pull visitors more strongly and tilt routes through their influence.
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
            foreach (var lantern in affectedLanterns)
            {
                var influenceTiles = GetLanternInfluenceTiles(lantern);
                foreach (var tile in influenceTiles)
                {
                    lanternInfluenceTiles.Add(tile);
                    // Negative cost = more attractive (param1 = attraction strength, default -2.0)
                    float attractionBonus = -Mathf.Abs(definition.param1 != 0 ? definition.param1 : 2.0f);
                    manager.PathModifier.AddModifier(tile, attractionBonus, definition.duration, ModifierSourceId);
                }
            }

            Debug.Log($"[HeartbeatOfLonging] Activated on {affectedLanterns.Count} lanterns affecting {lanternInfluenceTiles.Count} tiles");
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
                CheckForDev ouringChorus();
            }
        }

        public override void OnEnd()
        {
            // Cleanup path modifiers
            manager.PathModifier.ClearBySource(ModifierSourceId);
            affectedLanterns.Clear();
            lanternInfluenceTiles.Clear();

            Debug.Log($"[HeartbeatOfLonging] Effect ended");
        }

        private HashSet<Vector2Int> GetLanternInfluenceTiles(FaeLantern lantern)
        {
            // Get the lantern's influence area (simplified - assumes flood-fill from lantern position)
            HashSet<Vector2Int> tiles = new HashSet<Vector2Int>();
            Vector2Int lanternPos = lantern.GridPosition;
            int radius = definition.intParam1 > 0 ? definition.intParam1 : 6; // Default influence radius

            // Simple radius-based influence (could be improved with actual flood-fill)
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= radius) // Manhattan distance
                    {
                        Vector2Int tile = new Vector2Int(lanternPos.x + dx, lanternPos.y + dy);
                        if (manager.MazeGrid.Grid.InBounds(tile.x, tile.y))
                        {
                            var node = manager.MazeGrid.Grid.GetNode(tile.x, tile.y);
                            if (node != null && node.walkable)
                            {
                                tiles.Add(tile);
                            }
                        }
                    }
                }
            }

            return tiles;
        }

        private void ApplyEchoingThrum()
        {
            // Find all visitors and check if their upcoming path enters a lantern influence tile
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            int lookAheadSteps = definition.intParam2 > 0 ? definition.intParam2 : 5;

            foreach (var visitor in visitors)
            {
                if (visitor.State == VisitorControllerBase.VisitorState.Fascinated)
                {
                    continue; // Already fascinated
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
                    Debug.Log($"[HeartbeatOfLonging] Echoing Thrum triggered for visitor at {visitorGridPos}");
                }
            }
        }

        private void CheckForDevoringChorus()
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
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var v in visitors)
            {
                if (v.State != VisitorControllerBase.VisitorState.Consumed &&
                    v.State != VisitorControllerBase.VisitorState.Escaping)
                {
                    Vector2Int vPos = GetVisitorGridPosition(v);
                    if (lanternInfluenceTiles.Contains(vPos))
                    {
                        // Apply temporary strong Heart bias (would need visitor API to modify pathfinding)
                        Debug.Log($"[HeartbeatOfLonging] Devouring Chorus triggered for visitor at {vPos}");
                    }
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
    /// Creates corridors of desire or sealing, tilting pathfinding costs.
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
                    : -Mathf.Abs(definition.param1 != 0 ? definition.param1 : 3.0f); // Negative = attractive

                // Apply cost modifier to segment tiles
                foreach (var tile in pathSegment)
                {
                    manager.PathModifier.AddModifier(tile, costModifier, definition.duration, instanceSourceId);
                }

                Debug.Log($"[MurmuringPaths] Created {(sealMode ? "sealed" : "luring")} path segment with {pathSegment.Count} tiles");
            }
        }

        public override void OnEnd()
        {
            manager.PathModifier.ClearBySource(instanceSourceId);
            pathSegment.Clear();
            Debug.Log($"[MurmuringPaths] Effect ended");
        }

        private List<Vector2Int> GeneratePathSegment(Vector2Int startTile)
        {
            List<Vector2Int> segment = new List<Vector2Int>();
            int segmentLength = definition.intParam1 > 0 ? definition.intParam1 : 7; // param1 = segment length

            Vector2Int current = startTile;
            segment.Add(current);

            // Generate a simple path segment (random walk with constraints)
            for (int i = 0; i < segmentLength - 1; i++)
            {
                List<Vector2Int> neighbors = GetWalkableNeighbors(current);
                if (neighbors.Count == 0)
                {
                    break;
                }

                // Prefer continuing in a similar direction
                current = neighbors[Random.Range(0, neighbors.Count)];
                segment.Add(current);
            }

            return segment;
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
                Debug.LogWarning("[DreamSnare] Invalid target position");
                return;
            }

            centerTile = new Vector2Int(x, y);
            float radius = definition.radius > 0 ? definition.radius : 3f;

            // Find all visitors in AoE and apply Mesmerized
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
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

                    Debug.Log($"[DreamSnare] Mesmerized visitor at {visitorPos}");

                    // Tier III: Mark for harvest
                    if (definition.tier >= 3)
                    {
                        // Would need to add a flag to visitor for tracking
                        Debug.Log($"[DreamSnare] Marked visitor for harvest");
                    }
                }
            }

            // Tier I: Create lingering thorn tiles at edge
            if (definition.tier >= 1 && definition.flag1)
            {
                CreateLin geringThorns(radius);
            }

            Debug.Log($"[DreamSnare] Activated at {centerTile}, affected {affectedVisitors.Count} visitors");
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
            thornTiles.Clear();
            affectedVisitors.Clear();
            Debug.Log($"[DreamSnare] Effect ended");
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

            Debug.Log($"[DreamSnare] Created {thornTiles.Count} lingering thorn tiles");
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

            Debug.Log($"[DreamSnare] Visitor stepped on thorn tile, applying Frightened");
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
            foreach (var visitor in visitors)
            {
                if (visitor.State == VisitorControllerBase.VisitorState.Mesmerized ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
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

                    // Apply Heart-ward path bias
                    ApplyHeartwardBias(visitor);

                    Debug.Log($"[FeastwardPanic] Applied panic to visitor at {visitor.transform.position}");
                }
            }

            Debug.Log($"[FeastwardPanic] Activated, affected {frightenedVisitors.Count} visitors");
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
            Debug.Log($"[FeastwardPanic] Effect ended");
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

            Debug.Log($"[FeastwardPanic] Hunger Crescendo triggered, extended duration by {extensionTime}s");
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

            foreach (var wisp in affectedWisps)
            {
                // Mark wisp as controlled (would need WillowTheWisp API modifications)
                Debug.Log($"[CovenantWithWisps] Controlling wisp at {wisp.transform.position}");

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

            Debug.Log($"[CovenantWithWisps] Activated on {affectedWisps.Count} wisps");
        }

        public override void OnEnd()
        {
            affectedWisps.Clear();
            Debug.Log($"[CovenantWithWisps] Effect ended");
        }

        private void PlaceBeacon(WillowTheWisp wisp)
        {
            // Set wisp to patrol around target position
            // Would need WillowTheWisp API: SetPatrolBeacon(targetPosition, radius)
            Debug.Log($"[CovenantWithWisps] Placed beacon at {targetPosition} for wisp");
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

            Debug.Log($"[CovenantWithWisps] Burning Tithe granted {bonusEssence} bonus essence");
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
                Debug.LogWarning("[PukasBargain] No Puka found near target");
                return;
            }

            // Tier I: Find and mark pact pools (water tiles closer to Heart)
            if (definition.tier >= 1)
            {
                IdentifyPactPools();
            }

            Debug.Log($"[PukasBargain] Activated on Puka at {targetPuka.transform.position}");
        }

        public override void OnEnd()
        {
            pactPools.Clear();
            Debug.Log($"[PukasBargain] Effect ended");
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
                        }
                    }
                }
            }

            Debug.Log($"[PukasBargain] Identified {pactPools.Count} pact pools");
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
                        Debug.Log($"[PukasBargain] Undertow activated, teleporting to Heart entrance");
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
                    Debug.Log($"[PukasBargain] Drowning Debt triggered fear on visitor at {visitor.transform.position}");
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
                Debug.Log($"[RingOfInvitations] Enhanced ring at {ring.transform.position}");

                // Tier I: Spawn illusory rings
                if (definition.tier >= 1)
                {
                    SpawnIllusoryRings(ring);
                }
            }

            Debug.Log($"[RingOfInvitations] Activated on {affectedRings.Count} rings");
        }

        public override void OnEnd()
        {
            // Tier III: Closing the Dance - Mesmerize visitors inside rings
            if (definition.tier >= 3)
            {
                ApplyClosingDance();
            }

            affectedRings.Clear();
            entrancedVisitors.Clear();
            Debug.Log($"[RingOfInvitations] Effect ended");
        }

        private void SpawnIllusoryRings(FairyRing sourceRing)
        {
            // Spawn 1-2 temporary ring colliders along Heart-ward paths
            // Would need to instantiate temporary FairyRing objects
            int count = definition.intParam1 > 0 ? definition.intParam1 : 2;
            Debug.Log($"[RingOfInvitations] Spawning {count} illusory rings from {sourceRing.name}");

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
                        Debug.Log($"[RingOfInvitations] Closing Dance mesmerized visitor at {visitor.transform.position}");
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
                Debug.Log($"[RingOfInvitations] Visitor {visitor.name} will remember the circles");
            }
        }
    }

    #endregion
}

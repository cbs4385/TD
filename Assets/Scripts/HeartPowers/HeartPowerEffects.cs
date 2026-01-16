using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.HeartPowers
{
    /*
    #region Heartbeat of Longing

    /// <summary>
    /// Amplifies FaeLanterns to pull visitors more strongly and tilt routes through their influence.
    /// Coverage scales with tier: I = 50% map, II = 75% map, III = 100% map (entire map).
    /// Attraction strength diminishes with distance from lantern.
    /// Now uses world-space coordinates instead of grid positions.
    /// </summary>
    public class HeartbeatOfLongingEffect : ActivePowerEffect
    {
        private List<FaeLantern> affectedLanterns = new List<FaeLantern>();
        private HashSet<Vector3> lanternInfluencePositions = new HashSet<Vector3>();
        private const string ModifierSourceId = "HeartbeatOfLonging";

        public HeartbeatOfLongingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            // Mark all FaeLanterns as Heart-linked for duration
            affectedLanterns.AddRange(FaeLantern.All);

            // Get tier-based radius for world-space area effect
            float tierRadius = GetTierBasedRadius();

            foreach (var lantern in affectedLanterns)
            {
                Vector3 lanternWorldPos = lantern.transform.position;
                lanternInfluencePositions.Add(lanternWorldPos);

                // Add world-space tile visual at lantern position
                if (manager.TileVisualizer != null)
                {
                    float intensity = 1.0f;
                    manager.TileVisualizer.AddTileEffectAtWorldPos(lanternWorldPos, HeartPowerType.HeartbeatOfLonging, intensity, definition.duration);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Tier I: Echoing Thrum - Check visitors approaching lantern areas
            if (definition.tier >= 1 && definition.flag1)
            {
                ApplyEchoingThrum();
            }

            // Tier III: Devouring Chorus - Check for consumed visitors in lantern influence
            if (definition.tier >= 3 && definition.flag2)
            {
                CheckForDevouringChorus();
            }
        }

        public override void OnEnd()
        {
            // Remove tile visuals
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartbeatOfLonging);
            }

            affectedLanterns.Clear();
            lanternInfluencePositions.Clear();
        }

        /// <summary>
        /// Calculates tier-based radius for lantern influence in world units.
        /// </summary>
        private float GetTierBasedRadius()
        {
            // Use a reasonable world-space default radius
            float baseRadius = 20f;

            float coveragePercent = definition.tier switch
            {
                1 => 0.5f,  // 50% coverage
                2 => 0.75f, // 75% coverage
                3 => 1.0f,  // 100% coverage (entire map)
                _ => 0.5f   // Default to tier 1
            };

            return baseRadius * coveragePercent;
        }

        private void ApplyEchoingThrum()
        {
            // Use visitor registry instead of expensive FindObjectsByType
            var visitors = VisitorRegistry.All;
            float tierRadius = GetTierBasedRadius();

            foreach (var visitor in visitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Fascinated)
                {
                    continue;
                }

                // Check if visitor is near any lantern influence position (world-space distance)
                Vector3 visitorPos = visitor.transform.position;
                bool nearInfluence = IsNearLanternInfluence(visitorPos, tierRadius * 0.2f);

                if (nearInfluence && Random.value < 0.3f)
                {
                    // Apply fascination if the visitor has a public method for it
                }
            }
        }

        private void CheckForDevouringChorus()
        {
            // Placeholder for consumed visitor events
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3)
            {
                return;
            }

            var visitors = VisitorRegistry.All;
            float tierRadius = GetTierBasedRadius();

            foreach (var v in visitors)
            {
                if (v == null || v.State == VisitorControllerBase.VisitorState.Consumed ||
                    v.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                Vector3 vPos = v.transform.position;
                if (IsNearLanternInfluence(vPos, tierRadius))
                {
                    // Apply temporary strong Heart bias
                }
            }
        }

        private bool IsNearLanternInfluence(Vector3 worldPos, float range)
        {
            foreach (var lanternPos in lanternInfluencePositions)
            {
                float dist = Vector3.Distance(worldPos, lanternPos);
                if (dist <= range)
                {
                    return true;
                }
            }
            return false;
        }
    }

    #endregion
    */

    #region Murmuring Paths

    /// <summary>
    /// Affects all visitors on the targeted node or edge.
    /// Uses visitor pathfinding to find path to heart.
    /// Lures affected visitors toward the heart tile.
    /// Toggle power: no cooldown, expires when visitors consumed equals power tier.
    /// Visualizes with fairy ring style lights that trace paths to the heart from all affected positions.
    /// </summary>
    public class MurmuringPathsEffect : ActivePowerEffect
    {
        private List<Vector3> pathPositions = new List<Vector3>();
        private const string ModifierSourceId = "MurmuringPaths";
        private static int segmentCounter = 0;
        private string instanceSourceId;
        private float animationTime = 0f;

        // Track which node/edge is affected (initial trigger location)
        private int affectedNodeIndex = -1;
        private int affectedEdgeIndex = -1;

        // Track ALL nodes and edges along the path to the heart
        private HashSet<int> allAffectedNodeIndices = new HashSet<int>();
        private HashSet<int> allAffectedEdgeIndices = new HashSet<int>();

        // Track visitors affected by this power instance
        private HashSet<VisitorControllerBase> affectedVisitors = new HashSet<VisitorControllerBase>();

        // Toggle power expiration: expires when consumedCount reaches powerTier
        private int consumedCount = 0;
        private int requiredConsumptions = 1; // Set to power tier on start
        private bool hasExpired = false;

        // Visual elements - fog quad covering affected area
        private GameObject visualContainer;
        private GameObject fogQuad;
        private Material fogMaterial;
        private Texture2D pathMaskTexture;
        private Bounds fogBounds;

        // Fog visual settings
        private const float FOG_Z_POSITION = -0.2f; // Above path, below UI
        private const float FOG_PADDING = 3f; // Padding around affected area
        private const float MASK_PIXELS_PER_UNIT = 4f; // Resolution of path mask

        // Power color (Deep Red for MurmuringPaths - power index 0)
        private static readonly Color PowerFogColor = new Color(0.8f, 0.1f, 0.1f, 0.5f);
        private static readonly Color PowerFogColorDark = new Color(0.5f, 0.05f, 0.05f, 0.5f);
        private static readonly Color PowerGlowColor = new Color(1f, 0.4f, 0.3f, 1f);

        // Wave animation settings
        private const float WAVE_SPEED = 0.3f; // How fast wave travels (0-1 per second)
        private const float WAVE_CYCLE_PAUSE = 1.5f; // Pause between wave cycles
        private float waveProgress = 0f; // 0 = at furthest, 1 = at heart
        private bool waveActive = true;
        private float wavePauseTimer = 0f;

        // Furthest extent tracking for wave animation
        private Vector3 furthestPosition;
        private Vector3 heartPosition;

        // Store all paths from affected positions to heart
        private List<List<Vector3>> allPathsToHeart = new List<List<Vector3>>();

        // Store ALL tile positions on affected nodes/edges (for fog coverage)
        private List<Vector3> allAffectedTilePositions = new List<Vector3>();

        public MurmuringPathsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            instanceSourceId = $"{ModifierSourceId}_{segmentCounter++}";
        }

        /// <summary>
        /// Override IsExpired to use consumption-based expiration instead of duration.
        /// Power expires when consumed visitor count reaches the power tier.
        /// </summary>
        public override bool IsExpired => hasExpired;

        /// <summary>
        /// Called by HeartPowerManager when a visitor is consumed.
        /// Increments the consumption count and triggers expiration when threshold is reached.
        /// </summary>
        public void OnVisitorConsumed()
        {
            if (hasExpired)
            {
                return;
            }

            consumedCount++;
            if (consumedCount >= requiredConsumptions)
            {
                hasExpired = true;
            }
        }

        /// <summary>
        /// Gets the current consumption progress (for UI display).
        /// </summary>
        public int GetConsumedCount() => consumedCount;

        /// <summary>
        /// Gets the required consumption count to expire (power tier).
        /// </summary>
        public int GetRequiredConsumptions() => requiredConsumptions;

        public override void OnStart()
        {
            // Set required consumptions to the power tier
            requiredConsumptions = manager.GetPowerTier(HeartPowerType.MurmuringPaths);
            consumedCount = 0;
            hasExpired = false;

            // Find which node or edge the target position belongs to
            FindAffectedNodeOrEdge();

            // First generate the main path from target to heart - this determines all affected graph elements
            pathPositions = GeneratePathToHeart(targetPosition);

            // Identify ALL nodes and edges along the path (populates allAffectedNodeIndices/allAffectedEdgeIndices)
            var affectedPositions = GetAllPositionsAlongPath(pathPositions);

            // Generate paths from each affected position to the heart
            allPathsToHeart.Clear();
            foreach (var pos in affectedPositions)
            {
                var path = GeneratePathToHeart(pos);
                if (path.Count >= 2)
                {
                    allPathsToHeart.Add(path);
                }
            }

            // If no paths were generated, fall back to just the target position
            if (allPathsToHeart.Count == 0)
            {
                if (pathPositions.Count >= 2)
                {
                    allPathsToHeart.Add(pathPositions);
                }
            }

            // Store heart position for wave animation
            if (manager.MazeGrid != null)
            {
                heartPosition = manager.MazeGrid.HeartWorldPosition;
            }

            // Get ALL tile positions on affected nodes/edges for fog coverage
            PopulateAllAffectedTilePositions();

            // Find the furthest position from the heart for wave animation
            FindFurthestExtent();

            // Create power fog covering affected area
            CreatePowerFog();

            // Apply lure to all visitors currently on the affected node/edge
            ApplyLureToVisitorsOnAffectedArea();
        }

        /// <summary>
        /// Populates allAffectedTilePositions with every tile position on affected nodes/edges.
        /// This ensures the fog covers the FULL area, not just sampled path positions.
        /// For the triggering edge, only includes tiles BETWEEN the focal point and the heart.
        /// </summary>
        private void PopulateAllAffectedTilePositions()
        {
            allAffectedTilePositions.Clear();

            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
            {
                // Fallback to path positions
                allAffectedTilePositions.AddRange(pathPositions);
                return;
            }

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;
            Vector2 heartPos2D = new Vector2(heartPosition.x, heartPosition.y);
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);
            float targetDistFromHeart = Vector2.Distance(heartPos2D, targetPos2D);

            // Get ALL walkable tiles on affected nodes and edges (not just sampled positions)
            // We need full coverage for fog, not the sparse sampling used for lights
            foreach (var tile in mazeData.Tiles)
            {
                if (!tile.Walkable) continue;

                bool isOnAffectedNode = tile.NodeIndex >= 0 && allAffectedNodeIndices.Contains(tile.NodeIndex);
                bool isOnAffectedEdge = tile.EdgeIndex >= 0 && allAffectedEdgeIndices.Contains(tile.EdgeIndex);

                if (isOnAffectedNode || isOnAffectedEdge)
                {
                    // For tiles on the TRIGGERING edge, only include those closer to heart than the focal point
                    if (affectedEdgeIndex >= 0 && tile.EdgeIndex == affectedEdgeIndex)
                    {
                        float tileDistFromHeart = Vector2.Distance(heartPos2D, tile.Position);
                        if (tileDistFromHeart > targetDistFromHeart + 0.5f) // Small tolerance
                        {
                            // This tile is beyond the focal point (away from heart), skip it
                            continue;
                        }
                    }

                    allAffectedTilePositions.Add(new Vector3(tile.Position.x, tile.Position.y, targetPosition.z));
                }
            }


            // If no positions found, fall back to path positions
            if (allAffectedTilePositions.Count == 0)
            {
                allAffectedTilePositions.AddRange(pathPositions);
            }
        }

        public override void OnEnd()
        {
            // Remove all visual elements
            if (visualContainer != null)
            {
                Object.Destroy(visualContainer);
                visualContainer = null;
            }

            // Clean up fog resources
            if (fogMaterial != null)
            {
                Object.Destroy(fogMaterial);
                fogMaterial = null;
            }
            if (pathMaskTexture != null)
            {
                Object.Destroy(pathMaskTexture);
                pathMaskTexture = null;
            }
            fogQuad = null;

            // Clear Lured state from all affected visitors
            foreach (var visitor in affectedVisitors)
            {
                if (visitor != null && visitor.State == VisitorControllerBase.VisitorState.Lured)
                {
                    visitor.SetLured(false);
                }
            }

            affectedVisitors.Clear();
            pathPositions.Clear();
            allPathsToHeart.Clear();
            allAffectedNodeIndices.Clear();
            allAffectedEdgeIndices.Clear();
            allAffectedTilePositions.Clear();
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            animationTime += deltaTime;

            // Update fog wave animation
            UpdatePowerFog(deltaTime);

            // Check for new visitors entering the affected area
            CheckForNewVisitorsOnAffectedArea();

            // Clean up destroyed visitors from tracking set
            affectedVisitors.RemoveWhere(v => v == null);
        }

        /// <summary>
        /// Finds the furthest position from the heart among all affected positions.
        /// Used for wave animation starting point.
        /// </summary>
        private void FindFurthestExtent()
        {
            furthestPosition = targetPosition;
            float maxDist = 0f;

            // Check ALL tile positions on affected nodes/edges
            foreach (var pos in allAffectedTilePositions)
            {
                float dist = Vector3.Distance(pos, heartPosition);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    furthestPosition = pos;
                }
            }

        }

        /// <summary>
        /// Creates power fog covering all affected graph elements.
        /// Fog is color-coded to the power and shows wave animations.
        /// </summary>
        private void CreatePowerFog()
        {
            if (allPathsToHeart.Count == 0)
                return;

            visualContainer = new GameObject($"MurmuringPathsFog_{instanceSourceId}");

            // Calculate bounds covering all affected positions
            CalculateFogBounds();

            // Generate path mask texture
            GeneratePathMaskTexture();

            // Create fog material
            CreateFogMaterial();

            // Create fog quad
            CreateFogQuad();
        }

        /// <summary>
        /// Calculates bounds that cover all affected graph elements.
        /// </summary>
        private void CalculateFogBounds()
        {
            fogBounds = new Bounds(targetPosition, Vector3.zero);

            // Include heart position
            fogBounds.Encapsulate(heartPosition);

            // Include ALL tile positions on affected nodes/edges
            foreach (var pos in allAffectedTilePositions)
            {
                fogBounds.Encapsulate(pos);
            }

            // Add padding
            fogBounds.Expand(FOG_PADDING * 2f);

        }

        /// <summary>
        /// Generates a mask texture showing only the affected path/node areas.
        /// </summary>
        private void GeneratePathMaskTexture()
        {
            int texWidth = Mathf.CeilToInt(fogBounds.size.x * MASK_PIXELS_PER_UNIT);
            int texHeight = Mathf.CeilToInt(fogBounds.size.y * MASK_PIXELS_PER_UNIT);

            // Clamp to reasonable size
            texWidth = Mathf.Clamp(texWidth, 32, 512);
            texHeight = Mathf.Clamp(texHeight, 32, 512);

            pathMaskTexture = new Texture2D(texWidth, texHeight, TextureFormat.R8, false);
            pathMaskTexture.filterMode = FilterMode.Bilinear;
            pathMaskTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texWidth * texHeight];

            // Initialize all pixels to transparent (no fog)
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.black;
            }

            // World to texture scale
            float worldToTexX = texWidth / fogBounds.size.x;
            float worldToTexY = texHeight / fogBounds.size.y;

            // Paint all path positions with circular gradients
            float tileRadius = 1.5f; // Radius of effect around each path point
            int radiusPixels = Mathf.CeilToInt(tileRadius * Mathf.Max(worldToTexX, worldToTexY));

            // Helper to paint a position onto the mask
            void PaintPosition(Vector3 pos)
            {
                float worldX = pos.x - fogBounds.min.x;
                float worldY = pos.y - fogBounds.min.y;

                int centerTexX = Mathf.RoundToInt(worldX * worldToTexX);
                int centerTexY = Mathf.RoundToInt(worldY * worldToTexY);

                // Paint circular gradient
                for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                {
                    for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                    {
                        int px = centerTexX + dx;
                        int py = centerTexY + dy;

                        if (px < 0 || px >= texWidth || py < 0 || py >= texHeight)
                            continue;

                        float distWorld = Mathf.Sqrt(dx * dx / (worldToTexX * worldToTexX) + dy * dy / (worldToTexY * worldToTexY));

                        // Calculate fog amount (1 = full fog, 0 = no fog)
                        float fogAmount;
                        if (distWorld <= tileRadius * 0.5f)
                        {
                            fogAmount = 1f;
                        }
                        else if (distWorld <= tileRadius)
                        {
                            float t = (distWorld - tileRadius * 0.5f) / (tileRadius * 0.5f);
                            fogAmount = Mathf.SmoothStep(1f, 0f, t);
                        }
                        else
                        {
                            fogAmount = 0f;
                        }

                        // Take maximum (most visible) value
                        int pixelIndex = py * texWidth + px;
                        pixels[pixelIndex].r = Mathf.Max(pixels[pixelIndex].r, fogAmount);
                    }
                }
            }

            // Paint ALL tile positions on affected nodes/edges
            foreach (var pos in allAffectedTilePositions)
            {
                PaintPosition(pos);
            }

            pathMaskTexture.SetPixels(pixels);
            pathMaskTexture.Apply();
        }

        /// <summary>
        /// Creates the fog material with the PowerFog shader.
        /// </summary>
        private void CreateFogMaterial()
        {
            var shader = Shader.Find("Custom/PowerFog");
            if (shader == null)
            {
                Debug.LogWarning("PowerFog shader not found, falling back to default");
                shader = Shader.Find("Sprites/Default");
            }

            fogMaterial = new Material(shader);
            UpdateFogMaterialProperties();
        }

        /// <summary>
        /// Updates the fog material properties.
        /// </summary>
        private void UpdateFogMaterialProperties()
        {
            if (fogMaterial == null) return;

            // Colors
            fogMaterial.SetColor("_FogColor", PowerFogColor);
            fogMaterial.SetColor("_FogColorDark", PowerFogColorDark);
            fogMaterial.SetColor("_GlowColor", PowerGlowColor);

            // Path mask
            if (pathMaskTexture != null)
            {
                fogMaterial.SetTexture("_PathMask", pathMaskTexture);
            }

            // Wave animation
            fogMaterial.SetFloat("_WaveProgress", waveProgress);
            fogMaterial.SetVector("_HeartPosition", new Vector4(heartPosition.x, heartPosition.y, 0, 0));
            fogMaterial.SetVector("_FurthestPosition", new Vector4(furthestPosition.x, furthestPosition.y, 0, 0));
        }

        /// <summary>
        /// Creates the fog quad covering the affected area.
        /// </summary>
        private void CreateFogQuad()
        {
            fogQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fogQuad.name = "PowerFogQuad";
            fogQuad.transform.SetParent(visualContainer.transform);

            // Position and scale
            fogQuad.transform.position = new Vector3(fogBounds.center.x, fogBounds.center.y, FOG_Z_POSITION);
            fogQuad.transform.localScale = new Vector3(fogBounds.size.x, fogBounds.size.y, 1f);
            fogQuad.transform.rotation = Quaternion.identity;

            // Remove collider
            var collider = fogQuad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            // Apply material
            var renderer = fogQuad.GetComponent<Renderer>();
            renderer.material = fogMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Updates the power fog animation (wave sweep from furthest to heart).
        /// </summary>
        private void UpdatePowerFog(float deltaTime)
        {
            if (fogMaterial == null) return;

            // Update wave animation
            if (waveActive)
            {
                waveProgress += WAVE_SPEED * deltaTime;

                if (waveProgress >= 1f)
                {
                    // Wave reached heart, start pause
                    waveProgress = 1f;
                    waveActive = false;
                    wavePauseTimer = WAVE_CYCLE_PAUSE;
                }
            }
            else
            {
                // Pausing between waves
                wavePauseTimer -= deltaTime;
                if (wavePauseTimer <= 0f)
                {
                    // Start new wave from furthest extent
                    waveProgress = 0f;
                    waveActive = true;
                }
            }

            // Update shader
            fogMaterial.SetFloat("_WaveProgress", waveProgress);
        }

        /// <summary>
        /// Gets a world position along a path at a normalized distance (0-1).
        /// </summary>
        private Vector3 GetPositionAlongPath(List<Vector3> path, float normalizedDistance)
        {
            if (path.Count < 2)
                return path.Count > 0 ? path[0] : Vector3.zero;

            float totalLength = CalculatePathLength(path);
            float targetDistance = normalizedDistance * totalLength;
            float currentDistance = 0f;

            for (int i = 1; i < path.Count; i++)
            {
                float segmentLength = Vector3.Distance(path[i - 1], path[i]);

                if (currentDistance + segmentLength >= targetDistance)
                {
                    float t = (targetDistance - currentDistance) / segmentLength;
                    return Vector3.Lerp(path[i - 1], path[i], t);
                }

                currentDistance += segmentLength;
            }

            return path[path.Count - 1];
        }

        /// <summary>
        /// Calculates the total length of a path.
        /// </summary>
        private float CalculatePathLength(List<Vector3> path)
        {
            float totalLength = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                totalLength += Vector3.Distance(path[i - 1], path[i]);
            }
            return totalLength;
        }

        /// <summary>
        /// Gets all walkable positions along the ENTIRE path to the heart.
        /// Uses the pre-computed HeartPower1 position index from WorldSpaceMazeData.
        /// Also populates allAffectedNodeIndices and allAffectedEdgeIndices for visitor detection.
        /// When triggered on an edge, only includes positions UP TO the focal point (not past it).
        /// </summary>
        private List<Vector3> GetAllPositionsAlongPath(List<Vector3> mainPath)
        {
            var positions = new List<Vector3>();

            // Clear and prepare to populate affected indices
            allAffectedNodeIndices.Clear();
            allAffectedEdgeIndices.Clear();

            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null || mainPath.Count < 2)
            {
                positions.Add(targetPosition);
                return positions;
            }

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;
            Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;
            Vector2 heartPos2D = new Vector2(heartPos.x, heartPos.y);
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);

            // Ensure the triggering node/edge is always included
            // (The path might not sample it if activation point is mid-edge)
            if (affectedNodeIndex >= 0)
                allAffectedNodeIndices.Add(affectedNodeIndex);
            if (affectedEdgeIndex >= 0)
                allAffectedEdgeIndices.Add(affectedEdgeIndex);

            // Sample points along the path to identify all graph elements the path passes through
            float pathLength = CalculatePathLength(mainPath);
            int numSamples = Mathf.Max(20, (int)(pathLength / 2f)); // Sample every ~2 units

            for (int i = 0; i <= numSamples; i++)
            {
                float t = i / (float)numSamples;
                Vector3 samplePos = GetPositionAlongPath(mainPath, t);
                Vector2 samplePos2D = new Vector2(samplePos.x, samplePos.y);

                // Find the tile at this sample point
                var tile = FindNearestWalkableTile(mazeData, samplePos2D);
                if (tile != null)
                {
                    if (tile.NodeIndex >= 0)
                        allAffectedNodeIndices.Add(tile.NodeIndex);
                    if (tile.EdgeIndex >= 0)
                        allAffectedEdgeIndices.Add(tile.EdgeIndex);
                }
            }


            // Return target position - actual positions are gathered in PopulateAllAffectedTilePositions
            positions.Add(targetPosition);
            return positions;
        }

        /// <summary>
        /// Finds the node or edge at the target position.
        /// </summary>
        private void FindAffectedNodeOrEdge()
        {
            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
                return;

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;
            Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);

            // Find nearest walkable tile to determine node/edge
            float searchRadius = 3f;
            var nearbyTiles = mazeData.GetTilesNear(targetPos2D, searchRadius);

            float minDist = float.MaxValue;
            ForestMaze.WorldSpaceTile nearestTile = null;

            foreach (var tile in nearbyTiles)
            {
                if (!tile.Walkable) continue;

                float dist = Vector2.Distance(targetPos2D, tile.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestTile = tile;
                }
            }

            if (nearestTile != null)
            {
                affectedNodeIndex = nearestTile.NodeIndex;
                affectedEdgeIndex = nearestTile.EdgeIndex;
            }
        }

        /// <summary>
        /// Generates a path from start position to the heart using visitor pathfinding.
        /// </summary>
        private List<Vector3> GeneratePathToHeart(Vector3 startPos)
        {
            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
                return new List<Vector3>();

            Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;

            Vector2 startPos2D = new Vector2(startPos.x, startPos.y);
            Vector2 heartPos2D = new Vector2(heartPos.x, heartPos.y);

            var startTile = FindNearestWalkableTile(mazeData, startPos2D);
            var heartTile = FindNearestWalkableTile(mazeData, heartPos2D);

            if (startTile == null || heartTile == null)
            {
                return GenerateStraightLinePath(startPos, heartPos);
            }

            var tilePath = FindTilePath(mazeData, startTile, heartTile);
            if (tilePath == null || tilePath.Count == 0)
            {
                return GenerateStraightLinePath(startPos, heartPos);
            }

            var result = new List<Vector3>();
            foreach (var tile in tilePath)
            {
                result.Add(new Vector3(tile.Position.x, tile.Position.y, startPos.z));
            }

            return result;
        }

        private List<Vector3> GenerateStraightLinePath(Vector3 start, Vector3 end)
        {
            var positions = new List<Vector3>();
            int numPoints = 20;
            for (int i = 0; i <= numPoints; i++)
            {
                float t = i / (float)numPoints;
                positions.Add(Vector3.Lerp(start, end, t));
            }
            return positions;
        }

        private ForestMaze.WorldSpaceTile FindNearestWalkableTile(ForestMaze.WorldSpaceMazeData mazeData, Vector2 position)
        {
            float searchRadius = 5f;
            float minDist = float.MaxValue;
            ForestMaze.WorldSpaceTile nearest = null;

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
            return nearest;
        }

        private List<ForestMaze.WorldSpaceTile> FindTilePath(ForestMaze.WorldSpaceMazeData mazeData,
            ForestMaze.WorldSpaceTile start, ForestMaze.WorldSpaceTile end)
        {
            var visited = new HashSet<ForestMaze.WorldSpaceTile>();
            var queue = new Queue<ForestMaze.WorldSpaceTile>();
            var cameFrom = new Dictionary<ForestMaze.WorldSpaceTile, ForestMaze.WorldSpaceTile>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current == end)
                {
                    var path = new List<ForestMaze.WorldSpaceTile>();
                    var node = current;
                    while (node != null)
                    {
                        path.Add(node);
                        cameFrom.TryGetValue(node, out node);
                    }
                    path.Reverse();
                    return path;
                }

                float neighborRadius = 2f;
                var neighbors = mazeData.GetTilesNear(current.Position, neighborRadius);

                foreach (var neighbor in neighbors)
                {
                    if (!neighbor.Walkable || visited.Contains(neighbor))
                        continue;

                    if (!mazeData.AreTilesConnected(current, neighbor))
                        continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a visitor is on ANY affected node or edge along the path to the heart.
        /// </summary>
        private bool IsVisitorOnAffectedArea(VisitorControllerBase visitor)
        {
            if (visitor == null || manager.MazeGrid?.WorldSpaceMazeData == null)
                return false;

            // If no affected areas were identified, fall back to radius check
            if (allAffectedNodeIndices.Count == 0 && allAffectedEdgeIndices.Count == 0)
            {
                float radius = definition.radius > 0 ? definition.radius : 3f;
                return Vector3.Distance(visitor.transform.position, targetPosition) <= radius;
            }

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;
            Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);

            var nearestTile = FindNearestWalkableTile(mazeData, visitorPos2D);
            if (nearestTile == null)
                return false;

            // Check if visitor is on ANY affected node along the path
            if (nearestTile.NodeIndex >= 0 && allAffectedNodeIndices.Contains(nearestTile.NodeIndex))
                return true;

            // Check if visitor is on ANY affected edge along the path
            if (nearestTile.EdgeIndex >= 0 && allAffectedEdgeIndices.Contains(nearestTile.EdgeIndex))
                return true;

            return false;
        }

        /// <summary>
        /// Applies lure effect to all visitors currently on the affected area.
        /// </summary>
        private void ApplyLureToVisitorsOnAffectedArea()
        {
            var activeVisitors = VisitorRegistry.All;
            if (activeVisitors == null)
            {
                return;
            }


            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Consumed)
                    continue;

                bool isOnArea = IsVisitorOnAffectedArea(visitor);

                if (isOnArea)
                {
                    LureVisitorToHeart(visitor);
                }
            }
        }

        /// <summary>
        /// Checks for new visitors entering the affected area and lures them.
        /// Called every frame to catch visitors who walk into the affected zone.
        /// </summary>
        private void CheckForNewVisitorsOnAffectedArea()
        {
            var activeVisitors = VisitorRegistry.All;
            if (activeVisitors == null) return;

            foreach (var visitor in activeVisitors)
            {
                if (visitor == null)
                    continue;

                // Skip visitors in terminal or non-lurable states
                if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                    continue;

                // Skip visitors already affected by this power instance
                if (affectedVisitors.Contains(visitor))
                    continue;

                // Check if visitor is on the affected area
                if (IsVisitorOnAffectedArea(visitor))
                {
                    LureVisitorToHeart(visitor);
                }
            }
        }

        /// <summary>
        /// Lures a visitor toward the heart.
        /// Uses the visitor's own pathfinding to generate a path from their current position to the heart.
        /// This ensures the path is compatible with the visitor's movement system.
        /// </summary>
        private void LureVisitorToHeart(VisitorControllerBase visitor)
        {
            if (visitor == null) return;


            // Set lured state first
            visitor.SetLured(true);

            // Use the visitor's own pathfinding to build a path to the heart
            // This ensures compatibility with the visitor's spline-based movement system
            if (manager.MazeGrid != null)
            {
                Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;

                // Use visitor's BuildWorldPath for consistent pathfinding
                var visitorPath = visitor.BuildWorldPath(visitor.transform.position, heartPos);
                if (visitorPath != null && visitorPath.Count >= 2)
                {
                    visitor.SetPathDirectly(visitorPath);
                }
                else
                {
                    // Fallback: try the effect's internal pathfinding
                    var effectPath = GeneratePathToHeart(visitor.transform.position);
                    if (effectPath.Count >= 2)
                    {
                        visitor.SetPathDirectly(effectPath);
                    }
                    else if (pathPositions.Count > 0)
                    {
                        // Last resort: use activation path
                        visitor.SetPathDirectly(new List<Vector3>(pathPositions));
                    }
                    else
                    {
                    }
                }
            }
            else
            {
            }

            affectedVisitors.Add(visitor);
        }
    }

    #endregion

    /*
    #region Dream Snare

    /// <summary>
    /// AoE that Mesmerizes visitors, then pushes them into Lost state with Heart bias.
    /// Uses world-space radius checks instead of grid iteration.
    /// </summary>
    public class DreamSnareEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> affectedVisitors = new HashSet<VisitorControllerBase>();
        private HashSet<Vector3> thornPositions = new HashSet<Vector3>();
        private Vector3 centerWorldPos;
        private const string ModifierSourceId = "DreamSnare";

        public DreamSnareEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            centerWorldPos = targetPosition;
            float radius = definition.radius > 0 ? definition.radius : 3f;

            // Add world-space tile visual at center
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(centerWorldPos, HeartPowerType.DreamSnare, 1.0f, definition.duration);
            }

            // Find all visitors in world-space AoE and apply Mesmerized
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in visitors)
            {
                Vector3 visitorPos = visitor.transform.position;
                float distance = Vector3.Distance(targetPosition, visitorPos);

                if (distance <= radius)
                {
                    float mesmerizeDuration = definition.param1 > 0 ? definition.param1 : 4f;
                    visitor.SetMesmerized(mesmerizeDuration);
                    affectedVisitors.Add(visitor);

                    if (definition.tier >= 3)
                    {
                        // Mark for harvest
                    }
                }
            }

            // Tier I: Create lingering thorn positions at edge
            if (definition.tier >= 1 && definition.flag1)
            {
                CreateLingeringThorns(radius);
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DreamSnare);
            }

            thornPositions.Clear();
            affectedVisitors.Clear();
        }

        private void CreateLingeringThorns(float radius)
        {
            // Create thorn positions at edge of AoE (world-space)
            int numThorns = 8;
            for (int i = 0; i < numThorns; i++)
            {
                float angle = (i / (float)numThorns) * Mathf.PI * 2f;
                Vector3 thornPos = centerWorldPos + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
                thornPositions.Add(thornPos);
            }
        }

        public bool IsNearThorn(Vector3 worldPos)
        {
            float thornRadius = 0.5f;
            foreach (var thornPos in thornPositions)
            {
                if (Vector3.Distance(worldPos, thornPos) <= thornRadius)
                {
                    return true;
                }
            }
            return false;
        }

        public void OnVisitorStepOnThornTile(VisitorControllerBase visitor)
        {
            if (!IsNearThorn(visitor.transform.position))
            {
                return;
            }

            float frightenedDuration = definition.param2 > 0 ? definition.param2 : 2f;
            visitor.SetFrightened(frightenedDuration);
        }
    }

    #endregion

    #region Feastward Panic

    /// <summary>
    /// Releases a wave of fear that makes everywhere but the Heart feel deadly.
    /// Uses world-space distance checks instead of grid iteration.
    /// </summary>
    public class FeastwardPanicEffect : ActivePowerEffect
    {
        private HashSet<VisitorControllerBase> frightenedVisitors = new HashSet<VisitorControllerBase>();
        private const string ModifierSourceId = "FeastwardPanic";
        private Vector3 heartWorldPos;

        public FeastwardPanicEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            heartWorldPos = manager.MazeGrid.HeartWorldPosition;

            bool selectiveMode = definition.tier >= 1 && definition.flag1;

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in visitors)
            {
                if (visitor.State == VisitorControllerBase.VisitorState.Fascinated ||
                    visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                bool shouldAffect = true;

                if (selectiveMode)
                {
                    shouldAffect = IsInCone(visitor.transform.position);
                }

                if (shouldAffect)
                {
                    float frightenedDuration = definition.duration > 0 ? definition.duration : 5f;
                    visitor.SetFrightened(frightenedDuration);
                    frightenedVisitors.Add(visitor);
                }
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (definition.tier >= 2 && definition.flag2)
            {
                CheckLastRefuge();
            }
        }

        public override void OnEnd()
        {
            frightenedVisitors.Clear();

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.FeastwardPanic);
            }
        }

        private bool IsInCone(Vector3 visitorWorldPos)
        {
            Vector3 directionToTarget = (targetPosition - heartWorldPos).normalized;
            Vector3 directionToVisitor = (visitorWorldPos - heartWorldPos).normalized;

            float angle = Vector3.Angle(directionToTarget, directionToVisitor);
            float coneAngle = definition.param1 > 0 ? definition.param1 : 60f;

            return angle <= coneAngle / 2f;
        }

        private void CheckLastRefuge()
        {
            // Placeholder for visitor path analysis
        }

        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !frightenedVisitors.Contains(visitor))
            {
                return;
            }

            float extensionTime = definition.param3 > 0 ? definition.param3 : 1f;
            elapsedTime -= extensionTime;
        }
    }

    #endregion

    #region Covenant with the Wisps

    /// <summary>
    /// Wisps temporarily obey you, prioritizing marked victims and Heart-preferred routes.
    /// Uses world-space positioning.
    /// </summary>
    public class CovenantWithWispsEffect : ActivePowerEffect
    {
        private List<WillowTheWisp> affectedWisps = new List<WillowTheWisp>();

        public CovenantWithWispsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            affectedWisps.AddRange(Object.FindObjectsByType<WillowTheWisp>(FindObjectsSortMode.None));

            // Add world-space tile visual at beacon position
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(targetPosition, HeartPowerType.CovenantWithWisps, 1.0f, definition.duration);
            }

            foreach (var wisp in affectedWisps)
            {
                if (definition.tier >= 1)
                {
                    // Enable twin flames
                }

                if (definition.tier >= 2 && definition.flag1)
                {
                    PlaceBeacon(wisp);
                }
            }
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.CovenantWithWisps);
            }

            affectedWisps.Clear();
        }

        private void PlaceBeacon(WillowTheWisp wisp)
        {
            // Set wisp to patrol around target position
        }

        public void OnWispDeliverVisitor(WillowTheWisp wisp, VisitorControllerBase visitor)
        {
            if (definition.tier < 3 || !affectedWisps.Contains(wisp))
            {
                return;
            }

            int bonusEssence = definition.intParam1 > 0 ? definition.intParam1 : 2;
            manager.AddEssence(bonusEssence);
        }
    }

    #endregion

    #region Puka's Bargain

    /// <summary>
    /// Bribes a Puka: less random drowning, more helpful teleportation near Heart.
    /// Uses world-space positioning.
    /// </summary>
    public class PukasBargainEffect : ActivePowerEffect
    {
        private PukaHazard targetPuka;
        private HashSet<Vector3> pactPoolPositions = new HashSet<Vector3>();
        private bool undertowUsed = false;

        public PukasBargainEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
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

            if (definition.tier >= 1)
            {
                IdentifyPactPools();
            }
        }

        public override void OnEnd()
        {
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.PukasBargain);
            }

            pactPoolPositions.Clear();
        }

        private void IdentifyPactPools()
        {
            // Simplified: just mark positions near the heart
            Vector3 heartPos = manager.MazeGrid.HeartWorldPosition;
            float searchRadius = definition.intParam1 > 0 ? definition.intParam1 : 15f;

            // Add tile visual at heart area
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(heartPos, HeartPowerType.PukasBargain, 1.0f, definition.duration);
            }
        }

        public Vector3? GetPreferredTeleportTarget()
        {
            if (definition.tier >= 3 && !undertowUsed && Random.value < 0.2f)
            {
                undertowUsed = true;
                return manager.MazeGrid.HeartWorldPosition;
            }

            if (pactPoolPositions.Count > 0)
            {
                int randomIndex = Random.Range(0, pactPoolPositions.Count);
                return pactPoolPositions.ElementAt(randomIndex);
            }

            return null;
        }

        public float GetKillChanceModifier()
        {
            return definition.param1 > 0 ? definition.param1 : 0.5f;
        }

        public void OnPukaKillVisitor(Vector3 killWorldPos)
        {
            if (definition.tier < 2)
            {
                return;
            }

            float aoeRadius = definition.param2 > 0 ? definition.param2 : 3f;

            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var visitor in visitors)
            {
                float dist = Vector3.Distance(visitor.transform.position, killWorldPos);
                if (dist <= aoeRadius)
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
    /// Uses world-space positioning.
    /// </summary>
    public class RingOfInvitationsEffect : ActivePowerEffect
    {
        private List<FairyRing> affectedRings = new List<FairyRing>();
        private HashSet<VisitorControllerBase> entrancedVisitors = new HashSet<VisitorControllerBase>();

        public RingOfInvitationsEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            affectedRings.AddRange(Object.FindObjectsByType<FairyRing>(FindObjectsSortMode.None));

            foreach (var ring in affectedRings)
            {
                // Add world-space tile visual at ring position
                if (manager.TileVisualizer != null)
                {
                    manager.TileVisualizer.AddTileEffectAtWorldPos(ring.transform.position, HeartPowerType.RingOfInvitations, 1.0f, definition.duration);
                }

                if (definition.tier >= 1)
                {
                    SpawnIllusoryRings(ring);
                }
            }
        }

        public override void OnEnd()
        {
            if (definition.tier >= 3)
            {
                ApplyClosingDance();
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.RingOfInvitations);
            }

            affectedRings.Clear();
            entrancedVisitors.Clear();
        }

        private void SpawnIllusoryRings(FairyRing sourceRing)
        {
            // Spawn temporary ring colliders
        }

        private void ApplyClosingDance()
        {
            var visitors = Object.FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var ring in affectedRings)
            {
                foreach (var visitor in visitors)
                {
                    float dist = Vector3.Distance(visitor.transform.position, ring.transform.position);
                    float ringRadius = 1f;

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

            if (definition.tier >= 2)
            {
                // Circle Remembered
            }
        }
    }

    #endregion
    */

    #region Heartward Grasp

    /// <summary>
    /// Pulls a visitor through a wall toward the Heart.
    /// Uses world-space positioning and simplified animation.
    /// </summary>
    public class HeartwardGraspEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            InitialPause,
            PullToActivation,
            Repositioning,
            PushToDestination,
            FinalPause,
            Complete
        }

        private GameObject graspVisual;
        private VisitorControllerBase targetVisitor;
        private Vector3 activationWorldPos;
        private Vector3 visitorStartWorldPos;
        private Vector3 pullDestinationWorldPos;
        private const string ModifierSourceId = "HeartwardGrasp";

        private AnimationPhase currentPhase = AnimationPhase.InitialPause;
        private float phaseStartTime = 0f;
        private Vector3 lerpStartPosition;
        private Vector3 lerpEndPosition;
        private bool visitorMovementStopped = false;

        private Vector3 graspBasePosition;
        private Vector3 graspAnimationDirection;

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            activationWorldPos = targetPosition;
            Vector3 heartWorldPos = manager.MazeGrid.HeartWorldPosition;

            // Find nearest visitor within range of activation position
            float pullRange = definition.param1 > 0 ? definition.param1 : 3f;
            targetVisitor = FindNearestVisitor(activationWorldPos, pullRange);

            if (targetVisitor == null)
            {
                return;
            }

            visitorStartWorldPos = targetVisitor.transform.position;

            // Find destination: position toward heart from activation point
            pullDestinationWorldPos = FindDestinationTowardHeart(activationWorldPos, heartWorldPos);

            // Spawn grasp prefab
            SpawnGraspPrefab(activationWorldPos, visitorStartWorldPos);

            // Stop visitor movement
            StopVisitor();

            currentPhase = AnimationPhase.InitialPause;
            phaseStartTime = elapsedTime;
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
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.PullToActivation;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = targetVisitor.transform.position;
                        lerpEndPosition = activationWorldPos;
                    }
                    break;

                case AnimationPhase.PullToActivation:
                    float pullT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pullT);

                    if (phaseElapsed >= 0.25f)
                    {
                        currentPhase = AnimationPhase.Repositioning;
                        targetVisitor.transform.position = activationWorldPos;

                        UpdateGraspDirection(activationWorldPos, pullDestinationWorldPos);

                        currentPhase = AnimationPhase.PushToDestination;
                        phaseStartTime = elapsedTime;
                        lerpStartPosition = activationWorldPos;
                        lerpEndPosition = pullDestinationWorldPos;
                    }
                    break;

                case AnimationPhase.PushToDestination:
                    float pushT = Mathf.Clamp01(phaseElapsed / 0.25f);
                    targetVisitor.transform.position = Vector3.Lerp(lerpStartPosition, lerpEndPosition, pushT);

                    if (phaseElapsed >= 0.25f)
                    {
                        currentPhase = AnimationPhase.FinalPause;
                        phaseStartTime = elapsedTime;
                        targetVisitor.RecalculatePath();
                        ApplyTierEffects();
                    }
                    break;

                case AnimationPhase.FinalPause:
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.Complete;
                        ResumeVisitor();

                        if (graspVisual != null)
                        {
                            Object.Destroy(graspVisual);
                            graspVisual = null;
                        }
                    }
                    break;
            }

            if (graspVisual != null)
            {
                UpdateGraspVisualAnimation();
            }
        }

        private void UpdateGraspVisualAnimation()
        {
            float animTime = elapsedTime;
            Vector3 offset = Vector3.zero;

            if (animTime < 1.0f)
            {
                if (animTime < 0.25f)
                {
                    float t = animTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (animTime < 0.75f)
                {
                    offset = graspAnimationDirection;
                }
                else
                {
                    float t = (1.0f - animTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }
            else if (animTime < 2.0f)
            {
                float secondHalfTime = animTime - 1.0f;

                if (secondHalfTime < 0.25f)
                {
                    float t = secondHalfTime / 0.25f;
                    offset = graspAnimationDirection * t;
                }
                else if (secondHalfTime < 0.75f)
                {
                    offset = graspAnimationDirection;
                }
                else
                {
                    float t = (1.0f - secondHalfTime) / 0.25f;
                    offset = graspAnimationDirection * t;
                }
            }

            graspVisual.transform.position = graspBasePosition + offset;
        }

        public override void OnEnd()
        {
            if (targetVisitor != null && visitorMovementStopped)
            {
                ResumeVisitor();
            }

            if (graspVisual != null)
            {
                Object.Destroy(graspVisual);
                graspVisual = null;
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartwardGrasp);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            activationWorldPos += worldOffset;
            visitorStartWorldPos += worldOffset;
            pullDestinationWorldPos += worldOffset;
            lerpStartPosition += worldOffset;
            lerpEndPosition += worldOffset;
            graspBasePosition += worldOffset;

            if (graspVisual != null)
            {
                graspVisual.transform.position += worldOffset;
            }
        }

        private void StopVisitor()
        {
            if (targetVisitor == null || visitorMovementStopped)
            {
                return;
            }

            targetVisitor.Stop();
            visitorMovementStopped = true;
        }

        private void ResumeVisitor()
        {
            if (targetVisitor == null || !visitorMovementStopped)
            {
                return;
            }

            targetVisitor.Resume();
            visitorMovementStopped = false;
        }

        private void SpawnGraspPrefab(Vector3 position, Vector3 pointToward)
        {
            GameObject graspPrefab = Resources.Load<GameObject>("Prefabs/Props/Grasp/grasp");

            if (graspPrefab == null)
            {
                return;
            }

            Vector3 graspPosition = position;
            graspPosition.z = 0f;

            Vector3 direction = pointToward - graspPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            else
            {
                direction.Normalize();
            }

            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

            graspVisual = Object.Instantiate(graspPrefab, graspPosition, rotation);
            graspVisual.name = "GraspEffect";

            graspBasePosition = graspPosition;
            graspAnimationDirection = direction;
        }

        private void UpdateGraspDirection(Vector3 from, Vector3 to)
        {
            if (graspVisual == null)
            {
                return;
            }

            Vector3 direction = to - from;
            direction.z = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);
                graspVisual.transform.rotation = rotation;
                graspAnimationDirection = direction;
            }
        }

        private Vector3 FindDestinationTowardHeart(Vector3 from, Vector3 heartPos)
        {
            Vector3 direction = (heartPos - from).normalized;
            float pullDistance = 3f;
            return from + direction * pullDistance;
        }

        private void ApplyTierEffects()
        {
            if (targetVisitor == null)
            {
                return;
            }

            if (definition.tier >= 3 && definition.flag1)
            {
                float mesmerizeDuration = definition.param3 > 0 ? definition.param3 : 3f;
                targetVisitor.SetMesmerized(mesmerizeDuration);
            }
        }

        private VisitorControllerBase FindNearestVisitor(Vector3 worldPos, float range)
        {
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

                float distance = Vector3.Distance(visitor.transform.position, worldPos);

                if (distance <= range && distance < minDistance)
                {
                    nearest = visitor;
                    minDistance = distance;
                }
            }

            return nearest;
        }
    }

    #endregion

    #region Devouring Maw

    /// <summary>
    /// Instantly consumes a visitor on the targeted position, granting essence.
    /// Uses world-space positioning.
    /// </summary>
    public class DevouringMawEffect : ActivePowerEffect
    {
        private enum AnimationPhase
        {
            Pause,
            SinkAndDevour,
            Complete
        }

        private GameObject devourVisual;
        private VisitorControllerBase consumedVisitor;
        private Vector3 targetWorldPos;
        private AnimationPhase currentPhase;
        private float phaseStartTime;
        private Vector3 visitorStartPosition;
        private bool hasConsumedVisitor;
        private Vector3 devourBasePosition;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            targetWorldPos = targetPosition;

            // Always spawn devour prefab at target position
            InstantiateDevourVisual(targetWorldPos);

            // Add tile visualizer effect
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(targetWorldPos, HeartPowerType.DevouringMaw, 1.0f, 2.0f);
            }

            // Find visitor near the targeted position
            float targetRadius = 1.0f;
            VisitorControllerBase targetVisitorFound = FindVisitorNearPosition(targetWorldPos, targetRadius);

            if (targetVisitorFound == null)
            {
                return;
            }

            consumedVisitor = targetVisitorFound;

            consumedVisitor.Stop();
            visitorStartPosition = consumedVisitor.transform.position;

            currentPhase = AnimationPhase.Pause;
            phaseStartTime = 0f;
            hasConsumedVisitor = false;

            // Tier I: Apply fear to nearby visitors
            if (definition.tier >= 1 && definition.flag1)
            {
                ApplyEchoingTerror(targetWorldPos);
            }

            // Tier II: Slow nearby visitors
            if (definition.tier >= 2 && definition.flag2)
            {
                ApplyDrainingEmbrace(targetWorldPos);
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (devourVisual != null)
            {
                UpdateDevourVisualAnimation();
            }

            if (devourVisual != null && elapsedTime >= 1.0f)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }

            if (consumedVisitor == null)
            {
                return;
            }

            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case AnimationPhase.Pause:
                    if (phaseElapsed >= 0.75f)
                    {
                        currentPhase = AnimationPhase.SinkAndDevour;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case AnimationPhase.SinkAndDevour:
                    float sinkDuration = 0.25f;
                    float sinkT = Mathf.Clamp01(phaseElapsed / sinkDuration);

                    Vector3 sinkPosition = visitorStartPosition;
                    sinkPosition.z = Mathf.Lerp(visitorStartPosition.z, visitorStartPosition.z + 1f, sinkT);
                    consumedVisitor.transform.position = sinkPosition;

                    if (sinkT >= 1f && !hasConsumedVisitor)
                    {
                        hasConsumedVisitor = true;
                        ConsumeVisitor(consumedVisitor);

                        if (definition.tier >= 3)
                        {
                            ApplySoulHarvest();
                        }

                        currentPhase = AnimationPhase.Complete;
                    }
                    break;

                case AnimationPhase.Complete:
                    break;
            }
        }

        public override void OnEnd()
        {
            if (devourVisual != null)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.DevouringMaw);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            targetWorldPos += worldOffset;
            visitorStartPosition += worldOffset;
            devourBasePosition += worldOffset;

            if (devourVisual != null)
            {
                devourVisual.transform.position += worldOffset;
            }
        }

        private VisitorControllerBase FindVisitorNearPosition(Vector3 worldPos, float radius)
        {
            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (visitor == null)
                {
                    continue;
                }

                if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                float distance = Vector3.Distance(visitor.transform.position, worldPos);
                if (distance <= radius)
                {
                    return visitor;
                }
            }

            return null;
        }

        private void InstantiateDevourVisual(Vector3 position)
        {
            GameObject devourPrefab = Resources.Load<GameObject>("Prefabs/Props/devour");

            if (devourPrefab == null)
            {
                return;
            }

            Vector3 worldPos = position;
            worldPos.z = 0.75f;

            devourVisual = Object.Instantiate(devourPrefab, worldPos, Quaternion.identity);
            devourBasePosition = worldPos;

            // Fix MawThroat mesh rendering - disable backface culling so both sides are visible
            // The throat mesh has inward-facing normals by design
            SetDoubleSidedRendering(devourVisual);
        }

        /// <summary>
        /// Sets all materials on a GameObject to render both front and back faces.
        /// Used for meshes with inward-facing normals like the MawThroat.
        /// </summary>
        private void SetDoubleSidedRendering(GameObject obj)
        {
            if (obj == null) return;

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    // Disable culling (render both sides)
                    mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

                    // For URP/glTF shaders, also try these property names
                    if (mat.HasProperty("_CullMode"))
                    {
                        mat.SetFloat("_CullMode", 0f); // 0 = Off
                    }
                    if (mat.HasProperty("_DoubleSidedEnable"))
                    {
                        mat.SetFloat("_DoubleSidedEnable", 1f);
                    }
                }
            }
        }

        private void UpdateDevourVisualAnimation()
        {
            if (devourVisual == null)
            {
                return;
            }

            float animTime = elapsedTime;
            Vector3 offset = Vector3.zero;

            if (animTime < 0.25f)
            {
                float t = animTime / 0.25f;
                offset.z = Mathf.Lerp(0f, -1f, t);
            }
            else if (animTime < 0.75f)
            {
                offset.z = -1f;
            }
            else if (animTime < 1.0f)
            {
                float t = (animTime - 0.75f) / 0.25f;
                offset.z = Mathf.Lerp(-1f, 0f, t);
            }
            else
            {
                offset.z = 0f;
            }

            devourVisual.transform.position = devourBasePosition + offset;
        }

        private void ConsumeVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            int essence = visitor.GetEssenceReward();

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(essence);
            }

            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            Object.Destroy(visitor.gameObject);
        }

        private void ApplyEchoingTerror(Vector3 centerWorldPos)
        {
            float fearRadius = definition.param1 > 0 ? definition.param1 : 3f;
            float fearDuration = definition.param2 > 0 ? definition.param2 : 3f;

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

                float distance = Vector3.Distance(visitor.transform.position, centerWorldPos);

                if (distance <= fearRadius)
                {
                    visitor.SetFrightened(fearDuration);
                }
            }
        }

        private void ApplyDrainingEmbrace(Vector3 centerWorldPos)
        {
            float slowRadius = definition.intParam1 > 0 ? definition.intParam1 : 3f;
            float slowDuration = definition.param3 > 0 ? definition.param3 : 4f;

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

                float distance = Vector3.Distance(visitor.transform.position, centerWorldPos);

                if (distance <= slowRadius)
                {
                    visitor.SetMesmerized(slowDuration);
                }
            }
        }

        private void ApplySoulHarvest()
        {
            int bonusEssence = definition.intParam2 > 0 ? definition.intParam2 : 3;

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(bonusEssence);
            }
        }
    }

    #endregion
}

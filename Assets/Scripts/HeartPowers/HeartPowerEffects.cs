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
    /// Creates two grasp zones (grabbing and pushing) at walls along the heart-to-focal-point ray.
    /// Grabbing HGZ captures visitors and pulls them into the wall.
    /// Pushing HGZ releases visitors near the heart with a daze effect.
    /// Toggle power: deactivates after firing its effect (tier-count captures).
    /// Animation is frame-based using the Animator component.
    /// </summary>
    public class HeartwardGraspEffect : ActivePowerEffect
    {
        // Grabbing HGZ states
        private enum GrabPhase
        {
            Idle,           // Animation paused, no translation
            Reaching,       // Model X directed at visitor, animation frames 0-20
            Grabbing,       // Animation frames 20-46, visitor stops at frame 46
            Pulling,        // Animation frames 46-62, visitor translates with grabbing mesh into wall
            Transporting    // 1 second duration, visitor relocated to pushing HGZ
        }

        // Pushing HGZ states
        private enum PushPhase
        {
            Idle,           // Animation paused, no translation
            Pushing,        // Model translates 1 unit toward heart, hand stays closed (frame 24)
            Releasing,      // Model paused, animation plays reverse 24→0, visitor becomes visible and dazed
            Withdrawing     // Model translates back to wall, holds frame 1
        }

        // Constants
        private const float GRASP_ZONE_RADIUS = 2f;
        private const int MIN_WALL_THICKNESS = 3;         // Minimum wall models required for valid wall intersection
        private const float TRANSPORT_DURATION = 1.0f;    // Duration of transport phase
        private const float HGZ_WALL_OFFSET = 0.5f;       // How far to offset HGZ into the wall (away from path)
        private const float MIN_EDGE_DISTANCE = 3.0f;     // Minimum distance from path/node edge (~4 wall tiles * 0.8 spacing)
        private const float ANIMATION_FPS = 60f;          // Animation framerate (24 frames / 0.417 seconds ≈ 60fps)
        private const float GRAB_ANIMATION_DURATION = 1.0f;  // Duration to play grab animation
        private const float PULL_DURATION = 1.0f;         // Duration of pull phase

        // Animation frame constants (animation is 24 frames total)
        private const int GRAB_ANIMATION_FRAMES = 24;     // Total frames in grasp animation
        private const int PUSH_REACH_END_FRAME = 0;       // Pushing: reach ends (reverse from 24 to 0)
        private const int PUSH_RELEASE_END_FRAME = 12;    // Pushing: release ends, visitor dazed
        private const int PUSH_WITHDRAW_END_FRAME = 24;   // Pushing: withdraw ends (animation end)

        // Grabbing HGZ
        private GameObject grabbingZoneObject;
        private SphereCollider grabbingCollider;
        private GameObject grabbingVisual;
        private Animator grabbingAnimator;
        private SphereCollider grabbingTouchCollider;  // "touch" collider on grasp model
        private Vector3 grabbingWallPos;
        private Vector3 grabbingWallNormal;
        private Vector3 grabbingStartPos;  // Initial position for reach translation
        private GrabPhase grabPhase = GrabPhase.Idle;
        private float grabPhaseStartTime = 0f;
        private int grabCurrentFrame = 0;
        private const float REACH_SPEED = 8f;  // Units per second for reach translation

        // Pushing HGZ
        private GameObject pushingZoneObject;
        private SphereCollider pushingCollider;
        private GameObject pushingVisual;
        private Animator pushingAnimator;
        private SphereCollider pushingTouchCollider;  // "touch" collider on pushing grasp model
        private Vector3 pushingWallPos;
        private Vector3 pushingWallNormal;
        private Vector3 pushingStartPos;   // Initial position for push translation (at wall)
        private Vector3 pushingTargetPos;  // Target position after pushing (1 unit toward heart)
        private PushPhase pushPhase = PushPhase.Idle;
        private float pushPhaseStartTime = 0f;
        private int pushCurrentFrame = 24;  // Starts at end for reverse play
        private const float PUSH_DISTANCE = 1.0f;     // Distance to push toward heart
        private const float PUSH_DURATION = 0.5f;     // Duration of push translation
        private const float WITHDRAW_DURATION = 0.5f; // Duration of withdraw translation

        // Visitor processing
        private Queue<VisitorControllerBase> pendingVisitors = new Queue<VisitorControllerBase>();
        private VisitorControllerBase currentVisitor;
        private Vector3 visitorGrabOffset;  // Offset from grabbing HGZ when grabbed
        private Vector3 visitorPushOffset;  // Transformed offset for pushing HGZ
        private Vector3 heartNodePosition;
        private bool visitorVisible = true;
        private Vector3 grabStartPosition;  // Position where model was when it grabbed visitor

        // Particle effects
        private ParticleSystem grabbingParticles;
        private ParticleSystem pushingParticles;

        // Affected wall tiles in grabbing zone
        private List<GameObject> affectedGrabbingWalls = new List<GameObject>();
        private Dictionary<GameObject, Vector3> originalWallPositions = new Dictionary<GameObject, Vector3>();

        // Debug visualization
        private GameObject debugRayColumn;
        private GameObject debugGrabbingHitSphere;
        private GameObject debugPushingHitSphere;
        private GameObject debugGrabbingZoneCylinder;
        private GameObject debugPushingZoneCylinder;

        // Toggle power expiration
        private int capturedCount = 0;
        private int requiredCaptures = 1;
        private bool hasExpired = false;

        // Particle colors
        private static readonly Color LeafGreen = new Color(0.3f, 0.7f, 0.2f, 1f);
        private static readonly Color BarkBrown = new Color(0.6f, 0.4f, 0.15f, 1f);

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override bool IsExpired => hasExpired;

        public override void OnStart()
        {
            requiredCaptures = manager.GetPowerTier(HeartPowerType.HeartwardGrasp);
            capturedCount = 0;
            hasExpired = false;

            Debug.Log($"[HeartwardGrasp] OnStart - Click position: {targetPosition}, Tier: {requiredCaptures}");

            // Get heart node position
            if (manager.MazeGrid != null)
            {
                heartNodePosition = manager.MazeGrid.HeartWorldPosition;
                Debug.Log($"[HeartwardGrasp] Heart position: {heartNodePosition}");
            }

            // Find wall positions for both HGZs along the heart-to-focal ray
            FindWallPositions(targetPosition);

            Debug.Log($"[HeartwardGrasp] Grabbing wall: {grabbingWallPos}, Pushing wall: {pushingWallPos}");

            // Create both HGZs
            CreateGrabbingHGZ();
            CreatePushingHGZ();

            // Create particle effects for both zones
            CreateParticleSystem(grabbingZoneObject, ref grabbingParticles);
            CreateParticleSystem(pushingZoneObject, ref pushingParticles);

            // Find and affect wall tiles in grabbing zone
            FindAffectedGrabbingWalls();

            // Create debug ray visualization
            CreateDebugRayColumn(targetPosition);

            Debug.Log($"[HeartwardGrasp] Initialization complete. Zone radius: {GRASP_ZONE_RADIUS}, affected walls: {affectedGrabbingWalls.Count}");
        }

        /// <summary>
        /// Finds wall positions for both grabbing (near focal point) and pushing (near heart) HGZs.
        /// Uses Physics.RaycastAll to find wall colliders along the heart-to-focal ray.
        /// First hit = pushing HGZ, last hit before focal = grabbing HGZ.
        /// </summary>
        private void FindWallPositions(Vector3 focalPos)
        {
            Vector3 rayOrigin = new Vector3(heartNodePosition.x, heartNodePosition.y, 0f);
            Vector3 focalPos3D = new Vector3(focalPos.x, focalPos.y, 0f);
            Vector3 rayDirection = (focalPos3D - rayOrigin).normalized;
            float distToFocal = Vector3.Distance(rayOrigin, focalPos3D);

            // Raycast only to the focal point - no walls past focal should be hit
            // Use QueryTriggerInteraction.Collide to hit trigger colliders
            RaycastHit[] allHits = Physics.RaycastAll(rayOrigin, rayDirection, distToFocal, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            // Filter to only include wall tiles (name starts with "WorldTile_#")
            var wallHitsList = new System.Collections.Generic.List<RaycastHit>();
            foreach (var hit in allHits)
            {
                if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    wallHitsList.Add(hit);
                }
            }
            RaycastHit[] hits = wallHitsList.ToArray();
            Debug.Log($"[HeartwardGrasp] Raycast: allHits={allHits.Length}, wallHits={hits.Length}");

            // Sort hits by distance from ray origin (heart)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Log all wall hits for debugging
            for (int i = 0; i < hits.Length; i++)
            {
                Debug.Log($"[HeartwardGrasp] Wall hit {i}: dist={hits[i].distance:F2}, pos={hits[i].point}, obj={hits[i].collider.gameObject.name}");
            }

            // HGZ is placed directly at the wall center (no offset)
            const float MAX_PUSHING_DISTANCE_FROM_HEART = 3.5f;
            const float MAX_GRABBING_DISTANCE_FROM_FOCAL = 3.5f;

            Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
            Vector2 focalPos2D = new Vector2(focalPos.x, focalPos.y);

            if (hits.Length >= 1)
            {
                // Get first and last wall model transforms
                var firstHit = hits[0];
                var lastHit = hits[hits.Length - 1];

                // Get wall model centers
                Transform firstWallTransform = firstHit.collider.transform;
                Transform lastWallTransform = lastHit.collider.transform;

                Vector3 firstWallCenter = firstWallTransform.position;
                Vector3 lastWallCenter = lastWallTransform.position;

                // For pushing: place at wall center
                Vector2 pushingPos = new Vector2(firstWallCenter.x, firstWallCenter.y);

                // Check if pushing position is too far from heart (ray missed node border wall)
                float distFromHeart = Vector2.Distance(pushingPos, heartPos2D);

                if (distFromHeart > MAX_PUSHING_DISTANCE_FROM_HEART)
                {
                    Debug.Log($"[HeartwardGrasp] First hit too far from heart ({distFromHeart:F2} > {MAX_PUSHING_DISTANCE_FROM_HEART}), finding closest node border wall");

                    // Find closest wall on heart node border to the ray
                    Transform closestNodeWall = FindClosestNodeBorderWallToRay(heartPos2D, rayDirection);
                    if (closestNodeWall != null)
                    {
                        firstWallCenter = closestNodeWall.position;
                        pushingPos = new Vector2(firstWallCenter.x, firstWallCenter.y);
                        Debug.Log($"[HeartwardGrasp] Using node border wall at {firstWallCenter}");
                    }
                }

                // For grabbing: place at wall center
                Vector2 grabbingPos = new Vector2(lastWallCenter.x, lastWallCenter.y);

                // Check if grabbing position is too far from focal point (ray missed edge border wall)
                float distFromFocal = Vector2.Distance(grabbingPos, focalPos2D);

                if (distFromFocal > MAX_GRABBING_DISTANCE_FROM_FOCAL)
                {
                    Debug.Log($"[HeartwardGrasp] Last hit too far from focal ({distFromFocal:F2} > {MAX_GRABBING_DISTANCE_FROM_FOCAL}), finding closest edge border wall");

                    // Find closest wall to the focal point
                    Transform closestEdgeWall = FindClosestWallToPoint(focalPos2D, rayDirection);
                    if (closestEdgeWall != null)
                    {
                        lastWallCenter = closestEdgeWall.position;
                        grabbingPos = new Vector2(lastWallCenter.x, lastWallCenter.y);
                        Debug.Log($"[HeartwardGrasp] Using edge border wall at {lastWallCenter}");
                    }
                }

                // Calculate "into forest" direction using sampling approach for both
                // Sampling works for both node borders and edge walls
                Vector2 pushingIntoForest = GetIntoForestDirectionForEdge(pushingPos);
                Vector2 grabbingIntoForest = GetIntoForestDirectionForEdge(grabbingPos);

                // Offset into forest
                Vector2 pushingOffset = pushingIntoForest * HGZ_WALL_OFFSET;
                pushingWallPos = new Vector3(pushingPos.x + pushingOffset.x, pushingPos.y + pushingOffset.y, -0.4f);

                Vector2 grabbingOffset = grabbingIntoForest * HGZ_WALL_OFFSET;
                grabbingWallPos = new Vector3(grabbingPos.x + grabbingOffset.x, grabbingPos.y + grabbingOffset.y, -0.4f);

                // Store for later use (perpendicular is the into-forest direction)
                Vector2 pushingWallPerp = pushingIntoForest;
                Vector2 grabbingWallPerp = grabbingIntoForest;

                pushingWallNormal = new Vector3(pushingWallPerp.x, pushingWallPerp.y, 0f);
                grabbingWallNormal = new Vector3(grabbingWallPerp.x, grabbingWallPerp.y, 0f);

                Debug.Log($"[HeartwardGrasp] Pushing: intoForest={pushingIntoForest}, offset={pushingOffset}");
                Debug.Log($"[HeartwardGrasp] Grabbing: intoForest={grabbingIntoForest}, offset={grabbingOffset}");
                Debug.Log($"[HeartwardGrasp] First wall center: {firstWallCenter}, pushing at {pushingWallPos}");
                Debug.Log($"[HeartwardGrasp] Last wall center: {lastWallCenter}, grabbing at {grabbingWallPos}");

                // Create debug spheres at wall centers
                debugPushingHitSphere = CreateDebugSphere(new Vector3(firstWallCenter.x, firstWallCenter.y, 0f), Color.cyan, "Debug_PushingWallHit");
                debugGrabbingHitSphere = CreateDebugSphere(new Vector3(lastWallCenter.x, lastWallCenter.y, 0f), Color.magenta, "Debug_GrabbingWallHit");
            }
            else
            {
                // No walls hit along ray - find closest walls to heart and focal point
                Debug.Log($"[HeartwardGrasp] No walls hit along ray, searching for nearby walls");

                Vector2 rayDir2D = new Vector2(rayDirection.x, rayDirection.y).normalized;

                // Find closest wall to heart node border
                Transform closestNodeWall = FindClosestNodeBorderWallToRay(heartPos2D, rayDirection);
                if (closestNodeWall != null)
                {
                    Vector2 pushingPos = new Vector2(closestNodeWall.position.x, closestNodeWall.position.y);
                    // Use sampling approach for all wall types
                    Vector2 pushingIntoForest = GetIntoForestDirectionForEdge(pushingPos);
                    Vector2 pushingOffset = pushingIntoForest * HGZ_WALL_OFFSET;

                    pushingWallPos = new Vector3(
                        closestNodeWall.position.x + pushingOffset.x,
                        closestNodeWall.position.y + pushingOffset.y,
                        -0.4f);
                    pushingWallNormal = new Vector3(pushingIntoForest.x, pushingIntoForest.y, 0f);
                    Debug.Log($"[HeartwardGrasp] Found pushing wall at node border: {pushingWallPos}, intoForest={pushingIntoForest}");
                }
                else
                {
                    // Default fallback - just use ray direction (radially outward from heart)
                    Vector2 pushingIntoForest = rayDir2D;  // Ray goes from heart to focal, so same direction
                    Vector2 pushingOffset = pushingIntoForest * HGZ_WALL_OFFSET;
                    pushingWallPos = new Vector3(rayOrigin.x + rayDirection.x * 3.5f + pushingOffset.x, rayOrigin.y + rayDirection.y * 3.5f + pushingOffset.y, -0.4f);
                    pushingWallNormal = rayDirection;
                    Debug.Log($"[HeartwardGrasp] No node border wall found, using default pushing position");
                }

                // Find closest wall to focal point
                Transform closestFocalWall = FindClosestWallToPoint(focalPos2D, rayDirection);
                if (closestFocalWall != null)
                {
                    Vector2 grabbingPos = new Vector2(closestFocalWall.position.x, closestFocalWall.position.y);
                    // Use sampling approach for edge walls
                    Vector2 grabbingIntoForest = GetIntoForestDirectionForEdge(grabbingPos);
                    Vector2 grabbingOffset = grabbingIntoForest * HGZ_WALL_OFFSET;

                    grabbingWallPos = new Vector3(
                        closestFocalWall.position.x + grabbingOffset.x,
                        closestFocalWall.position.y + grabbingOffset.y,
                        -0.4f);
                    grabbingWallNormal = new Vector3(grabbingIntoForest.x, grabbingIntoForest.y, 0f);
                    Debug.Log($"[HeartwardGrasp] Found grabbing wall near focal: {grabbingWallPos}, intoForest={grabbingIntoForest}");
                }
                else
                {
                    // Default fallback - just use ray direction
                    Vector2 grabbingOffset = rayDir2D * HGZ_WALL_OFFSET;
                    grabbingWallPos = new Vector3(focalPos3D.x + grabbingOffset.x, focalPos3D.y + grabbingOffset.y, -0.4f);
                    grabbingWallNormal = -rayDirection;
                    Debug.Log($"[HeartwardGrasp] No focal wall found, using focal point as grabbing position");
                }

                // Create debug spheres at the offset positions
                debugPushingHitSphere = CreateDebugSphere(pushingWallPos, Color.cyan, "Debug_PushingWallHit");
                debugGrabbingHitSphere = CreateDebugSphere(grabbingWallPos, Color.magenta, "Debug_GrabbingWallHit");
            }

            Debug.Log($"[HeartwardGrasp] Final positions: pushing={pushingWallPos}, grabbing={grabbingWallPos}");
        }

        private Transform FindClosestNodeBorderWallToRay(Vector2 heartPos, Vector3 rayDir)
        {
            // Find all wall models near the heart node border (NodeWalls_0 contains heart node walls)
            GameObject nodeWallsContainer = GameObject.Find("NodeWalls_0");
            if (nodeWallsContainer == null)
            {
                Debug.LogWarning("[HeartwardGrasp] Could not find NodeWalls_0");
                return null;
            }

            Transform closestWall = null;
            float closestDistToRay = float.MaxValue;

            Vector2 rayDir2D = new Vector2(rayDir.x, rayDir.y).normalized;

            // Iterate through all child wall models
            foreach (Transform child in nodeWallsContainer.transform)
            {
                // Only consider wall tiles (WorldTile_#)
                if (!child.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(child.position.x, child.position.y);

                // Calculate distance from wall to the ray line
                // Ray starts at heart and goes in rayDir direction
                Vector2 heartToWall = wallPos - heartPos;
                float projectionLength = Vector2.Dot(heartToWall, rayDir2D);

                // Only consider walls in the direction of the ray (positive projection)
                if (projectionLength < 0) continue;

                // Point on ray closest to the wall
                Vector2 closestPointOnRay = heartPos + rayDir2D * projectionLength;
                float distToRay = Vector2.Distance(wallPos, closestPointOnRay);

                if (distToRay < closestDistToRay)
                {
                    closestDistToRay = distToRay;
                    closestWall = child;
                }
            }

            Debug.Log($"[HeartwardGrasp] Closest node border wall: {(closestWall != null ? closestWall.name : "none")} at dist {closestDistToRay:F2}");
            return closestWall;
        }

        /// <summary>
        /// Finds the closest wall to a given point, preferring walls on the correct side of the ray.
        /// Used when the ray doesn't directly hit a wall near the focal point.
        /// </summary>
        private Transform FindClosestWallToPoint(Vector2 targetPoint, Vector3 rayDir)
        {
            // Search for walls near the target point using physics
            const float SEARCH_RADIUS = 5f;
            Collider[] colliders = Physics.OverlapSphere(
                new Vector3(targetPoint.x, targetPoint.y, 0f),
                SEARCH_RADIUS,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            Transform closestWall = null;
            float closestDist = float.MaxValue;

            Vector2 rayDir2D = new Vector2(rayDir.x, rayDir.y).normalized;

            foreach (var collider in colliders)
            {
                // Only consider wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(collider.transform.position.x, collider.transform.position.y);
                float dist = Vector2.Distance(wallPos, targetPoint);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestWall = collider.transform;
                }
            }

            Debug.Log($"[HeartwardGrasp] Closest wall to focal: {(closestWall != null ? closestWall.name : "none")} at dist {closestDist:F2}");
            return closestWall;
        }

        /// <summary>
        /// Calculates the "into forest" direction for grabbing HGZ (near focal/edge).
        /// Wall tiles have transform.right pointing TOWARD the path (their normal).
        /// The OPPOSITE of the average normal = "into forest" direction.
        /// </summary>
        private Vector2 GetIntoForestDirectionForEdge(Vector2 position)
        {
            const float SAMPLE_RADIUS = 2.5f;  // Radius to sample nearby walls

            Vector3 pos3D = new Vector3(position.x, position.y, 0f);
            Collider[] nearbyColliders = Physics.OverlapSphere(pos3D, SAMPLE_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            Vector2 sumNormals = Vector2.zero;
            int wallCount = 0;

            foreach (var collider in nearbyColliders)
            {
                // Only include wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                // Wall's transform.right points TOWARD the path (the wall's normal/front face)
                Vector2 wallNormal = new Vector2(collider.transform.right.x, collider.transform.right.y);
                sumNormals += wallNormal;
                wallCount++;
            }

            if (wallCount == 0)
            {
                // Fallback: use direction away from heart
                Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
                Vector2 awayFromHeart = (position - heartPos2D).normalized;
                Debug.Log($"[HeartwardGrasp] GetIntoForestDirectionForEdge: no walls found, using away-from-heart direction {awayFromHeart}");
                return awayFromHeart;
            }

            // Wall's transform.right actually points AWAY from path (into forest)
            // So the average IS the "into forest" direction
            Vector2 avgNormal = (sumNormals / wallCount).normalized;
            Vector2 intoForest = avgNormal;  // Not negated - walls point into forest

            Debug.Log($"[HeartwardGrasp] GetIntoForestDirectionForEdge at {position}: sampled {wallCount} walls, avgNormal={avgNormal}, intoForest={intoForest}");
            return intoForest;
        }

        /// <summary>
        /// Calculates the "into forest" direction for pushing HGZ (near heart/node border).
        /// For node borders, "into forest" = AWAY from heart center (radially outward).
        /// </summary>
        private Vector2 GetIntoForestDirectionForNode(Vector2 position)
        {
            Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
            Vector2 awayFromHeart = (position - heartPos2D).normalized;
            Debug.Log($"[HeartwardGrasp] GetIntoForestDirectionForNode at {position}: awayFromHeart={awayFromHeart}");
            return awayFromHeart;
        }

        /// <summary>
        /// Validates that a position is at least MIN_EDGE_DISTANCE from any path/node edge.
        /// Returns a corrected position if the original is too close to an edge.
        /// For nodes, accounts for node radius (distance to edge = distance to center - radius).
        /// </summary>
        private Vector3 ValidateHGZPosition(Vector3 proposedPos, Vector2 offsetDir, float minEdgeDistance)
        {
            const float CHECK_RADIUS = 6.0f;  // Must be large enough to detect heart node center (radius 3.0 + buffer)
            const float NODE_RADIUS = 3.0f;   // Node radius from MazeRenderer

            Debug.Log($"[HeartwardGrasp] ValidateHGZPosition: checking pos={proposedPos}, offsetDir={offsetDir}, minDist={minEdgeDistance}");

            // Check if there are any path or node tiles near the proposed position
            Collider[] nearbyColliders = Physics.OverlapSphere(proposedPos, CHECK_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            Debug.Log($"[HeartwardGrasp] ValidateHGZPosition: found {nearbyColliders.Length} colliders within radius {CHECK_RADIUS}");

            float closestEdgeDist = float.MaxValue;
            string closestName = "";
            int pathCount = 0;
            int nodeCount = 0;

            foreach (var collider in nearbyColliders)
            {
                // Check for path tiles (MazePath tag) or node tiles (MazeNode tag)
                bool isPath = collider.CompareTag("MazePath") || collider.gameObject.name.Contains("PathTile");
                bool isNode = collider.CompareTag("MazeNode") || collider.gameObject.name.Contains("NodeColumn") || collider.gameObject.name.Contains("NodeCylinder");

                if (isPath) pathCount++;
                if (isNode) nodeCount++;

                if (isPath || isNode)
                {
                    float distToCenter = Vector3.Distance(proposedPos, collider.transform.position);
                    float distToEdge;

                    if (isNode)
                    {
                        // For nodes, the edge is NODE_RADIUS away from center
                        distToEdge = distToCenter - NODE_RADIUS;
                        Debug.Log($"[HeartwardGrasp] Found node: {collider.gameObject.name}, tag={collider.tag}, center={collider.transform.position}, distToCenter={distToCenter:F2}, distToEdge={distToEdge:F2}");
                    }
                    else
                    {
                        // For path tiles, the center IS approximately the edge (tiles are small)
                        distToEdge = distToCenter;
                    }

                    if (distToEdge < closestEdgeDist)
                    {
                        closestEdgeDist = distToEdge;
                        closestName = collider.gameObject.name;
                    }
                }
            }

            Debug.Log($"[HeartwardGrasp] ValidateHGZPosition: pathCount={pathCount}, nodeCount={nodeCount}, closestEdgeDist={closestEdgeDist:F2}, closestName={closestName}");

            // If we're too close to a path/node edge, push further in the offset direction
            if (closestEdgeDist < minEdgeDistance)
            {
                float additionalOffset = minEdgeDistance - closestEdgeDist + 0.5f;  // Add extra margin
                Vector3 correctedPos = proposedPos + new Vector3(offsetDir.x, offsetDir.y, 0f) * additionalOffset;
                Debug.Log($"[HeartwardGrasp] HGZ position too close to edge ({closestEdgeDist:F2} < {minEdgeDistance}), pushing {additionalOffset:F2} further. Nearest: {closestName}");
                return correctedPos;
            }

            Debug.Log($"[HeartwardGrasp] HGZ position validated: {closestEdgeDist:F2} >= {minEdgeDistance} from nearest edge ({closestName})");
            return proposedPos;
        }

        private void FindAffectedGrabbingWalls()
        {
            affectedGrabbingWalls.Clear();
            originalWallPositions.Clear();

            Vector2 grabPos2D = new Vector2(grabbingWallPos.x, grabbingWallPos.y);

            // Use Physics.OverlapSphere to find all colliders in the grabbing zone
            Collider[] colliders = Physics.OverlapSphere(grabbingWallPos, GRASP_ZONE_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                // Only include wall tiles
                if (collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    affectedGrabbingWalls.Add(collider.gameObject);
                    originalWallPositions[collider.gameObject] = collider.transform.position;
                }
            }

            Debug.Log($"[HeartwardGrasp] Found {affectedGrabbingWalls.Count} wall tiles in grabbing zone");
        }

        private void UpdateWallShakeEffect()
        {
            if (affectedGrabbingWalls.Count == 0) return;

            // Only shake when in idle/reaching phase (waiting for or grabbing visitor)
            bool shouldShake = grabPhase == GrabPhase.Idle || grabPhase == GrabPhase.Reaching || grabPhase == GrabPhase.Grabbing;

            foreach (var wall in affectedGrabbingWalls)
            {
                if (wall == null) continue;

                if (shouldShake && originalWallPositions.TryGetValue(wall, out Vector3 originalPos))
                {
                    // Apply random shake offset
                    float shakeIntensity = 0.03f;
                    float offsetX = (UnityEngine.Random.value - 0.5f) * 2f * shakeIntensity;
                    float offsetY = (UnityEngine.Random.value - 0.5f) * 2f * shakeIntensity;
                    wall.transform.position = originalPos + new Vector3(offsetX, offsetY, 0f);
                }
                else if (originalWallPositions.TryGetValue(wall, out Vector3 origPos))
                {
                    // Reset to original position when not shaking
                    wall.transform.position = origPos;
                }
            }
        }

        private void ResetWallPositions()
        {
            foreach (var wall in affectedGrabbingWalls)
            {
                if (wall != null && originalWallPositions.TryGetValue(wall, out Vector3 originalPos))
                {
                    wall.transform.position = originalPos;
                }
            }
        }

        private void CreateGrabbingHGZ()
        {
            // Create grabbing zone at wall near focal point
            grabbingZoneObject = new GameObject("GrabbingHGZ");
            grabbingZoneObject.transform.position = grabbingWallPos;

            // Add trigger collider
            grabbingCollider = grabbingZoneObject.AddComponent<SphereCollider>();
            grabbingCollider.radius = GRASP_ZONE_RADIUS;
            grabbingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            var rb = grabbingZoneObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Spawn and configure visual - oriented TOWARD focal point (away from heart)
            // Model +X axis should point toward where visitors approach from
            Vector3 dirTowardFocal = (targetPosition - grabbingWallPos).normalized;
            SpawnGraspVisual(grabbingZoneObject, grabbingWallPos, dirTowardFocal, ref grabbingVisual, ref grabbingAnimator, ref grabbingTouchCollider, "GrabbingHand");

            // Debug: create cylinder showing collision zone
            debugGrabbingZoneCylinder = CreateDebugZoneCylinder(grabbingWallPos, GRASP_ZONE_RADIUS, Color.green, "Debug_GrabbingZone");
        }

        private void CreatePushingHGZ()
        {
            // Create pushing zone at wall near heart
            pushingZoneObject = new GameObject("PushingHGZ");
            pushingZoneObject.transform.position = pushingWallPos;

            // Add trigger collider
            pushingCollider = pushingZoneObject.AddComponent<SphereCollider>();
            pushingCollider.radius = GRASP_ZONE_RADIUS;
            pushingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            var rb = pushingZoneObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Spawn and configure visual - oriented TOWARD heart
            // Model +X axis should point toward the heart where visitors are pushed
            Vector3 dirTowardHeart = (heartNodePosition - pushingWallPos).normalized;
            SpawnGraspVisual(pushingZoneObject, pushingWallPos, dirTowardHeart, ref pushingVisual, ref pushingAnimator, ref pushingTouchCollider, "PushingHand");

            // Debug: create cylinder showing collision zone
            debugPushingZoneCylinder = CreateDebugZoneCylinder(pushingWallPos, GRASP_ZONE_RADIUS, Color.red, "Debug_PushingZone");
        }

        private void SpawnGraspVisual(GameObject parent, Vector3 position, Vector3 forwardDir, ref GameObject visual, ref Animator animator, ref SphereCollider touchCollider, string name)
        {
            GameObject graspPrefab = Resources.Load<GameObject>("Prefabs/Props/Grasp/grasp");
            if (graspPrefab == null)
            {
                Debug.LogWarning("[HeartwardGrasp] Could not load grasp prefab");
                return;
            }

            // The grasp prefab's default orientation (with its baked-in rotation) has:
            // - Palm facing camera (-Z direction)
            // - Fingers pointing +X direction
            // This is correct for this game's coordinate system (XY plane, -Z up).
            // We only need to rotate around Z to point fingers toward the target direction.
            float angle = Mathf.Atan2(forwardDir.y, forwardDir.x) * Mathf.Rad2Deg;
            Quaternion finalRotation = Quaternion.Euler(0f, 0f, angle);

            Debug.Log($"[HeartwardGrasp] {name} forwardDir={forwardDir}, angle={angle}deg, rotation={finalRotation.eulerAngles}");

            visual = Object.Instantiate(graspPrefab, position, finalRotation);
            visual.name = name;
            visual.transform.SetParent(parent.transform, worldPositionStays: true);

            // Get animator and disable it (no animations during reaching)
            animator = visual.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visual.GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                // Log animator details
                var controller = animator.runtimeAnimatorController;
                Debug.Log($"[HeartwardGrasp] {name} animator found. Controller: {(controller != null ? controller.name : "NULL")}, enabled: {animator.enabled}");

                if (controller != null)
                {
                    // Keep animator enabled but paused at frame 0
                    animator.speed = 0f;
                    animator.Play("GraspFinal", 0, 0f);
                    animator.Update(0f);

                    // Verify the state was set
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    Debug.Log($"[HeartwardGrasp] {name} initialized. State hash: {stateInfo.fullPathHash}, normalizedTime: {stateInfo.normalizedTime:F3}, length: {stateInfo.length:F3}");
                }
                else
                {
                    Debug.LogWarning($"[HeartwardGrasp] {name} animator has no controller assigned!");
                }
            }
            else
            {
                Debug.LogWarning($"[HeartwardGrasp] {name} has no Animator component");
            }

            // Find or create the "touch" collider on the grasp model
            // The touch collider should be positioned at the palm/fingertips
            touchCollider = null;
            Transform touchTransform = visual.transform.Find("touch");
            if (touchTransform == null)
            {
                // Search recursively
                foreach (Transform child in visual.GetComponentsInChildren<Transform>())
                {
                    if (child.name == "touch")
                    {
                        touchTransform = child;
                        break;
                    }
                }
            }

            if (touchTransform != null)
            {
                touchCollider = touchTransform.GetComponent<SphereCollider>();
                if (touchCollider != null)
                {
                    touchCollider.isTrigger = true;
                    Debug.Log($"[HeartwardGrasp] {name} touch collider found, isTrigger={touchCollider.isTrigger}");
                }
            }

            // If no touch collider found, create one at runtime
            if (touchCollider == null)
            {
                // Create touch collider as sibling to visual (not child) to avoid scale inheritance issues
                // Position it at visual's XY position but at Z=-0.3 (closer to ground plane where visitors are)
                GameObject touchObj = new GameObject("touch");
                touchObj.transform.SetParent(visual.transform.parent, worldPositionStays: false);
                touchObj.transform.position = new Vector3(visual.transform.position.x, visual.transform.position.y, -0.3f);
                touchObj.transform.localScale = Vector3.one;

                touchCollider = touchObj.AddComponent<SphereCollider>();
                touchCollider.radius = 0.1f;  // Small radius for precise detection
                touchCollider.isTrigger = true;

                // Store reference so we can update position during reaching
                touchObj.name = $"touch_{name}";

                Debug.Log($"[HeartwardGrasp] {name} created touch collider at runtime, pos={touchObj.transform.position}, radius={touchCollider.radius}");
            }
        }

        private void CreateParticleSystem(GameObject parent, ref ParticleSystem particles)
        {
            if (parent == null) return;

            GameObject particleObj = new GameObject("DebrisParticles");
            particleObj.transform.position = parent.transform.position;
            particleObj.transform.SetParent(parent.transform);

            particles = particleObj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.startLifetime = 2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(LeafGreen, BarkBrown);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;
            main.playOnAwake = false;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = GRASP_ZONE_RADIUS * 0.5f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Sprites/Default");

            if (particleShader != null)
            {
                renderer.material = new Material(particleShader);
            }

            particles.Play();
        }

        private void CreateDebugRayColumn(Vector3 focalPos)
        {
            Vector3 rayOrigin = new Vector3(heartNodePosition.x, heartNodePosition.y, -0.5f);
            Vector3 focalPos3D = new Vector3(focalPos.x, focalPos.y, -0.5f);
            Vector3 midpoint = (rayOrigin + focalPos3D) / 2f;
            float rayLength = Vector3.Distance(rayOrigin, focalPos3D);
            Vector3 rayDirection = (focalPos3D - rayOrigin).normalized;

            // Create cylinder along the ray
            debugRayColumn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            debugRayColumn.name = "HeartwardGrasp_DebugRay";

            // Remove collider
            var collider = debugRayColumn.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            // Position at midpoint of ray
            debugRayColumn.transform.position = midpoint;

            // Cylinder default: height 2, radius 0.5, oriented along Y axis
            // Scale: radius 0.1 means radiusScale = 0.1/0.5 = 0.2, height = rayLength/2
            float radiusScale = 0.1f / 0.5f;  // radius 0.1
            float heightScale = rayLength / 2f;
            debugRayColumn.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

            // Orient cylinder along ray direction (default Y axis -> ray direction)
            float angle = Mathf.Atan2(rayDirection.y, rayDirection.x) * Mathf.Rad2Deg;
            debugRayColumn.transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

            // Bright yellow material
            var renderer = debugRayColumn.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Standard");

                if (shader != null)
                {
                    var mat = new Material(shader);
                    Color brightYellow = new Color(1f, 1f, 0f, 1f);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", brightYellow);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", brightYellow);
                    renderer.material = mat;
                }
            }

            Debug.Log($"[HeartwardGrasp] Debug ray created from {rayOrigin} to {focalPos3D}, length={rayLength}");
        }

        private GameObject CreateDebugSphere(Vector3 position, Color color, string name)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;

            // Remove collider
            var collider = sphere.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            // Position at z=-0.5, radius ~0.5
            sphere.transform.position = new Vector3(position.x, position.y, -0.5f);
            sphere.transform.localScale = new Vector3(1f, 1f, 1f);

            // Set color
            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Standard");

                if (shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", color);
                    renderer.material = mat;
                }
            }

            return sphere;
        }

        private GameObject CreateDebugZoneCylinder(Vector3 position, float radius, Color color, string name)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;

            // Remove collider
            var collider = cylinder.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            // Position at playing surface (z=0), height 0.1 along Z axis
            cylinder.transform.position = new Vector3(position.x, position.y, 0f);

            // Unity cylinder: height along local Y, radius in local XZ
            // We want: height along world Z, radius in world XY
            // Rotate -90° around X to map local Y to world Z
            cylinder.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            // Cylinder default: height 2, radius 0.5
            // After rotation: local Y (height) -> world Z, local X -> world X, local Z -> world Y
            // Scale: X and Z control radius in world XY plane, Y controls height in world Z
            float radiusScale = radius / 0.5f;
            float heightScale = 0.1f / 2f;
            cylinder.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

            // Set color
            var renderer = cylinder.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Standard");

                if (shader != null)
                {
                    var mat = new Material(shader);
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", color);
                    renderer.material = mat;
                }
            }

            return cylinder;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Process grabbing HGZ state machine
            UpdateGrabbingHGZ(deltaTime);

            // Process pushing HGZ state machine
            UpdatePushingHGZ(deltaTime);

            // Update particle emission
            UpdateParticles();

            // Update wall shake effect in grabbing zone
            UpdateWallShakeEffect();
        }

        private void CheckForVisitorsInGrabbingZone()
        {
            if (hasExpired || grabPhase != GrabPhase.Idle) return;

            var visitors = VisitorRegistry.All;
            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;
                if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                    continue;
                if (visitor == currentVisitor || pendingVisitors.Contains(visitor))
                    continue;

                float distance = Vector3.Distance(visitor.transform.position, grabbingWallPos);
                if (distance <= GRASP_ZONE_RADIUS)
                {
                    pendingVisitors.Enqueue(visitor);
                }
            }

            if (grabPhase == GrabPhase.Idle && pendingVisitors.Count > 0)
            {
                StartGrabbingVisitor(pendingVisitors.Dequeue());
            }
        }

        private void StartGrabbingVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null || hasExpired) return;

            Debug.Log($"[HeartwardGrasp] Starting grab on visitor at {visitor.transform.position}");

            currentVisitor = visitor;
            visitorVisible = true;
            // NOTE: Do NOT stop visitor here - they continue walking until grabbed at frame 46

            // Orient grabbing hand toward visitor
            OrientHandTowardTarget(grabbingVisual, visitor.transform.position);

            // Start reaching phase - animation frames 0-20
            grabPhase = GrabPhase.Reaching;
            grabPhaseStartTime = elapsedTime;
            grabCurrentFrame = 0;
            SetAnimatorFrame(grabbingAnimator, 0);

            if (grabbingParticles != null) grabbingParticles.Emit(25);

            Debug.Log($"[HeartwardGrasp] Grab phase: Idle -> Reaching");
        }

        private void UpdateGrabbingHGZ(float deltaTime)
        {
            // Check for visitors when idle
            if (grabPhase == GrabPhase.Idle)
            {
                CheckForVisitorsInGrabbingZone();
                return;
            }

            if (currentVisitor == null)
            {
                grabPhase = GrabPhase.Idle;
                return;
            }

            float phaseElapsed = elapsedTime - grabPhaseStartTime;

            switch (grabPhase)
            {
                case GrabPhase.Reaching:
                    // Translate grasp model toward visitor until touch collider hits
                    if (grabbingVisual != null && currentVisitor != null)
                    {
                        // Calculate direction to visitor (XY plane only)
                        Vector3 visitorPos = currentVisitor.transform.position;
                        Vector3 graspPos = grabbingVisual.transform.position;
                        Vector2 direction = new Vector2(visitorPos.x - graspPos.x, visitorPos.y - graspPos.y).normalized;

                        // Update orientation to track visitor as we move
                        OrientHandTowardTarget(grabbingVisual, visitorPos);

                        // Move grasp model toward visitor
                        float moveAmount = REACH_SPEED * deltaTime;
                        grabbingVisual.transform.position += new Vector3(direction.x * moveAmount, direction.y * moveAmount, 0f);

                        // Update touch collider position to follow the hand (it's a sibling, not child)
                        if (grabbingTouchCollider != null)
                        {
                            grabbingTouchCollider.transform.position = new Vector3(
                                grabbingVisual.transform.position.x,
                                grabbingVisual.transform.position.y,
                                -0.3f);  // Keep at Z=-0.3 to be on same plane as visitors
                        }

                        // Check if touch collider overlaps visitor
                        bool touchedVisitor = false;
                        if (grabbingTouchCollider != null)
                        {
                            // Get world position and radius of touch collider
                            Vector3 touchCenter = grabbingTouchCollider.transform.TransformPoint(grabbingTouchCollider.center);
                            float touchRadius = grabbingTouchCollider.radius * Mathf.Max(
                                grabbingTouchCollider.transform.lossyScale.x,
                                grabbingTouchCollider.transform.lossyScale.y,
                                grabbingTouchCollider.transform.lossyScale.z);

                            // Check for overlap with visitor colliders
                            Collider[] hits = Physics.OverlapSphere(touchCenter, touchRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                            foreach (var hit in hits)
                            {
                                if (hit.transform.IsChildOf(currentVisitor.transform) || hit.transform == currentVisitor.transform)
                                {
                                    touchedVisitor = true;
                                    Debug.Log($"[HeartwardGrasp] Touch detected! Hit: {hit.name}");
                                    break;
                                }
                            }

                            // Debug every frame to see what's happening
                            if (Time.frameCount % 30 == 0) // Every 30 frames
                            {
                                float distToVisitor = Vector2.Distance(new Vector2(touchCenter.x, touchCenter.y), new Vector2(visitorPos.x, visitorPos.y));
                                Debug.Log($"[HeartwardGrasp] Touch check: center={touchCenter}, radius={touchRadius:F3}, distToVisitor={distToVisitor:F2}, hits={hits.Length}");
                            }
                        }
                        else
                        {
                            if (Time.frameCount % 60 == 0)
                                Debug.LogWarning("[HeartwardGrasp] grabbingTouchCollider is NULL!");
                        }

                        if (touchedVisitor)
                        {
                            Debug.Log($"[HeartwardGrasp] SUCCESS: Touch collider triggered on visitor! Transitioning to Grabbing");
                            // Stop visitor movement immediately
                            currentVisitor.Stop();
                            // Store the grab position and visitor offset
                            grabStartPosition = grabbingVisual.transform.position;
                            visitorGrabOffset = currentVisitor.transform.position - grabStartPosition;
                            // Enable animator and start grab animation
                            if (grabbingAnimator != null)
                            {
                                SetAnimatorFrame(grabbingAnimator, 0);  // Start from beginning
                            }
                            // Transition to Grabbing phase
                            grabPhase = GrabPhase.Grabbing;
                            grabPhaseStartTime = elapsedTime;
                        }
                    }
                    break;

                case GrabPhase.Grabbing:
                    // Model stays still, animation plays for 1 second (24 frames)
                    // Visitor is already stopped
                    {
                        float grabProgress = Mathf.Clamp01(phaseElapsed / GRAB_ANIMATION_DURATION);
                        grabCurrentFrame = Mathf.FloorToInt(grabProgress * GRAB_ANIMATION_FRAMES);
                        SetAnimatorFrame(grabbingAnimator, grabCurrentFrame);

                        if (phaseElapsed >= GRAB_ANIMATION_DURATION)
                        {
                            Debug.Log($"[HeartwardGrasp] Grab phase: Grabbing -> Pulling (animation complete at frame {grabCurrentFrame})");
                            grabPhase = GrabPhase.Pulling;
                            grabPhaseStartTime = elapsedTime;
                            if (grabbingParticles != null) grabbingParticles.Emit(20);
                        }
                    }
                    break;

                case GrabPhase.Pulling:
                    // Model and visitor translate back to starting wall position over PULL_DURATION
                    // Hand stays closed (frame 24) - no animation change during pull
                    {
                        float pullProgress = Mathf.Clamp01(phaseElapsed / PULL_DURATION);

                        // Keep hand closed at frame 24
                        SetAnimatorFrame(grabbingAnimator, GRAB_ANIMATION_FRAMES);

                        // Move model back to starting wall position
                        Vector3 pullTarget = grabbingWallPos;
                        grabbingVisual.transform.position = Vector3.Lerp(grabStartPosition, pullTarget, pullProgress);

                        // Update touch collider position
                        if (grabbingTouchCollider != null)
                        {
                            grabbingTouchCollider.transform.position = new Vector3(
                                grabbingVisual.transform.position.x,
                                grabbingVisual.transform.position.y,
                                -0.3f);
                        }

                        // Move visitor with the hand
                        currentVisitor.transform.position = grabbingVisual.transform.position + visitorGrabOffset;

                        if (pullProgress >= 1f)
                        {
                            Debug.Log($"[HeartwardGrasp] Grab phase: Pulling -> Transporting");
                            SetVisitorVisible(currentVisitor, false);
                            visitorVisible = false;
                            grabPhase = GrabPhase.Transporting;
                            grabPhaseStartTime = elapsedTime;
                        }
                    }
                    break;

                case GrabPhase.Transporting:
                    // 1 second duration, then relocate visitor to pushing HGZ
                    if (phaseElapsed >= TRANSPORT_DURATION)
                    {
                        Debug.Log($"[HeartwardGrasp] Transport complete, starting push sequence");
                        Debug.Log($"[HeartwardGrasp] visitorGrabOffset={visitorGrabOffset}, pushingWallPos={pushingWallPos}");

                        // Transform the grab offset to pushing hand's orientation
                        // Grabbing hand points away from heart, pushing hand points toward heart
                        // Convert offset from grabbing hand's local space to pushing hand's local space
                        Vector3 grabbingDir = (grabbingWallPos - heartNodePosition).normalized;
                        Vector3 pushingDir = (heartNodePosition - pushingWallPos).normalized;

                        // Calculate the rotation difference between the two orientations
                        float grabbingAngle = Mathf.Atan2(grabbingDir.y, grabbingDir.x);
                        float pushingAngle = Mathf.Atan2(pushingDir.y, pushingDir.x);
                        float angleDiff = pushingAngle - grabbingAngle;

                        Debug.Log($"[HeartwardGrasp] grabbingDir={grabbingDir}, pushingDir={pushingDir}");
                        Debug.Log($"[HeartwardGrasp] grabbingAngle={grabbingAngle * Mathf.Rad2Deg}°, pushingAngle={pushingAngle * Mathf.Rad2Deg}°, angleDiff={angleDiff * Mathf.Rad2Deg}°");

                        // Rotate the offset to match pushing hand's orientation
                        visitorPushOffset = new Vector3(
                            visitorGrabOffset.x * Mathf.Cos(angleDiff) - visitorGrabOffset.y * Mathf.Sin(angleDiff),
                            visitorGrabOffset.x * Mathf.Sin(angleDiff) + visitorGrabOffset.y * Mathf.Cos(angleDiff),
                            visitorGrabOffset.z
                        );

                        Debug.Log($"[HeartwardGrasp] visitorPushOffset={visitorPushOffset}");

                        Vector3 newVisitorPos = pushingWallPos + visitorPushOffset;
                        Debug.Log($"[HeartwardGrasp] Setting visitor position to {newVisitorPos}");
                        currentVisitor.transform.position = newVisitorPos;

                        // Start pushing sequence
                        StartPushingSequence();

                        // Reset grabbing state
                        grabPhase = GrabPhase.Idle;
                        SetAnimatorFrame(grabbingAnimator, 0);
                    }
                    break;

            }
        }

        private void StartPushingSequence()
        {
            // Orient pushing hand toward heart
            OrientHandTowardTarget(pushingVisual, heartNodePosition);

            // Set animator to end of animation (frame 24 = closed hand)
            if (pushingAnimator != null)
            {
                SetAnimatorFrame(pushingAnimator, GRAB_ANIMATION_FRAMES);  // Frame 24
            }

            // Calculate push translation positions
            pushingStartPos = pushingWallPos;
            Vector3 dirToHeart = (heartNodePosition - pushingWallPos).normalized;
            pushingTargetPos = pushingStartPos + dirToHeart * PUSH_DISTANCE;

            // Start with Pushing phase (translate toward heart)
            pushPhase = PushPhase.Pushing;
            pushPhaseStartTime = elapsedTime;
            pushCurrentFrame = GRAB_ANIMATION_FRAMES;

            if (pushingParticles != null) pushingParticles.Emit(25);

            Debug.Log($"[HeartwardGrasp] Push phase: Idle -> Pushing (translating {PUSH_DISTANCE} unit toward heart)");
        }

        private void UpdatePushingHGZ(float deltaTime)
        {
            if (pushPhase == PushPhase.Idle) return;
            if (currentVisitor == null)
            {
                pushPhase = PushPhase.Idle;
                return;
            }

            float phaseElapsed = elapsedTime - pushPhaseStartTime;

            switch (pushPhase)
            {
                case PushPhase.Pushing:
                    // Model translates 1 unit toward heart, hand stays closed (frame 24)
                    {
                        float pushProgress = Mathf.Clamp01(phaseElapsed / PUSH_DURATION);

                        // Keep hand closed at frame 24
                        SetAnimatorFrame(pushingAnimator, GRAB_ANIMATION_FRAMES);

                        // Translate model toward heart
                        pushingVisual.transform.position = Vector3.Lerp(pushingStartPos, pushingTargetPos, pushProgress);

                        // Update touch collider position
                        if (pushingTouchCollider != null)
                        {
                            pushingTouchCollider.transform.position = new Vector3(
                                pushingVisual.transform.position.x,
                                pushingVisual.transform.position.y,
                                -0.3f);
                        }

                        // Move visitor with the hand
                        currentVisitor.transform.position = pushingVisual.transform.position + visitorPushOffset;

                        if (pushProgress >= 1f)
                        {
                            Debug.Log($"[HeartwardGrasp] Push phase: Pushing -> Releasing");
                            pushPhase = PushPhase.Releasing;
                            pushPhaseStartTime = elapsedTime;
                        }
                    }
                    break;

                case PushPhase.Releasing:
                    // Model paused, animation plays in reverse from frame 24 to 0 over 1 second
                    // Visitor becomes visible and dazed at end
                    {
                        float releaseProgress = Mathf.Clamp01(phaseElapsed / GRAB_ANIMATION_DURATION);

                        // Animation in reverse: 24 -> 0
                        pushCurrentFrame = Mathf.FloorToInt((1f - releaseProgress) * GRAB_ANIMATION_FRAMES);
                        SetAnimatorFrame(pushingAnimator, pushCurrentFrame);

                        // Make visitor visible partway through
                        if (!visitorVisible && releaseProgress > 0.3f)
                        {
                            SetVisitorVisible(currentVisitor, true);
                            visitorVisible = true;
                        }

                        if (releaseProgress >= 1f)
                        {
                            Debug.Log($"[HeartwardGrasp] Push phase: Releasing -> Withdrawing, visitor dazed");
                            // Apply daze to visitor
                            float dazeDuration = definition.param1 > 0 ? definition.param1 : 2f;
                            currentVisitor.OnWitnessMazeGrowth(dazeDuration);

                            pushPhase = PushPhase.Withdrawing;
                            pushPhaseStartTime = elapsedTime;
                            if (pushingParticles != null) pushingParticles.Emit(15);
                        }
                    }
                    break;

                case PushPhase.Withdrawing:
                    // Model translates back to wall, holds frame 1
                    {
                        float withdrawProgress = Mathf.Clamp01(phaseElapsed / WITHDRAW_DURATION);

                        // Hold at frame 1 (not 0 to avoid T-pose)
                        SetAnimatorFrame(pushingAnimator, 1);

                        // Translate model back to wall
                        pushingVisual.transform.position = Vector3.Lerp(pushingTargetPos, pushingStartPos, withdrawProgress);

                        // Update touch collider position
                        if (pushingTouchCollider != null)
                        {
                            pushingTouchCollider.transform.position = new Vector3(
                                pushingVisual.transform.position.x,
                                pushingVisual.transform.position.y,
                                -0.3f);
                        }

                        if (withdrawProgress >= 1f)
                        {
                            Debug.Log($"[HeartwardGrasp] Push phase: Withdrawing complete");
                            FinalizeCapture();
                        }
                    }
                    break;
            }
        }

        private void FinalizeCapture()
        {
            if (currentVisitor != null)
            {
                Debug.Log($"[HeartwardGrasp] FinalizeCapture - visitor position before resume: {currentVisitor.transform.position}");
                currentVisitor.Resume();
                currentVisitor.RecalculatePath();
                Debug.Log($"[HeartwardGrasp] FinalizeCapture - visitor position after resume: {currentVisitor.transform.position}");
                Debug.Log($"[HeartwardGrasp] Visitor released and dazed at heart area");
            }

            capturedCount++;
            Debug.Log($"[HeartwardGrasp] Capture complete. Count: {capturedCount}/{requiredCaptures}");

            if (capturedCount >= requiredCaptures)
            {
                hasExpired = true;
                Debug.Log($"[HeartwardGrasp] Power expired after {capturedCount} captures");
            }

            currentVisitor = null;
            pushPhase = PushPhase.Idle;

            // Reset pushing visual to wall position and closed hand for next capture
            if (pushingVisual != null)
            {
                pushingVisual.transform.position = pushingWallPos;
            }
            if (pushingTouchCollider != null)
            {
                pushingTouchCollider.transform.position = new Vector3(pushingWallPos.x, pushingWallPos.y, -0.3f);
            }
            SetAnimatorFrame(pushingAnimator, GRAB_ANIMATION_FRAMES);  // Reset to closed hand for next reverse play
        }

        private void OrientHandTowardTarget(GameObject visual, Vector3 targetPos)
        {
            if (visual == null) return;

            Vector3 direction = (targetPos - visual.transform.position).normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.right;

            // The prefab's default has fingers pointing +X, palm facing -Z.
            // Just rotate around Z to point fingers toward target.
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            visual.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SetAnimatorFrame(Animator animator, int frame)
        {
            if (animator == null)
            {
                Debug.LogWarning($"[HeartwardGrasp] SetAnimatorFrame called with null animator!");
                return;
            }

            // Ensure animator is enabled
            if (!animator.enabled)
            {
                animator.enabled = true;
                Debug.Log($"[HeartwardGrasp] SetAnimatorFrame: re-enabled animator");
            }

            // The GLB animation reports length: Infinity, so normalized time doesn't work.
            // Instead, use the actual animation time. The animation is ~0.4 seconds at 60fps (24 frames).
            // Frame 0 = time 0, Frame 24 = time 0.4
            const float ANIMATION_DURATION_SECONDS = 0.4f;  // 24 frames at 60fps
            float targetTime = (frame / (float)GRAB_ANIMATION_FRAMES) * ANIMATION_DURATION_SECONDS;

            // Play the animation and immediately jump to the target time
            animator.speed = 1f;  // Enable playback temporarily
            animator.Play("GraspFinal", 0, 0f);  // Start from beginning
            animator.Update(targetTime);  // Advance to target time
            animator.speed = 0f;  // Pause

            Debug.Log($"[HeartwardGrasp] SetAnimatorFrame: frame={frame}/{GRAB_ANIMATION_FRAMES}, targetTime={targetTime:F3}s");
        }

        private void SetVisitorVisible(VisitorControllerBase visitor, bool visible)
        {
            if (visitor == null) return;

            var renderers = visitor.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }

        private void UpdateParticles()
        {
            bool grabActive = grabPhase != GrabPhase.Idle;
            bool pushActive = pushPhase != PushPhase.Idle;

            if (grabbingParticles != null)
            {
                var emission = grabbingParticles.emission;
                emission.rateOverTime = grabActive ? 25f : 8f;
            }

            if (pushingParticles != null)
            {
                var emission = pushingParticles.emission;
                emission.rateOverTime = pushActive ? 25f : 8f;
            }
        }

        public override void OnEnd()
        {
            Debug.Log($"[HeartwardGrasp] OnEnd - Cleaning up. Captured: {capturedCount}");

            if (grabbingZoneObject != null)
            {
                Object.Destroy(grabbingZoneObject);
                grabbingZoneObject = null;
            }

            if (pushingZoneObject != null)
            {
                Object.Destroy(pushingZoneObject);
                pushingZoneObject = null;
            }

            // Clean up debug visualizations
            if (debugRayColumn != null)
            {
                Object.Destroy(debugRayColumn);
                debugRayColumn = null;
            }
            if (debugGrabbingHitSphere != null)
            {
                Object.Destroy(debugGrabbingHitSphere);
                debugGrabbingHitSphere = null;
            }
            if (debugPushingHitSphere != null)
            {
                Object.Destroy(debugPushingHitSphere);
                debugPushingHitSphere = null;
            }
            if (debugGrabbingZoneCylinder != null)
            {
                Object.Destroy(debugGrabbingZoneCylinder);
                debugGrabbingZoneCylinder = null;
            }
            if (debugPushingZoneCylinder != null)
            {
                Object.Destroy(debugPushingZoneCylinder);
                debugPushingZoneCylinder = null;
            }

            if (currentVisitor != null)
            {
                SetVisitorVisible(currentVisitor, true);
                currentVisitor.Resume();
                currentVisitor = null;
            }

            pendingVisitors.Clear();

            // Reset wall positions before cleanup
            ResetWallPositions();
            affectedGrabbingWalls.Clear();
            originalWallPositions.Clear();

            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.RemoveEffectsByPowerType(HeartPowerType.HeartwardGrasp);
            }
        }

        public override void ApplyWorldOffset(Vector3 worldOffset)
        {
            targetPosition += worldOffset;
            heartNodePosition += worldOffset;
            grabbingWallPos += worldOffset;
            pushingWallPos += worldOffset;

            if (grabbingZoneObject != null)
                grabbingZoneObject.transform.position += worldOffset;
            if (pushingZoneObject != null)
                pushingZoneObject.transform.position += worldOffset;

            // Move debug visualizations
            if (debugRayColumn != null)
                debugRayColumn.transform.position += worldOffset;
            if (debugGrabbingHitSphere != null)
                debugGrabbingHitSphere.transform.position += worldOffset;
            if (debugPushingHitSphere != null)
                debugPushingHitSphere.transform.position += worldOffset;
            if (debugGrabbingZoneCylinder != null)
                debugGrabbingZoneCylinder.transform.position += worldOffset;
            if (debugPushingZoneCylinder != null)
                debugPushingZoneCylinder.transform.position += worldOffset;
        }

        public int GetCapturedCount() => capturedCount;
        public int GetRequiredCaptures() => requiredCaptures;
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

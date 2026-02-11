using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Roguelike;
using ForestMaze;

namespace FaeMaze.HeartPowers
{
    #region Murmuring Paths

    /// <summary>
    /// Affects all visitors on the targeted node or edge.
    /// Uses visitor pathfinding to find path to heart.
    /// Lures affected visitors toward the heart tile.
    /// Toggle power: expires when visitors consumed equals power tier.
    /// Visualizes with fairy ring style lights that trace paths to the heart from all affected positions.
    /// </summary>
    public class MurmuringPathsEffect : ConsumptionBasedPowerEffect
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
        /// The focal point where the power was activated (far end of the fog from heart).
        /// </summary>
        public Vector3 TargetPosition => targetPosition;

        /// <summary>
        /// The furthest tile position from the heart on the fog-affected path.
        /// This is the best spawn point for tutorial visitors that need to walk through the fog.
        /// </summary>
        public Vector3 FurthestPosition => furthestPosition;

        /// <summary>
        /// Returns the set of all edge indices affected by this fog effect.
        /// Used by the minimap to highlight affected edges.
        /// </summary>
        public HashSet<int> AffectedEdgeIndices => allAffectedEdgeIndices;

        /// <summary>
        /// Returns the set of all node indices affected by this fog effect.
        /// Used by the minimap to highlight affected nodes.
        /// </summary>
        public HashSet<int> AffectedNodeIndices => allAffectedNodeIndices;

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

            // Create power fog covering affected area (includes trigger collider for visitor detection)
            CreatePowerFog();
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

            // Replace auto-generated MeshCollider with a BoxCollider trigger for visitor detection
            var meshCol = fogQuad.GetComponent<Collider>();
            if (meshCol != null) Object.Destroy(meshCol);

            var boxCol = fogQuad.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(1f, 1f, 3f); // Z depth spans visitor capsule range
            boxCol.center = Vector3.zero;

            // Fog quad needs a kinematic Rigidbody for trigger events to fire
            var rb = fogQuad.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Attach trigger handler that calls back into this effect
            var trigger = fogQuad.AddComponent<MurmuringFogTrigger>();
            trigger.Initialize(this);

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
        /// Called by MurmuringFogTrigger when a visitor's collider enters/stays in the fog bounds.
        /// Validates the visitor is near an actual affected tile (not just in the bounding box)
        /// before luring them toward the heart.
        /// </summary>
        public void TryLureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null || hasExpired) return;

            if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                visitor.State == VisitorControllerBase.VisitorState.Escaping)
                return;

            // Already lured by this power instance
            if (affectedVisitors.Contains(visitor)) return;

            // Validate visitor is near an actual affected tile (not just in the fog bounding box)
            if (IsNearAffectedTile(visitor))
            {
                LureVisitorToHeart(visitor);
            }
        }

        /// <summary>
        /// Checks if a visitor is near any affected tile position.
        /// Uses the same tile positions that generate the fog visual, so detection matches what the player sees.
        /// </summary>
        private bool IsNearAffectedTile(VisitorControllerBase visitor)
        {
            Vector2 pos = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
            const float DETECTION_RADIUS_SQ = 1.5f * 1.5f; // Matches tileRadius in GeneratePathMaskTexture

            foreach (var tilePos in allAffectedTilePositions)
            {
                float dx = pos.x - tilePos.x;
                float dy = pos.y - tilePos.y;
                if (dx * dx + dy * dy <= DETECTION_RADIUS_SQ)
                    return true;
            }

            return false;
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

    /// <summary>
    /// Trigger component attached to the MurmuringPaths fog quad.
    /// Detects when visitor colliders enter the fog bounds and notifies the effect to lure them.
    /// Uses OnTriggerStay to also catch visitors already inside when the fog spawns.
    /// </summary>
    public class MurmuringFogTrigger : MonoBehaviour
    {
        private MurmuringPathsEffect effect;

        public void Initialize(MurmuringPathsEffect effect)
        {
            this.effect = effect;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryLure(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryLure(other);
        }

        private void TryLure(Collider other)
        {
            if (effect == null) return;

            // Only respond to visitor colliders (layer 6)
            if (other.gameObject.layer != 6) return;

            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;

            effect.TryLureVisitor(visitor);
        }
    }
}

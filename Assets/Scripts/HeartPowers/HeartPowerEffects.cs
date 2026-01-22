using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Utility methods shared across multiple heart power effects.
    /// </summary>
    public static class HeartPowerUtils
    {
        #region Visitor Detection

        /// <summary>
        /// Finds the first visitor within a radius of a position that is in an active state.
        /// Excludes visitors in Consumed, Escaping, Grabbed, or Dazed states.
        /// </summary>
        /// <param name="position">Center position to search from</param>
        /// <param name="radius">Search radius</param>
        /// <param name="excludeList">Optional list of visitors to exclude from search</param>
        /// <returns>First matching visitor or null</returns>
        public static VisitorControllerBase FindVisitorInRadius(Vector3 position, float radius, ICollection<VisitorControllerBase> excludeList = null)
        {
            var visitors = VisitorRegistry.All;
            Vector2 pos2D = new Vector2(position.x, position.y);

            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;
                if (!IsVisitorTargetable(visitor)) continue;
                if (excludeList != null && excludeList.Contains(visitor)) continue;

                Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                float distance = Vector2.Distance(visitorPos2D, pos2D);

                if (distance <= radius)
                {
                    return visitor;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds all visitors within a radius of a position that are in active states.
        /// </summary>
        public static List<VisitorControllerBase> FindAllVisitorsInRadius(Vector3 position, float radius, ICollection<VisitorControllerBase> excludeList = null)
        {
            var result = new List<VisitorControllerBase>();
            var visitors = VisitorRegistry.All;
            Vector2 pos2D = new Vector2(position.x, position.y);

            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;
                if (!IsVisitorTargetable(visitor)) continue;
                if (excludeList != null && excludeList.Contains(visitor)) continue;

                Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                float distance = Vector2.Distance(visitorPos2D, pos2D);

                if (distance <= radius)
                {
                    result.Add(visitor);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a visitor is in a state that can be targeted by powers.
        /// </summary>
        public static bool IsVisitorTargetable(VisitorControllerBase visitor)
        {
            if (visitor == null) return false;

            var state = visitor.State;
            return state != VisitorControllerBase.VisitorState.Consumed &&
                   state != VisitorControllerBase.VisitorState.Escaping &&
                   state != VisitorControllerBase.VisitorState.Grabbed;
        }

        #endregion

        #region Animator Control

        /// <summary>
        /// Sets an animator to a specific frame by calculating normalized time.
        /// Stops the animator and forces the frame update.
        /// </summary>
        /// <param name="animator">The animator to control</param>
        /// <param name="frame">Target frame number (0-based)</param>
        /// <param name="totalFrames">Total frames in the animation</param>
        /// <param name="stateName">Optional state name to play (uses current state if null)</param>
        public static void SetAnimatorFrame(Animator animator, int frame, int totalFrames, string stateName = null)
        {
            if (animator == null) return;

            // Clamp to 0.999 to avoid looping back to frame 0 when at last frame
            float normalizedTime = Mathf.Min(frame / (float)totalFrames, 0.999f);

            animator.speed = 0f;

            if (stateName != null)
            {
                animator.Play(stateName, 0, normalizedTime);
            }
            else
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                animator.Play(stateInfo.fullPathHash, 0, normalizedTime);
            }

            animator.Update(0f);
        }

        #endregion

        #region Tile Effects

        /// <summary>
        /// Applies a shake effect to a collection of game objects.
        /// </summary>
        /// <param name="objects">Objects to shake</param>
        /// <param name="originalPositions">Dictionary of original positions</param>
        /// <param name="intensity">Shake intensity (default 0.03)</param>
        public static void ApplyShakeEffect(IEnumerable<GameObject> objects, Dictionary<GameObject, Vector3> originalPositions, float intensity = 0.03f)
        {
            foreach (var obj in objects)
            {
                if (obj == null) continue;

                if (originalPositions.TryGetValue(obj, out Vector3 originalPos))
                {
                    float offsetX = (Random.value - 0.5f) * 2f * intensity;
                    float offsetY = (Random.value - 0.5f) * 2f * intensity;
                    obj.transform.position = originalPos + new Vector3(offsetX, offsetY, 0f);
                }
            }
        }

        /// <summary>
        /// Resets objects to their original positions.
        /// </summary>
        public static void ResetToOriginalPositions(IEnumerable<GameObject> objects, Dictionary<GameObject, Vector3> originalPositions)
        {
            foreach (var obj in objects)
            {
                if (obj != null && originalPositions.TryGetValue(obj, out Vector3 originalPos))
                {
                    obj.transform.position = originalPos;
                }
            }
        }

        /// <summary>
        /// Finds path/node tiles within a radius using physics overlap.
        /// </summary>
        /// <param name="center">Center position</param>
        /// <param name="radius">Search radius</param>
        /// <param name="tiles">Output list of found tiles</param>
        /// <param name="originalPositions">Output dictionary of original positions</param>
        public static void FindPathTilesInRadius(Vector3 center, float radius, List<GameObject> tiles, Dictionary<GameObject, Vector3> originalPositions)
        {
            tiles.Clear();
            originalPositions.Clear();

            Collider[] colliders = Physics.OverlapSphere(center, radius + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Vector2 center2D = new Vector2(center.x, center.y);

            foreach (var collider in colliders)
            {
                string objName = collider.gameObject.name;

                // Include path tiles: WorldTile_. (dot = path), WorldTile_H, WorldTile_N, etc. but NOT WorldTile_# (walls)
                bool isPathTile = objName.StartsWith("WorldTile_") && !objName.StartsWith("WorldTile_#");

                // Include node columns/cylinders
                bool isNode = collider.CompareTag("MazeNode") ||
                              objName.Contains("NodeColumn") ||
                              objName.Contains("NodeCylinder");

                if (isPathTile || isNode)
                {
                    Vector2 tilePos2D = new Vector2(collider.transform.position.x, collider.transform.position.y);
                    float distFromCenter = Vector2.Distance(tilePos2D, center2D);

                    if (distFromCenter <= radius)
                    {
                        tiles.Add(collider.gameObject);
                        originalPositions[collider.gameObject] = collider.transform.position;
                    }
                }
            }
        }

        /// <summary>
        /// Finds wall tiles within a radius using physics overlap.
        /// </summary>
        public static void FindWallTilesInRadius(Vector3 center, float radius, List<GameObject> walls, Dictionary<GameObject, Vector3> originalPositions)
        {
            walls.Clear();
            originalPositions.Clear();

            Collider[] colliders = Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                if (collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    walls.Add(collider.gameObject);
                    originalPositions[collider.gameObject] = collider.transform.position;
                }
            }
        }

        #endregion

        #region Visitor Visibility

        /// <summary>
        /// Sets the visibility of a visitor by enabling/disabling all renderers.
        /// </summary>
        public static void SetVisitorVisible(VisitorControllerBase visitor, bool visible)
        {
            if (visitor == null) return;

            var renderers = visitor.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }

        #endregion

        #region Particle System Helpers

        /// <summary>
        /// Creates a basic particle system with common settings.
        /// </summary>
        /// <param name="parent">Parent game object</param>
        /// <param name="name">Name of the particle system object</param>
        /// <param name="position">World position</param>
        /// <param name="emissionRate">Particles per second</param>
        /// <param name="startSize">Particle start size range</param>
        /// <param name="startSpeed">Particle start speed range</param>
        /// <param name="lifetime">Particle lifetime range</param>
        /// <param name="color1">First color for gradient</param>
        /// <param name="color2">Second color for gradient</param>
        /// <param name="shapeRadius">Emission shape radius</param>
        /// <returns>The created ParticleSystem</returns>
        public static ParticleSystem CreateBasicParticleSystem(
            GameObject parent,
            string name,
            Vector3 position,
            float emissionRate,
            Vector2 startSize,
            Vector2 startSpeed,
            Vector2 lifetime,
            Color color1,
            Color color2,
            float shapeRadius)
        {
            GameObject particleObj = new GameObject(name);
            particleObj.transform.SetParent(parent.transform);
            particleObj.transform.position = position;

            ParticleSystem particles = particleObj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeed.x, startSpeed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize.x, startSize.y);
            main.startColor = new ParticleSystem.MinMaxGradient(color1, color2);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            var emission = particles.emission;
            emission.rateOverTime = emissionRate;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = shapeRadius;

            // Size over lifetime - fade out
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Use default particle material
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            particles.Play();

            return particles;
        }

        /// <summary>
        /// Safely destroys a particle system and its game object.
        /// </summary>
        public static void DestroyParticleSystem(ref ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Stop();
                Object.Destroy(particles.gameObject);
                particles = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Static events for heart power effects that notify other systems when visitors are affected.
    /// </summary>
    public static class HeartPowerEvents
    {
        /// <summary>
        /// Invoked when a visitor is grabbed by HeartwardGrasp (Power 2).
        /// Parameter is the world position where the grab occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorGrabbedByGrasp;

        /// <summary>
        /// Invoked when a visitor is pushed/released by HeartwardGrasp (Power 2).
        /// Parameter is the world position where the push occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorPushedByGrasp;

        /// <summary>
        /// Invoked when a visitor is consumed by DevouringMaw (Power 3).
        /// Parameter is the world position where consumption occurred.
        /// </summary>
        public static event System.Action<Vector3> OnVisitorConsumedByMaw;

        /// <summary>
        /// Invoke the grab event from HeartwardGrasp.
        /// </summary>
        public static void NotifyVisitorGrabbedByGrasp(Vector3 position)
        {
            OnVisitorGrabbedByGrasp?.Invoke(position);
        }

        /// <summary>
        /// Invoke the push event from HeartwardGrasp.
        /// </summary>
        public static void NotifyVisitorPushedByGrasp(Vector3 position)
        {
            OnVisitorPushedByGrasp?.Invoke(position);
        }

        /// <summary>
        /// Invoke the consumption event from DevouringMaw.
        /// </summary>
        public static void NotifyVisitorConsumedByMaw(Vector3 position)
        {
            OnVisitorConsumedByMaw?.Invoke(position);
        }
    }

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
            manager.AddEssence(bonusEssence, EssenceSource.HeartPowerBonus, "Wisp delivery bonus");
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
        private const float GRASP_ZONE_RADIUS = 2.5f;
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
        private Vector3 pushingTargetPos;  // Target position after pushing (dynamically calculated)
        private Vector3 pushingDir;        // Direction of push (toward heart)
        private PushPhase pushPhase = PushPhase.Idle;
        private float pushPhaseStartTime = 0f;
        private int pushCurrentFrame = 24;  // Starts at end for reverse play
        private const float MIN_PUSH_DISTANCE = 1.0f;     // Minimum distance to push toward heart
        private const float MAX_PUSH_DISTANCE = 10.0f;    // Maximum push distance (safety limit)
        private const float PUSH_SPEED = 2.0f;            // Units per second during push
        private const float WITHDRAW_DURATION = 0.5f;     // Duration of withdraw translation
        private const float VISITOR_CHECK_RADIUS = 0.3f;  // Radius to check for visitor collision with walls/paths

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

            // Get heart node position
            if (manager.MazeGrid != null)
            {
                heartNodePosition = manager.MazeGrid.HeartWorldPosition;
            }

            // Find wall positions for both HGZs along the heart-to-focal ray
            FindWallPositions(targetPosition);

            // Create both HGZs
            CreateGrabbingHGZ();
            CreatePushingHGZ();

            // Create particle effects for both zones
            CreateParticleSystem(grabbingZoneObject, ref grabbingParticles);
            CreateParticleSystem(pushingZoneObject, ref pushingParticles);

            // Find and affect wall tiles in grabbing zone
            FindAffectedGrabbingWalls();
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

            // Sort hits by distance from ray origin (heart)
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

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
                    // Find closest wall on heart node border to the ray
                    Transform closestNodeWall = FindClosestNodeBorderWallToRay(heartPos2D, rayDirection);
                    if (closestNodeWall != null)
                    {
                        firstWallCenter = closestNodeWall.position;
                        pushingPos = new Vector2(firstWallCenter.x, firstWallCenter.y);
                    }
                }

                // For grabbing: place at wall center
                Vector2 grabbingPos = new Vector2(lastWallCenter.x, lastWallCenter.y);

                // Check if grabbing position is too far from focal point (ray missed edge border wall)
                float distFromFocal = Vector2.Distance(grabbingPos, focalPos2D);

                if (distFromFocal > MAX_GRABBING_DISTANCE_FROM_FOCAL)
                {
                    // Find closest wall to the focal point
                    Transform closestEdgeWall = FindClosestWallToPoint(focalPos2D, rayDirection);
                    if (closestEdgeWall != null)
                    {
                        lastWallCenter = closestEdgeWall.position;
                        grabbingPos = new Vector2(lastWallCenter.x, lastWallCenter.y);
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
            }
            else
            {
                // No walls hit along ray - find closest walls to heart and focal point
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
                }
                else
                {
                    // Default fallback - just use ray direction (radially outward from heart)
                    Vector2 pushingIntoForest = rayDir2D;  // Ray goes from heart to focal, so same direction
                    Vector2 pushingOffset = pushingIntoForest * HGZ_WALL_OFFSET;
                    pushingWallPos = new Vector3(rayOrigin.x + rayDirection.x * 3.5f + pushingOffset.x, rayOrigin.y + rayDirection.y * 3.5f + pushingOffset.y, -0.4f);
                    pushingWallNormal = rayDirection;
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
                }
                else
                {
                    // Default fallback - just use ray direction
                    Vector2 grabbingOffset = rayDir2D * HGZ_WALL_OFFSET;
                    grabbingWallPos = new Vector3(focalPos3D.x + grabbingOffset.x, focalPos3D.y + grabbingOffset.y, -0.4f);
                    grabbingWallNormal = -rayDirection;
                }
            }
        }

        private Transform FindClosestNodeBorderWallToRay(Vector2 heartPos, Vector3 rayDir)
        {
            // Find all wall models near the heart node border (NodeWalls_0 contains heart node walls)
            GameObject nodeWallsContainer = GameObject.Find("NodeWalls_0");
            if (nodeWallsContainer == null)
            {
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
                return awayFromHeart;
            }

            // Wall's transform.right actually points AWAY from path (into forest)
            // So the average IS the "into forest" direction
            Vector2 avgNormal = (sumNormals / wallCount).normalized;
            Vector2 intoForest = avgNormal;  // Not negated - walls point into forest

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


            // Check if there are any path or node tiles near the proposed position
            Collider[] nearbyColliders = Physics.OverlapSphere(proposedPos, CHECK_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);


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


            // If we're too close to a path/node edge, push further in the offset direction
            if (closestEdgeDist < minEdgeDistance)
            {
                float additionalOffset = minEdgeDistance - closestEdgeDist + 0.5f;  // Add extra margin
                Vector3 correctedPos = proposedPos + new Vector3(offsetDir.x, offsetDir.y, 0f) * additionalOffset;
                return correctedPos;
            }

            return proposedPos;
        }

        private void FindAffectedGrabbingWalls()
        {
            HeartPowerUtils.FindWallTilesInRadius(grabbingWallPos, GRASP_ZONE_RADIUS, affectedGrabbingWalls, originalWallPositions);
        }

        private void UpdateWallShakeEffect()
        {
            if (affectedGrabbingWalls.Count == 0) return;

            // Only shake when in idle/reaching phase (waiting for or grabbing visitor)
            bool shouldShake = grabPhase == GrabPhase.Idle || grabPhase == GrabPhase.Reaching || grabPhase == GrabPhase.Grabbing;

            if (shouldShake)
            {
                HeartPowerUtils.ApplyShakeEffect(affectedGrabbingWalls, originalWallPositions, 0.03f);
            }
            else
            {
                HeartPowerUtils.ResetToOriginalPositions(affectedGrabbingWalls, originalWallPositions);
            }
        }

        private void ResetWallPositions()
        {
            HeartPowerUtils.ResetToOriginalPositions(affectedGrabbingWalls, originalWallPositions);
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
        }

        private void SpawnGraspVisual(GameObject parent, Vector3 position, Vector3 forwardDir, ref GameObject visual, ref Animator animator, ref SphereCollider touchCollider, string name)
        {
            GameObject graspPrefab = manager.GraspPrefab;
            if (graspPrefab == null)
            {
                Debug.LogWarning("[HeartwardGrasp] Grasp prefab not assigned on HeartPowerManager");
                return;
            }

            // The grasp prefab's default orientation (with its baked-in rotation) has:
            // - Palm facing camera (-Z direction)
            // - Fingers pointing +X direction
            // This is correct for this game's coordinate system (XY plane, -Z up).
            // We only need to rotate around Z to point fingers toward the target direction.
            float angle = Mathf.Atan2(forwardDir.y, forwardDir.x) * Mathf.Rad2Deg;
            Quaternion finalRotation = Quaternion.Euler(0f, 0f, angle);


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

                if (controller != null)
                {
                    // Keep animator enabled but paused at frame 0
                    animator.speed = 0f;
                    animator.Play("GraspFinal", 0, 0f);
                    animator.Update(0f);

                    // Verify the state was set
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                }
                else
                {
                }
            }
            else
            {
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
                                    break;
                                }
                            }

                        }

                        if (touchedVisitor)
                        {
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

                            // Deduct essence cost from the visitor for being grabbed
                            const float GRAB_ESSENCE_COST = 25f;
                            currentVisitor.DeductEssence(GRAB_ESSENCE_COST);

                            // Notify nearby visitors that a grab occurred - they become frightened
                            HeartPowerEvents.NotifyVisitorGrabbedByGrasp(currentVisitor.transform.position);
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
                        // Position visitor in front of pushing hand, along the push direction (toward heart)
                        // The visitor should be in contact with the touch collider and ahead of the hand
                        Vector3 pushingDir = (heartNodePosition - pushingWallPos).normalized;

                        // Offset distance: place visitor slightly in front of hand (along push direction)
                        // Use a small offset so visitor is in contact with touch collider
                        float forwardOffset = 0.3f;  // Distance in front of hand along push axis
                        visitorPushOffset = pushingDir * forwardOffset;
                        visitorPushOffset.z = visitorGrabOffset.z;  // Preserve Z offset from grab

                        Vector3 newVisitorPos = pushingWallPos + visitorPushOffset;
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
            pushingDir = (heartNodePosition - pushingWallPos).normalized;
            // Initial target is minimum distance, but will extend dynamically if visitor still in walls
            pushingTargetPos = pushingStartPos + pushingDir * MIN_PUSH_DISTANCE;

            // Start with Pushing phase (translate toward heart)
            pushPhase = PushPhase.Pushing;
            pushPhaseStartTime = elapsedTime;
            pushCurrentFrame = GRAB_ANIMATION_FRAMES;

            if (pushingParticles != null) pushingParticles.Emit(25);

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
                    // Model translates toward heart at constant speed until visitor is clear of walls and on path
                    {
                        // Keep hand closed at frame 24
                        SetAnimatorFrame(pushingAnimator, GRAB_ANIMATION_FRAMES);

                        // Move at constant speed toward heart
                        Vector3 currentPos = pushingVisual.transform.position;
                        Vector3 newPos = currentPos + pushingDir * PUSH_SPEED * deltaTime;

                        // Calculate how far we've traveled
                        float distanceTraveled = Vector3.Distance(pushingStartPos, newPos);

                        // Check if we've hit max distance (safety limit)
                        if (distanceTraveled >= MAX_PUSH_DISTANCE)
                        {
                            newPos = pushingStartPos + pushingDir * MAX_PUSH_DISTANCE;
                            pushingTargetPos = newPos;
                            pushingVisual.transform.position = newPos;
                            // Update touch collider and visitor positions
                            UpdatePushPositions(newPos);
                            // Force transition to releasing
                            pushPhase = PushPhase.Releasing;
                            pushPhaseStartTime = elapsedTime;
                            break;
                        }

                        // Update positions
                        pushingVisual.transform.position = newPos;
                        UpdatePushPositions(newPos);

                        // Check if visitor is clear of walls and on a valid walkable area
                        // Only check after minimum distance traveled
                        if (distanceTraveled >= MIN_PUSH_DISTANCE)
                        {
                            Vector3 visitorPos = currentVisitor.transform.position;
                            if (IsVisitorOnValidWalkableArea(visitorPos))
                            {
                                pushingTargetPos = newPos;
                                pushPhase = PushPhase.Releasing;
                                pushPhaseStartTime = elapsedTime;
                            }
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

                            // Notify nearby visitors that a push/release occurred - they become frightened
                            HeartPowerEvents.NotifyVisitorPushedByGrasp(currentVisitor.transform.position);
                        }

                        if (releaseProgress >= 1f)
                        {
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
                currentVisitor.Resume();
                currentVisitor.RecalculatePath();
            }

            capturedCount++;

            if (capturedCount >= requiredCaptures)
            {
                hasExpired = true;
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

        /// <summary>
        /// Updates touch collider and visitor positions during push phase.
        /// </summary>
        private void UpdatePushPositions(Vector3 handPos)
        {
            if (pushingTouchCollider != null)
            {
                pushingTouchCollider.transform.position = new Vector3(handPos.x, handPos.y, -0.3f);
            }

            if (currentVisitor != null)
            {
                currentVisitor.transform.position = handPos + visitorPushOffset;
            }
        }

        /// <summary>
        /// Checks if the visitor position is on a valid walkable area (path or node tile)
        /// and NOT colliding with any wall tiles.
        /// </summary>
        private bool IsVisitorOnValidWalkableArea(Vector3 visitorPos)
        {
            // Use XY position for 2D check (this game uses XY as ground plane)
            Vector3 checkPos = new Vector3(visitorPos.x, visitorPos.y, 0f);

            // Check for collisions at visitor position
            Collider[] hits = Physics.OverlapSphere(checkPos, VISITOR_CHECK_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            bool onWalkableTile = false;
            bool touchingWall = false;

            foreach (var hit in hits)
            {
                string objName = hit.gameObject.name;

                // Check for wall tiles (WorldTile_#)
                if (objName.StartsWith("WorldTile_#"))
                {
                    touchingWall = true;
                }

                // Check for path tiles (MazePath tag or PathTile/WorldTile_ without #)
                if (hit.CompareTag("MazePath") ||
                    objName.StartsWith("PathTile") ||
                    (objName.StartsWith("WorldTile_") && !objName.StartsWith("WorldTile_#")))
                {
                    onWalkableTile = true;
                }

                // Check for node tiles (MazeNode tag or NodeColumn/NodeCylinder)
                if (hit.CompareTag("MazeNode") ||
                    objName.StartsWith("NodeColumn") ||
                    objName.StartsWith("NodeCylinder"))
                {
                    onWalkableTile = true;
                }
            }

            // Visitor is valid if on a walkable tile AND not touching any walls
            return onWalkableTile && !touchingWall;
        }

        private void SetAnimatorFrame(Animator animator, int frame)
        {
            HeartPowerUtils.SetAnimatorFrame(animator, frame, GRAB_ANIMATION_FRAMES);
        }

        private void SetVisitorVisible(VisitorControllerBase visitor, bool visible)
        {
            HeartPowerUtils.SetVisitorVisible(visitor, visible);
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
        }

        public int GetCapturedCount() => capturedCount;
        public int GetRequiredCaptures() => requiredCaptures;
    }

    #endregion

    #region Devouring Maw

    /// <summary>
    /// Duration/cooldown power that creates a trigger zone to detect and devour visitors.
    /// When active, path tiles in the area shake with particles and fog effects.
    /// Visitors entering the zone are devoured sequentially with a 0.25s delay between each.
    /// </summary>
    public class DevouringMawEffect : ActivePowerEffect
    {
        // Constants
        private const float TRIGGER_RADIUS = 2.5f;
        private const float DEVOUR_CYCLE_DELAY = 0.25f;
        private const float EMERGE_DURATION = 1.04f; // 25 frames at 24fps = ~1.04 seconds for full bite animation
        private const float PAUSE_DURATION = 1.0f; // Full second pause for visibility
        private const float SINK_DURATION = 0.5f;
        private const float SHAKE_INTENSITY = 0.03f;
        private const float FOG_Z_POSITION = -0.2f;
        private const float PARTICLE_Z_MIN = -0.5f;
        private const float PARTICLE_Z_MAX = 0f;

        private enum DevourPhase
        {
            Idle,           // Waiting for visitor to enter trigger zone
            Emerging,       // Prefab translating from z=0 to z=-0.5
            Paused,         // Prefab at z=-0.5, waiting
            Sinking,        // Prefab and visitors translating to z=1
            Complete        // Visitors devoured, reset for next cycle
        }

        // Power state
        private Vector3 targetWorldPos;
        private float powerDuration;
        private bool cycleInProgress;
        private float cycleStartTime;
        private float lastCycleEndTime;

        // Animation constants
        private const string DEVOUR_ANIMATION_NAME = "FaceRigAction";
        private const int DEVOUR_ANIMATION_FRAMES = 62;  // Total frames in animation (1-62 at 60fps)

        // Current devour cycle state
        private DevourPhase currentPhase;
        private float phaseStartTime;
        private GameObject devourVisual;
        private Animator devourAnimator;
        private Vector3 devourBasePosition;
        private List<VisitorControllerBase> visitorsBeingDevoured = new List<VisitorControllerBase>();
        private Dictionary<VisitorControllerBase, Vector3> visitorStartPositions = new Dictionary<VisitorControllerBase, Vector3>();

        // Affected tiles for shake effect
        private List<GameObject> affectedPathTiles = new List<GameObject>();
        private Dictionary<GameObject, Vector3> originalTilePositions = new Dictionary<GameObject, Vector3>();

        // Visual effects
        private GameObject visualContainer;
        private ParticleSystem areaParticles;
        private Color pathSkinColor = new Color(0.55f, 0.27f, 0.07f, 1f); // Default saddle brown
        private Color[] skinColors = new Color[3]; // Multiple colors sampled from skin texture
        private GameObject fogQuad;
        private Material fogMaterial;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        public override void OnStart()
        {
            targetWorldPos = targetPosition;
            // Duration equals cooldown for this power
            powerDuration = definition.cooldown > 0 ? definition.cooldown : 10f;
            cycleInProgress = false;
            lastCycleEndTime = 0f;

            // Get path skin color from MazeRenderer if available
            ExtractPathSkinColor();

            // Create visual container
            visualContainer = new GameObject("DevourEffectContainer");
            visualContainer.transform.position = targetWorldPos;

            // Find affected path tiles in the trigger area
            FindAffectedPathTiles();

            // Create circular fog effect covering the trigger area
            CreateFogEffect();

            // Create particle effect
            CreateParticleEffect();

            // Add tile visualizer effect
            if (manager.TileVisualizer != null)
            {
                manager.TileVisualizer.AddTileEffectAtWorldPos(targetWorldPos, HeartPowerType.DevouringMaw, 1.0f, powerDuration);
            }

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

            // Update visual effects
            UpdateTileShake();

            // Check if we should start a new devour cycle
            if (!cycleInProgress)
            {
                // Check for visitors in trigger zone
                var visitor = FindVisitorInTriggerZone();
                if (visitor != null && (elapsedTime - lastCycleEndTime) >= DEVOUR_CYCLE_DELAY)
                {
                    StartDevourCycle(visitor);
                }
            }
            else
            {
                // Update current devour cycle
                UpdateDevourCycle();
            }
        }

        public override bool IsExpired
        {
            get
            {
                // Extend duration if a cycle is still in progress
                if (cycleInProgress)
                {
                    return false;
                }

                return elapsedTime >= powerDuration;
            }
        }

        public override void OnEnd()
        {
            // Clean up any in-progress devour
            if (devourVisual != null)
            {
                Object.Destroy(devourVisual);
                devourVisual = null;
            }
            devourAnimator = null;

            // Release any visitors being devoured
            foreach (var visitor in visitorsBeingDevoured)
            {
                if (visitor != null)
                {
                    visitor.Resume();
                }
            }
            visitorsBeingDevoured.Clear();
            visitorStartPositions.Clear();

            // Reset tile positions
            ResetTilePositions();

            // Clean up visual effects
            if (areaParticles != null)
            {
                areaParticles.Stop();
                Object.Destroy(areaParticles.gameObject);
                areaParticles = null;
            }

            if (dustParticles != null)
            {
                dustParticles.Stop();
                Object.Destroy(dustParticles.gameObject);
                dustParticles = null;
            }

            if (fogQuad != null)
            {
                Object.Destroy(fogQuad);
                fogQuad = null;
            }

            if (fogMaterial != null)
            {
                Object.Destroy(fogMaterial);
                fogMaterial = null;
            }

            if (visualContainer != null)
            {
                Object.Destroy(visualContainer);
                visualContainer = null;
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
            devourBasePosition += worldOffset;

            if (devourVisual != null)
            {
                devourVisual.transform.position += worldOffset;
            }

            if (visualContainer != null)
            {
                visualContainer.transform.position += worldOffset;
            }

            // Update original tile positions
            var updatedPositions = new Dictionary<GameObject, Vector3>();
            foreach (var kvp in originalTilePositions)
            {
                updatedPositions[kvp.Key] = kvp.Value + worldOffset;
            }
            originalTilePositions = updatedPositions;

            // Update visitor start positions
            var updatedVisitorPositions = new Dictionary<VisitorControllerBase, Vector3>();
            foreach (var kvp in visitorStartPositions)
            {
                updatedVisitorPositions[kvp.Key] = kvp.Value + worldOffset;
            }
            visitorStartPositions = updatedVisitorPositions;
        }

        private Texture2D LoadEarthenGroundTexture()
        {
            Texture2D texture = null;

            #if UNITY_EDITOR
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/EarthenGroundTexture.png");
            #endif

            return texture;
        }

        private void ExtractPathSkinColor()
        {
            // Load EarthenGroundTexture and sample 3 different circular regions
            Texture2D earthenTexture = LoadEarthenGroundTexture();

            if (earthenTexture != null && earthenTexture.isReadable)
            {
                int sampleRadius = Mathf.Min(8, earthenTexture.width / 8, earthenTexture.height / 8);

                // Sample 3 different random circular regions for color variation
                for (int colorIndex = 0; colorIndex < 3; colorIndex++)
                {
                    int centerX = UnityEngine.Random.Range(sampleRadius, earthenTexture.width - sampleRadius);
                    int centerY = UnityEngine.Random.Range(sampleRadius, earthenTexture.height - sampleRadius);

                    Color avgColor = Color.black;
                    int sampleCount = 0;

                    for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
                    {
                        for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
                        {
                            if (dx * dx + dy * dy <= sampleRadius * sampleRadius)
                            {
                                Color pixel = earthenTexture.GetPixel(centerX + dx, centerY + dy);
                                avgColor += pixel;
                                sampleCount++;
                            }
                        }
                    }

                    if (sampleCount > 0)
                    {
                        skinColors[colorIndex] = avgColor / sampleCount;
                    }
                    else
                    {
                        skinColors[colorIndex] = new Color(0.55f, 0.47f, 0.42f, 1f);
                    }
                }

                pathSkinColor = skinColors[0];
                return;
            }

            // Fallback: try to get texture from NodeColumn material
            Collider[] colliders = Physics.OverlapSphere(targetWorldPos, TRIGGER_RADIUS * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            foreach (var collider in colliders)
            {
                if (collider.gameObject.name.StartsWith("NodeColumn"))
                {
                    var renderer = collider.GetComponent<Renderer>();
                    if (renderer != null && renderer.sharedMaterial != null)
                    {
                        Material mat = renderer.sharedMaterial;

                        // Try to get the main texture and sample from it
                        if (mat.mainTexture != null && mat.mainTexture is Texture2D tex && tex.isReadable)
                        {
                            int sampleRadius = Mathf.Min(8, tex.width / 8, tex.height / 8);

                            for (int colorIndex = 0; colorIndex < 3; colorIndex++)
                            {
                                int centerX = UnityEngine.Random.Range(sampleRadius, tex.width - sampleRadius);
                                int centerY = UnityEngine.Random.Range(sampleRadius, tex.height - sampleRadius);

                                Color avgColor = Color.black;
                                int sampleCount = 0;

                                for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
                                {
                                    for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
                                    {
                                        if (dx * dx + dy * dy <= sampleRadius * sampleRadius)
                                        {
                                            Color pixel = tex.GetPixel(centerX + dx, centerY + dy);
                                            avgColor += pixel;
                                            sampleCount++;
                                        }
                                    }
                                }

                                skinColors[colorIndex] = sampleCount > 0 ? avgColor / sampleCount : new Color(0.55f, 0.47f, 0.42f, 1f);
                            }

                            pathSkinColor = skinColors[0];
                            return;
                        }

                        // Fall back to shader color properties with variations
                        if (mat.HasProperty("_MidTone"))
                        {
                            skinColors[0] = mat.HasProperty("_DarkBase") ? mat.GetColor("_DarkBase") : mat.GetColor("_MidTone") * 0.8f;
                            skinColors[1] = mat.GetColor("_MidTone");
                            skinColors[2] = mat.HasProperty("_LightMid") ? mat.GetColor("_LightMid") : mat.GetColor("_MidTone") * 1.2f;
                            pathSkinColor = skinColors[1];
                            return;
                        }
                    }
                }
            }

            // Final fallback: use default earthy browns with variation
            skinColors[0] = new Color(0.45f, 0.38f, 0.33f, 1f);
            skinColors[1] = new Color(0.55f, 0.47f, 0.42f, 1f);
            skinColors[2] = new Color(0.62f, 0.55f, 0.50f, 1f);
            pathSkinColor = skinColors[1];
        }

        private void FindAffectedPathTiles()
        {
            HeartPowerUtils.FindPathTilesInRadius(targetWorldPos, TRIGGER_RADIUS, affectedPathTiles, originalTilePositions);
        }

        private void CreateFogEffect()
        {
            // Create a circular fog quad using a simple transparent material
            CreateCircularFogQuad();

            // Create dust particles with reduced size (90% smaller) and color variation
            CreateDustParticles();
        }

        private ParticleSystem dustParticles;

        private void CreateCircularFogQuad()
        {
            // Create a circular mesh for the fog
            fogQuad = new GameObject("DevourFogCircle");
            fogQuad.transform.SetParent(visualContainer.transform);
            fogQuad.transform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, FOG_Z_POSITION);

            MeshFilter meshFilter = fogQuad.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = fogQuad.AddComponent<MeshRenderer>();

            // Create circular mesh
            int segments = 32;
            Mesh mesh = new Mesh();
            mesh.name = "CircleFogMesh";

            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * TRIGGER_RADIUS;
                float y = Mathf.Sin(angle) * TRIGGER_RADIUS;

                vertices[i + 1] = new Vector3(x, y, 0f);
                uvs[i + 1] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);

                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;

            // Create fog material using DevourDust shader for billowing dust effect
            Shader fogShader = Shader.Find("Custom/DevourDust");
            if (fogShader == null)
            {
                Debug.LogWarning("[DevouringMaw] DevourDust shader not found, using fallback");
                fogShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (fogShader == null) fogShader = Shader.Find("Unlit/Color");
            }

            fogMaterial = new Material(fogShader);

            // Assign EarthenGroundTexture for the dust effect
            Texture2D earthenTexture = LoadEarthenGroundTexture();
            if (earthenTexture != null)
            {
                fogMaterial.SetTexture("_MainTex", earthenTexture);
            }

            // Set overall alpha
            fogMaterial.SetFloat("_Alpha", 0.75f);

            // Set cloud parameters for churning dust effect
            fogMaterial.SetFloat("_CloudScale", 6.0f);
            fogMaterial.SetFloat("_CloudDetail", 2.5f);
            fogMaterial.SetFloat("_CloudDensity", 1.8f);
            fogMaterial.SetFloat("_CloudSharpness", 2.5f);

            // Animation speeds for dynamic dust
            fogMaterial.SetFloat("_WindSpeed", 0.25f);
            fogMaterial.SetFloat("_TurbulenceSpeed", 0.6f);
            fogMaterial.SetFloat("_TextureScrollSpeed", 0.08f);

            // Soft edge fade
            fogMaterial.SetFloat("_EdgeFade", 0.25f);

            meshRenderer.material = fogMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private void CreateDustParticles()
        {
            GameObject dustObj = new GameObject("DevourDustParticles");
            dustObj.transform.position = targetWorldPos;
            dustObj.transform.SetParent(visualContainer.transform);

            dustParticles = dustObj.AddComponent<ParticleSystem>();
            dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dustParticles.main;
            main.loop = true;
            main.startLifetime = 2.0f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.05f); // Reduced speed
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f); // 90% smaller (was 0.3-0.6)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            main.playOnAwake = false;

            // Use gradient for color variation between the 3 sampled skin colors
            var colorBySpeed = dustParticles.colorBySpeed;
            colorBySpeed.enabled = false;

            // Randomize start color between the 3 skin colors
            Gradient startGradient = new Gradient();
            startGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(skinColors[0], 0f),
                    new GradientColorKey(skinColors[1], 0.5f),
                    new GradientColorKey(skinColors[2], 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(startGradient);

            var emission = dustParticles.emission;
            emission.rateOverTime = 60f;

            // Circular emission shape
            var shape = dustParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = TRIGGER_RADIUS;
            shape.radiusThickness = 1f;
            shape.position = new Vector3(0f, 0f, -0.25f);

            var sizeOverLifetime = dustParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0.2f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = dustParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient alphaGradient = new Gradient();
            alphaGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = alphaGradient;

            // Noise for billowing effect
            var noise = dustParticles.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.3f;

            var renderer = dustObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");
            if (particleShader != null)
            {
                Material dustMat = new Material(particleShader);
                dustMat.SetColor("_BaseColor", Color.white); // Use white since particles have their own color
                renderer.material = dustMat;
            }

            dustParticles.Play();
        }

        private void CreateParticleEffect()
        {
            GameObject particleObj = new GameObject("DevourParticles");
            particleObj.transform.position = targetWorldPos;
            particleObj.transform.SetParent(visualContainer.transform);

            areaParticles = particleObj.AddComponent<ParticleSystem>();
            areaParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = areaParticles.main;
            main.loop = true;
            main.startLifetime = 1.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.12f); // 0.1 size as specified
            main.startColor = pathSkinColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            main.playOnAwake = false;

            var emission = areaParticles.emission;
            emission.rateOverTime = 20f;

            // Use circle shape for circular emission area
            var shape = areaParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = TRIGGER_RADIUS;
            shape.radiusThickness = 1f; // Emit from entire circle area, not just edge
            // Position in the z range between -0.5 and 0
            shape.position = new Vector3(0f, 0f, (PARTICLE_Z_MIN + PARTICLE_Z_MAX) / 2f);

            var sizeOverLifetime = areaParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = areaParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(pathSkinColor, 0f), new GradientColorKey(pathSkinColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");
            if (particleShader != null)
            {
                renderer.material = new Material(particleShader);
                renderer.material.SetColor("_BaseColor", pathSkinColor);
            }

            areaParticles.Play();
        }

        private void UpdateTileShake()
        {
            HeartPowerUtils.ApplyShakeEffect(affectedPathTiles, originalTilePositions, SHAKE_INTENSITY);
        }

        private void ResetTilePositions()
        {
            HeartPowerUtils.ResetToOriginalPositions(affectedPathTiles, originalTilePositions);
        }


        private VisitorControllerBase FindVisitorInTriggerZone()
        {
            return HeartPowerUtils.FindVisitorInRadius(targetWorldPos, TRIGGER_RADIUS, visitorsBeingDevoured);
        }

        private void StartDevourCycle(VisitorControllerBase triggeringVisitor)
        {
            cycleInProgress = true;
            cycleStartTime = elapsedTime;
            currentPhase = DevourPhase.Emerging;
            phaseStartTime = elapsedTime;

            // Find all visitors at this location
            visitorsBeingDevoured.Clear();
            visitorStartPositions.Clear();

            Vector3 devourLocation = triggeringVisitor.transform.position;
            devourLocation.z = 0f;

            // Find all visitors near the triggering visitor's location
            var visitors = VisitorRegistry.All;
            foreach (var visitor in visitors)
            {
                if (visitor == null) continue;

                if (visitor.State == VisitorControllerBase.VisitorState.Consumed ||
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
                {
                    continue;
                }

                Vector2 visitorPos2D = new Vector2(visitor.transform.position.x, visitor.transform.position.y);
                Vector2 devourPos2D = new Vector2(devourLocation.x, devourLocation.y);

                if (Vector2.Distance(visitorPos2D, devourPos2D) <= 1.0f)
                {
                    visitor.Stop();
                    visitorsBeingDevoured.Add(visitor);
                    visitorStartPositions[visitor] = visitor.transform.position;
                }
            }

            // Spawn devour prefab at z=0 (first frame of animation)
            InstantiateDevourVisual(devourLocation);
        }

        private void UpdateDevourCycle()
        {
            float phaseElapsed = elapsedTime - phaseStartTime;

            switch (currentPhase)
            {
                case DevourPhase.Emerging:
                    // Translate prefab from z=0 to z=-0.5 while playing full bite animation (frames 1→25)
                    float emergeT = Mathf.Clamp01(phaseElapsed / EMERGE_DURATION);
                    if (devourVisual != null)
                    {
                        Vector3 pos = devourBasePosition;
                        pos.z = Mathf.Lerp(0f, -0.5f, emergeT);
                        devourVisual.transform.position = pos;
                    }

                    // Play bite animation (frames 1→25)
                    int emergeFrame = 1 + Mathf.FloorToInt(emergeT * (DEVOUR_ANIMATION_FRAMES - 1));
                    SetDevourAnimatorFrame(emergeFrame);

                    if (emergeT >= 1f)
                    {
                        currentPhase = DevourPhase.Paused;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case DevourPhase.Paused:
                    // Hold at z=-0.5 for PAUSE_DURATION, then sink along +z
                    // Hold last frame of animation (mouth closed after bite)
                    if (devourVisual != null)
                    {
                        Vector3 pausePos = devourBasePosition;
                        pausePos.z = -0.5f;
                        devourVisual.transform.position = pausePos;
                    }

                    // Hold at last frame (closed after bite)
                    SetDevourAnimatorFrame(DEVOUR_ANIMATION_FRAMES);

                    if (phaseElapsed >= PAUSE_DURATION)
                    {
                        currentPhase = DevourPhase.Sinking;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case DevourPhase.Sinking:
                    // Translate prefab and visitors along +z (from -0.5 to 1.0)
                    // Hold last frame of animation (mouth stays closed)
                    float sinkT = Mathf.Clamp01(phaseElapsed / SINK_DURATION);

                    float devourZ = Mathf.Lerp(-0.5f, 1f, sinkT);

                    if (devourVisual != null)
                    {
                        Vector3 pos = devourBasePosition;
                        pos.z = devourZ;
                        devourVisual.transform.position = pos;
                    }

                    // Hold at last frame (closed mouth)
                    SetDevourAnimatorFrame(DEVOUR_ANIMATION_FRAMES);

                    // Move visitors in +z direction in tandem with the devour model
                    foreach (var visitor in visitorsBeingDevoured)
                    {
                        if (visitor != null && visitorStartPositions.TryGetValue(visitor, out Vector3 startPos))
                        {
                            Vector3 visitorPos = startPos;
                            visitorPos.z = Mathf.Lerp(startPos.z, 1f, sinkT);
                            visitor.transform.position = visitorPos;
                        }
                    }

                    if (sinkT >= 1f)
                    {
                        // Devour all visitors
                        foreach (var visitor in visitorsBeingDevoured)
                        {
                            if (visitor != null)
                            {
                                ConsumeVisitor(visitor);
                            }
                        }

                        // Tier III bonus
                        if (definition.tier >= 3)
                        {
                            ApplySoulHarvest();
                        }

                        currentPhase = DevourPhase.Complete;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case DevourPhase.Complete:
                    // Clean up and reset for next cycle
                    if (devourVisual != null)
                    {
                        Object.Destroy(devourVisual);
                        devourVisual = null;
                    }
                    devourAnimator = null;

                    visitorsBeingDevoured.Clear();
                    visitorStartPositions.Clear();
                    cycleInProgress = false;
                    lastCycleEndTime = elapsedTime;
                    break;
            }
        }

        private void SetDevourAnimatorFrame(int frame)
        {
            HeartPowerUtils.SetAnimatorFrame(devourAnimator, frame, DEVOUR_ANIMATION_FRAMES, DEVOUR_ANIMATION_NAME);
        }

        private void InstantiateDevourVisual(Vector3 position)
        {
            GameObject devourPrefab = manager.DevourPrefab;

            if (devourPrefab == null)
            {
                Debug.LogWarning("[DevouringMaw] Devour prefab not assigned on HeartPowerManager");
                return;
            }

            Vector3 worldPos = position;
            worldPos.z = 0f; // Start at z=0

            // Calculate rotation so model Y axis points toward the focal point (targetWorldPos)
            // In the XY plane, rotate around Z axis
            Vector2 toFocal = new Vector2(targetWorldPos.x - worldPos.x, targetWorldPos.y - worldPos.y);
            float angle = Mathf.Atan2(toFocal.y, toFocal.x) * Mathf.Rad2Deg - 90f; // -90 because Y axis should point to focal
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            devourVisual = Object.Instantiate(devourPrefab, worldPos, rotation);
            devourBasePosition = new Vector3(worldPos.x, worldPos.y, 0f);

            // Get animator and set up for frame-based control
            devourAnimator = devourVisual.GetComponent<Animator>();

            if (devourAnimator == null)
            {
                devourAnimator = devourVisual.GetComponentInChildren<Animator>();
            }

            if (devourAnimator != null)
            {
                var controller = devourAnimator.runtimeAnimatorController;

                // Load controller if not assigned
                if (controller == null)
                {
#if UNITY_EDITOR
                    controller = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Devour/devour.controller");
                    if (controller != null)
                    {
                        devourAnimator.runtimeAnimatorController = controller;
                    }
#endif
                }

                if (controller != null)
                {
                    // Start at frame 1 (normalized time ~0.04 for 25 frames)
                    float startNormalized = 1f / DEVOUR_ANIMATION_FRAMES;
                    devourAnimator.speed = 0f;
                    devourAnimator.Play(DEVOUR_ANIMATION_NAME, 0, startNormalized);
                    devourAnimator.Update(0f);
                }
                else
                {
                    Debug.LogWarning("[DevouringMaw] Animator has no RuntimeAnimatorController and could not load one");
                }
            }
            else
            {
                Debug.LogWarning("[DevouringMaw] No Animator found on devour prefab");
            }

            // Fix MawThroat mesh rendering
            SetDoubleSidedRendering(devourVisual);
        }

        private void SetDoubleSidedRendering(GameObject obj)
        {
            if (obj == null) return;

            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.materials)
                {
                    mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

                    if (mat.HasProperty("_CullMode"))
                    {
                        mat.SetFloat("_CullMode", 0f);
                    }
                    if (mat.HasProperty("_DoubleSidedEnable"))
                    {
                        mat.SetFloat("_DoubleSidedEnable", 1f);
                    }
                }
            }
        }

        private void ConsumeVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Capture position before destroying
            Vector3 consumptionPosition = visitor.transform.position;

            // Award 0.5 * essence value as specified
            int baseEssence = visitor.GetEssenceReward();
            int essence = Mathf.RoundToInt(baseEssence * 0.5f);

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(essence, EssenceSource.VisitorConsumedByMaw, $"50% of {baseEssence}");
            }
            else
            {
                Debug.LogWarning("[DevouringMaw] GameController is null, cannot add essence");
            }

            // Track visitor fate with essence value
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, Systems.VisitorFate.Devoured, essence);
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Notify nearby visitors that consumption occurred - they become frightened
            HeartPowerEvents.NotifyVisitorConsumedByMaw(consumptionPosition);

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
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
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
                    visitor.State == VisitorControllerBase.VisitorState.Escaping)
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
                manager.GameController.AddEssence(bonusEssence, EssenceSource.HeartPowerBonus, "Soul Harvest");
            }
        }
    }

    #endregion

    #region Sculpting

    /// <summary>
    /// Sculpting power allows the player to change the prop type of a node.
    /// When activated on a node, presents a circular menu with options:
    /// - Center: Cancel (red)
    /// - Top: Remove prop (earth texture color)
    /// - Left: Pond
    /// - Bottom: Fae Lantern
    /// - Right: Fairy Ring
    /// Only works when focal point is on a node, not an edge.
    /// </summary>
    public class SculptingEffect : ActivePowerEffect
    {
        // Menu state
        private bool menuActive = false;
        private int targetNodeIndex = -1;
        private Vector3 menuPosition;

        // UI elements
        private GameObject menuContainer;
        private Canvas menuCanvas;
        private UnityEngine.UI.Button centerButton;
        private UnityEngine.UI.Button topButton;
        private UnityEngine.UI.Button leftButton;
        private UnityEngine.UI.Button bottomButton;
        private UnityEngine.UI.Button rightButton;

        // Visual constants - proportions relative to menu size
        private const float MENU_SCREEN_HEIGHT_FRACTION = 0.5f;  // Menu is 50% of screen height
        private const float BUTTON_SIZE_FRACTION = 0.30f;        // Buttons are 30% of menu size
        private const float CENTER_BUTTON_FRACTION = 0.22f;      // Center button is 22% of menu size
        private const float MENU_RADIUS_FRACTION = 0.33f;        // Button positions at 33% from center

        // Colors for button backgrounds
        private static readonly Color CancelColor = new Color(0.7f, 0.15f, 0.15f, 1f);     // Red
        private static readonly Color RemoveColor = new Color(0.45f, 0.35f, 0.25f, 1f);    // Earth brown
        private static readonly Color PondColor = new Color(0.2f, 0.35f, 0.7f, 1f);        // Blue water
        private static readonly Color LanternColor = new Color(0.85f, 0.65f, 0.15f, 1f);   // Golden
        private static readonly Color RingColor = new Color(0.55f, 0.2f, 0.7f, 1f);        // Purple

        // Reference to DynamicMazeGrowth for prop manipulation
        private DynamicMazeGrowth dynamicMazeGrowth;

        // Track if we've applied an action
        private bool actionApplied = false;

        public SculptingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        /// <summary>
        /// Override IsExpired - expires when action is applied or menu is cancelled
        /// </summary>
        public override bool IsExpired => actionApplied;

        public override void OnStart()
        {
            // Find DynamicMazeGrowth
            dynamicMazeGrowth = Object.FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth == null)
            {
                Debug.LogWarning("[SculptingEffect] DynamicMazeGrowth not found!");
                actionApplied = true;
                return;
            }

            // Check if target position is on a node
            targetNodeIndex = dynamicMazeGrowth.FindNodeIndexAtPosition(targetPosition);
            if (targetNodeIndex < 0)
            {
                // Not on a node - cancel silently
                actionApplied = true;
                return;
            }

            // Block activation on node 0 (the heart/seed node)
            if (targetNodeIndex == 0)
            {
                actionApplied = true;
                return;
            }

            // Store menu position
            menuPosition = targetPosition;

            // Create the circular menu
            CreateCircularMenu();
            menuActive = true;
        }

        public override void OnEnd()
        {
            DestroyMenu();
            menuActive = false;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            // Check for escape key to cancel
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (menuActive && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelMenu();
            }
        }

        private void CreateCircularMenu()
        {
            // Ensure EventSystem exists for button interaction
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Create prop preview textures
            CreatePropPreviews();

            // Create a circular sprite for button masks
            Sprite circleSprite = CreateCircleSprite(64);

            // Create container
            menuContainer = new GameObject("SculptingMenu");

            // Create SCREEN-SPACE OVERLAY canvas (always on top, proper UI)
            menuCanvas = menuContainer.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 200; // High priority to be on top

            // Add CanvasScaler for consistent sizing across resolutions
            var scaler = menuContainer.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Add GraphicRaycaster for button interaction
            menuContainer.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Get the canvas RectTransform
            RectTransform canvasRect = menuCanvas.GetComponent<RectTransform>();

            // Convert world position to screen position for menu center
            Camera mainCamera = Camera.main;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(menuPosition);

            // Calculate sizes based on reference resolution height (50% of screen height for entire menu)
            // Use reference height (1080) since CanvasScaler is set to ScaleWithScreenSize
            float referenceHeight = 1080f;
            float menuSize = referenceHeight * MENU_SCREEN_HEIGHT_FRACTION;
            float menuRadius = menuSize * MENU_RADIUS_FRACTION;
            float buttonSize = menuSize * BUTTON_SIZE_FRACTION;
            float centerButtonSize = menuSize * CENTER_BUTTON_FRACTION;

            // Create a panel at the screen position
            GameObject panelObj = new GameObject("MenuPanel");
            panelObj.transform.SetParent(canvasRect, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
            panelRect.sizeDelta = new Vector2(menuSize, menuSize);

            // Create circular buttons around center (no labels, with preview images)
            // Center button (Cancel - red X)
            centerButton = CreateCircularButton(panelRect, Vector2.zero, centerButtonSize, CancelColor, circleSprite, null, "X", OnCancelClicked);

            // Top button (Remove) - uses earth ground texture
            Sprite removeSprite = propPreviewTextures != null && propPreviewTextures[0] != null
                ? Sprite.Create(propPreviewTextures[0], new Rect(0, 0, propPreviewTextures[0].width, propPreviewTextures[0].height), new Vector2(0.5f, 0.5f))
                : null;
            topButton = CreateCircularButton(panelRect, new Vector2(0, menuRadius), buttonSize, RemoveColor, circleSprite, removeSprite, null, OnRemoveClicked);

            // Left button (Pond)
            Sprite pondSprite = propPreviewTextures != null && propPreviewTextures[1] != null
                ? Sprite.Create(propPreviewTextures[1], new Rect(0, 0, propPreviewTextures[1].width, propPreviewTextures[1].height), new Vector2(0.5f, 0.5f))
                : null;
            leftButton = CreateCircularButton(panelRect, new Vector2(-menuRadius, 0), buttonSize, PondColor, circleSprite, pondSprite, null, OnPondClicked);

            // Bottom button (Lantern)
            Sprite lanternSprite = propPreviewTextures != null && propPreviewTextures[2] != null
                ? Sprite.Create(propPreviewTextures[2], new Rect(0, 0, propPreviewTextures[2].width, propPreviewTextures[2].height), new Vector2(0.5f, 0.5f))
                : null;
            bottomButton = CreateCircularButton(panelRect, new Vector2(0, -menuRadius), buttonSize, LanternColor, circleSprite, lanternSprite, null, OnLanternClicked);

            // Right button (Ring)
            Sprite ringSprite = propPreviewTextures != null && propPreviewTextures[3] != null
                ? Sprite.Create(propPreviewTextures[3], new Rect(0, 0, propPreviewTextures[3].width, propPreviewTextures[3].height), new Vector2(0.5f, 0.5f))
                : null;
            rightButton = CreateCircularButton(panelRect, new Vector2(menuRadius, 0), buttonSize, RingColor, circleSprite, ringSprite, null, OnRingClicked);
        }

        private Sprite CreateCircleSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = center - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist <= radius)
                    {
                        // Anti-aliased edge
                        float alpha = Mathf.Clamp01(radius - dist + 1f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Stored textures loaded from files
        private Texture2D[] propPreviewTextures;

        private void CreatePropPreviews()
        {
            // Load pre-saved preview textures from Assets/Textures/PropPreviews/
            // These are screenshots taken from the editor with correct orientations
            propPreviewTextures = new Texture2D[4];

#if UNITY_EDITOR
            // 0: Remove - earth ground texture
            propPreviewTextures[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/EarthenGroundTexture.png");

            // 1: Pond preview
            propPreviewTextures[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PropPreviews/pond_preview.png");

            // 2: Lantern preview
            propPreviewTextures[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PropPreviews/lantern_preview.png");

            // 3: Ring preview
            propPreviewTextures[3] = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/PropPreviews/ring_preview.png");
#endif
        }

        private UnityEngine.UI.Button CreateCircularButton(RectTransform parent, Vector2 position, float size, Color bgColor, Sprite circleMask, Sprite contentSprite, string fallbackText, UnityEngine.Events.UnityAction onClick)
        {
            // Create outer border circle (slightly larger)
            GameObject borderObj = new GameObject($"CircularButtonBorder");
            borderObj.transform.SetParent(parent, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchoredPosition = position;
            float borderSize = size + 6f; // 3px border on each side
            borderRect.sizeDelta = new Vector2(borderSize, borderSize);

            // Border image - white/light contrasting color
            var borderImage = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImage.sprite = circleMask;
            borderImage.color = new Color(0.9f, 0.9f, 0.9f, 1f); // Light border
            borderImage.type = UnityEngine.UI.Image.Type.Simple;
            borderImage.preserveAspect = true;
            borderImage.raycastTarget = false;

            // Create main button as child
            GameObject buttonObj = new GameObject($"CircularButton");
            buttonObj.transform.SetParent(borderObj.transform, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);

            // Add circular mask for content clipping
            var mask = buttonObj.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = true;

            // Background circle image
            var bgImage = buttonObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.sprite = circleMask;
            bgImage.color = bgColor;
            bgImage.type = UnityEngine.UI.Image.Type.Simple;
            bgImage.preserveAspect = true;

            // Add button component
            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = bgImage;

            // Set button colors with hover/press effects
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            button.onClick.AddListener(onClick);

            // Add content image (prop preview) if provided
            if (contentSprite != null)
            {
                GameObject contentObj = new GameObject("Content");
                contentObj.transform.SetParent(buttonObj.transform, false);

                RectTransform contentRect = contentObj.AddComponent<RectTransform>();
                // Fill the button area - images should be pre-cropped to fit the circle
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;

                var contentImage = contentObj.AddComponent<UnityEngine.UI.Image>();
                contentImage.sprite = contentSprite;
                contentImage.preserveAspect = false; // Stretch to fill circular mask area
                contentImage.raycastTarget = false;
            }
            else if (!string.IsNullOrEmpty(fallbackText))
            {
                // Fallback text (for cancel button)
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                text.text = fallbackText;
                text.fontSize = size * 0.5f;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = Color.white;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.raycastTarget = false;
            }

            return button;
        }

        private void DestroyMenu()
        {
            if (menuContainer != null)
            {
                Object.Destroy(menuContainer);
                menuContainer = null;
            }

            // Note: propPreviewTextures are asset references, don't destroy them
            propPreviewTextures = null;

            menuCanvas = null;
            centerButton = null;
            topButton = null;
            leftButton = null;
            bottomButton = null;
            rightButton = null;
        }

        private void CancelMenu()
        {
            actionApplied = true;
        }

        private void OnCancelClicked()
        {
            CancelMenu();
        }

        private void OnRemoveClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, null);
            }
            actionApplied = true;
        }

        private void OnPondClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.Pond);
            }
            actionApplied = true;
        }

        private void OnLanternClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FaeLantern);
            }
            actionApplied = true;
        }

        private void OnRingClicked()
        {
            if (dynamicMazeGrowth != null && targetNodeIndex >= 0)
            {
                Vector3? nodeCenter = dynamicMazeGrowth.GetNodeCenterPosition(targetNodeIndex);
                if (nodeCenter.HasValue)
                    SpawnSmokeEffect(nodeCenter.Value);
                dynamicMazeGrowth.SetNodeProp(targetNodeIndex, DynamicMazeGrowth.NodePropType.FairyRing);
            }
            actionApplied = true;
        }

        /// <summary>
        /// Spawns a fog effect that expands from node center to cover the node, then fades out.
        /// Uses a circular quad with the PowerFog shader, similar to MurmuringPaths effect.
        /// </summary>
        private void SpawnSmokeEffect(Vector3 nodeCenter)
        {
            const float NODE_RADIUS = 3.0f;
            const float SMOKE_DURATION = 0.8f;
            const float FOG_Z = -0.3f; // Above ground plane (-Z is up)

            // Play sculpt sound effect
            PlaySculptSound(nodeCenter);

            // Start the fog animation coroutine
            manager.StartCoroutine(AnimateSculptingFog(nodeCenter, NODE_RADIUS, SMOKE_DURATION, FOG_Z));
        }

        /// <summary>
        /// Plays the sculpt sound effect at the given position.
        /// </summary>
        private void PlaySculptSound(Vector3 position)
        {
            float volume = FaeMaze.Systems.GameSettings.SculptVolume * FaeMaze.Systems.GameSettings.SfxVolume;
            if (volume <= 0f) return;

#if UNITY_EDITOR
            AudioClip sculptClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sculpt.mp3");
            if (sculptClip != null)
            {
                // Create a temporary audio source for 3D positional audio
                GameObject audioObj = new GameObject("SculptSound");
                audioObj.transform.position = position;
                AudioSource audioSource = audioObj.AddComponent<AudioSource>();
                audioSource.clip = sculptClip;
                audioSource.spatialBlend = 1f; // 3D audio
                audioSource.volume = volume;
                audioSource.minDistance = 2f;
                audioSource.maxDistance = 15f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.Play();
                Object.Destroy(audioObj, sculptClip.length + 0.1f);
            }
#endif
        }

        /// <summary>
        /// Animates an expanding fog ring with particles on the leading edge.
        /// The ring expands from center to node edge, then fades out.
        /// Inner edge tapers to transparency for a soft billowing look.
        /// </summary>
        private System.Collections.IEnumerator AnimateSculptingFog(Vector3 nodeCenter, float targetRadius, float duration, float fogZ)
        {
            // Smoke colors - pale cream/tan
            Color smokeColor = new Color(0.85f, 0.80f, 0.72f, 0.85f);
            Color smokeColorDark = new Color(0.70f, 0.65f, 0.58f, 0.85f);

            const float RING_THICKNESS = 1.2f; // Width of the fog ring
            const int PARTICLE_COUNT = 200; // Particles on leading edge - dense cloud

            // Create container
            GameObject container = new GameObject("SculptingFogContainer");
            container.transform.position = new Vector3(nodeCenter.x, nodeCenter.y, fogZ);

            // Create the fog ring mesh
            GameObject fogRing = new GameObject("SculptingFogRing");
            fogRing.transform.SetParent(container.transform);
            fogRing.transform.localPosition = Vector3.zero;
            fogRing.transform.rotation = Quaternion.identity;

            MeshFilter meshFilter = fogRing.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = fogRing.AddComponent<MeshRenderer>();

            // Create material using PowerFog shader
            var shader = Shader.Find("Custom/PowerFog");
            if (shader == null)
            {
                Debug.LogWarning("PowerFog shader not found for sculpting smoke, using fallback");
                shader = Shader.Find("Sprites/Default");
            }

            Material fogMaterial = new Material(shader);
            fogMaterial.SetColor("_FogColor", smokeColor);
            fogMaterial.SetColor("_FogColorDark", smokeColorDark);
            fogMaterial.SetColor("_GlowColor", new Color(1f, 0.98f, 0.95f, 0.5f));
            fogMaterial.SetFloat("_WaveProgress", 0.5f);
            fogMaterial.SetVector("_HeartPosition", new Vector4(nodeCenter.x, nodeCenter.y, 0, 0));
            fogMaterial.SetVector("_FurthestPosition", new Vector4(nodeCenter.x, nodeCenter.y, 0, 0));

            // Cloud settings for billowy look - more detail and variation
            fogMaterial.SetFloat("_CloudScale", 12.0f);  // Smaller cloud features for more detail
            fogMaterial.SetFloat("_CloudDetail", 3.5f);  // More detail layers
            fogMaterial.SetFloat("_CloudDensity", 1.1f); // Denser clouds
            fogMaterial.SetFloat("_CloudSharpness", 2.0f); // Softer edges for billowy look
            fogMaterial.SetFloat("_WindSpeed", 0.12f);   // Slightly faster animation

            // Create radial gradient texture for inner edge fade (black at center, white at edge)
            // This makes the path mask fade from 0 (inner) to 1 (outer)
            Texture2D gradientTex = CreateRadialGradientTexture(64);
            fogMaterial.SetTexture("_PathMask", gradientTex);

            meshRenderer.material = fogMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // Create particle system for leading edge
            GameObject particleObj = new GameObject("LeadingEdgeParticles");
            particleObj.transform.SetParent(container.transform);
            particleObj.transform.localPosition = Vector3.zero;
            particleObj.transform.rotation = Quaternion.identity;

            ParticleSystem particles = particleObj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f); // Much smaller particles
            main.startColor = new Color(0.95f, 0.92f, 0.88f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000; // Many more particles allowed
            main.gravityModifier = 0f;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = PARTICLE_COUNT * 8; // Very dense emission for cloud effect

            // Shape will be updated each frame to match ring's leading edge
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.radiusThickness = 0.6f; // Broader distribution across ring
            shape.arc = 360f;
            shape.rotation = new Vector3(90f, 0f, 0f); // Emit in XY plane

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(0.6f, 1.2f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.98f, 0.96f, 0.93f), 0f),
                    new GradientColorKey(new Color(0.90f, 0.86f, 0.80f), 0.5f),
                    new GradientColorKey(new Color(0.80f, 0.75f, 0.68f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // Noise for organic movement
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 2f;
            noise.scrollSpeed = 0.5f;

            // Particle renderer
            var particleRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 101;

            var particleMat = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default"));
            particleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            particleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            particleMat.SetInt("_ZWrite", 0);
            particleMat.renderQueue = 3001;
            particleRenderer.material = particleMat;

            particles.Play();

            // Animation timing
            float expandDuration = duration * 0.5f;   // Expand phase
            float fadeDuration = duration * 0.5f;     // Fade phase

            float elapsed = 0f;
            float currentInnerRadius = 0f;
            float currentOuterRadius = RING_THICKNESS * 0.5f;

            // Phase 1: Expand ring outward
            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDuration);
                float easedT = 1f - (1f - t) * (1f - t); // Ease out

                // Ring expands: inner and outer both grow, maintaining thickness
                currentOuterRadius = Mathf.Lerp(RING_THICKNESS * 0.5f, targetRadius, easedT);
                currentInnerRadius = Mathf.Max(0f, currentOuterRadius - RING_THICKNESS);

                // Update ring mesh with UVs that encode radial position for gradient sampling
                meshFilter.mesh = CreateRingMeshWithGradientUVs(32, currentInnerRadius, currentOuterRadius);

                // Update particle emission radius to match leading edge
                shape.radius = currentOuterRadius;

                yield return null;
            }

            // Stop particle emission, let existing particles fade
            emission.rateOverTime = 0;

            // Phase 2: Fade out the ring
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float alpha = 1f - (t * t); // Ease in fade

                smokeColor.a = 0.85f * alpha;
                smokeColorDark.a = 0.85f * alpha;
                fogMaterial.SetColor("_FogColor", smokeColor);
                fogMaterial.SetColor("_FogColorDark", smokeColorDark);

                yield return null;
            }

            // Cleanup
            Object.Destroy(gradientTex);
            Object.Destroy(fogMaterial);
            Object.Destroy(particleMat);
            Object.Destroy(container);
        }

        /// <summary>
        /// Creates a radial gradient texture where center is black (0) and edge is white (1).
        /// Used for inner edge fade on the fog ring.
        /// </summary>
        private Texture2D CreateRadialGradientTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = size * 0.5f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalized = Mathf.Clamp01(dist / maxDist);

                    // Gradient from 0 (center) to 1 (edge) with strong ease for very soft inner fade
                    // Use quartic (power of 4) for much more gradual inner edge
                    float value = normalized * normalized * normalized * normalized;
                    tex.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Creates a ring mesh with UVs that map to a radial gradient texture.
        /// Inner edge UV samples from center (black), outer edge samples from edge (white).
        /// </summary>
        private Mesh CreateRingMeshWithGradientUVs(int segments, float innerRadius, float outerRadius)
        {
            Mesh mesh = new Mesh();
            mesh.name = "RingMeshGradient";

            int vertexCount = segments * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 6];

            // Calculate UV radius based on actual ring geometry
            // We want inner vertices to sample from inner part of gradient, outer from outer part
            float uvInnerRadius = innerRadius / (outerRadius > 0.001f ? outerRadius : 1f) * 0.5f;
            float uvOuterRadius = 0.5f; // Edge of texture

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner vertex
                vertices[i * 2] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
                // UV maps to inner ring of gradient texture
                uvs[i * 2] = new Vector2(cos * uvInnerRadius + 0.5f, sin * uvInnerRadius + 0.5f);

                // Outer vertex
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
                // UV maps to outer edge of gradient texture
                uvs[i * 2 + 1] = new Vector2(cos * uvOuterRadius + 0.5f, sin * uvOuterRadius + 0.5f);

                // Two triangles per segment
                int nextI = (i + 1) % segments;
                triangles[i * 6] = i * 2;
                triangles[i * 6 + 1] = i * 2 + 1;
                triangles[i * 6 + 2] = nextI * 2 + 1;

                triangles[i * 6 + 3] = i * 2;
                triangles[i * 6 + 4] = nextI * 2 + 1;
                triangles[i * 6 + 5] = nextI * 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }

    #endregion
}

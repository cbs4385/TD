using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Props;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Audio;
using ForestMaze;

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
                    float offsetX = (RandomManager.Value - 0.5f) * 2f * intensity;
                    float offsetY = (RandomManager.Value - 0.5f) * 2f * intensity;
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

                if (nearInfluence && RandomManager.Value < 0.3f)
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
            if (definition.tier >= 3 && !undertowUsed && RandomManager.Value < 0.2f)
            {
                undertowUsed = true;
                return manager.MazeGrid.HeartWorldPosition;
            }

            if (pactPoolPositions.Count > 0)
            {
                int randomIndex = RandomManager.Range(0, pactPoolPositions.Count);
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
        #region Static Properties

        /// <summary>
        /// Static flag indicating whether the HeartwardGrasp tongue has active solid colliders.
        /// Visitors check this to avoid expensive Physics.OverlapSphere calls when no tongue exists.
        /// </summary>
        public static bool IsGraspTongueActiveWithColliders { get; private set; } = false;

        /// <summary>
        /// Static reference to the active HeartwardGraspEffect for collision callbacks.
        /// </summary>
        public static HeartwardGraspEffect ActiveInstance { get; private set; } = null;

        #endregion

        // Grabbing HGZ states - Same as HeartOfTheMaze: Idle -> Emerging -> Extending -> Retracting
        private enum GrabPhase
        {
            Idle,           // Waiting for visitors to enter the zone
            Emerging,       // Tongue rises from ground, tip not yet at ground level
            Extending,      // Tip above ground, extending horizontally toward visitor (collision triggers grab)
            Retracting      // Visitor grabbed, tongue retracts pulling visitor into ground
        }

        // Pushing HGZ states - Reverse of grab: tongue emerges with visitor, extends, releases
        private enum PushPhase
        {
            Idle,           // Tongue hidden underground
            Emerging,       // Tongue rises from ground with visitor attached to tip
            Extending,      // Tongue extends horizontally, pushing visitor onto walkable area
            Releasing,      // Visitor released, tongue retracts back underground
            Retracting      // Tongue retracts underground (visitor already released)
        }

        // Constants - Architectural (not configurable)
        private const int MIN_WALL_THICKNESS = 3;         // Minimum wall models required for valid wall intersection
        private const float HGZ_WALL_OFFSET = 1.6f;       // Offset into forest (2 wall layers deep, second rank)
        private const float MIN_EDGE_DISTANCE = 3.0f;     // Minimum distance from path/node edge (~4 wall tiles * 0.8 spacing)

        // Settings - Loaded from GameSettings
        private readonly float graspZoneRadius;           // Detection radius for HeartwardGrasp zones
        private readonly float tongueEmergeSpeed;         // Units per second for vertical movement
        private readonly float tongueRetractSpeed;        // Speed when retracting
        private readonly float grabEssenceCost;           // Essence deducted from visitor when grabbed
        private const float TONGUE_START_Z = 28.0f;       // Starting Z position (tongue length ~27, so Z=28 keeps it underground)
        private const float TONGUE_GROUND_Z = 0.0f;       // Ground level where tip emerges
        private const int BEND_BONE_COUNT = 5;            // Bones for the 90° bend at ground level (matches HeartOfTheMaze)

        // Tongue bone tracking for grabbing HGZ
        private Transform[] grabbingTongueBones;              // Array of bone transforms from base to tip
        private Vector3[] grabbingBoneRestPositions;          // Original local positions
        private Quaternion[] grabbingBoneRestRotations;       // Original local rotations
        private SkinnedMeshRenderer grabbingTongueRenderer;   // Skinned mesh for the tongue
        private float grabbingTongueLength = 0f;              // Total length of armature
        private GameObject[] grabbingSolidColliders;          // Solid collider objects for collision detection

        // Grabbing HGZ
        private GameObject grabbingZoneObject;
        private SphereCollider grabbingCollider;
        private GameObject grabbingTongueInstance;         // Instantiated tongue prefab
        private Vector3 grabbingWallPos;                   // Wall tile position (spawn point, acts like heart center)
        private Vector3 grabbingWallNormal;
        private float grabbingTongueZPosition = TONGUE_START_Z;  // Z position of tongue root (high = below ground, low = emerged)
        private float grabbingTargetAngle = 0f;            // Angle toward visitor (updated during Extending)
        private GrabPhase grabPhase = GrabPhase.Idle;

        // Pushing HGZ - reverse of grabbing (tongue emerges with visitor, extends, releases)
        private GameObject pushingZoneObject;
        private SphereCollider pushingCollider;
        private GameObject pushingTongueInstance;          // Instantiated tongue prefab
        private Transform[] pushingTongueBones;            // Bone transforms for pushing tongue
        private Vector3[] pushingBoneRestPositions;        // Rest positions for pushing tongue
        private Quaternion[] pushingBoneRestRotations;     // Rest rotations for pushing tongue
        private SkinnedMeshRenderer pushingTongueRenderer; // Skinned mesh for pushing tongue
        private float pushingTongueLength = 0f;            // Total length of pushing tongue
        private float pushingTongueZPosition = TONGUE_START_Z;  // Z position of tongue root
        private float pushingTargetAngle = 0f;             // Angle toward heart (direction tongue extends)
        private Vector3 pushingWallPos;
        private Vector3 pushingWallNormal;
        private PushPhase pushPhase = PushPhase.Idle;
        private float pushPhaseStartTime = 0f;
        private const float VISITOR_CHECK_RADIUS = 0.3f;  // Radius to check for visitor collision with walls/paths

        // Visitor processing
        private Queue<VisitorControllerBase> pendingVisitors = new Queue<VisitorControllerBase>();
        private VisitorControllerBase currentVisitor;
        private Vector3 heartNodePosition;

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

        // Frightening event (registered when tongue is active)
        private FrighteningEventManager.FrighteningEvent currentFrighteningEvent;

        // Particle colors
        private static readonly Color LeafGreen = new Color(0.3f, 0.7f, 0.2f, 1f);
        private static readonly Color BarkBrown = new Color(0.6f, 0.4f, 0.15f, 1f);

        public HeartwardGraspEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            // Load settings from GameSettings
            graspZoneRadius = GameSettings.HeartwardGraspRadius;
            tongueEmergeSpeed = GameSettings.TongueEmergeSpeed;
            tongueRetractSpeed = GameSettings.TongueRetractSpeed;
            grabEssenceCost = GameSettings.HeartwardGraspEssenceCost;
        }

        public override bool IsExpired => hasExpired;

        /// <summary>
        /// Gets the position of the grabbing HGZ zone (for tutorial camera tracking).
        /// </summary>
        public Vector3 GrabbingZonePosition => grabbingWallPos;

        /// <summary>
        /// Gets the position of the pushing HGZ zone (for tutorial camera tracking).
        /// </summary>
        public Vector3 PushingZonePosition => pushingWallPos;

        public override void OnStart()
        {
            requiredCaptures = manager.GetPowerTier(HeartPowerType.HeartwardGrasp);
            capturedCount = 0;
            hasExpired = false;

            // Set static instance for collision callbacks
            ActiveInstance = this;

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
        ///
        /// GRABBING HGZ: Ray from FOCAL toward HEART
        ///   - First wall hit = grabbing placement point
        ///   - If no hits, find wall closest to the focal-to-heart ray
        ///
        /// PUSHING HGZ: Ray from HEART toward FOCAL
        ///   - First wall hit = pushing placement point
        ///   - If no hits, find wall closest to the heart-to-focal ray
        ///
        /// Both placement points are then offset into the forest.
        /// </summary>
        private void FindWallPositions(Vector3 focalPos)
        {
            Vector3 heartPos3D = new Vector3(heartNodePosition.x, heartNodePosition.y, 0f);
            Vector3 focalPos3D = new Vector3(focalPos.x, focalPos.y, 0f);
            float totalDist = Vector3.Distance(heartPos3D, focalPos3D);

            Vector2 heartPos2D = new Vector2(heartNodePosition.x, heartNodePosition.y);
            Vector2 focalPos2D = new Vector2(focalPos.x, focalPos.y);

            // Direction vectors
            Vector3 focalToHeartDir = (heartPos3D - focalPos3D).normalized;
            Vector3 heartToFocalDir = -focalToHeartDir;
            Vector2 focalToHeartDir2D = new Vector2(focalToHeartDir.x, focalToHeartDir.y);
            Vector2 heartToFocalDir2D = new Vector2(heartToFocalDir.x, heartToFocalDir.y);

            Debug.Log($"[HeartwardGrasp] ===== FindWallPositions START =====" +
                $"\n  Heart: {heartPos3D}" +
                $"\n  Focal: {focalPos3D}" +
                $"\n  Total distance: {totalDist:F2}" +
                $"\n  Focal->Heart dir: {focalToHeartDir}" +
                $"\n  Heart->Focal dir: {heartToFocalDir}");

            // ===== GRABBING HGZ: Ray from FOCAL toward HEART =====
            // First wall hit along this ray is the grabbing placement point
            RaycastHit[] grabbingHits = Physics.RaycastAll(focalPos3D, focalToHeartDir, totalDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            // Filter to only wall tiles and sort by distance from focal
            var grabbingWallHits = new System.Collections.Generic.List<RaycastHit>();
            foreach (var hit in grabbingHits)
            {
                if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    grabbingWallHits.Add(hit);
                }
            }
            grabbingWallHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            Debug.Log($"[HeartwardGrasp] GRABBING ray (focal->heart): {grabbingHits.Length} total hits, {grabbingWallHits.Count} wall hits");

            Vector2 grabbingPlacementPos;
            if (grabbingWallHits.Count > 0)
            {
                // First wall hit = grabbing placement point
                var firstGrabbingWall = grabbingWallHits[0];
                grabbingPlacementPos = new Vector2(firstGrabbingWall.collider.transform.position.x, firstGrabbingWall.collider.transform.position.y);
                Debug.Log($"[HeartwardGrasp] GRABBING: First wall hit = {firstGrabbingWall.collider.gameObject.name} at {grabbingPlacementPos}");
            }
            else
            {
                // No wall hits - find wall closest to the focal-to-heart ray
                Debug.Log($"[HeartwardGrasp] GRABBING: No wall hits along ray, searching for closest wall to ray");
                Transform closestWall = FindClosestWallToRay(focalPos2D, focalToHeartDir2D, totalDist);
                if (closestWall != null)
                {
                    grabbingPlacementPos = new Vector2(closestWall.position.x, closestWall.position.y);
                    Debug.Log($"[HeartwardGrasp] GRABBING: Closest wall to ray = {closestWall.name} at {grabbingPlacementPos}");
                }
                else
                {
                    // Ultimate fallback - use a point along the ray at NODE_RADIUS from focal
                    const float NODE_RADIUS = 3.0f;
                    grabbingPlacementPos = focalPos2D + focalToHeartDir2D * NODE_RADIUS;
                    Debug.Log($"[HeartwardGrasp] GRABBING: No walls found, using fallback point at {grabbingPlacementPos}");
                }
            }

            // Offset grabbing placement into forest
            Vector2 grabbingForestDir = FindForestDirection(grabbingPlacementPos, heartToFocalDir2D, heartPos2D);
            Vector2 grabbingOffset = grabbingForestDir * HGZ_WALL_OFFSET;
            grabbingWallPos = new Vector3(grabbingPlacementPos.x + grabbingOffset.x, grabbingPlacementPos.y + grabbingOffset.y, -0.4f);
            grabbingWallNormal = new Vector3(grabbingForestDir.x, grabbingForestDir.y, 0f);

            Debug.Log($"[HeartwardGrasp] GRABBING HGZ FINAL:" +
                $"\n  Placement point: {grabbingPlacementPos}" +
                $"\n  Forest direction: {grabbingForestDir} (angle: {Mathf.Atan2(grabbingForestDir.y, grabbingForestDir.x) * Mathf.Rad2Deg:F1}°)" +
                $"\n  Offset: {grabbingOffset} (magnitude {HGZ_WALL_OFFSET})" +
                $"\n  FINAL position: {grabbingWallPos}");

            // ===== PUSHING HGZ: Ray from HEART toward FOCAL =====
            // First wall hit along this ray is the pushing placement point
            RaycastHit[] pushingHits = Physics.RaycastAll(heartPos3D, heartToFocalDir, totalDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            // Filter to only wall tiles and sort by distance from heart
            var pushingWallHits = new System.Collections.Generic.List<RaycastHit>();
            foreach (var hit in pushingHits)
            {
                if (hit.collider != null && hit.collider.gameObject.name.StartsWith("WorldTile_#"))
                {
                    pushingWallHits.Add(hit);
                }
            }
            pushingWallHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            Debug.Log($"[HeartwardGrasp] PUSHING ray (heart->focal): {pushingHits.Length} total hits, {pushingWallHits.Count} wall hits");

            Vector2 pushingPlacementPos;
            if (pushingWallHits.Count > 0)
            {
                // First wall hit = pushing placement point
                var firstPushingWall = pushingWallHits[0];
                pushingPlacementPos = new Vector2(firstPushingWall.collider.transform.position.x, firstPushingWall.collider.transform.position.y);
                Debug.Log($"[HeartwardGrasp] PUSHING: First wall hit = {firstPushingWall.collider.gameObject.name} at {pushingPlacementPos}");
            }
            else
            {
                // No wall hits - find wall closest to the heart-to-focal ray
                Debug.Log($"[HeartwardGrasp] PUSHING: No wall hits along ray, searching for closest wall to ray");
                Transform closestWall = FindClosestWallToRay(heartPos2D, heartToFocalDir2D, totalDist);
                if (closestWall != null)
                {
                    pushingPlacementPos = new Vector2(closestWall.position.x, closestWall.position.y);
                    Debug.Log($"[HeartwardGrasp] PUSHING: Closest wall to ray = {closestWall.name} at {pushingPlacementPos}");
                }
                else
                {
                    // Ultimate fallback - use a point along the ray at NODE_RADIUS from heart
                    const float NODE_RADIUS = 3.0f;
                    pushingPlacementPos = heartPos2D + heartToFocalDir2D * NODE_RADIUS;
                    Debug.Log($"[HeartwardGrasp] PUSHING: No walls found, using fallback point at {pushingPlacementPos}");
                }
            }

            // Offset pushing placement into forest
            Vector2 pushingForestDir = FindForestDirection(pushingPlacementPos, heartToFocalDir2D, heartPos2D);
            Vector2 pushingOffset = pushingForestDir * HGZ_WALL_OFFSET;
            pushingWallPos = new Vector3(pushingPlacementPos.x + pushingOffset.x, pushingPlacementPos.y + pushingOffset.y, -0.4f);
            pushingWallNormal = new Vector3(pushingForestDir.x, pushingForestDir.y, 0f);

            Debug.Log($"[HeartwardGrasp] PUSHING HGZ FINAL:" +
                $"\n  Placement point: {pushingPlacementPos}" +
                $"\n  Forest direction: {pushingForestDir} (angle: {Mathf.Atan2(pushingForestDir.y, pushingForestDir.x) * Mathf.Rad2Deg:F1}°)" +
                $"\n  Offset: {pushingOffset} (magnitude {HGZ_WALL_OFFSET})" +
                $"\n  FINAL position: {pushingWallPos}");
        }

        /// <summary>
        /// Finds the wall tile closest to a ray (line from origin in direction).
        /// Used when no walls are directly hit by the ray.
        /// </summary>
        private Transform FindClosestWallToRay(Vector2 rayOrigin, Vector2 rayDir, float maxDist)
        {
            // Search for walls in a wide area around the ray
            const float SEARCH_WIDTH = 10f;
            Vector2 rayEnd = rayOrigin + rayDir * maxDist;
            Vector2 rayCenter = (rayOrigin + rayEnd) / 2f;
            float searchRadius = maxDist / 2f + SEARCH_WIDTH;

            Collider[] colliders = Physics.OverlapSphere(
                new Vector3(rayCenter.x, rayCenter.y, 0f),
                searchRadius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            Transform closestWall = null;
            float closestDistToRay = float.MaxValue;

            foreach (var collider in colliders)
            {
                // Only consider wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                Vector2 wallPos = new Vector2(collider.transform.position.x, collider.transform.position.y);

                // Calculate perpendicular distance from wall to ray
                Vector2 originToWall = wallPos - rayOrigin;
                float projectionLength = Vector2.Dot(originToWall, rayDir);

                // Only consider walls that are along the ray (positive projection, within max distance)
                if (projectionLength < 0 || projectionLength > maxDist) continue;

                Vector2 closestPointOnRay = rayOrigin + rayDir * projectionLength;
                float distToRay = Vector2.Distance(wallPos, closestPointOnRay);

                if (distToRay < closestDistToRay)
                {
                    closestDistToRay = distToRay;
                    closestWall = collider.transform;
                }
            }

            Debug.Log($"[HeartwardGrasp] FindClosestWallToRay: origin={rayOrigin}, dir={rayDir}, found={closestWall?.name ?? "null"}, distToRay={closestDistToRay:F2}");
            return closestWall;
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
            // Increased radius to find walls even when focal point is on a node
            const float SEARCH_RADIUS = 8f;
            Collider[] colliders = Physics.OverlapSphere(
                new Vector3(targetPoint.x, targetPoint.y, 0f),
                SEARCH_RADIUS,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            Debug.Log($"[HeartwardGrasp] FindClosestWallToPoint: target={targetPoint}, searchRadius={SEARCH_RADIUS}, found {colliders.Length} colliders");

            Transform closestWall = null;
            float closestDist = float.MaxValue;

            Vector2 rayDir2D = new Vector2(rayDir.x, rayDir.y).normalized;
            int wallsFound = 0;

            foreach (var collider in colliders)
            {
                // Only consider wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                wallsFound++;
                Vector2 wallPos = new Vector2(collider.transform.position.x, collider.transform.position.y);
                float dist = Vector2.Distance(wallPos, targetPoint);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestWall = collider.transform;
                }
            }

            Debug.Log($"[HeartwardGrasp] FindClosestWallToPoint: found {wallsFound} walls, closest={closestWall?.name} at dist={closestDist:F2}");
            return closestWall;
        }

        /// <summary>
        /// Calculates the "into forest" direction by averaging nearby wall normals.
        /// Wall's transform.right points TOWARD the path (front face of wall).
        /// So "into forest" = -transform.right (back of wall).
        /// Filters to only include walls on the correct side using the hint direction.
        /// </summary>
        /// <param name="position">Position to sample walls around</param>
        /// <param name="hintDirection">Expected "into forest" direction (used to filter walls)</param>
        private Vector2 GetIntoForestDirectionForEdge(Vector2 position, Vector2 hintDirection)
        {
            const float SAMPLE_RADIUS = 2.5f;  // Radius to sample nearby walls
            const float MIN_DOT_PRODUCT = 0.0f;  // Only include walls whose "into forest" aligns with hint

            Vector3 pos3D = new Vector3(position.x, position.y, 0f);
            Collider[] nearbyColliders = Physics.OverlapSphere(pos3D, SAMPLE_RADIUS, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

            Vector2 sumNormals = Vector2.zero;
            int wallCount = 0;

            foreach (var collider in nearbyColliders)
            {
                // Only include wall tiles
                if (!collider.gameObject.name.StartsWith("WorldTile_#")) continue;

                // Wall's transform.right points TOWARD the path (wall's front face)
                // "Into forest" = OPPOSITE direction = -transform.right
                Vector2 intoForest = new Vector2(-collider.transform.right.x, -collider.transform.right.y);

                // Only include walls whose "into forest" direction aligns with the hint
                // This filters out walls on the opposite side of the path
                float dot = Vector2.Dot(intoForest.normalized, hintDirection.normalized);

                if (dot < MIN_DOT_PRODUCT)
                {
                    continue;
                }

                sumNormals += intoForest;
                wallCount++;
            }

            if (wallCount == 0)
            {
                // Fallback: use the hint direction
                return hintDirection.normalized;
            }

            // Average of "into forest" directions from filtered walls
            Vector2 avgNormal = (sumNormals / wallCount).normalized;
            return avgNormal;
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
        /// Finds the direction from an HGZ position into the forest (non-walkable area).
        /// The "inside" of the forest is the direction where a point at fixed distance
        /// has the greatest minimum distance from all nearby graph elements (walkable tiles AND node centers).
        /// Node centers are unwalkable but are NOT forest - they are open areas we must avoid.
        /// </summary>
        /// <param name="hgzPosition">The HGZ position on the path</param>
        /// <param name="rayDir">Direction of the focal ray (heart to focal point)</param>
        /// <param name="heartPos">Position of the heart node</param>
        private Vector2 FindForestDirection(Vector2 hgzPosition, Vector2 rayDir, Vector2 heartPos)
        {
            const float ANGLE_STEP = 5f;           // Degrees between test directions
            const float SEARCH_RADIUS = 8f;        // Increased to catch nearby nodes
            const float CANDIDATE_DISTANCE = 3f;   // Distance to place candidate points
            const float TILE_SCAN_STEP = 0.5f;     // Step for finding walkable tiles
            const float NODE_RADIUS = 3.0f;        // Node clearing radius

            // Get the maze data for walkability checks
            var mazeData = manager.MazeGrid?.WorldSpaceMazeData;

            // Default perpendicular (fallback)
            Vector2 perpCCW = new Vector2(-rayDir.y, rayDir.x).normalized;

            if (mazeData == null)
            {
                return perpCCW;
            }

            // Collect all "graph element" positions - both walkable tiles AND node centers
            // We want to find the direction AWAY from all graph elements, not just walkable tiles
            List<Vector2> graphElements = new List<Vector2>();

            // Add walkable tiles
            for (float dx = -SEARCH_RADIUS; dx <= SEARCH_RADIUS; dx += TILE_SCAN_STEP)
            {
                for (float dy = -SEARCH_RADIUS; dy <= SEARCH_RADIUS; dy += TILE_SCAN_STEP)
                {
                    Vector2 testPoint = hgzPosition + new Vector2(dx, dy);
                    if (Vector2.Distance(testPoint, hgzPosition) <= SEARCH_RADIUS && mazeData.IsWalkable(testPoint))
                    {
                        graphElements.Add(testPoint);
                    }
                }
            }

            // Add node centers as graph elements (even though they're unwalkable, they're not forest!)
            var graphState = mazeData.GraphState;
            int nodesAdded = 0;
            if (graphState != null && graphState.Nodes != null)
            {
                foreach (var node in graphState.Nodes)
                {
                    Vector2 nodeCenter = node.Position;
                    float distToNode = Vector2.Distance(hgzPosition, nodeCenter);
                    if (distToNode <= SEARCH_RADIUS + NODE_RADIUS)
                    {
                        // Add the node center itself
                        graphElements.Add(nodeCenter);
                        nodesAdded++;
                        // Also add points around the node center to represent the full node area
                        for (float angle = 0; angle < 360; angle += 45)
                        {
                            float rad = angle * Mathf.Deg2Rad;
                            Vector2 nodeEdgePoint = nodeCenter + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (NODE_RADIUS * 0.5f);
                            graphElements.Add(nodeEdgePoint);
                        }
                    }
                }
            }

            Debug.Log($"[HeartwardGrasp] FindForestDirection collected:" +
                $"\n  Walkable tiles: {graphElements.Count - nodesAdded * 9}" +
                $"\n  Nodes added: {nodesAdded} (each adds 9 points)" +
                $"\n  Total graph elements: {graphElements.Count}");

            // If no graph elements found, use fallback
            if (graphElements.Count == 0)
            {
                Debug.Log($"[HeartwardGrasp] FindForestDirection: No graph elements found, using fallback perpCCW={perpCCW}");
                return perpCCW;
            }

            // Test all directions around the HGZ position
            // For each direction, place a candidate point at CANDIDATE_DISTANCE
            // Calculate the minimum distance from that point to any graph element
            // The direction with the greatest minimum distance is the "inside" of the forest
            Vector2 bestDir = perpCCW;
            float bestMinDist = -1f;
            int skippedWalkable = 0;
            int skippedInsideNode = 0;
            int validCandidates = 0;

            // Track top 3 candidates for debugging
            var topCandidates = new System.Collections.Generic.List<(float angle, Vector2 dir, float minDist)>();

            for (float angle = 0f; angle < 360f; angle += ANGLE_STEP)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 testDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 candidatePoint = hgzPosition + testDir * CANDIDATE_DISTANCE;

                // Skip if the candidate point itself is walkable (we want forest)
                if (mazeData.IsWalkable(candidatePoint))
                {
                    skippedWalkable++;
                    continue;
                }

                // Skip if candidate point is inside any node's area (node centers are not forest!)
                bool insideNode = false;
                if (graphState != null && graphState.Nodes != null)
                {
                    foreach (var node in graphState.Nodes)
                    {
                        if (Vector2.Distance(candidatePoint, node.Position) < NODE_RADIUS)
                        {
                            insideNode = true;
                            break;
                        }
                    }
                }
                if (insideNode)
                {
                    skippedInsideNode++;
                    continue;
                }

                validCandidates++;

                // Find minimum distance from candidate point to any graph element
                float minDistToGraph = float.MaxValue;
                foreach (var element in graphElements)
                {
                    float dist = Vector2.Distance(candidatePoint, element);
                    if (dist < minDistToGraph)
                    {
                        minDistToGraph = dist;
                    }
                }

                // Track top candidates for debugging
                topCandidates.Add((angle, testDir, minDistToGraph));

                // Track the direction with the greatest minimum distance to graph elements
                if (minDistToGraph > bestMinDist)
                {
                    bestMinDist = minDistToGraph;
                    bestDir = testDir;
                }
            }

            // Sort and get top 5 candidates
            topCandidates.Sort((a, b) => b.minDist.CompareTo(a.minDist));
            string topCandidatesStr = "";
            for (int i = 0; i < Mathf.Min(5, topCandidates.Count); i++)
            {
                var c = topCandidates[i];
                topCandidatesStr += $"\n    #{i + 1}: angle={c.angle:F0}° dir={c.dir} minDist={c.minDist:F2}";
            }

            // Log detailed results
            Debug.Log($"[HeartwardGrasp] FindForestDirection RESULT:" +
                $"\n  hgzPosition: {hgzPosition}" +
                $"\n  Candidates: {validCandidates} valid, {skippedWalkable} skipped (walkable), {skippedInsideNode} skipped (inside node)" +
                $"\n  graphElements found: {graphElements.Count}" +
                $"\n  bestDir: {bestDir} (angle: {Mathf.Atan2(bestDir.y, bestDir.x) * Mathf.Rad2Deg:F1}°)" +
                $"\n  bestMinDist to graph: {bestMinDist:F2}" +
                $"\n  rayDir for reference: {rayDir} (angle: {Mathf.Atan2(rayDir.y, rayDir.x) * Mathf.Rad2Deg:F1}°)" +
                $"\n  perpCCW fallback: {perpCCW} (angle: {Mathf.Atan2(perpCCW.y, perpCCW.x) * Mathf.Rad2Deg:F1}°)" +
                $"\n  Top candidates:{topCandidatesStr}");

            return bestDir.normalized;
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
            HeartPowerUtils.FindWallTilesInRadius(grabbingWallPos, graspZoneRadius, affectedGrabbingWalls, originalWallPositions);
        }

        private void UpdateWallShakeEffect()
        {
            if (affectedGrabbingWalls.Count == 0) return;

            // Only shake when in idle or early grab phases (waiting for or grabbing visitor)
            bool shouldShake = grabPhase == GrabPhase.Idle ||
                               grabPhase == GrabPhase.Emerging ||
                               grabPhase == GrabPhase.Extending;

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
            // Create grabbing zone at wall tile position
            // The wall tile position acts as the "heart" center - tongue emerges vertically from here
            grabbingZoneObject = new GameObject("GrabbingHGZ");
            grabbingZoneObject.transform.position = grabbingWallPos;

            // Add trigger collider for visitor detection
            grabbingCollider = grabbingZoneObject.AddComponent<SphereCollider>();
            grabbingCollider.radius = graspZoneRadius;
            grabbingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            var rb = grabbingZoneObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // The tongue will be spawned as a child of this object when a visitor enters
            // It will emerge vertically from below ground (like HeartOfTheMaze)
            grabbingTongueInstance = null;
            grabbingTongueZPosition = TONGUE_START_Z;
        }

        private void CreatePushingHGZ()
        {
            // Create pushing zone at wall near heart
            pushingZoneObject = new GameObject("PushingHGZ");
            pushingZoneObject.transform.position = pushingWallPos;

            // Add trigger collider
            pushingCollider = pushingZoneObject.AddComponent<SphereCollider>();
            pushingCollider.radius = graspZoneRadius;
            pushingCollider.isTrigger = true;

            // Add rigidbody for trigger detection
            var rb = pushingZoneObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Calculate direction toward heart for push orientation
            Vector3 dirTowardHeart = (heartNodePosition - pushingWallPos).normalized;
            pushingTargetAngle = Mathf.Atan2(dirTowardHeart.y, dirTowardHeart.x) * Mathf.Rad2Deg;

            // Tongue will be spawned when visitor is transported here
            pushingTongueInstance = null;
            pushingTongueZPosition = TONGUE_START_Z;
        }

        /// <summary>
        /// Spawns a tongue instance for grabbing. Based on HeartOfTheMaze.PreCreateTongueInstance().
        /// The tongue emerges vertically from below ground, like HeartOfTheMaze.
        /// </summary>
        private void SpawnGrabbingTongue(Vector3 visitorPos)
        {
            GameObject tonguePrefab = manager.TonguePrefab;
            if (tonguePrefab == null)
            {
                Debug.LogError("[HeartwardGrasp] TonguePrefab is null!");
                return;
            }

            if (grabbingTongueInstance != null)
            {
                Object.Destroy(grabbingTongueInstance);
            }

            // Initial angle toward visitor (will be updated each frame during Extending)
            UpdateGrabbingTargetAngle();

            // Instantiate tongue as child of grabbing zone at wall tile position
            // Like HeartOfTheMaze, the tongue is a child positioned at the "heart" (wall tile)
            grabbingTongueInstance = Object.Instantiate(tonguePrefab, grabbingZoneObject.transform);
            grabbingTongueInstance.name = "GrabbingTongue";

            // Initialize Z position (below ground, like HeartOfTheMaze)
            grabbingTongueZPosition = TONGUE_START_Z;
            Vector3 localPos = Vector3.zero;
            localPos.z = grabbingTongueZPosition;
            grabbingTongueInstance.transform.localPosition = localPos;

            // Remove any Light components
            foreach (var light in grabbingTongueInstance.GetComponentsInChildren<Light>())
            {
                Object.Destroy(light);
            }

            // Find and store bone references
            SetupGrabbingTongueBones();

            // Find solid colliders that are baked into the prefab (for collision detection)
            FindGrabbingSolidColliders();
        }

        /// <summary>
        /// Spawns a tongue instance for pushing (releasing visitor near heart).
        /// The tongue emerges from underground with visitor attached to tip, extends, then releases.
        /// This is the reverse of the grabbing sequence.
        /// </summary>
        private void SpawnPushingTongue()
        {
            GameObject tonguePrefab = manager.TonguePrefab;
            if (tonguePrefab == null)
            {
                Debug.LogError("[HeartwardGrasp] TonguePrefab is null!");
                return;
            }

            if (pushingTongueInstance != null)
            {
                Object.Destroy(pushingTongueInstance);
            }

            // Instantiate tongue as child of pushing zone
            pushingTongueInstance = Object.Instantiate(tonguePrefab, pushingZoneObject.transform);
            pushingTongueInstance.name = "PushingTongue";

            // Initialize Z position (below ground, visitor attached - reverse of grab end state)
            pushingTongueZPosition = TONGUE_START_Z;
            Vector3 localPos = Vector3.zero;
            localPos.z = pushingTongueZPosition;
            pushingTongueInstance.transform.localPosition = localPos;

            // Remove lights
            foreach (var light in pushingTongueInstance.GetComponentsInChildren<Light>())
            {
                Object.Destroy(light);
            }

            // Find bones
            SetupPushingTongueBones();
        }

        /// <summary>
        /// Finds and stores bone references for the grabbing tongue.
        /// </summary>
        private void SetupGrabbingTongueBones()
        {
            if (grabbingTongueInstance == null) return;

            // Find SkinnedMeshRenderer for bone info
            grabbingTongueRenderer = grabbingTongueInstance.GetComponentInChildren<SkinnedMeshRenderer>();

            if (grabbingTongueRenderer != null && grabbingTongueRenderer.bones != null && grabbingTongueRenderer.bones.Length > 0)
            {
                grabbingTongueBones = grabbingTongueRenderer.bones;
            }
            else
            {
                // Fallback: find transforms with "bone" in name
                var allTransforms = grabbingTongueInstance.GetComponentsInChildren<Transform>();
                var boneList = new List<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.ToLower().Contains("bone"))
                    {
                        boneList.Add(t);
                    }
                }

                // CRITICAL: Sort bones by their number (Bone_000, Bone_001, etc.)
                // Without sorting, bones may be in arbitrary order which breaks the bending logic
                boneList.Sort((a, b) => ExtractBoneNumber(a.name).CompareTo(ExtractBoneNumber(b.name)));

                grabbingTongueBones = boneList.ToArray();
            }

            // Store rest poses
            if (grabbingTongueBones != null && grabbingTongueBones.Length > 0)
            {
                grabbingBoneRestPositions = new Vector3[grabbingTongueBones.Length];
                grabbingBoneRestRotations = new Quaternion[grabbingTongueBones.Length];

                for (int i = 0; i < grabbingTongueBones.Length; i++)
                {
                    if (grabbingTongueBones[i] != null)
                    {
                        grabbingBoneRestPositions[i] = grabbingTongueBones[i].localPosition;
                        grabbingBoneRestRotations[i] = grabbingTongueBones[i].localRotation;
                    }
                }

                // Calculate tongue length
                CalculateGrabbingTongueLength();
            }
        }

        /// <summary>
        /// Finds and stores bone references for the pushing tongue.
        /// </summary>
        private void SetupPushingTongueBones()
        {
            if (pushingTongueInstance == null) return;

            pushingTongueRenderer = pushingTongueInstance.GetComponentInChildren<SkinnedMeshRenderer>();

            if (pushingTongueRenderer != null && pushingTongueRenderer.bones != null && pushingTongueRenderer.bones.Length > 0)
            {
                pushingTongueBones = pushingTongueRenderer.bones;
            }
            else
            {
                var allTransforms = pushingTongueInstance.GetComponentsInChildren<Transform>();
                var boneList = new List<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.name.ToLower().Contains("bone"))
                    {
                        boneList.Add(t);
                    }
                }

                // CRITICAL: Sort bones by their number (Bone_000, Bone_001, etc.)
                // Without sorting, bones may be in arbitrary order which breaks the bending logic
                boneList.Sort((a, b) => ExtractBoneNumber(a.name).CompareTo(ExtractBoneNumber(b.name)));

                pushingTongueBones = boneList.ToArray();
            }

            if (pushingTongueBones != null && pushingTongueBones.Length > 0)
            {
                pushingBoneRestPositions = new Vector3[pushingTongueBones.Length];
                pushingBoneRestRotations = new Quaternion[pushingTongueBones.Length];

                for (int i = 0; i < pushingTongueBones.Length; i++)
                {
                    if (pushingTongueBones[i] != null)
                    {
                        pushingBoneRestPositions[i] = pushingTongueBones[i].localPosition;
                        pushingBoneRestRotations[i] = pushingTongueBones[i].localRotation;
                    }
                }

                CalculatePushingTongueLength();
            }
        }

        /// <summary>
        /// Extracts the bone number from a bone name like "Bone_000", "Bone.001", "Bone123", etc.
        /// </summary>
        private int ExtractBoneNumber(string boneName)
        {
            string digits = "";
            foreach (char c in boneName)
            {
                if (char.IsDigit(c))
                {
                    digits += c;
                }
            }

            if (string.IsNullOrEmpty(digits))
            {
                return 0;
            }

            if (int.TryParse(digits, out int result))
            {
                return result;
            }

            return 0;
        }

        private void CalculateGrabbingTongueLength()
        {
            grabbingTongueLength = 0f;
            if (grabbingTongueBones == null || grabbingTongueBones.Length < 2) return;

            Vector3 firstBoneWorld = grabbingTongueBones[0].position;
            Vector3 lastBoneWorld = grabbingTongueBones[grabbingTongueBones.Length - 1].position;
            grabbingTongueLength = Vector3.Distance(firstBoneWorld, lastBoneWorld);

            // Add approximate length of last bone segment
            if (grabbingTongueBones.Length > 1)
            {
                float avgBoneSpacing = grabbingTongueLength / (grabbingTongueBones.Length - 1);
                grabbingTongueLength += avgBoneSpacing;
            }

        }

        private void CalculatePushingTongueLength()
        {
            pushingTongueLength = 0f;
            if (pushingTongueBones == null || pushingTongueBones.Length < 2) return;

            Vector3 firstBoneWorld = pushingTongueBones[0].position;
            Vector3 lastBoneWorld = pushingTongueBones[pushingTongueBones.Length - 1].position;
            pushingTongueLength = Vector3.Distance(firstBoneWorld, lastBoneWorld);

            if (pushingTongueBones.Length > 1)
            {
                float avgBoneSpacing = pushingTongueLength / (pushingTongueBones.Length - 1);
                pushingTongueLength += avgBoneSpacing;
            }
        }

        /// <summary>
        /// Finds colliders baked into the prefab for contact detection and physics blocking.
        /// BoneCollider_N = triggers for contact detection
        /// SolidCollider_N = solid colliders for collision detection (same as HeartOfTheMaze).
        /// </summary>
        private void FindGrabbingSolidColliders()
        {
            // Colliders are now baked into the prefab - just find them
            if (grabbingTongueInstance == null) return;

            var solidColliders = new List<GameObject>();
            Transform[] allTransforms = grabbingTongueInstance.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("SolidCollider_"))
                {
                    solidColliders.Add(t.gameObject);
                }
            }

            grabbingSolidColliders = solidColliders.ToArray();

            // Disable colliders initially (enable during Extending phase)
            SetGrabbingSolidCollidersEnabled(false);
        }

        /// <summary>
        /// Enables or disables solid colliders for collision detection.
        /// Same pattern as HeartOfTheMaze.EnableBoneColliders/DisableBoneColliders.
        /// Also sets the static flag so visitors know to check for tongue collision.
        /// </summary>
        private void SetGrabbingSolidCollidersEnabled(bool enabled)
        {
            if (grabbingSolidColliders == null) return;

            foreach (var colliderObj in grabbingSolidColliders)
            {
                if (colliderObj != null)
                {
                    var collider = colliderObj.GetComponent<SphereCollider>();
                    if (collider != null) collider.enabled = enabled;
                }
            }

            // Set static flag so visitors know to check for tongue collision
            IsGraspTongueActiveWithColliders = enabled;
        }

        /// <summary>
        /// Called by visitor when they collide with a tongue bone collider.
        /// This is the signal to grab them - same pattern as HeartOfTheMaze.NotifyVisitorTouchedTongue().
        /// </summary>
        public void NotifyVisitorTouchedGraspTongue(VisitorControllerBase visitor)
        {
            // Only respond during Extending phase
            if (grabPhase != GrabPhase.Extending) return;

            // Only grab our target visitor
            if (visitor != currentVisitor) return;

            TransitionToRetracting();
        }

        /// <summary>
        /// Updates the grabbing tongue's Z position (vertical emergence like HeartOfTheMaze).
        /// </summary>
        private void UpdateGrabbingTongueZPosition()
        {
            if (grabbingTongueInstance == null) return;

            // Update local Z position (tongue is child of grabbing zone at wall tile)
            Vector3 localPos = grabbingTongueInstance.transform.localPosition;
            localPos.z = grabbingTongueZPosition;
            grabbingTongueInstance.transform.localPosition = localPos;
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
            shape.radius = graspZoneRadius * 0.5f;

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
                if (distance <= graspZoneRadius)
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
            // NOTE: Do NOT stop visitor here - they continue walking until tongue contacts them

            // Spawn the tongue
            SpawnGrabbingTongue(visitor.transform.position);

            // Register frightening event - nearby visitors will flee
            if (FrighteningEventManager.Instance != null)
            {
                currentFrighteningEvent = FrighteningEventManager.Instance.RegisterEvent(
                    FrighteningEventManager.EventType.HeartwardGrasp,
                    grabbingWallPos,
                    this
                );
            }

            // Start emerging phase - tongue rises from underground (like HeartOfTheMaze)
            grabPhase = GrabPhase.Emerging;

            if (grabbingParticles != null) grabbingParticles.Emit(25);
        }

        /// <summary>
        /// Resets the grabbing tongue to rest state.
        /// </summary>
        private void ResetGrabbingTongue()
        {
            if (grabbingTongueBones == null || grabbingBoneRestRotations == null) return;

            for (int i = 0; i < grabbingTongueBones.Length; i++)
            {
                if (grabbingTongueBones[i] != null)
                {
                    grabbingTongueBones[i].localPosition = grabbingBoneRestPositions[i];
                    grabbingTongueBones[i].localRotation = grabbingBoneRestRotations[i];
                }
            }
        }

        /// <summary>
        /// Destroys the grabbing tongue instance and cleans up.
        /// </summary>
        private void DestroyGrabbingTongue()
        {
            // Unregister frightening event
            if (currentFrighteningEvent != null && FrighteningEventManager.Instance != null)
            {
                FrighteningEventManager.Instance.UnregisterEvent(currentFrighteningEvent);
                currentFrighteningEvent = null;
            }

            // Disable colliders and clear static flag
            SetGrabbingSolidCollidersEnabled(false);
            grabbingSolidColliders = null;

            if (grabbingTongueInstance != null)
            {
                Object.Destroy(grabbingTongueInstance);
                grabbingTongueInstance = null;
            }

            grabbingTongueBones = null;
            grabbingBoneRestPositions = null;
            grabbingBoneRestRotations = null;
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
                DestroyGrabbingTongue();
                grabPhase = GrabPhase.Idle;
                return;
            }

            switch (grabPhase)
            {
                case GrabPhase.Emerging:
                    UpdateGrabEmergingPhase(deltaTime);
                    break;

                case GrabPhase.Extending:
                    UpdateGrabExtendingPhase(deltaTime);
                    break;

                case GrabPhase.Retracting:
                    UpdateGrabRetractingPhase(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Emerging phase: Tongue rises from underground.
        /// Transitions to Extending when tip reaches ground level.
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabEmergingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Move tongue up (-Z is up in this coordinate system)
            grabbingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateGrabbingTongueZPosition();

            // Keep tongue straight (bones at rest pose)
            ApplyGrabbingTongueBoneState();

            // Calculate tip world Z position (accounting for parent Z offset)
            // Tip's world Z = parent Z + local tip Z = grabbingWallPos.z + (grabbingTongueZPosition - grabbingTongueLength)
            float tipWorldZ = grabbingWallPos.z + grabbingTongueZPosition - grabbingTongueLength;

            // Transition to Extending when tip reaches ground level (world Z=0)
            if (tipWorldZ <= TONGUE_GROUND_Z)
            {
                grabPhase = GrabPhase.Extending;

                // Enable colliders for collision detection
                SetGrabbingSolidCollidersEnabled(true);
            }
        }

        /// <summary>
        /// Extending phase: Tongue continues rising, bones bend 90° at ground level.
        /// Tongue tracks visitor position continuously.
        /// Transition to Retracting happens when visitor collides with tongue (NotifyVisitorTouchedGraspTongue).
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabExtendingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Continue rising (this increases horizontal extension)
            grabbingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdateGrabbingTongueZPosition();

            // Update target angle to track visitor (like HeartOfTheMaze)
            UpdateGrabbingTargetAngle();

            // Apply bone rotations (bend at ground level, extend toward visitor)
            ApplyGrabbingTongueBoneState();
        }

        /// <summary>
        /// Retracting phase: Visitor is grabbed, tongue descends back underground.
        /// Visitor is attached to tongue tip and follows it down.
        /// Transitions to transport when fully retracted.
        /// Same as HeartOfTheMaze.
        /// </summary>
        private void UpdateGrabRetractingPhase(float deltaTime)
        {
            if (grabbingTongueInstance == null || currentVisitor == null) return;

            // Retract tongue (increase Z)
            grabbingTongueZPosition += tongueRetractSpeed * deltaTime;
            UpdateGrabbingTongueZPosition();

            // Apply bone rotations
            ApplyGrabbingTongueBoneState();

            // Move visitor with tongue tip
            MoveGrabbedVisitorToTip();

            // Check if fully retracted
            if (grabbingTongueZPosition >= TONGUE_START_Z)
            {
                TransportVisitorToPushingZone();
            }
        }

        /// <summary>
        /// Transitions from Extending to Retracting phase.
        /// Called when visitor collides with tongue.
        /// Same pattern as HeartOfTheMaze.TransitionToGrabbing().
        /// </summary>
        private void TransitionToRetracting()
        {
            if (currentVisitor == null) return;

            // Stop visitor movement and set grabbed state (like HeartOfTheMaze)
            currentVisitor.SetGrabbedByHeart();

            // Deduct essence cost
            currentVisitor.DeductEssence(grabEssenceCost);

            // Notify nearby visitors
            HeartPowerEvents.NotifyVisitorGrabbedByGrasp(currentVisitor.transform.position);

            grabPhase = GrabPhase.Retracting;

            if (grabbingParticles != null) grabbingParticles.Emit(20);
        }

        /// <summary>
        /// Updates the target angle to track visitor position.
        /// Called each frame during Extending phase.
        /// Same as HeartOfTheMaze.UpdateTargetAngle().
        /// </summary>
        private void UpdateGrabbingTargetAngle()
        {
            if (currentVisitor == null) return;

            Vector2 wallPos2D = new Vector2(grabbingWallPos.x, grabbingWallPos.y);
            Vector2 visitorPos2D = new Vector2(currentVisitor.transform.position.x, currentVisitor.transform.position.y);
            Vector2 dirToVisitor = (visitorPos2D - wallPos2D).normalized;

            if (dirToVisitor.sqrMagnitude > 0.001f)
            {
                grabbingTargetAngle = Mathf.Atan2(dirToVisitor.y, dirToVisitor.x) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Moves the grabbed visitor to the tongue tip position.
        /// Called each frame during Retracting phase.
        /// Same as HeartOfTheMaze.MoveVisitorToTip().
        /// </summary>
        private void MoveGrabbedVisitorToTip()
        {
            if (currentVisitor == null || grabbingTongueBones == null || grabbingTongueBones.Length == 0) return;

            int tipIndex = grabbingTongueBones.Length - 1;
            Transform tipBone = grabbingTongueBones[tipIndex];

            if (tipBone != null)
            {
                // Position visitor at tip bone (maintain their original Z for proper rendering)
                Vector3 tipWorldPos = tipBone.position;
                currentVisitor.transform.position = new Vector3(
                    tipWorldPos.x,
                    tipWorldPos.y,
                    currentVisitor.transform.position.z
                );
            }
        }

        /// <summary>
        /// Transports the visitor from grabbing zone to pushing zone.
        /// Called when tongue is fully retracted.
        /// Spawns the pushing tongue immediately and positions visitor at its tip.
        /// </summary>
        private void TransportVisitorToPushingZone()
        {
            // Hide visitor during transport (they stay hidden until tongue emerges above ground)
            SetVisitorVisible(currentVisitor, false);

            // Destroy grabbing tongue
            DestroyGrabbingTongue();

            // Spawn pushing tongue immediately (starts underground)
            SpawnPushingTongue();

            // Apply initial tongue state (straight, pointing up)
            ApplyPushingTongueBoneState();

            // Position visitor at tongue tip (underground, hidden)
            MovePushedVisitorToTip();

            // Start Emerging phase immediately (no pause needed)
            pushPhase = PushPhase.Emerging;
            pushPhaseStartTime = elapsedTime;

            if (pushingParticles != null) pushingParticles.Emit(25);

            grabPhase = GrabPhase.Idle;
        }

        /// <summary>
        /// Applies bone rotations to the grabbing tongue based on current phase.
        /// Simplified to match HeartOfTheMaze - bones below ground straight, bend at ground, extend horizontally.
        /// </summary>
        private void ApplyGrabbingTongueBoneState()
        {
            if (grabbingTongueBones == null || grabbingTongueBones.Length == 0 || grabbingTongueInstance == null) return;

            int boneCount = grabbingTongueBones.Length;
            float boneSpacing = grabbingTongueLength / Mathf.Max(1, boneCount);

            // Update tongue instance rotation to point toward visitor
            grabbingTongueInstance.transform.localRotation =
                Quaternion.Euler(0f, 0f, grabbingTargetAngle) * Quaternion.Euler(0f, -90f, 0f);

            // During Emerging phase, keep all bones at rest pose (straight tongue)
            if (grabPhase == GrabPhase.Emerging)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (grabbingTongueBones[i] == null) continue;
                    grabbingTongueBones[i].localPosition = grabbingBoneRestPositions[i];
                    grabbingTongueBones[i].localRotation = grabbingBoneRestRotations[i];
                }
                return;
            }

            // Find which bone is at ground level (world Z=0)
            // The tongue is a child of grabbingZoneObject at grabbingWallPos.z
            // Bone i's world Z = grabbingWallPos.z + grabbingTongueZPosition - (i * boneSpacing)
            // We want world Z <= TONGUE_GROUND_Z (0), so:
            // grabbingWallPos.z + grabbingTongueZPosition - (i * boneSpacing) <= 0
            int groundBoneIndex = -1;
            for (int i = 0; i < boneCount; i++)
            {
                float boneWorldZ = grabbingWallPos.z + grabbingTongueZPosition - (i * boneSpacing);
                if (boneWorldZ <= TONGUE_GROUND_Z)
                {
                    groundBoneIndex = i;
                    break;
                }
            }

            // If no bone has emerged yet, keep all at rest
            if (groundBoneIndex < 0)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (grabbingTongueBones[i] == null) continue;
                    grabbingTongueBones[i].localPosition = grabbingBoneRestPositions[i];
                    grabbingTongueBones[i].localRotation = grabbingBoneRestRotations[i];
                }
                return;
            }

            // Reset bones below ground level to rest pose
            for (int i = 0; i < groundBoneIndex; i++)
            {
                if (grabbingTongueBones[i] == null) continue;
                grabbingTongueBones[i].localPosition = grabbingBoneRestPositions[i];
                grabbingTongueBones[i].localRotation = grabbingBoneRestRotations[i];
            }

            // Apply rotations to bones at and above ground level
            ApplyTongueBoneRotations(
                grabbingTongueBones,
                grabbingBoneRestRotations,
                groundBoneIndex,
                grabbingTargetAngle
            );
        }

        /// <summary>
        /// Shared method to apply bone rotations for tongue extending horizontally at ground level.
        /// Same logic as HeartOfTheMaze.ApplyTongueBoneRotations().
        /// Bones from lipBoneIndex onward are rotated to bend 90° and extend toward targetAngle.
        /// </summary>
        private void ApplyTongueBoneRotations(
            Transform[] bones,
            Quaternion[] restRotations,
            int lipBoneIndex,
            float targetAngle)
        {
            if (bones == null || bones.Length == 0) return;

            int boneCount = bones.Length;
            int bendEndIndex = Mathf.Min(lipBoneIndex + BEND_BONE_COUNT, boneCount - 1);

            // Direction toward target (horizontal)
            Vector3 targetDirWorld = new Vector3(
                Mathf.Cos(targetAngle * Mathf.Deg2Rad),
                Mathf.Sin(targetAngle * Mathf.Deg2Rad),
                0f
            );

            // Apply rotations from lip bone to tip
            for (int i = lipBoneIndex; i < boneCount && lipBoneIndex >= 0; i++)
            {
                if (bones[i] == null) continue;

                Quaternion parentWorldRot = bones[i].parent != null ?
                    bones[i].parent.rotation : Quaternion.identity;

                // Bone forward direction (local +Y points toward next bone)
                Vector3 boneLocalDir = Vector3.up;
                Vector3 boneWorldDir = parentWorldRot * restRotations[i] * boneLocalDir;

                Vector3 desiredDir;

                if (i >= lipBoneIndex && i <= bendEndIndex)
                {
                    // Bones in bend zone: interpolate from vertical (-Z is up) to horizontal
                    float bendT = (float)(i - lipBoneIndex) / (float)BEND_BONE_COUNT;
                    bendT = Mathf.Clamp01(bendT);
                    Vector3 upDir = Vector3.back;  // -Z is up
                    desiredDir = Vector3.Slerp(upDir, targetDirWorld, bendT);
                }
                else
                {
                    // Bones past bend zone: point horizontally toward target
                    desiredDir = targetDirWorld;
                }

                // Compute rotation to align bone toward desired direction
                if (desiredDir.sqrMagnitude > 0.0001f && boneWorldDir.sqrMagnitude > 0.0001f)
                {
                    desiredDir = desiredDir.normalized;
                    boneWorldDir = boneWorldDir.normalized;

                    float dot = Vector3.Dot(boneWorldDir, desiredDir);
                    Quaternion worldCorrection;

                    if (dot > 0.9999f)
                    {
                        worldCorrection = Quaternion.identity;
                    }
                    else if (dot < -0.9999f)
                    {
                        Vector3 perpAxis = Vector3.Cross(boneWorldDir, Vector3.up);
                        if (perpAxis.sqrMagnitude < 0.0001f)
                            perpAxis = Vector3.Cross(boneWorldDir, Vector3.right);
                        perpAxis.Normalize();
                        worldCorrection = Quaternion.AngleAxis(180f, perpAxis);
                    }
                    else
                    {
                        worldCorrection = Quaternion.FromToRotation(boneWorldDir, desiredDir);
                    }

                    Quaternion newLocalRot = Quaternion.Inverse(parentWorldRot) * worldCorrection * parentWorldRot * restRotations[i];

                    if (!float.IsNaN(newLocalRot.x) && !float.IsNaN(newLocalRot.y) && !float.IsNaN(newLocalRot.z) && !float.IsNaN(newLocalRot.w))
                    {
                        bones[i].localRotation = newLocalRot;
                    }
                }
            }
        }

        private void UpdatePushingHGZ(float deltaTime)
        {
            if (pushPhase == PushPhase.Idle) return;
            if (currentVisitor == null)
            {
                DestroyPushingTongue();
                pushPhase = PushPhase.Idle;
                return;
            }

            float phaseElapsed = elapsedTime - pushPhaseStartTime;

            switch (pushPhase)
            {
                case PushPhase.Emerging:
                    // Tongue rises from underground with visitor on tip
                    UpdatePushEmergingPhase(deltaTime);
                    break;

                case PushPhase.Extending:
                    // Tongue continues rising, extending toward walkable area
                    UpdatePushExtendingPhase(deltaTime);
                    break;

                case PushPhase.Releasing:
                    // Visitor released, tongue starts retracting
                    UpdatePushReleasingPhase(deltaTime);
                    break;

                case PushPhase.Retracting:
                    // Tongue retracts back underground
                    UpdatePushRetractingPhase(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Emerging phase (pushing): Tongue rises from underground with visitor attached to tip.
        /// Reverse of grab's Retracting phase.
        /// Transitions to Extending when tip reaches ground level.
        /// </summary>
        private void UpdatePushEmergingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null || currentVisitor == null) return;

            // Move tongue up (-Z is up)
            pushingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdatePushingTongueZPosition();

            // Keep tongue straight during emergence
            ApplyPushingTongueBoneState();

            // Move visitor with tongue tip
            MovePushedVisitorToTip();

            // Calculate tip world Z position (accounting for parent Z offset)
            float tipWorldZ = pushingWallPos.z + pushingTongueZPosition - pushingTongueLength;

            // Transition to Extending when tip reaches ground level (world Z=0)
            if (tipWorldZ <= TONGUE_GROUND_Z)
            {
                pushPhase = PushPhase.Extending;
                pushPhaseStartTime = elapsedTime;

                // Make visitor visible now that they're above ground
                SetVisitorVisible(currentVisitor, true);
            }
        }

        /// <summary>
        /// Extending phase (pushing): Tongue continues rising, bones bend at ground to extend horizontally.
        /// Reverse of grab's Extending phase.
        /// Transitions to Releasing when visitor is over walkable area.
        /// </summary>
        private void UpdatePushExtendingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null || currentVisitor == null) return;

            // Continue rising (increases horizontal extension)
            pushingTongueZPosition -= tongueEmergeSpeed * deltaTime;
            UpdatePushingTongueZPosition();

            // Apply bone rotations (bend at ground level, extend toward heart)
            ApplyPushingTongueBoneState();

            // Move visitor with tongue tip
            MovePushedVisitorToTip();

            // Check if visitor is over walkable area
            Vector3 visitorPos = currentVisitor.transform.position;
            if (IsVisitorOnValidWalkableArea(visitorPos))
            {
                TransitionToReleasing();
            }
        }

        /// <summary>
        /// Transitions to Releasing phase - releases visitor and starts retraction.
        /// </summary>
        private void TransitionToReleasing()
        {
            // Resume visitor movement
            if (currentVisitor != null)
            {
                Vector3 posBeforeRelease = currentVisitor.transform.position;

                // CRITICAL: Set visitor to ground level Z before releasing
                // The tongue tip may be slightly above or below ground, but visitors must be at Z≈0
                const float GROUND_Z = -0.01f;  // Slightly above ground (Z=0 is ground, -Z is up)
                currentVisitor.transform.position = new Vector3(
                    posBeforeRelease.x,
                    posBeforeRelease.y,
                    GROUND_Z
                );

                // Ensure visitor is visible when released
                SetVisitorVisible(currentVisitor, true);

                // CRITICAL: Clear the Grabbed state before resuming
                // RefreshStateFromFlags() returns early for Grabbed state, so we must clear it first
                currentVisitor.ClearGrabbedState();

                currentVisitor.Resume();
                currentVisitor.RecalculatePath();

                // Apply daze effect
                float dazeDuration = definition.param1 > 0 ? definition.param1 : 2f;
                currentVisitor.OnWitnessMazeGrowth(dazeDuration);

                // Notify of push
                HeartPowerEvents.NotifyVisitorPushedByGrasp(currentVisitor.transform.position);
            }

            pushPhase = PushPhase.Releasing;
            pushPhaseStartTime = elapsedTime;

            if (pushingParticles != null) pushingParticles.Emit(15);
        }

        /// <summary>
        /// Releasing phase: Brief pause after releasing visitor.
        /// Transitions to Retracting quickly.
        /// </summary>
        private void UpdatePushReleasingPhase(float deltaTime)
        {
            // Brief pause (0.2 seconds) then start retracting
            float phaseElapsed = elapsedTime - pushPhaseStartTime;
            if (phaseElapsed >= 0.2f)
            {
                pushPhase = PushPhase.Retracting;
                pushPhaseStartTime = elapsedTime;
            }
        }

        /// <summary>
        /// Retracting phase (pushing): Tongue descends back underground.
        /// Reverse of grab's Emerging phase.
        /// Finalizes capture when fully retracted.
        /// </summary>
        private void UpdatePushRetractingPhase(float deltaTime)
        {
            if (pushingTongueInstance == null) return;

            // Retract tongue (increase Z)
            pushingTongueZPosition += tongueRetractSpeed * deltaTime;
            UpdatePushingTongueZPosition();

            // Apply bone rotations
            ApplyPushingTongueBoneState();

            // Check if fully retracted
            if (pushingTongueZPosition >= TONGUE_START_Z)
            {
                FinalizeCapture();
            }
        }

        /// <summary>
        /// Updates the pushing tongue's Z position.
        /// </summary>
        private void UpdatePushingTongueZPosition()
        {
            if (pushingTongueInstance == null) return;

            Vector3 localPos = pushingTongueInstance.transform.localPosition;
            localPos.z = pushingTongueZPosition;
            pushingTongueInstance.transform.localPosition = localPos;
        }

        /// <summary>
        /// Moves the visitor being pushed to the tongue tip position.
        /// </summary>
        private void MovePushedVisitorToTip()
        {
            if (currentVisitor == null || pushingTongueBones == null || pushingTongueBones.Length == 0) return;

            int tipIndex = pushingTongueBones.Length - 1;
            Transform tipBone = pushingTongueBones[tipIndex];

            if (tipBone != null)
            {
                Vector3 tipWorldPos = tipBone.position;
                currentVisitor.transform.position = new Vector3(
                    tipWorldPos.x,
                    tipWorldPos.y,
                    currentVisitor.transform.position.z
                );
            }
        }

        /// <summary>
        /// Applies bone rotations to the pushing tongue.
        /// Same logic as grabbing tongue - bones below ground are straight,
        /// bones at ground level bend 90°, bones above ground extend horizontally toward heart.
        /// Uses the same ApplyTongueBoneRotations logic as the grabbing tongue.
        /// </summary>
        private void ApplyPushingTongueBoneState()
        {
            if (pushingTongueBones == null || pushingTongueBones.Length == 0 || pushingTongueInstance == null) return;

            int boneCount = pushingTongueBones.Length;
            float boneSpacing = pushingTongueLength / Mathf.Max(1, boneCount);

            // Update tongue instance rotation to point toward heart
            pushingTongueInstance.transform.localRotation =
                Quaternion.Euler(0f, 0f, pushingTargetAngle) * Quaternion.Euler(0f, -90f, 0f);

            // Find which bone is at ground level (world Z=0)
            // The tongue is a child of pushingZoneObject at pushingWallPos.z
            // Bone i's world Z = pushingWallPos.z + pushingTongueZPosition - (i * boneSpacing)
            // We want world Z <= TONGUE_GROUND_Z (0)
            int groundBoneIndex = -1;
            for (int i = 0; i < boneCount; i++)
            {
                float boneWorldZ = pushingWallPos.z + pushingTongueZPosition - (i * boneSpacing);
                if (boneWorldZ <= TONGUE_GROUND_Z)
                {
                    groundBoneIndex = i;
                    break;
                }
            }

            // If no bone has emerged yet, keep all at rest
            if (groundBoneIndex < 0)
            {
                for (int i = 0; i < boneCount; i++)
                {
                    if (pushingTongueBones[i] == null) continue;
                    pushingTongueBones[i].localPosition = pushingBoneRestPositions[i];
                    pushingTongueBones[i].localRotation = pushingBoneRestRotations[i];
                }
                return;
            }

            // Reset bones below ground level to rest pose
            for (int i = 0; i < groundBoneIndex; i++)
            {
                if (pushingTongueBones[i] == null) continue;
                pushingTongueBones[i].localPosition = pushingBoneRestPositions[i];
                pushingTongueBones[i].localRotation = pushingBoneRestRotations[i];
            }

            // Apply rotations to bones at and above ground level
            ApplyTongueBoneRotations(
                pushingTongueBones,
                pushingBoneRestRotations,
                groundBoneIndex,
                pushingTargetAngle
            );
        }

        /// <summary>
        /// Destroys the pushing tongue instance and cleans up.
        /// </summary>
        private void DestroyPushingTongue()
        {
            if (pushingTongueInstance != null)
            {
                Object.Destroy(pushingTongueInstance);
                pushingTongueInstance = null;
            }

            pushingTongueBones = null;
            pushingBoneRestPositions = null;
            pushingBoneRestRotations = null;
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

            // Destroy the pushing tongue
            DestroyPushingTongue();
        }

        /// <summary>
        /// Updates touch collider and visitor positions during push phase (LEGACY - not used with tongue).
        /// Kept for potential future use or fallback.
        /// </summary>
        private void UpdatePushPositions(Vector3 handPos)
        {
            // LEGACY: This method was used with the hand-based push system
            // Now the tongue-based system handles visitor positioning directly
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
            // Clear static instance reference
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            // Clear tongue references and destroy tongue instances
            DestroyGrabbingTongue();
            DestroyPushingTongue();

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

        /// <summary>
        /// Returns the world positions of both grabbing and pushing zones.
        /// Used by WaryWayfarer for hazard avoidance pathfinding.
        /// </summary>
        public List<Vector3> GetZonePositions()
        {
            var positions = new List<Vector3>();
            if (grabbingZoneObject != null)
                positions.Add(grabbingZoneObject.transform.position);
            if (pushingZoneObject != null)
                positions.Add(pushingZoneObject.transform.position);
            return positions;
        }
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
        // Settings - Loaded from GameSettings
        private readonly float triggerRadius;

        // Constants - Architectural (not configurable)
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

        // Consumption-based expiration (like MurmuringPaths)
        private int requiredConsumptions = 1;
        private int consumedCount = 0;
        private bool hasExpired = false;

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

        // Frightening event (registered when devour cycle is active)
        private FrighteningEventManager.FrighteningEvent currentFrighteningEvent;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            // Load settings from GameSettings
            triggerRadius = GameSettings.DevouringMawRadius;
        }

        /// <summary>
        /// DevouringMaw uses consumption-based expiration, not duration.
        /// Return a high value so the duration check doesn't prematurely expire the effect.
        /// </summary>
        public override float Duration => float.MaxValue;

        /// <summary>
        /// Override IsExpired to use consumption-based expiration instead of duration.
        /// Power expires when consumed visitor count reaches the power tier.
        /// Also extends while a devour cycle is still in progress.
        /// </summary>
        public override bool IsExpired
        {
            get
            {
                // Extend if a cycle is still in progress
                if (cycleInProgress)
                {
                    return false;
                }
                return hasExpired;
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

        /// <summary>
        /// Called by DevourTriggerHandler when a visitor enters the maw's trigger zone.
        /// Captures visitors during Emerging, Paused, or early Sinking phases.
        /// Sets the visitor to Grabbed state to prevent movement.
        /// </summary>
        public void NotifyVisitorEnteredMaw(VisitorControllerBase visitor)
        {
            // Capture during active phases (not Idle or Complete)
            if (currentPhase == DevourPhase.Idle || currentPhase == DevourPhase.Complete)
            {
                return;
            }

            // Don't capture already-captured visitors
            if (visitorsBeingDevoured.Contains(visitor))
            {
                return;
            }

            // Don't capture visitors in invalid states
            if (!HeartPowerUtils.IsVisitorTargetable(visitor))
            {
                return;
            }

            // Capture the visitor - set to Grabbed state to completely stop movement
            visitor.Stop();
            visitor.SetGrabbedByHeart();
            visitorsBeingDevoured.Add(visitor);
            visitorStartPositions[visitor] = visitor.transform.position;

            Debug.Log($"[DevouringMaw] Captured visitor via trigger: {visitor.name}, phase={currentPhase}");
        }

        public override void OnStart()
        {
            targetWorldPos = targetPosition;

            // Set required consumptions to the power tier (like MurmuringPaths)
            requiredConsumptions = manager.GetPowerTier(HeartPowerType.DevouringMaw);
            consumedCount = 0;
            hasExpired = false;

            // Duration for tile visualizer display (not used for expiration)
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

        public override void OnEnd()
        {
            // Unregister frightening event
            if (currentFrighteningEvent != null && FrighteningEventManager.Instance != null)
            {
                FrighteningEventManager.Instance.UnregisterEvent(currentFrighteningEvent);
                currentFrighteningEvent = null;
            }

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

        /// <summary>
        /// Returns the target world position of the DevouringMaw zone.
        /// Used by WaryWayfarer for hazard avoidance pathfinding.
        /// </summary>
        public Vector3 GetTargetPosition() => targetWorldPos;

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
                    int centerX = RandomManager.Range(sampleRadius, earthenTexture.width - sampleRadius);
                    int centerY = RandomManager.Range(sampleRadius, earthenTexture.height - sampleRadius);

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
            Collider[] colliders = Physics.OverlapSphere(targetWorldPos, triggerRadius * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);

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
                                int centerX = RandomManager.Range(sampleRadius, tex.width - sampleRadius);
                                int centerY = RandomManager.Range(sampleRadius, tex.height - sampleRadius);

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
            HeartPowerUtils.FindPathTilesInRadius(targetWorldPos, triggerRadius, affectedPathTiles, originalTilePositions);
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
                float x = Mathf.Cos(angle) * triggerRadius;
                float y = Mathf.Sin(angle) * triggerRadius;

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
            shape.radius = triggerRadius;
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
            shape.radius = triggerRadius;
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
            return HeartPowerUtils.FindVisitorInRadius(targetWorldPos, triggerRadius, visitorsBeingDevoured);
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

            // Register frightening event - nearby visitors will flee from the devour
            if (FrighteningEventManager.Instance != null)
            {
                currentFrighteningEvent = FrighteningEventManager.Instance.RegisterEvent(
                    FrighteningEventManager.EventType.DevouringMaw,
                    devourLocation,
                    this
                );
            }

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
                    // Stop visitor and set Grabbed state to prevent any movement
                    visitor.Stop();
                    visitor.SetGrabbedByHeart();
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
                    // Use Rigidbody.MovePosition for physics-compatible movement
                    foreach (var visitor in visitorsBeingDevoured)
                    {
                        if (visitor != null && visitorStartPositions.TryGetValue(visitor, out Vector3 startPos))
                        {
                            Vector3 visitorPos = startPos;
                            visitorPos.z = Mathf.Lerp(startPos.z, 1f, sinkT);

                            // Physics-based positioning - will throw NullReferenceException if Rigidbody missing
                            Rigidbody visitorRb = visitor.GetComponent<Rigidbody>();
                            visitorRb.MovePosition(visitorPos);
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
                    // Unregister frightening event
                    if (currentFrighteningEvent != null && FrighteningEventManager.Instance != null)
                    {
                        FrighteningEventManager.Instance.UnregisterEvent(currentFrighteningEvent);
                        currentFrighteningEvent = null;
                    }

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
            }

            // Fix MawThroat mesh rendering
            SetDoubleSidedRendering(devourVisual);

            // Connect the trigger handler so it can notify us when visitors enter the maw
            var handler = devourVisual.GetComponentInChildren<DevourTriggerHandler>();
            if (handler != null)
            {
                handler.SetOwner(this);
            }
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

            // Track visitor fate with essence value
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, Systems.VisitorFate.Devoured, essence);
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Notify nearby visitors that consumption occurred - they become frightened
            HeartPowerEvents.NotifyVisitorConsumedByMaw(consumptionPosition);

            Object.Destroy(visitor.gameObject);

            // Increment consumption count and check for expiration (like MurmuringPaths)
            consumedCount++;
            if (consumedCount >= requiredConsumptions)
            {
                hasExpired = true;
            }
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
        // Static instance for tutorial access
        public static SculptingEffect ActiveInstance { get; private set; }

        // Menu state
        private bool menuActive = false;

        /// <summary>
        /// Returns true if the sculpt menu is currently open.
        /// </summary>
        public bool IsMenuActive => menuActive;
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

        // Tutorial mode - only allow lantern selection
        private bool tutorialLanternOnlyMode = false;

        public SculptingEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition) { }

        /// <summary>
        /// Override IsExpired - expires when action is applied or menu is cancelled
        /// </summary>
        public override bool IsExpired => actionApplied;

        /// <summary>
        /// Highlights only the lantern button and disables all other buttons.
        /// Used by the tutorial to guide the player to select the lantern.
        /// Also disables keyboard shortcuts for non-lantern options.
        /// </summary>
        public void HighlightLanternButtonOnly()
        {
            if (!menuActive) return;

            // Enable tutorial mode - blocks non-lantern keyboard shortcuts
            tutorialLanternOnlyMode = true;

            // Visually dim and disable all buttons except lantern (bottomButton)
            DimAndDisableButton(centerButton);
            DimAndDisableButton(topButton);
            DimAndDisableButton(leftButton);
            DimAndDisableButton(rightButton);

            // Keep lantern button enabled and add a pulsing highlight effect
            if (bottomButton != null)
            {
                bottomButton.interactable = true;

                // Add a pulsing glow effect to the lantern button
                var buttonImage = bottomButton.GetComponent<UnityEngine.UI.Image>();
                if (buttonImage != null)
                {
                    // Create a coroutine host for the pulse effect
                    var pulseHost = bottomButton.gameObject.AddComponent<ButtonPulseEffect>();
                    pulseHost.StartPulse(buttonImage, LanternColor);
                }
            }
        }

        /// <summary>
        /// Dims a button visually and disables interaction.
        /// </summary>
        private void DimAndDisableButton(UnityEngine.UI.Button button)
        {
            if (button == null) return;

            button.interactable = false;

            // Visually dim the button by reducing alpha/brightness
            var bgImage = button.GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                Color dimColor = bgImage.color;
                dimColor.a = 0.3f; // Reduce opacity significantly
                dimColor.r *= 0.5f;
                dimColor.g *= 0.5f;
                dimColor.b *= 0.5f;
                bgImage.color = dimColor;
            }

            // Also dim the border (parent object)
            var borderImage = button.transform.parent?.GetComponent<UnityEngine.UI.Image>();
            if (borderImage != null)
            {
                Color dimBorderColor = borderImage.color;
                dimBorderColor.a = 0.3f;
                borderImage.color = dimBorderColor;
            }

            // Dim content image if present
            var contentImage = button.transform.Find("Content")?.GetComponent<UnityEngine.UI.Image>();
            if (contentImage != null)
            {
                Color dimContentColor = contentImage.color;
                dimContentColor.a = 0.3f;
                contentImage.color = dimContentColor;
            }
        }

        public override void OnStart()
        {
            // Set static instance for tutorial access
            ActiveInstance = this;

            // Find DynamicMazeGrowth
            dynamicMazeGrowth = Object.FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth == null)
            {
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

            // Clear static instance
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (!menuActive)
                return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;

            // Check for escape key to cancel (disabled in tutorial mode)
            if (!tutorialLanternOnlyMode && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelMenu();
                return;
            }

            // Keyboard shortcuts for sculpt menu options
            // In tutorial mode, only lantern shortcut is allowed
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasBindingPressedThisFrame(GameSettings.SculptPondBinding))
            {
                OnPondClicked();
                return;
            }
            if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.SculptLanternBinding))
            {
                OnLanternClicked();
                return;
            }
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasBindingPressedThisFrame(GameSettings.SculptRingBinding))
            {
                OnRingClicked();
                return;
            }
            if (!tutorialLanternOnlyMode && InputBindingHelper.WasBindingPressedThisFrame(GameSettings.SculptRemoveBinding))
            {
                OnRemoveClicked();
                return;
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

            // Calculate sizes based on reference resolution height (50% of screen height for entire menu)
            // Use reference height (1080) since CanvasScaler is set to ScaleWithScreenSize
            float referenceHeight = 1080f;
            float menuSize = referenceHeight * MENU_SCREEN_HEIGHT_FRACTION;
            float menuRadius = menuSize * MENU_RADIUS_FRACTION;
            float buttonSize = menuSize * BUTTON_SIZE_FRACTION;
            float centerButtonSize = menuSize * CENTER_BUTTON_FRACTION;

            // Find PowerButton_3 (Sculpting button) to center the menu directly above it
            // Default to screen center (will be converted to canvas coordinates below)
            Vector2 screenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GameObject powerButton = GameObject.Find("PowerButton_3");

            if (powerButton != null)
            {
                RectTransform powerButtonRect = powerButton.GetComponent<RectTransform>();
                if (powerButtonRect != null)
                {
                    // Get the screen position of the power button (world corners = screen coords for overlay canvas)
                    Vector3[] corners = new Vector3[4];
                    powerButtonRect.GetWorldCorners(corners);
                    // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right

                    Vector2 buttonCenter = new Vector2((corners[0].x + corners[2].x) / 2f, (corners[0].y + corners[2].y) / 2f);
                    float powerButtonTop = corners[1].y;

                    // Position menu so the bottom sculpt button sits just above the power button
                    // Need to calculate in screen pixels first, then convert to canvas coordinates
                    float gap = 15f; // Gap in screen pixels
                    // In screen space: menu center Y = powerButtonTop + gap + (distance from menu center to bottom of bottom button)
                    // The bottom button center is at menuRadius below menu center, and button extends buttonSize/2 below that
                    // So menu center Y = powerButtonTop + gap + menuRadius + buttonSize/2
                    // But menuRadius and buttonSize are in reference resolution (1080p), need to scale
                    float scaleFactor = Screen.height / 1080f;
                    float scaledMenuRadius = menuRadius * scaleFactor;
                    float scaledButtonSize = buttonSize * scaleFactor;
                    float menuCenterY = powerButtonTop + gap + scaledMenuRadius + scaledButtonSize * 0.5f;
                    screenPos = new Vector2(buttonCenter.x, menuCenterY);
                }
            }

            // Convert screen position to canvas local position
            // The canvas uses CanvasScaler with reference 1920x1080, so we need to scale
            float canvasScaleX = 1920f / Screen.width;
            float canvasScaleY = 1080f / Screen.height;
            Vector2 canvasPos = new Vector2(screenPos.x * canvasScaleX, screenPos.y * canvasScaleY);

            // Create a panel at the calculated position
            GameObject panelObj = new GameObject("MenuPanel");
            panelObj.transform.SetParent(canvasRect, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = canvasPos;
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

    /// <summary>
    /// Simple MonoBehaviour component to pulse a button's color for highlighting.
    /// </summary>
    public class ButtonPulseEffect : MonoBehaviour
    {
        private UnityEngine.UI.Image targetImage;
        private Color baseColor;
        private float pulseSpeed = 2f;
        private float pulseIntensity = 0.3f;

        public void StartPulse(UnityEngine.UI.Image image, Color baseCol)
        {
            targetImage = image;
            baseColor = baseCol;
        }

        private void Update()
        {
            if (targetImage == null) return;

            // Pulse brightness using sine wave
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
            float brightness = 1f + pulse * pulseIntensity;

            // Apply brighter color
            Color pulsedColor = new Color(
                Mathf.Min(baseColor.r * brightness, 1f),
                Mathf.Min(baseColor.g * brightness, 1f),
                Mathf.Min(baseColor.b * brightness, 1f),
                baseColor.a
            );

            targetImage.color = pulsedColor;
        }
    }

    #endregion
}

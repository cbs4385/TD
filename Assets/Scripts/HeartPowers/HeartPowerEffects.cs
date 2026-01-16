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

        // Visual elements - fairy ring style lights with trails
        private GameObject visualContainer;
        private List<PathLight> pathLightObjects = new List<PathLight>();

        // Light settings (reduced intensity - 25% of FairyRingSphere)
        private const float LIGHT_INTENSITY = 1.25f; // 25% of FairyRingSphere's 5f intensity
        private const float LIGHT_RANGE = 1.2f; // Match FairyRingSphere range
        private const float LIGHT_Z_OFFSET = -0.5f; // Slight offset above path
        private const int LIGHTS_PER_NODE = 8; // Number of lights per affected node
        private const int LIGHTS_PER_EDGE = 4; // Number of lights per affected edge

        // Rainbow hues (same as FairyRingSphere)
        private static readonly float[] RainbowHues = new float[]
        {
            0.00f,  // Red
            0.08f,  // Orange
            0.16f,  // Yellow
            0.33f,  // Green
            0.50f,  // Cyan
            0.66f,  // Blue
            0.80f,  // Violet
        };

        // Animation settings
        private float moveSpeed = 6.0f; // How fast lights move along path toward heart

        // Path boundary settings - lights travel the full path to the heart
        private const float PATH_END_THRESHOLD = 1.0f; // Travel full path
        private const float MIN_RESPAWN_DELAY = 0.2f; // Minimum delay between light spawns
        private const float MAX_RESPAWN_DELAY = 1.5f; // Maximum delay between light spawns

        // Erratic movement settings
        private const float MIN_WANDER_AMPLITUDE = 0.8f; // Minimum wander distance from path
        private const float MAX_WANDER_AMPLITUDE = 2.0f; // Maximum wander distance from path
        private const float MIN_WANDER_SPEED = 2.0f; // Minimum wander velocity
        private const float MAX_WANDER_SPEED = 5.0f; // Maximum wander velocity
        private const float MIN_DIRECTION_CHANGE = 0.2f; // Minimum time between direction changes
        private const float MAX_DIRECTION_CHANGE = 0.8f; // Maximum time between direction changes

        // Store all paths from affected positions to heart
        private List<List<Vector3>> allPathsToHeart = new List<List<Vector3>>();

        /// <summary>
        /// Helper class to manage individual path lights with trails
        /// </summary>
        private class PathLight
        {
            public GameObject gameObject;
            public Light light;
            public TrailRenderer trail;
            public int pathIndex;
            public float normalizedPosition; // 0-1 position along path
            public float colorTimeOffset;
            public float cycleDuration;
            public bool trailStarted; // Track if trail has begun emitting
            public float respawnDelay; // Random delay before respawning
            public float currentDelay; // Current delay countdown
            public bool isWaiting; // Whether light is waiting to respawn

            // Erratic movement properties
            public Vector2 wanderOffset; // Current offset from path center
            public Vector2 wanderVelocity; // Current wander velocity
            public float wanderChangeTimer; // Time until next direction change
            public float wanderAmplitude; // How far this light wanders from path
            public float wanderSpeed; // How fast this light changes direction
        }

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
            if (hasExpired) return;

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

            // Identify ALL nodes and edges along the path, then get positions from all of them
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

            // Create fairy ring style lights along all paths
            CreateFairyRingStyleLights();

            // Apply lure to all visitors currently on the affected node/edge
            ApplyLureToVisitorsOnAffectedArea();
        }

        public override void OnEnd()
        {
            // Remove all visual elements
            if (visualContainer != null)
            {
                Object.Destroy(visualContainer);
                visualContainer = null;
            }

            pathLightObjects.Clear();

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
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            animationTime += deltaTime;

            // Animate the lights - move along path and update colors
            UpdateFairyRingLights(deltaTime);

            // Check for new visitors entering the affected area
            CheckForNewVisitorsOnAffectedArea();

            // Clean up destroyed visitors from tracking set
            affectedVisitors.RemoveWhere(v => v == null);
        }

        /// <summary>
        /// Creates fairy ring style lights with trails along all paths to the heart.
        /// Generates a fixed number of lights per affected node and edge for denser coverage.
        /// </summary>
        private void CreateFairyRingStyleLights()
        {
            if (allPathsToHeart.Count == 0)
                return;

            visualContainer = new GameObject($"MurmuringPathsLights_{instanceSourceId}");

            int lightIndex = 0;

            // Calculate total lights needed based on affected nodes and edges
            int totalNodeLights = allAffectedNodeIndices.Count * LIGHTS_PER_NODE;
            int totalEdgeLights = allAffectedEdgeIndices.Count * LIGHTS_PER_EDGE;
            int totalLightsNeeded = totalNodeLights + totalEdgeLights;

            // Ensure we have at least some lights
            totalLightsNeeded = Mathf.Max(totalLightsNeeded, 4);

            // Distribute lights across all paths proportionally
            if (allPathsToHeart.Count > 0)
            {
                // Calculate total path length for proportional distribution
                float[] pathLengths = new float[allPathsToHeart.Count];
                float totalLength = 0f;
                for (int pathIdx = 0; pathIdx < allPathsToHeart.Count; pathIdx++)
                {
                    var path = allPathsToHeart[pathIdx];
                    float pathLen = CalculatePathLength(path);
                    pathLengths[pathIdx] = pathLen;
                    totalLength += pathLen;
                }

                // Create lights distributed across paths based on length
                for (int pathIdx = 0; pathIdx < allPathsToHeart.Count; pathIdx++)
                {
                    var path = allPathsToHeart[pathIdx];
                    if (path.Count < 2) continue;

                    // Calculate lights for this path proportional to its length
                    float pathProportion = totalLength > 0 ? pathLengths[pathIdx] / totalLength : 1f / allPathsToHeart.Count;
                    int numLightsForPath = Mathf.Max(1, Mathf.RoundToInt(totalLightsNeeded * pathProportion));

                    // Create lights for this path with staggered starting positions
                    for (int i = 0; i < numLightsForPath; i++)
                    {
                        float startPos = (float)i / numLightsForPath;
                        // Add some randomness to starting positions
                        startPos += UnityEngine.Random.Range(-0.05f, 0.05f);
                        startPos = Mathf.Clamp01(startPos);

                        var pathLight = CreateSingleFairyLight(lightIndex, pathIdx, startPos);
                        if (pathLight != null)
                        {
                            pathLightObjects.Add(pathLight);
                            lightIndex++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a single fairy ring style light with trail renderer.
        /// </summary>
        private PathLight CreateSingleFairyLight(int lightIndex, int pathIndex, float startingPosition)
        {
            if (pathIndex >= allPathsToHeart.Count)
                return null;

            var path = allPathsToHeart[pathIndex];
            if (path.Count < 2)
                return null;

            // Get starting position along path
            Vector3 startPos = GetPositionAlongPath(path, startingPosition);
            startPos.z = LIGHT_Z_OFFSET;

            // Create light object
            GameObject lightObj = new GameObject($"FairyPathLight_{lightIndex}");
            lightObj.transform.SetParent(visualContainer.transform);
            lightObj.transform.position = startPos;

            // Add point light component (matching FairyRingSphere style)
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = LIGHT_RANGE;
            light.intensity = LIGHT_INTENSITY;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;

            // Add trail renderer (matching FairyRingSphere style)
            TrailRenderer trail = lightObj.AddComponent<TrailRenderer>();
            trail.time = 2f;  // Long trail for light painting effect
            trail.startWidth = 0.15f;
            trail.endWidth = 0.03f;
            trail.minVertexDistance = 0.01f;
            trail.emitting = false; // Start disabled, enable after light moves
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sortingOrder = 100;
            trail.allowOcclusionWhenDynamic = false;

            // Set up trail material for additive blending (like FairyRingSphere)
            SetupTrailMaterial(trail);

            // Create PathLight wrapper with random initial delay for staggered spawning
            float initialDelay = Random.Range(0f, MAX_RESPAWN_DELAY);

            // Initialize erratic movement with random parameters
            float wanderAngle = Random.Range(0f, Mathf.PI * 2f);
            float wanderSpeed = Random.Range(MIN_WANDER_SPEED, MAX_WANDER_SPEED);

            PathLight pathLight = new PathLight
            {
                gameObject = lightObj,
                light = light,
                trail = trail,
                pathIndex = pathIndex,
                normalizedPosition = 0f, // Always start at beginning
                colorTimeOffset = Random.Range(0f, 10f), // Random phase offset for color
                cycleDuration = Random.Range(1f, 3f), // Random cycle duration like FairyRingSphere
                trailStarted = false, // Trail starts disabled
                respawnDelay = Random.Range(MIN_RESPAWN_DELAY, MAX_RESPAWN_DELAY),
                currentDelay = initialDelay, // Stagger initial spawns
                isWaiting = initialDelay > 0f, // Start waiting if there's an initial delay

                // Erratic movement initialization
                wanderOffset = Vector2.zero,
                wanderVelocity = new Vector2(Mathf.Cos(wanderAngle), Mathf.Sin(wanderAngle)) * wanderSpeed,
                wanderChangeTimer = Random.Range(MIN_DIRECTION_CHANGE, MAX_DIRECTION_CHANGE),
                wanderAmplitude = Random.Range(MIN_WANDER_AMPLITUDE, MAX_WANDER_AMPLITUDE),
                wanderSpeed = wanderSpeed
            };

            // Hide light initially if waiting
            if (pathLight.isWaiting)
            {
                lightObj.SetActive(false);
            }

            return pathLight;
        }

        /// <summary>
        /// Sets up trail material for additive blending (matching FairyRingSphere).
        /// </summary>
        private void SetupTrailMaterial(TrailRenderer trail)
        {
            if (trail == null) return;

            // Try URP Particles shader first
            var trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
            }
            if (trailMaterial.shader == null || trailMaterial.shader.name == "Hidden/InternalErrorShader")
            {
                trailMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            // Configure for additive blending
            trailMaterial.SetFloat("_Surface", 1); // Transparent
            trailMaterial.SetFloat("_Blend", 4); // Additive blend mode
            trailMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            trailMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            trailMaterial.SetInt("_ZWrite", 0);
            trailMaterial.renderQueue = 3500;
            trailMaterial.SetColor("_BaseColor", Color.white);
            trailMaterial.SetColor("_Color", Color.white);

            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trailMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            trail.material = trailMaterial;
        }

        /// <summary>
        /// Updates all fairy ring style lights - movement and color cycling.
        /// </summary>
        private void UpdateFairyRingLights(float deltaTime)
        {
            if (allPathsToHeart.Count == 0 || pathLightObjects.Count == 0)
                return;

            foreach (var pathLight in pathLightObjects)
            {
                if (pathLight == null || pathLight.gameObject == null)
                    continue;

                // Handle waiting state (random spawn delays)
                if (pathLight.isWaiting)
                {
                    pathLight.currentDelay -= deltaTime;
                    if (pathLight.currentDelay <= 0f)
                    {
                        // Done waiting, activate the light
                        pathLight.isWaiting = false;
                        pathLight.gameObject.SetActive(true);
                        pathLight.normalizedPosition = 0f;
                        pathLight.trailStarted = false;
                    }
                    continue; // Skip movement while waiting
                }

                // Move light along its path toward the heart
                if (pathLight.pathIndex < allPathsToHeart.Count)
                {
                    var path = allPathsToHeart[pathLight.pathIndex];
                    float pathLength = CalculatePathLength(path);

                    if (pathLength > 0)
                    {
                        // Move toward heart (position increases toward 1.0)
                        pathLight.normalizedPosition += (moveSpeed / pathLength) * deltaTime;

                        // Enable trail after light has moved 2% along the path
                        if (!pathLight.trailStarted && pathLight.normalizedPosition > 0.02f)
                        {
                            pathLight.trailStarted = true;
                            if (pathLight.trail != null)
                            {
                                pathLight.trail.emitting = true;
                            }
                        }

                        // When reaching the end of the path (heart), respawn after delay
                        if (pathLight.normalizedPosition >= PATH_END_THRESHOLD)
                        {
                            // Hide the light and start waiting
                            pathLight.gameObject.SetActive(false);
                            pathLight.isWaiting = true;
                            pathLight.currentDelay = Random.Range(MIN_RESPAWN_DELAY, MAX_RESPAWN_DELAY);
                            pathLight.normalizedPosition = 0f;
                            pathLight.trailStarted = false;

                            // Reset wander for next cycle
                            pathLight.wanderOffset = Vector2.zero;
                            float newAngle = Random.Range(0f, Mathf.PI * 2f);
                            pathLight.wanderVelocity = new Vector2(Mathf.Cos(newAngle), Mathf.Sin(newAngle)) * pathLight.wanderSpeed;

                            // Clear the trail
                            if (pathLight.trail != null)
                            {
                                pathLight.trail.Clear();
                                pathLight.trail.emitting = false;
                            }
                            continue;
                        }

                        // Update erratic wandering
                        UpdateErraticWander(pathLight, deltaTime);

                        // Get base position along path
                        Vector3 basePos = GetPositionAlongPath(path, pathLight.normalizedPosition);

                        // Apply wander offset perpendicular to path direction
                        Vector3 newPos = basePos;
                        newPos.x += pathLight.wanderOffset.x;
                        newPos.y += pathLight.wanderOffset.y;
                        newPos.z = LIGHT_Z_OFFSET;
                        pathLight.gameObject.transform.position = newPos;
                    }
                }

                // Update rainbow color cycling (matching FairyRingSphere style)
                float t = animationTime + pathLight.colorTimeOffset;
                Color currentColor = EvaluateRainbowCycle(t, pathLight.cycleDuration / RainbowHues.Length);

                // Update light color
                if (pathLight.light != null)
                {
                    pathLight.light.color = currentColor;
                }

                // Update trail color gradient
                if (pathLight.trail != null)
                {
                    Color brightColor = currentColor * 2f; // HDR boost for glow

                    Gradient gradient = new Gradient();
                    gradient.SetKeys(
                        new GradientColorKey[]
                        {
                            new GradientColorKey(brightColor, 0f),
                            new GradientColorKey(currentColor, 0.4f),
                            new GradientColorKey(currentColor * 0.5f, 1f)
                        },
                        new GradientAlphaKey[]
                        {
                            new GradientAlphaKey(1f, 0f),
                            new GradientAlphaKey(0.8f, 0.2f),
                            new GradientAlphaKey(0.4f, 0.5f),
                            new GradientAlphaKey(0f, 1f)
                        }
                    );
                    pathLight.trail.colorGradient = gradient;
                    pathLight.trail.startColor = brightColor;
                    pathLight.trail.endColor = new Color(currentColor.r * 0.5f, currentColor.g * 0.5f, currentColor.b * 0.5f, 0f);
                }
            }
        }

        /// <summary>
        /// Updates the erratic wandering behavior for a path light.
        /// The light moves in random directions but stays within its amplitude bounds.
        /// </summary>
        private void UpdateErraticWander(PathLight pathLight, float deltaTime)
        {
            // Update direction change timer
            pathLight.wanderChangeTimer -= deltaTime;
            if (pathLight.wanderChangeTimer <= 0f)
            {
                // Change to a new random direction
                float newAngle = Random.Range(0f, Mathf.PI * 2f);
                float speedVariation = Random.Range(0.7f, 1.3f);
                pathLight.wanderVelocity = new Vector2(
                    Mathf.Cos(newAngle),
                    Mathf.Sin(newAngle)
                ) * pathLight.wanderSpeed * speedVariation;

                // Reset timer with some randomness
                pathLight.wanderChangeTimer = Random.Range(MIN_DIRECTION_CHANGE, MAX_DIRECTION_CHANGE);
            }

            // Apply velocity to offset
            pathLight.wanderOffset += pathLight.wanderVelocity * deltaTime;

            // Clamp to amplitude bounds with soft bounce
            float currentDistance = pathLight.wanderOffset.magnitude;
            if (currentDistance > pathLight.wanderAmplitude)
            {
                // Reflect velocity inward when hitting boundary
                Vector2 normal = pathLight.wanderOffset.normalized;
                pathLight.wanderVelocity = Vector2.Reflect(pathLight.wanderVelocity, -normal);

                // Also nudge the offset back inside
                pathLight.wanderOffset = normal * pathLight.wanderAmplitude * 0.95f;

                // Add some randomness to the reflection
                float perturbAngle = Random.Range(-0.5f, 0.5f);
                float cos = Mathf.Cos(perturbAngle);
                float sin = Mathf.Sin(perturbAngle);
                Vector2 rotated = new Vector2(
                    pathLight.wanderVelocity.x * cos - pathLight.wanderVelocity.y * sin,
                    pathLight.wanderVelocity.x * sin + pathLight.wanderVelocity.y * cos
                );
                pathLight.wanderVelocity = rotated;
            }
        }

        /// <summary>
        /// Returns a smoothly cycling color through the rainbow (same as FairyRingSphere).
        /// </summary>
        private static Color EvaluateRainbowCycle(float timeSeconds, float holdDuration)
        {
            int colorCount = RainbowHues.Length;
            float totalCycleDuration = colorCount * holdDuration;

            if (holdDuration <= 0.0001f) return Color.HSVToRGB(RainbowHues[0], 0.8f, 1f);

            float cycleTime = Mathf.Repeat(timeSeconds, totalCycleDuration);
            int currentColorIndex = Mathf.FloorToInt(cycleTime / holdDuration);
            currentColorIndex = Mathf.Clamp(currentColorIndex, 0, colorCount - 1);

            float timeInSegment = cycleTime - (currentColorIndex * holdDuration);
            float segmentProgress = timeInSegment / holdDuration;

            float transitionT = 0f;
            if (segmentProgress > 0.5f)
            {
                float transitionProgress = (segmentProgress - 0.5f) * 2f;
                transitionT = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * transitionProgress);
            }

            float fromH = RainbowHues[currentColorIndex];
            float toH = RainbowHues[(currentColorIndex + 1) % colorCount];

            float hDiff = toH - fromH;
            if (hDiff > 0.5f) hDiff -= 1f;
            else if (hDiff < -0.5f) hDiff += 1f;
            float h = fromH + hDiff * transitionT;
            if (h < 0f) h += 1f;
            if (h > 1f) h -= 1f;

            const float saturation = 0.8f;
            const float value = 1.0f;

            return Color.HSVToRGB(h, saturation, value);
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

            // Use the pre-computed HeartPower1 position index
            var indexedPositions = mazeData.GetHeartPower1Positions(allAffectedNodeIndices, allAffectedEdgeIndices);

            // Calculate distance from heart to focal point (for filtering edge positions)
            float targetDistFromHeart = Vector2.Distance(heartPos2D, targetPos2D);

            // Convert Vector2 positions to Vector3
            // For the triggering edge, only include positions BETWEEN the heart and the activation point
            // (i.e., closer to or equal distance from heart as the activation point)
            foreach (var pos2D in indexedPositions)
            {
                // When triggered on an edge, filter out positions that are FARTHER from the heart
                // than the activation point (they are beyond the effect boundary)
                if (affectedEdgeIndex >= 0)
                {
                    // Check if this position is on the triggering edge
                    var tile = FindNearestWalkableTile(mazeData, pos2D);
                    if (tile != null && tile.EdgeIndex == affectedEdgeIndex)
                    {
                        // Include positions that are closer to or at the same distance as the activation point
                        // These are the positions BETWEEN the heart and activation point
                        float posDistFromHeart = Vector2.Distance(heartPos2D, pos2D);
                        if (posDistFromHeart > targetDistFromHeart + 0.5f) // Small tolerance
                        {
                            // This position is beyond the activation point (away from heart), skip it
                            continue;
                        }
                        // Positions closer to heart than activation point ARE included (no continue)
                    }
                }

                positions.Add(new Vector3(pos2D.x, pos2D.y, targetPosition.z));
            }

            // Limit total positions to avoid performance issues (max ~60 light trails)
            if (positions.Count > 60)
            {
                var sampled = new List<Vector3>();
                float sampleStep = (float)positions.Count / 60f;
                for (int i = 0; i < 60; i++)
                {
                    int idx = Mathf.Min((int)(i * sampleStep), positions.Count - 1);
                    sampled.Add(positions[idx]);
                }
                positions = sampled;
            }

            // If no positions found, fall back to target position
            if (positions.Count == 0)
            {
                positions.Add(targetPosition);
            }

            return positions;
        }

        /// <summary>
        /// Gets all walkable positions on the affected node or edge (legacy method).
        /// </summary>
        private List<Vector3> GetAllAffectedPositions()
        {
            var positions = new List<Vector3>();

            if (manager.MazeGrid == null || manager.MazeGrid.WorldSpaceMazeData == null)
            {
                // Fallback to just target position
                positions.Add(targetPosition);
                return positions;
            }

            var mazeData = manager.MazeGrid.WorldSpaceMazeData;

            // If we have an affected node or edge, get all walkable tiles on it
            if (affectedNodeIndex >= 0 || affectedEdgeIndex >= 0)
            {
                // Search a larger area to find all tiles belonging to this node/edge
                float searchRadius = 20f; // Large search to cover entire node/edge
                Vector2 targetPos2D = new Vector2(targetPosition.x, targetPosition.y);
                var nearbyTiles = mazeData.GetTilesNear(targetPos2D, searchRadius);

                // Filter to tiles on the affected node or edge
                foreach (var tile in nearbyTiles)
                {
                    if (!tile.Walkable) continue;

                    bool onAffectedNode = affectedNodeIndex >= 0 && tile.NodeIndex == affectedNodeIndex;
                    bool onAffectedEdge = affectedEdgeIndex >= 0 && tile.EdgeIndex == affectedEdgeIndex;

                    if (onAffectedNode || onAffectedEdge)
                    {
                        positions.Add(new Vector3(tile.Position.x, tile.Position.y, targetPosition.z));
                    }
                }

                // Sample positions to avoid too many paths (max ~8 light trails)
                if (positions.Count > 8)
                {
                    var sampled = new List<Vector3>();
                    float step = (float)positions.Count / 8f;
                    for (int i = 0; i < 8; i++)
                    {
                        int idx = Mathf.Min((int)(i * step), positions.Count - 1);
                        sampled.Add(positions[idx]);
                    }
                    positions = sampled;
                }
            }

            // If no positions found, fall back to target position
            if (positions.Count == 0)
            {
                positions.Add(targetPosition);
            }

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
            if (activeVisitors == null) return;

            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Consumed)
                    continue;

                if (IsVisitorOnAffectedArea(visitor))
                {
                    LureVisitorToHeart(visitor);
                }
            }
        }

        /// <summary>
        /// Checks for new visitors entering the affected area and lures them.
        /// </summary>
        private void CheckForNewVisitorsOnAffectedArea()
        {
            var activeVisitors = VisitorRegistry.All;
            if (activeVisitors == null) return;

            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitor.State == VisitorControllerBase.VisitorState.Consumed)
                    continue;

                if (affectedVisitors.Contains(visitor))
                    continue;

                if (IsVisitorOnAffectedArea(visitor))
                {
                    LureVisitorToHeart(visitor);
                }
            }
        }

        /// <summary>
        /// Lures a visitor toward the heart.
        /// Generates a path from the visitor's current position to the heart,
        /// not from the activation point (to avoid backtracking).
        /// </summary>
        private void LureVisitorToHeart(VisitorControllerBase visitor)
        {
            if (visitor == null) return;

            visitor.SetLured(true);

            // Generate a path from the VISITOR's current position to the heart
            // This prevents backtracking when visitor is between activation point and heart
            if (manager.MazeGrid != null)
            {
                var visitorPath = GeneratePathToHeart(visitor.transform.position);
                if (visitorPath.Count >= 2)
                {
                    visitor.SetPathDirectly(visitorPath);
                }
                else if (pathPositions.Count > 0)
                {
                    // Fallback to activation path if visitor path generation failed
                    visitor.SetPathDirectly(new List<Vector3>(pathPositions));
                }
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

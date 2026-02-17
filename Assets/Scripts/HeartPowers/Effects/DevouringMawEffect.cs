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
    #region Devouring Maw

    /// <summary>
    /// Toggle power that creates a trigger zone to detect and devour visitors.
    /// When active, path tiles in the area shake with particles and fog effects.
    /// Visitors entering the zone are devoured sequentially with a 0.25s delay between each.
    /// </summary>
    public class DevouringMawEffect : ConsumptionBasedPowerEffect
    {
        // Settings - Loaded from GameSettings
        private readonly float triggerRadius;

        // Visual cue radius is larger than detection radius so the prefab fits inside
        private const float VISUAL_RADIUS_OFFSET = 0.5f;
        private float visualRadius => triggerRadius + VISUAL_RADIUS_OFFSET;

        // Base durations (before blessing multipliers)
        private const float BASE_DEVOUR_CYCLE_DELAY = 0.25f;
        private const float BASE_EMERGE_DURATION = 1.04f; // 25 frames at 24fps = ~1.04 seconds for full bite animation
        private const float BASE_PAUSE_DURATION = 1.0f; // Full second pause for visibility
        private const float BASE_SINK_DURATION = 0.5f;

        // Effective durations (after blessing speed multiplier applied)
        private readonly float devourCycleDelay;
        private readonly float emergeDuration;
        private readonly float pauseDuration;
        private readonly float sinkDuration;
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

        // Frightening event (registered when devour cycle is active)
        private FrighteningEventManager.FrighteningEvent currentFrighteningEvent;

        public DevouringMawEffect(HeartPowerManager manager, HeartPowerDefinition definition, Vector3 targetPosition)
            : base(manager, definition, targetPosition)
        {
            // Load settings from GameSettings
            triggerRadius = GameSettings.DevouringMawRadius;

            // Apply blessing speed multiplier (Devouring Hunger: 50% faster = 1.5x speed = 0.67x duration)
            float speedMultiplier = BlessingManager.Instance?.GetMawSpeedMultiplier() ?? 1.0f;
            float durationMultiplier = 1.0f / speedMultiplier; // Faster speed = shorter duration

            devourCycleDelay = BASE_DEVOUR_CYCLE_DELAY * durationMultiplier;
            emergeDuration = BASE_EMERGE_DURATION * durationMultiplier;
            pauseDuration = BASE_PAUSE_DURATION * durationMultiplier;
            sinkDuration = BASE_SINK_DURATION * durationMultiplier;
        }

        /// <summary>
        /// Extends base expiration to also prevent expiring while a devour cycle is in progress.
        /// </summary>
        public override bool IsExpired
        {
            get
            {
                if (cycleInProgress) return false;
                return hasExpired;
            }
        }

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

            string visitorLabel = visitor.EntityLabel ?? visitor.gameObject.name;
            GameEventLogger.LogPowerVisitorEvent("DevouringMaw", visitorLabel, "Captured");
        }

        public override void OnStart()
        {
            targetWorldPos = targetPosition;

            // Set required consumptions to the power tier (like MurmuringPaths)
            requiredConsumptions = manager.GetPowerTier(HeartPowerType.DevouringMaw);
            consumedCount = 0;
            hasExpired = false;

            // Duration for tile visualizer display (not used for expiration)
            powerDuration = definition.duration > 0 ? definition.duration : 10f;
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

            // Register frightening event so visitors flee when they see the active maw zone
            currentFrighteningEvent = HeartPowerUtils.RegisterFrighteningEvent(
                FrighteningEventManager.EventType.DevouringMaw, targetWorldPos, this);

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
                if (visitor != null && (elapsedTime - lastCycleEndTime) >= devourCycleDelay)
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
            HeartPowerUtils.UnregisterFrighteningEvent(ref currentFrighteningEvent);

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
            Texture2D texture = Resources.Load<Texture2D>("EarthenGroundTexture");

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

            meshFilter.mesh = HeartPowerUtils.CreateCircleMesh(visualRadius, 32, "CircleFogMesh");

            // Create fog material using DevourDust shader for billowing dust effect
            Shader fogShader = HeartPowerUtils.LoadShader("Custom/DevourDust", "Universal Render Pipeline/Unlit")
                ?? HeartPowerUtils.LoadShader("Unlit/Color");

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

            // Circular emission shape - use visualRadius to match fog circle
            var shape = dustParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = visualRadius;
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

            Shader particleShader = HeartPowerUtils.GetParticleShader();
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

            // Use circle shape for circular emission area - use visualRadius to match fog circle
            var shape = areaParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = visualRadius;
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

            Shader particleShader = HeartPowerUtils.GetParticleShader();
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

            // Get devour location from triggering visitor
            Vector3 devourLocation = triggeringVisitor.transform.position;
            devourLocation.z = 0f;

            // Capture the triggering visitor immediately
            // Additional visitors will be captured by the DevourTriggerHandler when they
            // collide with the maw model's collider during the emerging/paused phases
            if (triggeringVisitor != null &&
                triggeringVisitor.State != VisitorControllerBase.VisitorState.Consumed &&
                triggeringVisitor.State != VisitorControllerBase.VisitorState.Escaping)
            {
                triggeringVisitor.Stop();
                triggeringVisitor.SetGrabbedByHeart();
                visitorsBeingDevoured.Add(triggeringVisitor);
                visitorStartPositions[triggeringVisitor] = triggeringVisitor.transform.position;
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
                    // Translate prefab from z=0 to z=-0.5 while playing full bite animation (frames 1â†’25)
                    float emergeT = Mathf.Clamp01(phaseElapsed / emergeDuration);
                    if (devourVisual != null)
                    {
                        Vector3 pos = devourBasePosition;
                        pos.z = Mathf.Lerp(0f, -0.5f, emergeT);
                        devourVisual.transform.position = pos;
                    }

                    // Play bite animation (frames 1â†’25)
                    int emergeFrame = 1 + Mathf.FloorToInt(emergeT * (DEVOUR_ANIMATION_FRAMES - 1));
                    SetDevourAnimatorFrame(emergeFrame);

                    if (emergeT >= 1f)
                    {
                        currentPhase = DevourPhase.Paused;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case DevourPhase.Paused:
                    // Hold at z=-0.5 for pauseDuration, then sink along +z
                    // Hold last frame of animation (mouth closed after bite)
                    if (devourVisual != null)
                    {
                        Vector3 pausePos = devourBasePosition;
                        pausePos.z = -0.5f;
                        devourVisual.transform.position = pausePos;
                    }

                    // Hold at last frame (closed after bite)
                    SetDevourAnimatorFrame(DEVOUR_ANIMATION_FRAMES);

                    if (phaseElapsed >= pauseDuration)
                    {
                        currentPhase = DevourPhase.Sinking;
                        phaseStartTime = elapsedTime;
                    }
                    break;

                case DevourPhase.Sinking:
                    // Translate prefab and visitors along +z (from -0.5 to 1.0)
                    // Hold last frame of animation (mouth stays closed)
                    float sinkT = Mathf.Clamp01(phaseElapsed / sinkDuration);

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
                    HeartPowerUtils.UnregisterFrighteningEvent(ref currentFrighteningEvent);

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
                    controller = Resources.Load<RuntimeAnimatorController>("Animations/Devour/devour");
                    if (controller != null)
                    {
                        devourAnimator.runtimeAnimatorController = controller;
                    }
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

            // Award 0.5 * essence value, applying heart form reward multiplier
            int essence = HeartPowerUtils.CalculateConsumptionEssence(visitor, additionalMultiplier: 0.5f);

            if (manager.GameController != null)
            {
                manager.GameController.AddEssence(essence, EssenceSource.VisitorConsumedByMaw, $"Maw reward: {essence}");
            }

            // Track visitor fate with essence value
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.RecordVisitorFate(visitor.Archetype, VisitorFate.Devoured, essence);
            }

            string visitorLabel = visitor.EntityLabel ?? visitor.gameObject.name;
            GameEventLogger.LogPowerVisitorEvent("DevouringMaw", visitorLabel, "Devoured");
            GameEventLogger.LogVisitorFate(visitorLabel, "Devoured", essence);

            SoundManager.Instance?.PlayVisitorConsumed();

            // Notify nearby visitors that consumption occurred - they become frightened
            HeartPowerEvents.NotifyVisitorConsumedByMaw(consumptionPosition);

            Object.Destroy(visitor.gameObject);

            // Increment consumption count and check for expiration
            OnVisitorConsumed();
            if (hasExpired)
            {
            }
        }

        private void ApplyEchoingTerror(Vector3 centerWorldPos)
        {
            float fearRadius = definition.param1 > 0 ? definition.param1 : 3f;
            float fearDuration = definition.param2 > 0 ? definition.param2 : 3f;

            var visitors = VisitorRegistry.All;

            foreach (var visitor in visitors)
            {
                if (!HeartPowerUtils.IsVisitorTargetable(visitor)) continue;

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
                if (!HeartPowerUtils.IsVisitorTargetable(visitor)) continue;

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
}

using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Creates a lit particle effect that covers the maze area and extends on the z-plane from 0 to -5.
    /// Particles are dark by default and only become visible when illuminated by 3D point lights
    /// from models (wisp, heart, visitors), helping players locate glowing entities in the maze.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class MazeParticleSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour to get maze dimensions")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Particle Settings")]
        [SerializeField]
        [Tooltip("Number of particles to emit")]
        private int maxParticles = 1000;

        [SerializeField]
        [Tooltip("Particle size")]
        private float particleSize = 0.08f;

        [SerializeField]
        [Tooltip("Particle base color (should be near-black so particles only show when lit)")]
        private Color particleColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        [SerializeField]
        [Tooltip("Minimum Z position (closest to camera)")]
        private float minZ = 0f;

        [SerializeField]
        [Tooltip("Maximum Z position (farthest from camera)")]
        private float maxZ = -5f;

        [SerializeField]
        [Tooltip("Particle drift speed")]
        private float driftSpeed = 0.2f;

        [SerializeField]
        [Tooltip("Enable particle rotation")]
        private bool enableRotation = true;

        [SerializeField]
        [Tooltip("Particle rotation speed")]
        private float rotationSpeed = 30f;

        [Header("Material Settings")]
        [SerializeField]
        [Tooltip("Optional custom lit material (uses URP Particles/Lit shader if not set)")]
        private Material particleMaterial;

        #endregion

        #region Private Fields

        private ParticleSystem particleSystemComponent;
        private ParticleSystemRenderer particleRenderer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Find maze grid if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            SetupParticleSystem();
        }

        private void Start()
        {
            if (mazeGridBehaviour == null)
            {
                Debug.LogWarning("[MazeParticleSystem] MazeGridBehaviour not found. Particle system may not cover the correct area.");
                return;
            }

            PositionParticleSystem();
        }

        #endregion

        #region Particle System Setup

        private void SetupParticleSystem()
        {
            particleSystemComponent = GetComponent<ParticleSystem>();
            if (particleSystemComponent == null)
            {
                particleSystemComponent = gameObject.AddComponent<ParticleSystem>();
            }

            particleRenderer = GetComponent<ParticleSystemRenderer>();

            // Main module
            var main = particleSystemComponent.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
            main.startSize = particleSize;
            main.startColor = particleColor;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            // Emission module
            var emission = particleSystemComponent.emission;
            emission.enabled = true;
            emission.rateOverTime = maxParticles / 10f; // Emit enough to maintain particle count

            // Shape module - emit in a box shape covering the maze
            var shape = particleSystemComponent.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // Will set size in PositionParticleSystem after we have maze dimensions

            // Velocity over lifetime - slow drift
            var velocity = particleSystemComponent.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-driftSpeed * 0.5f, driftSpeed * 0.5f);

            // Rotation over lifetime
            if (enableRotation)
            {
                var rotation = particleSystemComponent.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-rotationSpeed, rotationSpeed);
            }

            // Renderer settings
            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

                // Use provided material or create default unlit material
                if (particleMaterial != null)
                {
                    particleRenderer.material = particleMaterial;
                }
                else
                {
                    // Create lit material that responds to lights
                    // Particles will only be visible when illuminated by nearby point lights
                    Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
                    if (defaultMat.shader == null || defaultMat.shader.name == "Hidden/InternalErrorShader")
                    {
                        // Fallback to simple lit if particle lit shader not found
                        defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    }

                    defaultMat.SetColor("_BaseColor", particleColor);

                    // Configure for proper lighting response
                    defaultMat.SetFloat("_Smoothness", 0.2f);
                    defaultMat.SetFloat("_Metallic", 0f);

                    // Make particles additive/translucent so they blend with lights
                    if (defaultMat.HasProperty("_Surface"))
                    {
                        defaultMat.SetFloat("_Surface", 1); // Transparent
                    }
                    if (defaultMat.HasProperty("_Blend"))
                    {
                        defaultMat.SetFloat("_Blend", 0); // Alpha blend
                    }

                    defaultMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    defaultMat.EnableKeyword("_ALPHABLEND_ON");
                    defaultMat.renderQueue = 3000; // Transparent queue

                    particleRenderer.material = defaultMat;
                }

                // Enable receiving lights so particles are illuminated by nearby point lights
                particleRenderer.receiveShadows = false;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Enable light probes for better lighting
                particleRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            }
        }

        private void PositionParticleSystem()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            // Get maze dimensions
            int width = mazeGridBehaviour.Grid.Width;
            int height = mazeGridBehaviour.Grid.Height;
            float tileSize = mazeGridBehaviour.TileSize;

            // Calculate world-space bounds of the maze
            Vector3 minCorner = mazeGridBehaviour.GridToWorld(0, 0);
            Vector3 maxCorner = mazeGridBehaviour.GridToWorld(width - 1, height - 1);

            // Calculate center and size
            Vector3 center = (minCorner + maxCorner) / 2f;
            float worldWidth = Mathf.Abs(maxCorner.x - minCorner.x) + tileSize;
            float worldHeight = Mathf.Abs(maxCorner.y - minCorner.y) + tileSize;
            float zDepth = Mathf.Abs(maxZ - minZ);

            // Center the particle system in Z-space
            float centerZ = (minZ + maxZ) / 2f;

            // Position the particle system at the center of the maze
            transform.position = new Vector3(center.x, center.y, centerZ);

            // Configure shape module with maze dimensions
            var shape = particleSystemComponent.shape;
            shape.scale = new Vector3(worldWidth, worldHeight, zDepth);

            Debug.Log($"[MazeParticleSystem] Configured particle system for maze of size {width}x{height} (world: {worldWidth}x{worldHeight}), Z-depth: {minZ} to {maxZ}");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the particle emission rate.
        /// </summary>
        public void SetEmissionRate(float rate)
        {
            var emission = particleSystemComponent.emission;
            emission.rateOverTime = rate;
        }

        /// <summary>
        /// Sets the maximum number of particles.
        /// </summary>
        public void SetMaxParticles(int count)
        {
            maxParticles = count;
            var main = particleSystemComponent.main;
            main.maxParticles = count;
        }

        /// <summary>
        /// Enables or disables the particle system.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                particleSystemComponent.Play();
            }
            else
            {
                particleSystemComponent.Stop();
            }
        }

        #endregion
    }
}

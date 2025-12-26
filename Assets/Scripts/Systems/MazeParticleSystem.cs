using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Creates an unlit particle effect that covers the maze area and extends on the z-plane from 0 to -5.
    /// Particles reflect model glows from 3D lights (wisp, heart, visitors).
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
        private int maxParticles = 500;

        [SerializeField]
        [Tooltip("Particle size")]
        private float particleSize = 0.05f;

        [SerializeField]
        [Tooltip("Particle color (tinted by lights)")]
        private Color particleColor = new Color(0.9f, 0.9f, 1f, 0.3f);

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
        [Tooltip("Use unlit material that receives light influence")]
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
                    // Create simple unlit material with alpha blending
                    Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    defaultMat.SetColor("_BaseColor", particleColor);
                    defaultMat.SetFloat("_Surface", 1); // Transparent
                    defaultMat.SetFloat("_Blend", 0); // Alpha blend
                    defaultMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    defaultMat.EnableKeyword("_ALPHABLEND_ON");
                    defaultMat.renderQueue = 3000; // Transparent queue

                    particleRenderer.material = defaultMat;
                }

                // Enable receiving lights so particles reflect nearby glow effects
                particleRenderer.receiveShadows = false;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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

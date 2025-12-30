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
        [Tooltip("Particle base color (white, brightness determined by lights)")]
        private Color particleColor = new Color(1f, 1f, 1f, 0.8f);

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
                    // Create lit particle material that responds to 3D point lights
                    Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
                    if (defaultMat.shader == null || defaultMat.shader.name == "Hidden/InternalErrorShader")
                    {
                        // Fallback to built-in lit particles
                        defaultMat = new Material(Shader.Find("Particles/Standard Surface"));
                    }

                    // Set light gray base color with low alpha - bright enough to reflect light but transparent enough to be invisible when unlit
                    defaultMat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 0.15f));

                    // Configure material to be highly responsive to lights
                    if (defaultMat.HasProperty("_Smoothness"))
                    {
                        defaultMat.SetFloat("_Smoothness", 0.85f); // Smooth for light reflection
                    }
                    if (defaultMat.HasProperty("_Metallic"))
                    {
                        defaultMat.SetFloat("_Metallic", 0.0f); // Non-metallic for better diffuse response
                    }

                    // Use transparent surface with additive blending
                    if (defaultMat.HasProperty("_Surface"))
                    {
                        defaultMat.SetFloat("_Surface", 1); // Transparent
                    }
                    if (defaultMat.HasProperty("_Blend"))
                    {
                        defaultMat.SetFloat("_Blend", 1); // Additive
                    }
                    if (defaultMat.HasProperty("_SrcBlend"))
                    {
                        defaultMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    }
                    if (defaultMat.HasProperty("_DstBlend"))
                    {
                        defaultMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // Additive
                    }
                    if (defaultMat.HasProperty("_ZWrite"))
                    {
                        defaultMat.SetFloat("_ZWrite", 0); // No depth write for transparent
                    }

                    defaultMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    defaultMat.EnableKeyword("_BLENDMODE_ADD");
                    defaultMat.renderQueue = 3000; // Transparent queue

                    particleRenderer.material = defaultMat;
                }

                // Configure particle renderer for light interaction
                particleRenderer.receiveShadows = false;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Use vertex streams to allow lights to affect particles
                particleRenderer.enableGPUInstancing = true;
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

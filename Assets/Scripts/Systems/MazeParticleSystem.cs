using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Creates a lit particle effect that covers the maze area.
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
        [Tooltip("Particle base color")]
        private Color particleColor = new Color(1f, 1f, 1f, 0.8f);

        [SerializeField]
        [Tooltip("Minimum Z position")]
        private float minZ = 0f;

        [SerializeField]
        [Tooltip("Maximum Z position")]
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
        [Tooltip("Optional custom lit material")]
        private Material particleMaterial;

        #endregion

        #region Private Fields

        private ParticleSystem particleSystemComponent;
        private ParticleSystemRenderer particleRenderer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
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

            var main = particleSystemComponent.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, driftSpeed);
            main.startSize = particleSize;
            main.startColor = particleColor;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = particleSystemComponent.emission;
            emission.enabled = true;
            emission.rateOverTime = maxParticles / 10f;

            var shape = particleSystemComponent.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;

            var velocity = particleSystemComponent.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            velocity.y = new ParticleSystem.MinMaxCurve(-driftSpeed, driftSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(-driftSpeed * 0.5f, driftSpeed * 0.5f);

            if (enableRotation)
            {
                var rotation = particleSystemComponent.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-rotationSpeed, rotationSpeed);
            }

            if (particleRenderer != null)
            {
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;

                if (particleMaterial != null)
                {
                    particleRenderer.material = particleMaterial;
                }
                else
                {
                    Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
                    if (defaultMat.shader == null || defaultMat.shader.name == "Hidden/InternalErrorShader")
                    {
                        defaultMat = new Material(Shader.Find("Particles/Standard Surface"));
                    }

                    defaultMat.SetColor("_BaseColor", new Color(0.8f, 0.8f, 0.8f, 0.15f));

                    if (defaultMat.HasProperty("_Smoothness"))
                    {
                        defaultMat.SetFloat("_Smoothness", 0.85f);
                    }
                    if (defaultMat.HasProperty("_Metallic"))
                    {
                        defaultMat.SetFloat("_Metallic", 0.0f);
                    }
                    if (defaultMat.HasProperty("_Surface"))
                    {
                        defaultMat.SetFloat("_Surface", 1);
                    }
                    if (defaultMat.HasProperty("_Blend"))
                    {
                        defaultMat.SetFloat("_Blend", 1);
                    }
                    if (defaultMat.HasProperty("_SrcBlend"))
                    {
                        defaultMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    }
                    if (defaultMat.HasProperty("_DstBlend"))
                    {
                        defaultMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    }
                    if (defaultMat.HasProperty("_ZWrite"))
                    {
                        defaultMat.SetFloat("_ZWrite", 0);
                    }

                    defaultMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    defaultMat.EnableKeyword("_BLENDMODE_ADD");
                    defaultMat.renderQueue = 3000;

                    particleRenderer.material = defaultMat;
                }

                particleRenderer.receiveShadows = false;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                particleRenderer.enableGPUInstancing = true;
            }
        }

        private void PositionParticleSystem()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.ForestMapState == null)
            {
                return;
            }

            // Use world-space bounds from WorldSpaceMazeData
            if (mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            var bounds = mazeGridBehaviour.WorldSpaceMazeData.Bounds;
            float tileSize = mazeGridBehaviour.WorldSpaceTileSize;

            Vector3 center = bounds.center;
            float worldWidth = bounds.size.x + tileSize;
            float worldHeight = bounds.size.y + tileSize;
            float zDepth = Mathf.Abs(maxZ - minZ);

            float centerZ = (minZ + maxZ) / 2f;

            transform.position = new Vector3(center.x, center.y, centerZ);

            var shape = particleSystemComponent.shape;
            shape.scale = new Vector3(worldWidth, worldHeight, zDepth);
        }

        #endregion

        #region Public Methods

        public void SetEmissionRate(float rate)
        {
            var emission = particleSystemComponent.emission;
            emission.rateOverTime = rate;
        }

        public void SetMaxParticles(int count)
        {
            maxParticles = count;
            var main = particleSystemComponent.main;
            main.maxParticles = count;
        }

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

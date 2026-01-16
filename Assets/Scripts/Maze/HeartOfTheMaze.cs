using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Visitors;
namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the Heart of the Maze - the goal location where visitors are consumed for essence.
    /// Uses 3D meshes, materials, and URP 3D lighting.
    /// </summary>
    public class HeartOfTheMaze : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Position Settings")]
        [SerializeField]
        [Tooltip("Automatically position heart from maze data")]
        private bool autoPosition = true;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Amount of essence gained per visitor consumed")]
        private int essencePerVisitor = 10;

        [Header("Model Settings")]
        [SerializeField]
        [Tooltip("Model prefab to use for the heart visuals")]
        private GameObject heartModelPrefab;

        [SerializeField]
        [Tooltip("Size/scale of the heart model")]
        private float modelSize = 1.2f;

        [Header("Material Animation Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing emission on materials")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 2f;

        [SerializeField]
        [Tooltip("Pulse intensity multiplier")]
        private float pulseIntensity = 2f;

        [SerializeField]
        [Tooltip("Base emission color for pulsing")]
        private Color emissionColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("3D Lighting Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing 3D point light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow")]
        private Color glowColor = new Color(1f, 0.7f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 10f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz")]
        private float glowFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 2.0f;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Enable rotation and Z-axis animation")]
        private bool enableModelAnimation = true;

        [SerializeField]
        [Tooltip("Animation frequency in Hz")]
        private float animationFrequency = 1.5f;

        [SerializeField]
        [Tooltip("Minimum Z position for oscillation")]
        private float minZPosition = -1.3f;

        [SerializeField]
        [Tooltip("Maximum Z position for oscillation")]
        private float maxZPosition = -0.3f;

        #endregion

        #region Private Fields

        private Light glowLight;
        private GameObject modelInstance;
        private MeshRenderer[] meshRenderers;
        private Material[] materials;

        #endregion

        #region Properties

        /// <summary>Gets the essence value per visitor</summary>
        public int EssencePerVisitor => essencePerVisitor;

        #endregion

        #region Public Methods

        /// <summary>
        /// Positions the heart using the maze's world-space heart position.
        /// Can be called to reposition after maze regeneration.
        /// </summary>
        public void PositionFromMazeGrid()
        {
            // Find MazeGridBehaviour in scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Get heart position directly in world space
            Vector3 worldPos = mazeGridBehaviour.HeartWorldPosition;
            transform.position = worldPos;
        }

        /// <summary>
        /// Called when a visitor reaches the heart and is consumed.
        /// </summary>
        /// <param name="visitor">The visitor controller to consume</param>
        public void OnVisitorConsumed(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Track stats
            if (Systems.GameStatsTracker.Instance != null)
            {
                Systems.GameStatsTracker.Instance.RecordVisitorConsumed();
            }

            // Add essence to game controller - use archetype-specific reward if available
            if (GameController.Instance != null)
            {
                int essence = visitor.GetEssenceReward();
                GameController.Instance.AddEssence(essence);
            }

            // Notify HeartPowerManager of consumption (for toggle powers like MurmuringPaths)
            if (HeartPowers.HeartPowerManager.Instance != null)
            {
                HeartPowers.HeartPowerManager.Instance.NotifyVisitorConsumed();
            }

            SoundManager.Instance?.PlayVisitorConsumed();

            // Destroy the visitor
            Destroy(visitor.gameObject);
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Setup model and collect mesh renderers/materials
            SetupModel();

            // Ensure 3D physics setup for reliable trigger callbacks
            var rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;

            // Setup 3D point light
            SetupGlowLight();

            // Auto-position from maze grid if enabled
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }
        }

        private void Start()
        {
            // Always ensure we have a 3D trigger collider
            EnsureTriggerCollider();
        }

        private void Update()
        {
            // Update material emission pulsing
            if (enablePulse && materials != null && materials.Length > 0)
            {
                UpdateMaterialPulse();
            }

            // Update 3D point light pulsing
            if (enableGlow && glowLight != null)
            {
                UpdateGlowPulse();
            }

            // Update model animation (rotation and Z-axis movement)
            if (enableModelAnimation && modelInstance != null)
            {
                UpdateModelAnimation();
            }
        }

        private void EnsureTriggerCollider()
        {
            // Add SphereCollider for 3D trigger detection if not present
            var collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.5f;
                collider.isTrigger = true;
            }
            else
            {
                // Ensure it's set as trigger
                collider.isTrigger = true;
            }
        }


        private void SetupModel()
        {
            if (modelInstance != null)
            {
                return;
            }

            if (heartModelPrefab == null)
            {
                CreateFallbackHeartVisual();
                return;
            }

            // Instantiate the model prefab
            modelInstance = Instantiate(heartModelPrefab, transform);
            if (modelInstance == null)
            {
                CreateFallbackHeartVisual();
                return;
            }

            // Set position with proper Z offset
            modelInstance.transform.localPosition = new Vector3(0, 0, -0.3f);

            // Preserve prefab's scale and multiply by modelSize
            // (prefab has scale 100, so we keep that and just apply modelSize multiplier)
            Vector3 prefabScale = modelInstance.transform.localScale;
            modelInstance.transform.localScale = prefabScale * modelSize;

            // Collect all mesh renderers and replace materials with 3D PBR materials
            meshRenderers = modelInstance.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                System.Collections.Generic.List<Material> matList = new System.Collections.Generic.List<Material>();
                foreach (var renderer in meshRenderers)
                {
                    // Replace sprite materials with URP/Lit PBR emissive materials
                    Material[] pbrMats = new Material[renderer.materials.Length];
                    for (int i = 0; i < renderer.materials.Length; i++)
                    {
                        Material originalMat = renderer.materials[i];

                        // Try to preserve texture from original material
                        Texture baseTexture = null;
                        if (originalMat != null)
                        {
                            // Try common texture property names
                            baseTexture = originalMat.GetTexture("_MainTex");
                            if (baseTexture == null)
                            {
                                baseTexture = originalMat.GetTexture("_BaseMap");
                            }
                        }

                        // Use heart color for base tint
                        Color baseColor = new Color(0.9f, 0.35f, 0.35f); // Rich red/pink

                        // Use bright warm emission color (orange-yellow) for visible glow
                        Color brightEmission = new Color(1.0f, 0.6f, 0.2f); // Bright orange
                        float emissionIntensity = 3.0f; // Higher intensity for visibility

                        // Create new PBR emissive material with texture support
                        pbrMats[i] = Systems.PBRMaterialFactory.CreateEmissiveMaterialWithTexture(
                            baseColor,
                            brightEmission,
                            emissionIntensity,
                            baseTexture,
                            $"Heart_Material_{i}"
                        );
                        matList.Add(pbrMats[i]);
                    }
                    renderer.materials = pbrMats;
                }
                materials = matList.ToArray();
                meshRenderers = modelInstance.GetComponentsInChildren<MeshRenderer>(); // Refresh after material change
            }
            else
            {
            }
        }

        /// <summary>
        /// Creates a fallback visual for the heart when no model prefab is assigned.
        /// </summary>
        private void CreateFallbackHeartVisual()
        {
            // Create a simple sphere as fallback
            modelInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            modelInstance.transform.SetParent(transform);
            modelInstance.transform.localPosition = new Vector3(0, 0, -0.3f);
            modelInstance.transform.localScale = Vector3.one * modelSize;
            modelInstance.name = "Heart_Fallback";

            // Create emissive material
            MeshRenderer renderer = modelInstance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material heartMat = Systems.PBRMaterialFactory.CreateEmissiveMaterial(
                    new Color(0.9f, 0.35f, 0.35f), // Base color
                    emissionColor,
                    2.0f // Emission intensity
                );
                renderer.material = heartMat;

                // Store materials for pulsing
                materials = new Material[] { heartMat };
                meshRenderers = new MeshRenderer[] { renderer };
            }
        }

        private void SetupGlowLight()
        {
            if (!enableGlow)
                return;

            // Check if we already have a Light component
            glowLight = GetComponent<Light>();
            if (glowLight == null)
            {
                glowLight = gameObject.AddComponent<Light>();
            }

            // Configure the 3D point light
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowRange;
            glowLight.intensity = glowMaxIntensity;

            // Optional: Set light to use realtime mode for URP
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;

            // Optional: Enable shadows if desired (can be expensive)
            glowLight.shadows = LightShadows.None;
        }

        private void UpdateMaterialPulse()
        {
            // Calculate pulsing using sine wave
            float angle = Time.time * pulseSpeed * 2f * Mathf.PI;
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f; // [0, 1]

            // Calculate emission intensity
            float emissionStrength = normalizedPulse * pulseIntensity;

            // Apply to all materials
            foreach (var mat in materials)
            {
                if (mat == null) continue;

                // Set emission color with pulsing intensity
                Color finalEmission = emissionColor * emissionStrength;
                mat.SetColor("_EmissionColor", finalEmission);
                mat.EnableKeyword("_EMISSION");
            }
        }

        private void UpdateGlowPulse()
        {
            // Calculate pulsing intensity using sine wave
            float angle = Time.time * glowFrequency * 2f * Mathf.PI;

            // Map sin wave from [-1, 1] to [0, 1]
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;

            // Map to intensity range [min, max]
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);

            glowLight.intensity = intensity;
        }

        private void UpdateModelAnimation()
        {
            // Calculate animation phase using sine wave
            float angle = Time.time * animationFrequency * 2f * Mathf.PI;

            // Rotate around Z axis (full 360-degree rotation)
            float zRotation = Time.time * animationFrequency * 360f;
            modelInstance.transform.localRotation = Quaternion.Euler(
                modelInstance.transform.localRotation.eulerAngles.x,
                modelInstance.transform.localRotation.eulerAngles.y,
                zRotation
            );

            // Oscillate Z position between minZPosition and maxZPosition
            // Map sin wave from [-1, 1] to [minZ, maxZ]
            float normalizedSin = (Mathf.Sin(angle) + 1f) / 2f;
            float zPosition = Mathf.Lerp(maxZPosition, minZPosition, normalizedSin);

            modelInstance.transform.localPosition = new Vector3(
                modelInstance.transform.localPosition.x,
                modelInstance.transform.localPosition.y,
                zPosition
            );
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if a visitor entered the heart
            var visitor = other.GetComponent<VisitorControllerBase>();
            if (visitor != null)
            {
                OnVisitorConsumed(visitor);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            // Catch any visitors that miss the initial enter event
            var visitor = other.GetComponent<VisitorControllerBase>();
            if (visitor != null)
            {
                OnVisitorConsumed(visitor);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw heart marker in scene view
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw a pulsing effect
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f + pulse);
        }

        #endregion
    }
}

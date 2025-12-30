using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Creates a pulsing 3D point light glow effect at the focal point tile position.
    /// </summary>
    public class FocalPointGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the focal point transform")]
        private Transform focalPointTransform;

        [Header("3D Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing 3D point light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow")]
        private Color glowColor = new Color(0.5f, 1.0f, 0.0f, 1f); // Lime green

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 8f;

        [SerializeField]
        [Tooltip("Pulse speed in Hz")]
        private float pulseSpeed = 2.0f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.5f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 2.0f;

        [SerializeField]
        [Tooltip("Z offset for light position to illuminate tile surface")]
        private float lightZOffset = -0.5f;

        #endregion

        #region Private Fields

        private Light glowLight;
        private GameObject lightObject;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find references if not set
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (focalPointTransform == null)
            {
                // Try to find it from CameraController3D
                var cameraController = FindFirstObjectByType<CameraController3D>();
                if (cameraController != null)
                {
                    // We need to access the focal point transform - it's private, so we'll use this GameObject's transform
                    focalPointTransform = transform;
                }
            }

            CreateGlowLight();
        }

        private void Update()
        {
            if (enableGlow && glowLight != null && focalPointTransform != null && mazeGridBehaviour != null)
            {
                UpdateGlowPosition();
                UpdateGlowPulse();
            }
        }

        #endregion

        #region Glow Creation

        private void CreateGlowLight()
        {
            if (!enableGlow)
                return;

            // Create a child GameObject for the light
            lightObject = new GameObject("FocalPointLight");
            lightObject.transform.SetParent(transform, false);

            // Add Light component
            glowLight = lightObject.AddComponent<Light>();

            // Configure the 3D point light
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = glowRange;
            glowLight.intensity = glowMaxIntensity;

            // Set light to use realtime mode for URP
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;

            // Disable shadows for performance
            glowLight.shadows = LightShadows.None;

        }

        #endregion

        #region Glow Updates

        private void UpdateGlowPosition()
        {
            // Convert focal point world position to grid coordinates
            Vector3 focalPos = focalPointTransform.position;

            if (mazeGridBehaviour.WorldToGrid(focalPos, out int gridX, out int gridY))
            {
                // Get the center of the tile in world space
                Vector3 tileWorldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);

                // Position the light at the tile center with Z offset to illuminate the tile surface
                if (lightObject != null)
                {
                    lightObject.transform.position = new Vector3(tileWorldPos.x, tileWorldPos.y, lightZOffset);
                }
            }
        }

        private void UpdateGlowPulse()
        {
            // Calculate pulsing intensity using sine wave
            float angle = Time.time * pulseSpeed * 2f * Mathf.PI;

            // Map sin wave from [-1, 1] to [0, 1]
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;

            // Map to intensity range [min, max]
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);

            if (glowLight != null)
            {
                glowLight.intensity = intensity;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the focal point transform to track.
        /// </summary>
        public void SetFocalPointTransform(Transform focal)
        {
            focalPointTransform = focal;
        }

        /// <summary>
        /// Sets the maze grid behaviour reference.
        /// </summary>
        public void SetMazeGridBehaviour(MazeGridBehaviour grid)
        {
            mazeGridBehaviour = grid;
        }

        #endregion
    }
}

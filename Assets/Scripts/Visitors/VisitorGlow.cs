using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Adds a slowly pulsing glow effect to visitor models using 3D point lights.
    /// </summary>
    public class VisitorGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing glow light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the 3D point light glow")]
        private Color glowColor = new Color(0.9f, 0.85f, 1f, 1f); // Soft purple-white

        [SerializeField]
        [Tooltip("Range of the 3D point light")]
        private float glowRange = 5f;

        [SerializeField]
        [Tooltip("Glow pulse frequency in Hz (lower = slower pulse)")]
        private float glowFrequency = 0.5f;

        [SerializeField]
        [Tooltip("Minimum glow intensity")]
        private float glowMinIntensity = 0.3f;

        [SerializeField]
        [Tooltip("Maximum glow intensity")]
        private float glowMaxIntensity = 0.6f;

        #endregion

        #region Private Fields

        private Light glowLight;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupGlowLight();
        }

        private void Update()
        {
            if (enableGlow && glowLight != null)
            {
                UpdateGlowPulse();
            }
        }

        #endregion

        #region Glow Setup and Update

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

            // Set light to use realtime mode for URP
            glowLight.lightmapBakeType = LightmapBakeType.Realtime;

            // Disable shadows for performance
            glowLight.shadows = LightShadows.None;
        }

        private void UpdateGlowPulse()
        {
            // Calculate pulsing intensity using sine wave
            // frequency in Hz = cycles per second
            // Time.time * frequency * 2π gives us the angle for sin wave
            float angle = Time.time * glowFrequency * 2f * Mathf.PI;

            // Map sin wave from [-1, 1] to [0, 1]
            float normalizedPulse = (Mathf.Sin(angle) + 1f) / 2f;

            // Map to intensity range [min, max]
            float intensity = Mathf.Lerp(glowMinIntensity, glowMaxIntensity, normalizedPulse);

            glowLight.intensity = intensity;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Enables or disables the glow effect at runtime.
        /// </summary>
        public void SetGlowEnabled(bool enabled)
        {
            enableGlow = enabled;
            if (glowLight != null)
            {
                glowLight.enabled = enabled;
            }
        }

        /// <summary>
        /// Sets the glow color at runtime.
        /// </summary>
        public void SetGlowColor(Color color)
        {
            glowColor = color;
            if (glowLight != null)
            {
                glowLight.color = color;
            }
        }

        /// <summary>
        /// Sets the glow range at runtime.
        /// </summary>
        public void SetGlowRange(float range)
        {
            glowRange = range;
            if (glowLight != null)
            {
                glowLight.range = range;
            }
        }

        #endregion
    }
}

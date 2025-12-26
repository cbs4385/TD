using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Adds a slowly pulsing glow effect to visitor models.
    /// </summary>
    public class VisitorGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Glow Settings")]
        [SerializeField]
        [Tooltip("Enable pulsing glow light effect")]
        private bool enableGlow = true;

        [SerializeField]
        [Tooltip("Color of the glow")]
        private Color glowColor = new Color(0.9f, 0.85f, 1f, 1f); // Soft purple-white

        [SerializeField]
        [Tooltip("Radius of the glow effect")]
        private float glowRadius = 5f;

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

        private Light2D glowLight;

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

            try
            {
                // Remove any old 3D Light components that might conflict
                var oldLight = GetComponent<Light>();
                if (oldLight != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(oldLight);
#else
                    Destroy(oldLight);
#endif
                }

                // Check if we already have a Light2D component
                glowLight = GetComponent<Light2D>();
                if (glowLight == null)
                {
                    glowLight = gameObject.AddComponent<Light2D>();
                }

                // Configure the 2D light
                glowLight.lightType = Light2D.LightType.Point;
                glowLight.color = glowColor;
                glowLight.pointLightOuterRadius = glowRadius;
                glowLight.intensity = glowMaxIntensity;

                // Additional Light2D settings for proper color rendering
                glowLight.pointLightInnerRadius = 0f;
                glowLight.pointLightInnerAngle = 360f;
                glowLight.pointLightOuterAngle = 360f;

                // Use additive blend style (1) for colored lights instead of multiply (0)
                // Additive blending preserves light colors better
                glowLight.blendStyleIndex = 1;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[VisitorGlow] Failed to setup glow light: {e.Message}");
                glowLight = null;
            }
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
        /// Sets the glow radius at runtime.
        /// </summary>
        public void SetGlowRadius(float radius)
        {
            glowRadius = radius;
            if (glowLight != null)
            {
                glowLight.pointLightOuterRadius = radius;
            }
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FaeMaze.Systems;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Controls depth of field post-processing effect for maze scenes.
    /// Creates a tilt-shift blur effect with a central focused area.
    /// </summary>
    public class CameraDepthOfFieldController : MonoBehaviour
    {
        [Header("Volume References")]
        [SerializeField]
        [Tooltip("The volume containing the depth of field effect")]
        private Volume postProcessVolume;

        [Header("Depth of Field Settings")]
        [SerializeField]
        [Tooltip("Enable or disable the depth of field effect")]
        private bool enableDepthOfField = true;

        [SerializeField]
        [Tooltip("Focus distance (where the scene is sharp)")]
        private float focusDistance = 10f;

        [SerializeField]
        [Tooltip("Aperture value (lower = more blur)")]
        private float aperture = 5.6f;

        [SerializeField]
        [Tooltip("Gaussian blur start distance")]
        private float gaussianStart = 10f;

        [SerializeField]
        [Tooltip("Gaussian blur end distance")]
        private float gaussianEnd = 30f;

        [SerializeField]
        [Tooltip("Maximum blur radius")]
        private float gaussianMaxRadius = 1f;

        private DepthOfField depthOfField;

        private void Start()
        {
            // Find volume if not assigned
            if (postProcessVolume == null)
            {
                postProcessVolume = FindFirstObjectByType<Volume>();
            }

            // Get depth of field component from volume
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                if (postProcessVolume.profile.TryGet(out depthOfField))
                {
                    // Load settings from GameSettings
                    LoadFromSettings();
                }
                else
                {
                    Debug.LogWarning("[CameraDepthOfFieldController] DepthOfField component not found in volume profile");
                }
            }
            else
            {
                Debug.LogWarning("[CameraDepthOfFieldController] Post-process volume or profile not found");
            }
        }

        private void ApplySettings()
        {
            if (depthOfField == null) return;

            // Set mode to Gaussian for tilt-shift effect
            depthOfField.mode.overrideState = true;
            depthOfField.mode.value = DepthOfFieldMode.Gaussian;

            // Apply Gaussian mode settings
            depthOfField.gaussianStart.overrideState = true;
            depthOfField.gaussianStart.value = gaussianStart;

            depthOfField.gaussianEnd.overrideState = true;
            depthOfField.gaussianEnd.value = gaussianEnd;

            depthOfField.gaussianMaxRadius.overrideState = true;
            depthOfField.gaussianMaxRadius.value = gaussianMaxRadius;

            depthOfField.highQualitySampling.overrideState = true;
            depthOfField.highQualitySampling.value = true;

            // Apply focus settings (for Bokeh mode, but kept for compatibility)
            depthOfField.focusDistance.overrideState = true;
            depthOfField.focusDistance.value = focusDistance;

            depthOfField.aperture.overrideState = true;
            depthOfField.aperture.value = aperture;

            // Enable/disable the effect
            depthOfField.active = enableDepthOfField;
        }

        /// <summary>
        /// Enable or disable the depth of field effect
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enableDepthOfField = enabled;
            if (depthOfField != null)
            {
                depthOfField.active = enabled;
            }
        }

        /// <summary>
        /// Set the blur intensity (0 = no blur, 1 = maximum blur)
        /// </summary>
        public void SetBlurIntensity(float intensity)
        {
            intensity = Mathf.Clamp01(intensity);

            if (depthOfField != null)
            {
                // Adjust Gaussian blur parameters based on intensity
                depthOfField.gaussianStart.value = Mathf.Lerp(20f, 5f, intensity);
                depthOfField.gaussianEnd.value = Mathf.Lerp(40f, 20f, intensity);
                depthOfField.gaussianMaxRadius.value = Mathf.Lerp(0.5f, 1.5f, intensity);
            }

            gaussianStart = depthOfField.gaussianStart.value;
            gaussianEnd = depthOfField.gaussianEnd.value;
            gaussianMaxRadius = depthOfField.gaussianMaxRadius.value;
        }

        /// <summary>
        /// Load depth of field settings from GameSettings
        /// </summary>
        public void LoadFromSettings()
        {
            enableDepthOfField = GameSettings.EnableDepthOfField;
            SetEnabled(enableDepthOfField);
            SetBlurIntensity(GameSettings.DepthOfFieldIntensity);
        }

        private void OnValidate()
        {
            // Apply changes in editor when values are modified
            if (Application.isPlaying && depthOfField != null)
            {
                ApplySettings();
            }
        }
    }
}

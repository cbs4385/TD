using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FaeMaze.PostProcessing;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Automatically configures the Global Volume with Bloom, Fog, and RadialBlur effects.
    /// Attach this to any Global Volume that needs these effects.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    [ExecuteAlways]
    public class GlobalVolumeSetup : MonoBehaviour
    {
        [Header("Bloom Settings")]
        [Tooltip("Threshold for bloom - values above this will bloom")]
        [SerializeField] private float bloomThreshold = 0.9f;

        [Tooltip("Intensity of the bloom effect")]
        [SerializeField] private float bloomIntensity = 1.5f;

        [Tooltip("Scatter/spread of the bloom")]
        [SerializeField] private float bloomScatter = 0.7f;

        [Header("Fog Settings")]
        [Tooltip("Enable fog for visible light rays")]
        [SerializeField] private bool enableFog = true;

        [Tooltip("Fog color")]
        [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.6f, 1f);

        [Tooltip("Fog density/thickness")]
        [SerializeField] private float fogDensity = 0.02f;

        [Header("Radial Blur / Vignette Settings")]
        [Tooltip("Enable radial blur and vignette effect")]
        [SerializeField] private bool enableRadialBlur = true;

        [Tooltip("Clear radius as percentage of screen (0-100). Higher = more clear area in center.")]
        [SerializeField] private float blurClearRadius = 85f;

        [Tooltip("Intensity of the blur effect")]
        [SerializeField] private float blurIntensity = 0.5f;

        [Tooltip("Number of blur samples (higher = better quality)")]
        [SerializeField] private int blurSamples = 8;

        [Tooltip("Vignette coverage as percentage (0-100). Higher = more screen darkening.")]
        [SerializeField] private float vignetteCoverage = 30f;

        [Tooltip("Vignette darkness intensity (0-1). Higher = darker vignette.")]
        [SerializeField] private float vignetteIntensity = 0.8f;

        private Volume volume;

        private void OnEnable()
        {
            SetupVolume();
            SetupRenderSettingsFog();
        }

        private void OnValidate()
        {
            SetupVolume();
            SetupRenderSettingsFog();
        }

        private void SetupVolume()
        {
            volume = GetComponent<Volume>();
            if (volume == null)
            {
                Debug.LogError("[GlobalVolumeSetup] No Volume component found!");
                return;
            }

            // Create a new profile if none exists
            if (volume.profile == null)
            {
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile.name = "Global Volume Profile";
            }

            // Ensure volume is global
            volume.isGlobal = true;
            volume.priority = 1;

            // Add/configure Bloom
            ConfigureBloom();

            // Add/configure RadialBlur
            ConfigureRadialBlur();

        }

        private void SetupRenderSettingsFog()
        {
            if (!enableFog) return;

            // Use Unity's built-in fog via RenderSettings
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        private void ConfigureBloom()
        {
            if (volume.profile == null) return;

            Bloom bloom;
            if (!volume.profile.TryGet<Bloom>(out bloom))
            {
                bloom = volume.profile.Add<Bloom>(true);
            }

            if (bloom != null)
            {
                bloom.active = true;
                bloom.threshold.overrideState = true;
                bloom.threshold.value = bloomThreshold;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = bloomIntensity;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = bloomScatter;
            }
        }

        private void ConfigureRadialBlur()
        {
            if (volume.profile == null)
            {
                Debug.LogError("[GlobalVolumeSetup] ConfigureRadialBlur: profile is null!");
                return;
            }

            RadialBlur radialBlur;
            if (!volume.profile.TryGet<RadialBlur>(out radialBlur))
            {
                radialBlur = volume.profile.Add<RadialBlur>(true);
            }

            if (radialBlur != null)
            {
                radialBlur.active = true;
                radialBlur.enabled.overrideState = true;
                radialBlur.enabled.value = enableRadialBlur;
                radialBlur.blurAngleDegrees.overrideState = true;
                radialBlur.blurAngleDegrees.value = blurClearRadius;
                radialBlur.blurIntensity.overrideState = true;
                radialBlur.blurIntensity.value = blurIntensity;
                radialBlur.blurSamples.overrideState = true;
                radialBlur.blurSamples.value = blurSamples;
                radialBlur.vignetteCoverage.overrideState = true;
                radialBlur.vignetteCoverage.value = vignetteCoverage;
                radialBlur.vignetteIntensity.overrideState = true;
                radialBlur.vignetteIntensity.value = vignetteIntensity;

            }
            else
            {
                Debug.LogError("[GlobalVolumeSetup] Failed to add RadialBlur to profile!");
            }
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Automatically configures the Global Volume with Bloom and Fog effects for lantern glow.
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
            if (volume == null) return;

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
    }
}

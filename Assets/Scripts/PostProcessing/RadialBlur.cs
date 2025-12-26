using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FaeMaze.PostProcessing
{
    [System.Serializable, VolumeComponentMenu("Post-processing/Radial Blur")]
    public class RadialBlur : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Enable or disable the radial blur effect")]
        public BoolParameter enabled = new BoolParameter(false);

        [Tooltip("Percentage of screen radius that remains clear (80 = center 80% is sharp, outer 20% is blurred)")]
        public ClampedFloatParameter clearRadiusPercent = new ClampedFloatParameter(80f, 0f, 100f);

        [Tooltip("Intensity of the blur effect")]
        public ClampedFloatParameter blurIntensity = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Tooltip("Number of blur samples (higher = better quality, lower performance)")]
        public ClampedIntParameter blurSamples = new ClampedIntParameter(8, 4, 16);

        public bool IsActive() => enabled.value;

        public bool IsTileCompatible() => false;
    }
}

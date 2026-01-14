using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Reflection;
using FaeMaze.PostProcessing;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Automatically sets up PostProcessVolume when any scene loads
    /// This ensures vignette and radial blur effects are available
    /// </summary>
    public static class PostProcessVolumeRuntimeSetup
    {
        private static bool hasSetup = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupPostProcessVolume()
        {
            // Only setup once per scene load
            if (hasSetup)
            {
                hasSetup = false; // Reset for next scene
                return;
            }

            hasSetup = true;

            // Ensure Main Camera has post-processing enabled
            EnableCameraPostProcessing();

            // Check if Volume already exists
            Volume existingVolume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            if (existingVolume != null)
            {
                // Add Vignette for edge darkening effect
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<Vignette>(out var existingVignette))
                {
                    existingVignette = existingVolume.profile.Add<Vignette>(true);
                    existingVignette.intensity.overrideState = true;
                    existingVignette.intensity.value = 0.35f; // Moderate darkening at edges
                    existingVignette.smoothness.overrideState = true;
                    existingVignette.smoothness.value = 0.4f; // Smooth falloff
                    existingVignette.rounded.overrideState = true;
                    existingVignette.rounded.value = false; // Not rounded for better coverage
                }

                // Add RadialBlur for angle-based edge blur
                TryAddRadialBlur(existingVolume.profile);

                // Add Bloom for lantern glow effects
                TryAddBloom(existingVolume.profile);

                // Enable fog for visible light effects
                EnableFog();
                return;
            }

            // Try to find profile
            VolumeProfile profile = null;

            #if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:VolumeProfile PostProcessingProfile");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            }
            #else
            // In build, try to load from Resources
            profile = Resources.Load<VolumeProfile>("PostProcessingProfile");
            #endif

            if (profile == null)
            {
                return;
            }

            // Create new Volume GameObject
            GameObject volumeObject = new GameObject("PostProcessVolume");
            Volume volume = volumeObject.AddComponent<Volume>();

            // Configure volume
            volume.isGlobal = true;
            volume.priority = 1;
            volume.profile = profile;

            // Add Vignette for edge darkening effect
            if (!profile.TryGet<Vignette>(out var newVignette))
            {
                newVignette = profile.Add<Vignette>(true);
                newVignette.intensity.overrideState = true;
                newVignette.intensity.value = 0.35f; // Moderate darkening at edges
                newVignette.smoothness.overrideState = true;
                newVignette.smoothness.value = 0.4f; // Smooth falloff
                newVignette.rounded.overrideState = true;
                newVignette.rounded.value = false; // Not rounded for better coverage
            }

            // Add RadialBlur for angle-based edge blur
            TryAddRadialBlur(profile);

            // Add Bloom for lantern glow effects
            TryAddBloom(profile);

            // Enable fog for visible light effects
            EnableFog();
        }

        private static void EnableFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.4f, 0.4f, 0.5f, 1f);
            RenderSettings.fogDensity = 0.015f;
        }

        private static void EnableCameraPostProcessing()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var cameraData = mainCamera.GetUniversalAdditionalCameraData();
                if (cameraData != null)
                {
                    if (!cameraData.renderPostProcessing)
                    {
                        cameraData.renderPostProcessing = true;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to add RadialBlur component to profile
        /// </summary>
        private static void TryAddRadialBlur(VolumeProfile profile)
        {
            if (profile == null)
                return;

            // Check if RadialBlur already exists in profile
            RadialBlur radialBlur;
            bool isNew = !profile.TryGet<RadialBlur>(out radialBlur);

            if (isNew)
            {
                // Add RadialBlur component
                radialBlur = profile.Add<RadialBlur>(true);
            }

            if (radialBlur != null)
            {
                // Set properties with override states (whether new or existing)
                radialBlur.enabled.overrideState = true;
                radialBlur.blurAngleDegrees.overrideState = true;
                radialBlur.blurIntensity.overrideState = true;
                radialBlur.blurSamples.overrideState = true;

                // Only set default values for new components
                if (isNew)
                {
                    radialBlur.enabled.value = false;  // Disable RadialBlur for new components
                    radialBlur.blurAngleDegrees.value = 85f;  // 85% of screen is clear - only blur outer 15%
                    radialBlur.blurIntensity.value = 0.3f;    // Low intensity for subtle vignette
                    radialBlur.blurSamples.value = 8;
                }
                else
                {
                    // For existing components, ensure values are reasonable (fix if 0)
                    if (radialBlur.blurAngleDegrees.value < 1f)
                    {
                        radialBlur.blurAngleDegrees.value = 85f;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to add Bloom component to profile for lantern glow effects
        /// </summary>
        private static void TryAddBloom(VolumeProfile profile)
        {
            if (profile == null)
                return;

            // Check if Bloom already exists in profile
            Bloom bloom;
            bool isNew = !profile.TryGet<Bloom>(out bloom);

            if (isNew)
            {
                // Add Bloom component
                bloom = profile.Add<Bloom>(true);
            }

            if (bloom != null)
            {
                // Set properties with override states
                bloom.active = true;
                bloom.threshold.overrideState = true;
                bloom.intensity.overrideState = true;
                bloom.scatter.overrideState = true;

                // Set values for good lantern glow effect
                if (isNew)
                {
                    bloom.threshold.value = 0.9f;      // Only bloom bright HDR colors
                    bloom.intensity.value = 1.5f;      // Strong bloom for visible glow
                    bloom.scatter.value = 0.7f;        // Spread the glow nicely
                }
            }
        }
    }
}

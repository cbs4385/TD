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
                Debug.Log("[PostProcessVolumeRuntimeSetup] Volume already exists");

                // Add Vignette for edge darkening effect
                EnsureVignette(existingVolume.profile);

                // Add RadialBlur for angle-based edge blur
                TryAddRadialBlur(existingVolume.profile);
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
                Debug.LogWarning("[PostProcessVolumeRuntimeSetup] Could not find PostProcessingProfile");
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
            EnsureVignette(profile);

            // Add RadialBlur for angle-based edge blur
            TryAddRadialBlur(profile);

            Debug.Log("[PostProcessVolumeRuntimeSetup] Created PostProcessVolume with vignette effects");
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
                        Debug.Log("[PostProcessVolumeRuntimeSetup] Enabled post-processing on Main Camera");
                    }
                }
                else
                {
                    Debug.LogWarning("[PostProcessVolumeRuntimeSetup] Main Camera does not have URP camera data");
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

            if (!isNew)
            {
                Debug.Log("[PostProcessVolumeRuntimeSetup] RadialBlur already exists in profile, ensuring override states are set");
            }
            else
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
                radialBlur.vignetteIntensity.overrideState = true;

                // Only set default values for new components
                if (isNew)
                {
                    radialBlur.enabled.value = false;  // Disable RadialBlur for new components
                    radialBlur.blurAngleDegrees.value = 85f;  // 85% of screen is clear - only blur outer 15%
                    radialBlur.blurIntensity.value = 1f;    // Strong blur by default
                    radialBlur.blurSamples.value = 4;
                    radialBlur.vignetteIntensity.value = 1f; // Fully darkened when enabled
                }
                else
                {
                    // For existing components, ensure values are reasonable (fix if 0)
                    if (radialBlur.blurAngleDegrees.value < 1f)
                    {
                        radialBlur.blurAngleDegrees.value = 85f;
                        Debug.Log("[PostProcessVolumeRuntimeSetup] Fixed RadialBlur angle from 0 to 85%");
                    }
                }

                Debug.Log($"[PostProcessVolumeRuntimeSetup] {(isNew ? "Added" : "Updated")} RadialBlur component: clearRadius={radialBlur.blurAngleDegrees.value}%, intensity={radialBlur.blurIntensity.value}, samples={radialBlur.blurSamples.value}, enabled={radialBlur.enabled.value}");
            }
        }

        /// <summary>
        /// Ensures a Vignette exists on the given profile and is configured with rounded edges.
        /// </summary>
        private static void EnsureVignette(VolumeProfile profile)
        {
            if (profile == null)
                return;

            if (!profile.TryGet<Vignette>(out var vignette))
            {
                vignette = profile.Add<Vignette>(true);
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added Vignette component to profile");
            }

            vignette.intensity.overrideState = true;
            vignette.intensity.value = 1f; // Maximum darkening at edges by default
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 1f; // Smoothest falloff
            vignette.rounded.overrideState = true;
            vignette.rounded.value = true; // Rounded edges as requested
        }
    }
}

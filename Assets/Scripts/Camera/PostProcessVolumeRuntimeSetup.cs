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
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<Vignette>(out var existingVignette))
                {
                    existingVignette = existingVolume.profile.Add<Vignette>(true);
                    existingVignette.intensity.value = 0.35f; // Moderate darkening at edges
                    existingVignette.smoothness.value = 0.4f; // Smooth falloff
                    existingVignette.rounded.value = false; // Not rounded for better coverage
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added Vignette component to existing profile");
                }

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
            if (!profile.TryGet<Vignette>(out var newVignette))
            {
                newVignette = profile.Add<Vignette>(true);
                newVignette.intensity.value = 0.35f; // Moderate darkening at edges
                newVignette.smoothness.value = 0.4f; // Smooth falloff
                newVignette.rounded.value = false; // Not rounded for better coverage
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added Vignette component to profile");
            }

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
            if (profile.TryGet<RadialBlur>(out var existingBlur))
            {
                Debug.Log("[PostProcessVolumeRuntimeSetup] RadialBlur already exists in profile");
                return;
            }

            // Add RadialBlur component
            var radialBlur = profile.Add<RadialBlur>(true);
            if (radialBlur != null)
            {
                // Set properties directly
                radialBlur.enabled.value = false;  // Disable RadialBlur for now
                radialBlur.blurAngleDegrees.value = 75f;  // 75% of screen is clear
                radialBlur.blurIntensity.value = 0.3f;    // Low intensity for subtle vignette
                radialBlur.blurSamples.value = 8;

                Debug.Log($"[PostProcessVolumeRuntimeSetup] Added RadialBlur component: clearRadius={radialBlur.blurAngleDegrees.value}%, intensity={radialBlur.blurIntensity.value}, samples={radialBlur.blurSamples.value}, enabled={radialBlur.enabled.value}");
            }
        }
    }
}

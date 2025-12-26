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
    /// This ensures depth of field effect is always available
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

                // Depth of Field disabled - not needed for gameplay visibility
                // If you want background blur, uncomment and adjust aperture to 32 or higher for subtle effect
                /*
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<DepthOfField>(out var existingDof))
                {
                    existingDof = existingVolume.profile.Add<DepthOfField>(true);
                    existingDof.mode.value = DepthOfFieldMode.Bokeh;
                    existingDof.focusDistance.value = 5f;
                    existingDof.aperture.value = 32f; // Higher aperture = less blur
                    existingDof.focalLength.value = 50f;
                    existingDof.bladeCount.value = 6;
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField (Bokeh mode) with subtle blur");
                }
                */

                // Add subtle Vignette for edge darkening effect
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<Vignette>(out var existingVignette))
                {
                    existingVignette = existingVolume.profile.Add<Vignette>(true);
                    existingVignette.intensity.value = 0.2f; // Subtle darkening at edges only
                    existingVignette.smoothness.value = 0.5f; // Very smooth falloff
                    existingVignette.rounded.value = false; // Not rounded for better coverage
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added subtle Vignette component to existing profile");
                }

                // RadialBlur disabled - causing white screen issue
                // TryAddRadialBlur(existingVolume.profile);

                // Ensure it has the controller
                if (existingVolume.GetComponent<CameraDepthOfFieldController>() == null)
                {
                    existingVolume.gameObject.AddComponent<CameraDepthOfFieldController>();
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added CameraDepthOfFieldController");
                }
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

            // Depth of Field disabled - not needed for gameplay visibility
            // If you want background blur, uncomment and adjust aperture to 32 or higher for subtle effect
            /*
            if (!profile.TryGet<DepthOfField>(out var newDof))
            {
                newDof = profile.Add<DepthOfField>(true);
                newDof.mode.value = DepthOfFieldMode.Bokeh;
                newDof.focusDistance.value = 5f;
                newDof.aperture.value = 32f; // Higher aperture = less blur
                newDof.focalLength.value = 50f;
                newDof.bladeCount.value = 6;
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField (Bokeh mode) with subtle blur");
            }
            */

            // Add subtle Vignette for edge darkening effect
            if (!profile.TryGet<Vignette>(out var newVignette))
            {
                newVignette = profile.Add<Vignette>(true);
                newVignette.intensity.value = 0.2f; // Subtle darkening at edges only
                newVignette.smoothness.value = 0.5f; // Very smooth falloff
                newVignette.rounded.value = false; // Not rounded for better coverage
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added subtle Vignette component to profile");
            }

            // RadialBlur disabled - causing white screen issue
            // TryAddRadialBlur(profile);

            // Add controller
            volumeObject.AddComponent<CameraDepthOfFieldController>();

            Debug.Log("[PostProcessVolumeRuntimeSetup] Created PostProcessVolume with depth of field");
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
                // Set properties directly - vignette effect with 95% clear center
                radialBlur.enabled.value = true;
                radialBlur.clearRadiusPercent.value = 95f;  // Center 95% is clear, only outer 5% is blurred
                radialBlur.blurIntensity.value = 0.8f;
                radialBlur.blurSamples.value = 12;

                Debug.Log("[PostProcessVolumeRuntimeSetup] Added RadialBlur component to profile");
            }
        }
    }
}

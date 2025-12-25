using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Reflection;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Automatically sets up PostProcessVolume when any scene loads
    /// This ensures depth of field effect is always available
    /// </summary>
    public static class PostProcessVolumeRuntimeSetup
    {
        private static bool hasSetup = false;
        private static Type radialBlurType = null;

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

                // Ensure DepthOfField component exists in profile
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<DepthOfField>(out var existingDof))
                {
                    existingDof = existingVolume.profile.Add<DepthOfField>(true);
                    existingDof.mode.value = DepthOfFieldMode.Bokeh;
                    existingDof.focusDistance.value = 5f; // Focus closer to camera
                    existingDof.aperture.value = 0.1f; // Extremely low aperture for strong blur
                    existingDof.focalLength.value = 50f;
                    existingDof.bladeCount.value = 6;
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField (Bokeh mode) with extreme blur settings");
                }

                // Add Vignette for edge darkening/blur effect
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<Vignette>(out var existingVignette))
                {
                    existingVignette = existingVolume.profile.Add<Vignette>(true);
                    existingVignette.intensity.value = 0.35f; // Moderate darkening at edges
                    existingVignette.smoothness.value = 0.4f; // Smooth falloff
                    existingVignette.rounded.value = false; // Not rounded for better coverage
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added Vignette component to existing profile");
                }

                // Add RadialBlur for angle-based edge blur (using reflection to avoid compile-time dependency)
                TryAddRadialBlur(existingVolume.profile);

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

            // Ensure DepthOfField component exists in profile
            if (!profile.TryGet<DepthOfField>(out var newDof))
            {
                newDof = profile.Add<DepthOfField>(true);
                newDof.mode.value = DepthOfFieldMode.Bokeh;
                newDof.focusDistance.value = 5f; // Focus closer to camera
                newDof.aperture.value = 0.1f; // Extremely low aperture for strong blur
                newDof.focalLength.value = 50f;
                newDof.bladeCount.value = 6;
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField (Bokeh mode) with extreme blur settings");
            }

            // Add Vignette for edge darkening/blur effect
            if (!profile.TryGet<Vignette>(out var newVignette))
            {
                newVignette = profile.Add<Vignette>(true);
                newVignette.intensity.value = 0.35f; // Moderate darkening at edges
                newVignette.smoothness.value = 0.4f; // Smooth falloff
                newVignette.rounded.value = false; // Not rounded for better coverage
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added Vignette component to profile");
            }

            // Add RadialBlur for angle-based edge blur (using reflection to avoid compile-time dependency)
            TryAddRadialBlur(profile);

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
        /// Attempts to add RadialBlur component to profile using reflection
        /// This avoids compile-time dependency on FaeMaze.PostProcessing assembly
        /// </summary>
        private static void TryAddRadialBlur(VolumeProfile profile)
        {
            if (profile == null)
                return;

            // Cache the RadialBlur type lookup
            if (radialBlurType == null)
            {
                radialBlurType = Type.GetType("FaeMaze.PostProcessing.RadialBlur, FaeMaze.PostProcessing");
                if (radialBlurType == null)
                {
                    // Try fallback for Assembly-CSharp (if assembly definitions aren't set up)
                    radialBlurType = Type.GetType("FaeMaze.PostProcessing.RadialBlur, Assembly-CSharp");
                }

                if (radialBlurType == null)
                {
                    Debug.LogWarning("[PostProcessVolumeRuntimeSetup] RadialBlur type not found. Skipping radial blur setup.");
                    return;
                }
            }

            // Check if RadialBlur already exists in profile
            MethodInfo tryGetMethod = typeof(VolumeProfile).GetMethod("TryGet");
            if (tryGetMethod != null)
            {
                MethodInfo genericTryGet = tryGetMethod.MakeGenericMethod(radialBlurType);
                object[] parameters = new object[] { null };
                bool hasComponent = (bool)genericTryGet.Invoke(profile, parameters);

                if (hasComponent)
                {
                    Debug.Log("[PostProcessVolumeRuntimeSetup] RadialBlur already exists in profile");
                    return;
                }
            }

            // Add RadialBlur component
            MethodInfo addMethod = typeof(VolumeProfile).GetMethod("Add");
            if (addMethod != null)
            {
                MethodInfo genericAdd = addMethod.MakeGenericMethod(radialBlurType);
                object radialBlur = genericAdd.Invoke(profile, new object[] { true });

                if (radialBlur != null)
                {
                    // Set properties using reflection
                    SetVolumeParameter(radialBlur, "enabled", true);
                    SetVolumeParameter(radialBlur, "blurAngleDegrees", 10f);
                    SetVolumeParameter(radialBlur, "blurIntensity", 0.8f);
                    SetVolumeParameter(radialBlur, "blurSamples", 12);

                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added RadialBlur component to profile");
                }
            }
        }

        /// <summary>
        /// Sets a VolumeParameter value using reflection
        /// </summary>
        private static void SetVolumeParameter<T>(object component, string paramName, T value)
        {
            FieldInfo field = component.GetType().GetField(paramName);
            if (field != null)
            {
                object parameter = field.GetValue(component);
                if (parameter != null)
                {
                    PropertyInfo valueProperty = parameter.GetType().GetProperty("value");
                    if (valueProperty != null)
                    {
                        valueProperty.SetValue(parameter, value);
                    }
                }
            }
        }
    }
}

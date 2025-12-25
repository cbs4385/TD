using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            Volume existingVolume = Object.FindFirstObjectByType<Volume>();
            if (existingVolume != null)
            {
                Debug.Log("[PostProcessVolumeRuntimeSetup] Volume already exists");

                // Ensure DepthOfField component exists in profile
                if (existingVolume.profile != null && !existingVolume.profile.TryGet<DepthOfField>(out var existingDof))
                {
                    existingDof = existingVolume.profile.Add<DepthOfField>(true);
                    existingDof.mode.value = DepthOfFieldMode.Gaussian;
                    existingDof.gaussianStart.value = 320f;
                    existingDof.gaussianEnd.value = 420f;
                    existingDof.gaussianMaxRadius.value = 2.5f;
                    existingDof.focusDistance.value = 383f;
                    Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField component to existing profile");
                }

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
                newDof.mode.value = DepthOfFieldMode.Gaussian;
                newDof.gaussianStart.value = 320f;
                newDof.gaussianEnd.value = 420f;
                newDof.gaussianMaxRadius.value = 2.5f;
                newDof.focusDistance.value = 383f;
                Debug.Log("[PostProcessVolumeRuntimeSetup] Added DepthOfField component to profile");
            }

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
    }
}

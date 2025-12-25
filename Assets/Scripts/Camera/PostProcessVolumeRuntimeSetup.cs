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

            // Check if Volume already exists
            Volume existingVolume = Object.FindFirstObjectByType<Volume>();
            if (existingVolume != null)
            {
                Debug.Log("[PostProcessVolumeRuntimeSetup] Volume already exists");

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

            // Add controller
            volumeObject.AddComponent<CameraDepthOfFieldController>();

            Debug.Log("[PostProcessVolumeRuntimeSetup] Created PostProcessVolume with depth of field");

            // Verify Main Camera has post-processing enabled
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var cameraData = mainCamera.GetUniversalAdditionalCameraData();
                if (cameraData != null && !cameraData.renderPostProcessing)
                {
                    Debug.LogWarning("[PostProcessVolumeRuntimeSetup] Post-processing is disabled on Main Camera");
                }
            }
        }
    }
}

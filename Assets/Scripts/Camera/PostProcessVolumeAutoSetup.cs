using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Automatically sets up PostProcessVolume for maze scenes if not present
    /// Attach this to any GameObject in scenes that need post-processing
    /// </summary>
    public class PostProcessVolumeAutoSetup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The post-processing profile to use")]
        private VolumeProfile profile;

        [SerializeField]
        [Tooltip("Auto-find profile if not assigned")]
        private bool autoFindProfile = true;

        private void Awake()
        {
            SetupPostProcessVolume();
        }

        private void SetupPostProcessVolume()
        {
            // Check if Volume already exists
            Volume existingVolume = FindFirstObjectByType<Volume>();
            if (existingVolume != null)
            {
                Debug.Log("[PostProcessVolumeAutoSetup] Volume already exists, skipping setup");
                return;
            }

            // Try to find profile if not assigned
            if (profile == null && autoFindProfile)
            {
                profile = Resources.Load<VolumeProfile>("PostProcessingProfile");

                // If not in Resources, try to find it in the project
                if (profile == null)
                {
                    #if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:VolumeProfile PostProcessingProfile");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                        Debug.Log($"[PostProcessVolumeAutoSetup] Found profile at {path}");
                    }
                    #endif
                }
            }

            if (profile == null)
            {
                Debug.LogError("[PostProcessVolumeAutoSetup] Could not find PostProcessingProfile. Please assign it in the inspector.");
                return;
            }

            // Create new Volume GameObject
            GameObject volumeObject = new GameObject("PostProcessVolume");
            Volume volume = volumeObject.AddComponent<Volume>();

            // Configure volume
            volume.isGlobal = true;
            volume.priority = 1;
            volume.profile = profile;

            Debug.Log("[PostProcessVolumeAutoSetup] Created and configured PostProcessVolume");

            // Verify Main Camera has post-processing enabled
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var cameraData = mainCamera.GetUniversalAdditionalCameraData();
                if (cameraData != null)
                {
                    if (!cameraData.renderPostProcessing)
                    {
                        Debug.LogWarning("[PostProcessVolumeAutoSetup] Post-processing is disabled on Main Camera. Please enable it in the camera settings.");
                    }
                }
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-find profile in editor
            if (profile == null && autoFindProfile)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:VolumeProfile PostProcessingProfile");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    profile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                }
            }
        }
        #endif
    }
}

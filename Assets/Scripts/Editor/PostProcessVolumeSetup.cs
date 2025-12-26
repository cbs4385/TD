using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FaeMaze.Cameras;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to setup PostProcessVolume in maze scenes
    /// </summary>
    public static class PostProcessVolumeSetup
    {
        private const string PROFILE_PATH = "Assets/Settings/PostProcessingProfile.asset";
        private const string FAE_MAZE_SCENE = "Assets/Scenes/FaeMazeScene.unity";
        private const string PROCEDURAL_MAZE_SCENE = "Assets/Scenes/ProceduralMazeScene.unity";

        [MenuItem("FaeMaze/Setup Post-Processing/Setup All Scenes")]
        public static void SetupAllScenes()
        {
            SetupScene(FAE_MAZE_SCENE);
            SetupScene(PROCEDURAL_MAZE_SCENE);
            Debug.Log("[PostProcessVolumeSetup] All scenes configured successfully!");
        }

        [MenuItem("FaeMaze/Setup Post-Processing/Setup Current Scene")]
        public static void SetupCurrentScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[PostProcessVolumeSetup] No active scene found");
                return;
            }

            SetupSceneInternal(activeScene);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log($"[PostProcessVolumeSetup] Scene '{activeScene.name}' configured successfully!");
        }

        private static void SetupScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SetupSceneInternal(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetupSceneInternal(Scene scene)
        {
            // Load the profile
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
            if (profile == null)
            {
                Debug.LogError($"[PostProcessVolumeSetup] Could not find profile at {PROFILE_PATH}");
                return;
            }

            // Find or create PostProcessVolume
            Volume volume = GameObject.FindFirstObjectByType<Volume>();
            GameObject volumeObject;

            if (volume == null)
            {
                // Create new volume GameObject
                volumeObject = new GameObject("PostProcessVolume");
                volume = volumeObject.AddComponent<Volume>();
                Debug.Log($"[PostProcessVolumeSetup] Created new PostProcessVolume in {scene.name}");
            }
            else
            {
                volumeObject = volume.gameObject;
                Debug.Log($"[PostProcessVolumeSetup] Found existing PostProcessVolume in {scene.name}");
            }

            // Configure volume
            volume.isGlobal = true;
            volume.priority = 1;
            volume.profile = profile;

            // Find and configure Main Camera
            Camera mainCamera = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<Camera>();
            if (mainCamera != null)
            {
                var cameraData = mainCamera.GetUniversalAdditionalCameraData();
                if (cameraData != null)
                {
                    cameraData.renderPostProcessing = true;
                    Debug.Log($"[PostProcessVolumeSetup] Enabled post-processing on Main Camera in {scene.name}");
                }
                else
                {
                    Debug.LogWarning($"[PostProcessVolumeSetup] Main Camera in {scene.name} does not have URP camera data");
                }
            }
            else
            {
                Debug.LogWarning($"[PostProcessVolumeSetup] Could not find Main Camera in {scene.name}");
            }
        }
    }
}

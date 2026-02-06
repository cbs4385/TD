using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FaeMaze.UI;

namespace FaeMaze.Editor
{
    public static class SceneLoadingMaskSetup
    {
        [MenuItem("FaeMaze/Setup Scene Loading Mask")]
        public static void SetupSceneLoadingMask()
        {
            // Open PlanarForestMazeScene
            string scenePath = "Assets/Scenes/PlanarForestMazeScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Check if SceneLoadingMask already exists
            var existing = Object.FindFirstObjectByType<SceneLoadingMask>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Scene Loading Mask",
                    "SceneLoadingMask already exists in the scene.", "OK");
                return;
            }

            // Create a new root GameObject
            GameObject maskObj = new GameObject("SceneLoadingMask");
            maskObj.AddComponent<SceneLoadingMask>();

            // Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Scene Loading Mask",
                "SceneLoadingMask has been added to PlanarForestMazeScene.\nSave the scene to keep changes.",
                "OK");
        }
    }
}

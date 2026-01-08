using UnityEngine;
using UnityEditor;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Ensures DynamicMazeGrowth component is added when entering play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class EnsureDynamicMazeGrowth
    {
        static EnsureDynamicMazeGrowth()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingEditMode)
            {
                // Check if we're in PlanarForestMazeScene
                var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene.name != "PlanarForestMazeScene")
                    return;

                // Find MazeRoot
                GameObject mazeRoot = GameObject.Find("MazeRoot");
                if (mazeRoot == null)
                    return;

                // Check if DynamicMazeGrowth is missing
                var dynamicGrowth = mazeRoot.GetComponent<DynamicMazeGrowth>();
                if (dynamicGrowth == null)
                {
                    return;
                }

                // Check if portal prefab is assigned
                var serializedObject = new SerializedObject(dynamicGrowth);
                var portalPrefabProp = serializedObject.FindProperty("portalPrefab");

                if (portalPrefabProp.objectReferenceValue == null)
                {
                    return;
                }
            }
        }
    }
}

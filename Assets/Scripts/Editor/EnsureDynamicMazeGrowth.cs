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
                if (mazeRoot.GetComponent<DynamicMazeGrowth>() == null)
                {
                    Debug.LogWarning("[EnsureDynamicMazeGrowth] DynamicMazeGrowth component is missing from MazeRoot. Run 'FaeMaze/Setup PlanarForest Maze Scene' to add it.");
                }
                else
                {
                    // Check if portal prefab is assigned
                    var dynamicGrowth = mazeRoot.GetComponent<DynamicMazeGrowth>();
                    var serializedObject = new SerializedObject(dynamicGrowth);
                    var portalPrefabProp = serializedObject.FindProperty("portalPrefab");

                    if (portalPrefabProp.objectReferenceValue == null)
                    {
                        Debug.LogWarning("[EnsureDynamicMazeGrowth] Portal prefab is not assigned to DynamicMazeGrowth. Run 'FaeMaze/Setup PlanarForest Maze Scene' to configure it.");
                    }
                }
            }
        }
    }
}

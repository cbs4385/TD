using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using FaeMaze.UI;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to set up the Minimap component in scenes.
    /// </summary>
    public static class MinimapSetup
    {
        [MenuItem("FaeMaze/Setup Minimap")]
        public static void SetupMinimap()
        {
            // Find or create GameObject for Minimap
            GameObject minimapObj = GameObject.Find("Minimap");
            if (minimapObj == null)
            {
                minimapObj = new GameObject("Minimap");
            }

            // Check if component already exists
            Minimap minimap = minimapObj.GetComponent<Minimap>();
            if (minimap == null)
            {
                minimap = minimapObj.AddComponent<Minimap>();
            }

            // Find MazeGridBehaviour
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();

            // Find focal point
            GameObject focalPointObj = GameObject.Find("Focal Point");

            // Configure Minimap
            SerializedObject minimapSO = new SerializedObject(minimap);
            minimapSO.FindProperty("focalPoint").objectReferenceValue = focalPointObj != null ? focalPointObj.transform : null;
            minimapSO.FindProperty("mazeGridBehaviour").objectReferenceValue = mazeGrid;
            minimapSO.FindProperty("sizePercent").floatValue = 0.2f;
            minimapSO.FindProperty("viewRadiusTiles").floatValue = 20f;
            minimapSO.FindProperty("mapCorner").enumValueIndex = 1; // TopRight
            minimapSO.FindProperty("edgePadding").floatValue = 20f;

            // Colors
            minimapSO.FindProperty("backgroundColor").colorValue = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            minimapSO.FindProperty("borderColor").colorValue = new Color(0.3f, 0.3f, 0.3f, 1f);
            minimapSO.FindProperty("crosshairColor").colorValue = Color.white;
            minimapSO.FindProperty("heartColor").colorValue = new Color(1f, 0.2f, 0.2f, 1f);
            minimapSO.FindProperty("visitorColor").colorValue = new Color(0.3f, 1f, 0.3f, 1f);
            minimapSO.FindProperty("pathColor").colorValue = new Color(0.4f, 0.4f, 0.4f, 0.5f);

            // Dot sizes
            minimapSO.FindProperty("heartDotSize").floatValue = 8f;
            minimapSO.FindProperty("visitorDotSize").floatValue = 4f;
            minimapSO.FindProperty("crosshairSize").floatValue = 10f;

            minimapSO.ApplyModifiedProperties();

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());


            Selection.activeGameObject = minimapObj;
        }
    }
}

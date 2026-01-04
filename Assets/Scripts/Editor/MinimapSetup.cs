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
                Debug.Log("Created new Minimap GameObject");
            }

            // Check if component already exists
            Minimap minimap = minimapObj.GetComponent<Minimap>();
            if (minimap == null)
            {
                minimap = minimapObj.AddComponent<Minimap>();
                Debug.Log("Added Minimap component");
            }

            // Find MazeGridBehaviour
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                Debug.LogWarning("MazeGridBehaviour not found in scene!");
            }

            // Find focal point
            GameObject focalPointObj = GameObject.Find("Focal Point");
            if (focalPointObj == null)
            {
                Debug.LogWarning("Focal Point not found! Minimap will search for it at runtime.");
            }

            // Configure Minimap
            SerializedObject minimapSO = new SerializedObject(minimap);
            minimapSO.FindProperty("focalPoint").objectReferenceValue = focalPointObj != null ? focalPointObj.transform : null;
            minimapSO.FindProperty("mazeGridBehaviour").objectReferenceValue = mazeGrid;
            minimapSO.FindProperty("sizePercent").floatValue = 0.1f;
            minimapSO.FindProperty("viewRadiusTiles").floatValue = 10f;
            minimapSO.FindProperty("mapCorner").enumValueIndex = 1; // TopRight
            minimapSO.FindProperty("edgePadding").floatValue = 20f;

            // Colors
            minimapSO.FindProperty("backgroundColor").colorValue = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            minimapSO.FindProperty("borderColor").colorValue = new Color(0.3f, 0.3f, 0.3f, 1f);
            minimapSO.FindProperty("crosshairColor").colorValue = Color.white;
            minimapSO.FindProperty("heartColor").colorValue = new Color(1f, 0.2f, 0.2f, 1f);
            minimapSO.FindProperty("visitorColor").colorValue = new Color(0.3f, 1f, 0.3f, 1f);

            // Dot sizes
            minimapSO.FindProperty("heartDotSize").floatValue = 8f;
            minimapSO.FindProperty("visitorDotSize").floatValue = 4f;
            minimapSO.FindProperty("crosshairSize").floatValue = 10f;

            minimapSO.ApplyModifiedProperties();

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("Minimap setup complete!");
            Debug.Log($"- MazeGridBehaviour: {(mazeGrid != null ? mazeGrid.name : "Not found")}");
            Debug.Log($"- Focal Point: {(focalPointObj != null ? focalPointObj.name : "Will search at runtime")}");
            Debug.Log($"- Size: 10% of screen");
            Debug.Log($"- View Radius: 10 tiles");
            Debug.Log($"- Corner: Top Right");

            Selection.activeGameObject = minimapObj;
        }
    }
}

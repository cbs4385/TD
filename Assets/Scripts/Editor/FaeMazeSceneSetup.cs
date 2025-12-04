using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    public class FaeMazeSceneSetup : MonoBehaviour
    {
        [MenuItem("FaeMaze/Setup FaeMaze Scene")]
        public static void SetupFaeMazeScene()
        {
            // Load the FaeMazeScene
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/FaeMazeScene.unity");

            // Find or create MazeOrigin
            GameObject mazeOriginObj = GameObject.Find("MazeOrigin");
            if (mazeOriginObj == null)
            {
                mazeOriginObj = new GameObject("MazeOrigin");
                mazeOriginObj.transform.position = Vector3.zero;
                UnityEngine.Debug.Log("[FaeMazeSceneSetup] Created MazeOrigin");
            }

            // Find or create MazeRoot (parent for maze grid)
            GameObject mazeRootObj = GameObject.Find("MazeRoot");
            if (mazeRootObj == null)
            {
                mazeRootObj = new GameObject("MazeRoot");
                mazeRootObj.transform.position = Vector3.zero;
                UnityEngine.Debug.Log("[FaeMazeSceneSetup] Created MazeRoot");
            }

            // Load the existing HedgeMaze1 map
            string mazePath = "Assets/Maps/HedgeMaze1.txt";
            TextAsset mazeFile = AssetDatabase.LoadAssetAtPath<TextAsset>(mazePath);

            if (mazeFile == null)
            {
                UnityEngine.Debug.LogError($"[FaeMazeSceneSetup] Could not find maze file at {mazePath}!");
                return;
            }

            UnityEngine.Debug.Log($"[FaeMazeSceneSetup] Loaded maze file from {mazePath}");

            // Add MazeGridBehaviour to MazeRoot if not present
            MazeGridBehaviour mazeGrid = mazeRootObj.GetComponent<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                mazeGrid = mazeRootObj.AddComponent<MazeGridBehaviour>();
                UnityEngine.Debug.Log("[FaeMazeSceneSetup] Added MazeGridBehaviour");
            }

            // Configure MazeGridBehaviour using SerializedObject
            SerializedObject mazeGridSO = new SerializedObject(mazeGrid);
            mazeGridSO.FindProperty("mazeFile").objectReferenceValue = mazeFile;
            mazeGridSO.FindProperty("mazeOrigin").objectReferenceValue = mazeOriginObj.transform;
            mazeGridSO.ApplyModifiedProperties();

            // Add MazeRenderer if not present
            if (mazeRootObj.GetComponent<MazeRenderer>() == null)
            {
                mazeRootObj.AddComponent<MazeRenderer>();
                UnityEngine.Debug.Log("[FaeMazeSceneSetup] Added MazeRenderer");
            }

            // Add MazeVisualSetup if not present
            if (mazeRootObj.GetComponent<MazeVisualSetup>() == null)
            {
                mazeRootObj.AddComponent<MazeVisualSetup>();
                UnityEngine.Debug.Log("[FaeMazeSceneSetup] Added MazeVisualSetup");
            }

            // Mark scene as dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            UnityEngine.Debug.Log("[FaeMazeSceneSetup] FaeMazeScene setup complete! The maze should now be visible when you play the scene.");
        }
    }
}

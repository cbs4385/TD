using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add WaveManager to FaeMazeScene
    /// </summary>
    public class AddWaveManagerToScene
    {
        [MenuItem("FaeMaze/Add WaveManager to FaeMazeScene")]
        public static void AddWaveManager()
        {
            // Load the FaeMaze scene
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/FaeMazeScene.unity");

            // Check if WaveManager already exists
            Systems.WaveManager existingManager = Object.FindFirstObjectByType<Systems.WaveManager>();
            if (existingManager != null)
            {
                Debug.LogWarning("[AddWaveManagerToScene] WaveManager already exists in the scene!");
                EditorUtility.DisplayDialog("Wave Manager", "WaveManager already exists in the scene!", "OK");
                return;
            }

            // Find or create GameRoot
            GameObject gameRoot = GameObject.Find("GameRoot");
            if (gameRoot == null)
            {
                // Try to find any root object with systems
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject obj in rootObjects)
                {
                    if (obj.name.Contains("System") || obj.name.Contains("Manager") || obj.name.Contains("Root"))
                    {
                        gameRoot = obj;
                        break;
                    }
                }
            }

            // Create WaveManager GameObject
            GameObject waveManagerObj = new GameObject("WaveManager");

            // Add it as a child of GameRoot if found, otherwise at root level
            if (gameRoot != null)
            {
                waveManagerObj.transform.SetParent(gameRoot.transform);
            }

            // Add WaveManager component
            Systems.WaveManager waveManager = waveManagerObj.AddComponent<Systems.WaveManager>();

            // Try to find and assign WaveSpawner reference
            Systems.WaveSpawner waveSpawner = Object.FindFirstObjectByType<Systems.WaveSpawner>();
            if (waveSpawner != null)
            {
                SerializedObject so = new SerializedObject(waveManager);
                SerializedProperty waveSpawnerProp = so.FindProperty("waveSpawner");
                if (waveSpawnerProp != null)
                {
                    waveSpawnerProp.objectReferenceValue = waveSpawner;
                    so.ApplyModifiedProperties();
                    Debug.Log("[AddWaveManagerToScene] WaveSpawner reference assigned");
                }
            }

            // Try to find and assign GameController reference
            Systems.GameController gameController = Systems.GameController.Instance;
            if (gameController != null)
            {
                SerializedObject so = new SerializedObject(waveManager);
                SerializedProperty gameControllerProp = so.FindProperty("gameController");
                if (gameControllerProp != null)
                {
                    gameControllerProp.objectReferenceValue = gameController;
                    so.ApplyModifiedProperties();
                    Debug.Log("[AddWaveManagerToScene] GameController reference assigned");
                }
            }

            // Mark scene as dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[AddWaveManagerToScene] Successfully added WaveManager to FaeMazeScene!");
            EditorUtility.DisplayDialog("Success", "WaveManager has been added to FaeMazeScene!\n\nPlease configure the following in the Inspector:\n- Wave progression settings\n- UI panel references\n- Button references", "OK");

            // Select the new WaveManager in the hierarchy
            Selection.activeGameObject = waveManagerObj;
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using FaeMaze.Systems;

namespace FaeMaze.Editor
{
    public class PlanarForestMazeSceneSetup : MonoBehaviour
    {
        [MenuItem("FaeMaze/Setup PlanarForest Maze Scene")]
        public static void SetupPlanarForestMazeScene()
        {
            // Load the PlanarForestMazeScene
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/PlanarForestMazeScene.unity");

            // Find or create MazeOrigin
            GameObject mazeOriginObj = GameObject.Find("MazeOrigin");
            if (mazeOriginObj == null)
            {
                mazeOriginObj = new GameObject("MazeOrigin");
                mazeOriginObj.transform.position = Vector3.zero;
            }

            // Find or create MazeRoot (parent for maze grid)
            GameObject mazeRootObj = GameObject.Find("MazeRoot");
            if (mazeRootObj == null)
            {
                mazeRootObj = new GameObject("MazeRoot");
                mazeRootObj.transform.position = Vector3.zero;
            }

            // Add MazeGridBehaviour to MazeRoot if not present
            MazeGridBehaviour mazeGrid = mazeRootObj.GetComponent<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                mazeGrid = mazeRootObj.AddComponent<MazeGridBehaviour>();
            }

            // Configure MazeGridBehaviour to use runtime generation with planar generator
            SerializedObject mazeGridSO = new SerializedObject(mazeGrid);
            mazeGridSO.FindProperty("useRuntimeGeneration").boolValue = true;
            mazeGridSO.FindProperty("usePlanarGenerator").boolValue = true; // New property
            mazeGridSO.FindProperty("mazeOrigin").objectReferenceValue = mazeOriginObj.transform;

            // Configure planar generator settings
            SerializedProperty configProp = mazeGridSO.FindProperty("planarGeneratorConfig");
            if (configProp != null)
            {
                configProp.FindPropertyRelative("gridWidth").intValue = 60;
                configProp.FindPropertyRelative("gridHeight").intValue = 60;
                configProp.FindPropertyRelative("growthTurns").intValue = 25;
                configProp.FindPropertyRelative("randomSeed").intValue = 0;
            }

            mazeGridSO.ApplyModifiedProperties();

            // Add MazeRenderer if not present
            if (mazeRootObj.GetComponent<MazeRenderer>() == null)
            {
                mazeRootObj.AddComponent<MazeRenderer>();
            }

            // Add MazeVisualSetup if not present
            if (mazeRootObj.GetComponent<MazeVisualSetup>() == null)
            {
                mazeRootObj.AddComponent<MazeVisualSetup>();
            }

            // Add DynamicMazeGrowth if not present
            DynamicMazeGrowth dynamicGrowth = mazeRootObj.GetComponent<DynamicMazeGrowth>();
            if (dynamicGrowth == null)
            {
                dynamicGrowth = mazeRootObj.AddComponent<DynamicMazeGrowth>();
                Debug.Log("[PlanarForestMazeSceneSetup] Added DynamicMazeGrowth component to MazeRoot");
            }

            // Always configure portal prefab (in case it was previously missing)
            GameObject portalPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tile/portal.prefab");
            if (portalPrefab != null)
            {
                SerializedObject growthSO = new SerializedObject(dynamicGrowth);
                growthSO.FindProperty("portalPrefab").objectReferenceValue = portalPrefab;
                growthSO.FindProperty("growthInterval").floatValue = 30f;
                growthSO.FindProperty("autoGrowth").boolValue = true;
                growthSO.ApplyModifiedProperties();
                Debug.Log("[PlanarForestMazeSceneSetup] Assigned portal prefab to DynamicMazeGrowth");
            }
            else
            {
                Debug.LogError("[PlanarForestMazeSceneSetup] Failed to load portal prefab from Assets/Prefabs/Tile/portal.prefab");
            }

            // Mark scene as dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

        }
    }
}

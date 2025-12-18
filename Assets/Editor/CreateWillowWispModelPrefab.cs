using UnityEngine;
using UnityEditor;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to create a model prefab from the willowthewisp.fbx file
    /// </summary>
    public class CreateWillowWispModelPrefab
    {
        [MenuItem("FaeMaze/Create Willow Wisp Model Prefab")]
        public static void CreateModelPrefab()
        {
            // Load the FBX asset
            string fbxPath = "Assets/Animations/willowthewisp.fbx";
            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

            if (fbxAsset == null)
            {
                Debug.LogError($"Failed to load FBX at path: {fbxPath}");
                return;
            }

            // Create an instance
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            if (instance == null)
            {
                Debug.LogError("Failed to instantiate FBX asset");
                return;
            }

            // Set up the prefab save path
            string prefabPath = "Assets/Prefabs/Props/WillowWispModel.prefab";

            // Save as a prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            // Clean up the instance
            Object.DestroyImmediate(instance);

            if (prefab != null)
            {
                Debug.Log($"Successfully created Willow Wisp model prefab at: {prefabPath}");
                Debug.Log("Now assign this prefab to the 'Wisp Model Prefab' field in the WillowTheWisp prefab");

                // Select the created prefab
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            else
            {
                Debug.LogError("Failed to save prefab");
            }
        }
    }
}

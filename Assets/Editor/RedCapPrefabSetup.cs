using UnityEngine;
using UnityEditor;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add RedCapController component to the redcap prefab.
    /// Run from Unity menu: FaeMaze > Setup RedCap Prefab
    /// </summary>
    public class RedCapPrefabSetup : UnityEditor.Editor
    {
        [MenuItem("FaeMaze/Setup RedCap Prefab")]
        public static void SetupRedCapPrefab()
        {
            string prefabPath = "Assets/Prefabs/redcap.prefab";

            // Load the prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RedCapPrefabSetup] Could not find prefab at {prefabPath}");
                return;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            if (prefabRoot == null)
            {
                Debug.LogError("[RedCapPrefabSetup] Could not load prefab contents");
                return;
            }

            try
            {
                // Check if RedCapController already exists
                var existingController = prefabRoot.GetComponent<FaeMaze.Visitors.RedCapController>();
                if (existingController != null)
                {
                    Debug.Log("[RedCapPrefabSetup] RedCapController already exists on prefab");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                // Add RedCapController component
                var controller = prefabRoot.AddComponent<FaeMaze.Visitors.RedCapController>();

                if (controller == null)
                {
                    Debug.LogError("[RedCapPrefabSetup] Failed to add RedCapController component");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                // Save the prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                Debug.Log($"[RedCapPrefabSetup] Successfully added RedCapController to {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // Refresh asset database
            AssetDatabase.Refresh();
        }
    }
}

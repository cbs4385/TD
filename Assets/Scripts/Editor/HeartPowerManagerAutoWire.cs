using UnityEngine;
using UnityEditor;
using FaeMaze.HeartPowers;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to automatically wire HeartPowerDefinition assets to HeartPowerManager.
    /// </summary>
    public class HeartPowerManagerAutoWire
    {
        [MenuItem("FaeMaze/Heart Powers/Auto-Wire Power Definitions to Manager")]
        public static void AutoWirePowerDefinitions()
        {
            // Find HeartPowerManager in the scene
            HeartPowerManager manager = Object.FindFirstObjectByType<HeartPowerManager>();
            if (manager == null)
            {
                Debug.LogError("[HeartPowerManagerAutoWire] No HeartPowerManager found in the scene!");
                return;
            }

            // Load all HeartPowerDefinition assets
            string[] guids = AssetDatabase.FindAssets("t:HeartPowerDefinition");
            if (guids.Length == 0)
            {
                Debug.LogError("[HeartPowerManagerAutoWire] No HeartPowerDefinition assets found! Run 'FaeMaze > Heart Powers > Generate Power Definitions' first.");
                return;
            }

            HeartPowerDefinition[] definitions = new HeartPowerDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                definitions[i] = AssetDatabase.LoadAssetAtPath<HeartPowerDefinition>(path);
                Debug.Log($"[HeartPowerManagerAutoWire] Loaded: {definitions[i].displayName} ({definitions[i].powerType})");
            }

            // Use SerializedObject to modify the manager
            SerializedObject so = new SerializedObject(manager);
            SerializedProperty powerDefinitionsProp = so.FindProperty("powerDefinitions");

            if (powerDefinitionsProp != null)
            {
                powerDefinitionsProp.arraySize = definitions.Length;

                for (int i = 0; i < definitions.Length; i++)
                {
                    SerializedProperty element = powerDefinitionsProp.GetArrayElementAtIndex(i);
                    element.objectReferenceValue = definitions[i];
                }

                so.ApplyModifiedProperties();

                EditorUtility.SetDirty(manager);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

                Debug.Log($"[HeartPowerManagerAutoWire] ✓ Successfully wired {definitions.Length} power definitions to HeartPowerManager");
                Debug.Log($"[HeartPowerManagerAutoWire] Don't forget to save the scene!");
            }
            else
            {
                Debug.LogError("[HeartPowerManagerAutoWire] Could not find 'powerDefinitions' field on HeartPowerManager!");
            }
        }
    }
}

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
            bool createdTemporary = false;

            if (manager == null)
            {

                // Create a temporary HeartPowerManager GameObject
                GameObject managerObj = new GameObject("HeartPowerManager");
                manager = managerObj.AddComponent<HeartPowerManager>();
                createdTemporary = true;
            }

            // Load all HeartPowerDefinition assets
            string[] guids = AssetDatabase.FindAssets("t:HeartPowerDefinition");
            if (guids.Length == 0)
            {
                return;
            }

            HeartPowerDefinition[] definitions = new HeartPowerDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                definitions[i] = AssetDatabase.LoadAssetAtPath<HeartPowerDefinition>(path);
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

                if (!createdTemporary)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }


                if (createdTemporary)
                {
                }
                else
                {
                }
            }
            else
            {
            }
        }
    }
}

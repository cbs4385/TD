using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add the Misdirect (Power 5) binding row to the Options scene.
    /// Duplicates the Sculpting (Power 4) row and updates names/labels.
    /// Run from: FaeMaze > Setup Misdirect Options Row
    /// </summary>
    public class OptionsMisdirectRowSetup
    {
        [MenuItem("FaeMaze/Setup Misdirect Options Row")]
        public static void SetupMisdirectRow()
        {
            // Find the Power 4 row
            var power4Row = GameObject.Find("Sculpting(Power4)_Row");
            if (power4Row == null)
            {
                Debug.LogError("[OptionsMisdirectRowSetup] Could not find 'Sculpting(Power4)_Row' in scene. Make sure you're in the Options scene.");
                return;
            }

            // Check if Power 5 row already exists
            var existingRow = GameObject.Find("Misdirect(Power5)_Row");
            if (existingRow != null)
            {
                Debug.LogWarning("[OptionsMisdirectRowSetup] 'Misdirect(Power5)_Row' already exists. Delete it first if you want to recreate it.");
                return;
            }

            // Duplicate the Power 4 row
            var power5Row = Object.Instantiate(power4Row, power4Row.transform.parent);
            power5Row.name = "Misdirect(Power5)_Row";

            // Place it after Power 4 row in the hierarchy
            int power4SiblingIndex = power4Row.transform.GetSiblingIndex();
            power5Row.transform.SetSiblingIndex(power4SiblingIndex + 1);

            // Update the label text
            var labels = power5Row.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                if (label.text.Contains("Redecorating") || label.text.Contains("Power 4"))
                {
                    label.text = "Misdirect (Power 5)";
                    Debug.Log($"[OptionsMisdirectRowSetup] Updated label text to 'Misdirect (Power 5)'");
                }
            }

            // Rename the KeyBindingCapture button GameObjects
            // Power 4 buttons are named: HeartPower4_Button, HeartPower4Alt_Button, HeartPower4Tertiary_Button
            RenameChildRecursive(power5Row.transform, "HeartPower4_Button", "HeartPower5_Button");
            RenameChildRecursive(power5Row.transform, "HeartPower4Alt_Button", "HeartPower5Alt_Button");
            RenameChildRecursive(power5Row.transform, "HeartPower4Tertiary_Button", "HeartPower5Tertiary_Button");

            // Also rename any cloned objects that have "(Clone)" suffix
            CleanCloneSuffixes(power5Row.transform);

            // Mark scene dirty so changes can be saved
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("[OptionsMisdirectRowSetup] Successfully created Misdirect (Power 5) row in Options scene. Save the scene to persist changes.");

            // Select the new row in hierarchy
            Selection.activeGameObject = power5Row;
        }

        private static void RenameChildRecursive(Transform parent, string oldName, string newName)
        {
            foreach (Transform child in parent)
            {
                // Check for exact match or match with (Clone) suffix
                if (child.gameObject.name == oldName ||
                    child.gameObject.name == oldName + "(Clone)")
                {
                    child.gameObject.name = newName;
                    Debug.Log($"[OptionsMisdirectRowSetup] Renamed '{oldName}' to '{newName}'");
                }
                RenameChildRecursive(child, oldName, newName);
            }
        }

        private static void CleanCloneSuffixes(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.gameObject.name.EndsWith("(Clone)"))
                {
                    child.gameObject.name = child.gameObject.name.Replace("(Clone)", "");
                }
                CleanCloneSuffixes(child);
            }
        }
    }
}

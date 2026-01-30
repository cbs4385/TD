using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to remove obsolete UI elements from the Options scene.
    /// </summary>
    public static class OptionsSceneCleanup
    {
        [MenuItem("FaeMaze/Remove Obsolete Auto Start Next Wave Toggle")]
        public static void RemoveAutoStartNextWaveToggle()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsSceneCleanup: Canvas not found in scene. Open the Options scene first.");
                return;
            }

            // Find the toggle by name or by looking for "Auto Start Next Wave" label
            var allToggles = canvas.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in allToggles)
            {
                // Check if this toggle's parent row contains "Auto Start Next Wave" text
                var parentTexts = toggle.transform.parent.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in parentTexts)
                {
                    if (text.text.Contains("Auto Start Next Wave") || text.text.Contains("AutoStartNextWave"))
                    {
                        // Found it - delete the entire row (parent of toggle)
                        GameObject rowToDelete = toggle.transform.parent.gameObject;
                        Debug.Log($"OptionsSceneCleanup: Removing row '{rowToDelete.name}' containing Auto Start Next Wave toggle.");
                        Undo.DestroyObjectImmediate(rowToDelete);

                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                        Debug.Log("OptionsSceneCleanup: Removed Auto Start Next Wave toggle. Save the scene!");
                        return;
                    }
                }

                // Also check by toggle name
                if (toggle.gameObject.name.ToLower().Contains("autostartwave") ||
                    toggle.gameObject.name.ToLower().Contains("autostartnextwave"))
                {
                    GameObject rowToDelete = toggle.transform.parent.gameObject;
                    Debug.Log($"OptionsSceneCleanup: Removing row '{rowToDelete.name}' containing Auto Start Next Wave toggle.");
                    Undo.DestroyObjectImmediate(rowToDelete);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                    Debug.Log("OptionsSceneCleanup: Removed Auto Start Next Wave toggle. Save the scene!");
                    return;
                }
            }

            Debug.LogWarning("OptionsSceneCleanup: Auto Start Next Wave toggle not found in scene.");
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to remove the obsolete Wave section from the Options scene.
    /// Run from menu: Tools > FaeMaze > Remove Wave Section
    /// </summary>
    public class RemoveWaveSection
    {
        [MenuItem("Tools/FaeMaze/Remove Wave Section")]
        public static void RemoveWaveSectionFromOptionsScene()
        {
            // Make sure we're in the Options scene
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Options")
            {
                EditorUtility.DisplayDialog("Wrong Scene",
                    "Please open the Options scene first, then run this tool.",
                    "OK");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found in scene!");
                return;
            }

            // Search for WaveSection anywhere in the hierarchy
            var waveSection = FindChildByName(canvas.transform, "WaveSection");
            if (waveSection == null)
            {
                Debug.Log("WaveSection not found - it may have already been removed.");
                EditorUtility.DisplayDialog("Not Found",
                    "WaveSection was not found in the scene. It may have already been removed.",
                    "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(canvas, "Remove Wave Section");

            // Also find and remove individual wave-related rows if they exist outside WaveSection
            var visitorsPerWaveRow = FindChildByName(canvas.transform, "Visitors Per WaveRow");
            var waveDurationRow = FindChildByName(canvas.transform, "Wave Duration (s)Row");

            int removedCount = 0;

            if (waveSection != null)
            {
                Debug.Log($"Removing WaveSection: {waveSection.name}");
                Undo.DestroyObjectImmediate(waveSection.gameObject);
                removedCount++;
            }

            if (visitorsPerWaveRow != null)
            {
                Debug.Log($"Removing Visitors Per Wave row: {visitorsPerWaveRow.name}");
                Undo.DestroyObjectImmediate(visitorsPerWaveRow.gameObject);
                removedCount++;
            }

            if (waveDurationRow != null)
            {
                Debug.Log($"Removing Wave Duration row: {waveDurationRow.name}");
                Undo.DestroyObjectImmediate(waveDurationRow.gameObject);
                removedCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"Wave section removed successfully! Removed {removedCount} object(s). Save the scene to preserve changes.");
            EditorUtility.DisplayDialog("Success",
                $"Wave section removed successfully!\nRemoved {removedCount} object(s).\n\nDon't forget to save the scene.",
                "OK");
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

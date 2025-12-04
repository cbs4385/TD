using UnityEngine;
using UnityEditor;
using System.IO;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Utility to update Build Settings after GameOver scene is created
    /// </summary>
    public class BuildSettingsUpdater
    {
        [MenuItem("FaeMaze/Update Build Settings GUIDs")]
        public static void UpdateBuildSettingsGUIDs()
        {
            string gameOverMetaPath = "Assets/Scenes/GameOver.unity.meta";

            if (!File.Exists(gameOverMetaPath))
            {
                Debug.LogError("[BuildSettingsUpdater] GameOver.unity.meta not found! Generate the scene first using 'FaeMaze/Setup GameOver Scene'");
                return;
            }

            // Read the .meta file to get the GUID
            string[] metaLines = File.ReadAllLines(gameOverMetaPath);
            string gameOverGuid = null;

            foreach (string line in metaLines)
            {
                if (line.StartsWith("guid:"))
                {
                    gameOverGuid = line.Split(':')[1].Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(gameOverGuid))
            {
                Debug.LogError("[BuildSettingsUpdater] Could not find GUID in GameOver.unity.meta");
                return;
            }

            Debug.Log($"[BuildSettingsUpdater] Found GameOver scene GUID: {gameOverGuid}");

            // Read EditorBuildSettings.asset
            string buildSettingsPath = "ProjectSettings/EditorBuildSettings.asset";
            string[] buildSettingsLines = File.ReadAllLines(buildSettingsPath);

            // Find and update the GameOver GUID
            bool updated = false;
            for (int i = 0; i < buildSettingsLines.Length; i++)
            {
                if (buildSettingsLines[i].Contains("Assets/Scenes/GameOver.unity"))
                {
                    // Next line should be the GUID
                    if (i + 1 < buildSettingsLines.Length && buildSettingsLines[i + 1].Contains("guid:"))
                    {
                        string oldGuid = buildSettingsLines[i + 1].Split(':')[1].Trim();
                        buildSettingsLines[i + 1] = buildSettingsLines[i + 1].Replace(oldGuid, gameOverGuid);
                        updated = true;
                        Debug.Log($"[BuildSettingsUpdater] Updated GUID from {oldGuid} to {gameOverGuid}");
                        break;
                    }
                }
            }

            if (updated)
            {
                File.WriteAllLines(buildSettingsPath, buildSettingsLines);
                AssetDatabase.Refresh();
                Debug.Log("[BuildSettingsUpdater] Build Settings updated successfully!");
            }
            else
            {
                Debug.LogWarning("[BuildSettingsUpdater] GameOver scene not found in Build Settings");
            }
        }
    }
}

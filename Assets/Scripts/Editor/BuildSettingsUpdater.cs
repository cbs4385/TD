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
                return;
            }


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
                        break;
                    }
                }
            }

            if (updated)
            {
                File.WriteAllLines(buildSettingsPath, buildSettingsLines);
                AssetDatabase.Refresh();
            }
        }
    }
}

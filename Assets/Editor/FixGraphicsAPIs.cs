using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Linq;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Fixes the graphics API settings for Windows builds to include Direct3D 11 and Direct3D 12.
    /// Run from menu: FaeMaze > Fix Graphics APIs
    /// </summary>
    public static class FixGraphicsAPIs
    {
        [MenuItem("FaeMaze/Fix Graphics APIs", false, 200)]
        public static void FixWindowsGraphicsAPIs()
        {
            // Get current graphics APIs for Windows Standalone
            var currentAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);

            Debug.Log($"Current Graphics APIs: {string.Join(", ", currentAPIs.Select(a => a.ToString()))}");

            // Set the desired graphics APIs: D3D11 first (most compatible), then D3D12, then Vulkan as fallback
            var desiredAPIs = new GraphicsDeviceType[]
            {
                GraphicsDeviceType.Direct3D11,
                GraphicsDeviceType.Direct3D12,
                GraphicsDeviceType.Vulkan
            };

            // Disable auto graphics API selection first
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);

            // Set the graphics APIs
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, desiredAPIs);

            // Verify the change
            var newAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            Debug.Log($"New Graphics APIs: {string.Join(", ", newAPIs.Select(a => a.ToString()))}");

            // Save the project settings
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Graphics APIs Fixed",
                $"Windows graphics APIs have been set to:\n" +
                $"1. Direct3D 11 (primary)\n" +
                $"2. Direct3D 12\n" +
                $"3. Vulkan (fallback)\n\n" +
                $"Please restart Unity for the changes to take full effect, then try building again.",
                "OK");
        }

        [MenuItem("FaeMaze/Show Current Graphics APIs", false, 201)]
        public static void ShowCurrentGraphicsAPIs()
        {
            var windowsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var macAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX);

            var windowsAuto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var macAuto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX);

            string message = $"Windows (Auto: {windowsAuto}):\n";
            foreach (var api in windowsAPIs)
            {
                message += $"  - {api}\n";
            }

            message += $"\nmacOS (Auto: {macAuto}):\n";
            foreach (var api in macAPIs)
            {
                message += $"  - {api}\n";
            }

            Debug.Log(message);
            EditorUtility.DisplayDialog("Current Graphics APIs", message, "OK");
        }
    }
}

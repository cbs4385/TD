using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Linq;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Fixes the graphics API settings for Windows and macOS builds.
    /// Run from menu: FaeMaze > Fix Graphics APIs
    /// </summary>
    public static class FixGraphicsAPIs
    {
        [MenuItem("FaeMaze/Fix Graphics APIs", false, 200)]
        public static void FixAllGraphicsAPIs()
        {
            // === Windows Configuration ===
            var windowsAPIs = new GraphicsDeviceType[]
            {
                GraphicsDeviceType.Direct3D11,
                GraphicsDeviceType.Direct3D12,
                GraphicsDeviceType.Vulkan
            };

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, windowsAPIs);

            var newWindowsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            Debug.Log($"Windows Graphics APIs: {string.Join(", ", newWindowsAPIs.Select(a => a.ToString()))}");

            // === macOS Configuration ===
            var macAPIs = new GraphicsDeviceType[]
            {
                GraphicsDeviceType.Metal
            };

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, macAPIs);

            var newMacAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX);
            Debug.Log($"macOS Graphics APIs: {string.Join(", ", newMacAPIs.Select(a => a.ToString()))}");

            // Save the project settings
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Graphics APIs Fixed",
                "Windows graphics APIs:\n" +
                "  1. Direct3D 11 (primary)\n" +
                "  2. Direct3D 12\n" +
                "  3. Vulkan (fallback)\n\n" +
                "macOS graphics APIs:\n" +
                "  1. Metal\n\n" +
                "You can now try building again.",
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

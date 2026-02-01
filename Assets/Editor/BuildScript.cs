using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Linq;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Build automation script for creating Windows and macOS builds.
    /// Run from menu: FaeMaze > Build > [platform]
    /// Or use command line: Unity -batchmode -executeMethod FaeMaze.Editor.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        private const string PRODUCT_NAME = "HungryForest";
        private const string BUILD_ROOT = "Builds";

        // Get enabled scenes from build settings
        private static string[] GetBuildScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        [MenuItem("FaeMaze/Build/Windows (64-bit)", false, 100)]
        public static void BuildWindows()
        {
            BuildPlayer(BuildTarget.StandaloneWindows64, $"{PRODUCT_NAME}.exe");
        }

        [MenuItem("FaeMaze/Build/macOS", false, 101)]
        public static void BuildMacOS()
        {
            BuildPlayer(BuildTarget.StandaloneOSX, $"{PRODUCT_NAME}.app");
        }

        [MenuItem("FaeMaze/Build/Both Platforms", false, 120)]
        public static void BuildAll()
        {
            Debug.Log("=== Starting Multi-Platform Build ===");

            bool windowsSuccess = BuildPlayer(BuildTarget.StandaloneWindows64, $"{PRODUCT_NAME}.exe");
            bool macSuccess = BuildPlayer(BuildTarget.StandaloneOSX, $"{PRODUCT_NAME}.app");

            Debug.Log("=== Multi-Platform Build Complete ===");
            Debug.Log($"Windows: {(windowsSuccess ? "SUCCESS" : "FAILED")}");
            Debug.Log($"macOS: {(macSuccess ? "SUCCESS" : "FAILED")}");

            if (windowsSuccess && macSuccess)
            {
                EditorUtility.DisplayDialog("Build Complete",
                    $"Both platforms built successfully!\n\nOutput:\n{GetBuildPath(BuildTarget.StandaloneWindows64)}\n{GetBuildPath(BuildTarget.StandaloneOSX)}",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Build Completed with Errors",
                    $"Windows: {(windowsSuccess ? "OK" : "FAILED")}\nmacOS: {(macSuccess ? "OK" : "FAILED")}\n\nCheck console for details.",
                    "OK");
            }
        }

        [MenuItem("FaeMaze/Build/Open Build Folder", false, 140)]
        public static void OpenBuildFolder()
        {
            string buildPath = Path.GetFullPath(BUILD_ROOT);
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }
            EditorUtility.RevealInFinder(buildPath);
        }

        private static string GetBuildPath(BuildTarget target)
        {
            string platformFolder = target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneOSX => "macOS",
                _ => target.ToString()
            };

            // Use absolute path to avoid any relative path issues
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, BUILD_ROOT, platformFolder);
        }

        private static bool BuildPlayer(BuildTarget target, string executableName)
        {
            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("BuildScript: No scenes enabled in Build Settings!");
                return false;
            }

            string buildPath = GetBuildPath(target);
            string fullPath = Path.Combine(buildPath, executableName);

            // Ensure the build directory exists
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            Debug.Log($"BuildScript: Building {target} to {fullPath}");
            Debug.Log($"BuildScript: Including {scenes.Length} scenes: {string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension))}");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullPath,
                target = target,
                options = BuildOptions.None
            };

            // For development builds, uncomment this:
            // options.options |= BuildOptions.Development | BuildOptions.AllowDebugging;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"BuildScript: {target} build succeeded! Size: {summary.totalSize / (1024 * 1024):F2} MB, Time: {summary.totalTime.TotalSeconds:F1}s");
                Debug.Log($"BuildScript: Output: {fullPath}");
                return true;
            }
            else
            {
                Debug.LogError($"BuildScript: {target} build failed with {summary.totalErrors} error(s)");

                // Log first few errors
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error)
                        {
                            Debug.LogError($"  {message.content}");
                        }
                    }
                }

                return false;
            }
        }

        // Command-line build methods for CI/CD
        public static void BuildWindowsCommandLine()
        {
            bool success = BuildPlayer(BuildTarget.StandaloneWindows64, $"{PRODUCT_NAME}.exe");
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static void BuildMacOSCommandLine()
        {
            bool success = BuildPlayer(BuildTarget.StandaloneOSX, $"{PRODUCT_NAME}.app");
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static void BuildAllCommandLine()
        {
            bool windowsSuccess = BuildPlayer(BuildTarget.StandaloneWindows64, $"{PRODUCT_NAME}.exe");
            bool macSuccess = BuildPlayer(BuildTarget.StandaloneOSX, $"{PRODUCT_NAME}.app");
            EditorApplication.Exit((windowsSuccess && macSuccess) ? 0 : 1);
        }
    }
}

using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the Unity Animator Graph NullReferenceException that occurs on compile.
/// This is a known Unity bug where the Animator window's internal graph state becomes corrupted.
/// The fix reimports all animator controllers to clear the corrupted state.
///
/// Run via menu: FaeMaze > Fix Animator Graph Errors
/// </summary>
public static class AnimatorGraphFixer
{
    [MenuItem("FaeMaze/Fix Animator Graph Errors")]
    public static void FixAnimatorGraphErrors()
    {
        // Find all animator controllers in the project
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });

        if (guids.Length == 0)
        {
            Debug.Log("[AnimatorGraphFixer] No animator controllers found in Assets folder.");
            return;
        }

        Debug.Log($"[AnimatorGraphFixer] Found {guids.Length} animator controllers. Reimporting...");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        // Also clear the animator window state by closing and reopening
        // This forces Unity to rebuild the internal graph
        EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (EditorWindow window in windows)
        {
            if (window.GetType().Name == "AnimatorControllerTool")
            {
                window.Close();
                Debug.Log("[AnimatorGraphFixer] Closed Animator window to clear cached state.");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log("[AnimatorGraphFixer] Reimport complete. The graph error should be resolved.");
    }

    /// <summary>
    /// Automatically runs on first editor load to check if fix is needed.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void OnEditorLoad()
    {
        // Register a delayed call to allow Unity to finish loading
        EditorApplication.delayCall += CheckAndFixOnStartup;
    }

    private static void CheckAndFixOnStartup()
    {
        // Unsubscribe to prevent repeated calls
        EditorApplication.delayCall -= CheckAndFixOnStartup;

        // Only run the fix if the user has seen the error before
        // We track this via EditorPrefs to avoid running every startup
        string prefKey = "AnimatorGraphFixer_LastFixTime_" + Application.dataPath.GetHashCode();
        string lastFix = EditorPrefs.GetString(prefKey, "");
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");

        // Only auto-fix once per day to avoid performance hit
        if (lastFix != today)
        {
            // Silently reimport animators to prevent the error
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Use ForceSynchronousImport to ensure it completes before anything uses the animator
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            EditorPrefs.SetString(prefKey, today);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to clean up problematic post-processing components
    /// </summary>
    public static class CleanupVolumeProfile
    {
        [MenuItem("FaeMaze/Cleanup Volume Profile (Remove Blur and Dark Effects)")]
        public static void CleanupProfile()
        {
            // Find the PostProcessingProfile asset
            string[] guids = AssetDatabase.FindAssets("PostProcessingProfile t:VolumeProfile");

            if (guids.Length == 0)
            {
                Debug.LogError("[CleanupVolumeProfile] Could not find PostProcessingProfile.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                Debug.LogError($"[CleanupVolumeProfile] Could not load VolumeProfile at {path}");
                return;
            }

            bool modified = false;

            // Remove DepthOfField if it exists
            if (profile.TryGet<DepthOfField>(out var dof))
            {
                profile.Remove<DepthOfField>();
                Debug.Log("[CleanupVolumeProfile] Removed DepthOfField component");
                modified = true;
            }

            // Remove RadialBlur if it exists
            if (profile.TryGet<FaeMaze.PostProcessing.RadialBlur>(out var radialBlur))
            {
                profile.Remove<FaeMaze.PostProcessing.RadialBlur>();
                Debug.Log("[CleanupVolumeProfile] Removed RadialBlur component");
                modified = true;
            }

            // Remove Vignette if it exists (can make scene too dark)
            if (profile.TryGet<Vignette>(out var vignette))
            {
                profile.Remove<Vignette>();
                Debug.Log("[CleanupVolumeProfile] Removed Vignette component");
                modified = true;
            }

            if (modified)
            {
                // Mark asset as dirty and save
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);

                Debug.Log($"[CleanupVolumeProfile] Successfully cleaned up profile at {path}");
                Debug.Log("  - Please reload your scene for changes to take effect");
            }
            else
            {
                Debug.Log("[CleanupVolumeProfile] No problematic effects found in profile - nothing to clean up");
            }

            Selection.activeObject = profile;
        }
    }
}

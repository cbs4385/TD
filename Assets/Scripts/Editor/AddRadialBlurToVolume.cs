using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using FaeMaze.PostProcessing;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add RadialBlur component to the PostProcessing VolumeProfile
    /// </summary>
    public static class AddRadialBlurToVolume
    {
        [MenuItem("FaeMaze/Add RadialBlur to Volume Profile")]
        public static void AddRadialBlurComponent()
        {
            // Find the PostProcessingProfile asset
            string[] guids = AssetDatabase.FindAssets("PostProcessingProfile t:VolumeProfile");

            if (guids.Length == 0)
            {
                Debug.LogError("[AddRadialBlurToVolume] Could not find PostProcessingProfile. Please create a VolumeProfile asset first.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                Debug.LogError($"[AddRadialBlurToVolume] Could not load VolumeProfile at {path}");
                return;
            }

            // Check if RadialBlur already exists
            if (profile.TryGet<RadialBlur>(out var existingBlur))
            {
                Debug.LogWarning($"[AddRadialBlurToVolume] RadialBlur already exists in profile at {path}");
                Debug.Log($"  - enabled: {existingBlur.enabled.value}");
                Debug.Log($"  - clearRadiusPercent: {existingBlur.clearRadiusPercent.value}%");
                Debug.Log($"  - blurIntensity: {existingBlur.blurIntensity.value}");
                Selection.activeObject = profile;
                return;
            }

            // Add RadialBlur component
            var radialBlur = profile.Add<RadialBlur>(overrides: true);

            // Configure with good default values for vignette effect
            radialBlur.enabled.value = true;
            radialBlur.enabled.overrideState = true;

            radialBlur.clearRadiusPercent.value = 85f;  // Center 85% is clear, outer 15% is blurred
            radialBlur.clearRadiusPercent.overrideState = true;

            radialBlur.blurIntensity.value = 0.8f;  // Strong blur
            radialBlur.blurIntensity.overrideState = true;

            radialBlur.blurSamples.value = 12;  // Good quality
            radialBlur.blurSamples.overrideState = true;

            // Mark asset as dirty and save
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            Debug.Log($"[AddRadialBlurToVolume] Successfully added RadialBlur to profile at {path}");
            Debug.Log("  - enabled: true");
            Debug.Log("  - clearRadiusPercent: 85 (center 85% is clear, outer 15% is blurred)");
            Debug.Log("  - blurIntensity: 0.8");
            Debug.Log("  - blurSamples: 12");

            Selection.activeObject = profile;
        }

        [MenuItem("FaeMaze/Remove RadialBlur from Volume Profile")]
        public static void RemoveRadialBlurComponent()
        {
            // Find the PostProcessingProfile asset
            string[] guids = AssetDatabase.FindAssets("PostProcessingProfile t:VolumeProfile");

            if (guids.Length == 0)
            {
                Debug.LogError("[RemoveRadialBlurFromVolume] Could not find PostProcessingProfile.");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                Debug.LogError($"[RemoveRadialBlurFromVolume] Could not load VolumeProfile at {path}");
                return;
            }

            // Check if RadialBlur exists
            if (!profile.TryGet<RadialBlur>(out var radialBlur))
            {
                Debug.LogWarning($"[RemoveRadialBlurFromVolume] RadialBlur not found in profile at {path}");
                return;
            }

            // Remove RadialBlur component
            profile.Remove<RadialBlur>();

            // Mark asset as dirty and save
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

            Debug.Log($"[RemoveRadialBlurFromVolume] Successfully removed RadialBlur from profile at {path}");
        }
    }
}

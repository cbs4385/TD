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
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                return;
            }

            // Check if RadialBlur already exists
            if (profile.TryGet<RadialBlur>(out var existingBlur))
            {
                Selection.activeObject = profile;
                return;
            }

            // Add RadialBlur component
            var radialBlur = profile.Add<RadialBlur>(overrides: true);

            // Configure with good default values
            radialBlur.enabled.value = true;
            radialBlur.enabled.overrideState = true;

            radialBlur.blurAngleDegrees.value = 10f;  // 10% of screen from center is clear
            radialBlur.blurAngleDegrees.overrideState = true;

            radialBlur.blurIntensity.value = 0.8f;  // Strong blur
            radialBlur.blurIntensity.overrideState = true;

            radialBlur.blurSamples.value = 12;  // Good quality
            radialBlur.blurSamples.overrideState = true;

            // Mark asset as dirty and save
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);


            Selection.activeObject = profile;
        }

        [MenuItem("FaeMaze/Remove RadialBlur from Volume Profile")]
        public static void RemoveRadialBlurComponent()
        {
            // Find the PostProcessingProfile asset
            string[] guids = AssetDatabase.FindAssets("PostProcessingProfile t:VolumeProfile");

            if (guids.Length == 0)
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);

            if (profile == null)
            {
                return;
            }

            // Check if RadialBlur exists
            if (!profile.TryGet<RadialBlur>(out var radialBlur))
            {
                return;
            }

            // Remove RadialBlur component
            profile.Remove<RadialBlur>();

            // Mark asset as dirty and save
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);

        }
    }
}

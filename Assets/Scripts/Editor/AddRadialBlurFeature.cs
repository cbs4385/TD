using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Linq;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to programmatically add RadialBlurRenderFeature to ForwardRenderer3D
    /// This bypasses the UI dropdown issue in Unity 6
    /// </summary>
    public static class AddRadialBlurFeature
    {
        [MenuItem("FaeMaze/Add Radial Blur Render Feature")]
        public static void AddFeature()
        {
            // Find the ForwardRenderer3D asset
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData ForwardRenderer3D");
            if (guids.Length == 0)
            {
                Debug.LogError("[AddRadialBlurFeature] Could not find ForwardRenderer3D asset");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

            if (rendererData == null)
            {
                Debug.LogError("[AddRadialBlurFeature] Could not load ForwardRenderer3D asset");
                return;
            }

            // Check if RadialBlurRenderFeature already exists
            var existingFeature = rendererData.rendererFeatures
                .FirstOrDefault(f => f != null && f.GetType().Name == "RadialBlurRenderFeature");

            if (existingFeature != null)
            {
                Debug.LogWarning("[AddRadialBlurFeature] RadialBlurRenderFeature already exists on ForwardRenderer3D");
                Selection.activeObject = existingFeature;
                return;
            }

            // Find the RadialBlur shader
            Shader radialBlurShader = Shader.Find("Hidden/PostProcess/RadialBlur");
            if (radialBlurShader == null)
            {
                Debug.LogError("[AddRadialBlurFeature] Could not find RadialBlur shader. Make sure 'Hidden/PostProcess/RadialBlur' shader exists.");
                return;
            }

            // Create an instance of RadialBlurRenderFeature using reflection
            var featureType = System.Type.GetType("FaeMaze.PostProcessing.RadialBlurRenderFeature, Assembly-CSharp");
            if (featureType == null)
            {
                Debug.LogError("[AddRadialBlurFeature] Could not find RadialBlurRenderFeature type. Make sure the script is compiled.");
                return;
            }

            var feature = ScriptableObject.CreateInstance(featureType) as ScriptableRendererFeature;
            if (feature == null)
            {
                Debug.LogError("[AddRadialBlurFeature] Failed to create RadialBlurRenderFeature instance");
                return;
            }

            feature.name = "RadialBlur";

            // Set the shader using reflection
            var settingsField = featureType.GetField("settings");
            if (settingsField != null)
            {
                var settings = settingsField.GetValue(feature);
                var shaderField = settings.GetType().GetField("shader");
                if (shaderField != null)
                {
                    shaderField.SetValue(settings, radialBlurShader);
                    settingsField.SetValue(feature, settings);
                }
            }

            // Add to renderer
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Use reflection to add to rendererFeatures list since it might be private
            var rendererFeaturesProperty = new SerializedObject(rendererData).FindProperty("m_RendererFeatures");
            rendererFeaturesProperty.arraySize++;
            rendererFeaturesProperty.GetArrayElementAtIndex(rendererFeaturesProperty.arraySize - 1).objectReferenceValue = feature;
            rendererFeaturesProperty.serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AddRadialBlurFeature] Successfully added RadialBlurRenderFeature to ForwardRenderer3D!");
            Selection.activeObject = feature;
        }

        [MenuItem("FaeMaze/Remove Radial Blur Render Feature")]
        public static void RemoveFeature()
        {
            // Find the ForwardRenderer3D asset
            string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData ForwardRenderer3D");
            if (guids.Length == 0)
            {
                Debug.LogError("[RemoveRadialBlurFeature] Could not find ForwardRenderer3D asset");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

            if (rendererData == null)
            {
                Debug.LogError("[RemoveRadialBlurFeature] Could not load ForwardRenderer3D asset");
                return;
            }

            // Find RadialBlurRenderFeature
            var feature = rendererData.rendererFeatures
                .FirstOrDefault(f => f != null && f.GetType().Name == "RadialBlurRenderFeature");

            if (feature == null)
            {
                Debug.LogWarning("[RemoveRadialBlurFeature] RadialBlurRenderFeature not found on ForwardRenderer3D");
                return;
            }

            // Remove from renderer using SerializedObject
            var rendererFeaturesProperty = new SerializedObject(rendererData).FindProperty("m_RendererFeatures");
            for (int i = 0; i < rendererFeaturesProperty.arraySize; i++)
            {
                if (rendererFeaturesProperty.GetArrayElementAtIndex(i).objectReferenceValue == feature)
                {
                    rendererFeaturesProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            rendererFeaturesProperty.serializedObject.ApplyModifiedProperties();

            // Destroy the feature
            AssetDatabase.RemoveObjectFromAsset(feature);
            Object.DestroyImmediate(feature, true);

            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RemoveRadialBlurFeature] Successfully removed RadialBlurRenderFeature from ForwardRenderer3D");
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using System.Linq;
using FaeMaze.PostProcessing;

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
                .OfType<RadialBlurRenderFeature>()
                .FirstOrDefault();

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

            // Create an instance of RadialBlurRenderFeature
            var feature = ScriptableObject.CreateInstance<RadialBlurRenderFeature>();
            feature.name = "RadialBlur";
            feature.settings.shader = radialBlurShader;

            // Trigger Create() to properly initialize with the shader
            feature.Create();

            // Add to renderer asset
            AssetDatabase.AddObjectToAsset(feature, rendererData);

            // Use SerializedObject to properly modify and persist the feature list
            var serializedRenderer = new SerializedObject(rendererData);
            var rendererFeaturesProperty = serializedRenderer.FindProperty("m_RendererFeatures");

            // Add the feature to the array
            rendererFeaturesProperty.arraySize++;
            rendererFeaturesProperty.GetArrayElementAtIndex(rendererFeaturesProperty.arraySize - 1).objectReferenceValue = feature;

            // Apply the changes to the serialized object
            serializedRenderer.ApplyModifiedProperties();

            Debug.Log($"[AddRadialBlurFeature] Added feature to list, count={rendererFeaturesProperty.arraySize}");

            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
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
                .OfType<RadialBlurRenderFeature>()
                .FirstOrDefault();

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

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
            // Load the ForwardRenderer3D asset directly
            string path = "Assets/Settings/ForwardRenderer3D.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

            if (rendererData == null)
            {
                Debug.LogError("[AddRadialBlurFeature] Could not load ForwardRenderer3D asset at " + path);
                return;
            }

            // Check if RadialBlurRenderFeature already exists
            var existingFeature = rendererData.rendererFeatures
                .OfType<RadialBlurRenderFeature>()
                .FirstOrDefault();

            if (existingFeature != null)
            {
                Debug.LogWarning("[AddRadialBlurRenderFeature] RadialBlurRenderFeature already exists on ForwardRenderer3D");
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

            // Trigger OnValidate to rebuild internal state
            var onValidateMethod = typeof(UniversalRendererData).GetMethod("OnValidate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (onValidateMethod != null)
            {
                onValidateMethod.Invoke(rendererData, null);
                Debug.Log("[AddRadialBlurFeature] Called OnValidate on renderer data");
            }

            // Mark assets as dirty and save
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(feature);

            // Force Unity to reimport the asset to ensure changes are persisted
            string assetPath = AssetDatabase.GetAssetPath(rendererData);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[AddRadialBlurFeature] Successfully added RadialBlurRenderFeature to ForwardRenderer3D at {assetPath}");
            Selection.activeObject = feature;
        }

        [MenuItem("FaeMaze/Remove Radial Blur Render Feature")]
        public static void RemoveFeature()
        {
            // Load the ForwardRenderer3D asset directly
            string path = "Assets/Settings/ForwardRenderer3D.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);

            if (rendererData == null)
            {
                Debug.LogError("[RemoveRadialBlurFeature] Could not load ForwardRenderer3D asset at " + path);
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

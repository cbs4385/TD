using UnityEngine;
using UnityEditor;
using System.IO;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to extract textures from materials for modification.
    /// </summary>
    public class TextureExtractor : EditorWindow
    {
        private GameObject sourcePrefab;
        private string childName = "EarthenRingGround";
        private string outputPath = "Assets/Textures/ExtractedEarthenGround.png";

        [MenuItem("FaeMaze/Extract Texture from Prefab")]
        public static void ShowWindow()
        {
            GetWindow<TextureExtractor>("Texture Extractor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Extract Texture from Prefab Material", EditorStyles.boldLabel);
            GUILayout.Space(10);

            sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Source Prefab", sourcePrefab, typeof(GameObject), false);
            childName = EditorGUILayout.TextField("Child Object Name", childName);
            outputPath = EditorGUILayout.TextField("Output Path", outputPath);

            GUILayout.Space(10);

            if (GUILayout.Button("Extract Texture"))
            {
                ExtractTexture();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Extract from Heart Prefab (Quick)"))
            {
                ExtractFromHeartPrefab();
            }
        }

        private void ExtractFromHeartPrefab()
        {
            // Find the heart prefab
            string[] guids = AssetDatabase.FindAssets("heart t:Prefab", new[] { "Assets/Prefabs/Tile" });
            if (guids.Length == 0)
            {
                Debug.LogError("Could not find heart prefab in Assets/Prefabs/Tile");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            childName = "EarthenRingGround";
            outputPath = "Assets/Textures/EarthenGroundTexture.png";

            ExtractTexture();
        }

        [MenuItem("FaeMaze/Extract Base Color Texture from Heart")]
        public static void ExtractBaseColorFromHeart()
        {
            // Find the base color texture directly from the Animations folder
            string[] guids = AssetDatabase.FindAssets("EarthenRingGround_Earthen_Ground_Ring_BaseColor", new[] { "Assets/Animations" });
            if (guids.Length > 0)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                Texture2D sourceTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (sourceTex != null)
                {
                    CopyTextureStatic(sourceTex, "Assets/Textures/EarthenGroundTexture.png");
                    return;
                }
            }

            // If not found as standalone, look in heartbase.glb or other heart model sub-assets
            string[] glbGuids = AssetDatabase.FindAssets("heart t:Model", new[] { "Assets/Animations" });
            foreach (var guid in glbGuids)
            {
                string glbPath = AssetDatabase.GUIDToAssetPath(guid);
                var subAssets = AssetDatabase.LoadAllAssetsAtPath(glbPath);
                foreach (var asset in subAssets)
                {
                    if (asset is Texture2D tex && tex.name.Contains("BaseColor"))
                    {
                        Debug.Log($"Found embedded texture: {tex.name} in {glbPath}");
                        CopyTextureStatic(tex, "Assets/Textures/EarthenGroundTexture.png");
                        return;
                    }
                }
            }

            Debug.LogError("Could not find EarthenRingGround base color texture");
        }

        private static void CopyTextureStatic(Texture2D source, string destPath)
        {
            string absolutePath = Path.Combine(Application.dataPath, destPath.Replace("Assets/", ""));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Use RenderTexture to copy the texture (handles non-readable textures)
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            byte[] pngData = copy.EncodeToPNG();
            if (pngData != null && pngData.Length > 0)
            {
                File.WriteAllBytes(absolutePath, pngData);
                Debug.Log($"Extracted base color texture to: {absolutePath} ({pngData.Length} bytes, {source.width}x{source.height})");
            }
            else
            {
                Debug.LogError("Failed to encode texture to PNG");
            }

            Object.DestroyImmediate(copy);
            AssetDatabase.Refresh();

            var newTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(destPath);
            if (newTexture != null)
            {
                Selection.activeObject = newTexture;
                EditorGUIUtility.PingObject(newTexture);
            }
        }

        private void ExtractTexture()
        {
            if (sourcePrefab == null)
            {
                Debug.LogError("Please assign a source prefab");
                return;
            }

            // Find the child object
            Transform child = FindChildRecursive(sourcePrefab.transform, childName);
            if (child == null)
            {
                Debug.LogError($"Could not find child '{childName}' in prefab");
                return;
            }

            // Get the renderer and material
            var renderer = child.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError($"No MeshRenderer found on '{childName}'");
                return;
            }

            var material = renderer.sharedMaterial;
            if (material == null)
            {
                Debug.LogError($"No material found on '{childName}'");
                return;
            }

            // Try to get the main texture
            Texture2D sourceTexture = null;

            // Check common texture property names
            string[] textureProperties = { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_DiffuseMap" };
            foreach (var propName in textureProperties)
            {
                if (material.HasProperty(propName))
                {
                    sourceTexture = material.GetTexture(propName) as Texture2D;
                    if (sourceTexture != null)
                    {
                        Debug.Log($"Found texture in property: {propName}");
                        break;
                    }
                }
            }

            if (sourceTexture == null)
            {
                // List all texture properties for debugging
                Debug.LogWarning("Could not find texture in common properties. Material properties:");
                var shader = material.shader;
                int propCount = shader.GetPropertyCount();
                for (int i = 0; i < propCount; i++)
                {
                    if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        string propName = shader.GetPropertyName(i);
                        var tex = material.GetTexture(propName);
                        Debug.Log($"  Texture property: {propName} = {(tex != null ? tex.name : "null")}");
                        if (tex != null && sourceTexture == null)
                        {
                            sourceTexture = tex as Texture2D;
                        }
                    }
                }
            }

            if (sourceTexture == null)
            {
                Debug.LogError("No texture found in material. The material may use vertex colors or procedural shading.");

                // Try to create a texture from the material's base color
                if (material.HasProperty("_Color") || material.HasProperty("_BaseColor"))
                {
                    Color baseColor = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.GetColor("_Color");

                    Debug.Log($"Creating solid color texture from base color: {baseColor}");
                    CreateSolidColorTexture(baseColor, 256);
                }
                return;
            }

            // Ensure output directory exists
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Make texture readable and copy it
            CopyTexture(sourceTexture, outputPath);
        }

        private void CopyTexture(Texture2D source, string destPath)
        {
            // Convert asset path to absolute path
            string absolutePath = Path.Combine(Application.dataPath, destPath.Replace("Assets/", ""));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Get the source texture's asset path
            string sourcePath = AssetDatabase.GetAssetPath(source);

            // Check if texture is embedded in another asset (like GLB)
            bool isEmbedded = !string.IsNullOrEmpty(sourcePath) &&
                              (sourcePath.EndsWith(".glb") || sourcePath.EndsWith(".fbx") || sourcePath.EndsWith(".gltf"));

            if (!string.IsNullOrEmpty(sourcePath) && sourcePath.StartsWith("Assets") && !isEmbedded)
            {
                // It's a standalone asset, we can copy it directly
                AssetDatabase.CopyAsset(sourcePath, destPath);
                AssetDatabase.Refresh();
                Debug.Log($"Copied texture to: {destPath}");
            }
            else
            {
                // Texture is embedded or not readable, need to copy via RenderTexture
                Debug.Log($"Extracting embedded texture from: {sourcePath}");
                Debug.Log($"Source texture: {source.name}, {source.width}x{source.height}, format: {source.format}");

                RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, rt);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                copy.Apply();

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);

                byte[] pngData = copy.EncodeToPNG();

                if (pngData != null && pngData.Length > 0)
                {
                    File.WriteAllBytes(absolutePath, pngData);
                    Debug.Log($"Extracted texture to: {absolutePath} ({pngData.Length} bytes)");
                }
                else
                {
                    Debug.LogError("Failed to encode texture to PNG");
                }

                DestroyImmediate(copy);
                AssetDatabase.Refresh();
            }

            // Select the new texture in the project window
            var newTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(destPath);
            if (newTexture != null)
            {
                Selection.activeObject = newTexture;
                EditorGUIUtility.PingObject(newTexture);
            }
        }

        private void CreateSolidColorTexture(Color color, int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color;
            }
            tex.SetPixels(colors);
            tex.Apply();

            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] pngData = tex.EncodeToPNG();
            File.WriteAllBytes(outputPath, pngData);

            DestroyImmediate(tex);

            AssetDatabase.Refresh();
            Debug.Log($"Created solid color texture at: {outputPath}");
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;

            foreach (Transform child in parent)
            {
                var found = FindChildRecursive(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}

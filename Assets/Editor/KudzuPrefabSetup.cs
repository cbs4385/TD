using UnityEditor;
using UnityEngine;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Rebuilds the kudzu prefab from the source GLB with the intended orientation
    /// baked into the mesh vertices. This produces a prefab with identity root transform
    /// that follows the same Z-rotation convention as treeLOD2 walls.
    ///
    /// The original kudzu prefab had these transforms on the GLB root:
    ///   Position: (1.995, -0.32, -0.728)
    ///   Rotation: Quaternion(x=0.7071068, y=0.7071068, z=0, w=0) = Euler(0, -180, -270)
    ///   Scale: (0.5, 0.5, 1)
    ///
    /// This script bakes those transforms into the mesh vertices so the prefab
    /// root can be identity, matching the treeLOD2 convention.
    ///
    /// Run from menu: FaeMaze > Setup Kudzu Prefab
    /// </summary>
    public static class KudzuPrefabSetup
    {
        // The original transforms that gave the correct visual orientation
        private static readonly Vector3 OriginalPosition = new Vector3(1.995f, -0.32f, -0.728f);
        private static readonly Quaternion OriginalRotation = new Quaternion(0.7071068f, 0.7071068f, 0f, 0f);
        private static readonly Vector3 OriginalScale = new Vector3(0.5f, 0.5f, 1f);

        [MenuItem("FaeMaze/Setup Kudzu Prefab")]
        public static void SetupKudzuPrefab()
        {
            // Load the source GLB
            string glbPath = "Assets/Resources/Animations/kudzu.glb";
            GameObject glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);

            if (glbAsset == null)
            {
                Debug.LogError($"[KudzuPrefabSetup] Could not load GLB at {glbPath}");
                return;
            }

            // Instantiate the GLB to get the mesh hierarchy
            GameObject glbInstance = (GameObject)PrefabUtility.InstantiatePrefab(glbAsset);

            // Apply the original transforms that the user had set up
            glbInstance.transform.localPosition = OriginalPosition;
            glbInstance.transform.localRotation = OriginalRotation;
            glbInstance.transform.localScale = OriginalScale;

            // Find all meshes in the hierarchy
            MeshFilter[] meshFilters = glbInstance.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] meshRenderers = glbInstance.GetComponentsInChildren<MeshRenderer>(true);

            if (meshFilters.Length == 0)
            {
                Debug.LogError("[KudzuPrefabSetup] No MeshFilter found in GLB");
                Object.DestroyImmediate(glbInstance);
                return;
            }

            Debug.Log($"[KudzuPrefabSetup] Found {meshFilters.Length} mesh(es) in kudzu GLB");

            // Create a new clean prefab root
            GameObject newPrefabRoot = new GameObject("kudzu");
            newPrefabRoot.transform.position = Vector3.zero;
            newPrefabRoot.transform.rotation = Quaternion.identity;
            newPrefabRoot.transform.localScale = Vector3.one;

            for (int m = 0; m < meshFilters.Length; m++)
            {
                MeshFilter mf = meshFilters[m];
                Mesh originalMesh = mf.sharedMesh;
                if (originalMesh == null) continue;

                // Get the full transform from this mesh to world space
                // (includes the GLB root rotation/position/scale and any child transforms)
                Matrix4x4 meshToWorld = mf.transform.localToWorldMatrix;

                // Bake into a new mesh
                Mesh bakedMesh = Object.Instantiate(originalMesh);
                bakedMesh.name = originalMesh.name + "_baked";

                Vector3[] vertices = bakedMesh.vertices;
                Vector3[] normals = bakedMesh.normals;
                Vector4[] tangents = bakedMesh.tangents;

                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = meshToWorld.MultiplyPoint3x4(vertices[i]);
                }

                if (normals != null && normals.Length == vertices.Length)
                {
                    // Normal transform = inverse transpose of the model matrix
                    Matrix4x4 normalMatrix = meshToWorld.inverse.transpose;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                    }
                }

                if (tangents != null && tangents.Length == vertices.Length)
                {
                    for (int i = 0; i < tangents.Length; i++)
                    {
                        Vector3 t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                        t = meshToWorld.MultiplyVector(t).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                    }
                }

                bakedMesh.vertices = vertices;
                if (normals != null && normals.Length == vertices.Length)
                    bakedMesh.normals = normals;
                if (tangents != null && tangents.Length == vertices.Length)
                    bakedMesh.tangents = tangents;

                bakedMesh.RecalculateBounds();

                // Save baked mesh as asset
                string meshPath = $"Assets/Resources/Prefabs/Tile/kudzu_mesh_{m}.asset";
                AssetDatabase.CreateAsset(bakedMesh, meshPath);

                // Create child with identity transform
                string childName = meshFilters.Length > 1 ? $"Mesh_{m}" : "Mesh_0";
                GameObject meshChild = new GameObject(childName);
                meshChild.transform.SetParent(newPrefabRoot.transform, worldPositionStays: false);
                meshChild.transform.localPosition = Vector3.zero;
                meshChild.transform.localRotation = Quaternion.identity;
                meshChild.transform.localScale = Vector3.one;

                MeshFilter newMF = meshChild.AddComponent<MeshFilter>();
                newMF.sharedMesh = bakedMesh;

                MeshRenderer newMR = meshChild.AddComponent<MeshRenderer>();
                if (m < meshRenderers.Length)
                {
                    newMR.sharedMaterials = meshRenderers[m].sharedMaterials;
                }

                Debug.Log($"[KudzuPrefabSetup] Baked mesh {m}: {originalMesh.vertexCount} verts, transform applied: pos={OriginalPosition}, rot={OriginalRotation.eulerAngles}, scale={OriginalScale}");
            }

            // Clean up
            Object.DestroyImmediate(glbInstance);

            // Save as the kudzu prefab
            string prefabPath = "Assets/Resources/Prefabs/Tile/kudzu.prefab";
            PrefabUtility.SaveAsPrefabAsset(newPrefabRoot, prefabPath);
            Object.DestroyImmediate(newPrefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[KudzuPrefabSetup] Kudzu prefab rebuilt from GLB with baked orientation.");
            Debug.Log($"[KudzuPrefabSetup] Original rotation {OriginalRotation.eulerAngles} baked into vertices.");
            Debug.Log($"[KudzuPrefabSetup] Root transform is identity — use Quaternion.Euler(0,0,angle) for wall placement.");
        }
    }
}

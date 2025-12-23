using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Utility for combining multiple meshes into a single mesh to reduce draw calls.
    /// Optimizes rendering performance by batching static geometry.
    /// </summary>
    public static class MeshBatcher
    {
        #region Public Methods

        /// <summary>
        /// Combines multiple GameObjects with MeshFilters into a single mesh.
        /// </summary>
        /// <param name="objects">List of GameObjects to combine</param>
        /// <param name="parent">Parent transform for the combined mesh</param>
        /// <param name="batchName">Name for the batched GameObject</param>
        /// <param name="destroyOriginals">Whether to destroy original objects after batching</param>
        /// <returns>GameObject containing the combined mesh, or null if failed</returns>
        public static GameObject CombineMeshes(
            List<GameObject> objects,
            Transform parent,
            string batchName = "BatchedMesh",
            bool destroyOriginals = true)
        {
            if (objects == null || objects.Count == 0)
            {
                return null;
            }

            // Collect mesh filters and materials
            List<MeshFilter> meshFilters = new List<MeshFilter>();
            Material sharedMaterial = null;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                MeshFilter mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    meshFilters.Add(mf);

                    // Get material from first object (use .material to get instance, not sharedMaterial)
                    if (sharedMaterial == null)
                    {
                        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
                        if (mr != null && mr.material != null)
                        {
                            sharedMaterial = mr.material;
                        }
                    }
                }
            }

            if (meshFilters.Count == 0)
            {
                return null;
            }

            // Build CombineInstance array
            CombineInstance[] combines = new CombineInstance[meshFilters.Count];

            for (int i = 0; i < meshFilters.Count; i++)
            {
                combines[i].mesh = meshFilters[i].sharedMesh;
                combines[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            // Create combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = batchName;

            // Check if we need 32-bit indices (more than 65k vertices)
            int totalVertices = 0;
            foreach (var combine in combines)
            {
                totalVertices += combine.mesh.vertexCount;
            }

            if (totalVertices > 65535)
            {
                combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(combines, true, true);
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();

            // Create GameObject for batched mesh
            GameObject batchedObject = new GameObject(batchName);
            batchedObject.transform.SetParent(parent);
            batchedObject.transform.localPosition = Vector3.zero;
            batchedObject.transform.localRotation = Quaternion.identity;
            batchedObject.transform.localScale = Vector3.one;

            // Add mesh components
            MeshFilter batchedMF = batchedObject.AddComponent<MeshFilter>();
            batchedMF.sharedMesh = combinedMesh;

            MeshRenderer batchedMR = batchedObject.AddComponent<MeshRenderer>();
            batchedMR.sharedMaterial = sharedMaterial;

            // Mark as static for additional batching
            batchedObject.isStatic = true;

            // Destroy originals if requested
            if (destroyOriginals)
            {
                foreach (var obj in objects)
                {
                    if (obj != null)
                    {
                        Object.Destroy(obj);
                    }
                }
            }

            return batchedObject;
        }

        /// <summary>
        /// Batches objects by material to minimize draw calls.
        /// Groups objects with the same material and combines each group.
        /// </summary>
        /// <param name="objects">All objects to batch</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="destroyOriginals">Whether to destroy originals</param>
        /// <returns>List of batched GameObjects</returns>
        public static List<GameObject> BatchByMaterial(
            List<GameObject> objects,
            Transform parent,
            bool destroyOriginals = true)
        {
            if (objects == null || objects.Count == 0)
            {
                return new List<GameObject>();
            }

            // Group objects by material
            Dictionary<Material, List<GameObject>> materialGroups = new Dictionary<Material, List<GameObject>>();

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                MeshRenderer mr = obj.GetComponent<MeshRenderer>();
                if (mr != null && mr.material != null)
                {
                    Material mat = mr.material;  // Use .material to get instance

                    if (!materialGroups.ContainsKey(mat))
                    {
                        materialGroups[mat] = new List<GameObject>();
                    }

                    materialGroups[mat].Add(obj);
                }
            }

            // Combine each material group
            List<GameObject> batchedObjects = new List<GameObject>();
            int batchIndex = 0;

            foreach (var kvp in materialGroups)
            {
                Material material = kvp.Key;
                List<GameObject> group = kvp.Value;

                string batchName = $"Batch_{material.name}_{batchIndex}";
                GameObject batched = CombineMeshes(group, parent, batchName, destroyOriginals);

                if (batched != null)
                {
                    batchedObjects.Add(batched);
                }

                batchIndex++;
            }

            return batchedObjects;
        }

        /// <summary>
        /// Batches objects in chunks to avoid creating meshes that are too large.
        /// </summary>
        /// <param name="objects">Objects to batch</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="chunkSize">Maximum objects per batch</param>
        /// <param name="destroyOriginals">Whether to destroy originals</param>
        /// <returns>List of batched GameObjects</returns>
        public static List<GameObject> BatchInChunks(
            List<GameObject> objects,
            Transform parent,
            int chunkSize = 100,
            bool destroyOriginals = true)
        {
            List<GameObject> batchedObjects = new List<GameObject>();

            if (objects == null || objects.Count == 0)
            {
                return batchedObjects;
            }

            // Split into chunks
            for (int i = 0; i < objects.Count; i += chunkSize)
            {
                int count = Mathf.Min(chunkSize, objects.Count - i);
                List<GameObject> chunk = objects.GetRange(i, count);

                string batchName = $"Batch_Chunk_{i / chunkSize}";
                GameObject batched = CombineMeshes(chunk, parent, batchName, destroyOriginals);

                if (batched != null)
                {
                    batchedObjects.Add(batched);
                }
            }

            return batchedObjects;
        }

        #endregion
    }
}

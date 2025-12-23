using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages Level of Detail (LOD) for maze tiles.
    /// Optimizes rendering by showing simplified meshes at distance.
    /// </summary>
    public class TileLODManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("LOD Settings")]
        [SerializeField]
        [Tooltip("Enable LOD system")]
        private bool enableLOD = true;

        [SerializeField]
        [Tooltip("LOD0 (full detail) screen height threshold")]
        [Range(0f, 1f)]
        private float lod0ScreenHeight = 0.5f;

        [SerializeField]
        [Tooltip("LOD1 (medium detail) screen height threshold")]
        [Range(0f, 1f)]
        private float lod1ScreenHeight = 0.25f;

        [SerializeField]
        [Tooltip("LOD2 (low detail) screen height threshold")]
        [Range(0f, 1f)]
        private float lod2ScreenHeight = 0.1f;

        [Header("Tile Type Settings")]
        [SerializeField]
        [Tooltip("Apply LOD to wall tiles")]
        private bool lodWalls = true;

        [SerializeField]
        [Tooltip("Apply LOD to undergrowth tiles")]
        private bool lodUndergrowth = true;

        [SerializeField]
        [Tooltip("Apply LOD to water tiles")]
        private bool lodWater = false; // Water is usually flat, doesn't need LOD

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates an LOD group for a tile GameObject.
        /// </summary>
        /// <param name="tileObject">The tile GameObject</param>
        /// <param name="tileType">Type of tile (wall, undergrowth, etc.)</param>
        public void ApplyLOD(GameObject tileObject, TileType tileType)
        {
            if (!enableLOD || tileObject == null)
            {
                return;
            }

            // Check if LOD should be applied to this tile type
            bool shouldApplyLOD = false;
            switch (tileType)
            {
                case TileType.TreeBramble:
                    shouldApplyLOD = lodWalls;
                    break;
                case TileType.Undergrowth:
                    shouldApplyLOD = lodUndergrowth;
                    break;
                case TileType.Water:
                    shouldApplyLOD = lodWater;
                    break;
                default:
                    shouldApplyLOD = false;
                    break;
            }

            if (!shouldApplyLOD)
            {
                return;
            }

            // Get or add LODGroup component
            LODGroup lodGroup = tileObject.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = tileObject.AddComponent<LODGroup>();
            }

            // Get the mesh renderer
            MeshRenderer renderer = tileObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            // Create LOD levels
            LOD[] lods = new LOD[3];

            // LOD 0 - Full detail
            lods[0] = new LOD(lod0ScreenHeight, new Renderer[] { renderer });

            // LOD 1 - Medium detail (same mesh, but could be simplified)
            lods[1] = new LOD(lod1ScreenHeight, new Renderer[] { renderer });

            // LOD 2 - Low detail (could be a simple cube or nothing)
            lods[2] = new LOD(lod2ScreenHeight, new Renderer[] { renderer });

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        /// <summary>
        /// Creates simplified LOD meshes for a prefab.
        /// </summary>
        /// <param name="originalPrefab">The original prefab</param>
        /// <returns>GameObject with LOD group configured</returns>
        public static GameObject CreateLODPrefab(GameObject originalPrefab)
        {
            if (originalPrefab == null)
            {
                return null;
            }

            // Create a new GameObject for the LOD prefab
            GameObject lodPrefab = new GameObject(originalPrefab.name + "_LOD");

            // Get mesh from original
            MeshFilter originalMF = originalPrefab.GetComponent<MeshFilter>();
            MeshRenderer originalMR = originalPrefab.GetComponent<MeshRenderer>();

            if (originalMF == null || originalMR == null)
            {
                Object.Destroy(lodPrefab);
                return null;
            }

            // Create LOD0 (full detail) - exact copy
            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(lodPrefab.transform);
            lod0.transform.localPosition = Vector3.zero;
            lod0.transform.localRotation = Quaternion.identity;
            lod0.transform.localScale = Vector3.one;

            MeshFilter lod0MF = lod0.AddComponent<MeshFilter>();
            lod0MF.sharedMesh = originalMF.sharedMesh;

            MeshRenderer lod0MR = lod0.AddComponent<MeshRenderer>();
            lod0MR.sharedMaterial = originalMR.sharedMaterial;

            // Create LOD1 (medium detail) - simplified
            GameObject lod1 = CreateSimplifiedMesh(originalMF.sharedMesh, originalMR.sharedMaterial, "LOD1", 0.5f);
            lod1.transform.SetParent(lodPrefab.transform);

            // Create LOD2 (low detail) - very simplified or cube
            GameObject lod2 = CreateSimplifiedMesh(originalMF.sharedMesh, originalMR.sharedMaterial, "LOD2", 0.2f);
            lod2.transform.SetParent(lodPrefab.transform);

            // Setup LOD group
            LODGroup lodGroup = lodPrefab.AddComponent<LODGroup>();
            LOD[] lods = new LOD[3];

            lods[0] = new LOD(0.5f, new Renderer[] { lod0MR });
            lods[1] = new LOD(0.25f, new Renderer[] { lod1.GetComponent<MeshRenderer>() });
            lods[2] = new LOD(0.1f, new Renderer[] { lod2.GetComponent<MeshRenderer>() });

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            return lodPrefab;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a simplified version of a mesh (placeholder for actual mesh simplification).
        /// </summary>
        private static GameObject CreateSimplifiedMesh(Mesh original, Material material, string name, float quality)
        {
            GameObject simplified = new GameObject(name);

            // For now, just use the original mesh
            // In a real implementation, you would use a mesh simplification algorithm
            MeshFilter mf = simplified.AddComponent<MeshFilter>();
            mf.sharedMesh = original;

            MeshRenderer mr = simplified.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;

            simplified.transform.localPosition = Vector3.zero;
            simplified.transform.localRotation = Quaternion.identity;
            simplified.transform.localScale = Vector3.one;

            return simplified;
        }

        #endregion
    }
}

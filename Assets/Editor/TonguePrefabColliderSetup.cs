using UnityEngine;
using UnityEditor;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script that bakes colliders into the tongue prefab.
    /// Creates both solid and trigger colliders on every 10th bone for physics blocking and detection.
    /// Run via FaeMaze > Setup Tongue Prefab Colliders menu.
    /// </summary>
    public class TonguePrefabColliderSetup : UnityEditor.Editor
    {
        // Collider spacing - every 10th bone gets colliders
        private const int BONE_SPACING = 10;

        // Base radius for colliders (in local space, will be scaled by armature)
        // Tapered from base to tip
        private const float BASE_COLLIDER_RADIUS = 0.006f;  // ~0.3 world units at base (100x armature scale)
        private const float TIP_COLLIDER_RADIUS = 0.002f;   // ~0.1 world units at tip

        // Total bones in tongue
        private const int EXPECTED_BONE_COUNT = 540;

        [MenuItem("FaeMaze/Setup Tongue Prefab Colliders")]
        public static void SetupTonguePrefabColliders()
        {
            // Load the tongue prefab
            string prefabPath = "Assets/Resources/Prefabs/Tile/heart tongue.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                Debug.LogError($"Could not find tongue prefab at {prefabPath}");
                return;
            }

            // Open prefab for editing
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                // Find the Armature
                Transform armature = prefabRoot.transform.Find("Armature");
                if (armature == null)
                {
                    Debug.LogError("Could not find Armature in tongue prefab");
                    return;
                }

                // Remove any existing collider objects first
                RemoveExistingColliders(prefabRoot.transform);

                int collidersCreated = 0;

                // Find all bones and add colliders to every 10th one
                for (int i = 0; i < EXPECTED_BONE_COUNT; i += BONE_SPACING)
                {
                    string boneName = $"Bone_{i:D3}";
                    Transform bone = FindBoneRecursive(armature, boneName);

                    if (bone == null)
                    {
                        Debug.LogWarning($"Could not find bone: {boneName}");
                        continue;
                    }

                    // Calculate tapered radius based on bone index
                    float t = (float)i / EXPECTED_BONE_COUNT;
                    float radius = Mathf.Lerp(BASE_COLLIDER_RADIUS, TIP_COLLIDER_RADIUS, t);

                    // Create solid collider (for physics blocking)
                    CreateSolidCollider(bone, i / BONE_SPACING, radius);

                    // Create trigger collider (for detection events)
                    CreateTriggerCollider(bone, i / BONE_SPACING, radius);

                    collidersCreated += 2;
                }

                // Add tip trigger on the last bone
                Transform tipBone = FindBoneRecursive(armature, $"Bone_{EXPECTED_BONE_COUNT - 1:D3}");
                if (tipBone != null)
                {
                    CreateTipTrigger(tipBone);
                    collidersCreated++;
                }

                // Find the tongue mesh material to use for the tip cap
                Material tongueMaterial = FindTongueMaterial(prefabRoot.transform);

                // Add sphere cap to the tip
                CreateTipSphereCap(tipBone, tongueMaterial);

                // Save the prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                Debug.Log($"Successfully created {collidersCreated} colliders on tongue prefab (every {BONE_SPACING}th bone)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void RemoveExistingColliders(Transform root)
        {
            // Find and destroy all existing collider objects
            var toDestroy = new System.Collections.Generic.List<GameObject>();

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("SolidCollider_") ||
                    child.name.StartsWith("BoneCollider_") ||
                    child.name == "TipTrigger" ||
                    child.name == "TipSphereCap")
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (var obj in toDestroy)
            {
                DestroyImmediate(obj);
            }

            if (toDestroy.Count > 0)
            {
                Debug.Log($"Removed {toDestroy.Count} existing collider objects");
            }
        }

        private static Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName)
                return parent;

            foreach (Transform child in parent)
            {
                Transform found = FindBoneRecursive(child, boneName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void CreateSolidCollider(Transform bone, int index, float radius)
        {
            // Create child object for solid collider
            GameObject solidObj = new GameObject($"SolidCollider_{index}");
            solidObj.transform.SetParent(bone, false);
            solidObj.transform.localPosition = Vector3.zero;
            solidObj.transform.localRotation = Quaternion.identity;
            solidObj.transform.localScale = Vector3.one;

            // Add sphere collider (SOLID for physics blocking)
            SphereCollider solidSphere = solidObj.AddComponent<SphereCollider>();
            solidSphere.radius = radius;
            solidSphere.isTrigger = false;  // SOLID for physics blocking

            // Add kinematic rigidbody (moves with bones, doesn't respond to forces)
            Rigidbody solidRb = solidObj.AddComponent<Rigidbody>();
            solidRb.isKinematic = true;
            solidRb.useGravity = false;
        }

        private static void CreateTriggerCollider(Transform bone, int index, float radius)
        {
            // Create child object for trigger collider
            GameObject triggerObj = new GameObject($"BoneCollider_{index}");
            triggerObj.transform.SetParent(bone, false);
            triggerObj.transform.localPosition = Vector3.zero;
            triggerObj.transform.localRotation = Quaternion.identity;
            triggerObj.transform.localScale = Vector3.one;

            // Add sphere collider (TRIGGER for OnTriggerEnter events)
            SphereCollider triggerSphere = triggerObj.AddComponent<SphereCollider>();
            triggerSphere.radius = radius;
            triggerSphere.isTrigger = true;  // TRIGGER for detection events

            // Add kinematic rigidbody
            Rigidbody triggerRb = triggerObj.AddComponent<Rigidbody>();
            triggerRb.isKinematic = true;
            triggerRb.useGravity = false;
        }

        private static void CreateTipTrigger(Transform tipBone)
        {
            // Create tip trigger for exit detection
            GameObject tipObj = new GameObject("TipTrigger");
            tipObj.transform.SetParent(tipBone, false);
            tipObj.transform.localPosition = Vector3.zero;
            tipObj.transform.localRotation = Quaternion.identity;
            tipObj.transform.localScale = Vector3.one;

            // Small trigger at the tip
            SphereCollider tipSphere = tipObj.AddComponent<SphereCollider>();
            tipSphere.radius = TIP_COLLIDER_RADIUS;
            tipSphere.isTrigger = true;

            Rigidbody tipRb = tipObj.AddComponent<Rigidbody>();
            tipRb.isKinematic = true;
            tipRb.useGravity = false;
        }

        private static Material FindTongueMaterial(Transform root)
        {
            // Look for SkinnedMeshRenderer on the tongue model
            var skinnedMeshRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMaterial != null)
            {
                return skinnedMeshRenderer.sharedMaterial;
            }

            // Fallback: look for any MeshRenderer
            var meshRenderer = root.GetComponentInChildren<MeshRenderer>(true);
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                return meshRenderer.sharedMaterial;
            }

            Debug.LogWarning("Could not find tongue material - tip sphere will use default material");
            return null;
        }

        private static void CreateTipSphereCap(Transform tipBone, Material tongueMaterial)
        {
            if (tipBone == null) return;

            // Create a visual sphere cap at the tip of the tongue
            GameObject sphereCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereCap.name = "TipSphereCap";
            sphereCap.transform.SetParent(tipBone, false);
            sphereCap.transform.localPosition = Vector3.zero;
            sphereCap.transform.localRotation = Quaternion.identity;

            // Scale to match the tip collider size (accounting for armature scale)
            // The sphere primitive has radius 0.5, so scale to match desired radius
            float visualRadius = TIP_COLLIDER_RADIUS * 2f; // Double because primitive radius is 0.5
            sphereCap.transform.localScale = new Vector3(visualRadius, visualRadius, visualRadius);

            // Remove the collider from the visual sphere (we have separate colliders)
            var col = sphereCap.GetComponent<Collider>();
            if (col != null)
                DestroyImmediate(col);

            // Apply the tongue material to the sphere cap
            if (tongueMaterial != null)
            {
                var renderer = sphereCap.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = tongueMaterial;
                    Debug.Log($"Created TipSphereCap visual on tongue tip with material: {tongueMaterial.name}");
                }
            }
            else
            {
                Debug.Log("Created TipSphereCap visual on tongue tip (no material found)");
            }
        }
    }
}

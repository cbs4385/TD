using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to bake bone colliders into the heart tongue prefab.
    /// Run from Unity menu: FaeMaze > Setup Tongue Prefab Colliders
    ///
    /// Creates SolidCollider_N (blocking) and BoneCollider_N (trigger) as children of bones.
    /// Colliders inherit bone transforms automatically - no runtime position updates needed.
    ///
    /// CRITICAL: Creating physics objects at runtime causes 15+ second freezes due to
    /// physics broadphase rebuilds. The colliders MUST be baked into the prefab.
    ///
    /// Collider sizing (accounting for Armature scale of 100×):
    /// - Every 10th bone = 54 collider pairs (108 total)
    /// - Base (bone 0): scale 0.006, radius 0.5 → world radius ~0.3 (0.5 × 0.006 × 100)
    /// - Tip (bone 539): scale 0.002, radius 0.5 → world radius ~0.1 (0.5 × 0.002 × 100)
    /// </summary>
    public static class TonguePrefabColliderSetup
    {
        // Collider spacing - every 10th bone = 54 colliders
        private const int COLLIDER_SPACING = 10;

        // Prefab root scale: (1, 0.3, 0.3) - note: X is 1.0, Y/Z are 0.3
        // Bones have their own scale chain within the armature
        // NO runtime scaling - prefab scale is final
        //
        // Colliders taper from base to tip
        // IMPORTANT: Armature has scale (100, 100, 100), so collider scale must compensate
        // worldRadius = localRadius × colliderScale × armatureScale(100)
        // For world radius ~0.3 at base: 0.5 × 0.006 × 100 = 0.3
        // For world radius ~0.1 at tip:  0.5 × 0.002 × 100 = 0.1
        private const float BONE_COLLIDER_RADIUS_LOCAL = 0.5f;
        private const float BONE_COLLIDER_SCALE_BASE = 0.006f;   // Scale at base → world radius ~0.3
        private const float BONE_COLLIDER_SCALE_TIP = 0.002f;    // Scale at tip → world radius ~0.1
        private const int TOTAL_BONES = 540;

        private const string PREFAB_PATH = "Assets/Prefabs/Tile/heart tongue.prefab";

        [MenuItem("FaeMaze/Setup Tongue Prefab Colliders")]
        public static void SetupColliders()
        {
            // Load the prefab for editing
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);

            if (prefabRoot == null)
            {
                Debug.LogError($"[TonguePrefabColliderSetup] Could not load prefab at {PREFAB_PATH}");
                return;
            }

            try
            {
                // Remove any existing collider objects
                int removedCount = RemoveExistingColliders(prefabRoot);

                // Find bones via SkinnedMeshRenderer
                SkinnedMeshRenderer smr = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null || smr.bones == null || smr.bones.Length == 0)
                {
                    Debug.LogError("[TonguePrefabColliderSetup] No SkinnedMeshRenderer with bones found!");
                    return;
                }

                Transform[] bones = smr.bones;
                Debug.Log($"[TonguePrefabColliderSetup] Found {bones.Length} bones");

                // Create new colliders as children of bones
                int colliderCount = CreateColliders(bones);

                // Save the prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);

                int solidCount = colliderCount / 2;
                int triggerCount = colliderCount / 2;
                Debug.Log($"[TonguePrefabColliderSetup] Setup complete:");
                Debug.Log($"  - Removed {removedCount} existing collider objects");
                Debug.Log($"  - Created {solidCount} SolidCollider_N (physics blocking) + {triggerCount} BoneCollider_N (touch detection)");
                Debug.Log($"  - Tapered scale: {BONE_COLLIDER_SCALE_BASE:F4} (base) → {BONE_COLLIDER_SCALE_TIP:F4} (tip)");
                Debug.Log($"  - World radius (with 100× armature): {BONE_COLLIDER_RADIUS_LOCAL * BONE_COLLIDER_SCALE_BASE * 100f:F2} (base) → {BONE_COLLIDER_RADIUS_LOCAL * BONE_COLLIDER_SCALE_TIP * 100f:F2} (tip)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }

        private static int RemoveExistingColliders(GameObject root)
        {
            var toDestroy = new List<GameObject>();

            // Find all SolidCollider_* and BoneCollider_* objects anywhere in the hierarchy
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("SolidCollider_") || child.name.StartsWith("BoneCollider_"))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (var obj in toDestroy)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            return toDestroy.Count;
        }

        private static int CreateColliders(Transform[] bones)
        {
            int colliderCount = 0;

            for (int i = 0; i < bones.Length; i += COLLIDER_SPACING)
            {
                Transform bone = bones[i];
                if (bone == null) continue;

                // Calculate tapered scale based on bone position (0=base, 539=tip)
                float t = Mathf.Clamp01((float)i / (TOTAL_BONES - 1));
                float colliderScale = Mathf.Lerp(BONE_COLLIDER_SCALE_BASE, BONE_COLLIDER_SCALE_TIP, t);

                // Create SOLID collider for physics blocking (HeartOfTheMaze uses these)
                GameObject solidObj = new GameObject($"SolidCollider_{i}");
                solidObj.transform.SetParent(bone, false);
                solidObj.transform.localPosition = Vector3.zero;
                solidObj.transform.localRotation = Quaternion.identity;
                solidObj.transform.localScale = Vector3.one * colliderScale;

                SphereCollider solidSphere = solidObj.AddComponent<SphereCollider>();
                solidSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
                solidSphere.isTrigger = false;  // Solid for physics blocking

                Rigidbody solidRb = solidObj.AddComponent<Rigidbody>();
                solidRb.isKinematic = true;
                solidRb.useGravity = false;

                // Create TRIGGER collider for contact detection (HeartPowerEffects uses these)
                GameObject triggerObj = new GameObject($"BoneCollider_{i}");
                triggerObj.transform.SetParent(bone, false);
                triggerObj.transform.localPosition = Vector3.zero;
                triggerObj.transform.localRotation = Quaternion.identity;
                triggerObj.transform.localScale = Vector3.one * colliderScale;

                SphereCollider triggerSphere = triggerObj.AddComponent<SphereCollider>();
                triggerSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
                triggerSphere.isTrigger = true;  // Trigger for contact detection

                // worldRadius = localRadius × colliderScale × armatureScale(100)
                float worldRadius = BONE_COLLIDER_RADIUS_LOCAL * colliderScale * 100f;
                Debug.Log($"  Created colliders for bone {i} ({bone.name}), scale={colliderScale:F4}, worldRadius={worldRadius:F3}");
                colliderCount += 2;  // Count both solid and trigger
            }

            return colliderCount;
        }
    }
}

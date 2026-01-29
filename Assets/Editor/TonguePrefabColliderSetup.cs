using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ForestMaze;

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

        private const string FBX_PATH = "Assets/Animations/heart/heart tongue.fbx";

        [MenuItem("FaeMaze/Setup Tongue Prefab Colliders")]
        public static void SetupColliders()
        {
            // First, check if we need to recreate the prefab from FBX
            // The prefab might be missing the SkinnedMeshRenderer if created incorrectly
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            bool needsRecreation = false;

            if (existingPrefab != null)
            {
                SkinnedMeshRenderer existingSmr = existingPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                if (existingSmr == null)
                {
                    Debug.LogWarning("[TonguePrefabColliderSetup] Prefab exists but has no SkinnedMeshRenderer - will recreate from FBX");
                    needsRecreation = true;
                }
            }
            else
            {
                needsRecreation = true;
            }

            if (needsRecreation)
            {
                RecreateFromFBX();
            }

            // Now load the prefab for editing
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

                // Find bones - try SkinnedMeshRenderer first, then fall back to Armature hierarchy
                Transform[] bones = null;

                SkinnedMeshRenderer smr = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null && smr.bones != null && smr.bones.Length > 0)
                {
                    bones = smr.bones;
                    Debug.Log($"[TonguePrefabColliderSetup] Found {bones.Length} bones via SkinnedMeshRenderer");
                }
                else
                {
                    // Fall back to finding bones in Armature hierarchy
                    Debug.LogWarning("[TonguePrefabColliderSetup] SkinnedMeshRenderer has no bones, searching Armature hierarchy...");

                    // Find Armature transform
                    Transform armature = prefabRoot.transform.Find("Armature");
                    if (armature == null)
                    {
                        // Try to find it recursively
                        foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.name == "Armature")
                            {
                                armature = child;
                                break;
                            }
                        }
                    }

                    if (armature != null)
                    {
                        // Collect all bone transforms (Bone_XXX or Bone.XXX format)
                        var boneList = new List<Transform>();
                        foreach (Transform child in armature.GetComponentsInChildren<Transform>(true))
                        {
                            string name = child.name;
                            // Match Bone_XXX, Bone.XXX, or BoneXXX patterns
                            if (name.StartsWith("Bone"))
                            {
                                boneList.Add(child);
                            }
                        }

                        if (boneList.Count > 0)
                        {
                            // Sort by bone number - extract digits from name
                            boneList.Sort((a, b) => {
                                int numA = ExtractBoneNumber(a.name);
                                int numB = ExtractBoneNumber(b.name);
                                return numA.CompareTo(numB);
                            });
                            bones = boneList.ToArray();
                            Debug.Log($"[TonguePrefabColliderSetup] Found {bones.Length} bones via Armature hierarchy");
                            Debug.Log($"  First bone: {bones[0].name}, Last bone: {bones[bones.Length - 1].name}");
                        }
                    }
                }

                if (bones == null || bones.Length == 0)
                {
                    Debug.LogError("[TonguePrefabColliderSetup] No bones found! Make sure the FBX is imported correctly.");

                    // Debug: Log all transforms in hierarchy
                    Debug.Log("Prefab hierarchy:");
                    foreach (Transform t in prefabRoot.GetComponentsInChildren<Transform>(true))
                    {
                        Debug.Log($"  - {t.name}");
                    }
                    return;
                }

                // Create new colliders as children of bones
                int colliderCount = CreateColliders(bones);

                // Assign the tongue material if available
                AssignTongueMaterial(smr);

                // Save the prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);

                // colliderCount includes bone collider pairs (solid + trigger) + 1 TipTrigger
                int boneColliderPairs = (colliderCount - 1) / 2;  // Subtract TipTrigger, divide by 2 for pairs
                Debug.Log($"[TonguePrefabColliderSetup] Setup complete:");
                Debug.Log($"  - Removed {removedCount} existing collider objects");
                Debug.Log($"  - Created {boneColliderPairs} bone collider PAIRS:");
                Debug.Log($"      - {boneColliderPairs} SolidCollider_N (solid physics blocking)");
                Debug.Log($"      - {boneColliderPairs} BoneCollider_N (trigger for detection)");
                Debug.Log($"      - 1 TipTrigger (tip exit detection)");
                Debug.Log($"  - Total collider objects: {colliderCount}");
                Debug.Log($"  - Tapered scale: {BONE_COLLIDER_SCALE_BASE:F4} (base) → {BONE_COLLIDER_SCALE_TIP:F4} (tip)");
                Debug.Log($"  - World radius (with 100× armature): {BONE_COLLIDER_RADIUS_LOCAL * BONE_COLLIDER_SCALE_BASE * 100f:F2} (base) → {BONE_COLLIDER_RADIUS_LOCAL * BONE_COLLIDER_SCALE_TIP * 100f:F2} (tip)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Extracts the bone number from a bone name like "Bone_000", "Bone.001", "Bone123", etc.
        /// </summary>
        private static int ExtractBoneNumber(string boneName)
        {
            // Find digits in the name
            string digits = "";
            foreach (char c in boneName)
            {
                if (char.IsDigit(c))
                {
                    digits += c;
                }
            }

            if (string.IsNullOrEmpty(digits))
            {
                return 0;
            }

            // Parse as integer (handles leading zeros)
            if (int.TryParse(digits, out int result))
            {
                return result;
            }

            return 0;
        }

        /// <summary>
        /// Recreates the prefab from the FBX source, ensuring the SkinnedMeshRenderer is included.
        /// </summary>
        private static void RecreateFromFBX()
        {
            // Load the FBX
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
            if (fbxModel == null)
            {
                Debug.LogError($"[TonguePrefabColliderSetup] Could not load FBX at {FBX_PATH}");
                return;
            }

            // Log all components in the FBX for debugging
            Debug.Log($"[TonguePrefabColliderSetup] FBX root: {fbxModel.name}");
            foreach (var comp in fbxModel.GetComponentsInChildren<Component>(true))
            {
                if (comp != null)
                    Debug.Log($"  - {comp.GetType().Name} on {comp.gameObject.name}");
            }

            // Check FBX directly for SkinnedMeshRenderer
            SkinnedMeshRenderer fbxSmr = fbxModel.GetComponentInChildren<SkinnedMeshRenderer>();
            if (fbxSmr != null)
            {
                Debug.Log($"[TonguePrefabColliderSetup] FBX has SkinnedMeshRenderer: mesh={fbxSmr.sharedMesh?.name ?? "null"}, bones={fbxSmr.bones?.Length ?? 0}");
            }
            else
            {
                Debug.LogWarning("[TonguePrefabColliderSetup] FBX has no SkinnedMeshRenderer - checking for MeshRenderer instead");
                MeshRenderer mr = fbxModel.GetComponentInChildren<MeshRenderer>();
                if (mr != null)
                {
                    Debug.Log($"[TonguePrefabColliderSetup] FBX has MeshRenderer on {mr.gameObject.name}");
                }
            }

            // Instantiate the FBX model
            GameObject instance = Object.Instantiate(fbxModel);
            instance.name = "heart tongue";

            // Verify it has a SkinnedMeshRenderer
            SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.LogError("[TonguePrefabColliderSetup] Instantiated FBX has no SkinnedMeshRenderer!");
                Object.DestroyImmediate(instance);
                return;
            }

            Debug.Log($"[TonguePrefabColliderSetup] Instance has SkinnedMeshRenderer with {smr.bones?.Length ?? 0} bones and mesh '{smr.sharedMesh?.name ?? "null"}'");

            // Create/overwrite the prefab
            // Ensure the directory exists
            string directory = System.IO.Path.GetDirectoryName(PREFAB_PATH);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            // Save as new prefab (this will overwrite if exists)
            PrefabUtility.SaveAsPrefabAsset(instance, PREFAB_PATH);
            Debug.Log($"[TonguePrefabColliderSetup] Created prefab at {PREFAB_PATH}");

            // Cleanup the temporary instance
            Object.DestroyImmediate(instance);
        }

        private static int RemoveExistingColliders(GameObject root)
        {
            var toDestroy = new List<GameObject>();

            // Find all SolidCollider_*, BoneCollider_*, and TipTrigger objects anywhere in the hierarchy
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("SolidCollider_") ||
                    child.name.StartsWith("BoneCollider_") ||
                    child.name == "TipTrigger")
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

        /// <summary>
        /// Assigns a dark reddish-brown material to the SkinnedMeshRenderer.
        /// Creates or updates the TongueMat material using URP/Lit shader.
        /// </summary>
        private static void AssignTongueMaterial(SkinnedMeshRenderer smr)
        {
            if (smr == null)
            {
                Debug.LogWarning("[TonguePrefabColliderSetup] No SkinnedMeshRenderer provided for material assignment");
                return;
            }

            const string MATERIAL_PATH = "Assets/Animations/Materials/TongueMat.mat";

            // Try to load existing material first
            Material tongueMat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);

            // Find the URP Lit shader
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                Debug.LogError("[TonguePrefabColliderSetup] Could not find URP/Lit shader!");
                return;
            }

            // Create new material if it doesn't exist
            if (tongueMat == null)
            {
                tongueMat = new Material(urpLitShader);
                tongueMat.name = "TongueMat";

                // Ensure directory exists
                string directory = System.IO.Path.GetDirectoryName(MATERIAL_PATH);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                AssetDatabase.CreateAsset(tongueMat, MATERIAL_PATH);
                Debug.Log($"[TonguePrefabColliderSetup] Created new material at {MATERIAL_PATH}");
            }
            else
            {
                // Update existing material's shader if needed
                if (tongueMat.shader != urpLitShader)
                {
                    tongueMat.shader = urpLitShader;
                    Debug.Log("[TonguePrefabColliderSetup] Updated material shader to URP/Lit");
                }
            }

            // Set the dark reddish-brown color (flesh/tongue color)
            Color tongueColor = new Color(0.4f, 0.15f, 0.2f, 1f);
            tongueMat.SetColor("_BaseColor", tongueColor);
            tongueMat.SetColor("_Color", tongueColor);  // Legacy property

            // Set rendering properties
            tongueMat.SetFloat("_Cull", 0f);           // Double-sided (render both faces)
            tongueMat.SetFloat("_Smoothness", 0.3f);   // Slight glossiness for wet appearance
            tongueMat.SetFloat("_Metallic", 0f);       // Not metallic
            tongueMat.SetFloat("_Surface", 0f);        // Opaque
            tongueMat.SetFloat("_Blend", 0f);          // Alpha blend mode
            tongueMat.SetFloat("_AlphaClip", 0f);      // No alpha clipping
            tongueMat.SetFloat("_ZWrite", 1f);         // Write to depth buffer
            tongueMat.SetFloat("_SrcBlend", 1f);       // One
            tongueMat.SetFloat("_DstBlend", 0f);       // Zero

            // Enable double-sided global illumination
            tongueMat.doubleSidedGI = true;

            // Set render queue for opaque
            tongueMat.renderQueue = -1;  // Use shader's default

            // Mark as dirty so Unity saves changes
            EditorUtility.SetDirty(tongueMat);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TonguePrefabColliderSetup] Configured TongueMat with color RGB({tongueColor.r:F2}, {tongueColor.g:F2}, {tongueColor.b:F2})");

            // Assign the material to all material slots
            int slotCount = Mathf.Max(1, smr.sharedMaterials.Length);
            Material[] materials = new Material[slotCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = tongueMat;
            }
            smr.sharedMaterials = materials;

            Debug.Log($"[TonguePrefabColliderSetup] Assigned TongueMat material to {materials.Length} material slot(s)");
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
                float worldRadius = BONE_COLLIDER_RADIUS_LOCAL * colliderScale * 100f;

                // Create SOLID collider for physics-based blocking
                // KINEMATIC rigidbody moved via Rigidbody.MovePosition() in FixedUpdate
                // MovePosition on kinematic bodies DOES push dynamic bodies when they overlap
                GameObject solidObj = new GameObject($"SolidCollider_{i}");
                solidObj.transform.SetParent(bone, false);
                solidObj.transform.localPosition = Vector3.zero;
                solidObj.transform.localRotation = Quaternion.identity;
                solidObj.transform.localScale = Vector3.one * colliderScale;

                SphereCollider solidSphere = solidObj.AddComponent<SphereCollider>();
                solidSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
                solidSphere.isTrigger = false;  // SOLID for physics blocking

                Rigidbody solidRb = solidObj.AddComponent<Rigidbody>();
                solidRb.isKinematic = true;     // KINEMATIC - moved via MovePosition which pushes dynamic bodies
                solidRb.useGravity = false;
                solidRb.mass = 1000f;           // High mass for collision calculations
                solidRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                solidRb.interpolation = RigidbodyInterpolation.Interpolate;
                colliderCount++;

                // Create TRIGGER collider for detection (OnTriggerEnter events)
                // This allows HeartOfTheMaze to detect when tongue touches visitor
                // Same size as solid collider - they overlap exactly
                GameObject triggerObj = new GameObject($"BoneCollider_{i}");
                triggerObj.transform.SetParent(bone, false);
                triggerObj.transform.localPosition = Vector3.zero;
                triggerObj.transform.localRotation = Quaternion.identity;
                triggerObj.transform.localScale = Vector3.one * colliderScale;

                SphereCollider triggerSphere = triggerObj.AddComponent<SphereCollider>();
                triggerSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
                triggerSphere.isTrigger = true;  // TRIGGER for detection events

                Rigidbody triggerRb = triggerObj.AddComponent<Rigidbody>();
                triggerRb.isKinematic = true;
                triggerRb.useGravity = false;
                triggerRb.mass = 100f;          // High mass for stronger collision response
                colliderCount++;

                Debug.Log($"  Created solid+trigger pair for bone {i} ({bone.name}), scale={colliderScale:F4}, worldRadius={worldRadius:F3}");
            }

            // Create TipTrigger on the LAST bone (tip of tongue)
            // This is used by HeartOfTheMaze and HeartwardGrasp to detect when tip exits the detection zone
            int tipBoneIndex = bones.Length - 1;
            Transform tipBone = bones[tipBoneIndex];
            if (tipBone != null)
            {
                // Use the tip scale for consistency
                float tipScale = BONE_COLLIDER_SCALE_TIP;

                GameObject tipTriggerObj = new GameObject("TipTrigger");
                tipTriggerObj.transform.SetParent(tipBone, false);
                tipTriggerObj.transform.localPosition = Vector3.zero;
                tipTriggerObj.transform.localRotation = Quaternion.identity;
                tipTriggerObj.transform.localScale = Vector3.one * tipScale;

                SphereCollider tipSphere = tipTriggerObj.AddComponent<SphereCollider>();
                tipSphere.radius = BONE_COLLIDER_RADIUS_LOCAL;
                tipSphere.isTrigger = true;  // Trigger for exit detection

                Rigidbody tipRb = tipTriggerObj.AddComponent<Rigidbody>();
                tipRb.isKinematic = true;
                tipRb.useGravity = false;
                tipRb.mass = 100f;              // High mass for stronger collision response

                // Add the handler component for event forwarding
                tipTriggerObj.AddComponent<TipTriggerHandler>();

                float tipWorldRadius = BONE_COLLIDER_RADIUS_LOCAL * tipScale * 100f;
                Debug.Log($"  Created TipTrigger on bone {tipBoneIndex} ({tipBone.name}), scale={tipScale:F4}, worldRadius={tipWorldRadius:F3}");
                colliderCount += 1;
            }

            return colliderCount;
        }
    }
}

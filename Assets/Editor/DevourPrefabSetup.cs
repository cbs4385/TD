using UnityEngine;
using UnityEditor;
using FaeMaze.HeartPowers;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to configure the Detect collider on the devour prefab.
    /// Run from Unity menu: FaeMaze > Setup Devour Prefab Colliders
    ///
    /// The Detect collider is a trigger zone that catches visitors entering the maw
    /// during the Emerging and Paused phases. Since Rigidbody.MovePosition() bypasses
    /// solid colliders, we use trigger detection instead of physics blocking.
    /// </summary>
    public static class DevourPrefabSetup
    {
        private const string PREFAB_PATH = "Assets/Prefabs/Props/devour.prefab";

        // Collider dimensions
        // The game uses XY as ground plane, -Z as world up
        // Capsule direction 2 = Z-axis (vertical in this game)
        private const float DETECT_HEIGHT = 2.0f;
        private const float DETECT_RADIUS = 0.5f;  // Tight radius to trap visitors inside the maw mesh
        private const float DETECT_CENTER_Z = -0.5f;  // Above ground level

        [MenuItem("FaeMaze/Setup Devour Prefab Colliders")]
        public static void SetupColliders()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PREFAB_PATH);

            if (prefabRoot == null)
            {
                Debug.LogError($"[DevourPrefabSetup] Could not load prefab at {PREFAB_PATH}");
                return;
            }

            try
            {
                // Find the Detect child - check multiple possible locations
                Transform detectTransform = null;

                // First try direct child
                detectTransform = prefabRoot.transform.Find("Detect");

                // If not found, search recursively
                if (detectTransform == null)
                {
                    foreach (Transform child in prefabRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name == "Detect")
                        {
                            detectTransform = child;
                            break;
                        }
                    }
                }

                if (detectTransform == null)
                {
                    Debug.LogError("[DevourPrefabSetup] Could not find Detect child in devour prefab!");
                    Debug.Log("Prefab hierarchy:");
                    foreach (Transform t in prefabRoot.GetComponentsInChildren<Transform>(true))
                    {
                        Debug.Log($"  - {t.name}");
                    }
                    return;
                }

                Debug.Log($"[DevourPrefabSetup] Found Detect at: {GetTransformPath(detectTransform)}");

                // Get or add CapsuleCollider
                CapsuleCollider capsule = detectTransform.GetComponent<CapsuleCollider>();
                if (capsule == null)
                {
                    capsule = detectTransform.gameObject.AddComponent<CapsuleCollider>();
                    Debug.Log("[DevourPrefabSetup] Added CapsuleCollider component");
                }

                // Configure as TRIGGER for detection
                // Direction 2 = Z-axis (vertical in this game's coordinate system)
                capsule.isTrigger = true;
                capsule.direction = 2;  // Z-axis
                capsule.center = new Vector3(0, 0, DETECT_CENTER_Z);
                capsule.height = DETECT_HEIGHT;
                capsule.radius = DETECT_RADIUS;

                Debug.Log($"[DevourPrefabSetup] Configured CapsuleCollider: trigger=true, direction=Z, height={DETECT_HEIGHT}, radius={DETECT_RADIUS}");

                // Add kinematic Rigidbody for trigger events to work
                Rigidbody rb = detectTransform.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = detectTransform.gameObject.AddComponent<Rigidbody>();
                    Debug.Log("[DevourPrefabSetup] Added Rigidbody component");
                }
                rb.isKinematic = true;
                rb.useGravity = false;

                // Reset transform to align with world axes (remove any rotation)
                detectTransform.localRotation = Quaternion.identity;
                detectTransform.localPosition = Vector3.zero;
                Debug.Log("[DevourPrefabSetup] Reset Detect transform to identity");

                // Add or get DevourTriggerHandler component
                DevourTriggerHandler handler = detectTransform.GetComponent<DevourTriggerHandler>();
                if (handler == null)
                {
                    handler = detectTransform.gameObject.AddComponent<DevourTriggerHandler>();
                    Debug.Log("[DevourPrefabSetup] Added DevourTriggerHandler component");
                }

                // Remove the MeshFilter and MeshRenderer if present (visual capsule not needed)
                MeshFilter meshFilter = detectTransform.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = detectTransform.GetComponent<MeshRenderer>();
                if (meshFilter != null)
                {
                    Object.DestroyImmediate(meshFilter);
                    Debug.Log("[DevourPrefabSetup] Removed MeshFilter (visual capsule not needed)");
                }
                if (meshRenderer != null)
                {
                    Object.DestroyImmediate(meshRenderer);
                    Debug.Log("[DevourPrefabSetup] Removed MeshRenderer (visual capsule not needed)");
                }

                // Save the prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PREFAB_PATH);

                Debug.Log("[DevourPrefabSetup] Setup complete:");
                Debug.Log($"  - Detect configured as trigger zone");
                Debug.Log($"  - Capsule: height={DETECT_HEIGHT}, radius={DETECT_RADIUS}, center.z={DETECT_CENTER_Z}");
                Debug.Log($"  - Rigidbody: kinematic=true");
                Debug.Log($"  - DevourTriggerHandler component attached");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
        }

        private static string GetTransformPath(Transform t)
        {
            string path = t.name;
            Transform parent = t.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor script to add colliders to grasp prefab finger and thumb bones.
/// Modeled after the heart tongue bone collider system.
/// </summary>
public class GraspPrefabColliderSetup : EditorWindow
{
    // Collider radius matching "thumb 3 contact" existing collider
    private const float BONE_COLLIDER_RADIUS = 0.05f;

    // Bone name patterns to match for colliders
    // Grasp model uses: thumb 1-3, first 1-4 (index), second 1-4 (middle),
    // third 1-4 (ring), fourth 1-4 (pinky)
    private static readonly string[] FINGER_BONE_PATTERNS = new string[]
    {
        "thumb",
        "first",   // index finger
        "second",  // middle finger
        "third",   // ring finger
        "fourth"   // pinky finger
    };

    // Terminal bones (fingertips) that need an additional tip collider
    private static readonly string[] FINGERTIP_BONES = new string[]
    {
        "thumb 3",
        "first 4",
        "second 4",
        "third 4",
        "fourth 4"
    };

    // Offset for tip colliders (along local Y axis, the bone direction in this model)
    private const float TIP_COLLIDER_OFFSET = 0.15f;

    // Forearm capsule collider settings for contact detection
    private const float FOREARM_CAPSULE_RADIUS = 0.15f;
    private const float FOREARM_CAPSULE_HEIGHT = 1.0f;

    [MenuItem("FaeMaze/Setup Grasp Prefab Colliders")]
    public static void ShowWindow()
    {
        GetWindow<GraspPrefabColliderSetup>("Grasp Collider Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Grasp Prefab Bone Collider Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("This will:", EditorStyles.label);
        GUILayout.Label("1. Remove existing 'touch', 'thumb 3 contact', and tip colliders", EditorStyles.label);
        GUILayout.Label("2. Add sphere colliders (radius 0.05, trigger) to:", EditorStyles.label);
        GUILayout.Label("   - thumb 1, thumb 2, thumb 3", EditorStyles.label);
        GUILayout.Label("   - first 1-4, second 1-4, third 1-4, fourth 1-4", EditorStyles.label);
        GUILayout.Label("3. Add tip colliders at end of each fingertip bone", EditorStyles.label);
        GUILayout.Label("4. Add capsule collider to forearm for contact detection", EditorStyles.label);
        GUILayout.Space(10);

        if (GUILayout.Button("Preview Bones (No Changes)"))
        {
            PreviewBones();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Setup Colliders on Prefab"))
        {
            SetupColliders();
        }
    }

    private void PreviewBones()
    {
        string prefabPath = "Assets/Prefabs/Props/grasp.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            Debug.LogError($"Could not load prefab at {prefabPath}");
            return;
        }

        Debug.Log("=== Grasp Prefab Bone Preview ===");

        // Find all transforms in hierarchy
        Transform[] allTransforms = prefab.GetComponentsInChildren<Transform>(true);

        List<string> fingerBones = new List<string>();
        List<string> existingColliders = new List<string>();

        foreach (Transform t in allTransforms)
        {
            string nameLower = t.name.ToLower();

            // Check for existing colliders to remove
            if (t.name == "touch" || t.name == "thumb 3 contact")
            {
                var col = t.GetComponent<Collider>();
                if (col != null)
                {
                    existingColliders.Add($"{t.name} (has {col.GetType().Name})");
                }
            }

            // Check for finger/thumb bones
            foreach (string pattern in FINGER_BONE_PATTERNS)
            {
                if (nameLower.Contains(pattern))
                {
                    var existingCol = t.GetComponent<SphereCollider>();
                    string colStatus = existingCol != null ? " [HAS COLLIDER]" : "";
                    fingerBones.Add($"{t.name}{colStatus}");
                    break;
                }
            }
        }

        Debug.Log($"Found {existingColliders.Count} existing collider objects to remove:");
        foreach (string s in existingColliders)
        {
            Debug.Log($"  - {s}");
        }

        Debug.Log($"\nFound {fingerBones.Count} finger/thumb bones:");
        foreach (string s in fingerBones)
        {
            Debug.Log($"  - {s}");
        }
    }

    private void SetupColliders()
    {
        string prefabPath = "Assets/Prefabs/Props/grasp.prefab";

        // Load the prefab for editing
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        if (prefabRoot == null)
        {
            Debug.LogError($"Could not load prefab contents at {prefabPath}");
            return;
        }

        try
        {
            int removedCount = 0;
            int addedCount = 0;

            // Find all transforms in hierarchy
            Transform[] allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

            // First pass: Remove existing "touch", "thumb 3 contact", and tip collider GameObjects
            List<GameObject> objectsToRemove = new List<GameObject>();
            foreach (Transform t in allTransforms)
            {
                if (t.name == "touch" || t.name == "thumb 3 contact" || t.name.EndsWith("_tip"))
                {
                    objectsToRemove.Add(t.gameObject);
                }
            }

            foreach (GameObject obj in objectsToRemove)
            {
                Debug.Log($"Removing existing object: {obj.name}");
                DestroyImmediate(obj);
                removedCount++;
            }

            // Re-get transforms after removal
            allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);

            // Second pass: Add colliders to finger/thumb bones
            foreach (Transform t in allTransforms)
            {
                string nameLower = t.name.ToLower();

                // Check if this is a finger/thumb bone
                bool isFingerBone = false;
                foreach (string pattern in FINGER_BONE_PATTERNS)
                {
                    if (nameLower.Contains(pattern))
                    {
                        isFingerBone = true;
                        break;
                    }
                }

                if (!isFingerBone) continue;

                // Remove any existing sphere collider
                SphereCollider existingCol = t.GetComponent<SphereCollider>();
                if (existingCol != null)
                {
                    DestroyImmediate(existingCol);
                }

                // Add new sphere collider
                SphereCollider newCol = t.gameObject.AddComponent<SphereCollider>();
                newCol.radius = BONE_COLLIDER_RADIUS;
                newCol.isTrigger = true;
                newCol.center = Vector3.zero;

                Debug.Log($"Added collider to bone: {t.name}");
                addedCount++;
            }

            // Third pass: Add tip colliders to fingertip bones
            int tipCount = 0;
            allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                // Check if this is a fingertip bone
                bool isFingertip = false;
                foreach (string tipName in FINGERTIP_BONES)
                {
                    if (t.name == tipName)
                    {
                        isFingertip = true;
                        break;
                    }
                }

                if (!isFingertip) continue;

                // Create a child GameObject for the tip collider
                GameObject tipObj = new GameObject($"{t.name}_tip");
                tipObj.transform.SetParent(t, false);
                // Offset along local Y axis (bone direction in this model)
                tipObj.transform.localPosition = new Vector3(0f, TIP_COLLIDER_OFFSET, 0f);
                tipObj.transform.localRotation = Quaternion.identity;
                tipObj.transform.localScale = Vector3.one;

                // Add sphere collider
                SphereCollider tipCol = tipObj.AddComponent<SphereCollider>();
                tipCol.radius = BONE_COLLIDER_RADIUS;
                tipCol.isTrigger = true;
                tipCol.center = Vector3.zero;

                Debug.Log($"Added tip collider to: {t.name}");
                tipCount++;
            }

            // Fourth pass: Add capsule collider to forearm for contact detection
            bool forearmColliderAdded = false;
            allTransforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name == "forearm")
                {
                    // Remove existing capsule collider if present
                    CapsuleCollider existingCapsule = t.GetComponent<CapsuleCollider>();
                    if (existingCapsule != null)
                    {
                        DestroyImmediate(existingCapsule);
                    }

                    // Add new capsule collider
                    CapsuleCollider forearmCol = t.gameObject.AddComponent<CapsuleCollider>();
                    forearmCol.radius = FOREARM_CAPSULE_RADIUS;
                    forearmCol.height = FOREARM_CAPSULE_HEIGHT;
                    forearmCol.center = new Vector3(0f, 0.5f, -0.05f);
                    forearmCol.direction = 1; // Y-axis aligned
                    forearmCol.isTrigger = true;

                    Debug.Log($"Added capsule collider to forearm");
                    forearmColliderAdded = true;
                    break;
                }
            }

            // Save the modified prefab
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

            Debug.Log($"=== Grasp Prefab Collider Setup Complete ===");
            Debug.Log($"Removed {removedCount} existing collider objects");
            Debug.Log($"Added {addedCount} bone colliders");
            Debug.Log($"Added {tipCount} fingertip colliders");
            Debug.Log($"Forearm capsule collider: {(forearmColliderAdded ? "Added" : "Not found")}");
        }
        finally
        {
            // Unload the prefab contents
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.Refresh();
    }
}

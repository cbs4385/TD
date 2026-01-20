using UnityEngine;

namespace FaeMaze.Props
{
    /// <summary>
    /// Initializes fairy light animation on ring objects in the scene.
    /// Add this component to any GameObject to automatically find and animate
    /// all "ring" prefab instances with FairyRingSphere behavior.
    /// </summary>
    public class FairyLightInitializer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("If set, only initialize this specific ring. Otherwise finds all rings in scene.")]
        private Transform targetRing;

        private void Start()
        {
            if (targetRing != null)
            {
                SetupRing(targetRing);
            }
            else
            {
                // Find all objects named "ring" in the scene
                var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    if (t.name.ToLower().Contains("ring"))
                    {
                        SetupRing(t);
                    }
                }
            }
        }

        private void SetupRing(Transform ring)
        {
            // Find all Sphere children and add FairyRingSphere components
            var children = ring.GetComponentsInChildren<Transform>();
            int sphereIndex = 0;
            int totalSpheres = 0;

            // First pass: count spheres
            foreach (var t in children)
            {
                if (t != ring && t.name.Contains("Sphere"))
                {
                    totalSpheres++;
                }
            }

            if (totalSpheres == 0)
            {
                return;
            }

            // Second pass: setup spheres
            foreach (var t in children)
            {
                if (t != ring && t.name.Contains("Sphere"))
                {
                    // Clean up child Trail objects - TrailRenderer needs to be on the moving object itself
                    var childTrail = t.Find("Trail");
                    if (childTrail != null)
                    {
                        Destroy(childTrail.gameObject);
                    }

                    // Add FairyRingSphere component if not already present
                    var sphereScript = t.GetComponent<FairyRingSphere>();
                    if (sphereScript == null)
                    {
                        sphereScript = t.gameObject.AddComponent<FairyRingSphere>();
                    }

                    // Distribute colors evenly across the rainbow
                    int colorIndex = sphereIndex % 7;
                    sphereScript.SetStartingColorIndex(colorIndex);

                    sphereIndex++;
                }
            }
        }
    }
}

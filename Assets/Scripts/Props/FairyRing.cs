using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Fairy Ring that entrances and slows visitors passing through.
    /// Once entranced, a visitor remains in that state permanently (design choice).
    /// Requires a Collider component set to isTrigger = true for detection.
    /// Also manages rainbow-colored animated spheres within the ring.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FairyRing : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Entrancement Settings")]
        [SerializeField]
        [Tooltip("Speed multiplier applied to visitors inside the ring (0.5 = 50% speed)")]
        private float slowFactor = 0.5f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Pulse the sprite scale for visual effect (disabled by default for 3D ring prefab)")]
        private bool enablePulse = false;

        [SerializeField]
        [Tooltip("Pulse speed (higher = faster pulsing)")]
        private float pulseSpeed = 2f;

        [SerializeField]
        [Tooltip("Pulse magnitude (0.1 = 10% scale variation)")]
        private float pulseMagnitude = 0.1f;

        [Header("Sphere Setup")]
        [SerializeField]
        [Tooltip("Automatically add FairyRingSphere components and assign rainbow colors on start")]
        private bool autoSetupSpheres = true;

        #endregion

        #region Private Fields

        private Vector3 originalScale;

        #endregion

        #region Properties

        /// <summary>Gets the slow factor applied to visitors</summary>
        public float SlowFactor => slowFactor;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            originalScale = transform.localScale;

            if (autoSetupSpheres)
            {
                SetupSpheres();
            }
        }

        private void Update()
        {
            if (enablePulse)
            {
                UpdatePulse();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null)
            {
                OnVisitorEnter(visitor);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null)
            {
                OnVisitorExit(visitor);
            }
        }

        #endregion

        #region Visitor Interaction

        /// <summary>
        /// Called when a visitor enters the Fairy Ring.
        /// Marks them as entranced and applies speed reduction.
        /// </summary>
        /// <param name="visitor">The visitor entering the ring</param>
        private void OnVisitorEnter(VisitorControllerBase visitor)
        {

            // Mark as entranced (permanent effect - once entranced, always entranced)
            visitor.SetEntranced(true);

            // Apply slow effect
            visitor.SpeedMultiplier = slowFactor;

        }

        /// <summary>
        /// Called when a visitor exits the Fairy Ring.
        /// Restores normal speed but keeps entranced flag set.
        /// </summary>
        /// <param name="visitor">The visitor exiting the ring</param>
        private void OnVisitorExit(VisitorControllerBase visitor)
        {

            // Restore normal speed
            visitor.SpeedMultiplier = 1f;

            // Design choice: Keep entranced flag set permanently
            // Once a visitor passes through a Fairy Ring, they remain marked as entranced
            // This could be used for future mechanics (e.g., entranced visitors give more essence)

        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// Creates a pulsing visual effect for the Fairy Ring.
        /// </summary>
        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = originalScale * (1f + pulse);
        }

        #endregion

        #region Sphere Setup

        /// <summary>
        /// Finds all Sphere children and adds/configures FairyRingSphere components.
        /// Distributes starting colors evenly across the rainbow.
        /// </summary>
        [ContextMenu("Setup Spheres")]
        public void SetupSpheres()
        {
            // Find all sphere children (direct or nested under Cylinder)
            var children = GetComponentsInChildren<Transform>();
            int sphereIndex = 0;
            int totalSpheres = 0;

            // First pass: count spheres
            foreach (var t in children)
            {
                if (t != transform && t.name.Contains("Sphere"))
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
                if (t != transform && t.name.Contains("Sphere"))
                {
                    // Clean up child Trail objects - TrailRenderer needs to be on the moving object itself
                    var childTrail = t.Find("Trail");
                    if (childTrail != null)
                    {
                        if (Application.isPlaying)
                        {
                            Destroy(childTrail.gameObject);
                        }
                        else
                        {
                            DestroyImmediate(childTrail.gameObject);
                        }
                    }

                    // Add or get FairyRingSphere component
                    var sphereScript = t.GetComponent<FairyRingSphere>();
                    if (sphereScript == null)
                    {
                        sphereScript = t.gameObject.AddComponent<FairyRingSphere>();
                    }

                    // Distribute colors evenly - with 9 spheres and 7 colors, some will repeat
                    int colorIndex = sphereIndex % 7;
                    sphereScript.SetStartingColorIndex(colorIndex);

                    sphereIndex++;
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.3f);
            DrawColliderGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.6f);
            DrawColliderGizmo();

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }

        private void DrawColliderGizmo()
        {
            var sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                float radius = sphereCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
                Gizmos.DrawWireSphere(transform.position + sphereCollider.center, radius);
                return;
            }

            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                Vector3 center = transform.position + capsuleCollider.center;
                float radius = capsuleCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.z);
                float height = capsuleCollider.height * transform.localScale.y;
                Gizmos.DrawWireSphere(center + Vector3.up * (height / 2f - radius), radius);
                Gizmos.DrawWireSphere(center - Vector3.up * (height / 2f - radius), radius);
            }

            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                Vector3 size = Vector3.Scale(boxCollider.size, transform.localScale);
                Gizmos.DrawWireCube(transform.position + boxCollider.center, size);
            }
        }

        #endregion
    }
}

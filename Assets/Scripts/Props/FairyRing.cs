using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Audio;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Fairy Ring that fascinates visitors who pass through.
    /// Fascinated visitors are slowed to 50% speed and circle the ring for 30 seconds.
    /// After fascination ends, the visitor becomes lost and resumes pathing to their destination.
    /// Requires a Collider component set to isTrigger = true for detection.
    /// Also manages rainbow-colored animated spheres within the ring.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class FairyRing : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Fascination Settings")]
        [SerializeField]
        [Tooltip("Speed multiplier applied to fascinated visitors (0.5 = 50% speed)")]
        private float slowFactor = 0.5f;

        [SerializeField]
        [Tooltip("Duration visitors circle the ring while fascinated (seconds)")]
        private float fascinationDuration = 30f;

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
        private PropAudioSource propAudio;

        #endregion

        #region Properties

        /// <summary>Gets the slow factor applied to visitors</summary>
        public float SlowFactor => slowFactor;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            originalScale = transform.localScale;

            // Ensure Rigidbody is configured for trigger detection
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Ensure collider is a trigger
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
            }

            if (autoSetupSpheres)
            {
                SetupSpheres();
            }

            // Setup audio
            SetupAudio();
        }

        private void SetupAudio()
        {
            propAudio = GetComponent<PropAudioSource>();
            if (propAudio == null)
            {
                propAudio = gameObject.AddComponent<PropAudioSource>();
            }
            propAudio.SetSoundType(PropAudioSource.PropSoundType.FairyRing);
            propAudio.SetMaxDistance(10f);
            // Only play sound when actively fascinating a visitor
            propAudio.SetActiveStateCallback(HasFascinatedVisitor);
        }

        /// <summary>
        /// Returns true if any visitor is currently fascinated by this ring.
        /// Used by PropAudioSource to determine when sound should play.
        /// </summary>
        private bool HasFascinatedVisitor()
        {
            // Use VisitorRegistry to include all visitor types
            foreach (var visitor in VisitorRegistry.All)
            {
                if (visitor != null && visitor.CurrentFairyRing == this)
                {
                    return true;
                }
            }
            return false;
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

        private void OnDisable()
        {
            // Release all visitors fascinated by this ring before it's destroyed
            ReleaseAllFascinatedVisitors();
        }

        #endregion

        #region Visitor Interaction

        /// <summary>
        /// Releases all visitors currently fascinated by this ring.
        /// Called when the ring is destroyed or disabled.
        /// </summary>
        public void ReleaseAllFascinatedVisitors()
        {
            // Find all visitors and release any fascinated by this ring
            var allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var visitor in allVisitors)
            {
                if (visitor != null && visitor.CurrentFairyRing == this)
                {
                    visitor.EndRingFascination();
                }
            }
        }

        /// <summary>
        /// Called when a visitor enters the Fairy Ring.
        /// The visitor becomes fascinated and will circle the ring for the fascination duration.
        /// </summary>
        /// <param name="visitor">The visitor entering the ring</param>
        private void OnVisitorEnter(VisitorControllerBase visitor)
        {
            // Make visitor fascinated by this ring - they will circle it
            visitor.BecomeFascinatedByRing(this, fascinationDuration, slowFactor);
        }

        /// <summary>
        /// Called when a visitor exits the Fairy Ring trigger.
        /// Clears any immunity the visitor has to this ring.
        /// </summary>
        /// <param name="visitor">The visitor exiting the ring trigger</param>
        private void OnVisitorExit(VisitorControllerBase visitor)
        {
            // Clear immunity so visitor can be fascinated again if they re-enter
            visitor.ClearFairyRingImmunity(this);
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

using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Fairy Ring that entrances and slows visitors passing through.
    /// Once entranced, a visitor remains in that state permanently (design choice).
    /// </summary>
    public class FairyRing : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Entrancement Settings")]
        [SerializeField]
        [Tooltip("Speed multiplier applied to visitors inside the ring (0.5 = 50% speed)")]
        private float slowFactor = 0.5f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Pulse the sprite scale for visual effect")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed (higher = faster pulsing)")]
        private float pulseSpeed = 2f;

        [SerializeField]
        [Tooltip("Pulse magnitude (0.1 = 10% scale variation)")]
        private float pulseMagnitude = 0.1f;

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
        }

        private void Update()
        {
            if (enablePulse)
            {
                UpdatePulse();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if a visitor entered the ring
            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null)
            {
                OnVisitorEnter(visitor);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            // Check if a visitor left the ring
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
        private void OnVisitorEnter(VisitorController visitor)
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
        private void OnVisitorExit(VisitorController visitor)
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

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw ring area
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.3f); // Purple semi-transparent

            // Draw circle representing trigger area
            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                float radius = circleCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
                DrawCircle(transform.position, radius, 24);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw brighter when selected
            Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.6f);

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                float radius = circleCollider.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
                DrawCircle(transform.position, radius, 32);

                // Draw center point
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, 0.2f);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        #endregion
    }
}

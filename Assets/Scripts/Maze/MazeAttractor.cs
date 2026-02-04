using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Utilities;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Base class for props that influence visitor behavior through world-space triggers.
    /// Uses trigger colliders for visitor detection and interaction.
    /// </summary>
    public class MazeAttractor : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Attraction Settings")]
        [SerializeField]
        [Tooltip("Radius of attraction influence in world units")]
        private float radius = 3f;

        [SerializeField]
        [Tooltip("Strength of attraction (used for visitor behavior weighting)")]
        private float attractionStrength = 0.5f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw attraction radius in Scene view")]
        private bool showDebugRadius = true;

        [Header("Visitor Interaction")]
        [SerializeField]
        [Tooltip("Slow factor applied to visitors within radius (0.5 = half speed)")]
        private float visitorSlowFactor = 0.5f;

        [SerializeField]
        [Tooltip("Enable trigger-based visitor slowing")]
        private bool enableVisitorSlowing = true;

        [Header("Fascination (FaeLantern)")]
        [SerializeField]
        [Tooltip("Enable fascination mechanic (visitors retarget to attractor then wander)")]
        private bool enableFascination = false;

        #endregion

        #region Private Fields

        private MazeGridBehaviour gridBehaviour;
        private SphereCollider triggerCollider;

        #endregion

        #region Properties

        /// <summary>Gets the attraction radius in world units</summary>
        public float Radius => radius;

        /// <summary>Gets the attraction strength</summary>
        public float AttractionStrength => attractionStrength;

        /// <summary>Gets the world position of this attractor</summary>
        public Vector3 WorldPosition => transform.position;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Find the MazeGridBehaviour in the scene
            gridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
        }

        private void Start()
        {
            // Setup trigger collider for visitor interaction (either slowing or fascination)
            if (enableVisitorSlowing || enableFascination)
            {
                SetupTriggerCollider();
            }

            // Trigger path recalculation for all active visitors
            RecalculateAllVisitorPaths();
        }

        private void SetupTriggerCollider()
        {
            gameObject.AddKinematicRigidbody();

            triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<SphereCollider>();
            }

            triggerCollider.isTrigger = true;
            triggerCollider.radius = radius;
            triggerCollider.center = Vector3.zero; // XY-plane collision
        }

        private void OnTriggerEnter(Collider other)
        {
            var visitor = other.GetComponent<Visitors.VisitorController>();
            if (visitor != null)
            {
                if (enableVisitorSlowing)
                {
                    visitor.SpeedMultiplier = visitorSlowFactor;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!enableVisitorSlowing)
                return;

            var visitor = other.GetComponent<Visitors.VisitorController>();
            if (visitor != null)
            {
                visitor.SpeedMultiplier = 1f;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if a world position is within this attractor's influence radius.
        /// </summary>
        /// <param name="worldPos">World position to check</param>
        /// <returns>True if within influence radius</returns>
        public bool IsPositionInInfluence(Vector3 worldPos)
        {
            float distance = Vector3.Distance(transform.position, worldPos);
            return distance <= radius;
        }

        #endregion

        #region Visitor Path Recalculation

        private bool IsVisitorMoving(Visitors.VisitorController.VisitorState state)
        {
            return state == Visitors.VisitorController.VisitorState.Walking
                || state == Visitors.VisitorController.VisitorState.Fascinated
                || state == Visitors.VisitorController.VisitorState.Confused
                || state == Visitors.VisitorController.VisitorState.Frightened;
        }

        /// <summary>
        /// Triggers all active visitors to recalculate their paths.
        /// Called when a new attractor is placed so visitors can respond to the new attraction.
        /// </summary>
        private void RecalculateAllVisitorPaths()
        {
            // Find all active visitors in the scene
            var visitors = FindObjectsByType<Visitors.VisitorController>(FindObjectsSortMode.None);

            foreach (var visitor in visitors)
            {
                if (visitor != null && IsVisitorMoving(visitor.State))
                {
                    visitor.RecalculatePath();
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showDebugRadius)
                return;

            // Draw attraction radius
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange semi-transparent

            // Draw circle at attractor position
            DrawCircle(transform.position, radius, 32);

            // Draw filled circle for visual emphasis
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            DrawFilledCircle(transform.position, radius, 16);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugRadius)
                return;

            // Draw brighter when selected
            Gizmos.color = new Color(1f, 0.7f, 0f, 0.6f);
            DrawCircle(transform.position, radius, 32);

            // Draw attractor center
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        private void DrawCircle(Vector3 center, float circleRadius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(circleRadius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * circleRadius, Mathf.Sin(angle) * circleRadius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        private void DrawFilledCircle(Vector3 center, float circleRadius, int segments)
        {
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep * Mathf.Deg2Rad;
                float angle2 = (i + 1) * angleStep * Mathf.Deg2Rad;

                Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * circleRadius, Mathf.Sin(angle1) * circleRadius, 0);
                Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * circleRadius, Mathf.Sin(angle2) * circleRadius, 0);

                Gizmos.DrawLine(center, point1);
                Gizmos.DrawLine(center, point2);
                Gizmos.DrawLine(point1, point2);
            }
        }

        #endregion
    }
}

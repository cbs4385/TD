using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Audio;
using FaeMaze.Utilities;

namespace FaeMaze.Props
{
    /// <summary>
    /// FaeLantern that fascinates visitors using world-space area of effect.
    /// Visitors entering the influence area abandon their path, move to the lantern,
    /// stand still for 2 seconds, then wander randomly at intersections.
    /// </summary>
    public class FaeLantern : RegistryComponent<FaeLantern>
    {
        #region Serialized Fields

        [Header("Influence Settings")]
        [SerializeField]
        [Tooltip("Radius of influence in world units")]
        private float influenceRadius = 3f;

        [SerializeField]
        [Tooltip("Duration in seconds that visitor stands fascinated at the lantern")]
        private float fascinationDuration = 2f;

        [SerializeField]
        [Tooltip("Probability (0-1) that a visitor becomes fascinated when entering influence")]
        [Range(0f, 1f)]
        private float procChance = 1.0f;

        [SerializeField]
        [Tooltip("Cooldown in seconds before a visitor can be fascinated again")]
        private float cooldownSec = 5.0f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw the influence area in Scene view")]
        private bool debugDrawInfluence = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour _gridBehaviour;
        private Animator _animator;
        private PropAudioSource _propAudio;

        private const string DirectionParameter = "Direction";

        #endregion

        #region Properties

        /// <summary>Gets the world position of this lantern</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Gets the fascination duration in seconds</summary>
        public float FascinationDuration => fascinationDuration;

        /// <summary>Gets the proc chance for fascination (0-1)</summary>
        public float ProcChance => procChance;

        /// <summary>Gets the cooldown in seconds before re-fascination</summary>
        public float CooldownSec => cooldownSec;

        /// <summary>Gets the influence radius in world units</summary>
        public float InfluenceRadius => influenceRadius;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            // Find the MazeGridBehaviour in the scene
            _gridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (_gridBehaviour == null)
            {
                return;
            }

            SetIdleDirection();

            // Create exclusion zone to prevent visitors from entering lantern center
            CreateExclusionZone();

            // Setup audio
            SetupAudio();
        }

        private void SetupAudio()
        {
            _propAudio = PropAudioSource.EnsureOnGameObject(gameObject, PropAudioSource.PropSoundType.Lantern);
            _propAudio.SetMaxDistance(influenceRadius * 1.5f);
            // Only play sound when actively fascinating a visitor
            _propAudio.SetActiveStateCallback(HasFascinatedVisitor);
        }

        /// <summary>
        /// Returns true if any visitor is currently being targeted or fascinated by this lantern.
        /// Used by PropAudioSource to determine when sound should play.
        /// Sound plays while: visitor is walking toward lantern OR standing fascinated at lantern.
        /// </summary>
        private bool HasFascinatedVisitor()
        {
            // Use VisitorRegistry to include all visitor types (not just base VisitorController)
            foreach (var visitor in FaeMaze.Visitors.VisitorRegistry.All)
            {
                if (visitor != null && visitor.CurrentFaeLantern == this)
                {
                    return true;
                }
            }
            return false;
        }

        private void CreateExclusionZone()
        {
            // Check if exclusion zone already exists
            if (GetComponentInChildren<LanternExclusionZone>() != null) return;

            // Create child object for exclusion zone
            GameObject zoneObj = new GameObject("ExclusionZone");
            zoneObj.transform.SetParent(transform);
            zoneObj.transform.localPosition = Vector3.zero;

            // Add kinematic Rigidbody for trigger detection
            zoneObj.AddKinematicRigidbody();

            // Add sphere collider and exclusion zone component
            var collider = zoneObj.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1f; // 1 unit exclusion radius

            zoneObj.AddComponent<LanternExclusionZone>();
        }

        /// <summary>
        /// Called when unregistered from the static collection.
        /// Releases all visitors fascinated by this lantern.
        /// </summary>
        protected override void OnUnregistered()
        {
            ReleaseAllFascinatedVisitors();
        }

        /// <summary>
        /// Releases all visitors currently fascinated by this lantern.
        /// Called when the lantern is destroyed or disabled.
        /// </summary>
        public void ReleaseAllFascinatedVisitors()
        {
            // Find all visitors and release any fascinated by this lantern
            var allVisitors = FindObjectsByType<FaeMaze.Visitors.VisitorControllerBase>(FindObjectsSortMode.None);
            foreach (var visitor in allVisitors)
            {
                if (visitor != null && visitor.CurrentFaeLantern == this)
                {
                    visitor.EndLanternFascination();
                }
            }
        }

        private void Update()
        {
            UpdateDirectionToClosestVisitor();
        }

        #endregion

        #region Influence Checks

        /// <summary>
        /// Checks if a world position is within this lantern's influence area.
        /// </summary>
        /// <param name="worldPos">World position to check</param>
        /// <returns>True if the position is within influence radius</returns>
        public bool IsPositionInInfluence(Vector3 worldPos)
        {
            float distance = Vector3.Distance(transform.position, worldPos);
            return distance <= influenceRadius;
        }

        #endregion

        #region Animation Control

        /// <summary>
        /// Updates the animator direction to point toward the closest unfascinated visitor in range.
        /// Sets direction to idle (0) if no unfascinated visitors are in the influence area.
        /// Uses cached visitor registry for efficient lookup.
        /// </summary>
        private void UpdateDirectionToClosestVisitor()
        {
            if (_animator == null)
            {
                return;
            }

            FaeMaze.Visitors.VisitorController closestVisitor = null;
            float closestDistance = float.MaxValue;

            // Iterate through cached visitor registry instead of FindObjectsByType
            foreach (var visitor in FaeMaze.Visitors.VisitorController.All)
            {
                if (visitor == null)
                    continue;

                // Skip fascinated visitors
                if (visitor.IsFascinated)
                    continue;

                // Check if visitor is in influence area using world-space distance
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= influenceRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestVisitor = visitor;
                }
            }

            // Update direction based on closest visitor
            if (closestVisitor != null)
            {
                SetInteractionDirection(closestVisitor.transform.position);
            }
            else
            {
                // No unfascinated visitors in range - set to idle
                SetIdleDirection();
            }
        }

        /// <summary>
        /// Sets the animator Direction parameter based on where the visitor is relative to the lantern.
        /// </summary>
        /// <param name="visitorWorldPosition">World position of the interacting visitor.</param>
        public void SetInteractionDirection(Vector3 visitorWorldPosition)
        {
            if (_animator == null)
            {
                return;
            }

            Vector3 delta = visitorWorldPosition - transform.position;
            int directionValue = 0; // Idle by default

            // Determine dominant axis
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            {
                directionValue = delta.y > 0 ? 1 : 2; // Up : Down
            }
            else if (Mathf.Abs(delta.x) > 0.01f)
            {
                directionValue = delta.x < 0 ? 3 : 4; // Left : Right
            }

            _animator.SetInteger(DirectionParameter, directionValue);
        }

        /// <summary>
        /// Resets the animator Direction parameter to idle (0).
        /// </summary>
        public void SetIdleDirection()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetInteger(DirectionParameter, 0);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugDrawInfluence)
            {
                return;
            }

            // Draw influence area as a world-space circle
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.2f); // Semi-transparent golden
            DrawCircleGizmo(transform.position, influenceRadius, 32);

            // Draw lantern center
            Gizmos.color = new Color(1f, 0.7f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawInfluence)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.4f);
            DrawCircleGizmo(transform.position, influenceRadius, 32);

            // Draw influence radius as wire sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, influenceRadius);
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments)
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

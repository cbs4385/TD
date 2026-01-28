using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Props;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Component that makes a visitor follow a Willow-the-Wisp.
    /// Attached dynamically when a wisp captures a visitor.
    /// The visitor will follow the wisp until they reach a PukaHazard
    /// or become confused (escape condition).
    /// </summary>
    public class FollowWispBehavior : MonoBehaviour
    {
        #region Private Fields

        private WillowTheWisp targetWisp;
        private VisitorControllerBase visitorController;
        private Rigidbody rb3D;
        private bool isFollowing;

        // Physics-based movement: calculate in Update, apply in FixedUpdate
        private Vector3 desiredPosition;
        private bool hasDesiredPosition;

        // Path-based following
        private List<Vector3> followPath = new List<Vector3>();
        private int followPathIndex = 0;
        private Vector3 lastWispPosition;
        private float pathRecalculateTimer = 0f;
        private const float PATH_RECALCULATE_INTERVAL = 0.5f; // Recalculate path every 0.5 seconds
        private const float WISP_MOVE_THRESHOLD = 1.0f; // Recalculate if wisp moved more than this distance
        private const float WAYPOINT_REACHED_DISTANCE = 0.5f;

        [SerializeField]
        [Tooltip("Distance to maintain from the wisp")]
        private float followDistance = 0.5f;

        [SerializeField]
        [Tooltip("Speed multiplier when following wisp")]
        private float followSpeedMultiplier = 1f;

        #endregion

        #region Properties

        /// <summary>Gets whether this visitor is currently following a wisp</summary>
        public bool IsFollowing => isFollowing && targetWisp != null;

        /// <summary>Gets the wisp being followed</summary>
        public WillowTheWisp TargetWisp => targetWisp;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            visitorController = GetComponent<VisitorControllerBase>();
            rb3D = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!isFollowing || targetWisp == null)
            {
                // Wisp was destroyed or finished leading
                StopFollowing();
                return;
            }

            // Check if the wisp is still luring (in Reacting state)
            if (!targetWisp.IsLuring)
            {
                // Wisp stopped luring, stop following
                StopFollowing();
                return;
            }

            // Follow the wisp
            UpdateFollowing();
        }

        /// <summary>
        /// FixedUpdate handles physics-based movement.
        /// Movement is calculated in Update() and stored in desiredPosition,
        /// then applied here using MovePosition() which works correctly with non-kinematic Rigidbodies.
        /// </summary>
        private void FixedUpdate()
        {
            if (rb3D != null && hasDesiredPosition)
            {
                // Wake the Rigidbody in case it's sleeping
                if (rb3D.IsSleeping())
                {
                    rb3D.WakeUp();
                }

                // Use MovePosition only - do NOT override with transform.position
                // This allows the physics engine to handle collision resolution
                rb3D.MovePosition(desiredPosition);
                hasDesiredPosition = false;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start following the specified wisp.
        /// </summary>
        /// <param name="wisp">The wisp to follow</param>
        public void StartFollowing(WillowTheWisp wisp)
        {
            if (wisp == null)
            {
                return;
            }

            targetWisp = wisp;
            isFollowing = true;
            lastWispPosition = wisp.transform.position;
            pathRecalculateTimer = 0f;

            // Build initial path to the wisp
            RecalculatePath();
        }

        /// <summary>
        /// Stop following the wisp and return to normal behavior.
        /// </summary>
        public void StopFollowing()
        {
            if (!isFollowing)
                return;

            isFollowing = false;
            targetWisp = null;

            // Remove this component
            Destroy(this);
        }

        #endregion

        #region Following Logic

        private void UpdateFollowing()
        {
            if (visitorController == null || targetWisp == null)
                return;

            Vector3 wispPosition = targetWisp.transform.position;

            // Check if we need to recalculate the path
            pathRecalculateTimer += Time.deltaTime;
            float wispMoved = Vector3.Distance(wispPosition, lastWispPosition);

            if (pathRecalculateTimer >= PATH_RECALCULATE_INTERVAL || wispMoved > WISP_MOVE_THRESHOLD)
            {
                RecalculatePath();
                pathRecalculateTimer = 0f;
                lastWispPosition = wispPosition;
            }

            // If no path or reached end of path, just stay put
            if (followPath == null || followPath.Count == 0 || followPathIndex >= followPath.Count)
            {
                return;
            }

            // Get current waypoint
            Vector3 currentWaypoint = followPath[followPathIndex];

            // Check distance to wisp - if close enough, stop moving
            float distanceToWisp = Vector3.Distance(transform.position, wispPosition);
            if (distanceToWisp <= followDistance)
            {
                return;
            }

            // Check if we've reached the current waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);
            if (distanceToWaypoint <= WAYPOINT_REACHED_DISTANCE)
            {
                followPathIndex++;
                if (followPathIndex >= followPath.Count)
                {
                    return;
                }
                currentWaypoint = followPath[followPathIndex];
            }

            // Move toward current waypoint
            Vector3 direction = (currentWaypoint - transform.position).normalized;
            float speed = visitorController.MoveSpeed * followSpeedMultiplier;
            Vector3 newPosition = transform.position + direction * speed * Time.deltaTime;

            // Update facing direction
            visitorController.ApplyExternalAnimatorDirection(direction);

            // Record move cause for diagnostic logging
            visitorController.RecordMoveCause($"FollowWisp(waypoint={followPathIndex}/{followPath.Count})");

            // Store position for physics to apply in FixedUpdate
            desiredPosition = newPosition;
            hasDesiredPosition = true;
        }

        /// <summary>
        /// Recalculates the path from the visitor to the wisp using proper pathfinding.
        /// </summary>
        private void RecalculatePath()
        {
            if (visitorController == null || targetWisp == null)
            {
                followPath.Clear();
                followPathIndex = 0;
                return;
            }

            // Use the visitor's BuildWorldPath method to get a proper walkable path
            followPath = visitorController.BuildWorldPath(transform.position, targetWisp.transform.position);
            followPathIndex = 0;
        }

        #endregion
    }
}

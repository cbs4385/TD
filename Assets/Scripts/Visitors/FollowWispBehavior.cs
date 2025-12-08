using UnityEngine;
using FaeMaze.Props;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Component that makes a visitor follow a Willow-the-Wisp.
    /// Attached dynamically when a wisp captures a visitor.
    /// </summary>
    public class FollowWispBehavior : MonoBehaviour
    {
        #region Private Fields

        private WillowTheWisp targetWisp;
        private VisitorController visitorController;
        private bool isFollowing;

        [SerializeField]
        [Tooltip("Distance to maintain from the wisp")]
        private float followDistance = 0.3f;

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
            visitorController = GetComponent<VisitorController>();
        }

        private void Update()
        {
            if (!isFollowing || targetWisp == null)
            {
                // Wisp was destroyed or finished leading
                StopFollowing();
                return;
            }

            // Follow the wisp
            UpdateFollowing();
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

            // Get distance to wisp
            float distance = Vector3.Distance(transform.position, targetWisp.transform.position);

            // Face toward the wisp so animations match the leading direction
            Vector2 directionToWisp = targetWisp.transform.position - transform.position;
            visitorController.ApplyExternalAnimatorDirection(directionToWisp);

            // Only move if we're too far from the wisp
            if (distance > followDistance)
            {
                // Move toward wisp
                Vector3 direction = (targetWisp.transform.position - transform.position).normalized;
                float speed = visitorController.MoveSpeed * followSpeedMultiplier;

                Vector3 newPosition = transform.position + direction * speed * Time.deltaTime;

                // Use rigidbody if available for proper collision detection
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.MovePosition(newPosition);
                    Physics2D.SyncTransforms();
                }
                else
                {
                    transform.position = newPosition;
                }
            }
        }

        #endregion
    }
}

using UnityEngine;

namespace FaeMaze.UI
{
    /// <summary>
    /// Makes a GameObject always face the camera. Useful for world-space UI elements
    /// like floating text, health bars, or indicators in a 3D environment.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Billboard Settings")]
        [SerializeField]
        [Tooltip("The camera to face. If null, will use Camera.main")]
        private Camera targetCamera;

        [SerializeField]
        [Tooltip("Lock the Y-axis rotation to prevent tilting")]
        private bool lockYAxis = true;

        [SerializeField]
        [Tooltip("Reverse the facing direction")]
        private bool reverseFacing = false;

        #endregion

        #region Private Fields

        private Transform cameraTransform;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the target camera
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                cameraTransform = targetCamera.transform;
            }
        }

        private void LateUpdate()
        {
            // LateUpdate ensures this runs after camera movement
            if (cameraTransform == null)
            {
                // Try to re-acquire camera if it was null
                if (targetCamera == null)
                {
                    targetCamera = Camera.main;
                }

                if (targetCamera != null)
                {
                    cameraTransform = targetCamera.transform;
                }
                else
                {
                    return;
                }
            }

            // Calculate the direction to face
            Vector3 directionToCamera = cameraTransform.position - transform.position;

            if (lockYAxis)
            {
                // Keep Y axis locked (prevents tilting up/down)
                directionToCamera.y = 0f;
            }

            // If we have a valid direction, apply rotation
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(
                    reverseFacing ? -directionToCamera : directionToCamera
                );

                transform.rotation = targetRotation;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the target camera to face.
        /// </summary>
        /// <param name="camera">The camera to face</param>
        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
            cameraTransform = camera != null ? camera.transform : null;
        }

        #endregion
    }
}

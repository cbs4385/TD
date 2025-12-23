using UnityEngine;
using UnityEngine.UI;

namespace FaeMaze.UI
{
    /// <summary>
    /// Helper component to automatically configure world-space canvases for 3D rendering.
    /// Ensures proper scaling, billboarding, and camera assignment for world-space UI elements.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class WorldSpaceCanvasHelper : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Canvas Settings")]
        [SerializeField]
        [Tooltip("Automatically set canvas to WorldSpace mode")]
        private bool autoConfigureCanvas = true;

        [SerializeField]
        [Tooltip("Scale factor for the canvas (affects pixel density)")]
        private float canvasScale = 0.01f;

        [SerializeField]
        [Tooltip("Sort order for the canvas")]
        private int sortOrder = 0;

        [Header("Billboard Settings")]
        [SerializeField]
        [Tooltip("Make the canvas always face the camera")]
        private bool enableBillboarding = true;

        [SerializeField]
        [Tooltip("Lock Y-axis rotation when billboarding")]
        private bool lockYAxis = true;

        #endregion

        #region Private Fields

        private Canvas canvas;
        private Billboard billboard;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            canvas = GetComponent<Canvas>();

            if (autoConfigureCanvas)
            {
                ConfigureCanvas();
            }

            if (enableBillboarding)
            {
                SetupBillboard();
            }
        }

        #endregion

        #region Private Methods

        private void ConfigureCanvas()
        {
            if (canvas == null)
            {
                return;
            }

            // Set to WorldSpace mode
            canvas.renderMode = RenderMode.WorldSpace;

            // Set the world camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                canvas.worldCamera = mainCamera;
            }

            // Configure the RectTransform for proper sizing
            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // Set scale
                rectTransform.localScale = Vector3.one * canvasScale;

                // Set default size if needed
                if (rectTransform.sizeDelta == Vector2.zero)
                {
                    rectTransform.sizeDelta = new Vector2(100, 100);
                }
            }

            // Set sort order
            canvas.sortingOrder = sortOrder;

            // Optional: Add GraphicRaycaster if not present (for interaction)
            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void SetupBillboard()
        {
            // Add Billboard component if not already present
            billboard = GetComponent<Billboard>();
            if (billboard == null)
            {
                billboard = gameObject.AddComponent<Billboard>();
            }

            // Configure billboard settings via reflection to avoid tight coupling
            var billboardType = typeof(Billboard);

            var lockYAxisField = billboardType.GetField("lockYAxis",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (lockYAxisField != null)
            {
                lockYAxisField.SetValue(billboard, lockYAxis);
            }

            var targetCameraField = billboardType.GetField("targetCamera",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (targetCameraField != null && Camera.main != null)
            {
                targetCameraField.SetValue(billboard, Camera.main);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the canvas camera reference (useful when camera changes).
        /// </summary>
        /// <param name="newCamera">The new camera to use</param>
        public void UpdateCamera(Camera newCamera)
        {
            if (canvas != null)
            {
                canvas.worldCamera = newCamera;
            }

            if (billboard != null)
            {
                billboard.SetTargetCamera(newCamera);
            }
        }

        /// <summary>
        /// Sets the canvas scale.
        /// </summary>
        /// <param name="scale">New scale value</param>
        public void SetCanvasScale(float scale)
        {
            canvasScale = scale;

            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * canvasScale;
            }
        }

        #endregion
    }
}

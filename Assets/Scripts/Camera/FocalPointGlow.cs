using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Cameras
{
    /// <summary>
    /// Creates a column indicator at the focal point position.
    /// The column is oriented along the Z axis and tracks the focal point X/Y.
    /// </summary>
    public class FocalPointGlow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the MazeGridBehaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the focal point transform")]
        private Transform focalPointTransform;

        [Header("Column Settings")]
        [SerializeField]
        [Tooltip("Enable focal point column indicator")]
        private bool enableColumn = true;

        [SerializeField]
        [Tooltip("Color of the column")]
        private Color columnColor = new Color(1.0f, 0.2f, 0.8f, 1f); // Hot neon pink

        [SerializeField]
        [Tooltip("Radius of the column")]
        private float columnRadius = 0.15f;

        [SerializeField]
        [Tooltip("Length of the column")]
        private float columnLength = 1f;

        [SerializeField]
        [Tooltip("Z position of the column center")]
        private float columnZCenter = -0.25f;

        #endregion

        #region Private Fields

        private GameObject columnObject;
        private MeshRenderer columnRenderer;
        private Material columnMaterial;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find references if not set
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (focalPointTransform == null)
            {
                // Try to find it from CameraController3D
                var cameraController = FindFirstObjectByType<CameraController3D>();
                if (cameraController != null)
                {
                    // We need to access the focal point transform - it's private, so we'll use this GameObject's transform
                    focalPointTransform = transform;
                }
            }

            CreateColumn();
        }

        private void Update()
        {
            if (enableColumn && columnObject != null && focalPointTransform != null)
            {
                UpdateColumnPosition();
            }
        }

        private void OnDestroy()
        {
            if (columnMaterial != null)
            {
                Destroy(columnMaterial);
            }
        }

        #endregion

        #region Column Creation

        private void CreateColumn()
        {
            if (!enableColumn)
                return;

            // Create a cylinder primitive
            columnObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            columnObject.name = "FocalPointColumn";
            columnObject.transform.SetParent(transform, false);

            // Remove collider - we don't need it
            var collider = columnObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            // Cylinder default: height 2, radius 0.5, oriented along Y axis
            // We want: length 1, radius 0.15, oriented along Z axis
            // Scale: X and Z control radius (0.15/0.5 = 0.3), Y controls height (1/2 = 0.5)
            float radiusScale = columnRadius / 0.5f;
            float heightScale = columnLength / 2f;
            columnObject.transform.localScale = new Vector3(radiusScale, heightScale, radiusScale);

            // Rotate to orient along Z axis (rotate 90 degrees around X)
            columnObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Setup material
            columnRenderer = columnObject.GetComponent<MeshRenderer>();
            if (columnRenderer != null)
            {
                // Create material
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader != null)
                {
                    columnMaterial = new Material(shader);
                    if (columnMaterial.HasProperty("_BaseColor"))
                    {
                        columnMaterial.SetColor("_BaseColor", columnColor);
                    }
                    else if (columnMaterial.HasProperty("_Color"))
                    {
                        columnMaterial.SetColor("_Color", columnColor);
                    }
                    else
                    {
                        columnMaterial.color = columnColor;
                    }
                    columnRenderer.material = columnMaterial;
                }
            }
        }

        #endregion

        #region Column Updates

        private void UpdateColumnPosition()
        {
            // Position the column at the focal point's X/Y with fixed Z center
            Vector3 focalPos = focalPointTransform.position;
            columnObject.transform.position = new Vector3(focalPos.x, focalPos.y, columnZCenter);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the focal point transform to track.
        /// </summary>
        public void SetFocalPointTransform(Transform focal)
        {
            focalPointTransform = focal;
        }

        /// <summary>
        /// Sets the maze grid behaviour reference.
        /// </summary>
        public void SetMazeGridBehaviour(MazeGridBehaviour grid)
        {
            mazeGridBehaviour = grid;
        }

        #endregion
    }
}

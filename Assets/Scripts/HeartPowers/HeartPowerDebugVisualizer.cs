using UnityEngine;
using System.Collections.Generic;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Debug visualization for Heart powers in the Scene view.
    /// Shows AoE radii, active effects, etc. using world-space coordinates.
    /// </summary>
    public class HeartPowerDebugVisualizer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the HeartPowerManager")]
        private HeartPowerManager heartPowerManager;

        [Header("Visualization Settings")]
        [SerializeField]
        [Tooltip("Show power activation preview on mouse cursor")]
        private bool showMousePreview = true;

        [SerializeField]
        [Tooltip("Preview radius for AoE powers (in world units)")]
        private float previewRadius = 3f;

        [SerializeField]
        [Tooltip("Color for mouse preview")]
        private Color previewColor = new Color(1f, 1f, 0f, 0.5f);

        #endregion

        #region Private Fields

        private Camera mainCamera;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            mainCamera = Camera.main;

            if (heartPowerManager == null)
            {
                heartPowerManager = FindFirstObjectByType<HeartPowerManager>();
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (heartPowerManager == null)
            {
                return;
            }

            // Draw mouse preview
            if (showMousePreview)
            {
                DrawMousePreview();
            }
        }

        private void DrawMousePreview()
        {
            if (mainCamera == null)
            {
                return;
            }

            Vector3 mouseWorldPos = GetMouseWorldPosition();

            // Draw AoE preview circle (world-space radius)
            Gizmos.color = previewColor;
            DrawCircle(mouseWorldPos, previewRadius, 32);

            // Draw crosshair at mouse
            Gizmos.color = Color.yellow;
            float crossSize = 0.5f;
            Gizmos.DrawLine(mouseWorldPos + Vector3.left * crossSize, mouseWorldPos + Vector3.right * crossSize);
            Gizmos.DrawLine(mouseWorldPos + Vector3.up * crossSize, mouseWorldPos + Vector3.down * crossSize);
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 mousePos = Input.mousePosition;
            mousePos.z = Mathf.Abs(mainCamera.transform.position.z);
            return mainCamera.ScreenToWorldPoint(mousePos);
        }

        #endregion
    }
}

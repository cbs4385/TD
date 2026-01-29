using UnityEngine;
using UnityEngine.UI;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls canvas settings for the main game UI.
    /// Essence display is handled by EssenceBarController.
    /// Wave control is handled by WaveSpawner auto-start.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the wave spawner")]
        private WaveSpawner waveSpawner;

        [Header("Canvas Settings")]
        [SerializeField]
        [Tooltip("Reference pixels per unit to ensure sprites render at intended UI scale")]
        private float referencePixelsPerUnit = 32f;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ApplyCanvasSettings();
        }

        #endregion

        #region Private Methods

        private void ApplyCanvasSettings()
        {
            ApplyCanvasSettings(GetComponentInParent<Canvas>());
        }

        private void ApplyCanvasSettings(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.referencePixelsPerUnit = referencePixelsPerUnit;

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.referencePixelsPerUnit = referencePixelsPerUnit;
            }
        }

        #endregion
    }
}

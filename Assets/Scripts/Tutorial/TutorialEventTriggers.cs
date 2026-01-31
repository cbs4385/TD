using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Systems;
using FaeMaze.HeartPowers;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Listens to game events and input to notify TutorialManager when triggers are satisfied.
    /// </summary>
    public class TutorialEventTriggers : MonoBehaviour
    {
        #region Private Fields

        private TutorialManager manager;
        private Vector3 lastCameraPosition;
        private float cameraCheckInterval = 0.1f;
        private float lastCameraCheck;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            manager = TutorialManager.Instance;

            // Subscribe to game events
            SubscribeToEvents();

            // Initialize camera tracking
            var cam = Camera.main;
            if (cam != null)
            {
                lastCameraPosition = cam.transform.position;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void Update()
        {
            if (manager == null || !manager.IsActive) return;

            // Check for key press triggers
            CheckKeyPressTriggers();

            // Check camera movement
            CheckCameraMovement();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            // GameController events
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }

            // HeartPowerManager events
            if (HeartPowerManager.Instance != null)
            {
                HeartPowerManager.Instance.OnPowerActivated += OnPowerActivated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }

            if (HeartPowerManager.Instance != null)
            {
                HeartPowerManager.Instance.OnPowerActivated -= OnPowerActivated;
            }
        }

        #endregion

        #region Event Handlers

        private void OnEssenceChanged(int newEssence)
        {
            if (manager != null && manager.IsActive)
            {
                manager.NotifyEssenceChanged(newEssence);
            }
        }

        private void OnPowerActivated(HeartPowerType powerType)
        {
            if (manager != null && manager.IsActive)
            {
                // Convert power type to index
                int powerIndex = (int)powerType;
                manager.NotifyPowerActivated(powerIndex);
            }
        }

        #endregion

        #region Input Detection

        private void CheckKeyPressTriggers()
        {
            var step = manager.CurrentStep;
            if (step == null || step.triggerType != TutorialTriggerType.KeyPress) return;

            string triggerKey = step.triggerParameter;
            if (string.IsNullOrEmpty(triggerKey)) return;

            // Use InputBindingHelper to check for key press
            if (InputBindingHelper.WasBindingPressedThisFrame(triggerKey))
            {
                manager.NotifyKeyPressed(triggerKey);
            }

            // Also check common camera focus keys if waiting for F5
            if (triggerKey.Equals("F5", System.StringComparison.OrdinalIgnoreCase))
            {
                // Check using the actual binding from GameSettings
                if (InputBindingHelper.WasBindingPressedThisFrame(GameSettings.CameraFocusHeartBinding))
                {
                    manager.NotifyKeyPressed("F5");
                }
            }
        }

        private void CheckCameraMovement()
        {
            var step = manager.CurrentStep;
            if (step == null || step.triggerType != TutorialTriggerType.CameraMove) return;

            // Throttle camera checks
            if (Time.unscaledTime - lastCameraCheck < cameraCheckInterval) return;
            lastCameraCheck = Time.unscaledTime;

            var cam = Camera.main;
            if (cam == null) return;

            Vector3 currentPos = cam.transform.position;
            manager.NotifyCameraMoved(currentPos);

            lastCameraPosition = currentPos;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called by visitor spawner when a tutorial visitor spawns.
        /// </summary>
        public void NotifyVisitorSpawned()
        {
            if (manager != null && manager.IsActive)
            {
                manager.NotifyVisitorSpawned();
            }
        }

        #endregion
    }
}

using UnityEngine;
using UnityEngine.Rendering;
using FaeMaze.PostProcessing;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Controls the relationship between player essence and screen blur/vignette effects.
    /// Essence decrements over time, affecting the blur clear area and vignette coverage.
    /// </summary>
    public class EssenceBlurController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the global volume containing RadialBlur")]
        private Volume globalVolume;

        [SerializeField]
        [Tooltip("Reference to the GameController")]
        private GameController gameController;

        [Header("Essence Decay Settings")]
        [SerializeField]
        [Tooltip("Amount of essence to lose per second")]
        private float essenceDecayRate = 1f;

        [SerializeField]
        [Tooltip("Enable essence decay")]
        private bool enableEssenceDecay = true;

        [Header("Blur Maximization")]
        [SerializeField]
        [Tooltip("Maximize blur intensity to 1.0")]
        private bool maximizeBlurIntensity = true;

        [SerializeField]
        [Tooltip("Maximize blur samples to 16")]
        private bool maximizeBlurSamples = true;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Enable debug logging")]
        private bool debugLog = false;

        #endregion

        #region Private Fields

        private RadialBlur radialBlur;
        private float essenceDecayTimer;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find GameController if not assigned
            if (gameController == null)
            {
                gameController = GameController.Instance;
                if (gameController == null)
                {
                    gameController = FindFirstObjectByType<GameController>();
                }
            }

            // Find global volume if not assigned
            if (globalVolume == null)
            {
                globalVolume = FindFirstObjectByType<Volume>();
            }

            if (globalVolume == null)
            {
                Debug.LogError("[EssenceBlurController] No global volume found! Blur effects will not work.");
                enabled = false;
                return;
            }

            // Get RadialBlur component from volume
            if (!globalVolume.profile.TryGet(out radialBlur))
            {
                Debug.LogError("[EssenceBlurController] RadialBlur component not found in volume profile!");
                enabled = false;
                return;
            }

            // Maximize blur effects if enabled
            if (maximizeBlurIntensity)
            {
                radialBlur.blurIntensity.value = 1.0f;
                if (debugLog)
                {
                    Debug.Log("[EssenceBlurController] Maximized blur intensity to 1.0");
                }
            }

            if (maximizeBlurSamples)
            {
                radialBlur.blurSamples.value = 16;
                if (debugLog)
                {
                    Debug.Log("[EssenceBlurController] Maximized blur samples to 16");
                }
            }

            // Enable the blur effect
            radialBlur.enabled.value = true;

            // Initial update
            UpdateBlurFromEssence();

            if (debugLog)
            {
                Debug.Log("[EssenceBlurController] Initialized successfully");
            }
        }

        private void Update()
        {
            if (gameController == null || radialBlur == null)
            {
                return;
            }

            // Decay essence over time
            if (enableEssenceDecay)
            {
                DecayEssence();
            }

            // Update blur parameters based on current essence
            UpdateBlurFromEssence();
        }

        #endregion

        #region Essence Decay

        private void DecayEssence()
        {
            essenceDecayTimer += Time.deltaTime;

            // Decay 1 essence per second (or at configured rate)
            while (essenceDecayTimer >= 1f)
            {
                essenceDecayTimer -= 1f;

                int currentEssence = gameController.CurrentEssence;
                if (currentEssence > 0)
                {
                    int decayAmount = Mathf.RoundToInt(essenceDecayRate);
                    gameController.TrySpendEssence(decayAmount);

                    if (debugLog)
                    {
                        Debug.Log($"[EssenceBlurController] Essence decayed by {decayAmount}. New value: {gameController.CurrentEssence}");
                    }
                }
            }
        }

        #endregion

        #region Blur Updates

        private void UpdateBlurFromEssence()
        {
            int currentEssence = gameController.CurrentEssence;

            // Calculate blur clear area (linear from 0-200 essence)
            // 0 essence = 0% clear (everything blurred)
            // 200 essence = 100% clear (no blur)
            // Formula: clearArea = essence * 0.5 = (essence / 2)%
            float clearAreaPercentage = Mathf.Clamp(currentEssence * 0.5f, 0f, 100f);
            radialBlur.blurAngleDegrees.value = clearAreaPercentage;

            // Calculate vignette coverage (linear from 0-200 essence)
            // 0 essence = 100% coverage (maximum darkening)
            // 200 essence = 0% coverage (no vignette)
            // Formula: vignetteCoverage = 100 - (essence * 0.5) = (200 - 2*essence) / 2 %
            float vignetteCoveragePercentage = Mathf.Clamp(100f - (currentEssence * 0.5f), 0f, 100f);
            radialBlur.vignetteCoverage.value = vignetteCoveragePercentage;

            if (debugLog && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
            {
                Debug.Log($"[EssenceBlurController] Essence: {currentEssence}, Clear Area: {clearAreaPercentage:F1}%, Vignette Coverage: {vignetteCoveragePercentage:F1}%");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Manually trigger an update of blur parameters from current essence value.
        /// </summary>
        public void RefreshBlurEffects()
        {
            UpdateBlurFromEssence();
        }

        /// <summary>
        /// Enable or disable essence decay.
        /// </summary>
        public void SetEssenceDecayEnabled(bool enabled)
        {
            enableEssenceDecay = enabled;
        }

        #endregion
    }
}

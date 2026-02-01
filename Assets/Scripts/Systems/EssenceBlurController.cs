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
        [Tooltip("Amount of essence to lose per second (used when useGameSettings is false)")]
        private float essenceDecayRate = 1f;

        [SerializeField]
        [Tooltip("Enable essence decay (used when useGameSettings is false)")]
        private bool enableEssenceDecay = true;

        [SerializeField]
        [Tooltip("Use GameSettings for decay settings instead of serialized fields")]
        private bool useGameSettings = true;

        [Header("Blur Maximization")]
        [SerializeField]
        [Tooltip("Maximize blur intensity to 1.0")]
        private bool maximizeBlurIntensity = true;

        [SerializeField]
        [Tooltip("Maximize blur samples to 16")]
        private bool maximizeBlurSamples = true;

        #endregion

        #region Private Fields

        private RadialBlur radialBlur;
        private float essenceDecayTimer;
        private bool hasInitializedBlur;

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

                if (gameController == null)
                {
                    enabled = false;
                    return;
                }
            }

            // Try to find and initialize RadialBlur immediately
            // If not found now (likely because PostProcessVolumeRuntimeSetup hasn't run yet),
            // TryInitializeBlur() will retry in Update()
            TryInitializeBlur();
        }

        private void Update()
        {
            if (gameController == null)
            {
                return;
            }

            // Decay essence over time (use GameSettings if enabled)
            bool decayEnabled = useGameSettings ? GameSettings.EssenceDecayEnabled : enableEssenceDecay;
            if (decayEnabled)
            {
                DecayEssence();
            }

            // Try to find and initialize RadialBlur if not yet done
            // This handles the case where PostProcessVolumeRuntimeSetup adds RadialBlur after Start()
            if (!hasInitializedBlur)
            {
                TryInitializeBlur();
            }

            // Update blur parameters based on current essence (only if blur is available)
            if (radialBlur != null)
            {
                UpdateBlurFromEssence();
            }
        }

        #endregion

        #region Blur Initialization

        /// <summary>
        /// Attempts to find and initialize the RadialBlur component.
        /// Called from Update() to handle the case where PostProcessVolumeRuntimeSetup
        /// adds RadialBlur after this component's Start() runs.
        /// </summary>
        private void TryInitializeBlur()
        {
            // Find global volume if not assigned - search for one with RadialBlur
            if (globalVolume == null)
            {
                var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
                foreach (var vol in volumes)
                {
                    if (vol.profile != null && vol.profile.TryGet<RadialBlur>(out _))
                    {
                        globalVolume = vol;
                        break;
                    }
                }
            }

            // Try to get RadialBlur component from volume
            if (globalVolume != null && globalVolume.profile != null)
            {
                globalVolume.profile.TryGet(out radialBlur);
            }

            // Setup blur effects if found
            if (radialBlur != null)
            {
                if (maximizeBlurIntensity)
                {
                    radialBlur.blurIntensity.overrideState = true;
                    radialBlur.blurIntensity.value = 1.0f;
                }

                if (maximizeBlurSamples)
                {
                    radialBlur.blurSamples.overrideState = true;
                    radialBlur.blurSamples.value = 16;
                }

                // Enable the blur effect
                radialBlur.enabled.overrideState = true;
                radialBlur.enabled.value = true;

                // Also ensure vignette intensity is set
                radialBlur.vignetteIntensity.overrideState = true;
                radialBlur.vignetteIntensity.value = 0.8f;

                // Initial update
                UpdateBlurFromEssence();

                // Mark as initialized so we don't keep searching
                hasInitializedBlur = true;
                Debug.Log("[EssenceBlurController] RadialBlur initialized successfully");
            }
        }

        #endregion

        #region Essence Decay

        private void DecayEssence()
        {
            essenceDecayTimer += Time.deltaTime;

            // Decay essence per second (use GameSettings rate if enabled)
            float rate = useGameSettings ? GameSettings.EssenceDecayRate : essenceDecayRate;

            while (essenceDecayTimer >= 1f)
            {
                essenceDecayTimer -= 1f;

                int currentEssence = gameController.CurrentEssence;
                if (currentEssence > 0)
                {
                    int decayAmount = Mathf.RoundToInt(rate);
                    gameController.TrySpendEssence(decayAmount, EssenceSource.EssenceDecay);
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
            radialBlur.blurAngleDegrees.overrideState = true;
            radialBlur.blurAngleDegrees.value = clearAreaPercentage;

            // Calculate vignette coverage (linear from 0-200 essence)
            // 0 essence = 100% coverage (maximum darkening)
            // 200 essence = 0% coverage (no vignette)
            // Formula: vignetteCoverage = 100 - (essence * 0.5) = (200 - 2*essence) / 2 %
            float vignetteCoveragePercentage = Mathf.Clamp(100f - (currentEssence * 0.5f), 0f, 100f);
            radialBlur.vignetteCoverage.overrideState = true;
            radialBlur.vignetteCoverage.value = vignetteCoveragePercentage;

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

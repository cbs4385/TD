using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages difficulty progression based on essence thresholds.
    /// Difficulty tier increases when player accumulates essence milestones.
    /// Uses hysteresis to prevent rapid tier oscillation.
    /// </summary>
    public class DifficultyManager : MonoBehaviour
    {
        #region Singleton

        private static DifficultyManager _instance;
        public static DifficultyManager Instance => _instance;

        #endregion

        #region Constants

        /// <summary>
        /// Tier thresholds as multipliers of starting essence.
        /// Tier 1 = start, Tier 2 = 1.5x, Tier 3 = 2x (RedCap), etc.
        /// </summary>
        private static readonly float[] TIER_MULTIPLIERS = { 0f, 1.5f, 2f, 3f, 4f, 6f, 8f };

        /// <summary>Maximum tier (index into TIER_MULTIPLIERS + 1)</summary>
        public const int MAX_TIER = 7;

        #endregion

        #region Events

        /// <summary>Fired when difficulty tier changes. Parameter is new tier.</summary>
        public event System.Action<int> OnTierChanged;

        #endregion

        #region Private Fields

        private int currentTier = 1;
        private int peakTier = 1;
        private int startingEssence;
        private bool initialized;

        #endregion

        #region Properties

        /// <summary>Gets the current difficulty tier (1-7).</summary>
        public int CurrentTier => currentTier;

        /// <summary>Gets the highest tier reached this session.</summary>
        public int PeakTier => peakTier;

        /// <summary>Gets the starting essence used for threshold calculations.</summary>
        public int StartingEssence => startingEssence;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged += OnEssenceChanged;
            }
        }

        private void OnDisable()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnEssenceChanged -= OnEssenceChanged;
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (initialized) return;

            startingEssence = GameSettings.StartingEssence;
            currentTier = 1;
            peakTier = 1;

            // Calculate initial tier based on current essence
            if (GameController.Instance != null)
            {
                RecalculateTier(GameController.Instance.CurrentEssence);
            }

            initialized = true;
        }

        /// <summary>
        /// Resets the difficulty manager for a new game.
        /// </summary>
        public void Reset()
        {
            startingEssence = GameSettings.StartingEssence;
            currentTier = 1;
            peakTier = 1;
            initialized = false;
            Initialize();
        }

        #endregion

        #region Tier Calculation

        private void OnEssenceChanged(int newEssence)
        {
            if (!initialized) Initialize();
            RecalculateTier(newEssence);
        }

        /// <summary>
        /// Recalculates tier based on current essence with hysteresis.
        /// </summary>
        private void RecalculateTier(int essence)
        {
            int calculatedTier = CalculateTierForEssence(essence);

            // Track peak tier
            if (calculatedTier > peakTier)
            {
                peakTier = calculatedTier;
            }

            int newTier = currentTier;

            if (calculatedTier > currentTier)
            {
                // Always allow tier increase
                newTier = calculatedTier;
            }
            else if (calculatedTier < currentTier - 1)
            {
                // Hysteresis: only decrease if dropped below PREVIOUS tier threshold
                // This prevents oscillation at tier boundaries
                newTier = calculatedTier + 1;
            }
            // else: stay at current tier (in hysteresis buffer zone)

            if (newTier != currentTier)
            {
                int oldTier = currentTier;
                currentTier = newTier;
                OnTierChanged?.Invoke(currentTier);
            }
        }

        /// <summary>
        /// Calculates what tier a given essence value corresponds to.
        /// </summary>
        public int CalculateTierForEssence(int essence)
        {
            if (startingEssence <= 0) return 1;

            float essenceRatio = (float)essence / startingEssence;

            // Find highest tier whose threshold we've passed
            for (int tier = TIER_MULTIPLIERS.Length; tier >= 1; tier--)
            {
                float threshold = TIER_MULTIPLIERS[tier - 1];
                if (essenceRatio >= threshold)
                {
                    return Mathf.Min(tier, MAX_TIER);
                }
            }

            return 1;
        }

        /// <summary>
        /// Gets the essence threshold for a given tier.
        /// </summary>
        public int GetThresholdForTier(int tier)
        {
            if (tier < 1 || tier > TIER_MULTIPLIERS.Length)
            {
                return 0;
            }

            return Mathf.RoundToInt(startingEssence * TIER_MULTIPLIERS[tier - 1]);
        }

        /// <summary>
        /// Gets the essence needed to reach the next tier.
        /// Returns -1 if already at max tier.
        /// </summary>
        public int GetEssenceToNextTier()
        {
            if (currentTier >= MAX_TIER) return -1;

            int nextThreshold = GetThresholdForTier(currentTier + 1);
            int currentEssence = GameController.Instance?.CurrentEssence ?? 0;

            return Mathf.Max(0, nextThreshold - currentEssence);
        }

        #endregion
    }
}

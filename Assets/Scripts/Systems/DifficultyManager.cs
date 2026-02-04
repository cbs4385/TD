using UnityEngine;
using FaeMaze.Roguelike;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages difficulty progression based on essence thresholds.
    /// Difficulty tier increases when player accumulates essence milestones.
    /// Uses hysteresis to prevent rapid tier oscillation.
    /// </summary>
    public class DifficultyManager : SingletonMonoBehaviour<DifficultyManager>
    {

        #region Tier Multipliers

        /// <summary>
        /// Tier thresholds as multipliers of starting essence.
        /// Loaded from GameSettings.TierMultipliers (comma-separated string).
        /// Tier 1 = start, Tier 2 = 1.5x, Tier 3 = 2x (Goblin), etc.
        /// </summary>
        private float[] tierMultipliers;

        /// <summary>Maximum tier (determined by tierMultipliers array length)</summary>
        public int MaxTier => tierMultipliers?.Length ?? 7;

        /// <summary>
        /// Default tier multipliers used if parsing fails.
        /// </summary>
        private static readonly float[] DEFAULT_tierMultipliers = { 0f, 1.5f, 2f, 3f, 4f, 6f, 8f };

        /// <summary>
        /// Parses tier multipliers from GameSettings string.
        /// Expected format: "0,1.5,2,3,4,6,8"
        /// </summary>
        private void LoadTierMultipliers()
        {
            string multiplierString = GameSettings.TierMultipliers;
            if (string.IsNullOrEmpty(multiplierString))
            {
                tierMultipliers = DEFAULT_tierMultipliers;
                return;
            }

            try
            {
                string[] parts = multiplierString.Split(',');
                tierMultipliers = new float[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!float.TryParse(parts[i].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out tierMultipliers[i]))
                    {
                        tierMultipliers = DEFAULT_tierMultipliers;
                        return;
                    }
                }
            }
            catch (System.Exception)
            {
                tierMultipliers = DEFAULT_tierMultipliers;
            }
        }

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

        protected override void OnAwake()
        {
            // Initialization deferred to Start() to ensure GameController is ready
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

            // Load tier multipliers from GameSettings
            LoadTierMultipliers();

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

                // Notify MetaProgressionManager of tier change
                MetaProgressionManager.Instance?.RecordDifficultyTier(currentTier);
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
            for (int tier = tierMultipliers.Length; tier >= 1; tier--)
            {
                float threshold = tierMultipliers[tier - 1];
                if (essenceRatio >= threshold)
                {
                    return Mathf.Min(tier, MaxTier);
                }
            }

            return 1;
        }

        /// <summary>
        /// Gets the essence threshold for a given tier.
        /// </summary>
        public int GetThresholdForTier(int tier)
        {
            if (tier < 1 || tier > tierMultipliers.Length)
            {
                return 0;
            }

            return Mathf.RoundToInt(startingEssence * tierMultipliers[tier - 1]);
        }

        /// <summary>
        /// Gets the essence needed to reach the next tier.
        /// Returns -1 if already at max tier.
        /// </summary>
        public int GetEssenceToNextTier()
        {
            if (currentTier >= MaxTier) return -1;

            int nextThreshold = GetThresholdForTier(currentTier + 1);
            int currentEssence = GameController.Instance?.CurrentEssence ?? 0;

            return Mathf.Max(0, nextThreshold - currentEssence);
        }

        #endregion
    }
}

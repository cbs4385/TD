using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Provides difficulty scaling based on essence-threshold tiers.
    /// All scaling uses smooth asymptotic curves that increase challenge progressively.
    ///
    /// Design philosophy:
    /// - Tier 1 uses base values (multiplier = 1.0)
    /// - Difficulty increases at essence milestones (not waves - game is continuous)
    /// - Uses asymptotic curves to prevent runaway difficulty
    /// - Higher tiers are challenging but not impossible
    ///
    /// Tiers are based on essence multiples of starting essence:
    /// - Tier 1: Start (0+)
    /// - Tier 2: 1.5x starting essence
    /// - Tier 3: 2.0x starting essence (RedCap spawn point)
    /// - Tier 4: 3.0x starting essence
    /// - Tier 5: 4.0x starting essence
    /// - Tier 6: 6.0x starting essence
    /// - Tier 7: 8.0x starting essence (max)
    ///
    /// All scaling parameters are loaded from GameSettings for configurability.
    /// </summary>
    public static class DifficultyScaling
    {
        #region Cached Settings

        // Cached settings loaded from GameSettings on first access
        private static bool settingsLoaded = false;

        // Spawn interval settings
        private static float maxSpawnInterval;
        private static float spawnIntervalGrowthRate;

        // Visitor speed scaling
        private static float visitorSpeedMaxMultiplier;
        private static float visitorSpeedGrowthRate;

        // RedCap speed scaling
        private static float redCapSpeedMaxMultiplier;
        private static float redCapSpeedGrowthRate;

        // RedCap penalty scaling
        private static float redCapPenaltyMaxMultiplier;
        private static float redCapPenaltyGrowthRate;

        // Confusion chance scaling
        private static float confusionMaxMultiplier;
        private static float confusionGrowthRate;

        // Essence reward scaling
        private static float essenceRewardMaxMultiplier;
        private static float essenceRewardGrowthRate;

        /// <summary>
        /// Ensures settings are loaded from GameSettings.
        /// Called automatically on first access to any scaling method.
        /// </summary>
        private static void EnsureSettingsLoaded()
        {
            if (settingsLoaded) return;

            // Spawn interval
            maxSpawnInterval = GameSettings.MaxSpawnInterval;
            spawnIntervalGrowthRate = GameSettings.SpawnIntervalGrowthRate;

            // Visitor speed
            visitorSpeedMaxMultiplier = GameSettings.VisitorSpeedMaxMultiplier;
            visitorSpeedGrowthRate = GameSettings.VisitorSpeedGrowthRate;

            // RedCap speed
            redCapSpeedMaxMultiplier = GameSettings.RedCapSpeedMaxMultiplier;
            redCapSpeedGrowthRate = GameSettings.RedCapSpeedGrowthRate;

            // RedCap penalty
            redCapPenaltyMaxMultiplier = GameSettings.RedCapPenaltyMaxMultiplier;
            redCapPenaltyGrowthRate = GameSettings.RedCapPenaltyGrowthRate;

            // Confusion
            confusionMaxMultiplier = GameSettings.ConfusionMaxMultiplier;
            confusionGrowthRate = GameSettings.ConfusionGrowthRate;

            // Essence reward
            essenceRewardMaxMultiplier = GameSettings.EssenceRewardMaxMultiplier;
            essenceRewardGrowthRate = GameSettings.EssenceRewardGrowthRate;

            settingsLoaded = true;
        }

        /// <summary>
        /// Reloads settings from GameSettings. Call this if settings may have changed.
        /// </summary>
        public static void ReloadSettings()
        {
            settingsLoaded = false;
            EnsureSettingsLoaded();
        }

        #endregion

        #region Asymptotic Spawn Interval

        /// <summary>
        /// Calculates spawn interval that grows asymptotically toward a maximum.
        /// Formula: baseInterval + (maxInterval - baseInterval) * (1 - e^(-rate * spawnCount))
        /// </summary>
        /// <param name="spawnCount">Total visitors spawned so far.</param>
        /// <param name="baseInterval">Starting spawn interval in seconds.</param>
        /// <returns>Current spawn interval in seconds.</returns>
        public static float GetAsymptoticSpawnInterval(int spawnCount, float baseInterval)
        {
            EnsureSettingsLoaded();
            if (spawnCount <= 0) return baseInterval;

            float range = maxSpawnInterval - baseInterval;
            float progress = 1f - Mathf.Exp(-spawnIntervalGrowthRate * spawnCount);
            return baseInterval + (range * progress);
        }

        /// <summary>
        /// Calculates spawn interval with custom maximum.
        /// </summary>
        public static float GetAsymptoticSpawnInterval(int spawnCount, float baseInterval, float customMaxInterval)
        {
            EnsureSettingsLoaded();
            if (spawnCount <= 0) return baseInterval;

            float range = customMaxInterval - baseInterval;
            float progress = 1f - Mathf.Exp(-spawnIntervalGrowthRate * spawnCount);
            return baseInterval + (range * progress);
        }

        #endregion

        #region Core Scaling Functions

        /// <summary>
        /// Calculates an asymptotic growth multiplier that approaches a maximum value.
        /// Formula: 1 + (maxBonus) * (1 - e^(-rate * (tier-1)))
        /// </summary>
        /// <param name="tier">Current difficulty tier (1-based)</param>
        /// <param name="maxMultiplier">Maximum multiplier to approach</param>
        /// <param name="growthRate">How quickly to approach the maximum</param>
        /// <returns>Multiplier between 1.0 and maxMultiplier</returns>
        private static float CalculateGrowthMultiplier(int tier, float maxMultiplier, float growthRate)
        {
            if (tier <= 1) return 1f;

            float maxBonus = maxMultiplier - 1f;
            float progress = 1f - Mathf.Exp(-growthRate * (tier - 1));
            return 1f + (maxBonus * progress);
        }

        #endregion

        #region Tier-Based Multiplier Methods

        /// <summary>
        /// Gets the visitor speed multiplier for the given tier.
        /// Higher values = faster visitors.
        /// </summary>
        public static float GetVisitorSpeedMultiplier(int tier)
        {
            EnsureSettingsLoaded();
            return CalculateGrowthMultiplier(tier, visitorSpeedMaxMultiplier, visitorSpeedGrowthRate);
        }

        /// <summary>
        /// Gets the RedCap speed multiplier for the given tier.
        /// Higher values = faster RedCap.
        /// </summary>
        public static float GetRedCapSpeedMultiplier(int tier)
        {
            EnsureSettingsLoaded();
            return CalculateGrowthMultiplier(tier, redCapSpeedMaxMultiplier, redCapSpeedGrowthRate);
        }

        /// <summary>
        /// Gets the RedCap essence penalty multiplier for the given tier.
        /// Higher values = more essence lost when RedCap catches a visitor.
        /// </summary>
        public static float GetRedCapPenaltyMultiplier(int tier)
        {
            EnsureSettingsLoaded();
            return CalculateGrowthMultiplier(tier, redCapPenaltyMaxMultiplier, redCapPenaltyGrowthRate);
        }

        /// <summary>
        /// Gets the confusion chance multiplier for the given tier.
        /// Higher values = visitors get lost more often.
        /// </summary>
        public static float GetConfusionChanceMultiplier(int tier)
        {
            EnsureSettingsLoaded();
            return CalculateGrowthMultiplier(tier, confusionMaxMultiplier, confusionGrowthRate);
        }

        /// <summary>
        /// Gets the essence reward multiplier for the given tier.
        /// Higher values = more essence gained from consuming visitors.
        /// This helps offset increased difficulty.
        /// </summary>
        public static float GetEssenceRewardMultiplier(int tier)
        {
            EnsureSettingsLoaded();
            return CalculateGrowthMultiplier(tier, essenceRewardMaxMultiplier, essenceRewardGrowthRate);
        }

        /// <summary>
        /// Gets all difficulty multipliers for a given tier as a struct.
        /// </summary>
        public static DifficultyMultipliers GetAllMultipliers(int tier)
        {
            return new DifficultyMultipliers
            {
                Tier = tier,
                VisitorSpeed = GetVisitorSpeedMultiplier(tier),
                RedCapSpeed = GetRedCapSpeedMultiplier(tier),
                RedCapPenalty = GetRedCapPenaltyMultiplier(tier),
                ConfusionChance = GetConfusionChanceMultiplier(tier),
                EssenceReward = GetEssenceRewardMultiplier(tier)
            };
        }

        #endregion

        #region Scaled Value Helpers

        /// <summary>
        /// Gets the scaled visitor speed for a tier.
        /// </summary>
        public static float GetScaledVisitorSpeed(int tier, float baseSpeed)
        {
            return baseSpeed * GetVisitorSpeedMultiplier(tier);
        }

        /// <summary>
        /// Gets the scaled RedCap speed for a tier.
        /// </summary>
        public static float GetScaledRedCapSpeed(int tier, float baseSpeed)
        {
            return baseSpeed * GetRedCapSpeedMultiplier(tier);
        }

        /// <summary>
        /// Gets the scaled essence penalty for a tier.
        /// </summary>
        public static int GetScaledEssencePenalty(int tier, int basePenalty)
        {
            return Mathf.RoundToInt(basePenalty * GetRedCapPenaltyMultiplier(tier));
        }

        /// <summary>
        /// Gets the scaled confusion chance for a tier.
        /// Clamped to 0-1 range.
        /// </summary>
        public static float GetScaledConfusionChance(int tier, float baseChance)
        {
            return Mathf.Clamp01(baseChance * GetConfusionChanceMultiplier(tier));
        }

        /// <summary>
        /// Gets the scaled essence reward for a tier.
        /// </summary>
        public static int GetScaledEssenceReward(int tier, int baseReward)
        {
            return Mathf.RoundToInt(baseReward * GetEssenceRewardMultiplier(tier));
        }

        #endregion
    }

    /// <summary>
    /// Struct containing all difficulty multipliers for a given tier.
    /// </summary>
    public struct DifficultyMultipliers
    {
        public int Tier;
        public float VisitorSpeed;
        public float RedCapSpeed;
        public float RedCapPenalty;
        public float ConfusionChance;
        public float EssenceReward;

        public override string ToString()
        {
            return $"Tier {Tier} Difficulty:\n" +
                   $"  Visitor Speed: {VisitorSpeed:F2}x\n" +
                   $"  RedCap Speed: {RedCapSpeed:F2}x\n" +
                   $"  RedCap Penalty: {RedCapPenalty:F2}x\n" +
                   $"  Confusion Chance: {ConfusionChance:F2}x\n" +
                   $"  Essence Reward: {EssenceReward:F2}x";
        }
    }
}

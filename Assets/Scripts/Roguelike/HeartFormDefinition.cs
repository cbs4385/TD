using UnityEngine;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Types of Heart Forms available for selection at run start.
    /// Each form provides unique stat modifiers and playstyle.
    /// </summary>
    public enum HeartFormType
    {
        HungryHeart,    // Default balanced form - no modifiers
        RavenousHeart,  // Heart tongue 50% faster, -20 starting essence
        PatientHeart,   // Lanterns 30% more effective, hazards spawn 25% more often
        FamishedHeart   // +50% essence rewards, but 2/sec essence decay
    }

    /// <summary>
    /// ScriptableObject defining a Heart Form's properties and effects.
    /// Heart Forms are "character" selections that modify the player's stats and playstyle.
    /// </summary>
    [CreateAssetMenu(fileName = "New Heart Form", menuName = "FaeMaze/Roguelike/Heart Form Definition")]
    public class HeartFormDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private HeartFormType formType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("Unlock")]
        [SerializeField] private int faeDustCost = 0;
        [SerializeField] private bool isDefault = false; // True for HungryHeart
        [SerializeField] private string prerequisiteUnlock; // Optional - required unlock ID

        [Header("Stat Modifiers")]
        [Tooltip("Multiplier for Heart tongue speed (both emerge and retract). 1.0 = normal")]
        [SerializeField] private float tongueSpeedMultiplier = 1.0f;

        [Tooltip("Flat modifier to starting essence (can be negative). 0 = no change")]
        [SerializeField] private int startingEssenceModifier = 0;

        [Tooltip("Multiplier for lantern essence award rate. 1.0 = normal")]
        [SerializeField] private float lanternEffectivenessMultiplier = 1.0f;

        [Tooltip("Multiplier for hazard spawn frequency. 1.0 = normal, lower = less frequent")]
        [SerializeField] private float hazardSpawnRateMultiplier = 1.0f;

        [Tooltip("Multiplier for all essence rewards (consumption, powers, etc). 1.0 = normal")]
        [SerializeField] private float essenceRewardMultiplier = 1.0f;

        [Tooltip("Passive essence decay per second. 0 = no decay")]
        [SerializeField] private float passiveEssenceDecayPerSecond = 0f;

        #region Properties

        public HeartFormType Type => formType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int FaeDustCost => faeDustCost;
        public bool IsDefault => isDefault;
        public string PrerequisiteUnlock => prerequisiteUnlock;

        // Stat modifier properties
        public float TongueSpeedMultiplier => tongueSpeedMultiplier;
        public int StartingEssenceModifier => startingEssenceModifier;
        public float LanternEffectivenessMultiplier => lanternEffectivenessMultiplier;
        public float HazardSpawnRateMultiplier => hazardSpawnRateMultiplier;
        public float EssenceRewardMultiplier => essenceRewardMultiplier;
        public float PassiveEssenceDecayPerSecond => passiveEssenceDecayPerSecond;

        #endregion

        #region Helper Properties

        /// <summary>Check if this form affects tongue speed.</summary>
        public bool AffectsTongueSpeed => !Mathf.Approximately(tongueSpeedMultiplier, 1.0f);

        /// <summary>Check if this form modifies starting essence.</summary>
        public bool AffectsStartingEssence => startingEssenceModifier != 0;

        /// <summary>Check if this form affects lantern effectiveness.</summary>
        public bool AffectsLanternEffectiveness => !Mathf.Approximately(lanternEffectivenessMultiplier, 1.0f);

        /// <summary>Check if this form affects hazard spawn rate.</summary>
        public bool AffectsHazardSpawnRate => !Mathf.Approximately(hazardSpawnRateMultiplier, 1.0f);

        /// <summary>Check if this form affects essence rewards.</summary>
        public bool AffectsEssenceRewards => !Mathf.Approximately(essenceRewardMultiplier, 1.0f);

        /// <summary>Check if this form has passive essence decay.</summary>
        public bool HasPassiveEssenceDecay => passiveEssenceDecayPerSecond > 0f;

        #endregion

        /// <summary>
        /// Gets a formatted summary of this form's effects for UI display.
        /// </summary>
        public string GetEffectSummary()
        {
            var effects = new System.Collections.Generic.List<string>();

            if (AffectsTongueSpeed)
            {
                float pct = (tongueSpeedMultiplier - 1f) * 100f;
                effects.Add($"Tongue Speed: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (AffectsStartingEssence)
            {
                effects.Add($"Starting Threads: {(startingEssenceModifier >= 0 ? "+" : "")}{startingEssenceModifier}");
            }

            if (AffectsLanternEffectiveness)
            {
                float pct = (lanternEffectivenessMultiplier - 1f) * 100f;
                effects.Add($"Lantern Yield: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (AffectsHazardSpawnRate)
            {
                float pct = (hazardSpawnRateMultiplier - 1f) * 100f;
                effects.Add($"Hazard Spawns: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (AffectsEssenceRewards)
            {
                float pct = (essenceRewardMultiplier - 1f) * 100f;
                effects.Add($"Thread Rewards: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (HasPassiveEssenceDecay)
            {
                effects.Add($"Thread Decay: -{passiveEssenceDecayPerSecond:F1}/sec");
            }

            return string.Join("\n", effects);
        }
    }
}

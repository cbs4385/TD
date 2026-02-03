using UnityEngine;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Types of Challenge Modifiers that can be enabled at run start.
    /// Multiple challenges can be active simultaneously for stacking rewards.
    /// </summary>
    public enum ChallengeModifierType
    {
        None,
        FrugalHeart,        // Powers cost 50% more - 1.25x Fae Dust
        EndlessTide,        // Spawn interval halved - 1.5x Fae Dust
        BlindFaith,         // No visitor state indicators - 1.25x Fae Dust
        EssenceDrought,     // No essence from props - 1.5x Fae Dust
        ChampionVisitors    // 10% of visitors are elites (2x stats, 3x reward) - 1.75x Fae Dust
    }

    /// <summary>
    /// ScriptableObject defining a Challenge Modifier's properties and effects.
    /// Challenge Modifiers are optional difficulty increases that reward more Fae Dust.
    /// </summary>
    [CreateAssetMenu(fileName = "New Challenge", menuName = "FaeMaze/Roguelike/Challenge Modifier Definition")]
    public class ChallengeModifierDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private ChallengeModifierType modifierType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("Unlock")]
        [SerializeField] private int faeDustCost = 50;
        [SerializeField] private string prerequisiteUnlock; // Optional - required unlock ID

        [Header("Reward")]
        [Tooltip("Multiplier for Fae Dust earned during the run. 1.0 = no bonus")]
        [SerializeField] private float faeDustMultiplier = 1.0f;

        [Header("Effect Modifiers")]
        [Tooltip("Multiplier for power costs. 1.0 = normal, 1.5 = 50% more expensive")]
        [SerializeField] private float powerCostMultiplier = 1.0f;

        [Tooltip("Multiplier for spawn interval. 1.0 = normal, 0.5 = twice as fast")]
        [SerializeField] private float spawnIntervalMultiplier = 1.0f;

        [Tooltip("If true, visitor state indicators are hidden")]
        [SerializeField] private bool hideVisitorIndicators = false;

        [Tooltip("If true, props don't award essence to player")]
        [SerializeField] private bool disablePropEssence = false;

        [Tooltip("Chance for visitors to spawn as elite (0-1). 0 = disabled")]
        [SerializeField] private float eliteSpawnChance = 0f;

        [Tooltip("Stat multiplier for elite visitors")]
        [SerializeField] private float eliteStatMultiplier = 2.0f;

        [Tooltip("Essence reward multiplier for elite visitors")]
        [SerializeField] private float eliteRewardMultiplier = 3.0f;

        #region Properties

        public ChallengeModifierType Type => modifierType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int FaeDustCost => faeDustCost;
        public string PrerequisiteUnlock => prerequisiteUnlock;

        // Reward
        public float FaeDustMultiplier => faeDustMultiplier;

        // Effect modifiers
        public float PowerCostMultiplier => powerCostMultiplier;
        public float SpawnIntervalMultiplier => spawnIntervalMultiplier;
        public bool HideVisitorIndicators => hideVisitorIndicators;
        public bool DisablePropEssence => disablePropEssence;
        public float EliteSpawnChance => eliteSpawnChance;
        public float EliteStatMultiplier => eliteStatMultiplier;
        public float EliteRewardMultiplier => eliteRewardMultiplier;

        #endregion

        #region Helper Properties

        /// <summary>Check if this challenge affects power costs.</summary>
        public bool AffectsPowerCost => !Mathf.Approximately(powerCostMultiplier, 1.0f);

        /// <summary>Check if this challenge affects spawn rate.</summary>
        public bool AffectsSpawnRate => !Mathf.Approximately(spawnIntervalMultiplier, 1.0f);

        /// <summary>Check if this challenge enables elite visitors.</summary>
        public bool HasEliteVisitors => eliteSpawnChance > 0f;

        #endregion

        /// <summary>
        /// Gets a formatted summary of this challenge's effects for UI display.
        /// </summary>
        public string GetEffectSummary()
        {
            var effects = new System.Collections.Generic.List<string>();

            if (AffectsPowerCost)
            {
                float pct = (powerCostMultiplier - 1f) * 100f;
                effects.Add($"Power costs: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (AffectsSpawnRate)
            {
                float pct = (1f - spawnIntervalMultiplier) * 100f;
                effects.Add($"Spawn rate: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (hideVisitorIndicators)
            {
                effects.Add("Visitor indicators hidden");
            }

            if (disablePropEssence)
            {
                effects.Add("No essence from hazards");
            }

            if (HasEliteVisitors)
            {
                effects.Add($"{eliteSpawnChance * 100f:F0}% elite visitors");
            }

            return string.Join("\n", effects);
        }

        /// <summary>
        /// Gets the reward description for UI display.
        /// </summary>
        public string GetRewardText()
        {
            return $"{faeDustMultiplier:F2}x Fae Dust";
        }
    }
}

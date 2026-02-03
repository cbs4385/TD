using UnityEngine;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Types of Prop Mutations that can be enabled.
    /// Mutations modify how props interact with visitors and share essence.
    /// Only one mutation can be active per run.
    /// </summary>
    public enum PropMutationType
    {
        None,
        GreedyGlow,      // Lanterns award more essence to player
        RingTithe,       // Fairy Rings give player a portion of drained essence
        DrowningGift     // Puka/Pond shares essence when drowning visitors
    }

    /// <summary>
    /// ScriptableObject defining a Prop Mutation's properties and effects.
    /// Prop Mutations modify how props interact with visitors and essence economy.
    /// </summary>
    [CreateAssetMenu(fileName = "New Mutation", menuName = "FaeMaze/Roguelike/Prop Mutation Definition")]
    public class PropMutationDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private PropMutationType mutationType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("Unlock")]
        [SerializeField] private int faeDustCost = 100;
        [SerializeField] private string prerequisiteUnlock; // Optional - required unlock ID

        [Header("Effect Modifiers")]
        [Tooltip("Multiplier for lantern essence award to player. 1.0 = normal, 1.5 = 50% more")]
        [SerializeField] private float lanternAwardMultiplier = 1.0f;

        [Tooltip("Portion of fairy ring drained essence given to player (0-1). 0 = none, 0.5 = half")]
        [SerializeField] private float ringEssenceTithe = 0f;

        [Tooltip("Portion of drowned visitor essence given to player (0-1). 0 = none, 0.5 = half")]
        [SerializeField] private float pukaEssenceShare = 0f;

        #region Properties

        public PropMutationType Type => mutationType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int FaeDustCost => faeDustCost;
        public string PrerequisiteUnlock => prerequisiteUnlock;

        // Effect modifiers
        public float LanternAwardMultiplier => lanternAwardMultiplier;
        public float RingEssenceTithe => ringEssenceTithe;
        public float PukaEssenceShare => pukaEssenceShare;

        #endregion

        #region Helper Properties

        /// <summary>Check if this mutation affects lanterns.</summary>
        public bool AffectsLanterns => !Mathf.Approximately(lanternAwardMultiplier, 1.0f);

        /// <summary>Check if this mutation affects fairy rings.</summary>
        public bool AffectsRings => ringEssenceTithe > 0f;

        /// <summary>Check if this mutation affects puka/ponds.</summary>
        public bool AffectsPuka => pukaEssenceShare > 0f;

        #endregion

        /// <summary>
        /// Gets a formatted summary of this mutation's effects for UI display.
        /// </summary>
        public string GetEffectSummary()
        {
            var effects = new System.Collections.Generic.List<string>();

            if (AffectsLanterns)
            {
                float pct = (lanternAwardMultiplier - 1f) * 100f;
                effects.Add($"Lantern yield: {(pct >= 0 ? "+" : "")}{pct:F0}%");
            }

            if (AffectsRings)
            {
                effects.Add($"Ring tithe: {ringEssenceTithe * 100f:F0}% to player");
            }

            if (AffectsPuka)
            {
                effects.Add($"Drowning gift: {pukaEssenceShare * 100f:F0}% to player");
            }

            return string.Join("\n", effects);
        }
    }
}

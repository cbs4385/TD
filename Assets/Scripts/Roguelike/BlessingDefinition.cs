using UnityEngine;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Types of blessings that modify gameplay at run start.
    /// </summary>
    public enum BlessingType
    {
        None,
        GreedyHeart,      // +50% essence from consumed visitors, -25% starting essence
        PatientHunter,    // Visitors move 15% slower, spawn 20% faster
        DesperateGrasp,   // Grasp costs 0 essence when below 25 essence
        SpreadingCorruption, // Props affect 25% larger area
        DevouringHunger,  // Maw has 50% faster animation, costs 25% more
        ForestsFavor,     // Start with 1 extra Lantern placed
        VengefulSpirit    // When essence drops below 50, all powers cost 50% less
    }

    /// <summary>
    /// ScriptableObject defining a blessing's properties and effects.
    /// </summary>
    [CreateAssetMenu(fileName = "New Blessing", menuName = "FaeMaze/Roguelike/Blessing Definition")]
    public class BlessingDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private BlessingType blessingType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("Unlock")]
        [SerializeField] private int faeDustCost = 100;
        [SerializeField] private string prerequisiteAchievement; // Empty = no prerequisite

        [Header("Effect Modifiers")]
        [Tooltip("Multiplier for essence gained from Heart consumption (1.0 = normal)")]
        [SerializeField] private float essenceFromConsumptionMultiplier = 1.0f;

        [Tooltip("Multiplier for starting essence (1.0 = normal)")]
        [SerializeField] private float startingEssenceMultiplier = 1.0f;

        [Tooltip("Multiplier for visitor movement speed (1.0 = normal)")]
        [SerializeField] private float visitorSpeedMultiplier = 1.0f;

        [Tooltip("Multiplier for spawn interval (lower = faster spawns)")]
        [SerializeField] private float spawnIntervalMultiplier = 1.0f;

        [Tooltip("Essence threshold below which Grasp costs 0 (0 = disabled)")]
        [SerializeField] private int desperateGraspThreshold = 0;

        [Tooltip("Multiplier for prop effect radius (1.0 = normal)")]
        [SerializeField] private float propRadiusMultiplier = 1.0f;

        [Tooltip("Multiplier for Maw animation speed (1.0 = normal)")]
        [SerializeField] private float mawSpeedMultiplier = 1.0f;

        [Tooltip("Multiplier for Maw essence cost (1.0 = normal)")]
        [SerializeField] private float mawCostMultiplier = 1.0f;

        [Tooltip("Number of extra lanterns at start")]
        [SerializeField] private int extraStartingLanterns = 0;

        [Tooltip("Essence threshold below which powers cost 50% less (0 = disabled)")]
        [SerializeField] private int vengefulSpiritThreshold = 0;

        #region Properties

        public BlessingType Type => blessingType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public int FaeDustCost => faeDustCost;
        public string PrerequisiteAchievement => prerequisiteAchievement;

        // Effect properties
        public float EssenceFromConsumptionMultiplier => essenceFromConsumptionMultiplier;
        public float StartingEssenceMultiplier => startingEssenceMultiplier;
        public float VisitorSpeedMultiplier => visitorSpeedMultiplier;
        public float SpawnIntervalMultiplier => spawnIntervalMultiplier;
        public int DesperateGraspThreshold => desperateGraspThreshold;
        public float PropRadiusMultiplier => propRadiusMultiplier;
        public float MawSpeedMultiplier => mawSpeedMultiplier;
        public float MawCostMultiplier => mawCostMultiplier;
        public int ExtraStartingLanterns => extraStartingLanterns;
        public int VengefulSpiritThreshold => vengefulSpiritThreshold;

        #endregion

        /// <summary>
        /// Check if this blessing has any effect on consumption essence.
        /// </summary>
        public bool AffectsConsumptionEssence => !Mathf.Approximately(essenceFromConsumptionMultiplier, 1.0f);

        /// <summary>
        /// Check if this blessing has any effect on starting essence.
        /// </summary>
        public bool AffectsStartingEssence => !Mathf.Approximately(startingEssenceMultiplier, 1.0f);

        /// <summary>
        /// Check if this blessing has any effect on visitor speed.
        /// </summary>
        public bool AffectsVisitorSpeed => !Mathf.Approximately(visitorSpeedMultiplier, 1.0f);

        /// <summary>
        /// Check if this blessing has any effect on spawn rate.
        /// </summary>
        public bool AffectsSpawnRate => !Mathf.Approximately(spawnIntervalMultiplier, 1.0f);
    }
}

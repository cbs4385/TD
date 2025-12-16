using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// ScriptableObject configuration for visitor archetypes.
    /// Defines all behavioral parameters and hazard interaction multipliers.
    /// Create instances via Assets > Create > FaeMaze > Visitor Archetype Config.
    /// </summary>
    [CreateAssetMenu(fileName = "VisitorArchetypeConfig", menuName = "FaeMaze/Visitor Archetype Config", order = 100)]
    public class VisitorArchetypeConfig : ScriptableObject, IVisitorArchetypeConfig
    {
        [Header("Archetype Identity")]
        [SerializeField] private VisitorArchetype archetype = VisitorArchetype.LanternDrunk;

        [Header("Movement")]
        [SerializeField] [Tooltip("Base movement speed in units per second")]
        private float baseSpeed = 1.0f;

        [Header("Fascination (FaeLantern)")]
        [SerializeField] [Range(0f, 1f)] [Tooltip("Base probability of becoming fascinated on entering lantern influence")]
        private float fascinationChance = 0.5f;

        [SerializeField] [Tooltip("Minimum fascination duration in seconds")]
        private float fascinationDurationMin = 2f;

        [SerializeField] [Tooltip("Maximum fascination duration in seconds")]
        private float fascinationDurationMax = 5f;

        [SerializeField] [Tooltip("Cooldown before same lantern can fascinate again (seconds)")]
        private float fascinationCooldown = 3f;

        [SerializeField] [Tooltip("Minimum lantern-induced wander detour length (tiles)")]
        private float lanternWanderDetourMin = 4f;

        [SerializeField] [Tooltip("Maximum lantern-induced wander detour length (tiles)")]
        private float lanternWanderDetourMax = 8f;

        [Header("Confusion / Lost")]
        [SerializeField] [Range(0f, 1f)] [Tooltip("Chance to take wrong branch at intersections")]
        private float confusionIntersectionChance = 0.25f;

        [SerializeField] [Tooltip("Minimum lost state detour length (tiles)")]
        private float lostDetourMin = 5f;

        [SerializeField] [Tooltip("Maximum lost state detour length (tiles)")]
        private float lostDetourMax = 10f;

        [Header("Frightened")]
        [SerializeField] [Tooltip("Duration of frightened state (seconds)")]
        private float frightenedDuration = 3f;

        [SerializeField] [Tooltip("Speed multiplier when frightened")]
        private float frightenedSpeedMultiplier = 1.2f;

        [SerializeField] [Tooltip("If true, frightened visitors repath toward nearest exit")]
        private bool frightenedPrefersExit = false;

        [Header("Mesmerized")]
        [SerializeField] [Tooltip("Initial mesmerized duration at spawn (seconds, 0 = none)")]
        private float initialMesmerizedDuration = 0f;

        [SerializeField] [Tooltip("If true, visitor starts mesmerized and heads toward Heart")]
        private bool startsMesmerized = false;

        [Header("Hazard Multipliers")]
        [SerializeField] [Tooltip("Multiplier for FairyRing slow effect (1.0 = normal, >1.0 = slower)")]
        private float fairyRingSlowMultiplier = 1.0f;

        [SerializeField] [Range(0f, 1f)] [Tooltip("Chance for Puka to teleport this archetype")]
        private float pukaTeleportChance = 0.3f;

        [SerializeField] [Range(0f, 1f)] [Tooltip("Chance for Puka to kill this archetype")]
        private float pukaKillChance = 0.1f;

        [SerializeField] [Tooltip("Weight when Wisp choosing targets (higher = more attractive)")]
        private float wispCapturePriority = 1.0f;

        [Header("Reward")]
        [SerializeField] [Tooltip("Essence rewarded when this visitor is consumed")]
        private int essenceReward = 1;

        // Interface implementation
        public VisitorArchetype Archetype => archetype;
        public float BaseSpeed => baseSpeed;
        public float FascinationChance => fascinationChance;
        public float FascinationDurationMin => fascinationDurationMin;
        public float FascinationDurationMax => fascinationDurationMax;
        public float FascinationCooldown => fascinationCooldown;
        public float LanternWanderDetourMin => lanternWanderDetourMin;
        public float LanternWanderDetourMax => lanternWanderDetourMax;
        public float ConfusionIntersectionChance => confusionIntersectionChance;
        public float LostDetourMin => lostDetourMin;
        public float LostDetourMax => lostDetourMax;
        public float FrightenedDuration => frightenedDuration;
        public float FrightenedSpeedMultiplier => frightenedSpeedMultiplier;
        public bool FrightenedPrefersExit => frightenedPrefersExit;
        public float InitialMesmerizedDuration => initialMesmerizedDuration;
        public bool StartsMesmerized => startsMesmerized;
        public float FairyRingSlowMultiplier => fairyRingSlowMultiplier;
        public float PukaTeleportChance => pukaTeleportChance;
        public float PukaKillChance => pukaKillChance;
        public float WispCapturePriority => wispCapturePriority;
        public int EssenceReward => essenceReward;
    }
}

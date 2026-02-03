using UnityEngine;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// ScriptableObject defining the properties and tunables for a Heart power.
    /// </summary>
    [CreateAssetMenu(fileName = "HeartPower", menuName = "FaeMaze/Heart Powers/Power Definition", order = 1)]
    public class HeartPowerDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The type of this Heart power")]
        public HeartPowerType powerType;

        [Tooltip("Display name shown in UI")]
        public string displayName;

        [Tooltip("Short description of what this power does")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Upgrade tier (I, II, or III)")]
        [Range(1, 3)]
        public int tier = 1;

        [Header("Costs and Cooldown")]
        [Tooltip("Essence cost as a percentage of starting essence (0.0 to 1.0). E.g., 0.5 = 50% of starting essence.")]
        [Range(0f, 1f)]
        public float essenceCostPercent = 0f;

        [Tooltip("Legacy fixed essence cost (deprecated - use essenceCostPercent instead)")]
        [HideInInspector]
        public int essenceCost = 0;

        [Tooltip("Cooldown duration in seconds")]
        public float cooldown = 10f;

        [Header("Common Parameters")]
        [Tooltip("Duration for timed effects (in seconds)")]
        public float duration = 5f;

        [Tooltip("Radius for AoE effects (in grid tiles)")]
        public float radius = 3f;

        [Header("Power-Specific Tunables")]
        [Tooltip("Generic float parameters for power-specific tuning")]
        public float param1;
        public float param2;
        public float param3;

        [Tooltip("Generic int parameters for power-specific tuning")]
        public int intParam1;
        public int intParam2;

        [Tooltip("Generic bool flags for power-specific features")]
        public bool flag1;
        public bool flag2;
    }
}

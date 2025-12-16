namespace FaeMaze.Visitors
{
    /// <summary>
    /// Interface for visitor archetype configuration data.
    /// Defines all tunable parameters for visitor behavior and hazard interactions.
    /// </summary>
    public interface IVisitorArchetypeConfig
    {
        VisitorArchetype Archetype { get; }

        // Movement
        float BaseSpeed { get; }

        // Fascination (FaeLantern)
        float FascinationChance { get; }
        float FascinationDurationMin { get; }
        float FascinationDurationMax { get; }
        float FascinationCooldown { get; }
        float LanternWanderDetourMin { get; }
        float LanternWanderDetourMax { get; }

        // Confusion / Missteps / Lost
        float ConfusionIntersectionChance { get; }
        float LostDetourMin { get; }
        float LostDetourMax { get; }

        // Frightened
        float FrightenedDuration { get; }
        float FrightenedSpeedMultiplier { get; }
        bool FrightenedPrefersExit { get; }

        // Mesmerized
        float InitialMesmerizedDuration { get; }
        bool StartsMesmerized { get; }

        // Hazard multipliers
        float FairyRingSlowMultiplier { get; }
        float PukaTeleportChance { get; }
        float PukaKillChance { get; }
        float WispCapturePriority { get; }

        // Reward
        int EssenceReward { get; }
    }
}

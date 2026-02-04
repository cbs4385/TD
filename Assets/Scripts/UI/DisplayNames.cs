using FaeMaze.Visitors;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Provides display name mappings for game elements.
    /// Blends campy aesthetics with Unseelie Court "threads of fate" lore.
    /// </summary>
    public static class DisplayNames
    {
        /// <summary>
        /// Gets the campy display name for a visitor archetype.
        /// </summary>
        public static string GetDisplayName(this VisitorArchetype archetype) => archetype switch
        {
            VisitorArchetype.LanternDrunk => "Tourist",
            VisitorArchetype.WaryWayfarer => "Nervous Nelly",
            VisitorArchetype.SleepwalkingDevotee => "Sleepwalker",
            _ => archetype.ToString()
        };

        /// <summary>
        /// Gets the campy display name for a visitor fate.
        /// </summary>
        public static string GetFateDisplayName(this VisitorFate fate) => fate switch
        {
            VisitorFate.Consumed => "Nommed",
            VisitorFate.Devoured => "Chomped",
            VisitorFate.FairyRing => "Danced Out",
            VisitorFate.Lantern => "Dazzled",
            VisitorFate.GoblinKill => "Goblin'd",
            VisitorFate.Drowned => "Splashed",
            VisitorFate.Escaped => "Got Away",
            _ => fate.ToString()
        };

        /// <summary>
        /// Gets the campy display name for an essence source.
        /// </summary>
        public static string GetSourceDisplayName(this EssenceSource source) => source switch
        {
            EssenceSource.StartingEssence => "Starting Threads",
            EssenceSource.EssenceDecay => "Thread Decay",
            EssenceSource.LanternFascination => "Dazzle Bonus",
            EssenceSource.VisitorConsumedByHeart => "Nommed Threads",
            EssenceSource.VisitorConsumedByMaw => "Chomped Threads",
            EssenceSource.GoblinPenalty => "Goblin Tax",
            EssenceSource.HeartPowerCost => "Power Cost",
            EssenceSource.HeartPowerBonus => "Power Bonus",
            EssenceSource.RingTithe => "Ring Tithe",
            EssenceSource.PukaGift => "Puka Gift",
            _ => source.ToString()
        };
    }
}

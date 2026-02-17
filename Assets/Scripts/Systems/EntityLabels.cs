using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Generates unique, human-readable labels for all game entities.
    /// Labels match what's shown as floating text in-game and used in console logs.
    /// </summary>
    public static class EntityLabels
    {
        private static readonly Dictionary<string, int> visitorCounters = new Dictionary<string, int>();
        private static int goblinCounter;

        /// <summary>
        /// Resets all counters. Call at the start of each game.
        /// </summary>
        public static void Reset()
        {
            visitorCounters.Clear();
            goblinCounter = 0;
        }

        /// <summary>
        /// Generates a unique label for a visitor, e.g. "Visitor#1", "Mistaking#3", "Wary#7".
        /// </summary>
        public static string GenerateVisitorLabel(FaeMaze.Visitors.VisitorControllerBase visitor)
        {
            if (visitor == null) return "Unknown#0";

            string typeName = visitor.GetType().Name.Replace("Controller", "");

            // Shorten common names for readability
            if (typeName == "MistakingVisitor") typeName = "Mistaking";
            else if (typeName == "LanternDrunkVisitor") typeName = "LanternDrunk";
            else if (typeName == "WaryWayfarerVisitor") typeName = "Wary";
            else if (typeName == "SleepwalkingDevotee") typeName = "Sleepwalker";
            else if (typeName == "MistakingVisitor_FestivalTourist") typeName = "FestTourist";

            if (!visitorCounters.ContainsKey(typeName))
            {
                visitorCounters[typeName] = 0;
            }

            visitorCounters[typeName]++;
            return $"{typeName}#{visitorCounters[typeName]}";
        }

        /// <summary>
        /// Generates a unique label for a Goblin enemy, e.g. "Goblin#1".
        /// </summary>
        public static string GenerateEnemyLabel(string type)
        {
            goblinCounter++;
            return $"{type}#{goblinCounter}";
        }

        /// <summary>
        /// Derives a prop label from the GameObject name.
        /// "Lantern_Node5" → "Lantern@N5", "FairyRing_Node3" → "Ring@N3",
        /// "Pond_Node7" → "Pond@N7", "Wisp_Node2" → "Wisp@N2".
        /// </summary>
        public static string GetPropLabel(GameObject prop)
        {
            if (prop == null) return "Prop@?";

            string name = prop.name;

            // Parse "Type_NodeN" pattern
            int nodeIdx = name.IndexOf("_Node");
            if (nodeIdx >= 0)
            {
                string type = name.Substring(0, nodeIdx);
                string nodeNum = name.Substring(nodeIdx + 5); // skip "_Node"

                // Shorten FairyRing to Ring
                if (type == "FairyRing") type = "Ring";

                return $"{type}@N{nodeNum}";
            }

            return name;
        }
    }
}

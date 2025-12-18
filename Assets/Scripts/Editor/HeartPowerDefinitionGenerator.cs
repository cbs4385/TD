using UnityEngine;
using UnityEditor;
using System.IO;
using FaeMaze.HeartPowers;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to generate HeartPowerDefinition assets for all Heart Powers.
    /// </summary>
    public class HeartPowerDefinitionGenerator
    {
        [MenuItem("FaeMaze/Heart Powers/Generate Power Definitions")]
        public static void GeneratePowerDefinitions()
        {
            // Ensure the directory exists
            string assetPath = "Assets/ScriptableObjects/HeartPowers";
            if (!AssetDatabase.IsValidFolder(assetPath))
            {
                // Create parent folder if needed
                if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                {
                    AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
                }
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "HeartPowers");
            }

            // Generate definitions for each power type
            CreatePowerDefinition(HeartPowerType.HeartbeatOfLonging, "Heartbeat of Longing",
                "Amplifies all FaeLanterns, granting temporary power boost to nearby towers.",
                assetPath, chargeCost: 1, cooldown: 15f, duration: 10f, radius: 5f);

            CreatePowerDefinition(HeartPowerType.MurmuringPaths, "Murmuring Paths",
                "Marks a tile as \"alluring\" increasing its path cost for visitors.",
                assetPath, chargeCost: 1, cooldown: 5f, duration: 30f, radius: 1f, param1: 2.0f);

            CreatePowerDefinition(HeartPowerType.DreamSnare, "Dream Snare",
                "Stuns visitors in a target area for a short duration.",
                assetPath, chargeCost: 1, cooldown: 20f, duration: 3f, radius: 2f);

            CreatePowerDefinition(HeartPowerType.FeastwardPanic, "Feastward Panic",
                "Scatters visitors away from a target point, sending them on detours.",
                assetPath, chargeCost: 1, cooldown: 25f, duration: 5f, radius: 3f, param1: 8f);

            CreatePowerDefinition(HeartPowerType.CovenantWithWisps, "Covenant with Wisps",
                "Grants bonus essence when visitors are captured for a duration.",
                assetPath, chargeCost: 1, cooldown: 30f, duration: 20f, radius: 0f, param1: 0.5f);

            CreatePowerDefinition(HeartPowerType.PukasBargain, "Puka's Bargain",
                "Creates a temporary trap tile at target location that stuns visitors.",
                assetPath, chargeCost: 1, cooldown: 15f, duration: 60f, radius: 1f, param1: 2f);

            CreatePowerDefinition(HeartPowerType.RingOfInvitations, "Ring of Invitations",
                "Spawns temporary Wisp guardians around the heart to defend against visitors.",
                assetPath, chargeCost: 2, cooldown: 40f, duration: 15f, radius: 4f, intParam1: 4);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        private static void CreatePowerDefinition(
            HeartPowerType powerType,
            string displayName,
            string description,
            string assetPath,
            int chargeCost = 1,
            float cooldown = 10f,
            float duration = 5f,
            float radius = 3f,
            float param1 = 0f,
            float param2 = 0f,
            float param3 = 0f,
            int intParam1 = 0,
            int intParam2 = 0)
        {
            string fileName = $"{powerType}Definition.asset";
            string fullPath = Path.Combine(assetPath, fileName);

            // Check if asset already exists
            HeartPowerDefinition existing = AssetDatabase.LoadAssetAtPath<HeartPowerDefinition>(fullPath);
            if (existing != null)
            {
                return;
            }

            // Create new definition
            HeartPowerDefinition definition = ScriptableObject.CreateInstance<HeartPowerDefinition>();
            definition.powerType = powerType;
            definition.displayName = displayName;
            definition.description = description;
            definition.tier = 1;
            definition.chargeCost = chargeCost;
            definition.cooldown = cooldown;
            definition.duration = duration;
            definition.radius = radius;
            definition.param1 = param1;
            definition.param2 = param2;
            definition.param3 = param3;
            definition.intParam1 = intParam1;
            definition.intParam2 = intParam2;

            // Create asset
            AssetDatabase.CreateAsset(definition, fullPath);
        }
    }
}

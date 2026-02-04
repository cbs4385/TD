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
            CreatePowerDefinition(HeartPowerType.MurmuringPaths, "Murmuring Paths",
                "Marks a tile as \"alluring\" increasing its path cost for visitors.",
                assetPath, duration: 30f, radius: 1f, param1: 2.0f);

            CreatePowerDefinition(HeartPowerType.HeartwardGrasp, "Heartward Grasp",
                "Pulls a visitor through walls toward the Heart.",
                assetPath, duration: 2f, radius: 3f, param1: 3f);

            CreatePowerDefinition(HeartPowerType.DevouringMaw, "Devouring Maw",
                "Instantly consumes a visitor at the targeted position.",
                assetPath, duration: 1f, radius: 1f);

            CreatePowerDefinition(HeartPowerType.Sculpting, "Sculpting",
                "Change the hazard type on a node. Must be activated on a node (not an edge).",
                assetPath, duration: 0f, radius: 3f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }

        private static void CreatePowerDefinition(
            HeartPowerType powerType,
            string displayName,
            string description,
            string assetPath,
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

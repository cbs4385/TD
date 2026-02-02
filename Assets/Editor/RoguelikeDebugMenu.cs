using UnityEditor;
using UnityEngine;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor menu for testing roguelike features without playing.
    /// </summary>
    public static class RoguelikeDebugMenu
    {
        [MenuItem("FaeMaze/Roguelike Debug/Add 500 Fae Dust")]
        public static void Add500FaeDust()
        {
            int current = PlayerPrefs.GetInt("FaeMaze_FaeDust", 0);
            PlayerPrefs.SetInt("FaeMaze_FaeDust", current + 500);
            PlayerPrefs.Save();
            Debug.Log($"[RoguelikeDebug] Added 500 Fae Dust. Total: {current + 500}");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Add 1000 Fae Dust")]
        public static void Add1000FaeDust()
        {
            int current = PlayerPrefs.GetInt("FaeMaze_FaeDust", 0);
            PlayerPrefs.SetInt("FaeMaze_FaeDust", current + 1000);
            PlayerPrefs.Save();
            Debug.Log($"[RoguelikeDebug] Added 1000 Fae Dust. Total: {current + 1000}");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Show Current Fae Dust")]
        public static void ShowFaeDust()
        {
            int current = PlayerPrefs.GetInt("FaeMaze_FaeDust", 0);
            Debug.Log($"[RoguelikeDebug] Current Fae Dust: {current}");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Unlock All Blessings")]
        public static void UnlockAllBlessings()
        {
            string[] blessingTypes = {
                "GreedyHeart",
                "PatientHunter",
                "DesperateGrasp",
                "SpreadingCorruption",
                "DevouringHunger",
                "ForestsFavor",
                "VengefulSpirit"
            };

            foreach (var type in blessingTypes)
            {
                PlayerPrefs.SetInt($"FaeMaze_Blessing_{type}", 1);
            }
            PlayerPrefs.Save();
            Debug.Log($"[RoguelikeDebug] Unlocked all {blessingTypes.Length} blessings");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Unlock All Power Tiers")]
        public static void UnlockAllPowerTiers()
        {
            string[] powers = { "MurmuringPaths", "HeartwardGrasp", "DevouringMaw", "Sculpting" };
            int[] tiers = { 2, 3 };

            foreach (var power in powers)
            {
                foreach (var tier in tiers)
                {
                    string key = $"FaeMaze_Unlock_PowerTier_{power}_T{tier}";
                    PlayerPrefs.SetInt(key, 1);
                }
            }
            PlayerPrefs.Save();
            Debug.Log("[RoguelikeDebug] Unlocked all power tiers (T2 and T3 for all powers)");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Reset All Roguelike Progress")]
        public static void ResetAllProgress()
        {
            if (!EditorUtility.DisplayDialog("Reset Roguelike Progress",
                "This will delete ALL roguelike progress including Fae Dust, unlocks, and blessings. Are you sure?",
                "Yes, Reset Everything", "Cancel"))
            {
                return;
            }

            // Delete Fae Dust
            PlayerPrefs.DeleteKey("FaeMaze_FaeDust");

            // Delete blessing unlocks
            string[] blessingTypes = {
                "GreedyHeart", "PatientHunter", "DesperateGrasp",
                "SpreadingCorruption", "DevouringHunger", "ForestsFavor", "VengefulSpirit"
            };
            foreach (var type in blessingTypes)
            {
                PlayerPrefs.DeleteKey($"FaeMaze_Blessing_{type}");
            }

            // Delete power tier unlocks
            string[] powers = { "MurmuringPaths", "HeartwardGrasp", "DevouringMaw", "Sculpting" };
            foreach (var power in powers)
            {
                PlayerPrefs.DeleteKey($"FaeMaze_Unlock_PowerTier_{power}_T2");
                PlayerPrefs.DeleteKey($"FaeMaze_Unlock_PowerTier_{power}_T3");
            }

            // Delete lifetime stats
            PlayerPrefs.DeleteKey("FaeMaze_TotalRuns");
            PlayerPrefs.DeleteKey("FaeMaze_LastDailyRun");

            PlayerPrefs.Save();
            Debug.Log("[RoguelikeDebug] Reset all roguelike progress");
        }

        [MenuItem("FaeMaze/Roguelike Debug/Log All PlayerPrefs (Roguelike)")]
        public static void LogAllRoguelikePrefs()
        {
            Debug.Log("=== Roguelike PlayerPrefs ===");
            Debug.Log($"Fae Dust: {PlayerPrefs.GetInt("FaeMaze_FaeDust", 0)}");
            Debug.Log($"Total Runs: {PlayerPrefs.GetInt("FaeMaze_TotalRuns", 0)}");

            // Blessings
            string[] blessingTypes = {
                "GreedyHeart", "PatientHunter", "DesperateGrasp",
                "SpreadingCorruption", "DevouringHunger", "ForestsFavor", "VengefulSpirit"
            };
            Debug.Log("--- Blessings ---");
            foreach (var type in blessingTypes)
            {
                bool unlocked = PlayerPrefs.GetInt($"FaeMaze_Blessing_{type}", 0) == 1;
                Debug.Log($"  {type}: {(unlocked ? "UNLOCKED" : "locked")}");
            }

            // Power Tiers
            string[] powers = { "MurmuringPaths", "HeartwardGrasp", "DevouringMaw", "Sculpting" };
            Debug.Log("--- Power Tiers ---");
            foreach (var power in powers)
            {
                bool t2 = PlayerPrefs.GetInt($"FaeMaze_Unlock_PowerTier_{power}_T2", 0) == 1;
                bool t3 = PlayerPrefs.GetInt($"FaeMaze_Unlock_PowerTier_{power}_T3", 0) == 1;
                Debug.Log($"  {power}: T2={t2}, T3={t3}");
            }
        }
    }
}

using FaeMaze.Audio;
using FaeMaze.Cameras;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Centralized game settings that persist across sessions using PlayerPrefs
    /// </summary>
    public static class GameSettings
    {
        // Audio Settings
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("SfxVolume", 1f);
            set => PlayerPrefs.SetFloat("SfxVolume", Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("MusicVolume", 1f);
            set => PlayerPrefs.SetFloat("MusicVolume", Mathf.Clamp01(value));
        }

        // Camera Settings
        public static float CameraPanSpeed
        {
            get => PlayerPrefs.GetFloat("CameraPanSpeed", 10f);
            set => PlayerPrefs.SetFloat("CameraPanSpeed", Mathf.Max(1f, value));
        }

        public static float CameraZoomSpeed
        {
            get => PlayerPrefs.GetFloat("CameraZoomSpeed", 5f);
            set => PlayerPrefs.SetFloat("CameraZoomSpeed", Mathf.Max(1f, value));
        }

        public static float CameraMinZoom
        {
            get => PlayerPrefs.GetFloat("CameraMinZoom", 3f);
            set => PlayerPrefs.SetFloat("CameraMinZoom", Mathf.Max(1f, value));
        }

        public static float CameraMaxZoom
        {
            get => PlayerPrefs.GetFloat("CameraMaxZoom", 20f);
            set => PlayerPrefs.SetFloat("CameraMaxZoom", Mathf.Max(5f, value));
        }

        // Visitor Gameplay Settings
        public static float VisitorSpeed
        {
            get => PlayerPrefs.GetFloat("VisitorSpeed", 3f);
            set => PlayerPrefs.SetFloat("VisitorSpeed", Mathf.Max(0.5f, value));
        }

        public static bool ConfusionEnabled
        {
            get => PlayerPrefs.GetInt("ConfusionEnabled", 1) == 1;
            set => PlayerPrefs.SetInt("ConfusionEnabled", value ? 1 : 0);
        }

        public static float ConfusionChance
        {
            get => PlayerPrefs.GetFloat("ConfusionChance", 0.25f);
            set => PlayerPrefs.SetFloat("ConfusionChance", Mathf.Clamp01(value));
        }

        public static int ConfusionDistanceMin
        {
            get => PlayerPrefs.GetInt("ConfusionDistanceMin", 15);
            set => PlayerPrefs.SetInt("ConfusionDistanceMin", Mathf.Max(1, value));
        }

        public static int ConfusionDistanceMax
        {
            get => PlayerPrefs.GetInt("ConfusionDistanceMax", 20);
            set => PlayerPrefs.SetInt("ConfusionDistanceMax", Mathf.Max(1, value));
        }

        // Wave/Difficulty Settings
        public static int VisitorsPerWave
        {
            get => PlayerPrefs.GetInt("VisitorsPerWave", 10);
            set => PlayerPrefs.SetInt("VisitorsPerWave", Mathf.Max(1, value));
        }

        public static float SpawnInterval
        {
            get => PlayerPrefs.GetFloat("SpawnInterval", 1f);
            set => PlayerPrefs.SetFloat("SpawnInterval", Mathf.Max(0.1f, value));
        }

        public static float WaveDuration
        {
            get => PlayerPrefs.GetFloat("WaveDuration", 60f);
            set => PlayerPrefs.SetFloat("WaveDuration", Mathf.Max(10f, value));
        }

        public static bool EnableRedCap
        {
            get => PlayerPrefs.GetInt("EnableRedCap", 1) == 1;
            set => PlayerPrefs.SetInt("EnableRedCap", value ? 1 : 0);
        }

        public static float RedCapSpawnDelay
        {
            get => PlayerPrefs.GetFloat("RedCapSpawnDelay", 60f);
            set => PlayerPrefs.SetFloat("RedCapSpawnDelay", Mathf.Max(0f, value));
        }

        // Game Flow Settings
        public static bool AutoStartNextWave
        {
            get => PlayerPrefs.GetInt("AutoStartNextWave", 0) == 1;
            set => PlayerPrefs.SetInt("AutoStartNextWave", value ? 1 : 0);
        }

        public static float AutoStartDelay
        {
            get => PlayerPrefs.GetFloat("AutoStartDelay", 2f);
            set => PlayerPrefs.SetFloat("AutoStartDelay", Mathf.Max(0f, value));
        }

        public static int StartingEssence
        {
            get => PlayerPrefs.GetInt("StartingEssence", 100);
            set => PlayerPrefs.SetInt("StartingEssence", Mathf.Max(0, value));
        }

        /// <summary>
        /// Reset all settings to default values
        /// </summary>
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Save all current settings to disk
        /// </summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Apply current settings to active game systems
        /// </summary>
        public static void ApplySettings()
        {
            // Apply audio settings
            SoundManager soundManager = Object.FindFirstObjectByType<SoundManager>();
            if (soundManager != null)
            {
                soundManager.SetSfxVolume(SfxVolume);
                soundManager.SetMusicVolume(MusicVolume);
            }

            // Apply camera settings
            CameraController2D cameraController = Object.FindFirstObjectByType<CameraController2D>();
            if (cameraController != null)
            {
                // Note: CameraController2D fields are not directly accessible
                // They should be exposed via public setters or made serializable
            }

            // Other systems will read from GameSettings directly when initialized
        }
    }
}

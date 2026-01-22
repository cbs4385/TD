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
        // Video Settings
        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            set => PlayerPrefs.SetInt("Fullscreen", value ? 1 : 0);
        }

        public static int ResolutionIndex
        {
            get => PlayerPrefs.GetInt("ResolutionIndex", -1); // -1 means use current/default
            set => PlayerPrefs.SetInt("ResolutionIndex", value);
        }

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

        // Prop Sound Volume Settings (individual controls for each prop type)
        public static float LanternVolume
        {
            get => PlayerPrefs.GetFloat("LanternVolume", 1f);
            set => PlayerPrefs.SetFloat("LanternVolume", Mathf.Clamp01(value));
        }

        public static float FairyRingVolume
        {
            get => PlayerPrefs.GetFloat("FairyRingVolume", 1f);
            set => PlayerPrefs.SetFloat("FairyRingVolume", Mathf.Clamp01(value));
        }

        public static float PondVolume
        {
            get => PlayerPrefs.GetFloat("PondVolume", 1f);
            set => PlayerPrefs.SetFloat("PondVolume", Mathf.Clamp01(value));
        }

        public static float SculptVolume
        {
            get => PlayerPrefs.GetFloat("SculptVolume", 1f);
            set => PlayerPrefs.SetFloat("SculptVolume", Mathf.Clamp01(value));
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

        public static float CameraMovementSpeed
        {
            get => PlayerPrefs.GetFloat("CameraMovementSpeed", 1f);
            set => PlayerPrefs.SetFloat("CameraMovementSpeed", Mathf.Max(0.1f, value));
        }

        public static float CameraFieldOfView
        {
            get => PlayerPrefs.GetFloat("CameraFieldOfView", 60f);
            set => PlayerPrefs.SetFloat("CameraFieldOfView", Mathf.Clamp(value, 30f, 120f));
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

        // Spawning Settings
        public static float SpawnInterval
        {
            get => Mathf.Max(0.1f, PlayerPrefs.GetFloat("SpawnInterval", 1f));
            set => PlayerPrefs.SetFloat("SpawnInterval", Mathf.Max(0.1f, value));
        }

        public static bool EnableRedCap
        {
            get => PlayerPrefs.GetInt("EnableRedCap", 1) == 1;
            set => PlayerPrefs.SetInt("EnableRedCap", value ? 1 : 0);
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

        public static bool EssenceDecayEnabled
        {
            get => PlayerPrefs.GetInt("EssenceDecayEnabled", 1) == 1;
            set => PlayerPrefs.SetInt("EssenceDecayEnabled", value ? 1 : 0);
        }

        public static float EssenceDecayRate
        {
            get => PlayerPrefs.GetFloat("EssenceDecayRate", 1f);
            set => PlayerPrefs.SetFloat("EssenceDecayRate", Mathf.Max(0f, value));
        }

        // Screenshot Settings
        private static string DefaultScreenshotPath => System.IO.Path.Combine(Application.persistentDataPath, "Screenshots");

        public static string ScreenshotPath
        {
            get
            {
                string path = PlayerPrefs.GetString("ScreenshotPath", "");
                return string.IsNullOrEmpty(path) ? DefaultScreenshotPath : path;
            }
            set => PlayerPrefs.SetString("ScreenshotPath", value ?? "");
        }

        public static KeyCode ScreenshotKey
        {
            get => (KeyCode)PlayerPrefs.GetInt("ScreenshotKey", (int)KeyCode.F12);
            set => PlayerPrefs.SetInt("ScreenshotKey", (int)value);
        }

        // Player Control Settings
        public static float FocusSpeed
        {
            get => PlayerPrefs.GetFloat("FocusSpeed", 10f);
            set => PlayerPrefs.SetFloat("FocusSpeed", Mathf.Clamp(value, 5f, 15f));
        }

        // Heart Power Keybindings
        public static KeyCode HeartPower1Key
        {
            get => (KeyCode)PlayerPrefs.GetInt("HeartPower1Key", (int)KeyCode.Alpha1);
            set => PlayerPrefs.SetInt("HeartPower1Key", (int)value);
        }

        public static KeyCode HeartPower2Key
        {
            get => (KeyCode)PlayerPrefs.GetInt("HeartPower2Key", (int)KeyCode.Alpha2);
            set => PlayerPrefs.SetInt("HeartPower2Key", (int)value);
        }

        public static KeyCode HeartPower3Key
        {
            get => (KeyCode)PlayerPrefs.GetInt("HeartPower3Key", (int)KeyCode.Alpha3);
            set => PlayerPrefs.SetInt("HeartPower3Key", (int)value);
        }

        public static KeyCode HeartPower4Key
        {
            get => (KeyCode)PlayerPrefs.GetInt("HeartPower4Key", (int)KeyCode.Alpha4);
            set => PlayerPrefs.SetInt("HeartPower4Key", (int)value);
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
            // Apply video settings
            ApplyVideoSettings();

            // Apply audio settings
            SoundManager soundManager = Object.FindFirstObjectByType<SoundManager>();
            if (soundManager != null)
            {
                soundManager.SetSfxVolume(SfxVolume);
                soundManager.SetMusicVolume(MusicVolume);
            }

            // Apply camera settings
            CameraController3D cameraController = Object.FindFirstObjectByType<CameraController3D>();
            if (cameraController != null)
            {
                cameraController.ApplySettingsFromGameSettings();
            }

            // Other systems will read from GameSettings directly when initialized
        }

        /// <summary>
        /// Apply video settings (fullscreen and resolution)
        /// </summary>
        public static void ApplyVideoSettings()
        {
            Screen.fullScreen = Fullscreen;

            if (ResolutionIndex >= 0)
            {
                Resolution[] resolutions = Screen.resolutions;
                if (ResolutionIndex < resolutions.Length)
                {
                    Resolution res = resolutions[ResolutionIndex];
                    Screen.SetResolution(res.width, res.height, Fullscreen);
                }
            }
        }
    }
}

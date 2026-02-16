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

        // Store actual resolution dimensions (more reliable than index)
        public static int ResolutionWidth
        {
            get => PlayerPrefs.GetInt("ResolutionWidth", 0); // 0 means use current/default
            set => PlayerPrefs.SetInt("ResolutionWidth", value);
        }

        public static int ResolutionHeight
        {
            get => PlayerPrefs.GetInt("ResolutionHeight", 0); // 0 means use current/default
            set => PlayerPrefs.SetInt("ResolutionHeight", value);
        }

        /// <summary>
        /// Light level intensity for the directional light (0.0 to 2.0, default 0.9)
        /// </summary>
        public static float LightLevel
        {
            get => PlayerPrefs.GetFloat("LightLevel", 0.9f);
            set => PlayerPrefs.SetFloat("LightLevel", Mathf.Clamp(value, 0f, 2f));
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
            get => Mathf.Max(0.1f, PlayerPrefs.GetFloat("SpawnInterval", 5f));
            set => PlayerPrefs.SetFloat("SpawnInterval", Mathf.Max(0.1f, value));
        }

        public static bool EnableGoblin
        {
            get => PlayerPrefs.GetInt("EnableGoblin", 1) == 1;
            set => PlayerPrefs.SetInt("EnableGoblin", value ? 1 : 0);
        }

        // Random Seed Settings
        /// <summary>
        /// Random seed for deterministic gameplay. Set to 0 for time-based random seed.
        /// </summary>
        public static int RandomSeed
        {
            get => PlayerPrefs.GetInt("RandomSeed", 0);
            set => PlayerPrefs.SetInt("RandomSeed", value);
        }

        /// <summary>
        /// If true, use the specified RandomSeed. If false, generate a new random seed each game.
        /// </summary>
        public static bool UseFixedSeed
        {
            get => PlayerPrefs.GetInt("UseFixedSeed", 0) == 1;
            set => PlayerPrefs.SetInt("UseFixedSeed", value ? 1 : 0);
        }

        // Game Flow Settings
        public static float AutoStartDelay
        {
            get => PlayerPrefs.GetFloat("AutoStartDelay", 2f);
            set => PlayerPrefs.SetFloat("AutoStartDelay", Mathf.Max(0f, value));
        }

        public static int StartingEssence
        {
            get => PlayerPrefs.GetInt("StartingEssence", 200);
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

        // Player Control Settings
        public static float FocusSpeed
        {
            get => PlayerPrefs.GetFloat("FocusSpeed", 10f);
            set => PlayerPrefs.SetFloat("FocusSpeed", Mathf.Clamp(value, 5f, 15f));
        }

        // Heart Power Keybindings (string-based for flexibility with keyboard/mouse)
        public static string HeartPower1Binding
        {
            get => PlayerPrefs.GetString("HeartPower1Binding", "Alpha1");
            set => PlayerPrefs.SetString("HeartPower1Binding", value);
        }

        public static string HeartPower2Binding
        {
            get => PlayerPrefs.GetString("HeartPower2Binding", "Alpha2");
            set => PlayerPrefs.SetString("HeartPower2Binding", value);
        }

        public static string HeartPower3Binding
        {
            get => PlayerPrefs.GetString("HeartPower3Binding", "Alpha3");
            set => PlayerPrefs.SetString("HeartPower3Binding", value);
        }

        public static string HeartPower4Binding
        {
            get => PlayerPrefs.GetString("HeartPower4Binding", "Alpha4");
            set => PlayerPrefs.SetString("HeartPower4Binding", value);
        }

        public static string HeartPower5Binding
        {
            get => PlayerPrefs.GetString("HeartPower5Binding", "Alpha5");
            set => PlayerPrefs.SetString("HeartPower5Binding", value);
        }

        // Legacy KeyCode properties for backwards compatibility
        public static KeyCode HeartPower1Key
        {
            get => InputBindingHelper.ParseKeyCode(HeartPower1Binding);
            set => HeartPower1Binding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        public static KeyCode HeartPower2Key
        {
            get => InputBindingHelper.ParseKeyCode(HeartPower2Binding);
            set => HeartPower2Binding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        public static KeyCode HeartPower3Key
        {
            get => InputBindingHelper.ParseKeyCode(HeartPower3Binding);
            set => HeartPower3Binding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        public static KeyCode HeartPower4Key
        {
            get => InputBindingHelper.ParseKeyCode(HeartPower4Binding);
            set => HeartPower4Binding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        public static KeyCode HeartPower5Key
        {
            get => InputBindingHelper.ParseKeyCode(HeartPower5Binding);
            set => HeartPower5Binding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        // Sculpt Menu Keybindings (when sculpt radial menu is open)
        // Using Z, X, C, V to avoid conflicts with camera WASD
        public static string SculptPondBinding
        {
            get => PlayerPrefs.GetString("SculptPondBinding", "Z");
            set => PlayerPrefs.SetString("SculptPondBinding", value);
        }

        public static string SculptLanternBinding
        {
            get => PlayerPrefs.GetString("SculptLanternBinding", "X");
            set => PlayerPrefs.SetString("SculptLanternBinding", value);
        }

        public static string SculptRingBinding
        {
            get => PlayerPrefs.GetString("SculptRingBinding", "C");
            set => PlayerPrefs.SetString("SculptRingBinding", value);
        }

        public static string SculptRemoveBinding
        {
            get => PlayerPrefs.GetString("SculptRemoveBinding", "V");
            set => PlayerPrefs.SetString("SculptRemoveBinding", value);
        }

        // Camera Movement Keybindings
        public static string CameraMoveForwardBinding
        {
            get => PlayerPrefs.GetString("CameraMoveForwardBinding", "W");
            set => PlayerPrefs.SetString("CameraMoveForwardBinding", value);
        }

        public static string CameraMoveBackwardBinding
        {
            get => PlayerPrefs.GetString("CameraMoveBackwardBinding", "S");
            set => PlayerPrefs.SetString("CameraMoveBackwardBinding", value);
        }

        public static string CameraTurnLeftBinding
        {
            get => PlayerPrefs.GetString("CameraTurnLeftBinding", "Q");
            set => PlayerPrefs.SetString("CameraTurnLeftBinding", value);
        }

        public static string CameraTurnRightBinding
        {
            get => PlayerPrefs.GetString("CameraTurnRightBinding", "E");
            set => PlayerPrefs.SetString("CameraTurnRightBinding", value);
        }

        // Camera Strafe Keybindings
        public static string CameraStrafeLeftBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeLeftBinding", "A");
            set => PlayerPrefs.SetString("CameraStrafeLeftBinding", value);
        }

        public static string CameraStrafeRightBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeRightBinding", "D");
            set => PlayerPrefs.SetString("CameraStrafeRightBinding", value);
        }

        // Camera Movement Alt Keybindings (default to arrow keys)
        public static string CameraMoveForwardAltBinding
        {
            get => PlayerPrefs.GetString("CameraMoveForwardAltBinding", "UpArrow");
            set => PlayerPrefs.SetString("CameraMoveForwardAltBinding", value);
        }

        public static string CameraMoveBackwardAltBinding
        {
            get => PlayerPrefs.GetString("CameraMoveBackwardAltBinding", "DownArrow");
            set => PlayerPrefs.SetString("CameraMoveBackwardAltBinding", value);
        }

        public static string CameraTurnLeftAltBinding
        {
            get => PlayerPrefs.GetString("CameraTurnLeftAltBinding", "");
            set => PlayerPrefs.SetString("CameraTurnLeftAltBinding", value);
        }

        public static string CameraTurnRightAltBinding
        {
            get => PlayerPrefs.GetString("CameraTurnRightAltBinding", "");
            set => PlayerPrefs.SetString("CameraTurnRightAltBinding", value);
        }

        // Camera Strafe Alt Keybindings (default to arrow keys)
        public static string CameraStrafeLeftAltBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeLeftAltBinding", "LeftArrow");
            set => PlayerPrefs.SetString("CameraStrafeLeftAltBinding", value);
        }

        public static string CameraStrafeRightAltBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeRightAltBinding", "RightArrow");
            set => PlayerPrefs.SetString("CameraStrafeRightAltBinding", value);
        }

        // Camera Focus Shortcuts
        // Using F5/F6/F7 to avoid conflicts with HeartPower 1/2/3/4
        public static string CameraFocusHeartBinding
        {
            get => PlayerPrefs.GetString("CameraFocusHeartBinding", "F5");
            set => PlayerPrefs.SetString("CameraFocusHeartBinding", value);
        }

        public static string CameraFocusEntranceBinding
        {
            get => PlayerPrefs.GetString("CameraFocusEntranceBinding", "F6");
            set => PlayerPrefs.SetString("CameraFocusEntranceBinding", value);
        }

        public static string CameraFocusVisitorBinding
        {
            get => PlayerPrefs.GetString("CameraFocusVisitorBinding", "F7");
            set => PlayerPrefs.SetString("CameraFocusVisitorBinding", value);
        }

        // Camera Viewing Angle Keybindings
        public static string CameraAngleUpBinding
        {
            get => PlayerPrefs.GetString("CameraAngleUpBinding", "PageUp");
            set => PlayerPrefs.SetString("CameraAngleUpBinding", value);
        }

        public static string CameraAngleDownBinding
        {
            get => PlayerPrefs.GetString("CameraAngleDownBinding", "PageDown");
            set => PlayerPrefs.SetString("CameraAngleDownBinding", value);
        }

        // Camera Mouse Bindings
        public static string CameraForwardBinding
        {
            get => PlayerPrefs.GetString("CameraForwardBinding", "Mouse0");
            set => PlayerPrefs.SetString("CameraForwardBinding", value);
        }

        public static string CameraOrbitBinding
        {
            get => PlayerPrefs.GetString("CameraOrbitBinding", "Mouse1");
            set => PlayerPrefs.SetString("CameraOrbitBinding", value);
        }

        public static string CameraPanBinding
        {
            get => PlayerPrefs.GetString("CameraPanBinding", "Mouse2");
            set => PlayerPrefs.SetString("CameraPanBinding", value);
        }

        // Screenshot Binding (string-based)
        public static string ScreenshotBinding
        {
            get => PlayerPrefs.GetString("ScreenshotBinding", "F12");
            set => PlayerPrefs.SetString("ScreenshotBinding", value);
        }

        // Legacy ScreenshotKey for backwards compatibility
        public static KeyCode ScreenshotKey
        {
            get => InputBindingHelper.ParseKeyCode(ScreenshotBinding);
            set => ScreenshotBinding = InputBindingHelper.KeyCodeToBindingString(value);
        }

        // ===== Alt Bindings (column 2) for controls that previously had no alt =====
        // Heart Power Alt Bindings (default empty - no alt binding)
        public static string HeartPower1AltBinding
        {
            get => PlayerPrefs.GetString("HeartPower1AltBinding", "");
            set => PlayerPrefs.SetString("HeartPower1AltBinding", value);
        }
        public static string HeartPower2AltBinding
        {
            get => PlayerPrefs.GetString("HeartPower2AltBinding", "");
            set => PlayerPrefs.SetString("HeartPower2AltBinding", value);
        }
        public static string HeartPower3AltBinding
        {
            get => PlayerPrefs.GetString("HeartPower3AltBinding", "");
            set => PlayerPrefs.SetString("HeartPower3AltBinding", value);
        }
        public static string HeartPower4AltBinding
        {
            get => PlayerPrefs.GetString("HeartPower4AltBinding", "");
            set => PlayerPrefs.SetString("HeartPower4AltBinding", value);
        }
        public static string HeartPower5AltBinding
        {
            get => PlayerPrefs.GetString("HeartPower5AltBinding", "");
            set => PlayerPrefs.SetString("HeartPower5AltBinding", value);
        }

        // Sculpt Menu Alt Bindings (default empty)
        public static string SculptPondAltBinding
        {
            get => PlayerPrefs.GetString("SculptPondAltBinding", "");
            set => PlayerPrefs.SetString("SculptPondAltBinding", value);
        }
        public static string SculptLanternAltBinding
        {
            get => PlayerPrefs.GetString("SculptLanternAltBinding", "");
            set => PlayerPrefs.SetString("SculptLanternAltBinding", value);
        }
        public static string SculptRingAltBinding
        {
            get => PlayerPrefs.GetString("SculptRingAltBinding", "");
            set => PlayerPrefs.SetString("SculptRingAltBinding", value);
        }
        public static string SculptRemoveAltBinding
        {
            get => PlayerPrefs.GetString("SculptRemoveAltBinding", "");
            set => PlayerPrefs.SetString("SculptRemoveAltBinding", value);
        }

        // Camera Focus Alt Bindings (default empty)
        public static string CameraFocusHeartAltBinding
        {
            get => PlayerPrefs.GetString("CameraFocusHeartAltBinding", "");
            set => PlayerPrefs.SetString("CameraFocusHeartAltBinding", value);
        }
        public static string CameraFocusEntranceAltBinding
        {
            get => PlayerPrefs.GetString("CameraFocusEntranceAltBinding", "");
            set => PlayerPrefs.SetString("CameraFocusEntranceAltBinding", value);
        }
        public static string CameraFocusVisitorAltBinding
        {
            get => PlayerPrefs.GetString("CameraFocusVisitorAltBinding", "");
            set => PlayerPrefs.SetString("CameraFocusVisitorAltBinding", value);
        }

        // Camera Mouse Alt Bindings (default empty)
        public static string CameraForwardAltBinding
        {
            get => PlayerPrefs.GetString("CameraForwardAltBinding", "");
            set => PlayerPrefs.SetString("CameraForwardAltBinding", value);
        }
        public static string CameraOrbitAltBinding
        {
            get => PlayerPrefs.GetString("CameraOrbitAltBinding", "");
            set => PlayerPrefs.SetString("CameraOrbitAltBinding", value);
        }
        public static string CameraPanAltBinding
        {
            get => PlayerPrefs.GetString("CameraPanAltBinding", "");
            set => PlayerPrefs.SetString("CameraPanAltBinding", value);
        }

        // Screenshot Alt Binding (default empty)
        public static string ScreenshotAltBinding
        {
            get => PlayerPrefs.GetString("ScreenshotAltBinding", "");
            set => PlayerPrefs.SetString("ScreenshotAltBinding", value);
        }

        // ===== Tertiary Bindings (column 3) - defaults to gamepad layout =====
        // Heart Power Tertiary: D-pad directions (Up=P1, Right=P2, Down=P3, Left=P4)
        public static string HeartPower1TertiaryBinding
        {
            get => PlayerPrefs.GetString("HeartPower1TertiaryBinding", "GamepadDpadUp");
            set => PlayerPrefs.SetString("HeartPower1TertiaryBinding", value);
        }
        public static string HeartPower2TertiaryBinding
        {
            get => PlayerPrefs.GetString("HeartPower2TertiaryBinding", "GamepadDpadRight");
            set => PlayerPrefs.SetString("HeartPower2TertiaryBinding", value);
        }
        public static string HeartPower3TertiaryBinding
        {
            get => PlayerPrefs.GetString("HeartPower3TertiaryBinding", "GamepadDpadDown");
            set => PlayerPrefs.SetString("HeartPower3TertiaryBinding", value);
        }
        public static string HeartPower4TertiaryBinding
        {
            get => PlayerPrefs.GetString("HeartPower4TertiaryBinding", "GamepadDpadLeft");
            set => PlayerPrefs.SetString("HeartPower4TertiaryBinding", value);
        }
        public static string HeartPower5TertiaryBinding
        {
            get => PlayerPrefs.GetString("HeartPower5TertiaryBinding", "");
            set => PlayerPrefs.SetString("HeartPower5TertiaryBinding", value);
        }

        // Sculpt Menu Tertiary: Face buttons (South=Pond, East=Lantern, West=Ring, North=Remove)
        public static string SculptPondTertiaryBinding
        {
            get => PlayerPrefs.GetString("SculptPondTertiaryBinding", "GamepadButtonSouth");
            set => PlayerPrefs.SetString("SculptPondTertiaryBinding", value);
        }
        public static string SculptLanternTertiaryBinding
        {
            get => PlayerPrefs.GetString("SculptLanternTertiaryBinding", "GamepadButtonEast");
            set => PlayerPrefs.SetString("SculptLanternTertiaryBinding", value);
        }
        public static string SculptRingTertiaryBinding
        {
            get => PlayerPrefs.GetString("SculptRingTertiaryBinding", "GamepadButtonWest");
            set => PlayerPrefs.SetString("SculptRingTertiaryBinding", value);
        }
        public static string SculptRemoveTertiaryBinding
        {
            get => PlayerPrefs.GetString("SculptRemoveTertiaryBinding", "GamepadButtonNorth");
            set => PlayerPrefs.SetString("SculptRemoveTertiaryBinding", value);
        }

        // Camera Movement Tertiary: Left stick directions
        public static string CameraMoveForwardTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraMoveForwardTertiaryBinding", "GamepadLeftStickUp");
            set => PlayerPrefs.SetString("CameraMoveForwardTertiaryBinding", value);
        }
        public static string CameraMoveBackwardTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraMoveBackwardTertiaryBinding", "GamepadLeftStickDown");
            set => PlayerPrefs.SetString("CameraMoveBackwardTertiaryBinding", value);
        }
        public static string CameraTurnLeftTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraTurnLeftTertiaryBinding", "GamepadDpadLeft");
            set => PlayerPrefs.SetString("CameraTurnLeftTertiaryBinding", value);
        }
        public static string CameraTurnRightTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraTurnRightTertiaryBinding", "GamepadDpadRight");
            set => PlayerPrefs.SetString("CameraTurnRightTertiaryBinding", value);
        }

        // Camera Strafe Tertiary: Left stick left/right
        public static string CameraStrafeLeftTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeLeftTertiaryBinding", "GamepadLeftStickLeft");
            set => PlayerPrefs.SetString("CameraStrafeLeftTertiaryBinding", value);
        }
        public static string CameraStrafeRightTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraStrafeRightTertiaryBinding", "GamepadLeftStickRight");
            set => PlayerPrefs.SetString("CameraStrafeRightTertiaryBinding", value);
        }

        // Camera Focus Tertiary: Left Shoulder, Right Shoulder, Right Stick Press
        public static string CameraFocusHeartTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraFocusHeartTertiaryBinding", "GamepadLeftShoulder");
            set => PlayerPrefs.SetString("CameraFocusHeartTertiaryBinding", value);
        }
        public static string CameraFocusEntranceTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraFocusEntranceTertiaryBinding", "GamepadRightShoulder");
            set => PlayerPrefs.SetString("CameraFocusEntranceTertiaryBinding", value);
        }
        public static string CameraFocusVisitorTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraFocusVisitorTertiaryBinding", "GamepadRightStickPress");
            set => PlayerPrefs.SetString("CameraFocusVisitorTertiaryBinding", value);
        }

        // Camera Mouse Tertiary: Right Stick for orbit, Left Stick Press for click
        public static string CameraForwardTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraForwardTertiaryBinding", "GamepadLeftStickPress");
            set => PlayerPrefs.SetString("CameraForwardTertiaryBinding", value);
        }
        public static string CameraOrbitTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraOrbitTertiaryBinding", "GamepadRightTrigger");
            set => PlayerPrefs.SetString("CameraOrbitTertiaryBinding", value);
        }
        public static string CameraPanTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraPanTertiaryBinding", "GamepadLeftTrigger");
            set => PlayerPrefs.SetString("CameraPanTertiaryBinding", value);
        }

        // Screenshot Tertiary: Select/Share button
        public static string ScreenshotTertiaryBinding
        {
            get => PlayerPrefs.GetString("ScreenshotTertiaryBinding", "GamepadSelect");
            set => PlayerPrefs.SetString("ScreenshotTertiaryBinding", value);
        }

        // Camera Viewing Angle Tertiary (no default gamepad binding)
        public static string CameraAngleUpTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraAngleUpTertiaryBinding", "");
            set => PlayerPrefs.SetString("CameraAngleUpTertiaryBinding", value);
        }
        public static string CameraAngleDownTertiaryBinding
        {
            get => PlayerPrefs.GetString("CameraAngleDownTertiaryBinding", "");
            set => PlayerPrefs.SetString("CameraAngleDownTertiaryBinding", value);
        }

        #region Props - Essence Mechanics

        /// <summary>Essence drained from visitor per second while at fairy ring</summary>
        public static float RingEssenceDrainPerSecond
        {
            get => PlayerPrefs.GetFloat("RingEssenceDrainPerSecond", 5f);
            set => PlayerPrefs.SetFloat("RingEssenceDrainPerSecond", Mathf.Max(0f, value));
        }

        /// <summary>Essence drained from visitor per second while at lantern</summary>
        public static float LanternEssenceDrainPerSecond
        {
            get => PlayerPrefs.GetFloat("LanternEssenceDrainPerSecond", 5f);
            set => PlayerPrefs.SetFloat("LanternEssenceDrainPerSecond", Mathf.Max(0f, value));
        }

        /// <summary>Essence awarded to player per second while visitor is at lantern</summary>
        public static float LanternEssenceAwardPerSecond
        {
            get => PlayerPrefs.GetFloat("LanternEssenceAwardPerSecond", 2f);
            set => PlayerPrefs.SetFloat("LanternEssenceAwardPerSecond", Mathf.Max(0f, value));
        }

        #endregion

        #region Heart Powers

        /// <summary>Essence cost deducted from visitor when grabbed by HeartwardGrasp</summary>
        public static float HeartwardGraspEssenceCost
        {
            get => PlayerPrefs.GetFloat("HeartwardGraspEssenceCost", 25f);
            set => PlayerPrefs.SetFloat("HeartwardGraspEssenceCost", Mathf.Max(0f, value));
        }

        /// <summary>Detection radius for Heart of the Maze tongue</summary>
        public static float HeartDetectionRadius
        {
            get => PlayerPrefs.GetFloat("HeartDetectionRadius", 2.5f);
            set => PlayerPrefs.SetFloat("HeartDetectionRadius", Mathf.Max(0.5f, value));
        }

        /// <summary>Detection radius for HeartwardGrasp zones</summary>
        public static float HeartwardGraspRadius
        {
            get => PlayerPrefs.GetFloat("HeartwardGraspRadius", 2.5f);
            set => PlayerPrefs.SetFloat("HeartwardGraspRadius", Mathf.Max(0.5f, value));
        }

        /// <summary>Trigger radius for DevouringMaw (detection zone for visitors, visual cue is 0.5 larger)</summary>
        public static float DevouringMawRadius
        {
            get => PlayerPrefs.GetFloat("DevouringMawRadius", 2.0f);
            set => PlayerPrefs.SetFloat("DevouringMawRadius", Mathf.Max(0.5f, value));
        }

        /// <summary>Speed at which tongue emerges from ground (units per second)</summary>
        public static float TongueEmergeSpeed
        {
            get => PlayerPrefs.GetFloat("TongueEmergeSpeed", 9f);
            set => PlayerPrefs.SetFloat("TongueEmergeSpeed", Mathf.Max(1f, value));
        }

        /// <summary>Speed at which tongue retracts with visitor (units per second)</summary>
        public static float TongueRetractSpeed
        {
            get => PlayerPrefs.GetFloat("TongueRetractSpeed", 9f);
            set => PlayerPrefs.SetFloat("TongueRetractSpeed", Mathf.Max(1f, value));
        }

        #endregion

        #region Visitor Behavior

        private const float DEFAULT_FRIGHTENED_SPEED_MULTIPLIER = 1.3f;

        /// <summary>Speed multiplier when visitor is frightened (fleeing from Goblin)</summary>
        public static float FrightenedSpeedMultiplier
        {
            get => PlayerPrefs.GetFloat("FrightenedSpeedMultiplier", DEFAULT_FRIGHTENED_SPEED_MULTIPLIER);
            set => PlayerPrefs.SetFloat("FrightenedSpeedMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Resets frightened speed multiplier to default value</summary>
        public static void ResetFrightenedSpeedMultiplier()
        {
            PlayerPrefs.SetFloat("FrightenedSpeedMultiplier", DEFAULT_FRIGHTENED_SPEED_MULTIPLIER);
        }

        /// <summary>Chance per node for frightened visitor to recover (0-1)</summary>
        public static float FrightenedRecoveryChance
        {
            get => PlayerPrefs.GetFloat("FrightenedRecoveryChance", 0.25f);
            set => PlayerPrefs.SetFloat("FrightenedRecoveryChance", Mathf.Clamp01(value));
        }

        #endregion

        #region Difficulty Scaling

        /// <summary>Maximum spawn interval in seconds (asymptotic ceiling)</summary>
        public static float MaxSpawnInterval
        {
            get => PlayerPrefs.GetFloat("MaxSpawnInterval", 15f);
            set => PlayerPrefs.SetFloat("MaxSpawnInterval", Mathf.Max(5f, value));
        }

        /// <summary>Rate at which spawn interval approaches maximum (higher = faster approach)</summary>
        public static float SpawnIntervalGrowthRate
        {
            get => PlayerPrefs.GetFloat("SpawnIntervalGrowthRate", 0.02f);
            set => PlayerPrefs.SetFloat("SpawnIntervalGrowthRate", Mathf.Clamp(value, 0.001f, 0.1f));
        }

        /// <summary>
        /// Tier threshold multipliers as comma-separated values.
        /// Each value is a multiplier of StartingEssence for that tier.
        /// Tier 1 = 0 (start), Tier 2 = 1.5x, etc.
        /// </summary>
        public static string TierMultipliers
        {
            get => PlayerPrefs.GetString("TierMultipliers", "0,1.5,2,3,4,6,8");
            set => PlayerPrefs.SetString("TierMultipliers", value ?? "0,1.5,2,3,4,6,8");
        }

        /// <summary>Maximum visitor speed multiplier at highest tier</summary>
        public static float VisitorSpeedMaxMultiplier
        {
            get => PlayerPrefs.GetFloat("VisitorSpeedMaxMultiplier", 1.5f);
            set => PlayerPrefs.SetFloat("VisitorSpeedMaxMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Growth rate for visitor speed scaling (higher = faster approach to max)</summary>
        public static float VisitorSpeedGrowthRate
        {
            get => PlayerPrefs.GetFloat("VisitorSpeedGrowthRate", 0.25f);
            set => PlayerPrefs.SetFloat("VisitorSpeedGrowthRate", Mathf.Clamp(value, 0.1f, 1f));
        }

        /// <summary>Maximum Goblin speed multiplier at highest tier</summary>
        public static float GoblinSpeedMaxMultiplier
        {
            get => PlayerPrefs.GetFloat("GoblinSpeedMaxMultiplier", 1.6f);
            set => PlayerPrefs.SetFloat("GoblinSpeedMaxMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Growth rate for Goblin speed scaling</summary>
        public static float GoblinSpeedGrowthRate
        {
            get => PlayerPrefs.GetFloat("GoblinSpeedGrowthRate", 0.3f);
            set => PlayerPrefs.SetFloat("GoblinSpeedGrowthRate", Mathf.Clamp(value, 0.1f, 1f));
        }

        /// <summary>Maximum Goblin essence penalty multiplier at highest tier</summary>
        public static float GoblinPenaltyMaxMultiplier
        {
            get => PlayerPrefs.GetFloat("GoblinPenaltyMaxMultiplier", 2.5f);
            set => PlayerPrefs.SetFloat("GoblinPenaltyMaxMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Growth rate for Goblin penalty scaling</summary>
        public static float GoblinPenaltyGrowthRate
        {
            get => PlayerPrefs.GetFloat("GoblinPenaltyGrowthRate", 0.35f);
            set => PlayerPrefs.SetFloat("GoblinPenaltyGrowthRate", Mathf.Clamp(value, 0.1f, 1f));
        }

        /// <summary>Maximum confusion chance multiplier at highest tier</summary>
        public static float ConfusionMaxMultiplier
        {
            get => PlayerPrefs.GetFloat("ConfusionMaxMultiplier", 1.75f);
            set => PlayerPrefs.SetFloat("ConfusionMaxMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Growth rate for confusion chance scaling</summary>
        public static float ConfusionGrowthRate
        {
            get => PlayerPrefs.GetFloat("ConfusionGrowthRate", 0.25f);
            set => PlayerPrefs.SetFloat("ConfusionGrowthRate", Mathf.Clamp(value, 0.1f, 1f));
        }

        /// <summary>Maximum essence reward multiplier at highest tier</summary>
        public static float EssenceRewardMaxMultiplier
        {
            get => PlayerPrefs.GetFloat("EssenceRewardMaxMultiplier", 1.5f);
            set => PlayerPrefs.SetFloat("EssenceRewardMaxMultiplier", Mathf.Max(1f, value));
        }

        /// <summary>Growth rate for essence reward scaling</summary>
        public static float EssenceRewardGrowthRate
        {
            get => PlayerPrefs.GetFloat("EssenceRewardGrowthRate", 0.2f);
            set => PlayerPrefs.SetFloat("EssenceRewardGrowthRate", Mathf.Clamp(value, 0.1f, 1f));
        }

        #endregion

        #region Tutorial Settings

        /// <summary>
        /// Whether the tutorial has been completed at least once.
        /// </summary>
        public static bool TutorialCompleted
        {
            get => PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
            set => PlayerPrefs.SetInt("TutorialCompleted", value ? 1 : 0);
        }

        /// <summary>
        /// Whether to show the tutorial prompt on first run.
        /// If true and TutorialCompleted is false, tutorial will auto-start.
        /// </summary>
        public static bool ShowTutorialOnFirstRun
        {
            get => PlayerPrefs.GetInt("ShowTutorialOnFirstRun", 1) == 1;
            set => PlayerPrefs.SetInt("ShowTutorialOnFirstRun", value ? 1 : 0);
        }

        #endregion

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
        /// One-time migration to update StartingEssence default from 100 to 200 for Normal difficulty.
        /// Only runs once. If user still has old default (100), updates to new default (200).
        /// </summary>
        public static void MigrateStartingEssenceDefault()
        {
            const string MIGRATION_KEY = "StartingEssenceV2Migrated";
            if (PlayerPrefs.GetInt(MIGRATION_KEY, 0) == 1) return;

            // If user has old default of 100, update to new default of 200
            int current = PlayerPrefs.GetInt("StartingEssence", 200);
            if (current == 100)
            {
                PlayerPrefs.SetInt("StartingEssence", 200);
            }

            PlayerPrefs.SetInt(MIGRATION_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// One-time migration from old tank-control layout (A/D = turn) to strafe layout (A/D = strafe, Q/E = rotate).
        /// Only runs once per install. If user had old defaults, swaps them. If user had custom bindings, leaves them alone.
        /// </summary>
        public static void MigrateToStrafeLayout()
        {
            const string MIGRATION_KEY = "StrafeLayoutMigrated";
            if (PlayerPrefs.GetInt(MIGRATION_KEY, 0) == 1) return;

            // Check primary bindings - if user had old A/D defaults for turn, migrate
            string turnLeft = PlayerPrefs.GetString("CameraTurnLeftBinding", "");
            string turnRight = PlayerPrefs.GetString("CameraTurnRightBinding", "");

            if (turnLeft == "A" || turnLeft == "")
                PlayerPrefs.SetString("CameraTurnLeftBinding", "Q");
            if (turnRight == "D" || turnRight == "")
                PlayerPrefs.SetString("CameraTurnRightBinding", "E");

            // Migrate alt bindings: arrow keys move from turn to strafe
            string turnLeftAlt = PlayerPrefs.GetString("CameraTurnLeftAltBinding", "");
            string turnRightAlt = PlayerPrefs.GetString("CameraTurnRightAltBinding", "");

            if (turnLeftAlt == "LeftArrow")
            {
                PlayerPrefs.SetString("CameraTurnLeftAltBinding", "");
                PlayerPrefs.SetString("CameraStrafeLeftAltBinding", "LeftArrow");
            }
            if (turnRightAlt == "RightArrow")
            {
                PlayerPrefs.SetString("CameraTurnRightAltBinding", "");
                PlayerPrefs.SetString("CameraStrafeRightAltBinding", "RightArrow");
            }

            // Migrate gamepad tertiary: left stick L/R moves from turn to strafe
            string turnLeftTertiary = PlayerPrefs.GetString("CameraTurnLeftTertiaryBinding", "");
            string turnRightTertiary = PlayerPrefs.GetString("CameraTurnRightTertiaryBinding", "");

            if (turnLeftTertiary == "GamepadLeftStickLeft")
            {
                PlayerPrefs.SetString("CameraTurnLeftTertiaryBinding", "GamepadDpadLeft");
                PlayerPrefs.SetString("CameraStrafeLeftTertiaryBinding", "GamepadLeftStickLeft");
            }
            if (turnRightTertiary == "GamepadLeftStickRight")
            {
                PlayerPrefs.SetString("CameraTurnRightTertiaryBinding", "GamepadDpadRight");
                PlayerPrefs.SetString("CameraStrafeRightTertiaryBinding", "GamepadLeftStickRight");
            }

            PlayerPrefs.SetInt(MIGRATION_KEY, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Apply current settings to active game systems
        /// </summary>
        public static void ApplySettings()
        {
            // Run one-time migrations
            MigrateStartingEssenceDefault();
            MigrateToStrafeLayout();

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
        /// Apply video settings (fullscreen, resolution, and light level)
        /// </summary>
        public static void ApplyVideoSettings()
        {
            Screen.fullScreen = Fullscreen;

            // Use stored width/height if available, otherwise fall back to index-based lookup
            if (ResolutionWidth > 0 && ResolutionHeight > 0)
            {
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, Fullscreen);
            }
            else if (ResolutionIndex >= 0)
            {
                // Legacy fallback for old saved settings
                Resolution[] resolutions = Screen.resolutions;
                if (ResolutionIndex < resolutions.Length)
                {
                    Resolution res = resolutions[ResolutionIndex];
                    Screen.SetResolution(res.width, res.height, Fullscreen);
                }
            }
            else
            {
                // First run - no resolution saved. Auto-select highest available resolution.
                Resolution[] resolutions = Screen.resolutions;
                if (resolutions.Length > 0)
                {
                    // Find the highest resolution (last in the array is typically highest, but let's sort to be sure)
                    Resolution highest = resolutions[0];
                    foreach (Resolution res in resolutions)
                    {
                        if (res.width * res.height > highest.width * highest.height)
                        {
                            highest = res;
                        }
                    }

                    // Apply and save the highest resolution
                    Screen.SetResolution(highest.width, highest.height, Fullscreen);

                    // Save so this only happens once
                    ResolutionWidth = highest.width;
                    ResolutionHeight = highest.height;
                    ResolutionIndex = 0; // Mark as having a saved resolution (index 0 = highest in sorted list)
                    Save();
                }
            }

            // Apply light level to directional light
            ApplyLightLevel();
        }

        /// <summary>
        /// Apply light level setting to the directional light in the scene
        /// </summary>
        public static void ApplyLightLevel()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = LightLevel;
                    break;
                }
            }
        }
    }
}

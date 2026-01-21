using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using System.Collections.Generic;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Options menu UI and settings persistence.
    /// Uses a tabbed interface with Video, Audio, and Gameplay tabs.
    /// </summary>
    public class OptionsManager : MonoBehaviour
    {
        [Header("Tab System")]
        [SerializeField] private Button gameplayTabButton;
        [SerializeField] private Button videoTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject videoPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        [Header("Video Settings")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Slider fieldOfViewSlider;
        [SerializeField] private TextMeshProUGUI fieldOfViewText;
        [SerializeField] private Slider cameraPanSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraPanSpeedText;
        [SerializeField] private Slider cameraZoomSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraZoomSpeedText;
        [SerializeField] private Slider cameraMinZoomSlider;
        [SerializeField] private TextMeshProUGUI cameraMinZoomText;
        [SerializeField] private Slider cameraMaxZoomSlider;
        [SerializeField] private TextMeshProUGUI cameraMaxZoomText;
        [SerializeField] private Slider cameraMovementSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraMovementSpeedText;

        [Header("Audio Settings")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeText;

        [Header("Audio - Individual Sound Volumes")]
        [SerializeField] private Slider lanternVolumeSlider;
        [SerializeField] private TextMeshProUGUI lanternVolumeText;
        [SerializeField] private Slider fairyRingVolumeSlider;
        [SerializeField] private TextMeshProUGUI fairyRingVolumeText;
        [SerializeField] private Slider pondVolumeSlider;
        [SerializeField] private TextMeshProUGUI pondVolumeText;
        [SerializeField] private Slider sculptVolumeSlider;
        [SerializeField] private TextMeshProUGUI sculptVolumeText;

        [Header("Gameplay - Visitor Settings")]
        [SerializeField] private Slider visitorSpeedSlider;
        [SerializeField] private TextMeshProUGUI visitorSpeedText;
        [SerializeField] private Toggle confusionEnabledToggle;
        [SerializeField] private Slider confusionChanceSlider;
        [SerializeField] private TextMeshProUGUI confusionChanceText;
        [SerializeField] private Slider confusionDistanceMinSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMinText;
        [SerializeField] private Slider confusionDistanceMaxSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMaxText;

        [Header("Gameplay - Spawning Settings")]
        [SerializeField] private Slider spawnIntervalSlider;
        [SerializeField] private TextMeshProUGUI spawnIntervalText;
        [SerializeField] private Toggle enableRedCapToggle;

        [Header("Gameplay - Game Flow Settings")]
        [SerializeField] private Toggle autoStartNextWaveToggle;
        [SerializeField] private Slider autoStartDelaySlider;
        [SerializeField] private TextMeshProUGUI autoStartDelayText;
        [SerializeField] private Slider startingEssenceSlider;
        [SerializeField] private TextMeshProUGUI startingEssenceText;

        [Header("Gameplay - Player Controls")]
        [SerializeField] private Slider focusSpeedSlider;
        [SerializeField] private TextMeshProUGUI focusSpeedText;
        [SerializeField] private TMP_Dropdown heartPower1KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower2KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower3KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower4KeyDropdown;

        [Header("Video - Screenshot Settings")]
        [SerializeField] private TMP_InputField screenshotPathInput;
        [SerializeField] private Button browseScreenshotPathButton;
        [SerializeField] private TMP_Dropdown screenshotKeyDropdown;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        private SceneLoader sceneLoader;
        private int currentTab = 0;
        private List<Resolution> availableResolutions = new List<Resolution>();

        // Common keybinding options for dropdowns
        private static readonly KeyCode[] keybindOptions = new KeyCode[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
            KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P,
            KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K, KeyCode.L,
            KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12
        };

        private void Awake()
        {
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        private void Start()
        {
            PopulateResolutions();
            PopulateKeybindDropdowns();
            LoadSettings();
            SetupUIListeners();
            SelectTab(0); // Start on Gameplay tab
        }

        private void PopulateKeybindDropdowns()
        {
            List<string> options = new List<string>();
            foreach (KeyCode key in keybindOptions)
            {
                options.Add(KeyCodeToDisplayString(key));
            }

            PopulateDropdown(heartPower1KeyDropdown, options);
            PopulateDropdown(heartPower2KeyDropdown, options);
            PopulateDropdown(heartPower3KeyDropdown, options);
            PopulateDropdown(heartPower4KeyDropdown, options);
            PopulateDropdown(screenshotKeyDropdown, options);
        }

        private void PopulateDropdown(TMP_Dropdown dropdown, List<string> options)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
        }

        private string KeyCodeToDisplayString(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                default: return key.ToString();
            }
        }

        private int KeyCodeToDropdownIndex(KeyCode key)
        {
            for (int i = 0; i < keybindOptions.Length; i++)
            {
                if (keybindOptions[i] == key) return i;
            }
            return 0;
        }

        private KeyCode DropdownIndexToKeyCode(int index)
        {
            if (index >= 0 && index < keybindOptions.Length)
                return keybindOptions[index];
            return KeyCode.Alpha1;
        }

        private void PopulateResolutions()
        {
            availableResolutions.Clear();
            Resolution[] resolutions = Screen.resolutions;

            // Filter to unique width x height combinations (ignore refresh rate duplicates)
            // Sort by resolution size (largest first) for better UX
            HashSet<string> seen = new HashSet<string>();
            List<Resolution> uniqueResolutions = new List<Resolution>();

            foreach (Resolution res in resolutions)
            {
                string key = $"{res.width}x{res.height}";
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    uniqueResolutions.Add(res);
                }
            }

            // Sort by total pixels (descending) so highest resolution is first
            uniqueResolutions.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));
            availableResolutions = uniqueResolutions;

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                List<string> options = new List<string>();
                int currentIndex = 0;

                for (int i = 0; i < availableResolutions.Count; i++)
                {
                    Resolution res = availableResolutions[i];
                    options.Add($"{res.width} x {res.height}");

                    // Find current resolution
                    if (res.width == Screen.currentResolution.width &&
                        res.height == Screen.currentResolution.height)
                    {
                        currentIndex = i;
                    }
                }

                resolutionDropdown.AddOptions(options);

                // Use saved resolution index, or current resolution if not saved
                int savedIndex = GameSettings.ResolutionIndex;
                if (savedIndex >= 0 && savedIndex < availableResolutions.Count)
                {
                    resolutionDropdown.value = savedIndex;
                }
                else
                {
                    resolutionDropdown.value = currentIndex;
                }
            }
        }

        private void SelectTab(int tabIndex)
        {
            currentTab = tabIndex;

            // Show/hide panels - Tab order: Gameplay (0), Video (1), Audio (2)
            if (gameplayPanel != null) gameplayPanel.SetActive(tabIndex == 0);
            if (videoPanel != null) videoPanel.SetActive(tabIndex == 1);
            if (audioPanel != null) audioPanel.SetActive(tabIndex == 2);

            // Update tab button visuals
            UpdateTabButtonColors();
        }

        private void UpdateTabButtonColors()
        {
            SetTabButtonColor(gameplayTabButton, currentTab == 0);
            SetTabButtonColor(videoTabButton, currentTab == 1);
            SetTabButtonColor(audioTabButton, currentTab == 2);
        }

        private void SetTabButtonColor(Button button, bool isActive)
        {
            if (button == null) return;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isActive ? activeTabColor : inactiveTabColor;
            }
        }

        private void SetupUIListeners()
        {
            // Tab buttons - Tab order: Gameplay (0), Video (1), Audio (2)
            if (gameplayTabButton != null)
                gameplayTabButton.onClick.AddListener(() => SelectTab(0));
            if (videoTabButton != null)
                videoTabButton.onClick.AddListener(() => SelectTab(1));
            if (audioTabButton != null)
                audioTabButton.onClick.AddListener(() => SelectTab(2));

            // Video settings
            if (fullscreenToggle != null)
                fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            if (resolutionDropdown != null)
                resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            if (fieldOfViewSlider != null)
                fieldOfViewSlider.onValueChanged.AddListener(OnFieldOfViewChanged);
            if (cameraPanSpeedSlider != null)
                cameraPanSpeedSlider.onValueChanged.AddListener(OnCameraPanSpeedChanged);
            if (cameraZoomSpeedSlider != null)
                cameraZoomSpeedSlider.onValueChanged.AddListener(OnCameraZoomSpeedChanged);
            if (cameraMinZoomSlider != null)
                cameraMinZoomSlider.onValueChanged.AddListener(OnCameraMinZoomChanged);
            if (cameraMaxZoomSlider != null)
                cameraMaxZoomSlider.onValueChanged.AddListener(OnCameraMaxZoomChanged);
            if (cameraMovementSpeedSlider != null)
                cameraMovementSpeedSlider.onValueChanged.AddListener(OnCameraMovementSpeedChanged);

            // Audio settings
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            // Individual sound volumes
            if (lanternVolumeSlider != null)
                lanternVolumeSlider.onValueChanged.AddListener(OnLanternVolumeChanged);
            if (fairyRingVolumeSlider != null)
                fairyRingVolumeSlider.onValueChanged.AddListener(OnFairyRingVolumeChanged);
            if (pondVolumeSlider != null)
                pondVolumeSlider.onValueChanged.AddListener(OnPondVolumeChanged);
            if (sculptVolumeSlider != null)
                sculptVolumeSlider.onValueChanged.AddListener(OnSculptVolumeChanged);

            // Visitor Gameplay
            if (visitorSpeedSlider != null)
                visitorSpeedSlider.onValueChanged.AddListener(OnVisitorSpeedChanged);
            if (confusionEnabledToggle != null)
                confusionEnabledToggle.onValueChanged.AddListener(OnConfusionEnabledChanged);
            if (confusionChanceSlider != null)
                confusionChanceSlider.onValueChanged.AddListener(OnConfusionChanceChanged);
            if (confusionDistanceMinSlider != null)
                confusionDistanceMinSlider.onValueChanged.AddListener(OnConfusionDistanceMinChanged);
            if (confusionDistanceMaxSlider != null)
                confusionDistanceMaxSlider.onValueChanged.AddListener(OnConfusionDistanceMaxChanged);

            // Spawning Settings
            if (spawnIntervalSlider != null)
                spawnIntervalSlider.onValueChanged.AddListener(OnSpawnIntervalChanged);
            if (enableRedCapToggle != null)
                enableRedCapToggle.onValueChanged.AddListener(OnEnableRedCapChanged);

            // Game Flow
            if (autoStartNextWaveToggle != null)
                autoStartNextWaveToggle.onValueChanged.AddListener(OnAutoStartNextWaveChanged);
            if (autoStartDelaySlider != null)
                autoStartDelaySlider.onValueChanged.AddListener(OnAutoStartDelayChanged);
            if (startingEssenceSlider != null)
                startingEssenceSlider.onValueChanged.AddListener(OnStartingEssenceChanged);

            // Player Controls
            if (focusSpeedSlider != null)
                focusSpeedSlider.onValueChanged.AddListener(OnFocusSpeedChanged);

            // Screenshot Settings
            if (browseScreenshotPathButton != null)
                browseScreenshotPathButton.onClick.AddListener(OnBrowseScreenshotPath);

            // Buttons
            if (applyButton != null)
                applyButton.onClick.AddListener(OnApplyClicked);
            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetClicked);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        private void LoadSettings()
        {
            // Video settings
            if (fullscreenToggle != null)
                fullscreenToggle.isOn = GameSettings.Fullscreen;
            // Resolution dropdown is populated in PopulateResolutions()

            // Camera settings (in Video tab)
            SetSliderValue(fieldOfViewSlider, GameSettings.CameraFieldOfView, 30f, 120f);
            UpdateValueText(fieldOfViewText, GameSettings.CameraFieldOfView, "{0:F0}°");
            SetSliderValue(cameraPanSpeedSlider, GameSettings.CameraPanSpeed, 1f, 30f);
            UpdateValueText(cameraPanSpeedText, GameSettings.CameraPanSpeed, "{0:F1}");
            SetSliderValue(cameraZoomSpeedSlider, GameSettings.CameraZoomSpeed, 1f, 20f);
            UpdateValueText(cameraZoomSpeedText, GameSettings.CameraZoomSpeed, "{0:F1}");
            SetSliderValue(cameraMinZoomSlider, GameSettings.CameraMinZoom, 1f, 10f);
            UpdateValueText(cameraMinZoomText, GameSettings.CameraMinZoom, "{0:F1}");
            SetSliderValue(cameraMaxZoomSlider, GameSettings.CameraMaxZoom, 10f, 50f);
            UpdateValueText(cameraMaxZoomText, GameSettings.CameraMaxZoom, "{0:F1}");
            SetSliderValue(cameraMovementSpeedSlider, GameSettings.CameraMovementSpeed, 0.1f, 10f);
            UpdateValueText(cameraMovementSpeedText, GameSettings.CameraMovementSpeed, "{0:F1}");

            // Audio settings
            SetSliderValue(sfxVolumeSlider, GameSettings.SfxVolume, 0f, 1f);
            UpdateValueText(sfxVolumeText, GameSettings.SfxVolume, "{0:P0}");
            SetSliderValue(musicVolumeSlider, GameSettings.MusicVolume, 0f, 1f);
            UpdateValueText(musicVolumeText, GameSettings.MusicVolume, "{0:P0}");

            // Individual sound volumes
            SetSliderValue(lanternVolumeSlider, GameSettings.LanternVolume, 0f, 1f);
            UpdateValueText(lanternVolumeText, GameSettings.LanternVolume, "{0:P0}");
            SetSliderValue(fairyRingVolumeSlider, GameSettings.FairyRingVolume, 0f, 1f);
            UpdateValueText(fairyRingVolumeText, GameSettings.FairyRingVolume, "{0:P0}");
            SetSliderValue(pondVolumeSlider, GameSettings.PondVolume, 0f, 1f);
            UpdateValueText(pondVolumeText, GameSettings.PondVolume, "{0:P0}");
            SetSliderValue(sculptVolumeSlider, GameSettings.SculptVolume, 0f, 1f);
            UpdateValueText(sculptVolumeText, GameSettings.SculptVolume, "{0:P0}");

            // Visitor Gameplay
            SetSliderValue(visitorSpeedSlider, GameSettings.VisitorSpeed, 0.5f, 10f);
            UpdateValueText(visitorSpeedText, GameSettings.VisitorSpeed, "{0:F1}");
            if (confusionEnabledToggle != null)
                confusionEnabledToggle.isOn = GameSettings.ConfusionEnabled;
            SetSliderValue(confusionChanceSlider, GameSettings.ConfusionChance, 0f, 1f);
            UpdateValueText(confusionChanceText, GameSettings.ConfusionChance, "{0:P0}");
            SetSliderValue(confusionDistanceMinSlider, GameSettings.ConfusionDistanceMin, 1f, 50f);
            UpdateValueText(confusionDistanceMinText, GameSettings.ConfusionDistanceMin, "{0:F0}");
            SetSliderValue(confusionDistanceMaxSlider, GameSettings.ConfusionDistanceMax, 1f, 50f);
            UpdateValueText(confusionDistanceMaxText, GameSettings.ConfusionDistanceMax, "{0:F0}");

            // Spawning Settings
            SetSliderValue(spawnIntervalSlider, GameSettings.SpawnInterval, 0.1f, 5f);
            UpdateValueText(spawnIntervalText, GameSettings.SpawnInterval, "{0:F1}s");
            if (enableRedCapToggle != null)
                enableRedCapToggle.isOn = GameSettings.EnableRedCap;

            // Game Flow
            if (autoStartNextWaveToggle != null)
                autoStartNextWaveToggle.isOn = GameSettings.AutoStartNextWave;
            SetSliderValue(autoStartDelaySlider, GameSettings.AutoStartDelay, 0f, 10f);
            UpdateValueText(autoStartDelayText, GameSettings.AutoStartDelay, "{0:F1}s");
            SetSliderValue(startingEssenceSlider, GameSettings.StartingEssence, 0f, 1000f);
            UpdateValueText(startingEssenceText, GameSettings.StartingEssence, "{0:F0}");

            // Player Controls
            SetSliderValue(focusSpeedSlider, GameSettings.FocusSpeed, 5f, 15f);
            UpdateValueText(focusSpeedText, GameSettings.FocusSpeed, "{0:F1}");
            SetDropdownValue(heartPower1KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower1Key));
            SetDropdownValue(heartPower2KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower2Key));
            SetDropdownValue(heartPower3KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower3Key));
            SetDropdownValue(heartPower4KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower4Key));

            // Screenshot Settings
            if (screenshotPathInput != null)
                screenshotPathInput.text = GameSettings.ScreenshotPath;
            SetDropdownValue(screenshotKeyDropdown, KeyCodeToDropdownIndex(GameSettings.ScreenshotKey));
        }

        private void SetDropdownValue(TMP_Dropdown dropdown, int value)
        {
            if (dropdown != null)
                dropdown.value = value;
        }

        // Video callbacks
        private void OnFullscreenChanged(bool value)
        {
            // Apply immediately for instant feedback
            Screen.fullScreen = value;
        }

        private void OnResolutionChanged(int index)
        {
            // Apply immediately for instant feedback
            if (index >= 0 && index < availableResolutions.Count)
            {
                Resolution res = availableResolutions[index];
                bool isFullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
                Screen.SetResolution(res.width, res.height, isFullscreen);
            }
        }

        // Camera callbacks
        private void OnFieldOfViewChanged(float value)
        {
            UpdateValueText(fieldOfViewText, value, "{0:F0}°");
        }

        private void OnCameraPanSpeedChanged(float value)
        {
            UpdateValueText(cameraPanSpeedText, value, "{0:F1}");
        }

        private void OnCameraZoomSpeedChanged(float value)
        {
            UpdateValueText(cameraZoomSpeedText, value, "{0:F1}");
        }

        private void OnCameraMinZoomChanged(float value)
        {
            UpdateValueText(cameraMinZoomText, value, "{0:F1}");
        }

        private void OnCameraMaxZoomChanged(float value)
        {
            UpdateValueText(cameraMaxZoomText, value, "{0:F1}");
        }

        private void OnCameraMovementSpeedChanged(float value)
        {
            UpdateValueText(cameraMovementSpeedText, value, "{0:F1}");
        }

        // Audio callbacks
        private void OnSfxVolumeChanged(float value)
        {
            UpdateValueText(sfxVolumeText, value, "{0:P0}");
        }

        private void OnMusicVolumeChanged(float value)
        {
            UpdateValueText(musicVolumeText, value, "{0:P0}");
        }

        // Individual sound volume callbacks
        private void OnLanternVolumeChanged(float value)
        {
            UpdateValueText(lanternVolumeText, value, "{0:P0}");
        }

        private void OnFairyRingVolumeChanged(float value)
        {
            UpdateValueText(fairyRingVolumeText, value, "{0:P0}");
        }

        private void OnPondVolumeChanged(float value)
        {
            UpdateValueText(pondVolumeText, value, "{0:P0}");
        }

        private void OnSculptVolumeChanged(float value)
        {
            UpdateValueText(sculptVolumeText, value, "{0:P0}");
        }

        // Visitor callbacks
        private void OnVisitorSpeedChanged(float value)
        {
            UpdateValueText(visitorSpeedText, value, "{0:F1}");
        }

        private void OnConfusionEnabledChanged(bool value)
        {
            // Toggle is handled directly, no text update needed
        }

        private void OnConfusionChanceChanged(float value)
        {
            UpdateValueText(confusionChanceText, value, "{0:P0}");
        }

        private void OnConfusionDistanceMinChanged(float value)
        {
            UpdateValueText(confusionDistanceMinText, value, "{0:F0}");
        }

        private void OnConfusionDistanceMaxChanged(float value)
        {
            UpdateValueText(confusionDistanceMaxText, value, "{0:F0}");
        }

        // Spawning callbacks
        private void OnSpawnIntervalChanged(float value)
        {
            UpdateValueText(spawnIntervalText, value, "{0:F1}s");
        }

        private void OnEnableRedCapChanged(bool value)
        {
            // Toggle is handled directly
        }

        // Game Flow callbacks
        private void OnAutoStartNextWaveChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnAutoStartDelayChanged(float value)
        {
            UpdateValueText(autoStartDelayText, value, "{0:F1}s");
        }

        private void OnStartingEssenceChanged(float value)
        {
            UpdateValueText(startingEssenceText, value, "{0:F0}");
        }

        // Player Controls callbacks
        private void OnFocusSpeedChanged(float value)
        {
            UpdateValueText(focusSpeedText, value, "{0:F1}");
        }

        // Screenshot callbacks
        private void OnBrowseScreenshotPath()
        {
            // Note: Unity doesn't have a built-in folder browser dialog
            // In a real implementation, you'd use a native file dialog plugin
            // For now, the user can manually type the path in the input field
            Debug.Log("Browse button clicked - manual path entry required");
        }

        // Button handlers
        private void OnApplyClicked()
        {
            SaveSettings();
            GameSettings.ApplySettings();
        }

        private void OnResetClicked()
        {
            GameSettings.ResetToDefaults();
            PopulateResolutions(); // Refresh resolution list
            LoadSettings();
        }

        private void OnBackClicked()
        {
            sceneLoader.LoadMainMenu();
        }

        private void SaveSettings()
        {
            // Video settings
            if (fullscreenToggle != null)
                GameSettings.Fullscreen = fullscreenToggle.isOn;
            if (resolutionDropdown != null)
                GameSettings.ResolutionIndex = resolutionDropdown.value;

            // Camera settings
            GameSettings.CameraFieldOfView = GetSliderValue(fieldOfViewSlider);
            GameSettings.CameraPanSpeed = GetSliderValue(cameraPanSpeedSlider);
            GameSettings.CameraZoomSpeed = GetSliderValue(cameraZoomSpeedSlider);
            GameSettings.CameraMinZoom = GetSliderValue(cameraMinZoomSlider);
            GameSettings.CameraMaxZoom = GetSliderValue(cameraMaxZoomSlider);
            GameSettings.CameraMovementSpeed = GetSliderValue(cameraMovementSpeedSlider);

            // Audio settings
            GameSettings.SfxVolume = GetSliderValue(sfxVolumeSlider);
            GameSettings.MusicVolume = GetSliderValue(musicVolumeSlider);

            // Individual sound volumes
            GameSettings.LanternVolume = GetSliderValue(lanternVolumeSlider);
            GameSettings.FairyRingVolume = GetSliderValue(fairyRingVolumeSlider);
            GameSettings.PondVolume = GetSliderValue(pondVolumeSlider);
            GameSettings.SculptVolume = GetSliderValue(sculptVolumeSlider);

            // Visitor Gameplay
            GameSettings.VisitorSpeed = GetSliderValue(visitorSpeedSlider);
            if (confusionEnabledToggle != null)
                GameSettings.ConfusionEnabled = confusionEnabledToggle.isOn;
            GameSettings.ConfusionChance = GetSliderValue(confusionChanceSlider);
            GameSettings.ConfusionDistanceMin = (int)GetSliderValue(confusionDistanceMinSlider);
            GameSettings.ConfusionDistanceMax = (int)GetSliderValue(confusionDistanceMaxSlider);

            // Spawning Settings
            GameSettings.SpawnInterval = GetSliderValue(spawnIntervalSlider);
            if (enableRedCapToggle != null)
                GameSettings.EnableRedCap = enableRedCapToggle.isOn;

            // Game Flow
            if (autoStartNextWaveToggle != null)
                GameSettings.AutoStartNextWave = autoStartNextWaveToggle.isOn;
            GameSettings.AutoStartDelay = GetSliderValue(autoStartDelaySlider);
            GameSettings.StartingEssence = (int)GetSliderValue(startingEssenceSlider);

            // Player Controls
            GameSettings.FocusSpeed = GetSliderValue(focusSpeedSlider);
            if (heartPower1KeyDropdown != null)
                GameSettings.HeartPower1Key = DropdownIndexToKeyCode(heartPower1KeyDropdown.value);
            if (heartPower2KeyDropdown != null)
                GameSettings.HeartPower2Key = DropdownIndexToKeyCode(heartPower2KeyDropdown.value);
            if (heartPower3KeyDropdown != null)
                GameSettings.HeartPower3Key = DropdownIndexToKeyCode(heartPower3KeyDropdown.value);
            if (heartPower4KeyDropdown != null)
                GameSettings.HeartPower4Key = DropdownIndexToKeyCode(heartPower4KeyDropdown.value);

            // Screenshot Settings
            if (screenshotPathInput != null)
                GameSettings.ScreenshotPath = screenshotPathInput.text;
            if (screenshotKeyDropdown != null)
                GameSettings.ScreenshotKey = DropdownIndexToKeyCode(screenshotKeyDropdown.value);

            GameSettings.Save();
        }

        // Helper methods
        private void SetSliderValue(Slider slider, float value, float min, float max)
        {
            if (slider != null)
            {
                slider.minValue = min;
                slider.maxValue = max;
                slider.value = value;
            }
        }

        private float GetSliderValue(Slider slider)
        {
            return slider != null ? slider.value : 0f;
        }

        private void UpdateValueText(TextMeshProUGUI text, float value, string format)
        {
            if (text != null)
            {
                text.text = string.Format(format, value);
            }
        }
    }
}

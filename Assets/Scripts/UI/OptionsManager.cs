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
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject videoPanel;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private ScrollRect mainScrollRect;
        [SerializeField] private Color activeTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        [Header("Video Settings")]
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Slider fieldOfViewSlider;
        [SerializeField] private TextMeshProUGUI fieldOfViewText;
        [SerializeField] private TMP_InputField fieldOfViewInput;
        [SerializeField] private Slider cameraPanSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraPanSpeedText;
        [SerializeField] private TMP_InputField cameraPanSpeedInput;
        [SerializeField] private Slider cameraZoomSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraZoomSpeedText;
        [SerializeField] private TMP_InputField cameraZoomSpeedInput;
        [SerializeField] private Slider cameraMinZoomSlider;
        [SerializeField] private TextMeshProUGUI cameraMinZoomText;
        [SerializeField] private TMP_InputField cameraMinZoomInput;
        [SerializeField] private Slider cameraMaxZoomSlider;
        [SerializeField] private TextMeshProUGUI cameraMaxZoomText;
        [SerializeField] private TMP_InputField cameraMaxZoomInput;
        [SerializeField] private Slider cameraMovementSpeedSlider;
        [SerializeField] private TextMeshProUGUI cameraMovementSpeedText;
        [SerializeField] private TMP_InputField cameraMovementSpeedInput;
        [SerializeField] private Slider lightLevelSlider;
        [SerializeField] private TextMeshProUGUI lightLevelText;
        [SerializeField] private TMP_InputField lightLevelInput;

        [Header("Audio Settings")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        [SerializeField] private TMP_InputField sfxVolumeInput;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeText;
        [SerializeField] private TMP_InputField musicVolumeInput;

        [Header("Audio - Individual Sound Volumes")]
        [SerializeField] private Slider lanternVolumeSlider;
        [SerializeField] private TextMeshProUGUI lanternVolumeText;
        [SerializeField] private TMP_InputField lanternVolumeInput;
        [SerializeField] private Slider fairyRingVolumeSlider;
        [SerializeField] private TextMeshProUGUI fairyRingVolumeText;
        [SerializeField] private TMP_InputField fairyRingVolumeInput;
        [SerializeField] private Slider pondVolumeSlider;
        [SerializeField] private TextMeshProUGUI pondVolumeText;
        [SerializeField] private TMP_InputField pondVolumeInput;
        [SerializeField] private Slider sculptVolumeSlider;
        [SerializeField] private TextMeshProUGUI sculptVolumeText;
        [SerializeField] private TMP_InputField sculptVolumeInput;

        [Header("Gameplay - Visitor Settings")]
        [SerializeField] private Slider visitorSpeedSlider;
        [SerializeField] private TextMeshProUGUI visitorSpeedText;
        [SerializeField] private TMP_InputField visitorSpeedInput;
        [SerializeField] private Toggle confusionEnabledToggle;
        [SerializeField] private Slider confusionChanceSlider;
        [SerializeField] private TextMeshProUGUI confusionChanceText;
        [SerializeField] private TMP_InputField confusionChanceInput;
        [SerializeField] private Slider confusionDistanceMinSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMinText;
        [SerializeField] private TMP_InputField confusionDistanceMinInput;
        [SerializeField] private Slider confusionDistanceMaxSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMaxText;
        [SerializeField] private TMP_InputField confusionDistanceMaxInput;

        [Header("Gameplay - Spawning Settings")]
        [SerializeField] private Slider spawnIntervalSlider;  // Now used as difficulty selector (0=Easy, 1=Normal, 2=Hard)
        [SerializeField] private TextMeshProUGUI spawnIntervalText;  // Shows "EASY", "NORMAL", or "HARD"
        [SerializeField] private TMP_InputField spawnIntervalInput;  // Hidden for difficulty mode
        [SerializeField] private Toggle enableGoblinToggle;

        // Difficulty settings mapping (slider value -> spawn interval in seconds)
        private static readonly string[] difficultyLabels = { "EASY", "NORMAL", "HARD" };
        private static readonly float[] difficultySpawnIntervals = { 2f, 5f, 8f };  // Easy=2s, Normal=5s, Hard=8s

        [Header("Gameplay - Game Flow Settings")]
        [SerializeField] private Slider autoStartDelaySlider;
        [SerializeField] private TextMeshProUGUI autoStartDelayText;
        [SerializeField] private TMP_InputField autoStartDelayInput;
        [SerializeField] private Slider startingEssenceSlider;
        [SerializeField] private TextMeshProUGUI startingEssenceText;
        [SerializeField] private TMP_InputField startingEssenceInput;
        [SerializeField] private Toggle useFixedSeedToggle;
        [SerializeField] private TMP_InputField randomSeedInput;
        [SerializeField] private TextMeshProUGUI currentSeedText;
        [SerializeField] private Toggle showTutorialToggle;

        [Header("Gameplay - Player Controls")]
        [SerializeField] private Slider focusSpeedSlider;
        [SerializeField] private TextMeshProUGUI focusSpeedText;
        [SerializeField] private TMP_InputField focusSpeedInput;
        [SerializeField] private TMP_Dropdown heartPower1KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower2KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower3KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower4KeyDropdown;
        [SerializeField] private TMP_Dropdown heartPower5KeyDropdown;

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

        // Key binding capture state
        private KeyBindingCapture activeCapture = null;

        // Binding values (cached from UI, saved on Apply)
        // Primary bindings (column 1)
        private string bindingHeartPower1;
        private string bindingHeartPower2;
        private string bindingHeartPower3;
        private string bindingHeartPower4;
        private string bindingHeartPower5;
        private string bindingSculptPond;
        private string bindingSculptLantern;
        private string bindingSculptRing;
        private string bindingSculptRemove;
        private string bindingCameraMoveForward;
        private string bindingCameraMoveBackward;
        private string bindingCameraTurnLeft;
        private string bindingCameraTurnRight;
        private string bindingCameraFocusHeart;
        private string bindingCameraFocusEntrance;
        private string bindingCameraFocusVisitor;
        private string bindingCameraForward;
        private string bindingCameraOrbit;
        private string bindingCameraPan;
        private string bindingScreenshot;

        // Alt bindings (column 2)
        private string bindingHeartPower1Alt;
        private string bindingHeartPower2Alt;
        private string bindingHeartPower3Alt;
        private string bindingHeartPower4Alt;
        private string bindingHeartPower5Alt;
        private string bindingSculptPondAlt;
        private string bindingSculptLanternAlt;
        private string bindingSculptRingAlt;
        private string bindingSculptRemoveAlt;
        private string bindingCameraMoveForwardAlt;
        private string bindingCameraMoveBackwardAlt;
        private string bindingCameraTurnLeftAlt;
        private string bindingCameraTurnRightAlt;
        private string bindingCameraFocusHeartAlt;
        private string bindingCameraFocusEntranceAlt;
        private string bindingCameraFocusVisitorAlt;
        private string bindingCameraForwardAlt;
        private string bindingCameraOrbitAlt;
        private string bindingCameraPanAlt;
        private string bindingScreenshotAlt;

        // Tertiary bindings (column 3)
        private string bindingHeartPower1Tertiary;
        private string bindingHeartPower2Tertiary;
        private string bindingHeartPower3Tertiary;
        private string bindingHeartPower4Tertiary;
        private string bindingHeartPower5Tertiary;
        private string bindingSculptPondTertiary;
        private string bindingSculptLanternTertiary;
        private string bindingSculptRingTertiary;
        private string bindingSculptRemoveTertiary;
        private string bindingCameraMoveForwardTertiary;
        private string bindingCameraMoveBackwardTertiary;
        private string bindingCameraTurnLeftTertiary;
        private string bindingCameraTurnRightTertiary;
        private string bindingCameraFocusHeartTertiary;
        private string bindingCameraFocusEntranceTertiary;
        private string bindingCameraFocusVisitorTertiary;
        private string bindingCameraForwardTertiary;
        private string bindingCameraOrbitTertiary;
        private string bindingCameraPanTertiary;
        private string bindingScreenshotTertiary;

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
            PopulateDropdown(heartPower5KeyDropdown, options);
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

            // Show/hide panels - Tab order: Gameplay (0), Video (1), Audio (2), Controls (3)
            if (gameplayPanel != null) gameplayPanel.SetActive(tabIndex == 0);
            if (videoPanel != null) videoPanel.SetActive(tabIndex == 1);
            if (audioPanel != null) audioPanel.SetActive(tabIndex == 2);
            if (controlsPanel != null) controlsPanel.SetActive(tabIndex == 3);

            // Update ScrollRect content to the active panel so scrolling works
            UpdateScrollRectContent(tabIndex);

            // Cancel any active binding capture when switching tabs
            if (activeCapture != null)
            {
                activeCapture.CancelCapture();
                activeCapture = null;
            }

            // Update tab button visuals
            UpdateTabButtonColors();
        }

        private void UpdateScrollRectContent(int tabIndex)
        {
            if (mainScrollRect == null) return;

            RectTransform content = null;
            switch (tabIndex)
            {
                case 0:
                    content = gameplayPanel?.GetComponent<RectTransform>();
                    break;
                case 1:
                    content = videoPanel?.GetComponent<RectTransform>();
                    break;
                case 2:
                    content = audioPanel?.GetComponent<RectTransform>();
                    break;
                case 3:
                    content = controlsPanel?.GetComponent<RectTransform>();
                    break;
            }

            if (content != null)
            {
                mainScrollRect.content = content;
                // Reset scroll position to top when switching tabs
                mainScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void UpdateTabButtonColors()
        {
            SetTabButtonColor(gameplayTabButton, currentTab == 0);
            SetTabButtonColor(videoTabButton, currentTab == 1);
            SetTabButtonColor(audioTabButton, currentTab == 2);
            SetTabButtonColor(controlsTabButton, currentTab == 3);
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
            // Tab buttons - Tab order: Gameplay (0), Video (1), Audio (2), Controls (3)
            if (gameplayTabButton != null)
                gameplayTabButton.onClick.AddListener(() => SelectTab(0));
            if (videoTabButton != null)
                videoTabButton.onClick.AddListener(() => SelectTab(1));
            if (audioTabButton != null)
                audioTabButton.onClick.AddListener(() => SelectTab(2));
            if (controlsTabButton != null)
                controlsTabButton.onClick.AddListener(() => SelectTab(3));

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
            if (lightLevelSlider != null)
                lightLevelSlider.onValueChanged.AddListener(OnLightLevelChanged);

            // Video input field syncs
            SetupSliderInputSync(fieldOfViewSlider, fieldOfViewInput, "{0:F0}");
            SetupSliderInputSync(cameraPanSpeedSlider, cameraPanSpeedInput, "{0:F1}");
            SetupSliderInputSync(cameraZoomSpeedSlider, cameraZoomSpeedInput, "{0:F1}");
            SetupSliderInputSync(cameraMinZoomSlider, cameraMinZoomInput, "{0:F1}");
            SetupSliderInputSync(cameraMaxZoomSlider, cameraMaxZoomInput, "{0:F1}");
            SetupSliderInputSync(cameraMovementSpeedSlider, cameraMovementSpeedInput, "{0:F1}");
            SetupSliderInputSync(lightLevelSlider, lightLevelInput, "{0:F1}");

            // Audio settings
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            // Audio input field syncs (percentages)
            SetupSliderInputSync(sfxVolumeSlider, sfxVolumeInput, "{0:F0}", isPercentage: true);
            SetupSliderInputSync(musicVolumeSlider, musicVolumeInput, "{0:F0}", isPercentage: true);

            // Individual sound volumes
            if (lanternVolumeSlider != null)
                lanternVolumeSlider.onValueChanged.AddListener(OnLanternVolumeChanged);
            if (fairyRingVolumeSlider != null)
                fairyRingVolumeSlider.onValueChanged.AddListener(OnFairyRingVolumeChanged);
            if (pondVolumeSlider != null)
                pondVolumeSlider.onValueChanged.AddListener(OnPondVolumeChanged);
            if (sculptVolumeSlider != null)
                sculptVolumeSlider.onValueChanged.AddListener(OnSculptVolumeChanged);

            // Individual sound volume input field syncs (percentages)
            SetupSliderInputSync(lanternVolumeSlider, lanternVolumeInput, "{0:F0}", isPercentage: true);
            SetupSliderInputSync(fairyRingVolumeSlider, fairyRingVolumeInput, "{0:F0}", isPercentage: true);
            SetupSliderInputSync(pondVolumeSlider, pondVolumeInput, "{0:F0}", isPercentage: true);
            SetupSliderInputSync(sculptVolumeSlider, sculptVolumeInput, "{0:F0}", isPercentage: true);

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

            // Visitor Gameplay input field syncs
            SetupSliderInputSync(visitorSpeedSlider, visitorSpeedInput, "{0:F1}");
            SetupSliderInputSync(confusionChanceSlider, confusionChanceInput, "{0:F0}", isPercentage: true);
            SetupSliderInputSync(confusionDistanceMinSlider, confusionDistanceMinInput, "{0:F0}");
            SetupSliderInputSync(confusionDistanceMaxSlider, confusionDistanceMaxInput, "{0:F0}");

            // Spawning Settings - Difficulty selector (3 stops: Easy, Normal, Hard)
            if (spawnIntervalSlider != null)
            {
                spawnIntervalSlider.wholeNumbers = true;
                spawnIntervalSlider.minValue = 0;
                spawnIntervalSlider.maxValue = 2;
                spawnIntervalSlider.onValueChanged.AddListener(OnDifficultyChanged);
            }
            if (enableGoblinToggle != null)
                enableGoblinToggle.onValueChanged.AddListener(OnEnableGoblinChanged);

            // Hide the input field for difficulty mode (we show labels instead)
            if (spawnIntervalInput != null)
                spawnIntervalInput.gameObject.SetActive(false);

            // Game Flow
            if (autoStartDelaySlider != null)
                autoStartDelaySlider.onValueChanged.AddListener(OnAutoStartDelayChanged);
            if (startingEssenceSlider != null)
                startingEssenceSlider.onValueChanged.AddListener(OnStartingEssenceChanged);
            if (useFixedSeedToggle != null)
                useFixedSeedToggle.onValueChanged.AddListener(OnUseFixedSeedChanged);
            if (randomSeedInput != null)
                randomSeedInput.onEndEdit.AddListener(OnRandomSeedInputChanged);

            // Game Flow input field syncs
            SetupSliderInputSync(autoStartDelaySlider, autoStartDelayInput, "{0:F1}");
            SetupSliderInputSync(startingEssenceSlider, startingEssenceInput, "{0:F0}");

            // Player Controls
            if (focusSpeedSlider != null)
                focusSpeedSlider.onValueChanged.AddListener(OnFocusSpeedChanged);

            // Player Controls input field sync
            SetupSliderInputSync(focusSpeedSlider, focusSpeedInput, "{0:F1}");

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
            UpdateInputFromSlider(fieldOfViewInput, GameSettings.CameraFieldOfView, "{0:F0}");
            SetSliderValue(cameraPanSpeedSlider, GameSettings.CameraPanSpeed, 1f, 30f);
            UpdateValueText(cameraPanSpeedText, GameSettings.CameraPanSpeed, "{0:F1}");
            UpdateInputFromSlider(cameraPanSpeedInput, GameSettings.CameraPanSpeed, "{0:F1}");
            SetSliderValue(cameraZoomSpeedSlider, GameSettings.CameraZoomSpeed, 1f, 20f);
            UpdateValueText(cameraZoomSpeedText, GameSettings.CameraZoomSpeed, "{0:F1}");
            UpdateInputFromSlider(cameraZoomSpeedInput, GameSettings.CameraZoomSpeed, "{0:F1}");
            SetSliderValue(cameraMinZoomSlider, GameSettings.CameraMinZoom, 1f, 10f);
            UpdateValueText(cameraMinZoomText, GameSettings.CameraMinZoom, "{0:F1}");
            UpdateInputFromSlider(cameraMinZoomInput, GameSettings.CameraMinZoom, "{0:F1}");
            SetSliderValue(cameraMaxZoomSlider, GameSettings.CameraMaxZoom, 10f, 50f);
            UpdateValueText(cameraMaxZoomText, GameSettings.CameraMaxZoom, "{0:F1}");
            UpdateInputFromSlider(cameraMaxZoomInput, GameSettings.CameraMaxZoom, "{0:F1}");
            SetSliderValue(cameraMovementSpeedSlider, GameSettings.CameraMovementSpeed, 0.1f, 10f);
            UpdateValueText(cameraMovementSpeedText, GameSettings.CameraMovementSpeed, "{0:F1}");
            UpdateInputFromSlider(cameraMovementSpeedInput, GameSettings.CameraMovementSpeed, "{0:F1}");
            SetSliderValue(lightLevelSlider, GameSettings.LightLevel, 0f, 2f);
            UpdateValueText(lightLevelText, GameSettings.LightLevel, "{0:F1}");
            UpdateInputFromSlider(lightLevelInput, GameSettings.LightLevel, "{0:F1}");

            // Audio settings
            SetSliderValue(sfxVolumeSlider, GameSettings.SfxVolume, 0f, 1f);
            UpdateValueText(sfxVolumeText, GameSettings.SfxVolume, "{0:P0}");
            UpdateInputFromSlider(sfxVolumeInput, GameSettings.SfxVolume, "{0:F0}", isPercentage: true);
            SetSliderValue(musicVolumeSlider, GameSettings.MusicVolume, 0f, 1f);
            UpdateValueText(musicVolumeText, GameSettings.MusicVolume, "{0:P0}");
            UpdateInputFromSlider(musicVolumeInput, GameSettings.MusicVolume, "{0:F0}", isPercentage: true);

            // Individual sound volumes
            SetSliderValue(lanternVolumeSlider, GameSettings.LanternVolume, 0f, 1f);
            UpdateValueText(lanternVolumeText, GameSettings.LanternVolume, "{0:P0}");
            UpdateInputFromSlider(lanternVolumeInput, GameSettings.LanternVolume, "{0:F0}", isPercentage: true);
            SetSliderValue(fairyRingVolumeSlider, GameSettings.FairyRingVolume, 0f, 1f);
            UpdateValueText(fairyRingVolumeText, GameSettings.FairyRingVolume, "{0:P0}");
            UpdateInputFromSlider(fairyRingVolumeInput, GameSettings.FairyRingVolume, "{0:F0}", isPercentage: true);
            SetSliderValue(pondVolumeSlider, GameSettings.PondVolume, 0f, 1f);
            UpdateValueText(pondVolumeText, GameSettings.PondVolume, "{0:P0}");
            UpdateInputFromSlider(pondVolumeInput, GameSettings.PondVolume, "{0:F0}", isPercentage: true);
            SetSliderValue(sculptVolumeSlider, GameSettings.SculptVolume, 0f, 1f);
            UpdateValueText(sculptVolumeText, GameSettings.SculptVolume, "{0:P0}");
            UpdateInputFromSlider(sculptVolumeInput, GameSettings.SculptVolume, "{0:F0}", isPercentage: true);

            // Visitor Gameplay
            SetSliderValue(visitorSpeedSlider, GameSettings.VisitorSpeed, 0.5f, 10f);
            UpdateValueText(visitorSpeedText, GameSettings.VisitorSpeed, "{0:F1}");
            UpdateInputFromSlider(visitorSpeedInput, GameSettings.VisitorSpeed, "{0:F1}");
            if (confusionEnabledToggle != null)
                confusionEnabledToggle.isOn = GameSettings.ConfusionEnabled;
            SetSliderValue(confusionChanceSlider, GameSettings.ConfusionChance, 0f, 1f);
            UpdateValueText(confusionChanceText, GameSettings.ConfusionChance, "{0:P0}");
            UpdateInputFromSlider(confusionChanceInput, GameSettings.ConfusionChance, "{0:F0}", isPercentage: true);
            SetSliderValue(confusionDistanceMinSlider, GameSettings.ConfusionDistanceMin, 1f, 50f);
            UpdateValueText(confusionDistanceMinText, GameSettings.ConfusionDistanceMin, "{0:F0}");
            UpdateInputFromSlider(confusionDistanceMinInput, GameSettings.ConfusionDistanceMin, "{0:F0}");
            SetSliderValue(confusionDistanceMaxSlider, GameSettings.ConfusionDistanceMax, 1f, 50f);
            UpdateValueText(confusionDistanceMaxText, GameSettings.ConfusionDistanceMax, "{0:F0}");
            UpdateInputFromSlider(confusionDistanceMaxInput, GameSettings.ConfusionDistanceMax, "{0:F0}");

            // Spawning Settings - Load difficulty based on saved spawn interval
            int difficultyIndex = SpawnIntervalToDifficultyIndex(GameSettings.SpawnInterval);
            if (spawnIntervalSlider != null)
            {
                spawnIntervalSlider.wholeNumbers = true;
                spawnIntervalSlider.minValue = 0;
                spawnIntervalSlider.maxValue = 2;
                spawnIntervalSlider.value = difficultyIndex;
            }
            UpdateDifficultyText(difficultyIndex);
            if (enableGoblinToggle != null)
                enableGoblinToggle.isOn = GameSettings.EnableGoblin;

            // Game Flow
            SetSliderValue(autoStartDelaySlider, GameSettings.AutoStartDelay, 0f, 10f);
            UpdateValueText(autoStartDelayText, GameSettings.AutoStartDelay, "{0:F1}s");
            UpdateInputFromSlider(autoStartDelayInput, GameSettings.AutoStartDelay, "{0:F1}");
            SetSliderValue(startingEssenceSlider, GameSettings.StartingEssence, 0f, 1000f);
            UpdateValueText(startingEssenceText, GameSettings.StartingEssence, "{0:F0}");
            UpdateInputFromSlider(startingEssenceInput, GameSettings.StartingEssence, "{0:F0}");
            if (useFixedSeedToggle != null)
                useFixedSeedToggle.isOn = GameSettings.UseFixedSeed;
            if (randomSeedInput != null)
            {
                randomSeedInput.text = GameSettings.RandomSeed.ToString();
                randomSeedInput.characterLimit = 10; // Limit to 10 digits
                randomSeedInput.interactable = GameSettings.UseFixedSeed;
            }
            UpdateCurrentSeedDisplay();

            // Tutorial
            if (showTutorialToggle != null)
                showTutorialToggle.isOn = GameSettings.ShowTutorialOnFirstRun;

            // Player Controls
            SetSliderValue(focusSpeedSlider, GameSettings.FocusSpeed, 5f, 15f);
            UpdateValueText(focusSpeedText, GameSettings.FocusSpeed, "{0:F1}");
            UpdateInputFromSlider(focusSpeedInput, GameSettings.FocusSpeed, "{0:F1}");
            SetDropdownValue(heartPower1KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower1Key));
            SetDropdownValue(heartPower2KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower2Key));
            SetDropdownValue(heartPower3KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower3Key));
            SetDropdownValue(heartPower4KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower4Key));
            SetDropdownValue(heartPower5KeyDropdown, KeyCodeToDropdownIndex(GameSettings.HeartPower5Key));

            // Screenshot Settings
            if (screenshotPathInput != null)
                screenshotPathInput.text = GameSettings.ScreenshotPath;
            SetDropdownValue(screenshotKeyDropdown, KeyCodeToDropdownIndex(GameSettings.ScreenshotKey));

            // Key Bindings (Controls tab)
            LoadBindingSettings();
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
            UpdateInputFromSlider(fieldOfViewInput, value, "{0:F0}");
        }

        private void OnCameraPanSpeedChanged(float value)
        {
            UpdateValueText(cameraPanSpeedText, value, "{0:F1}");
            UpdateInputFromSlider(cameraPanSpeedInput, value, "{0:F1}");
        }

        private void OnCameraZoomSpeedChanged(float value)
        {
            UpdateValueText(cameraZoomSpeedText, value, "{0:F1}");
            UpdateInputFromSlider(cameraZoomSpeedInput, value, "{0:F1}");
        }

        private void OnCameraMinZoomChanged(float value)
        {
            UpdateValueText(cameraMinZoomText, value, "{0:F1}");
            UpdateInputFromSlider(cameraMinZoomInput, value, "{0:F1}");
        }

        private void OnCameraMaxZoomChanged(float value)
        {
            UpdateValueText(cameraMaxZoomText, value, "{0:F1}");
            UpdateInputFromSlider(cameraMaxZoomInput, value, "{0:F1}");
        }

        private void OnCameraMovementSpeedChanged(float value)
        {
            UpdateValueText(cameraMovementSpeedText, value, "{0:F1}");
            UpdateInputFromSlider(cameraMovementSpeedInput, value, "{0:F1}");
        }

        private void OnLightLevelChanged(float value)
        {
            UpdateValueText(lightLevelText, value, "{0:F1}");
            UpdateInputFromSlider(lightLevelInput, value, "{0:F1}");
            // Apply light level immediately to the directional light if it exists
            ApplyLightLevel(value);
        }

        private void ApplyLightLevel(float intensity)
        {
            // Find and update the directional light in the scene
            var directionalLight = FindDirectionalLight();
            if (directionalLight != null)
            {
                directionalLight.intensity = intensity;
            }
        }

        private Light FindDirectionalLight()
        {
            // Look for a light named "Directional Light" or the first directional light
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }
            return null;
        }

        // Audio callbacks
        private void OnSfxVolumeChanged(float value)
        {
            UpdateValueText(sfxVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(sfxVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        private void OnMusicVolumeChanged(float value)
        {
            UpdateValueText(musicVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(musicVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        // Individual sound volume callbacks
        private void OnLanternVolumeChanged(float value)
        {
            UpdateValueText(lanternVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(lanternVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        private void OnFairyRingVolumeChanged(float value)
        {
            UpdateValueText(fairyRingVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(fairyRingVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        private void OnPondVolumeChanged(float value)
        {
            UpdateValueText(pondVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(pondVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        private void OnSculptVolumeChanged(float value)
        {
            UpdateValueText(sculptVolumeText, value, "{0:P0}");
            UpdateInputFromSlider(sculptVolumeInput, value, "{0:F0}", isPercentage: true);
        }

        // Visitor callbacks
        private void OnVisitorSpeedChanged(float value)
        {
            UpdateValueText(visitorSpeedText, value, "{0:F1}");
            UpdateInputFromSlider(visitorSpeedInput, value, "{0:F1}");
        }

        private void OnConfusionEnabledChanged(bool value)
        {
            // Toggle is handled directly, no text update needed
        }

        private void OnConfusionChanceChanged(float value)
        {
            UpdateValueText(confusionChanceText, value, "{0:P0}");
            UpdateInputFromSlider(confusionChanceInput, value, "{0:F0}", isPercentage: true);
        }

        private void OnConfusionDistanceMinChanged(float value)
        {
            UpdateValueText(confusionDistanceMinText, value, "{0:F0}");
            UpdateInputFromSlider(confusionDistanceMinInput, value, "{0:F0}");
        }

        private void OnConfusionDistanceMaxChanged(float value)
        {
            UpdateValueText(confusionDistanceMaxText, value, "{0:F0}");
            UpdateInputFromSlider(confusionDistanceMaxInput, value, "{0:F0}");
        }

        // Spawning callbacks - Difficulty selector
        private void OnDifficultyChanged(float value)
        {
            int index = Mathf.RoundToInt(value);
            UpdateDifficultyText(index);
        }

        private void UpdateDifficultyText(int difficultyIndex)
        {
            if (spawnIntervalText != null && difficultyIndex >= 0 && difficultyIndex < difficultyLabels.Length)
            {
                spawnIntervalText.text = difficultyLabels[difficultyIndex];
            }
        }

        /// <summary>
        /// Converts a spawn interval value to the nearest difficulty index.
        /// </summary>
        private int SpawnIntervalToDifficultyIndex(float spawnInterval)
        {
            // Find the closest matching difficulty
            float minDiff = float.MaxValue;
            int bestIndex = 1; // Default to Normal

            for (int i = 0; i < difficultySpawnIntervals.Length; i++)
            {
                float diff = Mathf.Abs(spawnInterval - difficultySpawnIntervals[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Converts a difficulty index to the corresponding spawn interval.
        /// </summary>
        private float DifficultyIndexToSpawnInterval(int index)
        {
            if (index >= 0 && index < difficultySpawnIntervals.Length)
            {
                return difficultySpawnIntervals[index];
            }
            return 5f; // Default to Normal (5 seconds)
        }

        private void OnEnableGoblinChanged(bool value)
        {
            // Toggle is handled directly
        }

        // Game Flow callbacks
        private void OnAutoStartDelayChanged(float value)
        {
            UpdateValueText(autoStartDelayText, value, "{0:F1}s");
            UpdateInputFromSlider(autoStartDelayInput, value, "{0:F1}");
        }

        private void OnStartingEssenceChanged(float value)
        {
            UpdateValueText(startingEssenceText, value, "{0:F0}");
            UpdateInputFromSlider(startingEssenceInput, value, "{0:F0}");
        }

        private void OnUseFixedSeedChanged(bool value)
        {
            // Enable/disable seed input based on toggle
            if (randomSeedInput != null)
                randomSeedInput.interactable = value;
            UpdateCurrentSeedDisplay();
        }

        private void OnRandomSeedInputChanged(string value)
        {
            // Validate input is a valid integer
            if (!int.TryParse(value, out _) && randomSeedInput != null)
            {
                randomSeedInput.text = GameSettings.RandomSeed.ToString();
            }
            UpdateCurrentSeedDisplay();
        }

        private void UpdateCurrentSeedDisplay()
        {
            if (currentSeedText == null) return;

            if (useFixedSeedToggle != null && useFixedSeedToggle.isOn)
            {
                if (randomSeedInput != null && int.TryParse(randomSeedInput.text, out int seed))
                {
                    currentSeedText.text = $"Fixed Seed: {seed}";
                }
                else
                {
                    currentSeedText.text = $"Fixed Seed: {GameSettings.RandomSeed}";
                }
            }
            else
            {
                // Show the currently active seed from RandomManager
                if (RandomManager.IsInitialized)
                {
                    currentSeedText.text = $"Current Seed: {RandomManager.CurrentSeed} (random)";
                }
                else
                {
                    currentSeedText.text = "Current Seed: (random on start)";
                }
            }
        }

        // Player Controls callbacks
        private void OnFocusSpeedChanged(float value)
        {
            UpdateValueText(focusSpeedText, value, "{0:F1}");
            UpdateInputFromSlider(focusSpeedInput, value, "{0:F1}");
        }

        // Screenshot callbacks
        private void OnBrowseScreenshotPath()
        {
            // Open the folder in the system file explorer so user can copy the path
            string currentPath = screenshotPathInput != null ? screenshotPathInput.text : Application.persistentDataPath;
            if (System.IO.Directory.Exists(currentPath))
            {
                Application.OpenURL("file://" + currentPath);
            }
            else
            {
                Application.OpenURL("file://" + Application.persistentDataPath);
            }
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
            {
                GameSettings.ResolutionIndex = resolutionDropdown.value;
                // Also save the actual dimensions for reliable resolution setting
                int index = resolutionDropdown.value;
                if (index >= 0 && index < availableResolutions.Count)
                {
                    Resolution res = availableResolutions[index];
                    GameSettings.ResolutionWidth = res.width;
                    GameSettings.ResolutionHeight = res.height;
                }
            }

            // Camera settings
            GameSettings.CameraFieldOfView = GetSliderValue(fieldOfViewSlider);
            GameSettings.CameraPanSpeed = GetSliderValue(cameraPanSpeedSlider);
            GameSettings.CameraZoomSpeed = GetSliderValue(cameraZoomSpeedSlider);
            GameSettings.CameraMinZoom = GetSliderValue(cameraMinZoomSlider);
            GameSettings.CameraMaxZoom = GetSliderValue(cameraMaxZoomSlider);
            GameSettings.CameraMovementSpeed = GetSliderValue(cameraMovementSpeedSlider);
            GameSettings.LightLevel = GetSliderValue(lightLevelSlider);

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

            // Spawning Settings - Convert difficulty index to spawn interval
            if (spawnIntervalSlider != null)
            {
                int difficultyIndex = Mathf.RoundToInt(spawnIntervalSlider.value);
                GameSettings.SpawnInterval = DifficultyIndexToSpawnInterval(difficultyIndex);
            }
            if (enableGoblinToggle != null)
                GameSettings.EnableGoblin = enableGoblinToggle.isOn;

            // Game Flow
            if (autoStartDelaySlider != null)
                GameSettings.AutoStartDelay = autoStartDelaySlider.value;
            if (startingEssenceSlider != null)
                GameSettings.StartingEssence = (int)startingEssenceSlider.value;
            if (useFixedSeedToggle != null)
                GameSettings.UseFixedSeed = useFixedSeedToggle.isOn;
            if (randomSeedInput != null && int.TryParse(randomSeedInput.text, out int seedValue))
                GameSettings.RandomSeed = seedValue;

            // Tutorial
            if (showTutorialToggle != null)
                GameSettings.ShowTutorialOnFirstRun = showTutorialToggle.isOn;

            // Player Controls
            GameSettings.FocusSpeed = GetSliderValue(focusSpeedSlider);
            // Note: HeartPower key dropdowns are deprecated - use Controls tab bindings instead

            // Screenshot Settings
            if (screenshotPathInput != null)
                GameSettings.ScreenshotPath = screenshotPathInput.text;
            // Note: Screenshot key dropdown is deprecated - use Controls tab binding instead

            // Key Bindings (from Controls tab)
            // Primary bindings (column 1)
            if (!string.IsNullOrEmpty(bindingHeartPower1))
                GameSettings.HeartPower1Binding = bindingHeartPower1;
            if (!string.IsNullOrEmpty(bindingHeartPower2))
                GameSettings.HeartPower2Binding = bindingHeartPower2;
            if (!string.IsNullOrEmpty(bindingHeartPower3))
                GameSettings.HeartPower3Binding = bindingHeartPower3;
            if (!string.IsNullOrEmpty(bindingHeartPower4))
                GameSettings.HeartPower4Binding = bindingHeartPower4;
            if (!string.IsNullOrEmpty(bindingHeartPower5))
                GameSettings.HeartPower5Binding = bindingHeartPower5;
            if (!string.IsNullOrEmpty(bindingSculptPond))
                GameSettings.SculptPondBinding = bindingSculptPond;
            if (!string.IsNullOrEmpty(bindingSculptLantern))
                GameSettings.SculptLanternBinding = bindingSculptLantern;
            if (!string.IsNullOrEmpty(bindingSculptRing))
                GameSettings.SculptRingBinding = bindingSculptRing;
            if (!string.IsNullOrEmpty(bindingSculptRemove))
                GameSettings.SculptRemoveBinding = bindingSculptRemove;
            if (!string.IsNullOrEmpty(bindingCameraMoveForward))
                GameSettings.CameraMoveForwardBinding = bindingCameraMoveForward;
            if (!string.IsNullOrEmpty(bindingCameraMoveBackward))
                GameSettings.CameraMoveBackwardBinding = bindingCameraMoveBackward;
            if (!string.IsNullOrEmpty(bindingCameraTurnLeft))
                GameSettings.CameraTurnLeftBinding = bindingCameraTurnLeft;
            if (!string.IsNullOrEmpty(bindingCameraTurnRight))
                GameSettings.CameraTurnRightBinding = bindingCameraTurnRight;
            if (!string.IsNullOrEmpty(bindingCameraFocusHeart))
                GameSettings.CameraFocusHeartBinding = bindingCameraFocusHeart;
            if (!string.IsNullOrEmpty(bindingCameraFocusEntrance))
                GameSettings.CameraFocusEntranceBinding = bindingCameraFocusEntrance;
            if (!string.IsNullOrEmpty(bindingCameraFocusVisitor))
                GameSettings.CameraFocusVisitorBinding = bindingCameraFocusVisitor;
            if (!string.IsNullOrEmpty(bindingCameraForward))
                GameSettings.CameraForwardBinding = bindingCameraForward;
            if (!string.IsNullOrEmpty(bindingCameraOrbit))
                GameSettings.CameraOrbitBinding = bindingCameraOrbit;
            if (!string.IsNullOrEmpty(bindingCameraPan))
                GameSettings.CameraPanBinding = bindingCameraPan;
            if (!string.IsNullOrEmpty(bindingScreenshot))
                GameSettings.ScreenshotBinding = bindingScreenshot;

            // Alt bindings (column 2) - save even if empty (cleared binding)
            GameSettings.HeartPower1AltBinding = bindingHeartPower1Alt ?? "";
            GameSettings.HeartPower2AltBinding = bindingHeartPower2Alt ?? "";
            GameSettings.HeartPower3AltBinding = bindingHeartPower3Alt ?? "";
            GameSettings.HeartPower4AltBinding = bindingHeartPower4Alt ?? "";
            GameSettings.HeartPower5AltBinding = bindingHeartPower5Alt ?? "";
            GameSettings.SculptPondAltBinding = bindingSculptPondAlt ?? "";
            GameSettings.SculptLanternAltBinding = bindingSculptLanternAlt ?? "";
            GameSettings.SculptRingAltBinding = bindingSculptRingAlt ?? "";
            GameSettings.SculptRemoveAltBinding = bindingSculptRemoveAlt ?? "";
            GameSettings.CameraMoveForwardAltBinding = bindingCameraMoveForwardAlt ?? "";
            GameSettings.CameraMoveBackwardAltBinding = bindingCameraMoveBackwardAlt ?? "";
            GameSettings.CameraTurnLeftAltBinding = bindingCameraTurnLeftAlt ?? "";
            GameSettings.CameraTurnRightAltBinding = bindingCameraTurnRightAlt ?? "";
            GameSettings.CameraFocusHeartAltBinding = bindingCameraFocusHeartAlt ?? "";
            GameSettings.CameraFocusEntranceAltBinding = bindingCameraFocusEntranceAlt ?? "";
            GameSettings.CameraFocusVisitorAltBinding = bindingCameraFocusVisitorAlt ?? "";
            GameSettings.CameraForwardAltBinding = bindingCameraForwardAlt ?? "";
            GameSettings.CameraOrbitAltBinding = bindingCameraOrbitAlt ?? "";
            GameSettings.CameraPanAltBinding = bindingCameraPanAlt ?? "";
            GameSettings.ScreenshotAltBinding = bindingScreenshotAlt ?? "";

            // Tertiary bindings (column 3) - save even if empty (cleared binding)
            GameSettings.HeartPower1TertiaryBinding = bindingHeartPower1Tertiary ?? "";
            GameSettings.HeartPower2TertiaryBinding = bindingHeartPower2Tertiary ?? "";
            GameSettings.HeartPower3TertiaryBinding = bindingHeartPower3Tertiary ?? "";
            GameSettings.HeartPower4TertiaryBinding = bindingHeartPower4Tertiary ?? "";
            GameSettings.HeartPower5TertiaryBinding = bindingHeartPower5Tertiary ?? "";
            GameSettings.SculptPondTertiaryBinding = bindingSculptPondTertiary ?? "";
            GameSettings.SculptLanternTertiaryBinding = bindingSculptLanternTertiary ?? "";
            GameSettings.SculptRingTertiaryBinding = bindingSculptRingTertiary ?? "";
            GameSettings.SculptRemoveTertiaryBinding = bindingSculptRemoveTertiary ?? "";
            GameSettings.CameraMoveForwardTertiaryBinding = bindingCameraMoveForwardTertiary ?? "";
            GameSettings.CameraMoveBackwardTertiaryBinding = bindingCameraMoveBackwardTertiary ?? "";
            GameSettings.CameraTurnLeftTertiaryBinding = bindingCameraTurnLeftTertiary ?? "";
            GameSettings.CameraTurnRightTertiaryBinding = bindingCameraTurnRightTertiary ?? "";
            GameSettings.CameraFocusHeartTertiaryBinding = bindingCameraFocusHeartTertiary ?? "";
            GameSettings.CameraFocusEntranceTertiaryBinding = bindingCameraFocusEntranceTertiary ?? "";
            GameSettings.CameraFocusVisitorTertiaryBinding = bindingCameraFocusVisitorTertiary ?? "";
            GameSettings.CameraForwardTertiaryBinding = bindingCameraForwardTertiary ?? "";
            GameSettings.CameraOrbitTertiaryBinding = bindingCameraOrbitTertiary ?? "";
            GameSettings.CameraPanTertiaryBinding = bindingCameraPanTertiary ?? "";
            GameSettings.ScreenshotTertiaryBinding = bindingScreenshotTertiary ?? "";

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

        /// <summary>
        /// Updates the input field to match the slider value.
        /// </summary>
        private void UpdateInputFromSlider(TMP_InputField input, float value, string format, bool isPercentage = false)
        {
            if (input == null) return;

            if (isPercentage)
            {
                input.text = string.Format(format, value * 100f);
            }
            else
            {
                input.text = string.Format(format, value);
            }
        }

        /// <summary>
        /// Sets up bidirectional sync between a slider and input field.
        /// </summary>
        private void SetupSliderInputSync(Slider slider, TMP_InputField input, string format, bool isPercentage = false, string suffix = "")
        {
            if (slider == null || input == null) return;

            // Configure input field
            input.contentType = TMP_InputField.ContentType.DecimalNumber;

            // Initialize input with slider value
            UpdateInputFromSlider(input, slider.value, format, isPercentage);

            // Input -> Slider sync
            input.onEndEdit.AddListener((text) =>
            {
                // Clean the text (remove suffix and percentage sign)
                string cleanText = text.Replace("%", "");
                if (!string.IsNullOrEmpty(suffix))
                {
                    cleanText = cleanText.Replace(suffix, "");
                }
                cleanText = cleanText.Trim();

                if (float.TryParse(cleanText, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value))
                {
                    // Convert from percentage display to decimal if needed
                    if (isPercentage)
                    {
                        value /= 100f;
                    }

                    // Clamp to slider range
                    value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
                    slider.value = value;

                    // Update input to show clamped value
                    UpdateInputFromSlider(input, value, format, isPercentage);
                }
                else
                {
                    // Invalid input, revert to slider value
                    UpdateInputFromSlider(input, slider.value, format, isPercentage);
                }
            });

            // Slider -> Input sync (via existing onValueChanged callback)
            // We'll update the input field in the slider callback methods
        }

        #region Key Binding Capture Handling

        /// <summary>
        /// Called when a KeyBindingCapture starts capturing input.
        /// </summary>
        public void OnBindingCaptureStarted(KeyBindingCapture capture)
        {
            // Cancel any existing capture
            if (activeCapture != null && activeCapture != capture)
            {
                activeCapture.CancelCapture();
            }
            activeCapture = capture;
        }

        /// <summary>
        /// Called when a KeyBindingCapture completes capturing input.
        /// </summary>
        public void OnBindingCaptureCompleted(KeyBindingCapture capture)
        {
            if (activeCapture == capture)
            {
                activeCapture = null;
            }
        }

        /// <summary>
        /// Loads binding values from GameSettings into the cached binding variables.
        /// Called during LoadSettings.
        /// </summary>
        private void LoadBindingSettings()
        {
            // Primary bindings (column 1)
            bindingHeartPower1 = GameSettings.HeartPower1Binding;
            bindingHeartPower2 = GameSettings.HeartPower2Binding;
            bindingHeartPower3 = GameSettings.HeartPower3Binding;
            bindingHeartPower4 = GameSettings.HeartPower4Binding;
            bindingHeartPower5 = GameSettings.HeartPower5Binding;
            bindingSculptPond = GameSettings.SculptPondBinding;
            bindingSculptLantern = GameSettings.SculptLanternBinding;
            bindingSculptRing = GameSettings.SculptRingBinding;
            bindingSculptRemove = GameSettings.SculptRemoveBinding;
            bindingCameraMoveForward = GameSettings.CameraMoveForwardBinding;
            bindingCameraMoveBackward = GameSettings.CameraMoveBackwardBinding;
            bindingCameraTurnLeft = GameSettings.CameraTurnLeftBinding;
            bindingCameraTurnRight = GameSettings.CameraTurnRightBinding;
            bindingCameraFocusHeart = GameSettings.CameraFocusHeartBinding;
            bindingCameraFocusEntrance = GameSettings.CameraFocusEntranceBinding;
            bindingCameraFocusVisitor = GameSettings.CameraFocusVisitorBinding;
            bindingCameraForward = GameSettings.CameraForwardBinding;
            bindingCameraOrbit = GameSettings.CameraOrbitBinding;
            bindingCameraPan = GameSettings.CameraPanBinding;
            bindingScreenshot = GameSettings.ScreenshotBinding;

            // Alt bindings (column 2)
            bindingHeartPower1Alt = GameSettings.HeartPower1AltBinding;
            bindingHeartPower2Alt = GameSettings.HeartPower2AltBinding;
            bindingHeartPower3Alt = GameSettings.HeartPower3AltBinding;
            bindingHeartPower4Alt = GameSettings.HeartPower4AltBinding;
            bindingHeartPower5Alt = GameSettings.HeartPower5AltBinding;
            bindingSculptPondAlt = GameSettings.SculptPondAltBinding;
            bindingSculptLanternAlt = GameSettings.SculptLanternAltBinding;
            bindingSculptRingAlt = GameSettings.SculptRingAltBinding;
            bindingSculptRemoveAlt = GameSettings.SculptRemoveAltBinding;
            bindingCameraMoveForwardAlt = GameSettings.CameraMoveForwardAltBinding;
            bindingCameraMoveBackwardAlt = GameSettings.CameraMoveBackwardAltBinding;
            bindingCameraTurnLeftAlt = GameSettings.CameraTurnLeftAltBinding;
            bindingCameraTurnRightAlt = GameSettings.CameraTurnRightAltBinding;
            bindingCameraFocusHeartAlt = GameSettings.CameraFocusHeartAltBinding;
            bindingCameraFocusEntranceAlt = GameSettings.CameraFocusEntranceAltBinding;
            bindingCameraFocusVisitorAlt = GameSettings.CameraFocusVisitorAltBinding;
            bindingCameraForwardAlt = GameSettings.CameraForwardAltBinding;
            bindingCameraOrbitAlt = GameSettings.CameraOrbitAltBinding;
            bindingCameraPanAlt = GameSettings.CameraPanAltBinding;
            bindingScreenshotAlt = GameSettings.ScreenshotAltBinding;

            // Tertiary bindings (column 3)
            bindingHeartPower1Tertiary = GameSettings.HeartPower1TertiaryBinding;
            bindingHeartPower2Tertiary = GameSettings.HeartPower2TertiaryBinding;
            bindingHeartPower3Tertiary = GameSettings.HeartPower3TertiaryBinding;
            bindingHeartPower4Tertiary = GameSettings.HeartPower4TertiaryBinding;
            bindingHeartPower5Tertiary = GameSettings.HeartPower5TertiaryBinding;
            bindingSculptPondTertiary = GameSettings.SculptPondTertiaryBinding;
            bindingSculptLanternTertiary = GameSettings.SculptLanternTertiaryBinding;
            bindingSculptRingTertiary = GameSettings.SculptRingTertiaryBinding;
            bindingSculptRemoveTertiary = GameSettings.SculptRemoveTertiaryBinding;
            bindingCameraMoveForwardTertiary = GameSettings.CameraMoveForwardTertiaryBinding;
            bindingCameraMoveBackwardTertiary = GameSettings.CameraMoveBackwardTertiaryBinding;
            bindingCameraTurnLeftTertiary = GameSettings.CameraTurnLeftTertiaryBinding;
            bindingCameraTurnRightTertiary = GameSettings.CameraTurnRightTertiaryBinding;
            bindingCameraFocusHeartTertiary = GameSettings.CameraFocusHeartTertiaryBinding;
            bindingCameraFocusEntranceTertiary = GameSettings.CameraFocusEntranceTertiaryBinding;
            bindingCameraFocusVisitorTertiary = GameSettings.CameraFocusVisitorTertiaryBinding;
            bindingCameraForwardTertiary = GameSettings.CameraForwardTertiaryBinding;
            bindingCameraOrbitTertiary = GameSettings.CameraOrbitTertiaryBinding;
            bindingCameraPanTertiary = GameSettings.CameraPanTertiaryBinding;
            bindingScreenshotTertiary = GameSettings.ScreenshotTertiaryBinding;

            // Update any KeyBindingCapture components in the controls panel
            UpdateBindingCaptureDisplays();
        }

        /// <summary>
        /// Determines the column (primary/alt/tertiary) from the object name.
        /// Returns 0 for primary, 1 for alt, 2 for tertiary.
        /// </summary>
        private int GetBindingColumn(string objName)
        {
            if (objName.Contains("tertiary")) return 2;
            if (objName.Contains("alt")) return 1;
            return 0;
        }

        /// <summary>
        /// Identifies the binding category from the object name (ignoring column suffix).
        /// Returns a category string or null if unrecognized.
        /// </summary>
        private string GetBindingCategory(string objName)
        {
            // Heart powers
            if (objName.Contains("heartpower1") || objName.Contains("murmuring")) return "heartpower1";
            if (objName.Contains("heartpower2") || objName.Contains("grasp")) return "heartpower2";
            if (objName.Contains("heartpower3") || objName.Contains("devour")) return "heartpower3";
            if (objName.Contains("heartpower4") || objName.Contains("sculpting")) return "heartpower4";
            if (objName.Contains("heartpower5") || objName.Contains("misdirect")) return "heartpower5";
            // Sculpt menu
            if (objName.Contains("sculptpond") || objName.Contains("placepond")) return "sculptpond";
            if (objName.Contains("sculptlantern") || objName.Contains("placelantern")) return "sculptlantern";
            if (objName.Contains("sculptring") || objName.Contains("placering")) return "sculptring";
            if (objName.Contains("sculptremove") || objName.Contains("removeprop")) return "sculptremove";
            // Camera movement - check more specific names first
            if (objName.Contains("moveforward")) return "moveforward";
            if (objName.Contains("movebackward")) return "movebackward";
            if (objName.Contains("turnleft")) return "turnleft";
            if (objName.Contains("turnright")) return "turnright";
            // Camera focus
            if (objName.Contains("focusheart")) return "focusheart";
            if (objName.Contains("focusentrance")) return "focusentrance";
            if (objName.Contains("focusvisitor")) return "focusvisitor";
            // Camera mouse - "forwardmouse" must come before generic "forward"
            if (objName.Contains("forward") && objName.Contains("mouse")) return "forwardmouse";
            if (objName.Contains("orbit")) return "orbit";
            if (objName.Contains("pan")) return "pan";
            // Screenshot
            if (objName.Contains("screenshot")) return "screenshot";
            // Camera movement fallbacks (cameraforward without "mouse")
            if (objName.Contains("cameraforward")) return "moveforward";
            if (objName.Contains("camerabackward")) return "movebackward";
            if (objName.Contains("cameraleft")) return "turnleft";
            if (objName.Contains("cameraright")) return "turnright";
            return null;
        }

        /// <summary>
        /// Gets the cached binding value and assigns the callback for a given category and column.
        /// </summary>
        private void WireBinding(KeyBindingCapture capture, string category, int column)
        {
            // Get the correct value and callback for this category+column combination
            string value;
            System.Action<string> callback;

            switch (category)
            {
                case "heartpower1":
                    if (column == 0) { value = bindingHeartPower1; callback = b => bindingHeartPower1 = b; }
                    else if (column == 1) { value = bindingHeartPower1Alt; callback = b => bindingHeartPower1Alt = b; }
                    else { value = bindingHeartPower1Tertiary; callback = b => bindingHeartPower1Tertiary = b; }
                    break;
                case "heartpower2":
                    if (column == 0) { value = bindingHeartPower2; callback = b => bindingHeartPower2 = b; }
                    else if (column == 1) { value = bindingHeartPower2Alt; callback = b => bindingHeartPower2Alt = b; }
                    else { value = bindingHeartPower2Tertiary; callback = b => bindingHeartPower2Tertiary = b; }
                    break;
                case "heartpower3":
                    if (column == 0) { value = bindingHeartPower3; callback = b => bindingHeartPower3 = b; }
                    else if (column == 1) { value = bindingHeartPower3Alt; callback = b => bindingHeartPower3Alt = b; }
                    else { value = bindingHeartPower3Tertiary; callback = b => bindingHeartPower3Tertiary = b; }
                    break;
                case "heartpower4":
                    if (column == 0) { value = bindingHeartPower4; callback = b => bindingHeartPower4 = b; }
                    else if (column == 1) { value = bindingHeartPower4Alt; callback = b => bindingHeartPower4Alt = b; }
                    else { value = bindingHeartPower4Tertiary; callback = b => bindingHeartPower4Tertiary = b; }
                    break;
                case "heartpower5":
                    if (column == 0) { value = bindingHeartPower5; callback = b => bindingHeartPower5 = b; }
                    else if (column == 1) { value = bindingHeartPower5Alt; callback = b => bindingHeartPower5Alt = b; }
                    else { value = bindingHeartPower5Tertiary; callback = b => bindingHeartPower5Tertiary = b; }
                    break;
                case "sculptpond":
                    if (column == 0) { value = bindingSculptPond; callback = b => bindingSculptPond = b; }
                    else if (column == 1) { value = bindingSculptPondAlt; callback = b => bindingSculptPondAlt = b; }
                    else { value = bindingSculptPondTertiary; callback = b => bindingSculptPondTertiary = b; }
                    break;
                case "sculptlantern":
                    if (column == 0) { value = bindingSculptLantern; callback = b => bindingSculptLantern = b; }
                    else if (column == 1) { value = bindingSculptLanternAlt; callback = b => bindingSculptLanternAlt = b; }
                    else { value = bindingSculptLanternTertiary; callback = b => bindingSculptLanternTertiary = b; }
                    break;
                case "sculptring":
                    if (column == 0) { value = bindingSculptRing; callback = b => bindingSculptRing = b; }
                    else if (column == 1) { value = bindingSculptRingAlt; callback = b => bindingSculptRingAlt = b; }
                    else { value = bindingSculptRingTertiary; callback = b => bindingSculptRingTertiary = b; }
                    break;
                case "sculptremove":
                    if (column == 0) { value = bindingSculptRemove; callback = b => bindingSculptRemove = b; }
                    else if (column == 1) { value = bindingSculptRemoveAlt; callback = b => bindingSculptRemoveAlt = b; }
                    else { value = bindingSculptRemoveTertiary; callback = b => bindingSculptRemoveTertiary = b; }
                    break;
                case "moveforward":
                    if (column == 0) { value = bindingCameraMoveForward; callback = b => bindingCameraMoveForward = b; }
                    else if (column == 1) { value = bindingCameraMoveForwardAlt; callback = b => bindingCameraMoveForwardAlt = b; }
                    else { value = bindingCameraMoveForwardTertiary; callback = b => bindingCameraMoveForwardTertiary = b; }
                    break;
                case "movebackward":
                    if (column == 0) { value = bindingCameraMoveBackward; callback = b => bindingCameraMoveBackward = b; }
                    else if (column == 1) { value = bindingCameraMoveBackwardAlt; callback = b => bindingCameraMoveBackwardAlt = b; }
                    else { value = bindingCameraMoveBackwardTertiary; callback = b => bindingCameraMoveBackwardTertiary = b; }
                    break;
                case "turnleft":
                    if (column == 0) { value = bindingCameraTurnLeft; callback = b => bindingCameraTurnLeft = b; }
                    else if (column == 1) { value = bindingCameraTurnLeftAlt; callback = b => bindingCameraTurnLeftAlt = b; }
                    else { value = bindingCameraTurnLeftTertiary; callback = b => bindingCameraTurnLeftTertiary = b; }
                    break;
                case "turnright":
                    if (column == 0) { value = bindingCameraTurnRight; callback = b => bindingCameraTurnRight = b; }
                    else if (column == 1) { value = bindingCameraTurnRightAlt; callback = b => bindingCameraTurnRightAlt = b; }
                    else { value = bindingCameraTurnRightTertiary; callback = b => bindingCameraTurnRightTertiary = b; }
                    break;
                case "focusheart":
                    if (column == 0) { value = bindingCameraFocusHeart; callback = b => bindingCameraFocusHeart = b; }
                    else if (column == 1) { value = bindingCameraFocusHeartAlt; callback = b => bindingCameraFocusHeartAlt = b; }
                    else { value = bindingCameraFocusHeartTertiary; callback = b => bindingCameraFocusHeartTertiary = b; }
                    break;
                case "focusentrance":
                    if (column == 0) { value = bindingCameraFocusEntrance; callback = b => bindingCameraFocusEntrance = b; }
                    else if (column == 1) { value = bindingCameraFocusEntranceAlt; callback = b => bindingCameraFocusEntranceAlt = b; }
                    else { value = bindingCameraFocusEntranceTertiary; callback = b => bindingCameraFocusEntranceTertiary = b; }
                    break;
                case "focusvisitor":
                    if (column == 0) { value = bindingCameraFocusVisitor; callback = b => bindingCameraFocusVisitor = b; }
                    else if (column == 1) { value = bindingCameraFocusVisitorAlt; callback = b => bindingCameraFocusVisitorAlt = b; }
                    else { value = bindingCameraFocusVisitorTertiary; callback = b => bindingCameraFocusVisitorTertiary = b; }
                    break;
                case "forwardmouse":
                    if (column == 0) { value = bindingCameraForward; callback = b => bindingCameraForward = b; }
                    else if (column == 1) { value = bindingCameraForwardAlt; callback = b => bindingCameraForwardAlt = b; }
                    else { value = bindingCameraForwardTertiary; callback = b => bindingCameraForwardTertiary = b; }
                    break;
                case "orbit":
                    if (column == 0) { value = bindingCameraOrbit; callback = b => bindingCameraOrbit = b; }
                    else if (column == 1) { value = bindingCameraOrbitAlt; callback = b => bindingCameraOrbitAlt = b; }
                    else { value = bindingCameraOrbitTertiary; callback = b => bindingCameraOrbitTertiary = b; }
                    break;
                case "pan":
                    if (column == 0) { value = bindingCameraPan; callback = b => bindingCameraPan = b; }
                    else if (column == 1) { value = bindingCameraPanAlt; callback = b => bindingCameraPanAlt = b; }
                    else { value = bindingCameraPanTertiary; callback = b => bindingCameraPanTertiary = b; }
                    break;
                case "screenshot":
                    if (column == 0)
                    {
                        value = bindingScreenshot;
                        callback = b => { bindingScreenshot = b; SyncScreenshotBindings(b); };
                    }
                    else if (column == 1) { value = bindingScreenshotAlt; callback = b => bindingScreenshotAlt = b; }
                    else { value = bindingScreenshotTertiary; callback = b => bindingScreenshotTertiary = b; }
                    break;
                default:
                    return; // Unknown category
            }

            capture.SetBinding(value ?? "");
            // Use a local variable capture for the lambda to avoid closure issues
            var cb = callback;
            // Remove any previously attached lambda by storing in a dictionary would be complex,
            // so we use named methods for the capture callback approach
            capture.OnBindingCaptured += cb;
        }

        /// <summary>
        /// Finds KeyBindingCapture components in all panels and updates their display values.
        /// All three columns (primary, alt, tertiary) are now fully supported.
        /// </summary>
        private void UpdateBindingCaptureDisplays()
        {
            // Collect captures from all panels (Controls, Video, Gameplay, Audio)
            var allCaptures = new List<KeyBindingCapture>();

            if (controlsPanel != null)
                allCaptures.AddRange(controlsPanel.GetComponentsInChildren<KeyBindingCapture>(true));
            if (videoPanel != null)
                allCaptures.AddRange(videoPanel.GetComponentsInChildren<KeyBindingCapture>(true));
            if (gameplayPanel != null)
                allCaptures.AddRange(gameplayPanel.GetComponentsInChildren<KeyBindingCapture>(true));
            if (audioPanel != null)
                allCaptures.AddRange(audioPanel.GetComponentsInChildren<KeyBindingCapture>(true));

            foreach (var capture in allCaptures)
            {
                string objName = capture.gameObject.name.ToLower();
                int column = GetBindingColumn(objName);
                string category = GetBindingCategory(objName);

                if (category == null)
                {
                    continue;
                }

                WireBinding(capture, category, column);
            }
        }

        /// <summary>
        /// Synchronizes the PRIMARY screenshot binding across VIDEO and CONTROLS tabs.
        /// Only syncs the primary binding - Alt and Tertiary are independent.
        /// This ensures VIDEO tab mirrors the CONTROLS tab primary screenshot binding.
        /// </summary>
        private void SyncScreenshotBindings(string binding)
        {
            // Find all screenshot captures and update only PRIMARY ones (not Alt/Tertiary)
            var allCaptures = new List<KeyBindingCapture>();
            if (controlsPanel != null)
                allCaptures.AddRange(controlsPanel.GetComponentsInChildren<KeyBindingCapture>(true));
            if (videoPanel != null)
                allCaptures.AddRange(videoPanel.GetComponentsInChildren<KeyBindingCapture>(true));

            foreach (var capture in allCaptures)
            {
                string objName = capture.gameObject.name.ToLower();
                // Only sync PRIMARY screenshot bindings (not "alt" or "tertiary")
                if (objName.Contains("screenshot") && !objName.Contains("alt") && !objName.Contains("tertiary"))
                {
                    // Update the display without triggering the callback again
                    if (capture.GetBinding() != binding)
                    {
                        capture.SetBinding(binding);
                    }
                }
            }
        }

        #endregion
    }
}

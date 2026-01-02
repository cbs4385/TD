using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Options menu UI and settings persistence
    /// </summary>
    public class OptionsManager : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI sfxVolumeText;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeText;

        [Header("Camera Settings")]
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
        [SerializeField] private Toggle enableDepthOfFieldToggle;
        [SerializeField] private Slider depthOfFieldIntensitySlider;
        [SerializeField] private TextMeshProUGUI depthOfFieldIntensityText;

        [Header("Visitor Gameplay Settings")]
        [SerializeField] private Slider visitorSpeedSlider;
        [SerializeField] private TextMeshProUGUI visitorSpeedText;
        [SerializeField] private Toggle confusionEnabledToggle;
        [SerializeField] private Slider confusionChanceSlider;
        [SerializeField] private TextMeshProUGUI confusionChanceText;
        [SerializeField] private Slider confusionDistanceMinSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMinText;
        [SerializeField] private Slider confusionDistanceMaxSlider;
        [SerializeField] private TextMeshProUGUI confusionDistanceMaxText;

        [Header("Wave/Difficulty Settings")]
        [SerializeField] private Slider visitorsPerWaveSlider;
        [SerializeField] private TextMeshProUGUI visitorsPerWaveText;
        [SerializeField] private Slider spawnIntervalSlider;
        [SerializeField] private TextMeshProUGUI spawnIntervalText;
        [SerializeField] private Slider waveDurationSlider;
        [SerializeField] private TextMeshProUGUI waveDurationText;
        [SerializeField] private Toggle enableRedCapToggle;
        [SerializeField] private Slider redCapSpawnDelaySlider;
        [SerializeField] private TextMeshProUGUI redCapSpawnDelayText;

        [Header("Game Flow Settings")]
        [SerializeField] private Toggle autoStartNextWaveToggle;
        [SerializeField] private Slider autoStartDelaySlider;
        [SerializeField] private TextMeshProUGUI autoStartDelayText;
        [SerializeField] private Slider startingEssenceSlider;
        [SerializeField] private TextMeshProUGUI startingEssenceText;

        [Header("Visitor Type Settings")]
        [SerializeField] private Toggle enableBasicVisitorToggle;
        [SerializeField] private Toggle enableMistakingVisitorToggle;
        [SerializeField] private Toggle enableLanternDrunkVisitorToggle;
        [SerializeField] private Toggle enableWaryWayfarerVisitorToggle;
        [SerializeField] private Toggle enableSleepwalkingVisitorToggle;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backButton;

        private SceneLoader sceneLoader;

        private void Awake()
        {
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        private void Start()
        {
            LoadSettings();
            SetupUIListeners();
        }

        private void SetupUIListeners()
        {
            // Audio
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (musicVolumeSlider != null)
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

            // Camera
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
            if (enableDepthOfFieldToggle != null)
                enableDepthOfFieldToggle.onValueChanged.AddListener(OnEnableDepthOfFieldChanged);
            if (depthOfFieldIntensitySlider != null)
                depthOfFieldIntensitySlider.onValueChanged.AddListener(OnDepthOfFieldIntensityChanged);

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

            // Wave/Difficulty
            if (visitorsPerWaveSlider != null)
                visitorsPerWaveSlider.onValueChanged.AddListener(OnVisitorsPerWaveChanged);
            if (spawnIntervalSlider != null)
                spawnIntervalSlider.onValueChanged.AddListener(OnSpawnIntervalChanged);
            if (waveDurationSlider != null)
                waveDurationSlider.onValueChanged.AddListener(OnWaveDurationChanged);
            if (enableRedCapToggle != null)
                enableRedCapToggle.onValueChanged.AddListener(OnEnableRedCapChanged);
            if (redCapSpawnDelaySlider != null)
                redCapSpawnDelaySlider.onValueChanged.AddListener(OnRedCapSpawnDelayChanged);

            // Game Flow
            if (autoStartNextWaveToggle != null)
                autoStartNextWaveToggle.onValueChanged.AddListener(OnAutoStartNextWaveChanged);
            if (autoStartDelaySlider != null)
                autoStartDelaySlider.onValueChanged.AddListener(OnAutoStartDelayChanged);
            if (startingEssenceSlider != null)
                startingEssenceSlider.onValueChanged.AddListener(OnStartingEssenceChanged);

            // Visitor Types
            if (enableBasicVisitorToggle != null)
                enableBasicVisitorToggle.onValueChanged.AddListener(OnEnableBasicVisitorChanged);
            if (enableMistakingVisitorToggle != null)
                enableMistakingVisitorToggle.onValueChanged.AddListener(OnEnableMistakingVisitorChanged);
            if (enableLanternDrunkVisitorToggle != null)
                enableLanternDrunkVisitorToggle.onValueChanged.AddListener(OnEnableLanternDrunkVisitorChanged);
            if (enableWaryWayfarerVisitorToggle != null)
                enableWaryWayfarerVisitorToggle.onValueChanged.AddListener(OnEnableWaryWayfarerVisitorChanged);
            if (enableSleepwalkingVisitorToggle != null)
                enableSleepwalkingVisitorToggle.onValueChanged.AddListener(OnEnableSleepwalkingVisitorChanged);

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
            // Audio
            SetSliderValue(sfxVolumeSlider, GameSettings.SfxVolume, 0f, 1f);
            UpdateValueText(sfxVolumeText, GameSettings.SfxVolume, "{0:P0}");
            SetSliderValue(musicVolumeSlider, GameSettings.MusicVolume, 0f, 1f);
            UpdateValueText(musicVolumeText, GameSettings.MusicVolume, "{0:P0}");

            // Camera
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
            if (enableDepthOfFieldToggle != null)
                enableDepthOfFieldToggle.isOn = GameSettings.EnableDepthOfField;
            SetSliderValue(depthOfFieldIntensitySlider, GameSettings.DepthOfFieldIntensity, 0f, 1f);
            UpdateValueText(depthOfFieldIntensityText, GameSettings.DepthOfFieldIntensity, "{0:P0}");

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

            // Wave/Difficulty
            SetSliderValue(visitorsPerWaveSlider, GameSettings.VisitorsPerWave, 1f, 50f);
            UpdateValueText(visitorsPerWaveText, GameSettings.VisitorsPerWave, "{0:F0}");
            SetSliderValue(spawnIntervalSlider, GameSettings.SpawnInterval, 0.1f, 5f);
            UpdateValueText(spawnIntervalText, GameSettings.SpawnInterval, "{0:F1}s");
            SetSliderValue(waveDurationSlider, GameSettings.WaveDuration, 10f, 300f);
            UpdateValueText(waveDurationText, GameSettings.WaveDuration, "{0:F0}s");
            if (enableRedCapToggle != null)
                enableRedCapToggle.isOn = GameSettings.EnableRedCap;
            SetSliderValue(redCapSpawnDelaySlider, GameSettings.RedCapSpawnDelay, 0f, 120f);
            UpdateValueText(redCapSpawnDelayText, GameSettings.RedCapSpawnDelay, "{0:F0}s");

            // Game Flow
            if (autoStartNextWaveToggle != null)
                autoStartNextWaveToggle.isOn = GameSettings.AutoStartNextWave;
            SetSliderValue(autoStartDelaySlider, GameSettings.AutoStartDelay, 0f, 10f);
            UpdateValueText(autoStartDelayText, GameSettings.AutoStartDelay, "{0:F1}s");
            SetSliderValue(startingEssenceSlider, GameSettings.StartingEssence, 0f, 1000f);
            UpdateValueText(startingEssenceText, GameSettings.StartingEssence, "{0:F0}");

            // Visitor Types
            if (enableBasicVisitorToggle != null)
                enableBasicVisitorToggle.isOn = GameSettings.EnableVisitorType_Basic;
            if (enableMistakingVisitorToggle != null)
                enableMistakingVisitorToggle.isOn = GameSettings.EnableVisitorType_Mistaking;
            if (enableLanternDrunkVisitorToggle != null)
                enableLanternDrunkVisitorToggle.isOn = GameSettings.EnableVisitorType_LanternDrunk;
            if (enableWaryWayfarerVisitorToggle != null)
                enableWaryWayfarerVisitorToggle.isOn = GameSettings.EnableVisitorType_WaryWayfarer;
            if (enableSleepwalkingVisitorToggle != null)
                enableSleepwalkingVisitorToggle.isOn = GameSettings.EnableVisitorType_Sleepwalking;
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

        // Camera callbacks
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

        private void OnEnableDepthOfFieldChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnDepthOfFieldIntensityChanged(float value)
        {
            UpdateValueText(depthOfFieldIntensityText, value, "{0:P0}");
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

        // Wave/Difficulty callbacks
        private void OnVisitorsPerWaveChanged(float value)
        {
            UpdateValueText(visitorsPerWaveText, value, "{0:F0}");
        }

        private void OnSpawnIntervalChanged(float value)
        {
            UpdateValueText(spawnIntervalText, value, "{0:F1}s");
        }

        private void OnWaveDurationChanged(float value)
        {
            UpdateValueText(waveDurationText, value, "{0:F0}s");
        }

        private void OnEnableRedCapChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnRedCapSpawnDelayChanged(float value)
        {
            UpdateValueText(redCapSpawnDelayText, value, "{0:F0}s");
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

        // Visitor Type callbacks
        private void OnEnableBasicVisitorChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnEnableMistakingVisitorChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnEnableLanternDrunkVisitorChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnEnableWaryWayfarerVisitorChanged(bool value)
        {
            // Toggle is handled directly
        }

        private void OnEnableSleepwalkingVisitorChanged(bool value)
        {
            // Toggle is handled directly
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
            LoadSettings();
        }

        private void OnBackClicked()
        {
            sceneLoader.LoadMainMenu();
        }

        private void SaveSettings()
        {
            // Audio
            GameSettings.SfxVolume = GetSliderValue(sfxVolumeSlider);
            GameSettings.MusicVolume = GetSliderValue(musicVolumeSlider);

            // Camera
            GameSettings.CameraPanSpeed = GetSliderValue(cameraPanSpeedSlider);
            GameSettings.CameraZoomSpeed = GetSliderValue(cameraZoomSpeedSlider);
            GameSettings.CameraMinZoom = GetSliderValue(cameraMinZoomSlider);
            GameSettings.CameraMaxZoom = GetSliderValue(cameraMaxZoomSlider);
            GameSettings.CameraMovementSpeed = GetSliderValue(cameraMovementSpeedSlider);
            if (enableDepthOfFieldToggle != null)
                GameSettings.EnableDepthOfField = enableDepthOfFieldToggle.isOn;
            GameSettings.DepthOfFieldIntensity = GetSliderValue(depthOfFieldIntensitySlider);

            // Visitor Gameplay
            GameSettings.VisitorSpeed = GetSliderValue(visitorSpeedSlider);
            if (confusionEnabledToggle != null)
                GameSettings.ConfusionEnabled = confusionEnabledToggle.isOn;
            GameSettings.ConfusionChance = GetSliderValue(confusionChanceSlider);
            GameSettings.ConfusionDistanceMin = (int)GetSliderValue(confusionDistanceMinSlider);
            GameSettings.ConfusionDistanceMax = (int)GetSliderValue(confusionDistanceMaxSlider);

            // Wave/Difficulty
            GameSettings.VisitorsPerWave = (int)GetSliderValue(visitorsPerWaveSlider);
            GameSettings.SpawnInterval = GetSliderValue(spawnIntervalSlider);
            GameSettings.WaveDuration = GetSliderValue(waveDurationSlider);
            if (enableRedCapToggle != null)
                GameSettings.EnableRedCap = enableRedCapToggle.isOn;
            GameSettings.RedCapSpawnDelay = GetSliderValue(redCapSpawnDelaySlider);

            // Game Flow
            if (autoStartNextWaveToggle != null)
                GameSettings.AutoStartNextWave = autoStartNextWaveToggle.isOn;
            GameSettings.AutoStartDelay = GetSliderValue(autoStartDelaySlider);
            GameSettings.StartingEssence = (int)GetSliderValue(startingEssenceSlider);

            // Visitor Types
            if (enableBasicVisitorToggle != null)
                GameSettings.EnableVisitorType_Basic = enableBasicVisitorToggle.isOn;
            if (enableMistakingVisitorToggle != null)
                GameSettings.EnableVisitorType_Mistaking = enableMistakingVisitorToggle.isOn;
            if (enableLanternDrunkVisitorToggle != null)
                GameSettings.EnableVisitorType_LanternDrunk = enableLanternDrunkVisitorToggle.isOn;
            if (enableWaryWayfarerVisitorToggle != null)
                GameSettings.EnableVisitorType_WaryWayfarer = enableWaryWayfarerVisitorToggle.isOn;
            if (enableSleepwalkingVisitorToggle != null)
                GameSettings.EnableVisitorType_Sleepwalking = enableSleepwalkingVisitorToggle.isOn;

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

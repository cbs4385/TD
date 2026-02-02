using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Tutorial;
using FaeMaze.Roguelike;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Main Menu UI, including the RNG seed input.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Seed Input")]
        [SerializeField] private TMP_InputField seedInputField;
        [SerializeField] private Toggle useFixedSeedToggle;
        [SerializeField] private Button randomizeSeedButton;

        [Header("Tutorial")]
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Toggle tutorialModeToggle;

        [Header("Shrine (Unlock Shop)")]
        [SerializeField] private Button shrineButton;

        [Header("References")]
        [SerializeField] private SceneLoader sceneLoader;

        // Runtime-created UI components
        private UnlockShopUI _unlockShopUI;
        private TextMeshProUGUI _faeDustDisplayText;

        private void Start()
        {
            LoadSeedSettings();
            LoadTutorialSetting();
            SetupListeners();
            CreateShrineUI();
            CreateFaeDustDisplay();
        }

        private void LoadSeedSettings()
        {
            // Load current seed settings from GameSettings
            if (useFixedSeedToggle != null)
            {
                useFixedSeedToggle.isOn = GameSettings.UseFixedSeed;
            }

            if (seedInputField != null)
            {
                seedInputField.text = GameSettings.RandomSeed.ToString();
                seedInputField.characterLimit = 10; // Limit to 10 digits
                // Enable/disable based on toggle state
                seedInputField.interactable = GameSettings.UseFixedSeed;
            }

            if (randomizeSeedButton != null)
            {
                randomizeSeedButton.interactable = GameSettings.UseFixedSeed;
            }
        }

        private void LoadTutorialSetting()
        {
            // Load the "Show Tutorial on First Run" setting into the tutorial mode toggle
            if (tutorialModeToggle != null)
            {
                tutorialModeToggle.isOn = GameSettings.ShowTutorialOnFirstRun;
            }
        }

        private void SetupListeners()
        {
            if (useFixedSeedToggle != null)
            {
                useFixedSeedToggle.onValueChanged.AddListener(OnUseFixedSeedChanged);
            }

            if (seedInputField != null)
            {
                seedInputField.onEndEdit.AddListener(OnSeedInputChanged);
            }

            if (randomizeSeedButton != null)
            {
                randomizeSeedButton.onClick.AddListener(OnRandomizeSeedClicked);
            }

            if (shrineButton != null)
            {
                shrineButton.onClick.AddListener(OpenShrine);
            }

            if (tutorialModeToggle != null)
            {
                tutorialModeToggle.onValueChanged.AddListener(OnTutorialModeChanged);
            }
        }

        private void OnTutorialModeChanged(bool enabled)
        {
            // Sync with the GameSettings "Show Tutorial on First Run" setting
            GameSettings.ShowTutorialOnFirstRun = enabled;
        }

        private void CreateShrineUI()
        {
            // Create the UnlockShopUI if it doesn't exist
            _unlockShopUI = FindFirstObjectByType<UnlockShopUI>();
            if (_unlockShopUI == null)
            {
                GameObject shopObj = new GameObject("UnlockShopUI");
                _unlockShopUI = shopObj.AddComponent<UnlockShopUI>();
            }
        }

        private void CreateFaeDustDisplay()
        {
            // Create a small Fae Dust display in the corner of the main menu
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject dustObj = new GameObject("FaeDustDisplay");
            dustObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = dustObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-20, -20);
            rect.sizeDelta = new Vector2(200, 40);

            _faeDustDisplayText = dustObj.AddComponent<TextMeshProUGUI>();
            UpdateFaeDustDisplay();
            _faeDustDisplayText.fontSize = 24;
            _faeDustDisplayText.fontStyle = FontStyles.Bold;
            _faeDustDisplayText.alignment = TextAlignmentOptions.MidlineRight;
            _faeDustDisplayText.color = new Color(0.7f, 0.5f, 1f);

            // Subscribe to Fae Dust changes
            if (MetaProgressionManager.Instance != null)
            {
                MetaProgressionManager.Instance.OnFaeDustChanged += OnFaeDustChanged;
            }
        }

        private void UpdateFaeDustDisplay()
        {
            if (_faeDustDisplayText != null)
            {
                int dust = MetaProgressionManager.Instance?.FaeDust ?? 0;
                _faeDustDisplayText.text = $"Fae Dust: {dust}";
            }
        }

        private void OnFaeDustChanged(int newAmount)
        {
            UpdateFaeDustDisplay();
        }

        /// <summary>
        /// Opens the Shrine (Unlock Shop) UI.
        /// </summary>
        public void OpenShrine()
        {
            if (_unlockShopUI != null)
            {
                _unlockShopUI.Show();
            }
        }

        private void OnUseFixedSeedChanged(bool useFixed)
        {
            GameSettings.UseFixedSeed = useFixed;

            // Enable/disable seed input based on toggle
            if (seedInputField != null)
            {
                seedInputField.interactable = useFixed;
            }

            if (randomizeSeedButton != null)
            {
                randomizeSeedButton.interactable = useFixed;
            }
        }

        private void OnSeedInputChanged(string text)
        {
            if (int.TryParse(text, out int seed))
            {
                GameSettings.RandomSeed = seed;
            }
            else
            {
                // Invalid input - reset to current value
                if (seedInputField != null)
                {
                    seedInputField.text = GameSettings.RandomSeed.ToString();
                }
            }
        }

        private void OnRandomizeSeedClicked()
        {
            // Generate a random seed
            int newSeed = Random.Range(1, 10000000);
            GameSettings.RandomSeed = newSeed;

            if (seedInputField != null)
            {
                seedInputField.text = newSeed.ToString();
            }
        }

        /// <summary>
        /// Called by Play button to start the game with current seed settings.
        /// </summary>
        public void StartGame()
        {
            // Save seed settings before starting
            if (seedInputField != null && int.TryParse(seedInputField.text, out int seed))
            {
                GameSettings.RandomSeed = seed;
            }

            if (useFixedSeedToggle != null)
            {
                GameSettings.UseFixedSeed = useFixedSeedToggle.isOn;
            }

            // Check if tutorial mode is enabled - reset tutorial so it will play
            if (tutorialModeToggle != null && tutorialModeToggle.isOn)
            {
                TutorialManager.ResetTutorial();
            }

            // Save all settings before loading scene
            GameSettings.Save();

            // Load the game scene
            LoadGameScene();
        }

        /// <summary>
        /// Called by Tutorial button to start the game with tutorial enabled.
        /// </summary>
        public void StartTutorial()
        {
            // Reset tutorial completion so it will play
            TutorialManager.ResetTutorial();

            // Save seed settings
            if (seedInputField != null && int.TryParse(seedInputField.text, out int seed))
            {
                GameSettings.RandomSeed = seed;
            }

            if (useFixedSeedToggle != null)
            {
                GameSettings.UseFixedSeed = useFixedSeedToggle.isOn;
            }

            // Load the game scene (tutorial will auto-start)
            LoadGameScene();
        }

        private void LoadGameScene()
        {
            if (sceneLoader != null)
            {
                sceneLoader.LoadGameScene();
            }
            else
            {
                // Fallback if no SceneLoader reference
                var loader = FindFirstObjectByType<SceneLoader>();
                if (loader != null)
                {
                    loader.LoadGameScene();
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up listeners
            if (useFixedSeedToggle != null)
            {
                useFixedSeedToggle.onValueChanged.RemoveListener(OnUseFixedSeedChanged);
            }

            if (seedInputField != null)
            {
                seedInputField.onEndEdit.RemoveListener(OnSeedInputChanged);
            }

            if (randomizeSeedButton != null)
            {
                randomizeSeedButton.onClick.RemoveListener(OnRandomizeSeedClicked);
            }

            if (shrineButton != null)
            {
                shrineButton.onClick.RemoveListener(OpenShrine);
            }

            if (tutorialModeToggle != null)
            {
                tutorialModeToggle.onValueChanged.RemoveListener(OnTutorialModeChanged);
            }

            if (MetaProgressionManager.Instance != null)
            {
                MetaProgressionManager.Instance.OnFaeDustChanged -= OnFaeDustChanged;
            }
        }
    }
}

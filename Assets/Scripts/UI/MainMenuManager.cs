using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;

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

        [Header("References")]
        [SerializeField] private SceneLoader sceneLoader;

        private void Start()
        {
            LoadSeedSettings();
            SetupListeners();
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
                // Enable/disable based on toggle state
                seedInputField.interactable = GameSettings.UseFixedSeed;
            }

            if (randomizeSeedButton != null)
            {
                randomizeSeedButton.interactable = GameSettings.UseFixedSeed;
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

            // Load the game scene
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
        }
    }
}

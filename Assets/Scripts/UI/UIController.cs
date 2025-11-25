using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the main game UI including essence display and wave controls.
    /// </summary>
    public class UIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI Elements")]
        [SerializeField]
        [Tooltip("Text element displaying current essence")]
        private TextMeshProUGUI essenceText;

        [SerializeField]
        [Tooltip("Button to start a new wave")]
        private Button startWaveButton;

        [SerializeField]
        [Tooltip("Text element with placement instructions")]
        private TextMeshProUGUI placementInstructionsText;

        [Header("References")]
        [SerializeField]
        [Tooltip("Reference to the wave spawner")]
        private WaveSpawner waveSpawner;

        [Header("Audio")]
        [SerializeField]
        [Tooltip("Slider controlling SFX volume")] 
        private Slider sfxVolumeSlider;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Hook up button click event
            if (startWaveButton != null && waveSpawner != null)
            {
                startWaveButton.onClick.AddListener(OnStartWaveClicked);
            }

            // Initialize essence display
            UpdateEssence(0);

            // Initialize placement instructions
            if (placementInstructionsText != null)
            {
                placementInstructionsText.text = "Click on paths to place Fae Lanterns (Cost: 20 Essence)";
            }

            SetupSfxVolumeSlider();
        }

        private void OnDestroy()
        {
            // Clean up button listener
            if (startWaveButton != null)
            {
                startWaveButton.onClick.RemoveListener(OnStartWaveClicked);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the essence text display.
        /// </summary>
        /// <param name="value">The current essence value to display</param>
        public void UpdateEssence(int value)
        {
            if (essenceText != null)
            {
                essenceText.text = $"Essence: {value}";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Called when the Start Wave button is clicked.
        /// </summary>
        private void OnStartWaveClicked()
        {
            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        private void SetupSfxVolumeSlider()
        {
            if (sfxVolumeSlider == null)
            {
                sfxVolumeSlider = GetComponentInChildren<Slider>(true);
            }

            if (sfxVolumeSlider == null)
            {
                sfxVolumeSlider = CreateSfxVolumeUI();
            }

            if (sfxVolumeSlider == null)
            {
                return;
            }

            float initialVolume = SoundManager.Instance != null ? SoundManager.Instance.SfxVolume : 1f;
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.value = initialVolume;
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        private Slider CreateSfxVolumeUI()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);

                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }

            // Create a simple 1x1 sprite from the white texture to avoid missing built-in resource lookups.
            var whiteTexture = Texture2D.whiteTexture;
            Sprite defaultSprite = Sprite.Create(
                whiteTexture,
                new Rect(0, 0, whiteTexture.width, whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            var sliderObject = new GameObject("SfxVolumeSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvas.transform, false);

            var rectTransform = sliderObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-20f, -20f);
            rectTransform.sizeDelta = new Vector2(200f, 30f);

            var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(sliderObject.transform, false);
            var backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.sprite = defaultSprite;
            backgroundImage.type = Image.Type.Sliced;

            var fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(10f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(fillAreaObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.25f);
            fillRect.anchorMax = new Vector2(1f, 0.75f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillObject.GetComponent<Image>();
            fillImage.sprite = defaultSprite;
            fillImage.type = Image.Type.Sliced;

            var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleSlideArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(handleSlideArea.transform, false);
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(20f, 20f);
            var handleImage = handleObject.GetComponent<Image>();
            handleImage.sprite = defaultSprite;
            handleImage.type = Image.Type.Sliced;

            var slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            var labelObject = new GameObject("SFX Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(1f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(1f, 1f);
            labelRect.anchoredPosition = new Vector2(-20f, -5f);
            labelRect.sizeDelta = new Vector2(200f, 20f);

            var label = labelObject.GetComponent<Text>();
            label.text = "SFX";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.UpperRight;
            label.color = Color.white;

            return slider;
        }

        private void OnSfxVolumeChanged(float value)
        {
            SoundManager.Instance?.SetSfxVolume(value);
        }

        #endregion
    }
}

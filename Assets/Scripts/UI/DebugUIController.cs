using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Audio;

namespace FaeMaze.UI
{
    /// <summary>
    /// Controls the debug panel UI for toggling visualization features,
    /// adjusting timescale, and spawning test visitors.
    /// Automatically creates the UI if not manually set up.
    /// </summary>
    public class DebugUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References (Optional - will auto-create if null)")]
        [SerializeField]
        [Tooltip("Debug panel GameObject (will be shown/hidden with F1)")]
        private GameObject debugPanel;

        [SerializeField]
        [Tooltip("Toggle for grid gizmos visualization")]
        private Toggle gridToggle;

        [SerializeField]
        [Tooltip("Toggle for attraction heatmap visualization")]
        private Toggle heatmapToggle;

        [SerializeField]
        [Tooltip("Slider for adjusting game timescale (0.1 to 2.0)")]
        private Slider timescaleSlider;

        [SerializeField]
        [Tooltip("Button to spawn a test visitor")]
        private Button spawnTestVisitorButton;

        [SerializeField]
        [Tooltip("Slider for adjusting SFX volume (0.0 to 1.0)")]
        private Slider sfxVolumeSlider;

        [Header("System References")]
        [SerializeField]
        [Tooltip("Reference to the maze grid behaviour")]
        private MazeGridBehaviour mazeGridBehaviour;

        [SerializeField]
        [Tooltip("Reference to the wave spawner")]
        private WaveSpawner waveSpawner;

        #endregion

        #region Private Fields

        private TextMeshProUGUI timescaleValueText;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Auto-find system references if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
                if (mazeGridBehaviour != null)
                {
                    Debug.Log("DebugUIController: Found MazeGridBehaviour");
                }
                else
                {
                    Debug.LogWarning("DebugUIController: Could not find MazeGridBehaviour in scene!");
                }
            }

            if (waveSpawner == null)
            {
                waveSpawner = FindFirstObjectByType<WaveSpawner>();
                if (waveSpawner != null)
                {
                    Debug.Log("DebugUIController: Found WaveSpawner");
                }
                else
                {
                    Debug.LogWarning("DebugUIController: Could not find WaveSpawner in scene!");
                }
            }

            // Auto-create debug panel if not assigned
            if (debugPanel == null)
            {
                CreateDebugPanelUI();
            }

            // Initialize UI controls
            InitializeControls();
        }

        private void Update()
        {
            // Toggle debug panel with F1 key using new Input System
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleDebugPanel();
            }
        }

        private void OnDestroy()
        {
            // Clean up listeners
            if (gridToggle != null)
            {
                gridToggle.onValueChanged.RemoveListener(OnGridToggleChanged);
            }

            if (heatmapToggle != null)
            {
                heatmapToggle.onValueChanged.RemoveListener(OnHeatmapToggleChanged);
            }

            if (timescaleSlider != null)
            {
                timescaleSlider.onValueChanged.RemoveListener(OnTimescaleChanged);
            }

            if (spawnTestVisitorButton != null)
            {
                spawnTestVisitorButton.onClick.RemoveListener(OnSpawnTestVisitorClicked);
            }

            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all UI controls with default values and listeners.
        /// </summary>
        private void InitializeControls()
        {
            // Initialize grid toggle
            if (gridToggle != null)
            {
                gridToggle.isOn = true;
                gridToggle.onValueChanged.AddListener(OnGridToggleChanged);
            }

            // Initialize heatmap toggle
            if (heatmapToggle != null)
            {
                heatmapToggle.isOn = true;
                heatmapToggle.onValueChanged.AddListener(OnHeatmapToggleChanged);
            }

            // Initialize timescale slider
            if (timescaleSlider != null)
            {
                timescaleSlider.minValue = 0.1f;
                timescaleSlider.maxValue = 2.0f;
                timescaleSlider.value = 1.0f;
                timescaleSlider.onValueChanged.AddListener(OnTimescaleChanged);
            }

            // Initialize spawn button
            if (spawnTestVisitorButton != null)
            {
                spawnTestVisitorButton.onClick.AddListener(OnSpawnTestVisitorClicked);
            }

            // Initialize SFX volume slider
            if (sfxVolumeSlider != null)
            {
                float initialVolume = SoundManager.Instance != null
                    ? Mathf.Max(SoundManager.Instance.SfxVolume, SoundManager.Instance.MusicVolume)
                    : 1f;
                sfxVolumeSlider.minValue = 0f;
                sfxVolumeSlider.maxValue = 1f;
                sfxVolumeSlider.value = initialVolume;
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            // Debug panel starts visible
            if (debugPanel != null)
            {
                debugPanel.SetActive(true);
            }
        }

        #endregion

        #region UI Creation

        /// <summary>
        /// Automatically creates the debug panel UI hierarchy.
        /// </summary>
        private void CreateDebugPanelUI()
        {
            // Find or create canvas
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            // Create the debug panel
            debugPanel = CreatePanel(canvas.transform);

            // Create title
            CreateTitle(debugPanel.transform);

            // Create controls
            float yPos = -50f;
            gridToggle = CreateToggle(debugPanel.transform, "Grid Gizmos", yPos);
            yPos -= 40f;

            heatmapToggle = CreateToggle(debugPanel.transform, "Attraction Heatmap", yPos);
            yPos -= 30f;

            // Add helper text about gizmos
            CreateSmallLabel(debugPanel.transform, "(Gizmos visible in Scene view only)", yPos);
            yPos -= 35f;

            CreateLabel(debugPanel.transform, "Timescale:", yPos);
            yPos -= 30f;

            timescaleSlider = CreateSlider(debugPanel.transform, yPos);
            yPos -= 10f;

            timescaleValueText = CreateTimescaleValueLabel(debugPanel.transform, yPos);
            yPos -= 50f;

            spawnTestVisitorButton = CreateButton(debugPanel.transform, "Spawn Visitor", yPos);
            yPos -= 50f;

            CreateLabel(debugPanel.transform, "SFX Volume:", yPos);
            yPos -= 30f;

            sfxVolumeSlider = CreateSlider(debugPanel.transform, yPos);
        }

        /// <summary>
        /// Creates a Canvas for the UI.
        /// </summary>
        private Canvas CreateCanvas()
        {
            GameObject canvasObj = new GameObject("DebugCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Creates the main panel background.
        /// </summary>
        private GameObject CreatePanel(Transform parent)
        {
            GameObject panel = new GameObject("DebugPanel");
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.sizeDelta = new Vector2(300f, 400f);

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            return panel;
        }

        /// <summary>
        /// Creates a title text at the top of the panel.
        /// </summary>
        private void CreateTitle(Transform parent)
        {
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(parent, false);

            RectTransform rect = titleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(280f, 30f);

            TextMeshProUGUI text = titleObj.AddComponent<TextMeshProUGUI>();
            text.text = "DEBUG PANEL";
            text.fontSize = 20;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        /// <summary>
        /// Creates a text label.
        /// </summary>
        private void CreateLabel(Transform parent, string labelText, float yPos)
        {
            GameObject labelObj = new GameObject("Label_" + labelText);
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 25f);

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.fontSize = 16;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
        }

        /// <summary>
        /// Creates a small text label (for hints/notes).
        /// </summary>
        private void CreateSmallLabel(Transform parent, string labelText, float yPos)
        {
            GameObject labelObj = new GameObject("SmallLabel_" + labelText);
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 20f);

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.fontSize = 12;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            text.fontStyle = FontStyles.Italic;
        }

        /// <summary>
        /// Creates a toggle control.
        /// </summary>
        private Toggle CreateToggle(Transform parent, string labelText, float yPos)
        {
            GameObject toggleObj = new GameObject("Toggle_" + labelText);
            toggleObj.transform.SetParent(parent, false);

            RectTransform rect = toggleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 30f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();

            // Create background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(toggleObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.5f);
            bgRect.anchorMax = new Vector2(0f, 0.5f);
            bgRect.pivot = new Vector2(0f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(20f, 20f);

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create checkmark
            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(bgObj.transform, false);
            RectTransform checkRect = checkObj.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(2f, 2f);
            checkRect.offsetMax = new Vector2(-2f, -2f);

            Image checkImage = checkObj.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 1f, 0.2f, 1f);

            toggle.graphic = checkImage;
            toggle.targetGraphic = bgImage;

            // Create label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI labelTextComp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTextComp.text = labelText;
            labelTextComp.fontSize = 16;
            labelTextComp.alignment = TextAlignmentOptions.Left;
            labelTextComp.color = Color.white;

            return toggle;
        }

        /// <summary>
        /// Creates a slider control.
        /// </summary>
        private Slider CreateSlider(Transform parent, float yPos)
        {
            GameObject sliderObj = new GameObject("TimescaleSlider");
            sliderObj.transform.SetParent(parent, false);

            RectTransform rect = sliderObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 20f);

            Slider slider = sliderObj.AddComponent<Slider>();

            // Create background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create fill area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(10f, 0f);
            fillAreaRect.offsetMax = new Vector2(-10f, 0f);

            // Create fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.8f, 1f, 1f);

            // Create handle area
            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            // Create handle
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(15f, 0f);

            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = Color.white;

            // Configure slider
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        /// <summary>
        /// Creates a label to display the current timescale value.
        /// </summary>
        private TextMeshProUGUI CreateTimescaleValueLabel(Transform parent, float yPos)
        {
            GameObject labelObj = new GameObject("TimescaleValue");
            labelObj.transform.SetParent(parent, false);

            RectTransform rect = labelObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 25f);

            TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
            text.text = "1.0x";
            text.fontSize = 14;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            return text;
        }

        /// <summary>
        /// Creates a button control.
        /// </summary>
        private Button CreateButton(Transform parent, string buttonText, float yPos)
        {
            GameObject buttonObj = new GameObject("Button_" + buttonText);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yPos);
            rect.sizeDelta = new Vector2(280f, 40f);

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.5f, 0.8f, 1f);

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Create button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = buttonText;
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        #endregion

        #region UI Callbacks

        /// <summary>
        /// Called when the grid toggle value changes.
        /// </summary>
        private void OnGridToggleChanged(bool value)
        {
            Debug.Log($"DebugUIController: Grid Gizmos toggled to {value}");
            if (mazeGridBehaviour != null)
            {
                mazeGridBehaviour.SetDrawGridGizmos(value);
                Debug.Log($"DebugUIController: Set drawGridGizmos to {value} (view in Scene window)");
            }
            else
            {
                Debug.LogWarning("DebugUIController: Cannot toggle Grid Gizmos - MazeGridBehaviour is null!");
            }
        }

        /// <summary>
        /// Called when the heatmap toggle value changes.
        /// </summary>
        private void OnHeatmapToggleChanged(bool value)
        {
            Debug.Log($"DebugUIController: Attraction Heatmap toggled to {value}");
            if (mazeGridBehaviour != null)
            {
                mazeGridBehaviour.SetDrawAttractionHeatmap(value);
                Debug.Log($"DebugUIController: Set drawAttractionHeatmap to {value} (view in Scene window)");
            }
            else
            {
                Debug.LogWarning("DebugUIController: Cannot toggle Heatmap - MazeGridBehaviour is null!");
            }
        }

        /// <summary>
        /// Called when the timescale slider value changes.
        /// </summary>
        private void OnTimescaleChanged(float value)
        {
            Time.timeScale = value;

            // Update the value display
            if (timescaleValueText != null)
            {
                timescaleValueText.text = $"{value:F1}x";
            }
        }

        /// <summary>
        /// Called when the spawn test visitor button is clicked.
        /// </summary>
        private void OnSpawnTestVisitorClicked()
        {
            if (waveSpawner != null)
            {
                waveSpawner.SpawnSingleVisitorForDebug();
            }
            else
            {
                Debug.LogWarning("WaveSpawner reference is not assigned in DebugUIController!");
            }
        }

        /// <summary>
        /// Called when the SFX volume slider value changes.
        /// </summary>
        private void OnSfxVolumeChanged(float value)
        {
            if (SoundManager.Instance == null)
            {
                return;
            }

            SoundManager.Instance.SetSfxVolume(value);
            SoundManager.Instance.SetMusicVolume(value);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Toggles the debug panel visibility.
        /// </summary>
        private void ToggleDebugPanel()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }
        }

        #endregion
    }
}

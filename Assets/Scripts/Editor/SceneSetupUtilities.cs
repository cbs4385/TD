using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Shared utilities for editor scene setup scripts.
    /// Provides common methods for creating cameras, canvases, event systems, and UI elements.
    /// </summary>
    public static class SceneSetupUtilities
    {
        /// <summary>
        /// Creates a camera with standard configuration.
        /// </summary>
        /// <param name="backgroundColor">Background color for the camera</param>
        /// <param name="orthographic">Whether the camera should be orthographic (false by default)</param>
        /// <returns>The created camera GameObject</returns>
        public static GameObject CreateCamera(Color backgroundColor, bool orthographic = false)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.orthographic = orthographic;
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();
            return cameraObj;
        }

        /// <summary>
        /// Creates an EventSystem with InputSystemUIInputModule.
        /// </summary>
        /// <returns>The created EventSystem GameObject</returns>
        public static GameObject CreateEventSystem()
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();
            return eventSystemObj;
        }

        /// <summary>
        /// Creates a Canvas with CanvasScaler and GraphicRaycaster.
        /// </summary>
        /// <param name="referenceResolution">Reference resolution for UI scaling (defaults to 1920x1080)</param>
        /// <returns>The created Canvas GameObject</returns>
        public static GameObject CreateCanvas(Vector2? referenceResolution = null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            return canvasObj;
        }

        /// <summary>
        /// Creates a basic UI GameObject with RectTransform.
        /// </summary>
        /// <param name="name">Name of the GameObject</param>
        /// <param name="parent">Parent transform</param>
        /// <returns>The created GameObject with RectTransform</returns>
        public static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        /// <summary>
        /// Creates a TextMeshProUGUI text element with standard font.
        /// </summary>
        /// <param name="name">Name of the GameObject</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="text">Text content</param>
        /// <param name="fontSize">Font size</param>
        /// <param name="alignment">Text alignment</param>
        /// <param name="color">Text color (defaults to white)</param>
        /// <returns>The created text GameObject</returns>
        public static GameObject CreateTextMeshPro(string name, Transform parent, string text, int fontSize,
            TextAlignmentOptions alignment, Color? color = null)
        {
            GameObject textObj = CreateUIObject(name, parent);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color ?? Color.white;

            // Try to assign built-in font
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tmp.font = TMP_FontAsset.CreateFontAsset(font);
            }

            return textObj;
        }

        /// <summary>
        /// Creates a legacy Text UI element with standard font.
        /// </summary>
        /// <param name="name">Name of the GameObject</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="text">Text content</param>
        /// <param name="fontSize">Font size</param>
        /// <param name="alignment">Text alignment</param>
        /// <param name="color">Text color (defaults to dark gray)</param>
        /// <returns>The created text GameObject</returns>
        public static GameObject CreateLegacyText(string name, Transform parent, string text, int fontSize,
            TextAnchor alignment, Color? color = null)
        {
            GameObject textObj = CreateUIObject(name, parent);
            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = alignment;
            textComponent.color = color ?? new Color(0.196f, 0.196f, 0.196f);

            return textObj;
        }

        /// <summary>
        /// Creates a button with TextMeshPro text.
        /// </summary>
        /// <param name="name">Name of the button GameObject</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="text">Button text</param>
        /// <param name="color">Button background color</param>
        /// <param name="fontSize">Font size for button text (defaults to 24)</param>
        /// <returns>The Button component</returns>
        public static Button CreateButtonWithTextMeshPro(string name, Transform parent, string text, Color color, int fontSize = 24)
        {
            GameObject buttonObj = CreateUIObject(name, parent);
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            GameObject textObj = CreateTextMeshPro("Text", buttonObj.transform, text, fontSize, TextAlignmentOptions.Center);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>
        /// Creates a button with legacy Text component and optional positioning.
        /// </summary>
        /// <param name="name">Name of the button GameObject</param>
        /// <param name="parent">Parent transform</param>
        /// <param name="text">Button text</param>
        /// <param name="color">Button background color</param>
        /// <param name="position">Anchored position (optional)</param>
        /// <param name="size">Size delta (optional)</param>
        /// <param name="fontSize">Font size for button text (defaults to 24)</param>
        /// <returns>The Button component</returns>
        public static Button CreateButtonWithLegacyText(string name, Transform parent, string text, Color color,
            Vector2? position = null, Vector2? size = null, int fontSize = 24)
        {
            GameObject buttonObj = CreateUIObject(name, parent);

            RectTransform rectTransform = buttonObj.GetComponent<RectTransform>();
            if (position.HasValue || size.HasValue)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

                if (position.HasValue)
                    rectTransform.anchoredPosition = position.Value;
                if (size.HasValue)
                    rectTransform.sizeDelta = size.Value;
            }

            Image image = buttonObj.AddComponent<Image>();
            image.color = color;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject textObj = CreateLegacyText("Text", buttonObj.transform, text, fontSize, TextAnchor.MiddleCenter);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            return button;
        }

        /// <summary>
        /// Creates a slider with label and value text using TextMeshPro.
        /// </summary>
        /// <param name="parent">Parent transform</param>
        /// <param name="labelText">Label text</param>
        /// <param name="min">Minimum slider value</param>
        /// <param name="max">Maximum slider value</param>
        /// <param name="defaultValue">Default slider value</param>
        /// <returns>Tuple containing the Slider and its value TextMeshProUGUI</returns>
        public static (Slider slider, TextMeshProUGUI valueText) CreateSliderWithLabel(Transform parent, string labelText,
            float min, float max, float defaultValue)
        {
            GameObject rowObj = CreateUIObject($"{labelText}Row", parent);
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.spacing = 15;

            // Label
            GameObject labelObj = CreateTextMeshPro("Label", rowObj.transform, labelText, 20, TextAlignmentOptions.Left);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(300, 0);

            // Slider
            GameObject sliderObj = CreateUIObject("Slider", rowObj.transform);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(400, 0);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

            // Slider background
            GameObject bgObj = CreateUIObject("Background", sliderObj.transform);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Fill area
            GameObject fillAreaObj = CreateUIObject("Fill Area", sliderObj.transform);
            RectTransform fillAreaRect = fillAreaObj.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(10, 0);
            fillAreaRect.offsetMax = new Vector2(-10, 0);

            GameObject fillObj = CreateUIObject("Fill", fillAreaObj.transform);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);

            // Handle
            GameObject handleAreaObj = CreateUIObject("Handle Slide Area", sliderObj.transform);
            RectTransform handleAreaRect = handleAreaObj.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            GameObject handleObj = CreateUIObject("Handle", handleAreaObj.transform);
            RectTransform handleRect = handleObj.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 30);
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = Color.white;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            // Value text
            GameObject valueObj = CreateTextMeshPro("Value", rowObj.transform, defaultValue.ToString("F1"), 20, TextAlignmentOptions.Right);
            RectTransform valueRect = valueObj.GetComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(100, 0);
            TextMeshProUGUI valueText = valueObj.GetComponent<TextMeshProUGUI>();

            LayoutElement rowElement = rowObj.AddComponent<LayoutElement>();
            rowElement.minHeight = 40;
            rowElement.preferredHeight = 40;

            return (slider, valueText);
        }

        /// <summary>
        /// Creates a toggle with label using TextMeshPro.
        /// </summary>
        /// <param name="parent">Parent transform</param>
        /// <param name="labelText">Label text</param>
        /// <param name="defaultValue">Default toggle state</param>
        /// <returns>The Toggle component</returns>
        public static Toggle CreateToggle(Transform parent, string labelText, bool defaultValue)
        {
            GameObject rowObj = CreateUIObject($"{labelText}Row", parent);
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.spacing = 15;

            // Toggle
            GameObject toggleObj = CreateUIObject("Toggle", rowObj.transform);
            RectTransform toggleRect = toggleObj.GetComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(40, 40);

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultValue;

            // Background
            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            toggle.targetGraphic = toggleBg;

            // Checkmark
            GameObject checkObj = CreateUIObject("Checkmark", toggleObj.transform);
            RectTransform checkRect = checkObj.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.15f, 0.15f);
            checkRect.anchorMax = new Vector2(0.85f, 0.85f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkObj.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            toggle.graphic = checkImage;

            // Label
            GameObject labelObj = CreateTextMeshPro("Label", rowObj.transform, labelText, 20, TextAlignmentOptions.Left);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(600, 0);

            LayoutElement rowElement = rowObj.AddComponent<LayoutElement>();
            rowElement.minHeight = 40;
            rowElement.preferredHeight = 40;

            return toggle;
        }
    }
}

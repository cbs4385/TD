using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add Player Controls section to Gameplay tab and Screenshot section to Video tab.
    /// Also moves Focus Speed (formerly Camera Movement Speed) to Player Controls.
    /// Run from menu: Tools > FaeMaze > Add Player Controls Section
    /// </summary>
    public class AddPlayerControlsSection
    {
        [MenuItem("Tools/FaeMaze/Add Player Controls Section")]
        public static void Execute()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Options")
            {
                EditorUtility.DisplayDialog("Wrong Scene", "Please open the Options scene first.", "OK");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found!");
                return;
            }

            // Find GameplayPanel
            var gameplayPanel = FindChildRecursive(canvas.transform, "GameplayPanel");
            if (gameplayPanel == null)
            {
                Debug.LogError("GameplayPanel not found! Run the main restructure script first.");
                return;
            }

            // Find VideoPanel
            var videoPanel = FindChildRecursive(canvas.transform, "VideoPanel");
            if (videoPanel == null)
            {
                Debug.LogError("VideoPanel not found! Run the main restructure script first.");
                return;
            }

            // Find OptionsManager
            var optionsManager = GameObject.Find("OptionsManager");
            var optionsManagerComponent = optionsManager?.GetComponent<FaeMaze.UI.OptionsManager>();

            Undo.RegisterFullObjectHierarchyUndo(canvas, "Add Player Controls Section");

            // Find and remove existing CameraMovementSpeedRow from Camera Settings
            var cameraMovementSpeedRow = FindChildRecursive(videoPanel.transform, "CameraMovementSpeedRow");
            if (cameraMovementSpeedRow != null)
            {
                Debug.Log("Found CameraMovementSpeedRow, will move it to Player Controls");
            }

            // Check if Player Controls section already exists
            var existingPlayerControls = FindChildRecursive(gameplayPanel.transform, "PLAYERCONTROLSSection");
            if (existingPlayerControls != null)
            {
                Debug.Log("Player Controls section already exists, deleting and recreating...");
                Object.DestroyImmediate(existingPlayerControls.gameObject);
            }

            // Check if Screenshot section already exists
            var existingScreenshot = FindChildRecursive(videoPanel.transform, "SCREENSHOTSection");
            if (existingScreenshot != null)
            {
                Debug.Log("Screenshot section already exists, deleting and recreating...");
                Object.DestroyImmediate(existingScreenshot.gameObject);
            }

            // Create Player Controls section at the end of GameplayPanel
            var playerControlsSection = CreateCollapsibleSection(gameplayPanel.transform, "PLAYER CONTROLS");
            var playerControlsContent = playerControlsSection.transform.Find("Content");

            // Create Focus Speed slider in Player Controls
            var focusSpeedRow = CreateSliderRow(playerControlsContent, "Focus Speed", 0.1f, 10f, 1f, "");

            // Create keybinding dropdowns for heart powers
            var power1Row = CreateDropdownRow(playerControlsContent, "Murmuring Paths Key");
            var power2Row = CreateDropdownRow(playerControlsContent, "Heartward Grasp Key");
            var power3Row = CreateDropdownRow(playerControlsContent, "Devouring Maw Key");
            var power4Row = CreateDropdownRow(playerControlsContent, "Sculpting Key");

            // Delete old CameraMovementSpeedRow from Video panel
            if (cameraMovementSpeedRow != null)
            {
                Object.DestroyImmediate(cameraMovementSpeedRow.gameObject);
                Debug.Log("Removed old CameraMovementSpeedRow from Camera Settings");
            }

            // Create Screenshot section in Video panel
            var screenshotSection = CreateCollapsibleSection(videoPanel.transform, "SCREENSHOT");
            var screenshotContent = screenshotSection.transform.Find("Content");

            // Create screenshot path input
            var screenshotPathRow = CreateInputFieldRow(screenshotContent, "Screenshot Directory");

            // Create screenshot key dropdown
            var screenshotKeyRow = CreateDropdownRow(screenshotContent, "Screenshot Key");

            // Wire up OptionsManager
            if (optionsManagerComponent != null)
            {
                WireUpOptionsManager(optionsManagerComponent,
                    focusSpeedRow, power1Row, power2Row, power3Row, power4Row,
                    screenshotPathRow, screenshotKeyRow);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Player Controls and Screenshot sections added successfully! Save the scene.");
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject CreateCollapsibleSection(Transform parent, string headerText)
        {
            var section = new GameObject(headerText.Replace(" ", "") + "Section");
            section.transform.SetParent(parent, false);

            var sectionRect = section.AddComponent<RectTransform>();
            sectionRect.sizeDelta = new Vector2(0, 0);

            var sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 5;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = false;

            var sectionFitter = section.AddComponent<ContentSizeFitter>();
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sectionLayoutElement = section.AddComponent<LayoutElement>();
            sectionLayoutElement.flexibleWidth = 1;

            // Header
            var header = new GameObject("Header");
            header.transform.SetParent(section.transform, false);

            var headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 50);

            var headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.2f, 0.3f, 0.4f, 1f);

            var headerButton = header.AddComponent<Button>();
            headerButton.targetGraphic = headerImage;

            var headerLayoutElement = header.AddComponent<LayoutElement>();
            headerLayoutElement.minHeight = 50;
            headerLayoutElement.preferredHeight = 50;

            var headerHLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerHLayout.padding = new RectOffset(15, 15, 0, 0);
            headerHLayout.spacing = 10;
            headerHLayout.childForceExpandWidth = false;
            headerHLayout.childForceExpandHeight = true;
            headerHLayout.childControlWidth = false;
            headerHLayout.childControlHeight = true;
            headerHLayout.childAlignment = TextAnchor.MiddleLeft;

            // Arrow
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(header.transform, false);
            var arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(20, 50);
            var arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v";
            arrowText.fontSize = 20;
            arrowText.alignment = TextAlignmentOptions.MidlineLeft;
            arrowText.color = Color.white;
            var arrowLayout = arrowObj.AddComponent<LayoutElement>();
            arrowLayout.preferredWidth = 20;

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(300, 50);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = headerText;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.color = Color.white;
            var titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(section.transform, false);

            var contentRect = content.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, 0);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(20, 20, 10, 10);
            contentLayout.spacing = 10;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add CollapsibleSection component
            var collapsible = section.AddComponent<FaeMaze.UI.CollapsibleSection>();
            var so = new SerializedObject(collapsible);
            SetProperty(so, "headerButton", headerButton);
            SetProperty(so, "contentPanel", content);
            SetProperty(so, "headerText", titleText);
            SetProperty(so, "arrowText", arrowText);
            var startExpandedProp = so.FindProperty("startExpanded");
            if (startExpandedProp != null) startExpandedProp.boolValue = true;
            so.ApplyModifiedProperties();

            return section;
        }

        private static GameObject CreateSliderRow(Transform parent, string label, float min, float max, float defaultVal, string suffix)
        {
            var row = new GameObject(label.Replace(" ", "") + "Row");
            row.transform.SetParent(parent, false);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(300, 40);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            var labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 300;

            // Slider
            var sliderObj = CreateSlider(row.transform, label.Replace(" ", "") + "Slider", min, max, defaultVal);

            // Value text
            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(80, 40);
            var valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = defaultVal.ToString("F1") + suffix;
            valueText.fontSize = 20;
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.color = Color.white;
            var valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 80;

            return row;
        }

        private static GameObject CreateSlider(Transform parent, string name, float min, float max, float defaultVal)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(400, 20);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;

            var sliderLayout = sliderObj.AddComponent<LayoutElement>();
            sliderLayout.preferredWidth = 400;
            sliderLayout.preferredHeight = 20;

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.5f, 0.8f, 1f);
            slider.fillRect = fillRect;

            // Handle Area
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10, 0);
            handleAreaRect.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20, 0);
            handleRect.anchorMin = new Vector2(0, 0);
            handleRect.anchorMax = new Vector2(0, 1);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            return sliderObj;
        }

        private static GameObject CreateDropdownRow(Transform parent, string label)
        {
            var row = new GameObject(label.Replace(" ", "") + "Row");
            row.transform.SetParent(parent, false);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(300, 40);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            var labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 300;

            // Dropdown
            var dropdown = CreateDropdown(row.transform, label.Replace(" ", "") + "Dropdown");

            return row;
        }

        private static GameObject CreateDropdown(Transform parent, string name)
        {
            var dropdown = new GameObject(name);
            dropdown.transform.SetParent(parent, false);

            var rect = dropdown.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 40);

            var image = dropdown.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var dropdownComponent = dropdown.AddComponent<TMP_Dropdown>();
            dropdownComponent.targetGraphic = image;

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdown.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-35, 0);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "1";
            labelText.fontSize = 18;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            dropdownComponent.captionText = labelText;

            // Arrow
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(dropdown.transform, false);
            var arrowRect = arrow.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.pivot = new Vector2(1, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            arrowRect.sizeDelta = new Vector2(20, 20);
            var arrowImage = arrow.AddComponent<Image>();
            arrowImage.color = Color.white;

            // Template
            var template = CreateDropdownTemplate(dropdown.transform, dropdownComponent);
            dropdownComponent.template = template.GetComponent<RectTransform>();

            var dropdownLayout = dropdown.AddComponent<LayoutElement>();
            dropdownLayout.preferredWidth = 200;
            dropdownLayout.preferredHeight = 40;

            return dropdown;
        }

        private static GameObject CreateDropdownTemplate(Transform parent, TMP_Dropdown dropdownComponent)
        {
            var template = new GameObject("Template");
            template.transform.SetParent(parent, false);
            var templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(0, 150);

            var templateImage = template.AddComponent<Image>();
            templateImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var templateScroll = template.AddComponent<ScrollRect>();

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            var viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 30);

            // Item
            var item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            var itemRect = item.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 30);

            var itemToggle = item.AddComponent<Toggle>();

            // Item background
            var itemBg = new GameObject("Item Background");
            itemBg.transform.SetParent(item.transform, false);
            var itemBgRect = itemBg.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.sizeDelta = Vector2.zero;
            var itemBgImage = itemBg.AddComponent<Image>();
            itemBgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Item checkmark
            var itemCheck = new GameObject("Item Checkmark");
            itemCheck.transform.SetParent(item.transform, false);
            var itemCheckRect = itemCheck.AddComponent<RectTransform>();
            itemCheckRect.anchorMin = new Vector2(0, 0.5f);
            itemCheckRect.anchorMax = new Vector2(0, 0.5f);
            itemCheckRect.anchoredPosition = new Vector2(10, 0);
            itemCheckRect.sizeDelta = new Vector2(20, 20);
            var itemCheckImage = itemCheck.AddComponent<Image>();
            itemCheckImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);

            // Item label
            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            var itemLabelRect = itemLabel.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(35, 0);
            itemLabelRect.offsetMax = new Vector2(-10, 0);
            var itemLabelText = itemLabel.AddComponent<TextMeshProUGUI>();
            itemLabelText.text = "Option";
            itemLabelText.fontSize = 18;
            itemLabelText.alignment = TextAlignmentOptions.Left;
            itemLabelText.color = Color.white;

            itemToggle.targetGraphic = itemBgImage;
            itemToggle.graphic = itemCheckImage;

            dropdownComponent.itemText = itemLabelText;

            templateScroll.viewport = viewportRect;
            templateScroll.content = contentRect;

            template.SetActive(false);

            return template;
        }

        private static GameObject CreateInputFieldRow(Transform parent, string label)
        {
            var row = new GameObject(label.Replace(" ", "") + "Row");
            row.transform.SetParent(parent, false);

            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 40);

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(200, 40);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 20;
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.color = Color.white;
            var labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 200;

            // Input Field
            var inputObj = new GameObject(label.Replace(" ", "") + "Input");
            inputObj.transform.SetParent(row.transform, false);
            var inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(400, 40);

            var inputImage = inputObj.AddComponent<Image>();
            inputImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputImage;

            // Text Area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);
            var textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(10, 5);
            textAreaRect.offsetMax = new Vector2(-10, -5);

            // Placeholder
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform, false);
            var placeholderRect = placeholder.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = Vector2.zero;
            var placeholderText = placeholder.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Enter path...";
            placeholderText.fontSize = 16;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            // Text
            var text = new GameObject("Text");
            text.transform.SetParent(textArea.transform, false);
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var textComponent = text.AddComponent<TextMeshProUGUI>();
            textComponent.fontSize = 16;
            textComponent.alignment = TextAlignmentOptions.Left;
            textComponent.color = Color.white;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderText;

            var inputLayout = inputObj.AddComponent<LayoutElement>();
            inputLayout.preferredWidth = 400;
            inputLayout.preferredHeight = 40;
            inputLayout.flexibleWidth = 1;

            return row;
        }

        private static void WireUpOptionsManager(FaeMaze.UI.OptionsManager manager,
            GameObject focusSpeedRow, GameObject power1Row, GameObject power2Row,
            GameObject power3Row, GameObject power4Row,
            GameObject screenshotPathRow, GameObject screenshotKeyRow)
        {
            var so = new SerializedObject(manager);

            // Focus Speed
            var focusSpeedSlider = FindChildRecursive(focusSpeedRow.transform, "FocusSpeedSlider");
            var focusSpeedValue = FindChildRecursive(focusSpeedRow.transform, "Value");
            SetProperty(so, "focusSpeedSlider", focusSpeedSlider?.GetComponent<Slider>());
            SetProperty(so, "focusSpeedText", focusSpeedValue?.GetComponent<TextMeshProUGUI>());

            // Heart Power Keys
            var power1Dropdown = FindChildRecursive(power1Row.transform, "MurmuringPathsKeyDropdown");
            var power2Dropdown = FindChildRecursive(power2Row.transform, "HeartwardGraspKeyDropdown");
            var power3Dropdown = FindChildRecursive(power3Row.transform, "DevouringMawKeyDropdown");
            var power4Dropdown = FindChildRecursive(power4Row.transform, "SculptingKeyDropdown");
            SetProperty(so, "heartPower1KeyDropdown", power1Dropdown?.GetComponent<TMP_Dropdown>());
            SetProperty(so, "heartPower2KeyDropdown", power2Dropdown?.GetComponent<TMP_Dropdown>());
            SetProperty(so, "heartPower3KeyDropdown", power3Dropdown?.GetComponent<TMP_Dropdown>());
            SetProperty(so, "heartPower4KeyDropdown", power4Dropdown?.GetComponent<TMP_Dropdown>());

            // Screenshot
            var screenshotInput = FindChildRecursive(screenshotPathRow.transform, "ScreenshotDirectoryInput");
            var screenshotKeyDropdown = FindChildRecursive(screenshotKeyRow.transform, "ScreenshotKeyDropdown");
            SetProperty(so, "screenshotPathInput", screenshotInput?.GetComponent<TMP_InputField>());
            SetProperty(so, "screenshotKeyDropdown", screenshotKeyDropdown?.GetComponent<TMP_Dropdown>());

            // Clear old cameraMovementSpeedSlider reference (it's been removed/renamed)
            SetProperty(so, "cameraMovementSpeedSlider", null);
            SetProperty(so, "cameraMovementSpeedText", null);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);

            Debug.Log("OptionsManager wired up with new Player Controls and Screenshot settings!");
        }

        private static void SetProperty(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }
    }
}

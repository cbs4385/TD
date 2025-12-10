using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;

namespace FaeMaze.Editor
{
    public class OptionsSceneSetup
    {
        [MenuItem("FaeMaze/Setup Options Scene")]
        public static void SetupOptionsScene()
        {
            // Create or open the Options scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create Camera
            GameObject cameraObj = SceneSetupUtilities.CreateCamera(new Color(0.05f, 0.05f, 0.05f, 1f));

            // Create EventSystem
            GameObject eventSystemObj = SceneSetupUtilities.CreateEventSystem();

            // Create Canvas
            GameObject canvasObj = SceneSetupUtilities.CreateCanvas();

            // Create main panel with ScrollRect
            GameObject mainPanelObj = SceneSetupUtilities.CreateUIObject("MainPanel", canvasObj.transform);
            RectTransform mainRect = mainPanelObj.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.1f, 0.05f);
            mainRect.anchorMax = new Vector2(0.9f, 0.95f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            // Add background
            Image mainBg = mainPanelObj.AddComponent<Image>();
            mainBg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Title
            GameObject titleObj = SceneSetupUtilities.CreateTextMeshPro("Title", mainPanelObj.transform, "OPTIONS", 48, TextAlignmentOptions.Center);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.92f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(20, 0);
            titleRect.offsetMax = new Vector2(-20, -20);

            // ScrollView
            GameObject scrollViewObj = SceneSetupUtilities.CreateUIObject("ScrollView", mainPanelObj.transform);
            RectTransform scrollRect = scrollViewObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 0.12f);
            scrollRect.anchorMax = new Vector2(1, 0.90f);
            scrollRect.offsetMin = new Vector2(20, 0);
            scrollRect.offsetMax = new Vector2(-20, 0);

            ScrollRect scrollComponent = scrollViewObj.AddComponent<ScrollRect>();
            scrollComponent.horizontal = false;
            scrollComponent.vertical = true;
            scrollComponent.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewportObj = SceneSetupUtilities.CreateUIObject("Viewport", scrollViewObj.transform);
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportObj.AddComponent<Mask>().showMaskGraphic = false;
            viewportObj.AddComponent<Image>();

            // Content
            GameObject contentObj = SceneSetupUtilities.CreateUIObject("Content", viewportObj.transform);
            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 2500); // Large height for scrolling
            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 15;
            contentLayout.padding = new RectOffset(20, 20, 20, 20);
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            scrollComponent.content = contentRect;
            scrollComponent.viewport = viewportRect;

            // Create OptionsManager GameObject
            GameObject optionsManagerObj = new GameObject("OptionsManager");
            OptionsManager optionsManager = optionsManagerObj.AddComponent<OptionsManager>();

            // Bottom buttons panel
            GameObject buttonPanelObj = SceneSetupUtilities.CreateUIObject("ButtonPanel", mainPanelObj.transform);
            RectTransform buttonPanelRect = buttonPanelObj.GetComponent<RectTransform>();
            buttonPanelRect.anchorMin = new Vector2(0, 0);
            buttonPanelRect.anchorMax = new Vector2(1, 0.10f);
            buttonPanelRect.offsetMin = new Vector2(20, 20);
            buttonPanelRect.offsetMax = new Vector2(-20, 0);
            HorizontalLayoutGroup buttonLayout = buttonPanelObj.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 20;
            buttonLayout.childControlHeight = true;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandHeight = true;
            buttonLayout.childForceExpandWidth = true;

            Button applyButton = SceneSetupUtilities.CreateButtonWithTextMeshPro("ApplyButton", buttonPanelObj.transform, "Apply Changes", new Color(0.2f, 0.7f, 0.2f));
            Button resetButton = SceneSetupUtilities.CreateButtonWithTextMeshPro("ResetButton", buttonPanelObj.transform, "Reset to Defaults", new Color(0.7f, 0.3f, 0.2f));
            Button backButton = SceneSetupUtilities.CreateButtonWithTextMeshPro("BackButton", buttonPanelObj.transform, "Back to Menu", new Color(0.3f, 0.3f, 0.7f));

            // Build settings sections
            // Audio Section
            var (audioSection, audioContent) = CreateCollapsibleSection("AudioSection", contentObj.transform, "AUDIO SETTINGS");
            var (sfxVolSlider, sfxVolText) = SceneSetupUtilities.CreateSliderWithLabel(audioContent, "SFX Volume", 0f, 1f, 1f);
            var (musicVolSlider, musicVolText) = SceneSetupUtilities.CreateSliderWithLabel(audioContent, "Music Volume", 0f, 1f, 1f);

            // Camera Section
            var (cameraSection, cameraContent) = CreateCollapsibleSection("CameraSection", contentObj.transform, "CAMERA SETTINGS");
            var (panSpeedSlider, panSpeedText) = SceneSetupUtilities.CreateSliderWithLabel(cameraContent, "Pan Speed", 1f, 30f, 10f);
            var (zoomSpeedSlider, zoomSpeedText) = SceneSetupUtilities.CreateSliderWithLabel(cameraContent, "Zoom Speed", 1f, 20f, 5f);
            var (minZoomSlider, minZoomText) = SceneSetupUtilities.CreateSliderWithLabel(cameraContent, "Min Zoom", 1f, 10f, 3f);
            var (maxZoomSlider, maxZoomText) = SceneSetupUtilities.CreateSliderWithLabel(cameraContent, "Max Zoom", 10f, 50f, 20f);

            // Visitor Gameplay Section
            var (visitorSection, visitorContent) = CreateCollapsibleSection("VisitorSection", contentObj.transform, "VISITOR GAMEPLAY");
            var (visSpeedSlider, visSpeedText) = SceneSetupUtilities.CreateSliderWithLabel(visitorContent, "Visitor Speed", 0.5f, 10f, 3f);
            Toggle confusionToggle = SceneSetupUtilities.CreateToggle(visitorContent, "Confusion Enabled", true);
            var (confChanceSlider, confChanceText) = SceneSetupUtilities.CreateSliderWithLabel(visitorContent, "Confusion Chance", 0f, 1f, 0.25f);
            var (confMinSlider, confMinText) = SceneSetupUtilities.CreateSliderWithLabel(visitorContent, "Confusion Distance Min", 1f, 50f, 15f);
            var (confMaxSlider, confMaxText) = SceneSetupUtilities.CreateSliderWithLabel(visitorContent, "Confusion Distance Max", 1f, 50f, 20f);

            // Wave/Difficulty Section
            var (waveSection, waveContent) = CreateCollapsibleSection("WaveSection", contentObj.transform, "WAVE & DIFFICULTY");
            var (visPerWaveSlider, visPerWaveText) = SceneSetupUtilities.CreateSliderWithLabel(waveContent, "Visitors Per Wave", 1f, 50f, 10f);
            var (spawnIntSlider, spawnIntText) = SceneSetupUtilities.CreateSliderWithLabel(waveContent, "Spawn Interval (s)", 0.1f, 5f, 1f);
            var (waveDurSlider, waveDurText) = SceneSetupUtilities.CreateSliderWithLabel(waveContent, "Wave Duration (s)", 10f, 300f, 60f);
            Toggle redCapToggle = SceneSetupUtilities.CreateToggle(waveContent, "Enable Red Cap", true);
            var (redCapDelaySlider, redCapDelayText) = SceneSetupUtilities.CreateSliderWithLabel(waveContent, "Red Cap Spawn Delay (s)", 0f, 120f, 60f);

            // Game Flow Section
            var (flowSection, flowContent) = CreateCollapsibleSection("FlowSection", contentObj.transform, "GAME FLOW");
            Toggle autoStartToggle = SceneSetupUtilities.CreateToggle(flowContent, "Auto-Start Next Wave", false);
            var (autoDelaySlider, autoDelayText) = SceneSetupUtilities.CreateSliderWithLabel(flowContent, "Auto-Start Delay (s)", 0f, 10f, 2f);
            var (essenceSlider, essenceText) = SceneSetupUtilities.CreateSliderWithLabel(flowContent, "Starting Essence", 0f, 1000f, 100f);

            // Wire up OptionsManager references using SerializedObject
            SerializedObject optionsManagerSO = new SerializedObject(optionsManager);

            // Audio
            optionsManagerSO.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxVolSlider;
            optionsManagerSO.FindProperty("sfxVolumeText").objectReferenceValue = sfxVolText;
            optionsManagerSO.FindProperty("musicVolumeSlider").objectReferenceValue = musicVolSlider;
            optionsManagerSO.FindProperty("musicVolumeText").objectReferenceValue = musicVolText;

            // Camera
            optionsManagerSO.FindProperty("cameraPanSpeedSlider").objectReferenceValue = panSpeedSlider;
            optionsManagerSO.FindProperty("cameraPanSpeedText").objectReferenceValue = panSpeedText;
            optionsManagerSO.FindProperty("cameraZoomSpeedSlider").objectReferenceValue = zoomSpeedSlider;
            optionsManagerSO.FindProperty("cameraZoomSpeedText").objectReferenceValue = zoomSpeedText;
            optionsManagerSO.FindProperty("cameraMinZoomSlider").objectReferenceValue = minZoomSlider;
            optionsManagerSO.FindProperty("cameraMinZoomText").objectReferenceValue = minZoomText;
            optionsManagerSO.FindProperty("cameraMaxZoomSlider").objectReferenceValue = maxZoomSlider;
            optionsManagerSO.FindProperty("cameraMaxZoomText").objectReferenceValue = maxZoomText;

            // Visitor
            optionsManagerSO.FindProperty("visitorSpeedSlider").objectReferenceValue = visSpeedSlider;
            optionsManagerSO.FindProperty("visitorSpeedText").objectReferenceValue = visSpeedText;
            optionsManagerSO.FindProperty("confusionEnabledToggle").objectReferenceValue = confusionToggle;
            optionsManagerSO.FindProperty("confusionChanceSlider").objectReferenceValue = confChanceSlider;
            optionsManagerSO.FindProperty("confusionChanceText").objectReferenceValue = confChanceText;
            optionsManagerSO.FindProperty("confusionDistanceMinSlider").objectReferenceValue = confMinSlider;
            optionsManagerSO.FindProperty("confusionDistanceMinText").objectReferenceValue = confMinText;
            optionsManagerSO.FindProperty("confusionDistanceMaxSlider").objectReferenceValue = confMaxSlider;
            optionsManagerSO.FindProperty("confusionDistanceMaxText").objectReferenceValue = confMaxText;

            // Wave
            optionsManagerSO.FindProperty("visitorsPerWaveSlider").objectReferenceValue = visPerWaveSlider;
            optionsManagerSO.FindProperty("visitorsPerWaveText").objectReferenceValue = visPerWaveText;
            optionsManagerSO.FindProperty("spawnIntervalSlider").objectReferenceValue = spawnIntSlider;
            optionsManagerSO.FindProperty("spawnIntervalText").objectReferenceValue = spawnIntText;
            optionsManagerSO.FindProperty("waveDurationSlider").objectReferenceValue = waveDurSlider;
            optionsManagerSO.FindProperty("waveDurationText").objectReferenceValue = waveDurText;
            optionsManagerSO.FindProperty("enableRedCapToggle").objectReferenceValue = redCapToggle;
            optionsManagerSO.FindProperty("redCapSpawnDelaySlider").objectReferenceValue = redCapDelaySlider;
            optionsManagerSO.FindProperty("redCapSpawnDelayText").objectReferenceValue = redCapDelayText;

            // Flow
            optionsManagerSO.FindProperty("autoStartNextWaveToggle").objectReferenceValue = autoStartToggle;
            optionsManagerSO.FindProperty("autoStartDelaySlider").objectReferenceValue = autoDelaySlider;
            optionsManagerSO.FindProperty("autoStartDelayText").objectReferenceValue = autoDelayText;
            optionsManagerSO.FindProperty("startingEssenceSlider").objectReferenceValue = essenceSlider;
            optionsManagerSO.FindProperty("startingEssenceText").objectReferenceValue = essenceText;

            // Buttons
            optionsManagerSO.FindProperty("applyButton").objectReferenceValue = applyButton;
            optionsManagerSO.FindProperty("resetButton").objectReferenceValue = resetButton;
            optionsManagerSO.FindProperty("backButton").objectReferenceValue = backButton;

            optionsManagerSO.ApplyModifiedProperties();

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Options.unity");
        }

        /// <summary>
        /// Creates a collapsible section with header and content panel.
        /// This is specific to the Options scene UI structure.
        /// </summary>
        private static (GameObject section, Transform content) CreateCollapsibleSection(string name, Transform parent, string title)
        {
            GameObject sectionObj = SceneSetupUtilities.CreateUIObject(name, parent);
            RectTransform sectionRect = sectionObj.GetComponent<RectTransform>();
            sectionRect.sizeDelta = new Vector2(0, 0); // Will be sized by layout

            VerticalLayoutGroup sectionLayout = sectionObj.AddComponent<VerticalLayoutGroup>();
            sectionLayout.childControlHeight = false;
            sectionLayout.childControlWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.spacing = 5;

            ContentSizeFitter sectionFitter = sectionObj.AddComponent<ContentSizeFitter>();
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header button
            GameObject headerObj = SceneSetupUtilities.CreateUIObject("Header", sectionObj.transform);
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 50);
            Image headerBg = headerObj.AddComponent<Image>();
            headerBg.color = new Color(0.2f, 0.3f, 0.4f, 1f);
            Button headerButton = headerObj.AddComponent<Button>();

            HorizontalLayoutGroup headerLayout = headerObj.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(15, 15, 10, 10);
            headerLayout.childControlHeight = false;
            headerLayout.childControlWidth = false;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childForceExpandWidth = false;

            GameObject arrowObj = SceneSetupUtilities.CreateTextMeshPro("Arrow", headerObj.transform, "▼", 30, TextAlignmentOptions.Left);
            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(40, 40);

            GameObject titleObj = SceneSetupUtilities.CreateTextMeshPro("Title", headerObj.transform, title, 28, TextAlignmentOptions.Left);

            // Content panel
            GameObject contentObj = SceneSetupUtilities.CreateUIObject("Content", sectionObj.transform);
            Image contentBg = contentObj.AddComponent<Image>();
            contentBg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(20, 20, 15, 15);
            contentLayout.spacing = 12;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add CollapsibleSection component
            CollapsibleSection collapsible = sectionObj.AddComponent<CollapsibleSection>();
            SerializedObject collapsibleSO = new SerializedObject(collapsible);
            collapsibleSO.FindProperty("headerButton").objectReferenceValue = headerButton;
            collapsibleSO.FindProperty("contentPanel").objectReferenceValue = contentObj;
            collapsibleSO.FindProperty("headerText").objectReferenceValue = titleObj.GetComponent<TextMeshProUGUI>();
            collapsibleSO.FindProperty("arrowText").objectReferenceValue = arrowObj.GetComponent<TextMeshProUGUI>();
            collapsibleSO.FindProperty("startExpanded").boolValue = true;
            collapsibleSO.ApplyModifiedProperties();

            return (sectionObj, contentObj.transform);
        }
    }
}

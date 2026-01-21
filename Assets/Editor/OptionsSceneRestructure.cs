using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.Collections.Generic;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to restructure the Options scene with a tabbed interface.
    /// Run from menu: Tools > FaeMaze > Restructure Options Scene
    ///
    /// Creates three tabs: Gameplay (first), Video, Audio
    /// - Gameplay: VisitorSection, VisitorTypeSection, WaveSection, FlowSection
    /// - Video: Fullscreen, Resolution, FOV, CameraSection (minus DoF)
    /// - Audio: AudioSection (SFX and Music volume)
    /// </summary>
    public class OptionsSceneRestructure
    {
        // Colors for tab buttons
        private static readonly Color ActiveTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color InactiveTabColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color PanelBgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

        [MenuItem("Tools/FaeMaze/Restructure Options Scene")]
        public static void RestructureOptionsScene()
        {
            // Make sure we're in the Options scene
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.name != "Options")
            {
                EditorUtility.DisplayDialog("Wrong Scene",
                    "Please open the Options scene first, then run this tool.",
                    "OK");
                return;
            }

            // Warn user about needing a clean scene
            Debug.Log("=== Options Scene Restructure ===");
            Debug.Log("NOTE: For best results, restore Options.unity from git before running this script.");
            Debug.Log("If AudioSection or CameraSection are missing, their content won't be moved.");

            // Find required objects
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("Canvas not found in scene!");
                return;
            }

            var mainPanel = FindChildByName(canvas.transform, "MainPanel");
            if (mainPanel == null)
            {
                Debug.LogError("MainPanel not found in Canvas!");
                return;
            }

            var optionsManager = GameObject.Find("OptionsManager");
            if (optionsManager == null)
            {
                Debug.LogError("OptionsManager not found in scene!");
                return;
            }

            var optionsManagerComponent = optionsManager.GetComponent<FaeMaze.UI.OptionsManager>();
            if (optionsManagerComponent == null)
            {
                Debug.LogError("OptionsManager component not found!");
                return;
            }

            // Find existing ScrollView
            var scrollView = FindChildByName(mainPanel, "ScrollView");
            if (scrollView == null)
            {
                Debug.LogError("ScrollView not found in MainPanel!");
                return;
            }

            // Find existing Viewport and Content
            var viewport = FindChildByName(scrollView, "Viewport");
            if (viewport == null)
            {
                Debug.LogError("Viewport not found in ScrollView!");
                return;
            }

            // Use FindDirectChild to only look at viewport's direct children, not nested Content panels
            var existingContent = FindDirectChild(viewport, "Content");
            if (existingContent == null)
            {
                existingContent = FindDirectChild(viewport, "GameplayPanel");
            }
            if (existingContent == null)
            {
                Debug.LogError("Content/GameplayPanel not found as direct child of Viewport!");
                return;
            }

            // Find existing sections in Content
            var audioSection = FindChildByName(existingContent, "AudioSection");
            var cameraSection = FindChildByName(existingContent, "CameraSection");
            var visitorSection = FindChildByName(existingContent, "VisitorSection");
            var visitorTypeSection = FindChildByName(existingContent, "VisitorTypeSection");
            var waveSection = FindChildByName(existingContent, "WaveSection");
            var flowSection = FindChildByName(existingContent, "FlowSection");

            Debug.Log($"Found sections - Audio: {audioSection != null}, Camera: {cameraSection != null}, Visitor: {visitorSection != null}, VisitorType: {visitorTypeSection != null}, Wave: {waveSection != null}, Flow: {flowSection != null}");

            Undo.RegisterFullObjectHierarchyUndo(canvas, "Restructure Options Scene");

            // Clean up any existing TabBars (from previous runs)
            CleanupExistingTabBars(mainPanel);

            // Clean up duplicate panels in Viewport (from previous runs)
            CleanupDuplicatePanels(viewport);

            // Re-find Content after cleanup (it may have been renamed to GameplayPanel)
            existingContent = FindDirectChild(viewport, "Content");
            if (existingContent == null)
            {
                existingContent = FindDirectChild(viewport, "GameplayPanel");
            }
            if (existingContent == null)
            {
                Debug.LogError("Content/GameplayPanel not found in Viewport after cleanup!");
                return;
            }

            // Re-find sections after cleanup
            audioSection = FindChildByName(existingContent, "AudioSection");
            cameraSection = FindChildByName(existingContent, "CameraSection");
            visitorSection = FindChildByName(existingContent, "VisitorSection");
            visitorTypeSection = FindChildByName(existingContent, "VisitorTypeSection");
            waveSection = FindChildByName(existingContent, "WaveSection");
            flowSection = FindChildByName(existingContent, "FlowSection");

            // 1. Create TabBar as child of MainPanel, between Title and ScrollView
            var title = FindChildByName(mainPanel, "Title");
            var tabBar = CreateTabBar(mainPanel);

            // Position TabBar after Title
            if (title != null)
            {
                tabBar.transform.SetSiblingIndex(title.GetSiblingIndex() + 1);
            }

            // 2. Adjust ScrollView to make room for TabBar and bottom buttons
            var scrollViewRect = scrollView.GetComponent<RectTransform>();
            // Position below TabBar (which ends at 85% from bottom, i.e. 15% from top)
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 0.85f); // Top at 85% (below TabBar)
            scrollViewRect.offsetMin = new Vector2(0, 120); // Bottom padding for buttons (Apply/Reset/Back) - needs ~120px clearance
            scrollViewRect.offsetMax = new Vector2(0, -10); // Small gap below tab bar

            // 3. Rename existing Content to GameplayPanel
            existingContent.name = "GameplayPanel";
            var gameplayPanel = existingContent.gameObject;

            // 4. Create VideoPanel and AudioPanel as siblings to GameplayPanel
            var videoPanel = CreateContentPanel(viewport, "VideoPanel");
            var audioPanel = CreateContentPanel(viewport, "AudioPanel");

            // Set sibling order: GameplayPanel first (matches tab order)
            gameplayPanel.transform.SetSiblingIndex(0);
            videoPanel.transform.SetSiblingIndex(1);
            audioPanel.transform.SetSiblingIndex(2);

            // 5. Delete DoF rows from CameraSection before moving
            if (cameraSection != null)
            {
                DeleteChildrenContaining(cameraSection, "Depth of Field");
                DeleteChildrenContaining(cameraSection, "Enable Depth");
            }

            // 6. Create VIDEO sections with collapsible headers
            // Display Settings section
            var displaySection = CreateCollapsibleSection(videoPanel.transform, "DISPLAY SETTINGS");
            var displayContent = displaySection.transform.Find("Content");
            if (displayContent == null)
            {
                Debug.LogError("Failed to find Content in DISPLAY SETTINGS section!");
            }
            else
            {
                Debug.Log($"Creating display rows in {displayContent.name} (parent: {displayContent.parent?.name})");
            }
            var fullscreenRow = CreateToggleRow(displayContent, "Fullscreen");
            var resolutionRow = CreateDropdownRow(displayContent, "Resolution");
            var fovRow = CreateSliderRow(displayContent, "Field of View", 30f, 120f, 60f, "°");
            Debug.Log($"Display content now has {displayContent?.childCount ?? 0} children");

            // Camera Settings section
            var cameraSectionNew = CreateCollapsibleSection(videoPanel.transform, "CAMERA SETTINGS");
            var cameraContent = cameraSectionNew.transform.Find("Content");

            // 7. Move CameraSection content to Camera Settings section, or create if missing
            if (cameraSection != null)
            {
                Debug.Log($"Found CameraSection with {cameraSection.childCount} children");
                // Move all rows from CameraSection's Content to new camera section content
                MoveCollapsibleSectionContent(cameraSection, cameraContent);
                Debug.Log($"After move, cameraContent has {cameraContent?.childCount ?? 0} children");
                // Delete empty CameraSection
                Object.DestroyImmediate(cameraSection.gameObject);
            }
            else
            {
                Debug.LogWarning("CameraSection not found - creating camera settings from scratch");
                // Create camera settings rows since original section is missing
                CreateSliderRow(cameraContent, "Camera Min Zoom", 5f, 50f, 10f, "");
                CreateSliderRow(cameraContent, "Camera Max Zoom", 10f, 100f, 50f, "");
                CreateSliderRow(cameraContent, "Camera Movement Speed", 1f, 20f, 10f, "");
            }

            // 8. Create AUDIO sections with collapsible headers
            // Master Volume section
            var masterVolumeSection = CreateCollapsibleSection(audioPanel.transform, "MASTER VOLUME");
            var masterVolumeContent = masterVolumeSection.transform.Find("Content");

            // Move AudioSection content (SFX and Music sliders) to Master Volume section, or create if missing
            GameObject sfxVolumeRow = null;
            GameObject musicVolumeRow = null;
            if (audioSection != null)
            {
                Debug.Log($"Found AudioSection with {audioSection.childCount} children");
                MoveCollapsibleSectionContent(audioSection, masterVolumeContent);
                Debug.Log($"After move, masterVolumeContent has {masterVolumeContent?.childCount ?? 0} children");
                Object.DestroyImmediate(audioSection.gameObject);
            }
            else
            {
                Debug.LogWarning("AudioSection not found - creating master volume sliders from scratch");
                // Create SFX and Music volume sliders since original section is missing
                sfxVolumeRow = CreateSliderRow(masterVolumeContent, "SFX Volume", 0f, 1f, 1f, "%");
                musicVolumeRow = CreateSliderRow(masterVolumeContent, "Music Volume", 0f, 1f, 1f, "%");
            }

            // Prop Sounds section (nested under SFX conceptually)
            var propSoundsSection = CreateCollapsibleSection(audioPanel.transform, "PROP SOUNDS");
            var propSoundsContent = propSoundsSection.transform.Find("Content");

            // Add individual sound volume sliders to Prop Sounds section
            var lanternVolumeRow = CreateSliderRow(propSoundsContent, "Lantern Volume", 0f, 1f, 1f, "%");
            var fairyRingVolumeRow = CreateSliderRow(propSoundsContent, "Fairy Ring Volume", 0f, 1f, 1f, "%");
            var pondVolumeRow = CreateSliderRow(propSoundsContent, "Pond Volume", 0f, 1f, 1f, "%");
            var sculptVolumeRow = CreateSliderRow(propSoundsContent, "Sculpt Volume", 0f, 1f, 1f, "%");

            // 9. Keep Visitor, VisitorType, Wave, Flow sections in GameplayPanel (already there)
            Debug.Log("Gameplay sections remain in GameplayPanel");

            // 10. Wire up the ScrollRect to use GameplayPanel as content by default
            var scrollRect = scrollView.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.content = gameplayPanel.GetComponent<RectTransform>();
            }

            // 11. Set initial state - show Gameplay panel, hide others
            gameplayPanel.SetActive(true);
            videoPanel.SetActive(false);
            audioPanel.SetActive(false);

            // 12. Wire up OptionsManager
            WireUpOptionsManager(optionsManagerComponent, tabBar,
                gameplayPanel, videoPanel, audioPanel,
                fullscreenRow, resolutionRow, fovRow,
                lanternVolumeRow, fairyRingVolumeRow, pondVolumeRow, sculptVolumeRow);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Options scene restructured successfully! Save the scene to preserve changes.");
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var found = FindChildByName(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Finds a direct child by name (not recursive)
        /// </summary>
        private static Transform FindDirectChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }

        /// <summary>
        /// Removes any existing TabBar objects from MainPanel (from previous script runs)
        /// </summary>
        private static void CleanupExistingTabBars(Transform mainPanel)
        {
            var toDestroy = new List<GameObject>();
            foreach (Transform child in mainPanel)
            {
                if (child.name == "TabBar")
                {
                    toDestroy.Add(child.gameObject);
                }
            }
            foreach (var obj in toDestroy)
            {
                Debug.Log($"Removing duplicate TabBar: {obj.name}");
                Object.DestroyImmediate(obj);
            }
        }

        /// <summary>
        /// Removes duplicate VideoPanel and AudioPanel objects from Viewport (from previous script runs).
        /// Keeps the original Content/GameplayPanel and any sections within it.
        /// </summary>
        private static void CleanupDuplicatePanels(Transform viewport)
        {
            var toDestroy = new List<GameObject>();
            bool foundContent = false;
            bool foundGameplayPanel = false;

            foreach (Transform child in viewport)
            {
                // Keep the first Content or GameplayPanel (they're the same thing, renamed)
                if (child.name == "Content" && !foundContent && !foundGameplayPanel)
                {
                    foundContent = true;
                    continue;
                }
                if (child.name == "GameplayPanel" && !foundContent && !foundGameplayPanel)
                {
                    foundGameplayPanel = true;
                    continue;
                }

                // Remove duplicate Content/GameplayPanel
                if (child.name == "Content" || child.name == "GameplayPanel")
                {
                    toDestroy.Add(child.gameObject);
                    continue;
                }

                // Remove any VideoPanel or AudioPanel (they get recreated each run)
                if (child.name == "VideoPanel" || child.name == "AudioPanel")
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            foreach (var obj in toDestroy)
            {
                Debug.Log($"Removing duplicate panel: {obj.name}");
                Object.DestroyImmediate(obj);
            }
        }

        private static void DeleteChildrenContaining(Transform parent, string nameContains)
        {
            var toDelete = new List<Transform>();
            foreach (Transform child in parent)
            {
                if (child.name.ToLower().Contains(nameContains.ToLower()))
                {
                    toDelete.Add(child);
                }
            }
            foreach (var child in toDelete)
            {
                Debug.Log($"Deleting: {child.name}");
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void MoveAllChildren(Transform source, Transform destination)
        {
            // Get all children first to avoid modifying collection while iterating
            var children = new List<Transform>();
            foreach (Transform child in source)
            {
                // Skip headers/titles - they're section-specific
                if (child.name == "Header" || child.name == "Title")
                    continue;
                children.Add(child);
            }

            foreach (var child in children)
            {
                Debug.Log($"Moving {child.name} to {destination.name}");
                child.SetParent(destination, false);
            }
        }

        /// <summary>
        /// Moves content from an existing CollapsibleSection's Content child to a new destination.
        /// This handles the structure where sections have Header and Content children.
        /// </summary>
        private static void MoveCollapsibleSectionContent(Transform existingSection, Transform destinationContent)
        {
            // Find the Content child of the existing section
            var sourceContent = FindDirectChild(existingSection, "Content");
            if (sourceContent == null)
            {
                Debug.LogWarning($"No Content child found in {existingSection.name}");
                return;
            }

            // Move all children from the source Content to the destination Content
            var children = new List<Transform>();
            foreach (Transform child in sourceContent)
            {
                children.Add(child);
            }

            foreach (var child in children)
            {
                Debug.Log($"Moving {child.name} from {existingSection.name}/Content to {destinationContent.name}");
                child.SetParent(destinationContent, false);
            }
        }

        private static GameObject CreateTabBar(Transform parent)
        {
            var tabBar = new GameObject("TabBar");
            tabBar.transform.SetParent(parent, false);

            var rect = tabBar.AddComponent<RectTransform>();
            // Position below title with 5% screen height margin
            // Using anchors: title ends at ~5% from top, then 5% margin, then tab bar
            rect.anchorMin = new Vector2(0, 0.85f); // 15% from top (5% title + 5% margin + 5% tab height)
            rect.anchorMax = new Vector2(1, 0.90f); // 10% from top (5% title + 5% margin)
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(20, 0); // Left padding
            rect.offsetMax = new Vector2(-20, 0); // Right padding

            var layout = tabBar.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2;
            layout.padding = new RectOffset(20, 20, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // Create tab buttons - Gameplay first, then Video, then Audio
            CreateTabButton(tabBar.transform, "GameplayTabButton", "GAMEPLAY", true);
            CreateTabButton(tabBar.transform, "VideoTabButton", "VIDEO", false);
            CreateTabButton(tabBar.transform, "AudioTabButton", "AUDIO", false);

            return tabBar;
        }

        private static GameObject CreateTabButton(Transform parent, string name, string label, bool isActive)
        {
            var button = new GameObject(name);
            button.transform.SetParent(parent, false);

            var rect = button.AddComponent<RectTransform>();

            var image = button.AddComponent<Image>();
            image.color = isActive ? ActiveTabColor : InactiveTabColor;

            var btn = button.AddComponent<Button>();
            btn.targetGraphic = image;

            // Add text child
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(button.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        private static GameObject CreateContentPanel(Transform parent, string name)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 15;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return panel;
        }

        /// <summary>
        /// Creates a collapsible section with header button and content panel.
        /// Matches the structure expected by CollapsibleSection component.
        /// </summary>
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

            // Create Header button
            var header = new GameObject("Header");
            header.transform.SetParent(section.transform, false);

            var headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 50); // Taller header to match Gameplay sections

            var headerImage = header.AddComponent<Image>();
            headerImage.color = new Color(0.2f, 0.3f, 0.4f, 1f); // Blue/teal to match existing sections (FlowSection, etc.)

            var headerButton = header.AddComponent<Button>();
            headerButton.targetGraphic = headerImage;

            var headerLayoutElement = header.AddComponent<LayoutElement>();
            headerLayoutElement.minHeight = 50;
            headerLayoutElement.preferredHeight = 50;

            var headerHorizontalLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerHorizontalLayout.padding = new RectOffset(15, 15, 0, 0);
            headerHorizontalLayout.spacing = 10;
            headerHorizontalLayout.childForceExpandWidth = false;
            headerHorizontalLayout.childForceExpandHeight = true;
            headerHorizontalLayout.childControlWidth = false;
            headerHorizontalLayout.childControlHeight = true;
            headerHorizontalLayout.childAlignment = TextAnchor.MiddleLeft;

            // Arrow text
            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(header.transform, false);

            var arrowRect = arrowObj.AddComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(20, 50);

            var arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowText.text = "v"; // Expanded by default (v = down arrow indicates expanded)
            arrowText.fontSize = 20;
            arrowText.alignment = TextAlignmentOptions.MidlineLeft;
            arrowText.color = Color.white;

            var arrowLayout = arrowObj.AddComponent<LayoutElement>();
            arrowLayout.preferredWidth = 20;

            // Header title text
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(header.transform, false);

            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(300, 50);

            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = headerText;
            titleText.fontSize = 28; // Match Gameplay section headers (28px)
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.color = Color.white;

            var titleLayout = titleObj.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;

            // Content panel
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

            // Add CollapsibleSection component and wire up references
            var collapsible = section.AddComponent<FaeMaze.UI.CollapsibleSection>();
            var so = new SerializedObject(collapsible);
            SetSerializedProperty(so, "headerButton", headerButton);
            SetSerializedProperty(so, "contentPanel", content);
            SetSerializedProperty(so, "headerText", titleText);
            SetSerializedProperty(so, "arrowText", arrowText);
            // Ensure startExpanded is true so sections start open
            var startExpandedProp = so.FindProperty("startExpanded");
            if (startExpandedProp != null)
            {
                startExpandedProp.boolValue = true;
            }
            so.ApplyModifiedProperties();

            // Also ensure content is active in the editor
            content.SetActive(true);

            return section;
        }

        private static GameObject CreateToggleRow(Transform parent, string label)
        {
            if (parent == null)
            {
                Debug.LogError($"CreateToggleRow: parent is null for label '{label}'");
                return null;
            }
            var row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            Debug.Log($"Created toggle row '{label}' under {parent.name}");

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

            // Toggle
            var toggleObj = CreateToggle(row.transform, label + "Toggle");

            return row;
        }

        private static GameObject CreateToggle(Transform parent, string name)
        {
            var toggle = new GameObject(name);
            toggle.transform.SetParent(parent, false);

            var rect = toggle.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(30, 30);

            // Background
            var bg = new GameObject("Background");
            bg.transform.SetParent(toggle.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Checkmark
            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(bg.transform, false);
            var checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkRect.sizeDelta = Vector2.zero;
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.8f, 0.2f, 1f);

            var toggleComponent = toggle.AddComponent<Toggle>();
            toggleComponent.targetGraphic = bgImage;
            toggleComponent.graphic = checkImage;
            toggleComponent.isOn = true;

            var toggleLayout = toggle.AddComponent<LayoutElement>();
            toggleLayout.preferredWidth = 30;
            toggleLayout.preferredHeight = 30;

            return toggle;
        }

        private static GameObject CreateDropdownRow(Transform parent, string label)
        {
            if (parent == null)
            {
                Debug.LogError($"CreateDropdownRow: parent is null for label '{label}'");
                return null;
            }
            var row = new GameObject(label + "Row");
            row.transform.SetParent(parent, false);
            Debug.Log($"Created dropdown row '{label}' under {parent.name}");

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
            var dropdown = CreateDropdown(row.transform, label + "Dropdown");

            return row;
        }

        private static GameObject CreateDropdown(Transform parent, string name)
        {
            var dropdown = new GameObject(name);
            dropdown.transform.SetParent(parent, false);

            var rect = dropdown.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 40);

            var image = dropdown.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var dropdownComponent = dropdown.AddComponent<TMP_Dropdown>();
            dropdownComponent.targetGraphic = image;

            // Label text
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(dropdown.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0);
            labelRect.anchorMax = new Vector2(1, 1);
            labelRect.offsetMin = new Vector2(10, 0);
            labelRect.offsetMax = new Vector2(-35, 0);
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Select...";
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
            var template = CreateDropdownTemplate(dropdown.transform);
            dropdownComponent.template = template.GetComponent<RectTransform>();

            var dropdownLayout = dropdown.AddComponent<LayoutElement>();
            dropdownLayout.preferredWidth = 400;
            dropdownLayout.preferredHeight = 40;

            return dropdown;
        }

        private static GameObject CreateDropdownTemplate(Transform parent)
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
            var templateViewport = new GameObject("Viewport");
            templateViewport.transform.SetParent(template.transform, false);
            var viewportRect = templateViewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.offsetMin = new Vector2(0, 0);
            viewportRect.offsetMax = new Vector2(0, 0);
            var viewportMask = templateViewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            var viewportImage = templateViewport.AddComponent<Image>();
            viewportImage.color = Color.white;

            // Content
            var templateContent = new GameObject("Content");
            templateContent.transform.SetParent(templateViewport.transform, false);
            var contentRect = templateContent.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 30);

            // Item
            var item = new GameObject("Item");
            item.transform.SetParent(templateContent.transform, false);
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

            var dropdownParent = parent.GetComponent<TMP_Dropdown>();
            if (dropdownParent != null)
            {
                dropdownParent.itemText = itemLabelText;
            }

            templateScroll.viewport = viewportRect;
            templateScroll.content = contentRect;

            template.SetActive(false);

            return template;
        }

        private static GameObject CreateSliderRow(Transform parent, string label, float min, float max, float defaultValue, string suffix = "")
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
            var sliderObj = CreateSlider(row.transform, label.Replace(" ", "") + "Slider", min, max, defaultValue);

            // Value text
            var valueObj = new GameObject("Value");
            valueObj.transform.SetParent(row.transform, false);
            var valueRect = valueObj.AddComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(80, 40);
            var valueText = valueObj.AddComponent<TextMeshProUGUI>();
            valueText.text = defaultValue.ToString("F0") + suffix;
            valueText.fontSize = 20;
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.color = Color.white;
            var valueLayout = valueObj.AddComponent<LayoutElement>();
            valueLayout.preferredWidth = 80;

            return row;
        }

        private static GameObject CreateSlider(Transform parent, string name, float min, float max, float defaultValue)
        {
            var sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(400, 20);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;

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
            fillRect.sizeDelta = new Vector2(0, 0);
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

        private static void WireUpOptionsManager(FaeMaze.UI.OptionsManager manager, GameObject tabBar,
            GameObject gameplayPanel, GameObject videoPanel, GameObject audioPanel,
            GameObject fullscreenRow, GameObject resolutionRow, GameObject fovRow,
            GameObject lanternVolumeRow, GameObject fairyRingVolumeRow, GameObject pondVolumeRow, GameObject sculptVolumeRow)
        {
            var so = new SerializedObject(manager);

            // Tab buttons
            var gameplayTabBtn = tabBar.transform.Find("GameplayTabButton");
            var videoTabBtn = tabBar.transform.Find("VideoTabButton");
            var audioTabBtn = tabBar.transform.Find("AudioTabButton");

            SetSerializedProperty(so, "gameplayTabButton", gameplayTabBtn?.GetComponent<Button>());
            SetSerializedProperty(so, "videoTabButton", videoTabBtn?.GetComponent<Button>());
            SetSerializedProperty(so, "audioTabButton", audioTabBtn?.GetComponent<Button>());

            // Panels
            SetSerializedProperty(so, "gameplayPanel", gameplayPanel);
            SetSerializedProperty(so, "videoPanel", videoPanel);
            SetSerializedProperty(so, "audioPanel", audioPanel);

            // Fullscreen toggle
            var fullscreenToggle = fullscreenRow.transform.Find("FullscreenToggle");
            if (fullscreenToggle != null)
            {
                SetSerializedProperty(so, "fullscreenToggle", fullscreenToggle.GetComponent<Toggle>());
            }

            // Resolution dropdown
            var resolutionDropdown = resolutionRow.transform.Find("ResolutionDropdown");
            if (resolutionDropdown != null)
            {
                SetSerializedProperty(so, "resolutionDropdown", resolutionDropdown.GetComponent<TMP_Dropdown>());
            }

            // Field of View
            var fovSlider = fovRow.transform.Find("FieldofViewSlider");
            if (fovSlider != null)
            {
                SetSerializedProperty(so, "fieldOfViewSlider", fovSlider.GetComponent<Slider>());
            }
            var fovText = fovRow.transform.Find("Value");
            if (fovText != null)
            {
                SetSerializedProperty(so, "fieldOfViewText", fovText.GetComponent<TextMeshProUGUI>());
            }

            // Individual sound volume sliders
            WireUpSliderRow(so, lanternVolumeRow, "lanternVolumeSlider", "lanternVolumeText");
            WireUpSliderRow(so, fairyRingVolumeRow, "fairyRingVolumeSlider", "fairyRingVolumeText");
            WireUpSliderRow(so, pondVolumeRow, "pondVolumeSlider", "pondVolumeText");
            WireUpSliderRow(so, sculptVolumeRow, "sculptVolumeSlider", "sculptVolumeText");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);

            Debug.Log("OptionsManager wired up successfully!");
        }

        private static void WireUpSliderRow(SerializedObject so, GameObject row, string sliderProperty, string textProperty)
        {
            if (row == null) return;

            // Find the slider (child with "Slider" in name)
            foreach (Transform child in row.transform)
            {
                if (child.name.Contains("Slider"))
                {
                    var slider = child.GetComponent<Slider>();
                    if (slider != null)
                    {
                        SetSerializedProperty(so, sliderProperty, slider);
                    }
                    break;
                }
            }

            // Find the value text
            var valueText = row.transform.Find("Value");
            if (valueText != null)
            {
                SetSerializedProperty(so, textProperty, valueText.GetComponent<TextMeshProUGUI>());
            }
        }

        private static void SetSerializedProperty(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"Property '{propertyName}' not found on OptionsManager");
            }
        }
    }
}

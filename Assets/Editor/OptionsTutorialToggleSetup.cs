using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add Tutorial toggle to the Options scene Gameplay tab.
    /// Run from menu: FaeMaze > Setup Options Tutorial Toggle
    /// </summary>
    public static class OptionsTutorialToggleSetup
    {
        // Style constants matching existing Options UI
        private static readonly Color ROW_BACKGROUND_COLOR = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color TOGGLE_BACKGROUND_COLOR = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color CHECKMARK_COLOR = new Color(0.9f, 0.7f, 0.1f, 1f); // Yellow-orange
        private const float ROW_HEIGHT = 40f;
        private const float LABEL_FONT_SIZE = 20f;
        private const float TOGGLE_SIZE = 24f;

        [MenuItem("FaeMaze/Setup Options Tutorial Toggle")]
        public static void SetupTutorialToggle()
        {
            // Find the OptionsManager in the scene
            var optionsManager = Object.FindFirstObjectByType<UI.OptionsManager>();
            if (optionsManager == null)
            {
                Debug.LogError("OptionsTutorialToggleSetup: OptionsManager not found in scene. Open the Options scene first.");
                return;
            }

            // Find the Canvas to search from (the root of the UI hierarchy)
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsTutorialToggleSetup: Canvas not found in scene.");
                return;
            }

            // Check if toggle already exists
            var existingToggle = FindChildByName(canvas.transform, "showTutorial_Row");
            if (existingToggle != null)
            {
                Debug.Log("OptionsTutorialToggleSetup: Tutorial toggle already exists. Run 'Remove Options Tutorial Toggle' first to recreate.");
                return;
            }

            // Find the useFixedSeed_Row as a template/sibling position
            // Look in the Canvas hierarchy since that's where the UI elements are
            var useFixedSeedRow = FindChildByName(canvas.transform, "useFixedSeed_Row");
            if (useFixedSeedRow == null)
            {
                // Also try finding by the toggle name (useFixedSeed_Toggle's parent row)
                var useFixedSeedToggle = FindChildByName(canvas.transform, "useFixedSeed_Toggle");
                if (useFixedSeedToggle != null)
                {
                    useFixedSeedRow = useFixedSeedToggle.parent;
                    Debug.Log($"OptionsTutorialToggleSetup: Found toggle parent: {useFixedSeedRow?.name}");
                }
            }

            if (useFixedSeedRow == null)
            {
                // Last resort: find the GAMEFLOWSection Content panel
                var gameFlowSection = FindChildByName(canvas.transform, "GAMEFLOWSection");
                if (gameFlowSection != null)
                {
                    var content = FindChildByName(gameFlowSection, "Content");
                    if (content != null)
                    {
                        // Add to the Content panel directly
                        Debug.Log("OptionsTutorialToggleSetup: Using GAMEFLOWSection/Content as parent.");
                        GameObject newTutorialRow = CreateTutorialToggleRow(content);
                        WireUpToggle(optionsManager, newTutorialRow);
                        EditorUtility.SetDirty(optionsManager.gameObject);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(optionsManager.gameObject.scene);
                        Debug.Log("OptionsTutorialToggleSetup: Created Tutorial toggle in Gameplay tab. Save the scene!");
                        return;
                    }
                }

                Debug.LogError("OptionsTutorialToggleSetup: Could not find useFixedSeed_Row or GAMEFLOWSection/Content. Scene structure may have changed.");
                return;
            }

            // Create the tutorial toggle row as a sibling of useFixedSeed_Row
            Transform parent = useFixedSeedRow.parent;
            GameObject tutorialRow = CreateTutorialToggleRow(parent);

            // Position it after useFixedSeed_Row
            tutorialRow.transform.SetSiblingIndex(useFixedSeedRow.GetSiblingIndex() + 1);

            // Wire up to OptionsManager
            WireUpToggle(optionsManager, tutorialRow);

            // Mark scene dirty
            EditorUtility.SetDirty(optionsManager.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(optionsManager.gameObject.scene);

            Debug.Log("OptionsTutorialToggleSetup: Created Tutorial toggle in Gameplay tab. Save the scene!");
        }

        private static GameObject CreateTutorialToggleRow(Transform parent)
        {
            // Create row container
            GameObject row = new GameObject("showTutorial_Row");
            row.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(row, "Create Tutorial Toggle Row");

            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, ROW_HEIGHT);

            // Add layout element for proper sizing
            LayoutElement layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.minHeight = ROW_HEIGHT;
            layoutElement.preferredHeight = ROW_HEIGHT;
            layoutElement.flexibleWidth = 1f;

            // Add horizontal layout group
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 20, 5, 5);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Create label
            CreateLabel(row.transform, "Show Tutorial on First Run");

            // Create spacer to push toggle to the right
            CreateSpacer(row.transform);

            // Create toggle
            CreateToggle(row.transform);

            return row;
        }

        private static void CreateLabel(Transform parent, string text)
        {
            GameObject labelObj = new GameObject("showTutorial_Label");
            labelObj.transform.SetParent(parent, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();

            // Add layout element
            LayoutElement le = labelObj.AddComponent<LayoutElement>();
            le.minWidth = 250f;
            le.preferredWidth = 300f;

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = LABEL_FONT_SIZE;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void CreateSpacer(Transform parent)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);

            RectTransform spacerRect = spacer.AddComponent<RectTransform>();

            LayoutElement le = spacer.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        private static void CreateToggle(Transform parent)
        {
            GameObject toggleObj = new GameObject("showTutorial_Toggle");
            toggleObj.transform.SetParent(parent, false);

            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(TOGGLE_SIZE, TOGGLE_SIZE);

            // Add layout element
            LayoutElement le = toggleObj.AddComponent<LayoutElement>();
            le.minWidth = TOGGLE_SIZE;
            le.preferredWidth = TOGGLE_SIZE;
            le.minHeight = TOGGLE_SIZE;
            le.preferredHeight = TOGGLE_SIZE;

            // Toggle background
            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = TOGGLE_BACKGROUND_COLOR;

            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = true; // Default to show tutorial on first run

            // Toggle checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleObj.transform, false);

            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(4f, 4f);
            checkRect.offsetMax = new Vector2(-4f, -4f);

            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = CHECKMARK_COLOR;

            toggle.graphic = checkImage;
            toggle.targetGraphic = toggleBg;
        }

        private static void WireUpToggle(UI.OptionsManager manager, GameObject toggleRow)
        {
            SerializedObject so = new SerializedObject(manager);

            // Find the toggle in the row
            var toggle = toggleRow.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                var prop = so.FindProperty("showTutorialToggle");
                if (prop != null)
                {
                    prop.objectReferenceValue = toggle;
                    so.ApplyModifiedProperties();
                    Debug.Log("OptionsTutorialToggleSetup: Wired up showTutorialToggle to OptionsManager.");
                }
                else
                {
                    Debug.LogWarning("OptionsTutorialToggleSetup: Could not find showTutorialToggle property in OptionsManager.");
                }
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        [MenuItem("FaeMaze/Remove Options Tutorial Toggle")]
        public static void RemoveTutorialToggle()
        {
            var optionsManager = Object.FindFirstObjectByType<UI.OptionsManager>();
            var canvas = Object.FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("OptionsTutorialToggleSetup: Canvas not found in scene.");
                return;
            }

            // Remove the toggle row
            var toggleRow = FindChildByName(canvas.transform, "showTutorial_Row");
            if (toggleRow != null)
            {
                Undo.DestroyObjectImmediate(toggleRow.gameObject);
                Debug.Log("OptionsTutorialToggleSetup: Removed Tutorial toggle row.");
            }
            else
            {
                Debug.Log("OptionsTutorialToggleSetup: No tutorial toggle row found to remove.");
            }

            // Clear the reference in OptionsManager
            if (optionsManager != null)
            {
                SerializedObject so = new SerializedObject(optionsManager);
                var prop = so.FindProperty("showTutorialToggle");
                if (prop != null)
                {
                    prop.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("OptionsTutorialToggleSetup: Cleanup complete. Save the scene!");
        }
    }
}

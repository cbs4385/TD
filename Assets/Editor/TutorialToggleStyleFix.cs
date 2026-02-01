#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to fix styling consistency in the Options scene:
    /// 1. Make "Show Tutorial on First Run" look exactly like "Use Fixed Seed"
    /// 2. Make VIDEO tab's Screenshot Key row look exactly like CONTROLS tab's Take Screenshot row
    ///
    /// Run from Unity menu: FaeMaze > Fix Options Styling
    /// </summary>
    public class TutorialToggleStyleFix
    {
        // Layout constants matching ControlsTabMultiBindingSetup
        private const float LABEL_WIDTH = 300f;
        private const float BINDING_WIDTH = 120f;
        private const float TOGGLE_SIZE = 24f;
        private const float TOGGLE_TO_TEXT_GAP = 8f;
        private const float TEXT_TO_TOGGLE_GAP = 16f;

        private static readonly Color ToggleBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color ButtonBgColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color CheckmarkColor = new Color(1f, 0.8f, 0.3f, 1f);

        [MenuItem("FaeMaze/Fix Options Styling")]
        public static void FixOptionsStyles()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                EditorUtility.DisplayDialog("Error", "Please open the Options scene first!", "OK");
                return;
            }

            bool tutorialFixed = FixTutorialToggleStyle();
            bool screenshotFixed = FixVideoScreenshotRow();

            EditorSceneManager.MarkSceneDirty(scene);

            string message = "";
            if (tutorialFixed) message += "- Tutorial toggle style updated\n";
            if (screenshotFixed) message += "- VIDEO Screenshot row updated\n";
            if (string.IsNullOrEmpty(message)) message = "No changes needed.";

            EditorUtility.DisplayDialog("Done", message + "\nSave the scene!", "OK");
        }

        /// <summary>
        /// Makes "Show Tutorial on First Run" row look exactly like "Use Fixed Seed" row.
        /// </summary>
        private static bool FixTutorialToggleStyle()
        {
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform tutorialRow = null;
            Transform fixedSeedRow = null;
            Transform tutorialToggle = null;
            Transform fixedSeedToggle = null;

            foreach (var t in allTransforms)
            {
                if (t.name == "showTutorial_Row")
                    tutorialRow = t;
                else if (t.name == "useFixedSeed_Row")
                    fixedSeedRow = t;
                else if (t.name == "showTutorial_Toggle")
                    tutorialToggle = t;
                else if (t.name == "useFixedSeed_Toggle")
                    fixedSeedToggle = t;
            }

            if (tutorialRow == null || fixedSeedRow == null)
            {
                Debug.LogWarning("Could not find showTutorial_Row or useFixedSeed_Row");
                return false;
            }

            // Copy row's HorizontalLayoutGroup settings
            var seedHLG = fixedSeedRow.GetComponent<HorizontalLayoutGroup>();
            var tutorialHLG = tutorialRow.GetComponent<HorizontalLayoutGroup>();

            if (seedHLG != null && tutorialHLG != null)
            {
                tutorialHLG.padding = seedHLG.padding;
                tutorialHLG.spacing = seedHLG.spacing;
                tutorialHLG.childAlignment = seedHLG.childAlignment;
                tutorialHLG.childForceExpandWidth = seedHLG.childForceExpandWidth;
                tutorialHLG.childForceExpandHeight = seedHLG.childForceExpandHeight;
                tutorialHLG.childControlWidth = seedHLG.childControlWidth;
                tutorialHLG.childControlHeight = seedHLG.childControlHeight;
            }

            // Copy row's LayoutElement settings
            var seedRowLE = fixedSeedRow.GetComponent<LayoutElement>();
            var tutorialRowLE = tutorialRow.GetComponent<LayoutElement>();

            if (seedRowLE != null && tutorialRowLE != null)
            {
                tutorialRowLE.minHeight = seedRowLE.minHeight;
                tutorialRowLE.preferredHeight = seedRowLE.preferredHeight;
                tutorialRowLE.flexibleWidth = seedRowLE.flexibleWidth;
            }

            // Copy toggle styling
            if (tutorialToggle != null && fixedSeedToggle != null)
            {
                var seedToggleRect = fixedSeedToggle.GetComponent<RectTransform>();
                var tutorialToggleRect = tutorialToggle.GetComponent<RectTransform>();

                var seedToggleImage = fixedSeedToggle.GetComponent<Image>();
                var tutorialToggleImage = tutorialToggle.GetComponent<Image>();

                var seedToggleLE = fixedSeedToggle.GetComponent<LayoutElement>();
                var tutorialToggleLE = tutorialToggle.GetComponent<LayoutElement>();

                if (seedToggleRect != null && tutorialToggleRect != null)
                {
                    tutorialToggleRect.sizeDelta = seedToggleRect.sizeDelta;
                }

                if (seedToggleImage != null)
                {
                    if (tutorialToggleImage == null)
                    {
                        tutorialToggleImage = tutorialToggle.gameObject.AddComponent<Image>();
                    }
                    tutorialToggleImage.color = seedToggleImage.color;
                    tutorialToggleImage.sprite = seedToggleImage.sprite;
                    tutorialToggleImage.type = seedToggleImage.type;
                }

                if (seedToggleLE != null)
                {
                    if (tutorialToggleLE == null)
                    {
                        tutorialToggleLE = tutorialToggle.gameObject.AddComponent<LayoutElement>();
                    }
                    tutorialToggleLE.preferredWidth = seedToggleLE.preferredWidth;
                    tutorialToggleLE.preferredHeight = seedToggleLE.preferredHeight;
                    tutorialToggleLE.minWidth = seedToggleLE.minWidth;
                    tutorialToggleLE.minHeight = seedToggleLE.minHeight;
                }

                // Copy Toggle component settings
                var seedToggleComp = fixedSeedToggle.GetComponent<Toggle>();
                var tutorialToggleComp = tutorialToggle.GetComponent<Toggle>();

                if (seedToggleComp != null && tutorialToggleComp != null)
                {
                    tutorialToggleComp.transition = seedToggleComp.transition;
                    tutorialToggleComp.colors = seedToggleComp.colors;
                    tutorialToggleComp.targetGraphic = tutorialToggleImage;
                }

                Debug.Log("Updated showTutorial_Toggle to match useFixedSeed_Toggle");
            }

            // Update label to match
            Transform tutorialLabel = null;
            Transform seedLabel = null;

            foreach (Transform child in tutorialRow)
            {
                if (child.name.Contains("Label") || child.GetComponent<TextMeshProUGUI>() != null)
                {
                    var tmp = child.GetComponent<TextMeshProUGUI>();
                    if (tmp != null && tmp.text.Contains("Tutorial"))
                    {
                        tutorialLabel = child;
                        break;
                    }
                }
            }

            foreach (Transform child in fixedSeedRow)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null && tmp.text.Contains("Fixed Seed"))
                {
                    seedLabel = child;
                    break;
                }
            }

            if (tutorialLabel != null && seedLabel != null)
            {
                var seedLabelLE = seedLabel.GetComponent<LayoutElement>();
                var tutorialLabelLE = tutorialLabel.GetComponent<LayoutElement>();

                if (seedLabelLE != null)
                {
                    if (tutorialLabelLE == null)
                    {
                        tutorialLabelLE = tutorialLabel.gameObject.AddComponent<LayoutElement>();
                    }
                    tutorialLabelLE.preferredWidth = seedLabelLE.preferredWidth;
                    tutorialLabelLE.minWidth = seedLabelLE.minWidth;
                }

                var seedLabelRect = seedLabel.GetComponent<RectTransform>();
                var tutorialLabelRect = tutorialLabel.GetComponent<RectTransform>();

                if (seedLabelRect != null && tutorialLabelRect != null)
                {
                    tutorialLabelRect.sizeDelta = seedLabelRect.sizeDelta;
                }
            }

            return true;
        }

        /// <summary>
        /// Makes VIDEO tab's Screenshot Key row look exactly like CONTROLS tab's Take Screenshot row.
        /// Replaces the single KeyBindingCapture with a 3-column layout matching CONTROLS tab.
        /// </summary>
        private static bool FixVideoScreenshotRow()
        {
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform videoPanel = null;
            Transform screenshotKeyRow = null;

            foreach (var t in allTransforms)
            {
                if (t.name == "VideoPanel")
                    videoPanel = t;
                else if (t.name == "ScreenshotKeyRow")
                    screenshotKeyRow = t;
            }

            if (screenshotKeyRow == null)
            {
                Debug.LogWarning("Could not find ScreenshotKeyRow in VIDEO panel");
                return false;
            }

            // Get the current label text
            string labelText = "Screenshot Key";
            foreach (Transform child in screenshotKeyRow)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null && child.name == "Label")
                {
                    labelText = tmp.text;
                    break;
                }
            }

            // Remove all existing children except Label
            var childrenToDestroy = new System.Collections.Generic.List<GameObject>();
            Transform existingLabel = null;

            foreach (Transform child in screenshotKeyRow)
            {
                if (child.name == "Label")
                {
                    existingLabel = child;
                }
                else
                {
                    childrenToDestroy.Add(child.gameObject);
                }
            }

            foreach (var go in childrenToDestroy)
            {
                Object.DestroyImmediate(go);
            }

            // Update the row's layout to match CONTROLS tab rows
            var rowHLG = screenshotKeyRow.GetComponent<HorizontalLayoutGroup>();
            if (rowHLG == null)
            {
                rowHLG = screenshotKeyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            }
            rowHLG.padding = new RectOffset(0, 0, 0, 0);
            rowHLG.spacing = 0;
            rowHLG.childAlignment = TextAnchor.MiddleLeft;
            rowHLG.childForceExpandWidth = false;
            rowHLG.childForceExpandHeight = false;
            rowHLG.childControlWidth = false;
            rowHLG.childControlHeight = false;

            var rowLE = screenshotKeyRow.GetComponent<LayoutElement>();
            if (rowLE == null)
            {
                rowLE = screenshotKeyRow.gameObject.AddComponent<LayoutElement>();
            }
            rowLE.minHeight = 40;
            rowLE.preferredHeight = 40;

            // Update or create the label
            if (existingLabel == null)
            {
                CreateRowLabel(screenshotKeyRow, labelText);
            }
            else
            {
                // Update existing label
                var labelRect = existingLabel.GetComponent<RectTransform>();
                if (labelRect != null)
                {
                    labelRect.sizeDelta = new Vector2(LABEL_WIDTH, 40);
                }

                var labelLE = existingLabel.GetComponent<LayoutElement>();
                if (labelLE == null)
                {
                    labelLE = existingLabel.gameObject.AddComponent<LayoutElement>();
                }
                labelLE.minWidth = LABEL_WIDTH;
                labelLE.preferredWidth = LABEL_WIDTH;
            }

            // Create three binding columns matching CONTROLS tab
            CreateBindingCapture(screenshotKeyRow, "Screenshot", BINDING_WIDTH);
            CreateOptionalBindingCapture(screenshotKeyRow, "ScreenshotAlt", BINDING_WIDTH);
            CreateOptionalBindingCapture(screenshotKeyRow, "ScreenshotTertiary", BINDING_WIDTH);

            Debug.Log("Updated ScreenshotKeyRow to match CONTROLS tab Take Screenshot row");
            return true;
        }

        private static void CreateRowLabel(Transform parent, string label)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent, false);

            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(LABEL_WIDTH, 40);

            var labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.minWidth = LABEL_WIDTH;
            labelLE.preferredWidth = LABEL_WIDTH;

            var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 20;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.color = Color.white;
            labelTMP.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void CreateBindingCapture(Transform parent, string bindingName, float width)
        {
            float totalWidth = TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP + width + TEXT_TO_TOGGLE_GAP;

            GameObject captureObj = new GameObject(bindingName + "_Button");
            captureObj.transform.SetParent(parent, false);

            var captureRect = captureObj.AddComponent<RectTransform>();
            captureRect.sizeDelta = new Vector2(totalWidth, 40);

            var captureLE = captureObj.AddComponent<LayoutElement>();
            captureLE.minWidth = totalWidth;
            captureLE.minHeight = 40;
            captureLE.preferredWidth = totalWidth;
            captureLE.preferredHeight = 40;

            // CaptureToggle
            GameObject toggleObj = new GameObject("CaptureToggle");
            toggleObj.transform.SetParent(captureObj.transform, false);

            var toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.pivot = new Vector2(0, 0.5f);
            toggleRect.anchoredPosition = new Vector2(0, 0);
            toggleRect.sizeDelta = new Vector2(TOGGLE_SIZE, TOGGLE_SIZE);

            var toggleImage = toggleObj.AddComponent<Image>();
            toggleImage.color = ToggleBgColor;

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = toggleImage;
            toggle.isOn = false;

            // Checkmark
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);

            var checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;

            var checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = CheckmarkColor;

            toggle.graphic = checkmarkImage;

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(captureObj.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.pivot = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP, 0);
            bgRect.sizeDelta = new Vector2(width, 32);

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = ButtonBgColor;

            // BindingText
            GameObject textObj = new GameObject("BindingText");
            textObj.transform.SetParent(bgObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-28, 0);

            var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
            bindingTMP.text = "-";
            bindingTMP.fontSize = 16;
            bindingTMP.alignment = TextAlignmentOptions.Center;
            bindingTMP.color = Color.white;

            // Add KeyBindingCapture
            var capture = captureObj.AddComponent<KeyBindingCapture>();

            var so = new SerializedObject(capture);
            so.FindProperty("bindingText").objectReferenceValue = bindingTMP;
            so.FindProperty("captureToggle").objectReferenceValue = toggle;
            so.ApplyModifiedProperties();
        }

        private static void CreateOptionalBindingCapture(Transform parent, string bindingName, float width)
        {
            float totalWidth = TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP + width + TEXT_TO_TOGGLE_GAP;

            GameObject captureObj = new GameObject(bindingName + "_Button");
            captureObj.transform.SetParent(parent, false);

            var captureRect = captureObj.AddComponent<RectTransform>();
            captureRect.sizeDelta = new Vector2(totalWidth, 40);

            var captureLE = captureObj.AddComponent<LayoutElement>();
            captureLE.minWidth = totalWidth;
            captureLE.minHeight = 40;
            captureLE.preferredWidth = totalWidth;
            captureLE.preferredHeight = 40;

            // CaptureToggle
            GameObject toggleObj = new GameObject("CaptureToggle");
            toggleObj.transform.SetParent(captureObj.transform, false);

            var toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.pivot = new Vector2(0, 0.5f);
            toggleRect.anchoredPosition = new Vector2(0, 0);
            toggleRect.sizeDelta = new Vector2(TOGGLE_SIZE, TOGGLE_SIZE);

            var toggleImage = toggleObj.AddComponent<Image>();
            toggleImage.color = ToggleBgColor;

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.targetGraphic = toggleImage;
            toggle.isOn = false;

            // Checkmark
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);

            var checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;

            var checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = CheckmarkColor;

            toggle.graphic = checkmarkImage;

            // Background - slightly darker for optional
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(captureObj.transform, false);

            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.pivot = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = new Vector2(TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP, 0);
            bgRect.sizeDelta = new Vector2(width, 32);

            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // BindingText
            GameObject textObj = new GameObject("BindingText");
            textObj.transform.SetParent(bgObj.transform, false);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4, 0);
            textRect.offsetMax = new Vector2(-28, 0);

            var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
            bindingTMP.text = "-";
            bindingTMP.fontSize = 16;
            bindingTMP.alignment = TextAlignmentOptions.Center;
            bindingTMP.color = new Color(0.5f, 0.5f, 0.5f, 1f);

            // Add KeyBindingCapture
            var capture = captureObj.AddComponent<KeyBindingCapture>();

            var so = new SerializedObject(capture);
            so.FindProperty("bindingText").objectReferenceValue = bindingTMP;
            so.FindProperty("captureToggle").objectReferenceValue = toggle;
            so.ApplyModifiedProperties();
        }
    }
}
#endif

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add Toggle checkboxes to all KeyBindingCapture rows in the Options scene.
    /// This separates the "select this binding" action from "capture input" to avoid left-click conflicts.
    /// Also converts dropdown-based key bindings to KeyBindingCapture for consistency.
    /// </summary>
    public class OptionsBindingToggleSetup
    {
        [MenuItem("FaeMaze/Setup Binding Capture Toggles")]
        public static void SetupBindingToggles()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find all KeyBindingCapture components in the entire scene (not just ControlsPanel)
            var bindingCaptures = Object.FindObjectsByType<FaeMaze.UI.KeyBindingCapture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"Found {bindingCaptures.Length} KeyBindingCapture components in scene");

            int modifiedCount = 0;

            foreach (var capture in bindingCaptures)
            {
                if (AddToggleToCapture(capture))
                {
                    modifiedCount++;
                }
            }

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"Setup complete! Added Toggles to {modifiedCount} KeyBindingCapture components. Don't forget to save the scene.");
        }

        /// <summary>
        /// Adds a Toggle to a KeyBindingCapture component if it doesn't already have one.
        /// Returns true if a toggle was added.
        /// </summary>
        private static bool AddToggleToCapture(FaeMaze.UI.KeyBindingCapture capture)
        {
            // Check if this row already has a Toggle
            var existingToggle = capture.GetComponentInChildren<Toggle>(true);
            if (existingToggle != null)
            {
                // Check if it's wired up
                SerializedObject serializedCapture = new SerializedObject(capture);
                SerializedProperty toggleProp = serializedCapture.FindProperty("captureToggle");
                if (toggleProp != null && toggleProp.objectReferenceValue == null)
                {
                    toggleProp.objectReferenceValue = existingToggle;
                    serializedCapture.ApplyModifiedProperties();
                    Debug.Log($"  {capture.gameObject.name} - wired up existing Toggle");
                }
                else
                {
                    Debug.Log($"  {capture.gameObject.name} already has a Toggle, skipping");
                }
                return false;
            }

            // Find the button that was used for capturing (we'll convert it to display-only)
            var button = capture.GetComponent<Button>();
            if (button == null)
            {
                button = capture.GetComponentInChildren<Button>(true);
            }

            // Create a new Toggle to the left of the content
            GameObject toggleObj = new GameObject("CaptureToggle");
            toggleObj.transform.SetParent(capture.transform, false);

            // Add RectTransform
            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();

            // Position toggle to the left of the binding display
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.pivot = new Vector2(0, 0.5f);
            toggleRect.anchoredPosition = new Vector2(-30, 0);
            toggleRect.sizeDelta = new Vector2(24, 24);

            // Add Image component for the checkbox background
            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Add Toggle component
            Toggle toggle = toggleObj.AddComponent<Toggle>();

            // Create checkmark child
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);

            RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = Vector2.zero;
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.offsetMin = new Vector2(4, 4);
            checkmarkRect.offsetMax = new Vector2(-4, -4);

            Image checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = new Color(1f, 0.8f, 0.3f, 1f);  // Yellow-orange to match capture text color

            // Configure toggle
            toggle.targetGraphic = toggleBg;
            toggle.graphic = checkmarkImage;
            toggle.isOn = false;

            // Wire up the toggle to the KeyBindingCapture component via SerializedObject
            SerializedObject serializedCaptureObj = new SerializedObject(capture);
            SerializedProperty toggleProperty = serializedCaptureObj.FindProperty("captureToggle");
            if (toggleProperty != null)
            {
                toggleProperty.objectReferenceValue = toggle;
                serializedCaptureObj.ApplyModifiedProperties();
            }

            // Remove onClick listener from button if present
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
            }

            // Move toggle to be the first sibling so it appears on the left
            toggleObj.transform.SetAsFirstSibling();

            Debug.Log($"  Added Toggle to {capture.gameObject.name}");
            return true;
        }

        [MenuItem("FaeMaze/Convert Screenshot Dropdown to Capture")]
        public static void ConvertScreenshotDropdown()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find VideoPanel
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform videoPanel = null;
            Transform screenshotKeyRow = null;

            foreach (var t in allTransforms)
            {
                if (t.name == "VideoPanel")
                {
                    videoPanel = t;
                }
                // Look for the screenshot key row - might be named various things
                if (t.name.ToLower().Contains("screenshot") && t.name.ToLower().Contains("key"))
                {
                    screenshotKeyRow = t;
                }
            }

            if (videoPanel == null)
            {
                Debug.LogError("VideoPanel not found!");
                return;
            }

            // Find the screenshot dropdown in VideoPanel
            TMP_Dropdown screenshotDropdown = null;
            foreach (Transform child in videoPanel.GetComponentsInChildren<Transform>(true))
            {
                var dropdown = child.GetComponent<TMP_Dropdown>();
                if (dropdown != null && child.name.ToLower().Contains("screenshot"))
                {
                    screenshotDropdown = dropdown;
                    screenshotKeyRow = child.parent;
                    break;
                }
            }

            if (screenshotDropdown == null)
            {
                // Try to find it by looking at OptionsManager reference
                var optionsManager = Object.FindFirstObjectByType<FaeMaze.UI.OptionsManager>(FindObjectsInactive.Include);
                if (optionsManager != null)
                {
                    SerializedObject so = new SerializedObject(optionsManager);
                    SerializedProperty dropdownProp = so.FindProperty("screenshotKeyDropdown");
                    if (dropdownProp != null && dropdownProp.objectReferenceValue != null)
                    {
                        screenshotDropdown = dropdownProp.objectReferenceValue as TMP_Dropdown;
                        if (screenshotDropdown != null)
                        {
                            screenshotKeyRow = screenshotDropdown.transform.parent;
                        }
                    }
                }
            }

            if (screenshotDropdown == null)
            {
                Debug.LogWarning("Screenshot dropdown not found in VideoPanel. It may have already been converted.");
                return;
            }

            // Check if there's already a KeyBindingCapture on this row
            var existingCapture = screenshotKeyRow.GetComponentInChildren<FaeMaze.UI.KeyBindingCapture>(true);
            if (existingCapture != null)
            {
                Debug.Log("Screenshot row already has KeyBindingCapture. Adding toggle if needed...");
                AddToggleToCapture(existingCapture);
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            // Get dropdown's RectTransform info before replacing
            RectTransform dropdownRect = screenshotDropdown.GetComponent<RectTransform>();
            Vector2 anchorMin = dropdownRect.anchorMin;
            Vector2 anchorMax = dropdownRect.anchorMax;
            Vector2 anchoredPos = dropdownRect.anchoredPosition;
            Vector2 sizeDelta = dropdownRect.sizeDelta;

            // Create a new GameObject to hold the KeyBindingCapture
            GameObject captureObj = new GameObject("ScreenshotKey_Capture");
            captureObj.transform.SetParent(screenshotKeyRow, false);

            // Set up RectTransform to match dropdown position
            RectTransform captureRect = captureObj.AddComponent<RectTransform>();
            captureRect.anchorMin = anchorMin;
            captureRect.anchorMax = anchorMax;
            captureRect.anchoredPosition = anchoredPos;
            captureRect.sizeDelta = sizeDelta;

            // Add background image
            Image bgImage = captureObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Create text to display the binding
            GameObject textObj = new GameObject("BindingText");
            textObj.transform.SetParent(captureObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            TextMeshProUGUI bindingText = textObj.AddComponent<TextMeshProUGUI>();
            bindingText.text = "F12";
            bindingText.fontSize = 18;
            bindingText.alignment = TextAlignmentOptions.Center;
            bindingText.color = Color.white;

            // Add KeyBindingCapture component
            FaeMaze.UI.KeyBindingCapture capture = captureObj.AddComponent<FaeMaze.UI.KeyBindingCapture>();

            // Wire up the binding text
            SerializedObject serializedCapture = new SerializedObject(capture);
            SerializedProperty textProp = serializedCapture.FindProperty("bindingText");
            if (textProp != null)
            {
                textProp.objectReferenceValue = bindingText;
            }
            serializedCapture.ApplyModifiedProperties();

            // Add toggle to the capture
            AddToggleToCapture(capture);

            // Hide the old dropdown (don't destroy in case we need to revert)
            screenshotDropdown.gameObject.SetActive(false);

            // Position capture object at same sibling index
            captureObj.transform.SetSiblingIndex(screenshotDropdown.transform.GetSiblingIndex());

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Converted Screenshot dropdown to KeyBindingCapture with Toggle. Don't forget to save the scene!");
        }

        [MenuItem("FaeMaze/Remove Old Binding Buttons")]
        public static void RemoveOldBindingButtons()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find all KeyBindingCapture components and remove Button components
            var bindingCaptures = Object.FindObjectsByType<FaeMaze.UI.KeyBindingCapture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removedCount = 0;

            foreach (var capture in bindingCaptures)
            {
                var button = capture.GetComponent<Button>();
                if (button != null)
                {
                    Object.DestroyImmediate(button);
                    removedCount++;
                    Debug.Log($"  Removed Button from {capture.gameObject.name}");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Removed {removedCount} Button components. Don't forget to save the scene.");
        }
    }
}

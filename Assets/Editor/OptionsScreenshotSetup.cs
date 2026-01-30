using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to ensure the Screenshot Key control has a Toggle for capture activation.
    /// Works with KeyBindingCapture components in any panel (Gameplay, Video, Controls, etc.)
    /// </summary>
    public class OptionsScreenshotSetup
    {
        [MenuItem("FaeMaze/Setup Screenshot Capture Toggle")]
        public static void SetupScreenshotCaptureToggle()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find all KeyBindingCapture components in the entire scene
            var allCaptures = Object.FindObjectsByType<FaeMaze.UI.KeyBindingCapture>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int screenshotCaptureCount = 0;

            foreach (var capture in allCaptures)
            {
                string objName = capture.gameObject.name.ToLower();
                if (objName.Contains("screenshot"))
                {
                    screenshotCaptureCount++;
                    Debug.Log($"Found screenshot KeyBindingCapture: {capture.gameObject.name} in {GetFullPath(capture.transform)}");

                    // Check if it already has a toggle
                    var existingToggle = capture.GetComponentInChildren<Toggle>(true);
                    if (existingToggle != null)
                    {
                        Debug.Log($"  Already has toggle, ensuring it's wired up...");
                        EnsureToggleWiredUp(capture, existingToggle);
                    }
                    else
                    {
                        Debug.Log($"  Adding toggle...");
                        AddToggleToCapture(capture);
                    }
                }
            }

            if (screenshotCaptureCount == 0)
            {
                Debug.LogWarning("No screenshot KeyBindingCapture components found. They may need to be created first.");

                // Try to find any screenshot-related UI elements
                var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    if (t.name.ToLower().Contains("screenshot") && t.name.ToLower().Contains("key"))
                    {
                        Debug.Log($"Found screenshot-related object: {t.name} at {GetFullPath(t)}");

                        // Check if it has a Button that could be converted
                        var button = t.GetComponent<Button>();
                        if (button == null) button = t.GetComponentInChildren<Button>(true);

                        if (button != null)
                        {
                            Debug.Log($"  Has Button component - may need KeyBindingCapture added");
                        }

                        // Check if it has a Dropdown
                        var dropdown = t.GetComponent<TMP_Dropdown>();
                        if (dropdown == null) dropdown = t.GetComponentInChildren<TMP_Dropdown>(true);

                        if (dropdown != null)
                        {
                            Debug.Log($"  Has Dropdown component - needs conversion to KeyBindingCapture");
                        }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Setup complete! Processed {screenshotCaptureCount} screenshot captures. Don't forget to save the scene.");
        }

        private static string GetFullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private static void EnsureToggleWiredUp(FaeMaze.UI.KeyBindingCapture capture, Toggle toggle)
        {
            SerializedObject serializedCapture = new SerializedObject(capture);
            SerializedProperty toggleProp = serializedCapture.FindProperty("captureToggle");
            if (toggleProp != null)
            {
                if (toggleProp.objectReferenceValue == null)
                {
                    toggleProp.objectReferenceValue = toggle;
                    serializedCapture.ApplyModifiedProperties();
                    Debug.Log($"    Wired up existing toggle");
                }
                else
                {
                    Debug.Log($"    Toggle already wired up");
                }
            }
        }

        private static void AddToggleToCapture(FaeMaze.UI.KeyBindingCapture capture)
        {
            // Create toggle object
            GameObject toggleObj = new GameObject("CaptureToggle");
            toggleObj.transform.SetParent(capture.transform, false);

            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 0.5f);
            toggleRect.anchorMax = new Vector2(0, 0.5f);
            toggleRect.pivot = new Vector2(0, 0.5f);
            toggleRect.anchoredPosition = new Vector2(5, 0);
            toggleRect.sizeDelta = new Vector2(24, 24);

            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            Toggle toggle = toggleObj.AddComponent<Toggle>();

            // Create checkmark
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(toggleObj.transform, false);

            RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = Vector2.zero;
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.offsetMin = new Vector2(4, 4);
            checkmarkRect.offsetMax = new Vector2(-4, -4);

            Image checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = new Color(1f, 0.8f, 0.3f, 1f);

            toggle.targetGraphic = toggleBg;
            toggle.graphic = checkmarkImage;
            toggle.isOn = false;

            // Wire up to capture
            SerializedObject serializedCapture = new SerializedObject(capture);
            SerializedProperty toggleProp = serializedCapture.FindProperty("captureToggle");
            if (toggleProp != null)
            {
                toggleProp.objectReferenceValue = toggle;
                serializedCapture.ApplyModifiedProperties();
            }

            // Move toggle to first sibling
            toggleObj.transform.SetAsFirstSibling();

            Debug.Log($"    Added toggle to {capture.gameObject.name}");
        }

        [MenuItem("FaeMaze/Add KeyBindingCapture to Screenshot Button")]
        public static void AddKeyBindingCaptureToScreenshotButton()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find Screenshot_Button or similar
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var t in allTransforms)
            {
                string name = t.name.ToLower();
                if (name.Contains("screenshot") && (name.Contains("button") || name.Contains("capture")))
                {
                    // Check if it already has KeyBindingCapture
                    var existingCapture = t.GetComponent<FaeMaze.UI.KeyBindingCapture>();
                    if (existingCapture != null)
                    {
                        Debug.Log($"{t.name} already has KeyBindingCapture");
                        continue;
                    }

                    Debug.Log($"Adding KeyBindingCapture to {t.name}");

                    // Add KeyBindingCapture component
                    var capture = t.gameObject.AddComponent<FaeMaze.UI.KeyBindingCapture>();

                    // Find and wire up BindingText
                    var bindingText = t.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (bindingText != null)
                    {
                        SerializedObject so = new SerializedObject(capture);
                        SerializedProperty textProp = so.FindProperty("bindingText");
                        if (textProp != null)
                        {
                            textProp.objectReferenceValue = bindingText;
                            so.ApplyModifiedProperties();
                            Debug.Log($"  Wired up BindingText: {bindingText.gameObject.name}");
                        }
                    }

                    // Remove Button component if present (no longer needed)
                    var button = t.GetComponent<Button>();
                    if (button != null)
                    {
                        Object.DestroyImmediate(button);
                        Debug.Log($"  Removed old Button component");
                    }

                    // Add toggle
                    AddToggleToCapture(capture);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Done! Don't forget to save the scene.");
        }
    }
}

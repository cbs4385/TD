using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add missing control binding sections to the Options scene.
    /// Run from menu: FaeMaze > Setup Heart Powers Controls
    /// </summary>
    public static class OptionsControlsSetup
    {
        [MenuItem("FaeMaze/Setup Heart Power Activation Controls")]
        public static void SetupHeartPowerActivationControls()
        {
            // Find the OptionsManager in the scene
            var optionsManager = Object.FindFirstObjectByType<UI.OptionsManager>();
            if (optionsManager == null)
            {
                Debug.LogError("OptionsControlsSetup: OptionsManager not found in scene. Open the Options scene first.");
                return;
            }

            // Find the Controls panel
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsControlsSetup: Canvas not found in scene.");
                return;
            }

            // Find controlsPanel by looking for the panel with sections like CAMERAMOUSESection
            Transform controlsPanel = null;
            var allPanels = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allPanels)
            {
                if (t.name == "ControlsPanel" || t.name == "Controls Panel")
                {
                    controlsPanel = t;
                    break;
                }
            }

            // If not found by name, find by looking for parent of known section
            if (controlsPanel == null)
            {
                foreach (var t in allPanels)
                {
                    if (t.name == "CAMERAMOVEMENTSection" || t.name == "SCULPTMENUSection" ||
                        t.name == "HEARTPOWERSSection" || t.name == "CAMERAMOUSESection")
                    {
                        controlsPanel = t.parent;
                        break;
                    }
                }
            }

            if (controlsPanel == null)
            {
                Debug.LogError("OptionsControlsSetup: Controls panel not found in scene.");
                return;
            }

            Debug.Log($"OptionsControlsSetup: Found Controls panel: {controlsPanel.name}");

            // Check if HEART POWER ACTIVATION section already exists
            foreach (Transform child in controlsPanel)
            {
                if (child.name == "HEARTPOWERACTIVATIONSection")
                {
                    Debug.Log("OptionsControlsSetup: HEART POWER ACTIVATION section already exists.");
                    return;
                }
            }

            // Find a template section to copy structure from
            Transform templateSection = null;
            foreach (Transform child in controlsPanel)
            {
                if (child.name == "CAMERAMOVEMENTSection" || child.name == "SCULPTMENUSection" ||
                    child.name == "HEARTPOWERSSection" || child.name == "CAMERAMOUSESection")
                {
                    templateSection = child;
                    break;
                }
            }

            if (templateSection == null)
            {
                Debug.LogError("OptionsControlsSetup: Could not find template section to copy.");
                return;
            }

            Debug.Log($"OptionsControlsSetup: Using template section: {templateSection.name}");

            // Find a template row with a KeyBindingCapture FIRST - before creating anything
            GameObject templateRow = null;
            var allKeyBindings = controlsPanel.GetComponentsInChildren<UI.KeyBindingCapture>(true);
            if (allKeyBindings.Length > 0)
            {
                templateRow = allKeyBindings[0].transform.parent.gameObject;
            }

            if (templateRow == null)
            {
                Debug.LogError("OptionsControlsSetup: Could not find template key binding row.");
                return;
            }

            Debug.Log($"OptionsControlsSetup: Using template row: {templateRow.name}");

            // Create the HEART POWER ACTIVATION section by copying template
            var activationSection = Object.Instantiate(templateSection.gameObject, controlsPanel);
            activationSection.name = "HEARTPOWERACTIVATIONSection";

            // Position it at the very top (before other sections)
            activationSection.transform.SetAsFirstSibling();

            // Update the header text
            var headerTexts = activationSection.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in headerTexts)
            {
                if (text.gameObject.name.Contains("Header") ||
                    text.text == "SCULPT MENU" || text.text == "CAMERA MOVEMENT" ||
                    text.text == "HEART POWERS" || text.text == "CAMERA MOUSE")
                {
                    text.text = "HEART POWER ACTIVATION";
                    break;
                }
            }

            // Find the content area (where rows are)
            Transform contentPanel = null;
            foreach (Transform child in activationSection.transform)
            {
                if (child.name.Contains("Content") || child.name.Contains("Panel"))
                {
                    if (child.childCount > 0)
                    {
                        contentPanel = child;
                        break;
                    }
                }
            }

            if (contentPanel == null)
            {
                var layoutGroup = activationSection.GetComponentInChildren<VerticalLayoutGroup>();
                if (layoutGroup != null && layoutGroup.transform != activationSection.transform)
                {
                    contentPanel = layoutGroup.transform;
                }
            }

            if (contentPanel == null)
            {
                contentPanel = activationSection.transform;
            }

            Debug.Log($"OptionsControlsSetup: Content panel: {contentPanel.name}");

            // Clear existing rows in the content panel (except the header)
            var childrenToRemove = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in contentPanel)
            {
                if (child.name.Contains("Header"))
                    continue;
                childrenToRemove.Add(child.gameObject);
            }

            foreach (var obj in childrenToRemove)
            {
                Object.DestroyImmediate(obj);
            }

            // Create 4 heart power activation rows with KeyBindingCapture buttons
            string[] powerNames = { "Murmuring Paths (Power 1)", "Heartward Grasp (Power 2)", "Devouring Maw (Power 3)", "Sculpting (Power 4)" };
            string[] buttonNames = { "HeartPower1Activation_Button", "HeartPower2Activation_Button", "HeartPower3Activation_Button", "HeartPower4Activation_Button" };
            string[] defaultKeys = { "1", "2", "3", "4" };

            for (int i = 0; i < 4; i++)
            {
                var row = Object.Instantiate(templateRow, contentPanel);
                row.name = $"HeartPower{i + 1}Activation_Row";

                // Find and update the label
                var labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var label in labels)
                {
                    // Skip the button text (the one that shows the key)
                    var parentButton = label.transform.parent.GetComponent<Button>();
                    var parentKeyCapture = label.transform.parent.GetComponent<UI.KeyBindingCapture>();
                    if (parentButton != null || parentKeyCapture != null)
                    {
                        // This is the button's text - set to default key
                        label.text = defaultKeys[i];
                        continue;
                    }

                    // This should be the row label
                    label.text = powerNames[i];
                }

                // Find and configure the KeyBindingCapture
                var keyCapture = row.GetComponentInChildren<UI.KeyBindingCapture>();
                if (keyCapture != null)
                {
                    keyCapture.gameObject.name = buttonNames[i];

                    // Set the button text to show the default key
                    var buttonText = keyCapture.GetComponentInChildren<TextMeshProUGUI>();
                    if (buttonText != null)
                    {
                        buttonText.text = defaultKeys[i];
                    }
                }

                Undo.RegisterCreatedObjectUndo(row, "Create Heart Power Activation Row");
            }

            Undo.RegisterCreatedObjectUndo(activationSection, "Create Heart Power Activation Section");

            EditorUtility.SetDirty(controlsPanel.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("OptionsControlsSetup: Created HEART POWER ACTIVATION section with 4 key binding rows. Save the scene!");
            Debug.Log("NOTE: You may need to wire up the KeyBindingCapture callbacks in OptionsManager to save these bindings.");
        }

        [MenuItem("FaeMaze/Remove Heart Power Activation Controls")]
        public static void RemoveHeartPowerActivationControls()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsControlsSetup: Canvas not found in scene.");
                return;
            }

            var allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "HEARTPOWERACTIVATIONSection")
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    Debug.Log("OptionsControlsSetup: Removed HEART POWER ACTIVATION section.");
                    return;
                }
            }

            Debug.Log("OptionsControlsSetup: HEART POWER ACTIVATION section not found.");
        }

        // Keep old methods for backwards compatibility but mark them for removal
        [MenuItem("FaeMaze/Setup Heart Powers Controls (Legacy)")]
        public static void SetupHeartPowersControls()
        {
            Debug.LogWarning("OptionsControlsSetup: This menu item is deprecated. Use 'Setup Heart Power Activation Controls' instead.");
            SetupHeartPowerActivationControls();
        }

        [MenuItem("FaeMaze/Remove Heart Powers Controls (Legacy)")]
        public static void RemoveHeartPowersControls()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsControlsSetup: Canvas not found in scene.");
                return;
            }

            var allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "HEARTPOWERSSection")
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    Debug.Log("OptionsControlsSetup: Removed HEART POWERS section (legacy).");
                    return;
                }
            }

            Debug.Log("OptionsControlsSetup: HEART POWERS section not found.");
        }
    }
}

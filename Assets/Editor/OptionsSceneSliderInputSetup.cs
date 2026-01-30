using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add input fields alongside sliders in the Options scene.
    /// Run from menu: FaeMaze > Setup Slider Input Fields
    /// </summary>
    public static class OptionsSceneSliderInputSetup
    {
        [MenuItem("FaeMaze/Setup and Wire Slider Input Fields")]
        public static void SetupAndWireSliderInputFields()
        {
            SetupSliderInputFields();
            WireUpSliderInputFields();
        }

        [MenuItem("FaeMaze/Setup Slider Input Fields")]
        public static void SetupSliderInputFields()
        {
            // Find the OptionsManager in the scene (just to verify we're in the right scene)
            var optionsManager = Object.FindFirstObjectByType<UI.OptionsManager>();
            if (optionsManager == null)
            {
                Debug.LogError("OptionsSceneSliderInputSetup: OptionsManager not found in scene. Open the Options scene first.");
                return;
            }

            Debug.Log($"OptionsSceneSliderInputSetup: Found OptionsManager on '{optionsManager.gameObject.name}'");

            // Find the Canvas - sliders are children of Canvas, not OptionsManager
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsSceneSliderInputSetup: Canvas not found in scene.");
                return;
            }

            Debug.Log($"OptionsSceneSliderInputSetup: Found Canvas on '{canvas.gameObject.name}'");

            // Find all sliders in the scene under the Canvas
            var sliders = canvas.GetComponentsInChildren<Slider>(true);
            Debug.Log($"OptionsSceneSliderInputSetup: Found {sliders.Length} sliders");

            int createdCount = 0;
            int skippedNoParent = 0;
            int skippedHasInput = 0;
            int failedCreate = 0;

            foreach (var slider in sliders)
            {
                // Check if there's already an input field sibling
                var parent = slider.transform.parent;
                if (parent == null)
                {
                    Debug.LogWarning($"Slider '{slider.name}' has no parent, skipping");
                    skippedNoParent++;
                    continue;
                }

                // Look for existing input field in the same parent
                var existingInput = FindInputFieldInParent(parent, slider.name);
                if (existingInput != null)
                {
                    Debug.Log($"Slider '{slider.name}' already has input field '{existingInput.name}'");
                    skippedHasInput++;
                    continue;
                }

                // Create the input field
                Debug.Log($"Creating input field for slider: {slider.name} (parent: {parent.name})");
                var inputField = CreateInputFieldForSlider(slider);
                if (inputField != null)
                {
                    createdCount++;
                    Debug.Log($"Successfully created input field for slider: {slider.name}");
                }
                else
                {
                    Debug.LogWarning($"Failed to create input field for slider: {slider.name}");
                    failedCreate++;
                }
            }

            Debug.Log($"OptionsSceneSliderInputSetup Summary: Created={createdCount}, SkippedNoParent={skippedNoParent}, SkippedHasInput={skippedHasInput}, FailedCreate={failedCreate}");

            if (createdCount > 0)
            {
                EditorUtility.SetDirty(canvas.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                Debug.Log($"OptionsSceneSliderInputSetup: Created {createdCount} input fields. Scene marked dirty - save the scene!");
            }
            else
            {
                Debug.Log("OptionsSceneSliderInputSetup: No new input fields created.");
            }
        }

        private static TMP_InputField FindInputFieldInParent(Transform parent, string sliderName)
        {
            // Look for a slider input field with matching name pattern
            string expectedInputName = sliderName.Replace("_Slider", "") + "_SliderInput";

            foreach (Transform child in parent)
            {
                if (child.name == expectedInputName)
                {
                    var input = child.GetComponent<TMP_InputField>();
                    if (input != null) return input;
                }
            }
            return null;
        }

        private static TMP_InputField CreateInputFieldForSlider(Slider slider)
        {
            var parent = slider.transform.parent;
            if (parent == null) return null;

            // Find the value text (usually next to the slider)
            var valueText = FindValueTextInParent(parent);

            // Determine if this slider needs a unit suffix
            string sliderName = slider.name.ToLower();
            string unitSuffix = "";
            if (sliderName.Contains("interval") || sliderName.Contains("delay") || sliderName.Contains("speed"))
            {
                unitSuffix = "s"; // seconds
            }
            else if (sliderName.Contains("volume") || sliderName.Contains("chance"))
            {
                unitSuffix = "%"; // percentage
            }
            else if (sliderName.Contains("fov") || sliderName.Contains("fieldofview"))
            {
                unitSuffix = "°"; // degrees
            }

            // Create a new GameObject for the input field
            // Use _SliderInput suffix to distinguish from other input fields like randomSeed_Input
            var inputGO = new GameObject(slider.name.Replace("_Slider", "").Replace("Slider", "") + "_SliderInput");
            inputGO.transform.SetParent(parent, false);

            // Add RectTransform
            var rectTransform = inputGO.AddComponent<RectTransform>();

            // Position it in place of the value text (replacing it)
            if (valueText != null)
            {
                var valueTextRect = valueText.GetComponent<RectTransform>();
                var valueTextComponent = valueText.GetComponent<TextMeshProUGUI>();

                // Calculate width for input (leave room for unit suffix)
                float inputWidth = valueTextRect.sizeDelta.x;
                float unitWidth = 0f;
                if (!string.IsNullOrEmpty(unitSuffix))
                {
                    unitWidth = 25f; // Space for unit label
                    inputWidth -= unitWidth;
                }

                // Position input field
                rectTransform.anchorMin = valueTextRect.anchorMin;
                rectTransform.anchorMax = valueTextRect.anchorMax;
                rectTransform.anchoredPosition = valueTextRect.anchoredPosition;
                if (!string.IsNullOrEmpty(unitSuffix))
                {
                    // Shift input left to make room for unit
                    rectTransform.anchoredPosition -= new Vector2(unitWidth / 2f, 0f);
                }
                rectTransform.sizeDelta = new Vector2(inputWidth, valueTextRect.sizeDelta.y);
                rectTransform.pivot = valueTextRect.pivot;

                // Set sibling index to be at the same position in hierarchy
                int siblingIndex = valueText.transform.GetSiblingIndex();
                inputGO.transform.SetSiblingIndex(siblingIndex);

                // Create unit label if needed
                if (!string.IsNullOrEmpty(unitSuffix))
                {
                    var unitLabelGO = new GameObject(slider.name.Replace("_Slider", "").Replace("Slider", "") + "_Unit");
                    unitLabelGO.transform.SetParent(parent, false);
                    var unitRect = unitLabelGO.AddComponent<RectTransform>();
                    unitRect.anchorMin = valueTextRect.anchorMin;
                    unitRect.anchorMax = valueTextRect.anchorMax;
                    unitRect.anchoredPosition = valueTextRect.anchoredPosition + new Vector2((inputWidth + unitWidth) / 2f, 0f);
                    unitRect.sizeDelta = new Vector2(unitWidth, valueTextRect.sizeDelta.y);
                    unitRect.pivot = valueTextRect.pivot;
                    unitLabelGO.transform.SetSiblingIndex(siblingIndex + 1);

                    var unitTextComp = unitLabelGO.AddComponent<TextMeshProUGUI>();
                    unitTextComp.text = unitSuffix;
                    unitTextComp.fontSize = valueTextComponent != null ? valueTextComponent.fontSize : 20f;
                    unitTextComp.alignment = TextAlignmentOptions.Left;
                    unitTextComp.color = valueTextComponent != null ? valueTextComponent.color : Color.white;

                    Undo.RegisterCreatedObjectUndo(unitLabelGO, "Create Unit Label");
                }

                // Hide the original value text (don't destroy - may be referenced by OptionsManager)
                valueText.gameObject.SetActive(false);
                Debug.Log($"Hid value text '{valueText.name}' for slider '{slider.name}'");
            }
            else
            {
                // Default positioning
                rectTransform.anchorMin = new Vector2(1f, 0.5f);
                rectTransform.anchorMax = new Vector2(1f, 0.5f);
                rectTransform.anchoredPosition = new Vector2(-35f, 0f);
                rectTransform.sizeDelta = new Vector2(70f, 30f);
            }

            // Add Image component for background (subtle, semi-transparent)
            var image = inputGO.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);

            // Add TMP_InputField
            var inputField = inputGO.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;

            // Create text area child
            var textAreaGO = new GameObject("Text Area");
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var textAreaRect = textAreaGO.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(5f, 2f);
            textAreaRect.offsetMax = new Vector2(-5f, -2f);

            // Add RectMask2D
            textAreaGO.AddComponent<RectMask2D>();

            // Create text child
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.fontSize = 20f;  // Match existing value text size
            textComponent.alignment = TextAlignmentOptions.Right;  // Right-align like original
            textComponent.color = Color.white;

            // Create placeholder child
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderRect = placeholderGO.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            var placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.fontSize = 20f;
            placeholderText.alignment = TextAlignmentOptions.Right;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.text = "0";

            // Wire up the input field
            inputField.textViewport = textAreaRect;
            inputField.textComponent = textComponent;
            inputField.placeholder = placeholderText;

            // Set navigation to none to prevent keyboard navigation issues
            var nav = inputField.navigation;
            nav.mode = Navigation.Mode.None;
            inputField.navigation = nav;

            Undo.RegisterCreatedObjectUndo(inputGO, "Create Slider Input Field");

            return inputField;
        }

        private static TextMeshProUGUI FindValueTextInParent(Transform parent)
        {
            // Look for a TMP text component that's likely the value display
            foreach (Transform child in parent)
            {
                var text = child.GetComponent<TextMeshProUGUI>();
                if (text != null && !child.GetComponent<TMP_InputField>())
                {
                    // Skip labels (usually on the left, have specific names)
                    string name = child.name.ToLower();
                    if (name.Contains("label") || name.Contains("title"))
                        continue;

                    return text;
                }
            }
            return null;
        }

        [MenuItem("FaeMaze/Wire Up Slider Input Fields")]
        public static void WireUpSliderInputFields()
        {
            var optionsManager = Object.FindFirstObjectByType<UI.OptionsManager>();
            if (optionsManager == null)
            {
                Debug.LogError("OptionsSceneSliderInputSetup: OptionsManager not found in scene.");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsSceneSliderInputSetup: Canvas not found in scene.");
                return;
            }

            // Get the SerializedObject for OptionsManager to set field values
            var serializedObject = new SerializedObject(optionsManager);
            int wiredCount = 0;

            // Map of slider field name prefixes to input field property names
            var sliderToInputMap = new System.Collections.Generic.Dictionary<string, string>
            {
                // Video
                { "FieldofView", "fieldOfViewInput" },
                { "CameraPanSpeed", "cameraPanSpeedInput" },
                { "CameraZoomSpeed", "cameraZoomSpeedInput" },
                { "CameraMinZoom", "cameraMinZoomInput" },
                { "CameraMaxZoom", "cameraMaxZoomInput" },
                { "CameraMovementSpeed", "cameraMovementSpeedInput" },
                // Audio
                { "SFXVolume", "sfxVolumeInput" },
                { "MusicVolume", "musicVolumeInput" },
                { "LanternVolume", "lanternVolumeInput" },
                { "FairyRingVolume", "fairyRingVolumeInput" },
                { "PondVolume", "pondVolumeInput" },
                { "SculptVolume", "sculptVolumeInput" },
                // Gameplay
                { "visitorSpeed", "visitorSpeedInput" },
                { "confusionChance", "confusionChanceInput" },
                { "confusionDistanceMin", "confusionDistanceMinInput" },
                { "confusionDistanceMax", "confusionDistanceMaxInput" },
                { "spawnInterval", "spawnIntervalInput" },
                { "autoStartDelay", "autoStartDelayInput" },
                { "startingEssence", "startingEssenceInput" },
                { "focusSpeed", "focusSpeedInput" },
            };

            // Find all _SliderInput objects in the scene
            var inputFields = canvas.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var input in inputFields)
            {
                if (!input.name.EndsWith("_SliderInput")) continue;

                // Extract the base name (e.g., "spawnInterval" from "spawnInterval_SliderInput")
                string baseName = input.name.Replace("_SliderInput", "");

                // Find matching property
                bool found = false;
                foreach (var kvp in sliderToInputMap)
                {
                    if (baseName.Equals(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
                    {
                        var property = serializedObject.FindProperty(kvp.Value);
                        if (property != null)
                        {
                            property.objectReferenceValue = input;
                            Debug.Log($"Wired '{input.name}' to OptionsManager.{kvp.Value}");
                            wiredCount++;
                        }
                        else
                        {
                            Debug.LogWarning($"Property '{kvp.Value}' not found on OptionsManager");
                        }
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Debug.LogWarning($"No mapping found for input field '{input.name}' (baseName='{baseName}')");
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(optionsManager);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(optionsManager.gameObject.scene);

            Debug.Log($"OptionsSceneSliderInputSetup: Wired {wiredCount} input fields to OptionsManager. Save the scene!");
        }

        [MenuItem("FaeMaze/Remove All Slider Input Fields")]
        public static void RemoveAllSliderInputFields()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("OptionsSceneSliderInputSetup: Canvas not found in scene.");
                return;
            }

            int removedCount = 0;

            // Remove input fields
            var inputFields = canvas.GetComponentsInChildren<TMP_InputField>(true);
            foreach (var input in inputFields)
            {
                // Only remove inputs that are paired with sliders (named *_SliderInput)
                if (input.name.EndsWith("_SliderInput"))
                {
                    Undo.DestroyObjectImmediate(input.gameObject);
                    removedCount++;
                }
            }

            // Remove unit labels
            var allTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in allTexts)
            {
                if (text.name.EndsWith("_Unit"))
                {
                    Undo.DestroyObjectImmediate(text.gameObject);
                    removedCount++;
                }
            }

            // Re-enable hidden value texts
            var sliders = canvas.GetComponentsInChildren<Slider>(true);
            foreach (var slider in sliders)
            {
                var parent = slider.transform.parent;
                if (parent == null) continue;

                foreach (Transform child in parent)
                {
                    // Look for disabled TextMeshProUGUI that isn't a _Unit label
                    if (!child.gameObject.activeSelf && child.GetComponent<TextMeshProUGUI>() != null && !child.name.EndsWith("_Unit"))
                    {
                        child.gameObject.SetActive(true);
                        Debug.Log($"Re-enabled value text '{child.name}'");
                    }
                }
            }

            Debug.Log($"Removed {removedCount} slider input fields and unit labels.");
        }
    }
}

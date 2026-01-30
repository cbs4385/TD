using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add the Light Level slider control to the VIDEO tab in the Options scene.
    /// </summary>
    public class OptionsLightLevelSetup
    {
        [MenuItem("FaeMaze/Setup Options Light Level Control")]
        public static void SetupLightLevelControl()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find the VideoPanel - it's nested under ScrollView > Viewport
            // Note: VideoPanel is inactive by default (tabs system), so we must include inactive objects
            GameObject videoPanel = null;
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name == "VideoPanel")
                {
                    videoPanel = t.gameObject;
                    break;
                }
            }

            if (videoPanel == null)
            {
                Debug.LogError("VideoPanel not found in scene!");
                return;
            }

            // Find the content area within VideoPanel (likely a ScrollView > Viewport > Content)
            Transform contentParent = FindContentParent(videoPanel.transform);
            if (contentParent == null)
            {
                Debug.LogError("Could not find content area in VideoPanel!");
                return;
            }

            // Check if Light Level control already exists
            var existingControl = contentParent.Find("LightLevelRow");
            if (existingControl != null)
            {
                Debug.Log("Light Level control already exists. Updating...");
                Object.DestroyImmediate(existingControl.gameObject);
            }

            // Find an existing row to copy the style from (e.g., CameraMovementSpeedRow or similar)
            Transform templateRow = FindTemplateRow(contentParent);
            if (templateRow == null)
            {
                Debug.LogError("Could not find a template row to copy style from!");
                return;
            }

            // Create the Light Level row by duplicating the template
            GameObject lightLevelRow = Object.Instantiate(templateRow.gameObject, contentParent);
            lightLevelRow.name = "LightLevelRow";

            // Find and update the label, and handle unit text
            var labels = lightLevelRow.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                // Update the main label text
                if (label.gameObject.name.Contains("Label") || label.text.Contains("Speed") || label.text.Contains("Zoom") || label.text.Contains("FOV") || label.text.Contains("Field"))
                {
                    label.text = "Light Level";
                }
                // Remove or hide the unit text (degree symbol doesn't apply to light level)
                else if (label.gameObject.name.Contains("Unit") || label.text == "°")
                {
                    label.text = ""; // Clear the unit text
                }
            }

            // Find and configure the slider
            var slider = lightLevelRow.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 2f;
                slider.value = 0.9f; // Default light level
                slider.wholeNumbers = false;

                // Rename the slider GameObject for clarity
                if (slider.gameObject.name.Contains("Fieldofview") || slider.gameObject.name.Contains("FieldOfView"))
                {
                    slider.gameObject.name = "LightLevel_Slider";
                }
            }

            // Rename child elements to match LightLevel naming convention
            foreach (Transform child in lightLevelRow.transform)
            {
                if (child.name.Contains("Fieldofview") || child.name.Contains("FieldOfView"))
                {
                    child.name = child.name.Replace("Fieldofview", "LightLevel").Replace("FieldOfView", "LightLevel");
                }
            }

            // Find the OptionsManager and wire up the references
            var optionsManager = Object.FindFirstObjectByType<FaeMaze.UI.OptionsManager>(FindObjectsInactive.Include);
            if (optionsManager != null)
            {
                // Use SerializedObject to set the references
                SerializedObject serializedManager = new SerializedObject(optionsManager);

                // Find and set the slider reference
                SerializedProperty lightLevelSliderProp = serializedManager.FindProperty("lightLevelSlider");
                if (lightLevelSliderProp != null && slider != null)
                {
                    lightLevelSliderProp.objectReferenceValue = slider;
                }

                // Find and set the text reference
                SerializedProperty lightLevelTextProp = serializedManager.FindProperty("lightLevelText");
                if (lightLevelTextProp != null)
                {
                    // Find the value text (usually named "Value" or contains the numeric display)
                    foreach (var text in labels)
                    {
                        if (text.gameObject.name.Contains("Value") || text.gameObject.name.Contains("Text"))
                        {
                            if (!text.gameObject.name.Contains("Label"))
                            {
                                lightLevelTextProp.objectReferenceValue = text;
                                text.text = "0.9";
                                break;
                            }
                        }
                    }
                }

                // Find and set the input field reference
                SerializedProperty lightLevelInputProp = serializedManager.FindProperty("lightLevelInput");
                if (lightLevelInputProp != null)
                {
                    var inputField = lightLevelRow.GetComponentInChildren<TMP_InputField>(true);
                    if (inputField != null)
                    {
                        lightLevelInputProp.objectReferenceValue = inputField;
                        inputField.text = "0.9";
                    }
                }

                serializedManager.ApplyModifiedProperties();
            }

            // Position the row appropriately (after camera settings, before any divider)
            int siblingIndex = templateRow.GetSiblingIndex() + 1;
            lightLevelRow.transform.SetSiblingIndex(siblingIndex);

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Light Level control added to VIDEO tab! Don't forget to save the scene.");
        }

        private static Transform FindContentParent(Transform videoPanel)
        {
            // Look for DISPLAYSETTINGSSection which contains video settings like Fullscreen and Resolution
            Transform displaySection = videoPanel.Find("DISPLAYSETTINGSSection");
            if (displaySection != null)
            {
                return displaySection;
            }

            // Look for any section that has children (likely contains settings rows)
            foreach (Transform child in videoPanel)
            {
                if (child.name.Contains("Section") && child.childCount > 0)
                {
                    return child;
                }
            }

            // Fallback: return the panel itself if it has a layout group
            if (videoPanel.GetComponent<VerticalLayoutGroup>() != null)
            {
                return videoPanel.transform;
            }

            return null;
        }

        private static Transform FindTemplateRow(Transform contentParent)
        {
            // First, search within the contentParent (VideoPanel) for any slider row
            foreach (Transform child in contentParent)
            {
                // Check this child and all its descendants for a Slider
                if (child.GetComponentInChildren<Slider>(true) != null)
                {
                    // If this is a section (like DISPLAYSETTINGSSection), search within it
                    foreach (Transform grandchild in child)
                    {
                        if (grandchild.GetComponentInChildren<Slider>(true) != null)
                        {
                            return grandchild;
                        }
                    }
                    // The child itself might be a row with slider
                    return child;
                }
            }

            // If no slider row found in VideoPanel, look in GameplayPanel for a template
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform gameplayPanel = null;
            foreach (var t in allTransforms)
            {
                if (t.name == "GameplayPanel")
                {
                    gameplayPanel = t;
                    break;
                }
            }

            if (gameplayPanel != null)
            {
                // Look for slider rows in GameplayPanel sections
                foreach (Transform section in gameplayPanel)
                {
                    foreach (Transform row in section)
                    {
                        if (row.GetComponentInChildren<Slider>(true) != null)
                        {
                            return row;
                        }
                    }
                }
            }

            return null;
        }
    }
}

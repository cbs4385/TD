using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to convert the Spawn Interval slider to a 3-stop difficulty selector.
    /// </summary>
    public class OptionsDifficultySetup
    {
        [MenuItem("FaeMaze/Setup Difficulty Selector")]
        public static void SetupDifficultySelector()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find the spawn interval row
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform spawnIntervalRow = null;
            foreach (var t in allTransforms)
            {
                if (t.name == "spawnInterval_Row" || t.name == "Difficulty_Row")
                {
                    spawnIntervalRow = t;
                    break;
                }
            }

            if (spawnIntervalRow == null)
            {
                Debug.LogError("spawnInterval_Row not found in scene!");
                return;
            }

            // Find the slider
            var slider = spawnIntervalRow.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                // Configure as 3-stop discrete slider
                slider.wholeNumbers = true;
                slider.minValue = 0;
                slider.maxValue = 2;
                slider.value = 1; // Default to Normal
            }

            // Find the OptionsManager to wire up references
            var optionsManager = Object.FindFirstObjectByType<FaeMaze.UI.OptionsManager>(FindObjectsInactive.Include);
            TextMeshProUGUI difficultyText = null;

            // Find and update the label text
            var labels = spawnIntervalRow.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                Debug.Log($"Found label: {label.gameObject.name} with text: '{label.text}'");

                // Update the row label from "Spawn Interval" to "Difficulty"
                if (label.gameObject.name == "Label" && label.text.Contains("Spawn"))
                {
                    label.text = "Difficulty";
                }
                // Find and update the value display text (spawnInterval_Text)
                else if (label.gameObject.name == "spawnInterval_Text")
                {
                    label.text = "NORMAL";
                    label.gameObject.SetActive(true);  // Ensure it's visible!
                    difficultyText = label;
                }
                // Hide the unit label (the "s" suffix)
                else if (label.gameObject.name.Contains("Unit"))
                {
                    label.text = "";
                    label.gameObject.SetActive(false);
                }
            }

            // Hide the input field (not needed for discrete selection)
            var inputField = spawnIntervalRow.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                inputField.gameObject.SetActive(false);
            }

            // Wire up the OptionsManager references
            if (optionsManager != null)
            {
                SerializedObject serializedManager = new SerializedObject(optionsManager);

                // Wire up the slider
                SerializedProperty sliderProp = serializedManager.FindProperty("spawnIntervalSlider");
                if (sliderProp != null && slider != null)
                {
                    sliderProp.objectReferenceValue = slider;
                }

                // Wire up the text display
                SerializedProperty textProp = serializedManager.FindProperty("spawnIntervalText");
                if (textProp != null && difficultyText != null)
                {
                    textProp.objectReferenceValue = difficultyText;
                }

                serializedManager.ApplyModifiedProperties();
                Debug.Log("OptionsManager references wired up successfully.");
            }
            else
            {
                Debug.LogWarning("OptionsManager not found - references not wired up.");
            }

            // Rename the row for clarity
            spawnIntervalRow.name = "Difficulty_Row";

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Difficulty selector setup complete! Don't forget to save the scene.");
        }
    }
}

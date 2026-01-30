using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add the Camera Forward (Mouse) binding row to the Controls tab.
    /// </summary>
    public class OptionsForwardBindingSetup
    {
        [MenuItem("FaeMaze/Setup Camera Forward Binding")]
        public static void SetupForwardBinding()
        {
            // Make sure Options scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                Debug.LogError("Please open the Options scene first!");
                return;
            }

            // Find the CAMERA MOUSE section in ControlsPanel
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform cameraMouseSection = null;
            Transform orbitRow = null;

            foreach (var t in allTransforms)
            {
                if (t.name.Contains("CameraMouse") && t.name.Contains("Section"))
                {
                    cameraMouseSection = t;
                }
                else if (t.name.Contains("Orbit") && t.name.Contains("Row"))
                {
                    orbitRow = t;
                }
            }

            if (cameraMouseSection == null)
            {
                Debug.LogError("CAMERA MOUSE section not found! Looking for a row to duplicate...");

                // Try to find it by looking for Orbit row's parent
                if (orbitRow != null)
                {
                    cameraMouseSection = orbitRow.parent;
                    Debug.Log($"Using Orbit row's parent: {cameraMouseSection?.name}");
                }
            }

            if (orbitRow == null)
            {
                Debug.LogError("Orbit row not found to use as template!");
                return;
            }

            // Check if Forward row already exists
            foreach (Transform child in cameraMouseSection)
            {
                if (child.name.Contains("Forward"))
                {
                    Debug.Log("Camera Forward row already exists. Skipping creation.");
                    return;
                }
            }

            // Duplicate the Orbit row to create Forward row
            GameObject forwardRow = Object.Instantiate(orbitRow.gameObject, cameraMouseSection);
            forwardRow.name = "CameraMouseForward_Row";

            // Move it to be the first child (before Orbit)
            forwardRow.transform.SetSiblingIndex(orbitRow.GetSiblingIndex());

            // Update the label
            var labels = forwardRow.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var label in labels)
            {
                if (label.gameObject.name.Contains("Label") || label.text.Contains("Orbit"))
                {
                    label.text = "Forward";
                    break;
                }
            }

            // Update the KeyBindingCapture component if present
            var bindingCapture = forwardRow.GetComponentInChildren<FaeMaze.UI.KeyBindingCapture>(true);
            if (bindingCapture != null)
            {
                bindingCapture.SetBinding("Mouse0");

                // Wire up the bindingText reference if needed
                SerializedObject serializedCapture = new SerializedObject(bindingCapture);
                SerializedProperty textProp = serializedCapture.FindProperty("bindingText");
                if (textProp != null && textProp.objectReferenceValue == null)
                {
                    // Find the text that shows the binding value
                    foreach (var label in labels)
                    {
                        if (!label.gameObject.name.Contains("Label"))
                        {
                            textProp.objectReferenceValue = label;
                            break;
                        }
                    }
                    serializedCapture.ApplyModifiedProperties();
                }
            }

            // Also update any button/text that shows the binding
            var buttons = forwardRow.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons)
            {
                var buttonText = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (buttonText != null)
                {
                    buttonText.text = "Left Click";
                }
            }

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Camera Forward binding row added to CAMERA MOUSE section! Run 'FaeMaze > Setup Binding Capture Toggles' next, then save the scene.");
        }
    }
}

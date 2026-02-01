using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add Tutorial button to the Main Menu scene.
    /// Run from menu: FaeMaze > Setup Main Menu Tutorial Button
    /// </summary>
    public static class MainMenuTutorialSetup
    {
        // Button positioning - buttons are spaced 80 pixels apart vertically
        // Original layout: Start(100), Options(0), Exit(-100)
        // New layout: Start(140), Tutorial(60), Options(-20), Exit(-100)
        private const float START_BUTTON_Y = 140f;
        private const float TUTORIAL_BUTTON_Y = 60f;
        private const float OPTIONS_BUTTON_Y = -20f;
        private const float EXIT_BUTTON_Y = -100f;

        // Button dimensions matching existing buttons
        private const float BUTTON_WIDTH = 300f;
        private const float BUTTON_HEIGHT = 60f;

        [MenuItem("FaeMaze/Setup Main Menu Tutorial Button")]
        public static void SetupTutorialButton()
        {
            // Find the Canvas in the scene
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("MainMenuTutorialSetup: Canvas not found in scene. Open the MainMenu scene first.");
                return;
            }

            // Check if Tutorial button already exists
            var existingButton = FindChildByName(canvas.transform, "TutorialButton");
            if (existingButton != null)
            {
                Debug.Log("MainMenuTutorialSetup: Tutorial button already exists. Run 'Remove Main Menu Tutorial Button' first to recreate.");
                return;
            }

            // Find existing buttons
            var startButton = FindChildByName(canvas.transform, "StartGameButton");
            var optionsButton = FindChildByName(canvas.transform, "OptionsButton");
            var exitButton = FindChildByName(canvas.transform, "ExitButton");

            if (startButton == null || optionsButton == null || exitButton == null)
            {
                Debug.LogError("MainMenuTutorialSetup: Could not find all menu buttons (StartGameButton, OptionsButton, ExitButton).");
                return;
            }

            // Reposition existing buttons to make room
            RepositionButton(startButton, START_BUTTON_Y);
            RepositionButton(optionsButton, OPTIONS_BUTTON_Y);
            RepositionButton(exitButton, EXIT_BUTTON_Y);

            // Create the Tutorial button by cloning the Start button
            GameObject tutorialButton = CreateTutorialButton(startButton, canvas.transform);

            // Find or get the MainMenuManager
            var menuManager = Object.FindFirstObjectByType<UI.MainMenuManager>();
            if (menuManager == null)
            {
                Debug.LogError("MainMenuTutorialSetup: MainMenuManager not found. Run 'Setup Main Menu Seed Input' first.");
                return;
            }

            // Wire up the tutorial button to the MainMenuManager
            WireUpTutorialButton(menuManager, tutorialButton);

            EditorUtility.SetDirty(canvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("MainMenuTutorialSetup: Created Tutorial button and repositioned menu buttons. Save the scene!");
        }

        private static void RepositionButton(Transform button, float yPosition)
        {
            if (button == null) return;

            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                Undo.RecordObject(rect, "Reposition Menu Button");
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, yPosition);
                EditorUtility.SetDirty(rect);
            }
        }

        private static GameObject CreateTutorialButton(Transform templateButton, Transform parent)
        {
            // Clone the template button
            GameObject tutorialButton = Object.Instantiate(templateButton.gameObject, parent);
            tutorialButton.name = "TutorialButton";
            Undo.RegisterCreatedObjectUndo(tutorialButton, "Create Tutorial Button");

            // Position the new button
            var rect = tutorialButton.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, TUTORIAL_BUTTON_Y);
            rect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);

            // Update the button text
            var textComponent = tutorialButton.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = "Tutorial";
            }
            else
            {
                // Fallback to legacy Text component
                var legacyText = tutorialButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = "Tutorial";
                }
            }

            // Clear any existing onClick listeners from the cloned button
            var button = tutorialButton.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
            }

            return tutorialButton;
        }

        private static void WireUpTutorialButton(UI.MainMenuManager manager, GameObject tutorialButtonObj)
        {
            // Wire up the serialized field reference
            SerializedObject so = new SerializedObject(manager);
            var tutorialButtonProp = so.FindProperty("tutorialButton");

            var button = tutorialButtonObj.GetComponent<Button>();
            if (tutorialButtonProp != null && button != null)
            {
                tutorialButtonProp.objectReferenceValue = button;
                so.ApplyModifiedProperties();
            }

            // Also wire up the onClick event directly
            if (button != null)
            {
                UnityEventTools.AddPersistentListener(button.onClick, manager.StartTutorial);
                EditorUtility.SetDirty(button);
            }

            Debug.Log("MainMenuTutorialSetup: Wired up Tutorial button to MainMenuManager.StartTutorial()");
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        [MenuItem("FaeMaze/Remove Main Menu Tutorial Button")]
        public static void RemoveTutorialButton()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("MainMenuTutorialSetup: Canvas not found in scene.");
                return;
            }

            // Remove tutorial button
            var tutorialButton = FindChildByName(canvas.transform, "TutorialButton");
            if (tutorialButton != null)
            {
                Undo.DestroyObjectImmediate(tutorialButton.gameObject);
                Debug.Log("MainMenuTutorialSetup: Removed Tutorial button.");
            }

            // Restore original button positions
            var startButton = FindChildByName(canvas.transform, "StartGameButton");
            var optionsButton = FindChildByName(canvas.transform, "OptionsButton");
            var exitButton = FindChildByName(canvas.transform, "ExitButton");

            RepositionButton(startButton, 100f);
            RepositionButton(optionsButton, 0f);
            RepositionButton(exitButton, -100f);

            // Clear the tutorial button reference from MainMenuManager
            var manager = Object.FindFirstObjectByType<UI.MainMenuManager>();
            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                var tutorialButtonProp = so.FindProperty("tutorialButton");
                if (tutorialButtonProp != null)
                {
                    tutorialButtonProp.objectReferenceValue = null;
                    so.ApplyModifiedProperties();
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("MainMenuTutorialSetup: Cleanup complete. Save the scene!");
        }
    }
}

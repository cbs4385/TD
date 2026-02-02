using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to add Tutorial Mode toggle to Main Menu.
    /// This replaces the separate Tutorial button with a checkbox next to Start Game.
    /// </summary>
    public static class MainMenuTutorialToggleSetup
    {
        [MenuItem("FaeMaze/Setup Main Menu Tutorial Toggle")]
        public static void SetupTutorialToggle()
        {
            // Make sure we're in the MainMenu scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("MainMenu"))
            {
                EditorUtility.DisplayDialog("Wrong Scene",
                    "Please open the MainMenu scene first.", "OK");
                return;
            }

            // Find the MainMenuManager
            var mainMenuManager = Object.FindFirstObjectByType<FaeMaze.UI.MainMenuManager>();
            if (mainMenuManager == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find MainMenuManager in scene.", "OK");
                return;
            }

            // Find the StartGameButton
            var startGameButton = GameObject.Find("StartGameButton");
            if (startGameButton == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find StartGameButton in scene.", "OK");
                return;
            }

            // Find and disable the TutorialButton
            var tutorialButton = GameObject.Find("TutorialButton");
            if (tutorialButton != null)
            {
                tutorialButton.SetActive(false);
                Debug.Log("[MainMenuTutorialToggleSetup] Disabled TutorialButton");
            }

            // Adjust OptionsButton and ExitButton positions (move them up since Tutorial button is gone)
            // Original positions: Start=140, Tutorial=60, Options=-20, Exit=-100
            // New positions: Start=140 (with toggle), Options=60, Exit=-20
            var optionsButton = GameObject.Find("OptionsButton");
            if (optionsButton != null)
            {
                RectTransform optRect = optionsButton.GetComponent<RectTransform>();
                if (optRect != null)
                {
                    optRect.anchoredPosition = new Vector2(0, 60);
                    Debug.Log("[MainMenuTutorialToggleSetup] Moved OptionsButton to y=60");
                }
            }

            var exitButton = GameObject.Find("ExitButton");
            if (exitButton != null)
            {
                RectTransform exitRect = exitButton.GetComponent<RectTransform>();
                if (exitRect != null)
                {
                    exitRect.anchoredPosition = new Vector2(0, -20);
                    Debug.Log("[MainMenuTutorialToggleSetup] Moved ExitButton to y=-20");
                }
            }

            // Also adjust the Shrine button if it exists
            var shrineButton = GameObject.Find("ShrineButton");
            if (shrineButton != null)
            {
                RectTransform shrineRect = shrineButton.GetComponent<RectTransform>();
                if (shrineRect != null)
                {
                    // Move shrine button up by 80 (same offset as other buttons)
                    shrineRect.anchoredPosition = new Vector2(shrineRect.anchoredPosition.x, shrineRect.anchoredPosition.y + 80);
                    Debug.Log("[MainMenuTutorialToggleSetup] Adjusted ShrineButton position");
                }
            }

            // Get the parent of StartGameButton
            Transform parent = startGameButton.transform.parent;
            RectTransform startButtonRect = startGameButton.GetComponent<RectTransform>();

            // Create a container for the button + toggle row
            GameObject rowContainer = new GameObject("StartGameRow");
            rowContainer.transform.SetParent(parent, false);
            RectTransform rowRect = rowContainer.AddComponent<RectTransform>();

            // Position the row where StartGameButton was
            rowRect.anchorMin = startButtonRect.anchorMin;
            rowRect.anchorMax = startButtonRect.anchorMax;
            rowRect.anchoredPosition = startButtonRect.anchoredPosition;
            rowRect.sizeDelta = new Vector2(400, 60); // Wider to accommodate toggle
            rowRect.pivot = new Vector2(0.5f, 0.5f);

            // Add HorizontalLayoutGroup for automatic layout
            HorizontalLayoutGroup hlg = rowContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Reparent the StartGameButton to the row
            startGameButton.transform.SetParent(rowContainer.transform, false);
            // Reset its anchored position since it's now in a layout group
            startButtonRect.anchoredPosition = Vector2.zero;

            // IMPORTANT: Wire up the StartGameButton's OnClick to call MainMenuManager.StartGame()
            Button startButton = startGameButton.GetComponent<Button>();
            if (startButton != null)
            {
                // Clear any existing listeners and add the correct one
                startButton.onClick.RemoveAllListeners();

                // Use UnityEvent's AddPersistentListener via SerializedObject
                SerializedObject buttonSO = new SerializedObject(startButton);
                SerializedProperty onClickProp = buttonSO.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                onClickProp.ClearArray();
                onClickProp.InsertArrayElementAtIndex(0);
                SerializedProperty callProp = onClickProp.GetArrayElementAtIndex(0);
                callProp.FindPropertyRelative("m_Target").objectReferenceValue = mainMenuManager;
                callProp.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "FaeMaze.UI.MainMenuManager, Assembly-CSharp";
                callProp.FindPropertyRelative("m_MethodName").stringValue = "StartGame";
                callProp.FindPropertyRelative("m_Mode").intValue = 0; // No argument
                callProp.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly
                buttonSO.ApplyModifiedProperties();
                Debug.Log("[MainMenuTutorialToggleSetup] Wired StartGameButton.OnClick to MainMenuManager.StartGame()");
            }

            // Create the Tutorial Mode toggle
            GameObject toggleObj = CreateTutorialToggle(rowContainer.transform);

            // Wire up the MainMenuManager's serialized field
            SerializedObject so = new SerializedObject(mainMenuManager);

            // Find or add tutorialModeToggle field
            SerializedProperty tutorialToggleProp = so.FindProperty("tutorialModeToggle");
            if (tutorialToggleProp != null)
            {
                Toggle toggle = toggleObj.GetComponent<Toggle>();
                tutorialToggleProp.objectReferenceValue = toggle;
                so.ApplyModifiedProperties();
                Debug.Log("[MainMenuTutorialToggleSetup] Wired tutorialModeToggle to MainMenuManager");
            }
            else
            {
                Debug.LogWarning("[MainMenuTutorialToggleSetup] tutorialModeToggle field not found on MainMenuManager. " +
                    "Make sure to add the field to the script and re-run this setup.");
            }

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.DisplayDialog("Success",
                "Tutorial Mode toggle has been added to the Main Menu.\n\n" +
                "The TutorialButton has been disabled.\n" +
                "StartGameButton now calls MainMenuManager.StartGame().\n" +
                "Remember to save the scene!", "OK");
        }

        private static GameObject CreateTutorialToggle(Transform parent)
        {
            // Create the toggle container
            GameObject toggleContainer = new GameObject("TutorialModeToggle");
            toggleContainer.transform.SetParent(parent, false);

            RectTransform containerRect = toggleContainer.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(180, 60);

            // Add HorizontalLayoutGroup for toggle + label
            HorizontalLayoutGroup hlg = toggleContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(5, 5, 0, 0);

            // Create the actual toggle (checkbox)
            GameObject checkboxObj = new GameObject("Checkbox");
            checkboxObj.transform.SetParent(toggleContainer.transform, false);

            RectTransform checkboxRect = checkboxObj.AddComponent<RectTransform>();
            checkboxRect.sizeDelta = new Vector2(30, 30);

            // Add background image (dark gray)
            Image bgImage = checkboxObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Add LayoutElement for consistent sizing
            LayoutElement checkboxLayout = checkboxObj.AddComponent<LayoutElement>();
            checkboxLayout.preferredWidth = 30;
            checkboxLayout.preferredHeight = 30;

            // Create the checkmark child
            GameObject checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(checkboxObj.transform, false);

            RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            // Anchor to fill parent with small margin (0.1 to 0.9)
            checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            checkmarkRect.sizeDelta = Vector2.zero;

            // Add checkmark image (blue when checked - like Enable Red Cap)
            Image checkmarkImage = checkmarkObj.AddComponent<Image>();
            checkmarkImage.color = new Color(0.3f, 0.6f, 0.9f, 1f);
            checkmarkObj.AddComponent<CanvasRenderer>();

            // Add Toggle component to checkbox
            Toggle toggle = checkboxObj.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = false; // Default to OFF (normal game mode)
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;

            // Add CanvasRenderer to checkbox
            checkboxObj.AddComponent<CanvasRenderer>();

            // Create the label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleContainer.transform, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(130, 30);

            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Tutorial";
            labelText.fontSize = 20;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = Color.white;

            // Add LayoutElement for label
            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 130;
            labelLayout.preferredHeight = 30;

            labelObj.AddComponent<CanvasRenderer>();

            Debug.Log("[MainMenuTutorialToggleSetup] Created TutorialModeToggle");
            return checkboxObj; // Return the checkbox which has the Toggle component
        }
    }
}

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;

namespace FaeMaze.Editor
{
    public class GameOverSceneSetup
    {
        [MenuItem("FaeMaze/Setup GameOver Scene")]
        public static void SetupGameOverScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create Camera
            GameObject cameraObj = SceneSetupUtilities.CreateCamera(new Color(0.1f, 0.1f, 0.15f, 1f));

            // Create EventSystem
            GameObject eventSystemObj = SceneSetupUtilities.CreateEventSystem();

            // Create Canvas
            GameObject canvasObj = SceneSetupUtilities.CreateCanvas();

            // Create main panel
            GameObject mainPanelObj = SceneSetupUtilities.CreateUIObject("MainPanel", canvasObj.transform);
            RectTransform mainRect = mainPanelObj.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.2f, 0.15f);
            mainRect.anchorMax = new Vector2(0.8f, 0.85f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            Image mainBg = mainPanelObj.AddComponent<Image>();
            mainBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Title
            GameObject titleObj = SceneSetupUtilities.CreateTextMeshPro("Title", mainPanelObj.transform, "GAME OVER", 72, TextAlignmentOptions.Center);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(40, -20);
            titleRect.offsetMax = new Vector2(-40, -20);
            titleObj.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.3f, 0.3f, 1f);

            // Stats container
            GameObject statsContainerObj = SceneSetupUtilities.CreateUIObject("StatsContainer", mainPanelObj.transform);
            RectTransform statsRect = statsContainerObj.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.1f, 0.25f);
            statsRect.anchorMax = new Vector2(0.9f, 0.80f);
            statsRect.offsetMin = Vector2.zero;
            statsRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup statsLayout = statsContainerObj.AddComponent<VerticalLayoutGroup>();
            statsLayout.spacing = 30;
            statsLayout.padding = new RectOffset(40, 40, 40, 40);
            statsLayout.childControlHeight = false;
            statsLayout.childControlWidth = true;
            statsLayout.childForceExpandHeight = false;
            statsLayout.childForceExpandWidth = true;
            statsLayout.childAlignment = TextAnchor.UpperCenter;

            // Max Wave stat
            GameObject maxWaveObj = CreateStatRow(statsContainerObj.transform, "Max Wave Reached: 0");
            TextMeshProUGUI maxWaveText = maxWaveObj.GetComponent<TextMeshProUGUI>();

            // Visitors Consumed stat
            GameObject visitorsObj = CreateStatRow(statsContainerObj.transform, "Visitors Consumed: 0");
            TextMeshProUGUI visitorsText = visitorsObj.GetComponent<TextMeshProUGUI>();

            // Total Time stat
            GameObject timeObj = CreateStatRow(statsContainerObj.transform, "Total Time: 00:00");
            TextMeshProUGUI timeText = timeObj.GetComponent<TextMeshProUGUI>();

            // Props Placed stat
            GameObject propsObj = CreateStatRow(statsContainerObj.transform, "Props Placed:\nNo props placed");
            TextMeshProUGUI propsText = propsObj.GetComponent<TextMeshProUGUI>();
            propsText.alignment = TextAlignmentOptions.Top;
            RectTransform propsRect = propsObj.GetComponent<RectTransform>();
            propsRect.sizeDelta = new Vector2(0, 200);

            LayoutElement propsElement = propsObj.AddComponent<LayoutElement>();
            propsElement.minHeight = 150;
            propsElement.preferredHeight = 200;

            // Button container
            GameObject buttonContainerObj = SceneSetupUtilities.CreateUIObject("ButtonContainer", mainPanelObj.transform);
            RectTransform buttonRect = buttonContainerObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.25f, 0.05f);
            buttonRect.anchorMax = new Vector2(0.75f, 0.18f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // Main Menu button
            Button mainMenuButton = SceneSetupUtilities.CreateButtonWithTextMeshPro("MainMenuButton", buttonContainerObj.transform, "Return to Main Menu", new Color(0.3f, 0.5f, 0.8f), 32);
            RectTransform buttonRectTransform = mainMenuButton.GetComponent<RectTransform>();
            buttonRectTransform.anchorMin = Vector2.zero;
            buttonRectTransform.anchorMax = Vector2.one;
            buttonRectTransform.offsetMin = Vector2.zero;
            buttonRectTransform.offsetMax = Vector2.zero;

            // Create GameOverManager
            GameObject gameOverManagerObj = new GameObject("GameOverManager");
            GameOverManager gameOverManager = gameOverManagerObj.AddComponent<GameOverManager>();

            // Wire up references using SerializedObject
            SerializedObject managerSO = new SerializedObject(gameOverManager);
            managerSO.FindProperty("maxWaveText").objectReferenceValue = maxWaveText;
            managerSO.FindProperty("visitorsConsumedText").objectReferenceValue = visitorsText;
            managerSO.FindProperty("totalTimeText").objectReferenceValue = timeText;
            managerSO.FindProperty("propsPlacedText").objectReferenceValue = propsText;
            managerSO.FindProperty("mainMenuButton").objectReferenceValue = mainMenuButton;
            managerSO.ApplyModifiedProperties();

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameOver.unity");
        }

        /// <summary>
        /// Creates a stat row with text. This is specific to the GameOver scene UI structure.
        /// </summary>
        private static GameObject CreateStatRow(Transform parent, string text)
        {
            GameObject statObj = SceneSetupUtilities.CreateTextMeshPro("StatRow", parent, text, 36, TextAlignmentOptions.Center);

            RectTransform statRect = statObj.GetComponent<RectTransform>();
            statRect.sizeDelta = new Vector2(0, 60);

            LayoutElement statElement = statObj.AddComponent<LayoutElement>();
            statElement.minHeight = 50;
            statElement.preferredHeight = 60;

            return statObj;
        }
    }
}

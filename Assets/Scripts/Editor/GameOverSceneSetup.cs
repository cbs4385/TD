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
            GameObject cameraObj = new GameObject("Main Camera");
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
            camera.orthographic = false;
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();

            // Create EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // Create Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create main panel
            GameObject mainPanelObj = CreateUIObject("MainPanel", canvasObj.transform);
            RectTransform mainRect = mainPanelObj.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.2f, 0.15f);
            mainRect.anchorMax = new Vector2(0.8f, 0.85f);
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            Image mainBg = mainPanelObj.AddComponent<Image>();
            mainBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);

            // Title
            GameObject titleObj = CreateText("Title", mainPanelObj.transform, "GAME OVER", 72, TextAlignmentOptions.Center);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.85f);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.offsetMin = new Vector2(40, -20);
            titleRect.offsetMax = new Vector2(-40, -20);
            titleObj.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.3f, 0.3f, 1f);

            // Stats container
            GameObject statsContainerObj = CreateUIObject("StatsContainer", mainPanelObj.transform);
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
            GameObject buttonContainerObj = CreateUIObject("ButtonContainer", mainPanelObj.transform);
            RectTransform buttonRect = buttonContainerObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.25f, 0.05f);
            buttonRect.anchorMax = new Vector2(0.75f, 0.18f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            // Main Menu button
            Button mainMenuButton = CreateButton("MainMenuButton", buttonContainerObj.transform, "Return to Main Menu", new Color(0.3f, 0.5f, 0.8f));
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

            Debug.Log("[GameOverSceneSetup] GameOver scene created successfully at Assets/Scenes/GameOver.unity");
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }

        private static GameObject CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObj = CreateUIObject(name, parent);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tmp.font = TMP_FontAsset.CreateFontAsset(font);
            }

            return textObj;
        }

        private static GameObject CreateStatRow(Transform parent, string text)
        {
            GameObject statObj = CreateText("StatRow", parent, text, 36, TextAlignmentOptions.Center);

            RectTransform statRect = statObj.GetComponent<RectTransform>();
            statRect.sizeDelta = new Vector2(0, 60);

            LayoutElement statElement = statObj.AddComponent<LayoutElement>();
            statElement.minHeight = 50;
            statElement.preferredHeight = 60;

            return statObj;
        }

        private static Button CreateButton(string name, Transform parent, string text, Color color)
        {
            GameObject buttonObj = CreateUIObject(name, parent);
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color;

            Button button = buttonObj.AddComponent<Button>();

            GameObject textObj = CreateText("Text", buttonObj.transform, text, 32, TextAlignmentOptions.Center);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }
    }
}

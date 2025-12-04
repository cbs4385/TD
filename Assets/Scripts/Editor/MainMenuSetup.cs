using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using FaeMaze.UI;

namespace FaeMaze.Editor
{
    public class MainMenuSetup : MonoBehaviour
    {
        [MenuItem("FaeMaze/Setup Main Menu Scene")]
        public static void SetupMainMenuScene()
        {
            // Load or create the MainMenu scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            // Create MenuManager GameObject
            GameObject menuManagerObj = new GameObject("MenuManager");
            menuManagerObj.transform.SetParent(canvasObj.transform, false);
            RectTransform menuManagerRect = menuManagerObj.AddComponent<RectTransform>();
            menuManagerRect.anchoredPosition = Vector2.zero;
            menuManagerRect.sizeDelta = new Vector2(100, 100);

            MenuManager menuManager = menuManagerObj.AddComponent<MenuManager>();
            menuManagerObj.AddComponent<SceneLoader>();
            menuManagerObj.AddComponent<ButtonTextInitializer>();

            // Create Start Game Button
            Button startButton = CreateButton("StartGameButton", new Vector2(0, 100), new Vector2(300, 60), "Start Game", Color.green, canvasObj.transform);

            // Create Options Button
            Button optionsButton = CreateButton("OptionsButton", new Vector2(0, 0), new Vector2(300, 60), "Options", new Color(0.2f, 0.4f, 0.8f), canvasObj.transform);

            // Create Exit Button
            Button exitButton = CreateButton("ExitButton", new Vector2(0, -100), new Vector2(300, 60), "Exit", new Color(0.8f, 0.2f, 0.2f), canvasObj.transform);

            // Assign buttons to MenuManager
            SerializedObject so = new SerializedObject(menuManager);
            so.FindProperty("startGameButton").objectReferenceValue = startButton;
            so.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            so.FindProperty("exitButton").objectReferenceValue = exitButton;
            so.ApplyModifiedProperties();

            // Save the scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");

            UnityEngine.Debug.Log("[MainMenuSetup] MainMenu scene created successfully!");
        }

        private static Button CreateButton(string name, Vector2 position, Vector2 size, string text, Color buttonColor, Transform parent)
        {
            // Create button GameObject
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            Image image = buttonObj.AddComponent<Image>();
            image.color = buttonColor;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Create text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            Text textComponent = textObj.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 24;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = new Color(0.196f, 0.196f, 0.196f);

            return button;
        }
    }
}

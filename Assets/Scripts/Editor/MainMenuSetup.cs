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
            GameObject canvasObj = SceneSetupUtilities.CreateCanvas();

            // Create EventSystem
            GameObject eventSystemObj = SceneSetupUtilities.CreateEventSystem();

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
            Button startButton = SceneSetupUtilities.CreateButtonWithLegacyText(
                "StartGameButton", canvasObj.transform, "Start Game", Color.green,
                new Vector2(0, 100), new Vector2(300, 60));

            // Create Options Button
            Button optionsButton = SceneSetupUtilities.CreateButtonWithLegacyText(
                "OptionsButton", canvasObj.transform, "Options", new Color(0.2f, 0.4f, 0.8f),
                new Vector2(0, 0), new Vector2(300, 60));

            // Create Exit Button
            Button exitButton = SceneSetupUtilities.CreateButtonWithLegacyText(
                "ExitButton", canvasObj.transform, "Exit", new Color(0.8f, 0.2f, 0.2f),
                new Vector2(0, -100), new Vector2(300, 60));

            // Assign buttons to MenuManager
            SerializedObject so = new SerializedObject(menuManager);
            so.FindProperty("startGameButton").objectReferenceValue = startButton;
            so.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            so.FindProperty("exitButton").objectReferenceValue = exitButton;
            so.ApplyModifiedProperties();

            // Save the scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }
    }
}

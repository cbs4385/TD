using UnityEngine;
using UnityEditor;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add TutorialManager to the game scene.
    /// This is optional - GameController will auto-create TutorialManager if needed.
    /// Run from menu: FaeMaze > Setup Game Scene Tutorial
    /// </summary>
    public static class GameSceneTutorialSetup
    {
        [MenuItem("FaeMaze/Setup Game Scene Tutorial")]
        public static void SetupTutorialManager()
        {
            // Check if TutorialManager already exists
            var existingManager = Object.FindFirstObjectByType<Tutorial.TutorialManager>();
            if (existingManager != null)
            {
                Debug.Log("GameSceneTutorialSetup: TutorialManager already exists in scene.");
                return;
            }

            // Find the GameController to parent the TutorialManager near it
            var gameController = Object.FindFirstObjectByType<Systems.GameController>();
            if (gameController == null)
            {
                Debug.LogError("GameSceneTutorialSetup: GameController not found in scene. Open the game scene (PlanarForestMazeScene) first.");
                return;
            }

            // Create TutorialManager GameObject
            GameObject tutorialGO = new GameObject("TutorialManager");
            Undo.RegisterCreatedObjectUndo(tutorialGO, "Create TutorialManager");

            // Add required components
            var manager = tutorialGO.AddComponent<Tutorial.TutorialManager>();
            tutorialGO.AddComponent<Tutorial.TutorialEventTriggers>();
            // TutorialUIController and TutorialVisitorSpawner are auto-created by TutorialManager.Start()

            EditorUtility.SetDirty(tutorialGO);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorialGO.scene);

            Debug.Log("GameSceneTutorialSetup: Created TutorialManager in game scene. Save the scene!");
            Debug.Log("Note: TutorialUIController and TutorialVisitorSpawner will be auto-added at runtime.");
        }

        [MenuItem("FaeMaze/Remove Game Scene Tutorial")]
        public static void RemoveTutorialManager()
        {
            var manager = Object.FindFirstObjectByType<Tutorial.TutorialManager>();
            if (manager != null)
            {
                Undo.DestroyObjectImmediate(manager.gameObject);
                Debug.Log("GameSceneTutorialSetup: Removed TutorialManager from scene.");
            }
            else
            {
                Debug.Log("GameSceneTutorialSetup: No TutorialManager found in scene.");
            }
        }

        [MenuItem("FaeMaze/Reset Tutorial Progress")]
        public static void ResetTutorialProgress()
        {
            Tutorial.TutorialManager.ResetTutorial();
            Debug.Log("GameSceneTutorialSetup: Tutorial progress has been reset. Tutorial will show on next game start.");
        }
    }
}

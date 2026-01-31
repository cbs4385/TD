using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Tutorial;

namespace FaeMaze.UI
{
    public class EscapeHandler : MonoBehaviour
    {
        private SceneLoader sceneLoader;
        private InGameHelpOverlay helpOverlay;

        private void Awake()
        {
            sceneLoader = GetComponent<SceneLoader>();
            if (sceneLoader == null)
            {
                sceneLoader = gameObject.AddComponent<SceneLoader>();
            }
        }

        private void Start()
        {
            // Create help overlay if it doesn't exist
            helpOverlay = FindFirstObjectByType<InGameHelpOverlay>();
            if (helpOverlay == null)
            {
                var helpGO = new GameObject("InGameHelpOverlay");
                helpOverlay = helpGO.AddComponent<InGameHelpOverlay>();
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            // F1 toggles help overlay
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                if (helpOverlay != null)
                {
                    helpOverlay.Toggle();
                }
                return; // Don't process ESC if F1 was pressed
            }

            // ESC closes help overlay first, then returns to menu
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // If help overlay is open, close it instead of going to menu
                if (helpOverlay != null && helpOverlay.IsVisible)
                {
                    helpOverlay.Hide();
                    return;
                }

                // If tutorial is active, skip it
                var tutorialManager = TutorialManager.Instance;
                if (tutorialManager != null && tutorialManager.IsActive)
                {
                    tutorialManager.SkipTutorial();
                    return;
                }

                sceneLoader.LoadMainMenu();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to set up the UseFixedSeedToggle in the MainMenu scene with proper visuals.
    /// Matches the style used in the Options scene toggles.
    /// </summary>
    public class MainMenuToggleSetup
    {
        [MenuItem("FaeMaze/Setup Main Menu Seed Toggle")]
        public static void SetupSeedToggle()
        {
            // Make sure MainMenu scene is open
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("MainMenu"))
            {
                Debug.LogError("Please open the MainMenu scene first!");
                return;
            }

            // Find the UseFixedSeedToggle
            var toggle = GameObject.Find("UseFixedSeedToggle");
            if (toggle == null)
            {
                Debug.LogError("UseFixedSeedToggle not found in scene!");
                return;
            }

            // Get the Toggle component
            var toggleComponent = toggle.GetComponent<Toggle>();
            if (toggleComponent == null)
            {
                Debug.LogError("Toggle component not found!");
                return;
            }

            // Get or create the background Image - match Options scene style
            var backgroundImage = toggle.GetComponent<Image>();
            if (backgroundImage != null)
            {
                // No sprite, just solid color - matches Options scene
                backgroundImage.sprite = null;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray background (same as Options)
            }

            // Find or create checkmark child
            var checkmark = toggle.transform.Find("Checkmark");
            if (checkmark != null)
            {
                var checkmarkImage = checkmark.GetComponent<Image>();
                if (checkmarkImage != null)
                {
                    // No sprite, just solid blue color - matches Options scene exactly
                    checkmarkImage.sprite = null;
                    checkmarkImage.type = Image.Type.Simple;
                    checkmarkImage.color = new Color(0.3f, 0.6f, 0.9f, 1f); // Blue checkmark (same as Options: 0.3, 0.6, 0.9)
                    checkmarkImage.preserveAspect = false;
                }

                // Set the checkmark's graphic for the toggle
                toggleComponent.graphic = checkmark.GetComponent<Graphic>();

                // Fix checkmark RectTransform - anchors at 0.1-0.9 like Options scene
                var checkmarkRect = checkmark.GetComponent<RectTransform>();
                if (checkmarkRect != null)
                {
                    checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
                    checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
                    checkmarkRect.anchoredPosition = Vector2.zero;
                    checkmarkRect.sizeDelta = Vector2.zero;
                }
            }

            // Set the toggle's target graphic (background)
            toggleComponent.targetGraphic = backgroundImage;

            // Fix the RectTransform sizing - 30x0 like Options scene (height controlled by layout)
            var rectTransform = toggle.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(30, 0);
            }

            // Add LayoutElement to ensure proper sizing in layout group - matches Options scene
            var layoutElement = toggle.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = toggle.AddComponent<LayoutElement>();
            }
            layoutElement.minWidth = -1;
            layoutElement.minHeight = -1;
            layoutElement.preferredWidth = 30;
            layoutElement.preferredHeight = 30;
            layoutElement.flexibleWidth = -1;
            layoutElement.flexibleHeight = -1;
            layoutElement.layoutPriority = 1;

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("Main Menu seed toggle setup complete! Style now matches Options scene. Don't forget to save the scene.");
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor script to fix the styling of toggles in the Options scene.
    /// Makes "Show Tutorial on First Run" match the "Enable Red Cap" style.
    /// </summary>
    public static class OptionsToggleStyleFix
    {
        [MenuItem("FaeMaze/Fix Options Toggle Styles")]
        public static void FixToggleStyles()
        {
            // Make sure we're in the Options scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Options"))
            {
                EditorUtility.DisplayDialog("Wrong Scene",
                    "Please open the Options scene first.", "OK");
                return;
            }

            int fixedCount = 0;

            // Find the showTutorial_Toggle
            var showTutorialToggle = GameObject.Find("showTutorial_Toggle");
            if (showTutorialToggle != null)
            {
                if (FixToggleStyle(showTutorialToggle, "Show Tutorial on First Run"))
                {
                    fixedCount++;
                }
            }
            else
            {
                Debug.LogWarning("[OptionsToggleStyleFix] Could not find showTutorial_Toggle");
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("Success",
                    $"Fixed {fixedCount} toggle style(s).\n\nRemember to save the scene!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("No Changes",
                    "No toggles needed fixing or toggles were not found.", "OK");
            }
        }

        private static bool FixToggleStyle(GameObject toggleObj, string toggleName)
        {
            // Get the Toggle component
            Toggle toggle = toggleObj.GetComponent<Toggle>();
            if (toggle == null)
            {
                Debug.LogWarning($"[OptionsToggleStyleFix] {toggleName}: No Toggle component found");
                return false;
            }

            // Get or add the background Image
            Image bgImage = toggleObj.GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = toggleObj.AddComponent<Image>();
            }

            // Set background to dark gray (like Enable Red Cap: 0.2, 0.2, 0.2)
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            toggle.targetGraphic = bgImage;

            // Find or create the Checkmark child
            Transform checkmarkTransform = toggleObj.transform.Find("Checkmark");
            GameObject checkmarkObj;

            if (checkmarkTransform == null)
            {
                // Create the checkmark
                checkmarkObj = new GameObject("Checkmark");
                checkmarkObj.transform.SetParent(toggleObj.transform, false);

                RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
                // Anchor to fill parent with small margin (0.1 to 0.9) - like Enable Red Cap
                checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
                checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
                checkmarkRect.anchoredPosition = Vector2.zero;
                checkmarkRect.sizeDelta = Vector2.zero;

                checkmarkObj.AddComponent<CanvasRenderer>();
            }
            else
            {
                checkmarkObj = checkmarkTransform.gameObject;

                // Fix the checkmark rect anchors
                RectTransform checkmarkRect = checkmarkObj.GetComponent<RectTransform>();
                if (checkmarkRect != null)
                {
                    checkmarkRect.anchorMin = new Vector2(0.1f, 0.1f);
                    checkmarkRect.anchorMax = new Vector2(0.9f, 0.9f);
                    checkmarkRect.anchoredPosition = Vector2.zero;
                    checkmarkRect.sizeDelta = Vector2.zero;
                }
            }

            // Get or add the checkmark Image
            Image checkmarkImage = checkmarkObj.GetComponent<Image>();
            if (checkmarkImage == null)
            {
                checkmarkImage = checkmarkObj.AddComponent<Image>();
            }

            // Set checkmark to blue (like Enable Red Cap: 0.3, 0.6, 0.9)
            checkmarkImage.color = new Color(0.3f, 0.6f, 0.9f, 1f);

            // Wire up the toggle graphic
            toggle.graphic = checkmarkImage;
            toggle.toggleTransition = Toggle.ToggleTransition.Fade;

            // Add LayoutElement if not present (for consistent 30x30 size)
            LayoutElement layoutElement = toggleObj.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = toggleObj.AddComponent<LayoutElement>();
            }
            layoutElement.preferredWidth = 30;
            layoutElement.preferredHeight = 30;

            // Fix the RectTransform size
            RectTransform toggleRect = toggleObj.GetComponent<RectTransform>();
            if (toggleRect != null)
            {
                toggleRect.sizeDelta = new Vector2(30, 0); // Width 30, height from layout
            }

            Debug.Log($"[OptionsToggleStyleFix] Fixed {toggleName} toggle style");
            return true;
        }
    }
}

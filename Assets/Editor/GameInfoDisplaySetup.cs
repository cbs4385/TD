using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add seed and FPS display to the game scene.
    /// Run from menu: FaeMaze > Setup Game Info Display
    /// </summary>
    public static class GameInfoDisplaySetup
    {
        private static readonly Color TEXT_COLOR = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color BACKGROUND_COLOR = new Color(0f, 0f, 0f, 0.5f);
        private const float FONT_SIZE = 16f;

        [MenuItem("FaeMaze/Setup Game Info Display")]
        public static void SetupGameInfoDisplay()
        {
            // Find the Canvas in the scene
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("GameInfoDisplaySetup: Canvas not found in scene. Open the PlanarForestMazeScene first.");
                return;
            }

            // Check if GameInfoDisplay already exists
            var existingDisplay = Object.FindFirstObjectByType<UI.GameInfoDisplay>();
            if (existingDisplay != null)
            {
                Debug.Log("GameInfoDisplaySetup: GameInfoDisplay already exists. Run 'Remove Game Info Display' first to recreate.");
                return;
            }

            // Create the GameInfoDisplay object
            GameObject displayObj = new GameObject("GameInfoDisplay");
            displayObj.transform.SetParent(canvas.transform, false);
            var display = displayObj.AddComponent<UI.GameInfoDisplay>();

            // Create seed text (top-left)
            GameObject seedTextObj = CreateTextElement(canvas.transform, "SeedText",
                TextAnchor.UpperLeft, new Vector2(10f, -10f), new Vector2(200f, 30f));
            var seedText = seedTextObj.GetComponent<TextMeshProUGUI>();
            seedText.text = "Seed: 0";

            // Create FPS text (bottom-right, above the time display)
            GameObject fpsTextObj = CreateTextElement(canvas.transform, "FPSText",
                TextAnchor.LowerRight, new Vector2(-10f, 80f), new Vector2(100f, 30f));
            var fpsText = fpsTextObj.GetComponent<TextMeshProUGUI>();
            fpsText.text = "60 FPS";
            fpsText.alignment = TextAlignmentOptions.Right;

            // Wire up references
            SerializedObject so = new SerializedObject(display);
            so.FindProperty("seedText").objectReferenceValue = seedText;
            so.FindProperty("fpsText").objectReferenceValue = fpsText;
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(displayObj, "Create GameInfoDisplay");
            Undo.RegisterCreatedObjectUndo(seedTextObj, "Create Seed Text");
            Undo.RegisterCreatedObjectUndo(fpsTextObj, "Create FPS Text");

            EditorUtility.SetDirty(canvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("GameInfoDisplaySetup: Created seed and FPS display. Save the scene!");
        }

        private static GameObject CreateTextElement(Transform parent, string name,
            TextAnchor anchor, Vector2 position, Vector2 size)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();

            // Set anchors based on alignment
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case TextAnchor.LowerRight:
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    break;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            // Add text component
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = FONT_SIZE;
            text.color = TEXT_COLOR;
            text.alignment = anchor == TextAnchor.UpperLeft ?
                TextAlignmentOptions.TopLeft : TextAlignmentOptions.BottomRight;

            // Add background for readability
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(textObj.transform, false);
            bgObj.transform.SetAsFirstSibling(); // Behind text

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(-5f, -2f);
            bgRect.offsetMax = new Vector2(5f, 2f);

            Image bg = bgObj.AddComponent<Image>();
            bg.color = BACKGROUND_COLOR;

            return textObj;
        }

        [MenuItem("FaeMaze/Remove Game Info Display")]
        public static void RemoveGameInfoDisplay()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("GameInfoDisplaySetup: Canvas not found in scene.");
                return;
            }

            // Remove GameInfoDisplay component
            var display = Object.FindFirstObjectByType<UI.GameInfoDisplay>();
            if (display != null)
            {
                Undo.DestroyObjectImmediate(display.gameObject);
                Debug.Log("GameInfoDisplaySetup: Removed GameInfoDisplay.");
            }

            // Remove text elements by name
            var allTransforms = canvas.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t.name == "SeedText" || t.name == "FPSText")
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    Debug.Log($"GameInfoDisplaySetup: Removed {t.name}.");
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("GameInfoDisplaySetup: Cleanup complete. Save the scene!");
        }
    }
}

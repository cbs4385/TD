using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace FaeMaze.Editor
{
    /// <summary>
    /// Editor utility to add RNG seed input controls to the Main Menu scene.
    /// Run from menu: FaeMaze > Setup Main Menu Seed Input
    /// </summary>
    public static class MainMenuSeedSetup
    {
        // Style constants matching Options page
        private static readonly Color PANEL_BACKGROUND_COLOR = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        private static readonly Color INPUT_BACKGROUND_COLOR = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color TOGGLE_BACKGROUND_COLOR = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color BUTTON_NORMAL_COLOR = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color BUTTON_HOVER_COLOR = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color BUTTON_PRESSED_COLOR = new Color(0.15f, 0.15f, 0.15f, 1f);
        private const float LABEL_FONT_SIZE = 20f;
        private const float INPUT_FONT_SIZE = 18f;

        [MenuItem("FaeMaze/Setup Main Menu Seed Input")]
        public static void SetupSeedInput()
        {
            // Find the Canvas in the scene
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("MainMenuSeedSetup: Canvas not found in scene. Open the MainMenu scene first.");
                return;
            }

            // Check if MainMenuManager already exists
            var existingManager = Object.FindFirstObjectByType<UI.MainMenuManager>();
            if (existingManager != null)
            {
                Debug.Log("MainMenuSeedSetup: MainMenuManager already exists. Checking for UI elements...");
            }

            // Check if seed panel already exists
            var existingPanel = FindChildByName(canvas.transform, "SeedPanel");
            if (existingPanel != null)
            {
                Debug.Log("MainMenuSeedSetup: Seed panel already exists. Run 'Remove Main Menu Seed Input' first to recreate.");
                return;
            }

            // Always parent to Canvas directly for proper positioning at top of screen
            Debug.Log($"MainMenuSeedSetup: Creating seed panel under Canvas");

            // Create the seed panel - parent to Canvas, position at top center
            GameObject seedPanel = CreateSeedPanel(canvas.transform);

            // Create MainMenuManager if it doesn't exist
            if (existingManager == null)
            {
                GameObject managerObj = new GameObject("MainMenuManager");
                managerObj.transform.SetParent(canvas.transform);
                existingManager = managerObj.AddComponent<UI.MainMenuManager>();
                Undo.RegisterCreatedObjectUndo(managerObj, "Create MainMenuManager");
            }

            // Wire up the references using SerializedObject
            WireUpReferences(existingManager, seedPanel);

            EditorUtility.SetDirty(canvas.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            Debug.Log("MainMenuSeedSetup: Created seed input panel. Save the scene!");
        }

        private static GameObject CreateSeedPanel(Transform parent)
        {
            // Create main panel
            GameObject panel = new GameObject("SeedPanel");
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            // Position at top center of screen
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -15f); // 15 pixels from top
            panelRect.sizeDelta = new Vector2(280f, 50f);

            // Add background image
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = PANEL_BACKGROUND_COLOR;

            // Add horizontal layout for compact design
            HorizontalLayoutGroup hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 12, 8, 8);
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Create toggle (checkbox)
            CreateToggle(panel.transform);

            // Create "Seed:" label
            CreateLabel(panel.transform, "Seed:", 55f);

            // Create seed input field
            CreateSeedInput(panel.transform);

            // Create dice button (randomize)
            CreateDiceButton(panel.transform);

            Undo.RegisterCreatedObjectUndo(panel, "Create Seed Panel");

            return panel;
        }

        private static void CreateToggle(Transform parent)
        {
            GameObject toggleObj = new GameObject("UseFixedSeedToggle");
            toggleObj.transform.SetParent(parent, false);

            RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(24f, 24f);

            // Toggle background
            Image toggleBg = toggleObj.AddComponent<Image>();
            toggleBg.color = TOGGLE_BACKGROUND_COLOR;

            Toggle toggle = toggleObj.AddComponent<Toggle>();

            // Toggle checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(toggleObj.transform, false);

            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.offsetMin = new Vector2(5f, 5f);
            checkRect.offsetMax = new Vector2(-5f, -5f);

            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Color.white;

            toggle.graphic = checkImage;
            toggle.targetGraphic = toggleBg;
        }

        private static void CreateLabel(Transform parent, string text, float width)
        {
            GameObject labelObj = new GameObject("SeedLabel");
            labelObj.transform.SetParent(parent, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(width, 30f);

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = LABEL_FONT_SIZE;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void CreateSeedInput(Transform parent)
        {
            GameObject inputObj = new GameObject("SeedInputField");
            inputObj.transform.SetParent(parent, false);

            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(100f, 30f);

            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = INPUT_BACKGROUND_COLOR;

            TMP_InputField inputField = inputObj.AddComponent<TMP_InputField>();
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Text area
            GameObject textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform, false);

            RectTransform textAreaRect = textArea.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);

            // Input text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(textArea.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI inputText = textObj.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = INPUT_FONT_SIZE;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.Center;

            inputField.textViewport = textAreaRect;
            inputField.textComponent = inputText;
        }

        private static void CreateDiceButton(Transform parent)
        {
            GameObject buttonObj = new GameObject("RandomizeSeedButton");
            buttonObj.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(34f, 34f);

            // Load the dice sprite
            Sprite diceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/dice.png");

            Image buttonBg = buttonObj.AddComponent<Image>();
            if (diceSprite != null)
            {
                buttonBg.sprite = diceSprite;
                buttonBg.color = Color.white;
                buttonBg.preserveAspect = true;
            }
            else
            {
                // Fallback to solid color if sprite not found
                buttonBg.color = BUTTON_NORMAL_COLOR;
                Debug.LogWarning("MainMenuSeedSetup: Could not load dice sprite from Assets/Sprites/dice.png");
            }

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonBg;

            // Set up button colors for hover/press feedback
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
        }

        private static void WireUpReferences(UI.MainMenuManager manager, GameObject seedPanel)
        {
            SerializedObject so = new SerializedObject(manager);

            // Find the toggle
            var toggle = seedPanel.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                so.FindProperty("useFixedSeedToggle").objectReferenceValue = toggle;
            }

            // Find the input field
            var inputField = seedPanel.GetComponentInChildren<TMP_InputField>();
            if (inputField != null)
            {
                so.FindProperty("seedInputField").objectReferenceValue = inputField;
            }

            // Find the randomize button
            var buttons = seedPanel.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                if (button.gameObject.name.Contains("Randomize"))
                {
                    so.FindProperty("randomizeSeedButton").objectReferenceValue = button;
                    break;
                }
            }

            // Find the SceneLoader if it exists
            var sceneLoader = Object.FindFirstObjectByType<UI.SceneLoader>();
            if (sceneLoader != null)
            {
                so.FindProperty("sceneLoader").objectReferenceValue = sceneLoader;
            }

            so.ApplyModifiedProperties();

            Debug.Log("MainMenuSeedSetup: Wired up MainMenuManager references.");
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        [MenuItem("FaeMaze/Remove Main Menu Seed Input")]
        public static void RemoveSeedInput()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("MainMenuSeedSetup: Canvas not found in scene.");
                return;
            }

            // Remove seed panel
            var seedPanel = FindChildByName(canvas.transform, "SeedPanel");
            if (seedPanel != null)
            {
                Undo.DestroyObjectImmediate(seedPanel.gameObject);
                Debug.Log("MainMenuSeedSetup: Removed seed panel.");
            }

            // Remove MainMenuManager
            var manager = Object.FindFirstObjectByType<UI.MainMenuManager>();
            if (manager != null)
            {
                Undo.DestroyObjectImmediate(manager.gameObject);
                Debug.Log("MainMenuSeedSetup: Removed MainMenuManager.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("MainMenuSeedSetup: Cleanup complete. Save the scene!");
        }
    }
}

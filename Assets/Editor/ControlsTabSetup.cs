#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;

/// <summary>
/// Editor script to help set up the Controls tab in the Options scene.
/// Run from Unity menu: FaeMaze > Setup Controls Tab
/// </summary>
public class ControlsTabSetup : EditorWindow
{
    [MenuItem("FaeMaze/Setup Controls Tab")]
    public static void ShowWindow()
    {
        GetWindow<ControlsTabSetup>("Controls Tab Setup");
    }

    private GameObject optionsManagerObj;

    private void OnGUI()
    {
        GUILayout.Label("Controls Tab Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool creates the Controls tab UI elements for the Options scene.\n\n" +
            "1. Open the Options scene\n" +
            "2. Assign the OptionsManager object below\n" +
            "3. Click 'Create Controls Tab'",
            MessageType.Info);

        EditorGUILayout.Space();

        optionsManagerObj = (GameObject)EditorGUILayout.ObjectField("OptionsManager", optionsManagerObj, typeof(GameObject), true);

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Controls Tab", GUILayout.Height(30)))
        {
            if (optionsManagerObj == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign the OptionsManager GameObject first.", "OK");
                return;
            }

            CreateControlsTab();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "After running this script:\n" +
            "1. Assign the controlsTabButton and controlsPanel in OptionsManager inspector\n" +
            "2. Name each KeyBindingCapture button correctly (see naming conventions)\n" +
            "3. Test in play mode",
            MessageType.Warning);
    }

    private void CreateControlsTab()
    {
        var optionsManager = optionsManagerObj.GetComponent<OptionsManager>();
        if (optionsManager == null)
        {
            EditorUtility.DisplayDialog("Error", "Selected object does not have an OptionsManager component.", "OK");
            return;
        }

        // Find the tab buttons container (assuming similar structure to existing tabs)
        Transform tabsContainer = null;
        var audioTab = GetFieldValue<Button>(optionsManager, "audioTabButton");
        if (audioTab != null)
        {
            tabsContainer = audioTab.transform.parent;
        }

        // Find the panels container
        Transform panelsContainer = null;
        var audioPanel = GetFieldValue<GameObject>(optionsManager, "audioPanel");
        if (audioPanel != null)
        {
            panelsContainer = audioPanel.transform.parent;
        }

        if (tabsContainer == null || panelsContainer == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find tab or panel containers. Make sure audioTabButton and audioPanel are assigned.", "OK");
            return;
        }

        // Create Controls tab button (copy from Audio tab)
        GameObject controlsTabBtn = Instantiate(audioTab.gameObject, tabsContainer);
        controlsTabBtn.name = "ControlsTabButton";
        var tabText = controlsTabBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (tabText != null)
            tabText.text = "Controls";

        // Position the tab (after Audio)
        controlsTabBtn.transform.SetAsLastSibling();

        // Create Controls panel (copy structure from Audio panel)
        GameObject controlsPanel = Instantiate(audioPanel, panelsContainer);
        controlsPanel.name = "ControlsPanel";
        controlsPanel.SetActive(false);

        // Clear existing content in the panel
        var scrollContent = controlsPanel.GetComponentInChildren<VerticalLayoutGroup>();
        if (scrollContent != null)
        {
            // Delete all children
            for (int i = scrollContent.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(scrollContent.transform.GetChild(i).gameObject);
            }

            // Create sections with binding rows
            CreateBindingSection(scrollContent.transform, "HEART POWERS", new[]
            {
                ("HeartPower1_Murmuring", "Murmuring Paths"),
                ("HeartPower2_Grasp", "Heartward Grasp"),
                ("HeartPower3_Devour", "Devouring Maw"),
                ("HeartPower4_Sculpting", "Sculpting")
            });

            CreateBindingSection(scrollContent.transform, "SCULPT MENU", new[]
            {
                ("SculptPond_PlacePond", "Place Pond"),
                ("SculptLantern_PlaceLantern", "Place Lantern"),
                ("SculptRing_PlaceRing", "Place Fairy Ring"),
                ("SculptRemove_RemoveProp", "Remove Prop")
            });

            CreateBindingSection(scrollContent.transform, "CAMERA MOVEMENT", new[]
            {
                ("CameraForward_MoveForward", "Move Forward"),
                ("CameraBackward_MoveBackward", "Move Backward"),
                ("CameraLeft_TurnLeft", "Turn Left"),
                ("CameraRight_TurnRight", "Turn Right")
            });

            CreateBindingSection(scrollContent.transform, "CAMERA FOCUS", new[]
            {
                ("FocusHeart", "Focus on Heart"),
                ("FocusEntrance", "Focus on Entrance"),
                ("FocusVisitor", "Focus on Visitor")
            });

            CreateBindingSection(scrollContent.transform, "CAMERA MOUSE", new[]
            {
                ("Orbit", "Orbit Camera"),
                ("Pan", "Pan Camera")
            });

            CreateBindingSection(scrollContent.transform, "UTILITY", new[]
            {
                ("Screenshot", "Take Screenshot")
            });
        }

        // Select the created panel in the hierarchy
        Selection.activeGameObject = controlsPanel;

        EditorUtility.DisplayDialog("Success",
            "Controls tab created!\n\n" +
            "Next steps:\n" +
            "1. Assign controlsTabButton in OptionsManager\n" +
            "2. Assign controlsPanel in OptionsManager\n" +
            "3. Verify KeyBindingCapture components have bindingText assigned\n" +
            "4. Save the scene",
            "OK");
    }

    private void CreateBindingSection(Transform parent, string headerText, (string name, string label)[] bindings)
    {
        // Create section header
        GameObject headerObj = new GameObject(headerText + "_Header");
        headerObj.transform.SetParent(parent, false);

        var headerLayout = headerObj.AddComponent<LayoutElement>();
        headerLayout.minHeight = 40;
        headerLayout.preferredHeight = 40;

        var headerBg = headerObj.AddComponent<Image>();
        headerBg.color = new Color(0.2f, 0.3f, 0.4f, 1f);

        var headerTextObj = new GameObject("Text");
        headerTextObj.transform.SetParent(headerObj.transform, false);

        var headerTMP = headerTextObj.AddComponent<TextMeshProUGUI>();
        headerTMP.text = headerText;
        headerTMP.fontSize = 20;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var textRect = headerTextObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(15, 0);
        textRect.offsetMax = Vector2.zero;

        // Create binding rows
        foreach (var (name, label) in bindings)
        {
            CreateBindingRow(parent, name, label);
        }
    }

    private void CreateBindingRow(Transform parent, string bindingName, string labelText)
    {
        // Row container
        GameObject rowObj = new GameObject(bindingName + "_Row");
        rowObj.transform.SetParent(parent, false);

        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childControlWidth = false;
        rowLayout.spacing = 10;
        rowLayout.padding = new RectOffset(15, 15, 5, 5);

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 18;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 200;
        labelLayout.minWidth = 200;

        // Spacer
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(rowObj.transform, false);
        var spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1;

        // Binding button
        GameObject buttonObj = new GameObject(bindingName + "_Button");
        buttonObj.transform.SetParent(rowObj.transform, false);

        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        var buttonLayout = buttonObj.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 150;
        buttonLayout.preferredHeight = 35;

        // Add KeyBindingCapture component
        var capture = buttonObj.AddComponent<KeyBindingCapture>();

        // Binding text inside button
        GameObject textObj = new GameObject("BindingText");
        textObj.transform.SetParent(buttonObj.transform, false);

        var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
        bindingTMP.text = "[Not Set]";
        bindingTMP.fontSize = 16;
        bindingTMP.alignment = TextAlignmentOptions.Center;

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        // Assign bindingText to capture using SerializedObject
        var so = new SerializedObject(capture);
        var bindingTextProp = so.FindProperty("bindingText");
        if (bindingTextProp != null)
        {
            bindingTextProp.objectReferenceValue = bindingTMP;
            so.ApplyModifiedProperties();
        }
    }

    private T GetFieldValue<T>(object obj, string fieldName) where T : class
    {
        var type = obj.GetType();
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            return field.GetValue(obj) as T;
        return null;
    }
}
#endif

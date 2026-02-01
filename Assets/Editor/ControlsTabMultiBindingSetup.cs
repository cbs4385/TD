#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;

/// <summary>
/// Editor script to set up the Controls tab with proper multi-binding support.
///
/// Key features:
/// 1. Three binding columns per row: Primary, Secondary, Tertiary
/// 2. Toggle buttons positioned with 2x spacing before them vs after
/// 3. All columns (including former N/A) are functional KeyBindingCapture controls
/// 4. Users can assign any input to any binding slot
///
/// Run from Unity menu: FaeMaze > Setup Controls Tab (Multi-Binding)
/// </summary>
public class ControlsTabMultiBindingSetup : EditorWindow
{
    // Colors matching VIDEO tab exactly
    private static readonly Color HeaderBgColor = new Color(0.2f, 0.3f, 0.4f, 1f);
    private static readonly Color ToggleBgColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    private static readonly Color ButtonBgColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    private static readonly Color CheckmarkColor = new Color(1f, 0.8f, 0.3f, 1f);

    // Layout constants for proper spacing
    // The toggle for column N should be 2x as far from column N-1's text as from column N's text
    private const float LABEL_WIDTH = 300f;
    private const float BINDING_WIDTH = 120f;  // Width of the binding text area
    private const float TOGGLE_SIZE = 24f;
    private const float TOGGLE_TO_TEXT_GAP = 8f;  // Gap from toggle to its binding text
    private const float TEXT_TO_TOGGLE_GAP = 16f;  // Gap from binding text to NEXT toggle (2x)
    private const float COLUMN_SPACING = TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP + TEXT_TO_TOGGLE_GAP;

    [MenuItem("FaeMaze/Setup Controls Tab (Multi-Binding)")]
    public static void SetupMultiBindingControls()
    {
        var controlsPanel = GameObject.Find("ControlsPanel");
        if (controlsPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "ControlsPanel not found. Open Options scene first.", "OK");
            return;
        }

        Transform scrollContent = null;
        var scrollView = controlsPanel.GetComponentInChildren<ScrollRect>();
        if (scrollView != null && scrollView.content != null)
        {
            scrollContent = scrollView.content;
        }
        else
        {
            var vlg = controlsPanel.GetComponentInChildren<VerticalLayoutGroup>();
            if (vlg != null) scrollContent = vlg.transform;
        }

        if (scrollContent == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find scroll content.", "OK");
            return;
        }

        // Clear existing
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(scrollContent.GetChild(i).gameObject);
        }

        SetupScrollContent(scrollContent);

        // Heart Powers - single binding per row
        CreateSingleBindingSection(scrollContent, "HEART POWER ACTIVATION", new[]
        {
            ("Murmuring Paths (Power 1)", "HeartPower1"),
            ("Heartward Grasp (Power 2)", "HeartPower2"),
            ("Devouring Maw (Power 3)", "HeartPower3"),
            ("Sculpting (Power 4)", "HeartPower4"),
        });

        // Sculpt Menu - single binding per row
        CreateSingleBindingSection(scrollContent, "SCULPT MENU", new[]
        {
            ("Place Pond", "SculptPond"),
            ("Place Lantern", "SculptLantern"),
            ("Place Fairy Ring", "SculptRing"),
            ("Remove Prop", "SculptRemove"),
        });

        // Camera section - these use special row creation because Primary/Alt have different names
        CreateCameraSection(scrollContent);

        // Utility - single binding per row
        CreateSingleBindingSection(scrollContent, "UTILITY", new[]
        {
            ("Take Screenshot", "Screenshot"),
        });

        EditorUtility.SetDirty(scrollContent.gameObject);
        EditorUtility.DisplayDialog("Success", "Controls tab created! Save the scene.", "OK");
    }

    private static void SetupScrollContent(Transform scrollContent)
    {
        var vlg = scrollContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = scrollContent.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.padding = new RectOffset(20, 20, 10, 120);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        var csf = scrollContent.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = scrollContent.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Creates a section with single-binding rows (one binding per control).
    /// Used for Heart Powers, Sculpt Menu, and Utility sections.
    /// </summary>
    private static void CreateSingleBindingSection(Transform parent, string title, (string label, string bindingName)[] bindings)
    {
        string safeName = title.Replace(" ", "").Replace("/", "");
        GameObject sectionObj = new GameObject(safeName + "Section");
        sectionObj.transform.SetParent(parent, false);

        var sectionVLG = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionVLG.padding = new RectOffset(0, 0, 0, 0);
        sectionVLG.spacing = 5;
        sectionVLG.childAlignment = TextAnchor.UpperLeft;
        sectionVLG.childForceExpandWidth = true;
        sectionVLG.childForceExpandHeight = false;
        sectionVLG.childControlWidth = true;
        sectionVLG.childControlHeight = false;

        var sectionLE = sectionObj.AddComponent<LayoutElement>();
        sectionLE.flexibleWidth = 1;

        var sectionCSF = sectionObj.AddComponent<ContentSizeFitter>();
        sectionCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sectionCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var (headerObj, headerButton, arrowTMP, headerTMP) = CreateHeader(sectionObj.transform, title);

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(sectionObj.transform, false);

        var contentVLG = contentObj.AddComponent<VerticalLayoutGroup>();
        contentVLG.padding = new RectOffset(20, 20, 10, 10);
        contentVLG.spacing = 10;
        contentVLG.childAlignment = TextAnchor.UpperLeft;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = false;

        var contentCSF = contentObj.AddComponent<ContentSizeFitter>();
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var collapsible = sectionObj.AddComponent<CollapsibleSection>();
        var so = new SerializedObject(collapsible);
        so.FindProperty("headerButton").objectReferenceValue = headerButton;
        so.FindProperty("contentPanel").objectReferenceValue = contentObj;
        so.FindProperty("headerText").objectReferenceValue = headerTMP;
        so.FindProperty("arrowText").objectReferenceValue = arrowTMP;
        so.FindProperty("startExpanded").boolValue = true;
        so.ApplyModifiedProperties();

        foreach (var (label, bindingName) in bindings)
        {
            CreateSingleBindingRow(contentObj.transform, label, bindingName);
        }
    }

    private static void CreateSection(Transform parent, string title, (string label, string bindingName)[] bindings)
    {
        string safeName = title.Replace(" ", "").Replace("/", "");
        GameObject sectionObj = new GameObject(safeName + "Section");
        sectionObj.transform.SetParent(parent, false);

        var sectionVLG = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionVLG.padding = new RectOffset(0, 0, 0, 0);
        sectionVLG.spacing = 5;
        sectionVLG.childAlignment = TextAnchor.UpperLeft;
        sectionVLG.childForceExpandWidth = true;
        sectionVLG.childForceExpandHeight = false;
        sectionVLG.childControlWidth = true;
        sectionVLG.childControlHeight = false;

        var sectionLE = sectionObj.AddComponent<LayoutElement>();
        sectionLE.flexibleWidth = 1;

        var sectionCSF = sectionObj.AddComponent<ContentSizeFitter>();
        sectionCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sectionCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var (headerObj, headerButton, arrowTMP, headerTMP) = CreateHeader(sectionObj.transform, title);

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(sectionObj.transform, false);

        var contentVLG = contentObj.AddComponent<VerticalLayoutGroup>();
        contentVLG.padding = new RectOffset(20, 20, 10, 10);
        contentVLG.spacing = 10;
        contentVLG.childAlignment = TextAnchor.UpperLeft;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = false;

        var contentCSF = contentObj.AddComponent<ContentSizeFitter>();
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var collapsible = sectionObj.AddComponent<CollapsibleSection>();
        var so = new SerializedObject(collapsible);
        so.FindProperty("headerButton").objectReferenceValue = headerButton;
        so.FindProperty("contentPanel").objectReferenceValue = contentObj;
        so.FindProperty("headerText").objectReferenceValue = headerTMP;
        so.FindProperty("arrowText").objectReferenceValue = arrowTMP;
        so.FindProperty("startExpanded").boolValue = true;
        so.ApplyModifiedProperties();

        foreach (var (label, bindingName) in bindings)
        {
            CreateBindingRow(contentObj.transform, label, bindingName);
        }
    }

    /// <summary>
    /// Creates the CAMERA section with special handling for Primary/Alt bindings.
    /// Camera movement uses different binding names: Primary (WASD), Alt (Arrow keys).
    /// </summary>
    private static void CreateCameraSection(Transform parent)
    {
        string safeName = "CAMERA";
        GameObject sectionObj = new GameObject(safeName + "Section");
        sectionObj.transform.SetParent(parent, false);

        var sectionVLG = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionVLG.padding = new RectOffset(0, 0, 0, 0);
        sectionVLG.spacing = 5;
        sectionVLG.childAlignment = TextAnchor.UpperLeft;
        sectionVLG.childForceExpandWidth = true;
        sectionVLG.childForceExpandHeight = false;
        sectionVLG.childControlWidth = true;
        sectionVLG.childControlHeight = false;

        var sectionLE = sectionObj.AddComponent<LayoutElement>();
        sectionLE.flexibleWidth = 1;

        var sectionCSF = sectionObj.AddComponent<ContentSizeFitter>();
        sectionCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sectionCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var (headerObj, headerButton, arrowTMP, headerTMP) = CreateHeader(sectionObj.transform, "CAMERA");

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(sectionObj.transform, false);

        var contentVLG = contentObj.AddComponent<VerticalLayoutGroup>();
        contentVLG.padding = new RectOffset(20, 20, 10, 10);
        contentVLG.spacing = 10;
        contentVLG.childAlignment = TextAnchor.UpperLeft;
        contentVLG.childForceExpandWidth = true;
        contentVLG.childForceExpandHeight = false;
        contentVLG.childControlWidth = true;
        contentVLG.childControlHeight = false;

        var contentCSF = contentObj.AddComponent<ContentSizeFitter>();
        contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var collapsible = sectionObj.AddComponent<CollapsibleSection>();
        var so = new SerializedObject(collapsible);
        so.FindProperty("headerButton").objectReferenceValue = headerButton;
        so.FindProperty("contentPanel").objectReferenceValue = contentObj;
        so.FindProperty("headerText").objectReferenceValue = headerTMP;
        so.FindProperty("arrowText").objectReferenceValue = arrowTMP;
        so.FindProperty("startExpanded").boolValue = true;
        so.ApplyModifiedProperties();

        // Camera movement - Primary (WASD), Secondary (Arrow keys), no mouse binding
        CreateCameraMovementRow(contentObj.transform, "Move Forward", "MoveForward", "MoveForwardAlt");
        CreateCameraMovementRow(contentObj.transform, "Move Backward", "MoveBackward", "MoveBackwardAlt");
        CreateCameraMovementRow(contentObj.transform, "Turn Left", "TurnLeft", "TurnLeftAlt");
        CreateCameraMovementRow(contentObj.transform, "Turn Right", "TurnRight", "TurnRightAlt");

        // Focus shortcuts - single binding only
        CreateSingleBindingRow(contentObj.transform, "Focus on Heart", "FocusHeart");
        CreateSingleBindingRow(contentObj.transform, "Focus on Entrance", "FocusEntrance");
        CreateSingleBindingRow(contentObj.transform, "Focus on Visitor", "FocusVisitor");

        // Mouse controls - single binding only (these are mouse-specific)
        CreateSingleBindingRow(contentObj.transform, "Move to Click", "ForwardMouse");
        CreateSingleBindingRow(contentObj.transform, "Orbit Camera", "Orbit");
        CreateSingleBindingRow(contentObj.transform, "Pan Camera", "Pan");
    }

    /// <summary>
    /// Creates a camera movement row with Primary and Alt bindings (no mouse column).
    /// </summary>
    private static void CreateCameraMovementRow(Transform parent, string label, string primaryName, string altName)
    {
        GameObject rowObj = new GameObject(label.Replace(" ", "") + "_Row");
        rowObj.transform.SetParent(parent, false);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 40);

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.minHeight = 40;
        rowLE.preferredHeight = 40;

        // No spacing - spacing is built into each column
        var rowHLG = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowHLG.padding = new RectOffset(0, 0, 0, 0);
        rowHLG.spacing = 0;
        rowHLG.childAlignment = TextAnchor.MiddleLeft;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = false;
        rowHLG.childControlWidth = false;
        rowHLG.childControlHeight = false;

        // Label
        CreateRowLabel(rowObj.transform, label);

        // Primary binding (e.g., "MoveForward" -> matches "moveforward")
        CreateBindingCapture(rowObj.transform, primaryName, BINDING_WIDTH);
        // Alt/Secondary binding (e.g., "MoveForwardAlt" -> matches "moveforwardalt")
        CreateBindingCapture(rowObj.transform, altName, BINDING_WIDTH);
        // Tertiary binding - optional third binding slot
        CreateOptionalBindingCapture(rowObj.transform, primaryName + "Tertiary", BINDING_WIDTH);
    }

    /// <summary>
    /// Creates a row with a single binding (Focus, Mouse controls).
    /// </summary>
    private static void CreateSingleBindingRow(Transform parent, string label, string bindingName)
    {
        GameObject rowObj = new GameObject(label.Replace(" ", "") + "_Row");
        rowObj.transform.SetParent(parent, false);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 40);

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.minHeight = 40;
        rowLE.preferredHeight = 40;

        // No spacing - spacing is built into each column
        var rowHLG = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowHLG.padding = new RectOffset(0, 0, 0, 0);
        rowHLG.spacing = 0;
        rowHLG.childAlignment = TextAnchor.MiddleLeft;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = false;
        rowHLG.childControlWidth = false;
        rowHLG.childControlHeight = false;

        // Label
        CreateRowLabel(rowObj.transform, label);

        // Single binding
        CreateBindingCapture(rowObj.transform, bindingName, BINDING_WIDTH);
        // Secondary and tertiary optional bindings
        CreateOptionalBindingCapture(rowObj.transform, bindingName + "Alt", BINDING_WIDTH);
        CreateOptionalBindingCapture(rowObj.transform, bindingName + "Tertiary", BINDING_WIDTH);
    }

    /// <summary>
    /// Creates the label for a row.
    /// </summary>
    private static void CreateRowLabel(Transform parent, string label)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);

        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(LABEL_WIDTH, 40);

        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.minWidth = LABEL_WIDTH;
        labelLE.preferredWidth = LABEL_WIDTH;

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = label;
        labelTMP.fontSize = 20;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        labelTMP.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static (GameObject, Button, TextMeshProUGUI, TextMeshProUGUI) CreateHeader(Transform parent, string title)
    {
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(parent, false);

        var headerLE = headerObj.AddComponent<LayoutElement>();
        headerLE.minHeight = 50;
        headerLE.preferredHeight = 50;

        var headerHLG = headerObj.AddComponent<HorizontalLayoutGroup>();
        headerHLG.padding = new RectOffset(15, 15, 0, 0);
        headerHLG.spacing = 10;
        headerHLG.childAlignment = TextAnchor.MiddleLeft;
        headerHLG.childForceExpandWidth = false;
        headerHLG.childForceExpandHeight = true;
        headerHLG.childControlWidth = false;
        headerHLG.childControlHeight = true;

        var headerImage = headerObj.AddComponent<Image>();
        headerImage.color = HeaderBgColor;

        var headerButton = headerObj.AddComponent<Button>();
        headerButton.targetGraphic = headerImage;

        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(headerObj.transform, false);

        var arrowLE = arrowObj.AddComponent<LayoutElement>();
        arrowLE.preferredWidth = 20;

        var arrowTMP = arrowObj.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = "v";
        arrowTMP.fontSize = 20;
        arrowTMP.alignment = TextAlignmentOptions.MidlineLeft;
        arrowTMP.color = Color.white;

        GameObject textObj = new GameObject("HeaderText");
        textObj.transform.SetParent(headerObj.transform, false);

        var textLE = textObj.AddComponent<LayoutElement>();
        textLE.flexibleWidth = 1;

        var headerTMP = textObj.AddComponent<TextMeshProUGUI>();
        headerTMP.text = title;
        headerTMP.fontSize = 28;
        headerTMP.fontStyle = FontStyles.Bold;
        headerTMP.alignment = TextAlignmentOptions.MidlineLeft;
        headerTMP.color = Color.white;
        headerTMP.textWrappingMode = TextWrappingModes.NoWrap;

        return (headerObj, headerButton, arrowTMP, headerTMP);
    }

    private static void CreateBindingRow(Transform parent, string label, string bindingName)
    {
        // ScreenshotKeyRow exact structure:
        // - sizeDelta: (1456, 40)
        // - LayoutElement: minHeight=40, preferredHeight=40
        // - HorizontalLayoutGroup: spacing=15, childForceExpandWidth=0, childControlWidth=0

        GameObject rowObj = new GameObject(bindingName + "_Row");
        rowObj.transform.SetParent(parent, false);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, 40);

        var rowLE = rowObj.AddComponent<LayoutElement>();
        rowLE.minHeight = 40;
        rowLE.preferredHeight = 40;

        // EXACT match: childControlWidth = false, childForceExpandWidth = false
        var rowHLG = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowHLG.padding = new RectOffset(0, 0, 0, 0);
        rowHLG.spacing = 15;
        rowHLG.childAlignment = TextAnchor.UpperLeft;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = true;
        rowHLG.childControlWidth = false;
        rowHLG.childControlHeight = true;

        // Label - preferredWidth: 300, sizeDelta: (300, 40)
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);

        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelLE = labelObj.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 300;

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = label;
        labelTMP.fontSize = 20;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        labelTMP.textWrappingMode = TextWrappingModes.NoWrap;

        // Three binding captures with distinct binding key names
        // Primary uses the base name, Secondary uses "Alt" suffix, Mouse uses "Mouse" suffix
        CreateBindingCapture(rowObj.transform, bindingName, 200);           // Primary
        CreateBindingCapture(rowObj.transform, bindingName + "Alt", 200);   // Secondary
        CreateBindingCapture(rowObj.transform, bindingName + "Mouse", 200); // Mouse/Gamepad
    }

    /// <summary>
    /// Creates an optional binding slot that shows "(unbound)" when empty.
    /// This is a fully functional KeyBindingCapture control - users can assign any input.
    /// The empty state shows grayed text to indicate it's optional.
    /// </summary>
    private static void CreateOptionalBindingCapture(Transform parent, string bindingName, float width)
    {
        // Same structure as CreateBindingCapture but with "(unbound)" as default text
        float totalWidth = TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP + width + TEXT_TO_TOGGLE_GAP;

        GameObject captureObj = new GameObject(bindingName + "_Button");
        captureObj.transform.SetParent(parent, false);

        var captureRect = captureObj.AddComponent<RectTransform>();
        captureRect.sizeDelta = new Vector2(totalWidth, 40);

        var captureLE = captureObj.AddComponent<LayoutElement>();
        captureLE.minWidth = totalWidth;
        captureLE.minHeight = 40;
        captureLE.preferredWidth = totalWidth;
        captureLE.preferredHeight = 40;

        // CaptureToggle - positioned at the LEFT of the container
        GameObject toggleObj = new GameObject("CaptureToggle");
        toggleObj.transform.SetParent(captureObj.transform, false);

        var toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0, 0.5f);
        toggleRect.anchorMax = new Vector2(0, 0.5f);
        toggleRect.pivot = new Vector2(0, 0.5f);
        toggleRect.anchoredPosition = new Vector2(0, 0);
        toggleRect.sizeDelta = new Vector2(TOGGLE_SIZE, TOGGLE_SIZE);

        var toggleImage = toggleObj.AddComponent<Image>();
        toggleImage.color = ToggleBgColor;

        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = toggleImage;
        toggle.isOn = false;

        // Checkmark
        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);

        var checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        var checkmarkImage = checkmarkObj.AddComponent<Image>();
        checkmarkImage.color = CheckmarkColor;

        toggle.graphic = checkmarkImage;

        // Background for the binding text area - slightly darker for optional bindings
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(captureObj.transform, false);

        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0, 0.5f);
        bgRect.anchoredPosition = new Vector2(TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP, 0);
        bgRect.sizeDelta = new Vector2(width, 32);

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);  // Slightly darker than primary bindings

        // BindingText - shows "-" when empty, leaves room for clear button on right
        GameObject textObj = new GameObject("BindingText");
        textObj.transform.SetParent(bgObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4, 0);
        textRect.offsetMax = new Vector2(-28, 0);  // Leave room for clear button (24px)

        var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
        bindingTMP.text = "-";  // Unset text
        bindingTMP.fontSize = 16;
        bindingTMP.alignment = TextAlignmentOptions.Center;
        bindingTMP.color = new Color(0.5f, 0.5f, 0.5f, 1f);  // Gray for optional

        // Add KeyBindingCapture component
        var capture = captureObj.AddComponent<KeyBindingCapture>();

        var so = new SerializedObject(capture);
        so.FindProperty("bindingText").objectReferenceValue = bindingTMP;
        so.FindProperty("captureToggle").objectReferenceValue = toggle;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Creates a KeyBindingCapture control with proper spacing.
    ///
    /// Layout: [Toggle][TOGGLE_TO_TEXT_GAP][BindingText][TEXT_TO_TOGGLE_GAP]
    ///
    /// The toggle is positioned to the LEFT of the binding text box.
    /// The gap from toggle to text (8px) is half the gap from text to next toggle (16px).
    /// This creates the visual effect of toggles being "grouped" with their binding.
    /// </summary>
    private static void CreateBindingCapture(Transform parent, string bindingName, float width)
    {
        // Container for the entire binding column
        // Total width = toggle + gap + text + gap = 24 + 8 + width + 16 = width + 48
        float totalWidth = TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP + width + TEXT_TO_TOGGLE_GAP;

        GameObject captureObj = new GameObject(bindingName + "_Button");
        captureObj.transform.SetParent(parent, false);

        var captureRect = captureObj.AddComponent<RectTransform>();
        captureRect.sizeDelta = new Vector2(totalWidth, 40);

        var captureLE = captureObj.AddComponent<LayoutElement>();
        captureLE.minWidth = totalWidth;
        captureLE.minHeight = 40;
        captureLE.preferredWidth = totalWidth;
        captureLE.preferredHeight = 40;

        // CaptureToggle - positioned at the LEFT of the container
        GameObject toggleObj = new GameObject("CaptureToggle");
        toggleObj.transform.SetParent(captureObj.transform, false);

        var toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0, 0.5f);
        toggleRect.anchorMax = new Vector2(0, 0.5f);
        toggleRect.pivot = new Vector2(0, 0.5f);
        toggleRect.anchoredPosition = new Vector2(0, 0);  // Flush left
        toggleRect.sizeDelta = new Vector2(TOGGLE_SIZE, TOGGLE_SIZE);

        var toggleImage = toggleObj.AddComponent<Image>();
        toggleImage.color = ToggleBgColor;

        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = toggleImage;
        toggle.isOn = false;

        // Checkmark
        GameObject checkmarkObj = new GameObject("Checkmark");
        checkmarkObj.transform.SetParent(toggleObj.transform, false);

        var checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        var checkmarkImage = checkmarkObj.AddComponent<Image>();
        checkmarkImage.color = CheckmarkColor;

        toggle.graphic = checkmarkImage;

        // Background for the binding text area
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(captureObj.transform, false);

        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0, 0.5f);
        bgRect.anchoredPosition = new Vector2(TOGGLE_SIZE + TOGGLE_TO_TEXT_GAP, 0);
        bgRect.sizeDelta = new Vector2(width, 32);

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = ButtonBgColor;

        // BindingText - inside the background, leaves room for clear button on right
        GameObject textObj = new GameObject("BindingText");
        textObj.transform.SetParent(bgObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4, 0);
        textRect.offsetMax = new Vector2(-28, 0);  // Leave room for clear button (24px)

        var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
        bindingTMP.text = "-";
        bindingTMP.fontSize = 16;
        bindingTMP.alignment = TextAlignmentOptions.Center;
        bindingTMP.color = Color.white;

        // Add KeyBindingCapture component
        var capture = captureObj.AddComponent<KeyBindingCapture>();

        var so = new SerializedObject(capture);
        so.FindProperty("bindingText").objectReferenceValue = bindingTMP;
        so.FindProperty("captureToggle").objectReferenceValue = toggle;
        so.ApplyModifiedProperties();
    }
}
#endif

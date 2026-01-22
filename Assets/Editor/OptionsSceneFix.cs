#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using FaeMaze.UI;
using System.Collections.Generic;

/// <summary>
/// Editor script to fix issues in the Options scene:
/// 1. Fix Gameplay tab header styles to match other tabs (blue/teal color)
/// 2. Remove duplicate Player Controls section with heart power dropdowns
/// 3. Add missing Sculpt Menu binding rows to Controls tab
/// Run from Unity menu: FaeMaze > Fix Options Scene
/// </summary>
public class OptionsSceneFix : EditorWindow
{
    // Header style matching Video tab exactly
    // From scene file: m_fontStyle: 1 (Bold), m_VerticalAlignment: 4096 (Middle), m_fontSize: 28
    private static readonly Color headerColor = new Color(0.2f, 0.3f, 0.4f, 1f);
    private static readonly int headerFontSize = 28;
    private static readonly FontStyles headerFontStyle = FontStyles.Bold;
    private static readonly TextAlignmentOptions headerAlignment = TextAlignmentOptions.MidlineLeft;

    // Arrow/caret style matching Video tab
    private static readonly Vector2 arrowSize = new Vector2(20f, 50f);  // Video tab: narrow and tall
    private static readonly int arrowFontSize = 20;
    private static readonly string arrowExpandedChar = "v";  // Video tab uses ASCII "v"
    private static readonly string arrowCollapsedChar = ">";  // Video tab uses ASCII ">"

    // Content panel layout settings matching Video tab
    // Note: RectOffset values stored as ints to avoid ScriptableObject constructor issues
    private const int contentPanelPaddingLeft = 20;
    private const int contentPanelPaddingRight = 20;
    private const int contentPanelPaddingTop = 10;
    private const int contentPanelPaddingBottom = 10;
    private const float contentPanelSpacing = 10f;  // Video tab spacing

    [MenuItem("FaeMaze/Fix Options Scene")]
    public static void ShowWindow()
    {
        GetWindow<OptionsSceneFix>("Fix Options Scene");
    }

    private void OnGUI()
    {
        GUILayout.Label("Options Scene Fix", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool fixes issues in the Options scene:\n\n" +
            "1. Fixes Gameplay tab header styles to match blue/teal color\n" +
            "2. Removes duplicate Player Controls section (heart power dropdowns)\n" +
            "3. Adds missing Sculpt Menu binding rows to Controls tab\n\n" +
            "Make sure the Options scene is open before running.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Gameplay Tab Headers", GUILayout.Height(30)))
        {
            FixGameplayTabHeaders();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Controls Tab Headers", GUILayout.Height(30)))
        {
            FixControlsTabHeaders();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Remove Player Controls Section", GUILayout.Height(30)))
        {
            RemovePlayerControlsSection();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Sculpt Menu Bindings", GUILayout.Height(30)))
        {
            FixSculptMenuBindings();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild All Controls Tab Sections", GUILayout.Height(30)))
        {
            RebuildAllControlsTabSections();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Rebuild Gameplay Tab", GUILayout.Height(30)))
        {
            RebuildGameplayTab();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Fix Screenshot Directory Row", GUILayout.Height(30)))
        {
            FixScreenshotDirectoryRow();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Convert Screenshot Key to Rebindable", GUILayout.Height(30)))
        {
            ConvertScreenshotDropdownToCapture();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Run All Fixes", GUILayout.Height(40)))
        {
            RebuildGameplayTab();
            RebuildAllControlsTabSections();
            FixScreenshotDirectoryRow();
            ConvertScreenshotDropdownToCapture();
            WireUpScrollRectToOptionsManager();
            EditorUtility.DisplayDialog("Complete", "All fixes applied. Save the scene to persist changes.", "OK");
        }
    }

    private void WireUpScrollRectToOptionsManager()
    {
        // Find the main ScrollView that contains the tab panels
        var scrollView = FindInScene("ScrollView");
        if (scrollView == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find ScrollView in scene");
            return;
        }

        var scrollRect = scrollView.GetComponent<UnityEngine.UI.ScrollRect>();
        if (scrollRect == null)
        {
            Debug.LogWarning("[OptionsSceneFix] ScrollView does not have a ScrollRect component");
            return;
        }

        // Find OptionsManager
        var allManagers = Resources.FindObjectsOfTypeAll<OptionsManager>();
        OptionsManager manager = null;

        foreach (var m in allManagers)
        {
            if (m.gameObject.scene.IsValid())
            {
                manager = m;
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find OptionsManager in scene");
            return;
        }

        var so = new SerializedObject(manager);
        var scrollRectProp = so.FindProperty("mainScrollRect");
        if (scrollRectProp != null)
        {
            scrollRectProp.objectReferenceValue = scrollRect;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            Debug.Log("[OptionsSceneFix] Wired mainScrollRect to OptionsManager");
        }
        else
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find mainScrollRect field on OptionsManager");
        }
    }

    private void ConvertScreenshotDropdownToCapture()
    {
        // Find the screenshot key dropdown in the scene
        var allDropdowns = Resources.FindObjectsOfTypeAll<TMP_Dropdown>();
        TMP_Dropdown screenshotDropdown = null;

        foreach (var dropdown in allDropdowns)
        {
            if (!dropdown.gameObject.scene.IsValid()) continue; // Skip prefabs

            // Check parent name or dropdown name for screenshot
            string name = dropdown.gameObject.name.ToLower();
            string parentName = dropdown.transform.parent?.name.ToLower() ?? "";

            if (name.Contains("screenshot") || parentName.Contains("screenshot"))
            {
                screenshotDropdown = dropdown;
                Debug.Log($"[OptionsSceneFix] Found Screenshot dropdown: {dropdown.gameObject.name} (parent: {dropdown.transform.parent?.name})");
                break;
            }
        }

        if (screenshotDropdown == null)
        {
            Debug.Log("[OptionsSceneFix] Screenshot dropdown not found - may already be converted or doesn't exist");
            return;
        }

        // Get the parent row that contains this dropdown
        Transform rowParent = screenshotDropdown.transform.parent;
        if (rowParent == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Screenshot dropdown has no parent");
            return;
        }

        // Get the dropdown's RectTransform dimensions to match
        var dropdownRect = screenshotDropdown.GetComponent<RectTransform>();

        // Create a new button to replace the dropdown
        GameObject buttonObj = new GameObject("Screenshot_Button");
        Undo.RegisterCreatedObjectUndo(buttonObj, "Create Screenshot Button");
        buttonObj.transform.SetParent(rowParent, false);
        buttonObj.transform.SetSiblingIndex(screenshotDropdown.transform.GetSiblingIndex());

        var buttonRect = buttonObj.AddComponent<RectTransform>();
        // Copy size from dropdown
        if (dropdownRect != null)
        {
            buttonRect.sizeDelta = dropdownRect.sizeDelta;
        }
        else
        {
            buttonRect.sizeDelta = new Vector2(120, 30);
        }

        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        var buttonLayout = buttonObj.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 120;
        buttonLayout.minWidth = 100;
        buttonLayout.preferredHeight = 30;
        buttonLayout.minHeight = 30;

        // Add KeyBindingCapture component
        var capture = buttonObj.AddComponent<KeyBindingCapture>();

        // Create text inside button
        GameObject textObj = new GameObject("BindingText");
        textObj.transform.SetParent(buttonObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
        bindingTMP.text = "F12"; // Default screenshot key
        bindingTMP.fontSize = 16;
        bindingTMP.alignment = TextAlignmentOptions.Center;
        bindingTMP.color = Color.white;

        // Assign bindingText to capture
        var so = new SerializedObject(capture);
        var bindingTextProp = so.FindProperty("bindingText");
        if (bindingTextProp != null)
        {
            bindingTextProp.objectReferenceValue = bindingTMP;
            so.ApplyModifiedProperties();
        }

        // Delete the old dropdown
        Debug.Log($"[OptionsSceneFix] Replacing dropdown {screenshotDropdown.gameObject.name} with KeyBindingCapture button");
        Undo.DestroyObjectImmediate(screenshotDropdown.gameObject);

        EditorUtility.SetDirty(buttonObj);
        Debug.Log("[OptionsSceneFix] Screenshot dropdown converted to KeyBindingCapture button");
    }

    /// <summary>
    /// Find a GameObject by name, including inactive objects
    /// </summary>
    private GameObject FindInScene(string name)
    {
        // Search all root objects and their children
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            // Only consider objects in the active scene (not prefabs)
            if (obj.scene.IsValid() && obj.name == name)
            {
                return obj;
            }
        }
        return null;
    }

    /// <summary>
    /// Find all GameObjects containing a name pattern, including inactive objects
    /// </summary>
    private List<GameObject> FindAllContaining(string pattern, GameObject root = null)
    {
        var results = new List<GameObject>();
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (var obj in allObjects)
        {
            if (!obj.scene.IsValid()) continue; // Skip prefabs

            if (root != null)
            {
                // Check if this object is a child of root
                bool isChild = false;
                Transform t = obj.transform;
                while (t != null)
                {
                    if (t.gameObject == root)
                    {
                        isChild = true;
                        break;
                    }
                    t = t.parent;
                }
                if (!isChild) continue;
            }

            if (obj.name.ToLower().Contains(pattern.ToLower()))
            {
                results.Add(obj);
            }
        }
        return results;
    }

    private void FixGameplayTabHeaders()
    {
        // Find GameplayPanel
        var gameplayPanel = FindInScene("GameplayPanel");
        if (gameplayPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find GameplayPanel. Make sure Options scene is open.", "OK");
            return;
        }

        Debug.Log($"[OptionsSceneFix] Found GameplayPanel: {gameplayPanel.name}");

        int fixedCount = 0;

        // First, let's log ALL TextMeshProUGUI components to understand the structure
        var allTexts = gameplayPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        Debug.Log($"[OptionsSceneFix] Found {allTexts.Length} TextMeshProUGUI components in GameplayPanel");

        foreach (var tmp in allTexts)
        {
            string text = tmp.text.Trim();

            // Check if this looks like a section header (all caps, known header names)
            bool isHeader = !string.IsNullOrEmpty(text) && text == text.ToUpper() && text.Length > 3 &&
                (text.Contains("VISITOR") || text.Contains("GAME") || text.Contains("PLAYER") ||
                 text.Contains("SPAWNING") || text.Contains("FLOW") || text.Contains("TYPES") ||
                 text.Contains("CONTROLS") || text.Contains("SETTINGS") || text.Contains("WAVE") ||
                 text.Contains("ESSENCE") || text.Contains("REDCAP"));

            if (isHeader)
            {
                Debug.Log($"[OptionsSceneFix] Found header text: '{text}' on object: {tmp.gameObject.name}, parent: {tmp.transform.parent?.name}");

                // Find the Image component - could be on parent, grandparent, or sibling
                Image headerImage = null;

                // Check parent
                if (tmp.transform.parent != null)
                {
                    headerImage = tmp.transform.parent.GetComponent<Image>();
                    if (headerImage != null)
                    {
                        Debug.Log($"[OptionsSceneFix] Found Image on parent: {tmp.transform.parent.name}, color: {headerImage.color}");
                    }
                }

                // Check grandparent if no image on parent
                if (headerImage == null && tmp.transform.parent?.parent != null)
                {
                    headerImage = tmp.transform.parent.parent.GetComponent<Image>();
                    if (headerImage != null)
                    {
                        Debug.Log($"[OptionsSceneFix] Found Image on grandparent: {tmp.transform.parent.parent.name}, color: {headerImage.color}");
                    }
                }

                // Check siblings (look for sibling named "Background" or with Image)
                if (headerImage == null && tmp.transform.parent != null)
                {
                    foreach (Transform sibling in tmp.transform.parent)
                    {
                        if (sibling != tmp.transform)
                        {
                            var siblingImage = sibling.GetComponent<Image>();
                            if (siblingImage != null)
                            {
                                headerImage = siblingImage;
                                Debug.Log($"[OptionsSceneFix] Found Image on sibling: {sibling.name}, color: {siblingImage.color}");
                                break;
                            }
                        }
                    }
                }

                // Check the text object itself for an Image
                if (headerImage == null)
                {
                    headerImage = tmp.GetComponent<Image>();
                    if (headerImage != null)
                    {
                        Debug.Log($"[OptionsSceneFix] Found Image on same object as text, color: {headerImage.color}");
                    }
                }

                if (headerImage != null)
                {
                    Undo.RecordObject(headerImage, "Fix Header Color");
                    Color oldColor = headerImage.color;
                    headerImage.color = headerColor;
                    Debug.Log($"[OptionsSceneFix] Fixed header image: {headerImage.gameObject.name} (text: {text}) - changed from {oldColor} to {headerColor}");
                    EditorUtility.SetDirty(headerImage);
                }
                else
                {
                    Debug.LogWarning($"[OptionsSceneFix] Could not find Image for header text: '{text}'");
                }

                // Fix text styling to match Video tab: Bold, size 28, vertically centered
                Undo.RecordObject(tmp, "Fix Header Text Style");
                tmp.fontStyle = headerFontStyle;
                tmp.fontSize = headerFontSize;
                tmp.alignment = headerAlignment;
                fixedCount++;
                Debug.Log($"[OptionsSceneFix] Fixed header text style: '{text}' - Bold={tmp.fontStyle}, Size={tmp.fontSize}, Alignment={tmp.alignment}");
                EditorUtility.SetDirty(tmp);

                // Fix the arrow/caret sibling if present
                FixHeaderArrow(tmp.transform.parent);

                // Fix the HorizontalLayoutGroup on the header container
                FixHeaderLayoutGroup(tmp.transform.parent);

                // Fix the content panel layout for this section
                FixSectionContentPanel(tmp.transform.parent?.parent);
            }
        }

        Debug.Log($"[OptionsSceneFix] Fixed {fixedCount} headers in Gameplay panel");
        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(gameplayPanel);
        }
    }

    private void FixControlsTabHeaders()
    {
        // Find ControlsPanel
        var controlsPanel = FindInScene("ControlsPanel");
        if (controlsPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find ControlsPanel. Make sure Options scene is open.", "OK");
            return;
        }

        Debug.Log($"[OptionsSceneFix] Found ControlsPanel: {controlsPanel.name}");

        int fixedCount = 0;

        // Find all TextMeshProUGUI components that look like headers
        var allTexts = controlsPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        Debug.Log($"[OptionsSceneFix] Found {allTexts.Length} TextMeshProUGUI components in ControlsPanel");

        foreach (var tmp in allTexts)
        {
            string text = tmp.text.Trim();

            // Check if this looks like a section header (all caps, known header names)
            bool isHeader = !string.IsNullOrEmpty(text) && text == text.ToUpper() && text.Length > 3 &&
                (text.Contains("HEART") || text.Contains("POWER") || text.Contains("SCULPT") ||
                 text.Contains("CAMERA") || text.Contains("MOVEMENT") || text.Contains("FOCUS") ||
                 text.Contains("MOUSE") || text.Contains("UTILITY") || text.Contains("OTHER") ||
                 text.Contains("MENU"));

            if (isHeader)
            {
                Debug.Log($"[OptionsSceneFix] Found Controls header text: '{text}' on object: {tmp.gameObject.name}");

                // Find the Image component for background color
                Image headerImage = null;

                // Check parent
                if (tmp.transform.parent != null)
                {
                    headerImage = tmp.transform.parent.GetComponent<Image>();
                }

                // Check grandparent if no image on parent
                if (headerImage == null && tmp.transform.parent?.parent != null)
                {
                    headerImage = tmp.transform.parent.parent.GetComponent<Image>();
                }

                if (headerImage != null)
                {
                    Undo.RecordObject(headerImage, "Fix Header Color");
                    headerImage.color = headerColor;
                    Debug.Log($"[OptionsSceneFix] Fixed Controls header image color: {headerImage.gameObject.name}");
                    EditorUtility.SetDirty(headerImage);
                }

                // Fix text styling to match Video tab
                Undo.RecordObject(tmp, "Fix Header Text Style");
                tmp.fontStyle = headerFontStyle;
                tmp.fontSize = headerFontSize;
                tmp.alignment = headerAlignment;
                fixedCount++;
                EditorUtility.SetDirty(tmp);
            }
        }

        Debug.Log($"[OptionsSceneFix] Fixed {fixedCount} headers in Controls panel");
        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(controlsPanel);
        }
    }

    /// <summary>
    /// Fix the arrow/caret element in a header to match Video tab style
    /// </summary>
    private void FixHeaderArrow(Transform headerParent)
    {
        if (headerParent == null) return;

        // Look for Arrow child
        Transform arrowTransform = headerParent.Find("Arrow");
        if (arrowTransform == null)
        {
            // Try other common names
            foreach (Transform child in headerParent)
            {
                if (child.name.ToLower().Contains("arrow") || child.name.ToLower().Contains("caret"))
                {
                    arrowTransform = child;
                    break;
                }
            }
        }

        if (arrowTransform == null)
        {
            Debug.Log($"[OptionsSceneFix] No arrow found in {headerParent.name}");
            return;
        }

        // Fix arrow RectTransform size
        RectTransform arrowRect = arrowTransform.GetComponent<RectTransform>();
        if (arrowRect != null)
        {
            Undo.RecordObject(arrowRect, "Fix Arrow Size");
            arrowRect.sizeDelta = arrowSize;
            EditorUtility.SetDirty(arrowRect);
            Debug.Log($"[OptionsSceneFix] Fixed arrow size to {arrowSize}");
        }

        // Fix arrow LayoutElement if present
        LayoutElement arrowLayout = arrowTransform.GetComponent<LayoutElement>();
        if (arrowLayout != null)
        {
            Undo.RecordObject(arrowLayout, "Fix Arrow Layout");
            arrowLayout.preferredWidth = arrowSize.x;
            arrowLayout.preferredHeight = arrowSize.y;
            EditorUtility.SetDirty(arrowLayout);
        }

        // Find the TextMeshProUGUI inside the arrow and fix it
        TextMeshProUGUI arrowText = arrowTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        if (arrowText != null)
        {
            Undo.RecordObject(arrowText, "Fix Arrow Text");
            arrowText.fontSize = arrowFontSize;

            // Update caret character - check if expanded or collapsed
            string currentText = arrowText.text.Trim();
            if (currentText == "\u25BC" || currentText == "v" || currentText == "V" || currentText == "▼")
            {
                // Expanded state
                arrowText.text = arrowExpandedChar;
            }
            else if (currentText == "\u25B6" || currentText == ">" || currentText == "▶")
            {
                // Collapsed state
                arrowText.text = arrowCollapsedChar;
            }

            // Fix alignment to match Video tab (bottom alignment)
            arrowText.alignment = TextAlignmentOptions.Bottom;
            EditorUtility.SetDirty(arrowText);
            Debug.Log($"[OptionsSceneFix] Fixed arrow text: size={arrowFontSize}, char='{arrowText.text}'");
        }
    }

    /// <summary>
    /// Fix the HorizontalLayoutGroup on a header to match Video tab style
    /// </summary>
    private void FixHeaderLayoutGroup(Transform headerParent)
    {
        if (headerParent == null) return;

        HorizontalLayoutGroup hlg = headerParent.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            Debug.Log($"[OptionsSceneFix] No HorizontalLayoutGroup on {headerParent.name}");
            return;
        }

        Undo.RecordObject(hlg, "Fix Header Layout Group");

        // Match Video tab settings:
        // Padding: Left=15, Right=15, Top=0, Bottom=0
        // ChildAlignment: 3 (center/middle)
        // Spacing: 10
        // ChildForceExpandHeight: 1
        // ChildControlHeight: 1
        hlg.padding = new RectOffset(15, 15, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;  // Keep left alignment for consistency
        hlg.spacing = 10;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        EditorUtility.SetDirty(hlg);
        Debug.Log($"[OptionsSceneFix] Fixed HorizontalLayoutGroup on {headerParent.name}");
    }

    /// <summary>
    /// Fix the content panel layout inside a collapsible section to match Video tab style
    /// </summary>
    private void FixSectionContentPanel(Transform sectionRoot)
    {
        if (sectionRoot == null) return;

        // Look for ContentPanel child
        Transform contentPanel = sectionRoot.Find("ContentPanel");
        if (contentPanel == null)
        {
            // Try other names
            foreach (Transform child in sectionRoot)
            {
                if (child.name.ToLower().Contains("content"))
                {
                    contentPanel = child;
                    break;
                }
            }
        }

        if (contentPanel == null)
        {
            Debug.Log($"[OptionsSceneFix] No ContentPanel found in {sectionRoot.name}");
            return;
        }

        // Fix VerticalLayoutGroup settings
        VerticalLayoutGroup vlg = contentPanel.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            Undo.RecordObject(vlg, "Fix Content Panel Layout");
            vlg.padding = new RectOffset(contentPanelPaddingLeft, contentPanelPaddingRight, contentPanelPaddingTop, contentPanelPaddingBottom);
            vlg.spacing = contentPanelSpacing;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            EditorUtility.SetDirty(vlg);
            Debug.Log($"[OptionsSceneFix] Fixed ContentPanel layout on {contentPanel.name}");
        }

        // Fix all row heights in this content panel
        foreach (Transform row in contentPanel)
        {
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout != null)
            {
                Undo.RecordObject(rowLayout, "Fix Row Height");
                rowLayout.minHeight = 40;
                rowLayout.preferredHeight = 40;
                EditorUtility.SetDirty(rowLayout);
            }

            // Fix label sizes in the row (set height to 0 so it doesn't fight with layout)
            foreach (Transform child in row)
            {
                if (child.name.ToLower().Contains("label") || child.name.ToLower().Contains("text"))
                {
                    RectTransform labelRect = child.GetComponent<RectTransform>();
                    if (labelRect != null)
                    {
                        Undo.RecordObject(labelRect, "Fix Label Size");
                        // Keep width, set height to 0
                        labelRect.sizeDelta = new Vector2(labelRect.sizeDelta.x, 0);
                        EditorUtility.SetDirty(labelRect);
                    }
                }
            }
        }
    }

    private void RemovePlayerControlsSection()
    {
        // Find GameplayPanel
        var gameplayPanel = FindInScene("GameplayPanel");
        if (gameplayPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find GameplayPanel. Make sure Options scene is open.", "OK");
            return;
        }

        Debug.Log($"[OptionsSceneFix] Searching for Player Controls in: {gameplayPanel.name}");

        var toRemove = new List<GameObject>();

        // Find all children and check for player controls related items
        var allChildren = gameplayPanel.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            string nameLower = child.name.ToLower();

            // Find Player Controls header/section
            if (nameLower.Contains("playercontrol") || nameLower.Contains("player_control"))
            {
                toRemove.Add(child.gameObject);
                Debug.Log($"[OptionsSceneFix] Found player controls item: {child.name}");
            }

            // Find heart power key rows (dropdown-based)
            if ((nameLower.Contains("heartpower") || nameLower.Contains("murmuring") ||
                 nameLower.Contains("grasp") || nameLower.Contains("devour") ||
                 nameLower.Contains("sculpting")) &&
                (nameLower.Contains("key") || nameLower.Contains("dropdown")))
            {
                // Make sure it has a dropdown component (not the Controls tab buttons)
                if (child.GetComponentInChildren<TMP_Dropdown>(true) != null)
                {
                    toRemove.Add(child.gameObject);
                    Debug.Log($"[OptionsSceneFix] Found heart power dropdown: {child.name}");
                }
            }

            // Screenshot key row (not path)
            if (nameLower.Contains("screenshot") && nameLower.Contains("key") && !nameLower.Contains("path"))
            {
                if (child.GetComponentInChildren<TMP_Dropdown>(true) != null)
                {
                    toRemove.Add(child.gameObject);
                    Debug.Log($"[OptionsSceneFix] Found screenshot key dropdown: {child.name}");
                }
            }
        }

        // Remove duplicates and delete
        var uniqueToRemove = new HashSet<GameObject>(toRemove);
        int removedCount = 0;
        foreach (var obj in uniqueToRemove)
        {
            if (obj != null && obj != gameplayPanel)
            {
                Debug.Log($"[OptionsSceneFix] Removing: {obj.name}");
                Undo.DestroyObjectImmediate(obj);
                removedCount++;
            }
        }

        Debug.Log($"[OptionsSceneFix] Removed {removedCount} objects from Player Controls section");
        if (removedCount > 0)
        {
            EditorUtility.SetDirty(gameplayPanel);
        }
    }

    private void FixSculptMenuBindings()
    {
        // Just delegate to the full rebuild - it's cleaner to rebuild everything
        RebuildAllControlsTabSections();
    }

    /// <summary>
    /// Wire up the ControlsPanel to OptionsManager serialized field and create tab button if needed
    /// </summary>
    private void WireUpControlsPanelToOptionsManager(GameObject controlsPanel)
    {
        var allManagers = Resources.FindObjectsOfTypeAll<OptionsManager>();
        OptionsManager manager = null;

        foreach (var m in allManagers)
        {
            if (m.gameObject.scene.IsValid())
            {
                manager = m;
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find OptionsManager in scene to wire up ControlsPanel");
            return;
        }

        var so = new SerializedObject(manager);
        var controlsPanelProp = so.FindProperty("controlsPanel");
        if (controlsPanelProp != null)
        {
            controlsPanelProp.objectReferenceValue = controlsPanel;
            so.ApplyModifiedProperties();
            Debug.Log("[OptionsSceneFix] Wired ControlsPanel to OptionsManager");
        }

        // Also create a Controls tab button if it doesn't exist
        CreateControlsTabButtonIfNeeded(manager);
    }

    /// <summary>
    /// Create a Controls tab button by duplicating an existing tab button
    /// </summary>
    private void CreateControlsTabButtonIfNeeded(OptionsManager manager)
    {
        // Check if controlsTabButton field exists and needs to be populated
        var so = new SerializedObject(manager);
        var controlsTabProp = so.FindProperty("controlsTabButton");

        // If the field doesn't exist in OptionsManager, we need to add it
        // For now, just check if we can find an existing controls tab button
        var existingControlsTab = FindInScene("ControlsTabButton");
        if (existingControlsTab != null)
        {
            Debug.Log("[OptionsSceneFix] ControlsTabButton already exists");
            return;
        }

        // Find TabBar to add the button to
        var tabBar = FindInScene("TabBar");
        if (tabBar == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find TabBar to add ControlsTabButton");
            return;
        }

        // Find an existing tab button to duplicate
        var audioTabButton = FindInScene("AudioTabButton");
        if (audioTabButton == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find AudioTabButton to use as template");
            return;
        }

        // Duplicate the audio tab button
        GameObject controlsTabButton = Object.Instantiate(audioTabButton, tabBar.transform);
        Undo.RegisterCreatedObjectUndo(controlsTabButton, "Create ControlsTabButton");
        controlsTabButton.name = "ControlsTabButton";

        // Update the button text (uppercase to match other tabs)
        var tmp = controlsTabButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = "CONTROLS";
        }

        // Position it after the audio tab (as last tab)
        controlsTabButton.transform.SetAsLastSibling();

        // Wire up to OptionsManager if the field exists
        if (controlsTabProp != null)
        {
            var button = controlsTabButton.GetComponent<Button>();
            if (button != null)
            {
                controlsTabProp.objectReferenceValue = button;
                so.ApplyModifiedProperties();
                Debug.Log("[OptionsSceneFix] Wired ControlsTabButton to OptionsManager");
            }
        }
        else
        {
            Debug.LogWarning("[OptionsSceneFix] OptionsManager doesn't have controlsTabButton field - may need to add it manually");
        }

        Debug.Log("[OptionsSceneFix] Created ControlsTabButton");
    }

    /// <summary>
    /// Find a template section from the Video tab to duplicate
    /// </summary>
    private GameObject FindVideoTabTemplateSection()
    {
        var videoPanel = FindInScene("VideoPanel");
        if (videoPanel == null)
        {
            Debug.LogError("[OptionsSceneFix] Could not find VideoPanel for template");
            return null;
        }

        // Find DISPLAYSETTINGSSection or any section with CollapsibleSection component
        foreach (Transform child in videoPanel.transform)
        {
            var collapsible = child.GetComponent<CollapsibleSection>();
            if (collapsible != null)
            {
                Debug.Log($"[OptionsSceneFix] Found template section: {child.name}");
                return child.gameObject;
            }
        }

        Debug.LogError("[OptionsSceneFix] No template section found in VideoPanel");
        return null;
    }

    /// <summary>
    /// Find a template row from the Video tab to duplicate (toggle or slider row)
    /// </summary>
    private GameObject FindVideoTabTemplateRow(string rowType)
    {
        var videoPanel = FindInScene("VideoPanel");
        if (videoPanel == null) return null;

        // Search for a row of the specified type
        foreach (Transform section in videoPanel.transform)
        {
            var collapsible = section.GetComponent<CollapsibleSection>();
            if (collapsible == null) continue;

            // Find content panel
            Transform contentPanel = section.Find("ContentPanel");
            if (contentPanel == null) continue;

            foreach (Transform row in contentPanel)
            {
                string rowName = row.name.ToLower();
                if (rowType == "toggle" && rowName.Contains("toggle"))
                {
                    Debug.Log($"[OptionsSceneFix] Found template toggle row: {row.name}");
                    return row.gameObject;
                }
                if (rowType == "slider" && (rowName.Contains("slider") || rowName.Contains("fov") || rowName.Contains("zoom")))
                {
                    Debug.Log($"[OptionsSceneFix] Found template slider row: {row.name}");
                    return row.gameObject;
                }
            }
        }

        // Try looking in AudioPanel as fallback
        var audioPanel = FindInScene("AudioPanel");
        if (audioPanel != null)
        {
            foreach (Transform section in audioPanel.transform)
            {
                Transform contentPanel = section.Find("ContentPanel");
                if (contentPanel == null) continue;

                foreach (Transform row in contentPanel)
                {
                    if (row.GetComponentInChildren<Slider>(true) != null)
                    {
                        Debug.Log($"[OptionsSceneFix] Found template slider row from AudioPanel: {row.name}");
                        return row.gameObject;
                    }
                }
            }
        }

        Debug.LogWarning($"[OptionsSceneFix] No template {rowType} row found");
        return null;
    }

    /// <summary>
    /// Find the content panel in a section - searches recursively for common names
    /// </summary>
    private Transform FindContentPanelInSection(Transform section)
    {
        // Try direct child first
        Transform contentPanel = section.Find("ContentPanel");
        if (contentPanel != null) return contentPanel;

        // Try common variations
        string[] possibleNames = { "Content", "ContentPanel", "content", "Panel", "Items" };
        foreach (string name in possibleNames)
        {
            contentPanel = section.Find(name);
            if (contentPanel != null) return contentPanel;
        }

        // Search all children for anything containing "content"
        foreach (Transform child in section)
        {
            if (child.name.ToLower().Contains("content"))
            {
                return child;
            }
        }

        // If still not found, log all children for debugging
        Debug.LogWarning($"[OptionsSceneFix] Could not find ContentPanel in {section.name}. Children are:");
        foreach (Transform child in section)
        {
            Debug.Log($"  - {child.name}");
        }

        return null;
    }

    /// <summary>
    /// Find the scroll content area inside a panel (handles VideoPanel -> Viewport -> Content hierarchy)
    /// </summary>
    private Transform FindScrollContentInPanel(GameObject panel)
    {
        // Look for ScrollRect component to find the content target
        var scrollRect = panel.GetComponent<UnityEngine.UI.ScrollRect>();
        if (scrollRect != null && scrollRect.content != null)
        {
            return scrollRect.content;
        }

        // Try to find Viewport -> Content hierarchy manually
        Transform viewport = panel.transform.Find("Viewport");
        if (viewport != null)
        {
            Transform content = viewport.Find("Content");
            if (content != null) return content;
        }

        // Search recursively for a Content object with VerticalLayoutGroup
        foreach (Transform child in panel.transform)
        {
            if (child.name == "Viewport" || child.name == "viewport")
            {
                foreach (Transform grandChild in child)
                {
                    if (grandChild.name == "Content" || grandChild.name == "content")
                    {
                        return grandChild;
                    }
                }
            }
            // Also check if the child itself is Content
            if (child.name == "Content" && child.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() != null)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Duplicate a section template and configure it with new header text
    /// </summary>
    private GameObject DuplicateSection(GameObject template, Transform parent, string headerText, int siblingIndex)
    {
        GameObject newSection = Object.Instantiate(template, parent);
        Undo.RegisterCreatedObjectUndo(newSection, "Duplicate Section");

        string safeName = headerText.Replace(" ", "");
        newSection.name = safeName + "Section";
        newSection.transform.SetSiblingIndex(siblingIndex);

        // Log the structure for debugging
        Debug.Log($"[OptionsSceneFix] Duplicated section {newSection.name} from template {template.name}. Children:");
        foreach (Transform child in newSection.transform)
        {
            Debug.Log($"  - {child.name}");
        }

        // Update header text and fix word wrap
        var collapsible = newSection.GetComponent<CollapsibleSection>();
        if (collapsible != null)
        {
            // Find header text via serialized property or by searching
            var so = new SerializedObject(collapsible);
            var headerTextProp = so.FindProperty("headerText");
            if (headerTextProp != null && headerTextProp.objectReferenceValue != null)
            {
                var headerTMP = headerTextProp.objectReferenceValue as TextMeshProUGUI;
                if (headerTMP != null)
                {
                    headerTMP.text = headerText;
                    // Prevent word wrap - header should be single line
                    headerTMP.enableWordWrapping = false;
                    headerTMP.overflowMode = TextOverflowModes.Overflow;

                    // Ensure the header text can expand to fill available width
                    var headerLayout = headerTMP.GetComponent<LayoutElement>();
                    if (headerLayout == null)
                    {
                        headerLayout = headerTMP.gameObject.AddComponent<LayoutElement>();
                    }
                    headerLayout.flexibleWidth = 1;

                    // Also ensure the RectTransform stretches properly
                    var headerRect = headerTMP.GetComponent<RectTransform>();
                    if (headerRect != null)
                    {
                        // Don't constrain width - let layout group handle it
                        headerRect.sizeDelta = new Vector2(0, headerRect.sizeDelta.y);
                    }
                }
            }
        }

        // Find and clear the content panel - must destroy all children
        Transform contentPanel = FindContentPanelInSection(newSection.transform);
        if (contentPanel != null)
        {
            // Must destroy children in reverse order and use DestroyImmediate with allowDestroyingAssets=false
            int childCount = contentPanel.childCount;
            Debug.Log($"[OptionsSceneFix] ContentPanel has {childCount} children to clear in {newSection.name}");

            // Destroy from last to first to avoid index shifting issues
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = contentPanel.GetChild(i);
                if (child != null)
                {
                    string childName = child.name;
                    Undo.DestroyObjectImmediate(child.gameObject);
                    Debug.Log($"[OptionsSceneFix] Destroyed child: {childName}");
                }
            }

            Debug.Log($"[OptionsSceneFix] Cleared {childCount} children from ContentPanel of {newSection.name}, remaining: {contentPanel.childCount}");
        }
        else
        {
            Debug.LogWarning($"[OptionsSceneFix] ContentPanel not found in {newSection.name}");
        }

        Debug.Log($"[OptionsSceneFix] Created section: {newSection.name}");
        return newSection;
    }

    /// <summary>
    /// Duplicate a row template and configure it as a binding row.
    /// Since Video tab rows contain sliders/dropdowns, we need to completely rebuild the row content.
    /// </summary>
    private void DuplicateAsBindingRow(GameObject templateRow, Transform contentPanel, string bindingName, string labelText, int siblingIndex)
    {
        // Always create binding rows from scratch - duplicating Video tab rows is unreliable
        // because their structure (slider with fill area, handle, etc.) doesn't convert cleanly
        CreateBindingRow(contentPanel, bindingName, labelText, siblingIndex);
    }

    /// <summary>
    /// Completely rebuild all Controls tab sections by duplicating Video tab structure
    /// </summary>
    private void RebuildAllControlsTabSections()
    {
        // Find ControlsPanel - create it if it doesn't exist
        var controlsPanel = FindInScene("ControlsPanel");
        if (controlsPanel == null)
        {
            // Create ControlsPanel by copying from VideoPanel structure
            var videoPanel = FindInScene("VideoPanel");
            if (videoPanel == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not find VideoPanel to use as template. Make sure Options scene is open.", "OK");
                return;
            }

            // Find the parent container (should be the same parent as other panels)
            Transform panelParent = videoPanel.transform.parent;

            // Create ControlsPanel as a duplicate of VideoPanel's structure (preserves ScrollRect/Viewport/Content)
            controlsPanel = Object.Instantiate(videoPanel, panelParent);
            Undo.RegisterCreatedObjectUndo(controlsPanel, "Create ControlsPanel");
            controlsPanel.name = "ControlsPanel";

            // Set it inactive by default (will be activated by tab selection)
            controlsPanel.SetActive(false);

            Debug.Log($"[OptionsSceneFix] Created ControlsPanel from VideoPanel template (preserving scroll structure)");

            // Wire up the new ControlsPanel to OptionsManager
            WireUpControlsPanelToOptionsManager(controlsPanel);
        }

        // Find the scroll content area - this is where sections should be added
        // VideoPanel has: VideoPanel -> Viewport -> Content (with VerticalLayoutGroup)
        Transform scrollContentParent = FindScrollContentInPanel(controlsPanel);
        if (scrollContentParent == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find scroll Content in ControlsPanel, using panel directly");
            scrollContentParent = controlsPanel.transform;
        }
        else
        {
            Debug.Log($"[OptionsSceneFix] Found scroll content area: {scrollContentParent.name}");
        }

        // Find template section from Video tab
        GameObject templateSection = FindVideoTabTemplateSection();
        if (templateSection == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find template section in VideoPanel.", "OK");
            return;
        }

        // Find template row for binding rows (any row will do - we'll convert it)
        GameObject templateRow = FindVideoTabTemplateRow("slider");

        Debug.Log($"[OptionsSceneFix] Rebuilding Controls tab using template from: {templateSection.name}");

        // Define all sections and their bindings
        var sections = new[]
        {
            ("SCULPT MENU", new[] {
                ("SculptPond", "Place Pond"),
                ("SculptLantern", "Place Lantern"),
                ("SculptRing", "Place Fairy Ring"),
                ("SculptRemove", "Remove Prop")
            }),
            ("CAMERA MOVEMENT", new[] {
                ("MoveForward", "Move Forward"),
                ("MoveBackward", "Move Backward"),
                ("TurnLeft", "Turn Left"),
                ("TurnRight", "Turn Right")
            }),
            ("CAMERA FOCUS", new[] {
                ("FocusHeart", "Focus Heart"),
                ("FocusEntrance", "Focus Entrance"),
                ("FocusVisitor", "Focus Visitor")
            }),
            ("CAMERA MOUSE", new[] {
                ("Orbit", "Orbit Camera"),
                ("Pan", "Pan Camera")
            }),
            ("UTILITY", new[] {
                ("Screenshot", "Screenshot")
            })
        };

        // Delete only section children from scroll content area (preserve scroll structure)
        var toDelete = new List<GameObject>();
        for (int i = scrollContentParent.childCount - 1; i >= 0; i--)
        {
            var child = scrollContentParent.GetChild(i).gameObject;
            // Only delete section objects, not scroll structure (Viewport, Scrollbar, etc.)
            if (child.name.Contains("Section") || child.name.Contains("SETTINGS") ||
                child.name.Contains("MENU") || child.name.Contains("MOVEMENT") ||
                child.name.Contains("FOCUS") || child.name.Contains("MOUSE") ||
                child.name.Contains("UTILITY") || child.name.Contains("DISPLAY") ||
                child.name.Contains("SCREENSHOT") || child.name.Contains("Row"))
            {
                toDelete.Add(child);
            }
        }

        foreach (var obj in toDelete)
        {
            if (obj != null)
            {
                Debug.Log($"[OptionsSceneFix] Deleting section from ControlsPanel: {obj.name}");
                Object.DestroyImmediate(obj);
            }
        }
        Debug.Log($"[OptionsSceneFix] Cleared {toDelete.Count} sections from ControlsPanel scroll content");

        // Create all sections by duplicating the template
        int insertIndex = 0;
        foreach (var (sectionName, bindings) in sections)
        {
            // Duplicate section from template - add to scroll content area
            GameObject newSection = DuplicateSection(templateSection, scrollContentParent, sectionName, insertIndex++);

            // Find content panel and add binding rows - use helper that handles "Content" vs "ContentPanel"
            Transform sectionContentPanel = FindContentPanelInSection(newSection.transform);
            if (sectionContentPanel != null)
            {
                Debug.Log($"[OptionsSceneFix] Adding {bindings.Length} binding rows to {sectionName}");
                int rowIndex = 0;
                foreach (var (bindingName, label) in bindings)
                {
                    if (templateRow != null)
                    {
                        DuplicateAsBindingRow(templateRow, sectionContentPanel, bindingName, label, rowIndex++);
                    }
                    else
                    {
                        CreateBindingRow(sectionContentPanel, bindingName, label, rowIndex++);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[OptionsSceneFix] Could not find content panel in section {sectionName}");
            }
        }

        Debug.Log($"[OptionsSceneFix] Rebuilt Controls tab with {sections.Length} sections");
        EditorUtility.SetDirty(controlsPanel);
    }

    /// <summary>
    /// Duplicate a row template and configure it as a slider row.
    /// Since Video tab rows have complex structures that don't convert cleanly, always create from scratch.
    /// </summary>
    private void DuplicateAsSliderRow(GameObject templateRow, Transform contentPanel, string settingName, string labelText, float min, float max, float defaultValue, bool isPercent, int siblingIndex)
    {
        // Always create slider rows from scratch - duplicating Video tab rows is unreliable
        CreateSliderRow(contentPanel, settingName, labelText, min, max, defaultValue, isPercent, siblingIndex);
    }

    /// <summary>
    /// Duplicate a row template and configure it as a toggle row.
    /// Since Video tab rows have complex structures that don't convert cleanly, always create from scratch.
    /// </summary>
    private void DuplicateAsToggleRow(GameObject templateRow, Transform contentPanel, string settingName, string labelText, bool defaultValue, int siblingIndex)
    {
        // Always create toggle rows from scratch - duplicating Video tab rows is unreliable
        CreateToggleRow(contentPanel, settingName, labelText, defaultValue, siblingIndex);
    }

    /// <summary>
    /// Completely rebuild the Gameplay tab by duplicating Video tab structure
    /// </summary>
    private void RebuildGameplayTab()
    {
        var gameplayPanel = FindInScene("GameplayPanel");
        if (gameplayPanel == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find GameplayPanel. Make sure Options scene is open.", "OK");
            return;
        }

        // Find OptionsManager to wire up controls
        OptionsManager optionsManager = null;
        var allManagers = Resources.FindObjectsOfTypeAll<OptionsManager>();
        foreach (var m in allManagers)
        {
            if (m.gameObject.scene.IsValid())
            {
                optionsManager = m;
                break;
            }
        }

        if (optionsManager == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find OptionsManager - controls won't be wired up");
        }

        SerializedObject managerSO = optionsManager != null ? new SerializedObject(optionsManager) : null;

        // Find template section and rows from Video tab
        GameObject templateSection = FindVideoTabTemplateSection();
        if (templateSection == null)
        {
            EditorUtility.DisplayDialog("Error", "Could not find template section in VideoPanel.", "OK");
            return;
        }

        Debug.Log($"[OptionsSceneFix] Rebuilding Gameplay tab using template from: {templateSection.name}");

        // Find the scroll content area - this is where sections should be added
        Transform scrollContentParent = FindScrollContentInPanel(gameplayPanel);
        if (scrollContentParent == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find scroll Content in GameplayPanel, using panel directly");
            scrollContentParent = gameplayPanel.transform;
        }
        else
        {
            Debug.Log($"[OptionsSceneFix] Found scroll content area in GameplayPanel: {scrollContentParent.name}");
        }

        // Delete only section children from scroll content area (preserve scroll structure)
        var toDelete = new List<GameObject>();
        for (int i = scrollContentParent.childCount - 1; i >= 0; i--)
        {
            var child = scrollContentParent.GetChild(i).gameObject;
            // Only delete section objects, not scroll structure
            if (child.name.Contains("Section") || child.name.Contains("SETTINGS") ||
                child.name.Contains("SPAWNING") || child.name.Contains("FLOW") ||
                child.name.Contains("VISITOR") || child.name.Contains("Row"))
            {
                toDelete.Add(child);
            }
        }

        foreach (var obj in toDelete)
        {
            if (obj != null)
            {
                Debug.Log($"[OptionsSceneFix] Deleting section from GameplayPanel: {obj.name}");
                Object.DestroyImmediate(obj);
            }
        }
        Debug.Log($"[OptionsSceneFix] Cleared {toDelete.Count} sections from GameplayPanel scroll content");

        int insertIndex = 0;

        // VISITOR SETTINGS section
        GameObject visitorSection = DuplicateSection(templateSection, scrollContentParent, "VISITOR SETTINGS", insertIndex++);
        Transform visitorContent = FindContentPanelInSection(visitorSection.transform);
        if (visitorContent != null)
        {
            Debug.Log($"[OptionsSceneFix] Found visitorContent: {visitorContent.name}, adding controls...");
            var visitorSpeedSlider = CreateSliderRowWithRef(visitorContent, "visitorSpeed", "Movement Speed", 0.5f, 10f, 3f, false, 0);
            WireSliderToManager(managerSO, "visitorSpeedSlider", "visitorSpeedText", visitorSpeedSlider);

            var confusionToggle = CreateToggleRowWithRef(visitorContent, "confusionEnabled", "Enable Confusion", true, 1);
            WireToggleToManager(managerSO, "confusionEnabledToggle", confusionToggle);

            var confusionChanceSlider = CreateSliderRowWithRef(visitorContent, "confusionChance", "Confusion Chance", 0f, 1f, 0.25f, true, 2);
            WireSliderToManager(managerSO, "confusionChanceSlider", "confusionChanceText", confusionChanceSlider);

            var confusionMinSlider = CreateSliderRowWithRef(visitorContent, "confusionDistanceMin", "Min Confusion Distance", 1f, 50f, 15f, false, 3);
            WireSliderToManager(managerSO, "confusionDistanceMinSlider", "confusionDistanceMinText", confusionMinSlider);

            var confusionMaxSlider = CreateSliderRowWithRef(visitorContent, "confusionDistanceMax", "Max Confusion Distance", 1f, 50f, 20f, false, 4);
            WireSliderToManager(managerSO, "confusionDistanceMaxSlider", "confusionDistanceMaxText", confusionMaxSlider);
        }
        else
        {
            Debug.LogError($"[OptionsSceneFix] Could not find content panel in visitorSection!");
        }

        // SPAWNING SETTINGS section
        GameObject spawningSection = DuplicateSection(templateSection, scrollContentParent, "SPAWNING SETTINGS", insertIndex++);
        Transform spawningContent = FindContentPanelInSection(spawningSection.transform);
        if (spawningContent != null)
        {
            var spawnIntervalSlider = CreateSliderRowWithRef(spawningContent, "spawnInterval", "Spawn Interval", 0.1f, 5f, 5f, false, 0);
            WireSliderToManager(managerSO, "spawnIntervalSlider", "spawnIntervalText", spawnIntervalSlider);

            var redCapToggle = CreateToggleRowWithRef(spawningContent, "enableRedCap", "Enable Red Cap", true, 1);
            WireToggleToManager(managerSO, "enableRedCapToggle", redCapToggle);
        }

        // GAME FLOW section
        GameObject flowSection = DuplicateSection(templateSection, scrollContentParent, "GAME FLOW", insertIndex++);
        Transform flowContent = FindContentPanelInSection(flowSection.transform);
        if (flowContent != null)
        {
            var autoStartToggle = CreateToggleRowWithRef(flowContent, "autoStartNextWave", "Auto Start Next Wave", false, 0);
            WireToggleToManager(managerSO, "autoStartNextWaveToggle", autoStartToggle);

            var autoStartDelaySlider = CreateSliderRowWithRef(flowContent, "autoStartDelay", "Auto Start Delay", 0f, 10f, 2f, false, 1);
            WireSliderToManager(managerSO, "autoStartDelaySlider", "autoStartDelayText", autoStartDelaySlider);

            var startingEssenceSlider = CreateSliderRowWithRef(flowContent, "startingEssence", "Starting Essence", 0f, 1000f, 100f, false, 2);
            WireSliderToManager(managerSO, "startingEssenceSlider", "startingEssenceText", startingEssenceSlider);
        }

        if (managerSO != null)
        {
            managerSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(optionsManager);
        }

        Debug.Log("[OptionsSceneFix] Rebuilt Gameplay tab with 3 sections");
        EditorUtility.SetDirty(gameplayPanel);
    }

    /// <summary>
    /// Fix the Screenshot Directory row in the Video tab to match other row layouts
    /// and add a Browse button for directory selection
    /// </summary>
    private void FixScreenshotDirectoryRow()
    {
        // Find ScreenshotDirectoryRow
        var screenshotRow = FindInScene("ScreenshotDirectoryRow");
        if (screenshotRow == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find ScreenshotDirectoryRow");
            return;
        }

        Debug.Log($"[OptionsSceneFix] Found ScreenshotDirectoryRow: {screenshotRow.name}");

        // DO NOT change row's RectTransform - it's positioned by the parent's VerticalLayoutGroup
        // DO NOT change HorizontalLayoutGroup padding - other rows in this section use padding 0

        // Find and fix the Label to match ScreenshotKeyRow's label (which works correctly)
        // ScreenshotKeyRow label: sizeDelta=(300,40), preferredWidth=300, anchoredPosition=(150,-20)
        Transform labelTransform = screenshotRow.transform.Find("Label");
        if (labelTransform != null)
        {
            var labelRect = labelTransform.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                Undo.RecordObject(labelRect, "Fix Label Size");
                // Match ScreenshotKeyRow's label: anchors (0,1), sizeDelta (300,40), anchoredPosition (150,-20)
                labelRect.anchorMin = new Vector2(0, 1);
                labelRect.anchorMax = new Vector2(0, 1);
                labelRect.sizeDelta = new Vector2(300, 40);
                labelRect.anchoredPosition = new Vector2(150, -20);  // Center of 300 width
                EditorUtility.SetDirty(labelRect);
            }

            var labelLayout = labelTransform.GetComponent<LayoutElement>();
            if (labelLayout != null)
            {
                Undo.RecordObject(labelLayout, "Fix Label Layout");
                labelLayout.preferredWidth = 300;
                labelLayout.flexibleWidth = -1;
                EditorUtility.SetDirty(labelLayout);
            }

            Debug.Log("[OptionsSceneFix] Fixed Label to match ScreenshotKeyRow");
        }

        // Find the ScreenshotDirectoryInput
        Transform inputTransform = screenshotRow.transform.Find("ScreenshotDirectoryInput");
        if (inputTransform == null)
        {
            // Try finding by component
            var inputFields = screenshotRow.GetComponentsInChildren<TMP_InputField>(true);
            if (inputFields.Length > 0)
            {
                inputTransform = inputFields[0].transform;
            }
        }

        if (inputTransform != null)
        {
            var inputRect = inputTransform.GetComponent<RectTransform>();
            if (inputRect != null)
            {
                Undo.RecordObject(inputRect, "Fix Input Size");
                // Position after label (300) with spacing (15), width 350 to fit browse button
                inputRect.anchorMin = new Vector2(0, 1);
                inputRect.anchorMax = new Vector2(0, 1);
                inputRect.sizeDelta = new Vector2(350, 40);
                inputRect.anchoredPosition = new Vector2(490, -20);  // 300 (label) + 15 (spacing) + 175 (half of 350)
                EditorUtility.SetDirty(inputRect);
            }

            var inputLayout = inputTransform.GetComponent<LayoutElement>();
            if (inputLayout != null)
            {
                Undo.RecordObject(inputLayout, "Fix Input Layout");
                inputLayout.preferredWidth = 350;
                inputLayout.flexibleWidth = -1;
                EditorUtility.SetDirty(inputLayout);
            }

            // Check if the input field has proper text overflow handling
            var inputField = inputTransform.GetComponent<TMP_InputField>();
            if (inputField != null && inputField.textComponent != null)
            {
                Undo.RecordObject(inputField.textComponent, "Fix Input Text Overflow");
                inputField.textComponent.overflowMode = TextOverflowModes.Ellipsis;
                inputField.textComponent.enableWordWrapping = false;
                EditorUtility.SetDirty(inputField.textComponent);
            }

            Debug.Log("[OptionsSceneFix] Fixed Input field size and overflow");

            // Check if Browse button already exists
            Transform browseButton = screenshotRow.transform.Find("BrowseButton");
            if (browseButton == null)
            {
                // Create Browse button
                TMP_FontAsset tmpFont = FindTMPFont(screenshotRow.transform);

                GameObject buttonObj = new GameObject("BrowseButton");
                Undo.RegisterCreatedObjectUndo(buttonObj, "Create Browse Button");
                buttonObj.transform.SetParent(screenshotRow.transform, false);
                buttonObj.transform.SetAsLastSibling();

                var buttonRect = buttonObj.AddComponent<RectTransform>();
                // Position after input field: 300 (label) + 15 (space) + 350 (input) + 15 (space) + 40 (half of 80)
                buttonRect.anchorMin = new Vector2(0, 1);
                buttonRect.anchorMax = new Vector2(0, 1);
                buttonRect.sizeDelta = new Vector2(80, 40);
                buttonRect.anchoredPosition = new Vector2(720, -20);  // 300 + 15 + 350 + 15 + 40 = 720

                var buttonImage = buttonObj.AddComponent<Image>();
                buttonImage.color = new Color(0.3f, 0.4f, 0.5f, 1f);

                var button = buttonObj.AddComponent<Button>();
                button.targetGraphic = buttonImage;

                var buttonLayout = buttonObj.AddComponent<LayoutElement>();
                buttonLayout.preferredWidth = 80;
                buttonLayout.preferredHeight = 40;
                buttonLayout.flexibleWidth = -1;

                // Add text to button
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);

                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var textTMP = textObj.AddComponent<TextMeshProUGUI>();
                textTMP.text = "Browse";
                textTMP.fontSize = 18;
                textTMP.alignment = TextAlignmentOptions.Center;
                textTMP.color = Color.white;
                if (tmpFont != null) textTMP.font = tmpFont;

                // Wire up the browse button to OptionsManager
                WireBrowseButtonToManager(button);

                Debug.Log("[OptionsSceneFix] Created Browse button");
            }
            else
            {
                Debug.Log("[OptionsSceneFix] Browse button already exists");
                // Fix position of existing button
                var existingButtonRect = browseButton.GetComponent<RectTransform>();
                if (existingButtonRect != null)
                {
                    Undo.RecordObject(existingButtonRect, "Fix Browse Button Position");
                    existingButtonRect.anchorMin = new Vector2(0, 1);
                    existingButtonRect.anchorMax = new Vector2(0, 1);
                    existingButtonRect.sizeDelta = new Vector2(80, 40);
                    existingButtonRect.anchoredPosition = new Vector2(720, -20);
                    EditorUtility.SetDirty(existingButtonRect);
                }
                // Make sure it's wired up
                var existingButton = browseButton.GetComponent<Button>();
                if (existingButton != null)
                {
                    WireBrowseButtonToManager(existingButton);
                }
            }
        }
        else
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find ScreenshotDirectoryInput");
        }

        EditorUtility.SetDirty(screenshotRow);
        Debug.Log("[OptionsSceneFix] Fixed Screenshot Directory row");
    }

    /// <summary>
    /// Wire the browse button to OptionsManager's browseScreenshotPathButton field
    /// </summary>
    private void WireBrowseButtonToManager(Button button)
    {
        var allManagers = Resources.FindObjectsOfTypeAll<OptionsManager>();
        OptionsManager manager = null;

        foreach (var m in allManagers)
        {
            if (m.gameObject.scene.IsValid())
            {
                manager = m;
                break;
            }
        }

        if (manager == null)
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find OptionsManager to wire browse button");
            return;
        }

        var so = new SerializedObject(manager);
        var buttonProp = so.FindProperty("browseScreenshotPathButton");
        if (buttonProp != null)
        {
            buttonProp.objectReferenceValue = button;
            so.ApplyModifiedProperties();
            Debug.Log("[OptionsSceneFix] Wired Browse button to OptionsManager.browseScreenshotPathButton");
        }
        else
        {
            Debug.LogWarning("[OptionsSceneFix] Could not find browseScreenshotPathButton field in OptionsManager");
        }
    }

    /// <summary>
    /// Create a slider row and return references to slider and text components
    /// Matches Video tab styling: childControlWidth=false, fixed preferredWidths on children
    /// </summary>
    private (Slider slider, TextMeshProUGUI text) CreateSliderRowWithRef(Transform parent, string settingName, string labelText, float min, float max, float defaultValue, bool isPercent, int siblingIndex)
    {
        TMP_FontAsset tmpFont = FindTMPFont(parent);

        GameObject rowObj = new GameObject(settingName + "_Row");
        Undo.RegisterCreatedObjectUndo(rowObj, "Create Slider Row");
        rowObj.transform.SetParent(parent, false);
        rowObj.transform.SetSiblingIndex(siblingIndex);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0, 40);

        // Match Video tab HorizontalLayoutGroup: childControlWidth=0 (false), spacing=15
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;  // Video tab uses false - children control their own width
        rowLayout.childControlHeight = true;
        rowLayout.spacing = 15;  // Video tab spacing
        rowLayout.padding = new RectOffset(15, 15, 0, 0);  // Video tab padding

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label - match Video tab: sizeDelta=(300,40), preferredWidth=300, flexibleWidth=-1
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 20;  // Match Video tab font size
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        if (tmpFont != null) labelTMP.font = tmpFont;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 300;
        labelLayout.flexibleWidth = -1;  // Video tab uses -1 (not flexible)

        // Slider - match Video tab: sizeDelta=(400,20), preferredWidth=400
        GameObject sliderObj = new GameObject(settingName + "_Slider");
        sliderObj.transform.SetParent(rowObj.transform, false);

        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(400, 20);

        var sliderLayoutElement = sliderObj.AddComponent<LayoutElement>();
        sliderLayoutElement.preferredWidth = 400;
        sliderLayoutElement.preferredHeight = 20;
        sliderLayoutElement.flexibleWidth = -1;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);

        // Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.5f, 0.7f, 1f);

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);
        var handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0, 0);
        handleAreaRect.anchorMax = new Vector2(1, 1);
        handleAreaRect.offsetMin = new Vector2(10, 0);
        handleAreaRect.offsetMax = new Vector2(-10, 0);

        // Handle
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 0);
        var handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;

        // Add Slider component
        var slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.wholeNumbers = (min >= 0 && max >= 10 && defaultValue == Mathf.Floor(defaultValue) && !isPercent);

        // Value text - match Video tab styling
        GameObject valueObj = new GameObject(settingName + "_Text");
        valueObj.transform.SetParent(rowObj.transform, false);

        var valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.sizeDelta = new Vector2(80, 40);

        var valueTMP = valueObj.AddComponent<TextMeshProUGUI>();
        string format = isPercent ? "{0:P0}" : (slider.wholeNumbers ? "{0:F0}" : "{0:F1}");
        valueTMP.text = string.Format(format, defaultValue);
        valueTMP.fontSize = 20;  // Match Video tab
        valueTMP.alignment = TextAlignmentOptions.MidlineRight;
        valueTMP.color = Color.white;
        if (tmpFont != null) valueTMP.font = tmpFont;

        var valueLayout = valueObj.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 80;
        valueLayout.flexibleWidth = -1;

        Debug.Log($"[OptionsSceneFix] Created slider row with ref: {settingName}");
        return (slider, valueTMP);
    }

    /// <summary>
    /// Create a toggle row and return reference to toggle component
    /// Matches Video tab styling: childControlWidth=false, fixed preferredWidths on children
    /// </summary>
    private Toggle CreateToggleRowWithRef(Transform parent, string settingName, string labelText, bool defaultValue, int siblingIndex)
    {
        TMP_FontAsset tmpFont = FindTMPFont(parent);

        GameObject rowObj = new GameObject(settingName + "_Row");
        Undo.RegisterCreatedObjectUndo(rowObj, "Create Toggle Row");
        rowObj.transform.SetParent(parent, false);
        rowObj.transform.SetSiblingIndex(siblingIndex);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0, 40);

        // Match Video tab HorizontalLayoutGroup: childControlWidth=0 (false), spacing=15
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;  // Video tab uses false - children control their own width
        rowLayout.childControlHeight = true;
        rowLayout.spacing = 15;  // Video tab spacing
        rowLayout.padding = new RectOffset(15, 15, 0, 0);  // Video tab padding

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label - match Video tab: sizeDelta=(300,40), preferredWidth=300, flexibleWidth=-1
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 20;  // Match Video tab font size
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        if (tmpFont != null) labelTMP.font = tmpFont;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 300;
        labelLayout.flexibleWidth = -1;  // Video tab uses -1 (not flexible)

        // Toggle - match Video tab: sizeDelta=(30,30), preferredWidth=30
        GameObject toggleObj = new GameObject(settingName + "_Toggle");
        toggleObj.transform.SetParent(rowObj.transform, false);

        var toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.sizeDelta = new Vector2(30, 30);

        var toggleLayoutElement = toggleObj.AddComponent<LayoutElement>();
        toggleLayoutElement.preferredWidth = 30;
        toggleLayoutElement.preferredHeight = 30;
        toggleLayoutElement.flexibleWidth = -1;

        // Background
        var bgImage = toggleObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(toggleObj.transform, false);
        var checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        var checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.3f, 0.6f, 0.9f, 1f);

        // Add Toggle component
        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = defaultValue;

        Debug.Log($"[OptionsSceneFix] Created toggle row with ref: {settingName}");
        return toggle;
    }

    /// <summary>
    /// Wire a slider and its text to OptionsManager serialized fields
    /// </summary>
    private void WireSliderToManager(SerializedObject managerSO, string sliderFieldName, string textFieldName, (Slider slider, TextMeshProUGUI text) refs)
    {
        if (managerSO == null) return;

        var sliderProp = managerSO.FindProperty(sliderFieldName);
        if (sliderProp != null)
        {
            sliderProp.objectReferenceValue = refs.slider;
            Debug.Log($"[OptionsSceneFix] Wired {sliderFieldName}");
        }
        else
        {
            Debug.LogWarning($"[OptionsSceneFix] Could not find field: {sliderFieldName}");
        }

        var textProp = managerSO.FindProperty(textFieldName);
        if (textProp != null)
        {
            textProp.objectReferenceValue = refs.text;
            Debug.Log($"[OptionsSceneFix] Wired {textFieldName}");
        }
        else
        {
            Debug.LogWarning($"[OptionsSceneFix] Could not find field: {textFieldName}");
        }
    }

    /// <summary>
    /// Wire a toggle to OptionsManager serialized field
    /// </summary>
    private void WireToggleToManager(SerializedObject managerSO, string toggleFieldName, Toggle toggle)
    {
        if (managerSO == null) return;

        var toggleProp = managerSO.FindProperty(toggleFieldName);
        if (toggleProp != null)
        {
            toggleProp.objectReferenceValue = toggle;
            Debug.Log($"[OptionsSceneFix] Wired {toggleFieldName}");
        }
        else
        {
            Debug.LogWarning($"[OptionsSceneFix] Could not find field: {toggleFieldName}");
        }
    }

    /// <summary>
    /// Create a slider row for the Gameplay tab
    /// Matches Video tab styling: childControlWidth=false, fixed preferredWidths on children
    /// </summary>
    private void CreateSliderRow(Transform parent, string settingName, string labelText, float min, float max, float defaultValue, bool isPercent, int siblingIndex)
    {
        TMP_FontAsset tmpFont = FindTMPFont(parent);

        GameObject rowObj = new GameObject(settingName + "_Row");
        Undo.RegisterCreatedObjectUndo(rowObj, "Create Slider Row");
        rowObj.transform.SetParent(parent, false);
        rowObj.transform.SetSiblingIndex(siblingIndex);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0, 40);

        // Match Video tab HorizontalLayoutGroup: childControlWidth=0 (false), spacing=15
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;  // Video tab uses false
        rowLayout.childControlHeight = true;
        rowLayout.spacing = 15;  // Video tab spacing
        rowLayout.padding = new RectOffset(15, 15, 0, 0);  // Video tab padding

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label - match Video tab: sizeDelta=(300,40), preferredWidth=300
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 20;  // Match Video tab
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        if (tmpFont != null) labelTMP.font = tmpFont;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 300;
        labelLayout.flexibleWidth = -1;  // Video tab uses -1

        // Slider - match Video tab: sizeDelta=(400,20), preferredWidth=400
        GameObject sliderObj = new GameObject(settingName + "_Slider");
        sliderObj.transform.SetParent(rowObj.transform, false);

        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(400, 20);

        var sliderLayoutElement = sliderObj.AddComponent<LayoutElement>();
        sliderLayoutElement.preferredWidth = 400;
        sliderLayoutElement.preferredHeight = 20;
        sliderLayoutElement.flexibleWidth = -1;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.25f);
        bgRect.anchorMax = new Vector2(1, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        var fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);

        // Fill
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fillObj.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.5f, 0.7f, 1f);

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);
        var handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0, 0);
        handleAreaRect.anchorMax = new Vector2(1, 1);
        handleAreaRect.offsetMin = new Vector2(10, 0);
        handleAreaRect.offsetMax = new Vector2(-10, 0);

        // Handle
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        var handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 0);
        var handleImage = handleObj.AddComponent<Image>();
        handleImage.color = Color.white;

        // Add Slider component
        var slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.wholeNumbers = (min >= 1 && max >= 10 && defaultValue == Mathf.Floor(defaultValue));

        // Value text - match Video tab styling
        GameObject valueObj = new GameObject(settingName + "_Text");
        valueObj.transform.SetParent(rowObj.transform, false);

        var valueRect = valueObj.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.sizeDelta = new Vector2(80, 40);

        var valueTMP = valueObj.AddComponent<TextMeshProUGUI>();
        string format = isPercent ? "{0:F0}%" : (slider.wholeNumbers ? "{0:F0}" : "{0:F1}");
        valueTMP.text = string.Format(format, defaultValue);
        valueTMP.fontSize = 20;  // Match Video tab
        valueTMP.alignment = TextAlignmentOptions.MidlineRight;
        valueTMP.color = Color.white;
        if (tmpFont != null) valueTMP.font = tmpFont;

        var valueLayout = valueObj.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 80;
        valueLayout.flexibleWidth = -1;

        Debug.Log($"[OptionsSceneFix] Created slider row: {settingName}");
    }

    /// <summary>
    /// Create a toggle row for the Gameplay tab
    /// Matches Video tab styling: childControlWidth=false, fixed preferredWidths on children
    /// </summary>
    private void CreateToggleRow(Transform parent, string settingName, string labelText, bool defaultValue, int siblingIndex)
    {
        TMP_FontAsset tmpFont = FindTMPFont(parent);

        GameObject rowObj = new GameObject(settingName + "_Row");
        Undo.RegisterCreatedObjectUndo(rowObj, "Create Toggle Row");
        rowObj.transform.SetParent(parent, false);
        rowObj.transform.SetSiblingIndex(siblingIndex);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0, 40);

        // Match Video tab HorizontalLayoutGroup: childControlWidth=0 (false), spacing=15
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;  // Video tab uses false
        rowLayout.childControlHeight = true;
        rowLayout.spacing = 15;  // Video tab spacing
        rowLayout.padding = new RectOffset(15, 15, 0, 0);  // Video tab padding

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label - match Video tab: sizeDelta=(300,40), preferredWidth=300
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 20;  // Match Video tab
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        if (tmpFont != null) labelTMP.font = tmpFont;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 300;
        labelLayout.flexibleWidth = -1;  // Video tab uses -1

        // Toggle - match Video tab: sizeDelta=(30,30), preferredWidth=30
        GameObject toggleObj = new GameObject(settingName + "_Toggle");
        toggleObj.transform.SetParent(rowObj.transform, false);

        var toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.sizeDelta = new Vector2(30, 30);

        var toggleLayoutElement = toggleObj.AddComponent<LayoutElement>();
        toggleLayoutElement.preferredWidth = 30;
        toggleLayoutElement.preferredHeight = 30;
        toggleLayoutElement.flexibleWidth = -1;

        // Background
        var bgImage = toggleObj.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Checkmark
        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(toggleObj.transform, false);
        var checkRect = checkObj.AddComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.1f, 0.1f);
        checkRect.anchorMax = new Vector2(0.9f, 0.9f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        var checkImage = checkObj.AddComponent<Image>();
        checkImage.color = new Color(0.3f, 0.6f, 0.9f, 1f);

        // Add Toggle component
        var toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = defaultValue;

        Debug.Log($"[OptionsSceneFix] Created toggle row: {settingName}");
    }

    /// <summary>
    /// Find a TMP font from existing UI elements
    /// </summary>
    private TMP_FontAsset FindTMPFont(Transform parent)
    {
        var existingTMPs = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var existingTMP in existingTMPs)
        {
            if (existingTMP.font != null)
            {
                return existingTMP.font;
            }
        }
        return null;
    }

    /// <summary>
    /// Create a collapsible section with header and content panel matching Video tab styling.
    /// Returns the content panel Transform so binding rows can be added to it.
    /// </summary>
    private Transform CreateCollapsibleSectionWithContent(Transform parent, string headerText, int siblingIndex)
    {
        // Find a reference TMP font from existing UI elements
        TMP_FontAsset tmpFont = null;
        var existingTMPs = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var existingTMP in existingTMPs)
        {
            if (existingTMP.font != null)
            {
                tmpFont = existingTMP.font;
                break;
            }
        }

        string safeName = headerText.Replace(" ", "_");
        GameObject sectionObj = new GameObject(safeName + "_Section");
        Undo.RegisterCreatedObjectUndo(sectionObj, "Create Section");
        sectionObj.transform.SetParent(parent, false);
        sectionObj.transform.SetSiblingIndex(siblingIndex);

        var sectionRect = sectionObj.AddComponent<RectTransform>();
        sectionRect.anchorMin = new Vector2(0, 1);
        sectionRect.anchorMax = new Vector2(1, 1);
        sectionRect.pivot = new Vector2(0.5f, 1);

        var sectionVlg = sectionObj.AddComponent<VerticalLayoutGroup>();
        sectionVlg.childForceExpandWidth = true;
        sectionVlg.childForceExpandHeight = false;
        sectionVlg.childControlWidth = true;
        sectionVlg.childControlHeight = true;
        sectionVlg.spacing = 0;

        var sectionLayout = sectionObj.AddComponent<LayoutElement>();
        sectionLayout.flexibleWidth = 1;

        // Add CollapsibleSection component
        var collapsible = sectionObj.AddComponent<CollapsibleSection>();

        // Create Header (Button)
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(sectionObj.transform, false);

        var headerRect = headerObj.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);

        var headerImage = headerObj.AddComponent<Image>();
        headerImage.color = headerColor;

        var headerButton = headerObj.AddComponent<Button>();
        headerButton.targetGraphic = headerImage;
        headerButton.transition = Selectable.Transition.ColorTint;

        var headerLayout = headerObj.AddComponent<LayoutElement>();
        headerLayout.minHeight = 50;
        headerLayout.preferredHeight = 50;

        var headerHlg = headerObj.AddComponent<HorizontalLayoutGroup>();
        headerHlg.padding = new RectOffset(15, 15, 0, 0);
        headerHlg.spacing = 10;
        headerHlg.childAlignment = TextAnchor.MiddleLeft;
        headerHlg.childForceExpandWidth = false;
        headerHlg.childForceExpandHeight = true;
        headerHlg.childControlWidth = false;
        headerHlg.childControlHeight = true;

        // Create Arrow
        GameObject arrowObj = new GameObject("Arrow");
        arrowObj.transform.SetParent(headerObj.transform, false);

        var arrowRect = arrowObj.AddComponent<RectTransform>();
        arrowRect.sizeDelta = arrowSize;

        var arrowLayout = arrowObj.AddComponent<LayoutElement>();
        arrowLayout.preferredWidth = arrowSize.x;
        arrowLayout.preferredHeight = arrowSize.y;

        var arrowTMP = arrowObj.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = arrowExpandedChar;
        arrowTMP.fontSize = arrowFontSize;
        arrowTMP.alignment = TextAlignmentOptions.Bottom;
        arrowTMP.color = Color.white;
        if (tmpFont != null) arrowTMP.font = tmpFont;

        // Create Header Text
        GameObject textObj = new GameObject("HeaderText");
        textObj.transform.SetParent(headerObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();

        var textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;

        var headerTMP = textObj.AddComponent<TextMeshProUGUI>();
        headerTMP.text = headerText;
        headerTMP.fontSize = headerFontSize;
        headerTMP.fontStyle = headerFontStyle;
        headerTMP.alignment = headerAlignment;
        headerTMP.color = Color.white;
        if (tmpFont != null) headerTMP.font = tmpFont;

        // Create ContentPanel
        GameObject contentObj = new GameObject("ContentPanel");
        contentObj.transform.SetParent(sectionObj.transform, false);

        var contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);

        var contentVlg = contentObj.AddComponent<VerticalLayoutGroup>();
        contentVlg.padding = new RectOffset(contentPanelPaddingLeft, contentPanelPaddingRight, contentPanelPaddingTop, contentPanelPaddingBottom);
        contentVlg.spacing = contentPanelSpacing;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = false;

        // Wire up CollapsibleSection references using SerializedObject
        var so = new SerializedObject(collapsible);
        var headerButtonProp = so.FindProperty("headerButton");
        var contentPanelProp = so.FindProperty("contentPanel");
        var headerTextProp = so.FindProperty("headerText");
        var arrowTextProp = so.FindProperty("arrowText");
        var startExpandedProp = so.FindProperty("startExpanded");

        if (headerButtonProp != null) headerButtonProp.objectReferenceValue = headerButton;
        if (contentPanelProp != null) contentPanelProp.objectReferenceValue = contentObj;
        if (headerTextProp != null) headerTextProp.objectReferenceValue = headerTMP;
        if (arrowTextProp != null) arrowTextProp.objectReferenceValue = arrowTMP;
        if (startExpandedProp != null) startExpandedProp.boolValue = true;
        so.ApplyModifiedProperties();

        Debug.Log($"[OptionsSceneFix] Created collapsible section: {headerText}");

        // Return the content panel so binding rows can be added to it
        return contentObj.transform;
    }

    /// <summary>
    /// Create a binding row matching Video tab styling
    /// </summary>
    private void CreateBindingRow(Transform parent, string bindingName, string labelText, int siblingIndex)
    {
        TMP_FontAsset tmpFont = FindTMPFont(parent);

        // Row container - match Video tab styling
        GameObject rowObj = new GameObject(bindingName + "_Row");
        Undo.RegisterCreatedObjectUndo(rowObj, "Create Binding Row");
        rowObj.transform.SetParent(parent, false);
        rowObj.transform.SetSiblingIndex(siblingIndex);

        var rowRect = rowObj.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(0, 40);

        // Match Video tab HorizontalLayoutGroup: childControlWidth=false, spacing=15
        var rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;  // Video tab uses false
        rowLayout.childControlHeight = true;
        rowLayout.spacing = 15;  // Video tab spacing
        rowLayout.padding = new RectOffset(15, 15, 0, 0);  // Video tab padding

        var rowLayoutElement = rowObj.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 40;
        rowLayoutElement.preferredHeight = 40;

        // Label - match Video tab: sizeDelta=(300,40), preferredWidth=300
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(rowObj.transform, false);

        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(300, 40);

        var labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
        labelTMP.text = labelText;
        labelTMP.fontSize = 20;  // Match Video tab
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.color = Color.white;
        if (tmpFont != null) labelTMP.font = tmpFont;

        var labelLayout = labelObj.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 300;
        labelLayout.flexibleWidth = -1;

        // Binding button - match Video tab control width (like slider at 400)
        GameObject buttonObj = new GameObject(bindingName + "_Button");
        buttonObj.transform.SetParent(rowObj.transform, false);

        var buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(200, 40);  // Wider to match other controls

        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);  // Match Video tab background

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        var buttonLayout = buttonObj.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 200;
        buttonLayout.preferredHeight = 40;
        buttonLayout.flexibleWidth = -1;

        // Add KeyBindingCapture component
        var capture = buttonObj.AddComponent<KeyBindingCapture>();

        // Binding text inside button
        GameObject textObj = new GameObject("BindingText");
        textObj.transform.SetParent(buttonObj.transform, false);

        var textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var bindingTMP = textObj.AddComponent<TextMeshProUGUI>();
        bindingTMP.text = "[Not Set]";
        bindingTMP.fontSize = 20;  // Match Video tab
        bindingTMP.alignment = TextAlignmentOptions.Center;
        bindingTMP.color = Color.white;
        if (tmpFont != null) bindingTMP.font = tmpFont;

        // Assign bindingText to capture using SerializedObject
        var so = new SerializedObject(capture);
        var bindingTextProp = so.FindProperty("bindingText");
        if (bindingTextProp != null)
        {
            bindingTextProp.objectReferenceValue = bindingTMP;
            so.ApplyModifiedProperties();
        }

        Debug.Log($"[OptionsSceneFix] Created binding row: {bindingName} with label '{labelText}' at index {siblingIndex}");
    }
}
#endif

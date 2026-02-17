using UnityEngine;
using TMPro;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Spawns floating state indicator symbols above visitors.
    /// ? for confused (yellow, rotating), ! for frightened (red, static),
    /// * for lured (green, rotating).
    /// Follows the same static helper pattern as DazeStreamerEffect.
    /// </summary>
    public class VisitorStateIndicator : MonoBehaviour
    {
        private const string INDICATOR_NAME = "StateIndicator";
        private const string LABEL_NAME = "VisitorLabel";
        private const float Z_OFFSET = -0.5f; // Above visitor (toward camera, -Z is up)
        private const float LABEL_Z_OFFSET = -0.6f; // Name label just above state indicator
        private const float FONT_SIZE = 4f;
        private const float LABEL_FONT_SIZE = 3f;
        private const float ROTATE_SPEED = 180f; // Degrees per second for ? rotation

        private bool shouldRotate;

        private void Update()
        {
            // Billboard: face camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }

            // Rotate ? symbol around local forward (which faces camera after billboard)
            if (shouldRotate)
            {
                transform.Rotate(0f, 0f, ROTATE_SPEED * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>
        /// Shows a rotating yellow ? above the visitor for confused state.
        /// </summary>
        public static void ShowConfused(Transform visitor)
        {
            ClearIndicator(visitor);
            CreateIndicator(visitor, "?", new Color(1f, 0.85f, 0.2f), true);
        }

        /// <summary>
        /// Shows a red ! above the visitor for frightened state.
        /// </summary>
        public static void ShowFrightened(Transform visitor)
        {
            ClearIndicator(visitor);
            CreateIndicator(visitor, "!", new Color(1f, 0.3f, 0.2f), false);
        }

        /// <summary>
        /// Shows a rotating green * above the visitor for lured state (fog).
        /// </summary>
        public static void ShowLured(Transform visitor)
        {
            ClearIndicator(visitor);
            CreateIndicator(visitor, "*", new Color(0.2f, 0.9f, 0.4f), true);
        }

        /// <summary>
        /// Removes any state indicator from the visitor.
        /// </summary>
        public static void ClearIndicator(Transform visitor)
        {
            if (visitor == null) return;

            Transform existing = visitor.Find(INDICATOR_NAME);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        /// <summary>
        /// Shows or updates a persistent name label above the visitor for debugging.
        /// Extracts a short ID from the full GameObject name.
        /// </summary>
        public static void ShowLabel(Transform visitor)
        {
            if (visitor == null) return;

            // Extract short ID: "MistakingVisitor_T1_3_C" -> "3_C"
            string fullName = visitor.gameObject.name;
            string shortId = fullName;
            string[] parts = fullName.Split('_');
            if (parts.Length >= 2)
            {
                shortId = parts[parts.Length - 2] + "_" + parts[parts.Length - 1];
            }

            ShowLabel(visitor, shortId, Color.white);
        }

        /// <summary>
        /// Shows or updates a persistent label with explicit text above the target transform.
        /// </summary>
        public static void ShowLabel(Transform target, string labelText, Color color)
        {
            if (target == null) return;

            // Destroy existing label if present (allows updating text)
            Transform existing = target.Find(LABEL_NAME);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject labelObj = new GameObject(LABEL_NAME);
            labelObj.transform.SetParent(target, false);
            labelObj.transform.localPosition = new Vector3(0f, 0f, LABEL_Z_OFFSET);

            TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
            tmp.text = labelText;
            tmp.fontSize = LABEL_FONT_SIZE;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(3f, 1f);

            // Add billboard behavior (reuses VisitorStateIndicator but without rotation)
            var indicator = labelObj.AddComponent<VisitorStateIndicator>();
            indicator.shouldRotate = false;
        }

        /// <summary>
        /// Shows a light green label above a prop for debugging.
        /// </summary>
        public static void ShowPropLabel(Transform prop, string labelText)
        {
            ShowLabel(prop, labelText, new Color(0.6f, 1f, 0.6f));
        }

        private static void CreateIndicator(Transform visitor, string symbol, Color color, bool rotate)
        {
            GameObject indicatorObj = new GameObject(INDICATOR_NAME);
            indicatorObj.transform.SetParent(visitor, false);
            indicatorObj.transform.localPosition = new Vector3(0f, 0f, Z_OFFSET);

            TextMeshPro tmp = indicatorObj.AddComponent<TextMeshPro>();
            tmp.text = symbol;
            tmp.fontSize = FONT_SIZE;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            // Size the rect to fit the single character
            RectTransform rect = indicatorObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(2f, 2f);

            var indicator = indicatorObj.AddComponent<VisitorStateIndicator>();
            indicator.shouldRotate = rotate;
        }
    }
}

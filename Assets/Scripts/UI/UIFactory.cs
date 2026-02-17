using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace FaeMaze.UI
{
    /// <summary>
    /// Shared UI creation utilities to eliminate duplicated canvas and panel setup code.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>
        /// Finds an existing canvas on or above the component, then searches for one with the
        /// matching name, or creates a new ScreenSpaceOverlay canvas if none is found.
        /// Each UI system gets its own named canvas with the correct sorting order.
        /// </summary>
        public static Canvas FindOrCreateCanvas(MonoBehaviour context, string canvasName, int sortingOrder, Vector2? referenceResolution = null)
        {
            // First check parent hierarchy
            Canvas canvas = context.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas;

            // Search for a canvas with the matching name
            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in allCanvases)
            {
                if (c.gameObject.name == canvasName)
                {
                    return c;
                }
            }

            // No matching canvas found — create a new one with the correct sorting order
            return CreateOverlayCanvas(canvasName, sortingOrder, referenceResolution);
        }

        /// <summary>
        /// Creates a new ScreenSpaceOverlay canvas with CanvasScaler and GraphicRaycaster.
        /// </summary>
        public static Canvas CreateOverlayCanvas(string canvasName, int sortingOrder, Vector2? referenceResolution = null)
        {
            GameObject canvasObj = new GameObject(canvasName);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            if (referenceResolution.HasValue)
            {
                scaler.referenceResolution = referenceResolution.Value;
            }
            canvasObj.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists (required for UI button clicks)
            EnsureEventSystem();

            return canvas;
        }

        /// <summary>
        /// Ensures an EventSystem exists in the scene. UI buttons won't respond to clicks without one.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<InputSystemUIInputModule>();
            }
        }

        /// <summary>
        /// Sets up a RectTransform to fill its parent completely (anchors at corners, zero offsets).
        /// </summary>
        public static void StretchToFillParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Creates a full-screen panel with a background Image under the given parent.
        /// Returns the panel GameObject with RectTransform already stretched to fill parent.
        /// </summary>
        public static GameObject CreateFullScreenPanel(Transform parent, string panelName, Color backgroundColor)
        {
            GameObject panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            StretchToFillParent(panelRect);

            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = backgroundColor;

            return panel;
        }
    }
}

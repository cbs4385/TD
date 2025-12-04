using UnityEngine;
using UnityEngine.UI;

namespace FaeMaze.UI
{
    public class ButtonTextInitializer : MonoBehaviour
    {
        private void Start()
        {
            UnityEngine.Debug.Log("[ButtonTextInitializer] Starting button text initialization...");

            // Find all buttons and ensure they have text
            Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

            UnityEngine.Debug.Log($"[ButtonTextInitializer] Found {buttons.Length} buttons");

            foreach (Button button in buttons)
            {
                UnityEngine.Debug.Log($"[ButtonTextInitializer] Processing button: {button.gameObject.name}");

                if (button.gameObject.name == "StartGameButton")
                {
                    EnsureButtonHasText(button, "Start Game");
                }
                else if (button.gameObject.name == "OptionsButton")
                {
                    EnsureButtonHasText(button, "Options");
                }
                else if (button.gameObject.name == "ExitButton")
                {
                    EnsureButtonHasText(button, "Exit");
                }
            }
        }

        private void EnsureButtonHasText(Button button, string textContent)
        {
            UnityEngine.Debug.Log($"[ButtonTextInitializer] Ensuring text for {button.gameObject.name}: '{textContent}'");

            Text textComponent = button.GetComponentInChildren<Text>(true);

            if (textComponent == null)
            {
                UnityEngine.Debug.Log($"[ButtonTextInitializer] No Text component found, creating new one for {button.gameObject.name}");

                // Create text GameObject if it doesn't exist
                GameObject textObj = new GameObject("Text");
                textObj.layer = 5; // UI layer
                textObj.transform.SetParent(button.transform, false);

                RectTransform rectTransform = textObj.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;

                textComponent = textObj.AddComponent<Text>();
                textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textComponent.fontSize = 24;
                textComponent.fontStyle = FontStyle.Bold;
                textComponent.alignment = TextAnchor.MiddleCenter;
                textComponent.color = new Color(0.196f, 0.196f, 0.196f);
                textComponent.raycastTarget = false;

                UnityEngine.Debug.Log($"[ButtonTextInitializer] Created Text component for {button.gameObject.name}");
            }
            else
            {
                UnityEngine.Debug.Log($"[ButtonTextInitializer] Text component already exists for {button.gameObject.name}, updating text");
            }

            textComponent.text = textContent;
            UnityEngine.Debug.Log($"[ButtonTextInitializer] Set text to '{textContent}' for {button.gameObject.name}");
        }
    }
}

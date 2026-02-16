using UnityEngine;
using UnityEngine.UI;

namespace FaeMaze.UI
{
    public class ButtonTextInitializer : MonoBehaviour
    {
        private void Start()
        {
            // Find all buttons and ensure they have text
            Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

            foreach (Button button in buttons)
            {
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
            Text textComponent = button.GetComponentInChildren<Text>(true);

            if (textComponent == null)
            {
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
                textComponent.raycastTarget = false;
            }

            textComponent.text = textContent;
            textComponent.color = Color.white;
            textComponent.fontSize = 28;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleCenter;

            // Add thick dark outline for readability on any background
            Outline outline = textComponent.GetComponent<Outline>();
            if (outline == null)
            {
                outline = textComponent.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);

            // Add shadow for extra contrast
            Shadow shadow = textComponent.GetComponent<Shadow>();
            if (shadow == null || shadow is Outline)
            {
                shadow = textComponent.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(3f, -3f);
        }
    }
}

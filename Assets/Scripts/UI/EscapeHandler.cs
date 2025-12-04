using UnityEngine;
using UnityEngine.InputSystem;

namespace FaeMaze.UI
{
    public class EscapeHandler : MonoBehaviour
    {
        private SceneLoader sceneLoader;

        private void Awake()
        {
            sceneLoader = GetComponent<SceneLoader>();
            if (sceneLoader == null)
            {
                sceneLoader = gameObject.AddComponent<SceneLoader>();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                sceneLoader.LoadMainMenu();
            }
        }
    }
}

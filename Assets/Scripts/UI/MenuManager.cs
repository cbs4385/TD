using UnityEngine;
using UnityEngine.UI;

namespace FaeMaze.UI
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button exitButton;

        private SceneLoader sceneLoader;

        private void Awake()
        {
            sceneLoader = GetComponent<SceneLoader>();
            if (sceneLoader == null)
            {
                sceneLoader = gameObject.AddComponent<SceneLoader>();
            }
        }

        private void Start()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OnOptionsClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitClicked);
            }
        }

        private void OnStartGameClicked()
        {
            sceneLoader.LoadGameScene();
        }

        private void OnOptionsClicked()
        {
            sceneLoader.LoadOptionsScene();
        }

        private void OnExitClicked()
        {
            sceneLoader.QuitGame();
        }

        private void OnDestroy()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.RemoveListener(OnStartGameClicked);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OnOptionsClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
            }
        }
    }
}

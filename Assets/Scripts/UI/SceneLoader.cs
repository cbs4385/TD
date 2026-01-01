using UnityEngine;
using UnityEngine.SceneManagement;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    public class SceneLoader : MonoBehaviour
    {
        public void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        public void LoadMainMenu()
        {
            SceneManager.LoadScene("MainMenu");
        }

        public void LoadGameScene()
        {
            // Reset all persistent game state before starting a new game
            GameController.ResetPersistentGameState();
            WaveManager.ResetPersistentWaveState();

            SceneManager.LoadScene("FaeMazeScene");
        }

        public void LoadOptionsScene()
        {
            SceneManager.LoadScene("Options");
        }

        public void LoadGameOverScene()
        {
            SceneManager.LoadScene("GameOver");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

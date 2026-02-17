using UnityEngine;
using UnityEngine.SceneManagement;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    public class SceneLoader : MonoBehaviour
    {
        public void LoadScene(string sceneName)
        {
            GameEventLogger.LogScene("LoadScene", sceneName);
            SceneManager.LoadScene(sceneName);
        }

        public void LoadMainMenu()
        {
            GameEventLogger.LogScene("LoadMainMenu");
            SceneManager.LoadScene("MainMenu");
        }

        public void LoadGameScene()
        {
            GameEventLogger.LogScene("LoadGameScene", "Resetting persistent state, loading PlanarForestMazeScene");
            // Reset all persistent game state before starting a new game
            GameController.ResetPersistentGameState();
            WaveManager.ResetPersistentWaveState();

            SceneManager.LoadScene("PlanarForestMazeScene");
        }

        public void LoadOptionsScene()
        {
            GameEventLogger.LogScene("LoadOptionsScene");
            SceneManager.LoadScene("Options");
        }

        public void LoadGameOverScene()
        {
            GameEventLogger.LogScene("LoadGameOverScene");
            SceneManager.LoadScene("GameOver");
        }

        public void QuitGame()
        {
            GameEventLogger.LogScene("QuitGame");
            Application.Quit();
        }
    }
}

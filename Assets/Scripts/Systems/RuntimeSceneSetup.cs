using UnityEngine;
using UnityEngine.SceneManagement;
using FaeMaze.UI;

namespace FaeMaze.Systems
{
    [DefaultExecutionOrder(-100)]
    public class RuntimeSceneSetup : MonoBehaviour
    {
        private static RuntimeSceneSetup instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string sceneName = scene.name;

            if (sceneName == "FaeMazeScene" || sceneName == "Options")
            {
                GameObject escapeHandlerObj = GameObject.Find("EscapeHandler");
                if (escapeHandlerObj == null)
                {
                    escapeHandlerObj = new GameObject("EscapeHandler");
                    escapeHandlerObj.AddComponent<EscapeHandler>();
                }
            }

            // Auto-create WaveManager in FaeMazeScene if it doesn't exist
            if (sceneName == "FaeMazeScene")
            {
                WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
                if (waveManager == null)
                {
                    // Find GameRoot or create a Systems container
                    GameObject gameRoot = GameObject.Find("GameRoot");
                    if (gameRoot == null)
                    {
                        gameRoot = GameObject.Find("Systems");
                        if (gameRoot == null)
                        {
                            gameRoot = new GameObject("Systems");
                        }
                    }

                    // Create WaveManager GameObject
                    GameObject waveManagerObj = new GameObject("WaveManager");
                    waveManagerObj.transform.SetParent(gameRoot.transform);
                    waveManagerObj.AddComponent<WaveManager>();
                }
            }
        }
    }
}

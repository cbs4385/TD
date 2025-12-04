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

            if (sceneName == "FaeMazeScene" || sceneName == "ProceduralMazeScene" || sceneName == "Options")
            {
                GameObject escapeHandlerObj = GameObject.Find("EscapeHandler");
                if (escapeHandlerObj == null)
                {
                    escapeHandlerObj = new GameObject("EscapeHandler");
                    escapeHandlerObj.AddComponent<EscapeHandler>();
                }
            }

            // Handle ProceduralMazeScene setup
            if (sceneName == "ProceduralMazeScene")
            {
                SetupProceduralMazeScene();
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

        private static void SetupProceduralMazeScene()
        {
            // Find all MazeGridBehaviour components in the scene
            MazeGridBehaviour[] allMazeGrids = Object.FindObjectsByType<MazeGridBehaviour>(FindObjectsSortMode.None);

            MazeGridBehaviour runtimeGenMaze = null;

            // Find the one using runtime generation and disable others
            foreach (var mazeGrid in allMazeGrids)
            {
                // Check if this one uses runtime generation (via reflection to access private field)
                var field = typeof(MazeGridBehaviour).GetField("useRuntimeGeneration",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    bool usesRuntimeGen = (bool)field.GetValue(mazeGrid);

                    if (usesRuntimeGen)
                    {
                        // This is the runtime generation maze - keep it enabled
                        runtimeGenMaze = mazeGrid;
                        mazeGrid.enabled = true;
                    }
                    else
                    {
                        // Disable file-based maze grids in ProceduralMazeScene
                        mazeGrid.enabled = false;
                    }
                }
            }
        }
    }
}

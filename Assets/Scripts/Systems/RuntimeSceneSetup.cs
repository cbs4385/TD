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

            // Auto-create WaveManager and HeartPowerManager in both FaeMazeScene and ProceduralMazeScene if they don't exist
            if (sceneName == "FaeMazeScene" || sceneName == "ProceduralMazeScene")
            {
                // Find or create Systems container
                GameObject gameRoot = GameObject.Find("GameRoot");
                if (gameRoot == null)
                {
                    gameRoot = GameObject.Find("Systems");
                    if (gameRoot == null)
                    {
                        gameRoot = new GameObject("Systems");
                    }
                }

                // Auto-create WaveManager if missing
                WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
                if (waveManager == null)
                {
                    GameObject waveManagerObj = new GameObject("WaveManager");
                    waveManagerObj.transform.SetParent(gameRoot.transform);
                    waveManagerObj.AddComponent<WaveManager>();
                }

                // Auto-create HeartPowerManager if missing
                FaeMaze.HeartPowers.HeartPowerManager heartPowerManager = Object.FindFirstObjectByType<FaeMaze.HeartPowers.HeartPowerManager>();
                if (heartPowerManager == null)
                {
                    GameObject heartPowerManagerObj = new GameObject("HeartPowerManager");
                    heartPowerManagerObj.transform.SetParent(gameRoot.transform);
                    heartPowerManagerObj.AddComponent<FaeMaze.HeartPowers.HeartPowerManager>();
                    Debug.Log($"[RuntimeSceneSetup] Auto-created HeartPowerManager for {sceneName}");
                }

                // Auto-start first wave in ProceduralMazeScene
                if (sceneName == "ProceduralMazeScene")
                {
                    WaveSpawner waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
                    if (waveSpawner != null)
                    {
                        var spawnerType = typeof(WaveSpawner);

                        // Auto-assign visitor prefab if missing
                        var visitorPrefabField = spawnerType.GetField("visitorPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (visitorPrefabField != null)
                        {
                            var currentPrefab = visitorPrefabField.GetValue(waveSpawner);
                            if (currentPrefab == null)
                            {
                                // Try to load visitor prefab from Resources folder
                                var prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Visitors/Visitor_FestivalTourist");

                                if (prefab != null)
                                {
                                    visitorPrefabField.SetValue(waveSpawner, prefab);
                                }
                                else
                                {
                                }
                            }
                        }

                        // Auto-assign mistaking visitor prefab if missing
                        var mistakingVisitorPrefabField = spawnerType.GetField("mistakingVisitorPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (mistakingVisitorPrefabField != null)
                        {
                            var currentMistakingPrefab = mistakingVisitorPrefabField.GetValue(waveSpawner);
                            if (currentMistakingPrefab == null)
                            {
                                // Try to load mistaking visitor prefab from Resources folder
                                var mistakingPrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Visitors/MistakingVisitor_FestivalTourist");

                                if (mistakingPrefab != null)
                                {
                                    mistakingVisitorPrefabField.SetValue(waveSpawner, mistakingPrefab);
                                }
                                else
                                {
                                }
                            }
                        }

                        // Use delayed invoke to ensure all initialization is complete
                        var delayedStarter = new GameObject("WaveStarterDelay");
                        var starter = delayedStarter.AddComponent<DelayedWaveStarter>();
                        starter.StartFirstWave(waveSpawner, 0.5f);
                    }
                }
            }
        }

        private static void SetupProceduralMazeScene()
        {
            // Find all MazeGridBehaviour components in the scene
            MazeGridBehaviour[] allMazeGrids = Object.FindObjectsByType<MazeGridBehaviour>(FindObjectsSortMode.None);

            MazeGridBehaviour runtimeGenMaze = null;
            MazeGridBehaviour fileBasedMaze = null;

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
                        // This is the file-based maze - disable it but keep reference
                        fileBasedMaze = mazeGrid;
                        mazeGrid.enabled = false;
                    }
                }
            }

            if (runtimeGenMaze == null)
            {
                return;
            }

            // Update all component references to use the runtime maze
            UpdateMazeReferences(fileBasedMaze, runtimeGenMaze);

            // Re-position entrance and heart after maze is properly set up
            var entrance = Object.FindFirstObjectByType<FaeMaze.Maze.MazeEntrance>();
            var heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();

            if (entrance != null)
            {
                entrance.SetGridPosition(runtimeGenMaze.EntranceGridPos);
                Vector3 entranceWorld = runtimeGenMaze.GridToWorld(runtimeGenMaze.EntranceGridPos.x, runtimeGenMaze.EntranceGridPos.y);
                entrance.transform.position = entranceWorld;
            }

            if (heart != null)
            {
                heart.SetGridPosition(runtimeGenMaze.HeartGridPos);
                Vector3 heartWorld = runtimeGenMaze.GridToWorld(runtimeGenMaze.HeartGridPos.x, runtimeGenMaze.HeartGridPos.y);
                heart.transform.position = heartWorld;
            }
        }

        private static void UpdateMazeReferences(MazeGridBehaviour oldMaze, MazeGridBehaviour newMaze)
        {
            // Update GameController reference
            if (GameController.Instance != null)
            {
                var gcType = typeof(GameController);
                var mazeGridField = gcType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null && ReferenceEquals(mazeGridField.GetValue(GameController.Instance), oldMaze))
                {
                    // Re-register with the correct maze
                    GameController.Instance.RegisterMazeGrid(newMaze.Grid);
                }
            }

            // Update CameraController reference
            var cameraController = Object.FindFirstObjectByType<FaeMaze.Cameras.CameraController2D>();
            if (cameraController != null)
            {
                var ccType = typeof(FaeMaze.Cameras.CameraController2D);
                var mazeGridField = ccType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null)
                {
                    mazeGridField.SetValue(cameraController, newMaze);
                }
            }

            // Update PropPlacementController reference
            var propController = Object.FindFirstObjectByType<FaeMaze.Props.PropPlacementController>();
            if (propController != null)
            {
                var pcType = typeof(FaeMaze.Props.PropPlacementController);
                var mazeGridField = pcType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null)
                {
                    mazeGridField.SetValue(propController, newMaze);
                }
            }

            // Update HeartPowerManager reference
            var heartPowerManager = Object.FindFirstObjectByType<FaeMaze.HeartPowers.HeartPowerManager>();
            if (heartPowerManager != null)
            {
                var hpmType = typeof(FaeMaze.HeartPowers.HeartPowerManager);
                var mazeGridField = hpmType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null)
                {
                    mazeGridField.SetValue(heartPowerManager, newMaze);
                }
            }
        }
    }

    /// <summary>
    /// Helper component to start the first wave after a delay.
    /// Ensures all scene initialization is complete before starting wave spawning.
    /// </summary>
    internal class DelayedWaveStarter : MonoBehaviour
    {
        public void StartFirstWave(WaveSpawner waveSpawner, float delay)
        {
            StartCoroutine(StartAfterDelay(waveSpawner, delay));
        }

        private System.Collections.IEnumerator StartAfterDelay(WaveSpawner waveSpawner, float delay)
        {
            // Wait for end of frame to ensure all Start() methods have run
            yield return new WaitForEndOfFrame();

            // Additional delay to ensure all initialization is complete
            yield return new WaitForSeconds(delay);

            if (waveSpawner != null)
            {
                // Verify WaveSpawner is ready
                bool started = waveSpawner.StartWave();

                if (!started)
                {
                    yield return new WaitForSeconds(0.5f);

                    started = waveSpawner.StartWave();
                }

                if (!started)
                {
                }
            }

            // Clean up this helper object
            Destroy(gameObject);
        }
    }
}

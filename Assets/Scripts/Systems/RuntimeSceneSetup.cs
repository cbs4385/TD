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

            if (sceneName == "ProceduralMazeScene")
            {
                SetupProceduralMazeScene();
            }

            if (sceneName == "FaeMazeScene" || sceneName == "ProceduralMazeScene")
            {
                GameObject gameRoot = GameObject.Find("GameRoot");
                if (gameRoot == null)
                {
                    gameRoot = GameObject.Find("Systems");
                    if (gameRoot == null)
                    {
                        gameRoot = new GameObject("Systems");
                    }
                }

                WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
                if (waveManager == null)
                {
                    GameObject waveManagerObj = new GameObject("WaveManager");
                    waveManagerObj.transform.SetParent(gameRoot.transform);
                    waveManagerObj.AddComponent<WaveManager>();
                }

                FaeMaze.HeartPowers.HeartPowerManager heartPowerManager = Object.FindFirstObjectByType<FaeMaze.HeartPowers.HeartPowerManager>();
                if (heartPowerManager == null)
                {
                    GameObject heartPowerManagerObj = new GameObject("HeartPowerManager");
                    heartPowerManagerObj.transform.SetParent(gameRoot.transform);
                    heartPowerManagerObj.AddComponent<FaeMaze.HeartPowers.HeartPowerManager>();
                }

                GameObject heartModelPrefab = null;

#if UNITY_EDITOR
                heartModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/heartofmaze.prefab");
#else
                heartModelPrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/heartofmaze");
#endif

                FaeMaze.Maze.HeartOfTheMaze heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();

                if (heart == null)
                {
                    GameObject heartObj = new GameObject("HeartOfTheMaze");
                    heartObj.transform.SetParent(gameRoot.transform);
                    heartObj.SetActive(false);
                    heart = heartObj.AddComponent<FaeMaze.Maze.HeartOfTheMaze>();

                    if (heartModelPrefab != null)
                    {
                        var heartType = typeof(FaeMaze.Maze.HeartOfTheMaze);
                        var heartModelPrefabField = heartType.GetField("heartModelPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (heartModelPrefabField != null)
                        {
                            heartModelPrefabField.SetValue(heart, heartModelPrefab);
                        }
                    }

                    heartObj.SetActive(true);
                }
                else
                {
                    if (heartModelPrefab != null)
                    {
                        var heartType = typeof(FaeMaze.Maze.HeartOfTheMaze);
                        var heartModelPrefabField = heartType.GetField("heartModelPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (heartModelPrefabField != null)
                        {
                            heartModelPrefabField.SetValue(heart, heartModelPrefab);
                        }

                        var setupModelMethod = heartType.GetMethod("SetupModel",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (setupModelMethod != null)
                        {
                            setupModelMethod.Invoke(heart, null);
                        }
                    }
                }

                if (sceneName == "ProceduralMazeScene")
                {
                    WaveSpawner waveSpawner = Object.FindFirstObjectByType<WaveSpawner>();
                    if (waveSpawner != null)
                    {
                        var spawnerType = typeof(WaveSpawner);

                        var visitorPrefabField = spawnerType.GetField("visitorPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (visitorPrefabField != null)
                        {
                            var currentPrefab = visitorPrefabField.GetValue(waveSpawner);
                            if (currentPrefab == null)
                            {
                                var prefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Visitors/Visitor_FestivalTourist");
                                if (prefab != null)
                                {
                                    visitorPrefabField.SetValue(waveSpawner, prefab);
                                }
                            }
                        }

                        var mistakingVisitorPrefabField = spawnerType.GetField("mistakingVisitorPrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (mistakingVisitorPrefabField != null)
                        {
                            var currentMistakingPrefab = mistakingVisitorPrefabField.GetValue(waveSpawner);
                            if (currentMistakingPrefab == null)
                            {
                                var mistakingPrefab = UnityEngine.Resources.Load<GameObject>("Prefabs/Visitors/MistakingVisitor_FestivalTourist");
                                if (mistakingPrefab != null)
                                {
                                    mistakingVisitorPrefabField.SetValue(waveSpawner, mistakingPrefab);
                                }
                            }
                        }

                        var delayedStarter = new GameObject("WaveStarterDelay");
                        var starter = delayedStarter.AddComponent<DelayedWaveStarter>();
                        starter.StartFirstWave(waveSpawner, 0.5f);
                    }
                }
            }
        }

        private static void SetupProceduralMazeScene()
        {
            // Find the MazeGridBehaviour in the scene
            MazeGridBehaviour mazeGrid = Object.FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGrid == null)
            {
                return;
            }

            // Position heart at world-space heart position
            var heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();
            if (heart != null && mazeGrid.ForestMapState != null)
            {
                heart.transform.position = mazeGrid.HeartWorldPosition;
            }

            // Update camera and other references
            UpdateMazeReferences(mazeGrid);
        }

        private static void UpdateMazeReferences(MazeGridBehaviour newMaze)
        {
            // Update CameraController reference (check both 2D and 3D variants)
            var cameraController2D = Object.FindFirstObjectByType<FaeMaze.Cameras.CameraController2D>();
            if (cameraController2D != null)
            {
                var ccType = typeof(FaeMaze.Cameras.CameraController2D);
                var mazeGridField = ccType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null)
                {
                    mazeGridField.SetValue(cameraController2D, newMaze);
                }
            }

            var cameraController3D = Object.FindFirstObjectByType<FaeMaze.Cameras.CameraController3D>();
            if (cameraController3D != null)
            {
                var ccType = typeof(FaeMaze.Cameras.CameraController3D);
                var mazeGridField = ccType.GetField("mazeGridBehaviour",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mazeGridField != null)
                {
                    mazeGridField.SetValue(cameraController3D, newMaze);
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

    internal class DelayedWaveStarter : MonoBehaviour
    {
        public void StartFirstWave(WaveSpawner waveSpawner, float delay)
        {
            StartCoroutine(StartAfterDelay(waveSpawner, delay));
        }

        private System.Collections.IEnumerator StartAfterDelay(WaveSpawner waveSpawner, float delay)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(delay);

            if (waveSpawner != null)
            {
                bool started = waveSpawner.StartWave();

                if (!started)
                {
                    yield return new WaitForSeconds(0.5f);
                    started = waveSpawner.StartWave();
                }
            }

            Destroy(gameObject);
        }
    }
}

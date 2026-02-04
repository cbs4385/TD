using UnityEngine;
using UnityEngine.SceneManagement;
using FaeMaze.UI;
using FaeMaze.Roguelike;

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

            // NOTE: RandomManager initialization is handled by GameController.Awake()
            // Do NOT reset it here - this would overwrite the tutorial seed set by GameController

            if (sceneName == "FaeMazeScene" || sceneName == "ProceduralMazeScene" || sceneName == "PlanarForestMazeScene" || sceneName == "Options")
            {
                GameObject escapeHandlerObj = GameObject.Find("EscapeHandler");
                if (escapeHandlerObj == null)
                {
                    escapeHandlerObj = new GameObject("EscapeHandler");
                    escapeHandlerObj.AddComponent<EscapeHandler>();
                }
            }

            if (sceneName == "ProceduralMazeScene" || sceneName == "PlanarForestMazeScene")
            {
                SetupProceduralMazeScene();
            }

            if (sceneName == "FaeMazeScene" || sceneName == "ProceduralMazeScene" || sceneName == "PlanarForestMazeScene")
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

                // Load heart prefabs (two-part model: base + tongue)
                GameObject heartBasePrefab = null;
                GameObject heartTonguePrefab = null;

                heartBasePrefab = Resources.Load<GameObject>("Prefabs/Tile/heartbase");
                heartTonguePrefab = Resources.Load<GameObject>("Prefabs/Tile/heart tongue");

                FaeMaze.Maze.HeartOfTheMaze heart = Object.FindFirstObjectByType<FaeMaze.Maze.HeartOfTheMaze>();

                if (heart == null)
                {
                    GameObject heartObj = new GameObject("HeartOfTheMaze");
                    heartObj.transform.SetParent(gameRoot.transform);
                    heartObj.SetActive(false);
                    heart = heartObj.AddComponent<FaeMaze.Maze.HeartOfTheMaze>();
                    heartObj.SetActive(true);
                }

                // Assign prefabs via reflection
                if (heart != null)
                {
                    var heartType = typeof(FaeMaze.Maze.HeartOfTheMaze);

                    if (heartBasePrefab != null)
                    {
                        var baseField = heartType.GetField("heartBasePrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (baseField != null)
                        {
                            baseField.SetValue(heart, heartBasePrefab);
                        }
                    }

                    if (heartTonguePrefab != null)
                    {
                        var tongueField = heartType.GetField("heartTonguePrefab",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (tongueField != null)
                        {
                            tongueField.SetValue(heart, heartTonguePrefab);
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

            // Wait for DynamicMazeGrowth initial growth stages to complete
            DynamicMazeGrowth dynamicMazeGrowth = Object.FindFirstObjectByType<DynamicMazeGrowth>();
            if (dynamicMazeGrowth != null && !dynamicMazeGrowth.IsInitialGrowthComplete)
            {
                // Wait until initial growth is complete
                while (!dynamicMazeGrowth.IsInitialGrowthComplete)
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }

            // Show heart form selection UI if player has multiple unlocked forms
            yield return ShowHeartFormSelection();

            // Show challenge selection UI if player has unlocked challenges
            yield return ShowChallengeSelection();

            // Show mutation selection UI if player has unlocked mutations
            yield return ShowMutationSelection();

            // Show blessing selection UI if player has unlocked blessings
            yield return ShowBlessingSelection();

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

        private System.Collections.IEnumerator ShowHeartFormSelection()
        {
            // Check if player has multiple unlocked forms (more than just the default)
            var unlockedForms = HeartFormManager.Instance?.GetUnlockedForms();
            if (unlockedForms == null || unlockedForms.Count <= 1)
            {
                Debug.Log("[DelayedWaveStarter] Only default form unlocked, skipping form selection");
                yield break;
            }

            // Create or find the HeartFormSelectionUI
            var formUI = Object.FindFirstObjectByType<HeartFormSelectionUI>();
            if (formUI == null)
            {
                GameObject uiObj = new GameObject("HeartFormSelectionUI");
                formUI = uiObj.AddComponent<HeartFormSelectionUI>();
            }

            // Show the UI and wait for selection
            bool selectionComplete = false;
            formUI.Show((selectedForm) =>
            {
                selectionComplete = true;
                if (selectedForm != null)
                {
                    Debug.Log($"[DelayedWaveStarter] Heart Form selected: {selectedForm.DisplayName}");
                }
                else
                {
                    Debug.Log("[DelayedWaveStarter] Default form selected");
                }
            });

            // Wait for selection to complete (use realtime since game is paused)
            while (!selectionComplete)
            {
                yield return null;
            }
        }

        private System.Collections.IEnumerator ShowChallengeSelection()
        {
            // Check if player has any unlocked challenges
            var unlockedChallenges = ChallengeModifierManager.Instance?.GetUnlockedChallenges();
            if (unlockedChallenges == null || unlockedChallenges.Count == 0)
            {
                Debug.Log("[DelayedWaveStarter] No unlocked challenges, skipping selection");
                yield break;
            }

            // Create or find the ChallengeSelectionUI
            var challengeUI = Object.FindFirstObjectByType<ChallengeSelectionUI>();
            if (challengeUI == null)
            {
                GameObject uiObj = new GameObject("ChallengeSelectionUI");
                challengeUI = uiObj.AddComponent<ChallengeSelectionUI>();
            }

            // Show the UI and wait for selection
            bool selectionComplete = false;
            challengeUI.Show((selectedChallenges) =>
            {
                selectionComplete = true;
                if (selectedChallenges != null && selectedChallenges.Count > 0)
                {
                    Debug.Log($"[DelayedWaveStarter] {selectedChallenges.Count} challenge(s) selected");
                }
                else
                {
                    Debug.Log("[DelayedWaveStarter] No challenges selected");
                }
            });

            // Wait for selection to complete (use realtime since game is paused)
            while (!selectionComplete)
            {
                yield return null;
            }
        }

        private System.Collections.IEnumerator ShowMutationSelection()
        {
            // Check if player has any unlocked mutations
            var unlockedMutations = PropMutationManager.Instance?.GetUnlockedMutations();
            if (unlockedMutations == null || unlockedMutations.Count == 0)
            {
                Debug.Log("[DelayedWaveStarter] No unlocked mutations, skipping selection");
                yield break;
            }

            // Create or find the PropMutationSelectionUI
            var mutationUI = Object.FindFirstObjectByType<PropMutationSelectionUI>();
            if (mutationUI == null)
            {
                GameObject uiObj = new GameObject("PropMutationSelectionUI");
                mutationUI = uiObj.AddComponent<PropMutationSelectionUI>();
            }

            // Show the UI and wait for selection
            bool selectionComplete = false;
            mutationUI.Show((selectedMutation) =>
            {
                selectionComplete = true;
                if (selectedMutation != null)
                {
                    Debug.Log($"[DelayedWaveStarter] Mutation selected: {selectedMutation.DisplayName}");
                }
                else
                {
                    Debug.Log("[DelayedWaveStarter] No mutation selected");
                }
            });

            // Wait for selection to complete (use realtime since game is paused)
            while (!selectionComplete)
            {
                yield return null;
            }
        }

        private System.Collections.IEnumerator ShowBlessingSelection()
        {
            // Check if player has any unlocked blessings
            var unlockedBlessings = BlessingManager.Instance?.GetUnlockedBlessings();
            if (unlockedBlessings == null || unlockedBlessings.Count == 0)
            {
                Debug.Log("[DelayedWaveStarter] No unlocked blessings, skipping selection");
                yield break;
            }

            // Create or find the BlessingSelectionUI
            var blessingUI = Object.FindFirstObjectByType<BlessingSelectionUI>();
            if (blessingUI == null)
            {
                GameObject uiObj = new GameObject("BlessingSelectionUI");
                blessingUI = uiObj.AddComponent<BlessingSelectionUI>();
            }

            // Show the UI and wait for selection
            bool selectionComplete = false;
            blessingUI.Show((selectedBlessing) =>
            {
                selectionComplete = true;
                if (selectedBlessing != null)
                {
                    Debug.Log($"[DelayedWaveStarter] Blessing selected: {selectedBlessing.DisplayName}");
                }
                else
                {
                    Debug.Log("[DelayedWaveStarter] No blessing selected");
                }
            });

            // Wait for selection to complete (use realtime since game is paused)
            while (!selectionComplete)
            {
                yield return null;
            }
        }
    }
}

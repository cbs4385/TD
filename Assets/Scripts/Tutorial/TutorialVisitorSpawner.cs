using UnityEngine;
using System.Collections;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Handles controlled visitor spawning during the tutorial.
    /// Spawns visitors at specific tutorial steps for guaranteed player interaction.
    /// </summary>
    public class TutorialVisitorSpawner : MonoBehaviour
    {
        #region Private Fields

        private TutorialManager manager;
        private TutorialEventTriggers eventTriggers;
        private WaveSpawner waveSpawner;
        private MazeGridBehaviour mazeGrid;
        private bool spawnerWasActive;
        private int visitorsSpawned;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            manager = TutorialManager.Instance;
            eventTriggers = GetComponent<TutorialEventTriggers>();
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
            mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();

            if (manager != null)
            {
                manager.OnTutorialStarted += OnTutorialStarted;
                manager.OnTutorialCompleted += OnTutorialCompleted;
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnTutorialStarted -= OnTutorialStarted;
                manager.OnTutorialCompleted -= OnTutorialCompleted;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTutorialStarted()
        {
            visitorsSpawned = 0;

            // Disable normal wave spawner during tutorial
            if (waveSpawner != null)
            {
                spawnerWasActive = waveSpawner.IsWaveActive;
                // Note: We don't stop the spawner, we just track the state
                // The tutorial controls when visitors spawn
            }
        }

        private void OnTutorialCompleted()
        {
            // Re-enable normal spawning after tutorial
            if (waveSpawner != null && !waveSpawner.IsWaveActive)
            {
                // Start the wave after tutorial if it wasn't running
                waveSpawner.StartWave();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawns a tutorial visitor at the maze entrance.
        /// </summary>
        public void SpawnTutorialVisitor()
        {
            StartCoroutine(SpawnVisitorCoroutine());
        }

        #endregion

        #region Visitor Spawning

        private IEnumerator SpawnVisitorCoroutine()
        {
            // Small delay for visual effect
            yield return new WaitForSecondsRealtime(0.5f);

            // Find entrance position
            Vector3 spawnPosition = GetSpawnPosition();

            // Get visitor prefab from wave spawner or load it
            GameObject visitorPrefab = GetVisitorPrefab();
            if (visitorPrefab == null)
            {
                Debug.LogWarning("[TutorialVisitorSpawner] Could not find visitor prefab");
                yield break;
            }

            // Spawn the visitor
            GameObject visitor = Instantiate(visitorPrefab, spawnPosition, Quaternion.identity);
            visitorsSpawned++;

            // Initialize visitor
            var controller = visitor.GetComponent<VisitorControllerBase>();
            if (controller != null)
            {
                // Initialize with GameController (visitor will find maze data internally)
                controller.Initialize();
            }

            // Notify event triggers
            if (eventTriggers != null)
            {
                eventTriggers.NotifyVisitorSpawned();
            }

            // Also notify via WaveSpawner's active visitor tracking
            if (waveSpawner != null)
            {
                // The visitor will register itself when initialized
            }

            Debug.Log($"[TutorialVisitorSpawner] Spawned tutorial visitor #{visitorsSpawned} at {spawnPosition}");
        }

        private Vector3 GetSpawnPosition()
        {
            // Try to get position from maze entrance
            var entrance = GameController.Instance?.Entrance;
            if (entrance != null)
            {
                return entrance.transform.position;
            }

            // Try to find entrance in scene
            var mazeEntrance = FindFirstObjectByType<MazeEntrance>();
            if (mazeEntrance != null)
            {
                return mazeEntrance.transform.position;
            }

            // Fallback: use maze grid to find an entrance node
            if (mazeGrid != null && mazeGrid.WorldSpaceMazeData != null)
            {
                foreach (var node in mazeGrid.WorldSpaceMazeData.GraphState.Nodes)
                {
                    if (node.Kind == "entrance" || node.Kind == "exit")
                    {
                        return new Vector3(node.Position.x, node.Position.y, 0);
                    }
                }
            }

            // Last resort: arbitrary position
            Debug.LogWarning("[TutorialVisitorSpawner] Could not find entrance, using default position");
            return new Vector3(0, 20, 0);
        }

        private GameObject GetVisitorPrefab()
        {
            // Try to get from WaveSpawner via reflection or serialized field access
            if (waveSpawner != null)
            {
                // Use reflection to access private basicVisitorPrefab field
                var field = typeof(WaveSpawner).GetField("basicVisitorPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    var prefab = field.GetValue(waveSpawner) as VisitorController;
                    if (prefab != null)
                    {
                        return prefab.gameObject;
                    }
                }
            }

            // Try to load from assets
#if UNITY_EDITOR
            var visitor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Visitor.prefab");
            if (visitor != null) return visitor;

            visitor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Visitor.prefab");
            if (visitor != null) return visitor;
#endif

            // Try Resources.Load as last resort
            var loaded = Resources.Load<GameObject>("Visitor");
            if (loaded != null) return loaded;

            return null;
        }

        #endregion
    }
}

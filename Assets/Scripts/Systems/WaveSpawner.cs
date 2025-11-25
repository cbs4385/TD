using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.Visitors;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Manages wave-based spawning of visitors from the entrance to the heart.
    /// Spawns visitors in waves with configurable count and interval.
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("The maze entrance where visitors spawn")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("The heart of the maze (destination)")]
        private HeartOfTheMaze heart;

        [Header("Wave Settings")]
        [SerializeField]
        [Tooltip("Number of visitors to spawn per wave")]
        private int visitorsPerWave = 10;

        [SerializeField]
        [Tooltip("Time interval between spawns (in seconds)")]
        private float spawnInterval = 1.0f;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private bool isSpawning;
        private int currentWaveNumber;
        private int visitorsSpawnedThisWave;
        private int totalVisitorsSpawned;

        #endregion

        #region Properties

        /// <summary>Gets whether a wave is currently spawning</summary>
        public bool IsSpawning => isSpawning;

        /// <summary>Gets the current wave number</summary>
        public int CurrentWaveNumber => currentWaveNumber;

        /// <summary>Gets the total number of visitors spawned</summary>
        public int TotalVisitorsSpawned => totalVisitorsSpawned;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find the MazeGridBehaviour in the scene
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            ValidateReferences();
        }

        #endregion

        #region Wave Management

        /// <summary>
        /// Starts spawning a new wave of visitors.
        /// </summary>
        public void StartWave()
        {
            if (isSpawning)
            {
                return;
            }

            if (!ValidateReferences())
            {
                return;
            }

            currentWaveNumber++;
            visitorsSpawnedThisWave = 0;


            StartCoroutine(SpawnWaveCoroutine());
        }

        /// <summary>
        /// Coroutine that spawns visitors for the current wave.
        /// </summary>
        private IEnumerator SpawnWaveCoroutine()
        {
            isSpawning = true;

            for (int i = 0; i < visitorsPerWave; i++)
            {
                SpawnVisitor();
                visitorsSpawnedThisWave++;

                // Wait before spawning next visitor (unless it's the last one)
                if (i < visitorsPerWave - 1)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            isSpawning = false;
        }

        #endregion

        #region Visitor Spawning

        /// <summary>
        /// Spawns a single visitor at the entrance with a path to the heart.
        /// </summary>
        private void SpawnVisitor()
        {
            if (GameController.Instance == null)
            {
                return;
            }

            // Get grid positions
            Vector2Int entrancePos = entrance.GridPosition;
            Vector2Int heartPos = heart.GridPosition;

            // Find path using A*
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(entrancePos, heartPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                return;
            }

            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(entrancePos.x, entrancePos.y);

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);
            visitor.gameObject.name = $"Visitor_Wave{currentWaveNumber}_{visitorsSpawnedThisWave}";

            GameController.Instance.SetLastSpawnedVisitor(visitor);

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path
            visitor.SetPath(pathNodes);

            totalVisitorsSpawned++;

        }

        #endregion

        #region Validation

        private bool ValidateReferences()
        {
            bool isValid = true;

            if (visitorPrefab == null)
            {
                isValid = false;
            }

            if (entrance == null)
            {
                isValid = false;
            }

            if (heart == null)
            {
                isValid = false;
            }

            if (mazeGridBehaviour == null)
            {
                isValid = false;
            }

            return isValid;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (entrance != null && heart != null && mazeGridBehaviour != null)
            {
                // Draw line from entrance to heart
                Vector3 entranceWorld = mazeGridBehaviour.GridToWorld(entrance.GridPosition.x, entrance.GridPosition.y);
                Vector3 heartWorld = mazeGridBehaviour.GridToWorld(heart.GridPosition.x, heart.GridPosition.y);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(entranceWorld, heartWorld);

                // Draw spawn position
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(entranceWorld, 0.5f);
            }
        }

        #endregion
    }
}

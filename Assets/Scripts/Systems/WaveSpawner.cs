using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.Audio;
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

        [Header("Scene References (Legacy - Optional)")]
        [SerializeField]
        [Tooltip("(LEGACY) The maze entrance where visitors spawn. Leave empty to use spawn markers.")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("(LEGACY) The heart of the maze (destination). Leave empty to use spawn markers.")]
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
        /// Spawns a single visitor at a random start spawn point with a path to a different destination spawn point.
        /// Falls back to entrance/heart if spawn markers are not available.
        /// </summary>
        private void SpawnVisitor()
        {
            if (GameController.Instance == null)
            {
                return;
            }

            Vector2Int startPos;
            Vector2Int destPos;
            char startId = '\0';
            char destId = '\0';

            // Try to use spawn marker system first
            if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                // Use random spawn points
                if (!mazeGridBehaviour.TryGetRandomSpawnPair(out startId, out startPos, out destId, out destPos))
                {
                    Debug.LogError("WaveSpawner: Failed to get random spawn pair!");
                    return;
                }
            }
            // Fall back to legacy entrance/heart system
            else if (entrance != null && heart != null)
            {
                startPos = entrance.GridPosition;
                destPos = heart.GridPosition;
                Debug.LogWarning("WaveSpawner: Using legacy entrance/heart system. Consider adding spawn markers (A, B, C, D) to your maze file.");
            }
            else
            {
                Debug.LogError("WaveSpawner: No spawn system available! Need either 2+ spawn markers or entrance/heart references.");
                return;
            }

            // Find path using A*
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(startPos, destPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                Debug.LogWarning($"WaveSpawner: No path found from {startPos} to {destPos}");
                return;
            }

            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(startPos.x, startPos.y);

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);

            // Name includes spawn IDs if using spawn marker system
            if (startId != '\0')
            {
                visitor.gameObject.name = $"Visitor_W{currentWaveNumber}_{visitorsSpawnedThisWave}_{startId}to{destId}";
            }
            else
            {
                visitor.gameObject.name = $"Visitor_Wave{currentWaveNumber}_{visitorsSpawnedThisWave}";
            }

            SoundManager.Instance?.PlayVisitorSpawn();

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
                Debug.LogError("WaveSpawner: Visitor prefab not assigned!");
                isValid = false;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WaveSpawner: MazeGridBehaviour not found in scene!");
                isValid = false;
            }

            // Check if we have either spawn markers or legacy entrance/heart
            if (mazeGridBehaviour != null)
            {
                bool hasSpawnMarkers = mazeGridBehaviour.GetSpawnPointCount() >= 2;
                bool hasLegacySystem = entrance != null && heart != null;

                if (!hasSpawnMarkers && !hasLegacySystem)
                {
                    Debug.LogError("WaveSpawner: No spawn system available! Need either:\n" +
                        "  - 2+ spawn markers (A, B, C, D) in maze file, OR\n" +
                        "  - Entrance and Heart references assigned");
                    isValid = false;
                }
            }

            return isValid;
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// Spawns a single visitor immediately for debug purposes.
        /// </summary>
        public void SpawnSingleVisitorForDebug()
        {
            if (!ValidateReferences())
            {
                Debug.LogWarning("Cannot spawn debug visitor - references not valid");
                return;
            }

            SpawnVisitor();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (mazeGridBehaviour == null)
                return;

            // Draw spawn markers if available
            if (mazeGridBehaviour.GetSpawnPointCount() >= 2)
            {
                var spawnPoints = mazeGridBehaviour.GetAllSpawnPoints();

                // Draw each spawn point
                foreach (var kvp in spawnPoints)
                {
                    char spawnId = kvp.Key;
                    Vector2Int gridPos = kvp.Value;
                    Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridPos.x, gridPos.y);

                    // Color based on spawn ID
                    Gizmos.color = GetSpawnMarkerColor(spawnId);
                    Gizmos.DrawWireSphere(worldPos, 0.5f);

                    // Draw label (only in selected gizmos)
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(worldPos + Vector3.up * 0.7f, spawnId.ToString());
                    #endif
                }

                // Draw lines between all spawn point pairs
                Gizmos.color = new Color(0.5f, 1f, 0.5f, 0.3f);
                var keys = new List<char>(spawnPoints.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    for (int j = i + 1; j < keys.Count; j++)
                    {
                        Vector3 pos1 = mazeGridBehaviour.GridToWorld(spawnPoints[keys[i]].x, spawnPoints[keys[i]].y);
                        Vector3 pos2 = mazeGridBehaviour.GridToWorld(spawnPoints[keys[j]].x, spawnPoints[keys[j]].y);
                        Gizmos.DrawLine(pos1, pos2);
                    }
                }
            }
            // Fall back to legacy entrance/heart visualization
            else if (entrance != null && heart != null)
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

        private Color GetSpawnMarkerColor(char spawnId)
        {
            switch (spawnId)
            {
                case 'A': return Color.cyan;
                case 'B': return Color.magenta;
                case 'C': return Color.yellow;
                case 'D': return new Color(1f, 0.5f, 0f); // Orange
                default: return Color.white;
            }
        }

        #endregion
    }
}

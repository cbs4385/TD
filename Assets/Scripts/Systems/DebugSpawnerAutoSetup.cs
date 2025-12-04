using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.DebugTools;
using FaeMaze.Audio;
using FaeMaze.Visitors;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Component that automatically creates a DebugVisitorSpawner helper in the scene.
    /// Attach this to any GameObject and it will set up visitor spawning on press of Space key.
    /// </summary>
    public class DebugSpawnerAutoSetup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        private MazeEntrance entrance;
        private HeartOfTheMaze heart;
        private MazeGridBehaviour mazeGridBehaviour;

        private void Start()
        {
            // Find required components
            entrance = FindFirstObjectByType<MazeEntrance>();
            heart = FindFirstObjectByType<HeartOfTheMaze>();
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (entrance == null || heart == null || mazeGridBehaviour == null || visitorPrefab == null)
            {
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                SpawnVisitor();
            }
        }

        private void SpawnVisitor()
        {
            if (visitorPrefab == null)
            {
                return;
            }

            if (entrance == null || heart == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Get grid positions
            Vector2Int entrancePos = entrance.GridPosition;
            Vector2Int heartPos = heart.GridPosition;


            // Find path using A* through GameController
            System.Collections.Generic.List<MazeGrid.MazeNode> pathNodes = new System.Collections.Generic.List<MazeGrid.MazeNode>();
            bool pathFound = GameController.Instance.TryFindPath(entrancePos, heartPos, pathNodes);

            if (!pathFound || pathNodes.Count == 0)
            {
                return;
            }


            // Get world position for spawn
            Vector3 spawnWorldPos = mazeGridBehaviour.GridToWorld(entrancePos.x, entrancePos.y);

            // Instantiate visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.identity);
            visitor.gameObject.name = $"Visitor_{Time.frameCount}";

            SoundManager.Instance?.PlayVisitorSpawn();

            // Initialize visitor
            visitor.Initialize(GameController.Instance);

            // Set path
            visitor.SetPath(pathNodes);

        }
    }
}

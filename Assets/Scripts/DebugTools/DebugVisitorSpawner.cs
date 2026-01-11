using UnityEngine;
using UnityEngine.InputSystem;
using FaeMaze.Audio;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Visitors;

namespace FaeMaze.DebugTools
{
    /// <summary>
    /// Debug utility for spawning visitors at the entrance.
    /// Press Space to spawn a visitor that walks in a straight line to the heart.
    /// </summary>
    public class DebugVisitorSpawner : MonoBehaviour
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

        [Header("Spawn Settings")]
        [SerializeField]
        [Tooltip("Offset from entrance to spawn visitor (to avoid overlapping)")]
        private Vector3 spawnOffset = Vector3.zero;

        #endregion

        #region Private Fields

        private int visitorSpawnCount = 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            ValidateReferences();
        }

        private void Update()
        {
            // Check for Space key press using new Input System
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                SpawnVisitor();
            }
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawns a visitor at the entrance position.
        /// </summary>
        public void SpawnVisitor()
        {
            if (!ValidateReferences())
            {
                return;
            }

            // Get world position for spawn directly from entrance transform
            Vector3 spawnWorldPos = entrance.transform.position + spawnOffset;

            // Instantiate visitor (rotated 180 degrees on z-axis)
            VisitorController visitor = Instantiate(visitorPrefab, spawnWorldPos, Quaternion.Euler(0, 0, 180));
            visitor.gameObject.name = $"Visitor_{visitorSpawnCount++}";

            SoundManager.Instance?.PlayVisitorSpawn();

            GameController.Instance.SetLastSpawnedVisitor(visitor);

            // Initialize visitor - pathfinding is handled by the visitor controller in world-space mode
            visitor.Initialize(GameController.Instance);
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

            return isValid;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (entrance != null && heart != null)
            {
                // Draw line from entrance to heart using world positions
                Vector3 entranceWorld = entrance.transform.position;
                Vector3 heartWorld = heart.transform.position;

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(entranceWorld, heartWorld);

                // Draw spawn position
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(entranceWorld + spawnOffset, 0.3f);
            }
        }

        #endregion
    }
}

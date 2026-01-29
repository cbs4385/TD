using UnityEngine;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the entrance point of the maze.
    /// This is where visitors will spawn or enter the maze.
    /// </summary>
    public class MazeEntrance : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Position Settings")]
        [SerializeField]
        [Tooltip("Automatically position entrance from maze data")]
        private bool autoPosition = true;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-position from maze grid if enabled
            if (autoPosition)
            {
                PositionFromMazeGrid();
            }
        }

        private void Start()
        {
            // Position is set in Awake
        }

        /// <summary>
        /// Positions the entrance using the maze's world-space entrance position.
        /// Can be called to reposition after maze regeneration.
        /// </summary>
        public void PositionFromMazeGrid()
        {
            // Find MazeGridBehaviour in scene
            var mazeGridBehaviour = FindFirstObjectByType<FaeMaze.Systems.MazeGridBehaviour>();
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Get entrance position directly in world space
            Vector3 worldPos = mazeGridBehaviour.EntranceWorldPosition;
            transform.position = worldPos;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw entrance marker in scene view
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.7f);
        }

        #endregion
    }
}

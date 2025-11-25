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

        [Header("Grid Position")]
        [SerializeField]
        [Tooltip("X coordinate in the maze grid")]
        private int gridX;

        [SerializeField]
        [Tooltip("Y coordinate in the maze grid")]
        private int gridY;

        #endregion

        #region Properties

        /// <summary>Gets the grid position of this entrance</summary>
        public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the grid position for this entrance.
        /// </summary>
        /// <param name="pos">The grid position to set</param>
        public void SetGridPosition(Vector2Int pos)
        {
            gridX = pos.x;
            gridY = pos.y;
            Debug.Log($"MazeEntrance grid position set to: ({gridX}, {gridY})");
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Debug.Log($"MazeEntrance initialized at grid position ({gridX}, {gridY}), world position {transform.position}");
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

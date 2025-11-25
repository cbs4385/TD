using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Visitors;

namespace FaeMaze.Maze
{
    /// <summary>
    /// Represents the Heart of the Maze - the goal location where visitors are consumed for essence.
    /// </summary>
    public class HeartOfTheMaze : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Grid Position")]
        [SerializeField]
        [Tooltip("X coordinate in the maze grid")]
        private int gridX;

        [SerializeField]
        [Tooltip("Y coordinate in the maze grid")]
        private int gridY;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Amount of essence gained per visitor consumed")]
        private int essencePerVisitor = 10;

        #endregion

        #region Properties

        /// <summary>Gets the grid position of the heart</summary>
        public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

        /// <summary>Gets the essence value per visitor</summary>
        public int EssencePerVisitor => essencePerVisitor;

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the grid position for the heart.
        /// </summary>
        /// <param name="pos">The grid position to set</param>
        public void SetGridPosition(Vector2Int pos)
        {
            gridX = pos.x;
            gridY = pos.y;
            Debug.Log($"HeartOfTheMaze grid position set to: ({gridX}, {gridY})");
        }

        /// <summary>
        /// Called when a visitor reaches the heart and is consumed.
        /// </summary>
        /// <param name="visitor">The visitor controller to consume</param>
        public void OnVisitorConsumed(VisitorController visitor)
        {
            if (visitor == null)
            {
                Debug.LogWarning("Attempted to consume null visitor!");
                return;
            }

            Debug.Log($"Visitor {visitor.gameObject.name} consumed at Heart! Gaining {essencePerVisitor} essence.");

            // Add essence to game controller
            if (GameController.Instance != null)
            {
                GameController.Instance.AddEssence(essencePerVisitor);
            }
            else
            {
                Debug.LogError("GameController instance is null! Cannot add essence.");
            }

            // Destroy the visitor
            Destroy(visitor.gameObject);
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Debug.Log($"HeartOfTheMaze initialized at grid position ({gridX}, {gridY}), world position {transform.position}");
            Debug.Log($"Essence per visitor: {essencePerVisitor}");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Check if a visitor entered the heart
            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null)
            {
                OnVisitorConsumed(visitor);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw heart marker in scene view
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw a pulsing effect
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f + pulse);
        }

        #endregion
    }
}

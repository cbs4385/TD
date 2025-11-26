using UnityEngine;
using FaeMaze.Maze;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Renders the maze grid visually using sprites.
    /// Creates a visual representation of walls and pathways.
    /// </summary>
    [RequireComponent(typeof(MazeGridBehaviour))]
    public class MazeRenderer : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color for walkable path tiles")]
        private Color pathColor = new Color(0.8f, 0.9f, 0.8f, 1f); // Light green

        [SerializeField]
        [Tooltip("Color for wall tiles")]
        private Color wallColor = new Color(0.2f, 0.3f, 0.2f, 1f); // Dark green

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 0;

        [SerializeField]
        [Tooltip("Parent transform to hold all tile sprites")]
        private Transform tilesParent;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private GameObject tilesContainer;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("MazeRenderer requires MazeGridBehaviour component!");
                return;
            }

            RenderMaze();
        }

        #endregion

        #region Rendering

        private void RenderMaze()
        {
            if (mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("MazeGrid is null! Cannot render maze.");
                return;
            }

            // Create container for tiles if not assigned
            if (tilesParent == null)
            {
                tilesContainer = new GameObject("MazeTiles");
                tilesContainer.transform.SetParent(transform);
                tilesContainer.transform.localPosition = Vector3.zero;
                tilesParent = tilesContainer.transform;
            }

            MazeGrid grid = mazeGridBehaviour.Grid;
            int width = grid.Width;
            int height = grid.Height;


            int renderedTiles = 0;

            // Create a sprite for each grid cell
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var node = grid.GetNode(x, y);
                    if (node == null) continue;

                    // Determine color based on walkability
                    Color tileColor = node.walkable ? pathColor : wallColor;

                    // Create tile sprite
                    CreateTileSprite(x, y, tileColor);
                    renderedTiles++;
                }
            }

        }

        private void CreateTileSprite(int gridX, int gridY, Color color)
        {
            // Create GameObject for this tile
            GameObject tileObj = new GameObject($"Tile_{gridX}_{gridY}");
            tileObj.transform.SetParent(tilesParent);

            // Position in world space
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);
            tileObj.transform.position = worldPos;

            // Add SpriteRenderer
            SpriteRenderer spriteRenderer = tileObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateSquareSprite();
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;

            // Scale to fit grid cell (assuming 1 unit per cell)
            tileObj.transform.localScale = new Vector3(1f, 1f, 1f); // Slight gap between tiles
        }

        /// <summary>
        /// Creates a simple square sprite using a 1x1 white texture.
        /// </summary>
        private Sprite CreateSquareSprite()
        {
            // Create a 1x1 white texture
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            // Create sprite from texture
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), // Pivot at center
                1f // Pixels per unit
            );

            return sprite;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Forces a complete re-render of the maze.
        /// </summary>
        public void RefreshMaze()
        {
            // Clear existing tiles
            if (tilesParent != null)
            {
                foreach (Transform child in tilesParent)
                {
                    Destroy(child.gameObject);
                }
            }

            // Re-render
            RenderMaze();
        }

        #endregion
    }
}

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
        private Color pathColor = Color.white; // White

        [SerializeField]
        [Tooltip("Sprite for wall tiles (trees/brambles)")]
        private Sprite wallSprite;

        [SerializeField]
        [Tooltip("Prefab/model for wall tiles (trees/brambles)")]
        private GameObject wallPrefab;

        [SerializeField]
        [Tooltip("Sprite for undergrowth tiles")]
        private Sprite undergrowthSprite;

        [SerializeField]
        [Tooltip("Prefab/model for undergrowth tiles")]
        private GameObject undergrowthPrefab;

        [SerializeField]
        [Tooltip("Sprite for water tiles")]
        private Sprite waterSprite;

        [SerializeField]
        [Tooltip("Prefab/model for water tiles")]
        private GameObject waterPrefab;

        [SerializeField]
        [Tooltip("Color tint for wall tiles")]
        private Color wallColor = Color.black;
        
        [SerializeField]
        [Tooltip("Color for undergrowth tiles")]
        private Color undergrowthColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple

        [SerializeField]
        [Tooltip("Color for water tiles")]
        private Color waterColor = Color.magenta; // Magenta

        [SerializeField]
        [Tooltip("Color for the heart tile")]
        private Color heartColor = new Color(0.9f, 0.35f, 0.35f, 1f); // Highlighted

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

        #region Public API

        /// <summary>
        /// Indicates whether a wall prefab has been assigned.
        /// </summary>
        public bool HasWallPrefab => wallPrefab != null;

        /// <summary>
        /// Assigns the wall prefab to use when rendering wall tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for walls.</param>
        public void SetWallPrefab(GameObject prefab)
        {
            wallPrefab = prefab;
        }

        /// <summary>
        /// Indicates whether an undergrowth prefab has been assigned.
        /// </summary>
        public bool HasUndergrowthPrefab => undergrowthPrefab != null;

        /// <summary>
        /// Assigns the undergrowth prefab to use when rendering undergrowth tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for undergrowth.</param>
        public void SetUndergrowthPrefab(GameObject prefab)
        {
            undergrowthPrefab = prefab;
        }

        /// <summary>
        /// Indicates whether a water prefab has been assigned.
        /// </summary>
        public bool HasWaterPrefab => waterPrefab != null;

        /// <summary>
        /// Assigns the water prefab to use when rendering water tiles.
        /// </summary>
        /// <param name="prefab">Prefab or model to instantiate for water.</param>
        public void SetWaterPrefab(GameObject prefab)
        {
            waterPrefab = prefab;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Force color values to new defaults (overrides serialized values)
            pathColor = Color.saddleBrown;
            //wallColor = Color.black;
            //undergrowthColor = new Color(0.5f, 0f, 0.5f, 1f); // Purple
            waterColor = Color.magenta;
        }

        private void Start()
        {
            mazeGridBehaviour = GetComponent<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
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
                return;
            }

            // Log prefab status for debugging
            if (wallPrefab != null)
            {
                Debug.Log($"MazeRenderer: wallPrefab is set to {wallPrefab.name}");
            }
            else
            {
                Debug.LogWarning("MazeRenderer: wallPrefab is NULL! Walls will use sprites instead.");
            }

            if (undergrowthPrefab != null)
            {
                Debug.Log($"MazeRenderer: undergrowthPrefab is set to {undergrowthPrefab.name}");
            }
            else
            {
                Debug.LogWarning("MazeRenderer: undergrowthPrefab is NULL! Undergrowth will use sprites instead.");
            }

            if (waterPrefab != null)
            {
                Debug.Log($"MazeRenderer: waterPrefab is set to {waterPrefab.name}");
            }
            else
            {
                Debug.LogWarning("MazeRenderer: waterPrefab is NULL! Water will use sprites instead.");
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

                    // Determine color based on tile symbol definitions
                    Color tileColor = GetColorForSymbol(node.symbol, node.walkable);

                    // Create tile sprite
                    CreateTileSprite(x, y, node.symbol, tileColor);
                    renderedTiles++;
                }
            }

        }

        private void CreateTileSprite(int gridX, int gridY, char symbol, Color color)
        {
            Vector3 worldPos = mazeGridBehaviour.GridToWorld(gridX, gridY);

            // Add random jitter for wall and undergrowth sprites/prefabs
            bool useWallPrefab = symbol == '#' && wallPrefab != null;
            bool useUndergrowthPrefab = symbol == ';' && undergrowthPrefab != null;
            bool useWaterPrefab = symbol == '~' && waterPrefab != null;
            bool isWallSprite = symbol == '#' && wallSprite != null && !useWallPrefab;
            bool isUndergrowthSprite = symbol == ';' && undergrowthSprite != null && !useUndergrowthPrefab;
            bool isWaterSprite = symbol == '~' && waterSprite != null && !useWaterPrefab;
            if (isWallSprite || isUndergrowthSprite || isWaterSprite || useWallPrefab || useUndergrowthPrefab || useWaterPrefab)
            {
                float jitterX = Random.Range(-0.02f, 0.02f); // +/- 2 pixels (assuming 100 pixels per unit)
                float jitterY = Random.Range(-0.02f, 0.02f);
                worldPos += new Vector3(jitterX, jitterY, 0f);
            }

            float tileSize = mazeGridBehaviour.TileSize;

            if (useWallPrefab)
            {
                Debug.Log($"Using wall prefab for tile at ({gridX}, {gridY})");
                GameObject wallTile = Instantiate(wallPrefab, tilesParent);
                wallTile.name = $"Tile_{gridX}_{gridY}_Wall";
                wallTile.transform.position = worldPos;
                wallTile.transform.localScale = Vector3.one * tileSize;
                return;
            }

            if (useUndergrowthPrefab)
            {
                Debug.Log($"Using undergrowth prefab for tile at ({gridX}, {gridY})");
                GameObject undergrowthTile = Instantiate(undergrowthPrefab, tilesParent);
                undergrowthTile.name = $"Tile_{gridX}_{gridY}_Undergrowth";
                undergrowthTile.transform.position = worldPos;
                undergrowthTile.transform.localScale = Vector3.one * tileSize;
                return;
            }

            if (useWaterPrefab)
            {
                Debug.Log($"Using water prefab for tile at ({gridX}, {gridY})");
                GameObject waterTile = Instantiate(waterPrefab, tilesParent);
                waterTile.name = $"Tile_{gridX}_{gridY}_Water";
                waterTile.transform.position = worldPos;
                waterTile.transform.localScale = Vector3.one * tileSize;
                return;
            }

            if (symbol == '#')
            {
                Debug.LogWarning($"Wall tile at ({gridX}, {gridY}) not using prefab - wallPrefab is null!");
            }

            if (symbol == ';')
            {
                Debug.LogWarning($"Undergrowth tile at ({gridX}, {gridY}) not using prefab - undergrowthPrefab is null!");
            }

            if (symbol == '~')
            {
                Debug.LogWarning($"Water tile at ({gridX}, {gridY}) not using prefab - waterPrefab is null!");
            }

            GameObject tileObj = new GameObject($"Tile_{gridX}_{gridY}");
            tileObj.transform.SetParent(tilesParent);
            tileObj.transform.position = worldPos;

            SpriteRenderer spriteRenderer = tileObj.AddComponent<SpriteRenderer>();

            // NEW: use wallSprite for '#' tiles, otherwise fallback to square sprite
            Sprite spriteToUse;
            if (isWallSprite)
            {
                spriteToUse = wallSprite;
            }
            else if (isUndergrowthSprite)
            {
                spriteToUse = undergrowthSprite;
            }
            else if (isWaterSprite)
            {
                spriteToUse = waterSprite;
            }
            else
            {
                spriteToUse = CreateSquareSprite();
            }

            spriteRenderer.sprite = spriteToUse;
            spriteRenderer.color = color;

            // Set sorting order based on tile type (water < path < undergrowth < walls)
            spriteRenderer.sortingOrder = sortingOrder + GetLayerOffsetForSymbol(symbol);

            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 1f);
        }

        /// <summary>
        /// Returns the layer offset for a given tile symbol.
        /// Water (0) < Path (1) < Undergrowth (2) < Walls (3)
        /// </summary>
        private int GetLayerOffsetForSymbol(char symbol)
        {
            switch (symbol)
            {
                case '~': // Water
                    return 0;
                case '.': // Path
                case 'H': // Heart (treated as path)
                    return 1;
                case ';': // Undergrowth
                    return 2;
                case '#': // Tree/bramble walls
                    return 3;
                default:
                    return 1; // Default to path layer
            }
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

        private Color GetColorForSymbol(char symbol, bool walkable)
        {
            switch (symbol)
            {
                case '#':
                    return Color.white; // Use white to preserve sprite's original colors
                case ';':
                    return Color.white; // Use white to preserve sprite's original colors
                case '~':
                    return Color.white; // Use white to preserve sprite's original colors
                case 'H':
                    return heartColor;
                case '.':
                    return pathColor;
                default:
                    return walkable ? pathColor : wallColor;
            }
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

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Puka that links water tiles and creates hazards for visitors.
    /// When visitors become adjacent to a Puka, they may be teleported or destroyed.
    /// Pukas automatically link to at least two water tiles in the maze.
    /// </summary>
    public class PukaHazard : MonoBehaviour
    {
        #region Static Registry

        private static readonly List<PukaHazard> _allPukas = new List<PukaHazard>();

        /// <summary>Gets all active Pukas in the scene</summary>
        public static IReadOnlyList<PukaHazard> All => _allPukas;

        #endregion

        #region Serialized Fields

        [Header("Interaction Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) that nothing happens when visitor is adjacent")]
        [Range(0f, 1f)]
        private float noInteractionChance = 0.2f;

        [SerializeField]
        [Tooltip("Chance (0-1) that visitor is teleported to linked water tile")]
        [Range(0f, 1f)]
        private float teleportChance = 0.7f;

        [SerializeField]
        [Tooltip("Chance (0-1) that visitor is killed")]
        [Range(0f, 1f)]
        private float killChance = 0.1f;

        [SerializeField]
        [Tooltip("How often to scan for adjacent visitors (seconds)")]
        private float scanInterval = 0.5f;

        [SerializeField]
        [Tooltip("Maximum number of water tiles to link (0 = unlimited)")]
        private int maxLinkedTiles = 5;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the Puka sprite (default green)")]
        private Color pukaColor = new Color(0f, 1f, 0f, 1f); // Green

        [SerializeField]
        [Tooltip("Size of the Puka sprite")]
        private float pukaSize = 0.6f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 14;

        [SerializeField]
        [Tooltip("Enable pulsing glow effect")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 2.5f;

        [SerializeField]
        [Tooltip("Pulse magnitude")]
        private float pulseMagnitude = 0.12f;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals")]
        private bool useProceduralSprite = true;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw linked water tiles in Scene view")]
        private bool debugDrawLinks = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private Vector2Int gridPosition;
        private List<Vector2Int> linkedWaterTiles;
        private HashSet<GameObject> processedVisitors; // Track which visitors we've already interacted with
        private float scanTimer;
        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;
        private Vector3 initialScale;

        #endregion

        #region Properties

        /// <summary>Gets the grid position of this Puka</summary>
        public Vector2Int GridPosition => gridPosition;

        /// <summary>Gets the list of linked water tiles</summary>
        public IReadOnlyList<Vector2Int> LinkedWaterTiles => linkedWaterTiles;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            initialScale = transform.localScale;
            linkedWaterTiles = new List<Vector2Int>();
            processedVisitors = new HashSet<GameObject>();
            SetupSpriteRenderer();
        }

        private void Start()
        {
            // Find references
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("PukaHazard: MazeGridBehaviour not found!");
                return;
            }

            // Get grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                Debug.LogError("PukaHazard: Could not convert world position to grid position!");
                return;
            }

            gridPosition = new Vector2Int(x, y);

            // Find and link water tiles
            FindAndLinkWaterTiles();

            Debug.Log($"PukaHazard at {gridPosition} linked to {linkedWaterTiles.Count} water tiles");
        }

        private void OnEnable()
        {
            if (!_allPukas.Contains(this))
            {
                _allPukas.Add(this);
            }
        }

        private void OnDisable()
        {
            _allPukas.Remove(this);
        }

        private void Update()
        {
            if (enablePulse && spriteRenderer != null)
            {
                UpdatePulse();
            }

            // Periodically scan for adjacent visitors
            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                scanTimer = 0f;
                ScanForAdjacentVisitors();
            }
        }

        #endregion

        #region Water Tile Linking

        /// <summary>
        /// Finds and links to water tiles in the maze.
        /// Links to at least 2 water tiles if available.
        /// </summary>
        private void FindAndLinkWaterTiles()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return;
            }

            var grid = mazeGridBehaviour.Grid;
            linkedWaterTiles.Clear();

            // Find all water tiles in the maze
            List<Vector2Int> allWaterTiles = new List<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    var node = grid.GetNode(x, y);
                    if (node != null && node.terrain == TileType.Water)
                    {
                        Vector2Int waterPos = new Vector2Int(x, y);
                        // Don't link to ourselves
                        if (waterPos != gridPosition)
                        {
                            allWaterTiles.Add(waterPos);
                        }
                    }
                }
            }

            if (allWaterTiles.Count == 0)
            {
                Debug.LogWarning($"PukaHazard at {gridPosition}: No other water tiles found to link to!");
                return;
            }

            // Shuffle the list for random selection
            ShuffleList(allWaterTiles);

            // Link to at least 2 water tiles (or all available if fewer than 2)
            int tilesToLink = Mathf.Min(
                maxLinkedTiles > 0 ? maxLinkedTiles : allWaterTiles.Count,
                allWaterTiles.Count
            );
            tilesToLink = Mathf.Max(tilesToLink, Mathf.Min(2, allWaterTiles.Count));

            for (int i = 0; i < tilesToLink; i++)
            {
                linkedWaterTiles.Add(allWaterTiles[i]);
            }
        }

        /// <summary>
        /// Shuffles a list using Fisher-Yates algorithm.
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        #endregion

        #region Visitor Detection and Interaction

        private bool IsVisitorActive(FaeMaze.Visitors.VisitorControllerBase.VisitorState state)
        {
            return state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Walking
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Fascinated
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Confused
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Frightened;
        }

        /// <summary>
        /// Scans for visitors adjacent to this Puka (within 1 grid tile).
        /// </summary>
        private void ScanForAdjacentVisitors()
        {
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Find all visitors in the scene
            VisitorController[] allVisitors = FindObjectsByType<VisitorController>(FindObjectsSortMode.None);
            MistakingVisitorController[] mistakingVisitors = FindObjectsByType<MistakingVisitorController>(FindObjectsSortMode.None);

            // Process regular visitors
            foreach (var visitor in allVisitors)
            {
                if (visitor == null || processedVisitors.Contains(visitor.gameObject))
                {
                    continue;
                }

                // Check if visitor is walking
                if (!IsVisitorActive(visitor.State))
                {
                    continue;
                }

                // Get visitor grid position
                if (mazeGridBehaviour.WorldToGrid(visitor.transform.position, out int vx, out int vy))
                {
                    Vector2Int visitorPos = new Vector2Int(vx, vy);

                    // Check if visitor is adjacent (Manhattan distance = 1)
                    int distance = Mathf.Abs(visitorPos.x - gridPosition.x) + Mathf.Abs(visitorPos.y - gridPosition.y);
                    if (distance == 1)
                    {
                        InteractWithVisitor(visitor.gameObject, visitorPos);
                    }
                }
            }

            // Process mistaking visitors
            foreach (var mistakingVisitor in mistakingVisitors)
            {
                if (mistakingVisitor == null || processedVisitors.Contains(mistakingVisitor.gameObject))
                {
                    continue;
                }

                if (!IsVisitorActive(mistakingVisitor.State))
                {
                    continue;
                }

                // Get visitor grid position
                if (mazeGridBehaviour.WorldToGrid(mistakingVisitor.transform.position, out int vx, out int vy))
                {
                    Vector2Int visitorPos = new Vector2Int(vx, vy);

                    // Check if visitor is adjacent (Manhattan distance = 1)
                    int distance = Mathf.Abs(visitorPos.x - gridPosition.x) + Mathf.Abs(visitorPos.y - gridPosition.y);
                    if (distance == 1)
                    {
                        InteractWithVisitor(mistakingVisitor.gameObject, visitorPos);
                    }
                }
            }

            // Clean up destroyed visitors from processed set
            processedVisitors.RemoveWhere(v => v == null);
        }

        /// <summary>
        /// Interacts with a visitor that has become adjacent to this Puka.
        /// Rolls for one of three outcomes: no interaction, teleport, or kill.
        /// </summary>
        private void InteractWithVisitor(GameObject visitorObject, Vector2Int visitorPos)
        {
            if (visitorObject == null)
            {
                return;
            }

            // Mark as processed
            processedVisitors.Add(visitorObject);

            // Roll for interaction
            float roll = Random.value;

            if (roll < noInteractionChance)
            {
                // 20% - No interaction
                Debug.Log($"PukaHazard: Visitor at {visitorPos} avoided Puka hazard (no interaction)");
                return;
            }
            else if (roll < noInteractionChance + teleportChance)
            {
                // 70% - Teleport to linked water tile
                TeleportVisitor(visitorObject, visitorPos);
            }
            else
            {
                // 10% - Kill visitor
                KillVisitor(visitorObject, visitorPos);
            }
        }

        /// <summary>
        /// Teleports a visitor to a randomly selected linked water tile.
        /// </summary>
        private void TeleportVisitor(GameObject visitorObject, Vector2Int fromPos)
        {
            if (linkedWaterTiles.Count == 0)
            {
                Debug.LogWarning($"PukaHazard: Cannot teleport visitor - no linked water tiles!");
                return;
            }

            // Pick a random linked water tile
            Vector2Int targetTile = linkedWaterTiles[Random.Range(0, linkedWaterTiles.Count)];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetTile.x, targetTile.y);

            // Teleport the visitor
            visitorObject.transform.position = targetWorldPos;

            Debug.Log($"PukaHazard: Teleported visitor from {fromPos} to {targetTile}");

            // Try to recalculate path for the visitor
            var visitorController = visitorObject.GetComponent<VisitorController>();
            if (visitorController != null && GameController.Instance != null)
            {
                // Get heart position as destination
                if (GameController.Instance.Heart != null)
                {
                    Vector2Int heartPos = GameController.Instance.Heart.GridPosition;
                    List<MazeGrid.MazeNode> newPath = new List<MazeGrid.MazeNode>();

                    if (GameController.Instance.TryFindPath(targetTile, heartPos, newPath))
                    {
                        visitorController.SetPath(newPath);
                    }
                }
            }

            // Try for mistaking visitor
            var mistakingVisitorController = visitorObject.GetComponent<MistakingVisitorController>();
            if (mistakingVisitorController != null && GameController.Instance != null)
            {
                // Get heart position as destination
                if (GameController.Instance.Heart != null)
                {
                    Vector2Int heartPos = GameController.Instance.Heart.GridPosition;
                    List<MazeGrid.MazeNode> newPath = new List<MazeGrid.MazeNode>();

                    if (GameController.Instance.TryFindPath(targetTile, heartPos, newPath))
                    {
                        mistakingVisitorController.SetPath(newPath);
                    }
                }
            }

            // Play sound effect if available
            FaeMaze.Audio.SoundManager.Instance?.PlayLanternPlaced(); // Reuse lantern sound for now
        }

        /// <summary>
        /// Kills a visitor immediately.
        /// </summary>
        private void KillVisitor(GameObject visitorObject, Vector2Int atPos)
        {
            Debug.Log($"PukaHazard: Killed visitor at {atPos}");

            // Play death sound if available
            FaeMaze.Audio.SoundManager.Instance?.PlayVisitorConsumed(); // Reuse consumption sound

            // Destroy the visitor
            Destroy(visitorObject);

            // Track statistic (treat as consumed for now)
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.RecordVisitorConsumed();
            }
        }

        #endregion

        #region Visual

        private void SetupSpriteRenderer()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (useProceduralSprite)
            {
                CreateVisualSprite();
            }

            ApplySpriteSettings();
        }

        private void CreateVisualSprite()
        {
            // Create a simple circle sprite for the Puka
            spriteRenderer.sprite = CreateCircleSprite(32);
        }

        private void ApplySpriteSettings()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = pukaColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Only override scale when generating a procedural sprite
            if (useProceduralSprite)
            {
                baseScale = new Vector3(pukaSize, pukaSize, 1f);
                transform.localScale = baseScale;
            }
            else
            {
                baseScale = initialScale;
                transform.localScale = baseScale;
            }
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugDrawLinks || linkedWaterTiles == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Draw lines to linked water tiles
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green
            foreach (var waterTile in linkedWaterTiles)
            {
                Vector3 waterWorldPos = mazeGridBehaviour.GridToWorld(waterTile.x, waterTile.y);
                Gizmos.DrawLine(transform.position, waterWorldPos);

                // Draw small sphere at linked tile
                Gizmos.DrawWireSphere(waterWorldPos, 0.2f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawLinks || linkedWaterTiles == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
            foreach (var waterTile in linkedWaterTiles)
            {
                Vector3 waterWorldPos = mazeGridBehaviour.GridToWorld(waterTile.x, waterTile.y);
                Gizmos.DrawLine(transform.position, waterWorldPos);

                // Draw sphere at linked tile
                Gizmos.DrawSphere(waterWorldPos, 0.15f);
            }

            // Draw this Puka's position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Props;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Automatically places hazard props (FaeLantern, FairyRing, PukaHazard) on procedurally generated mazes.
    /// Scales hazard counts based on wave/level progression with configurable parameters.
    /// Enforces minimum distances from entrance/heart and prevents overlapping placements.
    ///
    /// USAGE:
    /// 1. Add this component to a GameObject in your procedural maze scene
    /// 2. Assign hazard prefabs to the config fields (faeLanternConfig.prefab, etc.)
    /// 3. Tune placement parameters (base counts, scaling factors, min/max limits)
    /// 4. Set autoPlaceOnStart = true for automatic placement, or call PlaceAllHazards() manually
    /// 5. Hazards will be placed after MazeGridBehaviour initializes
    ///
    /// FLOW:
    /// 1. Wait for MazeGridBehaviour to initialize (Start)
    /// 2. Query WaveSpawner for current wave number (defaults to 1 if not found)
    /// 3. Calculate hazard counts using base + (wave * scaling) capped at max
    /// 4. Find eligible spawn cells (walkable, proper distance from entrance/heart, not occupied)
    /// 5. Place hazards randomly across eligible cells
    /// 6. Initialize hazards (water-linking for Puka happens automatically in their Start)
    ///
    /// PLACEMENT RULES:
    /// - FaeLantern/FairyRing: Avoid water tiles, maintain min distance from entrance/heart
    /// - PukaHazard: Prefer cells near water (within 3 tiles), needs water proximity for linking
    /// - All hazards: Maintain minHazardSpacing between each other to prevent clustering
    /// - Occupied cells are tracked to prevent overlapping placements
    ///
    /// SCALING FORMULA:
    /// Count = baseCount + floor(waveNumber * scalingPerWave), capped at maxCount
    /// Example: base=2, scaling=0.3, wave 5 → 2 + floor(5*0.3) = 2+1 = 3 hazards
    /// </summary>
    [DefaultExecutionOrder(100)] // Execute after MazeGridBehaviour and other core systems
    public class HazardPlacementCoordinator : MonoBehaviour
    {
        #region Hazard Configuration

        [System.Serializable]
        public class HazardTypeConfig
        {
            [Tooltip("Prefab to instantiate for this hazard type")]
            public GameObject prefab;

            [Tooltip("Base count at wave 1")]
            [Min(0)]
            public int baseCount = 1;

            [Tooltip("Additional hazards per wave level")]
            [Min(0f)]
            public float scalingPerWave = 0.5f;

            [Tooltip("Maximum hazards of this type per maze (0 = unlimited)")]
            [Min(0)]
            public int maxCount = 10;

            [Tooltip("Minimum grid distance from entrance spawn points")]
            [Min(0)]
            public int minDistanceFromEntrance = 5;

            [Tooltip("Minimum grid distance from heart/exit")]
            [Min(0)]
            public int minDistanceFromHeart = 5;

            [Tooltip("For PukaHazard: prefer cells near water tiles")]
            public bool preferWaterProximity = false;

            [Tooltip("Enabled/disabled for placement")]
            public bool enabled = true;
        }

        #endregion

        #region Serialized Fields

        [Header("Hazard Prefabs")]
        [SerializeField]
        [Tooltip("Configuration for FaeLantern placement")]
        private HazardTypeConfig faeLanternConfig = new HazardTypeConfig
        {
            baseCount = 2,
            scalingPerWave = 0.3f,
            maxCount = 8,
            minDistanceFromEntrance = 6,
            minDistanceFromHeart = 6,
            enabled = true
        };

        [SerializeField]
        [Tooltip("Configuration for FairyRing placement")]
        private HazardTypeConfig fairyRingConfig = new HazardTypeConfig
        {
            baseCount = 1,
            scalingPerWave = 0.4f,
            maxCount = 6,
            minDistanceFromEntrance = 5,
            minDistanceFromHeart = 5,
            enabled = true
        };

        [SerializeField]
        [Tooltip("Configuration for PukaHazard placement")]
        private HazardTypeConfig pukaHazardConfig = new HazardTypeConfig
        {
            baseCount = 1,
            scalingPerWave = 0.2f,
            maxCount = 4,
            minDistanceFromEntrance = 7,
            minDistanceFromHeart = 7,
            preferWaterProximity = true,
            enabled = true
        };

        [Header("Global Settings")]
        [SerializeField]
        [Tooltip("Automatically place hazards on Start()")]
        private bool autoPlaceOnStart = true;

        [SerializeField]
        [Tooltip("Minimum grid spacing between any two hazards (prevents clustering)")]
        [Min(0)]
        private int minHazardSpacing = 2;

        [SerializeField]
        [Tooltip("Random seed for placement (0 = random each time)")]
        private int randomSeed = 0;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Log placement details to console")]
        private bool debugLogging = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private WaveSpawner waveSpawner;
        private HashSet<Vector2Int> occupiedCells;
        private List<GameObject> placedHazards;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Find required references
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            waveSpawner = FindFirstObjectByType<WaveSpawner>();

            // Guard: MazeGridBehaviour is required
            if (mazeGridBehaviour == null)
            {
                Debug.LogError("[HazardPlacement] MazeGridBehaviour not found! Cannot place hazards.");
                return;
            }

            // Guard: Grid must be initialized
            if (mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("[HazardPlacement] MazeGrid is null! Cannot place hazards.");
                return;
            }

            // Initialize tracking
            occupiedCells = new HashSet<Vector2Int>();
            placedHazards = new List<GameObject>();

            // Auto-place if enabled
            if (autoPlaceOnStart)
            {
                PlaceAllHazards();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Places all hazards based on current wave number and configuration.
        /// Can be called manually if autoPlaceOnStart is disabled.
        /// </summary>
        public void PlaceAllHazards()
        {
            // Guard: Validate required references
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                Debug.LogError("[HazardPlacement] Cannot place hazards: MazeGridBehaviour or Grid is null.");
                return;
            }

            // Initialize random seed
            if (randomSeed != 0)
            {
                Random.InitState(randomSeed);
            }

            // Clear previous placements
            ClearAllHazards();

            // Get current wave number (default to 1 if no WaveSpawner)
            int currentWave = waveSpawner != null ? waveSpawner.CurrentWaveNumber : 1;
            if (currentWave <= 0) currentWave = 1;

            if (debugLogging)
            {
                Debug.Log($"[HazardPlacement] Placing hazards for wave {currentWave}");
            }

            // Place each hazard type
            if (faeLanternConfig.enabled && faeLanternConfig.prefab != null)
            {
                PlaceHazardType("FaeLantern", faeLanternConfig, currentWave);
            }

            if (fairyRingConfig.enabled && fairyRingConfig.prefab != null)
            {
                PlaceHazardType("FairyRing", fairyRingConfig, currentWave);
            }

            if (pukaHazardConfig.enabled && pukaHazardConfig.prefab != null)
            {
                PlaceHazardType("PukaHazard", pukaHazardConfig, currentWave);
            }

            if (debugLogging)
            {
                Debug.Log($"[HazardPlacement] Placed {placedHazards.Count} total hazards");
            }
        }

        /// <summary>
        /// Removes all placed hazards.
        /// </summary>
        public void ClearAllHazards()
        {
            if (placedHazards != null)
            {
                foreach (var hazard in placedHazards)
                {
                    if (hazard != null)
                    {
                        Destroy(hazard);
                    }
                }
                placedHazards.Clear();
            }

            if (occupiedCells != null)
            {
                occupiedCells.Clear();
            }
        }

        #endregion

        #region Placement Logic

        /// <summary>
        /// Places hazards of a specific type based on configuration and wave number.
        /// </summary>
        private void PlaceHazardType(string typeName, HazardTypeConfig config, int waveNumber)
        {
            // Calculate count for this wave: base + (wave * scaling), capped at max
            int targetCount = CalculateHazardCount(config, waveNumber);

            if (targetCount <= 0)
            {
                return;
            }

            // Get eligible spawn cells for this hazard type
            List<Vector2Int> eligibleCells = GetEligibleSpawnCells(config);

            if (eligibleCells.Count == 0)
            {
                if (debugLogging)
                {
                    Debug.LogWarning($"[HazardPlacement] No eligible cells for {typeName}");
                }
                return;
            }

            // Shuffle eligible cells for random placement
            ShuffleList(eligibleCells);

            // Place hazards up to target count
            int placed = 0;
            foreach (var cell in eligibleCells)
            {
                if (placed >= targetCount)
                {
                    break;
                }

                // Double-check cell isn't occupied (shouldn't happen, but safety check)
                if (occupiedCells.Contains(cell))
                {
                    continue;
                }

                // Instantiate hazard at cell
                Vector3 worldPos = mazeGridBehaviour.GridToWorld(cell.x, cell.y);
                GameObject hazard = Instantiate(config.prefab, worldPos, Quaternion.identity, transform);
                hazard.name = $"{typeName}_{waveNumber}_{placed + 1}";

                // Track placement
                placedHazards.Add(hazard);
                occupiedCells.Add(cell);
                placed++;

                if (debugLogging)
                {
                    Debug.Log($"[HazardPlacement] Placed {typeName} at grid {cell} (world {worldPos})");
                }
            }

            if (debugLogging)
            {
                Debug.Log($"[HazardPlacement] {typeName}: placed {placed}/{targetCount} (eligible cells: {eligibleCells.Count})");
            }
        }

        /// <summary>
        /// Calculates the number of hazards to place based on wave number and config.
        /// Formula: base + floor(wave * scaling), capped at maxCount
        /// </summary>
        private int CalculateHazardCount(HazardTypeConfig config, int waveNumber)
        {
            int count = config.baseCount + Mathf.FloorToInt(waveNumber * config.scalingPerWave);

            if (config.maxCount > 0)
            {
                count = Mathf.Min(count, config.maxCount);
            }

            return count;
        }

        /// <summary>
        /// Gets list of eligible spawn cells based on hazard configuration.
        /// Filters by walkability, distance from entrance/heart, and existing hazard spacing.
        /// </summary>
        private List<Vector2Int> GetEligibleSpawnCells(HazardTypeConfig config)
        {
            List<Vector2Int> eligible = new List<Vector2Int>();
            MazeGrid grid = mazeGridBehaviour.Grid;

            // Get entrance and heart positions
            Vector2Int entrance = mazeGridBehaviour.EntranceGridPos;
            Vector2Int heart = mazeGridBehaviour.HeartGridPos;

            // Also check all spawn points (for multi-entrance mazes)
            List<Vector2Int> allSpawnPoints = GetAllSpawnPoints();

            // Scan entire grid
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    MazeGrid.MazeNode node = grid.GetNode(x, y);

                    // Must be walkable
                    if (node == null || !node.walkable)
                    {
                        continue;
                    }

                    // Check if cell is already occupied
                    if (occupiedCells.Contains(cell))
                    {
                        continue;
                    }

                    // Check minimum distance from entrance
                    int distToEntrance = ManhattanDistance(cell, entrance);
                    if (distToEntrance < config.minDistanceFromEntrance)
                    {
                        continue;
                    }

                    // Check minimum distance from all spawn points
                    bool tooCloseToSpawn = false;
                    foreach (var spawn in allSpawnPoints)
                    {
                        if (ManhattanDistance(cell, spawn) < config.minDistanceFromEntrance)
                        {
                            tooCloseToSpawn = true;
                            break;
                        }
                    }
                    if (tooCloseToSpawn)
                    {
                        continue;
                    }

                    // Check minimum distance from heart
                    int distToHeart = ManhattanDistance(cell, heart);
                    if (distToHeart < config.minDistanceFromHeart)
                    {
                        continue;
                    }

                    // Check minimum spacing from other hazards
                    if (minHazardSpacing > 0 && !CheckHazardSpacing(cell, minHazardSpacing))
                    {
                        continue;
                    }

                    // Special handling for water proximity (PukaHazard)
                    if (config.preferWaterProximity)
                    {
                        // Only include cells that have water tiles nearby
                        if (!HasNearbyWaterTiles(cell, 3))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // For non-Puka hazards, avoid placing on water
                        if (node.terrain == TileType.Water)
                        {
                            continue;
                        }
                    }

                    // Cell passes all filters
                    eligible.Add(cell);
                }
            }

            return eligible;
        }

        /// <summary>
        /// Gets all spawn points from the maze (for multi-entrance support).
        /// </summary>
        private List<Vector2Int> GetAllSpawnPoints()
        {
            List<Vector2Int> spawns = new List<Vector2Int>();

            // Add main entrance
            spawns.Add(mazeGridBehaviour.EntranceGridPos);

            // Add all lettered spawn points (A, B, C, D, etc.)
            char[] spawnIds = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
            foreach (char id in spawnIds)
            {
                if (mazeGridBehaviour.TryGetSpawnPoint(id, out Vector2Int pos))
                {
                    if (!spawns.Contains(pos))
                    {
                        spawns.Add(pos);
                    }
                }
            }

            return spawns;
        }

        /// <summary>
        /// Checks if a cell maintains minimum spacing from all existing hazards.
        /// </summary>
        private bool CheckHazardSpacing(Vector2Int cell, int minSpacing)
        {
            foreach (var occupied in occupiedCells)
            {
                if (ManhattanDistance(cell, occupied) < minSpacing)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if a cell has water tiles within the specified radius.
        /// Used for PukaHazard placement which needs water proximity.
        /// </summary>
        private bool HasNearbyWaterTiles(Vector2Int cell, int radius)
        {
            MazeGrid grid = mazeGridBehaviour.Grid;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int checkX = cell.x + dx;
                    int checkY = cell.y + dy;

                    var node = grid.GetNode(checkX, checkY);
                    if (node != null && node.terrain == TileType.Water)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Calculates Manhattan distance between two grid positions.
        /// </summary>
        private int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

        #region Cleanup

        private void OnDestroy()
        {
            ClearAllHazards();
        }

        #endregion
    }
}

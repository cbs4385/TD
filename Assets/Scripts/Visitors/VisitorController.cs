using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Controls a visitor's movement through the maze.
    /// Visitors follow a path of grid nodes.
    /// When using spawn markers: visitors escape at the destination (no essence).
    /// When using legacy heart: visitors are consumed at the heart (awards essence).
    /// </summary>
    public class VisitorController : MonoBehaviour
    {
        #region Enums

        public enum VisitorState
        {
            Idle,
            Walking,
            Consumed,
            Escaping
        }

        /// <summary>
        /// Represents a visited tile in the fascinated random walk with its unexplored neighbors.
        /// </summary>
        private class FascinatedPathNode
        {
            public Vector2Int Position { get; set; }
            public List<Vector2Int> UnexploredNeighbors { get; set; }

            public FascinatedPathNode(Vector2Int position, List<Vector2Int> unexploredNeighbors)
            {
                Position = position;
                UnexploredNeighbors = new List<Vector2Int>(unexploredNeighbors);
            }

            public bool HasUnexploredNeighbors => UnexploredNeighbors.Count > 0;

            public Vector2Int PopNextNeighbor()
            {
                if (UnexploredNeighbors.Count == 0)
                    throw new System.InvalidOperationException("No unexplored neighbors to pop");

                Vector2Int next = UnexploredNeighbors[0];
                UnexploredNeighbors.RemoveAt(0);
                return next;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed in units per second")]
        private float moveSpeed = 3f;

        [Header("Path Following")]
        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [Header("Confusion Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) for visitor to get confused at intersections")]
        [Range(0f, 1f)]
        private float confusionChance = 0.25f;

        [SerializeField]
        [Tooltip("Whether confusion is enabled")]
        private bool confusionEnabled = true;

        [SerializeField]
        [Tooltip("Minimum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int minConfusionDistance = 15;

        [SerializeField]
        [Tooltip("Maximum number of tiles to travel on confused detour path")]
        [Range(5, 50)]
        private int maxConfusionDistance = 20;

        [SerializeField]
        [Tooltip("Draw confusion segments in the scene view for debugging")]
        private bool debugConfusionGizmos;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the visitor sprite")]
        private Color visitorColor = new Color(0.3f, 0.6f, 1f, 1f); // Light blue

        [SerializeField]
        [Tooltip("Desired world-space diameter (in Unity units) for procedural visitors")]
        private float visitorSize = 30.0f;

        [SerializeField]
        [Tooltip("Pixels per unit for procedural visitor sprites (match imported visitor assets)")]
        private int proceduralPixelsPerUnit = 32;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations")]
        private bool useProceduralSprite = false;

        #endregion

        #region Private Fields

        private List<Vector2Int> path;
        private int currentPathIndex;
        private VisitorState state;
        private GameController gameController;
        private MazeGridBehaviour mazeGridBehaviour;
        private bool isEntranced;
        private float speedMultiplier = 1f;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Vector2 authoredSpriteWorldSize;
        private Vector2Int originalDestination; // Store original destination for confusion recovery

        private bool isConfused;
        private bool confusionSegmentActive;
        private int confusionSegmentEndIndex;
        private int confusionStepsTarget;
        private int confusionStepsTaken;
        private int waypointsTraversedSinceSpawn; // Track progress before allowing confusion

        private bool isCalculatingPath;

        // Fascination state (for FaeLantern)
        private bool isFascinated;
        private Vector2Int fascinationLanternPosition;
        private bool hasReachedLantern;
        private float fascinationTimer;
        private FaeMaze.Props.FaeLantern currentFaeLantern;

        // Cooldown tracking per lantern (prevents immediate re-triggering)
        private Dictionary<FaeMaze.Props.FaeLantern, float> lanternCooldowns;

        private Vector3 initialScale;

        // Track last 10 tiles reached to prevent short-term backtracking
        private Queue<Vector2Int> recentlyReachedTiles;
        private const int MAX_RECENT_TILES = 10;

        // Track visited tiles during fascinated random walk as a tree structure
        // Each node contains its position and list of unexplored neighbors
        private List<FascinatedPathNode> fascinatedPathNodes;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the visitor</summary>
        public VisitorState State => state;

        /// <summary>Gets the current move speed</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>Gets whether this visitor is entranced by a Fairy Ring</summary>
        public bool IsEntranced => isEntranced;

        /// <summary>Gets or sets the speed multiplier applied to movement</summary>
        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set => speedMultiplier = Mathf.Clamp(value, 0.1f, 2f);
        }

        /// <summary>Gets whether this visitor is fascinated by a FaeLantern</summary>
        public bool IsFascinated => isFascinated;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = VisitorState.Idle;
            isConfused = confusionEnabled;
            recentlyReachedTiles = new Queue<Vector2Int>();
            fascinatedPathNodes = new List<FascinatedPathNode>();
            lanternCooldowns = new Dictionary<FaeMaze.Props.FaeLantern, float>();
            initialScale = transform.localScale;
            spriteRenderer = GetComponent<SpriteRenderer>();
            CacheAuthoredSpriteSize();
            SetupSpriteRenderer();
            SetupPhysics();
        }

        private void CreateVisualSprite()
        {
            // Create a simple circle sprite for the visitor
            spriteRenderer.sprite = CreateCircleSprite(32);
            ApplySpriteSettings();
        }

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
            else
            {
                ApplySpriteSettings();
            }
        }

        private void ApplySpriteSettings()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = visitorColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Only override scale when generating a procedural sprite
            if (useProceduralSprite)
            {
                float baseSpriteSize = spriteRenderer.sprite != null
                    ? Mathf.Max(spriteRenderer.sprite.bounds.size.x, spriteRenderer.sprite.bounds.size.y)
                    : 1f;

                if (baseSpriteSize <= 0f)
                {
                    baseSpriteSize = 1f;
                }

                float targetWorldSize = authoredSpriteWorldSize != Vector2.zero
                    ? Mathf.Max(authoredSpriteWorldSize.x, authoredSpriteWorldSize.y)
                    : visitorSize;

                float scale = targetWorldSize / baseSpriteSize;
                transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                transform.localScale = initialScale;
            }
        }

        private void CacheAuthoredSpriteSize()
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                authoredSpriteWorldSize = Vector2.zero;
                return;
            }

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            authoredSpriteWorldSize = new Vector2(
                spriteSize.x * transform.localScale.x,
                spriteSize.y * transform.localScale.y
            );
        }

        private void SetupPhysics()
        {
            // Add Rigidbody2D for trigger collisions with MazeAttractors
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic so we control movement manually
            rb.gravityScale = 0f; // No gravity for 2D top-down

            // Add CircleCollider2D for trigger detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.radius = 0.3f; // Small radius for visitor collision
            collider.isTrigger = true; // Enable trigger events
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
                proceduralPixelsPerUnit
            );
        }

        private void Update()
        {
            // Update lantern cooldowns
            if (lanternCooldowns != null && lanternCooldowns.Count > 0)
            {
                // Create a list of lanterns to update (avoid modifying during iteration)
                List<FaeMaze.Props.FaeLantern> lanternsToUpdate = new List<FaeMaze.Props.FaeLantern>(lanternCooldowns.Keys);
                foreach (var lantern in lanternsToUpdate)
                {
                    if (lantern != null)
                    {
                        lanternCooldowns[lantern] -= Time.deltaTime;
                        if (lanternCooldowns[lantern] <= 0f)
                        {
                            lanternCooldowns.Remove(lantern);
                        }
                    }
                }
            }

            // Check for FaeLantern influence (grid-based detection)
            // Allow checking even when fascinated to handle multiple lanterns
            if (state == VisitorState.Walking)
            {
                // Don't check if currently paused at a lantern
                bool pausedAtLantern = isFascinated && hasReachedLantern && fascinationTimer > 0;
                if (!pausedAtLantern)
                {
                    CheckFaeLanternInfluence();
                }
            }

            // Handle fascination timer (2-second pause at lantern)
            if (isFascinated && hasReachedLantern && fascinationTimer > 0)
            {
                fascinationTimer -= Time.deltaTime;
                if (fascinationTimer <= 0)
                {
                    // Timer expired - start random wandering
                    Debug.Log($"[{gameObject.name}] FASCINATION TIMER EXPIRED | starting random wander");
                    // The random walk will be handled in HandleConfusionAtWaypoint
                }
                return; // Don't move while fascinated timer is active
            }

            if (state == VisitorState.Walking)
            {
                if (!isCalculatingPath)
                {
                    UpdateWalking();
                }
            }
            else if (state == VisitorState.Escaping)
            {
                // Optionally: add escape animation/effects here
                // Currently handled in OnPathCompleted with fade and delayed destroy
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the visitor with a reference to the game controller.
        /// </summary>
        /// <param name="controller">The game controller instance</param>
        public void Initialize(GameController controller)
        {
            gameController = controller;

            if (gameController != null && gameController.MazeGrid != null)
            {
                // Find the MazeGridBehaviour in the scene
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

                if (mazeGridBehaviour == null)
                {
                    Debug.LogError("VisitorController: Could not find MazeGridBehaviour in scene!");
                }
            }
            else
            {
                Debug.LogWarning("VisitorController: GameController or MazeGrid is null during initialization!");
            }
        }

        /// <summary>
        /// Initializes the visitor using the static GameController instance.
        /// </summary>
        public void Initialize()
        {
            Initialize(GameController.Instance);
        }

        #endregion

        #region Path Management

        /// <summary>
        /// Sets the path for the visitor to follow and begins walking.
        /// </summary>
        /// <param name="gridPath">List of grid coordinates forming the path</param>
        public void SetPath(List<Vector2Int> gridPath)
        {
            if (gridPath == null || gridPath.Count == 0)
            {
                Debug.LogError("VisitorController: Cannot set null or empty path!");
                return;
            }

            originalDestination = gridPath[gridPath.Count - 1];
            waypointsTraversedSinceSpawn = 0; // Reset waypoint counter
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;

            RecalculatePath();
        }

        /// <summary>
        /// Sets the path using MazeNode objects.
        /// </summary>
        /// <param name="nodePath">List of MazeNode objects forming the path</param>
        public void SetPath(List<MazeGrid.MazeNode> nodePath)
        {
            if (nodePath == null || nodePath.Count == 0)
            {
                Debug.LogError("VisitorController: Cannot set null or empty path!");
                return;
            }

            List<Vector2Int> gridPath = new List<Vector2Int>();
            foreach (var node in nodePath)
            {
                gridPath.Add(new Vector2Int(node.x, node.y));
            }

            if (gridPath.Count > 0)
            {
                originalDestination = gridPath[gridPath.Count - 1];
            }

            waypointsTraversedSinceSpawn = 0; // Reset waypoint counter
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            isConfused = confusionEnabled;

            RecalculatePath();
        }

        #endregion

        #region Movement

        private void UpdateWalking()
        {
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("VisitorController: No path set but state is Walking!");
                state = VisitorState.Idle;
                return;
            }

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("VisitorController: MazeGridBehaviour is null, cannot convert grid to world!");
                return;
            }

            // Bounds check for currentPathIndex
            if (currentPathIndex >= path.Count)
            {
                // Path index out of bounds - this can happen if fascinated random walk failed to extend path
                Debug.LogWarning($"[{gameObject.name}] PATH INDEX OUT OF BOUNDS | index={currentPathIndex} | pathCount={path.Count} | clamping to last waypoint");
                currentPathIndex = path.Count - 1;

                // If fascinated, clear fascination state since we can't continue
                if (isFascinated && hasReachedLantern)
                {
                    Debug.Log($"[{gameObject.name}] CLEARING FASCINATION | random walk failed");
                    isFascinated = false;
                    hasReachedLantern = false;
                    currentFaeLantern = null;
                    fascinatedPathNodes.Clear();
                }
            }

            // Get current target waypoint
            Vector2Int targetGridPos = path[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            // Move toward target (apply speed multiplier)
            float effectiveSpeed = moveSpeed * speedMultiplier;
            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                effectiveSpeed * Time.deltaTime
            );

            // Use Rigidbody2D.MovePosition for proper trigger detection
            if (rb != null)
            {
                rb.MovePosition(newPosition);
                // Manually sync transforms with physics system to ensure trigger detection works
                // This is necessary when AutoSyncTransforms is disabled in Physics2D settings
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if we've reached the waypoint
            float distanceToTarget = Vector3.Distance(transform.position, targetWorldPos);
            if (distanceToTarget < waypointReachedDistance)
            {
                OnWaypointReached();
            }
        }

        private void OnWaypointReached()
        {
            Vector2Int currentWaypoint = path[currentPathIndex];

            // Add to recently reached tiles queue (maintain last 10)
            if (recentlyReachedTiles != null)
            {
                recentlyReachedTiles.Enqueue(currentWaypoint);

                // Remove oldest tiles if we exceed the maximum
                while (recentlyReachedTiles.Count > MAX_RECENT_TILES)
                {
                    recentlyReachedTiles.Dequeue();
                }
            }

            // Increment waypoint counter
            waypointsTraversedSinceSpawn++;

            // VALIDATION: Check for backtracking by detecting if this waypoint position
            // appears earlier in the path (which would indicate we're stepping backward)
            // Skip this check for fascinated visitors doing random walk - they intentionally backtrack from dead ends
            if (!(isFascinated && hasReachedLantern))
            {
                for (int i = 0; i < currentPathIndex; i++)
                {
                    if (path[i] == currentWaypoint)
                    {
                        Debug.LogWarning($"[{gameObject.name}] BACKTRACKING DETECTED! | waypoint={currentWaypoint} at index {currentPathIndex} was already visited at index {i} | This should never happen!");
                        break;
                    }
                }
            }

            Debug.Log($"[{gameObject.name}] WAYPOINT REACHED | pos={currentWaypoint} | wpIndex={currentPathIndex}/{path.Count} | wpCount={waypointsTraversedSinceSpawn} | fascinated={isFascinated} | confusionActive={confusionSegmentActive}");

            // Check if fascinated visitor reached the lantern
            if (isFascinated && !hasReachedLantern && currentPathIndex < path.Count)
            {
                if (currentWaypoint == fascinationLanternPosition)
                {
                    hasReachedLantern = true;

                    // Start fascination timer (2 seconds by default)
                    if (currentFaeLantern != null)
                    {
                        fascinationTimer = currentFaeLantern.FascinationDuration;
                    }
                    else
                    {
                        fascinationTimer = 2f; // Fallback
                    }

                    Debug.Log($"[{gameObject.name}] REACHED LANTERN | pos={fascinationLanternPosition} | starting {fascinationTimer}s fascination pause");

                    // Pause at lantern - don't modify path, let timer run
                    // After timer expires, will resume to original destination
                    return; // Don't increment or handle confusion
                }
                else
                {
                    // Fascinated visitor reached intermediate waypoint on path to lantern
                    // Advance to next waypoint
                    currentPathIndex++;
                    if (currentPathIndex >= path.Count)
                    {
                        Debug.LogWarning($"[{gameObject.name}] FASCINATED PATH ENDED | reached end without finding lantern at {fascinationLanternPosition}");
                        OnPathCompleted();
                    }
                    return; // Don't call RecalculatePath for fascinated visitors
                }
            }

            // For fascinated visitors doing random walk, handle confusion/path extension
            if (isFascinated && hasReachedLantern && fascinationTimer <= 0)
            {
                // Check if current waypoint is the original destination (exit or heart)
                if (currentWaypoint == originalDestination)
                {
                    Debug.Log($"[{gameObject.name}] FASCINATED VISITOR REACHED DESTINATION | pos={currentWaypoint} | despawning");
                    OnPathCompleted();
                    return;
                }

                // Check if fascinated visitor wandered onto ANY spawn point (exit)
                // This handles the case where there are multiple exits and the visitor
                // wanders onto a different exit than their original destination
                if (mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2)
                {
                    if (mazeGridBehaviour.IsSpawnPoint(currentWaypoint))
                    {
                        Debug.Log($"[{gameObject.name}] FASCINATED VISITOR REACHED EXIT | pos={currentWaypoint} | despawning");
                        ForceEscape();
                        return;
                    }
                }

                currentPathIndex++;
                // Don't reset currentPathIndex - HandleFascinatedRandomWalk handles the case
                // where currentPathIndex >= path.Count by using path[path.Count - 1] as current position
                HandleConfusionAtWaypoint();
                return; // Don't call RecalculatePath for fascinated random walk
            }

            RecalculatePath();
        }

        private void OnPathCompleted()
        {
            // With spawn marker system: visitors escape (no essence awarded)
            // With legacy heart system: visitors are consumed (essence awarded)

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;

            // Check if we're using the new spawn marker system
            bool isUsingSpawnMarkers = mazeGridBehaviour != null && mazeGridBehaviour.GetSpawnPointCount() >= 2;

            if (isUsingSpawnMarkers)
            {
                // ESCAPE: Visitor reached destination spawn point
                state = VisitorState.Escaping;

                // Visual feedback: fade to transparent
                if (spriteRenderer != null)
                {
                    Color escapingColor = visitorColor;
                    escapingColor.a = 0.3f; // Fade out
                    spriteRenderer.color = escapingColor;
                }

                // Log escape
                Debug.Log($"[{gameObject.name}] ESCAPED | reached destination spawn point");

                // Destroy visitor after short delay for visual effect
                Destroy(gameObject, 0.2f);
            }
            else
            {
                // LEGACY CONSUMED: Visitor reached the heart
                state = VisitorState.Consumed;

                // Notify the Heart that this visitor has arrived (awards essence)
                if (gameController != null && gameController.Heart != null)
                {
                    gameController.Heart.OnVisitorConsumed(this);
                }
                else
                {
                    Debug.LogWarning("VisitorController: Could not notify Heart - reference is null. Destroying self.");
                    Destroy(gameObject);
                }
            }
        }

        #endregion

        #region FaeLantern Detection

        /// <summary>
        /// Checks if the visitor has entered any FaeLantern's influence area.
        /// Uses grid-based detection to check if current grid position is within
        /// the flood-filled influence area of any active lantern.
        /// </summary>
        private void CheckFaeLanternInfluence()
        {
            if (mazeGridBehaviour == null)
                return;

            // Get current grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
                return;

            Vector2Int currentGridPos = new Vector2Int(x, y);

            // Check all active FaeLanterns
            foreach (var lantern in FaeMaze.Props.FaeLantern.All)
            {
                if (lantern == null)
                    continue;

                // Check if this cell is in the lantern's influence
                if (lantern.IsCellInInfluence(currentGridPos))
                {
                    Debug.Log($"[{gameObject.name}] ENTERED FAE INFLUENCE | lanternPos={lantern.GridPosition} | visitorPos={currentGridPos}");
                    EnterFaeInfluence(lantern);
                    break; // Only one lantern can capture a visitor
                }
            }
        }

        /// <summary>
        /// Called when a visitor enters a FaeLantern's influence area.
        /// Abandons current path and paths to the lantern.
        /// Applies cooldown and proc chance checks per spec.
        /// </summary>
        private void EnterFaeInfluence(FaeMaze.Props.FaeLantern lantern)
        {
            // If already fascinated by this same lantern, ignore
            if (isFascinated && currentFaeLantern == lantern && fascinationLanternPosition == lantern.GridPosition)
                return;

            // Check cooldown (prevents immediate re-triggering)
            if (lanternCooldowns.ContainsKey(lantern) && lanternCooldowns[lantern] > 0f)
            {
                Debug.Log($"[{gameObject.name}] LANTERN COOLDOWN ACTIVE | remaining={lanternCooldowns[lantern]:F2}s");
                return;
            }

            // Check proc chance (probability of fascination)
            float roll = Random.value;
            if (roll > lantern.ProcChance)
            {
                Debug.Log($"[{gameObject.name}] FASCINATION PROC FAILED | roll={roll:F3} > procChance={lantern.ProcChance}");
                // Set cooldown even on failed proc to prevent spam checks
                lanternCooldowns[lantern] = lantern.CooldownSec;
                return;
            }

            Debug.Log($"[{gameObject.name}] FASCINATION PROC SUCCESS | roll={roll:F3} <= procChance={lantern.ProcChance}");

            // Allow re-fascination by a different lantern
            isFascinated = true;
            currentFaeLantern = lantern;
            fascinationLanternPosition = lantern.GridPosition;
            hasReachedLantern = false;
            fascinationTimer = 0f; // Will be set when reaching lantern

            // Set cooldown for this lantern
            lanternCooldowns[lantern] = lantern.CooldownSec;

            // Clear path nodes from previous fascination
            fascinatedPathNodes.Clear();

            Debug.Log($"[{gameObject.name}] FAE INFLUENCE CAPTURED | lanternPos={fascinationLanternPosition} | cooldown={lantern.CooldownSec}s");

            // Abandon current path
            path = null;
            currentPathIndex = 0;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;

            // Calculate path to lantern
            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                if (gameController.TryFindPath(currentPos, fascinationLanternPosition, pathToLantern) && pathToLantern.Count > 0)
                {
                    // Convert to Vector2Int path
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    // Find starting waypoint
                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        // Try to find current grid position in the path
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (path[i] == currentPos)
                            {
                                currentPathIndex = i;
                                break;
                            }
                        }

                        // If very close to current tile center, advance to next
                        Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                        float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                        if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                        {
                            currentPathIndex++;
                        }
                    }

                    Debug.Log($"[{gameObject.name}] FAE PATH SET | pathLength={path.Count - currentPathIndex} waypoints | startIndex={currentPathIndex}");
                    state = VisitorState.Walking;
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] FAE PATHFIND FAILED | no path from {currentPos} to {fascinationLanternPosition}");
                }
            }
        }

        #endregion

        #region Confusion System

        /// <summary>
        /// Gets all tiles that have been traversed so far (from spawn to current position).
        /// Used to prevent backtracking when building confusion paths or random walks.
        /// </summary>
        private HashSet<Vector2Int> GetTraversedTiles()
        {
            HashSet<Vector2Int> traversed = new HashSet<Vector2Int>();

            if (path == null || path.Count == 0)
            {
                return traversed;
            }

            // Add all tiles from start up to and including current index
            for (int i = 0; i <= currentPathIndex && i < path.Count; i++)
            {
                traversed.Add(path[i]);
            }

            return traversed;
        }

        /// <summary>
        /// Attempts to trigger confusion at the current waypoint if it's an intersection.
        /// For fascinated visitors who have reached the lantern, implements random walk behavior.
        /// </summary>
        private void HandleConfusionAtWaypoint()
        {
            if (mazeGridBehaviour == null || gameController == null)
            {
                return;
            }

            // Handle fascinated visitors who have reached the lantern and timer expired
            if (isFascinated && hasReachedLantern && fascinationTimer <= 0)
            {
                Debug.Log($"[{gameObject.name}] FASCINATED RANDOM WALK | wpIndex={currentPathIndex}/{path.Count}");
                HandleFascinatedRandomWalk();
                return;
            }

            if (!confusionEnabled || currentPathIndex >= path.Count - 1)
            {
                Debug.Log($"[{gameObject.name}] CONFUSION SKIP | confusionEnabled={confusionEnabled} | nearPathEnd={currentPathIndex >= path.Count - 1}");
                return;
            }

            // Prevent confusion for the first 10 waypoints after spawning
            // This ensures visitors make meaningful progress on their A* path before getting confused
            if (waypointsTraversedSinceSpawn < 10)
            {
                Debug.Log($"[{gameObject.name}] CONFUSION SKIP | wpCount={waypointsTraversedSinceSpawn} < 10 (safety buffer)");
                return;
            }

            Vector2Int currentPos = path[currentPathIndex];

            if (confusionSegmentActive)
            {
                // End of segment reached? Allow recovery logic and new decisions afterward.
                if (currentPathIndex > confusionSegmentEndIndex)
                {
                    Debug.Log($"[{gameObject.name}] CONFUSION SEGMENT ENDED | endIndex={confusionSegmentEndIndex} | deciding recovery");
                    confusionSegmentActive = false;
                    DecideRecoveryFromConfusion();
                }
                else
                {
                    Debug.Log($"[{gameObject.name}] IN CONFUSION SEGMENT | {currentPathIndex}/{confusionSegmentEndIndex}");
                    return; // Still traversing a confusion segment; no new detours.
                }
            }

            if (!isConfused)
            {
                Debug.Log($"[{gameObject.name}] NOT CONFUSED | navigating normally");
                return; // Currently navigating normally.
            }

            Vector2Int nextPos = path[currentPathIndex + 1];

            // Check if current position is an intersection (2+ walkable neighbors excluding the tile we arrived from)
            List<Vector2Int> walkableNeighbors = GetWalkableNeighbors(currentPos);

            // Exclude the tile we just came from to find forward options
            if (currentPathIndex > 0)
            {
                walkableNeighbors.Remove(path[currentPathIndex - 1]);
            }

            if (walkableNeighbors.Count < 2)
            {
                Debug.Log($"[{gameObject.name}] NOT INTERSECTION | neighbors={walkableNeighbors.Count}");
                return; // Not an intersection
            }

            // Roll for confusion
            float roll = Random.value;
            if (roll > confusionChance)
            {
                Debug.Log($"[{gameObject.name}] CONFUSION ROLL FAILED | roll={roll:F3} > chance={confusionChance}");
                return; // No confusion this time
            }

            Debug.Log($"[{gameObject.name}] CONFUSION TRIGGERED | roll={roll:F3} <= chance={confusionChance} | intersection with {walkableNeighbors.Count} neighbors");

            // Get confused - pick a non-forward direction
            List<Vector2Int> detourDirections = new List<Vector2Int>();
            foreach (var neighbor in walkableNeighbors)
            {
                if (neighbor != nextPos) // Don't pick the intended next waypoint
                {
                    detourDirections.Add(neighbor);
                }
            }

            if (detourDirections.Count == 0)
            {
                return; // No detour options
            }

            // Pick random detour direction
            Vector2Int detourStart = detourDirections[Random.Range(0, detourDirections.Count)];

            BeginConfusionSegment(currentPos, detourStart);
        }

        /// <summary>
        /// Gets all walkable neighbor positions for a grid position.
        /// </summary>
        private List<Vector2Int> GetWalkableNeighbors(Vector2Int gridPos)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>();

            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
            {
                return neighbors;
            }

            // Check 4 cardinal directions
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // Up
                new Vector2Int(0, -1),  // Down
                new Vector2Int(1, 0),   // Right
                new Vector2Int(-1, 0)   // Left
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = gridPos + dir;
                var node = mazeGridBehaviour.Grid.GetNode(neighborPos.x, neighborPos.y);

                if (node != null && node.walkable)
                {
                    neighbors.Add(neighborPos);
                }
            }

            return neighbors;
        }

        private void BeginConfusionSegment(Vector2Int currentPos, Vector2Int detourStart)
        {
            int stepsTarget = Mathf.Clamp(Random.Range(minConfusionDistance, maxConfusionDistance + 1), minConfusionDistance, maxConfusionDistance);

            // Use recently reached tiles (last 10) to prevent short-term backtracking
            HashSet<Vector2Int> traversedTiles = new HashSet<Vector2Int>(recentlyReachedTiles ?? new Queue<Vector2Int>());

            List<Vector2Int> confusionPath = BuildConfusionPath(currentPos, detourStart, stepsTarget, traversedTiles);

            if (confusionPath.Count == 0)
            {
                return;
            }

            Vector2Int confusionEnd = confusionPath[confusionPath.Count - 1];
            List<MazeGrid.MazeNode> recoveryPathNodes = new List<MazeGrid.MazeNode>();

            if (!gameController.TryFindPath(confusionEnd, originalDestination, recoveryPathNodes) || recoveryPathNodes.Count == 0)
            {
                Debug.Log($"[{gameObject.name}] CONFUSION FAILED | no recovery path from {confusionEnd} to {originalDestination}");
                return;
            }

            List<Vector2Int> recoveryPath = new List<Vector2Int>();
            foreach (var node in recoveryPathNodes)
            {
                recoveryPath.Add(new Vector2Int(node.x, node.y));
            }

            // Build the combined path: current position + confusion detour + recovery path
            List<Vector2Int> newPath = new List<Vector2Int>();
            newPath.Add(currentPos);

            // Add all confusion path tiles (already validated to not backtrack)
            newPath.AddRange(confusionPath);

            // Create a set of all tiles in the new path so far to avoid duplicates
            HashSet<Vector2Int> tilesInNewPath = new HashSet<Vector2Int>(newPath);

            // Add recovery path tiles, but skip any that would cause backtracking
            // Start from index 0 or 1 depending on whether the first tile duplicates confusionEnd
            int recoveryStartIndex = (recoveryPath.Count > 0 && recoveryPath[0] == confusionEnd) ? 1 : 0;

            int skippedTiles = 0;
            for (int i = recoveryStartIndex; i < recoveryPath.Count; i++)
            {
                Vector2Int recoveryTile = recoveryPath[i];

                // Skip tiles that were already traversed before confusion OR are duplicates in the new path
                if (traversedTiles.Contains(recoveryTile) || tilesInNewPath.Contains(recoveryTile))
                {
                    skippedTiles++;
                    Debug.Log($"[{gameObject.name}] RECOVERY PATH SKIP | tile={recoveryTile} would cause backtracking or duplicate");
                    continue; // Skip this tile to prevent backtracking
                }

                newPath.Add(recoveryTile);
                tilesInNewPath.Add(recoveryTile);
            }

            if (skippedTiles > 0)
            {
                Debug.Log($"[{gameObject.name}] RECOVERY PATH FILTERED | skipped {skippedTiles} backtracking tiles");
            }

            path = newPath;
            currentPathIndex = 0;

            confusionSegmentActive = true;
            confusionSegmentEndIndex = confusionPath.Count;
            confusionStepsTarget = stepsTarget;
            confusionStepsTaken = 0;
            isConfused = true;

            Debug.Log($"[{gameObject.name}] CONFUSION STARTED | from={currentPos} to={detourStart} | targetSteps={stepsTarget} | actualSteps={confusionPath.Count} | newPathLength={newPath.Count} | confusionEndIndex={confusionSegmentEndIndex}");
        }

        private List<Vector2Int> BuildConfusionPath(Vector2Int currentPos, Vector2Int detourStart, int stepsTarget, HashSet<Vector2Int> traversedTiles)
        {
            List<Vector2Int> confusionPath = new List<Vector2Int>();

            Vector2Int previousPos = currentPos;
            Vector2Int nextPos = detourStart;
            Vector2Int forwardDir = detourStart - currentPos;

            int safetyLimit = 250;
            int iterations = 0;

            // Track tiles in the confusion path to avoid loops within the detour
            HashSet<Vector2Int> confusionPathSet = new HashSet<Vector2Int>();

            while (iterations < safetyLimit && confusionPath.Count < stepsTarget)
            {
                if (!IsWalkable(nextPos))
                {
                    break;
                }

                // Check if this tile was already traversed before confusion started
                if (traversedTiles.Contains(nextPos))
                {
                    Debug.Log($"[{gameObject.name}] CONFUSION PATH BACKTRACK BLOCKED | tile={nextPos} was already traversed");
                    break; // Would backtrack to an earlier position
                }

                // Check if we're creating a loop within this confusion path
                if (confusionPathSet.Contains(nextPos))
                {
                    Debug.Log($"[{gameObject.name}] CONFUSION PATH LOOP BLOCKED | tile={nextPos} already in confusion detour");
                    break;
                }

                confusionPath.Add(nextPos);
                confusionPathSet.Add(nextPos);
                confusionStepsTaken = confusionPath.Count;

                if (IsDeadEndVisible(nextPos, forwardDir))
                {
                    break;
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(nextPos);
                neighbors.Remove(previousPos); // Don't go immediately backward

                // Remove any neighbors that would cause backtracking to already-traversed tiles
                neighbors.RemoveAll(n => traversedTiles.Contains(n));

                // Also avoid creating loops within the confusion path itself
                neighbors.RemoveAll(n => confusionPathSet.Contains(n));

                if (neighbors.Count == 0)
                {
                    Debug.Log($"[{gameObject.name}] CONFUSION PATH ENDED | no forward neighbors available (all would backtrack)");
                    break; // Cannot continue without backtracking
                }

                Vector2Int preferredForward = nextPos + forwardDir;
                Vector2Int chosenNext = neighbors.Contains(preferredForward) ? preferredForward : neighbors[Random.Range(0, neighbors.Count)];

                forwardDir = chosenNext - nextPos;
                previousPos = nextPos;
                nextPos = chosenNext;
                iterations++;
            }

            return confusionPath;
        }

        private bool IsWalkable(Vector2Int position)
        {
            var node = mazeGridBehaviour.Grid?.GetNode(position.x, position.y);
            return node != null && node.walkable;
        }

        private bool IsDeadEndVisible(Vector2Int startPos, Vector2Int forwardDir)
        {
            if (forwardDir == Vector2Int.zero)
            {
                return false;
            }

            Vector2Int previous = startPos;
            Vector2Int current = startPos + forwardDir;

            while (true)
            {
                var node = mazeGridBehaviour.Grid?.GetNode(current.x, current.y);
                if (node == null || !node.walkable)
                {
                    return false; // Wall encountered before a dead end
                }

                List<Vector2Int> neighbors = GetWalkableNeighbors(current);
                neighbors.Remove(previous);

                if (neighbors.Count == 0)
                {
                    return true; // Only way out is back where we came from
                }

                if (neighbors.Count > 1)
                {
                    return false; // Branch or corner blocks line of sight
                }

                Vector2Int nextForward = neighbors[0] - current;
                if (nextForward != forwardDir)
                {
                    return false; // Would require turning a corner
                }

                previous = current;
                current += forwardDir;
            }
        }

        private void DecideRecoveryFromConfusion()
        {
            float roll = Random.value;
            bool recover = roll <= 0.5f;
            isConfused = !recover;
            Debug.Log($"[{gameObject.name}] CONFUSION RECOVERY ROLL | roll={roll:F3} | recover={recover} | isConfused={isConfused}");
        }

        /// <summary>
        /// Handles random walk behavior for fascinated visitors after they've reached the lantern.
        /// Uses a tree structure where each node tracks its unexplored neighbors.
        /// Backtracks through the node list when dead ends are encountered.
        /// </summary>
        private void HandleFascinatedRandomWalk()
        {
            // Only extend path if we're near the end (within 3 waypoints)
            int waypointsRemaining = path.Count - currentPathIndex;
            if (waypointsRemaining > 3)
            {
                return; // Still have enough waypoints ahead
            }

            // Get the actual current position (where visitor is now)
            Vector2Int currentPos;
            if (currentPathIndex == 0 && path.Count > 0)
            {
                currentPos = path[0];
            }
            else if (currentPathIndex > 0 && currentPathIndex < path.Count)
            {
                currentPos = path[currentPathIndex - 1];
            }
            else if (currentPathIndex >= path.Count && path.Count > 0)
            {
                currentPos = path[path.Count - 1];
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] FASCINATED WALK INVALID INDEX | currentPathIndex={currentPathIndex} | pathCount={path.Count}");
                return;
            }

            // Initialize path nodes on first call after reaching lantern
            if (fascinatedPathNodes.Count == 0)
            {
                // Get all walkable neighbors
                List<Vector2Int> neighbors = GetWalkableNeighbors(currentPos);

                // Exclude the tile we came from (the previous tile before reaching lantern)
                // This ensures visitor continues forward into maze rather than immediately backtracking
                if (currentPathIndex > 1)
                {
                    Vector2Int previousTile = path[currentPathIndex - 2];
                    neighbors.Remove(previousTile);
                    Debug.Log($"[{gameObject.name}] FASCINATED WALK INIT | excluding previousTile={previousTile} to continue forward");
                }

                // Shuffle remaining neighbors randomly
                ShuffleList(neighbors);

                // Create initial node at lantern position
                FascinatedPathNode initialNode = new FascinatedPathNode(currentPos, neighbors);
                fascinatedPathNodes.Add(initialNode);
                Debug.Log($"[{gameObject.name}] FASCINATED WALK INIT | pos={currentPos} | unexploredNeighbors={neighbors.Count}");
            }

            // Get current node (last in list = current position)
            FascinatedPathNode currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];

            // Check if current node has unexplored neighbors
            while (!currentNode.HasUnexploredNeighbors && fascinatedPathNodes.Count > 0)
            {
                // Dead end - backtrack by removing current node
                fascinatedPathNodes.RemoveAt(fascinatedPathNodes.Count - 1);
                Debug.Log($"[{gameObject.name}] FASCINATED DEAD END | pos={currentNode.Position} | backtracking | pathDepth={fascinatedPathNodes.Count}");

                if (fascinatedPathNodes.Count == 0)
                {
                    // Exhausted all paths - visitor has fully explored, trigger completion
                    Debug.Log($"[{gameObject.name}] FASCINATED WALK EXHAUSTED | explored all reachable tiles | triggering completion");
                    OnPathCompleted();
                    return;
                }

                // Move to parent node
                currentNode = fascinatedPathNodes[fascinatedPathNodes.Count - 1];
                Vector2Int backtrackPos = currentNode.Position;

                // Add backtrack waypoint if we're not already there
                if (backtrackPos != currentPos)
                {
                    path.Add(backtrackPos);
                    currentPos = backtrackPos;
                    Debug.Log($"[{gameObject.name}] FASCINATED BACKTRACK | to={backtrackPos} | pathDepth={fascinatedPathNodes.Count}");
                }
            }

            if (fascinatedPathNodes.Count == 0)
            {
                return; // Fully explored
            }

            // Current node has unexplored neighbors - pick the next one
            Vector2Int nextPos = currentNode.PopNextNeighbor();

            // Get neighbors of next position and shuffle them
            List<Vector2Int> nextNeighbors = GetWalkableNeighbors(nextPos);

            // Build set of visited positions for filtering
            HashSet<Vector2Int> visitedPositions = new HashSet<Vector2Int>();
            foreach (var node in fascinatedPathNodes)
            {
                visitedPositions.Add(node.Position);
            }

            // Remove neighbors that we've already visited
            nextNeighbors.RemoveAll(n => visitedPositions.Contains(n));
            ShuffleList(nextNeighbors);

            // Create new node for next position
            FascinatedPathNode nextNode = new FascinatedPathNode(nextPos, nextNeighbors);
            fascinatedPathNodes.Add(nextNode);

            // Add to movement path
            path.Add(nextPos);
            Debug.Log($"[{gameObject.name}] FASCINATED WALK | from={currentNode.Position} to={nextPos} | unexploredNeighbors={nextNeighbors.Count} | pathDepth={fascinatedPathNodes.Count}");

            // Check if visitor has reached their original destination (exit or heart)
            if (nextPos == originalDestination)
            {
                Debug.Log($"[{gameObject.name}] FASCINATED WALK REACHED DESTINATION | pos={nextPos} | triggering completion");
                // Note: OnPathCompleted will be called when visitor actually reaches this waypoint
                // For now, just log it - the normal waypoint completion logic will handle despawn
            }
        }

        /// <summary>
        /// Shuffles a list in place using Fisher-Yates algorithm.
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

        #region Public Methods

        /// <summary>
        /// Stops the visitor's movement.
        /// </summary>
        public void Stop()
        {
            state = VisitorState.Idle;
        }

        /// <summary>
        /// Resumes the visitor's movement if they have a path.
        /// </summary>
        public void Resume()
        {
            if (path != null && path.Count > 0 && currentPathIndex < path.Count)
            {
                state = VisitorState.Walking;
            }
        }

        /// <summary>
        /// Recalculates the path to the original destination.
        /// Used when new attractors (lanterns) are placed to update the visitor's route.
        /// </summary>
        public void RecalculatePath()
        {
            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Fascinated visitors use random walk, not A* recalculation
            if (isFascinated)
            {
                Debug.Log($"[{gameObject.name}] PATH RECALC SKIP | fascinated=true | using random walk behavior");
                return;
            }

            isCalculatingPath = true;
            state = VisitorState.Idle;

            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                isCalculatingPath = false;
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);
            List<MazeGrid.MazeNode> newPathNodes = new List<MazeGrid.MazeNode>();

            if (!gameController.TryFindPath(currentPos, originalDestination, newPathNodes) || newPathNodes.Count == 0)
            {
                isCalculatingPath = false;
                return;
            }

            List<Vector2Int> newPath = new List<Vector2Int>();
            foreach (var node in newPathNodes)
            {
                newPath.Add(new Vector2Int(node.x, node.y));
            }

            path = newPath;
            recentlyReachedTiles.Clear();
            if (path.Count > 0)
            {
                recentlyReachedTiles.Enqueue(path[0]);
            }

            currentPathIndex = path.Count > 1 ? 1 : 0;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;

            Debug.Log($"[{gameObject.name}] PATH RECALC SUCCESS | start={currentPos} | dest={originalDestination} | length={path.Count}");

            if (path.Count <= 1)
            {
                isCalculatingPath = false;
                OnPathCompleted();
                return;
            }

            state = VisitorState.Walking;
            isCalculatingPath = false;
        }

        /// <summary>
        /// Sets the entranced state of this visitor.
        /// Entranced visitors are affected by Fairy Rings.
        /// </summary>
        /// <param name="value">True to mark as entranced, false to clear</param>
        public void SetEntranced(bool value)
        {
            if (isEntranced != value)
            {
                isEntranced = value;
            }
        }

        /// <summary>
        /// Forces the visitor to escape immediately.
        /// Used when visitor needs to despawn without awarding essence.
        /// </summary>
        public void ForceEscape()
        {
            state = VisitorState.Escaping;

            // Clear fascination state
            isFascinated = false;
            hasReachedLantern = false;

            // Visual feedback
            if (spriteRenderer != null)
            {
                Color escapingColor = visitorColor;
                escapingColor.a = 0.3f;
                spriteRenderer.color = escapingColor;
            }

            Debug.Log($"[{gameObject.name}] FORCED ESCAPE | no essence awarded");
            Destroy(gameObject, 0.2f);
        }

        /// <summary>
        /// Makes this visitor fascinated by a FaeLantern.
        /// Fascinated visitors immediately path to the lantern, pause at it,
        /// then resume their journey to the original destination.
        /// </summary>
        /// <param name="lanternGridPosition">Grid position of the lantern</param>
        public void BecomeFascinated(Vector2Int lanternGridPosition)
        {
            if (state != VisitorState.Walking)
            {
                Debug.Log($"[{gameObject.name}] FASCINATION FAILED | state={state} (not Walking)");
                return; // Only walking visitors can be fascinated
            }

            isFascinated = true;
            fascinationLanternPosition = lanternGridPosition;
            hasReachedLantern = false;

            // Immediately discard current path and create new path to lantern
            path = null;
            currentPathIndex = 0;
            confusionSegmentActive = false;
            confusionSegmentEndIndex = 0;
            waypointsTraversedSinceSpawn = 0;

            // Get current position and pathfind to lantern
            if (gameController != null && mazeGridBehaviour != null &&
                mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Vector2Int currentPos = new Vector2Int(currentX, currentY);

                Debug.Log($"[{gameObject.name}] FASCINATION TRIGGERED | currentPos={currentPos} | lanternPos={lanternGridPosition} | calculating new path");

                // Find NEW path to lantern (ignores prior A* plan to original destination)
                List<MazeGrid.MazeNode> pathToLantern = new List<MazeGrid.MazeNode>();
                if (gameController.TryFindPath(currentPos, lanternGridPosition, pathToLantern) && pathToLantern.Count > 0)
                {
                    // Convert to Vector2Int path
                    path = new List<Vector2Int>();
                    foreach (var node in pathToLantern)
                    {
                        path.Add(new Vector2Int(node.x, node.y));
                    }

                    // Find starting waypoint without backtracking
                    currentPathIndex = 0;
                    if (path.Count > 1)
                    {
                        // First, try to find current grid position in the path
                        int currentGridIndex = -1;
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (path[i] == currentPos)
                            {
                                currentGridIndex = i;
                                break;
                            }
                        }

                        if (currentGridIndex >= 0)
                        {
                            // Found current tile in path - start from there
                            currentPathIndex = currentGridIndex;

                            // If very close to center, advance to next waypoint
                            Vector3 currentTileWorldPos = mazeGridBehaviour.GridToWorld(currentPos.x, currentPos.y);
                            float distToCurrentTile = Vector3.Distance(transform.position, currentTileWorldPos);
                            if (currentPathIndex < path.Count - 1 && distToCurrentTile < waypointReachedDistance)
                            {
                                currentPathIndex++;
                            }
                        }
                        else
                        {
                            // Current tile not in path - use closest waypoint with forward bias
                            Vector3 currentWorldPos = transform.position;
                            float closestDist = float.MaxValue;

                            for (int i = 0; i < path.Count; i++)
                            {
                                Vector3 waypointWorldPos = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                                float dist = Vector3.Distance(currentWorldPos, waypointWorldPos);

                                // Prefer waypoints ahead by biasing distance for waypoints at later indices
                                // This ensures we favor forward progress when distances are similar
                                float biasedDist = dist - (i * 0.01f);

                                if (biasedDist < closestDist)
                                {
                                    closestDist = biasedDist;
                                    currentPathIndex = i;
                                }
                            }

                            if (currentPathIndex < path.Count - 1 && closestDist < waypointReachedDistance)
                            {
                                currentPathIndex++;
                            }
                        }
                    }

                    Debug.Log($"[{gameObject.name}] FASCINATION PATH SET | pathLength={path.Count - currentPathIndex} waypoints | startIndex={currentPathIndex}");
                }
                else
                {
                    Debug.Log($"[{gameObject.name}] FASCINATION PATHFIND FAILED | no path from {currentPos} to {lanternGridPosition}");
                }
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw current path
            if (path != null && path.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = Color.cyan;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                    Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }

                if (debugConfusionGizmos && confusionSegmentEndIndex > 0)
                {
                    Gizmos.color = Color.magenta;
                    int lastConfusionIndex = Mathf.Min(confusionSegmentEndIndex, path.Count - 1);

                    for (int i = 0; i < lastConfusionIndex; i++)
                    {
                        Vector3 start = mazeGridBehaviour.GridToWorld(path[i].x, path[i].y);
                        Vector3 end = mazeGridBehaviour.GridToWorld(path[i + 1].x, path[i + 1].y);
                        Gizmos.DrawLine(start, end);
                    }
                }

                // Draw current target
                if (state == VisitorState.Walking && currentPathIndex < path.Count)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 target = mazeGridBehaviour.GridToWorld(path[currentPathIndex].x, path[currentPathIndex].y);
                    Gizmos.DrawWireSphere(target, 0.3f);
                }
            }
        }

        #endregion
    }
}

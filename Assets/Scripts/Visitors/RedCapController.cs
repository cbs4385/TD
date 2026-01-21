using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;
using ForestMaze;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Red Cap - A hostile actor that hunts visitors and drains essence.
    /// Moves faster than visitors, actively stalks them, and penalizes the player
    /// when catching one.
    /// Uses world-space navigation to pursue targets.
    /// </summary>
    public class RedCapController : MonoBehaviour
    {
        #region Enums

        public enum RedCapState
        {
            Idle,
            Hunting,
            Killing,
            Fleeing
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Movement speed multiplier relative to visitor speed (1.25 = 25% faster)")]
        private float speedMultiplier = 1.25f;

        [SerializeField]
        [Tooltip("Base movement speed in units per second")]
        private float baseMoveSpeed = 3f;

        [Header("Hunting Settings")]
        [SerializeField]
        [Tooltip("How often to update target selection (in seconds)")]
        private float targetUpdateInterval = 0.5f;

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [SerializeField]
        [Tooltip("Detection radius for visitor contact (collision)")]
        private float contactRadius = 0.3f;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Essence penalty multiplier when catching a visitor (2.0 = double the normal reward)")]
        private float essencePenaltyMultiplier = 2.0f;

        [SerializeField]
        [Tooltip("Base essence value per visitor (should match HeartOfTheMaze setting)")]
        private int baseEssencePerVisitor = 10;

        [Header("Killing Settings")]
        [SerializeField]
        [Tooltip("Duration of the killing animation in seconds")]
        private float killingDuration = 1.0f;

        [Header("Frightening Settings")]
        [SerializeField]
        [Tooltip("Radius within which visitors become frightened when they see the Red Cap")]
        private float frightenRadius = 5.0f;

        [SerializeField]
        [Tooltip("How often to check for visitors to frighten (in seconds)")]
        private float frightenCheckInterval = 0.25f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use procedural sprite if true, otherwise relies on child SpriteRenderer/Animator")]
        private bool useProceduralSprite = false;

        [SerializeField]
        [Tooltip("Color of the Red Cap (used for procedural sprite)")]
        private Color redCapColor = new Color(0.8f, 0.1f, 0.1f, 1f); // Dark red

        [SerializeField]
        [Tooltip("Size of the Red Cap sprite")]
        private float redCapSize = 1.2f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Animator parameter name for direction")]
        private string directionParameterName = "Direction";

        #endregion

        #region Private Fields

        private RedCapState state = RedCapState.Idle;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private List<Vector3> worldPath = new List<Vector3>();
        private int currentWaypointIndex;
        private VisitorControllerBase targetVisitor;
        private float targetUpdateTimer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private float moveSpeed;
        private bool initialized;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        // Base rotation from prefab (captured at initialization)
        private Quaternion baseRotation;
        private bool baseRotationCaptured = false;

        // Killing state tracking
        private float killingTimer;
        private VisitorControllerBase killingTarget;

        // Frightening tracking
        private float frightenCheckTimer;

        // Starting essence reference (for flee threshold)
        private int startingEssence;

        // Path recalculation tracking
        private float pathRecalculationTimer;
        private const float PATH_RECALCULATION_INTERVAL = 0.25f;
        private Vector3 lastTargetPosition;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Red Cap</summary>
        public RedCapState State => state;

        /// <summary>Gets the current target visitor</summary>
        public VisitorControllerBase TargetVisitor => targetVisitor;

        /// <summary>Gets the calculated move speed</summary>
        public float MoveSpeed => moveSpeed;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Calculate actual move speed
            moveSpeed = baseMoveSpeed * speedMultiplier;
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!AcquireDependencies())
            {
                return;
            }

            TryInitialize();

            // Check essence threshold - flee if below starting essence
            if (state != RedCapState.Fleeing && state != RedCapState.Killing)
            {
                if (gameController != null && gameController.CurrentEssence < startingEssence)
                {
                    StartFleeing();
                }
            }

            switch (state)
            {
                case RedCapState.Hunting:
                    UpdateTargetSelection();
                    FollowPath();
                    CheckForVisitorContact();
                    CheckForVisitorsToFrighten();
                    break;

                case RedCapState.Killing:
                    UpdateKilling();
                    break;

                case RedCapState.Fleeing:
                    FollowPath();
                    CheckForReachedExit();
                    CheckForVisitorsToFrighten();
                    break;
            }
        }

        private void TryInitialize()
        {
            if (initialized)
            {
                return;
            }

            // Find required components
            AcquireDependencies();
            // Look for Animator on this GameObject or children (for Blender imports)
            animator = GetComponentInChildren<Animator>();

            if (gameController == null || mazeGridBehaviour == null)
            {
                return;
            }

            // Create visual representation only if using procedural sprite
            if (useProceduralSprite)
            {
                CreateProceduralVisual();
            }
            else
            {
                // Get existing SpriteRenderer if not using procedural
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // Initialize animator direction
            if (animator != null)
            {
                // Capture the base rotation from the prefab before applying any directional rotation
                if (!baseRotationCaptured)
                {
                    baseRotation = animator.transform.localRotation;
                    baseRotationCaptured = true;
                }

                SetAnimatorDirection(IdleDirection);
            }

            // Capture starting essence for flee threshold
            startingEssence = GameSettings.StartingEssence;

            // Start hunting
            state = RedCapState.Hunting;
            initialized = true;
        }

        private bool AcquireDependencies()
        {
            bool ready = true;

            if (gameController == null)
            {
                gameController = GameController.Instance;
            }

            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (gameController == null || mazeGridBehaviour == null)
            {
                ready = false;
            }

            return ready;
        }

        #endregion

        #region Hunting Behavior

        /// <summary>
        /// Updates target selection at regular intervals.
        /// Finds the closest visitor or switches to a closer one.
        /// Also recalculates path if target has moved significantly.
        /// </summary>
        private void UpdateTargetSelection()
        {
            targetUpdateTimer -= Time.deltaTime;
            pathRecalculationTimer -= Time.deltaTime;

            bool needsTargetUpdate = targetUpdateTimer <= 0f;
            bool needsPathRecalculation = pathRecalculationTimer <= 0f;

            if (needsTargetUpdate)
            {
                targetUpdateTimer = targetUpdateInterval;

                // Find all visitors in the scene
                VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

                if (allVisitors.Length == 0)
                {
                    targetVisitor = null;
                    worldPath.Clear();
                    state = RedCapState.Idle;
                    return;
                }

                // Find closest visitor
                VisitorControllerBase closestVisitor = null;
                float closestDistance = float.MaxValue;

                foreach (var visitor in allVisitors)
                {
                    if (visitor == null || visitor.gameObject == null)
                        continue;

                    float distance = Vector3.Distance(transform.position, visitor.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestVisitor = visitor;
                    }
                }

                // Update target if we found a new one
                if (closestVisitor != targetVisitor)
                {
                    targetVisitor = closestVisitor;
                    RecalculatePathToTarget();
                    needsPathRecalculation = false; // Already recalculated
                }
            }

            // Recalculate path if target has moved significantly
            if (needsPathRecalculation && targetVisitor != null)
            {
                pathRecalculationTimer = PATH_RECALCULATION_INTERVAL;

                float targetMovement = Vector3.Distance(targetVisitor.transform.position, lastTargetPosition);
                if (targetMovement > 1.0f) // Target moved more than 1 unit
                {
                    RecalculatePathToTarget();
                }
            }
        }

        /// <summary>
        /// Recalculates the path to the current target visitor using world-space pathfinding.
        /// </summary>
        private void RecalculatePathToTarget()
        {
            if (targetVisitor == null || mazeGridBehaviour == null)
            {
                worldPath.Clear();
                return;
            }

            // Store target position for movement tracking
            lastTargetPosition = targetVisitor.transform.position;

            // Use tile-based world-space pathfinding (same as visitors)
            worldPath = BuildWorldPath(transform.position, targetVisitor.transform.position);
            currentWaypointIndex = 0;
        }

        /// <summary>
        /// Builds a world-space path from start to end using A* through walkable tiles.
        /// Uses actual tile positions from WorldSpaceMazeData to guarantee paths stay on walkable terrain.
        /// This is the same pathfinding approach used by visitors.
        /// </summary>
        private List<Vector3> BuildWorldPath(Vector3 start, Vector3 end)
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                // No pathfinding data available - return empty list (no fallback to direct path)
                return new List<Vector3>();
            }

            var mazeData = mazeGridBehaviour.WorldSpaceMazeData;
            var result = new List<Vector3>();

            Vector2 startPos2D = new Vector2(start.x, start.y);
            Vector2 endPos2D = new Vector2(end.x, end.y);

            // Find nearest walkable tile to start position
            var startTile = FindNearestWalkableTile(mazeData, startPos2D);
            if (startTile == null)
            {
                return new List<Vector3>();
            }

            // Find nearest walkable tile to end position
            var endTile = FindNearestWalkableTile(mazeData, endPos2D);
            if (endTile == null)
            {
                return new List<Vector3>();
            }

            // A* through walkable tiles
            var tilePath = FindTilePath(mazeData, startTile, endTile);

            if (tilePath == null || tilePath.Count == 0)
            {
                // No valid path found - return empty list
                return new List<Vector3>();
            }

            // Convert tile path to world positions
            // Add current position first if close to start tile
            float distToStartTile = Vector2.Distance(startPos2D, startTile.Position);
            if (distToStartTile < 1.5f)
            {
                result.Add(start);
            }

            // Add all tile positions
            foreach (var tile in tilePath)
            {
                result.Add(new Vector3(tile.Position.x, tile.Position.y, start.z));
            }

            // Add final destination if close to end tile
            float distFromLastTile = Vector2.Distance(endPos2D, endTile.Position);
            if (distFromLastTile > 0.1f && distFromLastTile < 1.5f)
            {
                result.Add(end);
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest walkable tile to a position.
        /// </summary>
        private WorldSpaceTile FindNearestWalkableTile(WorldSpaceMazeData mazeData, Vector2 position)
        {
            float searchRadius = 5f;
            float minDist = float.MaxValue;
            WorldSpaceTile nearest = null;

            // Expand search radius if needed
            for (int attempt = 0; attempt < 3 && nearest == null; attempt++)
            {
                var nearbyTiles = mazeData.GetTilesNear(position, searchRadius);
                foreach (var tile in nearbyTiles)
                {
                    if (!tile.Walkable) continue;

                    float dist = Vector2.Distance(position, tile.Position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = tile;
                    }
                }
                searchRadius *= 2f;
            }

            return nearest;
        }

        /// <summary>
        /// Finds a path through walkable tiles using A* algorithm.
        /// Uses Euclidean distance as heuristic to prefer geometrically shorter paths.
        /// </summary>
        private List<WorldSpaceTile> FindTilePath(WorldSpaceMazeData mazeData,
            WorldSpaceTile startTile, WorldSpaceTile endTile)
        {
            if (startTile == endTile)
            {
                return new List<WorldSpaceTile> { startTile };
            }

            // A* algorithm using distance-based costs
            var gScore = new Dictionary<WorldSpaceTile, float>();
            var fScore = new Dictionary<WorldSpaceTile, float>();
            var parent = new Dictionary<WorldSpaceTile, WorldSpaceTile>();
            var closedSet = new HashSet<WorldSpaceTile>();
            var openSet = new List<WorldSpaceTile>();

            gScore[startTile] = 0f;
            fScore[startTile] = Vector2.Distance(startTile.Position, endTile.Position);
            openSet.Add(startTile);
            parent[startTile] = null;

            // Tiles are placed at ~0.5 unit intervals along curves and 1.0 in nodes
            float neighborRadius = mazeData.TileSize * 1.42f;

            int iterations = 0;
            int maxIterations = 50000;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Find node with lowest fScore in open set
                WorldSpaceTile current = null;
                float lowestF = float.MaxValue;
                foreach (var tile in openSet)
                {
                    float f = fScore.TryGetValue(tile, out float fs) ? fs : float.MaxValue;
                    if (f < lowestF)
                    {
                        lowestF = f;
                        current = tile;
                    }
                }

                if (current == null) break;

                // Check if reached destination
                float distToEnd = Vector2.Distance(current.Position, endTile.Position);
                if (distToEnd < mazeData.TileSize * 0.5f || current == endTile)
                {
                    // Reconstruct path
                    var path = new List<WorldSpaceTile>();
                    var node = current;
                    while (node != null)
                    {
                        path.Add(node);
                        parent.TryGetValue(node, out node);
                    }
                    path.Reverse();
                    return path;
                }

                openSet.Remove(current);
                closedSet.Add(current);

                // Get neighboring walkable tiles
                var neighbors = mazeData.GetTilesNear(current.Position, neighborRadius);
                float currentG = gScore.TryGetValue(current, out float cg) ? cg : float.MaxValue;

                foreach (var neighbor in neighbors)
                {
                    if (!neighbor.Walkable) continue;
                    if (closedSet.Contains(neighbor)) continue;

                    // Must be within neighbor radius
                    float stepDist = Vector2.Distance(current.Position, neighbor.Position);
                    if (stepDist > neighborRadius) continue;

                    // Topology check: verify tiles are connected through graph structure
                    if (!mazeData.AreTilesConnected(current, neighbor))
                        continue;

                    // Calculate tentative gScore
                    float tentativeG = currentG + stepDist;
                    float neighborG = gScore.TryGetValue(neighbor, out float ng) ? ng : float.MaxValue;

                    if (tentativeG < neighborG)
                    {
                        parent[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Vector2.Distance(neighbor.Position, endTile.Position);

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            return null; // No path found
        }

        /// <summary>
        /// Follows the current path toward the target.
        /// Does NOT fall back to direct movement - stays on walkable tiles only.
        /// </summary>
        private void FollowPath()
        {
            if (worldPath.Count == 0 || currentWaypointIndex >= worldPath.Count)
            {
                // No path or reached end - recalculate
                RecalculatePathToTarget();

                // If we still don't have a path, wait (don't cut through walls)
                if (worldPath.Count == 0)
                {
                    return;
                }
            }

            // Get current waypoint in world space
            Vector3 waypointWorldPos = worldPath[currentWaypointIndex];

            // Move toward waypoint
            Vector3 direction = (waypointWorldPos - transform.position).normalized;
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // Update animation direction based on movement
            UpdateAnimationDirection(direction);

            // Check if reached waypoint
            float distanceToWaypoint = Vector3.Distance(transform.position, waypointWorldPos);
            if (distanceToWaypoint < waypointReachedDistance)
            {
                currentWaypointIndex++;

                // Recalculate path periodically to adjust for moving target
                if (currentWaypointIndex >= worldPath.Count)
                {
                    RecalculatePathToTarget();
                }
            }
        }

        /// <summary>
        /// Updates the animation direction based on movement vector.
        /// </summary>
        private void UpdateAnimationDirection(Vector3 movement)
        {
            if (animator == null)
                return;

            // Calculate direction from movement (similar to VisitorController)
            int direction = GetDirectionFromMovement(new Vector2(movement.x, movement.y));
            SetAnimatorDirection(direction);
        }

        /// <summary>
        /// Gets the direction enum from a movement vector.
        /// </summary>
        private int GetDirectionFromMovement(Vector2 movement)
        {
            float movementThreshold = 0.01f;

            if (movement.sqrMagnitude <= movementThreshold * movementThreshold)
            {
                return lastDirection; // Retain last direction when not moving much
            }

            float absX = Mathf.Abs(movement.x);
            float absY = Mathf.Abs(movement.y);

            // Determine dominant axis
            int newDirection;
            if (absY >= absX)
            {
                // Vertical movement dominant
                newDirection = movement.y > 0f ? 1 : 2; // 1 = Up, 2 = Down
            }
            else
            {
                // Horizontal movement dominant
                newDirection = movement.x < 0f ? 3 : 4; // 3 = Left, 4 = Right
            }

            if (newDirection != IdleDirection)
            {
                lastDirection = newDirection;
            }

            return newDirection;
        }

        /// <summary>
        /// Sets the animator direction parameter and rotates model to face direction of motion.
        /// </summary>
        private void SetAnimatorDirection(int direction)
        {
            // Guard against redundant animator parameter writes
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }

            // Rotate the model to face the direction of motion
            if (!useProceduralSprite && animator != null && baseRotationCaptured)
            {
                // Determine which direction to use for rotation
                int rotationDirection = direction;
                if (rotationDirection == IdleDirection && lastDirection != IdleDirection)
                {
                    rotationDirection = lastDirection;
                }
                // Default to facing down if never moved
                if (rotationDirection == IdleDirection)
                {
                    rotationDirection = 2; // Down
                }

                // Calculate Z-axis rotation based on movement direction
                // This rotation is applied on top of the base rotation from the prefab
                float zRotation = 0f;
                switch (rotationDirection)
                {
                    case 1: // Up (+Y in world)
                        zRotation = 180f;
                        break;
                    case 2: // Down (-Y in world)
                        zRotation = 0f;
                        break;
                    case 3: // Left (-X in world)
                        zRotation = 90f;
                        break;
                    case 4: // Right (+X in world)
                        zRotation = -90f;
                        break;
                }

                // Apply directional rotation on top of the base rotation from the prefab
                Quaternion directionRotation = Quaternion.Euler(0f, 0f, zRotation);
                animator.transform.localRotation = directionRotation * baseRotation;
            }
        }

        /// <summary>
        /// Checks if the Red Cap is in contact with a visitor.
        /// </summary>
        private void CheckForVisitorContact()
        {
            if (targetVisitor == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, targetVisitor.transform.position);

            if (distance <= contactRadius)
            {
                CaptureVisitor(targetVisitor);
            }
        }

        /// <summary>
        /// Captures a visitor and starts the killing process.
        /// </summary>
        /// <param name="visitor">The visitor to capture</param>
        private void CaptureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            // Start killing state
            state = RedCapState.Killing;
            killingTarget = visitor;
            killingTimer = killingDuration;

            // Immobilize the visitor (daze them for the kill duration so they can't move)
            visitor.OnWitnessMazeGrowth(killingDuration + 1f);

            // Stop the RedCap's movement
            worldPath.Clear();
            targetVisitor = null;
        }

        /// <summary>
        /// Updates the killing state, completing the kill after the duration.
        /// </summary>
        private void UpdateKilling()
        {
            killingTimer -= Time.deltaTime;

            if (killingTimer <= 0f)
            {
                CompleteKill();
            }
        }

        /// <summary>
        /// Completes the kill, despawns the visitor, and charges the essence penalty.
        /// </summary>
        private void CompleteKill()
        {
            if (killingTarget != null)
            {
                // Calculate essence penalty
                int essencePenalty = Mathf.RoundToInt(baseEssencePerVisitor * essencePenaltyMultiplier);

                // Deduct essence from player
                if (gameController != null)
                {
                    gameController.AddEssence(-essencePenalty, EssenceSource.RedCapPenalty, $"Visitor killed");
                }

                // Track visitor fate - negative essence since it's a penalty
                if (GameStatsTracker.Instance != null)
                {
                    GameStatsTracker.Instance.RecordVisitorFate(killingTarget.Archetype, VisitorFate.RedCapKill, -essencePenalty);
                }

                // Despawn the visitor
                Destroy(killingTarget.gameObject);
                killingTarget = null;
            }

            // Return to hunting (or flee if essence is low)
            if (gameController != null && gameController.CurrentEssence < startingEssence)
            {
                StartFleeing();
            }
            else
            {
                state = RedCapState.Hunting;
            }
        }

        #endregion

        #region Fleeing Behavior

        /// <summary>
        /// Starts the fleeing state, causing the Red Cap to path to an exit.
        /// </summary>
        private void StartFleeing()
        {
            if (state == RedCapState.Fleeing)
                return;

            state = RedCapState.Fleeing;
            targetVisitor = null;

            // Find and path to nearest exit
            BuildPathToNearestExit();

            Debug.Log("[RedCap] Fleeing - essence dropped below starting value");
        }

        /// <summary>
        /// Builds a path to the nearest exit spawn point.
        /// </summary>
        private void BuildPathToNearestExit()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                worldPath.Clear();
                return;
            }

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            if (spawnPoints.Count == 0)
            {
                worldPath.Clear();
                return;
            }

            // Find nearest spawn point (exit)
            Vector3 nearestExit = Vector3.zero;
            float nearestDist = float.MaxValue;

            foreach (var kvp in spawnPoints)
            {
                float dist = Vector3.Distance(transform.position, kvp.Value);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestExit = kvp.Value;
                }
            }

            // Build path to exit
            worldPath = BuildWorldPath(transform.position, nearestExit);
            currentWaypointIndex = 0;
        }

        /// <summary>
        /// Checks if the Red Cap has reached an exit and should despawn.
        /// </summary>
        private void CheckForReachedExit()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
                return;

            var spawnPoints = mazeGridBehaviour.WorldSpaceMazeData.GetSpawnPointPositions();
            foreach (var kvp in spawnPoints)
            {
                float dist = Vector3.Distance(transform.position, kvp.Value);
                if (dist < waypointReachedDistance * 2f)
                {
                    // Reached exit - despawn
                    Debug.Log("[RedCap] Reached exit - despawning");
                    Destroy(gameObject);
                    return;
                }
            }

            // If we've run out of path, rebuild it
            if (worldPath.Count == 0 || currentWaypointIndex >= worldPath.Count)
            {
                BuildPathToNearestExit();
            }
        }

        #endregion

        #region Frightening Visitors

        /// <summary>
        /// Checks for nearby visitors and frightens them.
        /// </summary>
        private void CheckForVisitorsToFrighten()
        {
            frightenCheckTimer -= Time.deltaTime;
            if (frightenCheckTimer > 0f)
                return;

            frightenCheckTimer = frightenCheckInterval;

            // Find all visitors in frighten radius
            VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            foreach (var visitor in allVisitors)
            {
                if (visitor == null || visitor.gameObject == null)
                    continue;

                // Skip the visitor we're currently killing
                if (visitor == killingTarget)
                    continue;

                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= frightenRadius)
                {
                    // Frighten the visitor - they flee away from the Red Cap
                    visitor.SetFrightened(transform.position);
                }
            }
        }

        #endregion

        #region Visual

        /// <summary>
        /// Creates the procedural visual representation of the Red Cap.
        /// </summary>
        private void CreateProceduralVisual()
        {
            // Add SpriteRenderer if not already present
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a sprite (simple circle for now)
            spriteRenderer.sprite = CreateCircleSprite(32);
            spriteRenderer.color = redCapColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(redCapSize, redCapSize, 1f);

            SphereCollider collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = contactRadius;
                collider.isTrigger = true;
                collider.center = Vector3.zero; // XY-plane collision
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

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw Red Cap position
            Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw contact radius
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, contactRadius);

            // Draw path in world space
            if (worldPath != null && worldPath.Count > 0)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);

                for (int i = currentWaypointIndex; i < worldPath.Count; i++)
                {
                    Vector3 worldPos = worldPath[i];
                    Gizmos.DrawSphere(worldPos, 0.1f);

                    if (i > currentWaypointIndex)
                    {
                        Vector3 prevWorldPos = worldPath[i - 1];
                        Gizmos.DrawLine(prevWorldPos, worldPos);
                    }
                    else if (i == currentWaypointIndex)
                    {
                        Gizmos.DrawLine(transform.position, worldPos);
                    }
                }
            }

            // Draw line to target visitor
            if (targetVisitor != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
                Gizmos.DrawLine(transform.position, targetVisitor.transform.position);
            }
        }

        #endregion
    }
}

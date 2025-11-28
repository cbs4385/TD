using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;
using FaeMaze.Maze;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Willow-the-Wisp that wanders the maze and lures visitors to the Heart of the Maze.
    /// Wanders at 2x visitor speed when alone, slows to visitor speed when leading.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class WillowTheWisp : MonoBehaviour
    {
        #region Enums

        public enum WispState
        {
            Wandering,  // Randomly wandering the maze
            Leading     // Leading a visitor to the heart
        }

        #endregion

        #region Serialized Fields

        [Header("Movement Settings")]
        [SerializeField]
        [Tooltip("Base movement speed (2x visitor speed when wandering)")]
        private float wanderSpeed = 6f; // 2x the default visitor speed of 3

        [SerializeField]
        [Tooltip("Speed when leading a visitor (matches visitor speed)")]
        private float leadSpeed = 3f; // Matches visitor speed

        [SerializeField]
        [Tooltip("Distance threshold to consider a waypoint reached")]
        private float waypointReachedDistance = 0.05f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the wisp sprite")]
        private Color wispColor = new Color(0.9f, 1f, 0.4f, 1f); // Yellow-green glow

        [SerializeField]
        [Tooltip("Size of the wisp sprite")]
        private float wispSize = 0.5f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 16;

        [SerializeField]
        [Tooltip("Enable pulsing glow effect")]
        private bool enablePulse = true;

        [SerializeField]
        [Tooltip("Pulse speed")]
        private float pulseSpeed = 3f;

        [SerializeField]
        [Tooltip("Pulse magnitude")]
        private float pulseMagnitude = 0.15f;

        #endregion

        #region Private Fields

        private WispState state;
        private MazeGridBehaviour mazeGridBehaviour;
        private GameController gameController;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Vector3 baseScale;

        // Wandering path
        private List<Vector2Int> wanderPath;
        private int currentPathIndex;

        // Visitor being led
        private VisitorController followingVisitor;

        // Target destination (Heart of the Maze)
        private Vector2Int heartGridPosition;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the wisp</summary>
        public WispState State => state;

        /// <summary>Gets whether this wisp is currently leading a visitor</summary>
        public bool IsLeading => state == WispState.Leading && followingVisitor != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            state = WispState.Wandering;
            CreateVisualSprite();
            SetupColliders();
        }

        private void Start()
        {
            // Find references
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            gameController = GameController.Instance;

            if (mazeGridBehaviour == null)
            {
                Debug.LogError("WillowTheWisp: Could not find MazeGridBehaviour!");
                return;
            }

            if (gameController == null)
            {
                Debug.LogError("WillowTheWisp: Could not find GameController!");
                return;
            }

            // Get heart position
            if (gameController.Heart != null)
            {
                heartGridPosition = gameController.Heart.GridPosition;
            }
            else
            {
                Debug.LogWarning("WillowTheWisp: Heart not found! Will try to locate it later.");
            }

            // Start wandering
            GenerateRandomWanderPath();
        }

        private void Update()
        {
            if (enablePulse && spriteRenderer != null)
            {
                UpdatePulse();
            }

            if (state == WispState.Wandering)
            {
                UpdateWandering();
            }
            else if (state == WispState.Leading)
            {
                UpdateLeading();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Only capture visitors when wandering
            if (state != WispState.Wandering)
                return;

            var visitor = other.GetComponent<VisitorController>();
            if (visitor != null && visitor.State == VisitorController.VisitorState.Walking)
            {
                CaptureVisitor(visitor);
            }
        }

        #endregion

        #region Visual Setup

        private void CreateVisualSprite()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a glowing circle sprite
            spriteRenderer.sprite = CreateCircleSprite(32);
            spriteRenderer.color = wispColor;
            spriteRenderer.sortingOrder = sortingOrder;

            baseScale = new Vector3(wispSize, wispSize, 1f);
            transform.localScale = baseScale;
        }

        private Sprite CreateCircleSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle with soft edges (glow effect)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
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

        private void SetupColliders()
        {
            // Rigidbody2D for physics
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            // Trigger collider for visitor detection
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }
            collider.radius = 0.4f;
            collider.isTrigger = true;
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        #endregion

        #region Wandering Behavior

        private void GenerateRandomWanderPath()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Get current grid position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Debug.LogWarning("WillowTheWisp: Could not convert world position to grid!");
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Pick a random walkable tile as destination
            Vector2Int randomDest = GetRandomWalkableTile();
            if (randomDest == currentPos)
            {
                // Try again in a moment
                Invoke(nameof(GenerateRandomWanderPath), 1f);
                return;
            }

            // Find path to random destination
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, randomDest, pathNodes) && pathNodes.Count > 0)
            {
                wanderPath = new List<Vector2Int>();
                foreach (var node in pathNodes)
                {
                    wanderPath.Add(new Vector2Int(node.x, node.y));
                }

                currentPathIndex = 0;
                Debug.Log($"WillowTheWisp: Generated wander path with {wanderPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"WillowTheWisp: Failed to find wander path from {currentPos} to {randomDest}");
                // Try again
                Invoke(nameof(GenerateRandomWanderPath), 1f);
            }
        }

        private Vector2Int GetRandomWalkableTile()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.Grid == null)
                return Vector2Int.zero;

            var grid = mazeGridBehaviour.Grid;
            int maxAttempts = 50;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                int x = Random.Range(0, grid.Width);
                int y = Random.Range(0, grid.Height);

                var node = grid.GetNode(x, y);
                if (node != null && node.walkable)
                {
                    return new Vector2Int(x, y);
                }

                attempts++;
            }

            // Fallback: return current position
            if (mazeGridBehaviour.WorldToGrid(transform.position, out int cx, out int cy))
            {
                return new Vector2Int(cx, cy);
            }

            return Vector2Int.zero;
        }

        private void UpdateWandering()
        {
            if (wanderPath == null || wanderPath.Count == 0)
            {
                GenerateRandomWanderPath();
                return;
            }

            if (currentPathIndex >= wanderPath.Count)
            {
                // Reached end of wander path, generate new one
                GenerateRandomWanderPath();
                return;
            }

            // Move toward current waypoint
            Vector2Int targetGridPos = wanderPath[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                wanderSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if waypoint reached
            float distance = Vector3.Distance(transform.position, targetWorldPos);
            if (distance < waypointReachedDistance)
            {
                currentPathIndex++;
            }
        }

        #endregion

        #region Leading Behavior

        private void CaptureVisitor(VisitorController visitor)
        {
            Debug.Log($"WillowTheWisp: Captured visitor {visitor.name}!");

            followingVisitor = visitor;
            state = WispState.Leading;

            // Notify visitor to follow this wisp
            var followWisp = visitor.gameObject.GetComponent<FollowWispBehavior>();
            if (followWisp == null)
            {
                followWisp = visitor.gameObject.AddComponent<FollowWispBehavior>();
            }
            followWisp.StartFollowing(this);

            // Generate path to heart
            GeneratePathToHeart();
        }

        private void GeneratePathToHeart()
        {
            if (mazeGridBehaviour == null || gameController == null)
                return;

            // Update heart position if we didn't have it before
            if (gameController.Heart != null)
            {
                heartGridPosition = gameController.Heart.GridPosition;
            }
            else
            {
                Debug.LogError("WillowTheWisp: Cannot lead to heart - heart not found!");
                ReturnToWandering();
                return;
            }

            // Get current position
            if (!mazeGridBehaviour.WorldToGrid(transform.position, out int currentX, out int currentY))
            {
                Debug.LogWarning("WillowTheWisp: Could not convert position to grid!");
                ReturnToWandering();
                return;
            }

            Vector2Int currentPos = new Vector2Int(currentX, currentY);

            // Find path to heart
            List<MazeGrid.MazeNode> pathNodes = new List<MazeGrid.MazeNode>();
            if (gameController.TryFindPath(currentPos, heartGridPosition, pathNodes) && pathNodes.Count > 0)
            {
                wanderPath = new List<Vector2Int>();
                foreach (var node in pathNodes)
                {
                    wanderPath.Add(new Vector2Int(node.x, node.y));
                }

                currentPathIndex = 0;
                Debug.Log($"WillowTheWisp: Leading visitor to heart with path of {wanderPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning($"WillowTheWisp: Failed to find path to heart from {currentPos}");
                ReturnToWandering();
            }
        }

        private void UpdateLeading()
        {
            // Check if visitor is still following
            if (followingVisitor == null || followingVisitor.State == VisitorController.VisitorState.Consumed)
            {
                Debug.Log("WillowTheWisp: Visitor consumed or lost, returning to wandering");
                ReturnToWandering();
                return;
            }

            if (wanderPath == null || wanderPath.Count == 0)
            {
                GeneratePathToHeart();
                return;
            }

            if (currentPathIndex >= wanderPath.Count)
            {
                // Reached heart - visitor should be consumed soon
                Debug.Log("WillowTheWisp: Reached heart, returning to wandering");
                ReturnToWandering();
                return;
            }

            // Move toward current waypoint at visitor speed
            Vector2Int targetGridPos = wanderPath[currentPathIndex];
            Vector3 targetWorldPos = mazeGridBehaviour.GridToWorld(targetGridPos.x, targetGridPos.y);

            Vector3 newPosition = Vector3.MoveTowards(
                transform.position,
                targetWorldPos,
                leadSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.MovePosition(newPosition);
                Physics2D.SyncTransforms();
            }
            else
            {
                transform.position = newPosition;
            }

            // Check if waypoint reached
            float distance = Vector3.Distance(transform.position, targetWorldPos);
            if (distance < waypointReachedDistance)
            {
                currentPathIndex++;
            }
        }

        private void ReturnToWandering()
        {
            state = WispState.Wandering;
            followingVisitor = null;
            wanderPath = null;
            currentPathIndex = 0;

            // Generate new random wander path
            GenerateRandomWanderPath();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Draw current path
            if (wanderPath != null && wanderPath.Count > 0 && mazeGridBehaviour != null)
            {
                Gizmos.color = state == WispState.Leading ? Color.yellow : Color.green;

                for (int i = 0; i < wanderPath.Count - 1; i++)
                {
                    Vector3 start = mazeGridBehaviour.GridToWorld(wanderPath[i].x, wanderPath[i].y);
                    Vector3 end = mazeGridBehaviour.GridToWorld(wanderPath[i + 1].x, wanderPath[i + 1].y);
                    Gizmos.DrawLine(start, end);
                }

                // Draw current target
                if (currentPathIndex < wanderPath.Count)
                {
                    Gizmos.color = Color.yellow;
                    Vector3 target = mazeGridBehaviour.GridToWorld(wanderPath[currentPathIndex].x, wanderPath[currentPathIndex].y);
                    Gizmos.DrawWireSphere(target, 0.2f);
                }
            }
        }

        #endregion
    }
}

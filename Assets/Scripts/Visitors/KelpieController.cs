using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Maze;
using FaeMaze.Props;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Kelpie - A water spirit bound to a water tile near Puka hazards.
    /// Remains stationary and lures adjacent visitors toward the nearby Puka.
    /// Activates and animates when visitors come near.
    /// </summary>
    public class KelpieController : MonoBehaviour
    {
        #region Enums

        public enum KelpieState
        {
            Idle,
            Luring
        }

        #endregion

        #region Serialized Fields

        [Header("Detection Settings")]
        [SerializeField]
        [Tooltip("How often to scan for adjacent visitors (in seconds)")]
        private float scanInterval = 0.5f;

        [SerializeField]
        [Tooltip("Distance to detect adjacent visitors")]
        private float detectionRadius = 1.5f;

        [SerializeField]
        [Tooltip("Chance (0-1) that visitor is lured toward Puka when adjacent")]
        [Range(0f, 1f)]
        private float lureChance = 0.8f;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Use procedural sprite if true, otherwise relies on child SpriteRenderer/Animator")]
        private bool useProceduralSprite = false;

        [SerializeField]
        [Tooltip("Color of the Kelpie (used for procedural sprite)")]
        private Color kelpieColor = new Color(0.1f, 0.6f, 0.8f, 1f); // Aqua blue

        [SerializeField]
        [Tooltip("Size of the Kelpie sprite")]
        private float kelpieSize = 1.0f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 15;

        [Header("Animation Settings")]
        [SerializeField]
        [Tooltip("Animator parameter name for direction")]
        private string directionParameterName = "Direction";

        #endregion

        #region Private Fields

        private KelpieState state = KelpieState.Idle;
        private MazeGridBehaviour mazeGridBehaviour;
        private PukaHazard assignedPuka;
        private HashSet<GameObject> processedVisitors;
        private float scanTimer;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private bool initialized;
        private Vector2Int gridPosition;

        // Direction tracking for animation
        private const int IdleDirection = 0;
        private int lastDirection = IdleDirection;
        private int currentAnimatorDirection = IdleDirection;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the Kelpie</summary>
        public KelpieState State => state;

        /// <summary>Gets the assigned Puka hazard</summary>
        public PukaHazard AssignedPuka => assignedPuka;

        /// <summary>Gets the grid position of this Kelpie</summary>
        public Vector2Int GridPosition => gridPosition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            processedVisitors = new HashSet<GameObject>();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!initialized)
            {
                TryInitialize();
                return;
            }

            // Periodically scan for adjacent visitors
            scanTimer += Time.deltaTime;
            if (scanTimer >= scanInterval)
            {
                scanTimer = 0f;
                ScanForAdjacentVisitors();
            }

            // Update state based on whether we have nearby visitors
            if (state == KelpieState.Luring)
            {
                // Play luring animation
                AnimateLuring();
            }
        }

        private void TryInitialize()
        {
            if (initialized)
            {
                return;
            }

            // Find required components
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            animator = GetComponent<Animator>();

            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Get grid position
            if (mazeGridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                gridPosition = new Vector2Int(x, y);
            }

            // Find nearest Puka
            AssignNearestPuka();

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
                SetAnimatorDirection(IdleDirection);
            }

            // Start idle
            state = KelpieState.Idle;
            initialized = true;
        }

        #endregion

        #region Puka Assignment

        /// <summary>
        /// Assigns the nearest Puka hazard to this Kelpie.
        /// </summary>
        private void AssignNearestPuka()
        {
            var allPukas = PukaHazard.All;

            if (allPukas.Count == 0)
            {
                return;
            }

            PukaHazard nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var puka in allPukas)
            {
                if (puka == null) continue;

                float distance = Vector3.Distance(transform.position, puka.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = puka;
                }
            }

            assignedPuka = nearest;
        }

        #endregion

        #region Visitor Detection

        private bool IsVisitorActive(VisitorControllerBase.VisitorState state)
        {
            return state == VisitorControllerBase.VisitorState.Walking
                || state == VisitorControllerBase.VisitorState.Fascinated
                || state == VisitorControllerBase.VisitorState.Confused
                || state == VisitorControllerBase.VisitorState.Frightened;
        }

        /// <summary>
        /// Scans for visitors adjacent to this Kelpie.
        /// </summary>
        private void ScanForAdjacentVisitors()
        {
            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Find all visitors in the scene
            VisitorControllerBase[] allVisitors = FindObjectsByType<VisitorControllerBase>(FindObjectsSortMode.None);

            bool hasAdjacentVisitor = false;

            foreach (var visitor in allVisitors)
            {
                if (visitor == null || processedVisitors.Contains(visitor.gameObject))
                {
                    continue;
                }

                // Check if visitor is active
                if (!IsVisitorActive(visitor.State))
                {
                    continue;
                }

                // Check if visitor is adjacent (within detection radius)
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= detectionRadius)
                {
                    hasAdjacentVisitor = true;
                    LureVisitor(visitor);
                }
            }

            // Update state based on whether we have adjacent visitors
            if (hasAdjacentVisitor)
            {
                if (state != KelpieState.Luring)
                {
                    state = KelpieState.Luring;
                }
            }
            else
            {
                if (state != KelpieState.Idle)
                {
                    state = KelpieState.Idle;
                    SetAnimatorDirection(IdleDirection);
                }
            }

            // Clean up destroyed visitors from processed set
            processedVisitors.RemoveWhere(v => v == null);
        }

        /// <summary>
        /// Lures a visitor toward the assigned Puka.
        /// </summary>
        private void LureVisitor(VisitorControllerBase visitor)
        {
            if (visitor == null || assignedPuka == null)
            {
                return;
            }

            // Mark as processed
            processedVisitors.Add(visitor.gameObject);

            // Roll for lure success
            float roll = Random.value;
            if (roll > lureChance)
            {
                return;
            }

            // Calculate direction to Puka and animate
            Vector3 directionToPuka = (assignedPuka.transform.position - transform.position).normalized;
            int animDirection = GetDirectionFromMovement(new Vector2(directionToPuka.x, directionToPuka.y));
            SetAnimatorDirection(animDirection);

            // TODO: Could add path modification here to lure visitor toward Puka
            // For now, the Kelpie just animates when visitors are near
        }

        /// <summary>
        /// Animates the Kelpie luring gesture toward the Puka.
        /// </summary>
        private void AnimateLuring()
        {
            if (assignedPuka == null)
            {
                return;
            }

            // Calculate direction to Puka and animate
            Vector3 directionToPuka = (assignedPuka.transform.position - transform.position).normalized;
            int animDirection = GetDirectionFromMovement(new Vector2(directionToPuka.x, directionToPuka.y));
            SetAnimatorDirection(animDirection);
        }

        #endregion

        #region Animation

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
        /// Sets the animator direction parameter.
        /// </summary>
        private void SetAnimatorDirection(int direction)
        {
            if (animator != null && currentAnimatorDirection != direction)
            {
                animator.SetInteger(directionParameterName, direction);
                currentAnimatorDirection = direction;
            }
        }

        #endregion

        #region Visual

        /// <summary>
        /// Creates the procedural visual representation of the Kelpie.
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
            spriteRenderer.color = kelpieColor;
            spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(kelpieSize, kelpieSize, 1f);
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
            // Draw Kelpie position
            Gizmos.color = new Color(0.1f, 0.6f, 0.8f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

            // Draw detection radius
            Gizmos.color = new Color(0.1f, 0.8f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Draw line to assigned Puka
            if (assignedPuka != null)
            {
                Gizmos.color = state == KelpieState.Luring ? new Color(1f, 0.5f, 0f, 0.7f) : new Color(0f, 1f, 1f, 0.5f);
                Gizmos.DrawLine(transform.position, assignedPuka.transform.position);
            }
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// FaeLantern that fascinates visitors using flood-fill area of effect.
    /// Visitors entering the influence area abandon their path, move to the lantern,
    /// stand still for 2 seconds, then wander randomly at intersections.
    /// </summary>
    public class FaeLantern : MonoBehaviour
    {
        #region Static Registry

        private static readonly HashSet<FaeLantern> _activeLanterns = new HashSet<FaeLantern>();

        /// <summary>Gets all active FaeLanterns in the scene</summary>
        public static IReadOnlyCollection<FaeLantern> All => _activeLanterns;

        #endregion

        #region Serialized Fields

        [Header("Influence Settings")]
        [SerializeField]
        [Tooltip("Radius of influence in grid tiles (Manhattan distance)")]
        private int influenceRadius = 6;

        [SerializeField]
        [Tooltip("Maximum flood-fill steps for influence area calculation")]
        private int maxFloodFillSteps = 24;

        [SerializeField]
        [Tooltip("Duration in seconds that visitor stands fascinated at the lantern")]
        private float fascinationDuration = 2f;

        [SerializeField]
        [Tooltip("Probability (0-1) that a visitor becomes fascinated when entering influence")]
        [Range(0f, 1f)]
        private float procChance = 1.0f;

        [SerializeField]
        [Tooltip("Cooldown in seconds before a visitor can be fascinated again")]
        private float cooldownSec = 5.0f;

        [SerializeField]
        [Tooltip("Attraction delta applied to grid nodes within influence area")]
        private float attractionDelta = 2.0f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw the flood-fill influence area in Scene view")]
        private bool debugDrawInfluence = true;

        [Header("Visual Settings")]
        [SerializeField]
        [Tooltip("Color of the lantern sprite")]
        private Color lanternColor = new Color(1f, 0.9f, 0.3f, 1f); // Golden yellow

        [SerializeField]
        [Tooltip("Size of the lantern sprite")]
        private float lanternSize = 0.8f;

        [SerializeField]
        [Tooltip("Sprite rendering layer order")]
        private int sortingOrder = 12;

        [SerializeField]
        [Tooltip("Generate a procedural sprite instead of using imported visuals/animations")]
        private bool useProceduralSprite = false;

        #endregion

        #region Private Fields

        private MazeGridBehaviour _gridBehaviour;
        private Vector2Int _gridPosition;
        private HashSet<Vector2Int> _influenceCells;
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private Vector3 _initialScale;

        private const string DirectionParameter = "Direction";

        #endregion

        #region Properties

        /// <summary>Gets the grid position of this lantern</summary>
        public Vector2Int GridPosition => _gridPosition;

        /// <summary>Gets the fascination duration in seconds</summary>
        public float FascinationDuration => fascinationDuration;

        /// <summary>Gets the proc chance for fascination (0-1)</summary>
        public float ProcChance => procChance;

        /// <summary>Gets the cooldown in seconds before re-fascination</summary>
        public float CooldownSec => cooldownSec;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _initialScale = transform.localScale;

            _animator = GetComponent<Animator>();

            // Find the MazeGridBehaviour in the scene
            _gridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (_gridBehaviour == null)
            {
                return;
            }

            SetupSpriteRenderer();

            SetIdleDirection();
        }

        private void Start()
        {
            // Wait for grid to be ready and calculate influence area
            if (_gridBehaviour != null && _gridBehaviour.Grid != null)
            {
                CalculateInfluenceArea();
            }
        }

        private void OnEnable()
        {
            _activeLanterns.Add(this);
        }

        private void OnDisable()
        {
            _activeLanterns.Remove(this);
        }

        private void Update()
        {
            UpdateDirectionToClosestVisitor();
        }

        #endregion

        #region Influence Calculation

        /// <summary>
        /// Calculates the flood-fill influence area for this lantern.
        /// Only tiles reachable via walkable paths are included.
        /// Applies attraction delta to all influenced nodes.
        /// </summary>
        private void CalculateInfluenceArea()
        {
            // Convert world position to grid coordinates
            if (!_gridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                return;
            }

            _gridPosition = new Vector2Int(x, y);

            // Use flood-fill to get reachable tiles (stops when either radius or step limit is reached)
            _influenceCells = _gridBehaviour.Grid.FloodFillReachable(x, y, influenceRadius, maxFloodFillSteps);

            // Apply attraction delta to all influenced cells
            foreach (var cell in _influenceCells)
            {
                _gridBehaviour.Grid.AddAttraction(cell.x, cell.y, attractionDelta);
            }

        }

        /// <summary>
        /// Checks if a grid cell is within this lantern's influence area.
        /// </summary>
        /// <param name="cell">Grid position to check</param>
        /// <returns>True if the cell is within influence</returns>
        public bool IsCellInInfluence(Vector2Int cell)
        {
            return _influenceCells != null && _influenceCells.Contains(cell);
        }

        /// <summary>
        /// Gets the cached flood-filled influence area for visitor checks.
        /// </summary>
        /// <returns>Read-only collection of influenced grid cells</returns>
        public IReadOnlyCollection<Vector2Int> GetInfluenceArea()
        {
            return _influenceCells ?? new HashSet<Vector2Int>();
        }

        #endregion

        #region Animation Control

        /// <summary>
        /// Updates the animator direction to point toward the closest unfascinated visitor in range.
        /// Sets direction to idle (0) if no unfascinated visitors are in the influence area.
        /// Uses cached visitor registry for efficient lookup.
        /// </summary>
        private void UpdateDirectionToClosestVisitor()
        {
            if (_animator == null || _influenceCells == null || _gridBehaviour == null)
            {
                return;
            }

            FaeMaze.Visitors.VisitorController closestVisitor = null;
            float closestDistance = float.MaxValue;

            // Iterate through cached visitor registry instead of FindObjectsByType
            foreach (var visitor in FaeMaze.Visitors.VisitorController.All)
            {
                if (visitor == null)
                    continue;

                // Skip fascinated visitors
                if (visitor.IsFascinated)
                    continue;

                // Get visitor's grid position
                if (_gridBehaviour.WorldToGrid(visitor.transform.position, out int visitorX, out int visitorY))
                {
                    Vector2Int visitorGridPos = new Vector2Int(visitorX, visitorY);

                    // Check if visitor is in influence area
                    if (IsCellInInfluence(visitorGridPos))
                    {
                        // Calculate distance to lantern
                        float distance = Vector2Int.Distance(visitorGridPos, _gridPosition);

                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestVisitor = visitor;
                        }
                    }
                }
            }

            // Update direction based on closest visitor
            if (closestVisitor != null)
            {
                // Get visitor's grid position
                if (_gridBehaviour.WorldToGrid(closestVisitor.transform.position, out int visitorX, out int visitorY))
                {
                    Vector2Int visitorGridPos = new Vector2Int(visitorX, visitorY);
                    SetInteractionDirection(visitorGridPos);
                }
            }
            else
            {
                // No unfascinated visitors in range - set to idle
                SetIdleDirection();
            }
        }

        /// <summary>
        /// Sets the animator Direction parameter based on where the visitor is relative to the lantern.
        /// </summary>
        /// <param name="visitorGridPosition">Grid position of the interacting visitor.</param>
        public void SetInteractionDirection(Vector2Int visitorGridPosition)
        {
            if (_animator == null)
            {
                return;
            }

            int directionValue = 0; // Idle by default

            if (visitorGridPosition.y > _gridPosition.y)
            {
                directionValue = 1; // Up (-y direction)
            }
            else if (visitorGridPosition.y < _gridPosition.y)
            {
                directionValue = 2; // Down (+y direction)
            }
            else if (visitorGridPosition.x < _gridPosition.x)
            {
                directionValue = 3; // Left (-x direction)
            }
            else if (visitorGridPosition.x > _gridPosition.x)
            {
                directionValue = 4; // Right (+x direction)
            }

            _animator.SetInteger(DirectionParameter, directionValue);
        }

        /// <summary>
        /// Resets the animator Direction parameter to idle (0).
        /// </summary>
        public void SetIdleDirection()
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetInteger(DirectionParameter, 0);
        }

        #endregion

        #region Visual

        private void SetupSpriteRenderer()
        {
            _spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
                gameObject,
                createProceduralSprite: useProceduralSprite,
                useSoftEdges: false,
                resolution: 32,
                pixelsPerUnit: 32
            );

            ApplySpriteSettings();
        }

        private void ApplySpriteSettings()
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            // Only override scale when generating a procedural sprite
            if (useProceduralSprite)
            {
                ProceduralSpriteFactory.ApplySpriteSettings(
                    _spriteRenderer,
                    lanternColor,
                    sortingOrder,
                    lanternSize,
                    applyScale: true
                );
            }
            else
            {
                ProceduralSpriteFactory.ApplySpriteSettings(
                    _spriteRenderer,
                    lanternColor,
                    sortingOrder,
                    applyScale: false
                );
                transform.localScale = _initialScale;
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugDrawInfluence || _influenceCells == null || _gridBehaviour == null)
            {
                return;
            }

            // Draw influence area
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.2f); // Semi-transparent golden
            foreach (var cell in _influenceCells)
            {
                Vector3 worldPos = _gridBehaviour.GridToWorld(cell.x, cell.y);
                Gizmos.DrawCube(worldPos, new Vector3(0.9f, 0.9f, 0.1f));
            }

            // Draw lantern center
            if (_gridPosition != Vector2Int.zero)
            {
                Gizmos.color = new Color(1f, 0.7f, 0f, 0.8f);
                Vector3 centerWorld = _gridBehaviour.GridToWorld(_gridPosition.x, _gridPosition.y);
                Gizmos.DrawWireSphere(centerWorld, 0.4f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawInfluence || _influenceCells == null || _gridBehaviour == null)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.4f);
            foreach (var cell in _influenceCells)
            {
                Vector3 worldPos = _gridBehaviour.GridToWorld(cell.x, cell.y);
                Gizmos.DrawCube(worldPos, new Vector3(0.95f, 0.95f, 0.1f));
            }

            // Draw influence radius as wire sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, influenceRadius);
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// FaeLantern that fascinates visitors using world-space area of effect.
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
        [Tooltip("Radius of influence in world units")]
        private float influenceRadius = 6f;

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

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Draw the influence area in Scene view")]
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
        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private Vector3 _initialScale;

        private const string DirectionParameter = "Direction";

        #endregion

        #region Properties

        /// <summary>Gets the world position of this lantern</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Gets the fascination duration in seconds</summary>
        public float FascinationDuration => fascinationDuration;

        /// <summary>Gets the proc chance for fascination (0-1)</summary>
        public float ProcChance => procChance;

        /// <summary>Gets the cooldown in seconds before re-fascination</summary>
        public float CooldownSec => cooldownSec;

        /// <summary>Gets the influence radius in world units</summary>
        public float InfluenceRadius => influenceRadius;

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

            // Only setup sprite renderer if this is NOT a 3D model (no MeshRenderer)
            // 3D models use LanternGlow for visuals instead
            if (GetComponentInChildren<MeshRenderer>() == null)
            {
                SetupSpriteRenderer();
            }

            SetIdleDirection();
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

        #region Influence Checks

        /// <summary>
        /// Checks if a world position is within this lantern's influence area.
        /// </summary>
        /// <param name="worldPos">World position to check</param>
        /// <returns>True if the position is within influence radius</returns>
        public bool IsPositionInInfluence(Vector3 worldPos)
        {
            float distance = Vector3.Distance(transform.position, worldPos);
            return distance <= influenceRadius;
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
            if (_animator == null)
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

                // Check if visitor is in influence area using world-space distance
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= influenceRadius && distance < closestDistance)
                {
                    closestDistance = distance;
                    closestVisitor = visitor;
                }
            }

            // Update direction based on closest visitor
            if (closestVisitor != null)
            {
                SetInteractionDirection(closestVisitor.transform.position);
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
        /// <param name="visitorWorldPosition">World position of the interacting visitor.</param>
        public void SetInteractionDirection(Vector3 visitorWorldPosition)
        {
            if (_animator == null)
            {
                return;
            }

            Vector3 delta = visitorWorldPosition - transform.position;
            int directionValue = 0; // Idle by default

            // Determine dominant axis
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            {
                directionValue = delta.y > 0 ? 1 : 2; // Up : Down
            }
            else if (Mathf.Abs(delta.x) > 0.01f)
            {
                directionValue = delta.x < 0 ? 3 : 4; // Left : Right
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
            if (!debugDrawInfluence)
            {
                return;
            }

            // Draw influence area as a world-space circle
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.2f); // Semi-transparent golden
            DrawCircleGizmo(transform.position, influenceRadius, 32);

            // Draw lantern center
            Gizmos.color = new Color(1f, 0.7f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawInfluence)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.4f);
            DrawCircleGizmo(transform.position, influenceRadius, 32);

            // Draw influence radius as wire sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, influenceRadius);
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        #endregion
    }
}

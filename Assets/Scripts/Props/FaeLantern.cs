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
        [Tooltip("Radius of influence in grid steps (tiles)")]
        private int influenceRadius = 8;

        [SerializeField]
        [Tooltip("Duration in seconds that visitor stands fascinated at the lantern")]
        private float fascinationDuration = 2f;

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

        #endregion

        #region Private Fields

        private MazeGridBehaviour _gridBehaviour;
        private Vector2Int _gridPosition;
        private HashSet<Vector2Int> _influenceCells;
        private SpriteRenderer _spriteRenderer;

        #endregion

        #region Properties

        /// <summary>Gets the grid position of this lantern</summary>
        public Vector2Int GridPosition => _gridPosition;

        /// <summary>Gets the fascination duration in seconds</summary>
        public float FascinationDuration => fascinationDuration;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Find the MazeGridBehaviour in the scene
            _gridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (_gridBehaviour == null)
            {
                Debug.LogError("FaeLantern: Could not find MazeGridBehaviour in scene!");
                return;
            }
        }

        private void Start()
        {
            // Create visual sprite
            CreateVisualSprite();

            // Wait for grid to be ready and calculate influence area
            if (_gridBehaviour != null && _gridBehaviour.Grid != null)
            {
                CalculateInfluenceArea();
            }
            else
            {
                Debug.LogError("FaeLantern: Grid not ready in Start(), cannot calculate influence!");
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

        #endregion

        #region Influence Calculation

        /// <summary>
        /// Calculates the flood-fill influence area for this lantern.
        /// Only tiles reachable via walkable paths are included.
        /// </summary>
        private void CalculateInfluenceArea()
        {
            // Convert world position to grid coordinates
            if (!_gridBehaviour.WorldToGrid(transform.position, out int x, out int y))
            {
                Debug.LogWarning($"FaeLantern: Position {transform.position} is outside grid bounds!");
                return;
            }

            _gridPosition = new Vector2Int(x, y);

            // Use flood-fill to get reachable tiles
            _influenceCells = _gridBehaviour.Grid.FloodFillReachable(x, y, influenceRadius);

            Debug.Log($"FaeLantern at {_gridPosition}: Influence area calculated with {_influenceCells.Count} tiles (radius={influenceRadius})");
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

        #endregion

        #region Visual

        private void CreateVisualSprite()
        {
            // Add SpriteRenderer if not already present
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a simple circle sprite for the lantern
            _spriteRenderer.sprite = CreateLanternSprite(32);
            _spriteRenderer.color = lanternColor;
            _spriteRenderer.sortingOrder = sortingOrder;

            // Set scale
            transform.localScale = new Vector3(lanternSize, lanternSize, 1f);
        }

        private Sprite CreateLanternSprite(int resolution)
        {
            int size = resolution;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;

            // Create a circle (simplified lantern shape)
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

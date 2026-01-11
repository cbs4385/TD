using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Visitors;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// A mystical Puka that creates hazards for visitors using world-space coordinates.
    /// When visitors become adjacent to a Puka, they may be teleported or destroyed.
    /// Pukas can link to water locations in the maze.
    /// </summary>
    public class PukaHazard : MonoBehaviour
    {
        #region Static Registry

        private static readonly List<PukaHazard> _allPukas = new List<PukaHazard>();

        /// <summary>Gets all active Pukas in the scene</summary>
        public static IReadOnlyList<PukaHazard> All => _allPukas;

        #endregion

        #region Serialized Fields

        [Header("Kelpie Spawning")]
        [SerializeField]
        [Tooltip("Prefab for Kelpie water spirit that lures visitors toward this Puka")]
        private GameObject kelpiePrefab;

        [SerializeField]
        [Tooltip("Should this Puka spawn a Kelpie guardian?")]
        private bool spawnKelpie = true;

        [SerializeField]
        [Tooltip("Offset from Puka position to spawn Kelpie")]
        private Vector3 kelpieSpawnOffset = new Vector3(2f, 0f, 0f);

        [Header("Interaction Settings")]
        [SerializeField]
        [Tooltip("Chance (0-1) that nothing happens when visitor is adjacent")]
        [Range(0f, 1f)]
        private float noInteractionChance = 0.2f;

        [SerializeField]
        [Tooltip("Chance (0-1) that visitor is teleported to linked water location")]
        [Range(0f, 1f)]
        private float teleportChance = 0.7f;

        // Note: Kill chance is implicit (1.0 - noInteractionChance - teleportChance)
        // Typically 10% with default settings (1.0 - 0.2 - 0.7 = 0.1)

        [SerializeField]
        [Tooltip("How often to scan for adjacent visitors (seconds)")]
        private float scanInterval = 0.5f;

        [SerializeField]
        [Tooltip("World-space distance to consider a visitor adjacent")]
        private float adjacencyDistance = 1.5f;

        [Header("Linked Teleport Locations")]
        [SerializeField]
        [Tooltip("World positions where visitors can be teleported to (water locations)")]
        private List<Vector3> linkedWaterPositions = new List<Vector3>();

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
        [Tooltip("Draw linked water locations in Scene view")]
        private bool debugDrawLinks = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGridBehaviour;
        private HashSet<GameObject> processedVisitors; // Track which visitors we've already interacted with
        private float scanTimer;
        private SpriteRenderer spriteRenderer;
        private Vector3 baseScale;
        private Vector3 initialScale;
        private GameObject spawnedKelpie;

        #endregion

        #region Properties

        /// <summary>Gets the world position of this Puka</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Gets the list of linked water positions in world space</summary>
        public IReadOnlyList<Vector3> LinkedWaterPositions => linkedWaterPositions;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            initialScale = transform.localScale;
            processedVisitors = new HashSet<GameObject>();
            SetupSpriteRenderer();
        }

        private void Start()
        {
            // Find references
            mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();

            if (mazeGridBehaviour == null)
            {
                return;
            }

            // Spawn Kelpie if enabled
            if (spawnKelpie && kelpiePrefab != null)
            {
                SpawnKelpie();
            }
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

        #region Visitor Detection and Interaction

        private bool IsVisitorActive(FaeMaze.Visitors.VisitorControllerBase.VisitorState state)
        {
            return state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Walking
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Fascinated
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Confused
                || state == FaeMaze.Visitors.VisitorControllerBase.VisitorState.Frightened;
        }

        /// <summary>
        /// Scans for visitors adjacent to this Puka using world-space distance.
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

                // Check if visitor is active
                if (!IsVisitorActive(visitor.State))
                {
                    continue;
                }

                // Check world-space distance for adjacency
                float distance = Vector3.Distance(transform.position, visitor.transform.position);
                if (distance <= adjacencyDistance)
                {
                    InteractWithVisitor(visitor.gameObject, visitor.transform.position);
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

                // Check world-space distance for adjacency
                float distance = Vector3.Distance(transform.position, mistakingVisitor.transform.position);
                if (distance <= adjacencyDistance)
                {
                    InteractWithVisitor(mistakingVisitor.gameObject, mistakingVisitor.transform.position);
                }
            }

            // Clean up destroyed visitors from processed set
            processedVisitors.RemoveWhere(v => v == null);
        }

        /// <summary>
        /// Interacts with a visitor that has become adjacent to this Puka.
        /// Rolls for one of three outcomes: no interaction, teleport, or kill.
        /// </summary>
        private void InteractWithVisitor(GameObject visitorObject, Vector3 visitorWorldPos)
        {
            if (visitorObject == null)
            {
                return;
            }

            // Mark as processed
            processedVisitors.Add(visitorObject);

            // Calculate approach direction based on visitor position relative to Puka
            int direction = CalculateApproachDirection(visitorWorldPos);

            // Set Direction parameter on Animator if present
            var animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetInteger("Direction", direction);
            }

            // Roll for interaction
            float roll = Random.value;

            if (roll < noInteractionChance)
            {
                // 20% - No interaction
                return;
            }
            else if (roll < noInteractionChance + teleportChance)
            {
                // 70% - Teleport to linked water location
                TeleportVisitor(visitorObject);
            }
            else
            {
                // 10% - Kill visitor
                KillVisitor(visitorObject);
            }
        }

        /// <summary>
        /// Calculates which direction the visitor is approaching from using world-space.
        /// Returns: 1 (+y), 2 (-y), 3 (-x), 4 (+x), or 0 (other)
        /// </summary>
        private int CalculateApproachDirection(Vector3 visitorWorldPos)
        {
            Vector3 delta = visitorWorldPos - transform.position;

            // Determine dominant direction
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            {
                return delta.y > 0 ? 1 : 2; // +y (north) : -y (south)
            }
            else if (Mathf.Abs(delta.x) > 0.01f)
            {
                return delta.x < 0 ? 3 : 4; // -x (west) : +x (east)
            }

            return 0; // Centered or other
        }

        /// <summary>
        /// Teleports a visitor to a randomly selected linked water location.
        /// </summary>
        private void TeleportVisitor(GameObject visitorObject)
        {
            if (linkedWaterPositions.Count == 0)
            {
                return;
            }

            // Pick a random linked water position
            Vector3 targetWorldPos = linkedWaterPositions[Random.Range(0, linkedWaterPositions.Count)];

            // Teleport the visitor
            visitorObject.transform.position = targetWorldPos;

            // Let the visitor recalculate its own path
            var visitorController = visitorObject.GetComponent<VisitorController>();
            if (visitorController != null)
            {
                visitorController.RecalculatePath();
            }

            var mistakingVisitorController = visitorObject.GetComponent<MistakingVisitorController>();
            if (mistakingVisitorController != null)
            {
                mistakingVisitorController.RecalculatePath();
            }

            // Play sound effect if available
            FaeMaze.Audio.SoundManager.Instance?.PlayLanternPlaced(); // Reuse lantern sound for now
        }

        /// <summary>
        /// Kills a visitor immediately.
        /// </summary>
        private void KillVisitor(GameObject visitorObject)
        {
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
            spriteRenderer = ProceduralSpriteFactory.SetupSpriteRenderer(
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
            if (spriteRenderer == null)
            {
                return;
            }

            // Only override scale when generating a procedural sprite
            if (useProceduralSprite)
            {
                baseScale = new Vector3(pukaSize, pukaSize, 1f);
                ProceduralSpriteFactory.ApplySpriteSettings(
                    spriteRenderer,
                    pukaColor,
                    sortingOrder,
                    pukaSize,
                    applyScale: true
                );
            }
            else
            {
                baseScale = initialScale;
                ProceduralSpriteFactory.ApplySpriteSettings(
                    spriteRenderer,
                    pukaColor,
                    sortingOrder,
                    applyScale: false
                );
                transform.localScale = baseScale;
            }
        }

        private void UpdatePulse()
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseMagnitude;
            transform.localScale = baseScale * (1f + pulse);
        }

        #endregion

        #region Kelpie Spawning

        /// <summary>
        /// Spawns a Kelpie water spirit near this Puka.
        /// </summary>
        private void SpawnKelpie()
        {
            if (spawnedKelpie != null)
            {
                return; // Already spawned
            }

            Vector3 spawnPosition = transform.position + kelpieSpawnOffset;
            spawnedKelpie = Instantiate(kelpiePrefab, spawnPosition, Quaternion.identity);
            spawnedKelpie.name = $"Kelpie_{gameObject.name}";
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a water position to the linked teleport locations.
        /// </summary>
        /// <param name="worldPos">World position to add</param>
        public void AddLinkedWaterPosition(Vector3 worldPos)
        {
            if (!linkedWaterPositions.Contains(worldPos))
            {
                linkedWaterPositions.Add(worldPos);
            }
        }

        /// <summary>
        /// Clears all linked water positions.
        /// </summary>
        public void ClearLinkedWaterPositions()
        {
            linkedWaterPositions.Clear();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!debugDrawLinks || linkedWaterPositions == null)
            {
                return;
            }

            // Draw lines to linked water positions
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green
            foreach (var waterPos in linkedWaterPositions)
            {
                Gizmos.DrawLine(transform.position, waterPos);

                // Draw small sphere at linked position
                Gizmos.DrawWireSphere(waterPos, 0.2f);
            }

            // Draw adjacency radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, adjacencyDistance);
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugDrawLinks || linkedWaterPositions == null)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
            foreach (var waterPos in linkedWaterPositions)
            {
                Gizmos.DrawLine(transform.position, waterPos);

                // Draw sphere at linked position
                Gizmos.DrawSphere(waterPos, 0.15f);
            }

            // Draw this Puka's position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Draw adjacency radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, adjacencyDistance);
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.UI;
using FaeMaze.Visitors;
using UnityEngine.Rendering;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Core game controller managing the Fae Maze gameplay.
    /// Singleton pattern for easy access from other systems.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        #region Singleton

        private static GameController _instance;

        public static GameController Instance
        {
            get
            {
                return _instance;
            }
        }

        #endregion

        #region Serialized Fields

        [Header("Maze References")]
        [SerializeField]
        [Tooltip("The transform acting as the origin point for the maze grid")]
        private Transform mazeOrigin;

        [SerializeField]
        [Tooltip("Reference to the maze entrance")]
        private MazeEntrance entrance;

        [SerializeField]
        [Tooltip("Reference to the Heart of the Maze")]
        private HeartOfTheMaze heart;

        [Header("System References")]
        [SerializeField]
        [Tooltip("Reference to the UI controller")]
        private UIController uiController;

        [Header("Essence Settings")]
        [SerializeField]
        [Tooltip("Essence amount the player starts with when the game begins")]
        private int startingEssence = 200;

        #endregion

        #region Private Fields

        private MazeGrid mazeGrid;
        private MazePathfinder pathfinder;
        private int currentEssence;
        private VisitorController lastSpawnedVisitor;
        private static int? persistentEssence;

        private const string ParticleLayerObjectName = "ThinParticleLayer";
        private const float ParticleLayerDepth = 5f;
        private ParticleSystem thinParticleSystem;
        private ParticleSystemRenderer thinParticleRenderer;

        #endregion

        #region Events

        /// <summary>
        /// Event invoked whenever the essence value changes.
        /// Passes the new essence value as a parameter.
        /// </summary>
        public event System.Action<int> OnEssenceChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently registered maze grid.
        /// </summary>
        public MazeGrid MazeGrid => mazeGrid;

        /// <summary>
        /// Gets the maze entrance.
        /// </summary>
        public MazeEntrance Entrance => entrance;

        /// <summary>
        /// Gets the Heart of the Maze.
        /// </summary>
        public HeartOfTheMaze Heart => heart;

        /// <summary>
        /// Gets the last spawned visitor.
        /// </summary>
        public VisitorController LastSpawnedVisitor => lastSpawnedVisitor;

        /// <summary>
        /// Gets the current essence count.
        /// </summary>
        public int CurrentEssence => currentEssence;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton pattern enforcement
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            int configuredStartingEssence = GameSettings.StartingEssence;
            if (configuredStartingEssence <= 0)
            {
                configuredStartingEssence = startingEssence;
            }

            startingEssence = configuredStartingEssence;

            if (!persistentEssence.HasValue)
            {
                persistentEssence = Mathf.Max(0, configuredStartingEssence);
            }

            currentEssence = Mathf.Max(0, persistentEssence.Value);

            EnsureThinParticleLayer();

        }

        private void Start()
        {
            ValidateReferences();

            EnsurePlacementUI();

            EnsureResourcesUI();

            // Invoke event for initial essence value
            OnEssenceChanged?.Invoke(currentEssence);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers the maze grid with the game controller.
        /// </summary>
        /// <param name="grid">The MazeGrid instance to register</param>
        public void RegisterMazeGrid(MazeGrid grid)
        {
            if (grid == null)
            {
                return;
            }

            mazeGrid = grid;

            // Create pathfinder once grid is registered
            pathfinder = new MazePathfinder(mazeGrid);

            UpdateThinParticleLayerToMaze();
        }

        /// <summary>
        /// Attempts to find a path from start to end using A* pathfinding.
        /// </summary>
        /// <param name="start">Start position in grid coordinates</param>
        /// <param name="end">End position in grid coordinates</param>
        /// <param name="resultPath">Output list of MazeNodes forming the path</param>
        /// <param name="attractionMultiplier">Multiplier for attraction effect (1.0 = normal, -1.0 = inverted, 0 = ignore)</param>
        /// <returns>True if path was found, false otherwise</returns>
        public bool TryFindPath(Vector2Int start, Vector2Int end, List<MazeGrid.MazeNode> resultPath, float attractionMultiplier = 1.0f)
        {
            if (pathfinder == null)
            {
                return false;
            }

            return pathfinder.TryFindPath(start.x, start.y, end.x, end.y, resultPath, attractionMultiplier);
        }

        /// <summary>
        /// Gets the transform acting as the maze origin point.
        /// </summary>
        /// <returns>The maze origin transform, or null if not assigned</returns>
        public Transform GetMazeOrigin()
        {
            return mazeOrigin;
        }

        /// <summary>
        /// Gets the currently registered maze grid.
        /// </summary>
        /// <returns>The registered MazeGrid instance, or null if none registered</returns>
        public MazeGrid GetMazeGrid()
        {
            return mazeGrid;
        }

        /// <summary>
        /// Gets the UI controller reference.
        /// </summary>
        /// <returns>The UIController instance, or null if not assigned</returns>
        public UIController GetUIController()
        {
            return uiController;
        }

        /// <summary>
        /// Adds essence to the current total.
        /// </summary>
        /// <param name="amount">Amount of essence to add</param>
        public void AddEssence(int amount)
        {
            if (amount < 0)
            {
                return;
            }

            currentEssence += amount;

            persistentEssence = currentEssence;

            // Invoke event for essence change
            OnEssenceChanged?.Invoke(currentEssence);
        }

        /// <summary>
        /// Attempts to spend essence. Returns true and deducts if enough, otherwise returns false.
        /// </summary>
        /// <param name="cost">Amount of essence to spend</param>
        /// <returns>True if essence was spent, false if insufficient funds</returns>
        public bool TrySpendEssence(int cost)
        {
            if (cost < 0)
            {
                return false;
            }

            if (currentEssence >= cost)
            {
                currentEssence -= cost;

                persistentEssence = currentEssence;

                // Invoke event for essence change
                OnEssenceChanged?.Invoke(currentEssence);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Sets the last spawned visitor reference.
        /// </summary>
        public void SetLastSpawnedVisitor(VisitorController visitor)
        {
            lastSpawnedVisitor = visitor;
        }

        #endregion

        #region Private Methods

        private void ValidateReferences()
        {
            // UIController is optional at startup

            // Auto-find HeartOfTheMaze if reference is broken/null
            if (heart == null)
            {
                heart = FindFirstObjectByType<HeartOfTheMaze>();
                if (heart != null)
                {
                }
            }

            // Auto-find MazeEntrance if reference is broken/null
            if (entrance == null)
            {
                entrance = FindFirstObjectByType<MazeEntrance>();
                if (entrance != null)
                {
                }
            }
        }

        /// <summary>
        /// Helper method to find or instantiate a UI component and parent it under the UIController.
        /// </summary>
        /// <typeparam name="T">The component type to find or create</typeparam>
        /// <param name="gameObjectName">The name to assign to the GameObject if it needs to be created</param>
        /// <returns>The found or created component instance</returns>
        private T EnsureUIComponent<T>(string gameObjectName) where T : Component
        {
            var component = FindFirstObjectByType<T>();
            if (component != null)
            {
                return component;
            }

            GameObject uiObject = new GameObject(gameObjectName);
            if (uiController != null)
            {
                uiObject.transform.SetParent(uiController.transform, false);
            }

            return uiObject.AddComponent<T>();
        }

        private void EnsurePlacementUI()
        {
            EnsureUIComponent<PlacementUIController>("PlacementUI");
        }

        private void EnsureResourcesUI()
        {
            EnsureUIComponent<FaeMaze.UI.PlayerResourcesUIController>("PlayerResourcesUI");
        }

        private void EnsureThinParticleLayer()
        {
            if (GameObject.Find(ParticleLayerObjectName) != null)
            {
                thinParticleSystem = GameObject.Find(ParticleLayerObjectName).GetComponent<ParticleSystem>();
                thinParticleRenderer = thinParticleSystem != null ? thinParticleSystem.GetComponent<ParticleSystemRenderer>() : null;
                ApplyThinParticleLighting();
                UpdateThinParticleLayerToMaze();
                return;
            }

            GameObject layerObject = new GameObject(ParticleLayerObjectName);
            layerObject.transform.SetParent(transform, false);

            thinParticleSystem = layerObject.AddComponent<ParticleSystem>();
            var main = thinParticleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.5f;
            main.startSpeed = 0.1f;
            main.startSize = 0.025f;
            main.maxParticles = 300;

            var emission = thinParticleSystem.emission;
            emission.rateOverTime = 60f;

            var shape = thinParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            thinParticleRenderer = thinParticleSystem.GetComponent<ParticleSystemRenderer>();
            thinParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            ApplyThinParticleLighting();

            UpdateThinParticleLayerToMaze();
        }

        private void UpdateThinParticleLayerToMaze()
        {
            if (thinParticleSystem == null)
            {
                return;
            }

            float tileSize = 1f;
            var gridBehaviour = FindObjectOfType<MazeGridBehaviour>();
            if (gridBehaviour != null)
            {
                tileSize = Mathf.Max(0.01f, gridBehaviour.TileSize);
            }

            Vector3 origin = mazeOrigin != null ? mazeOrigin.position : Vector3.zero;
            Vector3 minBounds = origin;
            Vector3 maxBounds = origin + new Vector3(
                (mazeGrid != null ? mazeGrid.Width : 1) * tileSize,
                (mazeGrid != null ? mazeGrid.Height : 1) * tileSize,
                -ParticleLayerDepth
            );

            UpdateThinParticleLayerBounds(minBounds, maxBounds);
        }

        private void UpdateThinParticleLayerBounds(Vector3 minBounds, Vector3 maxBounds)
        {
            if (thinParticleSystem == null)
            {
                return;
            }

            Vector3 center = (minBounds + maxBounds) * 0.5f;
            Vector3 size = new Vector3(
                Mathf.Abs(maxBounds.x - minBounds.x),
                Mathf.Abs(maxBounds.y - minBounds.y),
                Mathf.Abs(maxBounds.z - minBounds.z)
            );

            thinParticleSystem.transform.position = center;

            var shape = thinParticleSystem.shape;
            shape.scale = size;
        }

        private void ApplyThinParticleLighting()
        {
            if (thinParticleRenderer == null)
            {
                return;
            }

            thinParticleRenderer.receiveShadows = true;
            thinParticleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            thinParticleRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            thinParticleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Shader litParticleShader = Shader.Find("Particles/Standard Surface");
            if (litParticleShader != null)
            {
                if (thinParticleRenderer.sharedMaterial == null || thinParticleRenderer.sharedMaterial.shader != litParticleShader)
                {
                    var material = new Material(litParticleShader)
                    {
                        color = Color.white
                    };
                    thinParticleRenderer.sharedMaterial = material;
                }
            }
        }

        public static void ResetPersistentEssence()
        {
            persistentEssence = null;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                persistentEssence = currentEssence;
            }
        }

        #endregion
    }
}

using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.UI;
using FaeMaze.Visitors;

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

        // Persistent essence tracking across scenes
        private static int? persistentEssence = null;
        private static bool hasInitializedEssence = false;

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
            // Note: Unity's null check returns false for destroyed objects, so this handles scene reloads
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                // Another instance exists and is still valid, destroy this duplicate
                Destroy(gameObject);
                return;
            }

            // Initialize essence: use persistent value if available, otherwise use starting essence
            if (hasInitializedEssence && persistentEssence.HasValue)
            {
                currentEssence = persistentEssence.Value;
            }
            else
            {
                currentEssence = Mathf.Max(0, startingEssence);
                hasInitializedEssence = true;
                persistentEssence = currentEssence;
            }

            // Particle system spawner disabled - using URP Volume Fog instead
            // EnsureParticleSystemSpawner();
        }

        private void EnsureParticleSystemSpawner()
        {
            // Check if this GameObject already has a MazeParticleSystemSpawner
            if (GetComponent<MazeParticleSystemSpawner>() == null)
            {
                gameObject.AddComponent<MazeParticleSystemSpawner>();
            }
        }

        private void Start()
        {
            ValidateReferences();

            // EnsurePlacementUI(); // Disabled - BuildPanel no longer needed in new HUD
            // EnsureResourcesUI(); // Disabled - HeartPowerPanelController now displays essence

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
            int before = currentEssence;
            currentEssence += amount;

            Debug.Log($"[GameController] AddEssence called: adding {amount}, before={before}, after={currentEssence}");

            // Update persistent essence
            persistentEssence = currentEssence;
            Debug.Log($"[GameController] Updated persistentEssence to {persistentEssence}");

            // Invoke event for essence change
            OnEssenceChanged?.Invoke(currentEssence);
            Debug.Log($"[GameController] OnEssenceChanged event invoked with {currentEssence}");
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
                Debug.LogWarning($"[GameController] TrySpendEssence: negative cost {cost}, rejecting");
                return false;
            }

            if (currentEssence >= cost)
            {
                int before = currentEssence;
                currentEssence -= cost;

                // Update persistent essence
                persistentEssence = currentEssence;

                Debug.Log($"[GameController] TrySpendEssence: spent {cost}, before={before}, after={currentEssence}");

                // Invoke event for essence change
                OnEssenceChanged?.Invoke(currentEssence);

                return true;
            }

            Debug.Log($"[GameController] TrySpendEssence: insufficient essence (have {currentEssence}, need {cost}), rejecting");
            return false;
        }

        /// <summary>
        /// Sets the last spawned visitor reference.
        /// </summary>
        public void SetLastSpawnedVisitor(VisitorController visitor)
        {
            lastSpawnedVisitor = visitor;
        }

        /// <summary>
        /// Resets essence to the starting value.
        /// Call this when starting a new game from the beginning.
        /// </summary>
        public void ResetEssenceToStart()
        {
            currentEssence = Mathf.Max(0, startingEssence);
            persistentEssence = currentEssence;
            hasInitializedEssence = true;

            // Invoke event for essence change
            OnEssenceChanged?.Invoke(currentEssence);
        }

        /// <summary>
        /// Resets all persistent game state (static fields).
        /// Call this before loading a new game from the main menu.
        /// </summary>
        public static void ResetPersistentGameState()
        {
            persistentEssence = null;
            hasInitializedEssence = false;
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

        #endregion
    }
}

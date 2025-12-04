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
        private int startingEssence = 100;

        #endregion

        #region Private Fields

        private MazeGrid mazeGrid;
        private MazePathfinder pathfinder;
        private int currentEssence;
        private VisitorController lastSpawnedVisitor;

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

            currentEssence = Mathf.Max(0, startingEssence);

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
        }

        /// <summary>
        /// Attempts to find a path from start to end using A* pathfinding.
        /// </summary>
        /// <param name="start">Start position in grid coordinates</param>
        /// <param name="end">End position in grid coordinates</param>
        /// <param name="resultPath">Output list of MazeNodes forming the path</param>
        /// <returns>True if path was found, false otherwise</returns>
        public bool TryFindPath(Vector2Int start, Vector2Int end, List<MazeGrid.MazeNode> resultPath)
        {
            if (pathfinder == null)
            {
                return false;
            }

            return pathfinder.TryFindPath(start.x, start.y, end.x, end.y, resultPath);
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
        }

        private void EnsurePlacementUI()
        {
            var placementUI = FindFirstObjectByType<PlacementUIController>();
            if (placementUI != null)
            {
                return;
            }

            GameObject placementUiObject = new GameObject("PlacementUI");
            if (uiController != null)
            {
                placementUiObject.transform.SetParent(uiController.transform, false);
            }

            placementUI = placementUiObject.AddComponent<PlacementUIController>();
        }

        private void EnsureResourcesUI()
        {
            var resourcesUI = FindFirstObjectByType<FaeMaze.UI.PlayerResourcesUIController>();
            if (resourcesUI != null)
            {
                return;
            }

            GameObject resourcesUiObject = new GameObject("PlayerResourcesUI");
            if (uiController != null)
            {
                resourcesUiObject.transform.SetParent(uiController.transform, false);
            }

            resourcesUI = resourcesUiObject.AddComponent<FaeMaze.UI.PlayerResourcesUIController>();
        }

        #endregion
    }
}

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
                if (_instance == null)
                {
                    Debug.LogError("GameController instance is null! Make sure it exists in the scene.");
                }
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

        #endregion

        #region Private Fields

        private MazeGrid mazeGrid;
        private MazePathfinder pathfinder;
        private int currentEssence;
        private VisitorController lastSpawnedVisitor;

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
                Debug.LogError("Multiple GameController instances detected! Destroying duplicate on: " + gameObject.name);
                Destroy(gameObject);
                return;
            }

            _instance = this;

            Debug.Log("GameController initialized successfully.");
        }

        private void Start()
        {
            ValidateReferences();
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
                Debug.LogError("Attempted to register null MazeGrid!");
                return;
            }

            mazeGrid = grid;
            Debug.Log("MazeGrid registered with GameController.");

            // Create pathfinder once grid is registered
            pathfinder = new MazePathfinder(mazeGrid);
            Debug.Log("MazePathfinder initialized.");
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
                Debug.LogError("GameController: Pathfinder is not initialized! Make sure MazeGrid is registered first.");
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
            if (mazeOrigin == null)
            {
                Debug.LogWarning("MazeOrigin is not assigned in GameController!");
            }
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
                Debug.LogWarning($"Attempted to add negative essence: {amount}. Use TrySpendEssence for spending.");
                return;
            }

            currentEssence += amount;
            Debug.Log($"Added {amount} essence. Current total: {currentEssence}");

            // Update UI
            if (uiController != null)
            {
                uiController.UpdateEssence(currentEssence);
            }
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
                Debug.LogWarning($"Attempted to spend negative essence: {cost}.");
                return false;
            }

            if (currentEssence >= cost)
            {
                currentEssence -= cost;
                Debug.Log($"Spent {cost} essence. Remaining: {currentEssence}");

                // Update UI
                if (uiController != null)
                {
                    uiController.UpdateEssence(currentEssence);
                }

                return true;
            }

            Debug.LogWarning($"Insufficient essence to spend {cost}. Current: {currentEssence}");
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
            if (mazeOrigin == null)
            {
                Debug.LogWarning("MazeOrigin is not assigned in GameController!");
            }

            if (mazeGrid == null)
            {
                Debug.LogWarning("MazeGrid has not been registered yet. Make sure MazeGridBehaviour initializes before GameController.Start()");
            }

            if (entrance == null)
            {
                Debug.LogWarning("Entrance is not assigned in GameController!");
            }

            if (heart == null)
            {
                Debug.LogWarning("Heart is not assigned in GameController!");
            }

            // UIController is optional at startup
            if (uiController == null)
            {
                Debug.Log("UIController reference not yet assigned (will be set later).");
            }
        }

        #endregion
    }
}

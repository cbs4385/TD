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
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
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
        }

        private void Start()
        {
            ValidateReferences();

            // Invoke event for initial essence value
            OnEssenceChanged?.Invoke(currentEssence);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the transform acting as the maze origin point.
        /// </summary>
        public Transform GetMazeOrigin()
        {
            return mazeOrigin;
        }

        /// <summary>
        /// Gets the UI controller reference.
        /// </summary>
        public UIController GetUIController()
        {
            return uiController;
        }

        /// <summary>
        /// Adds essence to the current total.
        /// </summary>
        public void AddEssence(int amount)
        {
            currentEssence += amount;
            persistentEssence = currentEssence;
            OnEssenceChanged?.Invoke(currentEssence);
        }

        /// <summary>
        /// Attempts to spend essence. Returns true and deducts if enough, otherwise returns false.
        /// </summary>
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

        /// <summary>
        /// Resets essence to the starting value.
        /// </summary>
        public void ResetEssenceToStart()
        {
            currentEssence = Mathf.Max(0, startingEssence);
            persistentEssence = currentEssence;
            hasInitializedEssence = true;
            OnEssenceChanged?.Invoke(currentEssence);
        }

        /// <summary>
        /// Resets all persistent game state (static fields).
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
            if (heart == null)
            {
                heart = FindFirstObjectByType<HeartOfTheMaze>();
            }

            if (entrance == null)
            {
                entrance = FindFirstObjectByType<MazeEntrance>();
            }
        }

        #endregion
    }
}

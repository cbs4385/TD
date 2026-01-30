using UnityEngine;
using FaeMaze.Maze;
using FaeMaze.Visitors;
using System.Collections.Generic;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Identifies the source of an essence transaction for audit logging.
    /// </summary>
    public enum EssenceSource
    {
        Unknown,
        StartingEssence,
        EssenceDecay,
        LanternFascination,
        VisitorConsumedByHeart,
        VisitorConsumedByMaw,
        RedCapPenalty,
        PropPlacement,
        HeartPowerCost,
        HeartPowerBonus
    }

    /// <summary>
    /// Represents a single essence transaction for audit purposes.
    /// </summary>
    public struct EssenceTransaction
    {
        public float Timestamp;
        public EssenceSource Source;
        public int Amount;
        public int RunningTotal;
        public string Details;

        public override string ToString()
        {
            string sign = Amount >= 0 ? "+" : "";
            return $"[{Timestamp:F2}s] {Source}: {sign}{Amount} (Total: {RunningTotal}) {Details}";
        }
    }

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

        // Essence audit log
        private List<EssenceTransaction> essenceAuditLog = new List<EssenceTransaction>();
        private const int MAX_AUDIT_LOG_ENTRIES = 500;

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

        /// <summary>
        /// Gets a read-only view of the essence audit log.
        /// </summary>
        public IReadOnlyList<EssenceTransaction> EssenceAuditLog => essenceAuditLog;

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

            // Initialize the random manager FIRST - all other systems depend on it
            RandomManager.Initialize();

            // NOTE: Visitor-to-Visitor collision is disabled in Project Settings > Physics > Layer Collision Matrix
            // Do NOT call Physics.IgnoreLayerCollision at runtime - it causes Unity assertion errors

            // Load starting essence from settings (overrides serialized default)
            startingEssence = GameSettings.StartingEssence;

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
                // Log starting essence
                LogEssenceTransaction(EssenceSource.StartingEssence, currentEssence, "Game start");

                // Initialize max essence tracking with starting value
                GameStatsTracker.Instance?.UpdateMaxEssence(currentEssence);
            }
        }

        private void Start()
        {
            ValidateReferences();

            // Apply saved settings (light level, video settings, etc.)
            GameSettings.ApplySettings();

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
        /// Adds essence to the current total.
        /// </summary>
        public void AddEssence(int amount)
        {
            AddEssence(amount, EssenceSource.Unknown, null);
        }

        /// <summary>
        /// Adds essence to the current total with source tracking for audit log.
        /// </summary>
        public void AddEssence(int amount, EssenceSource source, string details = null)
        {
            currentEssence += amount;
            persistentEssence = currentEssence;
            LogEssenceTransaction(source, amount, details);
            OnEssenceChanged?.Invoke(currentEssence);

            // Track maximum essence achieved
            GameStatsTracker.Instance?.UpdateMaxEssence(currentEssence);
        }

        /// <summary>
        /// Attempts to spend essence. Returns true and deducts if enough, otherwise returns false.
        /// </summary>
        public bool TrySpendEssence(int cost)
        {
            return TrySpendEssence(cost, EssenceSource.Unknown, null);
        }

        /// <summary>
        /// Attempts to spend essence with source tracking for audit log.
        /// Returns true and deducts if enough, otherwise returns false.
        /// </summary>
        public bool TrySpendEssence(int cost, EssenceSource source, string details = null)
        {
            if (cost < 0)
            {
                return false;
            }

            if (currentEssence >= cost)
            {
                currentEssence -= cost;
                persistentEssence = currentEssence;
                LogEssenceTransaction(source, -cost, details);
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
            // Clear audit log on reset
            essenceAuditLog.Clear();

            // Re-read starting essence from settings in case it changed
            startingEssence = GameSettings.StartingEssence;

            currentEssence = Mathf.Max(0, startingEssence);
            persistentEssence = currentEssence;
            hasInitializedEssence = true;
            LogEssenceTransaction(EssenceSource.StartingEssence, currentEssence, "Reset to start");
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

        /// <summary>
        /// Clears the essence audit log.
        /// </summary>
        public void ClearAuditLog()
        {
            essenceAuditLog.Clear();
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

        private void LogEssenceTransaction(EssenceSource source, int amount, string details)
        {
            var transaction = new EssenceTransaction
            {
                Timestamp = Time.time,
                Source = source,
                Amount = amount,
                RunningTotal = currentEssence,
                Details = details ?? ""
            };

            essenceAuditLog.Add(transaction);

            // Trim log if it exceeds max size
            if (essenceAuditLog.Count > MAX_AUDIT_LOG_ENTRIES)
            {
                essenceAuditLog.RemoveAt(0);
            }
        }

        #endregion

        #region Debug Methods

        /// <summary>
        /// Prints the full audit log to the console.
        /// </summary>
        public void PrintAuditLog()
        {
            // Logging removed - use GetAuditLog() to access entries programmatically
        }

        /// <summary>
        /// Gets a summary of essence transactions by source.
        /// </summary>
        public Dictionary<EssenceSource, int> GetAuditSummary()
        {
            var summary = new Dictionary<EssenceSource, int>();
            foreach (var entry in essenceAuditLog)
            {
                if (!summary.ContainsKey(entry.Source))
                {
                    summary[entry.Source] = 0;
                }
                summary[entry.Source] += entry.Amount;
            }
            return summary;
        }

        /// <summary>
        /// Prints a summary of essence transactions by source.
        /// </summary>
        public void PrintAuditSummary()
        {
            // Logging removed - use GetAuditSummary() to access summary programmatically
        }

        #endregion
    }
}

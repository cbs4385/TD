using UnityEngine;
using System.Collections.Generic;
using FaeMaze.Visitors;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Defines the different fates a visitor can meet
    /// </summary>
    public enum VisitorFate
    {
        Consumed,       // Consumed by the Heart (tongue)
        Devoured,       // Devoured by the Maw power
        FairyRing,      // Essence depleted at fairy ring
        Lantern,        // Essence depleted at lantern
        Escaped,        // Escaped through exit portal
        RedCapKill,     // Killed by RedCap
        Drowned         // Drowned by Puka/Kelpie in pond
    }

    /// <summary>
    /// Tracks statistics for a specific visitor archetype
    /// </summary>
    public class ArchetypeStats
    {
        public Dictionary<VisitorFate, int> FateCounts = new Dictionary<VisitorFate, int>();
        public Dictionary<VisitorFate, int> EssenceByFate = new Dictionary<VisitorFate, int>();

        public ArchetypeStats()
        {
            // Initialize all fates to 0
            foreach (VisitorFate fate in System.Enum.GetValues(typeof(VisitorFate)))
            {
                FateCounts[fate] = 0;
                EssenceByFate[fate] = 0;
            }
        }

        public int TotalCount => GetTotalCount();
        public int TotalEssence => GetTotalEssence();

        private int GetTotalCount()
        {
            int total = 0;
            foreach (var count in FateCounts.Values)
                total += count;
            return total;
        }

        private int GetTotalEssence()
        {
            int total = 0;
            foreach (var essence in EssenceByFate.Values)
                total += essence;
            return total;
        }
    }

    /// <summary>
    /// Tracks game statistics for display on Game Over screen
    /// </summary>
    public class GameStatsTracker : MonoBehaviour
    {
        #region Singleton

        private static GameStatsTracker instance;
        public static GameStatsTracker Instance => instance;

        #endregion

        #region Statistics

        private int maxWaveReached = 0;
        private float totalTimePlayed = 0f;
        private float sessionStartTime;
        private int maxEssenceAchieved = 0;

        // Visitor fate tracking by archetype
        private Dictionary<VisitorArchetype, ArchetypeStats> archetypeStats = new Dictionary<VisitorArchetype, ArchetypeStats>();

        // Aggregate essence tracking by source
        private Dictionary<EssenceSource, int> essenceBySource = new Dictionary<EssenceSource, int>();

        #endregion

        #region Properties

        /// <summary>Gets the maximum wave number reached in this session</summary>
        public int MaxWaveReached => maxWaveReached;

        /// <summary>Gets the total time played in this session (seconds)</summary>
        public float TotalTimePlayed => totalTimePlayed;

        /// <summary>Gets the maximum essence achieved during this session</summary>
        public int MaxEssenceAchieved => maxEssenceAchieved;

        /// <summary>Gets the archetype statistics dictionary</summary>
        public Dictionary<VisitorArchetype, ArchetypeStats> ArchetypeStats => archetypeStats;

        /// <summary>Gets the essence by source dictionary</summary>
        public Dictionary<EssenceSource, int> EssenceBySource => essenceBySource;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            // Note: Unity's null check returns false for destroyed objects, so this handles scene reloads
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                ResetStats();
            }
            else if (instance != this)
            {
                // Another instance exists and is still valid, destroy this duplicate
                Destroy(gameObject);
                return;
            }
        }

        private void Update()
        {
            // Update total time played
            totalTimePlayed += Time.deltaTime;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records that a wave was reached
        /// </summary>
        public void RecordWaveReached(int waveNumber)
        {
            if (waveNumber > maxWaveReached)
            {
                maxWaveReached = waveNumber;
            }
        }

        /// <summary>
        /// Updates the maximum essence achieved if current essence exceeds it
        /// </summary>
        public void UpdateMaxEssence(int currentEssence)
        {
            if (currentEssence > maxEssenceAchieved)
            {
                maxEssenceAchieved = currentEssence;
            }
        }

        /// <summary>
        /// Records a visitor's fate with their archetype and essence value
        /// </summary>
        public void RecordVisitorFate(VisitorArchetype archetype, VisitorFate fate, int essenceValue)
        {
            if (!archetypeStats.ContainsKey(archetype))
            {
                archetypeStats[archetype] = new ArchetypeStats();
            }

            archetypeStats[archetype].FateCounts[fate]++;
            archetypeStats[archetype].EssenceByFate[fate] += essenceValue;
        }

        /// <summary>
        /// Resets all statistics to zero
        /// </summary>
        public void ResetStats()
        {
            maxWaveReached = 0;
            totalTimePlayed = 0f;
            maxEssenceAchieved = 0;
            archetypeStats.Clear();
            essenceBySource.Clear();
            sessionStartTime = Time.time;
        }

        /// <summary>
        /// Gets a formatted time string (MM:SS)
        /// </summary>
        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(totalTimePlayed / 60f);
            int seconds = Mathf.FloorToInt(totalTimePlayed % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Gets the total count of visitors for a specific fate across all archetypes
        /// </summary>
        public int GetTotalByFate(VisitorFate fate)
        {
            int total = 0;
            foreach (var stats in archetypeStats.Values)
            {
                total += stats.FateCounts[fate];
            }
            return total;
        }

        /// <summary>
        /// Gets the total essence for a specific fate across all archetypes
        /// </summary>
        public int GetTotalEssenceByFate(VisitorFate fate)
        {
            int total = 0;
            foreach (var stats in archetypeStats.Values)
            {
                total += stats.EssenceByFate[fate];
            }
            return total;
        }

        /// <summary>
        /// Gets essence totals by source from the GameController's audit log
        /// </summary>
        public Dictionary<EssenceSource, int> GetEssenceTotalsBySource()
        {
            var totals = new Dictionary<EssenceSource, int>();

            if (GameController.Instance == null)
                return totals;

            foreach (var transaction in GameController.Instance.EssenceAuditLog)
            {
                if (!totals.ContainsKey(transaction.Source))
                {
                    totals[transaction.Source] = 0;
                }
                totals[transaction.Source] += transaction.Amount;
            }

            return totals;
        }

        /// <summary>
        /// Gets the total number of visitors across all archetypes and fates
        /// </summary>
        public int GetTotalVisitors()
        {
            int total = 0;
            foreach (var stats in archetypeStats.Values)
            {
                total += stats.TotalCount;
            }
            return total;
        }

        #endregion
    }
}

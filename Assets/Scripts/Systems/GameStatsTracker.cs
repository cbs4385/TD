using UnityEngine;
using System.Collections.Generic;

namespace FaeMaze.Systems
{
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
        private int visitorsConsumed = 0;
        private float totalTimePlayed = 0f;
        private Dictionary<string, int> propsPlaced = new Dictionary<string, int>();
        private float sessionStartTime;

        #endregion

        #region Properties

        /// <summary>Gets the maximum wave number reached in this session</summary>
        public int MaxWaveReached => maxWaveReached;

        /// <summary>Gets the total number of visitors consumed by the Heart</summary>
        public int VisitorsConsumed => visitorsConsumed;

        /// <summary>Gets the total time played in this session (seconds)</summary>
        public float TotalTimePlayed => totalTimePlayed;

        /// <summary>Gets the dictionary of props placed (prop name -> count)</summary>
        public Dictionary<string, int> PropsPlaced => new Dictionary<string, int>(propsPlaced);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            ResetStats();
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
        /// Records that a visitor was consumed by the Heart
        /// </summary>
        public void RecordVisitorConsumed()
        {
            visitorsConsumed++;
        }

        /// <summary>
        /// Records that a prop was placed
        /// </summary>
        public void RecordPropPlaced(string propName)
        {
            if (string.IsNullOrEmpty(propName))
                return;

            if (propsPlaced.ContainsKey(propName))
            {
                propsPlaced[propName]++;
            }
            else
            {
                propsPlaced[propName] = 1;
            }
        }

        /// <summary>
        /// Resets all statistics to zero
        /// </summary>
        public void ResetStats()
        {
            maxWaveReached = 0;
            visitorsConsumed = 0;
            totalTimePlayed = 0f;
            propsPlaced.Clear();
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
        /// Gets a summary of all props placed as a formatted string
        /// </summary>
        public string GetPropsSummary()
        {
            if (propsPlaced.Count == 0)
                return "No props placed";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var kvp in propsPlaced)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }

            return sb.ToString().TrimEnd();
        }

        #endregion
    }
}

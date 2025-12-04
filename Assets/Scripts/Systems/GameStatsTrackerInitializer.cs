using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Ensures GameStatsTracker singleton is created at runtime
    /// </summary>
    public static class GameStatsTrackerInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Create GameStatsTracker if it doesn't exist
            if (GameStatsTracker.Instance == null)
            {
                GameObject trackerObj = new GameObject("GameStatsTracker");
                trackerObj.AddComponent<GameStatsTracker>();
                Debug.Log("[GameStatsTrackerInitializer] Created GameStatsTracker singleton");
            }
        }
    }
}

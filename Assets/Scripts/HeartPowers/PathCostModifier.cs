using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Manages temporary pathfinding cost modifiers using world-space positions.
    /// Works purely with Vector3 world positions.
    /// </summary>
    public class PathCostModifier
    {
        #region Nested Types

        /// <summary>
        /// Represents a temporary cost modifier at a world position.
        /// </summary>
        public class CostModification
        {
            public Vector3 worldPosition;
            public float costDelta;
            public float expirationTime;
            public string sourceId;

            public bool IsExpired => expirationTime > 0 && Time.time >= expirationTime;
        }

        #endregion

        #region Private Fields

        private readonly List<CostModification> modifiers = new List<CostModification>();

        // Reusable buffers
        private readonly List<CostModification> _modifiersToRemove = new List<CostModification>();

        #endregion

        #region Constructor

        public PathCostModifier()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a temporary cost modifier at a world position.
        /// </summary>
        public void AddModifier(Vector3 worldPos, float costDelta, float duration, string sourceId)
        {
            float expirationTime = duration > 0 ? Time.time + duration : 0;
            modifiers.Add(new CostModification
            {
                worldPosition = worldPos,
                costDelta = costDelta,
                expirationTime = expirationTime,
                sourceId = sourceId
            });
        }

        /// <summary>
        /// Removes all modifiers at a specific world position.
        /// </summary>
        public void ClearAtPosition(Vector3 worldPos, float proximityThreshold = 0.5f)
        {
            modifiers.RemoveAll(m => Vector3.Distance(m.worldPosition, worldPos) < proximityThreshold);
        }

        /// <summary>
        /// Removes all modifiers with a specific source ID.
        /// </summary>
        public void ClearBySource(string sourceId)
        {
            modifiers.RemoveAll(m => m.sourceId == sourceId);
        }

        /// <summary>
        /// Removes all expired modifiers.
        /// </summary>
        public void CleanupExpired()
        {
            modifiers.RemoveAll(m => m.IsExpired);
        }

        /// <summary>
        /// Gets the total cost modifier at a world position.
        /// </summary>
        public float GetTotalModifier(Vector3 worldPos, float proximityThreshold = 0.5f)
        {
            float total = 0f;
            foreach (var mod in modifiers)
            {
                if (!mod.IsExpired && Vector3.Distance(mod.worldPosition, worldPos) < proximityThreshold)
                {
                    total += mod.costDelta;
                }
            }
            return total;
        }

        /// <summary>
        /// Gets all world positions with active modifiers.
        /// </summary>
        public IEnumerable<Vector3> GetModifiedPositions()
        {
            var positions = new List<Vector3>();
            foreach (var mod in modifiers)
            {
                if (!mod.IsExpired)
                {
                    positions.Add(mod.worldPosition);
                }
            }
            return positions;
        }

        /// <summary>
        /// Clears all modifiers.
        /// </summary>
        public void ClearAll()
        {
            modifiers.Clear();
        }

        #endregion
    }
}

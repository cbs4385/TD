using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Manages temporary pathfinding cost modifiers applied to maze tiles.
    /// Powers can add/remove cost biases without permanently modifying the MazeGrid.
    /// </summary>
    public class PathCostModifier
    {
        #region Nested Types

        /// <summary>
        /// Represents a temporary cost modifier applied to a tile.
        /// </summary>
        public class CostModification
        {
            public Vector2Int tile;
            public float costDelta;          // Positive = more expensive, negative = cheaper
            public float expirationTime;     // Time.time when this modifier expires (0 = permanent until removed)
            public string sourceId;          // Identifier for the power/effect that created this

            public bool IsExpired => expirationTime > 0 && Time.time >= expirationTime;
        }

        #endregion

        #region Private Fields

        private readonly Dictionary<Vector2Int, List<CostModification>> modifiers = new Dictionary<Vector2Int, List<CostModification>>();
        private readonly MazeGridBehaviour gridBehaviour;

        // Reusable buffers to avoid GC allocations during cleanup operations
        private readonly List<Vector2Int> _tilesToUpdate = new List<Vector2Int>();
        private readonly List<Vector2Int> _allTilesBuffer = new List<Vector2Int>();

        #endregion

        #region Constructor

        public PathCostModifier(MazeGridBehaviour gridBehaviour)
        {
            this.gridBehaviour = gridBehaviour;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a temporary cost modifier to a tile.
        /// </summary>
        /// <param name="tile">The tile position</param>
        /// <param name="costDelta">Cost change (negative = cheaper/more desirable, positive = more expensive)</param>
        /// <param name="duration">Duration in seconds (0 = permanent until manually removed)</param>
        /// <param name="sourceId">Identifier for tracking (e.g., "MurmuringPaths_1")</param>
        public void AddModifier(Vector2Int tile, float costDelta, float duration, string sourceId)
        {
            if (!modifiers.ContainsKey(tile))
            {
                modifiers[tile] = new List<CostModification>();
            }

            float expirationTime = duration > 0 ? Time.time + duration : 0;
            modifiers[tile].Add(new CostModification
            {
                tile = tile,
                costDelta = costDelta,
                expirationTime = expirationTime,
                sourceId = sourceId
            });

            ApplyModifierToGrid(tile);
        }

        /// <summary>
        /// Removes all modifiers from a specific tile.
        /// </summary>
        public void ClearTile(Vector2Int tile)
        {
            if (modifiers.ContainsKey(tile))
            {
                modifiers.Remove(tile);
                ApplyModifierToGrid(tile);
            }
        }

        /// <summary>
        /// Removes all modifiers with a specific source ID.
        /// </summary>
        public void ClearBySource(string sourceId)
        {
            // Clear and reuse the buffer instead of creating new list
            _tilesToUpdate.Clear();

            foreach (var kvp in modifiers)
            {
                kvp.Value.RemoveAll(m => m.sourceId == sourceId);
                if (kvp.Value.Count == 0)
                {
                    _tilesToUpdate.Add(kvp.Key);
                }
                else
                {
                    ApplyModifierToGrid(kvp.Key);
                }
            }

            foreach (var tile in _tilesToUpdate)
            {
                modifiers.Remove(tile);
                ApplyModifierToGrid(tile);
            }
        }

        /// <summary>
        /// Removes all expired modifiers and updates affected tiles.
        /// Call this regularly (e.g., in Update or via a coroutine).
        /// </summary>
        public void CleanupExpired()
        {
            // Clear and reuse the buffer instead of creating new list
            _tilesToUpdate.Clear();

            foreach (var kvp in modifiers)
            {
                bool hadExpired = kvp.Value.RemoveAll(m => m.IsExpired) > 0;
                if (hadExpired)
                {
                    if (kvp.Value.Count == 0)
                    {
                        _tilesToUpdate.Add(kvp.Key);
                    }
                    else
                    {
                        ApplyModifierToGrid(kvp.Key);
                    }
                }
            }

            foreach (var tile in _tilesToUpdate)
            {
                modifiers.Remove(tile);
                ApplyModifierToGrid(tile);
            }
        }

        /// <summary>
        /// Gets the total cost modifier for a tile (sum of all active modifiers).
        /// </summary>
        public float GetTotalModifier(Vector2Int tile)
        {
            if (!modifiers.ContainsKey(tile))
            {
                return 0f;
            }

            float total = 0f;
            foreach (var mod in modifiers[tile])
            {
                if (!mod.IsExpired)
                {
                    total += mod.costDelta;
                }
            }

            return total;
        }

        /// <summary>
        /// Gets all tiles currently affected by modifiers.
        /// </summary>
        public IEnumerable<Vector2Int> GetModifiedTiles()
        {
            return modifiers.Keys;
        }

        /// <summary>
        /// Clears all modifiers.
        /// </summary>
        public void ClearAll()
        {
            // Clear and reuse the buffer instead of creating new list
            _allTilesBuffer.Clear();
            _allTilesBuffer.AddRange(modifiers.Keys);
            modifiers.Clear();

            foreach (var tile in _allTilesBuffer)
            {
                ApplyModifierToGrid(tile);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies the total cost modifier for a tile to the MazeGrid's attraction value.
        /// Negative costDelta = more attractive (reduces cost), positive = less attractive (increases cost).
        /// </summary>
        private void ApplyModifierToGrid(Vector2Int tile)
        {
            if (gridBehaviour == null || gridBehaviour.Grid == null)
            {
                return;
            }

            var node = gridBehaviour.Grid.GetNode(tile.x, tile.y);
            if (node == null)
            {
                return;
            }

            // Reset attraction to 0, then apply total modifier
            // (assumes no other systems are directly modifying attraction outside of this system)
            float oldAttraction = node.attraction;
            node.attraction = 0f;

            if (modifiers.ContainsKey(tile))
            {
                float total = GetTotalModifier(tile);
                // Convert costDelta to attraction: negative cost = positive attraction
                node.attraction = -total;

                // Debug: Log significant attraction changes
                if (Mathf.Abs(total) > 10.0f)
                {
                }
            }
        }

        #endregion
    }
}

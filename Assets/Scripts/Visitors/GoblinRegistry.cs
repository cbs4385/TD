using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Centralized registry for all active Goblins in the scene.
    /// Maintains a cached list to avoid expensive FindObjectsByType calls.
    /// Goblins automatically register/unregister themselves on Enable/Disable.
    /// </summary>
    public static class GoblinRegistry
    {
        private static readonly List<GoblinController> _allGoblins = new List<GoblinController>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets a read-only list of all active Goblins.
        /// This is much more efficient than Object.FindObjectsByType.
        /// </summary>
        public static IReadOnlyList<GoblinController> All
        {
            get
            {
                lock (_lock)
                {
                    return _allGoblins.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the current count of registered Goblins.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _allGoblins.Count;
                }
            }
        }

        /// <summary>
        /// Registers a Goblin with the registry.
        /// Called automatically by GoblinController.OnEnable().
        /// </summary>
        internal static void Register(GoblinController goblin)
        {
            if (goblin == null)
            {
                return;
            }

            lock (_lock)
            {
                if (!_allGoblins.Contains(goblin))
                {
                    _allGoblins.Add(goblin);
                }
            }
        }

        /// <summary>
        /// Unregisters a Goblin from the registry.
        /// Called automatically by GoblinController.OnDisable().
        /// </summary>
        internal static void Unregister(GoblinController goblin)
        {
            if (goblin == null)
            {
                return;
            }

            lock (_lock)
            {
                _allGoblins.Remove(goblin);
            }
        }

        /// <summary>
        /// Clears all registered Goblins.
        /// Useful for scene transitions or cleanup.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _allGoblins.Clear();
            }
        }
    }
}

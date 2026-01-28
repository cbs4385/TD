using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Centralized registry for all active RedCaps in the scene.
    /// Maintains a cached list to avoid expensive FindObjectsByType calls.
    /// RedCaps automatically register/unregister themselves on Enable/Disable.
    /// </summary>
    public static class RedCapRegistry
    {
        private static readonly List<RedCapController> _allRedCaps = new List<RedCapController>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets a read-only list of all active RedCaps.
        /// This is much more efficient than Object.FindObjectsByType.
        /// </summary>
        public static IReadOnlyList<RedCapController> All
        {
            get
            {
                lock (_lock)
                {
                    return _allRedCaps.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the current count of registered RedCaps.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _allRedCaps.Count;
                }
            }
        }

        /// <summary>
        /// Registers a RedCap with the registry.
        /// Called automatically by RedCapController.OnEnable().
        /// </summary>
        internal static void Register(RedCapController redCap)
        {
            if (redCap == null)
            {
                return;
            }

            lock (_lock)
            {
                if (!_allRedCaps.Contains(redCap))
                {
                    _allRedCaps.Add(redCap);
                }
            }
        }

        /// <summary>
        /// Unregisters a RedCap from the registry.
        /// Called automatically by RedCapController.OnDisable().
        /// </summary>
        internal static void Unregister(RedCapController redCap)
        {
            if (redCap == null)
            {
                return;
            }

            lock (_lock)
            {
                _allRedCaps.Remove(redCap);
            }
        }

        /// <summary>
        /// Clears all registered RedCaps.
        /// Useful for scene transitions or cleanup.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _allRedCaps.Clear();
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Centralized registry for all active visitors in the scene.
    /// Maintains a cached list to avoid expensive FindObjectsByType calls.
    /// Visitors automatically register/unregister themselves on Enable/Disable.
    /// </summary>
    public static class VisitorRegistry
    {
        private static readonly List<VisitorControllerBase> _allVisitors = new List<VisitorControllerBase>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets a read-only list of all active visitors.
        /// This is much more efficient than Object.FindObjectsByType.
        /// </summary>
        public static IReadOnlyList<VisitorControllerBase> All
        {
            get
            {
                lock (_lock)
                {
                    return _allVisitors.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the current count of registered visitors.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _allVisitors.Count;
                }
            }
        }

        /// <summary>
        /// Registers a visitor with the registry.
        /// Called automatically by VisitorControllerBase.OnEnable().
        /// </summary>
        internal static void Register(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                Debug.LogWarning("[VisitorRegistry] Attempted to register null visitor");
                return;
            }

            lock (_lock)
            {
                if (!_allVisitors.Contains(visitor))
                {
                    _allVisitors.Add(visitor);
                }
            }
        }

        /// <summary>
        /// Unregisters a visitor from the registry.
        /// Called automatically by VisitorControllerBase.OnDisable().
        /// </summary>
        internal static void Unregister(VisitorControllerBase visitor)
        {
            if (visitor == null)
            {
                return;
            }

            lock (_lock)
            {
                _allVisitors.Remove(visitor);
            }
        }

        /// <summary>
        /// Clears all registered visitors.
        /// Useful for scene transitions or cleanup.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _allVisitors.Clear();
            }
        }

        /// <summary>
        /// Validates the registry and removes any null entries.
        /// This is a safety measure in case a visitor is destroyed without proper cleanup.
        /// </summary>
        public static void ValidateRegistry()
        {
            lock (_lock)
            {
                _allVisitors.RemoveAll(v => v == null);
            }
        }
    }
}

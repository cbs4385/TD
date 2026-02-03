using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Base class for components that need to maintain a static registry of all active instances.
    /// Eliminates boilerplate for the common pattern of tracking instances in a HashSet.
    ///
    /// Usage:
    /// - Inherit from RegistryComponent&lt;T&gt; where T is your component type
    /// - Access all active instances via T.All or T.ActiveInstances
    /// - Override OnRegistered/OnUnregistered for custom behavior
    /// </summary>
    /// <typeparam name="T">The concrete component type</typeparam>
    public abstract class RegistryComponent<T> : MonoBehaviour where T : RegistryComponent<T>
    {
        private static readonly HashSet<T> _activeInstances = new HashSet<T>();

        /// <summary>
        /// Gets all currently active instances of this component type.
        /// Returns a read-only view of the internal collection.
        /// </summary>
        public static IReadOnlyCollection<T> All => _activeInstances;

        /// <summary>
        /// Gets the count of active instances.
        /// </summary>
        public static int ActiveCount => _activeInstances.Count;

        /// <summary>
        /// Checks if any instances are currently active.
        /// </summary>
        public static bool HasActiveInstances => _activeInstances.Count > 0;

        /// <summary>
        /// Finds the nearest instance to a given position.
        /// Returns null if no instances are active.
        /// </summary>
        /// <param name="position">The position to measure from</param>
        /// <returns>The nearest instance, or null if none exist</returns>
        public static T FindNearest(Vector3 position)
        {
            T nearest = null;
            float nearestDistSq = float.MaxValue;

            foreach (var instance in _activeInstances)
            {
                if (instance == null) continue;

                float distSq = (instance.transform.position - position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = instance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Finds all instances within a given radius of a position.
        /// </summary>
        /// <param name="position">The center position</param>
        /// <param name="radius">The search radius</param>
        /// <returns>List of instances within the radius</returns>
        public static List<T> FindWithinRadius(Vector3 position, float radius)
        {
            var results = new List<T>();
            float radiusSq = radius * radius;

            foreach (var instance in _activeInstances)
            {
                if (instance == null) continue;

                float distSq = (instance.transform.position - position).sqrMagnitude;
                if (distSq <= radiusSq)
                {
                    results.Add(instance);
                }
            }

            return results;
        }

        protected virtual void OnEnable()
        {
            _activeInstances.Add((T)this);
            OnRegistered();
        }

        protected virtual void OnDisable()
        {
            _activeInstances.Remove((T)this);
            OnUnregistered();
        }

        /// <summary>
        /// Called after this instance is registered in the static collection.
        /// Override to add custom behavior when the component becomes active.
        /// </summary>
        protected virtual void OnRegistered() { }

        /// <summary>
        /// Called after this instance is unregistered from the static collection.
        /// Override to add custom behavior when the component becomes inactive.
        /// </summary>
        protected virtual void OnUnregistered() { }

        /// <summary>
        /// Clears all registered instances. Use with caution - primarily for testing or scene transitions.
        /// </summary>
        public static void ClearAll()
        {
            _activeInstances.Clear();
        }
    }
}

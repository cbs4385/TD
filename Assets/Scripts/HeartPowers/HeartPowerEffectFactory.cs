using System;
using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Factory for creating heart power effect instances.
    /// Replaces the switch statement in HeartPowerManager with a dictionary-based dispatch.
    /// </summary>
    public class HeartPowerEffectFactory
    {
        /// <summary>
        /// Delegate type for creating power effects.
        /// </summary>
        public delegate ActivePowerEffect EffectCreator(
            HeartPowerManager manager,
            HeartPowerDefinition definition,
            Vector3 worldPosition);

        private readonly Dictionary<HeartPowerType, EffectCreator> _factories;

        /// <summary>
        /// Creates a new factory with default effect registrations.
        /// </summary>
        public HeartPowerEffectFactory()
        {
            _factories = new Dictionary<HeartPowerType, EffectCreator>();
            RegisterDefaultEffects();
        }

        /// <summary>
        /// Registers all default heart power effects.
        /// </summary>
        private void RegisterDefaultEffects()
        {
            // Active powers (currently enabled)
            Register(HeartPowerType.MurmuringPaths, (m, d, p) => new MurmuringPathsEffect(m, d, p));
            Register(HeartPowerType.HeartwardGrasp, (m, d, p) => new HeartwardGraspEffect(m, d, p));
            Register(HeartPowerType.DevouringMaw, (m, d, p) => new DevouringMawEffect(m, d, p));
            Register(HeartPowerType.Sculpting, (m, d, p) => new SculptingEffect(m, d, p));
            Register(HeartPowerType.Misdirect, (m, d, p) => new MisdirectEffect(m, d, p));

        }

        /// <summary>
        /// Registers a factory for a specific power type.
        /// </summary>
        /// <param name="powerType">The power type to register</param>
        /// <param name="creator">The factory delegate that creates effect instances</param>
        public void Register(HeartPowerType powerType, EffectCreator creator)
        {
            _factories[powerType] = creator;
        }

        /// <summary>
        /// Unregisters a factory for a specific power type.
        /// </summary>
        /// <param name="powerType">The power type to unregister</param>
        public void Unregister(HeartPowerType powerType)
        {
            _factories.Remove(powerType);
        }

        /// <summary>
        /// Checks if a factory is registered for the specified power type.
        /// </summary>
        public bool IsRegistered(HeartPowerType powerType)
        {
            return _factories.ContainsKey(powerType);
        }

        /// <summary>
        /// Creates a power effect instance for the specified power type.
        /// </summary>
        /// <param name="powerType">The type of power to create</param>
        /// <param name="manager">The HeartPowerManager instance</param>
        /// <param name="definition">The power definition</param>
        /// <param name="worldPosition">The world position for the effect</param>
        /// <returns>The created effect, or null if no factory is registered</returns>
        public ActivePowerEffect Create(
            HeartPowerType powerType,
            HeartPowerManager manager,
            HeartPowerDefinition definition,
            Vector3 worldPosition)
        {
            if (_factories.TryGetValue(powerType, out var creator))
            {
                return creator(manager, definition, worldPosition);
            }

            return null;
        }

        /// <summary>
        /// Gets all registered power types.
        /// </summary>
        public IEnumerable<HeartPowerType> RegisteredPowerTypes => _factories.Keys;
    }
}

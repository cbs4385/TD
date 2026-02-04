using System;
using System.Collections.Generic;
using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Props
{
    /// <summary>
    /// Factory for spawning node props.
    /// Replaces the switch statement dispatch in DynamicMazeGrowth with a dictionary-based approach.
    /// </summary>
    public class NodePropFactory
    {
        /// <summary>
        /// Delegate type for prop spawning functions.
        /// </summary>
        /// <param name="node">The maze node to spawn at</param>
        /// <param name="nodeIndex">The index of the node</param>
        /// <returns>True if spawn was successful</returns>
        public delegate bool PropSpawner(ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex);

        private readonly Dictionary<DynamicMazeGrowth.NodePropType, PropSpawner> _spawners;

        /// <summary>
        /// Creates a new factory. Call RegisterSpawners after construction with DynamicMazeGrowth methods.
        /// </summary>
        public NodePropFactory()
        {
            _spawners = new Dictionary<DynamicMazeGrowth.NodePropType, PropSpawner>();
        }

        /// <summary>
        /// Registers all spawner methods from DynamicMazeGrowth.
        /// Must be called after DynamicMazeGrowth is initialized.
        /// </summary>
        public void RegisterSpawners(
            PropSpawner pondSpawner,
            PropSpawner fairyRingSpawner,
            PropSpawner lanternSpawner,
            PropSpawner wispSpawner,
            PropSpawner pukaSpawner)
        {
            _spawners[DynamicMazeGrowth.NodePropType.Pond] = pondSpawner;
            _spawners[DynamicMazeGrowth.NodePropType.FairyRing] = fairyRingSpawner;
            _spawners[DynamicMazeGrowth.NodePropType.FaeLantern] = lanternSpawner;
            _spawners[DynamicMazeGrowth.NodePropType.WillowTheWisp] = wispSpawner;
            _spawners[DynamicMazeGrowth.NodePropType.Puka] = pukaSpawner;
        }

        /// <summary>
        /// Registers a single spawner for a prop type.
        /// </summary>
        public void Register(DynamicMazeGrowth.NodePropType propType, PropSpawner spawner)
        {
            _spawners[propType] = spawner;
        }

        /// <summary>
        /// Checks if a spawner is registered for the given prop type.
        /// </summary>
        public bool IsRegistered(DynamicMazeGrowth.NodePropType propType)
        {
            return _spawners.ContainsKey(propType);
        }

        /// <summary>
        /// Spawns a prop at the specified node.
        /// </summary>
        /// <param name="propType">The type of prop to spawn</param>
        /// <param name="node">The maze node to spawn at</param>
        /// <param name="nodeIndex">The index of the node</param>
        /// <returns>True if spawn was successful</returns>
        public bool Spawn(DynamicMazeGrowth.NodePropType propType, ForestMaze.PlanarForestMazeGenerator.Node node, int nodeIndex)
        {
            if (_spawners.TryGetValue(propType, out var spawner))
            {
                return spawner(node, nodeIndex);
            }

            return false;
        }

        /// <summary>
        /// Gets all registered prop types.
        /// </summary>
        public IEnumerable<DynamicMazeGrowth.NodePropType> RegisteredPropTypes => _spawners.Keys;
    }
}

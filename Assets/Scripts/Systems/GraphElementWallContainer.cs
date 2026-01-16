using System.Collections.Generic;
using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Container for walls belonging to a specific graph element (node or edge).
    /// Walls are created as children of this container, establishing clear ownership.
    /// Only this container can trigger wall collision checks for its children.
    /// </summary>
    public class GraphElementWallContainer : MonoBehaviour
    {
        public enum ElementType { Node, Edge }

        [SerializeField] private ElementType elementType;
        [SerializeField] private int elementId;

        private List<WallCollisionChecker> childWalls = new List<WallCollisionChecker>();

        public ElementType Type => elementType;
        public int ElementId => elementId;

        /// <summary>
        /// Initialize this container for a node.
        /// </summary>
        public void InitializeForNode(int nodeId, Vector2 position)
        {
            elementType = ElementType.Node;
            elementId = nodeId;
            gameObject.name = $"NodeWalls_{nodeId}";
            transform.position = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>
        /// Initialize this container for an edge.
        /// </summary>
        public void InitializeForEdge(int edgeIndex, Vector2 startPos, Vector2 endPos)
        {
            elementType = ElementType.Edge;
            elementId = edgeIndex;
            Vector2 center = (startPos + endPos) * 0.5f;
            gameObject.name = $"EdgeWalls_{edgeIndex}";
            transform.position = new Vector3(center.x, center.y, 0f);
        }

        /// <summary>
        /// Register a wall as belonging to this container.
        /// Called automatically by WallCollisionChecker when it's added to a wall.
        /// </summary>
        public void RegisterWall(WallCollisionChecker wall)
        {
            if (!childWalls.Contains(wall))
            {
                childWalls.Add(wall);
            }
        }

        /// <summary>
        /// Unregister a wall from this container.
        /// Called automatically by WallCollisionChecker when destroyed.
        /// </summary>
        public void UnregisterWall(WallCollisionChecker wall)
        {
            childWalls.Remove(wall);
        }

        /// <summary>
        /// Trigger collision checks for all walls in this container.
        /// This is the ONLY valid way to remove walls - they check against paths/nodes.
        /// </summary>
        public void TriggerWallCollisionChecks()
        {
            // Sync transforms and run a physics step so newly created colliders are detected
            Physics.SyncTransforms();
            Physics.Simulate(Time.fixedDeltaTime);

            // Iterate backwards since walls may be destroyed during iteration
            for (int i = childWalls.Count - 1; i >= 0; i--)
            {
                if (childWalls[i] != null)
                {
                    childWalls[i].CheckAndDestroyIfColliding();
                }
            }
        }

        /// <summary>
        /// Get the number of walls currently in this container.
        /// </summary>
        public int WallCount => childWalls.Count;
    }
}

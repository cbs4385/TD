using UnityEngine;

namespace FaeMaze.Visitors
{
    /// <summary>
    /// Base class for visitor controllers to allow polymorphic handling of visitor behaviors.
    /// </summary>
    public abstract class VisitorControllerBase : MonoBehaviour
    {
        public enum VisitorState
        {
            Idle,
            Walking,
            Consumed,
            Escaping
        }

        public abstract VisitorState State { get; }

        public abstract float MoveSpeed { get; }

        public abstract bool IsEntranced { get; }

        public abstract float SpeedMultiplier { get; set; }

        public abstract bool IsFascinated { get; }
    }
}

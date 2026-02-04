using UnityEngine;

namespace FaeMaze.Utilities
{
    /// <summary>
    /// Extension methods for common GameObject operations.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Adds a Rigidbody configured as kinematic (no gravity).
        /// If a Rigidbody already exists, configures it as kinematic.
        /// </summary>
        public static Rigidbody AddKinematicRigidbody(this GameObject go)
        {
            // Cannot use ?? with Unity objects — GetComponent returns a
            // "fake null" that overrides == but is not C# null.
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
            return rb;
        }
    }
}

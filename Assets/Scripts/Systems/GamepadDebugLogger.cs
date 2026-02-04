using UnityEngine;
using UnityEngine.InputSystem;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Attach to any GameObject in the game scene to continuously monitor gamepad input.
    /// Logs raw gamepad state whenever any button/stick is active.
    /// Also logs gamepad connect/disconnect events.
    ///
    /// DELETE THIS COMPONENT when gamepad debugging is complete.
    /// </summary>
    public class GamepadDebugLogger : MonoBehaviour
    {
        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void Update()
        {
        }
    }
}

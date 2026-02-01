using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.HeartPowers
{
    /// <summary>
    /// Handles trigger events for the DevouringMaw effect.
    /// Attached to the Detect child of the devour prefab.
    /// Catches visitors that enter/stay within the maw during the devour cycle.
    /// </summary>
    public class DevourTriggerHandler : MonoBehaviour
    {
        private DevouringMawEffect ownerEffect;

        /// <summary>
        /// Sets the DevouringMawEffect that owns this trigger handler.
        /// Called when the devour visual is instantiated.
        /// </summary>
        public void SetOwner(DevouringMawEffect effect)
        {
            ownerEffect = effect;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryCapture(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryCapture(other);
        }

        private void TryCapture(Collider other)
        {
            if (ownerEffect == null)
            {
                Debug.Log($"[DevourTriggerHandler] TryCapture: ownerEffect is null");
                return;
            }

            // Check if this is a visitor (layer 6 = Visitor)
            if (other.gameObject.layer == 6)
            {
                // Get the visitor controller from the parent (collider is on Detect child)
                VisitorControllerBase visitor = other.GetComponentInParent<VisitorControllerBase>();
                if (visitor != null)
                {
                    Debug.Log($"[DevourTriggerHandler] TryCapture: Attempting to capture {visitor.name}");
                    ownerEffect.NotifyVisitorEnteredMaw(visitor);
                }
                else
                {
                    Debug.Log($"[DevourTriggerHandler] TryCapture: No VisitorControllerBase found on {other.gameObject.name}");
                }
            }
        }
    }
}

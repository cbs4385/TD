using UnityEngine;
using FaeMaze.Visitors;

namespace FaeMaze.Props
{
    /// <summary>
    /// Trigger component attached to a FaeLantern child object.
    /// Detects when visitor colliders enter the lantern's influence area
    /// and notifies the lantern to attempt fascination.
    /// Uses OnTriggerStay to also catch visitors already inside when the lantern spawns.
    /// </summary>
    public class LanternInfluenceTrigger : MonoBehaviour
    {
        private FaeLantern lantern;

        public void Initialize(FaeLantern lantern)
        {
            this.lantern = lantern;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryFascinate(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryFascinate(other);
        }

        private void TryFascinate(Collider other)
        {
            if (lantern == null) return;

            // Visitor "Detect" child is on layer 6
            if (other.gameObject.layer != 6) return;

            // VisitorControllerBase is on root, collider is on "Detect" child
            var visitor = other.GetComponentInParent<VisitorControllerBase>();
            if (visitor == null) return;

            lantern.OnVisitorEnteredInfluence(visitor);
        }
    }
}

using UnityEngine;
using System;

namespace ForestMaze
{
    /// <summary>
    /// Component attached to the TipTrigger object baked into the tongue prefab.
    /// Detects when the tip exits a trigger collider (the detection zone) via OnTriggerExit.
    ///
    /// IMPORTANT: This component is baked into the prefab by TonguePrefabColliderSetup.
    /// The collider is properly scaled to match the bone collider pattern (scale 0.002, radius 0.5).
    /// Do NOT create TipTrigger objects at runtime - they are pre-baked.
    /// </summary>
    public class TipTriggerHandler : MonoBehaviour
    {
        /// <summary>
        /// Fired when the tip exits a trigger collider.
        /// </summary>
        public event Action<Collider> OnTipExitedTrigger;

        /// <summary>
        /// The collider we're tracking (detection zone). Only fire events for this collider.
        /// Set this at runtime before the tongue starts extending.
        /// </summary>
        public Collider TrackedCollider { get; set; }

        private void OnTriggerExit(Collider other)
        {
            // Only fire if this is the collider we're tracking
            if (TrackedCollider != null && other == TrackedCollider)
            {
                OnTipExitedTrigger?.Invoke(other);
            }
        }
    }
}

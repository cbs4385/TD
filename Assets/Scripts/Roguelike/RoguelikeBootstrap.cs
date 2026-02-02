using UnityEngine;
using FaeMaze.UI;

namespace FaeMaze.Roguelike
{
    /// <summary>
    /// Ensures all roguelike managers are created at game startup.
    /// Attach to a GameObject in the MainMenu scene or use RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class RoguelikeBootstrap : MonoBehaviour
    {
        private static bool _initialized;

        /// <summary>
        /// Called automatically before any scene loads.
        /// Creates all roguelike managers if they don't exist.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_initialized) return;

            // Create a parent object for all roguelike managers
            GameObject roguelikeRoot = new GameObject("[RoguelikeManagers]");
            DontDestroyOnLoad(roguelikeRoot);

            // Create MetaProgressionManager if it doesn't exist
            if (MetaProgressionManager.Instance == null)
            {
                roguelikeRoot.AddComponent<MetaProgressionManager>();
            }

            // Create UnlockManager if it doesn't exist
            if (UnlockManager.Instance == null)
            {
                roguelikeRoot.AddComponent<UnlockManager>();
            }

            // Create PowerProgressionManager if it doesn't exist
            if (PowerProgressionManager.Instance == null)
            {
                roguelikeRoot.AddComponent<PowerProgressionManager>();
            }

            // Create TierUpgradeUI (scene-specific, will be recreated per scene)
            roguelikeRoot.AddComponent<TierUpgradeUI>();

            _initialized = true;
            Debug.Log("[RoguelikeBootstrap] Managers initialized");
        }

        /// <summary>
        /// Resets the initialization flag (for testing purposes).
        /// </summary>
        public static void ResetInitialization()
        {
            _initialized = false;
        }
    }
}

using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Generic base class for singleton MonoBehaviours.
    /// Eliminates boilerplate singleton pattern code across manager classes.
    ///
    /// Usage:
    ///   public class MyManager : SingletonMonoBehaviour&lt;MyManager&gt;
    ///   {
    ///       protected override void OnAwake() { /* your init code */ }
    ///   }
    ///
    /// Features:
    /// - Thread-safe lazy initialization
    /// - Optional DontDestroyOnLoad via persistAcrossScenes property
    /// - Automatic duplicate destruction
    /// - Virtual OnAwake/OnSingletonDestroyed hooks for subclass initialization
    /// </summary>
    /// <typeparam name="T">The concrete singleton type</typeparam>
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting;

        /// <summary>
        /// Gets the singleton instance. Returns null if the application is quitting.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    return null;
                }

                lock (_lock)
                {
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Returns true if an instance exists and the application is not quitting.
        /// Use this to safely check for instance existence without triggering warnings.
        /// </summary>
        public static bool HasInstance => !_applicationIsQuitting && _instance != null;

        /// <summary>
        /// Override to control whether this singleton persists across scene loads.
        /// Default is false (destroyed on scene change).
        /// </summary>
        protected virtual bool PersistAcrossScenes => false;

        /// <summary>
        /// Called after singleton initialization. Override instead of Awake().
        /// </summary>
        protected virtual void OnAwake() { }

        /// <summary>
        /// Called when a duplicate singleton is destroyed.
        /// </summary>
        protected virtual void OnDuplicateDestroyed() { }

        /// <summary>
        /// Called when the singleton instance is being destroyed.
        /// Override instead of OnDestroy() for cleanup.
        /// </summary>
        protected virtual void OnSingletonDestroyed() { }

        private void Awake()
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)this;

                    if (PersistAcrossScenes)
                    {
                        DontDestroyOnLoad(gameObject);
                    }

                    OnAwake();
                }
                else if (_instance != this)
                {
                    OnDuplicateDestroyed();
                    Destroy(gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            lock (_lock)
            {
                if (_instance == this)
                {
                    OnSingletonDestroyed();
                    _instance = null;
                }
            }
        }

        private void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        /// <summary>
        /// Resets the static state. Only call this for testing purposes.
        /// </summary>
        public static void ResetStatic()
        {
            lock (_lock)
            {
                _instance = null;
                _applicationIsQuitting = false;
            }
        }
    }

    /// <summary>
    /// Singleton that automatically persists across scene loads.
    /// Use this for managers that need to survive scene transitions.
    /// </summary>
    /// <typeparam name="T">The concrete singleton type</typeparam>
    public abstract class PersistentSingletonMonoBehaviour<T> : SingletonMonoBehaviour<T>
        where T : PersistentSingletonMonoBehaviour<T>
    {
        protected override bool PersistAcrossScenes => true;
    }
}

using UnityEngine;

namespace FaeMaze.Audio
{
    /// <summary>
    /// Centralized sound manager for handling sound effects across the game.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        #region Singleton

        /// <summary>Singleton instance of the SoundManager.</summary>
        public static SoundManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Audio Sources")]
        [SerializeField]
        [Tooltip("Audio source used for playing sound effects.")]
        private AudioSource sfxSource;

        [Header("Sound Effects")]
        [SerializeField]
        [Tooltip("Clip played when a visitor spawns.")]
        private AudioClip visitorSpawnClip;

        [SerializeField]
        [Tooltip("Clip played when a visitor is consumed by the heart.")]
        private AudioClip visitorConsumedClip;

        [SerializeField]
        [Tooltip("Clip played when a FaeLantern is placed.")]
        private AudioClip lanternPlacedClip;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();

                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (sfxSource != null)
            {
                sfxSource.playOnAwake = false;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>Plays the visitor spawn sound effect.</summary>
        public void PlayVisitorSpawn()
        {
            PlayClip(visitorSpawnClip);
        }

        /// <summary>Plays the visitor consumed sound effect.</summary>
        public void PlayVisitorConsumed()
        {
            PlayClip(visitorConsumedClip);
        }

        /// <summary>Plays the lantern placement sound effect.</summary>
        public void PlayLanternPlaced()
        {
            PlayClip(lanternPlacedClip);
        }

        #endregion

        #region Private Methods

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip);
        }

        #endregion
    }
}

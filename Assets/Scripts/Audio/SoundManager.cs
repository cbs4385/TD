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

        [SerializeField]
        [Tooltip("Audio source used for playing music or ambient loops.")]
        private AudioSource musicSource;

        [Header("Music & Ambience")]
        [SerializeField]
        [Tooltip("Looping ambient clip played on start.")]
        private AudioClip ambientLoopClip;

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

        private void Start()
        {
            if (ambientLoopClip == null)
            {
                return;
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            musicSource.loop = true;
            musicSource.clip = ambientLoopClip;
            musicSource.playOnAwake = false;
            musicSource.Play();
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

        /// <summary>Sets the volume for sound effects.</summary>
        /// <param name="volume">Volume between 0 and 1.</param>
        public void SetSfxVolume(float volume)
        {
            if (sfxSource != null)
            {
                sfxSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>Sets the volume for music or ambient audio.</summary>
        /// <param name="volume">Volume between 0 and 1.</param>
        public void SetMusicVolume(float volume)
        {
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(volume);
            }
        }

        /// <summary>Gets the current SFX volume.</summary>
        public float SfxVolume => sfxSource != null ? sfxSource.volume : 1f;

        /// <summary>Gets the current music or ambient volume.</summary>
        public float MusicVolume => musicSource != null ? musicSource.volume : 1f;

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

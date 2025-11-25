using UnityEngine;

namespace FaeMaze.Audio
{
    /// <summary>
    /// Centralized sound effect manager for common gameplay events.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [SerializeField]
        private AudioSource sfxSource;

        [Header("Sound Effects")]
        [SerializeField]
        private AudioClip visitorSpawnClip;

        [SerializeField]
        private AudioClip visitorConsumedClip;

        [SerializeField]
        private AudioClip lanternPlacedClip;

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
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void PlayVisitorSpawn()
        {
            PlayClip(visitorSpawnClip);
        }

        public void PlayVisitorConsumed()
        {
            PlayClip(visitorConsumedClip);
        }

        public void PlayLanternPlaced()
        {
            PlayClip(lanternPlacedClip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip);
        }
    }
}

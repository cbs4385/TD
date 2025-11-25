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

        private const int DefaultSampleRate = 44100;
        private const float DefaultClipDuration = 0.25f;

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

            EnsureDefaultClips();
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

        private void EnsureDefaultClips()
        {
            // Generate simple tone placeholders at runtime if clips are missing so gameplay
            // events always produce audible feedback even without committed audio assets.
            visitorSpawnClip ??= GenerateToneClip(523.25f); // C5
            visitorConsumedClip ??= GenerateToneClip(392f); // G4
            lanternPlacedClip ??= GenerateToneClip(659.25f); // E5
        }

        private AudioClip GenerateToneClip(float frequency)
        {
            int sampleCount = Mathf.CeilToInt(DefaultSampleRate * DefaultClipDuration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)DefaultSampleRate;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.1f;
            }

            AudioClip clip = AudioClip.Create($"Tone_{frequency:F0}", sampleCount, 1, DefaultSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

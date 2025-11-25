using UnityEngine;

namespace FaeMaze.Audio
{
    /// <summary>
    /// Centralized sound effect manager for common gameplay events.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        private const int DefaultSampleRate = 44100;
        private const float DefaultClipDuration = 0.25f;

        [SerializeField]
        private AudioSource sfxSource;

        [Header("Sound Effects")]
        [SerializeField]
        private AudioClip visitorSpawnClip;

        [SerializeField]
        private AudioClip visitorConsumedClip;

        [SerializeField]
        private AudioClip lanternPlacedClip;

        private bool initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            InitializeIfNeeded();
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;

            EnsureDefaultClips();

            initialized = true;
        }

        public static void PlayVisitorSpawn()
        {
            EnsureInstanceExists();
            Instance?.PlayClipWithFallback(ref Instance.visitorSpawnClip, 523.25f);
        }

        public static void PlayVisitorConsumed()
        {
            EnsureInstanceExists();
            Instance?.PlayClipWithFallback(ref Instance.visitorConsumedClip, 392f);
        }

        public static void PlayLanternPlaced()
        {
            EnsureInstanceExists();
            Instance?.PlayClipWithFallback(ref Instance.lanternPlacedClip, 659.25f);
        }

        private void PlayClipWithFallback(ref AudioClip clip, float fallbackFrequency)
        {
            if (clip == null)
            {
                clip = GenerateToneClip(fallbackFrequency);
            }

            PlayClip(clip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip);
        }

        private static void EnsureInstanceExists()
        {
            if (Instance != null)
            {
                return;
            }

            SoundManager existing = FindFirstObjectByType<SoundManager>();
            if (existing != null)
            {
                Instance = existing;
                Instance.InitializeIfNeeded();
                return;
            }

            GameObject soundManagerObject = new GameObject("SoundManager");
            Instance = soundManagerObject.AddComponent<SoundManager>();
            Instance.InitializeIfNeeded();
            DontDestroyOnLoad(soundManagerObject);
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

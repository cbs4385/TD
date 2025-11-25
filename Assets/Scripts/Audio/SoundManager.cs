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
                Debug.LogWarning("Duplicate SoundManager detected and destroyed.");
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
                Debug.Log("SoundManager already initialized.");
                return;
            }

            if (sfxSource == null)
            {
                sfxSource = GetComponent<AudioSource>();
                Debug.Log($"SoundManager: Retrieved AudioSource from GameObject: {sfxSource != null}");
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                Debug.Log("SoundManager: Added new AudioSource component.");
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
                Debug.LogWarning($"SoundManager: Missing clip for frequency {fallbackFrequency}, generating fallback.");
                clip = GenerateToneClip(fallbackFrequency);
            }

            PlayClip(clip);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip == null || sfxSource == null)
            {
                Debug.LogError($"SoundManager: Cannot play clip. Clip null: {clip == null}, AudioSource null: {sfxSource == null}");
                return;
            }

            Debug.Log($"SoundManager: Playing clip '{clip.name}' with length {clip.length:0.00}s.");
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
                Debug.Log("SoundManager: Found existing instance in scene.");
                Instance.InitializeIfNeeded();
                return;
            }

            GameObject soundManagerObject = new GameObject("SoundManager");
            Instance = soundManagerObject.AddComponent<SoundManager>();
            Debug.Log("SoundManager: Created new runtime instance.");
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
            Debug.Log($"SoundManager: Generated tone clip '{clip.name}' at {frequency}Hz with {sampleCount} samples.");
            return clip;
        }
    }
}

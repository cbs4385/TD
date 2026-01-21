using UnityEngine;
using FaeMaze.Systems;

namespace FaeMaze.Audio
{
    /// <summary>
    /// Handles localized 3D audio for props. Sound is audible when the camera focus is near
    /// the prop and fades out with distance. Each prop type has its own volume setting.
    /// </summary>
    public class PropAudioSource : MonoBehaviour
    {
        public enum PropSoundType
        {
            Lantern,
            FairyRing,
            Pond
        }

        #region Serialized Fields

        [Header("Audio Settings")]
        [SerializeField]
        [Tooltip("The audio clip to play (looping)")]
        private AudioClip audioClip;

        [SerializeField]
        [Tooltip("Type of prop sound - determines which volume setting to use")]
        private PropSoundType soundType = PropSoundType.Lantern;

        [SerializeField]
        [Tooltip("Maximum distance at which the sound is audible")]
        private float maxDistance = 10f;

        [SerializeField]
        [Tooltip("Distance at which the sound is at full volume")]
        private float minDistance = 2f;

        [SerializeField]
        [Tooltip("Only play when the prop is active/animating")]
        private bool requiresActiveState = true;

        #endregion

        #region Private Fields

        private AudioSource audioSource;
        private float baseVolume = 1f;
        private bool isPlaying = false;
        private bool wasActive = false;  // Track previous active state to detect transitions
        private int inactiveFrameCount = 0;  // Count frames of inactivity to bridge gaps
        private const int INACTIVE_FRAMES_BEFORE_STOP = 2;  // Wait this many frames before stopping
        private Transform cameraTransform;
        private Animator animator;
        private System.Func<bool> activeStateCallback;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupAudioSource();
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Start()
        {
            // Find the main camera
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            // Load the audio clip if not assigned
            LoadAudioClipIfNeeded();

            // Setup audio clip but don't start playing yet - wait for active state
            if (audioClip != null)
            {
                audioSource.clip = audioClip;
                audioSource.loop = true;
                // Don't play yet - UpdatePlaybackState will start it when active
            }
        }

        private void Update()
        {
            UpdatePlaybackState();
            UpdateVolume();
        }

        private void OnEnable()
        {
            // Reset state tracking when re-enabled
            wasActive = false;
            inactiveFrameCount = 0;
        }

        private void OnDisable()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
                isPlaying = false;
            }
            wasActive = false;
            inactiveFrameCount = 0;
        }

        #endregion

        #region Setup

        private void SetupAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Configure for 3D spatial audio
            audioSource.spatialBlend = 1f; // Full 3D
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.dopplerLevel = 0f; // Disable doppler for ambient sounds
        }

        private void LoadAudioClipIfNeeded()
        {
            if (audioClip != null) return;

#if UNITY_EDITOR
            string clipPath = soundType switch
            {
                PropSoundType.Lantern => "Assets/Audio/SFX/lantern.mp3",
                PropSoundType.FairyRing => "Assets/Audio/SFX/ring.mp3",
                PropSoundType.Pond => "Assets/Audio/SFX/puka.mp3",
                _ => null
            };

            if (!string.IsNullOrEmpty(clipPath))
            {
                audioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            }
#endif
        }

        #endregion

        #region Playback Control

        /// <summary>
        /// Manages starting and stopping audio based on active state.
        /// Starts audio from beginning when prop becomes active, stops when inactive.
        /// Uses frame counting to bridge brief gaps between visitors.
        /// </summary>
        private void UpdatePlaybackState()
        {
            if (audioSource == null || audioSource.clip == null) return;

            bool isActive = !requiresActiveState || IsAnimationActive();

            if (isActive)
            {
                // Reset inactive counter when active
                inactiveFrameCount = 0;

                // Start playing if not already playing
                if (!isPlaying)
                {
                    audioSource.time = 0f;
                    audioSource.Play();
                    isPlaying = true;
                }
            }
            else if (isPlaying)
            {
                // Currently playing but inactive - count frames before stopping
                inactiveFrameCount++;

                if (inactiveFrameCount >= INACTIVE_FRAMES_BEFORE_STOP)
                {
                    // Been inactive long enough - stop playing
                    audioSource.Stop();
                    isPlaying = false;
                    inactiveFrameCount = 0;
                }
                // Otherwise keep playing to bridge the gap
            }

            wasActive = isActive;
        }

        #endregion

        #region Volume Control

        private void UpdateVolume()
        {
            if (audioSource == null || !isPlaying) return;

            // Get the prop-specific volume from GameSettings
            float propVolume = GetPropVolume();

            // Get the master SFX volume
            float masterSfxVolume = GameSettings.SfxVolume;

            // Calculate distance-based volume
            float distanceVolume = CalculateDistanceVolume();

            // Final volume = prop volume * master volume * distance falloff
            float finalVolume = propVolume * masterSfxVolume * distanceVolume;
            audioSource.volume = finalVolume;
        }

        private float GetPropVolume()
        {
            return soundType switch
            {
                PropSoundType.Lantern => GameSettings.LanternVolume,
                PropSoundType.FairyRing => GameSettings.FairyRingVolume,
                PropSoundType.Pond => GameSettings.PondVolume,
                _ => 1f
            };
        }

        private bool IsAnimationActive()
        {
            // If a custom callback is set, use it to determine active state
            if (activeStateCallback != null)
            {
                return activeStateCallback();
            }

            // Fallback to animator-based detection
            if (animator == null) return true;

            // Check if animator speed is > 0 (playing)
            return animator.speed > 0f;
        }

        private float CalculateDistanceVolume()
        {
            if (cameraTransform == null)
            {
                // Try to find camera again
                if (Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }
                if (cameraTransform == null) return 0f;
            }

            float distance = Vector3.Distance(transform.position, cameraTransform.position);

            if (distance <= minDistance)
            {
                return 1f;
            }
            else if (distance >= maxDistance)
            {
                return 0f;
            }
            else
            {
                // Linear falloff between min and max distance
                return 1f - ((distance - minDistance) / (maxDistance - minDistance));
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the audio clip to play.
        /// </summary>
        public void SetAudioClip(AudioClip clip)
        {
            audioClip = clip;
            if (audioSource != null && isPlaying)
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
        }

        /// <summary>
        /// Sets the sound type for volume control.
        /// </summary>
        public void SetSoundType(PropSoundType type)
        {
            soundType = type;
        }

        /// <summary>
        /// Sets the max distance for sound falloff.
        /// </summary>
        public void SetMaxDistance(float distance)
        {
            maxDistance = distance;
            if (audioSource != null)
            {
                audioSource.maxDistance = distance;
            }
        }

        /// <summary>
        /// Sets a callback function that determines if the prop is actively interacting.
        /// When set, this overrides the default animator-based active state detection.
        /// The callback should return true when sound should play, false otherwise.
        /// </summary>
        public void SetActiveStateCallback(System.Func<bool> callback)
        {
            activeStateCallback = callback;
        }

        #endregion
    }
}

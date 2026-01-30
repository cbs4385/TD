using UnityEngine;
using TMPro;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Displays game information like the current seed and FPS counter.
    /// </summary>
    public class GameInfoDisplay : MonoBehaviour
    {
        [Header("Seed Display")]
        [SerializeField] private TextMeshProUGUI seedText;

        [Header("FPS Display")]
        [SerializeField] private TextMeshProUGUI fpsText;
        [SerializeField] private float fpsUpdateInterval = 0.5f;

        private float fpsTimer;
        private int frameCount;
        private float currentFps;

        private void Start()
        {
            // Display the seed used for this game session
            UpdateSeedDisplay();
        }

        private void Update()
        {
            UpdateFpsCounter();
        }

        private void UpdateSeedDisplay()
        {
            if (seedText == null) return;

            int seed = RandomManager.CurrentSeed;
            if (seed != 0)
            {
                seedText.text = $"Seed: {seed}";
            }
            else
            {
                // RandomManager might not be initialized yet, try again next frame
                Invoke(nameof(UpdateSeedDisplay), 0.1f);
            }
        }

        private void UpdateFpsCounter()
        {
            if (fpsText == null) return;

            frameCount++;
            fpsTimer += Time.unscaledDeltaTime;

            if (fpsTimer >= fpsUpdateInterval)
            {
                currentFps = frameCount / fpsTimer;
                fpsText.text = $"{currentFps:F0} FPS";

                // Color code based on performance
                if (currentFps >= 55f)
                {
                    fpsText.color = Color.green;
                }
                else if (currentFps >= 30f)
                {
                    fpsText.color = Color.yellow;
                }
                else
                {
                    fpsText.color = Color.red;
                }

                frameCount = 0;
                fpsTimer = 0f;
            }
        }
    }
}

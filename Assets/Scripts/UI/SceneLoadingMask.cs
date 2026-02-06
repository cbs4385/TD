using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using FaeMaze.Systems;

namespace FaeMaze.UI
{
    /// <summary>
    /// Full-screen black overlay shown immediately when the scene loads.
    /// Hides uninitialized UI until maze generation and all IReadyReporter components are ready.
    /// Fades out smoothly once everything is initialized.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class SceneLoadingMask : MonoBehaviour
    {
        private const float FADE_DURATION = 0.5f;
        private const int CANVAS_SORT_ORDER = 9999;

        private GameObject canvasObject;
        private Image blackImage;
        private DynamicMazeGrowth mazeGrowth;
        private IReadyReporter[] readyReporters;
        private bool mazeReady;
        private bool fadingOut;

        private void Awake()
        {
            // Create overlay canvas at highest sort order so it covers everything
            canvasObject = new GameObject("SceneLoadingMaskCanvas");
            canvasObject.transform.SetParent(transform);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CANVAS_SORT_ORDER;

            canvasObject.AddComponent<CanvasScaler>();

            // Create full-screen black image
            GameObject imageObj = new GameObject("BlackOverlay");
            imageObj.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = imageObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            blackImage = imageObj.AddComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = true;
        }

        private void Start()
        {
            mazeGrowth = FindFirstObjectByType<DynamicMazeGrowth>();

            if (mazeGrowth != null)
            {
                if (mazeGrowth.IsInitialGrowthComplete)
                {
                    mazeReady = true;
                }
                else
                {
                    mazeGrowth.OnInitialGrowthComplete += OnMazeReady;
                }
            }
            else
            {
                // No maze growth found — don't block on it
                mazeReady = true;
            }

            // Collect all IReadyReporter components in the scene
            readyReporters = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                as IReadyReporter[];

            // FindObjectsByType returns MonoBehaviour[], filter to IReadyReporter
            var allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var reporters = new System.Collections.Generic.List<IReadyReporter>();
            foreach (var mb in allMonoBehaviours)
            {
                if (mb is IReadyReporter reporter)
                {
                    reporters.Add(reporter);
                }
            }
            readyReporters = reporters.ToArray();
        }

        private void OnMazeReady()
        {
            mazeReady = true;
        }

        private void Update()
        {
            if (fadingOut) return;

            if (!mazeReady) return;

            // Check all ready reporters
            if (readyReporters != null)
            {
                foreach (var reporter in readyReporters)
                {
                    if (reporter == null) continue;
                    if (!reporter.IsReady) return;
                }
            }

            // All ready — start fade out
            fadingOut = true;
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            Color color = blackImage.color;

            while (elapsed < FADE_DURATION)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / FADE_DURATION);
                color.a = alpha;
                blackImage.color = color;
                yield return null;
            }

            // Clean up
            if (mazeGrowth != null)
            {
                mazeGrowth.OnInitialGrowthComplete -= OnMazeReady;
            }

            Destroy(canvasObject);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (mazeGrowth != null)
            {
                mazeGrowth.OnInitialGrowthComplete -= OnMazeReady;
            }
        }
    }
}

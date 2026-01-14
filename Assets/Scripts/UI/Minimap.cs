using UnityEngine;
using UnityEngine.UI;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;
using FaeMaze.Props;
using System.Collections.Generic;

namespace FaeMaze.UI
{
    /// <summary>
    /// Displays a minimap showing the entire maze, rotated so camera forward is up.
    /// Shows hazards (lanterns, pukas), visitors, and the heart.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Maze grid behaviour for bounds and coordinate conversion")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Size as percentage of smaller screen dimension (0.2 = 20%)")]
        [Range(0.05f, 0.5f)]
        private float sizePercent = 0.2f;

        [SerializeField]
        [Tooltip("Corner to place minimap in")]
        private Corner mapCorner = Corner.TopRight;

        [SerializeField]
        [Tooltip("Padding from screen edges in pixels")]
        private float edgePadding = 20f;

        [SerializeField]
        [Tooltip("Extra padding around maze bounds (world units)")]
        private float boundsPadding = 5f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background color")]
        private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        [SerializeField]
        [Tooltip("Border color")]
        private Color borderColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Heart of maze color")]
        private Color heartColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Visitor dot color")]
        private Color visitorColor = new Color(0.3f, 1f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Lantern dot color")]
        private Color lanternColor = new Color(1f, 0.9f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Puka hazard dot color")]
        private Color pukaColor = new Color(0.8f, 0.2f, 0.8f, 1f);

        [Header("Dot Sizes")]
        [SerializeField]
        [Tooltip("Heart dot size in pixels")]
        private float heartDotSize = 10f;

        [SerializeField]
        [Tooltip("Visitor dot size in pixels")]
        private float visitorDotSize = 4f;

        [SerializeField]
        [Tooltip("Hazard dot size in pixels")]
        private float hazardDotSize = 6f;

        public enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private Canvas canvas;
        private RectTransform minimapPanel;
        private RawImage backgroundImage;
        private RectTransform dotsContainer;
        private Camera mainCamera;
        private HeartOfTheMaze heart;

        private List<Image> visitorDots = new List<Image>();
        private List<Image> lanternDots = new List<Image>();
        private List<Image> pukaDots = new List<Image>();
        private Image heartDot;

        private Sprite circleSprite;
        private float currentMapSize;
        private float pixelsPerUnit = 1f;
        private Vector2 mazeCenter = Vector2.zero;
        private bool boundsInitialized = false;

        private void Awake()
        {
            mainCamera = Camera.main;
            circleSprite = CreateCircleSprite();
            CreateMinimapUI();
        }

        private void Start()
        {
            // Find maze grid if not assigned
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            // Find heart
            heart = FindFirstObjectByType<HeartOfTheMaze>();
            if (heart != null)
            {
                CreateHeartDot();
            }
        }

        private void CreateMinimapUI()
        {
            // Create canvas
            GameObject canvasObj = new GameObject("MinimapCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Create minimap panel
            GameObject panelObj = new GameObject("MinimapPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            minimapPanel = panelObj.AddComponent<RectTransform>();

            // Add background
            backgroundImage = panelObj.AddComponent<RawImage>();
            backgroundImage.color = backgroundColor;

            // Add border
            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(2, 2);

            // Create dots container (this will rotate with camera)
            GameObject dotsObj = new GameObject("DotsContainer");
            dotsObj.transform.SetParent(panelObj.transform, false);
            dotsContainer = dotsObj.AddComponent<RectTransform>();
            dotsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            dotsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            dotsContainer.sizeDelta = Vector2.zero;
            dotsContainer.anchoredPosition = Vector2.zero;

            UpdateMinimapSize();
        }

        private void CreateHeartDot()
        {
            GameObject dotObj = new GameObject("HeartDot");
            dotObj.transform.SetParent(dotsContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            // Anchor to center so anchoredPosition is relative to center
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(heartDotSize, heartDotSize);

            heartDot = dotObj.AddComponent<Image>();
            heartDot.color = heartColor;
            heartDot.sprite = circleSprite;
        }

        private Image CreateDot(string name, Color color, float size, Transform parent)
        {
            GameObject dotObj = new GameObject(name);
            dotObj.transform.SetParent(parent, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            // Anchor to center so anchoredPosition is relative to center
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(size, size);

            Image dot = dotObj.AddComponent<Image>();
            dot.color = color;
            dot.sprite = circleSprite;

            return dot;
        }

        private Sprite CreateCircleSprite()
        {
            int resolution = 32;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[resolution * resolution];

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    pixels[y * resolution + x] = distance <= radius ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
                if (mazeGridBehaviour == null)
                {
                    return;
                }
            }

            UpdateMinimapSize();
            UpdateScaleFromBounds();

            // Only update dots after bounds are initialized
            if (!boundsInitialized)
            {
                return;
            }

            UpdateRotation();
            UpdateHeartDot();
            UpdateVisitorDots();
            UpdateLanternDots();
            UpdatePukaDots();
        }

        private void UpdateMinimapSize()
        {
            if (minimapPanel == null)
            {
                return;
            }

            // Calculate size based on smaller screen dimension
            float smallerDimension = Mathf.Min(Screen.width, Screen.height);
            currentMapSize = smallerDimension * sizePercent;

            minimapPanel.sizeDelta = new Vector2(currentMapSize, currentMapSize);

            // Position based on corner
            Vector2 anchorMin, anchorMax, pivot, anchoredPosition;

            switch (mapCorner)
            {
                case Corner.TopRight:
                    anchorMin = anchorMax = pivot = new Vector2(1, 1);
                    anchoredPosition = new Vector2(-edgePadding, -edgePadding);
                    break;
                case Corner.TopLeft:
                    anchorMin = anchorMax = pivot = new Vector2(0, 1);
                    anchoredPosition = new Vector2(edgePadding, -edgePadding);
                    break;
                case Corner.BottomRight:
                    anchorMin = anchorMax = pivot = new Vector2(1, 0);
                    anchoredPosition = new Vector2(-edgePadding, edgePadding);
                    break;
                case Corner.BottomLeft:
                    anchorMin = anchorMax = pivot = new Vector2(0, 0);
                    anchoredPosition = new Vector2(edgePadding, edgePadding);
                    break;
                default:
                    anchorMin = anchorMax = pivot = new Vector2(1, 1);
                    anchoredPosition = new Vector2(-edgePadding, -edgePadding);
                    break;
            }

            minimapPanel.anchorMin = anchorMin;
            minimapPanel.anchorMax = anchorMax;
            minimapPanel.pivot = pivot;
            minimapPanel.anchoredPosition = anchoredPosition;
        }

        private void UpdateScaleFromBounds()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            var bounds = mazeGridBehaviour.WorldSpaceMazeData.WorldBounds;

            // Skip if bounds are invalid (zero size)
            if (bounds.size.x < 0.01f && bounds.size.y < 0.01f)
            {
                return;
            }

            // Calculate maze extent with padding
            float mazeWidth = bounds.size.x + boundsPadding * 2f;
            float mazeHeight = bounds.size.y + boundsPadding * 2f;
            float maxExtent = Mathf.Max(mazeWidth, mazeHeight);

            // Calculate pixels per world unit to fit entire maze
            if (maxExtent > 0.01f && currentMapSize > 0)
            {
                pixelsPerUnit = currentMapSize / maxExtent;
                boundsInitialized = true;
            }

            // Cache maze center for coordinate conversion
            mazeCenter = new Vector2(bounds.center.x, bounds.center.y);
        }

        private void UpdateRotation()
        {
            if (dotsContainer == null || mainCamera == null)
            {
                return;
            }

            // Get camera's forward direction projected onto XY plane (the world plane)
            // Camera looking along +X means forward2D = (1, 0)
            // We want that direction to appear as "up" on the minimap
            Vector3 camForward = mainCamera.transform.forward;
            Vector2 forward2D = new Vector2(camForward.x, camForward.y);

            // If camera is looking straight down (Z axis), use camera's up vector instead
            if (forward2D.sqrMagnitude < 0.01f)
            {
                Vector3 camUp = mainCamera.transform.up;
                forward2D = new Vector2(camUp.x, camUp.y);
            }

            forward2D = forward2D.normalized;

            // Calculate the angle from +Y axis to the camera forward direction
            // Atan2(x, y) gives angle from +Y axis, clockwise positive
            // If camera looks along +X, forward2D = (1, 0), angle = 90 degrees
            // We need to rotate the minimap content by this angle so +X appears as up
            float angle = Mathf.Atan2(forward2D.x, forward2D.y) * Mathf.Rad2Deg;

            // Rotate the dots container so camera forward direction points up
            dotsContainer.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void UpdateHeartDot()
        {
            if (heartDot == null || heart == null)
            {
                return;
            }

            Vector3 heartWorldPos = heart.transform.position;
            Vector2 minimapPos = WorldToMinimapPosition(heartWorldPos);
            heartDot.rectTransform.anchoredPosition = minimapPos;
            heartDot.gameObject.SetActive(true);
        }

        private void UpdateVisitorDots()
        {
            IReadOnlyList<VisitorControllerBase> activeVisitors = VisitorRegistry.All;

            // Ensure we have enough dots
            while (visitorDots.Count < activeVisitors.Count)
            {
                visitorDots.Add(CreateDot("VisitorDot", visitorColor, visitorDotSize, dotsContainer));
            }

            // Update each dot
            int visitorIndex = 0;
            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitorIndex >= visitorDots.Count)
                {
                    continue;
                }

                Image dot = visitorDots[visitorIndex];
                Vector2 minimapPos = WorldToMinimapPosition(visitor.transform.position);
                dot.rectTransform.anchoredPosition = minimapPos;
                dot.gameObject.SetActive(true);

                visitorIndex++;
            }

            // Hide unused dots
            for (int i = visitorIndex; i < visitorDots.Count; i++)
            {
                visitorDots[i].gameObject.SetActive(false);
            }
        }

        private void UpdateLanternDots()
        {
            var activeLanterns = FaeLantern.All;
            int count = 0;

            // Count active lanterns
            foreach (var lantern in activeLanterns)
            {
                if (lantern != null) count++;
            }

            // Ensure we have enough dots
            while (lanternDots.Count < count)
            {
                lanternDots.Add(CreateDot("LanternDot", lanternColor, hazardDotSize, dotsContainer));
            }

            // Update each dot
            int lanternIndex = 0;
            foreach (var lantern in activeLanterns)
            {
                if (lantern == null || lanternIndex >= lanternDots.Count)
                {
                    continue;
                }

                Image dot = lanternDots[lanternIndex];
                Vector2 minimapPos = WorldToMinimapPosition(lantern.transform.position);
                dot.rectTransform.anchoredPosition = minimapPos;
                dot.gameObject.SetActive(true);

                lanternIndex++;
            }

            // Hide unused dots
            for (int i = lanternIndex; i < lanternDots.Count; i++)
            {
                lanternDots[i].gameObject.SetActive(false);
            }
        }

        private void UpdatePukaDots()
        {
            var activePukas = PukaHazard.All;

            // Ensure we have enough dots
            while (pukaDots.Count < activePukas.Count)
            {
                pukaDots.Add(CreateDot("PukaDot", pukaColor, hazardDotSize, dotsContainer));
            }

            // Update each dot
            int pukaIndex = 0;
            foreach (var puka in activePukas)
            {
                if (puka == null || pukaIndex >= pukaDots.Count)
                {
                    continue;
                }

                Image dot = pukaDots[pukaIndex];
                Vector2 minimapPos = WorldToMinimapPosition(puka.transform.position);
                dot.rectTransform.anchoredPosition = minimapPos;
                dot.gameObject.SetActive(true);

                pukaIndex++;
            }

            // Hide unused dots
            for (int i = pukaIndex; i < pukaDots.Count; i++)
            {
                pukaDots[i].gameObject.SetActive(false);
            }
        }

        private Vector2 WorldToMinimapPosition(Vector3 worldPos)
        {
            if (minimapPanel == null)
            {
                return Vector2.zero;
            }

            // Get position relative to maze center
            float relativeX = worldPos.x - mazeCenter.x;
            float relativeY = worldPos.y - mazeCenter.y;

            // Convert to minimap pixels
            float minimapX = relativeX * pixelsPerUnit;
            float minimapY = relativeY * pixelsPerUnit;

            return new Vector2(minimapX, minimapY);
        }
    }
}

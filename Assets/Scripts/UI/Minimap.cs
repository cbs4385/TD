using UnityEngine;
using UnityEngine.UI;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;
using System.Collections.Generic;

namespace FaeMaze.UI
{
    /// <summary>
    /// Displays a minimap in the upper corner showing the focal point, heart, and visitors.
    /// Shows a 10 tile radius around the focal point with color-coded dots.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Focal point transform to center the map on")]
        private Transform focalPoint;

        [SerializeField]
        [Tooltip("Maze grid behaviour for coordinate conversion")]
        private MazeGridBehaviour mazeGridBehaviour;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Size as percentage of smaller screen dimension (0.1 = 10%)")]
        [Range(0.05f, 0.3f)]
        private float sizePercent = 0.1f;

        [SerializeField]
        [Tooltip("View radius in tiles")]
        private float viewRadiusTiles = 10f;

        [SerializeField]
        [Tooltip("Corner to place minimap in")]
        private Corner mapCorner = Corner.TopRight;

        [SerializeField]
        [Tooltip("Padding from screen edges in pixels")]
        private float edgePadding = 20f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background color")]
        private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        [SerializeField]
        [Tooltip("Border color")]
        private Color borderColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Focal point crosshair color")]
        private Color crosshairColor = Color.white;

        [SerializeField]
        [Tooltip("Heart of maze color")]
        private Color heartColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Visitor dot color")]
        private Color visitorColor = new Color(0.3f, 1f, 0.3f, 1f);

        [Header("Dot Sizes")]
        [SerializeField]
        [Tooltip("Heart dot size in pixels")]
        private float heartDotSize = 8f;

        [SerializeField]
        [Tooltip("Visitor dot size in pixels")]
        private float visitorDotSize = 4f;

        [SerializeField]
        [Tooltip("Crosshair size in pixels")]
        private float crosshairSize = 10f;

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
        private Image heartDot;
        private RectTransform crosshair;

        private void Awake()
        {
            mainCamera = Camera.main;
            CreateMinimapUI();
        }

        private void Start()
        {
            // Find focal point if not assigned
            if (focalPoint == null)
            {
                GameObject focalPointObj = GameObject.Find("Focal Point");
                if (focalPointObj != null)
                {
                    focalPoint = focalPointObj.transform;
                }
                else if (mainCamera != null)
                {
                    focalPoint = mainCamera.transform;
                }
            }

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
            canvas.sortingOrder = 100; // Render on top

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

            // Create dots container
            GameObject dotsObj = new GameObject("DotsContainer");
            dotsObj.transform.SetParent(panelObj.transform, false);
            dotsContainer = dotsObj.AddComponent<RectTransform>();
            dotsContainer.anchorMin = Vector2.zero;
            dotsContainer.anchorMax = Vector2.one;
            dotsContainer.sizeDelta = Vector2.zero;
            dotsContainer.anchoredPosition = Vector2.zero;

            // Create crosshair
            CreateCrosshair();

            UpdateMinimapSize();
        }

        private void CreateCrosshair()
        {
            GameObject crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(dotsContainer, false);
            crosshair = crosshairObj.AddComponent<RectTransform>();
            crosshair.sizeDelta = new Vector2(crosshairSize, crosshairSize);

            // Horizontal line
            GameObject hLineObj = new GameObject("HorizontalLine");
            hLineObj.transform.SetParent(crosshairObj.transform, false);
            RectTransform hLineRect = hLineObj.AddComponent<RectTransform>();
            hLineRect.anchorMin = new Vector2(0.5f, 0.5f);
            hLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            hLineRect.sizeDelta = new Vector2(crosshairSize, 2f);
            Image hLineImg = hLineObj.AddComponent<Image>();
            hLineImg.color = crosshairColor;

            // Vertical line
            GameObject vLineObj = new GameObject("VerticalLine");
            vLineObj.transform.SetParent(crosshairObj.transform, false);
            RectTransform vLineRect = vLineObj.AddComponent<RectTransform>();
            vLineRect.anchorMin = new Vector2(0.5f, 0.5f);
            vLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            vLineRect.sizeDelta = new Vector2(2f, crosshairSize);
            Image vLineImg = vLineObj.AddComponent<Image>();
            vLineImg.color = crosshairColor;
        }

        private void CreateHeartDot()
        {
            GameObject dotObj = new GameObject("HeartDot");
            dotObj.transform.SetParent(dotsContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(heartDotSize, heartDotSize);

            heartDot = dotObj.AddComponent<Image>();
            heartDot.color = heartColor;

            // Make it circular
            heartDot.sprite = CreateCircleSprite();
        }

        private Image CreateVisitorDot()
        {
            GameObject dotObj = new GameObject("VisitorDot");
            dotObj.transform.SetParent(dotsContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(visitorDotSize, visitorDotSize);

            Image dot = dotObj.AddComponent<Image>();
            dot.color = visitorColor;
            dot.sprite = CreateCircleSprite();

            return dot;
        }

        private Sprite CreateCircleSprite()
        {
            // Create a simple circle texture
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
            if (focalPoint == null || mazeGridBehaviour == null)
            {
                return;
            }

            UpdateMinimapSize();
            UpdateHeartDot();
            UpdateVisitorDots();
            UpdateCrosshair();
        }

        private void UpdateMinimapSize()
        {
            if (minimapPanel == null)
            {
                return;
            }

            // Calculate size based on smaller screen dimension
            float smallerDimension = Mathf.Min(Screen.width, Screen.height);
            float mapSize = smallerDimension * sizePercent;

            minimapPanel.sizeDelta = new Vector2(mapSize, mapSize);

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

        private void UpdateHeartDot()
        {
            if (heartDot == null || heart == null)
            {
                return;
            }

            Vector3 heartWorldPos = heart.transform.position;
            Vector2 minimapPos = WorldToMinimapPosition(heartWorldPos);

            // Check if in view radius
            if (IsInViewRadius(heartWorldPos))
            {
                heartDot.gameObject.SetActive(true);
                heartDot.rectTransform.anchoredPosition = minimapPos;
            }
            else
            {
                heartDot.gameObject.SetActive(false);
            }
        }

        private void UpdateVisitorDots()
        {
            // Get all active visitors
            List<VisitorControllerBase> activeVisitors = VisitorRegistry.All;

            // Ensure we have enough dots
            while (visitorDots.Count < activeVisitors.Count)
            {
                visitorDots.Add(CreateVisitorDot());
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
                Vector3 visitorWorldPos = visitor.transform.position;

                if (IsInViewRadius(visitorWorldPos))
                {
                    dot.gameObject.SetActive(true);
                    Vector2 minimapPos = WorldToMinimapPosition(visitorWorldPos);
                    dot.rectTransform.anchoredPosition = minimapPos;
                }
                else
                {
                    dot.gameObject.SetActive(false);
                }

                visitorIndex++;
            }

            // Hide unused dots
            for (int i = visitorIndex; i < visitorDots.Count; i++)
            {
                visitorDots[i].gameObject.SetActive(false);
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshair == null)
            {
                return;
            }

            // Crosshair is always at center (focal point)
            crosshair.anchoredPosition = Vector2.zero;
        }

        private Vector2 WorldToMinimapPosition(Vector3 worldPos)
        {
            if (focalPoint == null || mazeGridBehaviour == null || minimapPanel == null)
            {
                return Vector2.zero;
            }

            // Get positions relative to focal point
            Vector3 focalWorldPos = focalPoint.position;
            Vector3 relativePos = worldPos - focalWorldPos;

            // Convert to tiles (only X and Y matter for top-down view)
            float tileSize = mazeGridBehaviour.TileSize;
            float relativeX = relativePos.x / tileSize;
            float relativeY = relativePos.y / tileSize;

            // Convert to minimap pixels
            float mapSize = minimapPanel.rect.width;
            float pixelsPerTile = mapSize / (viewRadiusTiles * 2f);

            float minimapX = relativeX * pixelsPerTile;
            float minimapY = relativeY * pixelsPerTile;

            return new Vector2(minimapX, minimapY);
        }

        private bool IsInViewRadius(Vector3 worldPos)
        {
            if (focalPoint == null || mazeGridBehaviour == null)
            {
                return false;
            }

            Vector3 focalWorldPos = focalPoint.position;
            Vector3 relativePos = worldPos - focalWorldPos;

            float tileSize = mazeGridBehaviour.TileSize;
            float distanceInTiles = Mathf.Sqrt(
                (relativePos.x * relativePos.x + relativePos.y * relativePos.y) / (tileSize * tileSize)
            );

            return distanceInTiles <= viewRadiusTiles;
        }
    }
}

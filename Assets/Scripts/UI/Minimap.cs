using UnityEngine;
using UnityEngine.UI;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;
using ForestMaze;
using System.Collections.Generic;

namespace FaeMaze.UI
{
    /// <summary>
    /// Radar-style minimap centered on the focal point with circular mask.
    /// Shows a configurable tile radius with edge indicators for distant objects.
    /// Rotates with camera so forward is always up.
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
        [Tooltip("Size as percentage of smaller screen dimension (0.2 = 20%)")]
        [Range(0.05f, 0.3f)]
        private float sizePercent = 0.3f;

        [SerializeField]
        [Tooltip("View radius in tiles")]
        private float viewRadiusTiles = 40f;

        [SerializeField]
        [Tooltip("Corner to place minimap in")]
        private Corner mapCorner = Corner.TopRight;

        [SerializeField]
        [Tooltip("Padding from screen edges in pixels")]
        private float edgePadding = 20f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background color")]
        private Color backgroundColor = new Color(0.05f, 0.1f, 0.05f, 0.9f);

        [SerializeField]
        [Tooltip("Border color")]
        private Color borderColor = new Color(0.2f, 0.4f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Radar ring color")]
        private Color ringColor = new Color(0.2f, 0.4f, 0.2f, 0.5f);

        [SerializeField]
        [Tooltip("Focal point crosshair color")]
        private Color crosshairColor = new Color(0.3f, 0.6f, 0.3f, 0.8f);

        [SerializeField]
        [Tooltip("Heart of maze color")]
        private Color heartColor = new Color(1f, 0.2f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Visitor dot color")]
        private Color visitorColor = new Color(0.3f, 1f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Edge indicator color")]
        private Color edgeIndicatorColor = new Color(1f, 0.8f, 0.2f, 1f);

        [SerializeField]
        [Tooltip("Graph node color")]
        private Color graphNodeColor = new Color(0.4f, 0.6f, 0.8f, 0.6f);

        [SerializeField]
        [Tooltip("Graph edge color")]
        private Color graphEdgeColor = new Color(0.3f, 0.5f, 0.3f, 0.4f);

        [Header("Dot Sizes")]
        [SerializeField]
        [Tooltip("Heart dot size in pixels")]
        private float heartDotSize = 10f;

        [SerializeField]
        [Tooltip("Visitor dot size in pixels")]
        private float visitorDotSize = 6f;

        [SerializeField]
        [Tooltip("Crosshair size in pixels")]
        private float crosshairSize = 12f;

        [SerializeField]
        [Tooltip("Edge indicator size in pixels")]
        private float edgeIndicatorSize = 8f;

        [SerializeField]
        [Tooltip("Graph node dot size in pixels")]
        private float graphNodeSize = 8f;

        [SerializeField]
        [Tooltip("Graph edge line width in pixels")]
        private float graphEdgeWidth = 2f;

        public enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private Canvas canvas;
        private RectTransform minimapPanel;
        private RectTransform radarContainer;
        private RectTransform dotsContainer;
        private Camera mainCamera;
        private HeartOfTheMaze heart;

        private List<Image> visitorDots = new List<Image>();
        private List<Image> visitorEdgeIndicators = new List<Image>();
        private Image heartDot;
        private Image heartEdgeIndicator;
        private RectTransform crosshair;
        private List<Image> radarRings = new List<Image>();
        private Sprite circleSprite;
        private Sprite ringSprite;
        private Sprite triangleSprite;

        // Graph visualization
        private RectTransform graphContainer;
        private List<Image> graphNodeDots = new List<Image>();
        private List<List<RectTransform>> graphEdgeSegments = new List<List<RectTransform>>(); // Each edge has multiple segments

        private void Awake()
        {
            mainCamera = Camera.main;
            CreateSprites();
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
                CreateHeartEdgeIndicator();
            }
        }

        private void CreateSprites()
        {
            circleSprite = CreateCircleSprite();
            ringSprite = CreateRingSprite();
            triangleSprite = CreateTriangleSprite();
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

            // Create minimap panel (outer container for positioning)
            GameObject panelObj = new GameObject("MinimapPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            minimapPanel = panelObj.AddComponent<RectTransform>();

            // Create circular background with mask
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(panelObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;

            // Use Image with circle sprite for mask (Mask requires Image, not RawImage)
            Image maskImage = bgObj.AddComponent<Image>();
            maskImage.sprite = circleSprite;
            maskImage.color = backgroundColor;
            maskImage.type = Image.Type.Simple;
            maskImage.preserveAspect = true;

            // Add mask for circular clipping
            Mask mask = bgObj.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Create radar container (this rotates with camera)
            GameObject radarObj = new GameObject("RadarContainer");
            radarObj.transform.SetParent(bgObj.transform, false);
            radarContainer = radarObj.AddComponent<RectTransform>();
            radarContainer.anchorMin = new Vector2(0.5f, 0.5f);
            radarContainer.anchorMax = new Vector2(0.5f, 0.5f);
            radarContainer.pivot = new Vector2(0.5f, 0.5f);
            radarContainer.sizeDelta = Vector2.zero;
            radarContainer.anchoredPosition = Vector2.zero;

            // Create radar rings (decorative)
            CreateRadarRings();

            // Create graph container (for nodes and edges - below other elements)
            CreateGraphContainer();

            // Create crosshair
            CreateCrosshair();

            // Create dots container
            GameObject dotsObj = new GameObject("DotsContainer");
            dotsObj.transform.SetParent(radarContainer, false);
            dotsContainer = dotsObj.AddComponent<RectTransform>();
            dotsContainer.anchorMin = new Vector2(0.5f, 0.5f);
            dotsContainer.anchorMax = new Vector2(0.5f, 0.5f);
            dotsContainer.pivot = new Vector2(0.5f, 0.5f);
            dotsContainer.sizeDelta = Vector2.zero;
            dotsContainer.anchoredPosition = Vector2.zero;

            // Create border ring
            CreateBorderRing(panelObj);

            UpdateMinimapSize();
        }

        private void CreateRadarRings()
        {
            // Create concentric rings at 25%, 50%, 75% of radius
            float[] ringRadii = { 0.25f, 0.5f, 0.75f };

            foreach (float radiusFraction in ringRadii)
            {
                GameObject ringObj = new GameObject($"RadarRing_{radiusFraction * 100}");
                ringObj.transform.SetParent(radarContainer, false);
                RectTransform ringRect = ringObj.AddComponent<RectTransform>();
                ringRect.anchorMin = new Vector2(0.5f, 0.5f);
                ringRect.anchorMax = new Vector2(0.5f, 0.5f);
                ringRect.pivot = new Vector2(0.5f, 0.5f);
                ringRect.anchoredPosition = Vector2.zero;

                Image ringImage = ringObj.AddComponent<Image>();
                ringImage.sprite = ringSprite;
                ringImage.color = ringColor;
                ringImage.type = Image.Type.Simple;
                ringImage.preserveAspect = true;
                ringImage.raycastTarget = false;

                radarRings.Add(ringImage);
            }
        }

        private void CreateBorderRing(GameObject parent)
        {
            GameObject borderObj = new GameObject("BorderRing");
            borderObj.transform.SetParent(parent.transform, false);
            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            borderRect.anchoredPosition = Vector2.zero;

            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.sprite = ringSprite;
            borderImage.color = borderColor;
            borderImage.type = Image.Type.Simple;
            borderImage.preserveAspect = true;
            borderImage.raycastTarget = false;
        }

        private void CreateCrosshair()
        {
            GameObject crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(radarContainer, false);
            crosshair = crosshairObj.AddComponent<RectTransform>();
            crosshair.anchorMin = new Vector2(0.5f, 0.5f);
            crosshair.anchorMax = new Vector2(0.5f, 0.5f);
            crosshair.pivot = new Vector2(0.5f, 0.5f);
            crosshair.sizeDelta = new Vector2(crosshairSize, crosshairSize);
            crosshair.anchoredPosition = Vector2.zero;

            // Horizontal line
            GameObject hLineObj = new GameObject("HorizontalLine");
            hLineObj.transform.SetParent(crosshairObj.transform, false);
            RectTransform hLineRect = hLineObj.AddComponent<RectTransform>();
            hLineRect.anchorMin = new Vector2(0.5f, 0.5f);
            hLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            hLineRect.pivot = new Vector2(0.5f, 0.5f);
            hLineRect.sizeDelta = new Vector2(crosshairSize, 1f);
            hLineRect.anchoredPosition = Vector2.zero;
            Image hLineImg = hLineObj.AddComponent<Image>();
            hLineImg.color = crosshairColor;
            hLineImg.raycastTarget = false;

            // Vertical line
            GameObject vLineObj = new GameObject("VerticalLine");
            vLineObj.transform.SetParent(crosshairObj.transform, false);
            RectTransform vLineRect = vLineObj.AddComponent<RectTransform>();
            vLineRect.anchorMin = new Vector2(0.5f, 0.5f);
            vLineRect.anchorMax = new Vector2(0.5f, 0.5f);
            vLineRect.pivot = new Vector2(0.5f, 0.5f);
            vLineRect.sizeDelta = new Vector2(1f, crosshairSize);
            vLineRect.anchoredPosition = Vector2.zero;
            Image vLineImg = vLineObj.AddComponent<Image>();
            vLineImg.color = crosshairColor;
            vLineImg.raycastTarget = false;
        }

        private void CreateHeartDot()
        {
            GameObject dotObj = new GameObject("HeartDot");
            dotObj.transform.SetParent(dotsContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(heartDotSize, heartDotSize);

            heartDot = dotObj.AddComponent<Image>();
            heartDot.color = heartColor;
            heartDot.sprite = circleSprite;
            heartDot.raycastTarget = false;
        }

        private void CreateHeartEdgeIndicator()
        {
            GameObject indicatorObj = new GameObject("HeartEdgeIndicator");
            indicatorObj.transform.SetParent(dotsContainer, false);
            RectTransform indicatorRect = indicatorObj.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.sizeDelta = new Vector2(edgeIndicatorSize, edgeIndicatorSize);

            heartEdgeIndicator = indicatorObj.AddComponent<Image>();
            heartEdgeIndicator.color = heartColor;
            heartEdgeIndicator.sprite = triangleSprite;
            heartEdgeIndicator.raycastTarget = false;
            heartEdgeIndicator.gameObject.SetActive(false);
        }

        private Image CreateVisitorDot()
        {
            GameObject dotObj = new GameObject("VisitorDot");
            dotObj.transform.SetParent(dotsContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(visitorDotSize, visitorDotSize);

            Image dot = dotObj.AddComponent<Image>();
            dot.color = visitorColor;
            dot.sprite = circleSprite;
            dot.raycastTarget = false;

            return dot;
        }

        private Image CreateVisitorEdgeIndicator()
        {
            GameObject indicatorObj = new GameObject("VisitorEdgeIndicator");
            indicatorObj.transform.SetParent(dotsContainer, false);
            RectTransform indicatorRect = indicatorObj.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.sizeDelta = new Vector2(edgeIndicatorSize, edgeIndicatorSize);

            Image indicator = indicatorObj.AddComponent<Image>();
            indicator.color = edgeIndicatorColor;
            indicator.sprite = triangleSprite;
            indicator.raycastTarget = false;

            return indicator;
        }

        private Sprite CreateCircleSprite()
        {
            int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[resolution * resolution];

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float radius = resolution / 2f - 1f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= radius)
                    {
                        // Solid fill with smooth edge anti-aliasing only at the border
                        float alpha = distance > radius - 1f ? Mathf.Clamp01(radius - distance + 1f) : 1f;
                        pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * resolution + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateRingSprite()
        {
            int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[resolution * resolution];

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float outerRadius = resolution / 2f - 1f;
            float innerRadius = outerRadius - 2f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= outerRadius && distance >= innerRadius)
                    {
                        float alpha = 1f;
                        // Smooth outer edge
                        if (distance > outerRadius - 0.5f)
                            alpha = Mathf.Clamp01(outerRadius - distance + 0.5f);
                        // Smooth inner edge
                        if (distance < innerRadius + 0.5f)
                            alpha = Mathf.Min(alpha, Mathf.Clamp01(distance - innerRadius + 0.5f));

                        pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * resolution + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateTriangleSprite()
        {
            int resolution = 32;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[resolution * resolution];

            // Create an upward-pointing triangle
            Vector2 top = new Vector2(resolution / 2f, resolution - 2f);
            Vector2 bottomLeft = new Vector2(2f, 2f);
            Vector2 bottomRight = new Vector2(resolution - 2f, 2f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    if (IsPointInTriangle(point, top, bottomLeft, bottomRight))
                    {
                        pixels[y * resolution + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * resolution + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;

            return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private bool IsPointInTriangle(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            float d1 = Sign(p, v1, v2);
            float d2 = Sign(p, v2, v3);
            float d3 = Sign(p, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private void Update()
        {
            if (focalPoint == null)
            {
                return;
            }

            // Try to find mazeGridBehaviour if not set
            if (mazeGridBehaviour == null)
            {
                mazeGridBehaviour = FindFirstObjectByType<MazeGridBehaviour>();
            }

            UpdateMinimapSize();
            UpdateRadarRotation();
            UpdateRadarRings();
            UpdateGraphVisualization();
            UpdateHeartDot();
            UpdateVisitorDots();
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

        private void UpdateRadarRotation()
        {
            if (radarContainer == null || mainCamera == null)
            {
                return;
            }

            // Get camera forward direction in 2D (XY plane)
            Vector3 forward = mainCamera.transform.forward;
            Vector2 forward2D = new Vector2(forward.x, forward.y).normalized;

            // Calculate angle: we want camera forward to point "up" (positive Y) on the minimap
            // Atan2(x, y) gives angle from +Y axis, which is what we want
            float angle = Mathf.Atan2(forward2D.x, forward2D.y) * Mathf.Rad2Deg;

            // Apply rotation to radar container
            radarContainer.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateRadarRings()
        {
            if (minimapPanel == null)
            {
                return;
            }

            float mapSize = minimapPanel.rect.width;
            float[] ringRadii = { 0.25f, 0.5f, 0.75f };

            for (int i = 0; i < radarRings.Count && i < ringRadii.Length; i++)
            {
                float ringSize = mapSize * ringRadii[i] * 2f;
                radarRings[i].rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
            }
        }

        private void UpdateHeartDot()
        {
            if (heart == null)
            {
                return;
            }

            if (heartDot == null)
            {
                CreateHeartDot();
            }

            if (heartEdgeIndicator == null)
            {
                CreateHeartEdgeIndicator();
            }

            Vector3 heartWorldPos = heart.transform.position;
            UpdateDotOrEdgeIndicator(heartWorldPos, heartDot, heartEdgeIndicator, heartColor);
        }

        private void UpdateVisitorDots()
        {
            // Get all active visitors
            IReadOnlyList<VisitorControllerBase> activeVisitors = VisitorRegistry.All;

            // Ensure we have enough dots and edge indicators
            while (visitorDots.Count < activeVisitors.Count)
            {
                visitorDots.Add(CreateVisitorDot());
                visitorEdgeIndicators.Add(CreateVisitorEdgeIndicator());
            }

            // Update each visitor
            int visitorIndex = 0;
            foreach (var visitor in activeVisitors)
            {
                if (visitor == null || visitorIndex >= visitorDots.Count)
                {
                    continue;
                }

                Image dot = visitorDots[visitorIndex];
                Image edgeIndicator = visitorEdgeIndicators[visitorIndex];
                Vector3 visitorWorldPos = visitor.transform.position;

                UpdateDotOrEdgeIndicator(visitorWorldPos, dot, edgeIndicator, visitorColor);

                visitorIndex++;
            }

            // Hide unused dots and edge indicators
            for (int i = visitorIndex; i < visitorDots.Count; i++)
            {
                visitorDots[i].gameObject.SetActive(false);
                visitorEdgeIndicators[i].gameObject.SetActive(false);
            }
        }

        private void UpdateDotOrEdgeIndicator(Vector3 worldPos, Image dot, Image edgeIndicator, Color dotColor)
        {
            if (focalPoint == null || minimapPanel == null)
            {
                return;
            }

            Vector3 focalWorldPos = focalPoint.position;
            Vector3 relativePos = worldPos - focalWorldPos;

            // Calculate distance in world units
            float distanceWorld = Mathf.Sqrt(relativePos.x * relativePos.x + relativePos.y * relativePos.y);

            // Get map dimensions
            float mapSize = minimapPanel.rect.width;
            float mapRadius = mapSize / 2f;
            float viewRadiusWorld = viewRadiusTiles;
            float pixelsPerUnit = mapRadius / viewRadiusWorld;

            // Check if within view radius
            if (distanceWorld <= viewRadiusWorld)
            {
                // Show dot, hide edge indicator
                dot.gameObject.SetActive(true);
                if (edgeIndicator != null)
                {
                    edgeIndicator.gameObject.SetActive(false);
                }

                // Calculate position on minimap
                float minimapX = relativePos.x * pixelsPerUnit;
                float minimapY = relativePos.y * pixelsPerUnit;
                dot.rectTransform.anchoredPosition = new Vector2(minimapX, minimapY);
            }
            else
            {
                // Hide dot, show edge indicator at edge of radar
                dot.gameObject.SetActive(false);

                if (edgeIndicator != null)
                {
                    edgeIndicator.gameObject.SetActive(true);

                    // Calculate direction to object
                    Vector2 direction = new Vector2(relativePos.x, relativePos.y).normalized;

                    // Position at edge (95% of radius to keep inside mask)
                    float edgeDistance = mapRadius * 0.9f;
                    float edgeX = direction.x * edgeDistance;
                    float edgeY = direction.y * edgeDistance;
                    edgeIndicator.rectTransform.anchoredPosition = new Vector2(edgeX, edgeY);

                    // Rotate triangle to point outward
                    float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                    edgeIndicator.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);

                    // Use the dot color for consistency
                    edgeIndicator.color = dotColor;
                }
            }
        }

        private void CreateGraphContainer()
        {
            GameObject graphObj = new GameObject("GraphContainer");
            graphObj.transform.SetParent(radarContainer, false);
            graphContainer = graphObj.AddComponent<RectTransform>();
            graphContainer.anchorMin = new Vector2(0.5f, 0.5f);
            graphContainer.anchorMax = new Vector2(0.5f, 0.5f);
            graphContainer.pivot = new Vector2(0.5f, 0.5f);
            graphContainer.sizeDelta = Vector2.zero;
            graphContainer.anchoredPosition = Vector2.zero;
        }

        private void UpdateGraphVisualization()
        {
            if (mazeGridBehaviour == null || mazeGridBehaviour.WorldSpaceMazeData == null)
            {
                return;
            }

            if (focalPoint == null || minimapPanel == null || graphContainer == null)
            {
                return;
            }

            var graphState = mazeGridBehaviour.WorldSpaceMazeData.GraphState;
            if (graphState == null)
            {
                return;
            }

            // Get map dimensions for coordinate conversion
            float mapSize = minimapPanel.rect.width;
            float mapRadius = mapSize / 2f;
            float viewRadiusWorld = viewRadiusTiles;
            float pixelsPerUnit = mapRadius / viewRadiusWorld;
            Vector3 focalWorldPos = focalPoint.position;

            // Update edges first (so they render behind nodes)
            UpdateGraphEdges(graphState, focalWorldPos, pixelsPerUnit, mapRadius);

            // Update nodes
            UpdateGraphNodes(graphState, focalWorldPos, pixelsPerUnit, mapRadius);
        }

        private void UpdateGraphNodes(PlanarForestMazeGenerator.ForestMapState graphState, Vector3 focalWorldPos, float pixelsPerUnit, float mapRadius)
        {
            // Create or update node dots
            int nodeCount = graphState.Nodes.Count;

            // Ensure we have enough node dots
            while (graphNodeDots.Count < nodeCount)
            {
                graphNodeDots.Add(CreateGraphNodeDot());
            }

            // Update each node
            for (int i = 0; i < nodeCount; i++)
            {
                var node = graphState.Nodes[i];
                Image nodeDot = graphNodeDots[i];

                // Convert node position to minimap coordinates
                Vector2 nodeWorldPos = node.Position;
                Vector2 relativePos = nodeWorldPos - new Vector2(focalWorldPos.x, focalWorldPos.y);
                float distance = relativePos.magnitude;

                // Only show nodes within view radius
                if (distance <= viewRadiusTiles)
                {
                    nodeDot.gameObject.SetActive(true);
                    float minimapX = relativePos.x * pixelsPerUnit;
                    float minimapY = relativePos.y * pixelsPerUnit;
                    nodeDot.rectTransform.anchoredPosition = new Vector2(minimapX, minimapY);
                }
                else
                {
                    nodeDot.gameObject.SetActive(false);
                }
            }

            // Hide unused node dots
            for (int i = nodeCount; i < graphNodeDots.Count; i++)
            {
                graphNodeDots[i].gameObject.SetActive(false);
            }
        }

        private void UpdateGraphEdges(PlanarForestMazeGenerator.ForestMapState graphState, Vector3 focalWorldPos, float pixelsPerUnit, float mapRadius)
        {
            int edgeCount = graphState.Edges.Count;

            // Ensure we have enough edge segment lists
            while (graphEdgeSegments.Count < edgeCount)
            {
                graphEdgeSegments.Add(new List<RectTransform>());
            }

            Vector2 focalPos2D = new Vector2(focalWorldPos.x, focalWorldPos.y);

            // Update each edge
            for (int i = 0; i < edgeCount; i++)
            {
                var edge = graphState.Edges[i];
                List<RectTransform> segments = graphEdgeSegments[i];

                // Get polyline points
                if (edge.PolylinePoints.Count < 2)
                {
                    // Hide all segments for this edge
                    foreach (var segment in segments)
                    {
                        segment.gameObject.SetActive(false);
                    }
                    continue;
                }

                int segmentCount = edge.PolylinePoints.Count - 1;

                // Ensure we have enough segments for this edge
                while (segments.Count < segmentCount)
                {
                    segments.Add(CreateGraphEdgeLine());
                }

                // Check if any point is within view radius
                bool anyVisible = false;
                for (int p = 0; p < edge.PolylinePoints.Count; p++)
                {
                    Vector2 pointRelative = edge.PolylinePoints[p] - focalPos2D;
                    if (pointRelative.magnitude <= viewRadiusTiles)
                    {
                        anyVisible = true;
                        break;
                    }
                }

                if (!anyVisible)
                {
                    // Check if any segment crosses the view area
                    for (int s = 0; s < segmentCount && !anyVisible; s++)
                    {
                        Vector2 segStart = edge.PolylinePoints[s] - focalPos2D;
                        Vector2 segEnd = edge.PolylinePoints[s + 1] - focalPos2D;
                        // Simple check: midpoint
                        Vector2 midPoint = (segStart + segEnd) * 0.5f;
                        if (midPoint.magnitude <= viewRadiusTiles)
                        {
                            anyVisible = true;
                        }
                    }
                }

                if (!anyVisible)
                {
                    // Hide all segments for this edge
                    foreach (var segment in segments)
                    {
                        segment.gameObject.SetActive(false);
                    }
                    continue;
                }

                // Update each segment of the polyline
                for (int s = 0; s < segmentCount; s++)
                {
                    RectTransform segmentLine = segments[s];

                    Vector2 segStartWorld = edge.PolylinePoints[s];
                    Vector2 segEndWorld = edge.PolylinePoints[s + 1];

                    Vector2 segStartRelative = segStartWorld - focalPos2D;
                    Vector2 segEndRelative = segEndWorld - focalPos2D;

                    // Check if this segment is visible
                    float startDist = segStartRelative.magnitude;
                    float endDist = segEndRelative.magnitude;

                    if (startDist > viewRadiusTiles && endDist > viewRadiusTiles)
                    {
                        // Both outside - check midpoint
                        Vector2 midRelative = (segStartRelative + segEndRelative) * 0.5f;
                        if (midRelative.magnitude > viewRadiusTiles)
                        {
                            segmentLine.gameObject.SetActive(false);
                            continue;
                        }
                    }

                    segmentLine.gameObject.SetActive(true);

                    // Clamp points to view radius for visual display
                    Vector2 clampedStart = ClampToViewRadius(segStartRelative, viewRadiusTiles);
                    Vector2 clampedEnd = ClampToViewRadius(segEndRelative, viewRadiusTiles);

                    // Convert to minimap coordinates
                    Vector2 startMinimap = clampedStart * pixelsPerUnit;
                    Vector2 endMinimap = clampedEnd * pixelsPerUnit;

                    // Position and rotate the line
                    PositionEdgeLine(segmentLine, startMinimap, endMinimap);
                }

                // Hide unused segments for this edge
                for (int s = segmentCount; s < segments.Count; s++)
                {
                    segments[s].gameObject.SetActive(false);
                }
            }

            // Hide unused edge segment lists
            for (int i = edgeCount; i < graphEdgeSegments.Count; i++)
            {
                foreach (var segment in graphEdgeSegments[i])
                {
                    segment.gameObject.SetActive(false);
                }
            }
        }

        private Vector2 ClampToViewRadius(Vector2 relativePos, float radius)
        {
            if (relativePos.magnitude <= radius)
            {
                return relativePos;
            }
            return relativePos.normalized * radius;
        }

        private Image CreateGraphNodeDot()
        {
            GameObject dotObj = new GameObject("GraphNodeDot");
            dotObj.transform.SetParent(graphContainer, false);
            RectTransform dotRect = dotObj.AddComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0.5f, 0.5f);
            dotRect.anchorMax = new Vector2(0.5f, 0.5f);
            dotRect.pivot = new Vector2(0.5f, 0.5f);
            dotRect.sizeDelta = new Vector2(graphNodeSize, graphNodeSize);

            Image dot = dotObj.AddComponent<Image>();
            dot.color = graphNodeColor;
            dot.sprite = circleSprite;
            dot.raycastTarget = false;

            return dot;
        }

        private RectTransform CreateGraphEdgeLine()
        {
            GameObject lineObj = new GameObject("GraphEdgeLine");
            lineObj.transform.SetParent(graphContainer, false);
            RectTransform lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0f, 0.5f); // Pivot at left-center for rotation

            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = graphEdgeColor;
            lineImage.raycastTarget = false;

            return lineRect;
        }

        private void PositionEdgeLine(RectTransform lineRect, Vector2 start, Vector2 end)
        {
            // Calculate line properties
            Vector2 direction = end - start;
            float length = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Position at start point
            lineRect.anchoredPosition = start;

            // Set size (width = length, height = line width)
            lineRect.sizeDelta = new Vector2(length, graphEdgeWidth);

            // Rotate to point toward end
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}

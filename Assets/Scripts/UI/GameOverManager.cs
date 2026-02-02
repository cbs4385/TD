using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using System.Collections.Generic;
using System.Linq;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Game Over scene UI and statistics display.
    /// Shows run length, peak essence, essence ledger, and hazard effectiveness histogram.
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Text displaying run length (replaces max wave)")]
        private TextMeshProUGUI runLengthText;

        [SerializeField]
        [Tooltip("Text displaying peak essence achieved")]
        private TextMeshProUGUI peakEssenceText;

        [SerializeField]
        [Tooltip("Container for essence ledger (gains vs costs)")]
        private RectTransform essenceLedgerContainer;

        [SerializeField]
        [Tooltip("Container for hazard histogram")]
        private RectTransform hazardHistogramContainer;

        [SerializeField]
        [Tooltip("Text displaying escaped count")]
        private TextMeshProUGUI escapedText;

        [Header("Colors")]
        [SerializeField]
        private Color heartColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField]
        private Color mawColor = new Color(0.5f, 0.1f, 0.15f);
        [SerializeField]
        private Color lanternColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField]
        private Color fairyRingColor = new Color(0.3f, 0.7f, 0.4f);
        [SerializeField]
        private Color kelpieColor = new Color(0.2f, 0.5f, 0.8f);
        [SerializeField]
        private Color redCapColor = new Color(0.4f, 0.2f, 0.2f);
        [SerializeField]
        private Color escapedColor = new Color(0.6f, 0.6f, 0.6f);

        // Archetype colors for procedural icons
        private readonly Color lanternDrunkColor = new Color(1f, 0.6f, 0.2f);    // Orange
        private readonly Color waryWayfarerColor = new Color(0.3f, 0.5f, 0.8f);  // Blue
        private readonly Color sleepwalkingDevoteeColor = new Color(0.6f, 0.3f, 0.7f); // Purple

        private SceneLoader sceneLoader;
        private Canvas canvas;
        private Button createdMainMenuButton;

        private void Awake()
        {
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        private void Start()
        {
            SetupCanvas();
            HideOldElements();
            CreateFullScreenUI();
            DisplayStatistics();

            // Finalize run stats for meta-progression (calculates Fae Dust rewards)
            GameStatsTracker.Instance?.FinalizeRunStats();
        }

        private void Update()
        {
            // Check for ESC key
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReturnToMainMenu();
            }
        }

        private void SetupCanvas()
        {
            canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("GameOverCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        /// <summary>
        /// Hide old scene elements that are no longer needed
        /// </summary>
        private void HideOldElements()
        {
            // Find and hide the old MainPanel (with its grey background and all children)
            Transform mainPanel = canvas.transform.Find("MainPanel");
            if (mainPanel != null)
            {
                // Destroy the entire MainPanel to prevent any interference
                Destroy(mainPanel.gameObject);
            }
        }

        /// <summary>
        /// Creates a full-screen UI layout without the grey background panel
        /// </summary>
        private void CreateFullScreenUI()
        {
            // Title at top (anchor 0.5, 0.93)
            CreateHeaderText("TitleText", new Vector2(0.5f, 0.93f), "GAME OVER", 72, new Color(1f, 0.3f, 0.3f));

            // Run length below title
            runLengthText = CreateHeaderText("RunLengthText", new Vector2(0.5f, 0.84f), "Run Length: 00:00", 28);

            // Peak essence
            peakEssenceText = CreateHeaderText("PeakEssenceText", new Vector2(0.5f, 0.78f), "Peak Threads: 0", 22);

            // Essence Ledger container
            essenceLedgerContainer = CreateContainer("EssenceLedger", new Vector2(0.5f, 0.62f), new Vector2(600, 160));

            // Hazard Histogram container (positioned to leave room for button at bottom)
            hazardHistogramContainer = CreateContainer("HazardHistogram", new Vector2(0.5f, 0.32f), new Vector2(700, 280));

            // Main Menu button at very bottom
            CreateMainMenuButton();
        }

        private void CreateMainMenuButton()
        {
            GameObject buttonObj = new GameObject("MainMenuButton_New");
            buttonObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.03f);
            rect.anchorMax = new Vector2(0.5f, 0.03f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(350, 60);

            Image bgImage = buttonObj.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.5f, 0.8f, 1f);

            createdMainMenuButton = buttonObj.AddComponent<Button>();
            createdMainMenuButton.targetGraphic = bgImage;
            createdMainMenuButton.onClick.AddListener(ReturnToMainMenu);

            // Button text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "Return to Main Menu";
            tmp.fontSize = 28;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        private TextMeshProUGUI CreateHeaderText(string name, Vector2 anchorPos, string defaultText, float fontSize, Color? color = null)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800, 60);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;

            return tmp;
        }

        private RectTransform CreateContainer(string name, Vector2 anchorPos, Vector2 size)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(canvas.transform, false);

            RectTransform rect = container.AddComponent<RectTransform>();
            rect.anchorMin = anchorPos;
            rect.anchorMax = anchorPos;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            return rect;
        }

        private void DisplayStatistics()
        {
            if (GameStatsTracker.Instance == null)
            {
                DisplayDefaultStats();
                return;
            }

            var stats = GameStatsTracker.Instance;

            // Run length (replaces max wave)
            if (runLengthText != null)
            {
                runLengthText.text = $"Run Length: {stats.GetFormattedTime()}";
            }

            // Peak essence achieved
            if (peakEssenceText != null)
            {
                peakEssenceText.text = $"Peak Threads: {stats.MaxEssenceAchieved}";
            }

            // Build essence ledger
            BuildEssenceLedger(stats);

            // Build hazard histogram (now includes Escaped)
            BuildHazardHistogram(stats);
        }

        /// <summary>
        /// Builds the essence ledger showing gains and costs side by side
        /// </summary>
        private void BuildEssenceLedger(GameStatsTracker stats)
        {
            if (essenceLedgerContainer == null) return;

            // Clear existing children
            foreach (Transform child in essenceLedgerContainer)
            {
                Destroy(child.gameObject);
            }

            var essenceTotals = stats.GetEssenceTotalsBySource();

            // Create header
            CreateLedgerText("THREAD LEDGER", 22, FontStyles.Bold, essenceLedgerContainer, new Vector2(0, 70));

            // Gains column (left)
            float leftX = -140f;
            float startY = 40f;
            float lineHeight = 24f;

            CreateLedgerText("GAINS", 18, FontStyles.Bold, essenceLedgerContainer, new Vector2(leftX, startY), new Color(0.5f, 0.9f, 0.5f));

            int yIndex = 0;
            int totalGains = 0;

            // Heart consumption
            if (essenceTotals.TryGetValue(EssenceSource.VisitorConsumedByHeart, out int heartEssence) && heartEssence > 0)
            {
                int heartCount = stats.GetTotalByFate(VisitorFate.Consumed);
                CreateLedgerText($"Nommed: +{heartEssence} ({heartCount})", 16, FontStyles.Normal, essenceLedgerContainer,
                    new Vector2(leftX, startY - lineHeight * (yIndex + 1)), heartColor);
                totalGains += heartEssence;
                yIndex++;
            }

            // Maw consumption
            if (essenceTotals.TryGetValue(EssenceSource.VisitorConsumedByMaw, out int mawEssence) && mawEssence > 0)
            {
                int mawCount = stats.GetTotalByFate(VisitorFate.Devoured);
                CreateLedgerText($"Chomped: +{mawEssence} ({mawCount})", 16, FontStyles.Normal, essenceLedgerContainer,
                    new Vector2(leftX, startY - lineHeight * (yIndex + 1)), mawColor);
                totalGains += mawEssence;
                yIndex++;
            }

            // Lantern fascination
            if (essenceTotals.TryGetValue(EssenceSource.LanternFascination, out int lanternEssence) && lanternEssence > 0)
            {
                CreateLedgerText($"Dazzled: +{lanternEssence}", 16, FontStyles.Normal, essenceLedgerContainer,
                    new Vector2(leftX, startY - lineHeight * (yIndex + 1)), lanternColor);
                totalGains += lanternEssence;
                yIndex++;
            }

            // Costs column (right)
            float rightX = 140f;
            yIndex = 0;
            int totalCosts = 0;

            CreateLedgerText("COSTS", 18, FontStyles.Bold, essenceLedgerContainer, new Vector2(rightX, startY), new Color(0.9f, 0.5f, 0.5f));

            // Heart power costs
            if (essenceTotals.TryGetValue(EssenceSource.HeartPowerCost, out int powerCost) && powerCost < 0)
            {
                CreateLedgerText($"Powers: {powerCost}", 16, FontStyles.Normal, essenceLedgerContainer,
                    new Vector2(rightX, startY - lineHeight * (yIndex + 1)), Color.white);
                totalCosts += powerCost;
                yIndex++;
            }

            // Goblin penalties
            if (essenceTotals.TryGetValue(EssenceSource.RedCapPenalty, out int redCapPenalty) && redCapPenalty < 0)
            {
                CreateLedgerText($"Goblin: {redCapPenalty}", 16, FontStyles.Normal, essenceLedgerContainer,
                    new Vector2(rightX, startY - lineHeight * (yIndex + 1)), redCapColor);
                totalCosts += redCapPenalty;
                yIndex++;
            }

            // Net essence at bottom
            int netEssence = totalGains + totalCosts;
            string netStr = netEssence >= 0 ? $"+{netEssence}" : netEssence.ToString();
            Color netColor = netEssence >= 0 ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.9f, 0.5f, 0.5f);
            CreateLedgerText($"Net: {netStr}", 20, FontStyles.Bold, essenceLedgerContainer, new Vector2(0, -70), netColor);
        }

        private TextMeshProUGUI CreateLedgerText(string text, float fontSize, FontStyles style, Transform parent, Vector2 position, Color? color = null)
        {
            GameObject textObj = new GameObject("LedgerText");
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280, 30);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color ?? Color.white;

            return tmp;
        }

        /// <summary>
        /// Builds the hazard effectiveness histogram ordered by kills.
        /// Now includes Escaped at the end.
        /// </summary>
        private void BuildHazardHistogram(GameStatsTracker stats)
        {
            if (hazardHistogramContainer == null) return;

            // Clear existing children
            foreach (Transform child in hazardHistogramContainer)
            {
                Destroy(child.gameObject);
            }

            // Collect hazard data (including Escaped at the end)
            var hazardData = new List<(string name, int count, Color color, VisitorFate fate)>
            {
                ("Nommed", stats.GetTotalByFate(VisitorFate.Consumed), heartColor, VisitorFate.Consumed),
                ("Chomped", stats.GetTotalByFate(VisitorFate.Devoured), mawColor, VisitorFate.Devoured),
                ("Splashed", stats.GetTotalByFate(VisitorFate.Drowned), kelpieColor, VisitorFate.Drowned),
                ("Goblin'd", stats.GetTotalByFate(VisitorFate.RedCapKill), redCapColor, VisitorFate.RedCapKill),
                ("Dazzled", stats.GetTotalByFate(VisitorFate.Lantern), lanternColor, VisitorFate.Lantern),
                ("Danced Out", stats.GetTotalByFate(VisitorFate.FairyRing), fairyRingColor, VisitorFate.FairyRing)
            };

            // Sort hazards by count descending
            hazardData = hazardData.OrderByDescending(h => h.count).ToList();

            // Add Escaped at the end (not sorted with hazards)
            int escapedCount = stats.GetTotalByFate(VisitorFate.Escaped);
            hazardData.Add(("Got Away", escapedCount, escapedColor, VisitorFate.Escaped));

            // Find max for bar scaling (include escaped in max calculation)
            int maxCount = hazardData.Max(h => h.count);
            if (maxCount == 0) maxCount = 1; // Avoid division by zero

            // Create header
            CreateLedgerText("VISITOR FATES", 22, FontStyles.Bold, hazardHistogramContainer, new Vector2(0, 130));

            // Create histogram bars
            float startY = 100f;
            float barHeight = 22f;
            float barSpacing = 4f;
            float barMaxWidth = 280f;
            float labelWidth = 130f;
            float countWidth = 50f;

            int displayedCount = 0;
            for (int i = 0; i < hazardData.Count; i++)
            {
                var (name, count, color, fate) = hazardData[i];

                // Show all items (even with 0 count for Escaped to always show it)
                if (count > 0 || fate == VisitorFate.Escaped)
                {
                    float yPos = startY - (displayedCount * (barHeight + barSpacing));
                    CreateHistogramRow(name, count, maxCount, color, yPos, barMaxWidth, labelWidth, countWidth, stats, fate);
                    displayedCount++;
                }
            }
        }

        private void CreateHistogramRow(string label, int count, int maxCount, Color color, float yPos,
            float barMaxWidth, float labelWidth, float countWidth, GameStatsTracker stats, VisitorFate fate)
        {
            float totalWidth = labelWidth + barMaxWidth + countWidth + 30;
            float startX = -totalWidth / 2;

            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(hazardHistogramContainer, false);

            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(startX, yPos);
            labelRect.sizeDelta = new Vector2(labelWidth, 24);

            TextMeshProUGUI labelTmp = labelObj.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineRight;
            labelTmp.color = color;

            // Bar background
            GameObject barBgObj = new GameObject("BarBg");
            barBgObj.transform.SetParent(hazardHistogramContainer, false);

            RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            barBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.anchoredPosition = new Vector2(startX + labelWidth + 10, yPos);
            barBgRect.sizeDelta = new Vector2(barMaxWidth, 20);

            Image barBgImage = barBgObj.AddComponent<Image>();
            barBgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            // Bar fill
            float fillWidth = (count / (float)maxCount) * barMaxWidth;
            if (fillWidth > 0)
            {
                GameObject barFillObj = new GameObject("BarFill");
                barFillObj.transform.SetParent(hazardHistogramContainer, false);

                RectTransform barFillRect = barFillObj.AddComponent<RectTransform>();
                barFillRect.anchorMin = new Vector2(0.5f, 0.5f);
                barFillRect.anchorMax = new Vector2(0.5f, 0.5f);
                barFillRect.pivot = new Vector2(0f, 0.5f);
                barFillRect.anchoredPosition = new Vector2(startX + labelWidth + 10, yPos);
                barFillRect.sizeDelta = new Vector2(fillWidth, 20);

                Image barFillImage = barFillObj.AddComponent<Image>();
                barFillImage.color = color;
            }

            // Count text
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(hazardHistogramContainer, false);

            RectTransform countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0.5f);
            countRect.anchorMax = new Vector2(0.5f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.anchoredPosition = new Vector2(startX + labelWidth + barMaxWidth + 15, yPos);
            countRect.sizeDelta = new Vector2(countWidth, 24);

            TextMeshProUGUI countTmp = countObj.AddComponent<TextMeshProUGUI>();
            countTmp.text = count.ToString();
            countTmp.fontSize = 16;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.alignment = TextAlignmentOptions.MidlineLeft;
            countTmp.color = Color.white;

            // Add archetype breakdown icons after count (skip for Escaped since it's not tracked by archetype the same way)
            if (fate != VisitorFate.Escaped)
            {
                AddArchetypeBreakdown(fate, stats, startX + labelWidth + barMaxWidth + countWidth + 25, yPos);
            }
        }

        /// <summary>
        /// Adds small colored squares showing archetype breakdown for a hazard
        /// </summary>
        private void AddArchetypeBreakdown(VisitorFate fate, GameStatsTracker stats, float xPos, float yPos)
        {
            float iconSize = 14f;
            float iconSpacing = 2f;
            int iconIndex = 0;

            foreach (var kvp in stats.ArchetypeStats)
            {
                VisitorArchetype archetype = kvp.Key;
                int count = kvp.Value.FateCounts[fate];

                if (count > 0)
                {
                    // Create colored square for each archetype with kills by this hazard
                    Color archetypeColor = GetArchetypeColor(archetype);

                    for (int i = 0; i < Mathf.Min(count, 4); i++) // Max 4 icons per archetype
                    {
                        CreateArchetypeIcon(archetypeColor, xPos + (iconIndex * (iconSize + iconSpacing)), yPos, iconSize);
                        iconIndex++;
                    }

                    // If more than 4, show "+N" text
                    if (count > 4)
                    {
                        GameObject plusObj = new GameObject("PlusCount");
                        plusObj.transform.SetParent(hazardHistogramContainer, false);

                        RectTransform plusRect = plusObj.AddComponent<RectTransform>();
                        plusRect.anchorMin = new Vector2(0.5f, 0.5f);
                        plusRect.anchorMax = new Vector2(0.5f, 0.5f);
                        plusRect.pivot = new Vector2(0f, 0.5f);
                        plusRect.anchoredPosition = new Vector2(xPos + (iconIndex * (iconSize + iconSpacing)), yPos);
                        plusRect.sizeDelta = new Vector2(30, 16);

                        TextMeshProUGUI plusTmp = plusObj.AddComponent<TextMeshProUGUI>();
                        plusTmp.text = $"+{count - 4}";
                        plusTmp.fontSize = 10;
                        plusTmp.alignment = TextAlignmentOptions.MidlineLeft;
                        plusTmp.color = archetypeColor;
                        iconIndex++;
                    }
                }
            }
        }

        private void CreateArchetypeIcon(Color color, float x, float y, float size)
        {
            GameObject iconObj = new GameObject("ArchetypeIcon");
            iconObj.transform.SetParent(hazardHistogramContainer, false);

            RectTransform rect = iconObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(size, size);

            Image image = iconObj.AddComponent<Image>();
            image.color = color;
        }

        private Color GetArchetypeColor(VisitorArchetype archetype)
        {
            return archetype switch
            {
                VisitorArchetype.LanternDrunk => lanternDrunkColor,
                VisitorArchetype.WaryWayfarer => waryWayfarerColor,
                VisitorArchetype.SleepwalkingDevotee => sleepwalkingDevoteeColor,
                _ => Color.white
            };
        }

        private void DisplayDefaultStats()
        {
            if (runLengthText != null)
                runLengthText.text = "Run Length: 00:00";

            if (peakEssenceText != null)
                peakEssenceText.text = "Peak Threads: 0";
        }

        private void ReturnToMainMenu()
        {
            // Reset stats for next game
            if (GameStatsTracker.Instance != null)
            {
                GameStatsTracker.Instance.ResetStats();
            }

            sceneLoader.LoadMainMenu();
        }
    }
}

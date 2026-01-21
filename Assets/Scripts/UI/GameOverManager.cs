using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FaeMaze.Systems;
using FaeMaze.Visitors;

namespace FaeMaze.UI
{
    /// <summary>
    /// Manages the Game Over scene UI and statistics display
    /// </summary>
    public class GameOverManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("Text displaying max wave reached")]
        private TextMeshProUGUI maxWaveText;

        [SerializeField]
        [Tooltip("Text displaying total time played")]
        private TextMeshProUGUI totalTimeText;

        [SerializeField]
        [Tooltip("Text displaying visitor fates summary")]
        private TextMeshProUGUI visitorFatesText;

        [SerializeField]
        [Tooltip("Text displaying essence summary")]
        private TextMeshProUGUI essenceSummaryText;

        [Header("Buttons")]
        [SerializeField]
        [Tooltip("Button to return to main menu")]
        private Button mainMenuButton;

        private SceneLoader sceneLoader;

        private void Awake()
        {
            sceneLoader = gameObject.AddComponent<SceneLoader>();
        }

        private void Start()
        {
            SetupButtons();
            DisplayStatistics();
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

        private void SetupButtons()
        {
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void DisplayStatistics()
        {
            if (GameStatsTracker.Instance == null)
            {
                DisplayDefaultStats();
                return;
            }

            var stats = GameStatsTracker.Instance;

            // Max wave reached
            if (maxWaveText != null)
            {
                maxWaveText.text = $"Max Wave Reached: {stats.MaxWaveReached}";
            }

            // Total time played
            if (totalTimeText != null)
            {
                totalTimeText.text = $"Game Length: {stats.GetFormattedTime()}";
            }

            // Visitor fates summary
            if (visitorFatesText != null)
            {
                visitorFatesText.text = BuildVisitorFatesSummary(stats);
            }

            // Essence summary
            if (essenceSummaryText != null)
            {
                essenceSummaryText.text = BuildEssenceSummary(stats);
            }
        }

        private string BuildVisitorFatesSummary(GameStatsTracker stats)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Visitor Fates</b>");
            sb.AppendLine();

            int totalVisitors = stats.GetTotalVisitors();
            sb.AppendLine($"Total Visitors: {totalVisitors}");
            sb.AppendLine();

            // Show fates with counts and essence
            AppendFateLine(sb, stats, VisitorFate.Consumed, "Consumed by Heart");
            AppendFateLine(sb, stats, VisitorFate.Devoured, "Devoured by Maw");
            AppendFateLine(sb, stats, VisitorFate.FairyRing, "Fairy Ring");
            AppendFateLine(sb, stats, VisitorFate.Lantern, "Lantern");
            AppendFateLine(sb, stats, VisitorFate.Drowned, "Drowned by Kelpie");
            AppendFateLine(sb, stats, VisitorFate.Escaped, "Escaped");
            AppendFateLine(sb, stats, VisitorFate.RedCapKill, "RedCap Kills");

            return sb.ToString().TrimEnd();
        }

        private void AppendFateLine(System.Text.StringBuilder sb, GameStatsTracker stats, VisitorFate fate, string label)
        {
            int count = stats.GetTotalByFate(fate);
            int essence = stats.GetTotalEssenceByFate(fate);

            if (count > 0 || fate == VisitorFate.Consumed || fate == VisitorFate.Escaped)
            {
                string essenceStr = essence >= 0 ? $"+{essence}" : essence.ToString();
                sb.AppendLine($"  {label}: {count} ({essenceStr} essence)");
            }
        }

        private string BuildEssenceSummary(GameStatsTracker stats)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Essence Summary</b>");
            sb.AppendLine();

            var essenceTotals = stats.GetEssenceTotalsBySource();

            // Show relevant essence sources
            AppendEssenceLine(sb, essenceTotals, EssenceSource.VisitorConsumedByHeart, "Heart Consumption");
            AppendEssenceLine(sb, essenceTotals, EssenceSource.VisitorConsumedByMaw, "Maw Devouring");
            AppendEssenceLine(sb, essenceTotals, EssenceSource.LanternFascination, "Lantern Fascination");
            AppendEssenceLine(sb, essenceTotals, EssenceSource.HeartPowerCost, "Heart Power Costs");
            AppendEssenceLine(sb, essenceTotals, EssenceSource.RedCapPenalty, "RedCap Penalties");
            AppendEssenceLine(sb, essenceTotals, EssenceSource.EssenceDecay, "Essence Decay");

            // Calculate net essence change
            int netEssence = 0;
            foreach (var kvp in essenceTotals)
            {
                if (kvp.Key != EssenceSource.StartingEssence)
                {
                    netEssence += kvp.Value;
                }
            }

            sb.AppendLine();
            string netStr = netEssence >= 0 ? $"+{netEssence}" : netEssence.ToString();
            sb.AppendLine($"<b>Net Essence: {netStr}</b>");

            return sb.ToString().TrimEnd();
        }

        private void AppendEssenceLine(System.Text.StringBuilder sb, System.Collections.Generic.Dictionary<EssenceSource, int> totals, EssenceSource source, string label)
        {
            if (totals.TryGetValue(source, out int amount) && amount != 0)
            {
                string amountStr = amount >= 0 ? $"+{amount}" : amount.ToString();
                sb.AppendLine($"  {label}: {amountStr}");
            }
        }

        private void DisplayDefaultStats()
        {
            if (maxWaveText != null)
                maxWaveText.text = "Max Wave Reached: 0";

            if (totalTimeText != null)
                totalTimeText.text = "Game Length: 00:00";

            if (visitorFatesText != null)
                visitorFatesText.text = "<b>Visitor Fates</b>\n\nNo visitors encountered";

            if (essenceSummaryText != null)
                essenceSummaryText.text = "<b>Essence Summary</b>\n\nNo essence transactions";
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

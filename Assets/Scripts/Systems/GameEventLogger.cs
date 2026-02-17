using UnityEngine;

namespace FaeMaze.Systems
{
    /// <summary>
    /// Centralized game event logging with consistent formatting.
    /// All log messages use the pattern: [Category] Subject: Event details
    /// </summary>
    public static class GameEventLogger
    {
        // --- Visitor Events ---

        public static void LogVisitorSpawn(string label, Vector3 pos, int tier, string spawnId)
        {
            Debug.Log($"[Visitor] {label}: Spawned at ({pos.x:F1},{pos.y:F1}) tier={tier} portal={spawnId}");
        }

        public static void LogVisitorStateChange(string label, string from, string to, string reason)
        {
            Debug.Log($"[Visitor] {label}: State {from} → {to} ({reason})");
        }

        public static void LogVisitorFascination(string label, string propLabel)
        {
            Debug.Log($"[Visitor] {label}: Fascinated by {propLabel}");
        }

        public static void LogVisitorFascinationEnd(string label, string propLabel, string reason)
        {
            Debug.Log($"[Visitor] {label}: Fascination ended at {propLabel} ({reason})");
        }

        public static void LogVisitorFate(string label, string fate, int essence)
        {
            Debug.Log($"[Visitor] {label}: Fate={fate} essence={essence}");
        }

        public static void LogVisitorDazed(string label, float duration)
        {
            Debug.Log($"[Visitor] {label}: Dazed for {duration:F1}s");
        }

        public static void LogVisitorFrightened(string label, string source)
        {
            Debug.Log($"[Visitor] {label}: Frightened by {source}");
        }

        // --- Prop Events ---

        public static void LogPropSpawn(string label, Vector3 pos)
        {
            Debug.Log($"[Prop] {label}: Spawned at ({pos.x:F1},{pos.y:F1})");
        }

        public static void LogPropVisitorEvent(string propLabel, string visitorLabel, string evt)
        {
            Debug.Log($"[Prop] {propLabel}: {evt} {visitorLabel}");
        }

        // --- Power Events ---

        public static void LogPowerActivated(string name, Vector3 pos, int cost, int before, int after)
        {
            Debug.Log($"[Power] {name}: Activated at ({pos.x:F1},{pos.y:F1}) cost={cost} essence={before}→{after}");
        }

        public static void LogPowerDeactivated(string name)
        {
            Debug.Log($"[Power] {name}: Deactivated");
        }

        public static void LogPowerVisitorEvent(string name, string visitorLabel, string evt)
        {
            Debug.Log($"[Power] {name}: {evt} {visitorLabel}");
        }

        // --- Heart Events ---

        public static void LogHeartEvent(string evt, string visitorLabel)
        {
            Debug.Log($"[Heart] {evt} {visitorLabel}");
        }

        // --- Enemy Events ---

        public static void LogEnemyEvent(string label, string evt, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Enemy] {label}: {evt}");
            else
                Debug.Log($"[Enemy] {label}: {evt} {details}");
        }

        // --- Game Events ---

        public static void LogGame(string evt, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Game] {evt}");
            else
                Debug.Log($"[Game] {evt} {details}");
        }

        // --- UI Events ---

        public static void LogUI(string screen, string action, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[UI] {screen}: {action}");
            else
                Debug.Log($"[UI] {screen}: {action} — {details}");
        }

        // --- Scene Events ---

        public static void LogScene(string evt, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Scene] {evt}");
            else
                Debug.Log($"[Scene] {evt} — {details}");
        }

        // --- Input Events ---

        public static void LogInput(string source, string action, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Input] {source}: {action}");
            else
                Debug.Log($"[Input] {source}: {action} — {details}");
        }

        // --- Essence Events ---

        public static void LogEssence(string action, int amount, int total, string source = "")
        {
            string sign = amount >= 0 ? "+" : "";
            if (string.IsNullOrEmpty(source))
                Debug.Log($"[Essence] {action}: {sign}{amount} total={total}");
            else
                Debug.Log($"[Essence] {action}: {sign}{amount} total={total} source={source}");
        }

        // --- Camera Events ---

        public static void LogCamera(string action, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Camera] {action}");
            else
                Debug.Log($"[Camera] {action} — {details}");
        }

        // --- Tutorial Events ---

        public static void LogTutorial(string action, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Tutorial] {action}");
            else
                Debug.Log($"[Tutorial] {action} — {details}");
        }

        // --- Roguelike Events ---

        public static void LogRoguelike(string system, string action, string details = "")
        {
            if (string.IsNullOrEmpty(details))
                Debug.Log($"[Roguelike] {system}: {action}");
            else
                Debug.Log($"[Roguelike] {system}: {action} — {details}");
        }
    }
}

using UnityEngine;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Audio;

namespace FaeMaze.DebugTools
{
    /// <summary>
    /// Debug utility for testing heart tongue consumption.
    /// Spawns a visitor at the edge of node 0 (heart node) heading toward the heart center
    /// after a configurable delay (default 10 seconds).
    /// </summary>
    public class DebugHeartTongueSpawner : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Prefab References")]
        [SerializeField]
        [Tooltip("The visitor prefab to spawn")]
        private VisitorController visitorPrefab;

        [Header("Spawn Settings")]
        [SerializeField]
        [Tooltip("Delay in seconds before spawning the visitor")]
        private float spawnDelay = 10f;

        [SerializeField]
        [Tooltip("Distance from heart center to spawn (should be at edge of node 0, radius ~3.0)")]
        private float spawnDistanceFromHeart = 3.0f;

        [SerializeField]
        [Tooltip("Angle in degrees from +X axis to determine spawn direction")]
        private float spawnAngleDegrees = 45f;

        [SerializeField]
        [Tooltip("Auto-spawn on Start")]
        private bool autoSpawn = true;

        #endregion

        #region Private Fields

        private MazeGridBehaviour mazeGrid;
        private bool hasSpawned = false;
        private float timer = 0f;
        private int visitorCount = 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            if (mazeGrid == null)
            {
                Debug.LogError("[DebugHeartTongueSpawner] MazeGridBehaviour not found in scene!");
                enabled = false;
                return;
            }

            if (visitorPrefab == null)
            {
                // Try to load from Resources or find in scene
                Debug.LogWarning("[DebugHeartTongueSpawner] Visitor prefab not assigned. Looking for existing spawner...");
                var existingSpawner = FindFirstObjectByType<DebugVisitorSpawner>();
                if (existingSpawner != null)
                {
                    // Use reflection to get the prefab from existing spawner
                    var prefabField = typeof(DebugVisitorSpawner).GetField("visitorPrefab",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (prefabField != null)
                    {
                        visitorPrefab = prefabField.GetValue(existingSpawner) as VisitorController;
                    }
                }
            }

            if (visitorPrefab == null)
            {
                Debug.LogError("[DebugHeartTongueSpawner] Visitor prefab not assigned and could not be found!");
                enabled = false;
                return;
            }

            Debug.Log($"[DebugHeartTongueSpawner] Initialized. Visitor will spawn in {spawnDelay} seconds at edge of heart node.");
        }

        private void Update()
        {
            if (!autoSpawn || hasSpawned) return;

            timer += Time.deltaTime;

            // Log countdown every second
            int secondsRemaining = Mathf.CeilToInt(spawnDelay - timer);
            if (Mathf.FloorToInt(timer) != Mathf.FloorToInt(timer - Time.deltaTime) && secondsRemaining > 0)
            {
                Debug.Log($"[DebugHeartTongueSpawner] Spawning in {secondsRemaining}...");
            }

            if (timer >= spawnDelay)
            {
                SpawnVisitorAtHeartEdge();
                hasSpawned = true;
            }
        }

        #endregion

        #region Spawning

        /// <summary>
        /// Spawns a visitor at the edge of node 0, facing toward the heart center.
        /// </summary>
        public void SpawnVisitorAtHeartEdge()
        {
            if (mazeGrid == null || visitorPrefab == null)
            {
                Debug.LogError("[DebugHeartTongueSpawner] Cannot spawn - missing references!");
                return;
            }

            Vector3 heartPos = mazeGrid.HeartWorldPosition;

            // Calculate spawn position at edge of node 0
            // Angle is measured from +X axis in XY plane
            float angleRad = spawnAngleDegrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            Vector3 spawnPos = new Vector3(
                heartPos.x + direction.x * spawnDistanceFromHeart,
                heartPos.y + direction.y * spawnDistanceFromHeart,
                heartPos.z  // Same Z as heart
            );

            // Calculate rotation to face toward heart center
            // Direction FROM spawn TO heart
            Vector2 toHeart = new Vector2(heartPos.x - spawnPos.x, heartPos.y - spawnPos.y).normalized;
            float facingAngle = Mathf.Atan2(toHeart.y, toHeart.x) * Mathf.Rad2Deg;

            // Visitor model faces +X by default, so rotate around Z to face the heart
            Quaternion rotation = Quaternion.Euler(0, 0, facingAngle);

            // Spawn the visitor
            VisitorController visitor = Instantiate(visitorPrefab, spawnPos, rotation);
            visitor.gameObject.name = $"DebugHeartVisitor_{visitorCount++}";

            Debug.Log($"[DebugHeartTongueSpawner] Spawned visitor at {spawnPos} facing heart at {heartPos}");

            SoundManager.Instance?.PlayVisitorSpawn();

            if (GameController.Instance != null)
            {
                GameController.Instance.SetLastSpawnedVisitor(visitor);
            }

            // Initialize the visitor
            visitor.Initialize(GameController.Instance);

            // Set destination to the heart - this is critical for the visitor to move!
            Vector3 heartWorldPos = mazeGrid.HeartWorldPosition;
            visitor.SetWorldDestination(heartWorldPos);
            Debug.Log($"[DebugHeartTongueSpawner] Set visitor destination to heart at {heartWorldPos}");
        }

        /// <summary>
        /// Manually trigger a spawn (can be called from inspector button or script).
        /// </summary>
        [ContextMenu("Spawn Visitor Now")]
        public void SpawnNow()
        {
            SpawnVisitorAtHeartEdge();
        }

        /// <summary>
        /// Reset the timer to spawn again after the delay.
        /// </summary>
        [ContextMenu("Reset Timer")]
        public void ResetTimer()
        {
            timer = 0f;
            hasSpawned = false;
            Debug.Log($"[DebugHeartTongueSpawner] Timer reset. Will spawn in {spawnDelay} seconds.");
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            // Try to find MazeGridBehaviour for gizmo drawing
            var grid = mazeGrid;
            if (grid == null)
            {
                grid = FindFirstObjectByType<MazeGridBehaviour>();
            }

            if (grid == null) return;

            Vector3 heartPos = grid.HeartWorldPosition;

            // Draw heart center
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(heartPos, 0.5f);

            // Draw node 0 edge (radius 3.0)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f); // Orange
            DrawCircle(heartPos, 3.0f, 32);

            // Calculate and draw spawn position
            float angleRad = spawnAngleDegrees * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector3 spawnPos = new Vector3(
                heartPos.x + direction.x * spawnDistanceFromHeart,
                heartPos.y + direction.y * spawnDistanceFromHeart,
                heartPos.z
            );

            // Draw spawn position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPos, 0.4f);

            // Draw direction arrow from spawn to heart
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(spawnPos, heartPos);

            // Draw arrowhead
            Vector3 toHeart = (heartPos - spawnPos).normalized;
            Vector3 arrowEnd = heartPos - toHeart * 0.5f;
            Vector3 perpendicular = new Vector3(-toHeart.y, toHeart.x, 0) * 0.2f;
            Gizmos.DrawLine(arrowEnd, arrowEnd - toHeart * 0.3f + perpendicular);
            Gizmos.DrawLine(arrowEnd, arrowEnd - toHeart * 0.3f - perpendicular);
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        #endregion
    }
}

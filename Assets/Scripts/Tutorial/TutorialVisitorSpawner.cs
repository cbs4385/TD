using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;
using FaeMaze.Cameras;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Handles controlled visitor spawning during the tutorial.
    /// Spawns visitors at specific tutorial steps for guaranteed player interaction.
    /// Visitors spawn at the edge of the focused node and path to a random exit.
    /// </summary>
    public class TutorialVisitorSpawner : MonoBehaviour
    {
        #region Constants

        private const float NODE_RADIUS = 3.0f;

        #endregion

        #region Private Fields

        private TutorialManager manager;
        private TutorialEventTriggers eventTriggers;
        private WaveSpawner waveSpawner;
        private MazeGridBehaviour mazeGrid;
        private DynamicMazeGrowth dynamicMaze;
        private CameraController3D cameraController;
        private bool spawnerWasActive;
        private int visitorsSpawned;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            manager = TutorialManager.Instance;
            eventTriggers = GetComponent<TutorialEventTriggers>();
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
            mazeGrid = FindFirstObjectByType<MazeGridBehaviour>();
            dynamicMaze = FindFirstObjectByType<DynamicMazeGrowth>();
            cameraController = FindFirstObjectByType<CameraController3D>();

            if (manager != null)
            {
                manager.OnTutorialStarted += OnTutorialStarted;
                manager.OnTutorialCompleted += OnTutorialCompleted;
            }
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnTutorialStarted -= OnTutorialStarted;
                manager.OnTutorialCompleted -= OnTutorialCompleted;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTutorialStarted()
        {
            visitorsSpawned = 0;

            // Disable normal wave spawner during tutorial
            if (waveSpawner != null)
            {
                spawnerWasActive = waveSpawner.IsWaveActive;
                if (spawnerWasActive)
                {
                    // Stop the wave spawner so tutorial controls all visitor spawning
                    Debug.Log("[TutorialVisitorSpawner] Stopping wave spawner for tutorial");
                    waveSpawner.ResetWaveState();
                }
            }
        }

        private void OnTutorialCompleted()
        {
            // Re-enable normal spawning after tutorial
            if (waveSpawner != null)
            {
                // Start the wave after tutorial completes
                Debug.Log("[TutorialVisitorSpawner] Tutorial completed, restarting wave spawner");
                waveSpawner.StartWave();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawns a tutorial visitor at the maze entrance.
        /// </summary>
        public void SpawnTutorialVisitor()
        {
            StartCoroutine(SpawnVisitorCoroutine());
        }

        #endregion

        #region Visitor Spawning

        private IEnumerator SpawnVisitorCoroutine()
        {
            Debug.Log("[TutorialVisitorSpawner] SpawnVisitorCoroutine started");

            // Small delay for visual effect
            yield return new WaitForSecondsRealtime(0.5f);

            // Find spawn position at edge of focused node
            Vector3 spawnPosition = GetSpawnPositionAtFocusedNode();
            Debug.Log($"[TutorialVisitorSpawner] Spawn position determined: {spawnPosition}");

            // Get a random exit as destination
            Vector3 destinationPosition = GetRandomExitPosition(spawnPosition);
            Debug.Log($"[TutorialVisitorSpawner] Destination (random exit): {destinationPosition}");

            // Get visitor prefab from wave spawner or load it
            GameObject visitorPrefab = GetVisitorPrefab();
            if (visitorPrefab == null)
            {
                Debug.LogError("[TutorialVisitorSpawner] Could not find visitor prefab!");
                yield break;
            }
            Debug.Log($"[TutorialVisitorSpawner] Using prefab: {visitorPrefab.name}");

            // Spawn the visitor
            GameObject visitor = Instantiate(visitorPrefab, spawnPosition, Quaternion.identity);
            visitorsSpawned++;
            Debug.Log($"[TutorialVisitorSpawner] Instantiated visitor: {visitor.name}");

            // Initialize visitor
            var controller = visitor.GetComponent<VisitorControllerBase>();
            if (controller != null)
            {
                Debug.Log($"[TutorialVisitorSpawner] Initializing visitor controller: {controller.GetType().Name}");
                // Initialize with GameController (visitor will find maze data internally)
                controller.Initialize();

                // Set original spawn position to prevent retargeting back to where they spawned
                controller.SetOriginalSpawnPosition(spawnPosition);

                // Set their destination to a random exit
                controller.SetWorldDestination(destinationPosition);

                Debug.Log($"[TutorialVisitorSpawner] Visitor initialized. State={controller.State}, Destination={destinationPosition}");
            }
            else
            {
                Debug.LogWarning("[TutorialVisitorSpawner] Visitor has no VisitorControllerBase component!");
            }

            // Notify event triggers
            if (eventTriggers != null)
            {
                eventTriggers.NotifyVisitorSpawned();
            }

            Debug.Log($"[TutorialVisitorSpawner] Spawned tutorial visitor #{visitorsSpawned} at {spawnPosition} -> {destinationPosition}");
        }

        /// <summary>
        /// Gets spawn position at the edge of the node nearest to the camera focal point.
        /// Spawns on the side of the node closest to the camera to ensure visibility.
        /// </summary>
        private Vector3 GetSpawnPositionAtFocusedNode()
        {
            // Get focal point position from camera
            Vector3 focalPos = Vector3.zero;
            if (cameraController != null && cameraController.FocalPointTransform != null)
            {
                focalPos = cameraController.FocalPointPosition;
            }
            else
            {
                Debug.LogWarning("[TutorialVisitorSpawner] No focal point - falling back to heart position");
                focalPos = mazeGrid?.HeartWorldPosition ?? Vector3.zero;
            }

            // Find the node closest to the focal point
            var mapState = mazeGrid?.WorldSpaceMazeData?.GraphState;
            if (mapState == null || mapState.Nodes.Count == 0)
            {
                Debug.LogWarning("[TutorialVisitorSpawner] No map state - using focal position");
                return focalPos;
            }

            float closestDist = float.MaxValue;
            Vector2 closestNodeCenter = Vector2.zero;
            int closestNodeIndex = -1;

            for (int i = 0; i < mapState.Nodes.Count; i++)
            {
                var node = mapState.Nodes[i];
                Vector2 nodePos = node.Position;
                float dist = Vector2.Distance(new Vector2(focalPos.x, focalPos.y), nodePos);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestNodeCenter = nodePos;
                    closestNodeIndex = i;
                }
            }

            if (closestNodeIndex < 0)
            {
                Debug.LogWarning("[TutorialVisitorSpawner] Could not find closest node");
                return focalPos;
            }

            Debug.Log($"[TutorialVisitorSpawner] Found closest node {closestNodeIndex} at {closestNodeCenter}, dist={closestDist}");

            // Find an exit position to determine direction for spawning
            Vector3 exitPos = GetRandomExitPositionInternal(Vector3.zero);
            Vector2 exitPos2D = new Vector2(exitPos.x, exitPos.y);

            // Calculate direction from node center toward exit - we'll spawn visitor facing toward exit
            Vector2 dirToExit = (exitPos2D - closestNodeCenter).normalized;
            if (dirToExit == Vector2.zero)
            {
                // Fallback: random direction
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                dirToExit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            // Spawn at edge of node (NODE_RADIUS from center) in the direction of the exit
            Vector2 spawnPos2D = closestNodeCenter + dirToExit * NODE_RADIUS;

            Debug.Log($"[TutorialVisitorSpawner] Spawning at node edge: {spawnPos2D} (dir to exit: {dirToExit})");
            return new Vector3(spawnPos2D.x, spawnPos2D.y, 0f);
        }

        /// <summary>
        /// Gets a random exit portal position as destination.
        /// Excludes portals too close to the spawn position.
        /// </summary>
        private Vector3 GetRandomExitPosition(Vector3 spawnPosition)
        {
            return GetRandomExitPositionInternal(spawnPosition);
        }

        private Vector3 GetRandomExitPositionInternal(Vector3 excludePosition)
        {
            if (dynamicMaze != null)
            {
                var portalPositions = dynamicMaze.GetPortalPositions();
                if (portalPositions != null && portalPositions.Count > 0)
                {
                    // Collect valid portals (not too close to spawn position)
                    var validPortals = new List<Vector3>();
                    foreach (var portal in portalPositions)
                    {
                        // Skip portals very close to the exclude position
                        if (excludePosition != Vector3.zero)
                        {
                            float dist = Vector3.Distance(portal, excludePosition);
                            if (dist < 5f)
                                continue;
                        }
                        validPortals.Add(portal);
                    }

                    if (validPortals.Count > 0)
                    {
                        // Pick a random valid portal
                        int randomIndex = Random.Range(0, validPortals.Count);
                        return validPortals[randomIndex];
                    }
                    else if (portalPositions.Count > 0)
                    {
                        // All portals were too close, just pick any
                        int randomIndex = Random.Range(0, portalPositions.Count);
                        return portalPositions[randomIndex];
                    }
                }
            }

            // Fallback: use heart position
            Debug.LogWarning("[TutorialVisitorSpawner] No portal positions found, using heart as destination");
            return mazeGrid?.HeartWorldPosition ?? Vector3.zero;
        }

        /// <summary>
        /// Spawns a tutorial visitor at a specific position heading toward a destination.
        /// Used for HeartwardGrasp demonstration where visitor needs to walk through the HGZ.
        /// </summary>
        public void SpawnVisitorForHGZ(Vector3 spawnPosition, Vector3 destinationPosition)
        {
            Debug.Log($"[TutorialVisitorSpawner] SpawnVisitorForHGZ called: spawn={spawnPosition}, dest={destinationPosition}");
            StartCoroutine(SpawnVisitorAtPositionCoroutine(spawnPosition, destinationPosition));
        }

        private IEnumerator SpawnVisitorAtPositionCoroutine(Vector3 spawnPosition, Vector3 destinationPosition)
        {
            // Small delay for visual effect
            yield return new WaitForSecondsRealtime(0.3f);

            GameObject visitorPrefab = GetVisitorPrefab();
            if (visitorPrefab == null)
            {
                Debug.LogError("[TutorialVisitorSpawner] Could not find visitor prefab for HGZ spawn!");
                yield break;
            }

            GameObject visitor = Instantiate(visitorPrefab, spawnPosition, Quaternion.identity);
            visitorsSpawned++;
            Debug.Log($"[TutorialVisitorSpawner] Instantiated HGZ visitor: {visitor.name} at {spawnPosition}");

            var controller = visitor.GetComponent<VisitorControllerBase>();
            if (controller != null)
            {
                controller.Initialize();
                controller.SetOriginalSpawnPosition(spawnPosition);
                controller.SetWorldDestination(destinationPosition);
                Debug.Log($"[TutorialVisitorSpawner] HGZ visitor initialized. State={controller.State}, Destination={destinationPosition}");
            }
            else
            {
                Debug.LogWarning("[TutorialVisitorSpawner] HGZ visitor has no VisitorControllerBase component!");
            }

            if (eventTriggers != null)
            {
                eventTriggers.NotifyVisitorSpawned();
            }

            Debug.Log($"[TutorialVisitorSpawner] Spawned HGZ tutorial visitor #{visitorsSpawned} at {spawnPosition} -> {destinationPosition}");
        }

        private GameObject GetVisitorPrefab()
        {
            // Try to get from WaveSpawner via reflection or serialized field access
            if (waveSpawner != null)
            {
                // Use reflection to access private basicVisitorPrefab field
                var field = typeof(WaveSpawner).GetField("basicVisitorPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    var prefab = field.GetValue(waveSpawner) as VisitorController;
                    if (prefab != null)
                    {
                        return prefab.gameObject;
                    }
                }
            }

            // Try to load from assets
#if UNITY_EDITOR
            var visitor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Visitor.prefab");
            if (visitor != null) return visitor;

            visitor = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Visitor.prefab");
            if (visitor != null) return visitor;
#endif

            // Try Resources.Load as last resort
            var loaded = Resources.Load<GameObject>("Visitor");
            if (loaded != null) return loaded;

            return null;
        }

        #endregion
    }
}

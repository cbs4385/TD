using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FaeMaze.Systems;
using FaeMaze.Visitors;
using FaeMaze.Maze;
using FaeMaze.Cameras;
using FaeMaze.HeartPowers;
using ForestMaze;

namespace FaeMaze.Tutorial
{
    /// <summary>
    /// Handles controlled visitor spawning during the tutorial.
    /// Spawns visitors at specific tutorial steps for guaranteed player interaction.
    /// Visitors spawn at the edge of the focused node and path toward the heart or a random exit.
    /// All random choices use RandomManager for deterministic behavior.
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
                    waveSpawner.ResetWaveState();
                }
            }
        }

        private void OnTutorialCompleted()
        {
            if (waveSpawner != null)
            {
                waveSpawner.StartWave();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Spawns a tutorial visitor at the maze entrance, pathing toward a random exit.
        /// </summary>
        public void SpawnTutorialVisitor()
        {
            StartCoroutine(SpawnVisitorCoroutine(pathTowardHeart: false));
        }

        /// <summary>
        /// Spawns a tutorial visitor that paths toward the heart/seed node.
        /// Use this for power demonstrations where the visitor needs to reach the heart.
        /// </summary>
        public void SpawnTutorialVisitorTowardHeart()
        {
            StartCoroutine(SpawnVisitorCoroutine(pathTowardHeart: true));
        }

        /// <summary>
        /// Spawns a tutorial visitor that will walk through the active Devouring Maw.
        /// Visitor spawns 5 units from the Maw (away from heart) and paths 5 units past it (toward heart).
        /// </summary>
        public void SpawnVisitorThroughMaw()
        {
            StartCoroutine(SpawnVisitorThroughMawCoroutine());
        }

        private IEnumerator SpawnVisitorThroughMawCoroutine()
        {
            // Get the active Maw position
            var heartPowerManager = HeartPowerManager.Instance;
            if (heartPowerManager == null)
            {
                yield break;
            }

            var mawPositions = heartPowerManager.GetActiveDevouringMawPositions();
            if (mawPositions == null || mawPositions.Count == 0)
            {
                SpawnTutorialVisitorTowardHeart();
                yield break;
            }

            Vector3 mawPos = mawPositions[0];
            mawPos.z = 0f;

            // Get heart position to determine direction
            Vector3 heartPos = mazeGrid?.HeartWorldPosition ?? Vector3.zero;
            Vector3 dirToHeart = (heartPos - mawPos).normalized;

            // If direction is zero (Maw is at heart), use a default direction
            if (dirToHeart.sqrMagnitude < 0.01f)
            {
                dirToHeart = Vector3.down; // Default direction in XY plane
            }

            // Calculate ideal spawn position: 5 units from Maw (away from heart)
            Vector3 idealSpawnPos = mawPos - dirToHeart * 5f;
            idealSpawnPos.z = 0f;

            // CRITICAL: Find the nearest walkable tile to the ideal spawn position
            // The calculated position might be off the path (in the forest)
            var mazeData = mazeGrid?.WorldSpaceMazeData;
            if (mazeData == null)
            {
                yield break;
            }

            var nearestWalkableTile = MazePathfinding.FindNearestWalkableTile(
                mazeData, new Vector2(idealSpawnPos.x, idealSpawnPos.y));

            Vector3 spawnPos;
            if (nearestWalkableTile != null)
            {
                spawnPos = new Vector3(nearestWalkableTile.Position.x, nearestWalkableTile.Position.y, 0f);
            }
            else
            {
                // Fallback: use a position closer to the Maw which should be on a path
                spawnPos = mawPos - dirToHeart * 2f;
                spawnPos.z = 0f;
            }

            // Destination is the heart (visitor walks through Maw toward heart)
            // Using heart as destination ensures the visitor has a valid path through the Maw
            Vector3 destPos = heartPos;
            destPos.z = 0f;

            // Use existing spawn method
            SpawnVisitorForHGZ(spawnPos, destPos);
        }

        #endregion

        #region Visitor Spawning

        private IEnumerator SpawnVisitorCoroutine(bool pathTowardHeart = false)
        {
            // Small delay for visual effect
            yield return new WaitForSecondsRealtime(0.5f);

            // Find spawn position at edge of focused node
            Vector3 spawnPosition = GetSpawnPositionAtFocusedNode();

            // Get destination - heart position or random exit based on parameter
            Vector3 destinationPosition;
            if (pathTowardHeart)
            {
                // Path toward heart/seed node for power demonstrations
                destinationPosition = mazeGrid?.HeartWorldPosition ?? Vector3.zero;
            }
            else
            {
                destinationPosition = GetRandomExitPosition(spawnPosition);
            }

            // Get visitor prefab from wave spawner or load it
            GameObject visitorPrefab = GetVisitorPrefab();
            if (visitorPrefab == null)
            {
                yield break;
            }
            // Spawn the visitor
            GameObject visitor = Instantiate(visitorPrefab, spawnPosition, Quaternion.identity);
            visitorsSpawned++;

            // Initialize visitor
            var controller = visitor.GetComponent<VisitorControllerBase>();
            if (controller != null)
            {
                // Initialize with GameController (visitor will find maze data internally)
                controller.Initialize();

                // Mark as tutorial visitor - immune to being frightened and dazed
                controller.SetTutorialVisitor(true);

                // Power demo visitors are immune to fascination so they reach their targets
                controller.SetFascinationImmune(true);

                // Set original spawn position to prevent retargeting back to where they spawned
                controller.SetOriginalSpawnPosition(spawnPosition);

                // Set their destination
                controller.SetWorldDestination(destinationPosition);

                // If pathing toward heart, mark as lured so they path into the detection zone
                if (pathTowardHeart)
                {
                    controller.SetLured(true);
                }
            }

            // Notify event triggers
            if (eventTriggers != null)
            {
                eventTriggers.NotifyVisitorSpawned();
            }

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
                focalPos = mazeGrid?.HeartWorldPosition ?? Vector3.zero;
            }

            // Find the node closest to the focal point
            var mapState = mazeGrid?.WorldSpaceMazeData?.GraphState;
            if (mapState == null || mapState.Nodes.Count == 0)
            {
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
                return focalPos;
            }

            // Find an exit position to determine direction for spawning
            Vector3 exitPos = GetRandomExitPositionInternal(Vector3.zero);
            Vector2 exitPos2D = new Vector2(exitPos.x, exitPos.y);

            // Calculate direction from node center toward exit - we'll spawn visitor facing toward exit
            Vector2 dirToExit = (exitPos2D - closestNodeCenter).normalized;
            if (dirToExit == Vector2.zero)
            {
                // Fallback: deterministic random direction using seeded RandomManager
                float angle = RandomManager.Range(0f, 360f) * Mathf.Deg2Rad;
                dirToExit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            // Spawn at edge of node (NODE_RADIUS from center) in the direction of the exit
            Vector2 spawnPos2D = closestNodeCenter + dirToExit * NODE_RADIUS;

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
                        // Pick a deterministic random valid portal using seeded RandomManager
                        int randomIndex = RandomManager.Range(0, validPortals.Count);
                        return validPortals[randomIndex];
                    }
                    else if (portalPositions.Count > 0)
                    {
                        // All portals were too close, just pick any (deterministic)
                        int randomIndex = RandomManager.Range(0, portalPositions.Count);
                        return portalPositions[randomIndex];
                    }
                }
            }

            return mazeGrid?.HeartWorldPosition ?? Vector3.zero;
        }

        /// <summary>
        /// Spawns a tutorial visitor at a specific position heading toward a destination.
        /// Used for HeartwardGrasp demonstration where visitor needs to walk through the HGZ.
        /// </summary>
        public void SpawnVisitorForHGZ(Vector3 spawnPosition, Vector3 destinationPosition, bool fascinationImmune = true)
        {
            StartCoroutine(SpawnVisitorAtPositionCoroutine(spawnPosition, destinationPosition, fascinationImmune));
        }

        private IEnumerator SpawnVisitorAtPositionCoroutine(Vector3 spawnPosition, Vector3 destinationPosition, bool fascinationImmune = true)
        {
            // Small delay for visual effect
            yield return new WaitForSecondsRealtime(0.3f);

            GameObject visitorPrefab = GetVisitorPrefab();
            if (visitorPrefab == null)
            {
                yield break;
            }

            GameObject visitor = Instantiate(visitorPrefab, spawnPosition, Quaternion.identity);
            visitorsSpawned++;

            var controller = visitor.GetComponent<VisitorControllerBase>();
            if (controller != null)
            {
                controller.Initialize();

                // Mark as tutorial visitor - immune to being frightened and dazed
                controller.SetTutorialVisitor(true);

                // Set fascination immunity - power demo visitors should ignore lanterns,
                // but lantern demo visitors need to be fascinated
                controller.SetFascinationImmune(fascinationImmune);

                controller.SetOriginalSpawnPosition(spawnPosition);
                controller.SetWorldDestination(destinationPosition);

                // Set initial facing direction toward destination so visitor doesn't start facing backwards
                Vector2 facingDir = new Vector2(
                    destinationPosition.x - spawnPosition.x,
                    destinationPosition.y - spawnPosition.y);
                controller.SetFacingDirectionImmediate(facingDir);
            }

            if (eventTriggers != null)
            {
                eventTriggers.NotifyVisitorSpawned();
            }

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

            // Try to load from Resources
            var loaded = Resources.Load<GameObject>("Prefabs/Visitors/Visitor_FestivalTourist");
            if (loaded != null) return loaded;

            return null;
        }

        #endregion
    }
}
